using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Discord.Commands;
using Discord.WebSocket;
using ikanopu.Config;
using ikanopu.Core;
using ikanopu.Service;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using OpenCvSharp;

namespace ikanopu {
    public class Program {
        public static readonly string CONFIG_PATH = "config.json";
        public static readonly string SECRET_PATH = "secret.json";

        #region discord
        private static DiscordSocketClient discord;
        private static CommandService commands;
        private static IServiceProvider services;
        #endregion

        [STAThread]
        static async Task Main(string[] args) {
            // 設定読み込み
            using (var config = (File.Exists(CONFIG_PATH)) ? JsonConvert.DeserializeObject<GlobalConfig>(File.ReadAllText(CONFIG_PATH)) : new GlobalConfig()) {
                var secret = (File.Exists(SECRET_PATH)) ? JsonConvert.DeserializeObject<SecretConfig>(File.ReadAllText(SECRET_PATH)) : new SecretConfig();

                #region 前処理

                CheckDiscordTokenSecret(secret);
                CheckRegisterUserImage(config);
                
                #endregion

                #region Setup Discord

                // socket clientの初期化
                discord = new DiscordSocketClient();
                discord.MessageReceived += Client_MessageReceived;
                discord.Log += Client_Log;

                // サービスのDI
                services =
                    new ServiceCollection()
                    .AddSingleton<DiscordSocketClient>()
                    .AddSingleton<ImageProcessingService>()
                    .BuildServiceProvider();

                // 本プロジェクトにあるコマンドを全部ロード
                commands = new CommandService();
                await commands.AddModulesAsync(Assembly.GetEntryAssembly(), services);

                //ログインして通信開始
                await discord.LoginAsync(Discord.TokenType.Bot, secret.DiscordToken);
                await discord.StartAsync();

                #endregion

                #region 画像処理
                await services.GetRequiredService<ImageProcessingService>().InitializeAsync(config);
                // メインスレッドで画像を表示してあげるしかない
                using (Window captureRawWindow = new Window("Capture Raw")) {
                    var cancelTokenSource = new CancellationTokenSource();
#pragma warning disable CS4014
                    services.GetRequiredService<ImageProcessingService>().CaptureAsync(cancelTokenSource.Token);
#pragma warning restore CS4014
                    while (Cv2.WaitKey(config.CaptureDelayMs) != config.CaptureBreakKey) {
                        captureRawWindow.ShowImage(services.GetRequiredService<ImageProcessingService>().CaptureRawMat);
                    }
                    cancelTokenSource.Cancel();
                }
                #endregion

                #region Finalize
                // 終了時にコンフィグを書き直してあげる（バージョンが変わっていたときなど、あるじゃん)
                config.UpdatedAt = DateTime.Now;
                secret.UpdatedAt = DateTime.Now;
                File.WriteAllText(CONFIG_PATH, JsonConvert.SerializeObject(config, Formatting.Indented));
                File.WriteAllText(SECRET_PATH, JsonConvert.SerializeObject(secret, Formatting.Indented));
                #endregion
            }
        }

        private static void CheckRegisterUserImage(GlobalConfig config) {
            if (!config.RegisterUsers.Any(x => File.Exists(x.ImagePath))) {
                Console.WriteLine("Error: Registered Userの画像が参照できませんでした");
                Environment.Exit(1);
            }
        }

        private static void CheckDiscordTokenSecret(SecretConfig secret) {
            if (string.IsNullOrWhiteSpace(secret.DiscordToken)) {
                Console.WriteLine("DiscordのBot用のトークンを入力してください(secret.jsonに保存されるだけなので心配しないで");
                Console.Write(">");
                secret.DiscordToken = Console.ReadLine();
                secret.UpdatedAt = DateTime.Now;
                File.WriteAllText(SECRET_PATH, JsonConvert.SerializeObject(secret, Formatting.Indented));
            }
        }

        private static Task Client_Log(Discord.LogMessage message) {
            Console.WriteLine(message.ToString());
            return Task.CompletedTask;
        }

        private static async Task Client_MessageReceived(SocketMessage socketMessage) {
            var message = socketMessage as SocketUserMessage;
            Console.WriteLine($"#{message.Channel.Name}: @{message.Author.Username}: {message}");

            int argPos = 0;
            if (message.Author.IsBot) return;
            if (!message.HasCharPrefix('!', ref argPos) && !message.HasMentionPrefix(discord.CurrentUser, ref argPos)) {
                return;
            }
            // コマンド実行してあげる
            var context = new CommandContext(discord, message);
            var result = await commands.ExecuteAsync(context, argPos, services);
            // ダメ
            if (!result.IsSuccess) {
                Console.WriteLine(result.ErrorReason);
                return;
            }

        }

        /// <summary>
        /// TODO: ImageProcessingServiceに移動
        /// </summary>
        /// <param name="config"></param>
        /// <param name="secret"></param>
        private static void ProcessImage(GlobalConfig config, SecretConfig secret) {

            // 毎回計算するのも面倒なので、座標は確定しておく
            var cropPositions =
                config.CropOptions
                      .Select(x => x.CropPosition.ToArray())
                      .ToArray();

            // 画像処理メインらへん
            using (var capture = new VideoCapture(CaptureDevice.Any, config.CameraIndex) {
                FrameWidth = config.CaptureWidth,
                FrameHeight = config.CaptureHeight,
            }) {
                #region 前準備
                var win = new Window("capture raw");
                var captureMat = new Mat(config.CaptureHeight, config.CaptureWidth, MatType.CV_8UC3);
                var matcher = new BFMatcher();
                // ゴミが入っているので最初に読んでおく
                capture.Read(captureMat);

                #endregion

                // ループとか
                while (Cv2.WaitKey(config.CaptureDelayMs) != config.CaptureBreakKey) {
                    // 現在の画面を取得
                    capture.Read(captureMat);

                    // 設定された領域を両方解析して、精度の高い方を使う。もしくはマッチしなかったという結果を返す
                    // 観戦者の有無で領域が変わるので一応両方試してあげる
                    var results = cropPositions.Select(cropPosition => {
                        #region 画像処理するところ

                        var isInvalid = false;
                        var invalidMessage = "";

                        #region 前処理: 名前ごとに分解
                        var cropMats = captureMat.CropNames(cropPosition).ToArray();
                        var postMats = cropMats.RemoveBackground().ToArray();
                        #endregion

                        #region  抽出した画像の特徴量を計算
                        var computeDatas = postMats.Select(m => {
                            var descriptor = new Mat();
                            m.Item2.Compute(out var kp, descriptor);
                            return new {
                                Team = m.Item1,
                                KeyPoints = kp,
                                Descriptor = descriptor,
                                Image = m.Item2,
                            };
                        }).ToArray();
                        // 保存されてるやつとテンプレートマッチングする
                        var matchResults =
                            config.RegisterUsers
                                  .Select(user => {
                                      var (kp, d) = user.ComputeData;
                                      return (user, computeDatas.Select(data => {
                                          var matches = data.KeyPoints.Length == 0 ? new DMatch[] { } : matcher.Match(d, data.Descriptor).ToArray();
                                          // 同じ場所切り取ってるしdistanceの総和でも見とけばいいでしょ TODO: #ちゃんと検証しろ
                                          var score = matches.Length == 0 ? 0 : matches.Average(m => m.Distance);
                                          return new {
                                              // 元データ
                                              Team = data.Team,
                                              Image = data.Image,
                                              KeyPoints = data.KeyPoints,
                                              // 計算後のデータ
                                              Matches = matches, // 一致した点の数。これは多いほうが良い
                                              Score = score, // 小さいほどよい。KeyPointsがなければそもそも0になるのでそこだけ注意
                                          };
                                      })
                                        .ToArray()
                                      );
                                  }).ToArray();
                        #endregion

                        #region for Debug: マッチの結果を全部画像に出力に書く
                        // デバッグ 
                        ////foreach (var (user, datas) in matchResults) {
                        ////    var imgs =
                        ////        datas.Select(data => {
                        ////            Mat img = new Mat();
                        ////            Cv2.DrawMatches(user.PreLoadImage, user.ComputeKeyPoints, data.Image, data.KeyPoints, data.Matches, img);
                        ////            return img;
                        ////        });
                        ////    var names = datas.Select((data, i) => $"{user.DisplayName}[{i}], {data.Score}");
                        //    //foreach (var n in names) {
                        //    //    Console.WriteLine(n);
                        //    //}
                        //    //Window.ShowImages(imgs, names);
                        //}
                        #endregion

                        #region 一致するユーザを判定

                        var recognizedUsers =
                            matchResults.Select(r => {
                                var user = r.user;
                                var datas = r.Item2;
                                // まず8箇所あるよね
                                if (datas.Length < 8) return null;
                                // alphaとbravoの1人目は取得できてるよね
                                if (datas[0].Matches.Length == 0 || datas[4].Matches.Length == 0) return null;
                                // alpha0~3, bravo0~3の途中にZeroが挟まっていた場合→プラベの画面ではない可能性が高い
                                // ex) 100, 0(2人目だけマッチングが欠落することはありえない), 200, 300
                                for (int i = 0; i < 2; ++i) {
                                    // alpha
                                    if (datas[1 + i].Matches.Length == 0 && datas[2 + i].Matches.Length > 0) return null;
                                    // bravo
                                    if (datas[4 + i].Matches.Length == 0 && datas[5 + i].Matches.Length > 0) return null;
                                }
                                // 0を除外した値で平均と偏差を計算
                                // 一致画像は平均値-2sigmaを推移するため、これを満たす画像が1枚だけ見つかるときは真
                                var src =
                                datas.Select((x, i) => new { Index = i, Value = x })
                                     .Where(x => x.Value.Matches.Length > 0)
                                     .ToArray();
                                var sum = src.Sum(x => x.Value.Score);
                                var average = sum / (double)src.Length;
                                var sigma = Math.Sqrt(src.Sum(x => Math.Pow(x.Value.Score - average, 2)) / src.Length);
                                var threash = average - sigma * config.RecognizeSigmaRatio; // 一応sigmaユーザーが指定できる
                                                                                            // threashを下回ったもののみ抽出
                                var detects =
                                    src.Where(x => x.Value.Score < threash)
                                       .OrderBy(x => x.Value.Score)
                                       .ToArray();
                                //検出できなかった
                                if (detects.Length == 0) return null;
                                // 複数ある場合
                                var multipleDetect = detects.Length > 1;
                                if (multipleDetect && !config.IsPrioritizeDetect) return null;
                                // こいつが正解
                                var detect = detects.First();
                                // 完了
                                return new {
                                    User = user,
                                    Index = detect.Index,
                                    Team = datas[detect.Index].Team,
                                    Data = datas[detect.Index],
                                    // あとで重複が発生したときのための評価要素も残しておく
                                    Datas = datas,
                                    Detects = detects, // 検出したやつ
                                    Independency = (average - detect.Value.Score) / sigma, // スコアが平均値からどの程度遠ざかっているか
                                    IsMultipleDetect = multipleDetect, // 似たような値が他にあった場合
                                };
                            })
                            .Where(x => x != null)
                            .ToArray();
                        #endregion

                        #region 評価しておく
                        if (recognizedUsers.Length == 0) {
                            invalidMessage = "名前を認識できませんでした";
                            isInvalid = true;
                        }
                        // 複数のプレイヤーが同じ場所を見ていた場合の修正
                        // 独立性が高く、複数検出されなかったものに優先度を置く
                        var filteredUsers =
                            recognizedUsers.GroupBy(x => x.Index)
                                           .Select(y =>
                                                y.OrderByDescending(x => x.Independency + (x.IsMultipleDetect ? -100 : 0)).First()
                                           )
                                           .ToList();
                        // ボツになったやつも第二候補に動かしてあげる
                        if (config.IsPrioritizeDetect) {
                            var diffUsers =
                                recognizedUsers.Where(x => !filteredUsers.Contains(x)) // 現在ない中で
                                               .Where(x => x.IsMultipleDetect) // 複数個選択されていて
                                               .Where(x => filteredUsers.FirstOrDefault(y => y.Index == x.Detects[1].Index) == null) // 2候補のIndexが現在のものと重複していない場合
                                               .Select(x => {
                                                   var detect = x.Detects[1];
                                                   return new {
                                                       User = x.User,
                                                       Index = detect.Index,
                                                       Team = x.Datas[detect.Index].Team,
                                                       Data = x.Data,
                                                       //
                                                       Datas = x.Datas,
                                                       Detects = x.Detects.Skip(1).ToArray(),
                                                       Independency = x.Independency, // TODO:
                                                       IsMultipleDetect = true,
                                                   };
                                               }).ToArray();
                            filteredUsers.AddRange(diffUsers);
                        }
                        if (recognizedUsers.Select(x => x.Index).Distinct().Count() != recognizedUsers.Length) {
                            invalidMessage = "複数プレイヤが同じ箇所を誤って認識している可能性があります。";
                            isInvalid = true;
                        }
                        #endregion

                        // cropOptionが複数あるのでとりあえず後で使うデータは返しておく
                        return new {
                            IsInvalid = isInvalid,
                            InvalidMessage = invalidMessage,
                            CropMats = cropMats,
                            PostMats = postMats,
                            RecognizedUsers = filteredUsers.ToArray(),
                            CropPosition = cropPosition,
                        };
                        #endregion
                    })
                    .Where(x => x != null)
                    .ToArray();

                    // いい方を使ってあげる
                    //var result = results[1];
                    var result =
                        results//.Where(x => !x.IsInvalid && x.RecognizedUsers.Length > 0)
                               .OrderByDescending(x => x.RecognizedUsers?.Length ?? 0)
                               .FirstOrDefault();

                    if (result != null) {
                        // デバッグ用に画像の保存
                        if (config.IsSaveDebugImage) {
                            result.CropMats.Select(x => x.Item2).SaveAll("origin");
                            result.PostMats.Select(x => x.Item2).SaveAll("post");

                            #region for Debug: 登録ユーザのダミー作成
                            //config.RegisterUsers.Add(new RegisterUser(config.RegisterImageDirectory, postMats[1].Item2) { DisplayName = "ふみふみ" });
                            //config.RegisterUsers.Add(new RegisterUser(config.RegisterImageDirectory, postMats[3].Item2) { DisplayName = "あのさん" });
                            //config.RegisterUsers.Add(new RegisterUser(config.RegisterImageDirectory, postMats[5].Item2) { DisplayName = "ゆきだるまテレビ" });
                            #endregion
                        }
                        // プレビュー画像に書き込み
                        captureMat.DrawCropPreview(result.CropPosition, result.RecognizedUsers.Select(x => (x.Index, $"{x.User.DisplayName}")));

                    }
                    // Timestamp書いとく
                    captureMat.PutText($"{DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss.fff")}", new Point(0, 32), HersheyFonts.HersheyComplex, 1, Scalar.White, 1, LineTypes.AntiAlias, false);
                    win.ShowImage(captureMat);
                }
            }
        }
    }
}

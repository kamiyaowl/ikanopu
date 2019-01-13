using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ikanopu.Config;
using ikanopu.Core;
using Newtonsoft.Json;
using OpenCvSharp;

namespace ikanopu {
    class Program {
        public static readonly string CONFIG_PATH = "config.json";
        public static readonly string SECRET_PATH = "secret.json";

        [STAThread]
        static void Main(string[] args) {
            // 設定読み込み
            using (var config = (File.Exists(CONFIG_PATH)) ? JsonConvert.DeserializeObject<GlobalConfig>(File.ReadAllText(CONFIG_PATH)) : new GlobalConfig()) {
                var secret = (File.Exists(SECRET_PATH)) ? JsonConvert.DeserializeObject<SecretConfig>(File.ReadAllText(SECRET_PATH)) : new SecretConfig();

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

                            #region 前処理: 名前ごとに分解
                            var cropMats = captureMat.CropNames(cropPosition).ToArray();
                            var teams = cropMats.Select(x => x.Item1).ToArray();
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
                                              var matches = matcher.Match(d, data.Descriptor).ToArray();
                                              // 同じ場所切り取ってるしdistanceの総和でも見とけばいいでしょ TODO: #ちゃんと検証しろ
                                              var score = matches.Sum(m => m.Distance);
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
                            //foreach (var (user, datas) in matchResults) {
                            //    var imgs =
                            //        datas.Select(data => {
                            //            Mat img = new Mat();
                            //            Cv2.DrawMatches(user.PreLoadImage, user.ComputeKeyPoints, data.Image, data.KeyPoints, data.Matches, img);
                            //            return img;
                            //        });
                            //    var names = datas.Select((data, i) => $"{user.DisplayName}[{i}], {data.Score}");
                            //    foreach (var n in names) {
                            //        Console.WriteLine(n);
                            //    }
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
                                    var sum = datas.Sum(x => x.Score);
                                    var average = sum / (double)datas.Length;
                                    var sigma = Math.Sqrt(src.Sum(x => Math.Pow(x.Value.Score - average, 2)) / src.Length);
                                    var threash = average - sigma * config.RecognizeSigmaRatio; // 一応sigmaユーザーが指定できる
                                    // threashを下回ったもののみ抽出
                                    var detects = src.Where(x => x.Value.Score < threash).ToArray();
                                    if (detects.Length == 0 || detects.Length > 1) return null; // ないか、複数出てくる場合は検出失敗
                                    // こいつが正解
                                    var detect = detects.First();
                                    // 完了
                                    return new { User = user, Index = detect.Index, Team = datas[detect.Index].Team, Data = datas[detect.Index] };
                                })
                                .Where(x => x != null)
                                .ToArray();
                            #endregion

                            #region 評価しておく
                            if (recognizedUsers.Length == 0) {
                                Console.WriteLine("名前を認識できませんでした");
                                isInvalid = true;
                            }
                            if (recognizedUsers.Select(x => x.Index).Distinct().Count() != recognizedUsers.Length) {
                                Console.WriteLine("複数プレイヤが同じ箇所を誤って認識している可能性があります。");
                                isInvalid = true;
                            }

                            // cropOptionが複数あるのでとりあえず後で使うデータは返しておく
                            return new { IsInvalid = isInvalid, CropMats = cropMats, PostMats = postMats, RecognizedUsers = recognizedUsers };
                            #endregion

                            #endregion
                        })
                        .Where(x => x != null)
                        .ToArray();

                        // いい方を使ってあげる
                        var result =
                            results.Where(x => !x.IsInvalid && x.RecognizedUsers.Length > 0)
                                   .OrderByDescending(x => x.RecognizedUsers)
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
                            captureMat.DrawCropPreview(cropPositions[0], result.RecognizedUsers.Select(x => (x.Index, $"{x.User.DisplayName}")));
                        }
                        win.ShowImage(captureMat);

                        Console.WriteLine($"Ticks: {DateTime.Now.Ticks}");
                    }
                }

                // 終了時にコンフィグを書き直してあげる（バージョンが変わっていたときなど、あるじゃん)
                config.UpdatedAt = DateTime.Now;
                secret.UpdatedAt = DateTime.Now;
                File.WriteAllText(CONFIG_PATH, JsonConvert.SerializeObject(config, Formatting.Indented));
                File.WriteAllText(SECRET_PATH, JsonConvert.SerializeObject(secret, Formatting.Indented));
            }
        }

    }
}

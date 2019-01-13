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
                    // 前準備
                    var win = new Window("capture raw");
                    var captureMat = new Mat(config.CaptureHeight, config.CaptureWidth, MatType.CV_8UC3);
                    var matcher = new BFMatcher();
                    // ループとか
                    while (Cv2.WaitKey(config.CaptureDelayMs) != config.CaptureBreakKey) {
                        capture.Read(captureMat);
                        //TODO: 設定された領域を両方解析して、精度の高い方を使う。もしくはマッチしなかったという結果を返す

                        // 名前ごとに分解して前処理してあげる
                        var cropMats = captureMat.CropNames(cropPositions[0]).ToArray();
                        var teams = cropMats.Select(x => x.Item1).ToArray();
                        var postMats = cropMats.RemoveBackground().ToArray();
                        // 抽出した画像の特徴量を計算
                        var computeDatas = postMats.Select(m => {
                            var engine = BRISK.Create();
                            var descriptor = new Mat();
                            engine.DetectAndCompute(m.Item2, null, out var kp, descriptor);
                            return new {
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
                                              Image = data.Image,
                                              KeyPoints = data.KeyPoints,
                                              Matches = matches,
                                              Score = score,
                                          };
                                      }));
                                  }).ToArray();
                        // デバッグ マッチの結果を全部書く
                        foreach (var (user, datas) in matchResults) {
                            var imgs =
                                datas.Select(data => {
                                    Mat img = new Mat();
                                    Cv2.DrawMatches(user.PreLoadImage, user.ComputeData.Item1, data.Image, data.KeyPoints, data.Matches, img);
                                    return img;
                                });
                            var names = datas.Select((data, i) => $"{user.DisplayName}-[{i}] : {data.Score}");
                            Window.ShowImages(imgs, names);
                        }


                        // デバッグ用に画像の保存
                        if (config.IsSaveDebugImage) {
                            cropMats.Select(x => x.Item2).SaveAll("origin");
                            postMats.Select(x => x.Item2).SaveAll("post");

                            // test
                            //config.RegisterUsers.Add(new RegisterUser(config.RegisterImageDirectory, postMats[1].Item2) { DisplayName = "ふみふみ" });
                            //config.RegisterUsers.Add(new RegisterUser(config.RegisterImageDirectory, postMats[3].Item2) { DisplayName = "あのさん" });
                            //config.RegisterUsers.Add(new RegisterUser(config.RegisterImageDirectory, postMats[5].Item2) { DisplayName = "ゆきだるまテレビ" });
                        }
                        captureMat.DrawCropPreview(cropPositions[0]);
                        win.ShowImage(captureMat);
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

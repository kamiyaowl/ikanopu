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
            var config = (File.Exists(CONFIG_PATH)) ? JsonConvert.DeserializeObject<GlobalConfig>(File.ReadAllText(CONFIG_PATH)) : new GlobalConfig();
            var secret = (File.Exists(SECRET_PATH)) ? JsonConvert.DeserializeObject<SecretConfig>(File.ReadAllText(SECRET_PATH)) : new SecretConfig();

            // 毎回計算するのも面倒なので、座標は確定しておく
            var cropPositions =
                config.CropOptions
                      .Select(x => x.CropPosition.ToArray())
                      .ToArray();
            // 画像処理メインらへん
            using (var capture = new VideoCapture(CaptureDevice.Any, config.CameraIndex)) {
                capture.FrameWidth = config.CaptureWidth;
                capture.FrameHeight = config.CaptureHeight;
                var win = new Window("capture raw");

                var mat = new Mat(config.CaptureHeight, config.CaptureWidth, MatType.CV_8UC3);
                while (Cv2.WaitKey(1) == -1) { // TODO: shutdownほうほうはもっとまともに
                    capture.Read(mat);
                    //TODO: 設定された領域を両方解析して、精度の高い方を使う。もしくはマッチしなかったという結果を返す

                    // 名前ごとに分解して前処理してあげる
                    var cropMats = mat.CropNames(cropPositions[0]).ToArray();
                    var teams = cropMats.Select(x => x.Item1).ToArray();
                    var postMats = cropMats.RemoveBackground().ToArray();
                    // 保存されてるやつとテンプレートマッチングする
                    if (config.IsSaveDebugImage) {
                        cropMats.Select(x => x.Item2).SaveAll("origin");
                        postMats.Select(x => x.Item2).SaveAll("post");

                        // test
                        //config.RegisterUsers.Add(new RegisterUser(config.RegisterImageDirectory, postMats[1].Item2) { DisplayName = "ふみふみ" });
                        //config.RegisterUsers.Add(new RegisterUser(config.RegisterImageDirectory, postMats[3].Item2) { DisplayName = "あのさん" });
                        //config.RegisterUsers.Add(new RegisterUser(config.RegisterImageDirectory, postMats[5].Item2) { DisplayName = "ゆきだるまテレビ" });
                    }
                    mat.DrawCropPreview(cropPositions[0]);
                    win.ShowImage(mat);
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

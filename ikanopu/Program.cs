using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ikanopu.Config;
using ikanopu.Core;
using OpenCvSharp;

namespace ikanopu {
    class Program {
        [STAThread]
        static void Main(string[] args) {
            // TODO: 設定ファイルからのロード
            var config = new GlobalConfig();
            var secret = new SecretConfig();

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
                    // 
                    var cropMats = mat.CropNames(cropPositions[0]).ToArray();
                    var teams = cropMats.Select(x => x.Item1).ToArray();
                    var postMats =
                        cropMats.Select(x => x.Item2.CvtColor(ColorConversionCodes.RGB2GRAY))
                                .Select(x => x.Threshold(200, 255, ThresholdTypes.Binary))
                                .ToArray();

                    if (config.IsSaveDebugImage) {
                        cropMats.Select(x => x.Item2).SaveAll("origin");
                        postMats.SaveAll("post");
                    }
                    mat.DrawCropPreview(cropPositions[0]);
                    win.ShowImage(mat);
                }
            }
        }


    }
}

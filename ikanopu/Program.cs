using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ikanopu.Core;
using OpenCvSharp;

namespace ikanopu {
    class Program {

        static void Main(string[] args) {
            var cameraIndex = 0;
            var width = 1920;
            var height = 1080;
            // TODO: 観戦者有無で座標が変わるが総当たりするしかないっぽい、現物合わせ
            var cropTargets = new[]{
                CropOption.Generate(),
                CropOption.GenerateWithWatcher(),
                };
            // 毎回計算するのも面倒なので、座標は確定しておく
            var cropPositions =
                cropTargets.Select(x => x.CropPosition.ToArray())
                           .ToArray();

            using (var capture = new VideoCapture(CaptureDevice.Any, cameraIndex)) {
                capture.FrameWidth = width;
                capture.FrameHeight = height;
                var win = new Window("capture raw");

                var mat = new Mat(height, width, MatType.CV_8UC3);
                while (Cv2.WaitKey(1) == -1) {
                    capture.Read(mat);
                    mat.DrawCropPreview(cropPositions[0]);

                    win.ShowImage(mat);
                }
            }
        }
    }
}

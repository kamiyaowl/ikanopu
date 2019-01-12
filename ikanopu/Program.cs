using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenCvSharp;

namespace ikanopu {
    class Program {
        static void Main(string[] args) {
            var cameraIndex = 0;
            var width = 1920;
            var height = 1080;

            using (var capture = new VideoCapture(CaptureDevice.Any, cameraIndex)) {
                capture.FrameWidth = width;
                capture.FrameHeight = height;
                var win = new Window("capture raw");

                var mat = new Mat(height, width, MatType.CV_8UC3);
                while (Cv2.WaitKey(1) == -1) {
                    capture.Read(mat);
                    win.ShowImage(mat);
                }
            }
        }
    }
}

using OpenCvSharp;
using System;

namespace ikanopu_dotnet {
    class Program {
        static void Main(string[] args) {
            using (var mat = new Mat(1080, 1920, MatType.CV_8UC3))
            using (var captureRawWindow = new Window("Capture Raw", WindowMode.KeepRatio))
            using (var capture = new VideoCapture(0) { }) {
                capture.FrameWidth = 1920;
                capture.FrameHeight = 1080;

                while (Cv2.WaitKey(1) == -1) {
                    capture.Read(mat);
                    captureRawWindow.ShowImage(mat);
                }
            }
        }
    }
}

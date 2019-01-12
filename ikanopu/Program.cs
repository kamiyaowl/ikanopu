using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenCvSharp;

namespace ikanopu {
    class CropOption {
        /// <summary>
        /// 名前が書いてあるところの幅
        /// </summary>
        public int BoxWidth { get; set; }
        /// <summary>
        /// 名前が書いてあるところの高さ
        /// </summary>
        public int BoxHeight { get; set; }
        /// <summary>
        /// 名前同士の間隔(Center to Centerで)
        /// </summary>
        public int MarginHeight { get; set; }
        /// <summary>
        /// 名前が書いてあるところの中心X座標
        /// </summary>
        public int BaseX { get; set; }
        /// <summary>
        /// Alphaチーム1人目のY座標
        /// </summary>
        public int AlphaY { get; set; }
        /// <summary>
        /// Bravoチーム1人目のY座標
        /// </summary>
        public int BravoY { get; set; }
        /// <summary>
        /// 観戦者1人目のY座標、nullの場合観戦者なし
        /// </summary>
        public int? WatcherY { get; set; }
        /// <summary>
        /// 参加プレイヤー数
        /// </summary>
        public int PlayerCount { get; set; }

        #region Preset Generate
        public static CropOption Generate8Player() {
            return new CropOption() {
                BoxWidth = 400,
                BoxHeight = 60,
                MarginHeight = 80,
                BaseX = 1420,
                AlphaY = 235,
                BravoY = 615,
                WatcherY = null,
                PlayerCount = 8,
            };
        }

        public static CropOption GenerateWith9Player() {
            throw new NotImplementedException();
        }
        public static CropOption GenerateWith10Player() {
            throw new NotImplementedException();
        }
        #endregion

        /// <summary>
        /// Optionの内容から名前クリップする座標を一式返します
        /// </summary>
        /// <returns></returns>
        public IEnumerable<Rect2d> GenerateCropPosition() {
            // alpha
            for (int i = 0; i < 4; ++i) {
                yield return new Rect2d(BaseX, AlphaY + i * MarginHeight, BoxWidth, MarginHeight);
            }
            // bravo
            for (int i = 0; i < 4; ++i) {
                yield return new Rect2d(BaseX, BravoY + i * MarginHeight, BoxWidth, BoxHeight);
            }
            // watcher
            if (PlayerCount > 8 && WatcherY.HasValue) {
                yield return new Rect2d(BaseX, WatcherY.Value, BoxWidth, BoxHeight);
                yield return new Rect2d(BaseX, WatcherY.Value + MarginHeight, BoxWidth, BoxHeight);
            }
        }
    }
    class Program {

        static void Main(string[] args) {
            var cameraIndex = 0;
            var cropTarget = CropOption.Generate8Player(); // TODO: 10人のときもやってね
            var cropPosition = cropTarget.GenerateCropPosition().ToArray();
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
                mat.SaveImage("pu.bmp");
            }
        }
    }
}

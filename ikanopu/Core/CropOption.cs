using Newtonsoft.Json;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ikanopu.Core {
    class CropOption {
        public enum Team {
            Alpha, Bravo, Watcher, None
        }
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
        public static CropOption Generate() =>
            new CropOption() {
                BoxWidth = 400,
                BoxHeight = 50,
                MarginHeight = 75,
                BaseX = 1450,
                AlphaY = 240,
                BravoY = 615,
                WatcherY = null,
                PlayerCount = 8,
            };

        public static CropOption GenerateWithWatcher() =>
            new CropOption() {
                BoxWidth = 400,
                BoxHeight = 50,
                MarginHeight = 75,
                BaseX = 1450,
                AlphaY = 140,
                BravoY = 515,
                WatcherY = 910,
                PlayerCount = 10,
            };
        #endregion

        /// <summary>
        /// Optionの内容から名前クリップする座標を一式返します
        /// </summary>
        /// <returns></returns>
        [JsonIgnore]
        public IEnumerable<(Team t, Rect r)> CropPosition {
            get {
                // alpha
                for (int i = 0; i < 4; ++i) {
                    yield return (Team.Alpha, new Rect(BaseX - BoxWidth / 2, AlphaY + i * MarginHeight - BoxHeight / 2, BoxWidth, BoxHeight));
                }
                // bravo
                for (int i = 0; i < 4; ++i) {
                    yield return (Team.Bravo, new Rect(BaseX - BoxWidth / 2, BravoY + i * MarginHeight - BoxHeight / 2, BoxWidth, BoxHeight));
                }
                // watcher
                if (PlayerCount > 8 && WatcherY.HasValue) {
                    yield return (Team.Alpha, new Rect(BaseX - BoxWidth / 2, WatcherY.Value + 0 * MarginHeight - BoxHeight / 2, BoxWidth, BoxHeight));
                    yield return (Team.Alpha, new Rect(BaseX - BoxWidth / 2, WatcherY.Value + 1 * MarginHeight - BoxHeight / 2, BoxWidth, BoxHeight));
                }
            }
        }
    }
}

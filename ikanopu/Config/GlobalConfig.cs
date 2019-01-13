using ikanopu.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ikanopu.Config {
    class GlobalConfig {
        /// <summary>
        /// キャプチャデバイスのインデックス
        /// </summary>
        public int CameraIndex { get; set; } = 0;
        /// <summary>
        /// 取得画像の幅
        /// </summary>
        public int CaptureWidth { get; set; } = 1920;
        /// <summary>
        /// 取得画像の高さ
        /// </summary>
        public int CaptureHeight { get; set; } = 1080;
        /// <summary>
        /// デバッグ用に画像を保存するか
        /// </summary>
        public bool IsSaveDebugImage { get; set; } = true;
        /// <summary>
        /// 切り出す名前の座標設定
        /// </summary>
        public CropOption[] CropOptions { get; set; } = new[] {
            CropOption.Generate(),
            CropOption.GenerateWithWatcher(),
            };
        /// <summary>
        /// 登録した画像のデフォルト保存先
        /// </summary>
        public string RegisterImageDirectory { get; set; } = "regiters";
        /// <summary>
        /// テンプレートマッチ登録者一覧
        /// </summary>
        public RegisterUser[] RegisterUsers { get; set; } = new RegisterUser[] { };
    }
}

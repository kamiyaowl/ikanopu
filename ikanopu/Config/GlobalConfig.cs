using ikanopu.Core;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ikanopu.Config {
    public class GlobalConfig : IDisposable {
        public static readonly string PATH = "config.json";
        /// <summary>
        /// エラーメッセージを返すかどうか
        /// </summary>
        public bool IsReplyError { get; set; } = true;
        /// <summary>
        /// キャプチャデバイスのインデックス
        /// </summary>
        public int CameraIndex { get; set; } = 0;
        /// <summary>
        /// キャプチャ画面を表示するか
        /// </summary>
        public bool IsShowCaptureWindow { get; set; } = true;
        /// <summary>
        /// USB接続のキャプチャデバイス以外を使用する場合はtrue
        /// </summary>
        public bool IsCustomCaptureSource { get; set; } = false;
        /// <summary>
        /// UDP、RTSPなどを受ける場合のソース
        /// </summary>
        public string CustomCaptureSource { get; set; } = "";
        /// <summary>
        /// Captureループの待ち時間
        /// </summary>
        public int CaptureDelayMs { get; set; } = 100;
        /// <summary>
        /// キャプチャ終了時のキー(ASCII)、27はESCAPE
        /// </summary>
        public int CaptureBreakKey { get; set; } = 27;
        /// <summary>
        /// 取得画像の幅
        /// </summary>
        public int CaptureWidth { get; set; } = 1920;
        /// <summary>
        /// 取得画像の高さ
        /// </summary>
        public int CaptureHeight { get; set; } = 1080;
        /// <summary>
        /// Average - XXX*sigmaを下回ったら正しい画像だと判定するか
        /// </summary>
        public double RecognizeSigmaRatio { get; set; } = 0.8;
        /// <summary>
        /// 特徴点数がどの程度異なったときに弾くか設定できる
        /// </summary>
        public double KeyPointCountThreash { get; set; } = 1.7;
        /// <summary>
        /// 特徴量の近い値が複数あったときに、
        /// true: 破棄せず一番良いものを採用
        /// false: 破棄
        /// </summary>
        public bool IsPrioritizeDetect { get; set; } = true;
        /// <summary>
        /// (Debug用) 認識を走らせ続ける場合はtrue
        /// </summary>
        public bool IsAlwaysRunDetect { get; set; } = false;

        /// <summary>
        /// Alphaチーム
        /// </summary>
        public ulong AlphaVoiceChannelId { get; set; } = 0;
        /// <summary>
        /// Bravoチーム
        /// </summary>
        public ulong BravoVoiceChannelId { get; set; } = 0;
        /// <summary>
        /// ロビー
        /// </summary>
        public ulong LobbyVoiceChannelId { get; set; } = 0;

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
        /// 一時ファイル置き場
        /// </summary>
        public string TemporaryDirectory { get; set; } = "tmp";
        /// <summary>
        /// テンプレートマッチ登録者一覧
        /// </summary>
        public List<RegisterUser> RegisterUsers { get; set; } = new List<RegisterUser>();
        /// <summary>
        /// Config書き換えなどができるユーザ一覧
        /// </summary>
        public List<ulong> AdminUsers { get; set; } = new List<ulong>();
        /// <summary>
        /// 最終更新日
        /// </summary>
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        #region IDisposable Support
        private bool disposedValue = false;

        protected virtual void Dispose(bool disposing) {
            if (!disposedValue) {
                if (disposing) {
                    if (RegisterUsers != null) {
                        foreach (var r in RegisterUsers) {
                            r.Dispose();
                        }
                        RegisterUsers = null;
                    }
                }
                disposedValue = true;
            }
        }
        public void Dispose() {
            Dispose(true);
        }
        #endregion
    }
}

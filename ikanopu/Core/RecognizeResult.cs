using Newtonsoft.Json;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ikanopu.Core {
    /// <summary>
    /// 画像認識結果を返します
    /// </summary>
    public class RecognizeResult : IDisposable {
        /// <summary>
        /// 認識に失敗した要素がある場合はtrue
        /// </summary>
        public Boolean IsInvalid { get; set; } = false;
        /// <summary>
        /// 失敗したときのメッセージ
        /// </summary>
        public string InvalidMessage { get; set; } = "";
        /// <summary>
        /// 認識できたユーザ一覧を返す
        /// </summary>
        public RecognizedUser[] RecognizedUsers { get; set; }
        /// <summary>
        /// 認識に使用した画像を保持しておきます
        /// </summary>
        public Mat[] SourceMats { get; set; }
        /// <summary>
        /// 切り取り領域
        /// </summary>
        public (CropOption.Team t, Rect r)[] CropOption { get; set; }

        /// <summary>
        /// RecognizeResult同士で比較するため、高いほどよい
        /// </summary>
        [JsonIgnore]
        public double Score =>
            ((RecognizedUsers?.Sum(x => x.Independency) ?? 0) * 100) +
            ((RecognizedUsers?.Length ?? 0) * 10) +
            (IsInvalid ? -1000 : 0);
        /// <summary>
        /// 認識結果を描画します
        /// </summary>
        /// <param name="originMat">originMatが直接編集されます</param>
        /// <returns></returns>
        public void DrawPreview(Mat originMat) {
            if (RecognizedUsers == null) { return; }
            originMat.DrawCropPreview(CropOption, RecognizedUsers.Select(x => (x.Index, $"{x.User.DisplayName}")));
        }

        #region IDisposable Support
        private bool disposedValue = false;

        protected virtual void Dispose(bool disposing) {
            if (!disposedValue) {
                if (disposing) {
                    SourceMats?.DisposeAll();
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

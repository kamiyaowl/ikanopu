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
    public class RecognizeResult: IDisposable {
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

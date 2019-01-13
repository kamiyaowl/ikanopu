﻿using Newtonsoft.Json;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ikanopu.Core {
    class RegisterUser : IDisposable {
        //TODO: discord周りの設定も追加
        public string DisplayName { get; set; }
        public string ImagePath { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        #region 画像処理周り、永続化する必要はなし
        private Mat _preloadImage = null;
        private KeyPoint[] _keyPoints = null;
        private Mat _descriptor = null;

        /// <summary>
        /// 読み込み済みの画像
        /// </summary>
        [JsonIgnore]
        public Mat PreLoadImage {
            get {
                if (_preloadImage == null) {
                    _preloadImage = Image;
                }
                return _preloadImage;
            }
        }
        /// <summary>
        /// AKAZE特徴量を返します
        /// </summary>
        [JsonIgnore]
        public (KeyPoint[], Mat) ComputeData {
            get {
                if (_keyPoints == null || _descriptor == null) {
                    var engine = BRISK.Create();
                    _descriptor = new Mat();
                    _keyPoints = engine.Detect(PreLoadImage);

                    engine.DetectAndCompute(PreLoadImage, null, out _keyPoints, _descriptor);
                }
                return (_keyPoints, _descriptor);

            }
        }


        /// <summary>
        /// 画像を読み込んで返します。パス先にないとnullが帰るからちゃんとしてね
        /// </summary>
        [JsonIgnore]
        public Mat Image {
            get => File.Exists(ImagePath) ? new Mat(ImagePath, ImreadModes.Grayscale) : null;
        }

        #endregion
        /// <summary>
        /// Json復元のためには仕方なかったんや
        /// </summary>
        public RegisterUser() { }

        /// <summary>
        /// 指定された画像を保存してからインスタンスを生成します
        /// </summary>
        /// <param name="basePath"></param>
        /// <param name="postMat"></param>
        public RegisterUser(string basePath, Mat postMat) {
            string path = GeneratePath(basePath);
            postMat.SaveImage(path);
            ImagePath = path;
        }
        /// <summary>
        /// ファイルの保存先を適当につけてくれます
        /// </summary>
        /// <param name="basePath"></param>
        /// <returns></returns>
        private static string GeneratePath(string basePath) {
            if (!Directory.Exists(basePath)) {
                Directory.CreateDirectory(basePath);
            }
            string path;
            do {
                path = Path.Combine(basePath, $"{(new Random()).Next()}.bmp");
            } while (File.Exists(path));
            return path;
        }

        #region IDisposable Support
        private bool disposedValue = false;

        protected virtual void Dispose(bool disposing) {
            if (!disposedValue) {
                if (disposing) {
                    if (_preloadImage != null) {
                        _preloadImage.Dispose();
                        _preloadImage = null;
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

using Newtonsoft.Json;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ikanopu.Core {
    class RegisterUser {
        //TODO: discord周りの設定も追加
        public string DisplayName { get; set; }
        public string ImagePath { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// 画像を読み込んで返します。パス先にないとnullが帰るからちゃんとしてね
        /// </summary>
        [JsonIgnore]
        public Mat Image {
            get => File.Exists(ImagePath) ? new Mat(ImagePath) : null;
        }
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
    }
}

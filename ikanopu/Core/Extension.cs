using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ikanopu.Core {
    public static class Extension {
        /// <summary>
        /// Jsonファイルからクラスを復元
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="path"></param>
        /// <param name="generator"></param>
        /// <returns></returns>
        public static T FromJsonFile<T>(this string path, Func<T> generator) =>
            File.Exists(path) ? JsonConvert.DeserializeObject<T>(File.ReadAllText(path)) : generator();
        /// <summary>
        /// Jsonファイルに書き出す
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="src"></param>
        /// <param name="path"></param>
        /// <param name="format"></param>
        public static void ToJsonFile<T>(this T src, string path, Formatting format = Formatting.Indented) =>
           File.WriteAllText(path, JsonConvert.SerializeObject(src, format));
    }
}

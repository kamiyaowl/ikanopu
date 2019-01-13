using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ikanopu.Core {
    /// <summary>
    /// 画像処理系の内容はすべてここに
    /// 返却するMatの破棄は、呼び出し元が責任をもって
    /// </summary>
    static class ImageUtil {
        /// <summary>
        /// 画像切り抜きのプレビューを表示します。デバッグ用
        /// </summary>
        /// <param name="mat"></param>
        /// <param name="src"></param>
        public static void DrawCropPreview(this Mat mat, IEnumerable<(CropOption.Team, Rect)> src, IEnumerable<(int, string)> recognized) {
            // デバッグ中に同一キー確認で落ちるのが煩わしい
            Dictionary<int, string> recs;
            try {
                recs = recognized.ToDictionary(x => x.Item1, x => x.Item2);
            } catch (Exception ex) {
                Console.WriteLine(ex);
                return;
            }

            int i = 0;
            foreach (var (team, rect) in src) {
                Scalar color;
                switch (team) {
                    case CropOption.Team.Alpha:
                        color = Scalar.Red;
                        break;
                    case CropOption.Team.Bravo:
                        color = Scalar.Green;
                        break;
                    case CropOption.Team.Watcher:
                        color = Scalar.Blue;
                        break;
                    default:
                        throw new NotImplementedException();
                }
                mat.Rectangle(rect, color);
                if (recs.ContainsKey(i)) {
                    mat.PutText(recs[i], rect.BottomRight, HersheyFonts.HersheyComplex, 1.0, Scalar.White, 2, LineTypes.AntiAlias, false);
                }
                i++;
            }
        }
        /// <summary>
        /// 画像の配列を手っ取り早く保存
        /// </summary>
        /// <param name="mats"></param>
        /// <param name="identifier"></param>
        public static void SaveAll(this IEnumerable<Mat> mats, string identifier = "") {
            foreach (var (m, i) in mats.Select((x, i) => (x, i))) {
                m.SaveImage($"{identifier}-{i}.bmp");
            }
        }
        /// <summary>
        /// 名前の座標を切り抜いて別のMatにコピー
        /// </summary>
        /// <param name="mat"></param>
        /// <param name="src"></param>
        /// <returns></returns>
        public static IEnumerable<(CropOption.Team, Mat)> CropNames(this Mat mat, IEnumerable<(CropOption.Team, Rect)> src) {
            foreach (var (team, rect) in src) {
                yield return (team, mat.Clone(rect));
            }
        }
        /// <summary>
        /// インクっぽい背景を消して名前だけにします
        /// </summary>
        /// <param name="src"></param>
        /// <param name="threash"></param>
        /// <param name="max"></param>
        /// <returns></returns>
        public static IEnumerable<(CropOption.Team, Mat)> RemoveBackground(
            this IEnumerable<(CropOption.Team, Mat)> src,
            double threash = 200,
            double max = 255) {
            foreach (var (t, m) in src) {
                using (var cvt = m.CvtColor(ColorConversionCodes.RGB2GRAY)) {
                    // TODO:もしかしたらeroson->dilationする必要があるかも

                    var dst = cvt.Threshold(threash, max, ThresholdTypes.BinaryInv);
                    yield return (t, dst);
                }
            }
        }

        /// <summary>
        /// いくつも作るな
        /// </summary>
        static BRISK computeEngine = null;
        /// <summary>
        /// 特徴量を計算
        /// </summary>
        /// <param name="mat"></param>
        /// <param name="keyPoint"></param>
        /// <param name="descriptor"></param>
        public static void Compute(this Mat mat, out KeyPoint[] keyPoint, Mat descriptor) {
            if (computeEngine == null) {
                computeEngine = BRISK.Create();
            }
            computeEngine.DetectAndCompute(mat, null, out keyPoint, descriptor);
        }


    }
}

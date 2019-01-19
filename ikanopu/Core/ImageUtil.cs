using ikanopu.Config;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using OpenCvSharp.Extensions;

namespace ikanopu.Core {
    /// <summary>
    /// 画像処理系の内容はすべてここに
    /// 返却するMatの破棄は、呼び出し元が責任をもって
    /// </summary>
    public static class ImageUtil {
        /// <summary>
        /// System.Drawingの支援を受け、日本語を書く
        /// あんまはやくないけどまぁいいや
        /// </summary>
        /// <param name="src"></param>
        /// <param name="text"></param>
        /// <param name=""></param>
        public static void PutTextExtra(this Mat src, Font font, Brush brush, int x, int y, string text) {
            using (var bmp = src.ToBitmap())
            using (var g = Graphics.FromImage(bmp)) {
                g.DrawString(text, font, brush, x, y);

                using (var mat = bmp.ToMat()) {
                    src[0, src.Rows, 0, src.Cols] = mat;
                }
            }
        }
        /// <summary>
        /// 画像切り抜きのプレビューを表示します。デバッグ用
        /// </summary>
        /// <param name="mat"></param>
        /// <param name="src"></param>
        public static void DrawCropPreview(this Mat mat, IEnumerable<(CropOption.Team, Rect)> src, IEnumerable<(int, string)> recognized) {
            // デバッグ中に同一キー確認で落ちるのが煩わしい
            Dictionary<int, string> recs = new Dictionary<int, string>();
            foreach (var (index, str) in recognized) {
                if (recs.ContainsKey(index)) {
                    recs[index] += $"/{str}";
                } else {
                    recs.Add(index, str);
                }
            }
            var font = new Font("MS UI Gothic", 20);

            foreach (var ((team, rect), i) in src.Select((x, i) => (x, i))) {
                Scalar color;
                Brush brush;
                switch (team) {
                    case CropOption.Team.Alpha:
                        color = Scalar.Green;
                        brush = Brushes.Green;
                        break;
                    case CropOption.Team.Bravo:
                        color = Scalar.Red;
                        brush = Brushes.Red;
                        break;
                    case CropOption.Team.Watcher:
                        color = Scalar.White;
                        brush = Brushes.White;
                        break;
                    default:
                        throw new NotImplementedException();
                }

                // 認識領域を書く
                mat.Rectangle(rect, color);
                // 認識結果を書く
                if (recs.ContainsKey(i)) {
                    mat.PutTextExtra(font, brush, rect.BottomRight.X, rect.BottomRight.Y, $"[{i}]: {recs[i]}");
                } else {
                    mat.PutTextExtra(font, brush, rect.BottomRight.X, rect.BottomRight.Y, $"[{i}]: [未登録/未検出]");
                }
            }
        }
        /// <summary>
        /// 全部リソースを開放します
        /// </summary>
        /// <param name="src"></param>
        public static void DisposeAll(this IEnumerable<(CropOption.Team, Mat)> src) {
            foreach (var (t, m) in src) {
                m.Dispose();
            }
        }
        /// <summary>
        /// 全部リソースを開放します
        /// </summary>
        /// <param name="src"></param>
        public static void DisposeAll(this IEnumerable<Mat> src) {
            foreach (var m in src) {
                m.Dispose();
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
        static Feature2D computeEngine = null;
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

        public static RecognizeResult Recognize(this (CropOption.Team, Mat)[] postMats, GlobalConfig config) {
            var matcher = new BFMatcher();

            #region  抽出した画像の特徴量を計算
            var computeDatas = postMats.Select(m => {
                var descriptor = new Mat();
                m.Item2.Compute(out var kp, descriptor);
                return new {
                    Team = m.Item1,
                    KeyPoints = kp,
                    Descriptor = descriptor,
                    Image = m.Item2,
                };
            }).ToArray();
            // 保存されてるやつとテンプレートマッチングする
            var matchResults =
                config.RegisterUsers
                      .Select(user => {
                          var (kp, d) = user.ComputeData;
                          return (user, computeDatas.Select(data => {
                              var matches = data.KeyPoints.Length == 0 ? new DMatch[] { } : matcher.Match(d, data.Descriptor).ToArray();
                              // 同じ場所切り取ってるしdistanceの総和でも見とけばいいでしょ TODO: #ちゃんと検証しろ
                              var score = matches.Length == 0 ? 0 : matches.Average(m => m.Distance);
                              return new {
                                  // 元データ
                                  Team = data.Team,
                                  Image = data.Image,
                                  KeyPoints = data.KeyPoints,
                                  // 計算後のデータ
                                  Matches = matches, // 一致した点の数。これは多いほうが良い
                                  Score = score, // 小さいほどよい。KeyPointsがなければそもそも0になるのでそこだけ注意
                              };
                          })
                            .ToArray()
                          );
                      }).ToArray();
            #endregion

            #region 一致するユーザを判定

            var recognizedUsers =
                matchResults.Select(r => {
                    var user = r.user;
                    var datas = r.Item2;
                    // まず8箇所あるよね
                    if (datas.Length < 8) return null;
                    // alphaとbravoの1人目は取得できてるよね
                    if (datas[0].Matches.Length == 0 || datas[4].Matches.Length == 0) return null;
                    // alpha0~3, bravo0~3の途中にZeroが挟まっていた場合→プラベの画面ではない可能性が高い
                    // ex) 100, 0(2人目だけマッチングが欠落することはありえない), 200, 300
                    for (int i = 0; i < 2; ++i) {
                        // alpha
                        if (datas[1 + i].Matches.Length == 0 && datas[2 + i].Matches.Length > 0) return null;
                        // bravo
                        if (datas[4 + i].Matches.Length == 0 && datas[5 + i].Matches.Length > 0) return null;
                    }
                    // 0を除外した値で平均と偏差を計算
                    // 一致画像は平均値-2sigmaを推移するため、これを満たす画像が1枚だけ見つかるときは真
                    var src =
        datas.Select((x, i) => new { Index = i, Value = x })
             .Where(x => x.Value.Matches.Length > 0)
             .ToArray();
                    var sum = src.Sum(x => x.Value.Score);
                    var average = sum / (double)src.Length;
                    var sigma = Math.Sqrt(src.Sum(x => Math.Pow(x.Value.Score - average, 2)) / src.Length);
                    var threash = average - sigma * config.RecognizeSigmaRatio; // 一応sigmaユーザーが指定できる
                                                                                // threashを下回ったもののみ抽出
                    var detects =
            src.Where(x => x.Value.Score < threash)
               .OrderBy(x => x.Value.Score)
               .ToArray();
                    //検出できなかった
                    if (detects.Length == 0) return null;
                    // 複数ある場合
                    var multipleDetect = detects.Length > 1;
                    if (multipleDetect && !config.IsPrioritizeDetect) return null;
                    // こいつが正解
                    var detect = detects.First();
                    // 完了
                    return new {
                        User = user,
                        Index = detect.Index,
                        Team = datas[detect.Index].Team,
                        Data = datas[detect.Index],
                        // あとで重複が発生したときのための評価要素も残しておく
                        Datas = datas,
                        Detects = detects, // 検出したやつ
                        Independency = (average - detect.Value.Score) / sigma, // スコアが平均値からどの程度遠ざかっているか
                        IsMultipleDetect = multipleDetect, // 似たような値が他にあった場合
                    };
                })
                .Where(x => x != null)
                .ToArray();
            #endregion

            #region 評価しておく
            if (recognizedUsers.Length == 0) {
                return new RecognizeResult() {
                    IsInvalid = true,
                    InvalidMessage = "誰も認識できませんでした",
                    SourceMats = postMats.Select(x => x.Item2).ToArray(),
                };
            }
            // 複数のプレイヤーが同じ場所を見ていた場合の修正
            // 独立性が高く、複数検出されなかったものに優先度を置く
            var filteredUsers =
                recognizedUsers.GroupBy(x => x.Index)
                               .Select(y =>
                                    y.OrderByDescending(x => x.Independency + (x.IsMultipleDetect ? -100 : 0)).First()
                               )
                               .ToList();
            // ボツになったやつも第二候補に動かしてあげる
            if (config.IsPrioritizeDetect) {
                var diffUsers =
                    recognizedUsers.Where(x => !filteredUsers.Contains(x)) // 現在ない中で
                                   .Where(x => x.IsMultipleDetect) // 複数個選択されていて
                                   .Where(x => filteredUsers.FirstOrDefault(y => y.Index == x.Detects[1].Index) == null) // 2候補のIndexが現在のものと重複していない場合
                                   .Select(x => {
                                       var detect = x.Detects[1];
                                       return new {
                                           User = x.User,
                                           Index = detect.Index,
                                           Team = x.Datas[detect.Index].Team,
                                           Data = x.Data,
                                           //
                                           Datas = x.Datas,
                                           Detects = x.Detects.Skip(1).ToArray(),
                                           Independency = x.Independency, // TODO:
                                           IsMultipleDetect = true,
                                       };
                                   }).ToArray();
                filteredUsers.AddRange(diffUsers);
            }
            #endregion

            return new RecognizeResult() {
                IsInvalid = false,
                InvalidMessage = "",
                RecognizedUsers =
                filteredUsers.Select(x => {
                    return new RecognizedUser() {
                        User = x.User,
                        Index = x.Index,
                        Team = x.Team,

                        Independency = x.Independency,
                        IsMultipleDetect = x.IsMultipleDetect,
                    };
                }).ToArray(),
                SourceMats = postMats.Select(x => x.Item2).ToArray(),
            };
        }

    }
}

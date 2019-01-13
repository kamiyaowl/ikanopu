using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ikanopu.Core {
    /// <summary>
    /// 画像処理系の内容はすべてここに
    /// </summary>
    static class ImageUtil {
        public static void DrawCropPreview(this Mat mat, IEnumerable<(CropOption.Team, Rect)> src) {
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
            }
        }
    }
}

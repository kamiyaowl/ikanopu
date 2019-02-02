using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ikanopu.Core {
    /// <summary>
    /// 画像認識したユーザ単位の結果
    /// </summary>
    public class RecognizedUser {
        public override string ToString() =>
            $"[{Team}:{Index}]{User} {Independency}";


        public RegisterUser User { get; set; }
        public int Index { get; set; }
        public CropOption.Team Team { get; set; }

        #region 参考値
        public double Independency { get; set; }
        public bool IsMultipleDetect { get; set; }
        #endregion
    }
}

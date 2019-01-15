using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ikanopu.Config {
    class SecretConfig {
        /// <summary>
        /// Botアクセス用のトークン
        /// </summary>
        public string DiscordToken { get; set; } = "";
        /// <summary>
        /// 最終更新日
        /// </summary>
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}

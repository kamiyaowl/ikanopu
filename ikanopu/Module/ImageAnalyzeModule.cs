using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ikanopu.Module {
    public class ImageAnalyzeModule : ModuleBase {
        [Command("pu")]
        public async Task Help() {
            var sb = new StringBuilder();
            sb.AppendLine("ikanopu(beta)");
            sb.AppendLine("プライベートマッチの音声チャンネル遷移を自動でやってくれます");
            await ReplyAsync(sb.ToString());
        }
        [Command("pu echo")]
        public async Task Echo([Remainder] string text) {
            await ReplyAsync($"\u200B{text}");
        }
    }
}

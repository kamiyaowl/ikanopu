using Discord.Commands;
using ikanopu.Config;
using ikanopu.Service;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ikanopu.Module {
    public class ImageProcessingModule : ModuleBase {
        public ImageProcessingService ImageProcessingService { get; set; }

        [Command("pu")]
        public async Task Help() {
            var sb = new StringBuilder();
            sb.AppendLine("ikanopu(beta)");
            sb.AppendLine("プライベートマッチの音声チャンネル遷移を自動でやってくれるかも");
            sb.AppendLine($"DIしたサービスの値を読み出してみる：{ImageProcessingService.TestData}");
            await ReplyAsync(sb.ToString());
        }
        [Command("pu echo")]
        public async Task Echo([Remainder] string text) {
            await ReplyAsync($"\u200B{text}");
        }
    }
}

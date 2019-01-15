using Discord;
using Discord.Commands;
using ikanopu.Config;
using ikanopu.Service;
using Newtonsoft.Json;
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
            await ReplyAsync(sb.ToString());
        }
        [Command("pu show config")]
        public async Task ShowConfig() {
            await ReplyAsync($"```\n{JsonConvert.SerializeObject(ImageProcessingService.Config, Formatting.None)}\n```");
        }
        [Command("pu echo")]
        public async Task Echo([Remainder] string text) {
            await ReplyAsync($"\u200B{text}");
        }
    }
}

using Discord;
using Discord.Commands;
using Discord.WebSocket;
using ikanopu.Config;
using ikanopu.Service;
using Newtonsoft.Json;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ikanopu.Module {
    public class PuService : ModuleBase {
        public DiscordSocketClient Discord { get; set; }
        public ImageProcessingService ImageProcessingService { get; set; }

        [Command("pu")]
        [Alias("ikanopu")]
        public async Task Help() {
            var sb = new StringBuilder();
            sb.AppendLine("ikanopu(beta)");
            sb.AppendLine("プライベートマッチの音声チャンネル遷移を自動でやってくれるかも");
            await ReplyAsync(sb.ToString());
        }

        [Command("pu capture")]
        public async Task Capture() {
            Mat mat = null;
            lock (ImageProcessingService.CaptureRawMat) {
                mat = ImageProcessingService.CaptureRawMat.Clone();
            }
            mat.SaveImage("capture.jpg");
            mat.Dispose();
            mat = null;

            await Context.Channel.SendFileAsync("capture.jpg", "capture.jpg");
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

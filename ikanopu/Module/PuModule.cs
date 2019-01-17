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
    public class PuModule : ModuleBase {
        public DiscordSocketClient Discord { get; set; }
        public CommandService CommandService { get; set; }
        public ImageProcessingService ImageProcessingService { get; set; }

        [Command("pu"), Summary("コマンド一覧を表示します")]
        [Alias("ikanopu", "pu help", "ikanopu help")]
        public async Task Help() {
            var sb = new StringBuilder();
            sb.AppendLine("*ikanopu(beta)*");
            sb.AppendLine("プライベートマッチの音声チャンネル遷移を自動でやってくれるかも");
            sb.AppendLine();
            sb.AppendLine("コマンドは先頭に`!`をつけた後に以下リストにあるものが使用できます");
            sb.AppendLine("https://github.com/kamiyaowl/ikanopu/blob/master/ikanopu/Module/PuModule.cs");

            var builder = new EmbedBuilder();
            foreach (var c in CommandService.Commands) {
                builder.AddField(
                    c.Name + " " + string.Join(" ", c.Parameters.Select(x => $"[{x.Name}]")),
                    (c.Summary ?? "no description") + "\n" +
                        string.Join("\n", c.Parameters.Select(x => $"[{x.Name}]: {x.Summary}"))
                );
            }
            await ReplyAsync(sb.ToString(), false, builder.Build());
        }

        [Command("pu capture"), Summary("現在のキャプチャデバイスの画像を取得します")]
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
        [Command("pu show config"), Summary("config.jsonの内容を表示します")]
        public async Task ShowConfigRaw([Summary("子要素名、`--all`指定するとすべて表示")] string name) {
            string str = null;
            if (name.Equals("--all")) {
                str = JsonConvert.SerializeObject(ImageProcessingService.Config, Formatting.None);
            } else {
                dynamic config = ImageProcessingService.Config;
                var deserialized = JsonConvert.DeserializeObject(JsonConvert.SerializeObject(config));
                str = JsonConvert.SerializeObject(deserialized[name], Formatting.Indented);
            }
            await ReplyAsync($"```\n{str}\n```");
        }
        [Command("pu echo"), Summary("俺がオウムだ")]
        public async Task Echo([Remainder, Summary("適当なテキスト")] string text) {
            await ReplyAsync($"\u200B{text}");
        }
    }
}

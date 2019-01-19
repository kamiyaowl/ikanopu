using Discord;
using Discord.Commands;
using Discord.WebSocket;
using ikanopu.Config;
using ikanopu.Service;
using Newtonsoft.Json;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ikanopu.Core;
using static ikanopu.Core.CropOption;

namespace ikanopu.Module {
    [Group("pu"), Alias("ikanopu")]
    public class PuModule : ModuleBase {
        public DiscordSocketClient Discord { get; set; }
        public CommandService CommandService { get; set; }
        public ImageProcessingService ImageProcessingService { get; set; }

        [Command, Summary("コマンド一覧を表示")]
        [Alias("help")]
        public async Task Help() {
            var sb = new StringBuilder();
            sb.AppendLine("*ikanopu(beta)*");
            sb.AppendLine();
            sb.AppendLine("コマンドは先頭に`!`をつけた後に以下リストにあるものが使用できます。詳細は実装を参照。");
            sb.AppendLine("https://github.com/kamiyaowl/ikanopu/blob/master/ikanopu/Module/PuModule.cs");

            var builder = new EmbedBuilder();
            foreach (var c in CommandService.Commands) {
                builder.AddField(
                    c.Aliases.First() + " " + string.Join(" ", c.Parameters.Select(x => $"[{x}]")),
                    (c.Summary ?? "no description") + "\n" +
                        string.Join("\n", c.Parameters.Select(x => $"[{x.Name}]: {x.Summary}"))
                );
            }
            await ReplyAsync(sb.ToString(), false, builder.Build());
        }

        [Command("lobby"), Summary("ボイスチャット参加者をロビーに集めます。\nアルファ、ブラボー、ロビーのVCに参加していて、ステータスがオフラインではないユーザが対象です")]
        public async Task Lobby() {
            var vcs = await Context.Guild.GetVoiceChannelsAsync();
            var targetChannels = new[]{
                        (Team.Alpha, ImageProcessingService.Config.AlphaVoiceChannelId),
                        (Team.Bravo, ImageProcessingService.Config.BravoVoiceChannelId),
                        (Team.Watcher, ImageProcessingService.Config.LobbyVoiceChannelId),
                    }
                .Where(x => x.Item2.HasValue)
                .Select(x => (x.Item1, vcs.FirstOrDefault(vc => vc.Id == x.Item2.Value)))
                .Where(x => x.Item2 != null)
                .ToDictionary(x => x.Item1, x => x.Item2);

            if (!targetChannels.ContainsKey(Team.Watcher)) {
                await ReplyAsync("[Error] LobbyVoiceChannelIdが未登録か無効です");
                return;
            }
            var lobby = targetChannels[Team.Watcher];
            var builder = new EmbedBuilder();
            foreach (var c in targetChannels) {
                // 動かす必要なかった
                if (c.Key == Team.Watcher) continue;

                var users = await c.Value.GetUsersAsync().FlattenAsync();
                var filtered =
                    users.Where(x => x.Status != UserStatus.Offline)
                         .ToArray();
                foreach (var u in users) {
                    builder.AddField($"{u.Username}#{u.Discriminator}", $"{u.VoiceChannel.Name} -> {lobby.Name}");
                    await u.ModifyAsync(x => x.ChannelId = Optional.Create(lobby.Id));
                }
            }
            await ReplyAsync("以下の通り移動しました", false, builder.Build());
        }

        [Command("detect"), Summary("画像認識を行いボイスチャットを遷移させます。\nステータスをオフラインにしていないユーザすべてが対象です。")]
        public async Task Capture(
            [Summary("(optional: true) 推測結果からユーザを移動させる場合はtrue")] bool move = true,
            [Summary("(optional: -1) 切り出す領域を設定します。`-1`の場合は結果の良い方を採用")] int cropIndex = -1,
            [Summary("(optional: true) 認識に使用した画像を表示する場合はtrue")] bool uploadImage = true,
            [Summary("(optional: true) 認識できなかった結果を破棄する場合はtrue")] bool preFilter = true
            ) {
            var rawPath = Path.Combine(ImageProcessingService.Config.TemporaryDirectory, "raw.jpg");
            var path = Path.Combine(ImageProcessingService.Config.TemporaryDirectory, "recognize.jpg");
            // cropIndexの領域確認
            if (cropIndex > -1 && cropIndex >= ImageProcessingService.Config.CropOptions.Length) {
                await ReplyAsync($"cropIndexが設定不可能な値です。0-{ImageProcessingService.Config.CropOptions.Length}までの値を指定してください。");
                return;
            }
            // とりあえず認識してあげる
            var targetIndexes = cropIndex == -1 ? Enumerable.Range(0, ImageProcessingService.Config.CropOptions.Length) : new[] { cropIndex };
            var results = await ImageProcessingService.RecognizeAllAsync(targetIndexes, preFilter);
            if (results.Length == 0) {
                await ReplyAsync($"正常に認識することができませんでした。\nデバッグ目的やまだ1人も登録していない場合に登録用の画像を生成したい場合は\n`!pu detect false [cropIndex] true false`\nを試してください。");
                return;
            }
            var result = results.First();
            Mat mat = null;
            // アップロード用のプレビューを作る
            lock (ImageProcessingService.CaptureRawMat) {
                mat = ImageProcessingService.CaptureRawMat.Clone();
            }
            mat.SaveImage(rawPath);
            // 認識結果を書き込む
            result.DrawPreview(mat);
            mat.SaveImage(path);
            mat.Dispose();
            mat = null;
            // あとで登録できるようにpostMatsをローカルに保管する
            foreach (var (sourceMat, i) in result.SourceMats.Select((x, i) => (x, i))) {
                var p = Path.Combine(ImageProcessingService.Config.TemporaryDirectory, $"recognize-[{i}].bmp");
                sourceMat.SaveImage(p);
            }

            // 認識結果の埋め込みを作ってあげる
            var builder = new EmbedBuilder();
            // 認識失敗だけどpreFilter切っている場合などはここで終わり
            if (!result.IsInvalid && (result.RecognizedUsers?.Length ?? 0) > 0) {
                foreach (var r in result.RecognizedUsers.OrderBy(x => x.Index)) {
                    builder.AddField($"[{r.Index}] {r.Team}: {r.User.DisplayName}", $"Discord ID:{r.User.DiscordId}\nScore: {r.Independency}");
                }
                // ボイスチャットを移動させる
                if (move) {
                    var allUsers =
                        (await Context.Guild.GetUsersAsync())
                        .Where(x => x.Status != UserStatus.Offline) // offlineユーザは動かさないであげよう
                        .ToArray();
                    var targets =
                        result.RecognizedUsers
                              .Select(recognized => (recognized, allUsers.FirstOrDefault(u => u.Id == recognized.User.DiscordId)))
                              .Where(x => x.Item2 != null)
                              .Select(x => new { Team = x.recognized.Team, GuildUser = x.Item2 })
                              .ToArray();
                    // 移動させよう
                    var targetChannels = new[]{
                        (Team.Alpha, ImageProcessingService.Config.AlphaVoiceChannelId),
                        (Team.Bravo, ImageProcessingService.Config.BravoVoiceChannelId),
                        (Team.Watcher, ImageProcessingService.Config.LobbyVoiceChannelId),
                    }
                        .Where(x => x.Item2.HasValue);
                    foreach (var (team, vcId) in targetChannels) {
                        var users = targets.Where(x => x.Team == team);
                        foreach (var u in users) {
                            await u.GuildUser.ModifyAsync(x => x.ChannelId = Optional.Create(vcId.Value));
                        }
                    }
                }
                // Disposeしとく
                foreach (var r in results) {
                    r.Dispose();
                }
            }

            // 結果を返す
            // 返す
            var message = @"*認識結果*

登録されてないユーザは以下のコマンドで登録できます。
`!pu register [Discord IDもしくは表示名] [登録したい名前の横に書かれた数字]`
";
            if (uploadImage) {
                await Context.Channel.SendFileAsync(path, message, false, builder.Build());
            } else {
                await Context.Channel.SendMessageAsync(message, false, builder.Build());
            }
        }
        [Group("register")]
        public class RegisterModule : ModuleBase {
            public ImageProcessingService ImageProcessingService { get; set; }

            [Command, Summary("コマンド一覧を表示")]
            [Alias("add")]
            public async Task Add() {

            }

        }

        [Group("config")]
        public class ConfigModule : ModuleBase {
            public ImageProcessingService ImageProcessingService { get; set; }

            [Command("show"), Summary("config.jsonの内容を表示します")]
            public async Task ShowConfigRaw([Summary("子要素名、`--all`指定するとすべて表示")] string name) {
                string str = null;
                if (name.Equals("--all")) {
                    str = JsonConvert.SerializeObject(ImageProcessingService.Config, Formatting.Indented);
                } else {
                    dynamic config = ImageProcessingService.Config;
                    var deserialized = JsonConvert.DeserializeObject(JsonConvert.SerializeObject(config));
                    str = JsonConvert.SerializeObject(deserialized[name], Formatting.Indented);
                }

                // 2000文字対策
                var TEXT_LENGTH_N = 1900;
                var n = (str.Length / TEXT_LENGTH_N) + 1;
                for (int i = 0; i < n; ++i) {
                    var length = (n - 1 == i) ? str.Length % TEXT_LENGTH_N : TEXT_LENGTH_N;
                    await Context.Channel.SendMessageAsync($"```\n{str.Substring(i * TEXT_LENGTH_N, length)}\n```");
                }
            }

            [Command("sync users"), Summary("RegisterUsersにあるユーザー名をDiscordと同期します")]
            public async Task SyncUser() {
                foreach (var ru in ImageProcessingService.Config.RegisterUsers.Where(x => x.DiscordId.HasValue)) {
                    var user = await Context.Guild.GetUserAsync(ru.DiscordId.Value);
                    ru.DisplayName = user.Username;
                }
                ImageProcessingService.Config.ToJsonFile(GlobalConfig.PATH);
                await this.ShowConfigRaw("RegisterUsers");
            }

        }

        [Group("debug")]
        public class DebugModule : ModuleBase {
            public ImageProcessingService ImageProcessingService { get; set; }

            [Command("echo"), Summary("俺がオウムだ")]
            public async Task Echo([Remainder, Summary("適当なテキスト")] string text) {
                await ReplyAsync($"\u200B{text}");
            }

            [Command("capture"), Summary("現在のキャプチャデバイスの画像を取得します")]
            public async Task Capture() {
                var path = Path.Combine(ImageProcessingService.Config.TemporaryDirectory, "capture.jpg");

                Mat mat = null;
                lock (ImageProcessingService.CaptureRawMat) {
                    mat = ImageProcessingService.CaptureRawMat.Clone();
                }
                mat.SaveImage(path);
                mat.Dispose();
                mat = null;

                await Context.Channel.SendFileAsync(path, "capture.jpg");
            }

            [Command("userinfo"), Summary("ユーザー情報を返します")]
            public async Task UserInfo([Summary("(optional: bot_id) ユーザID及び名前など(@hogehoge, hogehoge#1234, raw_id)。省略した場合は自身の情報")] IUser user = null) {
                var userInfo = user ?? Context.Client.CurrentUser;
                await ReplyAsync($"{userInfo.Username}#{userInfo.Discriminator} (ID: {userInfo.Id})");
            }

            [Command("move"), Summary("ボイスチャンネル移動テスト")]
            public async Task Move(
                [Summary("ユーザID及び名前など(@hogehoge, hogehoge#1234, raw_id)")] IUser user,
                [Summary("ボイスチャンネルID")] IVoiceChannel vc
                ) {
                var guildUser = await Context.Guild.GetUserAsync(user.Id);
                await guildUser.ModifyAsync(x => x.Channel = Optional.Create(vc));
            }

            [Command("vc users"), Summary("ボイスチャットに参加しているユーザー一覧を返します")]
            public async Task UserInfo() {
                var channels = await Context.Guild.GetVoiceChannelsAsync();
                var builder = new EmbedBuilder();
                foreach (var c in channels) {
                    var users = await c.GetUsersAsync().FlattenAsync();
                    var header = $"{c.Name}({c.Id})";
                    if (ImageProcessingService.Config.AlphaVoiceChannelId.GetValueOrDefault() == c.Id) {
                        header += " [アルファチーム会場]";
                    }
                    if (ImageProcessingService.Config.BravoVoiceChannelId.GetValueOrDefault() == c.Id) {
                        header += " [ブラボーチーム会場]";
                    }
                    if (ImageProcessingService.Config.LobbyVoiceChannelId.GetValueOrDefault() == c.Id) {
                        header += " [ロビー]";
                    }

                    var body = string.Join("\n", users.Select(x => $"{x.Username}#{x.Discriminator} ({x.Id}) {(ImageProcessingService.Config.RegisterUsers.Any(ru => ru.DiscordId.GetValueOrDefault() == x.Id) ? "[登録済]" : "[未登録]")}"));
                    builder.AddField(header, body.Length > 0 ? body : "*no user*");
                }
                await ReplyAsync("", false, builder.Build());
            }

            [Command("clean"), Summary("ikanopuのつぶやきをなかったことにする")]
            public async Task Clean([Summary("(optional: 100) 遡って削除する上限数")] int limit = 100) {
                var messages = await Context.Channel.GetMessagesAsync(limit).FlattenAsync();
                var filtered = messages.Where(x => x.Author.Id == Context.Client.CurrentUser.Id);
                foreach (var m in filtered) {
                    Console.WriteLine($"MessageMessageAsync: {m}");
                    await Context.Channel.DeleteMessageAsync(m);
                }
            }
        }
    }
}

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
using System.Net.Http;
using Newtonsoft.Json.Linq;

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
            sb.AppendLine("コマンドは先頭に`!`をつけた後に以下リストにあるものが使用できます。詳細は実装を参照。");
            sb.AppendLine("https://github.com/kamiyaowl/ikanopu/blob/master/ikanopu/Module/PuModule.cs");

            var builder = new EmbedBuilder();
            foreach (var c in CommandService.Commands) {
                builder.AddField(
                    c.Aliases.First() + " " + string.Join(" ", c.Parameters.Select(x => $"[{x}]")),
                    (c.Summary ?? "no description") + "\n" +
                        string.Join("\n", c.Parameters.Select(x => $"[{x.Name}]: {x.Summary}")) + "\n\n"
                );
            }
            await ReplyAsync(sb.ToString(), false, builder.Build());
        }

        [Command("lobby"), Summary("ボイスチャット参加者をロビーに集めます。\nアルファ、ブラボー、ロビーのVCに参加していて、ステータスがオフラインではないユーザが対象です")]
        [Alias("l")]
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
                         .Where(x => !x.IsBot)
                         .ToArray();
                foreach (var u in filtered) {
                    builder.AddField($"{u.Username}#{u.Discriminator}", $"{u.VoiceChannel.Name} -> {lobby.Name}");
                    await u.ModifyAsync(x => x.ChannelId = Optional.Create(lobby.Id));
                }
            }
            await ReplyAsync("以下の通り移動しました", false, builder.Build());
        }

        [Command("detect"), Summary("画像認識を行いボイスチャットを遷移させます。\nステータスをオフラインにしていないユーザすべてが対象です。")]
        [Alias("d")]
        public async Task Capture(
            [Summary("(option: true) 推測結果からユーザを移動させる場合はtrue")] bool move = true,
            [Summary("(option: -1) 切り出す領域を設定します。`-1`の場合は結果の良い方を採用")] int cropIndex = -1,
            [Summary("(option: true) 認識に使用した画像を表示する場合はtrue")] bool uploadImage = true,
            [Summary("(option: true) 認識できなかった結果を破棄する場合はtrue")] bool preFilter = true,
            [Summary("(option: true) 観戦者をAlpha/Bravoチャンネルに移動させる場合はtrue")] bool watcherMove = true
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
                    }.Where(x => x.Item2.HasValue);

                    foreach (var (team, vcId) in targetChannels) {
                        var users = targets.Where(x => x.Team == team);
                        foreach (var u in users) {
                            await u.GuildUser.ModifyAsync(x => x.ChannelId = Optional.Create(vcId.Value));
                        }
                    }
                    // 観戦者ムーブ
                    if (watcherMove) {
                        var watchers = targets.Where(x => x.Team == Team.Watcher).ToArray();
                        var r = new Random();
                        var isAlpha = r.NextDouble() > 0.5;
                        for (int i = 0; i < watchers.Length; ++i) {
                            // 交互に飛ばす
                            var u = watchers[i];
                            var (team, vcId) = isAlpha ? targetChannels.FirstOrDefault(x => x.Item1 == Team.Alpha) : targetChannels.FirstOrDefault(x => x.Item1 == Team.Bravo);
                            isAlpha = !isAlpha; // 反転
                            if (vcId != null) {
                                await u.GuildUser.ModifyAsync(x => x.ChannelId = Optional.Create(vcId.Value));
                            }
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
        [Alias("r")]
        public class RegisterModule : ModuleBase {
            public ImageProcessingService ImageProcessingService { get; set; }

            [Command, Summary("画像とDiscord Userの関連付けを追加します")]
            [Alias("add", "create")]
            public async Task Add(
                    [Summary("追加するユーザID及び名前など(@hogehoge, hogehoge#1234, raw_id)")] IUser user,
                    [Summary("削除するインデックス。必ず`!pu register show`で確認してください。")] int index
                ) {
                var srcPath = Path.Combine(ImageProcessingService.Config.TemporaryDirectory, $"recognize-[{index}].bmp");
                var tmpFilePath = Path.Combine(ImageProcessingService.Config.TemporaryDirectory, "tmp.jpg");
                if (user.IsBot || user.IsWebhook) {
                    await ReplyAsync("BotおよびWebhook Userは登録できません");
                    return;
                }
                if (!File.Exists(srcPath)) {
                    await ReplyAsync("指定されたインデックスの画像が存在しません");
                }
                RegisterUser ru;
                using (var mat = new Mat(srcPath)) {
                    ru = new RegisterUser(ImageProcessingService.Config.RegisterImageDirectory, mat, user);
                    mat.SaveImage(tmpFilePath);
                }
                ImageProcessingService.Config.RegisterUsers.Add(ru);
                ImageProcessingService.Config.ToJsonFile(GlobalConfig.PATH);

                // 登録できたので画像とJson返しとく
                await Context.Channel.SendFileAsync(tmpFilePath, $"以下のユーザを登録しました\n```\n{JsonConvert.SerializeObject(ru, Formatting.Indented)}\n```");
            }

            [Command("remove"), Summary("画像とDiscord Userの関連付けを削除します")]
            [Alias("delete", "rm", "del")]
            public async Task Remove(
                    [Summary("削除するインデックス。必ず`!pu register show`で確認してください。")] int index,
                    [Summary("(option: false) 確認用。本当に削除する場合はtrue")] bool delete = false
                ) {
                if (index < 0 || index >= ImageProcessingService.Config.RegisterUsers.Count) {
                    await ReplyAsync($"インデックスの値が不正です。[0-{ImageProcessingService.Config.RegisterUsers.Count - 1}]の範囲で指定してください。");
                    return;
                }
                var target = ImageProcessingService.Config.RegisterUsers[index];
                if (!delete) {
                    await ReplyAsync($"まだ消してません。内容確認して問題なければ、\n`!pu register remove {index} true`で削除することができます。\n```\n{JsonConvert.SerializeObject(target, Formatting.Indented)}\n```");
                    return;
                }
                ImageProcessingService.Config.RegisterUsers.RemoveAt(index);
                ImageProcessingService.Config.ToJsonFile(GlobalConfig.PATH);

                await ReplyAsync($"次のユーザを削除しました。\n```\n{JsonConvert.SerializeObject(target, Formatting.Indented)}\n```");
            }

            [Group("show")]
            [Alias("s")]
            public class ShowModule : ModuleBase {
                public ImageProcessingService ImageProcessingService { get; set; }
                [Command("now"), Summary("登録済一覧を表示します")]
                [Alias("registered", "current")]
                public async Task Registered(
                    [Summary("(option: false) 登録画像も一緒に表示する場合はtrue")] bool showImage = false,
                    [Summary("(option: false) bitmapのオリジナル画像が欲しい場合はtrue")] bool useBitmap = false
                    ) {
                    var tmpFilePath = Path.Combine(ImageProcessingService.Config.TemporaryDirectory, "tmp.jpg");

                    foreach (var (ru, i) in ImageProcessingService.Config.RegisterUsers.Select((x, i) => (x, i))) {
                        var message = $"*[{i}] {ru.DisplayName}({ru.DiscordId})*\n{ru.ImagePath}\n※この登録を削除する場合は、`!pu register remove {i}`を実行します";
                        if (showImage) {
                            string path = ru.ImagePath;
                            if (!useBitmap) {
                                ru.PreLoadImage.SaveImage(tmpFilePath);
                                path = tmpFilePath;
                            }
                            await Context.Channel.SendFileAsync(path, message);
                        } else {
                            await Context.Channel.SendMessageAsync(message);
                        }
                    }
                }

                [Command("images"), Summary("現在登録可能な画像一覧を返します。(`!pu detect`実行時にキャッシュされます")]
                public async Task Images(
                    [Summary("(option: false) bitmapのオリジナル画像が欲しい場合はtrue")] bool useBitmap = false
                ) {
                    var files = Directory.GetFiles(ImageProcessingService.Config.TemporaryDirectory, "recognize-*");
                    var rawFilePath = Path.Combine(ImageProcessingService.Config.TemporaryDirectory, "raw.jpg");
                    var tmpFilePath = Path.Combine(ImageProcessingService.Config.TemporaryDirectory, "tmp.jpg");
                    if (files.Length == 0) {
                        string message = $"現在利用可能な画像がありません。`!pu detect`を実行することで切り抜いた画像をローカルにキャッシュすることができます";
                        await base.ReplyAsync(message);
                        return;
                    }
                    // オリジナル画像をアップしとく
                    if (File.Exists(rawFilePath)) {
                        await Context.Channel.SendFileAsync(rawFilePath, $"元画像");
                    }
                    // recognize-[i].bmpを順にアップしていく
                    foreach (var (f, i) in files.Select((x, i) => (x, i))) {
                        string path;
                        if (useBitmap) {
                            path = f;
                        } else {
                            // bitmapはプレビューが生成されないらしいので
                            using (var mat = new Mat(f)) {
                                mat.SaveImage(tmpFilePath);
                            }
                            path = tmpFilePath;
                        }
                        await Context.Channel.SendFileAsync(path, $"この画像で登録する場合は、`!pu register [Discordユーザ名 or ID] {i}`を実行します");
                    }

                }
            }

        }

        [Group("config")]
        [Alias("c")]
        public class ConfigModule : ModuleBase {
            public ImageProcessingService ImageProcessingService { get; set; }

            [Command("get"), Summary("config.jsonの内容を表示します")]
            [Alias("show")]
            public async Task Get(
                [Summary("子要素名、`--all`指定するとすべて表示")] string name
                ) {
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

            [Command("sync"), Summary("RegisterUsersにあるユーザー名をDiscordと同期します")]
            public async Task SyncUser() {
                foreach (var ru in ImageProcessingService.Config.RegisterUsers.Where(x => x.DiscordId.HasValue)) {
                    var user = await Context.Guild.GetUserAsync(ru.DiscordId.Value);
                    ru.DisplayName = user.Username;
                }
                ImageProcessingService.Config.ToJsonFile(GlobalConfig.PATH);
                await this.Get("RegisterUsers");
            }
        }

        [Command("rule"), Summary("ステージとルールに悩んだらこれ")]
        [Alias("r")]
        public async Task Rule(
            [Summary("(option: false) ナワバリバトルを含める場合はtrue")] bool nawabari = false
            ) {
            var rules = new List<string>() {
                "ガチエリア",
                "ガチホコ",
                "ガチヤグラ",
                "ガチアサリ",
            };
            if (nawabari) {
                rules.Add("ナワバリ");
            }
            var r = new Random();

            var c = new HttpClient();
            var json = await c.GetStringAsync("https://stat.ink/api/v2/stage");
            var stagesJson = JsonConvert.DeserializeObject(json) as JArray;
            var stages = stagesJson?.Select(x => x["name"]["ja_JP"]).ToArray();
            if (stages == null) {
                await ReplyAsync("正常に取得できませんでした");
                return;
            }
            var stage = stages[r.Next(stages.Length)];
            var rule = rules[r.Next(rules.Count)];

            await ReplyAsync($"{stage} {rule}");
        }
        [Command("buki"), Summary("ブキに悩んだらこれ")]
        [Alias("b")]
        public async Task Buki(
            [Summary("(option: 8) おみくじの回数。8人分用意すればいいよね")] int count = 8
            ) {
            if (count < 1) return;

            var r = new Random();

            var c = new HttpClient();
            var json = await c.GetStringAsync("https://stat.ink/api/v2/weapon");
            var weaponsJson = JsonConvert.DeserializeObject(json) as JArray;
            var weapons = weaponsJson?.ToList();
            if (weapons == null) {
                await ReplyAsync("正常に取得できませんでした");
                return;
            }
            // 指定回数分だけ繰り返す
            var builder = new EmbedBuilder();
            for (int i = 0; i < count; ++i) {
                if (weapons.Count < 1) break; // overrun対策

                var index = r.Next(weapons.Count);
                var w = weapons[index];
                weapons.RemoveAt(index);

                var main = w["name"]["ja_JP"];
                var sub = w["sub"]["name"]["ja_JP"];
                var special = w["special"]["name"]["ja_JP"];

                builder.AddField($"[{i}] {main}", $"{sub}/{special}");
            }
            await ReplyAsync($"", false, builder.Build());
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
            public async Task UserInfo(
                [Summary("(option: bot_id) ユーザID及び名前など(@hogehoge, hogehoge#1234, raw_id)。省略した場合は自身の情報")] IUser user = null
                ) {
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

            [Group("clean")]
            public class CleanModule : ModuleBase {
                public ImageProcessingService ImageProcessingService { get; set; }

                [Command("images"), Summary("登録されていない画像キャッシュを削除します")]
                public async Task Images(
                    [Summary("(option: false) 確認用。本当に削除する場合はtrue")] bool delete = false
                    ) {
                    var targets = Directory.GetFiles(ImageProcessingService.Config.RegisterImageDirectory);
                    var useFiles =
                        ImageProcessingService.Config
                                              .RegisterUsers
                                              .Select(x => x.ImagePath)
                                              .Select(x => Path.GetFileName(x))
                                              .ToArray();

                    var sb = new StringBuilder();
                    sb.AppendLine("削除対象");
                    if (!delete) {
                        sb.AppendLine("※本当に削除する場合は`!pu debug clean images true`を実行してください。");
                    }
                    foreach (var filePath in targets) {
                        // ファイル名で比較して、使ってれば無視
                        var filename = Path.GetFileName(filePath);
                        if (useFiles.Contains(filename)) continue;

                        sb.AppendLine(filePath);
                        if (delete) {
                            File.Delete(filePath);
                        }
                    }
                    await ReplyAsync(sb.ToString());
                }

                [Command("posts"), Summary("ikanopuのつぶやきをなかったことにする")]
                public async Task Post(
                    [Summary("(option: false) 確認用。本当に削除する場合はtrue")] bool delete = false,
                    [Summary("(option: 100) 遡って削除する上限数")] int limit = 100
                    ) {
                    var messages = await Context.Channel.GetMessagesAsync(limit).FlattenAsync();
                    var filtered = messages.Where(x => x.Author.Id == Context.Client.CurrentUser.Id);

                    var sb = new StringBuilder("削除対象\n本当に削除する場合は`!pu debug clean posts true`を実行してください。");
                    foreach (var m in filtered) {
                        Console.WriteLine($"[{DateTime.Now}] MessageMessageAsync: {m}");
                        if (delete) {
                            await Context.Channel.DeleteMessageAsync(m);
                        } else {
                            sb.AppendLine($"[{m.CreatedAt}] {m.Content}");
                        }
                    }
                    // deleteしない場合はメッセージを出す
                    if (!delete) {
                        await ReplyAsync(sb.ToString());
                    }
                }
            }
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Discord.Commands;
using Discord.WebSocket;
using ikanopu.Config;
using ikanopu.Core;
using ikanopu.Service;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using OpenCvSharp;

namespace ikanopu {
    public class Program {
        #region discord
        private static DiscordSocketClient discord;
        private static CommandService commands;
        private static IServiceProvider services;
        #endregion

        [STAThread]
        static async Task Main(string[] args) {
            Console.WriteLine("#################################################");
            Console.WriteLine("#                                               #");
            Console.WriteLine("#                    ikanopu                    #");
            Console.WriteLine("#   url: https://github.com/kamiyaowl/ikanopu   #");
            Console.WriteLine("#                                               #");
            Console.WriteLine("#################################################");
            Console.WriteLine();
            // 設定読み込み
            using (var config = GlobalConfig.PATH.FromJsonFile(() => new GlobalConfig())) {
                var secret = SecretConfig.PATH.FromJsonFile(() => new SecretConfig());


                #region 環境変数からのconfig書き換え(コンテナ実行時のみ有効)
                //#if !DEBUG
                if (Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER")?.Equals("true") ?? false) {
                    //#endif
                    ApplyEnvironments(config, secret);
                    //#if !DEBUG
                }
                //#endif
                #endregion


                #region 前処理
                if (!Directory.Exists(config.RegisterImageDirectory)) {
                    Directory.CreateDirectory(config.RegisterImageDirectory);
                }
                if (!Directory.Exists(config.TemporaryDirectory)) {
                    Directory.CreateDirectory(config.TemporaryDirectory);
                }
                CheckDiscordTokenSecret(secret);
                CheckRegisterUserImage(config);

                #endregion

                #region Setup Discord

                // socket clientの初期化
                discord = new DiscordSocketClient();
                discord.MessageReceived += Client_MessageReceived;
                discord.Log += Client_Log;

                // サービスのDI
                services =
                    new ServiceCollection()
                    .AddSingleton<DiscordSocketClient>()
                    .AddSingleton<ImageProcessingService>()
                    .BuildServiceProvider();

                // 本プロジェクトにあるコマンドを全部ロード
                commands = new CommandService(new CommandServiceConfig() {
                    DefaultRunMode = RunMode.Async,
                });
                await commands.AddModulesAsync(Assembly.GetEntryAssembly(), services);

                //ログインして通信開始
                if (!string.IsNullOrWhiteSpace(secret.DiscordToken)) {
                    await discord.LoginAsync(Discord.TokenType.Bot, secret.DiscordToken);
                    await discord.StartAsync();
                    await discord.SetGameAsync("イカ", "https://github.com/kamiyaowl/ikanopu", Discord.ActivityType.Playing);
                } else {
                    Console.WriteLine($"[{DateTime.Now}] DiscordTokenが指定されていないため、利用することができません。");
                }

                #endregion

                #region 画像処理
                await services.GetRequiredService<ImageProcessingService>().InitializeAsync(config);
                // メインスレッドで画像を表示してあげるしかない
                Window captureRawWindow = null;
                if (config.IsShowCaptureWindow) {
                    captureRawWindow = new Window("Capture Raw", WindowMode.KeepRatio);
                } else {
                    Console.WriteLine($"[{DateTime.Now}] IsShowCaptureWindow=falseのため、プレビューウインドウは表示されません");
                }
                var cancelTokenSource = new CancellationTokenSource();

#pragma warning disable CS4014
                services.GetRequiredService<ImageProcessingService>().CaptureAsync(cancelTokenSource.Token);
#pragma warning restore CS4014

                while (Cv2.WaitKey(config.CaptureDelayMs) != config.CaptureBreakKey) {
                    if (config.IsAlwaysRunDetect) {
                        // 常に推論動かす場合は詳細を出してあげる
                        var results = services.GetRequiredService<ImageProcessingService>().CacheResults;
                        if ((results?.Length ?? 0) > 0) {
                            var r = results.First();
                            if (r.RecognizedUsers?.Any() ?? false) {
                                Mat mat;
                                lock (services.GetRequiredService<ImageProcessingService>().CaptureRawMat) {
                                    mat = services.GetRequiredService<ImageProcessingService>().CaptureRawMat.Clone();
                                }
                                r.DrawPreview(mat, true);
                                captureRawWindow?.ShowImage(mat);
                            }
                        }
                    } else {
                        captureRawWindow?.ShowImage(services.GetRequiredService<ImageProcessingService>().CaptureRawMat);
                    }
                }
                #endregion

                #region Finalize
                // いろいろ消す
                cancelTokenSource.Cancel();
                captureRawWindow?.Close();
                captureRawWindow?.Dispose();

                // 終了時にコンフィグを書き直してあげる（バージョンが変わっていたときなど、あるじゃん)
                config.UpdatedAt = DateTime.Now;
                secret.UpdatedAt = DateTime.Now;
                config.ToJsonFile(GlobalConfig.PATH);
                secret.ToJsonFile(SecretConfig.PATH);

                Console.WriteLine($"[{DateTime.Now}] config.jsonを更新しました");

                await discord.StopAsync();
                Console.WriteLine($"[{DateTime.Now}] Discordの通信を切断しました");

                GC.Collect();
                Console.WriteLine($"[{DateTime.Now}] コンソールを閉じて終了します");
                #endregion
            }
        }
        /// <summary>
        /// 環境変数をconfigに設定します
        /// </summary>
        /// <param name="config"></param>
        /// <param name="secret"></param>
        private static void ApplyEnvironments(GlobalConfig config, SecretConfig secret) {
            Console.WriteLine($"[{DateTime.Now}] 環境変数からconfig.json, secret.jsonの内容を更新します");
            Console.WriteLine($"[{DateTime.Now}] ヒント：docker実行の場合、永続化するためには/app/config.jsonをマウントしてください");
            var props = typeof(GlobalConfig).GetProperties();
            var envs = Environment.GetEnvironmentVariables();
            foreach (DictionaryEntry env in envs) {
                var header = env.Key as string;
                var value = env.Value as string;
                // 値がない
                if (string.IsNullOrWhiteSpace(header)) {
                    continue;
                }
                // pu_をプレフィックスにしているか
                var key = header.Replace("PU_", "").Replace("pu_", "");
                if (header.Length == key.Length) {
                    continue;
                }
                // Discord Secretを書き直す
                if (key.Equals("DiscordToken")) {
                    Console.WriteLine($"[{DateTime.Now}] [Config<-Env] DiscordToken = XXXXXXXXXX");
                    secret.DiscordToken = value as string;
                }
                // GlobalConfigのメンバに当たるやつがいればそれを書き直す
                foreach (var prop in props) {
                    if (key.ToLower().Equals(prop.Name.ToLower())) {
                        // 型変換が必要な場合がある。力技
                        object dst = null;
                        var t = prop.PropertyType;

                        if (t == typeof(string)) {
                            dst = value;
                        }
                        if (t == typeof(bool)) {
                            dst = bool.Parse(value);
                        }
                        if (t == typeof(int)) {
                            dst = int.Parse(value);
                        }
                        if (t == typeof(double)) {
                            dst = double.Parse(value);
                        }
                        if (t == typeof(ulong)) {
                            dst = ulong.Parse(value);
                        }
                        Console.WriteLine($"[{DateTime.Now}] [Config<-Env] {prop.Name} = {value}");
                        prop.SetValue(config, dst);
                    }
                }
            }
            config.ToJsonFile(GlobalConfig.PATH);
            Console.WriteLine($"[{DateTime.Now}] 適用終了");
        }

        /// <summary>
        /// 登録画像がすべて存在するか確認します
        /// </summary>
        /// <param name="config"></param>
        private static void CheckRegisterUserImage(GlobalConfig config) {
            foreach (var ru in config.RegisterUsers.Where(x => !File.Exists(x.ImagePath))) {
                Console.WriteLine($"Error: {ru.DisplayName}の画像({ru.ImagePath})が参照できませんでした");
                Environment.Exit(1);
            }
        }
        /// <summary>
        /// ディスコードのトークンがない場合に登録させます
        /// </summary>
        /// <param name="secret"></param>
        private static void CheckDiscordTokenSecret(SecretConfig secret) {
            if (string.IsNullOrWhiteSpace(secret.DiscordToken)) {
                Console.WriteLine("DiscordのBot用のトークンを入力してください(secret.jsonに保存されるだけなので心配しないで");
                Console.Write(">");
                secret.DiscordToken = Console.ReadLine();
                Console.WriteLine();

                secret.UpdatedAt = DateTime.Now;
                secret.ToJsonFile(SecretConfig.PATH);
            }
        }
        /// <summary>
        /// Discordのログ出力
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        private static Task Client_Log(Discord.LogMessage message) {
            Console.WriteLine($"[{DateTime.Now}] {message.Message}");
            return Task.CompletedTask;
        }
        /// <summary>
        /// メッセージ受信時に、Userから発行されたコマンドであれば実行します
        /// </summary>
        /// <param name="socketMessage"></param>
        /// <returns></returns>
        private static async Task Client_MessageReceived(SocketMessage socketMessage) {
            var message = socketMessage as SocketUserMessage;
            Console.WriteLine($"[{DateTime.Now}] #{message.Channel.Name} - {message.Author.Username}#{message.Author.Discriminator}: {message}");

            int argPos = 0;
            // botからの自動返信などは受けない
            //if (message.Author.IsBot) return;
            if (!message.HasCharPrefix('!', ref argPos) && !message.HasMentionPrefix(discord.CurrentUser, ref argPos)) {
                return;
            }
            // コマンド実行してあげる
            var context = new CommandContext(discord, message);
            var result = await commands.ExecuteAsync(context, argPos, services);
            // ダメ
            if (!result.IsSuccess) {
                Console.WriteLine($"[{DateTime.Now}] {result.ErrorReason}");
                // エラーリプ
                var config = services.GetRequiredService<ImageProcessingService>().Config;
                if (config.IsReplyError && !result.ErrorReason.Equals("Unknown command.")) {
                    await context.Channel.SendMessageAsync($"<@{context.User.Id}> [Error] {result.ErrorReason}");
                }
                return;
            }

        }
    }
}

using System;
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
                commands = new CommandService();
                await commands.AddModulesAsync(Assembly.GetEntryAssembly(), services);

                //ログインして通信開始
                await discord.LoginAsync(Discord.TokenType.Bot, secret.DiscordToken);
                await discord.StartAsync();
                await discord.SetGameAsync("イカ", "https://github.com/kamiyaowl/ikanopu", Discord.ActivityType.Watching);

                #endregion

                #region 画像処理
                await services.GetRequiredService<ImageProcessingService>().InitializeAsync(config);
                // メインスレッドで画像を表示してあげるしかない
                using (Window captureRawWindow = new Window("Capture Raw")) {
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
                                    captureRawWindow.ShowImage(mat);
                                }
                            }
                        } else {
                            captureRawWindow.ShowImage(services.GetRequiredService<ImageProcessingService>().CaptureRawMat);
                        }
                    }
                    cancelTokenSource.Cancel();
                }
                #endregion

                #region Finalize
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
            Console.WriteLine($"[{DateTime.Now}] #{message.Channel.Name}{message.Author.Username}#{message.Author.Discriminator}: {message}");

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
                return;
            }

        }
    }
}

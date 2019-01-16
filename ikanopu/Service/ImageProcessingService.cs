﻿using Discord.Commands;
using Discord.WebSocket;
using ikanopu.Config;
using ikanopu.Core;
using Microsoft.Extensions.DependencyInjection;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ikanopu.Service {
    public class ImageProcessingService : IDisposable {
        private readonly IServiceProvider serviceProvider;
        private readonly DiscordSocketClient discord;

        public GlobalConfig Config { get; set; }
        public Mat CaptureRawMat { get; set; }

        private (CropOption.Team t, Rect r)[][] cropPositions;

        public ImageProcessingService(IServiceProvider services) {
            this.serviceProvider = services;
            this.discord = services.GetRequiredService<DiscordSocketClient>();
        }
        /// <summary>
        /// いろいろ初期化します
        /// </summary>
        /// <param name="config"></param>
        /// <returns></returns>
        public Task InitializeAsync(GlobalConfig config) {
            this.Config = config;
            // 切り抜き位置の計算
            cropPositions =
                Config.CropOptions
                      .Select(x => x.CropPosition.ToArray())
                      .ToArray();
            CaptureRawMat = new Mat(Config.CaptureHeight, Config.CaptureWidth, MatType.CV_8UC3);

            return Task.CompletedTask;
        }
        /// <summary>
        /// キャプチャデバイスから画像を取得し続けます
        /// </summary>
        /// <param name="cancelToken"></param>
        /// <returns></returns>
        public Task CaptureAsync(CancellationToken cancellationToken) {
            return Task.Run(() => {
                using (var capture = new VideoCapture(CaptureDevice.Any, Config.CameraIndex) {
                    FrameWidth = Config.CaptureWidth,
                    FrameHeight = Config.CaptureHeight,
                }) {
                    // ゴミが入っているので最初に読んでおく
                    capture.Read(this.CaptureRawMat);
                    while (!cancellationToken.IsCancellationRequested) {
                        capture.Read(this.CaptureRawMat);
                        Task.Delay(Config.CaptureDelayMs);
                    }
                    Console.WriteLine("ImageProcessingService#CaptureAsync() Canceled");
                }
            }, cancellationToken);
        }
        /// <summary>
        /// 指定された領域で名前画像の切り抜きと背景削除を行います
        /// </summary>
        /// <param name="cropIndex"></param>
        /// <param name="cropMats"></param>
        /// <param name="postMats"></param>
        /// <returns></returns>
        private Task<(bool, string)> CropAndRemoveBackgroundAsync(int cropIndex, out (CropOption.Team, Mat)[] cropMats, out (CropOption.Team, Mat)[] postMats) {
            cropMats = new(CropOption.Team, Mat)[] { };
            postMats = new(CropOption.Team, Mat)[] { };
            if (this.cropPositions.Length <= cropIndex) {
                return Task.FromResult((false, "cropIndexが不正です"));
            }
            // 切り抜き
            var cropPosition = this.cropPositions[cropIndex];
            lock (this.CaptureRawMat) {
                cropMats = this.CaptureRawMat.CropNames(cropPosition).ToArray();
            }
            var teams = cropMats.Select(x => x.Item1).ToArray();
            postMats = cropMats.RemoveBackground().ToArray();

            return Task.FromResult((true, ""));
        }


        #region IDisposable Support
        private bool disposedValue = false; // 重複する呼び出しを検出するには

        protected virtual void Dispose(bool disposing) {
            if (!disposedValue) {
                if (disposing) {
                    if (CaptureRawMat != null) {
                        CaptureRawMat.Dispose();
                        CaptureRawMat = null;
                    }
                }
                disposedValue = true;
            }
        }

        public void Dispose() {
            Dispose(true);
        }
        #endregion
    }
}
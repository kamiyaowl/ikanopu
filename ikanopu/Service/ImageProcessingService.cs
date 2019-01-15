using Discord.Commands;
using Discord.WebSocket;
using ikanopu.Config;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ikanopu.Service {
    public class ImageProcessingService {
        private readonly IServiceProvider serviceProvider;
        private readonly DiscordSocketClient discord;

        public GlobalConfig Config { get; set; }

        public ImageProcessingService(IServiceProvider services) {
            this.serviceProvider = services;
            this.discord = services.GetRequiredService<DiscordSocketClient>();
        }

        public Task InitializeAsync(GlobalConfig config) {
            this.Config = config;
            return Task.CompletedTask;
        }
    }
}

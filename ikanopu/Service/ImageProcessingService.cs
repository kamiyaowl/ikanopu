using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ikanopu.Service {
    public class ImageProcessingService {
        private readonly DiscordSocketClient discord;
        private readonly IServiceProvider serviceProvider;
        public string TestData { get; set; } = "Hi";

        public ImageProcessingService(IServiceProvider services) {
            this.serviceProvider = services;
            this.discord = services.GetRequiredService<DiscordSocketClient>();
        }
    }
}

using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ikanopu.Core {
    class MessageResponder : ModuleBase {
        [Command("!pu")]
        public async Task Hello() {
            await ReplyAsync("はろー");
        }
    }
}

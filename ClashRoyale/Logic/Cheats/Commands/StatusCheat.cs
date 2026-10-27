namespace ClashRoyale.Logic.Cheats.Commands
{
    using System;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using ClashRoyale.Logic.Player;

    public sealed class StatusCheat : Cheat
    {
        // The executable supplies live counters; the shared library has no server dependency.
        public static Func<string> ActivitySummary { get; set; } = () => "Activity counters unavailable.";

        public override CheatResult Prepare(Player player, string[] arguments)
        {
            if (arguments.Length != 0) return new("Usage: /status");
            using var process = Process.GetCurrentProcess();
            var uptime = DateTime.UtcNow - process.StartTime.ToUniversalTime();
            string server = $"Server {Config.ServerVersion} | Uptime {uptime.Days}d {uptime.Hours:00}h {uptime.Minutes:00}m {uptime.Seconds:00}s";
            return new($"{server}\n{ActivitySummary()}\nMemory {process.WorkingSet64 / (1024 * 1024)} MB | {RuntimeInformation.FrameworkDescription}\nID {player.HighId}:{player.LowId} | King {player.ExpLevel} | Cards {player.Home.SpellCount}\nGold {player.Gold} | Gems {player.Diamonds} | Trophies {player.Score}");
        }
    }
}

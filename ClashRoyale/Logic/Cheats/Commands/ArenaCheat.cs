namespace ClashRoyale.Logic.Cheats.Commands
{
    using System;
    using System.Globalization;
    using System.Linq;
    using ClashRoyale.Enums;
    using ClashRoyale.Files.Csv;
    using ClashRoyale.Files.Csv.Logic;
    using ClashRoyale.Logic.Player;

    public sealed class ArenaCheat : Cheat
    {
        public override CheatResult Prepare(Player player, string[] arguments)
        {
            var arenas = CsvFiles.Get(Gamefile.Arenas).Datas.Cast<ArenaData>()
                .Where(a => a.IsInUse && !a.TrainingCamp && !a.PveArena).OrderBy(a => a.Arena).ToArray();
            if (arguments.Length != 1 || !int.TryParse(arguments[0], NumberStyles.None, CultureInfo.InvariantCulture, out int number))
                return new("Usage: /arena <number>. Available: " + string.Join(", ", arenas.Select(a => a.Arena)));
            var arena = arenas.FirstOrDefault(a => a.Arena == number);
            if (arena == null) return new("Unknown arena. Type /arena for available numbers.");
            return new($"Arena {arena.Arena} selected: trophies set to {arena.TrophyLimit}.", () =>
            {
                player.Score = arena.TrophyLimit;
                player.SetMaxScore(Math.Max(player.MaxScore, player.Score));
                player.Arena = arena;
            });
        }
    }
}

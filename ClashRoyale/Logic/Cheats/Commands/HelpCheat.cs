namespace ClashRoyale.Logic.Cheats.Commands
{
    using System.Linq;
    using ClashRoyale.Logic.Player;
    using ClashRoyale.Messages;
    public sealed class HelpCheat : Cheat
    {
        public override CheatResult Prepare(Player player, string[] arguments) => arguments.Length != 0
            ? new("Usage: /help")
            : new("Commands: " + string.Join(", ", MessageFactory.Cheats.Keys.Where(name => name != "arena" || player.Rank == ClashRoyale.Enums.Rank.Administrator).Select(name => MessageFactory.Delimiter + name))
                + ". /gold and /gems take an amount."
                + (player.Rank == ClashRoyale.Enums.Rank.Administrator ? " /arena takes an arena number." : "") + " Replies are private.");
    }
}

namespace ClashRoyale.Logic.Cheats.Commands
{
    using System.Linq;
    using ClashRoyale.Logic.Player;
    using ClashRoyale.Messages;
    public sealed class HelpCheat : Cheat
    {
        public override CheatResult Prepare(Player player, string[] arguments) => arguments.Length != 0
            ? new("Usage: /help")
            : new("Commands: " + string.Join(", ", MessageFactory.Cheats.Keys.Select(name => MessageFactory.Delimiter + name))
                + ". /gold and /gems take an amount; /arena takes an arena number. Replies are private.");
    }
}

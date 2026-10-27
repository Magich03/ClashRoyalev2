namespace ClashRoyale.Logic.Cheats.Commands
{
    using ClashRoyale.Logic.Player;
    public sealed class InfoCheat : Cheat
    {
        public override CheatResult Prepare(Player player, string[] arguments) => arguments.Length != 0
            ? new("Usage: /info")
            : new($"Server {Config.ServerVersion}. Commands affect only your account.\n/max: max king/cards, currencies and highest-league trophies. /min: Arena 1, zero trophies/currencies, base king and starting eight cards. Clan and battle history stay.");
    }
}

namespace ClashRoyale.Logic.Cheats.Commands
{
    using ClashRoyale.Logic.Player;
    public sealed class MaxCheat : Cheat
    {
        public override CheatResult Prepare(Player player, string[] arguments) => arguments.Length != 0
            ? new("Usage: /max")
            : new("Account maxed: king/cards at valid maximum levels, gold and gems filled, trophies raised to the highest league.", () => AccountPreset.Apply(player, true));
    }
}

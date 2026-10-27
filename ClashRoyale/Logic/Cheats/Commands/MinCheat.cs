namespace ClashRoyale.Logic.Cheats.Commands
{
    using ClashRoyale.Logic.Player;
    public sealed class MinCheat : Cheat
    {
        public override CheatResult Prepare(Player player, string[] arguments) => arguments.Length != 0
            ? new("Usage: /min")
            : new("Minimal account applied: king level 1, only the starting eight cards at base levels, zero gold and gems. Arena and trophies preserved.", () => AccountPreset.Apply(player, false));
    }
}

namespace ClashRoyale.Logic.Cheats.Commands
{
    using System.Globalization;
    using ClashRoyale.Logic.Player;
    public sealed class GemsCheat : Cheat
    {
        public const int MaximumGems = 1000000;
        public override CheatResult Prepare(Player player, string[] arguments)
        {
            int limit = MaximumGems;
            if (arguments.Length != 1 || !int.TryParse(arguments[0], NumberStyles.None, CultureInfo.InvariantCulture, out int amount)
                || amount < 0 || amount > limit) return new($"Usage: /gems <0-{limit}>. Sets your balance.");
            return new($"Gems set to {amount}.", () => player.SetFreeDiamonds(amount));
        }
    }
}

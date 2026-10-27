namespace ClashRoyale.Logic.Cheats.Commands
{
    using System.Globalization;
    using ClashRoyale.Logic.Player;
    public sealed class GoldCheat : Cheat
    {

        public override CheatResult Prepare(Player player, string[] arguments)
        {
            int limit = player.MaxGold;
            if (arguments.Length != 1 || !int.TryParse(arguments[0], NumberStyles.None, CultureInfo.InvariantCulture, out int amount)
                || amount < 0 || amount > limit) return new($"Usage: /gold <0-{limit}>. Sets your balance.");
            return new($"Gold set to {amount}.", () => player.SetFreeGold(amount));
        }
    }
}

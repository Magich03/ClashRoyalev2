namespace ClashRoyale.Logic.Cheats
{
    using System;
    using ClashRoyale.Logic.Player;

    public abstract class Cheat
    {
        // Preparing validates arguments without changing the account.
        public abstract CheatResult Prepare(Player player, string[] arguments);
    }
    public sealed record CheatResult(string Response, Action Apply = null);
}

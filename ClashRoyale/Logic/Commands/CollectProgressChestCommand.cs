namespace ClashRoyale.Logic.Commands
{
    using ClashRoyale.Logic.Mode;
    public class CollectProgressChestCommand : Command
    {
        private readonly int type;
        public override int Type => type;
        public CollectProgressChestCommand(int type) { this.type = type; }
        public override byte Execute(GameMode mode)
        {
            if (mode?.Player == null || mode.Home == null) return 1;
            var home = mode.Home;
            home.RefreshCrownChest(System.DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            var chest = type == 595 ? home.StarChest : home.ClanCrownChest;
            if (chest == null || chest.IsClaimed || home.IsClaimingReward) return 2;
            if (type == 595 && (home.StarChestCounter < ClashRoyale.Extensions.Game.Globals.CrownChestCrownCount || home.CrownChestCredits < 1)) return 3;
            chest.SetClaimed(mode, type == 595 ? 3 : 9);
            return 0;
        }
    }
}

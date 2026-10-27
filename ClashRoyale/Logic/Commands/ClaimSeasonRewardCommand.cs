namespace ClashRoyale.Logic.Commands
{
    using ClashRoyale.Logic.Mode;
    public sealed class ClaimSeasonRewardCommand : Command
    {
        public override int Type => 559;
        public override byte Execute(GameMode mode) => ClashRoyale.Logic.Seasons.SeasonRewards.Claim(mode);
    }
}

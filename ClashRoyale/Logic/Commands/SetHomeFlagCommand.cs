namespace ClashRoyale.Logic.Commands
{
    using ClashRoyale.Extensions;
    using ClashRoyale.Logic.Mode;
    // Native command 573; flag 0 acknowledges popup_legend_season_intro.
    public sealed class SetHomeFlagCommand : Command
    {
        public override int Type => 573;
        public int Flag;
        public bool Value;
        public override void Decode(ByteStream stream)
        {
            base.Decode(stream);
            Flag = stream.ReadVInt();
            Value = stream.ReadBoolean();
        }
        public override byte Execute(GameMode gameMode)
        {
            if (gameMode?.Home == null) return 2;
            if (Flag < 0 || Flag > 3) return 4;
            int mask = 1 << Flag;
            gameMode.Home.HomeFlags = Value ? gameMode.Home.HomeFlags | mask : gameMode.Home.HomeFlags & ~mask;
            return 0;
        }
    }
}

namespace ClashRoyale.Logic.Commands
{
    using ClashRoyale.Extensions;
    using ClashRoyale.Logic.Mode;

    // Native 3.830: StartExploring, SpeedUpExploring, ClearChestSource, StartRewardClaim.
    public class ChestActionCommand : Command
    {
        private readonly int commandType;
        public override int Type => commandType;
        public int ChestId;
        public bool ClaimAfterSpeedUp;
        public ChestActionCommand(int type) { commandType = type; }
        public override void Decode(ByteStream stream)
        {
            base.Decode(stream);
            ChestId = stream.ReadVInt();
            if (Type == 543) ClaimAfterSpeedUp = stream.ReadBoolean();
        }
        public override void Encode(ChecksumEncoder stream)
        {
            base.Encode(stream);
            stream.WriteVInt(ChestId);
            if (Type == 543) stream.WriteBoolean(ClaimAfterSpeedUp);
        }
        public override byte Execute(GameMode mode)
        {
            var home = mode.Home;
            var chest = home?.Chests.Find(c => c != null && c.Id == ChestId);
            if (chest == null || mode.Player == null) return 1;
            if (Type == 568) { chest.SetSource(0); chest.SetNew(false); return 0; }
            if (chest.IsClaimed || home.IsClaimingReward) return 2;
            if (Type == 542)
            {
                if (!chest.CanStartUnlocking || home.FreeWorkers <= 0) return 3;
                chest.StartUnlocking();
                return 0;
            }
            if (Type == 543)
            {
                int seconds = chest.IsUnlocked ? 0 : chest.IsUnlocking ? chest.UnlockingSecondsLeft : chest.Data.TotalTimeTakenSeconds;
                int perHour = ClashRoyale.Files.Csv.CsvFiles.Get(ClashRoyale.Enums.Gamefile.Globals).GetData<ClashRoyale.Files.Csv.Logic.GlobalData>("CHEST_SPEED_UP_COST_PER_HOUR").NumberValue;
                int cost = (int)(((long)seconds * perHour + 3599) / 3600);
                if (!mode.Player.HasEnoughDiamonds(cost)) return 4;
                mode.Player.UseDiamonds(cost);
                chest.SpeedUpUnlocking();
                if (ClaimAfterSpeedUp) chest.SetClaimed(mode, 1);
                return 0;
            }
            if (!chest.IsUnlocked) return 5;
            chest.SetClaimed(mode, 1);
            return 0;
        }
    }
}

namespace ClashRoyale.Logic.Commands
{
    using ClashRoyale.Enums;
    using ClashRoyale.Extensions;
    using ClashRoyale.Files.Csv;
    using ClashRoyale.Files.Csv.Logic;
    using ClashRoyale.Logic.Event;
    using ClashRoyale.Logic.Mode;

    public sealed class ChallengeCommand : Command
    {
        private readonly int CommandType;
        public int ModeId;
        public bool HasPartner, QuickMatch;
        public int PartnerHigh, PartnerLow, PartnerValue;
        public string PartnerToken;
        public override int Type => CommandType;
        public ChallengeCommand(int type) { CommandType = type; }
        public override void Decode(ByteStream stream)
        {
            base.Decode(stream);
            if (Type == 541) ModeId = stream.ReadVInt();
            if (Type == 504)
            {
                HasPartner = stream.ReadBoolean();
                if (HasPartner)
                {
                    PartnerHigh = stream.ReadInt(); PartnerLow = stream.ReadInt();
                    PartnerToken = stream.ReadString(); PartnerValue = stream.ReadVInt();
                }
                QuickMatch = stream.ReadBoolean();
            }
        }
        public override void Encode(ChecksumEncoder stream)
        {
            base.Encode(stream);
            if (Type == 541) stream.WriteVInt(ModeId);
            if (Type == 504)
            {
                stream.WriteBoolean(HasPartner);
                if (HasPartner)
                {
                    stream.WriteInt(PartnerHigh); stream.WriteInt(PartnerLow);
                    stream.WriteString(PartnerToken); stream.WriteVInt(PartnerValue);
                }
                stream.WriteBoolean(QuickMatch);
            }
        }
        public byte Result { get; private set; }
        public override byte Execute(GameMode mode)
        {
            if (Type == 541) mode.RequestedChallengeId = ModeId;
            Result = ExecuteCore(mode);
            System.Console.WriteLine($"[EVENT] command={Type} requested={ModeId} active={mode.Home?.Challenge?.ModeId} mode={mode.Home?.Challenge?.GameMode?.Name} result={Result}");
            return Result;
        }
        private byte ExecuteCore(GameMode mode)
        {
            var player = mode.Player;
            var home = mode.Home;
            if (player == null || home == null) return 1;
            if (Type == 541)
            {
                if (player.ExpLevel < 8 || home.ChallengeReward != null) return 2;
                // Free, unplayed special matches can be reselected. Paid challenge progress is retained.
                if (home.Challenge != null && !(ModeId / 1000000 == 73 && home.Challenge.SingleMatch && !home.Challenge.Finished)) return 2;
                var data = LiveEventCatalog.Resolve(ModeId);
                if (data == null || !data.Enabled || data.EventOnly || data.MaxWins < 1 || data.MaxLoss < 1) return 3;
                if (data.JoinCostResource != "Diamonds" || data.JoinCost < 0 || player.Diamonds < data.JoinCost) return 4;
                player.UseDiamonds(data.JoinCost);
                home.Challenge = new ChallengeRun { ModeId = ModeId };
                if (ModeId / 1000000 == 73) {
                    home.EventEntries ??= new System.Collections.Generic.Dictionary<int,int>();
                    home.EventEntries.TryGetValue(ModeId % 1000000, out int entries);
                    home.EventEntries[ModeId % 1000000] = entries + 1;
                }
                return 0;
            }
            if (Type == 504)
            {
                // Partner invitations require membership validation; never silently queue a different match.
                if (HasPartner) return 7;
                if (home.Challenge == null || home.Challenge.Finished || home.Challenge.RewardClaimed ||
                    (mode.RequestedChallengeId != 0 && mode.RequestedChallengeId != home.Challenge.ModeId)) return 5;
                mode.Listener.ChallengeMatchmaking();
                return 0;
            }
            if (Type == 534)
            {
                if (home.Challenge == null || !home.Challenge.Finished || home.ChallengeReward == null || home.ChallengeReward.IsClaimed) return 6;
                home.ChallengeReward.SetClaimed(mode, 6);
                return 0;
            }
            // The client requests a copy of avatar progress into its home; both are encoded from one persisted run.
            return 0;
        }
    }
}

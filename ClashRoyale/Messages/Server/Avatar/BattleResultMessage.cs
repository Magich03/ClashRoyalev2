namespace ClashRoyale.Messages.Server.Avatar
{
    using ClashRoyale.Enums;
    using ClashRoyale.Extensions;
    using ClashRoyale.Extensions.Helper;
    using ClashRoyale.Files.Csv.Logic;

    // Native decode 0x603c6 and encode 0x24647c (ARMv7 3.830).
    public class BattleResultMessage : Message
    {
        public override short Type => 28934;
        public override Node ServiceNode => Node.Avatar;
        public int Result, TrophyDelta, Gold, OwnCrowns, OpponentCrowns;
        public TreasureChestData ChestData;
        public byte[] FullUpdate;
        public BattleResultMessage() { }
        public BattleResultMessage(ByteStream stream) : base(stream) { }
        public BattleResultMessage(byte[] update) { FullUpdate = update; }
        public override void Encode()
        {
            Stream.WriteVInt(Result);
            Stream.WriteVInt(TrophyDelta);
            Stream.WriteVInt(Gold);
            for (int i=0;i<5;i++) Stream.WriteVInt(0);
            Stream.WriteVInt(OwnCrowns);
            Stream.WriteVInt(OpponentCrowns);
            Stream.EncodeData(ChestData);
            // A null snapshot preserves the already-simulated arena (native handler explicitly supports it).
            Stream.WriteBytes(FullUpdate);
            Stream.WriteBoolean(false);
            for (int i=0;i<4;i++) Stream.WriteVInt(0); // optional data references
            Stream.WriteBoolean(false);
        }
    }
}

namespace ClashRoyale.Logic.Reward
{
    using ClashRoyale.Extensions;

    /// <summary>
    /// LogicInteractiveReward (reward type 2 in client 3.830): a chest reward the player can interact with,
    /// e.g. the Lightning chest's strikes. Layout from the client decoder: base reward, then 8 VInts.
    /// </summary>
    public class InteractiveReward : Reward
    {
        /// <summary>
        /// Seed the client uses to re-roll cards when a strike is used (client field +0x2c).
        /// </summary>
        public int RerollSeed;

        /// <summary>
        /// Number of strikes available (client field +0x30).
        /// </summary>
        public int Rerolls;

        public override int Type
        {
            get
            {
                return 2; // LogicInteractiveReward::getType in client 3.830 (the wire type is getType)
            }
        }

        public InteractiveReward()
        {
        }

        public InteractiveReward(Reward Reward)
        {
            this.Spells          = Reward.Spells;
            this.DiscardedSpells = Reward.DiscardedSpells;
            this.Gold            = Reward.Gold;
            this.Diamonds        = Reward.Diamonds;
        }

        public override void Decode(ByteStream Stream)
        {
            base.Decode(Stream);

            Stream.ReadVInt();
            Stream.ReadVInt();
            Stream.ReadVInt();
            Stream.ReadVInt();
            Stream.ReadVInt();
            this.RerollSeed = Stream.ReadVInt();
            this.Rerolls    = Stream.ReadVInt();
            Stream.ReadVInt();
        }

        public override void Encode(ChecksumEncoder Stream)
        {
            base.Encode(Stream);

            Stream.WriteVInt(0);
            Stream.WriteVInt(0);
            Stream.WriteVInt(0);
            Stream.WriteVInt(0);
            Stream.WriteVInt(0);
            Stream.WriteVInt(this.RerollSeed);
            Stream.WriteVInt(this.Rerolls);
            Stream.WriteVInt(0);
        }
    }
}

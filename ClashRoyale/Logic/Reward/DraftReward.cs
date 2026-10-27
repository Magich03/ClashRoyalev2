namespace ClashRoyale.Logic.Reward
{
    using ClashRoyale.Extensions;

    public class DraftReward : Reward
    {
        public int ChestGlobalId;
        public int ChestId;
        public int ChestType;
        public int FreeGems;
        public int PaidGems;

        /// <summary>
        ///     Gets the type of this reward.
        /// </summary>
        public override int Type
        {
            get
            {
                return 1; // LogicDraftReward::getType in client 3.830 (type 2 is LogicInteractiveReward)
            }
        }

        /// <summary>
        ///     Decodes this instance.
        /// </summary>
        public override void Decode(ByteStream Stream)
        {
            base.Decode(Stream);

            this.ChestId = Stream.ReadVInt();
            this.ChestGlobalId = Stream.ReadVInt();
            this.ChestType = Stream.ReadVInt();
            this.PaidGems = Stream.ReadVInt();
            this.FreeGems = Stream.ReadVInt();
        }

        /// <summary>
        ///     Encodes this instance.
        /// </summary>
        public override void Encode(ChecksumEncoder Stream)
        {
            base.Encode(Stream);

            Stream.WriteVInt(this.ChestId);
            Stream.WriteVInt(this.ChestGlobalId);
            Stream.WriteVInt(this.ChestType);
            Stream.WriteVInt(this.PaidGems);
            Stream.WriteVInt(this.FreeGems);
        }
    }
}
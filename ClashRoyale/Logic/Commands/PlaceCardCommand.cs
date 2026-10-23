namespace ClashRoyale.Logic.Commands
{
    using ClashRoyale.Extensions;
    using ClashRoyale.Logic.Mode;

    /// <summary>
    /// LogicDoSpellCommand (51) in client 3.830: a card placed in battle. The client simulates the battle itself, so
    /// the server only decodes it and echoes it back unchanged in the next SectorHeartbeatMessage.
    /// Layout (0x231176): base, vint hand index, data global id (vint class + vint instance), vint,
    /// bool -> card (vint data index, vint level, + vint for Hero cards), position (2 vints).
    /// </summary>
    public class PlaceCardCommand : Command
    {
        public int HandIndex;
        public int DataClass;
        public int DataInstance;
        public int Value;
        public bool HasCard;
        public int CardData;
        public int CardLevel;
        public int X;
        public int Y;

        public override int Type
        {
            get
            {
                return 51;
            }
        }

        public override void Decode(ByteStream Stream)
        {
            base.Decode(Stream);

            this.HandIndex = Stream.ReadVInt();
            this.DataClass = Stream.ReadVInt();

            if (this.DataClass != 0)
            {
                this.DataInstance = Stream.ReadVInt();
            }

            this.Value = Stream.ReadVInt();
            this.HasCard = Stream.ReadBoolean();

            if (this.HasCard)
            {
                this.CardData = Stream.ReadVInt();
                this.CardLevel = Stream.ReadVInt();
            }

            this.X = Stream.ReadVInt();
            this.Y = Stream.ReadVInt();
        }

        public override void Encode(ChecksumEncoder Stream)
        {
            base.Encode(Stream);

            Stream.WriteVInt(this.HandIndex);
            Stream.WriteVInt(this.DataClass);

            if (this.DataClass != 0)
            {
                Stream.WriteVInt(this.DataInstance);
            }

            Stream.WriteVInt(this.Value);
            Stream.WriteBoolean(this.HasCard);

            if (this.HasCard)
            {
                Stream.WriteVInt(this.CardData);
                Stream.WriteVInt(this.CardLevel);
            }

            Stream.WriteVInt(this.X);
            Stream.WriteVInt(this.Y);
        }

        public override byte Execute(GameMode GameMode)
        {
            return 0;
        }

        public override string ToString()
        {
            return "PlaceCard(hand " + this.HandIndex + ", data " + this.DataClass + ":" + this.DataInstance + ", at " + this.X + "," + this.Y + ", tick " + this.ExecuteTick + ")";
        }
    }
}

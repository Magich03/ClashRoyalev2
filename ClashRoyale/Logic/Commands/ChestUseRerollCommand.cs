namespace ClashRoyale.Logic.Commands
{
    using ClashRoyale.Extensions;
    using ClashRoyale.Logic.Mode;

    /// <summary>
    /// LogicChestUseRerollCommand (506): the player used a Lightning chest strike.
    /// Layout from the client decoder: command base, then one VInt.
    /// </summary>
    public class ChestUseRerollCommand : Command
    {
        public override int Type
        {
            get
            {
                return 506;
            }
        }

        public int Index;

        public override void Decode(ByteStream Stream)
        {
            base.Decode(Stream);
            this.Index = Stream.ReadVInt();
        }

        public override void Encode(ChecksumEncoder Stream)
        {
            base.Encode(Stream);
            Stream.WriteVInt(this.Index);
        }

        public override byte Execute(GameMode GameMode)
        {
            // The client re-rolls the struck card itself from the reward's seed; the server keeps the original roll.
            return 0;
        }
    }
}

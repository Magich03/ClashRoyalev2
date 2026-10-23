namespace ClashRoyale.Logic.Commands
{
    using ClashRoyale.Extensions;
    public sealed class PickChooseCommand : Command
    {
        public int Choice;
        public override int Type => 23;
        public override void Decode(ByteStream stream) { base.Decode(stream); Choice=stream.ReadVInt(); }
        public override void Encode(ChecksumEncoder stream) { base.Encode(stream); stream.WriteVInt(Choice); }
    }
}

namespace ClashRoyale.Logic.Commands
{
    using ClashRoyale.Extensions;
    public sealed class DraftChooseCommand : Command
    {
        public int Round, Choice;
        public override int Type => 34;
        public override void Decode(ByteStream stream) { base.Decode(stream); Round=stream.ReadVInt(); Choice=stream.ReadVInt(); }
        public override void Encode(ChecksumEncoder stream) { base.Encode(stream); stream.WriteVInt(Round); stream.WriteVInt(Choice); }
    }
}

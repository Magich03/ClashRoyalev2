namespace ClashRoyale.Logic.Commands.Server
{
    using ClashRoyale.Extensions;
    using ClashRoyale.Logic.Mode;
    public class AddClanChestCommand : ServerCommand
    {
        public int Gold, Cards, EventType;
        public override int Type => 276;
        public override void Encode(ChecksumEncoder stream) { base.Encode(stream); stream.WriteVInt(Gold); stream.WriteVInt(Cards); stream.WriteVInt(EventType); }
        public override void Decode(ByteStream stream) { base.Decode(stream); Gold=stream.ReadVInt(); Cards=stream.ReadVInt(); EventType=stream.ReadVInt(); }
        // The durable entitlement is created before sending this presentation command.
        public override byte Execute(GameMode mode) => 0;
    }
}

namespace ClashRoyale.Messages.Client.Attack;
using ClashRoyale.Enums;
using ClashRoyale.Extensions;
public sealed class SendRematchMessage : Message
{
    public override short Type => 18088;
    public override Node ServiceNode => Node.Attack;
    public bool Ready;
    public SendRematchMessage() { }
    public SendRematchMessage(ByteStream stream) : base(stream) { }
    public override void Decode() => Ready = Stream.ReadBoolean();
    public override void Encode() => Stream.WriteBoolean(Ready);
}

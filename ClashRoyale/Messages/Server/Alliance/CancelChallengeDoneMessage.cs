namespace ClashRoyale.Messages.Server.Alliance;
using ClashRoyale.Enums;
using ClashRoyale.Extensions;
public sealed class CancelChallengeDoneMessage : Message
{
    public override short Type => 20416;
    public override Node ServiceNode => Node.Avatar;
    public CancelChallengeDoneMessage() { }
    public CancelChallengeDoneMessage(ByteStream stream):base(stream) { }
}

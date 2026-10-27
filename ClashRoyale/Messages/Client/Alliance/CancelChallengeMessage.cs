namespace ClashRoyale.Messages.Client.Alliance;
using ClashRoyale.Enums;
using ClashRoyale.Extensions;
public sealed class CancelChallengeMessage : Message
{
    public override short Type => 15860;
    public override Node ServiceNode => Node.Alliance;
    public CancelChallengeMessage() { }
    public CancelChallengeMessage(ByteStream stream):base(stream) { }
}

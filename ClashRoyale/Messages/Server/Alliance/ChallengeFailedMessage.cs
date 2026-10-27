namespace ClashRoyale.Messages.Server.Alliance;
using ClashRoyale.Enums;
using ClashRoyale.Extensions;
public sealed class ChallengeFailedMessage : Message
{
    public override short Type => 20685;
    public override Node ServiceNode => Node.Avatar;
    public ChallengeFailedMessage() { }
    public ChallengeFailedMessage(ByteStream stream):base(stream) { }
}

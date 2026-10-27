namespace ClashRoyale.Messages.Server.Attack;
using ClashRoyale.Enums;
using ClashRoyale.Extensions;
using ClashRoyale.Extensions.Helper;
public sealed class RematchMessage : Message
{
    public override short Type => 29246;
    public override Node ServiceNode => Node.Attack;
    public bool Ready;
    public int HighId, LowId;
    public override void Encode()
    {
        Stream.WriteBoolean(Ready);
        Stream.WriteLogicLong(HighId, LowId);
    }
}

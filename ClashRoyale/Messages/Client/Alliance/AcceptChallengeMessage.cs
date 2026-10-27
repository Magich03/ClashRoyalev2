namespace ClashRoyale.Messages.Client.Alliance;
using ClashRoyale.Enums;
using ClashRoyale.Extensions;
public sealed class AcceptChallengeMessage : Message
{
    public override short Type => 11339;
    public override Node ServiceNode => Node.Avatar;
    public long EntryId;
    public int Slot;
    public AcceptChallengeMessage() { }
    public AcceptChallengeMessage(ByteStream stream):base(stream) { }
    public override void Decode(){EntryId=Stream.ReadLong();Slot=Stream.ReadVInt();}
    public override void Encode(){Stream.WriteLong(EntryId);Stream.WriteVInt(Slot);}
}

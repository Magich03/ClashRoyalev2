namespace ClashRoyale.Messages.Client.Alliance;
using ClashRoyale.Enums;
using ClashRoyale.Extensions;
using ClashRoyale.Extensions.Helper;
using ClashRoyale.Files.Csv.Logic;
public class DonateAllianceUnitMessage : Message
{
    public override short Type => 18277;
    public override Node ServiceNode => Node.Alliance;
    public SpellData Card;
    public long RequestId;
    public DonateAllianceUnitMessage() { }
    public DonateAllianceUnitMessage(ByteStream stream):base(stream) { }
    public override void Decode(){Card=Stream.DecodeData<SpellData>();RequestId=Stream.ReadLong();}
    public override void Encode(){Stream.EncodeData(Card);Stream.WriteLong(RequestId);}
}

namespace ClashRoyale.Logic.Commands.Server;
using ClashRoyale.Extensions;
using ClashRoyale.Extensions.Helper;
using ClashRoyale.Files.Csv.Logic;
using ClashRoyale.Logic.Mode;
public class DonateAllianceUnitCommand : ServerCommand
{
    public override int Type => 293;
    public SpellData Card;
    public long RequestId;
    public override void Decode(ByteStream s){Card=s.DecodeData<SpellData>();RequestId=s.ReadLong();base.Decode(s);}
    public override void Encode(ChecksumEncoder s){s.EncodeData(Card);s.WriteLong(RequestId);base.Encode(s);}
    // The saved transfer must not be applied again when the client acknowledges it.
    public override byte Execute(GameMode mode)=>0;
}

namespace ClashRoyale.Logic.Commands;
using ClashRoyale.Extensions;
using ClashRoyale.Extensions.Helper;
using ClashRoyale.Files.Csv;
using ClashRoyale.Files.Csv.Logic;
using ClashRoyale.Logic.Mode;

// LogicChallengeCommand (509): its payload precedes the base command fields.
public sealed class CreateFriendlyChallengeCommand : Command
{
    public override int Type => 509;
    public string Text, PartnerToken, Extra;
    public bool Tournament, Shared, HasPartner, Executed, Handled;
    public GameModeData Mode;
    public CsvData Event;
    public long PartnerId;
    public int PartnerValue, ArenaIndex, ExtraValue;
    public override void Decode(ByteStream s)
    {
        Text=s.ReadString();Tournament=s.ReadBoolean();Shared=s.ReadBoolean();Mode=s.DecodeData<GameModeData>();
        HasPartner=s.ReadBoolean();if(HasPartner)PartnerId=s.ReadLong();
        PartnerToken=s.ReadString();PartnerValue=s.ReadVInt();ArenaIndex=s.ReadVInt();
        Extra=s.ReadString();ExtraValue=s.ReadVInt();Event=s.DecodeData();base.Decode(s);
    }
    public override void Encode(ChecksumEncoder s)
    {
        s.WriteString(Text);s.WriteBoolean(Tournament);s.WriteBoolean(Shared);s.EncodeData(Mode);
        s.WriteBoolean(HasPartner);if(HasPartner)s.WriteLong(PartnerId);
        s.WriteString(PartnerToken);s.WriteVInt(PartnerValue);s.WriteVInt(ArenaIndex);
        s.WriteString(Extra);s.WriteVInt(ExtraValue);s.EncodeData(Event);base.Encode(s);
    }
    public override byte Execute(GameMode mode)
    {
        if(!Executed){Executed=true;mode.CommandManager.CompletedFriendlyChallenges.Enqueue(this);}
        return 0;
    }
}

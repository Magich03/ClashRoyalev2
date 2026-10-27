namespace ClashRoyale.Logic.Alliance.Stream;
using ClashRoyale.Extensions;
using ClashRoyale.Extensions.Helper;
using ClashRoyale.Files.Csv;
using ClashRoyale.Files.Csv.Logic;
using ClashRoyale.Logic.Player;
using Newtonsoft.Json.Linq;

public sealed class ChallengeStreamEntry : StreamEntry
{
    public override int Type => 10;
    public long AcceptingAvatarId, HostPartnerId, OpponentPartnerId;
    public string HostPartner, OpponentPartner;
    public bool Team => Mode?.Players == "TvT";
    public string AcceptingAvatar, Message;
    public int SpectatorCount, SenderScore, EventId;
    public bool TournamentMode, Closed, MatchmakingParty;
    public GameModeData Mode;
    public ChallengeStreamEntry() { }
    public ChallengeStreamEntry(Player sender):base(sender){SenderScore=sender.Score;}
    public override void Encode(ChecksumEncoder s)
    {
        base.Encode(s);
        s.WriteString(Message);
        s.WriteBoolean(AcceptingAvatar!=null);if(AcceptingAvatar!=null)s.WriteString(AcceptingAvatar);
        s.WriteVInt(SenderScore);s.WriteBoolean(Closed);s.WriteBoolean(TournamentMode);s.WriteVInt(SpectatorCount);
        s.WriteBoolean(Team && !MatchmakingParty); // Native +0x54 distinguishes four-player friendlies from teammate requests.
        s.WriteBoolean(AcceptingAvatarId!=0);if(AcceptingAvatarId!=0)s.WriteLong(AcceptingAvatarId);
        s.EncodeData(Mode);
        if(Team)
        {
            s.WriteBoolean(false); // +0x57
            s.WriteBoolean(false); // Clan invitation carries player names/IDs, not an external party.
            if (!MatchmakingParty)
            {
            s.WriteString(HostPartner);s.WriteString(OpponentPartner);
            s.WriteBoolean(HostPartnerId!=0);if(HostPartnerId!=0)s.WriteLong(HostPartnerId);
            s.WriteBoolean(OpponentPartnerId!=0);if(OpponentPartnerId!=0)s.WriteLong(OpponentPartnerId);
            }
        }
        s.WriteBoolean(false); // Optional arena override.
        ClashRoyale.Logic.Event.LiveEventCatalog.EncodeBattleEvent(s, EventId);
    }
    public override void Load(JToken j)
    {
        base.Load(j);AcceptingAvatar=(string)j["accepting_avatar"];AcceptingAvatarId=(long?)j["accepting_id"]??0;
        Message=(string)j["message"];SenderScore=(int?)j["sender_score"]??0;SpectatorCount=0;
        TournamentMode=(bool?)j["tournament_mode"]??true;Mode=CsvFiles.GetWithGlobalId((int?)j["mode"]??0) as GameModeData ?? CsvFiles.Get(ClashRoyale.Enums.Gamefile.GameModes).GetData<GameModeData>("Friendly");
        HostPartner=(string)j["host_partner"];OpponentPartner=(string)j["opponent_partner"];HostPartnerId=(long?)j["host_partner_id"]??0;OpponentPartnerId=(long?)j["opponent_partner_id"]??0;
        // Invitations belong to live connections; never resurrect one after a server restart.
        EventId=(int?)j["event_id"]??0;Closed=true;MatchmakingParty=(bool?)j["matchmaking_party"]??false;
    }
    public override JObject Save()
    {
        var j=base.Save();j["accepting_avatar"]=AcceptingAvatar;j["accepting_id"]=AcceptingAvatarId;
        j["message"]=Message;j["sender_score"]=SenderScore;j["tournament_mode"]=TournamentMode;
        j["host_partner"]=HostPartner;j["opponent_partner"]=OpponentPartner;j["host_partner_id"]=HostPartnerId;j["opponent_partner_id"]=OpponentPartnerId;
        j["event_id"]=EventId;j["matchmaking_party"]=MatchmakingParty;j["closed"]=Closed;j["mode"]=Mode?.GlobalId;return j;
    }
}

namespace ClashRoyale.Handlers.Client.Alliance;
using System.Collections.Concurrent;
using ClashRoyale.Enums;
using ClashRoyale.Files.Csv;
using ClashRoyale.Files.Csv.Logic;
using ClashRoyale.Logic;
using ClashRoyale.Logic.Alliance;
using ClashRoyale.Logic.Alliance.Stream;
using ClashRoyale.Logic.Battle;
using ClashRoyale.Logic.Collections;
using ClashRoyale.Logic.Commands;
using ClashRoyale.Messages;
using ClashRoyale.Messages.Client.Alliance;
using ClashRoyale.Messages.Server.Alliance;

public static class ClanFriendlyBattleHandler
{
    private sealed record Invitation(Device Owner, Clan Clan, ChallengeStreamEntry Entry, ArenaData Arena, long Expires)
    {
        public Device[] Seats = new Device[4]; // Teams: 0/2 versus 1/3, matching battle simulation slots.
    }
    private static readonly SemaphoreSlim Gate=new(1,1);
    private static readonly ConcurrentDictionary<long,Invitation> Invitations=new();
    private static readonly Timer Pulse=new(state=>_ = Expire(),null,1000,1000);
    public static bool Contains(Device device)=>device.GameMode.Player!=null && Invitations.ContainsKey(device.GameMode.Player.PlayerId);
    private static bool Ready(Device d)=>d?.Token?.IsConnected==true && d.GameMode.State==HomeState.Home &&
        !MatchBattleSession.Contains(d) && !TrainingBattleSession.Contains(d) && !LadderMatchmaking.Contains(d) &&
        Enumerable.Range(0,8).Select(i=>d.GameMode.Home.SpellDeck[i]?.Data?.GlobalId ?? 0).Where(id=>id!=0).Distinct().Count()==8;
    private static bool Member(Invitation i,Device d)=>d!=null && i.Clan.Members.GetConnected(d.GameMode.Player.PlayerId)==d.GameMode.Player &&
        i.Clan.Members.ContainsKey(d.GameMode.Player.PlayerId) && d.GameMode.Player.ClanHighId==i.Clan.HighId && d.GameMode.Player.ClanLowId==i.Clan.LowId;
    private static bool Valid(Invitation i)=>(i.Entry.EventId==0 || (i.Owner.GameMode.Home.Challenge?.ModeId==i.Entry.EventId && !i.Owner.GameMode.Home.Challenge.Finished)) && i.Expires>Environment.TickCount64 && Ready(i.Owner) && Member(i,i.Owner) && !i.Entry.Removed && !i.Entry.Closed;
    private static void Failed(Device d)=>d.NetworkManager.SendMessage(new ChallengeFailedMessage());
    // Event sharing may omit the CSV mode; resolve it from the currently entered free event.
    public static int SharedEventId(CreateFriendlyChallengeCommand command,ClashRoyale.Logic.Event.ChallengeRun run)
    {
        if(run==null || !run.SingleMatch || run.Finished || run.RewardClaimed || run.GameMode==null)return 0;
        if(command.Event!=null && command.Event.GlobalId!=run.ModeId)return 0;
        // A generic clan friendly must not inherit an unrelated event left open at home.
        if(command.Tournament && command.Mode?.GlobalId!=run.GameMode.GlobalId && command.Event==null)return 0;
        if(command.Mode==null || command.Mode.GlobalId==run.GameMode.GlobalId ||
            (command.Shared && run.GameMode.Players=="TvT" && command.Mode.Name=="TeamVsTeamLadder"))return run.ModeId;
        return 0;
    }
    public static bool PrepareSharedEvent(Device device,int eventId)
    {
        var run=device.GameMode.Home.Challenge;
        if(run?.ModeId==eventId && !run.Finished && !run.RewardClaimed)return true;
        if(eventId/1000000!=73)return false;
        return new ChallengeCommand(541){ModeId=eventId}.Execute(device.GameMode)==0;
    }
    public static int SelectTeamSlot(bool[] occupied,int team)
    {
        if(occupied.Length!=4 || team<0 || team>1)return -1;
        // Client join choice 1 selects the host's side; 0 selects the opposing side.
        int side=1-team;
        return !occupied[side]?side:!occupied[side+2]?side+2:-1;
    }
    private static void UpdateEntry(Invitation i)
    {
        i.Entry.AcceptingAvatar=i.Seats[1]?.GameMode.Player.Name;i.Entry.AcceptingAvatarId=i.Seats[1]?.GameMode.Player.PlayerId??0;
        i.Entry.HostPartner=i.Seats[2]?.GameMode.Player.Name;i.Entry.HostPartnerId=i.Seats[2]?.GameMode.Player.PlayerId??0;
        i.Entry.OpponentPartner=i.Seats[3]?.GameMode.Player.Name;i.Entry.OpponentPartnerId=i.Seats[3]?.GameMode.Player.PlayerId??0;
    }
    private static async Task Broadcast(Invitation i)
    {
        UpdateEntry(i);await Clans.Save(i.Clan);i.Clan.Members.Broadcast(()=>new AllianceStreamEntryMessage(i.Entry));
    }
    public static async Task Create(Device device,CreateFriendlyChallengeCommand command)
    {
        await Gate.WaitAsync();
        try
        {
            var clan=await ClanHelper.GetClan(device.GameMode.Player);
            var active=device.GameMode.Home.Challenge;
            int eventId=SharedEventId(command,active);
            var mode=eventId!=0?active.GameMode:command.Mode??CsvFiles.Get(Gamefile.GameModes).GetData<GameModeData>("Friendly");
            Console.WriteLine($"[CLAN INVITE] mode={mode.Name} shared={command.Shared} tournament={command.Tournament} partner={command.HasPartner} rawMode={command.Mode?.Name??"null"} event={eventId}");
            bool party=mode.Name=="TeamVsTeamLadder" || (eventId!=0 && mode.Players=="TvT");
            if(clan==null || command.HasPartner || (command.Event!=null && eventId==0) || (eventId==0 && mode.Name!="Friendly" && mode.Name!="TeamVsTeam" && !party)) {Failed(device);return;}
            var arena=party || eventId!=0 || command.ArenaIndex<0?device.GameMode.Player.Arena:CsvFiles.Get(Gamefile.Arenas).Datas.OfType<ArenaData>().FirstOrDefault(a=>a.Instance==command.ArenaIndex);
            if(arena==null || !arena.IsInUse || arena.TrainingCamp || arena.PveArena || arena.Arena>device.GameMode.Player.Arena.Arena) {Failed(device);return;}
            var entry=new ChallengeStreamEntry(device.GameMode.Player){Message=(command.Text??"").Substring(0,Math.Min(command.Text?.Length??0,256)),TournamentMode=!party,Mode=mode,MatchmakingParty=party,EventId=eventId};
            var invite=new Invitation(device,clan,entry,arena,Environment.TickCount64+300000);invite.Seats[0]=device;
            if(!LadderMatchmaking.FriendlyTransition(()=>Ready(device) && Invitations.TryAdd(device.GameMode.Player.PlayerId,invite))) {Failed(device);return;}
            clan.Messages.AddEntry(entry);await Broadcast(invite);
        }
        finally {Gate.Release();}
    }
    public static async Task Accept(Device device,Message message,CancellationToken cancellation)
    {
        await Gate.WaitAsync(cancellation);
        try
        {
            var clan=await ClanHelper.GetClan(device.GameMode.Player);var request=(AcceptChallengeMessage)message;
            var invite=Invitations.Values.FirstOrDefault(i=>i.Entry.StreamId==request.EntryId && i.Clan==clan);
            if(invite==null || Contains(device)) {Failed(device);return;}
            if(invite.Entry.EventId!=0)
            {
                if(!Valid(invite) || !Ready(device) || !Member(invite,device) || !PrepareSharedEvent(device,invite.Entry.EventId)) {Failed(device);return;}
                if(await ClashRoyale.Database.Models.PlayerDb.Save(device.GameMode.Player)==null) {Failed(device);return;}
            }
            int seat=invite.Entry.MatchmakingParty?2:invite.Entry.Team?SelectTeamSlot(invite.Seats.Select(d=>d!=null).ToArray(),request.Slot):1;
            if(seat<0){Failed(device);return;}
            bool started=false;
            try
            {
                bool accepted=LadderMatchmaking.FriendlyTransition(()=>
                {
                    if(!Valid(invite) || !Ready(device) || !Member(invite,device) || invite.Seats[seat]!=null ||
                        invite.Seats.Any(d=>d!=null && (!Ready(d)||!Member(invite,d))))return false;
                    if(!Invitations.TryAdd(device.GameMode.Player.PlayerId,invite))return false;
                    invite.Seats[seat]=device;
                    if(invite.Entry.MatchmakingParty)
                    {
                        Invitations.TryRemove(invite.Owner.GameMode.Player.PlayerId,out _);
                        Invitations.TryRemove(device.GameMode.Player.PlayerId,out _);
                        if(!LadderMatchmaking.JoinParty(invite.Owner,device,invite.Entry.EventId!=0))
                        {
                            Invitations.TryAdd(invite.Owner.GameMode.Player.PlayerId,invite);
                            invite.Seats[seat]=null;
                            return false;
                        }
                        started=true;
                    }
                    else if(!invite.Entry.Team || invite.Seats.All(d=>d!=null))
                    {
                        if(invite.Entry.Team)MatchBattleSession.StartFriendlyTeam(invite.Seats.ToArray(),invite.Arena);
                        else if(invite.Entry.EventId!=0) MatchBattleSession.Start(invite.Owner,device,true);
                        else MatchBattleSession.StartFriendly(invite.Owner,device,invite.Arena);
                        started=true;
                    }
                    return true;
                });
                if(!accepted){Failed(device);return;}
            }
            catch(Exception error)
            {
                foreach(var participant in invite.Seats.Where(d=>d!=null))MatchBattleSession.Stop(participant);
                await Remove(invite,true);Failed(device);Logging.Error(typeof(ClanFriendlyBattleHandler),error.ToString());return;
            }
            Console.WriteLine($"[FRIENDLY] joined mode={invite.Entry.Mode.Name} side={request.Slot} slot={seat} started={started}");
            if(started)await Remove(invite,false);else await Broadcast(invite);
        }
        finally {Gate.Release();}
    }
    public static async Task Cancel(Device device,Message message,CancellationToken cancellation)
    {
        await Gate.WaitAsync(cancellation);
        try
        {
            if(Invitations.TryGetValue(device.GameMode.Player.PlayerId,out var i))
            {
                if(i.Owner==device){await Remove(i,true);return;}
                else {Leave(i,device);await Broadcast(i);}
            }
            device.NetworkManager.SendMessage(new CancelChallengeDoneMessage());
        }
        finally {Gate.Release();}
    }
    private static void Leave(Invitation i,Device d)
    {
        Invitations.TryRemove(d.GameMode.Player.PlayerId,out _);
        int seat=Array.IndexOf(i.Seats,d);if(seat>=0)i.Seats[seat]=null;
    }
    private static async Task Remove(Invitation i,bool acknowledge)
    {
        foreach(var d in i.Seats.Where(d=>d!=null))
        {
            Invitations.TryRemove(d.GameMode.Player.PlayerId,out _);
            if(acknowledge && d.Token?.IsConnected==true)d.NetworkManager.SendMessage(new CancelChallengeDoneMessage());
        }
        i.Entry.Closed=true;i.Clan.Messages.RemoveEntry(i.Entry);
        i.Clan.Members.Broadcast(()=>new AllianceStreamEntryMessage(i.Entry));await Clans.Save(i.Clan);
    }
    private static async Task Expire()
    {
        if(!await Gate.WaitAsync(0))return;
        try
        {
            foreach(var i in Invitations.Values.Distinct().ToArray())
            {
                if(!Valid(i)){await Remove(i,true);continue;}
                bool changed=false;
                foreach(var d in i.Seats.Skip(1).Where(d=>d!=null).ToArray())
                    if(!Ready(d)||!Member(i,d)){Leave(i,d);changed=true;}
                if(changed)await Broadcast(i);
            }
        }
        catch(Exception error){Logging.Error(typeof(ClanFriendlyBattleHandler),error.ToString());}
        finally {Gate.Release();}
    }
}

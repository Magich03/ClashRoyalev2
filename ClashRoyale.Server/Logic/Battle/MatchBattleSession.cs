namespace ClashRoyale.Logic.Battle
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using ClashRoyale.Logic.Commands;
    using ClashRoyale.Logic.Player;
    using LogicLong = ClashRoyale.Maths.LogicLong;
    using ClashRoyale.Messages.Server.Sector;

    // Orders commands for deterministic client simulation. This is not an authoritative combat simulator.
    public sealed class MatchBattleSession
    {
        private static readonly ConcurrentDictionary<Device, MatchBattleSession> Sessions = new ConcurrentDictionary<Device, MatchBattleSession>();
        private readonly object Gate = new object();
        private readonly Device[] Devices;
        private readonly Player[] Players;
        private readonly List<Command> Pending = new List<Command>();
        private readonly HashSet<Device> Departed = new HashSet<Device>();
        private MatchBotBrain[] BotBrains;
        private readonly bool Team;
        private readonly bool Challenge;
        private readonly int EventId;
        private ClashRoyale.Logic.Event.PickSelection Pick;
        private string[] ChallengeRunIds;
        private readonly ClashRoyale.Logic.Event.EventBattleRules Rules;
        private readonly int[] HumanSlots;
        private readonly bool HasBot;
        private Timer Timer;
        private int Turn;
        private bool Closed, Finished;
        private long ResultReadyAt;
        private const int ResultAnimationMilliseconds = 3500;
        private readonly int Seed = Random.Shared.Next(1, int.MaxValue);
        private readonly ClashRoyale.Files.Csv.Logic.ArenaData BattleArena;
        private ClashRoyale.Logic.Event.DraftSelection Draft;
        private int DraftFinishedTurn;
        private int TouchdownScore0, TouchdownScore1, TouchdownResumeTurn;
        private int OpeningTurns => !Rules.Touchdown ? 0 : Team && Draft == null ? 20 : 14;
        private readonly string MatchId = Guid.NewGuid().ToString("N");
        private readonly Dictionary<Device, (int C0, int C1)> Reports = new Dictionary<Device, (int, int)>();
        private System.Threading.Tasks.Task FinishTask;
        private ClashRoyale.Messages.Server.Avatar.BattleResultMessage[] Rewards;


        private MatchBattleSession(Device first, Device second, bool challenge = false)
        {
            Devices = second == null ? new[] { first } : new[] { first, second };
            Players = new[] { first.GameMode.Player, second?.GameMode.Player ?? MatchBotFactory.Create(first.GameMode.Player) };
            Challenge = challenge;
            EventId = challenge ? Devices[0].GameMode.Home.Challenge.ModeId : 0;
            var mode = challenge ? first.GameMode.Home.Challenge.GameMode : ClashRoyale.Files.Csv.CsvFiles.GameModeLadderData;
            BattleArena = BattleStateEncoder.ArenaFor(Players[0], mode, Seed, EventId);
            Rules = new ClashRoyale.Logic.Event.EventBattleRules(mode, BattleArena.PvPLocationData);
            if (challenge) for (int i = 0; i < Players.Length; i++) Players[i] = ChallengeBattle.Snapshot(Players[i]);
            HasBot = second == null;
            HumanSlots = second == null ? new[] {0} : new[] {0, 1};
        }
        private MatchBattleSession(Device[] humans, bool challenge = false)
        {
            if (humans.Length < 1 || humans.Length > 4 || humans.Any(d => d?.GameMode.Player == null) ||
                humans.Select(d => d.GameMode.Player.PlayerId).Distinct().Count() != humans.Length)
                throw new ArgumentException("2v2 needs one to four distinct human players");
            Team = true;
            Devices = humans;
            HumanSlots = humans.Length <= 2 ? new[] { 0, 2 }.Take(humans.Length).ToArray() : Enumerable.Range(0, humans.Length).ToArray();
            Players = new Player[4];
            for (int i = 0; i < humans.Length; i++) Players[HumanSlots[i]] = humans[i].GameMode.Player;
            for (int i = 0; i < 4; i++) if (Players[i] == null) Players[i] = MatchBotFactory.Create(Players[0]);
            Challenge = challenge;
            EventId = challenge ? Devices[0].GameMode.Home.Challenge.ModeId : 0;
            var mode = challenge ? humans[0].GameMode.Home.Challenge.GameMode : ClashRoyale.Files.Csv.CsvFiles.Get(ClashRoyale.Enums.Gamefile.GameModes).GetData<ClashRoyale.Files.Csv.Logic.GameModeData>("TeamVsTeamLadder");
            BattleArena = BattleStateEncoder.ArenaFor(Players[0], mode, Seed, EventId, true);
            Rules = new ClashRoyale.Logic.Event.EventBattleRules(mode, BattleArena.TeamVsTeamLocationData);
            if (challenge) for (int i=0;i<Players.Length;i++) Players[i]=ChallengeBattle.Snapshot(Players[i]);
            HasBot = humans.Length < 4;
        }
        public static void StartTeam(Device[] humans, bool challenge = false) => StartSession(new MatchBattleSession(humans, challenge));
        public static int ActiveCount => Sessions.Values.Distinct().Count(s => !s.Closed && !s.Finished);
        public static bool Contains(Device device) => Sessions.ContainsKey(device);
        public static void Start(Device first, Device second, bool challenge = false)
        {
            StartSession(new MatchBattleSession(first, second, challenge));
        }
        private static void StartSession(MatchBattleSession session)
        {
            session.ChallengeRunIds = session.Devices.Select(d => session.Challenge ? d.GameMode.Home.Challenge.Id : null).ToArray();
            int seed = session.Seed;
            if (session.Rules.Mode.DeckSelection == "Pick") session.Pick=new ClashRoyale.Logic.Event.PickSelection(seed);
            else if (session.Rules.Mode.DeckSelection == "Draft")
                {
                session.Draft = new ClashRoyale.Logic.Event.DraftSelection(session.Players.Length, seed, session.Rules.Touchdown, ClashRoyale.Logic.Event.LiveEventCatalog.Events.FirstOrDefault(e=>e.GlobalId==session.EventId)?.FeaturedCard);
                foreach(var participant in session.Players) participant.ExpLevel=9;
                }
            else if (session.Challenge) EventDeckBuilder.Prepare(session.Players, session.Rules.Mode, seed, ClashRoyale.Logic.Event.LiveEventCatalog.Events.FirstOrDefault(e=>e.GlobalId==session.EventId)?.FeaturedCard);
            session.BotBrains = new MatchBotBrain[session.Players.Length];
            for (int i = 0; i < session.Players.Length; i++)
                if (!session.HumanSlots.Contains(i)) session.BotBrains[i] = new MatchBotBrain(session.Players[i], seed ^ (i * 7919), session.Team ? 0.85 : 1.0, session.Team ? 0.70 : 1.0, session.Rules);
            byte[] state = session.Team ? BattleStateEncoder.EncodeTeamBattle(session.Players, seed, session.Rules.Mode, session.Draft, session.EventId) :
                BattleStateEncoder.EncodeMatchBattle(session.Players[0], session.Players[1], seed, session.Rules.Mode, session.Draft, session.EventId, session.Pick);
            foreach (var device in session.Devices)
            {
                TrainingBattleSession.Stop(device);
                if (!Sessions.TryAdd(device, session)) throw new InvalidOperationException("Player already in battle");
                device.NetworkManager.SendMessage(new SectorStateMessage(state));
            }
            Console.WriteLine($"[EVENT] battle={session.MatchId} event={session.EventId} mode={session.Rules.Mode.Name} arena={session.BattleArena.Name} players={session.Players.Length} draft={session.Draft != null} elixir={session.Rules.ElixirPerSecond(0):F3}");
            session.Timer = new Timer(_ => session.Heartbeat(), null, 500, 500);
            Logging.Info(typeof(MatchBattleSession), "Started " + (session.Team ? "2v2 " : "1v1 ") + (session.HasBot ? "bot" : "human") + " match: " + session.Players[0].Name + " vs " + session.Players[1].Name);
        }
        public static void Stop(Device device)
        {
            if (!Sessions.TryRemove(device, out var session)) return;
            lock (session.Gate)
            {
                session.Departed.Add(device);
                if (session.Devices.All(d => session.Departed.Contains(d)))
                {
                    session.Closed = true;
                    session.Timer?.Dispose();
                }
            }
        }
        public static bool AddCommand(Device device, Command command)
        {
            if (!Sessions.TryGetValue(device, out var session)) return false;
            lock (session.Gate)
            {
                if (session.Closed || session.Finished || session.Departed.Contains(device)) return true;
                if (command is PickChooseCommand pickChoice)
                {
                    int slot=session.HumanSlots[Array.IndexOf(session.Devices,device)];
                    session.ChoosePick(slot,pickChoice.Choice); return true;
                }
                if (session.Pick != null && (!session.Pick.Complete || session.Turn < session.DraftFinishedTurn+8)) return true;
                if (command is DraftChooseCommand draftChoice)
                {
                    int slot = session.HumanSlots[Array.IndexOf(session.Devices, device)];
                    session.ChooseDraft(slot, draftChoice.Round, draftChoice.Choice);
                    return true;
                }
                if (session.Draft != null && (!session.Draft.Complete || session.Turn < session.DraftFinishedTurn + 8)) return true;
                // The connection owns the executor identity. Never accept an opponent identity from a client.
                if (command is PlaceCardCommand card && card.HandIndex >= 0 && card.HandIndex < 8 && card.X >= 0 && card.X <= 18000 && card.Y >= 0 && card.Y <= 32000 && session.Pending.Count < 64)
                {
                    int deviceIndex = Array.IndexOf(session.Devices, device);
                    var data = session.Players[session.HumanSlots[deviceIndex]].Home.SpellDeck[card.HandIndex]?.Data;
                    if (data == null || (card.DataClass != 0 && (card.DataClass != data.GlobalId / 1000000 || card.DataInstance != data.Instance))) return true;
                    device.GameMode.Home.Quests.Cast(data, session.Team, session.Challenge);
                    // Snapshot the public deployment; client-supplied levels and opponent IDs cannot alter it.
                    session.Pending.Add(new PlaceCardCommand {
                        ExecutorId = new LogicLong(device.GameMode.Player.HighId, device.GameMode.Player.LowId),
                        TickWhenGiven = card.TickWhenGiven, HandIndex = card.HandIndex,
                        DataClass = data.GlobalId / 1000000, DataInstance = data.Instance,
                        Value = card.Value, HasCard = false, X = card.X, Y = card.Y
                    });
                }
                return true;
            }
        }
        public static async System.Threading.Tasks.Task<bool> ReceiveResult(Device device, string text)
        {
            const string prefix = "ASTRAL_RESULT_V1:";
            if (text == null || !text.StartsWith(prefix) || !Sessions.TryGetValue(device, out var session)) return false;
            if (text.Length > 256) return true;
            System.Threading.Tasks.Task task = null;
            lock (session.Gate)
            {
                if (session.Closed || session.Departed.Contains(device)) return true;
                Newtonsoft.Json.Linq.JObject report;
                try { report = Newtonsoft.Json.Linq.JObject.Parse(text.Substring(prefix.Length)); }
                catch { return true; }
                int seed, tick, c0, c1, paused;
                try { seed=(int)report["seed"]; tick=(int)report["tick"]; c0=(int)report["c0"]; c1=(int)report["c1"]; paused=(int?)report["paused"] ?? 0; }
                catch { return true; }
                if (session.Challenge && session.Devices.Where((d,i) => d.GameMode.Home.Challenge?.Id != session.ChallengeRunIds[i]).Any()) return true;
                if (seed != session.Seed || tick < 100 || tick > session.Turn*10+20 || c0<0 || c0>3 || c1<0 || c1>3 || (c0==3 && c1==3)) return true;
                if (paused < 0 || paused > tick || paused > (session.Rules.Touchdown ? 1200 : 0)) return true;
                if (session.Rules.Touchdown && !session.Finished && !session.Rules.IsFinished(tick - paused - session.DraftFinishedTurn * 10, c0, c1))
                {
                    // A round report does not complete the match or award anything.
                    if (session.Turn*10-tick > 200 || c0 < session.TouchdownScore0 || c1 < session.TouchdownScore1 ||
                        c0+c1 <= session.TouchdownScore0+session.TouchdownScore1) return true;
                    session.TouchdownScore0=c0; session.TouchdownScore1=c1;
                    session.TouchdownResumeTurn=session.Turn+4;
                    foreach(var brain in session.BotBrains) brain?.ResetTouchdownRound(session.Turn-session.DraftFinishedTurn-session.OpeningTurns);
                    Console.WriteLine($"[EVENT] touchdown round score={c0}-{c1} match={session.MatchId}");
                    return true;
                }
                if (!session.Rules.IsFinished(tick - paused - session.DraftFinishedTurn * 10, c0, c1)) return true;
                if (!session.Finished)
                {
                    if (session.Turn*10-tick > 200) return true;
                    session.Reports[device]=(c0,c1);
                    if (session.Reports.Count != session.Devices.Length || session.Reports.Values.Any(r => r != (c0,c1))) return true;
                    session.Finished=true;
                    session.ResultReadyAt = Environment.TickCount64 + ResultAnimationMilliseconds;
                    session.Pending.Clear();
                    session.Rewards = new ClashRoyale.Messages.Server.Avatar.BattleResultMessage[session.Devices.Length];
                    for(int i=0;i<session.Devices.Length;i++)
                        session.Rewards[i]=session.Challenge
                            ? ChallengeBattle.Apply(session.Devices[i].GameMode.Player, session.MatchId, session.HumanSlots[i]%2==0?c0:c1, session.HumanSlots[i]%2==0?c1:c0)
                            : MatchReward.Apply(session.Players[session.HumanSlots[i]], session.MatchId, session.HumanSlots[i]%2==0?c0:c1, session.HumanSlots[i]%2==0?c1:c0, session.Team);
                    Logging.Info(typeof(MatchBattleSession), "Result agreed: " + c0 + "-" + c1 + "; match " + session.MatchId);
                }
                if (session.FinishTask == null || session.FinishTask.IsFaulted)
                    session.FinishTask=session.SaveResults();
                task=session.FinishTask;
            }
            await task;
            return true;
        }
        private async System.Threading.Tasks.Task SaveResults()
        {
            for(int i=0;i<Devices.Length;i++)
                if(Rewards[i]!=null) await ClashRoyale.Logic.Alliance.ClanChestService.Contribute(Devices[i].GameMode.Player, MatchId, Rewards[i].OwnCrowns);
            for(int i=0;i<Devices.Length;i++)
            {
                if (await ClashRoyale.Database.Models.PlayerDb.Save(Devices[i].GameMode.Player) == null)
                    throw new InvalidOperationException("Could not persist battle reward");
            }
            // Keep simulation ticks flowing while the last tower and crown animate.
            int remaining = (int)Math.Max(0, ResultReadyAt - Environment.TickCount64);
            if (remaining > 0) await System.Threading.Tasks.Task.Delay(remaining);
            lock (Gate)
            {
                Timer?.Dispose();
                for(int i=0;i<Devices.Length;i++)
                    if (!Closed && Rewards[i] != null && !Departed.Contains(Devices[i])) Devices[i].NetworkManager.SendMessage(Rewards[i]);
            }
        }
        public static async System.Threading.Tasks.Task WaitForResult(Device device)
        {
            if (Sessions.TryGetValue(device,out var session) && session.FinishTask != null) await session.FinishTask;
        }
        private void ChooseDraft(int slot, int round, int choice)
        {
            if (Draft == null || !Draft.Choose(slot,round,choice)) return;
            Pending.Add(new DraftChooseCommand { Round=round, Choice=choice, TickWhenGiven=Turn*10,
                ExecutorId=new LogicLong(Players[slot].HighId,Players[slot].LowId) });
            if (!Draft.Complete) return;
            DraftFinishedTurn=Turn;
            for(int i=0;i<Players.Length;i++)
            {
                var cards=Draft.Deck(i);
                Players[i].ExpLevel=9;
                for(int j=0;j<8;j++) Players[i].Home.SpellDeck.SetSpell(j,new ClashRoyale.Logic.Home.Spells.Spell(cards[j]) {
                    Level=Math.Max(0,8-cards[j].RarityData.RelativeLevel) });
            }
        }
        private void ChoosePick(int slot,int choice)
        {
            if(Pick == null || !Pick.Choose(slot,choice)) return;
            Pending.Add(new PickChooseCommand { Choice=choice,TickWhenGiven=Turn*10,
                ExecutorId=new LogicLong(Players[slot].HighId,Players[slot].LowId) });
            if(!Pick.Complete) return;
            DraftFinishedTurn=Turn;
            for(int i=0;i<Players.Length;i++) {
                Players[i].ExpLevel=9;
                var cards=Pick.Deck(i);
                for(int j=0;j<8;j++) Players[i].Home.SpellDeck.SetSpell(j,new ClashRoyale.Logic.Home.Spells.Spell(cards[j]) {Level=Math.Max(0,8-cards[j].RarityData.RelativeLevel)});
            }
        }
        private void Heartbeat()
        {
            lock (Gate)
            {
                if (Closed) return;
                try
                {
                    if (Finished)
                    {
                        foreach (var device in Devices)
                            if (!Departed.Contains(device)) device.NetworkManager.SendMessage(new SectorHearbeatMessage(Turn, 0, Array.Empty<Command>()));
                        Turn++;
                        return;
                    }
                    foreach (var device in Devices)
                        if (device.Token == null || !device.Token.IsConnected) Stop(device);
                    if (Closed || Finished) return;
                    if (Turn >= (Rules.MaxSeconds + 90) * 2) // Include the selection phase and result animation.
                    {
                        foreach (var device in Devices) Stop(device);
                        return;
                    }
                    if(Pick != null && !Pick.Complete)
                        for(int slot=0;slot<Players.Length;slot++)
                            if(Turn >= (HumanSlots.Contains(slot)?60:4) && Pick.Count(slot)<8) ChoosePick(slot,Pick.NextChoice(slot));
                    if (Draft != null && !Draft.Complete)
                    {
                        for (int slot=0;slot<Players.Length;slot++)
                            if (Turn >= (HumanSlots.Contains(slot) ? 48 : 4))
                                for (int round=0;round<4;round++)
                                    if (Draft.Choice(slot,round)<0) { ChooseDraft(slot,round,(Seed+slot+round)&1); break; }
                    }
                    if (HasBot && (Pick == null || Pick.Complete && Turn >= DraftFinishedTurn+8) && Turn >= DraftFinishedTurn + OpeningTurns && Turn >= TouchdownResumeTurn && (Draft == null || Draft.Complete && Turn >= DraftFinishedTurn + 8))
                    {
                        for (int bot = 0; bot < BotBrains.Length; bot++)
                        {
                            var brain = BotBrains[bot]; if (brain == null) continue;
                            foreach (var card in Pending.OfType<PlaceCardCommand>().ToArray())
                            {
                                int owner = Array.FindIndex(Players, p => p.HighId == card.ExecutorId.HigherInt && p.LowId == card.ExecutorId.LowerInt);
                                if (owner < 0 || owner % 2 == bot % 2) continue;
                                var observed = new PlaceCardCommand { X = card.X, Y = bot % 2 == 0 ? 32000 - card.Y : card.Y };
                                brain.Observe(observed, Players[owner].Home.SpellDeck[card.HandIndex].Data, Turn - DraftFinishedTurn - OpeningTurns);
                            }
                            var botCard = brain.Think(Turn - DraftFinishedTurn - OpeningTurns);
                            if (botCard != null) { if (bot % 2 == 0) botCard.Y = 32000 - botCard.Y; Pending.Add(botCard); }
                        }
                    }
                    var commands = Pending.ToArray();
                    Pending.Clear();
                    foreach (var command in commands) command.ExecuteTick = Turn * 10;
                    foreach (var device in Devices)
                        if (!Departed.Contains(device)) device.NetworkManager.SendMessage(new SectorHearbeatMessage(Turn, 0, commands));
                    Turn++;
                }
                catch (Exception error)
                {
                    Logging.Error(typeof(MatchBattleSession), error.ToString());
                    foreach (var device in Devices) Stop(device);
                }
            }
        }
    }
}

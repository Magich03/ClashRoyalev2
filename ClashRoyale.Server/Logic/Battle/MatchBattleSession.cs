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

    // Orders commands and, when enabled, runs the same battle in an isolated native worker.
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
        private readonly bool Friendly;
        private readonly int EventId;
        private ClashRoyale.Logic.Event.PickSelection Pick;
        private string[] ChallengeRunIds;
        private readonly ClashRoyale.Logic.Event.EventBattleRules Rules;
        private readonly int[] HumanSlots;
        private readonly bool HasBot;
        private Timer Timer;
        private NativeBattleHost Native;
        private readonly Dictionary<Device, long> LastStateRequest = new();
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


        private MatchBattleSession(Device first, Device second, bool challenge = false, ClashRoyale.Files.Csv.Logic.ArenaData friendlyArena = null)
        {
            Friendly = friendlyArena != null;
            Devices = second == null ? new[] { first } : new[] { first, second };
            Players = new[] { first.GameMode.Player, second?.GameMode.Player ?? MatchBotFactory.Create(first.GameMode.Player) };
            Challenge = challenge;
            EventId = challenge ? Devices[0].GameMode.Home.Challenge.ModeId : 0;
            var mode = Friendly ? ClashRoyale.Files.Csv.CsvFiles.Get(ClashRoyale.Enums.Gamefile.GameModes).GetData<ClashRoyale.Files.Csv.Logic.GameModeData>("Friendly") : challenge ? first.GameMode.Home.Challenge.GameMode : ClashRoyale.Files.Csv.CsvFiles.GameModeLadderData;
            if (Friendly) for(int i=0;i<Players.Length;i++) { Players[i]=ChallengeBattle.Snapshot(Players[i]); Players[i].Arena=friendlyArena; }
            BattleArena = BattleStateEncoder.ArenaFor(Players[0], mode, Seed, EventId);
            Rules = new ClashRoyale.Logic.Event.EventBattleRules(mode, BattleArena.PvPLocationData);
            if (challenge) for (int i = 0; i < Players.Length; i++) Players[i] = ChallengeBattle.Snapshot(Players[i]);
            HasBot = second == null;
            HumanSlots = second == null ? new[] {0} : new[] {0, 1};
        }
        private MatchBattleSession(Device[] humans, bool challenge = false, ClashRoyale.Files.Csv.Logic.ArenaData friendlyArena = null, int[] slots = null)
        {
            if (humans.Length < 1 || humans.Length > 4 || humans.Any(d => d?.GameMode.Player == null) ||
                humans.Select(d => d.GameMode.Player.PlayerId).Distinct().Count() != humans.Length)
                throw new ArgumentException("2v2 needs one to four distinct human players");
            Team = true;
            Friendly = friendlyArena != null;
            if(Friendly && humans.Length!=4)throw new ArgumentException("Friendly 2v2 requires four humans");
            Devices = humans;
            HumanSlots = slots?.ToArray() ?? Enumerable.Range(0, humans.Length).ToArray();
            if (HumanSlots.Length != humans.Length || HumanSlots.Any(s => s < 0 || s > 3) || HumanSlots.Distinct().Count() != humans.Length) throw new ArgumentException("Invalid human battle slots");
            Players = new Player[4];
            for (int i = 0; i < humans.Length; i++) Players[HumanSlots[i]] = humans[i].GameMode.Player;
            for (int i = 0; i < 4; i++) if (Players[i] == null) Players[i] = MatchBotFactory.Create(Players[0]);
            Challenge = challenge;
            EventId = challenge ? Devices[0].GameMode.Home.Challenge.ModeId : 0;
            var mode = Friendly ? ClashRoyale.Files.Csv.CsvFiles.Get(ClashRoyale.Enums.Gamefile.GameModes).GetData<ClashRoyale.Files.Csv.Logic.GameModeData>("TeamVsTeam") : challenge ? humans[0].GameMode.Home.Challenge.GameMode : ClashRoyale.Files.Csv.CsvFiles.Get(ClashRoyale.Enums.Gamefile.GameModes).GetData<ClashRoyale.Files.Csv.Logic.GameModeData>("TeamVsTeamLadder");
            if(Friendly)for(int i=0;i<4;i++){Players[i]=ChallengeBattle.Snapshot(Players[i]);Players[i].Arena=friendlyArena;}
            BattleArena = BattleStateEncoder.ArenaFor(Players[0], mode, Seed, EventId, true);
            Rules = new ClashRoyale.Logic.Event.EventBattleRules(mode, BattleArena.TeamVsTeamLocationData);
            if (challenge) for (int i=0;i<Players.Length;i++) Players[i]=ChallengeBattle.Snapshot(Players[i]);
            HasBot = humans.Length < 4;
        }
        public static void StartFriendly(Device first, Device second, ClashRoyale.Files.Csv.Logic.ArenaData arena) => StartSession(new MatchBattleSession(first, second, false, arena));
        public static void StartFriendlyTeam(Device[] humans, ClashRoyale.Files.Csv.Logic.ArenaData arena) => StartSession(new MatchBattleSession(humans,false,arena));
        public static void StartTeam(Device[] humans, bool challenge = false, int[] slots = null) => StartSession(new MatchBattleSession(humans, challenge, null, slots));
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
            else if (session.Challenge || session.Friendly) EventDeckBuilder.Prepare(session.Players, session.Rules.Mode, seed, ClashRoyale.Logic.Event.LiveEventCatalog.Events.FirstOrDefault(e=>e.GlobalId==session.EventId)?.FeaturedCard);
            session.BotBrains = new MatchBotBrain[session.Players.Length];
            for (int i = 0; i < session.Players.Length; i++)
                if (!session.HumanSlots.Contains(i)) session.BotBrains[i] = new MatchBotBrain(session.Players[i], seed ^ (i * 7919), session.Team ? 0.85 : 1.0, session.Team ? 0.70 : 1.0, session.Rules);
            byte[] state = session.Team ? BattleStateEncoder.EncodeTeamBattle(session.Players, seed, session.Rules.Mode, session.Draft, session.EventId) :
                BattleStateEncoder.EncodeMatchBattle(session.Players[0], session.Players[1], seed, session.Rules.Mode, session.Draft, session.EventId, session.Pick);
            var registered = new List<Device>();
            try
            {
                if (NativeBattleHost.Enabled && session.Draft == null && session.Pick == null)
                {
                    session.Native = new NativeBattleHost(state);
                    Console.WriteLine($"[NATIVE] started match={session.MatchId} mode={session.Rules.Mode.Name}");
                }
                foreach (var device in session.Devices)
                {
                    if (!Sessions.TryAdd(device, session)) throw new InvalidOperationException("Player already in battle");
                    registered.Add(device);
                }
                foreach (var device in session.Devices)
                {
                    TrainingBattleSession.Stop(device);
                    device.NetworkManager.SendMessage(new SectorStateMessage(state));
                }
            }
            catch
            {
                session.Native?.Dispose();
                foreach (var device in registered) Sessions.TryRemove(device, out _);
                throw;
            }
            Console.WriteLine($"[EVENT] battle={session.MatchId} event={session.EventId} mode={session.Rules.Mode.Name} arena={session.BattleArena.Name} players={session.Players.Length} draft={session.Draft != null} elixir={session.Rules.ElixirPerSecond(0):F3}");
            session.Timer = new Timer(_ => session.Heartbeat(), null, 500, 500);
            Logging.Info(typeof(MatchBattleSession), "Started " + (session.Team ? "2v2 " : "1v1 ") + (session.HasBot ? "bot" : "human") + " match: " + session.Players[0].Name + " vs " + session.Players[1].Name);
        }
        public static void Stop(Device device, bool rematching = false)
        {
            if (!Sessions.TryRemove(device, out var session)) return;
            lock (session.Gate)
            {
                if (session.Team && session.Finished && !rematching)
                {
                    session.RematchReady.Remove(device);
                    session.NotifyRematch(device, false);
                }
                session.Departed.Add(device);
                if (session.Devices.All(d => session.Departed.Contains(d)))
                {
                    session.Closed = true;
                    session.Timer?.Dispose();
                    session.Native?.Dispose();
                }
            }
        }
        private readonly Dictionary<int, Dictionary<Device, int>> ClientChecksums = new Dictionary<int, Dictionary<Device, int>>();
        private int ChecksumWarnings, ResultDiagnostics, CardDiagnostics;
        public static void ObserveClientState(Device device, int tick, int checksum)
        {
            if (!Sessions.TryGetValue(device, out var session) || checksum == 0 || tick < 0) return;
            lock (session.Gate)
            {
                if (session.Closed || session.Finished) return;
                if (session.Native != null && session.Native.Checksums.TryGetValue(tick, out uint authoritative))
                {
                    if (unchecked((uint)checksum) != authoritative && session.ChecksumWarnings++ < 20)
                        Console.WriteLine($"[NATIVE-SYNC] match={session.MatchId} player={device.GameMode.Player.PlayerId} tick={tick} client={unchecked((uint)checksum)} server={authoritative}");
                    else if (unchecked((uint)checksum) == authoritative && session.CardDiagnostics++ < 5)
                        Console.WriteLine($"[NATIVE-SYNC] matched match={session.MatchId} tick={tick}");
                }
                if (!session.ClientChecksums.TryGetValue(tick, out var reports)) session.ClientChecksums[tick] = reports = new Dictionary<Device, int>();
                reports[device] = checksum;
                if (reports.Count > 1 && reports.Values.Distinct().Count() > 1 && session.ChecksumWarnings++ < 5)
                    Console.WriteLine($"[SYNC] match={session.MatchId} tick={tick} reports={string.Join(",", reports.Select(r => r.Key.GameMode.Player.PlayerId + ":" + r.Value))}");
                foreach (var old in session.ClientChecksums.Keys.OrderByDescending(t => t).Skip(32).ToArray()) session.ClientChecksums.Remove(old);
            }
        }

        public static bool SendNativeState(Device device, int clientTick)
        {
            if (!Sessions.TryGetValue(device, out var session)) return false;
            lock (session.Gate)
            {
                if (session.Native == null || session.Closed || session.Finished || session.Departed.Contains(device) || clientTick < 0) return false;
                long now = Environment.TickCount64;
                if (session.LastStateRequest.TryGetValue(device, out long last) && now - last < 2000) return true;
                var snapshot = session.Native.GetSnapshot();
                session.LastStateRequest[device] = now;
                device.NetworkManager.SendMessage(new SectorStateMessage(snapshot.State));
                Console.WriteLine($"[NATIVE] state refresh match={session.MatchId} clientTick={clientTick} serverTick={snapshot.Status.Tick}");
                return true;
            }
        }

        private readonly HashSet<Device> RematchReady = new HashSet<Device>();
        private void NotifyRematch(Device sender, bool ready)
        {
            int index = Array.IndexOf(Devices, sender);
            if (index < 0) return;
            for (int i = 0; i < Devices.Length; i++)
                if (HumanSlots[i] % 2 == HumanSlots[index] % 2 && !Departed.Contains(Devices[i]))
                    Devices[i].NetworkManager.SendMessage(new ClashRoyale.Messages.Server.Attack.RematchMessage {
                        Ready = ready, HighId = sender.GameMode.Player.HighId, LowId = sender.GameMode.Player.LowId
                    });
        }
        public static async System.Threading.Tasks.Task<int> Rematch(Device device, bool ready)
        {
            if (!Sessions.TryGetValue(device, out var session)) return 0;
            System.Threading.Tasks.Task saved;
            lock (session.Gate)
            {
                if (!session.Team || session.Friendly || session.Challenge || !session.Finished || session.Closed || session.Departed.Contains(device)) return 0;
                saved = session.FinishTask;
            }
            if (saved == null) return 0;
            await saved;
            int result = 0;
            // Queue -> session is the same lock order used when starting matches.
            LadderMatchmaking.FriendlyTransition(() =>
            {
                lock (session.Gate)
                {
                    if (!Sessions.TryGetValue(device, out var current) || current != session || session.Closed || session.Departed.Contains(device)) return false;
                    bool changed = ready ? session.RematchReady.Add(device) : session.RematchReady.Remove(device);
                    if (changed) session.NotifyRematch(device, ready);
                    result = 1;
                    if (!ready) return true;
                    int index = Array.IndexOf(session.Devices, device);
                    var teammate = session.Devices.Where((d, i) => d != device && session.HumanSlots[i] % 2 == session.HumanSlots[index] % 2 && !session.Departed.Contains(d)).FirstOrDefault();
                    if (teammate != null && !session.RematchReady.Contains(teammate)) return true;
                    // Release this side only. The other team can finish viewing its result or request its own rematch.
                    session.RematchReady.Remove(device);
                    if (teammate != null) session.RematchReady.Remove(teammate);
                    Stop(device, true);
                    if (teammate != null) Stop(teammate, true);
                    bool queued;
                    if (teammate != null) queued = LadderMatchmaking.JoinParty(device, teammate);
                    else { LadderMatchmaking.Join(device, true); queued = LadderMatchmaking.Contains(device); }
                    result = queued ? 2 : 0;
                    if (!queued)
                        foreach (var d in teammate == null ? new[] { device } : new[] { device, teammate })
                            d.NetworkManager.SendMessage(new ClashRoyale.Messages.Server.Matchmaking.MatchmakeFailedMessage());
                    Console.WriteLine($"[REMATCH] queued={queued} players={device.GameMode.Player.PlayerId},{teammate?.GameMode.Player.PlayerId}");
                    return queued;
                }
            });
            return result;
        }

        private readonly Dictionary<Device, long> LastBattleEvent = new Dictionary<Device, long>();

        // Emotes and quick chat belong only to the authenticated sender's current match.
        public static int RelayBattleEvent(Device device, ClashRoyale.Logic.Battle.Event.BattleEvent battleEvent)
        {
            if (battleEvent == null || !Sessions.TryGetValue(device, out var session)) return 0;
            lock (session.Gate)
            {
                // A finished battle still owns its result-screen chat until players return home.
                if (session.Closed || session.Departed.Contains(device)) return 0;
                if (battleEvent.Type > 8 || battleEvent.Ticks == null || battleEvent.Coords == null || battleEvent.Params == null ||
                    battleEvent.Ticks.Count > 256 || battleEvent.Coords.Count > 256 || battleEvent.Params.Count > 256 ||
                    (battleEvent.Text?.Length ?? 0) > 256 || (battleEvent.Text?.StartsWith("ASTRAL_RESULT_V1:") ?? false)) return 0;
                long now = Environment.TickCount64;
                if (session.LastBattleEvent.TryGetValue(device, out long last) && now - last < 500) return 0;
                session.LastBattleEvent[device] = now;
                var outgoing = new ClashRoyale.Logic.Battle.Event.BattleEvent(battleEvent.Type) {
                    HighId = device.GameMode.Player.HighId, LowId = device.GameMode.Player.LowId,
                    Text = battleEvent.Text, Flag = battleEvent.Flag,
                    Ticks = new List<int>(battleEvent.Ticks), Coords = new List<int>(battleEvent.Coords), Params = new List<int>(battleEvent.Params)
                };
                int recipients = 0;
                foreach (var recipient in session.Devices)
                {
                    if (recipient == device || session.Departed.Contains(recipient)) continue;
                    recipient.NetworkManager.SendMessage(new ClashRoyale.Messages.Server.Attack.BattleEventMessage(outgoing));
                    recipients++;
                }
                return recipients;
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
                    if (data == null || (card.DataClass != 0 && (card.DataClass != data.GlobalId / 1000000 || card.DataInstance != data.Instance)))
                    {
                        if (session.CardDiagnostics++ < 12) Console.WriteLine($"[CARD] rejected match={session.MatchId} player={device.GameMode.Player.PlayerId} index={card.HandIndex} received={card.DataClass}:{card.DataInstance} expected={data?.GlobalId}");
                        return true;
                    }
                    if (!session.Friendly) device.GameMode.Home.Quests.Cast(data, session.Team, session.Challenge);
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
                // Results and round transitions belong to the native simulation, not client reports.
                if (session.Native != null) return true;
                Newtonsoft.Json.Linq.JObject report;
                try { report = Newtonsoft.Json.Linq.JObject.Parse(text.Substring(prefix.Length)); }
                catch { return true; }
                int seed, tick, c0, c1, paused;
                try { seed=(int)report["seed"]; tick=(int)report["tick"]; c0=(int)report["c0"]; c1=(int)report["c1"]; paused=(int?)report["paused"] ?? 0; }
                catch { return true; }
                if (session.ResultDiagnostics++ < 24)
                    Console.WriteLine($"[RESULT] match={session.MatchId} player={device.GameMode.Player.PlayerId} seedOK={seed==session.Seed} tick={tick} serverTick={session.Turn*10} paused={paused} score={c0}-{c1} departed={session.Departed.Count}");
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
                    // A terminal report remains terminal even when rendering or transport lags.
                    // The seed, clock bounds, event rules and agreement checks still apply.
                    session.Reports[device]=(c0,c1);
                    if (!ResultsAgree(session.Devices.Where(d => !session.Departed.Contains(d)), session.Reports, c0, c1))
                    {
                        Console.WriteLine($"[RESULT] waiting match={session.MatchId} reports={string.Join(",", session.Reports.Select(r => r.Key.GameMode.Player.PlayerId + ":" + r.Value.C0 + "-" + r.Value.C1))}");
                        return true;
                    }
                    session.Finished=true;
                    session.ResultReadyAt = Environment.TickCount64 + ResultAnimationMilliseconds;
                    session.Pending.Clear();
                    session.Rewards = new ClashRoyale.Messages.Server.Avatar.BattleResultMessage[session.Devices.Length];
                    for(int i=0;i<session.Devices.Length;i++)
                        session.Rewards[i]=session.Friendly
                            ? FriendlyResult(session.HumanSlots[i]%2==0?c0:c1, session.HumanSlots[i]%2==0?c1:c0)
                            : session.Challenge
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
        public static bool ResultsAgree(IEnumerable<Device> active, IReadOnlyDictionary<Device, (int C0, int C1)> reports, int first, int second)
        {
            var participants = active.ToArray();
            return participants.Length > 0 && participants.All(d => reports.TryGetValue(d, out var score) && score == (first, second));
        }
        public static ClashRoyale.Messages.Server.Avatar.BattleResultMessage FriendlyResult(int own, int enemy) => new() { Result=own>enemy?1:own<enemy?2:3, OwnCrowns=own, OpponentCrowns=enemy };
        private async System.Threading.Tasks.Task SaveResults()
        {
            for(int i=0;i<Devices.Length;i++)
                if(!Friendly && Rewards[i]!=null) await ClashRoyale.Logic.Alliance.ClanChestService.Contribute(Devices[i].GameMode.Player, MatchId, Rewards[i].OwnCrowns);
            for(int i=0;i<Devices.Length;i++)
            {
                if (!Friendly && await ClashRoyale.Database.Models.PlayerDb.Save(Devices[i].GameMode.Player) == null)
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
            // A slow native tick must not accumulate concurrent timer callbacks.
            if (!Monitor.TryEnter(Gate)) return;
            try
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
                    if (Friendly && Departed.Count > 0)
                    {
                        Finished=true;
                        ResultReadyAt=Environment.TickCount64+ResultAnimationMilliseconds;
                        Pending.Clear();
                        var losingTeams=Devices.Select((d,i)=>(d,i)).Where(x=>Departed.Contains(x.d)).Select(x=>HumanSlots[x.i]%2).ToHashSet();
                        Rewards=Devices.Select((d,i)=>losingTeams.Count==2?FriendlyResult(0,0):losingTeams.Contains(HumanSlots[i]%2)?FriendlyResult(0,3):FriendlyResult(3,0)).ToArray();
                        FinishTask=SaveResults();
                        return;
                    }
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
                    if (Native != null)
                    {
                        Native.Step((Turn + 1) * 10, commands);
                        var status = Native.Current;
                        if (Rules.Touchdown && status.Side0Crowns + status.Side1Crowns > TouchdownScore0 + TouchdownScore1)
                        {
                            TouchdownScore0 = status.Side0Crowns; TouchdownScore1 = status.Side1Crowns;
                            TouchdownResumeTurn = Turn + 20;
                            foreach (var brain in BotBrains) brain?.ResetTouchdownRound(Turn - OpeningTurns);
                            Console.WriteLine($"[NATIVE] touchdown match={MatchId} tick={status.Tick} score={TouchdownScore0}-{TouchdownScore1}");
                        }
                    }
                    foreach (var device in Devices)
                        if (!Departed.Contains(device)) device.NetworkManager.SendMessage(new SectorHearbeatMessage(Turn, 0, commands));
                    Turn++;
                    if (Native?.Current.Finished == true) CompleteNativeBattle();
                }
                catch (Exception error)
                {
                    Logging.Error(typeof(MatchBattleSession), error.ToString());
                    foreach (var device in Devices) Stop(device);
                }
            }
            finally { Monitor.Exit(Gate); }
        }

        private void CompleteNativeBattle()
        {
            if (Finished) return;
            var status = Native.Current;
            int c0 = status.Side0Crowns, c1 = status.Side1Crowns;
            Finished = true;
            ResultReadyAt = Environment.TickCount64 + ResultAnimationMilliseconds;
            Pending.Clear();
            Rewards = new ClashRoyale.Messages.Server.Avatar.BattleResultMessage[Devices.Length];
            for (int i = 0; i < Devices.Length; i++)
            {
                int own = HumanSlots[i] % 2 == 0 ? c0 : c1, enemy = HumanSlots[i] % 2 == 0 ? c1 : c0;
                Rewards[i] = Friendly ? FriendlyResult(own, enemy) : Challenge
                    ? ChallengeBattle.Apply(Devices[i].GameMode.Player, MatchId, own, enemy)
                    : MatchReward.Apply(Players[HumanSlots[i]], MatchId, own, enemy, Team);
            }
            Console.WriteLine($"[NATIVE] finished match={MatchId} tick={status.Tick} score={c0}-{c1}");
            FinishTask = SaveResults();
        }
    }
}

namespace ClashRoyale.Logic.Battle
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using ClashRoyale.Logic.Player;
    using ClashRoyale.Messages.Server.Home;
    using ClashRoyale.Messages.Server.Matchmaking;

    public static class LadderMatchmaking
    {
        public const int BotWaitSeconds = 15;
        private static readonly object Gate = new object();
        private static readonly Dictionary<long, Entry> Waiting = new Dictionary<long, Entry>();
        private static readonly Timer Pulse = new Timer(_ => Tick(), null, 1000, 1000);
        private sealed class Entry
        {
            public Device Device;
            public Device Partner;
            public IEnumerable<Device> Members => Partner == null ? new[] { Device } : new[] { Device, Partner };
            public long Joined;
            public bool Team;
            public int ChallengeId;
            public string ChallengeRunId, PartnerChallengeRunId;
        }
        internal static bool FriendlyTransition(Func<bool> transition) { lock (Gate) return transition(); }
        public static int QueueCount { get { lock (Gate) return Waiting.Values.Sum(e => e.Partner == null ? 1 : 2); } }
        public static bool Contains(Device device)
        {
            lock (Gate) return Waiting.Values.Any(e => e.Members.Any(d => d == device || d.GameMode.Player.PlayerId == device.GameMode.Player?.PlayerId));
        }
        public static void Join(Device device, bool team = false, bool challenge = false)
        {
            lock (Gate)
            {
                if (device.GameMode.Player == null || MatchBattleSession.Contains(device) || Contains(device) || ClashRoyale.Handlers.Client.Alliance.ClanFriendlyBattleHandler.Contains(device)) return;
                int challengeId = challenge ? device.GameMode.Home.Challenge?.ModeId ?? 0 : 0;
                if (challenge && (challengeId == 0 || device.GameMode.Home.Challenge.Finished)) return;
                if (challenge) team = device.GameMode.Home.Challenge.GameMode.Players == "TvT";
                Waiting[device.GameMode.Player.PlayerId] = new Entry { Device = device, Joined = Environment.TickCount64, Team = team, ChallengeId = challengeId, ChallengeRunId = challenge ? device.GameMode.Home.Challenge.Id : null };
                device.NetworkManager.SendMessage(new MatchmakeInfoMessage(BotWaitSeconds));
                Console.WriteLine($"[QUEUE] player={device.GameMode.Player.PlayerId} team={team} event={challengeId}");
            }
        }
        // Called atomically after the clan invitation has released both reservations.
        public static bool JoinParty(Device first, Device second, bool challenge = false)
        {
            lock (Gate)
            {
                if (first == null || second == null || first.GameMode.Player.PlayerId == second.GameMode.Player.PlayerId ||
                    new[] { first, second }.Any(d => Contains(d) || MatchBattleSession.Contains(d) || TrainingBattleSession.Contains(d) || ClashRoyale.Handlers.Client.Alliance.ClanFriendlyBattleHandler.Contains(d))) return false;
                var run = first.GameMode.Home.Challenge;
                var partnerRun = second.GameMode.Home.Challenge;
                if (challenge && (run == null || partnerRun == null || run.Finished || partnerRun.Finished || run.RewardClaimed || partnerRun.RewardClaimed || run.ModeId != partnerRun.ModeId || run.GameMode?.Players != "TvT")) return false;
                Waiting[first.GameMode.Player.PlayerId] = new Entry { Device = first, Partner = second, Team = true, Joined = Environment.TickCount64,
                    ChallengeId = challenge ? run.ModeId : 0, ChallengeRunId = challenge ? run.Id : null, PartnerChallengeRunId = challenge ? partnerRun.Id : null };
                foreach (var d in new[] { first, second }) d.NetworkManager.SendMessage(new MatchmakeInfoMessage(BotWaitSeconds));
                Console.WriteLine($"[QUEUE] party={first.GameMode.Player.PlayerId},{second.GameMode.Player.PlayerId}");
                return true;
            }
        }
        public static void Cancel(Device device, bool acknowledge = true)
        {
            lock (Gate)
            {
                var entry = Waiting.Values.FirstOrDefault(e => e.Members.Contains(device));
                if (entry != null)
                {
                    Waiting.Remove(entry.Device.GameMode.Player.PlayerId);
                    foreach (var member in entry.Members)
                        if ((acknowledge || member != device) && !MatchBattleSession.Contains(member)) member.NetworkManager.SendMessage(new CancelMatchmakeDoneMessage());
                }
                else if (acknowledge && !MatchBattleSession.Contains(device)) device.NetworkManager.SendMessage(new CancelMatchmakeDoneMessage());
            }
        }
        public static bool Compatible(Player a, Player b) => Math.Abs((long)a.Score - b.Score) <= 200 && Math.Abs(a.ExpLevel - b.ExpLevel) <= 2;
        public static bool CompatibleAfterWait(Player a, Player b, bool challenge, long waitMilliseconds) => challenge || waitMilliseconds >= 10000 || Compatible(a, b);
        private static bool CompatibleEntries(Entry a, Entry b, long now) => a.Team == b.Team && a.ChallengeId == b.ChallengeId &&
            CompatibleAfterWait(a.Device.GameMode.Player, b.Device.GameMode.Player, a.ChallengeId != 0, now - Math.Min(a.Joined, b.Joined));

        // The flattened members of every two-person party must occupy the same side.
        public static int[] TeamSlots(int[] groupSizes)
        {
            if (groupSizes.Any(n => n < 1 || n > 2) || groupSizes.Sum() > 4 || groupSizes.Length == 0) throw new ArgumentException("Invalid team groups");
            var slots = new int[groupSizes.Sum()]; var used = new bool[4]; int offset = 0;
            foreach (int size in groupSizes)
            {
                if (size == 2)
                {
                    int side = !used[0] && !used[2] ? 0 : !used[1] && !used[3] ? 1 : -1;
                    if (side < 0) throw new ArgumentException("Parties must precede solo players");
                    slots[offset++] = side; slots[offset++] = side + 2; used[side] = used[side + 2] = true;
                }
                else { int slot = Array.FindIndex(used, v => !v); slots[offset++] = slot; used[slot] = true; }
            }
            return slots;
        }
        private static void Tick()
        {
            lock (Gate)
            {
                try
                {
                    foreach (var entry in Waiting.Values.ToArray())
                        if (entry.Members.Any(d => d.Token == null || !d.Token.IsConnected) || (entry.ChallengeId != 0 && (entry.Device.GameMode.Home.Challenge?.Id != entry.ChallengeRunId || entry.Device.GameMode.Home.Challenge.Finished || (entry.Partner != null && (entry.Partner.GameMode.Home.Challenge?.Id != entry.PartnerChallengeRunId || entry.Partner.GameMode.Home.Challenge.Finished)))))
                            Cancel(entry.Device);
                    long now = Environment.TickCount64;
                    foreach (var entry in Waiting.Values.OrderBy(e => e.Joined).ToArray())
                    {
                        if (!Waiting.ContainsKey(entry.Device.GameMode.Player.PlayerId)) continue;
                        if (entry.Team)
                        {
                            var group = new List<Entry> { entry }; int count = entry.Members.Count();
                            foreach (var candidate in Waiting.Values.Where(e => e != entry && CompatibleEntries(entry, e, now)).OrderBy(e => e.Joined))
                                if (count + candidate.Members.Count() <= 4) { group.Add(candidate); count += candidate.Members.Count(); }
                            if (count < 4 && now - entry.Joined < BotWaitSeconds * 1000L) continue;
                            var ordered = group.OrderByDescending(e => e.Members.Count()).ToArray();
                            var humans = ordered.SelectMany(e => e.Members).ToArray();
                            foreach (var member in group) Waiting.Remove(member.Device.GameMode.Player.PlayerId);
                            Start(humans, entry.ChallengeId != 0, TeamSlots(ordered.Select(e => e.Members.Count()).ToArray()));
                        }
                        else
                        {
                            var other = Waiting.Values.Where(e => e != entry && CompatibleEntries(entry, e, now)).OrderBy(e => e.Joined).FirstOrDefault();
                            if (other == null && now - entry.Joined < BotWaitSeconds * 1000L) continue;
                            Waiting.Remove(entry.Device.GameMode.Player.PlayerId);
                            if (other != null) Waiting.Remove(other.Device.GameMode.Player.PlayerId);
                            Start(other == null ? new[] { entry.Device } : new[] { entry.Device, other.Device }, entry.ChallengeId != 0, null);
                        }
                    }
                }
                catch (Exception error) { Logging.Error(typeof(LadderMatchmaking), error.ToString()); }
            }
        }
        private static void Start(Device[] humans, bool challenge, int[] slots)
        {
            try
            {
                Console.WriteLine($"[QUEUE] matched humans={humans.Length} team={slots != null} event={challenge} players={string.Join(",", humans.Select(d => d.GameMode.Player.PlayerId))}");
                if (slots != null) MatchBattleSession.StartTeam(humans, challenge, slots);
                else MatchBattleSession.Start(humans[0], humans.Length > 1 ? humans[1] : null, challenge);
            }
            catch (Exception error)
            {
                foreach (var human in humans) { MatchBattleSession.Stop(human); human.NetworkManager.SendMessage(new MatchmakeFailedMessage()); }
                Logging.Error(typeof(LadderMatchmaking), error.ToString());
            }
        }
    }
}

namespace ClashRoyale.Logic.Battle
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using ClashRoyale.Logic.Player;
    using ClashRoyale.Messages.Server.Home;
    using ClashRoyale.Messages.Server.Matchmaking;

    // All queue transitions share a lock: cancellation cannot race a timeout into a second match.
    public static class LadderMatchmaking
    {
        public const int BotWaitSeconds = 15;
        private static readonly object Gate = new object();
        private static readonly Dictionary<long, Entry> Waiting = new Dictionary<long, Entry>();
        private static readonly Timer Pulse = new Timer(_ => Tick(), null, 1000, 1000);
        private sealed class Entry
        {
            public Device Device;
            public long Joined;
            public bool Team;
            public int ChallengeId;
            public string ChallengeRunId;
        }

        public static int QueueCount { get { lock (Gate) return Waiting.Count; } }

        public static bool Contains(Device device)
        {
            lock (Gate) return device.GameMode.Player != null && Waiting.ContainsKey(device.GameMode.Player.PlayerId);
        }

        public static void Join(Device device, bool team = false, bool challenge = false)
        {
            lock (Gate)
            {
                if (device.GameMode.Player == null || MatchBattleSession.Contains(device)) return;
                int challengeId = challenge ? device.GameMode.Home.Challenge?.ModeId ?? 0 : 0;
                if (challenge && (challengeId == 0 || device.GameMode.Home.Challenge.Finished)) return;
                if (challenge) team = device.GameMode.Home.Challenge.GameMode.Players == "TvT";
                long id = device.GameMode.Player.PlayerId;
                if (Waiting.TryGetValue(id, out var old) && old.Device == device && old.Team == team && old.ChallengeId == challengeId) return;
                Waiting[id] = new Entry { Device = device, Joined = Environment.TickCount64, Team = team, ChallengeId = challengeId, ChallengeRunId = challenge ? device.GameMode.Home.Challenge.Id : null };
                device.NetworkManager.SendMessage(new MatchmakeInfoMessage(BotWaitSeconds));
                Logging.Info(typeof(LadderMatchmaking), "Queued " + (team ? "2v2 " : "1v1 ") + id);
            }
        }

        public static void Cancel(Device device, bool acknowledge = true)
        {
            lock (Gate)
            {
                long id = device.GameMode.Player?.PlayerId ?? 0;
                if (Waiting.TryGetValue(id, out var entry) && entry.Device == device) Waiting.Remove(id);
                if (acknowledge && !MatchBattleSession.Contains(device))
                    device.NetworkManager.SendMessage(new CancelMatchmakeDoneMessage());
            }
        }

        public static bool Compatible(Player a, Player b) => Math.Abs(a.Score - b.Score) <= 200 && Math.Abs(a.ExpLevel - b.ExpLevel) <= 2;

        private static void Tick()
        {
            lock (Gate)
            {
                try
                {
                    foreach (var entry in Waiting.Values.ToArray())
                        if (entry.Device.Token == null || !entry.Device.Token.IsConnected || (entry.ChallengeId != 0 && (entry.Device.GameMode.Home.Challenge?.Id != entry.ChallengeRunId || entry.Device.GameMode.Home.Challenge.Finished)))
                            Waiting.Remove(entry.Device.GameMode.Player.PlayerId);
                    foreach (var entry in Waiting.Values.OrderBy(e => e.Joined).ToArray())
                    {
                        var player = entry.Device.GameMode.Player;
                        if (!Waiting.ContainsKey(player.PlayerId)) continue;
                        if (entry.Team)
                        {
                            var group = Waiting.Values.Where(e => e.Team && e.ChallengeId == entry.ChallengeId && Compatible(player, e.Device.GameMode.Player)).OrderBy(e => e.Joined).Take(4).ToArray();
                            if (group.Length < 4 && Environment.TickCount64 - entry.Joined < BotWaitSeconds * 1000L) continue;
                            foreach (var member in group) Waiting.Remove(member.Device.GameMode.Player.PlayerId);
                            var humans = group.Select(e => e.Device).ToArray();
                            try { MatchBattleSession.StartTeam(humans, entry.ChallengeId != 0); }
                            catch (Exception error)
                            {
                                foreach (var human in humans) { MatchBattleSession.Stop(human); human.NetworkManager.SendMessage(new MatchmakeFailedMessage()); }
                                Logging.Error(typeof(LadderMatchmaking), error.ToString());
                            }
                            continue;
                        }
                        var other = Waiting.Values.FirstOrDefault(e => !e.Team && e != entry && e.ChallengeId == entry.ChallengeId && Compatible(player, e.Device.GameMode.Player));
                        if (other != null)
                        {
                            Waiting.Remove(player.PlayerId);
                            Waiting.Remove(other.Device.GameMode.Player.PlayerId);
                            Start(entry.Device, other.Device, entry.ChallengeId != 0);
                        }
                        else if (Environment.TickCount64 - entry.Joined >= BotWaitSeconds * 1000L)
                        {
                            Waiting.Remove(player.PlayerId);
                            Start(entry.Device, null, entry.ChallengeId != 0);
                        }
                    }
                }
                catch (Exception error) { Logging.Error(typeof(LadderMatchmaking), error.ToString()); }
            }
        }

        private static void Start(Device first, Device second, bool challenge)
        {
            try { MatchBattleSession.Start(first, second, challenge); }
            catch (Exception error)
            {
                MatchBattleSession.Stop(first);
                if (second != null) MatchBattleSession.Stop(second);
                first.NetworkManager.SendMessage(new MatchmakeFailedMessage());
                second?.NetworkManager.SendMessage(new MatchmakeFailedMessage());
                Logging.Error(typeof(LadderMatchmaking), error.ToString());
            }
        }
    }
}

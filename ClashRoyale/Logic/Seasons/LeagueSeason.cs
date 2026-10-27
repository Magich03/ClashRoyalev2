namespace ClashRoyale.Logic.Seasons
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using ClashRoyale.Enums;
    using ClashRoyale.Extensions;
    using ClashRoyale.Extensions.Helper;
    using ClashRoyale.Files.Csv;
    using ClashRoyale.Files.Csv.Logic;
    using ClashRoyale.Logic.Home;
    using ClashRoyale.Logic.Home.Spells;
    using ClashRoyale.Logic.Player;
    using ClashRoyale.Logic.Reward;
    using Newtonsoft.Json;

    public sealed class SeasonResult
    {
        public int Id;
        public int HighScore;
        public int Score;
        public int Rank;
        public ArenaData Arena;
        public void Encode(ChecksumEncoder stream)
        {
            stream.WriteVInt(Id); stream.WriteVInt(HighScore); stream.WriteVInt(Score); stream.WriteVInt(Rank);
            stream.EncodeLogicData(Arena, 54);
        }
    }

    public sealed class SeasonPrize
    {
        public int SeasonId;
        public ArenaData Arena;
        public Chest Chest;
        public DraftReward Draft;
        public List<Spell> Chosen = new();
        public List<Spell> Discarded = new();
        public bool ClaimStarted;
    }

    public sealed class LeagueSeason
    {
        public int Id;
        public long StartedAt;
        public int HighestScore;
        public bool Active;
        public SeasonResult Previous;
        public SeasonResult Best;
        public List<SeasonResult> History = new();
        public List<SeasonPrize> Prizes = new();
        [JsonIgnore] public SeasonPrize Pending => Prizes.FirstOrDefault();

        // Monthly legacy league seasons. Keep calendar calculation independent of server uptime.
        public static DateTimeOffset Boundary(int year, int month)
        {
            var first = new DateTimeOffset(year, month, 1, 9, 0, 0, TimeSpan.Zero);
            return first.AddDays(((int)DayOfWeek.Monday - (int)first.DayOfWeek + 7) % 7);
        }
        public static DateTimeOffset Start(DateTimeOffset now)
        {
            now = now.ToUniversalTime();
            var first = Boundary(now.Year, now.Month);
            if (now < first) { var previous = now.AddMonths(-1); first = Boundary(previous.Year, previous.Month); }
            return first;
        }
        public static DateTimeOffset End(DateTimeOffset now)
        {
            var next = Start(now).AddMonths(1);
            return Boundary(next.Year, next.Month);
        }
        public static int SecondsLeft(long now) => (int)Math.Max(0, End(DateTimeOffset.FromUnixTimeSeconds(now)).ToUnixTimeSeconds() - now);
        public static int SeasonId(DateTimeOffset start) => (start.Year - 2016) * 12 + start.Month;
        public static ArenaData ArenaFor(int score) => CsvFiles.Get(Gamefile.Arenas).Datas.Cast<ArenaData>()
            .Where(a => a.IsInUse && !a.TrainingCamp && !a.PveArena && a.TrophyLimit <= score)
            .OrderByDescending(a => a.TrophyLimit).First();

        public bool Refresh(Player player, DateTimeOffset now, int rank = 0)
        {
            History ??= new(); Prizes ??= new();
            var start = Start(now);
            var id = SeasonId(start);
            bool changed = false;
            if (Id == 0)
            {
                Id = id; StartedAt = start.ToUnixTimeSeconds(); HighestScore = player.Score; Active = true;
                return true;
            }
            // A clock moving backwards must never repeat a completed reset or reward.
            while (Id < id)
            {
                var finalArena = ArenaFor(player.Score);
                var highestArena = ArenaFor(Math.Max(HighestScore, player.Score));
                if (Active && highestArena.SeasonTrophyReset > 0)
                {
                    var result = new SeasonResult { Id = Id, HighScore = Math.Max(HighestScore, player.Score),
                        Score = player.Score, Rank = rank, Arena = highestArena };
                    Previous = result;
                    if (Best == null || result.Score > Best.Score || (result.Score == Best.Score && result.Rank > 0 && (Best.Rank == 0 || result.Rank < Best.Rank))) Best = result;
                    History.RemoveAll(s => s.Id == result.Id); History.Add(result);
                    if (History.Count > 24) History.RemoveRange(0, History.Count - 24);
                    if (highestArena.SeasonTrophyReset > 0 && !Prizes.Any(p => p.SeasonId == Id))
                    {
                        var data = CsvFiles.Get(Gamefile.TreasureChests).GetData<TreasureChestData>(highestArena.SeasonRewardChest);
                        if (data == null) throw new InvalidOperationException("Missing season reward for " + highestArena.Name);
                        var chest = new Chest(data); chest.UnlockDone(); chest.SetSource(10); chest.SetId(Id);
                        Prizes.Add(new SeasonPrize { SeasonId = Id, Arena = highestArena, Chest = chest });
                    }
                }
                if (finalArena.SeasonTrophyReset > 0)
                {
                    player.Score = Math.Min(player.Score, finalArena.SeasonTrophyReset);
                    player.Arena = ArenaFor(player.Score);
                }
                var next = new DateTimeOffset(2016 + (Id - 1) / 12, (Id - 1) % 12 + 1, 1, 9, 0, 0, TimeSpan.Zero).AddMonths(1);
                var nextStart = Boundary(next.Year, next.Month);
                Id = SeasonId(nextStart); StartedAt = nextStart.ToUnixTimeSeconds();
                HighestScore = player.Score; Active = false; rank = 0; changed = true;
            }
            if (!Active) { Active = true; changed = true; }
            if (player.Score > HighestScore) { HighestScore = player.Score; changed = true; }
            return changed;
        }

        public void EncodePrevious(ChecksumEncoder stream) => (Previous ?? new SeasonResult()).Encode(stream);
        public void EncodeBest(ChecksumEncoder stream) => (Best ?? new SeasonResult()).Encode(stream);
    }
}

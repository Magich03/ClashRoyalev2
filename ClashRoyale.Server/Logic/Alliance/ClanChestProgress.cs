namespace ClashRoyale.Logic.Alliance
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using ClashRoyale.Enums;
    using ClashRoyale.Files.Csv;
    using ClashRoyale.Files.Csv.Logic;
    using ClashRoyale.Logic.Alliance.Entries;

    public class ClanChestProgress
    {
        public long Start, End;
        public int Crowns;
        public Dictionary<long,int> Contributions = new();
        public HashSet<string> AppliedBattles = new();
        public Dictionary<long,List<ClanChestEntitlement>> Rewards = new();
        public static int[] Thresholds => CsvFiles.Get(Gamefile.Globals).GetData<GlobalData>("CLAN_CROWN_CHEST_THRESHOLDS").NumberArray;
        public static int TierFor(int crowns) => Thresholds.Count(t => crowns >= t);
        public void Advance(Clan clan, DateTimeOffset now)
        {
            long time=now.ToUnixTimeSeconds();
            if (Start>0 && time>=End)
            {
                int tier=TierFor(Crowns);
                if(tier>0) foreach(var item in Contributions)
                    if(item.Value>0 && clan.Members.ContainsKey(item.Key))
                    {
                        string id=clan.AllianceId+":"+Start;
                        if(!Rewards.TryGetValue(item.Key,out var pending)) Rewards[item.Key]=pending=new();
                        if(!pending.Any(x=>x.EventId==id)) pending.Add(new ClanChestEntitlement { EventId=id, Tier=tier });
                    }
            }
            var status=AllianceCrownChestEntry.CreateWeekly(now);
            if(Start!=status.StartTime)
            {
                Start=status.StartTime; End=status.EndTime; Crowns=0;
                Contributions.Clear(); AppliedBattles.Clear();
                foreach(var member in clan.Members.Values) member.Crowns=0;
            }
        }
        public bool Add(Clan clan, long playerId, string matchId, int crowns, DateTimeOffset now)
        {
            Advance(clan,now);
            long time=now.ToUnixTimeSeconds();
            if(crowns<=0 || time<Start || time>=End || !clan.Members.TryGetValue(playerId,out var member) || member.JoinedAt>Start) return false;
            if(!AppliedBattles.Add(matchId+":"+playerId)) return false;
            int count=Math.Clamp(crowns,0,3);
            Contributions.TryGetValue(playerId,out int previous);
            Contributions[playerId]=previous+count; member.Crowns=previous+count;
            Crowns+=count;
            return true;
        }
        public AllianceCrownChestEntry Status(Clan clan,DateTimeOffset now)
        {
            var status=AllianceCrownChestEntry.CreateWeekly(now);
            status.Progress=Start == status.StartTime ? Crowns : 0;
            return status;
        }
    }
    public class ClanChestEntitlement { public string EventId; public int Tier; }
}

namespace ClashRoyale.Logic.Scoring;
using ClashRoyale.Database;
using ClashRoyale.Logic.Collections;
using ClashRoyale.Logic.Player;
using ClashRoyale.Logic.Seasons;
using Newtonsoft.Json;

public static class SeasonService
{
    private static readonly SemaphoreSlim Gate=new(1,1);
    private static readonly string DirectoryPath=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Data","Seasons");
    public sealed class Placement { public long PlayerId; public int Score, Rank; public string Name; }
    private static async Task<List<Player>> Accounts()
    {
        var accounts=(await GameDb.Players.GetAllAsync()).Where(x=>x.Profile!=null).Select(x=>x.Profile).ToDictionary(x=>x.PlayerId);
        if(Players.Initialized)Players.ForEach(p=>accounts[p.PlayerId]=p,false);
        return accounts.Values.ToList();
    }
    public static async Task Refresh(Player player)
    {
        var now=DateTimeOffset.UtcNow;var season=player.Season??=new();int rank=0;
        if(season.Id>0 && season.Id<LeagueSeason.SeasonId(LeagueSeason.Start(now)))
        {
            await Gate.WaitAsync();
            try
            {
                Directory.CreateDirectory(DirectoryPath);
                string file=Path.Combine(DirectoryPath,season.Id+".json");
                Placement[] placements;
                if(File.Exists(file))placements=JsonConvert.DeserializeObject<Placement[]>(await File.ReadAllTextAsync(file))??Array.Empty<Placement>();
                else
                {
                    var accounts=await Accounts();
                    if(accounts.All(p=>p.PlayerId!=player.PlayerId))accounts.Add(player);
                    placements=accounts.Select(p=>new {Player=p,Result=p.Season?.History?.FirstOrDefault(h=>h.Id==season.Id)})
                        .Where(x=>x.Result!=null || x.Player.Season?.Id==season.Id && x.Player.Season.Active)
                        .Select(x=>new Placement {PlayerId=x.Player.PlayerId,Name=x.Player.Name,Score=x.Result?.Score??x.Player.Score})
                        .Where(x=>x.Score>=4000).OrderByDescending(x=>x.Score).ThenBy(x=>x.PlayerId)
                        .Select((x,i)=>{x.Rank=i+1;return x;}).ToArray();
                    string temp=file+".tmp";await File.WriteAllTextAsync(temp,JsonConvert.SerializeObject(placements));File.Move(temp,file);
                }
                rank=placements.FirstOrDefault(x=>x.PlayerId==player.PlayerId)?.Rank??0;
            }
            finally { Gate.Release(); }
        }
        if(season.Refresh(player,now,rank))await Players.Save(player);
    }
    public static async Task<(AvatarRankingEntry[] Current,AvatarRankingEntry[] Previous)> Rankings(string region=null)
    {
        var accounts=await Accounts();
        var current=accounts.Where(p=>p.IsNameSet && (region==null || p.AccountLocation?.Name==region)).OrderByDescending(p=>p.Score).ThenBy(p=>p.PlayerId).Take(200)
            .Select((p,i)=>{var e=new AvatarRankingEntry(p);e.Order=i+1;e.PreviousOrder=i+1;return e;}).ToArray();
        int previousId=LeagueSeason.SeasonId(LeagueSeason.Start(DateTimeOffset.UtcNow))-1;
        var previous=accounts.Select(p=>new {Player=p,Result=p.Season?.History?.FirstOrDefault(h=>h.Id==previousId)})
            .Where(x=>x.Result!=null && (region==null || x.Player.AccountLocation?.Name==region)).OrderByDescending(x=>x.Result.Score).ThenBy(x=>x.Player.PlayerId).Take(3)
            .Select((x,i)=>{var e=new AvatarRankingEntry(x.Player);e.Score=x.Result.Score;e.Arena=x.Result.Arena;e.Order=i+1;e.PreviousOrder=i+1;return e;}).ToArray();
        return (current,previous);
    }
}

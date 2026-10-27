namespace ClashRoyale.Handlers.Client.Account;
using System.Security.Cryptography;
using System.Text;
using ClashRoyale.Logic.Collections;
using ClashRoyale.Logic.Player;
using Newtonsoft.Json;

// Optional operator-managed recovery links. Normal account authentication always runs first.
public static class AccountRecovery
{
    public sealed class Link
    {
        public int SourceHighId, SourceLowId, TargetHighId, TargetLowId;
        public string SourceTokenHash, TargetTokenHash;
    }
    public static string TokenHash(string token)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token ?? "")));
    public static bool Matches(Link link, Player source, Player target)=>
        source!=null && target!=null && source.PlayerId!=target.PlayerId &&
        link.SourceHighId==source.HighId && link.SourceLowId==source.LowId &&
        link.TargetHighId==target.HighId && link.TargetLowId==target.LowId &&
        !string.IsNullOrEmpty(source.Token) && !string.IsNullOrEmpty(target.Token) &&
        string.Equals(link.SourceTokenHash,TokenHash(source.Token),StringComparison.OrdinalIgnoreCase) &&
        string.Equals(link.TargetTokenHash,TokenHash(target.Token),StringComparison.OrdinalIgnoreCase);
    public static async Task<Player> Resolve(Player authenticated)
    {
        var path=Path.Combine(AppContext.BaseDirectory,"Data","account-recovery.json");
        if(!File.Exists(path))return authenticated;
        try
        {
            var links=JsonConvert.DeserializeObject<Link[]>(await File.ReadAllTextAsync(path)) ?? Array.Empty<Link>();
            var link=links.SingleOrDefault(x=>x.SourceHighId==authenticated.HighId && x.SourceLowId==authenticated.LowId);
            if(link==null)return authenticated;
            var target=await Players.Get(link.TargetHighId,link.TargetLowId);
            if(!Matches(link,authenticated,target))return authenticated;
            Console.WriteLine($"[ACCOUNT] Restored {authenticated.HighId}_{authenticated.LowId} to {target.HighId}_{target.LowId}");
            return target;
        }
        catch(Exception error)
        {
            Logging.Error(typeof(AccountRecovery),"Could not read recovery links: "+error.GetType().Name);
            return authenticated;
        }
    }
}

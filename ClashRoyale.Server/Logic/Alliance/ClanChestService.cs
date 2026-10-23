namespace ClashRoyale.Logic.Alliance
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using ClashRoyale.Enums;
    using ClashRoyale.Files.Csv;
    using ClashRoyale.Files.Csv.Logic;
    using ClashRoyale.Handlers.Client.Alliance;
    using ClashRoyale.Database.Models;
    using ClashRoyale.Logic.Player;
    using ClashRoyale.Logic.Home;
    using ClashRoyale.Logic.Commands.Server;

    public static class ClanChestService
    {
        private static readonly SemaphoreSlim Gate = new(1,1);
        public static async Task Contribute(Player player,string matchId,int crowns)
        {
            var clan=await ClanHelper.GetClan(player);
            if(clan==null) return;
            await Gate.WaitAsync();
            try
            {
                clan.ChestProgress.Add(clan,player.PlayerId,matchId,crowns,DateTimeOffset.UtcNow);
                if(await ClanDb.Save(clan)==null) throw new InvalidOperationException("Clan chest progress could not be saved");
                ClanHelper.BroadcastData(clan);
            }
            finally { Gate.Release(); }
        }
        public static async Task Refresh(Player player,bool notify)
        {
            var clan=await ClanHelper.GetClan(player);
            if(clan==null) return;
            await Gate.WaitAsync();
            try
            {
                clan.ChestProgress.Advance(clan,DateTimeOffset.UtcNow);
                if(clan.ChestProgress.Rewards.TryGetValue(player.PlayerId,out var pending) && pending.Count>0)
                {
                    var earned=pending[0];
                    if(player.Home.ClanChestDelivered==earned.EventId) pending.RemoveAt(0);
                    else if(player.Home.ClanCrownChest==null)
                    {
                        var table=CsvFiles.Get(Gamefile.Globals);
                        int gold=table.GetData<GlobalData>("CLAN_CROWN_CHEST_GOLD").NumberArray[earned.Tier-1];
                        int cards=table.GetData<GlobalData>("CLAN_CROWN_CHEST_CARDS").NumberArray[earned.Tier-1];
                        var chest=new Chest(player.Arena.ClanCrownChestData) {
                            OverrideGold=player.Arena.GetScaledChestReward(gold), OverrideCards=player.Arena.GetScaledChestReward(cards) };
                        chest.UnlockDone(); chest.SetSource(13); chest.SetId(++player.Home.RunningChestId);
                        player.Home.ClanCrownChest=chest;
                        player.Home.ClanChestNotifiedSession=null;
                        player.Home.ClanChestDelivered=earned.EventId;
                        if(await PlayerDb.Save(player)==null) throw new InvalidOperationException("Clan chest entitlement could not be saved");
                        pending.RemoveAt(0);
                    }
                }
                if(await ClanDb.Save(clan)==null) throw new InvalidOperationException("Clan chest state could not be saved");
                if(notify) Notify(player);
            }
            finally { Gate.Release(); }
        }
        public static void Notify(Player player)
        {
            var chest=player.Home.ClanCrownChest;
            if(chest==null || chest.IsClaimed || player.GameMode==null || ReferenceEquals(player.Home.ClanChestNotifiedSession,player.GameMode)) return;
            foreach(var command in player.GameMode.CommandManager.AvailableServerCommands.Values)
                if(command is AddClanChestCommand) return;
            player.Home.ClanChestNotifiedSession=player.GameMode;
            player.GameMode.CommandManager.AddAvailableServerCommand(new AddClanChestCommand { Gold=chest.OverrideGold, Cards=chest.OverrideCards, EventType=0 });
        }
    }
}

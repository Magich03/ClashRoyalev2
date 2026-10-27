namespace ClashRoyale.Handlers.Client.Alliance;
using ClashRoyale.Enums;
using ClashRoyale.Files.Csv.Logic;
using ClashRoyale.Logic;
using ClashRoyale.Logic.Alliance;
using ClashRoyale.Logic.Alliance.Stream;
using ClashRoyale.Logic.Collections;
using ClashRoyale.Logic.Commands.Server;
using ClashRoyale.Messages;
using ClashRoyale.Messages.Client.Alliance;
using ClashRoyale.Messages.Server.Alliance;

public static class ClanDonationHandler
{
    private static readonly SemaphoreSlim Gate=new(1,1);
    public static async Task Request(Device device, SpellData card)
    {
        await Gate.WaitAsync();
        try
        {
            var player=device.GameMode.Player;
            var clan=await ClanHelper.GetClan(player);
            if(clan==null || device.GameMode.State!=HomeState.Home)return;
            var now=DateTimeOffset.UtcNow;
            var entry=CardDonations.Request(player,card,now);
            if(entry==null)return;
            foreach(var old in clan.Messages.ToArray().OfType<DonateStreamEntry>().Where(x=>x.SenderHighId==player.HighId && x.SenderLowId==player.LowId))
            {
                clan.Messages.RemoveEntry(old);
                clan.Members.Broadcast(()=>new AllianceStreamEntryMessage(old));
            }
            clan.Messages.AddEntry(entry);
            await Players.Save(player);await Clans.Save(clan);
            clan.Members.Broadcast(()=>new AllianceStreamEntryMessage(entry));
        }
        finally {Gate.Release();}
    }
    public static async Task Handle(Device device,Message message,CancellationToken cancellation)
    {
        var donation=(DonateAllianceUnitMessage)message;
        await Gate.WaitAsync(cancellation);
        try
        {
            var donor=device.GameMode.Player;
            var clan=await ClanHelper.GetClan(donor);
            if(clan==null || device.GameMode.State!=HomeState.Home)return;
            if(clan.Messages.GetEntry(donation.RequestId) is not DonateStreamEntry entry)return;
            long recipientId=((long)entry.SenderHighId<<32)|(uint)entry.SenderLowId;
            if(!clan.Members.ContainsKey(recipientId))return;
            var recipient=clan.Members.GetConnected(recipientId)??await Players.Get(entry.SenderHighId,entry.SenderLowId,false);
            if(recipient==null || !CardDonations.Donate(donor,recipient,entry,donation.Card,DateTimeOffset.UtcNow))return;
            clan.Refresh();
            if(clan.Members.TryGetValue(donor.PlayerId,out var member))member.Donations+=donation.Card.RarityData.DonateCapacity;
            clan.HeaderEntry.Donations=clan.Members.Values.Sum(m=>m.Donations);
            await Players.Save(donor);await Players.Save(recipient);await Clans.Save(clan);
            device.GameMode.CommandManager.AddAvailableServerCommand(new DonateAllianceUnitCommand{Card=donation.Card,RequestId=entry.StreamId});
            if(recipient.IsConnected && recipient.GameMode.State==HomeState.Home)
                recipient.GameMode.CommandManager.AddAvailableServerCommand(new AllianceUnitReceivedCommand(donor.Name,donation.Card){AlreadyApplied=true});
            clan.Members.Broadcast(()=>new AllianceStreamEntryMessage(entry));
            ClanHelper.BroadcastData(clan);
        }
        finally {Gate.Release();}
    }
}

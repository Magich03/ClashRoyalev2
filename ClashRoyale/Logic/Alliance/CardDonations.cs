namespace ClashRoyale.Logic.Alliance;
using System;
using ClashRoyale.Enums;
using ClashRoyale.Files.Csv;
using ClashRoyale.Files.Csv.Logic;
using ClashRoyale.Logic.Alliance.Stream;
using ClashRoyale.Logic.Player;
using ClashRoyale.Logic.Reward;

public static class CardDonations
{
    public static int Setting(string name) => CsvFiles.Get(Gamefile.Globals).GetData<GlobalData>(name).NumberValue;
    public static int RequestCapacity(Player p, SpellData card) => p.Arena.RequestSize / Math.Max(1,card.RarityData.DonateCapacity);
    public static int MemberCapacity(Player p, SpellData card) => card.RarityData.Name switch
    {
        "Common" => p.Arena.MaxDonationCountCommon,
        "Rare" => p.Arena.MaxDonationCountRare,
        "Epic" => p.Arena.MaxDonationCountEpic,
        _ => 0
    };
    public static bool CanRequest(Player p, SpellData card, DateTimeOffset now)
    {
        if (!p.IsInAlliance || p.ExpLevel < Setting("ALLIANCE_DONATION_UNLOCK_EXP_LEVEL") || card == null || !p.Home.RequestCooldownTimer.IsFinished) return false;
        var spell=p.Home.GetSpellByData(card);
        if (spell==null || spell.Level>=card.MaxLevelIndex || spell.Count>=spell.MaxMaterialCount) return false;
        string rarity=card.RarityData.Name;
        if (rarity!="Common" && rarity!="Rare" && rarity!="Epic") return false;
        if (rarity=="Epic" && (now.UtcDateTime.DayOfWeek!=DayOfWeek.Sunday || p.Home.LastEpicRequestDay==now.ToUnixTimeSeconds()/86400)) return false;
        return RequestCapacity(p,card)>0;
    }
    public static DonateStreamEntry Request(Player p, SpellData card, DateTimeOffset now)
    {
        if (!CanRequest(p,card,now)) return null;
        p.Home.RequestCooldownTimer.Reset();
        p.Home.RequestCooldownTimer.StartTimer(Setting("REQUEST_SPELLS_COOLDOWN_MINUTES")*60);
        p.Home.RequestedCard=card;
        if(card.RarityData.Name=="Epic")p.Home.LastEpicRequestDay=now.ToUnixTimeSeconds()/86400;
        p.Home.Quests.ClanAction("Request",card,1);
        return new DonateStreamEntry(p) { Card=card, RequestSize=p.Arena.RequestSize, Capacity=RequestCapacity(p,card), CreationDateTime=now.UtcDateTime };
    }
    public static bool Donate(Player donor, Player recipient, DonateStreamEntry request, SpellData card, DateTimeOffset now)
    {
        if (request==null || card==null || request.Card!=card || request.Removed || donor.PlayerId==recipient.PlayerId ||
            !donor.IsInAlliance || donor.AllianceId!=recipient.AllianceId || request.SenderHighId!=recipient.HighId || request.SenderLowId!=recipient.LowId ||
            donor.ExpLevel<Setting("ALLIANCE_DONATION_UNLOCK_EXP_LEVEL") || request.Total>=request.Capacity ||
            now.UtcDateTime<request.CreationDateTime || (now.UtcDateTime-request.CreationDateTime).TotalSeconds>=Setting("REQUEST_SPELLS_COOLDOWN_MINUTES")*60) return false;
        var own=donor.Home.GetSpellByData(card);var receiving=recipient.Home.GetSpellByData(card);
        if (own==null || own.Count<1 || receiving==null || receiving.Count>=receiving.MaxMaterialCount) return false;
        if (request.Given.GetValueOrDefault(donor.PlayerId)>=MemberCapacity(donor,card))return false;
        RefreshCapacity(donor);
        int weight=card.RarityData.DonateCapacity;
        if(weight<=0 || donor.Home.DonationCapacityLimit+weight>donor.Arena.DailyDonationCapacityLimit)return false;
        if(donor.Home.DonationCapacityCooldownTimer.IsFinished)
        {
            donor.Home.DonationCapacityCooldownTimer.Reset();
            donor.Home.DonationCapacityCooldownTimer.StartTimer(Setting("DONATION_CAPACITY_LIMIT_COOLDOWN_HOURS")*3600);
        }
        own.Count--;receiving.AddMaterial(1);
        request.Given[donor.PlayerId]=request.Given.GetValueOrDefault(donor.PlayerId)+1;
        donor.Home.DonationCapacityLimit+=weight;
        int gold=Math.Min(card.RarityData.DonateReward,Math.Max(0,donor.MaxGold-donor.Gold));
        donor.AddFreeGold(gold);donor.XpGainHelper(card.RarityData.DonateXp);
        donor.AddResource(CsvFiles.Get(Gamefile.Resources).GetData<ResourceData>("DonationsTotal"),weight);
        donor.GameMode?.AchievementManager.UpdateAchievementProgress(1,weight);
        donor.Home.Quests.ClanAction("Donate",card,1);
        donor.Home.Quests.Collect(new Reward {Gold=gold},null);
        var received=new ClashRoyale.Logic.Home.Spells.Spell(card);received.SetMaterialCount(1);
        recipient.Home.Quests.Collect(new Reward {Spells=new(){received}},null);
        return true;
    }
    public static void RefreshCapacity(Player p)
    {
        if(p.Home.DonationCapacityCooldownTimer.IsFinished)p.Home.DonationCapacityLimit=0;
    }
}

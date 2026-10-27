namespace ClashRoyale.Logic.Quests;

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
using ClashRoyale.Logic.Mode;
using ClashRoyale.Logic.Player;
using ClashRoyale.Logic.Reward;
using ClashRoyale.Logic.Commands.Server;
using ClashRoyale.Crypto.Randomizers;

public sealed class QuestState
{
    public List<QuestEntry> Entries = new();
    public int Points, NextId=1, ChestIndex;
    public long NextFillAt, LastDiscardAt;
    public bool Seen, MainClaimStarted;
    public string ChestCycle;
    public Chest MainChest, PendingChest;
    public Reward PendingReward;
    public int PendingType;
    private static readonly Lazy<CsvData[]> Catalog = new(() => new[] {"play","cast","collect"}
        .SelectMany((name,i)=>new CsvTable(101+i,"Gamefiles/csv_logic/quests/"+name+".csv").Datas).ToArray());
    public static long Now => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    public bool Refresh(Player p,long now)
    {
        bool changed=false;
        Entries ??= new();
        if (ChestCycle==null) { ChestCycle=p.Arena.Arena>=5?"Quest_lategame_1":"Quest_earlygame_1";changed=true; }
        if (MainChest==null) { CreateMainChest(p);changed=true; }
        var daily=Entries.FirstOrDefault(q=>q.Type==1);
        if (daily==null || daily.Progress>=3 && now>=daily.ReadyAt)
        {
            if(daily!=null)Entries.Remove(daily);
            Entries.Add(new QuestEntry {Type=1,Id=NextId++,Name="Quest_FreeChests",Title="TID_DAILY_GIFTS",Info="TID_FREE_CHEST_QUEST",Export="quest_item_free_chest",Goal=3,Points=5,ReadyAt=now});changed=true;
        }
        int slots=CsvFiles.Get(Gamefile.ExpLevels).GetWithInstanceId<ExpLevelData>(Math.Max(0,p.ExpLevel-1)).QuestSlots;
        if(now>=NextFillAt)
        {
            int allowance=NextFillAt==0?Math.Clamp(slots,1,3):1;
            while(allowance-->0 && Entries.Count(q=>q.Type!=1)<Math.Clamp(slots,1,3)) { Entries.Add(Generate(p));changed=true; }
            if(NextFillAt!=0 && changed)NextFillAt=now+86400;
        }
        return changed;
    }

    private QuestEntry Generate(Player p)
    {
        string Value(CsvData d,string field)=>d.CsvRow.GetSize(field)>0?d.CsvRow.GetValue(field,0):"";
        int Number(CsvData d,string field)=>int.TryParse(Value(d,field),out var n)?n:0;
        bool ArenaAllowed(CsvData d,string field,bool min) { string name=Value(d,field);var a=CsvFiles.Get(Gamefile.Arenas).GetData<ArenaData>(name);return a==null || (min?p.Arena.Arena>=a.Arena:p.Arena.Arena<=a.Arena); }
        var pool=Catalog.Value.Where(d=>Number(d,"Weight")>0 && ArenaAllowed(d,"MinArena",true) && ArenaAllowed(d,"MaxArena",false))
            .Where(d=> !Entries.Any(q=>q.Name==d.Name))
            // Only generate objectives whose authoritative game actions are available on this server.
            .Where(d=> d.Type!=101 || Value(d,"GameType")=="Ladder" && Value(d,"PlayerType")=="1v1")
            .Where(d=> d.Type!=102 || string.IsNullOrEmpty(Value(d,"Type")))
            .Where(d=>Enumerable.Range(0,d.CsvRow.GetSize("Spell")).Select(i=>CsvFiles.GetSpellDataByName(d.CsvRow.GetValue("Spell",i))).All(card=>card==null || p.Home.GetSpellByData(card)!=null))
            .ToArray();
        if(pool.Length==0)throw new InvalidOperationException("No eligible quests for arena "+p.Arena.Name);
        var order=CsvFiles.Get(Gamefile.QuestOrder).GetData<QuestOrderData>(p.Arena.QuestCycle) ?? CsvFiles.Get(Gamefile.QuestOrder).Datas.Cast<QuestOrderData>().First();
        string Family(CsvData d)=>d.Type==101?"Win":d.Type==102?"Cast":"Collect";
        var families=pool.Select(Family).Distinct().Select(name=>new {Name=name,Weight=order.QuestCount[Array.IndexOf(order.QuestType,name)]}).Where(x=>x.Weight>0).ToArray();
        if(families.Length>0)
        {
            int familyRoll=Random.Shared.Next(families.Sum(x=>x.Weight));
            string selected=families[0].Name;
            foreach(var family in families){familyRoll-=family.Weight;if(familyRoll<0){selected=family.Name;break;}}
            pool=pool.Where(d=>Family(d)==selected).ToArray();
        }
        int roll=Random.Shared.Next(pool.Sum(d=>Number(d,"Weight")));var chosen=pool[0];
        foreach(var d in pool) {roll-=Number(d,"Weight");if(roll<0){chosen=d;break;}}
        var q=new QuestEntry {Id=NextId++,Type=chosen.Type==101?2:chosen.Type==102?4:5,Name=chosen.Name,Title=Value(chosen,"Title"),Info=Value(chosen,"Info"),Export=Value(chosen,"ItemExportName"),Goal=Number(chosen,"Count"),Size=Number(chosen,"Size"),Rarity=Value(chosen,"Rarity"),MinElixir=Number(chosen,"MinElixir"),MaxElixir=Number(chosen,"MaxElixir"),Action=Value(chosen,chosen.Type==103?"DataType":"Type"),Resource=Value(chosen,"Resource"),BaseChest=Value(chosen,"BaseChest")};
        q.SpellId=CsvFiles.GetSpellDataByName(Value(chosen,"Spell"))?.GlobalId??0;
        q.RequiredCards=Enumerable.Range(0,chosen.CsvRow.GetSize("Spell")).Select(i=>CsvFiles.GetSpellDataByName(chosen.CsvRow.GetValue("Spell",i))?.GlobalId??0).Where(id=>id!=0).ToArray();
        int reward=Random.Shared.Next(order.RewardCount.Sum());int idx=0;
        for(;idx<order.RewardCount.Length-1;idx++){reward-=order.RewardCount[idx];if(reward<0)break;}
        int amount=(q.Size==0?order.RewardAmountSmall:order.RewardAmountLarge)[idx];q.Points=(q.Size==0?order.QuestPointsSmall:order.QuestPointsLarge)[idx];
        switch(order.RewardType[idx]){case "Gold":q.Gold=amount;break;case "Gems":q.Gems=amount;break;default:q.CardCount=amount;q.RewardRarity=order.RewardType[idx];break;}
        if(q.CardCount>0)
        {
            var cards=new SpellSet(p.Arena,null).Spells.SelectMany(x=>x).Where(d=>d.RarityData.Name==q.RewardRarity).ToArray();
            if(cards.Length==0)throw new InvalidOperationException("No eligible quest reward cards");
            q.RewardSpellId=cards[Random.Shared.Next(cards.Length)].GlobalId;
        }
        return q;
    }
    private QuestMainRewardData MainData => CsvFiles.Get(91).Datas.Cast<QuestMainRewardData>().First(d=>d.Chest==(MainChest?.Data.RootChest.Name??"Gold"));
    private void CreateMainChest(Player p)
    {
        var cycle=CsvFiles.Get(Gamefile.ChestOrder).GetData<ChestOrderData>(ChestCycle);
        string name=cycle.Chest[ChestIndex%cycle.Chest.Length];
        var root=CsvFiles.Get(Gamefile.TreasureChests).GetData<TreasureChestData>(name);
        var data=CsvFiles.Get(Gamefile.TreasureChests).Datas.Cast<TreasureChestData>().Where(d=>d.RootChest==root && d.ArenaData!=null && d.ArenaData.TrophyLimit<=p.Arena.TrophyLimit).OrderByDescending(d=>d.ArenaData.TrophyLimit).FirstOrDefault() ?? root;
        MainChest=new Chest(data); MainChest.UnlockDone();MainChest.SetSource(13);MainChest.SetId(ChestIndex+1);
    }
    public void Battle(Player p,int own,int enemy,bool team)
    {
        if(team)return;
        foreach(var q in Entries.Where(q=>q.Type==2 && !q.Completed))
        {
            if(q.RequiredCards.Any(id=>!Enumerable.Range(0,8).Any(i=>p.Home.SpellDeck[i]?.Data.GlobalId==id)))continue;
            int add=q.Action=="Wins"?(own>enemy?1:0):q.Action=="Crowns"?own:1;
            q.Progress=Math.Min(q.Goal,q.Progress+add);
        }
    }
    public void Cast(SpellData data,bool team,bool challenge)
    {
        if(team||challenge)return;
        foreach(var q in Entries.Where(q=>q.Type==4 && !q.Completed))
            if((q.SpellId==0||q.SpellId==data.GlobalId) && (string.IsNullOrEmpty(q.Rarity)||q.Rarity==data.RarityData.Name) && (q.MinElixir==0||data.ManaCost>=q.MinElixir) && (q.MaxElixir==0||data.ManaCost<=q.MaxElixir)) q.Progress=Math.Min(q.Goal,q.Progress+1);
    }
    public void Collect(Reward reward,TreasureChestData chest)
    {
        foreach(var q in Entries.Where(q=>q.Type==5 && !q.Completed))
        {
            int amount=q.Action=="Chest"?(chest!=null && (string.IsNullOrEmpty(q.BaseChest)||q.BaseChest==chest.RootChest.Name)?1:0):q.Action=="Card"?(reward.Spells??new()).Where(s=>string.IsNullOrEmpty(q.Rarity)||s.Data.RarityData.Name==q.Rarity).Sum(s=>s.Count):q.Resource=="Gold"?reward.Gold:q.Resource=="Diamonds"?reward.Diamonds:0;
            q.Progress=Math.Min(q.Goal,q.Progress+Math.Max(0,amount));
        }
    }
    public byte Claim(GameMode mode,int id,bool special,long now)
    {
        var q=Entries.FirstOrDefault(q=>q.Id==id && q.Special==special);
        if(q==null || PendingReward!=null || mode.Home.IsClaimingReward)return 3;
        if(q.Type==1)
        {
            if(q.Progress>=3 || now<q.ReadyAt)return 3;
            var free=CsvFiles.Get(Gamefile.TreasureChests).Datas.Cast<TreasureChestData>().Where(d=>d.RootChest.Name=="Free" && d.ArenaData!=null && d.ArenaData.TrophyLimit<=mode.Player.Arena.TrophyLimit).OrderByDescending(d=>d.ArenaData.TrophyLimit).FirstOrDefault() ?? CsvFiles.Get(Gamefile.TreasureChests).GetData<TreasureChestData>("Free");
            PendingChest=new Chest(free);PendingChest.UnlockDone();PendingChest.SetId(q.Id);
            q.DailyClaimed[q.Progress++]=1;q.ReadyAt=now+(q.Progress>=3?86400:14400);
            Points+=q.Points;StartReward(mode,14);return 0;
        }
        if(!q.Completed)return 3;
        var reward=new Reward { Gold=q.Gold,Diamonds=q.Gems,Spells=new(),DiscardedSpells=new() };
        if(q.CardCount>0)
        {
            var data=CsvFiles.GetWithGlobalId(q.RewardSpellId) as SpellData;
            if(data==null)return 3;var spell=RewardRandomizer.CreateSpell(data);spell.SetMaterialCount(q.CardCount);reward.Spells.Add(spell);
        }
        Entries.Remove(q);Points+=q.Points;NextFillAt=Math.Max(NextFillAt,now+86400);
        return new ClaimRewardCommand(reward,0,0).Execute(mode);
    }
    public byte Discard(GameMode mode,int id,bool special,long now)
    {
        var q=Entries.FirstOrDefault(q=>q.Id==id && q.Special==special);
        if(q==null||q.Type==1||q.Completed||special||now<LastDiscardAt+86400)return 3;
        Entries.Remove(q);LastDiscardAt=now;var replacement=Generate(mode.Player);Entries.Add(replacement);
        mode.CommandManager.AddAvailableServerCommand(new UpdateQuestsCommand(new[]{replacement}));return 0;
    }
    public byte ClaimMain(GameMode mode)
    {
        if(Points<MainData.QuestThreshold||PendingReward!=null||MainClaimStarted||mode.Home.IsClaimingReward)return 3;
        MainClaimStarted=true;PendingChest=MainChest;StartReward(mode,13);return 0;
    }
    private void StartReward(GameMode mode,int type)
    {
        PendingType=type;PendingReward=RewardRandomizer.RandomizeReward(PendingChest,mode.Home);
        mode.Home.SetClaimingReward(true);mode.CommandManager.AddAvailableServerCommand(new ClaimRewardCommand(PendingReward,type,PendingChest.Id));
    }
    public bool CanFinish(int type,int id)=>PendingReward!=null && PendingType==type && PendingChest?.Id==id;
    public void Finish(Player p)
    {
        if(PendingType==13){Points-=MainData.QuestThreshold;ChestIndex++;MainClaimStarted=false;CreateMainChest(p);}
        PendingReward=null;PendingChest=null;PendingType=0;
    }
    public void Resume(GameMode mode)
    {
        if(PendingReward==null)return;
        mode.Home.SetClaimingReward(true);mode.CommandManager.AddAvailableServerCommand(new ClaimRewardCommand(PendingReward,PendingType,PendingChest.Id));
    }
    public void Encode(ByteStream s,long now)
    {
        s.WriteBoolean(true);s.WriteVInt(Entries.Count);foreach(var q in Entries)q.Encode(s,now);s.WriteVInt(0);
        s.WriteVInt(Points);s.WriteVInt(MainData.QuestThreshold);s.WriteVInt(Seen?1:0);
        s.WriteVInt((int)Math.Max(0,LastDiscardAt+86400-now));s.WriteVInt((int)Math.Max(0,NextFillAt-now));s.WriteVInt(0);s.WriteVInt(NextId);s.WriteVInt(ChestIndex);
        s.WriteVInt(0);s.EncodeLogicData(CsvFiles.Get(Gamefile.ChestOrder).GetData(ChestCycle),52);s.EncodeLogicData(MainData,91);
        s.WriteBoolean(Points>=MainData.QuestThreshold);if(Points>=MainData.QuestThreshold)MainChest.Encode(s);
        for(int i=0;i<14;i++)s.WriteVInt(0);s.WriteVInt(0);s.WriteVInt(0);s.WriteVInt(-1);s.WriteVInt(-1);
    }
}

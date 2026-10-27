namespace ClashRoyale.Logic.Quests;

using ClashRoyale.Extensions;
using ClashRoyale.Extensions.Helper;
using ClashRoyale.Files.Csv;

// 3.830 LogicQuest and its Battle/Cast/Collect/Donate/Daily subclasses.
public sealed class QuestEntry
{
    public int Type, Id, Goal, Progress, Points = 20, Size;
    public string Name = "", Title = "", Info = "", Export = "quest_item_pvp";
    public string Rarity = "", RewardRarity = "", Action = "", Resource = "", BaseChest = "";
    public int MinElixir, MaxElixir, SpellId, Gold, Gems, CardCount, RewardSpellId;
    public int[] RequiredCards = System.Array.Empty<int>();
    public bool Special;
    public long ReadyAt;
    public int[] DailyClaimed = new int[3];
    public bool Completed => Type != 1 && Progress >= Goal;

    public void Encode(ChecksumEncoder s, long now)
    {
        s.WriteVInt(Type); s.WriteVInt(Id); s.WriteBoolean(Special);
        s.WriteString(Title); s.WriteString(""); s.WriteVInt(Goal); s.WriteVInt(Progress); s.WriteVInt(0);
        s.WriteString(Name); s.WriteString(Info); s.WriteString("sc/ui.sc"); s.WriteString(Export); s.WriteVInt(Points);
        if (Type == 1)
        {
            s.WriteVInt(3);
            for(int i=0;i<3;i++) { s.EncodeData(CsvFiles.Get(19).GetData("Free")); s.WriteVInt(1); }
        }
        else
        {
            s.WriteVInt(1);
            s.EncodeData(CardCount > 0 ? RewardSpellId>0?CsvFiles.GetWithGlobalId(RewardSpellId):CsvFiles.Get(14).GetData(RewardRarity) : CsvFiles.Get(5).GetData(Gems > 0 ? "Diamonds" : "Gold"));
            s.WriteVInt(CardCount > 0 ? CardCount : Gems > 0 ? Gems : Gold);
        }
        switch(Type)
        {
            case 1:
                s.WriteVInt(3); foreach(int c in DailyClaimed) s.WriteVInt(c);
                s.WriteVInt(3); s.WriteVInt(0); s.WriteVInt(4); s.WriteVInt(4);
                s.WriteBoolean(true);
                int remaining=(int)System.Math.Clamp(ReadyAt-now,0,86400);
                s.WriteVInt(remaining*20); s.WriteVInt(14400*20); s.WriteVInt(remaining>0?(int)ReadyAt:-1);
                s.WriteVInt(0); break;
            case 2:
                s.WriteVInt(Action=="Wins"?1:Action=="Crowns"?2:0);
                s.WriteVInt(RequiredCards.Length);
                foreach(int id in RequiredCards) s.EncodeLogicData(CsvFiles.GetWithGlobalId(id),26);
                EncodeBattleFilter(s); break;
            case 4:
                s.EncodeLogicData(SpellId==0?null:CsvFiles.GetWithGlobalId(SpellId),26);
                s.EncodeLogicData(string.IsNullOrEmpty(Rarity)?null:CsvFiles.Get(14).GetData(Rarity),14);
                s.WriteVInt(0); s.WriteVInt(MinElixir); s.WriteVInt(MaxElixir); EncodeBattleFilter(s); break;
            case 5:
                s.WriteVInt(Action=="Card"?1:Action=="Resource"?2:0);
                s.EncodeLogicData(string.IsNullOrEmpty(Resource)?null:CsvFiles.Get(5).GetData(Resource),5);
                s.EncodeLogicData(string.IsNullOrEmpty(Rarity)?null:CsvFiles.Get(14).GetData(Rarity),14);
                s.EncodeLogicData(string.IsNullOrEmpty(BaseChest)?null:CsvFiles.Get(19).GetData(BaseChest),19); break;
            case 6: s.WriteVInt(Action=="Request"?1:0);s.WriteString(Rarity);break;
            default: throw new System.InvalidOperationException("Unsupported quest type " + Type);
        }
    }
    private static void EncodeBattleFilter(ChecksumEncoder s) { s.WriteVInt(1);s.WriteVInt(1);s.WriteVInt(0); }

    public static QuestEntry Decode(ByteStream s)
    {
        var q=new QuestEntry {Type=s.ReadVInt(),Id=s.ReadVInt(),Special=s.ReadBoolean(),Title=s.ReadString()};
        s.ReadString();q.Goal=s.ReadVInt();q.Progress=s.ReadVInt();s.ReadVInt();q.Name=s.ReadString();q.Info=s.ReadString();s.ReadString();q.Export=s.ReadString();q.Points=s.ReadVInt();
        if(s.ReadVInt()!=1)throw new System.IO.InvalidDataException("Invalid replacement quest rewards");
        var data=s.DecodeData();int amount=s.ReadVInt();
        if(data.Type>=26 && data.Type<=29){q.CardCount=amount;q.RewardSpellId=data.GlobalId;}else if(data.Type==14){q.CardCount=amount;q.RewardRarity=data.Name;}else if(data.Name=="Diamonds")q.Gems=amount;else q.Gold=amount;
        switch(q.Type)
        {
            case 2:
                int kind=s.ReadVInt();q.Action=kind==1?"Wins":kind==2?"Crowns":"Games";
                int count=s.ReadVInt();if(count<0||count>8)throw new System.IO.InvalidDataException("Invalid required cards");
                q.RequiredCards=new int[count];for(int i=0;i<count;i++)q.RequiredCards[i]=s.DecodeLogicData(26)?.GlobalId??0;
                ReadFilter(s);break;
            case 4:q.SpellId=s.DecodeLogicData(26)?.GlobalId??0;q.Rarity=s.DecodeLogicData(14)?.Name??"";s.ReadVInt();q.MinElixir=s.ReadVInt();q.MaxElixir=s.ReadVInt();ReadFilter(s);break;
            case 5:int type=s.ReadVInt();q.Action=type==1?"Card":type==2?"Resource":"Chest";q.Resource=s.DecodeLogicData(5)?.Name??"";q.Rarity=s.DecodeLogicData(14)?.Name??"";q.BaseChest=s.DecodeLogicData(19)?.Name??"";break;
            case 6:q.Action=s.ReadVInt()==1?"Request":"Donate";q.Rarity=s.ReadString();break;
            default:throw new System.IO.InvalidDataException("Unsupported replacement quest");
        }
        return q;
    }
    private static void ReadFilter(ByteStream s)
    { if(s.ReadVInt()!=1||s.ReadVInt()!=1||s.ReadVInt()!=0)throw new System.IO.InvalidDataException("Invalid replacement quest filter"); }
}

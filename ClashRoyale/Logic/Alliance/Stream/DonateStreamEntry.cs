namespace ClashRoyale.Logic.Alliance.Stream;
using System.Collections.Generic;
using System.Linq;
using ClashRoyale.Extensions;
using ClashRoyale.Extensions.Helper;
using ClashRoyale.Files.Csv;
using ClashRoyale.Files.Csv.Logic;
using ClashRoyale.Logic.Player;
using Newtonsoft.Json.Linq;
public class DonateStreamEntry : StreamEntry
{
    public override int Type => 1;
    public SpellData Card;
    public int RequestSize, Capacity;
    public Dictionary<long,int> Given=new();
    public int Total => Given.Values.Sum();
    public DonateStreamEntry() { }
    public DonateStreamEntry(Player player) : base(player) { }
    public override void Encode(ChecksumEncoder s)
    {
        base.Encode(s);s.WriteInt(RequestSize);s.WriteInt(Given.Count);
        // Each donor owns a list of donated card global IDs, not a donation counter.
        // The client resolves every element to card data when calculating the filled capacity.
        foreach(var item in Given.OrderBy(x=>x.Key))
        {
            s.WriteLong(item.Key);s.WriteVInt(item.Value);
            for(int i=0;i<item.Value;i++)s.WriteVInt(Card.GlobalId);
        }
        s.WriteVInt(1);s.EncodeData(Card);s.WriteBoolean(false);
    }
    public override void Load(JToken json)
    {
        base.Load(json);Card=CsvFiles.GetWithGlobalId((int)json["card"]) as SpellData;
        RequestSize=(int)json["requestSize"];Capacity=(int)json["capacity"];
        Given=json["given"]?.ToObject<Dictionary<long,int>>()??new();Removed=(bool?)json["removed"]??false;
    }
    public override JObject Save()
    {
        var json=base.Save();json["card"]=Card.GlobalId;json["requestSize"]=RequestSize;json["capacity"]=Capacity;
        json["given"]=JObject.FromObject(Given);json["removed"]=Removed;return json;
    }
}

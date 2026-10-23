namespace ClashRoyale.Logic.Event
{
    using System;
    using System.Linq;
    using ClashRoyale.Enums;
    using ClashRoyale.Extensions;
    using ClashRoyale.Extensions.Helper;
    using ClashRoyale.Files.Csv;
    using ClashRoyale.Files.Csv.Logic;

    // Native Pick decoder 0x280254 and command 23 (0x1707fe).
    public sealed class PickSelection
    {
        private readonly SpellData[][] Offers;
        private readonly int[][] Picks;
        public bool Complete => Picks.All(p=>p.All(i=>i>=0));
        public int Count(int player) => Picks[player].Count(i=>i>=0);
        public SpellData[] Deck(int player) => Picks[player].Where(i=>i>=0).Select(i=>Offers[player][i]).ToArray();
        public PickSelection(int seed)
        {
            var random=new Random(seed);
            var pool=new[]{26,27,28}.SelectMany(t=>CsvFiles.Get((Gamefile)t).Datas).OfType<SpellData>().Where(c=>!c.NotInUse).ToArray();
            Offers=Enumerable.Range(0,2).Select(_=>pool.OrderBy(c=>random.Next()).Take(16).ToArray()).ToArray();
            Picks=Enumerable.Range(0,2).Select(_=>Enumerable.Repeat(-1,8).ToArray()).ToArray();
        }
        public bool Choose(int player,int offer)
        {
            if(player<0 || player>=2 || offer<0 || offer>=16 || Count(player)>=8 || Picks[player].Contains(offer)) return false;
            Picks[player][Count(player)]=offer; return true;
        }
        public int NextChoice(int player) => Enumerable.Range(0,16).FirstOrDefault(i=>!Picks[player].Contains(i),-1);
        public void Encode(ChecksumEncoder stream,int player)
        {
            stream.WriteVInt(16);
            foreach(var card in Offers[player]) stream.EncodeLogicData(card,26);
            stream.WriteVInt(8);
            foreach(var pick in Picks[player]) {
                stream.WriteBoolean(pick>=0);
                if(pick>=0) stream.EncodeLogicData(Offers[player][pick],26);
            }
            stream.WriteVInt(1); // accepting choices
        }
    }
}

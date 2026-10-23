namespace ClashRoyale.Logic.Event
{
    using System;
    using System.Linq;
    using System.Collections.Generic;
    using ClashRoyale.Enums;
    using ClashRoyale.Extensions;
    using ClashRoyale.Extensions.Helper;
    using ClashRoyale.Files.Csv;
    using ClashRoyale.Files.Csv.Logic;

    public sealed class DraftSelection
    {
        private readonly SpellData[][] Offers;
        private readonly SpellData[][] Decks;
        private readonly int[][] Picks;
        public int PlayerCount => Decks.Length;
        public bool Complete => Picks.All(p=>p.All(c=>c>=0));
        public int Choice(int player,int round) => Picks[player][round];
        public SpellData[] Deck(int player) => (SpellData[])Decks[player].Clone();
        public DraftSelection(int players,int seed,bool touchdown=false, string featuredCard=null)
        {
            if(players!=2 && players!=4) throw new ArgumentOutOfRangeException(nameof(players));
            Decks=Enumerable.Range(0,players).Select(_=>new SpellData[8]).ToArray();
            Picks=Enumerable.Range(0,players).Select(_=>Enumerable.Repeat(-1,4).ToArray()).ToArray();
            Offers=new SpellData[players/2][];
            var random=new Random(seed);
            var draft=CsvFiles.Get(Gamefile.DraftDeck).GetData<DraftDeckData>(touchdown?"Draft_Touchdown_v1":"Draft_v1");
            var required=draft.RequiredSets.Where(n=>!string.IsNullOrEmpty(n)).ToList();
            var optional=draft.OptionalSets.Where(n=>!string.IsNullOrEmpty(n)).OrderBy(_=>random.Next()).ToArray();
            while(required.Count<8) required.Add(optional[(required.Count-draft.RequiredSets.Count(n=>!string.IsNullOrEmpty(n)))%optional.Length]);
            for(int pair=0;pair<Offers.Length;pair++)
            {
                var used=new HashSet<int>();
                var offers=new List<SpellData>();
                foreach(var set in required.Take(8).OrderBy(_=>random.Next()))
                {
                    var pool=CsvFiles.Get(Gamefile.SpellSets).GetData<SpellSetData>(set)?.SpellsData
                        ?.Where(c=>c!=null && !c.NotInUse && !used.Contains(c.GlobalId)).OrderBy(_=>random.Next()).Take(2).ToArray();
                    if(pool==null || pool.Length!=2) throw new InvalidOperationException("Draft pool exhausted: "+set);
                    foreach(var card in pool) { offers.Add(card); used.Add(card.GlobalId); }
                }
                if (!string.IsNullOrEmpty(featuredCard))
                {
                    var featured=CsvFiles.GetSpellDataByName(featuredCard);
                    if (featured == null || featured.NotInUse) throw new InvalidOperationException("Unavailable featured draft card");
                    // The release card appears in one real choice. The other player receives it if passed.
                    if (!offers.Any(c=>c.GlobalId==featured.GlobalId)) offers[0]=featured;
                }
                Offers[pair]=offers.ToArray();
            }
        }
        public bool Choose(int player,int round,int choice)
        {
            if(player<0 || player>=PlayerCount || round<0 || round>=4 || choice<0 || choice>1 || Picks[player][round]>=0) return false;
            int opponent=player^1;
            int offset=round*4+(player%2)*2;
            var offers=Offers[player/2];
            Picks[player][round]=choice;
            Decks[player][round]=offers[offset+choice];
            Decks[opponent][7-round]=offers[offset+1-choice];
            return true;
        }
        // Native draft decoder 0x13812c: state, state ticks, total ticks, selected cards,
        // shared offers, auto-pick flag. Player index is supplied by the battle decoder.
        public void Encode(ChecksumEncoder stream,int player)
        {
            stream.WriteVInt(1); stream.WriteVInt(0); stream.WriteVInt(0);
            foreach(var card in Decks[player]) stream.EncodeLogicData(card,26);
            stream.WriteVInt(Offers[player/2].Length);
            foreach(var card in Offers[player/2]) stream.EncodeLogicData(card,26);
            stream.WriteBoolean(false);
        }
    }
}

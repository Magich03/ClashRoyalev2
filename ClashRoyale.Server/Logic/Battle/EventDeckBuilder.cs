namespace ClashRoyale.Logic.Battle
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using ClashRoyale.Enums;
    using ClashRoyale.Files.Csv;
    using ClashRoyale.Files.Csv.Logic;
    using ClashRoyale.Logic.Home.Spells;
    using ClashRoyale.Logic.Player;

    public static class EventDeckBuilder
    {
        public static void Prepare(Player[] snapshots, GameModeData mode, int seed, string featuredCard = null)
        {
            var random = new Random(seed);
            var shared = new Dictionary<int, SpellData[]>();
            for (int index=0;index<snapshots.Length;index++)
            {
                var player = snapshots[index];
                var originalLevels=player.Home.SpellDeck.GetSpells().Where(c=>c?.Data!=null)
                    .ToDictionary(c=>c.Data.GlobalId,c=>c.Level);
                SpellData[] cards;
                if (mode.DeckSelection == "Predefined")
                {
                    int group = mode.SeparateTeamDecks ? index / 2 : 0;
                    if (!mode.SameDeckOnBoth || !shared.TryGetValue(group, out cards))
                    {
                        var names = mode.PredefinedDecks?.Where(n=>!string.IsNullOrEmpty(n)).ToArray();
                        if (names == null || names.Length==0) throw new InvalidOperationException("Event has no predefined decks");
                        cards = BuildPredefined(names[random.Next(names.Length)], random);
                        shared[group] = cards;
                    }
                }
                else if (mode.DeckSelection == "EventDeck")
                {
                    var pool = string.IsNullOrEmpty(mode.EventDeckSetLimit) ? AllCards() : Pool(mode.EventDeckSetLimit);
                    var allowed = pool.Select(c=>c.GlobalId).ToHashSet();
                    var chosen = new List<SpellData>();
                    if (!string.IsNullOrEmpty(featuredCard)) Add(chosen, CsvFiles.GetSpellDataByName(featuredCard));
                    else if (mode.Name == "BattleRam") Add(chosen, CsvFiles.GetSpellDataByName("BattleRam"));
                    else if (mode.ForcedDeckCardsUsingCardTheme) throw new InvalidOperationException("Card release event requires a featured card");
                    if (!string.IsNullOrEmpty(mode.ForcedDeckCards))
                        foreach (var name in mode.ForcedDeckCards.Split(','))
                            Add(chosen, CsvFiles.GetSpellDataByName(name.Trim()));
                    foreach (var card in player.Home.SpellDeck.GetSpells())
                        if (card?.Data != null && allowed.Contains(card.Data.GlobalId) && chosen.Count<8) Add(chosen,card.Data);
                    foreach (var card in pool.OrderBy(_=>random.Next()))
                        if (chosen.Count<8) Add(chosen,card);
                    cards=chosen.ToArray();
                }
                else if (mode.DeckSelection=="Collection")
                    cards=player.Home.SpellDeck.GetSpells().Select(c=>c.Data).ToArray();
                else throw new NotSupportedException("Event deck requires its selection flow: " + mode.DeckSelection);
                if (cards.Length!=8 || cards.Select(c=>c.GlobalId).Distinct().Count()!=8)
                    throw new InvalidOperationException("Event deck must contain eight different cards");
                for(int slot=0;slot<8;slot++)
                {
                    int cap=Math.Max(0,8-cards[slot].RarityData.RelativeLevel);
                    int level=mode.CardLevelAdjustment=="Friendly" ? cap : Math.Min(originalLevels.TryGetValue(cards[slot].GlobalId,out var oldLevel) ? oldLevel : cap,cap);
                    player.Home.SpellDeck.SetSpell(slot,new Spell(cards[slot]) { Level=level });
                }
                if(mode.CardLevelAdjustment=="Friendly") player.ExpLevel=9;
            }
        }
        public static SpellData[] BuildPredefined(string name, Random random)
        {
            var data=CsvFiles.Get(Gamefile.PredefinedDecks).GetData<PredefinedDeckData>(name)
                ?? throw new InvalidOperationException("Missing predefined deck " + name);
            var cards=new List<SpellData>();
            foreach(var spell in data.Spells ?? Array.Empty<string>())
                if(!string.IsNullOrEmpty(spell)) Add(cards,CsvFiles.GetSpellDataByName(spell));
            foreach(var set in data.RandomSpellSets ?? Array.Empty<string>())
            {
                if(string.IsNullOrEmpty(set) || cards.Count>=8) continue;
                var choices=Pool(set).Where(c=>cards.All(s=>s.GlobalId!=c.GlobalId)).ToArray();
                if(choices.Length==0) throw new InvalidOperationException("Exhausted event card pool " + set);
                cards.Add(choices[random.Next(choices.Length)]);
            }
            if(cards.Count!=8) throw new InvalidOperationException("Incomplete predefined deck " + name);
            return cards.ToArray();
        }
        private static void Add(List<SpellData> cards,SpellData card)
        {
            if(card==null) throw new InvalidOperationException("Event refers to a missing card");
            if(cards.All(c=>c.GlobalId!=card.GlobalId)) cards.Add(card);
        }
        private static SpellData[] Pool(string name) => CsvFiles.Get(Gamefile.SpellSets).GetData<SpellSetData>(name)?.SpellsData
            ?.Where(c=>c!=null && !c.NotInUse).ToArray() ?? throw new InvalidOperationException("Missing spell set " + name);
        private static SpellData[] AllCards() => new[]{26,27,28}.SelectMany(t=>CsvFiles.Get((Gamefile)t).Datas)
            .OfType<SpellData>().Where(c=>!c.NotInUse).ToArray();
    }
}

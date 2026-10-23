namespace ClashRoyale.Logic.Battle
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using ClashRoyale.Files.Csv;
    using ClashRoyale.Files.Csv.Logic;
    using ClashRoyale.Logic.Player;
    using ClashRoyale.Logic.Home.Spells;

    public static class MatchBotFactory
    {
        private static int Sequence;
        private static readonly object Gate = new object();
        private static readonly Dictionary<long, int> LastDeck = new Dictionary<long, int>();
        private static readonly string[] Names = { "RiverKnight", "CrownFox", "Ash", "Milo", "Nova", "IronOak", "Finn", "SkyArcher" };
        // 2017 card pool. Early-arena lists are adaptations; classic lists use their old card versions.
        // Historical context and provenance: outputs/bot-decks.txt in the task workspace.
        private static readonly (string Name, string Cards)[] Decks = {
            ("Starter Giant", "Giant,Knight,Archer,Bomber,Musketeer,MiniPekka,Arrows,Fireball"),
            ("Giant Witch", "Giant,Witch,Knight,Archer,Bomber,MiniPekka,Arrows,Fireball"),
            ("Giant Baby Dragon", "Giant,BabyDragon,Knight,Archer,Musketeer,MiniPekka,Arrows,Fireball"),
            ("Giant Prince", "Giant,Prince,Archer,Bomber,Musketeer,MiniPekka,Arrows,Fireball"),
            ("Giant Musketeer", "Giant,Musketeer,MiniPekka,Valkyrie,Goblins,Minions,Arrows,Fireball"),
            ("Hog Trifecta", "HogRider,Valkyrie,Musketeer,Cannon,Skeletons,Goblins,Zap,Fireball"),
            ("Hog 2.6", "HogRider,IceGolemite,Musketeer,IceSpirits,Skeletons,Cannon,Fireball,Log"),
            ("Classic Log Bait", "GoblinBarrel,Princess,Knight,GoblinGang,IceSpirits,InfernoTower,Rocket,Log"),
            ("Giant Double Prince", "Giant,Prince,DarkPrince,MegaMinion,ElectroWizard,Miner,Fireball,Zap"),
            ("LavaLoon", "LavaHound,Balloon,MegaMinion,Minions,SkeletonArmy,Tombstone,Arrows,Lightning"),
            ("Golem Night Witch", "Golem,DarkWitch,BabyDragon,MegaMinion,Elixir Collector,Lightning,Zap,Log"),
            ("PEKKA Bridge Spam", "Pekka,BattleRam,Assassin,ElectroWizard,Minions,IceGolemite,Poison,Zap"),
            ("Mortar Cycle", "Mortar,Knight,Archer,Skeletons,IceSpirits,Rocket,Log,Tornado"),
            ("X-Bow Cycle", "Xbow,Tesla,Archer,IceGolemite,Skeletons,IceSpirits,Fireball,Log")
        };
        public static Player Create(Player human)
        {
            int number = Interlocked.Increment(ref Sequence);
            var bot = new Player(2147483646, number);
            bot.Initialize();
            bot.Name = Names[(number - 1) % Names.Length];
            bot.ExpLevel = human.ExpLevel;
            bot.Arena = human.Arena;
            bot.Score = human.Score;
            var owned = Enumerable.Range(0,8).Select(i => human.Home.SpellDeck[i]).Where(s => s?.Data != null).ToArray();
            // Compare power using rarity offsets: old rare level 1 is common-equivalent level 3.
            int target = Math.Max(2, Math.Min(human.ExpLevel - 1, owned.Length == 0 ? human.ExpLevel - 1 :
                (int)Math.Floor(owned.Average(s => s.Level + s.Data.RarityData.RelativeLevel))));
            int rarityCeiling = Math.Max(target, owned.Length == 0 ? 2 : owned.Max(s => s.Data.RarityData.RelativeLevel));
            var eligible = Decks.Select((d,i) => new { Index=i, d.Name, Cards=d.Cards.Split(',').Select(CsvFiles.GetSpellDataByName).ToArray() })
                .Where(d => d.Cards.All(c => c != null && !c.NotInUse && c.IsUnlockedInArena(human.Arena) &&
                    c.RarityData.RelativeLevel <= rarityCeiling)).ToArray();
            if (eligible.Length == 0) throw new InvalidOperationException("No eligible historical bot deck for this arena");
            int selected;
            lock (Gate)
            {
                var options = eligible.Where(d => !LastDeck.TryGetValue(human.PlayerId, out var last) || d.Index != last).ToArray();
                if (options.Length == 0) options = eligible;
                selected = options[Random.Shared.Next(options.Length)].Index;
                if (LastDeck.Count >= 4096 && !LastDeck.ContainsKey(human.PlayerId)) LastDeck.Clear();
                LastDeck[human.PlayerId] = selected;
            }
            var deck = eligible.First(d => d.Index == selected);
            var cards = deck.Cards.Select(data => {
                int level = Math.Clamp(target - data.RarityData.RelativeLevel, 0, data.MaxLevelIndex);
                var same = owned.FirstOrDefault(s => s.Data.GlobalId == data.GlobalId);
                if (same != null) level = Math.Min(level, same.Level);
                return new Spell(data) { Level = level };
            }).ToArray();
            // Shuffle opening hands independently of the chosen archetype.
            for (int i=cards.Length-1;i>0;i--) { int j=Random.Shared.Next(i+1); (cards[i],cards[j])=(cards[j],cards[i]); }
            for (int i=0;i<8;i++) bot.Home.SpellDeck.SetSpell(i,cards[i]);
            Logging.Info(typeof(MatchBotFactory), "Bot deck: " + deck.Name + "; king level " + bot.ExpLevel);
            return bot;
        }
    }
}

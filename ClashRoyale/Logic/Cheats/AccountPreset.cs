using ClashRoyale.Logic.Cheats.Commands;
namespace ClashRoyale.Logic.Cheats
{
    using System.Linq;
    using ClashRoyale.Files.Csv;
    using ClashRoyale.Logic.Player;
    internal static class AccountPreset
    {
        internal static void Apply(Player player, bool max)
        {
            player.ExpLevel = max ? CsvFiles.MaxExpLevel : 1;
            player.ExpPoints = 0;
            player.Home.SetLastShownLevelUp(player.ExpLevel);
            player.SetFreeGold(max ? player.MaxGold : 0);
            player.SetFreeDiamonds(max ? GemsCheat.MaximumGems : 0);
            if (max)
            {
                player.Home.AddAllSpells();
                var topArena = CsvFiles.Get(ClashRoyale.Enums.Gamefile.Arenas).Datas
                    .Cast<ClashRoyale.Files.Csv.Logic.ArenaData>()
                    .Where(a => a.IsInUse && !a.TrainingCamp && !a.PveArena)
                    .OrderByDescending(a => a.TrophyLimit).First();
                player.Score = System.Math.Max(player.Score, topArena.TrophyLimit);
                player.SetMaxScore(System.Math.Max(player.MaxScore, player.Score));
                player.Arena = topArena;
            }
            else
            {
                // Starting eight cards from this version's starting_home.json.
                int[] starterIds = { 26000000, 26000001, 26000013, 28000001, 28000000, 26000003, 26000004, 26000005 };
                var starterDeck = new ClashRoyale.Logic.Home.Spells.SpellDeck();
                for (int i = 0; i < starterIds.Length; i++)
                    starterDeck.SetSpell(i, new ClashRoyale.Logic.Home.Spells.Spell(
                        CsvFiles.Spells.Single(s => s.GlobalId == starterIds[i])));
                player.Home.SpellDeck = starterDeck;
                player.Home.SpellCollection = new ClashRoyale.Logic.Home.Spells.SpellCollection();
                player.Home.SelectedDeck = 0;
                player.Home.SavedDecks = Enumerable.Range(0, 5).Select(_ => (int[])starterIds.Clone()).ToArray();
                player.Score = 0;
                player.SetMaxScore(0);
                player.Arena = CsvFiles.Get(ClashRoyale.Enums.Gamefile.Arenas).Datas
                    .Cast<ClashRoyale.Files.Csv.Logic.ArenaData>()
                    .Where(a => a.IsInUse && !a.TrainingCamp && !a.PveArena)
                    .OrderBy(a => a.TrophyLimit).First();
            }
            foreach (var spell in player.Home.SpellDeck.GetSpells().Concat(player.Home.SpellCollection.GetSpells()).Where(s => s != null))
            {
                spell.Level = max ? spell.Data.MaxLevelIndex : 0;
                spell.Count = 0; spell.NewCount = 0; spell.NewFlag = false; spell.NewUpgrade = false;
            }
        }
    }
}

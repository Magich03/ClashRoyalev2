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

            }
            foreach (var spell in player.Home.SpellDeck.GetSpells().Concat(player.Home.SpellCollection.GetSpells()).Where(s => s != null))
            {
                spell.Level = max ? spell.Data.MaxLevelIndex : 0;
                spell.Count = 0; spell.NewCount = 0; spell.NewFlag = false; spell.NewUpgrade = false;
            }
        }
    }
}

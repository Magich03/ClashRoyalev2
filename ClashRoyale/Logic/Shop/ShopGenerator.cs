namespace ClashRoyale.Logic.Shop
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using ClashRoyale.Enums;
    using ClashRoyale.Files.Csv;
    using ClashRoyale.Files.Csv.Logic;
    using ClashRoyale.Logic.Home;
    using ClashRoyale.Logic.Shop.Items;

    /// <summary>
    /// Builds the daily shop (card deals + chest offers). The offers are derived from the UTC date and the
    /// player id, so they stay identical across logins on the same day without being persisted.
    /// </summary>
    public static class ShopGenerator
    {
        private static readonly string[] ChestTypes = { "Lightning", "Fortune", "Kings" };

        public static void Refresh(Home Home, Player.Player Player)
        {
            DateTime Now = DateTime.UtcNow;
            bool NewDay  = Home.ShopDay != Now.Day;

            if (!NewDay && Home.ShopItems.Count > 0)
            {
                return;
            }

            if (NewDay || Home.ShopBought == null)
            {
                Home.ShopBought = new List<int>();
            }

            Home.ShopDay = Now.Day;
            Home.ShopTimer.StartTimer((int) (Now.Date.AddDays(1) - Now).TotalSeconds);

            Home.ShopItems.Clear();
            Home.ShopChests.Clear();

            int ArenaNumber = Player.Arena?.Arena ?? 0;
            Random Random   = new Random(Now.Year * 1000 + Now.DayOfYear ^ Player.LowId * 7919);

            ShopGenerator.AddCardDeals(Home, Random, ArenaNumber, "Common", 0, "Common_Stack_Size", "Common_Cost", 0);
            ShopGenerator.AddCardDeals(Home, Random, ArenaNumber, "Rare", 1, "Rare_Stack_Size", "Rare_Cost", 3);

            ShopGenerator.AddChestOffers(Home, Player, Random, ArenaNumber);
        }

        private static void AddCardDeals(Home Home, Random Random, int ArenaNumber, string Rarity, int RarityIndex, string StackRow, string CostRow, int FirstIndex)
        {
            CsvTable Cycle   = CsvFiles.Get(Gamefile.ShopCycle);
            ShopCycleData Stacks = Cycle.GetData<ShopCycleData>(StackRow);
            ShopCycleData Costs  = Cycle.GetData<ShopCycleData>(CostRow);

            if (Stacks == null || Costs == null)
            {
                Logging.Error(typeof(ShopGenerator), "Missing shop_cycle row " + StackRow + " or " + CostRow + ".");
                return;
            }

            int Row = Math.Clamp(ArenaNumber, 0, Stacks.A.Length - 1);
            int[] Amounts = { Stacks.A[Row], Stacks.B[Row], Stacks.C[Row] };
            int CostPerCard = Costs.A[0];

            List<SpellData> Pool = ShopGenerator.CardPool(ArenaNumber, Rarity);

            for (int I = 0; I < Amounts.Length && Pool.Count > 0; I++)
            {
                SpellData Spell = Pool[Random.Next(Pool.Count)];
                Pool.Remove(Spell);

                int Index = FirstIndex + I;

                Home.ShopItems.Add(new SpellShopItem(Index, Amounts[I] * CostPerCard, CsvFiles.GoldData, Spell, Amounts[I], RarityIndex)
                {
                    Seed   = Home.ShopSeed,
                    Bought = Home.ShopBought.Contains(Index)
                });
            }
        }

        private static void AddChestOffers(Home Home, Player.Player Player, Random Random, int ArenaNumber)
        {
            CsvTable ShopChests = CsvFiles.Get(Gamefile.ShopChests);
            CsvTable Chests     = CsvFiles.Get(Gamefile.TreasureChests);

            if (ShopChests == null || Player.Arena == null)
            {
                return;
            }

            foreach (string Type in ShopGenerator.ChestTypes)
            {
                ShopChestData Offer = ShopChests.Datas.OfType<ShopChestData>().FirstOrDefault(Data =>
                    Data.Type == Type && ShopGenerator.InArenaRange(ArenaNumber, Data.MinArena, Data.MaxArena));

                TreasureChestData Chest = Offer == null ? null : Chests.GetData<TreasureChestData>(Offer.Name + "_" + Player.Arena.Name);

                if (Chest == null)
                {
                    continue;
                }

                ChestEvent Event = new ChestEvent(Offer, Chest)
                {
                    Index = Home.ShopChests.Count
                };

                if (Offer.DailyCardRotation)
                {
                    List<SpellData> Pool = ShopGenerator.CardPool(ArenaNumber, null);

                    for (int I = 0; I < 4 && Pool.Count > 0; I++)
                    {
                        SpellData Spell = Pool[Random.Next(Pool.Count)];
                        Pool.Remove(Spell);
                        Event.FortuneSpells.Add(Spell);
                    }
                }

                Home.ShopChests.Add(Event);
            }
        }

        private static List<SpellData> CardPool(int ArenaNumber, string Rarity)
        {
            return CsvFiles.Spells.Where(Spell =>
                !Spell.NotInUse &&
                Spell.UnlockArenaData != null &&
                Spell.UnlockArenaData.Arena <= ArenaNumber &&
                (Rarity == null || Spell.Rarity == Rarity)).ToList();
        }

        private static bool InArenaRange(int ArenaNumber, string MinArena, string MaxArena)
        {
            CsvTable Arenas = CsvFiles.Get(Gamefile.Arenas);

            if (!string.IsNullOrEmpty(MinArena) && Arenas.GetData<ArenaData>(MinArena) is ArenaData Min && ArenaNumber < Min.Arena)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(MaxArena) && Arenas.GetData<ArenaData>(MaxArena) is ArenaData Max && ArenaNumber > Max.Arena)
            {
                return false;
            }

            return true;
        }
    }
}

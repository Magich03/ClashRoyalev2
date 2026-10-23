namespace ClashRoyale.Files.Csv.Logic
{
    using System;
    using ClashRoyale.Enums;
    using Math = ClashRoyale.Maths.Math;

    public class TreasureChestData : CsvData
    {
        public ArenaData ArenaData;
        public TreasureChestData BaseTreasureChestData;
        public SpellData[] GuaranteedSpellsData;

        public int TotalTimeTakenSeconds;

        /// <summary>
        ///     Initializes a new instance of the <see cref="TreasureChestData" /> class.
        /// </summary>
        /// <param name="CsvRow">The row.</param>
        /// <param name="CsvTable">The data table.</param>
        public TreasureChestData(CsvRow CsvRow, CsvTable CsvTable) : base(CsvRow, CsvTable)
        {
            // TreasureChestData.
        }

        /// <summary>
        ///     Gets the shop price.
        /// </summary>
        public int ShopPrice
        {
            get
            {
                int ShopPriceWithoutSpeedUp = 0;
                int SpeedUpCost = 0;
                int Cost = 0;

                if (this.BaseTreasureChestData != null)
                {
                    if (this.ArenaData != null)
                    {
                        ShopPriceWithoutSpeedUp = this.ArenaData.ChestShopPriceMultiplier * this.BaseTreasureChestData.ShopPriceWithoutSpeedUp / 100;
                    }
                    else
                    {
                        ShopPriceWithoutSpeedUp = this.BaseTreasureChestData.ShopPriceWithoutSpeedUp;
                    }

                    SpeedUpCost = this.BaseTreasureChestData.GetSpeedUpCost(this.BaseTreasureChestData.TotalTimeTakenSeconds);
                }
                else
                {
                    ShopPriceWithoutSpeedUp = this.ShopPriceWithoutSpeedUp;
                    SpeedUpCost = this.GetSpeedUpCost(this.TotalTimeTakenSeconds);
                }

                Cost = ShopPriceWithoutSpeedUp + SpeedUpCost;

                if (Cost > 20)
                {
                    if (Cost > 100)
                    {
                        if (Cost > 500)
                        {
                            return 100 * ((Cost + 50) / 100);
                        }

                        return 10 * ((Cost + 5) / 10);
                    }

                    return 5 * ((Cost + 3) / 5);
                }

                return Cost;
            }
        }

        /// <summary>
        ///     Gets the random spell count.
        /// </summary>
        public int RandomSpellCount
        {
            get
            {
                TreasureChestData root = this.RootChest;
                int count;
                if (root.SpecialOffer || this.DraftChest)
                    count = this.RandomSpells;
                else if (root.InShop)
                    count = (this.ArenaData.ShopChestRewardMultiplier * this.RandomSpells + 50) / 100;
                else
                    count = this.ArenaData.GetScaledChestReward((this.BaseTreasureChestData ?? this).RandomSpells);
                return System.Math.Max(0, count - this.GuaranteedSpellsData.Length);
            }
        }

        /// <summary>
        ///     Gets the different spell count.
        /// </summary>
        public int DifferentSpellCount
        {
            get
            {
                if (this.BaseChest != null)
                {
                    return this.BaseTreasureChestData.DifferentSpellCount;
                }

                return this.DifferentSpells;
            }
        }

        /// <summary>
        ///     Gets the max gold.
        /// </summary>
        public TreasureChestData RootChest => this.BaseTreasureChestData == null ? this : this.BaseTreasureChestData.RootChest;

        public int MaxGold => this.GetGold(this.RootChest.MaxGoldPerCard);
        public int MinGold => this.GetGold(this.RootChest.MinGoldPerCard);

        private int GetGold(int perCard)
        {
            int gold = this.RandomSpellCount * perCard;
            if (!this.RootChest.InShop) return gold;
            if (gold > 1100) return 100 * ((gold + 50) / 100);
            if (gold > 100) return 10 * ((gold + 5) / 10);
            if (gold > 20) return 5 * ((gold + 3) / 5);
            return gold;
        }

        public string BaseChest { get; set; }

        public string Arena { get; set; }

        public bool InShop { get; set; }

        public bool InArenaInfo { get; set; }

        public bool TournamentChest { get; set; }

        public bool SurvivalChest { get; set; }

        public int ShopPriceWithoutSpeedUp { get; set; }

        public int TimeTakenDays { get; set; }

        public int TimeTakenHours { get; set; }

        public int TimeTakenMinutes { get; set; }

        public int TimeTakenSeconds { get; set; }

        public int RandomSpells { get; set; }

        public int DifferentSpells { get; set; }

        public int ChestCountInChestCycle { get; set; }

        public int RareChance { get; set; }

        public int EpicChance { get; set; }

        public int LegendaryChance { get; set; }

        public int LegendaryOverrideChance { get; set; }

        public int SkinChance { get; set; }

        public string[] GuaranteedSpells { get; set; }

        public int MinGoldPerCard { get; set; }

        public int MaxGoldPerCard { get; set; }

        public string FileName { get; set; }

        public string ExportName { get; set; }

        public string ShopExportName { get; set; }

        public string GainedExportName { get; set; }

        public string AnimExportName { get; set; }

        public string OpenInstanceName { get; set; }

        public string SlotLandEffect { get; set; }

        public string OpenEffect { get; set; }

        public string TapSound { get; set; }

        public string TapSoundShop { get; set; }

        public string DescriptionTid { get; set; }

        public string Tid { get; set; }

        public string NotificationTid { get; set; }

        public string SpellSet { get; set; }

        public int Exp { get; set; }

        public int SortValue { get; set; }

        public bool SpecialOffer { get; set; }

        public bool DraftChest { get; set; }

        public string IconSwf { get; set; }

        public string IconExportName { get; set; }

        public bool BoostedChest { get; set; }

        /// <summary>
        ///     Called when all instances has been loaded for initialized members in instance.
        /// </summary>
        public override void LoadingFinished()
        {
            if (!string.IsNullOrEmpty(this.Arena))
            {
                this.ArenaData = CsvFiles.Get(Gamefile.Arenas).GetData<ArenaData>(this.Arena);

                if (this.ArenaData == null)
                {
                    throw new Exception("Arena " + this.Arena + " does not exist.");
                }
            }

            if (!string.IsNullOrEmpty(this.BaseChest))
            {
                this.BaseTreasureChestData = this.CsvTable.GetData<TreasureChestData>(this.BaseChest);
            }

            if (!string.IsNullOrEmpty(this.GuaranteedSpells[0]))
            {
                this.GuaranteedSpellsData = new SpellData[this.GuaranteedSpells.Length];

                for (int I = 0; I < this.GuaranteedSpells.Length; I++)
                {
                    // Chests finish loading before CsvFiles.Spells is populated.
                    foreach (Gamefile file in new[] { Gamefile.SpellsCharacters, Gamefile.SpellsBuildings, Gamefile.SpellsOther })
                    {
                        this.GuaranteedSpellsData[I] = CsvFiles.Get(file).Datas.Find(d => d.Name == this.GuaranteedSpells[I]) as SpellData;
                        if (this.GuaranteedSpellsData[I] != null) break;
                    }
                    if (this.GuaranteedSpellsData[I] == null)
                        throw new Exception("Unknown guaranteed card " + this.GuaranteedSpells[I]);
                }
            }
            else
            {
                this.GuaranteedSpellsData = new SpellData[0];
            }

            this.TotalTimeTakenSeconds = this.TimeTakenDays * 86400 + this.TimeTakenHours * 3600 + this.TimeTakenMinutes * 60 + this.TimeTakenSeconds;

            if (this.BaseTreasureChestData != null)
            {
                if (!this.InShop)
                {
                    this.InShop = this.BaseTreasureChestData.InShop;
                }

                if (this.TotalTimeTakenSeconds == 0)
                {
                    this.TotalTimeTakenSeconds = this.BaseTreasureChestData.TotalTimeTakenSeconds;
                }
            }
        }

        /// <summary>
        ///     Gets the chance for specified rarity.
        /// </summary>
        public int GetChanceForRarity(RarityData Data)
        {
            int available = this.ArenaData.GetUnlockedSpellCountForRarity(Data);
            if (available < 1) return 0;
            TreasureChestData root = this.RootChest;
            switch (Data.Name)
            {
                case "Rare": return root.RareChance;
                case "Epic": return root.EpicChance;
                case "Legendary":
                    if (root.SpecialOffer && root.Name == "Legendary") return this.RandomSpellCount;
                    if (root.LegendaryOverrideChance > 0)
                        return root.RandomSpellCount * root.LegendaryOverrideChance;
                    var common = CsvFiles.Get(Gamefile.Rarities).GetData<RarityData>("Common");
                    int commonCount = this.ArenaData.GetUnlockedSpellCountForRarity(common);
                    return commonCount > available ? root.LegendaryChance * commonCount / available : root.LegendaryChance;
                default: return 0;
            }
        }

        /// <summary>
        ///     Gets the speed up cost.
        /// </summary>
        public int GetSpeedUpCost(int RemainingTime)
        {
            if (RemainingTime <= 0)
            {
                return 0;
            }

            return Math.Clamp((this.TotalTimeTakenSeconds + 600 * RemainingTime - 1) / this.TotalTimeTakenSeconds, 1, this.TotalTimeTakenSeconds / 600);
        }
    }
}
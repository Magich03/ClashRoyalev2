namespace ClashRoyale.Logic.Reward
{
    using System.Collections.Generic;
    using ClashRoyale.Crypto.Randomizers;
    using ClashRoyale.Enums;
    using ClashRoyale.Extensions.Game;
    using ClashRoyale.Extensions.Utils;
    using ClashRoyale.Files.Csv;
    using ClashRoyale.Files.Csv.Logic;
    using ClashRoyale.Logic.Home;
    using ClashRoyale.Logic.Home.Spells;

    public class RewardRandomizer
    {
        /// <summary>
        ///     Creates a spell with spell data.
        /// </summary>
        public static Spell CreateSpell(SpellData Data)
        {
            if (Data == null)
            {
                return null;
            }

            Spell Spell = new Spell(Data);

            Spell.SetCreateTime(TimeUtil.MinutesSince1970);
            Spell.SetShowNewIcon(true);

            return Spell;
        }

        /// <summary>
        ///     Combines a list of spells.
        /// </summary>
        public static void CombineSpells(List<Spell> Spells, int Count, int[] CountByRarity, Home Home)
        {
            if (Count > 0)
            {
                if (Spells.Count >= 2)
                {
                    int RndCatchupChance = XorShift.Next(100);
                    int CatchupChance = Globals.ChestCatchupChance;

                    int Multiplier1 = 500 * Spells.Count;
                    int Multiplier2 = 250 * Spells.Count;
                    int Multiplier3 = 1000 * Spells.Count / 3;

                    int I = 0;

                    while (Spells.Count > Count)
                    {
                        int Rnd = XorShift.Next(Spells.Count);
                        Spell Spell = Spells[Rnd];
                        SpellData SpellData = Spell.Data;
                        Spell Existing = Home.GetSpellByData(SpellData);

                        ++I;

                        if (Existing != null)
                        {
                            if (Spells.Count >= 2)
                            {
                                int J = 0;
                                int K = 0;

                                Spell Existing2 = null;

                                while (true)
                                {
                                    K = (Rnd + ++J) % Spells.Count;
                                    Existing2 = Home.GetSpellByData(Spells[K].Data);

                                    if (Existing2 != null)
                                    {
                                        if (Existing != Existing2)
                                        {
                                            int LvlIdx1 = Existing.LevelIndexIfAllMaterialUsed;
                                            int LvlIdx2 = Existing2.LevelIndexIfAllMaterialUsed;

                                            if ((I >= Multiplier2) | (RndCatchupChance >= CatchupChance) | (LvlIdx1 <= LvlIdx2))
                                            {
                                                if (Existing.Data.RarityData == Existing2.Data.RarityData)
                                                {
                                                    break;
                                                }
                                            }
                                        }
                                    }
                                    else
                                    {
                                        if (I >= Multiplier3 || Home.LockedSpellCount > 1)
                                        {
                                            if (Existing.Data.RarityData == Spells[K].Data.RarityData)
                                            {
                                                break;
                                            }
                                        }
                                    }

                                    if (J >= Spells.Count)
                                    {
                                        goto End;
                                    }
                                }

                                Spell.SetMaterialCount(Spell.Count + Spells[K].Count);
                                Spells.RemoveAt(K);
                            }
                        }

                        End:

                        if (I >= Multiplier1)
                        {
                            break;
                        }
                    }
                }
            }
        }

        /// <summary>
        ///     Randomizes the reward.
        /// </summary>
        public static Reward RandomizeReward(Chest Chest, Home Home)
        {
            TreasureChestData ChestData = Chest.Data;
            Reward Reward = new Reward();

            Reward.Spells = RewardRandomizer.RandomizeSpells(ChestData, Home, Chest.OverrideCards > 0 ? Chest.OverrideCards : (int?)null);

            int MaxGold = Chest.OverrideGold > 0 ? Chest.OverrideGold : ChestData.MaxGold;
            int MinGold = Chest.OverrideGold > 0 ? Chest.OverrideGold : ChestData.MinGold;

            if (MaxGold > 0)
            {
                if (MaxGold == MinGold)
                {
                    Reward.Gold = MaxGold;
                }
                else
                {
                    Reward.Gold = XorShift.Next(MinGold, MaxGold + 1);
                }
            }

            if (ChestData.RootChest.Name == "Free")
                Reward.Diamonds = Globals.FreeChestDiamondLoop[Home.FreeChestIdx % Globals.FreeChestDiamondLoop.Length];
            else if (ChestData.RootChest.Name == "Star")
                Reward.Diamonds = Globals.CrownDiamondLoop[Home.CrownChestIdx % Globals.CrownDiamondLoop.Length];

            // Shop chests with rerolls (Lightning) are interactive rewards in client 3.830: the client shows the strikes.
            if (Chest.ShopChest != null && Chest.ShopChest.NumRerolls > 0)
            {
                return new InteractiveReward(Reward)
                {
                    RerollSeed = XorShift.Next(1, int.MaxValue),
                    Rerolls    = Chest.ShopChest.NumRerolls
                };
            }

            return Reward;
        }

        /// <summary>
        ///     Randomizes spells.
        /// </summary>
        public static List<Spell> RandomizeSpells(TreasureChestData Data, Home Home, int? countOverride = null)
        {
            List<Spell> Spells = new List<Spell>();

            if (Data.GuaranteedSpellsData.Length > 0)
            {
                for (int I = 0; I < Data.GuaranteedSpellsData.Length; I++)
                {
                    Spell Spell = RewardRandomizer.CreateSpell(Data.GuaranteedSpellsData[I]);
                    Spell.SetMaterialCount(1);
                    Spells.Add(Spell);
                }
            }

            int totalCards = countOverride ?? Data.RandomSpellCount;
            int RandomSpellCount = totalCards;
            SpellSet SpellSet = new SpellSet(Data.ArenaData, null);
            CsvTable RaritiesTable = CsvFiles.Get(Gamefile.Rarities);
            int[] CountByRarity = new int[RaritiesTable.Datas.Count];

            for (int I = 1; I < CountByRarity.Length; I++)
            {
                int Chance = Data.GetChanceForRarity((RarityData) RaritiesTable.Datas[I]);

                if (Chance > 0)
                {
                    if (RandomSpellCount > 0)
                    {
                        int Cnt = System.Math.Min(RandomSpellCount, totalCards / Chance);

                        CountByRarity[I] = Cnt;
                        RandomSpellCount -= Cnt;

                        if (RandomSpellCount > 0 && XorShift.Next(Chance) < totalCards % Chance)
                        {
                            ++CountByRarity[I];
                            --RandomSpellCount;
                        }
                    }
                }
            }

            CountByRarity[0] = RandomSpellCount;

            for (int I = 0; I < CountByRarity.Length; I++)
            {
                if (CountByRarity[I] == 0) continue;
                if (SpellSet.Spells[I].Count == 0)
                    throw new System.InvalidOperationException("No eligible cards for " + Data.Name + " rarity " + I);
                for (int K = 0; K < CountByRarity[I]; K++)
                {
                    SpellData selected = SpellSet.GetRandomSpell(RaritiesTable.GetWithInstanceId<RarityData>(I));
                    Spell existing = Spells.Find(s => s.Data == selected);
                    if (existing == null)
                    {
                        existing = RewardRandomizer.CreateSpell(selected);
                        Spells.Add(existing);
                    }
                    // Reward stacks must not be capped by collection storage limits.
                    existing.SetMaterialCount(existing.Count + 1);
                }
            }

            if (Data.GuaranteedSpellsData.Length == 0)
                RewardRandomizer.CombineSpells(Spells, Data.DifferentSpellCount, CountByRarity, Home);
            // Collection ownership must not leave a chest with more stacks than its UI allows.
            while (Data.DifferentSpellCount > 0 && Spells.Count > Data.DifferentSpellCount)
            {
                int source = -1, target = -1;
                for (int i = 0; i < Spells.Count && source < 0; i++)
                {
                    if (System.Array.IndexOf(Data.GuaranteedSpellsData, Spells[i].Data) >= 0) continue;
                    for (int j = 0; j < Spells.Count; j++)
                        if (i != j && Spells[i].Data.RarityData == Spells[j].Data.RarityData)
                        { source = i; target = j; break; }
                }
                if (source < 0) break;
                Spells[target].SetMaterialCount(Spells[target].Count + Spells[source].Count);
                Spells.RemoveAt(source);
            }

            for (int I = 0; I < Spells.Count; I++)
            {
                // TODO : Implement Sort Spells.
            }

            return Spells;
        }
    }
}
namespace ClashRoyale.Logic.Home
{
    using System;
    using System.Collections.Generic;

    using ClashRoyale.Extensions;
    using ClashRoyale.Extensions.Game;
    using ClashRoyale.Extensions.Helper;
    using ClashRoyale.Files.Csv;
    using ClashRoyale.Files.Csv.Logic;
    using ClashRoyale.Logic.Converters;
    using ClashRoyale.Logic.Home.Spells;
    using ClashRoyale.Logic.Mode;
    using ClashRoyale.Logic.Player;
    using ClashRoyale.Logic.Shop.Items;
    using ClashRoyale.Logic.Time;
    using Newtonsoft.Json;

    using Math = ClashRoyale.Maths.Math;

    [JsonObject(MemberSerialization.OptIn)]
    public class Home
    {
        public GameMode GameMode;

        [JsonProperty("highId")] public int HighId;
        [JsonProperty("lowId")] public int LowId;

        // SHOP

        [JsonProperty("shopSeed")] public int ShopSeed;
        [JsonProperty("shopDay")] public int ShopDay;
        [JsonProperty("shopDaySeen")] public int ShopDaySeen;
        [JsonProperty("dayOfYear")] public int DayOfYear;

        [JsonProperty("shopTimer")] public Timer ShopTimer;

        [JsonProperty("shopItems")] public List<ShopItem> ShopItems;
        [JsonProperty("shopChests")] public List<ChestEvent> ShopChests;

        // CHEST

        [JsonProperty("runningChestId")] public int RunningChestId;

        [JsonProperty("freeChest")] public Chest FreeChest;
        [JsonProperty("starChest")] public Chest StarChest;
        [JsonProperty("shopChest")] public Chest ShopChest;
        [JsonProperty("clanCrownChest")] public Chest ClanCrownChest;
        [JsonProperty("purchasedChest")] public Chest PurchasedChest;

        [JsonProperty("chests")] public List<Chest> Chests;

        [JsonProperty("starChestCooldown")] public bool StarChestCooldown;

        [JsonProperty("chestSlotCnt")] public int ChestSlotCount;
        [JsonProperty("freeChestCnt")] public int FreeChestCount;
        [JsonProperty("starChestCounter")] public int StarChestCounter;
        [JsonProperty("crownChestIdx")] public int CrownChestIdx;
        [JsonProperty("freeChestIdx")] public int FreeChestIdx;

        [JsonProperty("freeChestTimer")] public Timer FreeChestTimer;
        [JsonProperty("starChestTimer")] public Timer StarChestTimer;

        // SEEN

        [JsonProperty("pageOpenedBits")] public int PageOpened;
        [JsonProperty("lastLevelUpPopup")] public int LastLevelUpPopup;

        [JsonProperty("lastArena")] public CsvData LastArena;

        [JsonProperty("newArenasSeen")] public List<CsvData> NewArenasSeen;
        [JsonProperty("replaysSeen")] public List<int> ReplaysSeen;

        // PACKS

        [JsonProperty("boughtPacks")] public List<CsvData> BoughtPacks;

        // SPELL

        [JsonConverter(typeof(SavedDecksConverter))]
        [JsonProperty("savedDecks")] public int[][] SavedDecks;

        [JsonProperty("selectedDeck")] public int SelectedDeck;


        [JsonProperty("decks")] public SpellDeck SpellDeck;
        [JsonProperty("collection")] public SpellCollection SpellCollection;

        // OTHER TIMER

        [JsonProperty("donationCooldownTimer")] public Timer DonationCapacityCooldownTimer;
        [JsonProperty("requestCooldownTimer")] public Timer RequestCooldownTimer;
        [JsonProperty("shareTimer")] public Timer ShareTimer;
        [JsonProperty("sendMail")] public Timer SendMailTimer;
        [JsonProperty("elderKick")] public Timer ElderKickTimer;

        // TV

        // EVENT

        // TOURNAMENT

        // TUTORIAL

        public int Tutorial;

        // OTHER

        private bool ClaimingReward;

        public int DonationCapacityLimit;
        public DateTime LastTick;

        /// <summary>
        /// Gets the home ID.
        /// </summary>
        public long HomeId
        {
            get
            {
                return (long)this.HighId << 32 | (uint)this.LowId;
            }
        }

        /// <summary>
        /// Gets the checksum of this instance.
        /// </summary>
        public int Checksum
        {
            get
            {
                return this.RunningChestId + (this.SpellCollection.Count << 16);
            }
        }

        /// <summary>
        /// Gets the number of assigned workers.
        /// </summary>
        public int AssignedWorkers
        {
            get
            {
                int Count = 0;

                for (int I = 0; I < this.Chests.Count; I++)
                {
                    if (this.Chests[I] != null)
                    {
                        if (this.Chests[I].IsUnlocking)
                        {
                            ++Count;
                        }
                    }
                }

                return Count;
            }
        }

        /// <summary>
        /// Gets the number of free workers.
        /// </summary>
        public int FreeWorkers
        {
            get
            {
                return Globals.MaxChestOpening - this.AssignedWorkers;
            }
        }

        /// <summary>
        /// Gets total seconds since last save.
        /// </summary>
        public int SecondsSinceLastSave
        {
            get
            {
                return Math.Max((int)DateTime.UtcNow.Subtract(this.LastTick).TotalSeconds, 0);
            }
        }

        /// <summary>
        /// Gets the number of spells.
        /// </summary>
        public int SpellCount
        {
            get
            {
                return this.SpellDeck.SpellCount + this.SpellCollection.Count;
            }
        }

        /// <summary>
        /// Gets the number of locked spell.
        /// </summary>
        public int LockedSpellCount
        {
            get
            {
                int Count = 0;

                CsvFiles.Spells.ForEach(SpellData =>
                {
                    if (!this.HasSpell(SpellData))
                    {
                        ++Count;
                    }
                });

                return Count;
            }
        }

        /// <summary>
        /// Gets if the player has all cards full.
        /// </summary>
        public bool HasAllCardsFull
        {
            get
            {
                for (int I = this.SpellCount - 1; I >= 0; I--)
                {
                    // TODO : Implement ClientHome::hasAllCardsFull().
                }

                return true;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Home"/> class.
        /// </summary>
        public Home()
        {
            // SHOP

            this.ShopItems = new List<ShopItem>(8);
            this.ShopChests = new List<ChestEvent>(3);

            this.ShopTimer = new Timer();
            this.ShopTimer.StartTimer(86400);

            this.ShopDay = DateTime.UtcNow.Day;
            this.ShopDaySeen = DateTime.UtcNow.Day;
            this.ShopSeed = 346;

            // CHEST

            this.FreeChestTimer = new Timer();
            this.StarChestTimer = new Timer();

            this.Chests = new List<Chest>(8);

            // SEEN

            this.LastLevelUpPopup = 1;
            this.ReplaysSeen = new List<int>(15);
            this.NewArenasSeen = new List<CsvData>(1); // 54000010

            // PACKS

            this.BoughtPacks = new List<CsvData>(0);

            // SPELL

            this.SpellDeck = new SpellDeck();
            this.SpellCollection = new SpellCollection();

            this.SavedDecks = new int[5][];

            for (int i = 0; i < 5; i++)
            {
                this.SavedDecks[i] = new int[8];
            }

            // OTHER TIMER

            this.DonationCapacityCooldownTimer = new Timer();
            this.RequestCooldownTimer = new Timer();
            this.ShareTimer = new Timer();
            this.SendMailTimer = new Timer();
            this.ElderKickTimer = new Timer();

            // TV

            // EVENT

            // TOURNAMENT

            // TUTORIAL
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Home"/> class.
        /// </summary>
        /// <param name="HighId">The high identifier.</param>
        /// <param name="LowId">The low identifier.</param>
        public Home(int HighId, int LowId) : this()
        {
            this.HighId = HighId;
            this.LowId = LowId;
        }

        /// <summary>
        /// Adds the specified spell.
        /// </summary>
        public void AddSpell(Spell Spell)
        {
            if (!this.HasSpell(Spell.Data))
            {
                int DeckSpellCount = this.SpellDeck.SpellCount;

                if (DeckSpellCount != 8)
                {
                    this.SpellDeck.SetSpell(this.SpellDeck.SpellCount, Spell);
                }
                else
                {
                    this.SpellCollection.AddSpell(Spell);
                }

                return;
            }

            Logging.Error(this.GetType(), "Trying to add spell that already exists in collection, data:" + Spell.Data.GlobalId);
        }

        /// <summary>
        /// Adds the specified chest.
        /// </summary>
        public void AddChest(Chest Chest, int Idx)
        {
            // TODO : Implement AddChest().
        }

        /// <summary>
        /// Clears the shop item.
        /// </summary>
        public void ClearShopItems()
        {
            this.ShopItems.Clear();
        }

        /// <summary>
        /// Clears the shop item.
        /// </summary>
        public void CreateFreeChest()
        {
            if (this.FreeChest != null)
            {
                Logging.Info(this.GetType(), "CreateFreeChest() - _FreeChest should be null");
            }

            this.FreeChest = new Chest(this.GameMode.Player.Arena.FreeChestData);
            ++this.RunningChestId;
        }

        /// <summary>
        /// Called when the specified chest has been purchased.
        /// </summary>
        public void ChestPurchased(TreasureChestData Data, int Source)
        {
            if (Data == null)
            {
                Logging.Error(this.GetType(), "ChestPurchased() - chest data is NULL");
                return;
            }

            if (this.PurchasedChest != null)
            {
                Logging.Error(this.GetType(), "ChestPurchased() - Previous purchased chest not collected");
                return;
            }

            ++this.RunningChestId;

            this.PurchasedChest = new Chest(Data);
            this.PurchasedChest.SetSource(Source);
            this.PurchasedChest.SetClaimed(this.GameMode, 4);
        }

        /// <summary>
        /// Called when the crown chest has been collected.
        /// </summary>
        public void CrownChestCollected()
        {
            this.StarChest = null;

            if (this.StarChestTimer.IsFinished)
            {
                this.StartStarChestTimer(0);
                return;
            }

            this.StarChestCooldown = true;
        }

        /// <summary>
        /// Called when the free chest has been collected.
        /// </summary>
        public void FreeChestCollected()
        {
            this.FreeChest = null;
            this.FreeChestCount++;
        }

        /// <summary>
        /// Called when the purchased chest has been collected.
        /// </summary>
        public void PurchasedChestCollected()
        {
            this.PurchasedChest = null;
        }

        /// <summary>
        /// Gets the spell at specified index.
        /// </summary>
        public Spell GetSpellAt(int Index)
        {
            int DeckSpellCount = this.SpellDeck.SpellCount;

            if (DeckSpellCount > Index)
            {
                return this.SpellDeck[Index];
            }

            if (this.SpellCollection.Count > Index - DeckSpellCount)
            {
                return this.SpellCollection[Index];
            }

            Logging.Error(this.GetType(), "GetSpellAt() - Index out of bounds.");

            return null;
        }

        /// <summary>
        /// Gets spell by data.
        /// </summary>
        public Spell GetSpellByData(SpellData Data)
        {
            Spell Spell = this.SpellDeck.GetSpellByData(Data);

            if (Spell == null)
            {
                Spell = this.SpellCollection.GetSpellByData(Data);
            }

            return Spell;
        }

        /// <summary>
        /// Gets if the player have the specified spell.
        /// </summary>
        public bool HasSpell(SpellData Data)
        {
            return this.GetSpellByData(Data) != null;
        }

        /// <summary>
        /// Adds all spells not owned by the player.
        /// </summary>
        public void AddAllSpells()
        {
            foreach (SpellData SpellData in CsvFiles.Spells)
            {
                if (this.HasSpell(SpellData))
                {
                    continue;
                }

                this.AddSpell(new Spell(SpellData));
            }
        }

        /// <summary>
        /// Max all spells owned by the player.
        /// </summary>
        public void MaxAllSpells()
        {
            foreach (var Spell in this.SpellDeck.GetSpells())
            {
                Spell.SetMaterialCount(0);
                Spell.Level = 99;
            }

            foreach (var Spell in this.SpellCollection.GetSpells())
            {
                Spell.SetMaterialCount(0);
                Spell.Level = 99;
            }
        }

        /// <summary>
        /// Increases the number of stars for star chest.
        /// </summary>
        public void IncreaseStarsToStarChest(int Count)
        {
            if (this.StarChest != null)
            {
                if (!this.StarChestCooldown)
                {
                    this.StarChestCounter = Math.Min(this.StarChestCounter + Count, Globals.CrownChestCrownCount);
                }
            }
        }

        /// <summary>
        /// Called after loading of state for refresh this instance.
        /// </summary>
        public void LoadingFinished()
        {
            this.SaveCurrentDeckTo(this.SelectedDeck);

            if (Globals.RefreshArenaInLoadingFinished)
            {
                this.GameMode.Player.RefreshArena();
            }

            this.UpdateChestCountToPlayer();
            this.UpdateCardCountToPlayer();

            if (this.GameMode.Player.IsInAlliance)
            {
                this.GameMode.AchievementManager.UpdateAchievementProgress(0, 1);
            }
        }

        /// <summary>
        /// Sets if the player claim a reward.
        /// </summary>
        public void SetClaimingReward(bool Value)
        {
            this.ClaimingReward = Value;
        }

        /// <summary>
        /// Sets the last shown level up.
        /// </summary>
        public void SetLastShownLevelUp(int ExpLevel)
        {
            this.LastLevelUpPopup = ExpLevel;
        }

        /// <summary>
        /// Sets the opened page.
        /// </summary>
        public void SetPageOpened(int Page)
        {
            this.PageOpened |= 1 << Page;
        }

        /// <summary>
        /// Sets the shop weekday seen.
        /// </summary>
        public void SetShopWeekdayIndexSeen(int Day)
        {
            this.ShopDaySeen = Day;
        }

        /// <summary>
        /// Sets the tutorial to finish step.
        /// </summary>
        public void SetTutorialFinished(TutorialData Data)
        {
            // TODO : Implement LogicClientHome::setTutorialFinished().
        }

        /// <summary>
        /// Starts the donation cooldown.
        /// </summary>
        public void StartDonationCooldown(int Time)
        {
            this.RequestCooldownTimer.StartTimer(Time);
        }

        /// <summary>
        /// Starts the start chest timer.
        /// </summary>
        public void StartFreeChestTimer(int SkipSeconds)
        {
            this.FreeChestTimer.StartTimer(3600 * Globals.FreeChestIntervalHours - this.FreeChestTimer.RemainingSeconds - SkipSeconds);
        }

        /// <summary>
        /// Starts the start chest timer.
        /// </summary>
        public void StartStarChestTimer(int SkipSeconds)
        {
            if (!this.StarChestTimer.IsFinished)
            {
                Logging.Error(this.GetType(), "StartStarChestTimer() - Timer still runing");
            }

            this.StarChestTimer.StartTimer(3600 * Globals.CrownChestCooldownHours - this.StarChestTimer.RemainingSeconds - SkipSeconds);
        }

        /// <summary>
        /// Updates the number of chest to player.
        /// </summary>
        public void UpdateArenaFromPlayer()
        {
            Player Player = this.GameMode.Player;

            if (Player != null)
            {
                this.LastArena = Player.Arena;
            }
        }

        /// <summary>
        /// Updates the number of card to player.
        /// </summary>
        public void UpdateCardCountToPlayer()
        {
            Player Player = this.GameMode.Player;

            if (Player != null)
            {
                Player.SetCardsFound(this.SpellCount);
            }
        }

        /// <summary>
        /// Updates the number of chest to player.
        /// </summary>
        public void UpdateChestCountToPlayer()
        {
            Player Player = this.GameMode.Player;

            if (Player != null)
            {
                Player.SetChestCount(this.Chests.Count);
            }
        }

        /// <summary>
        /// Updates the free chest diamond index.
        /// </summary>
        public void UpdateFreeChestDiamondIndex()
        {
            this.FreeChestIdx += 1;
            this.FreeChestIdx %= Globals.FreeChestDiamondLoop.Length;
        }

        /// <summary>
        /// Updates the crown chest diamond index.
        /// </summary>
        public void UpdateCrownChestDiamondIndex()
        {
            this.CrownChestIdx += 1;
            this.CrownChestIdx %= Globals.FreeChestDiamondLoop.Length;
        }

        /// <summary>
        /// Updates the stars from avatar.
        /// </summary>
        public void UpdateStarsFromAvatar()
        {
            if (this.GameMode != null)
            {
                Player Player = this.GameMode.Player;

                if (Player != null)
                {
                    int StarCount = Player.StarCount;

                    if (StarCount > 0)
                    {
                        Player.SetStarCount(0);
                        this.IncreaseStarsToStarChest(StarCount);
                    }
                }
            }
        }

        /// <summary>
        /// Saves the current deco to the specified preset.
        /// </summary>
        public void SaveCurrentDeckTo(int Index)
        {
            if (Index >= 5)
            {
                Logging.Error(this.GetType(), "SaveCurrentDeckTo() - deckIdx out of range.");
                return;
            }

            for (int I = 0; I < 8; I++)
            {
                if (this.SpellDeck[I] != null)
                {
                    this.SavedDecks[Index][I] = this.SpellDeck[I].Data.GlobalId;
                }
                else
                {
                    this.SavedDecks[Index][I] = 0;
                }
            }
        }

        /// <summary>
        /// Sets the selected deck.
        /// </summary>
        public void SetSelectedDeck(int Index)
        {
            if (Globals.MultipleDecks)
            {
                if (Index >= 5)
                {
                    Logging.Error(this.GetType(), "SetSelectedDeck() - deckIdx out of range.");
                    return;
                }

                if (this.SelectedDeck != Index)
                {
                    if (Array.TrueForAll(this.SavedDecks[Index], Id => Id == 0))
                    {
                        this.SaveCurrentDeckTo(Index);
                        return;
                    }

                    for (int I = 0; I < 8; I++)
                    {
                        SpellData Data = CsvFiles.Spells.Find(T => T.GlobalId == this.SavedDecks[Index][I]);

                        if (Data != null)
                        {
                            int SpellIdx = this.SpellDeck.GetSpellIdxByData(Data);

                            if (SpellIdx == -1)
                            {
                                SpellIdx = this.SpellCollection.GetSpellIdxByData(Data);

                                if (SpellIdx == -1)
                                {
                                    if (this.SpellCollection.Count < 1)
                                    {
                                        Spell Spell = this.SpellDeck[I];

                                        if (Spell != null)
                                        {
                                            this.SavedDecks[Index][I] = Spell.Data.GlobalId;
                                        }
                                        else
                                        {
                                            this.SavedDecks[Index][I] = -1;
                                        }

                                        continue;
                                    }

                                    this.SavedDecks[Index][I] = this.SpellCollection[0].Data.GlobalId;
                                }

                                Spell InDeck = this.SpellDeck[I];
                                Spell InCollection = this.SpellCollection[SpellIdx];

                                if (InDeck != null)
                                {
                                    this.SpellCollection.SetSpell(SpellIdx, InDeck);
                                }
                                else
                                {
                                    this.SpellCollection.RemoveSpell(SpellIdx);
                                }

                                this.SpellDeck.SetSpell(I, InCollection);
                            }
                            else if (I != SpellIdx)
                            {
                                this.SpellDeck.SwapSpells(SpellIdx, I);
                            }
                        }
                    }
                }

                this.SelectedDeck = Index;
            }
        }

        /// <summary>
        /// Creates a fast forward.
        /// </summary>
        public void FastForward(int Seconds)
        {
            for (int I = 0; I < this.Chests.Count; I++)
            {
                if (this.Chests[I] != null)
                {
                    this.Chests[I].FastForward(Seconds);
                }
            }

            if (this.FreeChestCount == 0)
            {
                if (this.FreeChest == null)
                {
                    this.CreateFreeChest();
                }
            }

            if (this.FreeChestTimer.RemainingSeconds <= Seconds)
            {
                int Remaining = this.StarChestTimer.RemainingSeconds;

                this.FreeChestTimer.Reset();

                if (this.FreeChest == null)
                {
                    if (this.FreeChestTimer.IsFinished)
                    {
                        this.CreateFreeChest();
                        this.StartFreeChestTimer(Seconds - Remaining);
                    }
                }
            }
            else
            {
                this.FreeChestTimer.FastForward(Seconds);
            }

            this.DonationCapacityCooldownTimer.FastForward(Seconds);
            this.RequestCooldownTimer.FastForward(Seconds);
            this.ElderKickTimer.FastForward(Seconds);
            this.SendMailTimer.FastForward(Seconds);
            this.ShareTimer.FastForward(Seconds);
            this.ShopTimer.FastForward(Seconds);

            if (this.StarChestTimer.RemainingSeconds <= Seconds)
            {
                int Remaining = this.StarChestTimer.RemainingSeconds;

                this.StarChestTimer.Reset();

                if (this.StarChestCooldown)
                {
                    this.StartStarChestTimer(Seconds - Remaining);
                }
            }
            else
            {
                this.StarChestTimer.FastForward(Seconds);
            }
        }

        /// <summary>
        /// Ticks this instance.
        /// </summary>
        public void Tick()
        {
            this.LastTick = DateTime.UtcNow;

            this.FreeChestTimer.Tick();

            if (this.FreeChestCount == 0)
            {
                if (this.FreeChest == null)
                {
                    this.CreateFreeChest();
                }

                this.StartFreeChestTimer(0);
            }

            if (this.FreeChestTimer.IsFinished)
            {
                if (this.FreeChest == null)
                {
                    this.CreateFreeChest();
                    this.StartFreeChestTimer(0);
                }
            }

            if (this.StarChestTimer.IsFinished)
            {
                if (this.StarChestCooldown)
                {
                    this.StartFreeChestTimer(0);
                }

                this.StarChestCooldown = false;
            }
            else
            {
                this.StarChestTimer.Tick();
            }

            if (this.PurchasedChest != null)
            {
                if (!this.ClaimingReward)
                {
                    this.PurchasedChest.SetClaimed(this.GameMode, 4);
                }
            }

            this.DonationCapacityCooldownTimer.Tick();
            this.RequestCooldownTimer.Tick();
            this.ElderKickTimer.Tick();
            this.SendMailTimer.Tick();
            this.ShareTimer.Tick();
            this.ShopTimer.Tick();

            if (this.StarChestCounter >= Globals.CrownChestCrownCount)
            {
                this.StarChest = null;
                this.StarChest = new Chest(this.GameMode.Player.Arena.StarChestData);
            }

            this.UpdateStarsFromAvatar();
            this.UpdateArenaFromPlayer();
        }

        /// <summary>
        /// Encodes this instance.
        /// </summary>
        public void Encode(ByteStream Stream)
        {
            //Stream.AddRange("00-00".HexaToBytes());
            Stream.WriteInt(HighId);
            Stream.WriteInt(LowId);
            Stream.WriteVInt(14);//Unk (3)
            Stream.WriteVInt(0);
            Stream.WriteVInt(0);//Tick 1 (1727700)
            Stream.WriteVInt(0);//Tick 2 (1727700)
            Stream.WriteVInt((int)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

            Stream.WriteVInt(0);
            Stream.WriteVInt(5);//this.device.player.decks.length
            /* this.device.player.decks.forEach(deck => {
                 Stream.WriteVInt(deck.length);
                 deck.forEach(card => {
                     Stream.WriteVInt(card);
                 });
             });*/

            Stream.WriteByte(0xFF);
            //let currentDeck = this.device.player.decks[this.device.player.selectedDeck];
            //currentDeck.forEach(cardSCID => {
            // let card = utils.findObjectByKey(this.device.player.cards, "ID", cardUtils.SCIDtoInstanceID(cardSCID));

            Stream.WriteVInt(1);
            Stream.WriteVInt(5);
            Stream.WriteVInt(0);
            Stream.WriteVInt(30);
            Stream.WriteVInt(0);
            Stream.WriteVInt(0);
            Stream.WriteVInt(0);
            Stream.WriteVInt(0);
            //});

            Stream.WriteVInt(29 - 8);/// player cards lenght

            // this.device.player.cards.forEach(card => {
            // if (!currentDeck.includes(cardUtils.instanceIDtoSCID(card.ID)))
            {
                Stream.WriteVInt(1);//card.ID
                Stream.WriteVInt(5);//card.level
                Stream.WriteVInt(0);
                Stream.WriteVInt(30);//card.xpPoints
                Stream.WriteVInt(0);
                Stream.WriteVInt(0);
                Stream.WriteVInt(0);
                Stream.WriteVInt(0);
                // }
                // });

                Stream.WriteVInt(1);// selected deck    this.device.player.selectedDeck
                Stream.WriteVInt(0);

                Stream.WriteByte(127);
                Stream.WriteByte(127);
                Stream.WriteByte(127);
                Stream.WriteVInt((int)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                Stream.WriteVInt(1);
                Stream.WriteVInt(0);
                Stream.WriteVInt(6);


                Stream.WriteVInt(1109);
                Stream.WriteString("2v2 Button");
                Stream.WriteVInt(8);
                Stream.WriteVInt(1503298800);
                Stream.WriteVInt(1534834800);
                Stream.WriteVInt(1502694000);
                Stream.WriteInt(0);
                Stream.WriteInt(0);
                Stream.WriteString("2v2 Button");
                Stream.WriteString("{\"HideTimer\":true,\"HidePopupTimer\":true,\"ExtraGameModeChance\":0,\"GameMode\":\"TeamVsTeamLadder\",\"ExtraGameMode\":\"None\"}");

                Stream.WriteByte(0);

                Stream.WriteVInt(1305);
                Stream.WriteString("1v1 Draft Friendly ");
                Stream.WriteVInt(5);
                Stream.WriteVInt(1517472000);
                Stream.WriteVInt(1522569600);
                Stream.WriteVInt(1517472000);
                Stream.WriteInt(0);
                Stream.WriteInt(0);
                Stream.WriteString("1v1 Draft Friendly ");
                Stream.WriteString("{\"Title\":\"Amichevole strategica 1v1\",\"Subtitle\":\"Gioca coi membri del clan!\",\"GameMode\":\"DraftMode\",\"Background\":{\"Path\":\"/62ce7186-d295-4100-9b4e-58d86d3a854c_friendly_draft.png\",\"Checksum\":\"71c0065635a551b109400a766cbdabfc\",\"File\":\"friendly_draft.png\"},\"DraftDeck\":\"Draft_v1\"}");
                Stream.WriteByte(0);

                Stream.WriteVInt(1343);
                Stream.WriteString("Clash Royale League Challenge Header");//to start again
                Stream.WriteVInt(13);
                Stream.WriteVInt(1521014400);
                Stream.WriteVInt(1521446400);
                Stream.WriteVInt(1521014400);
                Stream.WriteInt(0);
                Stream.WriteInt(0);
                Stream.WriteString("Clash Royale League Challenge Header");
                Stream.WriteString("{\"Image\":{\"Path\":\"/5231460d-4307-4a3e-b989-8bbf2e6f8db7_cr_league_challenge_header.png\",\"Checksum\":\"45440c7c948e9175f69030bde7d2c630\",\"File\":\"cr_league_challenge_header.png\"},\"TitleGlow\":true,\"Icon\":\"icon_tournament_special_grand\",\"Title\":\"Clash Royale League\"}");
                Stream.WriteByte(0);

                Stream.WriteVInt(1344);
                Stream.WriteString("Clash Royale League Challenge");
                Stream.WriteVInt(2);
                Stream.WriteVInt(1521014400);
                Stream.WriteVInt(1521446400);
                Stream.WriteVInt(1520841600);
                Stream.WriteInt(0);
                Stream.WriteInt(0);
                Stream.WriteString("Clash Royale League Challenge");
                Stream.WriteString("{\"GameMode\":\"Challenge\",\"Title\":\"Sfida Clash Royale League\",\"FreePass\":3,\"JoinCost\":10,\"JoinCostResource\":\"Gems\",\"MaxLosses\":3,\"Rewards\":[{\"Gold\":20,\"Cards\":2},{\"Gold\":30,\"Cards\":3},{\"Gold\":50,\"Milestone\":{\"Type\":\"Gold\",\"Amount\":500},\"Cards\":5},{\"Gold\":80,\"Cards\":8},{\"Gold\":120,\"Milestone\":{\"Type\":\"RandomSpell\",\"Amount\":15,\"RandomSpell\":\"Rare\"},\"Cards\":12},{\"Gold\":170,\"Cards\":17},{\"Gold\":230,\"Milestone\":{\"Type\":\"Gold\",\"Amount\":1500},\"Cards\":23},{\"Gold\":300,\"Cards\":30},{\"Gold\":380,\"Milestone\":{\"Chest\":\"Giant_<max_arena>\",\"Type\":\"Chest\"},\"Cards\":38},{\"Gold\":470,\"Cards\":47},{\"Gold\":570,\"Milestone\":{\"Type\":\"Gold\",\"Amount\":5000},\"Cards\":57},{\"Gold\":680,\"Cards\":68},{\"Gold\":800,\"Milestone\":{\"Chest\":\"Magic_<max_arena>\",\"Type\":\"Chest\"},\"Cards\":80},{\"Gold\":930,\"Cards\":93},{\"Gold\":1070,\"Milestone\":{\"Type\":\"Gold\",\"Amount\":10000},\"Cards\":107},{\"Gold\":1220,\"Cards\":122},{\"Gold\":1400,\"Milestone\":{\"Type\":\"RandomSpell\",\"Amount\":10,\"RandomSpell\":\"Epic\"},\"Cards\":140},{\"Gold\":1650,\"Cards\":165},{\"Gold\":2000,\"Milestone\":{\"Chest\":\"Legendary\",\"Type\":\"Chest\"},\"Cards\":200},{\"Gold\":2450,\"Cards\":245},{\"Gold\":3000,\"Milestone\":{\"IsHighlighted\":true,\"Type\":\"Gold\",\"Amount\":200000},\"Cards\":300}],\"IconExportName\":\"icon_tournament_special_grand\",\"WinIconExportName\":\"tournament_open_wins_badge_gold\",\"Subtitle\":\"Ecco come funziona\",\"Description\":\"Arriva la Clash Royale League: la tua occasione per diventare un giocatore professionista! Raggiungi 20 vittorie per proseguire nella competizione. Troverai altre informazioni nella tua posta in arrivo in gioco!\",\"Background\":{\"Path\":\"/da8810e1-e57c-4d9e-a854-9dd41b38fd81_cr_league_challenge.png\",\"Checksum\":\"a1d8b04a8e911ed1e21b0aba5cb25b85\",\"File\":\"cr_league_challenge.png\"},\"Background_Complete\":{\"Path\":\"/da8810e1-e57c-4d9e-a854-9dd41b38fd81_cr_league_challenge.png\",\"Checksum\":\"3e1910cc44d36e1bacbb2b26ec3b51cf\",\"File\":\"cr_league_challenge.png\"},\"UnlockedForXP\":\"Experienced\",\"EndNotification\":\"La sfida Clash Royale League termina tra 2 ore!\"}");
                Stream.WriteByte(0);

                Stream.WriteVInt(1345);
                Stream.WriteString("Boosts");
                Stream.WriteVInt(10);
                Stream.WriteVInt(1521187200);
                Stream.WriteVInt(1521446400);
                Stream.WriteVInt(1521187200);
                Stream.WriteInt(0);
                Stream.WriteInt(0);
                Stream.WriteString("Boosts");
                Stream.WriteString("{\"Title\":\"Potenziamenti!\",\"Boosts\":[{\"Type\":\"VictoryGold\",\"durationInHours\":168,\"Cost\":300},{\"Type\":\"CrownChestCards\",\"durationInHours\":168,\"Cost\":300},{\"Type\":\"ChestSpeedUp\",\"durationInHours\":168,\"Cost\":300}]}");
                Stream.WriteByte(0);

                Stream.WriteVInt(1346);
                Stream.WriteString("Barbarian Barrel Draft Challenge ");
                Stream.WriteVInt(2);
                Stream.WriteVInt(1521792000);
                Stream.WriteVInt(1522051200);
                Stream.WriteVInt(1521705600);
                Stream.WriteInt(0);
                Stream.WriteInt(0);
                Stream.WriteString("Barbarian Barrel Draft Challenge ");
                Stream.WriteString("{\"Casual\":false,\"GameMode\":\"CardReleaseDraft\",\"Title\":\"Sfida strategica barile barbarico\",\"FreePass\":1,\"JoinCost\":100,\"JoinCostResource\":\"Gems\",\"MaxLosses\":3,\"Rewards\":[{\"Gold\":700,\"Cards\":10},{\"Gold\":950,\"Cards\":15},{\"Gold\":1250,\"Milestone\":{\"Type\":\"Gold\",\"Amount\":1000},\"Cards\":25},{\"Gold\":1600,\"Cards\":43},{\"Gold\":2000,\"Milestone\":{\"Type\":\"RandomSpell\",\"Amount\":20,\"RandomSpell\":\"Rare\"},\"Cards\":65},{\"Gold\":2500,\"Cards\":93},{\"Gold\":3100,\"Milestone\":{\"Type\":\"Gold\",\"Amount\":3000},\"Cards\":125},{\"Gold\":3800,\"Cards\":165},{\"Gold\":4650,\"Milestone\":{\"Chest\":\"Giant_<max_arena>\",\"Type\":\"Chest\"},\"Cards\":210},{\"Gold\":5750,\"Cards\":265},{\"Gold\":7100,\"Milestone\":{\"Type\":\"Gold\",\"Amount\":9000},\"Cards\":335},{\"Gold\":8750,\"Cards\":430},{\"Gold\":11000,\"Milestone\":{\"IsHighlighted\":true,\"Type\":\"Spell\",\"Amount\":10,\"Spell\":\"BarbLog\"},\"Cards\":550}],\"CardTheme\":\"BarbLog\",\"IconExportName\":\"icon_tournament_card_release_grand\",\"WinIconExportName\":\"tournament_open_wins_badge_gold\",\"Arena\":\"All\",\"Description\":\"Scegli 4 carte e ricevine 4 dall'avversario: uno dei due potrA schierare il barile barbarico! Sblocca la nuova carta con 12 vittorie, ma occhio: con 3 sconfitte sei fuori!\",\"EndNotification\":\"La sfida strategica barile barbarico termina tra due ore! Prova a sbloccare la nuova carta!\",\"StartNotification\":\"La sfida strategica barile barbarico A¨ iniziata! Sblocca subito una nuova carta!\",\"UnlockedForXP\":\"Experienced\",\"DraftDeck\":\"Draft_v1\",\"DraftDeckCardThemeSetOverride\":\"Draft_AllRandom\",\"Subtitle\":\"Ecco come funziona\",\"Subtitle_Short\":\"Sblocca una nuova carta!\",\"Background\":{\"Path\":\"/_barbarian_barrel_challenge.png\",\"Checksum\":\"62bc57e2a6827d434c0ce8a604c3f586\",\"File\":\"barbarian_barrel_challenge.png\"},\"Background_Complete\":{\"Path\":\"/_barbarian_barrel_challenge.png\",\"Checksum\":\"25c6e25d7f322acef312ab50b009b68c\",\"File\":\"barbarian_barrel_challenge.png\"}}");

                Stream.WriteByte(0);

                Stream.AddRange(new byte[] { 0, 0, 0, 0, 0, 0 });
                Stream.WriteByte(127);
                Stream.WriteByte(127);
                Stream.WriteByte(127);
                Stream.WriteVInt(0);
                Stream.WriteVInt(0);
                Stream.WriteVInt(0);

                Stream.WriteVInt(6); //Events count again

                Stream.WriteVInt(1109);
                Stream.WriteVInt(1);

                Stream.WriteVInt(1305);
                Stream.WriteVInt(1);

                Stream.WriteVInt(1343);
                Stream.WriteVInt(1);

                Stream.WriteVInt(1344);
                Stream.WriteVInt(1);

                Stream.WriteVInt(1345);
                Stream.WriteVInt(1);

                Stream.WriteVInt(1346);
                Stream.WriteVInt(1);

                Stream.WriteVInt(6);//Events count again, what's wrong with you Supercell :p

                Stream.WriteVInt(1109);
                Stream.WriteVInt(2);
                Stream.WriteVInt(0);

                Stream.WriteVInt(1305);
                Stream.WriteVInt(2);
                Stream.WriteVInt(0);

                Stream.WriteVInt(1343);
                Stream.WriteVInt(3);
                Stream.WriteVInt(0);

                Stream.WriteVInt(1344);
                Stream.WriteVInt(3);
                Stream.WriteVInt(0);

                Stream.WriteVInt(1345);
                Stream.WriteVInt(3);
                Stream.WriteVInt(0);

                Stream.WriteVInt(1346);
                Stream.WriteVInt(1);
                Stream.WriteVInt(0);

                Stream.WriteVInt(1);

                Stream.WriteString("{'ID':'SHOP_CYCLE_MANAGEMENT', 'Params':{ 'LegendaryChestCycleDuration':14}}");
                Stream.WriteVInt(1);
                Stream.WriteString("{'ID':'CARD_RELEASE_V2', 'Params':{ 'Cards':[{'ShowAsSoon':false,'Spell':'Ghost', 'Date':'20180104'},{'ShowAsSoon':false,'Spell':'EliteArcher', 'Date':'20180302'},{'ShowAsSoon':false,'Spell':'BarbLog', 'Date':'20180406'}]}}");
                Stream.WriteVInt(4);
                Stream.WriteString("{'ID':'CLAN_CHEST', 'Params':{ 'StartTime':'20170317T070000.000Z','ActiveDuration':'P3dT0h', 'InactiveDuration':'P4dT0h', 'ChestType':['ClanCrowns']}}");
                Stream.WriteVInt(1);
                Stream.WriteString("{'ID':'PERMANENT_GAME_MODES', 'Params':{ 'FixedDeckOrderOptionEnabled':true}}");


                Stream.WriteVInt(4); //Chest Slots Count

                /* if (this.device.player.chests && this.device.player.chests.length > 0 && this.device.player.chests.length <= 4)
                 {
                     Stream.WriteVInt(1);
                     this.device.player.chests.forEach(chest => {
                         Stream.WriteVInt(19);
                         Stream.WriteVInt(chest.chestID);

                         Stream.WriteVInt(10); //Is opened??? 0 no 2 yep 4???? 6 opened but locked 8 new with anim 10 new with animation and opened 12 new with animation and closed 14 new with animation and opened but locked 16 unlocking
                         Stream.WriteVInt(this.device.player.chests.indexOf(chest) * 2 + 4); //Chest Index
                         Stream.WriteVInt(6);

                         Stream.WriteVInt(this.device.player.chests.indexOf(chest));
                         Stream.WriteVInt(0);
                         Stream.WriteVInt(0);
                         if (this.device.player.chests)
                         {
                             Stream.WriteVInt(this.device.player.chests.indexOf(chest) === (this.device.player.chests.length - 1) ? 0 : 8);
                         }
                     });
                 }*/
                // else
                //{
                Stream.WriteVInt(0);
                //}

                Stream.WriteVInt(0);
                Stream.WriteVInt(0);
                Stream.WriteVInt((int)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                Stream.WriteVInt(0);
                Stream.WriteVInt(0);
                Stream.WriteByte(127);

                Stream.WriteVInt(1);//Count???
                Stream.WriteVInt(19);
                Stream.WriteVInt(7);
                Stream.WriteVInt(2);
                Stream.WriteVInt(2);
                Stream.WriteVInt(0);
                Stream.WriteByte(127);//Maybe end...

                Stream.AddRange(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 });
                Stream.WriteVInt(10);//Crown Chest Crowns Count
                Stream.WriteVInt(0);//Has been opened (bool)

                Stream.WriteVInt(0);//2x(2xTick + Timestamp)
                Stream.WriteVInt(0);
                Stream.WriteVInt((int)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                Stream.WriteVInt(0);
                Stream.WriteVInt(0);
                Stream.WriteVInt((int)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

                Stream.WriteVInt(0);
                Stream.WriteVInt(0);
                Stream.WriteVInt(0);
                Stream.WriteByte(127);

                Stream.WriteVInt(2817);
                Stream.AddRange(new byte[] { 0, 0, 0, 0, 0, 0, 0 });
                Stream.WriteVInt(2);
                Stream.WriteVInt(1);//PLayer Level
                Stream.WriteVInt(54);
                Stream.WriteVInt(1);
                Stream.WriteVInt(5);
                Stream.WriteVInt(447);
                Stream.WriteVInt(5);
                Stream.WriteVInt(0);
                Stream.WriteVInt(0);
                Stream.WriteVInt((int)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

                Stream.WriteVInt(0);
                Stream.WriteVInt(0);
                Stream.WriteVInt(0);
                Stream.WriteByte(127);

                Stream.WriteVInt(0);
                Stream.WriteVInt(0);
                Stream.WriteByte(127);

                Stream.WriteVInt(0);
                Stream.WriteVInt(0);
                Stream.WriteByte(127);

                Stream.WriteVInt(0);
                Stream.WriteVInt(0);
                Stream.WriteVInt(1);

                Stream.AddRange(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 });
                Stream.WriteVInt(10);
                Stream.WriteVInt(0);
                Stream.WriteVInt(0);
                Stream.WriteVInt(0);
                Stream.WriteVInt(0);


                Stream.AddRange("F8-07".HexaToBytes());
                //currentDeck.forEach(cardSCID => {
                // let card = utils.findObjectByKey(this.device.player.cards, "ID", cardUtils.SCIDtoInstanceID(cardSCID));

                Stream.WriteVInt(1);
                Stream.WriteVInt(5);
                Stream.WriteVInt(0);
                Stream.WriteVInt(30);
                Stream.WriteVInt(0);
                Stream.WriteVInt(0);
                Stream.WriteVInt(0);
                Stream.WriteVInt(0);
                // });

                Stream.WriteVInt(1);
                Stream.WriteVInt(26);
                Stream.WriteVInt(62);
                Stream.WriteVInt(1);
                Stream.WriteVInt(26);
                Stream.WriteVInt(62);

                Stream.AddRange(new byte[] { 0, 0, 0, 0, 0, 0 });

                Stream.WriteVInt(1);
                Stream.WriteVInt((int)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

                Stream.WriteVInt(0);
                Stream.WriteVInt(0);
                Stream.WriteVInt(1);

                Stream.AddRange(new byte[] { 0, 0, 0, 0, 0, 0, 0 });

                Stream.WriteVInt(2);
                Stream.WriteVInt(1);
                Stream.WriteVInt(2);
                Stream.WriteVInt(1);
                Stream.WriteVInt(0);
                Stream.WriteVInt(0);

                //-- Quests --//
                Stream.AddRange("00-00-00-0F-54-49-44-5F-44-41-49-4C-59-5F-47-49-46-54-53-00-00-00-00-03-00-00-00-00-00-11-51-75-65-73-74-5F-46-72-65-65-47-69-66-74-73-31-30-00-00-00-14-54-49-44-5F-46-52-45-45-5F-43-48-45-53-54-5F-51-55-45-53-54-00-00-00-08-73-63-2F-75-69-2E-73-63-00-00-00-15-71-75-65-73-74-5F-69-74-65-6D-5F-66-72-65-65-5F-63-68-65-73-74-05-03-13-07-01-13-07-01-05-01-90-01-03-00-00-00-03-00-04-04-00-00-02-01-00-00-00-00-15-54-49-44-5F-4C-41-44-44-45-52-5F-51-55-45-53-54-5F-50-4C-41-59-00-00-00-00-0A-01-00-00-00-00-1A-50-6C-61-79-5F-51-75-65-73-74-5F-50-6C-61-79-5F-4C-61-64-64-65-72-5F-50-76-50-00-00-00-1A-54-49-44-5F-4C-41-44-44-45-52-5F-51-55-45-53-54-5F-50-4C-41-59-5F-49-4E-46-4F-00-00-00-08-73-63-2F-75-69-2E-73-63-00-00-00-0E-71-75-65-73-74-5F-69-74-65-6D-5F-70-76-70-14-01-0E-00-0A-00-00-01-01-00-00-00-32-01-00-00-BF-06-02-01-00-03-01-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00".HexaToBytes());

                //-- Shop Begins --//
                Stream.WriteVInt(14);
                Stream.WriteVInt(4);
                Stream.WriteVInt(0);
                Stream.WriteVInt(4);
                Stream.WriteVInt(1);
                Stream.WriteVInt(4);
                Stream.WriteVInt(1);
                Stream.WriteVInt(4);
                Stream.WriteVInt(0);
                Stream.WriteVInt(5);
                Stream.WriteVInt(2);
                Stream.WriteVInt(4);
                Stream.WriteVInt(0);
                Stream.WriteVInt(4);
                Stream.WriteVInt(2);
                Stream.WriteVInt(4);
                Stream.WriteVInt(0);
                Stream.WriteVInt(5);
                Stream.WriteVInt(0);
                Stream.WriteVInt(2);
                Stream.WriteVInt(0);
                Stream.WriteVInt(4);
                Stream.WriteVInt(3);
                Stream.WriteVInt(4);
                Stream.WriteVInt(2);
                Stream.WriteVInt(5);
                Stream.WriteVInt(3);
                Stream.WriteVInt(2);
                Stream.WriteVInt(3);
                Stream.WriteVInt(1);
                Stream.WriteVInt(8);
                Stream.WriteByte(127);
                Stream.WriteVInt(0);
                Stream.WriteVInt(0);
                Stream.WriteVInt(5);
                Stream.WriteVInt(3);
                Stream.WriteVInt(3);

                Stream.WriteVInt(0);
                Stream.WriteVInt(0);
                Stream.WriteVInt(447);
                Stream.WriteVInt(0);//Cost
                Stream.WriteVInt(5);//SCID Res Type - Resources
                Stream.WriteVInt(1);//SCID Res ID - Gold
                Stream.WriteVInt(19);//SCID Res Type - Chests
                Stream.WriteVInt(114);//SCID Res ID - Chest ID
                Stream.WriteVInt(0);
                Stream.WriteVInt(1);

                Stream.WriteVInt(0);
                Stream.WriteVInt(1);
                Stream.WriteVInt(447);
                Stream.WriteVInt(50);
                Stream.WriteVInt(5);
                Stream.WriteVInt(1);
                Stream.WriteVInt(26);
                Stream.WriteVInt(0);
                Stream.WriteVInt(5);
                Stream.WriteVInt(0);
                Stream.WriteVInt(0);
                Stream.WriteVInt(3000);
                Stream.WriteVInt(0);
                Stream.WriteVInt(0);
                Stream.WriteVInt(1);

                Stream.WriteVInt(0);
                Stream.WriteVInt(2);
                Stream.WriteVInt(447);
                Stream.WriteVInt(100);
                Stream.WriteVInt(5);
                Stream.WriteVInt(1);
                Stream.WriteVInt(28);
                Stream.WriteVInt(0);
                Stream.WriteVInt(1);
                Stream.WriteVInt(1);
                Stream.WriteVInt(0);
                Stream.WriteVInt(3000);
                Stream.WriteVInt(0);
                Stream.WriteVInt(0);
                //-- Shop Ends --//

                //-- Shop Chests Begins --//
                Stream.WriteVInt(3); //Chests Count

                // #1
                Stream.WriteVInt(19);
                Stream.WriteVInt(306);
                Stream.WriteVInt(88);
                Stream.WriteVInt(0);
                Stream.WriteString("Lightning");
                Stream.WriteVInt(447);
                Stream.WriteVInt(0);//Chest index
                Stream.WriteVInt(0);
                Stream.WriteByte(127);
                Stream.WriteVInt(0);
                Stream.WriteByte(127);

                // #2
                Stream.WriteVInt(19);
                Stream.WriteVInt(318);
                Stream.WriteVInt(88);
                Stream.WriteVInt(1);
                Stream.WriteString("Fortune");
                Stream.WriteVInt(447);
                Stream.WriteVInt(1);//Chest index
                Stream.WriteVInt(0);
                Stream.WriteByte(127);
                Stream.WriteVInt(0);
                Stream.WriteByte(127);

                // #3
                Stream.WriteVInt(19);
                Stream.WriteVInt(330);
                Stream.WriteVInt(88);
                Stream.WriteVInt(3);
                Stream.WriteString("Kings");
                Stream.WriteVInt(447);
                Stream.WriteVInt(2);//Chest index
                Stream.WriteVInt(0);
                Stream.WriteByte(127);
                Stream.WriteVInt(0);
                Stream.WriteByte(127);
                //-- Shop Chests Ends --//

                Stream.AddRange(new byte[] { 0, 0, 0, 0, 0, 0, 0 });
                Stream.WriteByte(127);
                Stream.WriteVInt(1109);
                Stream.WriteVInt(0);
                Stream.WriteVInt(0);

                Stream.WriteVInt(HighId);
                Stream.WriteVInt(LowId);
                Stream.WriteVInt(HighId);
                Stream.WriteVInt(LowId);
                Stream.WriteVInt(HighId);
                Stream.WriteVInt(LowId);
                Stream.WriteString("HuzaBird");// Player Name

                Stream.WriteVInt(0);
                Stream.WriteVInt(2);
                Stream.WriteVInt(3800);//Trophies
                Stream.AddRange(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 });
                Stream.WriteVInt(41);
                Stream.AddRange(new byte[] { 0, 0, 0, 0, 0 });

                Stream.WriteVInt(8); //Resources Prefix???
                Stream.WriteVInt(14); //Resources Count

                Stream.WriteVInt(5);//SCID Resource Type (Resources)
                Stream.WriteVInt(1);//SCID Resource ID (Gold)
                Stream.WriteVInt(999999999);///PLAYER GOLD

                Stream.WriteVInt(5);//SCID Resource Type (Resources)
                Stream.WriteVInt(5);//SCID Resource ID (Gold)
                Stream.WriteVInt(999999999);///PLAYER GOLD

                Stream.WriteVInt(5);//SCID Resource Type (Resources)
                Stream.WriteVInt(29);//SCID Resource ID (GameMode)
                Stream.WriteVInt(72000006);

                Stream.WriteVInt(5);//SCID Resource Type (Resources)
                Stream.WriteVInt(13);//SCID Resource ID (Unknown)
                Stream.WriteVInt(0);

                Stream.WriteVInt(5);//SCID Resource Type (Resources)
                Stream.WriteVInt(33);//SCID Resource ID (Unknown)
                Stream.WriteVInt(0);

                Stream.WriteVInt(5);//SCID Resource Type (Resources)
                Stream.WriteVInt(34);//SCID Resource ID (Unknown)
                Stream.WriteVInt(0);

                Stream.WriteVInt(5);//SCID Resource Type (Resources)
                Stream.WriteVInt(14);//SCID Resource ID (Unknown)
                Stream.WriteVInt(5);

                Stream.WriteVInt(5);//SCID Resource Type (Resources)
                Stream.WriteVInt(38);//SCID Resource ID (Unknown)
                Stream.WriteVInt(0);

                Stream.WriteVInt(5);//SCID Resource Type (Resources)
                Stream.WriteVInt(3);//SCID Resource ID (Unknown)
                Stream.WriteVInt(2);

                Stream.WriteVInt(5);//SCID Resource Type (Resources)
                Stream.WriteVInt(37);//SCID Resource ID (Unknown)
                Stream.WriteVInt(0);

                Stream.WriteVInt(5);//SCID Resource Type (Resources)
                Stream.WriteVInt(2);//SCID Resource ID (Unknown)
                Stream.WriteVInt(6);

                Stream.WriteVInt(5);//SCID Resource Type (Resources)
                Stream.WriteVInt(16);//SCID Resource ID (Unknown)
                Stream.WriteVInt(1);

                Stream.WriteVInt(5);//SCID Resource Type (Resources)
                Stream.WriteVInt(4);//SCID Resource ID (Unknown)
                Stream.WriteVInt(0);

                Stream.WriteVInt(5);//SCID Resource Type (Resources)
                Stream.WriteVInt(28);//SCID Resource ID (Unknown)
                Stream.WriteVInt(0);

                Stream.WriteVInt(0);
                Stream.WriteVInt(6);
                Stream.WriteVInt(60);

                Stream.WriteVInt(7);
                Stream.WriteVInt(9);
                Stream.WriteVInt(60);

                Stream.WriteVInt(8);
                Stream.WriteVInt(9);
                Stream.WriteVInt(60);

                Stream.WriteVInt(9);
                Stream.WriteVInt(9);
                Stream.WriteVInt(60);

                Stream.WriteVInt(4);
                Stream.WriteVInt(1);
                Stream.WriteVInt(60);

                Stream.WriteVInt(5);
                Stream.WriteVInt(1);
                Stream.WriteVInt(60);

                Stream.WriteVInt(6);
                Stream.WriteVInt(1);
                Stream.WriteVInt(0);

                Stream.WriteVInt(6);
                Stream.WriteVInt(5);
                Stream.WriteVInt(11);
                Stream.WriteVInt(41);
                Stream.WriteVInt(5);
                Stream.WriteVInt(8);
                Stream.WriteVInt(9);
                Stream.WriteVInt(5);
                Stream.WriteVInt(27);
                Stream.WriteVInt(1);
                Stream.WriteVInt(5);
                Stream.WriteVInt(7);
                Stream.WriteVInt(1);
                Stream.WriteVInt(5);
                Stream.WriteVInt(6);
                Stream.WriteVInt(30);
                Stream.WriteVInt(5);

                Stream.WriteVInt(9);
                Stream.WriteVInt(26000003);

                Stream.WriteVInt(9);
                Stream.WriteVInt(26);
                Stream.WriteVInt(3);

                Stream.WriteVInt(0);
                Stream.WriteVInt(26);
                Stream.WriteVInt(1);

                Stream.WriteVInt(0);
                Stream.WriteVInt(26);
                Stream.WriteVInt(13);

                Stream.WriteVInt(0);
                Stream.WriteVInt(26);
                Stream.WriteVInt(0);

                Stream.WriteVInt(0);
                Stream.WriteVInt(28);
                Stream.WriteVInt(1);

                Stream.WriteVInt(0);
                Stream.WriteVInt(28);
                Stream.WriteVInt(0);

                Stream.WriteVInt(0);
                Stream.WriteVInt(26);
                Stream.WriteVInt(12);

                Stream.WriteVInt(0);
                Stream.WriteVInt(26);
                Stream.WriteVInt(18);

                Stream.WriteVInt(0);
                Stream.WriteVInt(26);
                Stream.WriteVInt(14);


                Stream.WriteVInt(0);
                Stream.WriteVInt(0);
                Stream.WriteVInt(0);
                Stream.WriteVInt(999999999);//Diamonds
                Stream.WriteVInt(999999999);//Free Diamonds

                Stream.WriteVInt(30);//XPlevel
                Stream.WriteVInt(1);//Level
                Stream.WriteVInt(0);//Unk

                Stream.WriteVInt(1);//ChangeNameCount
                Stream.WriteVInt(1);
                Stream.WriteVInt(0);
                Stream.WriteVInt(0);
                Stream.WriteVInt(1);
                Stream.WriteVInt(0);
                Stream.WriteVInt(1);
                Stream.WriteVInt(5);
                Stream.WriteVInt(0);
                Stream.WriteVInt(0);
                Stream.WriteVInt(0);
                Stream.WriteVInt(1);
                Stream.WriteVInt(0);
                Stream.WriteVInt((int)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                Stream.WriteVInt((int)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                Stream.WriteVInt(0);//Tick
            }
        }
    }
}

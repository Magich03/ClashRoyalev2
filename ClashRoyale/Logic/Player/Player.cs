namespace ClashRoyale.Logic.Player
{
    using System;

    using ClashRoyale.Enums;
    using ClashRoyale.Extensions;
    using ClashRoyale.Extensions.Game;
    using ClashRoyale.Extensions.Helper;
    using ClashRoyale.Files.Csv;
    using ClashRoyale.Files.Csv.Client;
    using ClashRoyale.Files.Csv.Logic;
    using ClashRoyale.Logic.Apis;
    using ClashRoyale.Logic.Home;
    using ClashRoyale.Logic.Mode;
    using ClashRoyale.Logic.Player.Enums;
    using ClashRoyale.Logic.Player.Slots;

    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    using Math = ClashRoyale.Maths.Math;

    [JsonObject(MemberSerialization.OptIn)]
    public class Player : PlayerBase
    {
        public GameMode GameMode;
        [JsonProperty("season")] public ClashRoyale.Logic.Seasons.LeagueSeason Season = new();

        // Id

        [JsonProperty("accountHigh")]           public int HighId;
        [JsonProperty("accountLow")]            public int LowId;
        
        [JsonProperty("accountToken")]          public string Token;
        [JsonProperty("accountLocation")]       public LocaleData AccountLocation;

        [JsonProperty("clanHighId")]            public int ClanHighId;
        [JsonProperty("clanLowId")]             public int ClanLowId;

        [JsonProperty("isDemoAccount")]         public bool IsDemoAccount;

        [JsonProperty("lastRewardedMatch")] public string LastRewardedMatch;

        // Game
        
        [JsonProperty("isNameSet")]             public bool IsNameSet;

        [JsonProperty("diamonds")]              public int Diamonds;
        [JsonProperty("freeDiamonds")]          public int FreeDiamonds;
        [JsonProperty("score")]                 public int Score;
        [JsonProperty("score2v2")]              public int Score2V2;
        [JsonProperty("npcWins")]               public int NpcWins;
        [JsonProperty("npcLosses")]             public int NpcLosses;
        [JsonProperty("pvpWins")]               public int PvPWins;
        [JsonProperty("pvpLosses")]             public int PvPLosses;
        [JsonProperty("battles")]               public int Battles;
        [JsonProperty("tbattles")]              public int TotalBattles;

        [JsonProperty("lastTourScore")]         public int LastTournamentScore;
        [JsonProperty("lastTourBestScore")]     public int LastTournamentBestScore;
        [JsonProperty("lastTourHighId")]        public int LastTournamentHighId;
        [JsonProperty("lastTourLowId")]         public int LastTournamentLowId;

        [JsonProperty("expLevel")]              public int ExpLevel;
        [JsonProperty("expPoints")]             public int ExpPoints;

        [JsonProperty("nameChangeState")]       public int NameChangeState;
        [JsonProperty("clanRole")]              public int AllianceRole;

        [JsonProperty("username")]              public string Name;
        [JsonProperty("clanName")]              public string AllianceName;

        [JsonProperty("arena")]                 public ArenaData Arena;
        [JsonProperty("lastTourArena")]         public ArenaData LastTournamentArena;
        [JsonProperty("clanBadge")]             public AllianceBadgeData Badge;

        [JsonProperty("commodities")]           public CommoditySlots CommoditySlots;

        // DateTime

        [JsonProperty("save")]                  public DateTime Update    = DateTime.UtcNow;
        [JsonProperty("creation")]              public DateTime Created   = DateTime.UtcNow;
        [JsonProperty("ban")]                   public DateTime Ban       = DateTime.UtcNow;

        // Home
        
        [JsonProperty("home")]                  public Home Home;

        // Debug
        
        [JsonProperty("debugRank")]             public Rank Rank = Rank.Player;

        // Apis
        
        [JsonProperty("api")]                   public ApiManager ApiManager;

        // Inbox

        [JsonProperty("mails")]                 public System.Collections.Generic.List<AllianceMailEntry> Mails = new System.Collections.Generic.List<AllianceMailEntry>();
        [JsonProperty("mailSeed")]              public int MailSeed;

        /// <summary>
        /// Adds a clan mail to the inbox, keeping the newest 50.
        /// </summary>
        public void AddMail(AllianceMailEntry Mail)
        {
            Mail.HighId = 0;
            Mail.LowId  = ++this.MailSeed;

            this.Mails.Add(Mail);

            if (this.Mails.Count > 50)
            {
                this.Mails.RemoveAt(0);
            }
        }

        /// <summary>
        /// Gets the player identifier.
        /// </summary>
        public long PlayerId
        {
            get
            {
                return (long) this.HighId << 32 | (uint) this.LowId;
            }
        }

        /// <summary>
        /// Gets the alliance identifier.
        /// </summary>
        public long AllianceId
        {
            get
            {
                return (long) this.ClanHighId << 32 | (uint) this.ClanLowId;
            }
        }
        
        /// <summary>
        /// Gets a value indicating whether the player is connected.
        /// </summary>
        public bool IsConnected
        {
            get
            {
                if (this.GameMode != null)
                {
                    return this.GameMode.IsConnected;
                }

                return false;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the player is in an alliance.
        /// </summary>
        public bool IsInAlliance
        {
            get
            {
                return this.AllianceId != 0;
            }
        }

        /// <summary>
        /// Gets a value indicating whether this player is banned.
        /// </summary>
        public bool IsBanned
        {
            get
            {
                return this.Ban > DateTime.UtcNow;
            }
        }

        /// <summary>
        /// Gets the time left in seconds before the ban ends.
        /// </summary>
        public int BanSecondsLeft
        {
            get
            {
                return (int) this.Ban.Subtract(DateTime.UtcNow).TotalSeconds;
            }
        }

        /// <summary>
        /// Gets this instance generated checksum.
        /// </summary>
        public override int Checksum
        {
            get
            {
                return 0;
            }
        }

        /// <summary>
        /// Gets the number of gold.
        /// </summary>
        public int Gold
        {
            get
            {
                return this.CommoditySlots.GetCommodityCount(CommodityType.Resource, CsvFiles.GoldData);
            }
        }

        /// <summary>
        /// Gets the number of max gold.
        /// </summary>
        public int MaxGold
        {
            get
            {
                return CsvFiles.GoldData.Cap;
            }
        }

        /// <summary>
        /// Gets the number of max score.
        /// </summary>
        public int MaxScore
        {
            get
            {
                return this.CommoditySlots.GetCommodityCount(CommodityType.Resource, CsvFiles.Get(Gamefile.Resources).GetData<ResourceData>("MaxScore"));
            }
        }

        /// <summary>
        /// Gets the count of star.
        /// </summary>
        public int StarCount
        {
            get
            {
                return this.CommoditySlots.GetCommodityCount(CommodityType.Resource, CsvFiles.StarCountData);
            }
        }

        /// <summary>
        /// Gets the experience level data.
        /// </summary>
        public CsvData ExpLevelData
        {
            get
            {
                return CsvFiles.Get(Gamefile.ExpLevels).GetWithInstanceId(this.ExpLevel - 1);
            }
        }
        /// <summary>
        /// Initializes a new instance of the <see cref="Player"/> class.
        /// </summary>
        public Player()
        {
            this.Home           = new Home();
            this.CommoditySlots = new CommoditySlots();
            this.ApiManager     = new ApiManager();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Player"/> class.
        /// </summary>
        /// <param name="HighId">The high identifier.</param>
        /// <param name="LowId">The low identifier.</param>
        public Player(int HighId, int LowId) : this()
        {
            this.HighId         = HighId;
            this.LowId          = LowId;

            this.Initialize();
        }

        /// <summary>
        /// Initializes values of members.
        /// </summary>
        public void Initialize()
        {
            this.CommoditySlots.Initialize();

            this.ExpLevel       = 1;
            this.NameChangeState= -1;
            this.Diamonds       = Globals.StartingDiamonds;
            this.FreeDiamonds   = Globals.StartingDiamonds;
            this.Arena          = CsvFiles.Get(Gamefile.Arenas).GetWithInstanceId<ArenaData>(1);
        }

        /// <summary>
        /// Adds free diamond.
        /// </summary>
        public void AddFreeDiamonds(int Count)
        {
            if (Count > 0)
            {
                this.Diamonds     += Count;
                this.FreeDiamonds += Count;
            }
        }

        /// <summary>
        /// Adds the specified free gold.
        /// </summary>
        /// <param name="Count">The count.</param>
        public void AddFreeGold(int Count)
        {
            this.AddResource(CsvFiles.GoldData, Count);
            this.AddResource(CsvFiles.FreeGoldData, Count);
        }

        /// <summary>
        /// Adds the specified resource.
        /// </summary>
        /// <param name="ResourceData">The resource data.</param>
        /// <param name="Count">The count.</param>
        public void AddResource(ResourceData ResourceData, int Count)
        {
            if (Count <= 0)
            {
                return;
            }

            if (ResourceData.HasCap())
            {
                int CurrentResource = this.CommoditySlots.GetCommodityCount(CommodityType.Resource, ResourceData);

                if (CurrentResource + Count > ResourceData.Cap)
                {
                    return;
                }
            }

            this.CommoditySlots.AddCommodityCount(CommodityType.Resource, ResourceData, Count);
        }
        
        /// <summary>
        /// Removes the specified resource.
        /// </summary>
        /// <param name="ResourceData">The resource data.</param>
        /// <param name="Count">The count.</param>
        public void RemoveResource(ResourceData ResourceData, int Count)
        {
            if (Count <= 0)
            {
                return;
            }

            this.CommoditySlots.UseCommodity(CommodityType.Resource, ResourceData, Count);
        }

        /// <summary>
        /// Gets the achievement progress by data.
        /// </summary>
        public int GetAchievementProgress(CsvData CsvData)
        {
            return this.CommoditySlots.GetCommodityCount(CommodityType.AchievementProgress, CsvData);
        }

        /// <summary>
        /// Gets if the specified achievement reward has been claimed.
        /// </summary>
        public bool GetIsAchievementRewardClaimed(AchievementData Data)
        {
            return this.CommoditySlots.Exists(3, Data);
        }

        /// <summary>
        /// Gets if the player has enough diamonds.
        /// </summary>
        public bool HasEnoughDiamonds(int Count)
        {
            return this.Diamonds >= Count;
        }

        /// <summary>
        /// Gets if the player has enough resources.
        /// </summary>
        public bool HasEnoughResources(ResourceData Data, int Count)
        {
            return this.CommoditySlots.GetCommodityCount(CommodityType.Resource, Data) >= Count;
        }

        /// <summary>
        /// Increases the number of three crown wins.
        /// </summary>
        public void IncreaseThreeCrownWins()
        {
            this.CommoditySlots.AddCommodityCount(CommodityType.Resource, CsvFiles.Get(Gamefile.Resources).GetData<ResourceData>("ThreeCrownWins"), 1);
        }

        /// <summary>
        /// Gets if the specified achievement is completed.
        /// </summary>
        public bool IsAchievementCompleted(AchievementData Data)
        {
            return Data.ActionCount <= this.GetAchievementProgress(Data);
        }

        /// <summary>
        /// Levels up this player.
        /// </summary>
        private void LevelUp(int ExpLevel)
        {
            ExpLevelData Data = CsvFiles.Get(Gamefile.ExpLevels).GetWithInstanceId<ExpLevelData>(ExpLevel - 1);

            this.AddFreeDiamonds(Data.DiamondReward);
        }

        /// <summary>
        /// Refreshes the arena.
        /// </summary>
        public void RefreshArena()
        {
            CsvTable ArenaTable = CsvFiles.Get(Gamefile.Arenas);

            if (ArenaTable.Datas.Count > 0)
            {
                ArenaData NextArena = null;

                for (int I = 0; I < ArenaTable.Datas.Count; I++)
                {
                    ArenaData Data = (ArenaData) ArenaTable.Datas[I];

                    if (this.Score < Data.TrophyLimit)
                    {
                        if (NextArena == null || NextArena.TrophyLimit < Data.TrophyLimit)
                        {
                            if (Data == this.Arena)
                            {
                                if (this.Score >= Data.DemoteTrophyLimit)
                                {
                                    NextArena = Data;
                                }
                            }
                        }
                    }
                    else
                    {
                        if (!Data.TrainingCamp)
                        {
                            if (NextArena != null)
                            {
                                if (NextArena.TrophyLimit < Data.TrophyLimit)
                                {
                                    NextArena = Data;
                                }
                            }
                            else
                            {
                                NextArena = Data;
                            }
                        }
                    }
                }

                if (NextArena != null)
                {
                    this.Arena = NextArena;
                }
            }
        }

        /// <summary>
        /// Called when the score of player has been changed.
        /// </summary>
        public void ScoreChanged(int Count)
        {
            (this.Season ??= new()).Refresh(this, DateTimeOffset.UtcNow);
            this.Score = Math.Max(this.Score + Count, 0);
            this.Season.HighestScore = System.Math.Max(this.Season.HighestScore, this.Score);

            if (this.Score > this.MaxScore)
            {
                this.SetMaxScore(this.Score);
            }
        }

        /// <summary>
        /// Sets the achievement progress.
        /// </summary>
        public void SetAchievementProgress(AchievementData Data, int Count)
        {
            this.CommoditySlots.SetCommodityCount(CommodityType.AchievementProgress, Data, Count);
        }

        /// <summary>
        /// Sets if the specified achievement reward is claimed.
        /// </summary>
        public void SetAchievementRewardClaimed(AchievementData Data, int Count)
        {
            this.CommoditySlots.SetCommodityCount(CommodityType.AchievementCompleted, Data, Count);
        }

        /// <summary>
        /// Sets the alliance id.
        /// </summary>
        public void SetAllianceId(int HighId, int LowId)
        {
            this.ClanHighId = HighId;
            this.ClanLowId  = LowId;
        }

        /// <summary>
        /// Sets the alliance name.
        /// </summary>
        public void SetAllianceName(string Name)
        {
            this.AllianceName = Name;
        }

        /// <summary>
        /// Sets the name of the player.
        /// </summary>
        public void SetName(string Name)
        {
            this.Name = Name;
        }

        /// <summary>
        /// Sets the role of the player in alliance.
        /// </summary>=
        public void SetAllianceRole(int Role)
        {
            this.AllianceRole = Role;
        }

        /// <summary>
        /// Sets the role of the player in alliance.
        /// </summary>=
        public void SetAllianceBadge(AllianceBadgeData Data)
        {
            this.Badge = Data;
        }

        /// <summary>
        /// Sets the number of star count.
        /// </summary>=
        public void SetStarCount(int Count)
        {
            if (Globals.StartingArena != this.Arena)
            {
                if (Count >= 4)
                {
                    Logging.Error(this.GetType(), "SetStarCount() - Player: set star count out of bounds: " + Count);
                    return;
                }

                this.CommoditySlots.SetCommodityCount(CommodityType.Resource, CsvFiles.StarCountData, Count);

                if (Count == 3)
                {
                    this.IncreaseThreeCrownWins();
                }
            }
        }

        /// <summary>
        /// Sets the number of gold.
        /// </summary>
        public void SetFreeGold(int Count)
        {
            this.CommoditySlots.SetCommodityCount(CommodityType.Resource, CsvFiles.GoldData, Count);
            this.CommoditySlots.SetCommodityCount(CommodityType.Resource, CsvFiles.FreeGoldData, Count);
        }

        /// <summary>
        /// Sets the number of diamonds.
        /// </summary>
        public void SetFreeDiamonds(int Count)
        {
            this.Diamonds = Count;
            this.FreeDiamonds = Count;
        }

        /// <summary>
        /// Sets the number of chest.
        /// </summary>
        public void SetCardsFound(int Count)
        {
            this.CommoditySlots.SetCommodityCount(CommodityType.ProfileResource, CsvFiles.CardCountData, Count);
        }

        /// <summary>
        /// Sets the number of chest.
        /// </summary>
        public void SetChestCount(int Count)
        {
            if (Count > Globals.MaxChest)
            {
                Logging.Error(this.GetType() , "SetChestCount() - Set chest count out of bounds: "+ Count);
                return;
            }

            this.CommoditySlots.SetCommodityCount(CommodityType.Resource, CsvFiles.ChestCountData, Count);
        }

        /// <summary>
        /// Sets the favourite spell.
        /// </summary>
        public void SetFavouriteSpell(SpellData Spell)
        {
            if (Spell != null)
            {
                CsvData CsvData = CsvFiles.Get(Gamefile.Resources).GetData("FavouriteSpell");
                this.CommoditySlots.SetCommodityCount(CommodityType.ProfileResource, CsvData, CsvData.GlobalId);
            }
        }

        /// <summary>
        /// Sets the max score.
        /// </summary>
        public void SetMaxScore(int Count)
        {
            CsvData CsvData = CsvFiles.Get(Gamefile.Resources).GetData("MaxScore");
            this.CommoditySlots.SetCommodityCount(CommodityType.Resource, CsvData, Math.Max(0, Count));
        }

        /// <summary>
        /// Sets the number of diamonds.
        /// </summary>
        public void SetNameChangeState(int State)
        {
            this.NameChangeState = State;
        }

        /// <summary>
        /// Sets if the name is set by user.
        /// </summary>
        public void SetNameSetByUser(bool Value)
        {
            this.IsNameSet = Value;
        }

        /// <summary>
        /// Uses the specified diamonds.
        /// </summary>
        public void UseDiamonds(int Count)
        {
            this.Diamonds       -= Count;
            this.FreeDiamonds   -= Count;
        }

        /// <summary>
        /// Uses the specified diamonds.
        /// </summary>
        public void UseGold(int Count)
        {
            this.CommoditySlots.UseCommodity(CommodityType.Resource, CsvFiles.GoldData, Count);
            this.CommoditySlots.UseCommodity(CommodityType.Resource, CsvFiles.FreeGoldData, Count);
        }

        /// <summary>
        /// Adds the number of specified exp points.
        /// </summary>
        public void XpGainHelper(int ExpPoints)
        {
            int MaxExpLevel = CsvFiles.MaxExpLevel;

            if (CsvFiles.MaxExpLevel > this.ExpLevel)
            {
                this.ExpPoints += ExpPoints;

                ExpLevelData Data;

                for (int I = this.ExpLevel; I < MaxExpLevel; I++)
                {
                    Data = CsvFiles.Get(Gamefile.ExpLevels).GetWithInstanceId<ExpLevelData>(this.ExpLevel - 1);

                    if (this.ExpPoints >= Data.ExpToNextLevel)
                    {
                        this.ExpPoints -= Data.ExpToNextLevel;
                        ++this.ExpLevel;

                        this.LevelUp(this.ExpLevel - 1);
                    }
                }

                Data = CsvFiles.Get(Gamefile.ExpLevels).GetWithInstanceId<ExpLevelData>(this.ExpLevel - 1);

                if (this.ExpPoints > Data.ExpToNextLevel)
                {
                    this.ExpPoints = Data.ExpToNextLevel;
                }
            }
        }

        /// <summary>
        /// Encodes in the specified stream.
        /// </summary>
        /// <param name="Stream">The stream.</param>
        /// <param name="BattleEncode">if set to <c>true</c> [battle encode].</param>
        public void Encode(ChecksumEncoder Stream, bool BattleEncode = false, GameModeData BattleMode = null)
        {
            Stream.WriteLogicLong(this.HighId, this.LowId); // Avatar
            Stream.WriteLogicLong(this.HighId, this.LowId); // Account
            Stream.WriteLogicLong(this.HighId, this.LowId); // Home

            Stream.WriteString(this.Name);

            if (!BattleEncode)
            {
                
                Stream.WriteVInt(this.NameChangeState);
            }

            Stream.EncodeLogicData(this.Arena, 54);
            Stream.WriteVInt(this.Score);
            Stream.WriteVInt(0);
            Stream.WriteVInt(this.MaxScore);

            if (!BattleEncode)
            {
                Stream.WriteVInt(0);
            }

            (this.Season ??= new()).EncodePrevious(Stream);
            Stream.WriteVInt(0); // Legacy field +0x34, separate from best-season record.
            this.Season.EncodeBest(Stream);

            // Send temporary event cosmetics in the battle snapshot without changing saved selections.
            System.Collections.Generic.Dictionary<CsvData, int> skinOverrides = null;
            if (BattleEncode && BattleMode != null)
            {
                skinOverrides = new System.Collections.Generic.Dictionary<CsvData, int>();
                foreach (var selection in new[] { ("CurrentKingSkin", BattleMode.KingSkin), ("CurrentPrincessSkin", BattleMode.PrincessSkin) })
                {
                    if (string.IsNullOrEmpty(selection.Item2)) continue;
                    var skin = CsvFiles.Get(Gamefile.Skins).GetData<SkinData>(selection.Item2)
                        ?? throw new InvalidOperationException("Missing event tower skin " + selection.Item2);
                    var resource = CsvFiles.Get(Gamefile.Resources).GetData<ResourceData>(selection.Item1)
                        ?? throw new InvalidOperationException("Missing skin selection resource " + selection.Item1);
                    skinOverrides[resource] = skin.GlobalId;
                }
            }
            this.CommoditySlots.Encode(Stream, skinOverrides);

            if (!BattleEncode)
            {
                Stream.WriteVInt(this.Diamonds);
                Stream.WriteVInt(this.FreeDiamonds);
                Stream.WriteVInt(this.ExpPoints);
                Stream.WriteVInt(this.ExpLevel);
                Stream.WriteVInt(0);

                

                Stream.WriteBoolean(this.IsNameSet);
                Stream.WriteBoolean(false); // ?
            }
            else
            {
                Stream.WriteVInt(this.ExpLevel);
            }

            Stream.WriteBoolean(false); // ?
            Stream.WriteBoolean(this.IsInAlliance);

            if (this.IsInAlliance)
            {
                Stream.WriteLogicLong(this.ClanHighId, this.ClanLowId);
                Stream.WriteString(this.AllianceName);
                Stream.EncodeLogicData(this.Badge, 16);

                if (!BattleEncode)
                {
                    Stream.WriteVInt(this.AllianceRole);
                }
            }

            Stream.WriteVInt(0);
            Stream.WriteVInt(0);
            Stream.WriteVInt(0);
            Stream.WriteVInt(0);
            Stream.WriteVInt(0);

            Stream.WriteVInt(0); // +0xdc
            Stream.WriteVInt(7); // training step +0xe0
            Stream.WriteVInt(1); // +0xe4
            Stream.WriteVInt(0); // array +0xf4
            var challenge = this.Home?.Challenge;
            bool active = challenge != null && !challenge.Finished && !challenge.RewardClaimed;
            Stream.WriteBoolean(active);
            if (active) challenge.Encode(Stream);
            Stream.WriteBoolean(false);
            Stream.WriteVInt(1);
            Stream.WriteBoolean(false);

        }

        /// <summary>
        /// Saves this instance to json.
        /// </summary>
        public JObject Save(bool BattleSave)
        {
            JObject Json = new JObject();

            if (BattleSave)
            {
                Json.Add("accountID.hi", this.HighId);
                Json.Add("accountID.lo", this.LowId);
                Json.Add("expLevel", this.ExpLevel);
                Json.Add("name", this.Name);
                Json.Add("clan_name", this.AllianceName);
                Json.Add("scr", this.Score);
                Json.Add("clan_id_hi", this.ClanHighId);
                Json.Add("clan_id_lo", this.ClanLowId);
                Json.Add("productRed", false);

                JsonHelper.SetLogicData(Json, "arena", this.Arena);
                JsonHelper.SetLogicData(Json, "badge", this.Badge);
                JsonHelper.SetLogicData(Json, "kingSkin", CsvFiles.Get(Gamefile.Skins).GetWithInstanceId<SkinData>(0));
            }

            return Json;
        }

        /// <summary>
        /// Returns a <see cref="string" /> that represents this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="string" /> that represents this instance.
        /// </returns>
        public override string ToString()
        {
            return this.HighId + "-" + this.LowId;
        }
    }
}

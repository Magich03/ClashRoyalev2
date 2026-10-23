namespace ClashRoyale.Logic.Battle
{
    using System.Linq;
    using ClashRoyale.Enums;
    using ClashRoyale.Extensions;
    using ClashRoyale.Extensions.Helper;
    using ClashRoyale.Extensions.Utils;
    using ClashRoyale.Files.Csv;
    using ClashRoyale.Files.Csv.Logic;
    using ClashRoyale.Logic.Player;

    /// <summary>
    /// Builds the battle state of a training battle (player vs NPC) for SectorStateMessage, in the layout client 3.830
    /// reads it (LogicGameMode::decodeOnce + LogicGameMode::decode with a LogicBattle, reverse-engineered from libg.so).
    /// The client creates the arena and its towers itself from the location, so a fresh battle sends no game objects.
    /// </summary>
    public static class BattleStateEncoder
    {
        private const int PlayerCount = 2;

        /// <summary>
        /// Client config switch (byte +0x36f of the live config) that enables per-player quests in battles. When set,
        /// LogicBattle::decodeOnce reads an optional quest block and LogicBattle::decode a quest list per player.
        /// </summary>
        private const bool BattleQuests = true;

        public static byte[] EncodeTrainingBattle(Player Player, NpcData Npc, int Seed)
        {
            LocationData Location   = CsvFiles.Get(Gamefile.Locations).GetData<LocationData>(Npc.Location);
            GameModeData GameMode   = CsvFiles.Get(Gamefile.GameModes).GetData<GameModeData>("Training");

            Player Opponent         = new Player(-1, Npc.Instance);
            Opponent.Initialize();
            Opponent.Name           = "Trainer";
            Opponent.ExpLevel       = Npc.ExpLevel > 0 ? Npc.ExpLevel : 1;
            Opponent.Arena          = Player.Arena;

            ChecksumEncoder Stream  = new ChecksumEncoder(new ByteStream());

            Stream.WriteBoolean(true); // has decodeOnce data
            BattleStateEncoder.EncodeOnce(Stream, Player, Opponent, Mode: GameMode);
            BattleStateEncoder.EncodeFullUpdate(Stream, Player, Npc, Location, GameMode, Seed);

            return Stream.ByteStream.ToArray(0, Stream.ByteStream.Offset);
        }

        // Matchmade battles share the tested wire layout, but have real avatar identities and no NPC mission.
        public static byte[] EncodeMatchBattle(Player first, Player second, int seed, GameModeData mode = null, ClashRoyale.Logic.Event.DraftSelection draft = null, int eventId = 0, ClashRoyale.Logic.Event.PickSelection pick = null)
        {
            var stream = new ChecksumEncoder(new ByteStream());
            stream.WriteBoolean(true);
            mode ??= CsvFiles.GameModeLadderData;
            EncodeOnce(stream, first, second, null, draft, eventId, mode);
            var arena = ArenaFor(first, mode, seed, eventId);
            EncodeFullUpdate(stream, first, null, arena.PvPLocationData, mode, seed, second, null, draft, pick, arena);
            return stream.ByteStream.ToArray(0, stream.ByteStream.Offset);
        }

        public static byte[] EncodeTeamBattle(Player[] players, int seed, GameModeData mode = null, ClashRoyale.Logic.Event.DraftSelection draft = null, int eventId = 0)
        {
            if (players == null || players.Length != 4 || System.Array.Exists(players, p => p == null))
                throw new System.ArgumentException("2v2 requires four players");
            var stream = new ChecksumEncoder(new ByteStream());
            stream.WriteBoolean(true);
            mode ??= CsvFiles.Get(Gamefile.GameModes).GetData<GameModeData>("TeamVsTeamLadder");
            EncodeOnce(stream, players[0], players[1], players, draft, eventId, mode);
            var arena = ArenaFor(players[0], mode, seed, eventId, true);
            EncodeFullUpdate(stream, players[0], null, arena.TeamVsTeamLocationData,
                mode, seed, players[1], players, draft, null, arena);
            return stream.ByteStream.ToArray(0, stream.ByteStream.Offset);
        }

        // This selects a battle map only; account arena and reward eligibility stay unchanged.
        public static ArenaData ArenaFor(Player player, GameModeData mode, int seed = 0, int eventId = 0, bool team = false)
        {
            if (mode.TargetTouchdowns > 0)
                return CsvFiles.Get(Gamefile.Arenas).GetData<ArenaData>("Arena_TouchdownTest");
            if (!team && mode.Players != "TvT" && eventId <= 0) return player.Arena;
            var arenas = CsvFiles.Get(Gamefile.Arenas).Datas.OfType<ArenaData>()
                .Where(a => a.IsInUse && !a.TrainingCamp && a.PvPLocationData != null && a.TeamVsTeamLocationData != null
                    && a.PvPLocationData.TileDataFileName == "tilemaps/tilemap.csv"
                    && a.TeamVsTeamLocationData.TileDataFileName == "tilemaps/tilemap_2v2.csv")
                .GroupBy(a => a.PvPLocationData.Name).Select(g => g.First()).OrderBy(a => a.GlobalId).ToArray();
            return arenas.Length == 0 ? player.Arena : arenas[new System.Random(seed).Next(arenas.Length)];
        }

        /// <summary>
        /// LogicGameMode::decodeOnce (0x249ec4).
        /// </summary>
        private static void EncodeOnce(ChecksumEncoder Stream, Player Player, Player Opponent, Player[] Team = null, ClashRoyale.Logic.Event.DraftSelection Draft = null, int eventId = 0, GameModeData Mode = null)
        {
            Stream.WriteVInt(42);

            // LogicBattle::decodeOnce (0x31e7f4): player count, then an optional event/game-mode block.
            Stream.WriteVInt(Team?.Length ?? BattleStateEncoder.PlayerCount);
            ClashRoyale.Logic.Event.LiveEventCatalog.EncodeBattleEvent(Stream, eventId);

            if (BattleStateEncoder.BattleQuests)
            {
                Stream.WriteBoolean(false); // no quest block (player index + LogicQuestManager)
            }

            // Up to 4 avatars, decoded with the battle flag (LogicClientAvatar::decode(stream, true)).
            Player[] Avatars = Team ?? new[] { Player, Opponent, null, null };

            foreach (Player Avatar in Avatars)
            {
                Stream.WriteBoolean(Avatar != null);
                Avatar?.Encode(Stream, true, Mode);
            }

            Stream.WriteVInt(43);
        }

        /// <summary>
        /// LogicGameMode::decode (0x1b316c).
        /// </summary>
        private static void EncodeFullUpdate(ChecksumEncoder Stream, Player Player, NpcData Npc, LocationData Location, GameModeData GameMode, int Seed, Player Opponent = null, Player[] Team = null, ClashRoyale.Logic.Event.DraftSelection Draft = null, ClashRoyale.Logic.Event.PickSelection Pick = null, ArenaData Arena = null)
        {
            Stream.WriteVInt(0);                    // tick
            Stream.WriteVInt(0);                    // checksum
            Stream.WriteVInt(TimeUtil.Timestamp);
            Stream.WriteVInt(11);

            Stream.WriteVInt(0);                    // LogicTime
            Stream.WriteInt(Seed);                  // LogicRandom
            Stream.WriteVInt(Seed);

            BattleStateEncoder.EncodeBattleCore(Stream, Player, Npc, Location, GameMode, Opponent, Team, Draft, Pick, Arena);

            Stream.WriteVInt(12);

            // Tutorial manager (0x300338): tutorial data, step, and a game object reference when in battle.
            Stream.WriteVInt(0);
            Stream.WriteVInt(0);
            Stream.WriteVInt(0);

            Stream.WriteVInt(0);                    // checksum

            Stream.WriteVInt(0);                    // command manager: no queued commands
        }

        /// <summary>
        /// LogicBattle::decode (0x3202f8).
        /// </summary>
        private static void EncodeBattle(ChecksumEncoder Stream, Player Player, NpcData Npc, LocationData Location, GameModeData GameMode)
        {
            EncodeBattleCore(Stream, Player, Npc, Location, GameMode, null);
        }

        private static void EncodeBattleCore(ChecksumEncoder Stream, Player Player, NpcData Npc, LocationData Location, GameModeData GameMode, Player Opponent, Player[] Team = null, ClashRoyale.Logic.Event.DraftSelection Draft = null, ClashRoyale.Logic.Event.PickSelection Pick = null, ArenaData Arena = null)
        {
            Stream.EncodeLogicData(Location, (int) Gamefile.Locations);
            Stream.EncodeLogicData(Npc, (int) Gamefile.Npcs);
            Stream.EncodeLogicData(Arena ?? ArenaFor(Player, GameMode), (int) Gamefile.Arenas);

            if (Team != null)
                foreach (var participant in Team) { Stream.WriteLogicLong(participant.HighId, participant.LowId); Stream.WriteVInt(0); }
            else
            {
                Stream.WriteLogicLong(Player.HighId, Player.LowId); Stream.WriteVInt(0);
                Stream.WriteLogicLong(Opponent?.HighId ?? -1, Opponent?.LowId ?? Npc.Instance); Stream.WriteVInt(0);
            }

            BattleStateEncoder.WriteVIntArray(Stream, 8);

            // Battle settings (0x15fdce): 4 VInts, game mode, PvE mode, TvE mode.
            Stream.WriteVInt(0);
            Stream.WriteVInt(0);
            Stream.WriteVInt(0);
            Stream.WriteVInt(0);
            Stream.EncodeLogicData(GameMode, (int) Gamefile.GameModes);
            Stream.WriteVInt(0);
            Stream.WriteVInt(0);

            Stream.WriteBoolean(false);
            Stream.WriteBoolean(false);
            Stream.WriteBoolean(false);
            Stream.WriteBoolean(false);
            Stream.WriteBoolean(false);
            Stream.WriteBoolean(false);             // when true the battle state is forced to 4
            // Client mission predicate 0x21389c requires state 1 and a non-null NPC.
            Stream.WriteVInt(Opponent == null ? 1 : 0); // mission or ordinary match

            Stream.WriteVInt(0);
            Stream.WriteVInt(0);

            int[] levels = Team == null ? (Opponent == null ? null : new[] { Player.ExpLevel - 1, Opponent.ExpLevel - 1 }) :
                new[] { (Team[0].ExpLevel + Team[2].ExpLevel) / 2 - 1, (Team[1].ExpLevel + Team[3].ExpLevel) / 2 - 1,
                        (Team[0].ExpLevel + Team[2].ExpLevel) / 2 - 1, (Team[1].ExpLevel + Team[3].ExpLevel) / 2 - 1 };
            bool touchdown = GameMode.TargetTouchdowns > 0;
            int[][] layout = touchdown ? (Team == null ? TouchdownSummoners : TeamTouchdownSummoners) : (Team == null ? null : TeamTowers);
            // Tower visuals are selected by the battle avatars; character skins replace troop animations.
            EncodeTowers(Stream, levels, layout, GameMode.UseStartingElixir ? System.Math.Clamp(GameMode.StartingElixir, 0, 10) : 5, touchdown, GameMode.DeckSelection == "Draft");
            if (Team == null)
            {
                EncodeCardCycle(Stream, GetPlayerDeck(Player));
                EncodeCardCycle(Stream, Opponent == null ? GetNpcDeck(Npc) : GetPlayerDeck(Opponent));
            }
            else
            {
                // Native 0x320438: one presence flag per side, then both teammates' decks.
                EncodeCardCycle(Stream, GetPlayerDeck(Team[0]));
                EncodeCardCycle(Stream, GetPlayerDeck(Team[2]), false);
                EncodeCardCycle(Stream, GetPlayerDeck(Team[1]));
                EncodeCardCycle(Stream, GetPlayerDeck(Team[3]), false);
            }
            if (touchdown)
            {
                for (int i=0;i<(Team?.Length ?? 2);i++) Stream.WriteVInt(i+1);
                Stream.WriteVInt(0); Stream.WriteVInt(0); // No princess towers on the field.
            }
            else
            {
            Stream.WriteVInt(PlayerKingTower + 1);
            Stream.WriteVInt(OpponentKingTower + 1);
            if (Team != null) { Stream.WriteVInt(7); Stream.WriteVInt(8); }

            // Per side: its princess towers (the crown counter counts the towers missing from these lists).
            Stream.WriteVInt(2);
            Stream.WriteVInt(1 + 1);                // player: left (3500, 6500)
            Stream.WriteVInt(3 + 1);                // player: right (14500, 6500)
            Stream.WriteVInt(2);
            Stream.WriteVInt(2 + 1);                // trainer: left (3500, 25500)
            Stream.WriteVInt(0 + 1);                // trainer: right (14500, 25500)

            }

            BattleStateEncoder.WriteVIntArray(Stream, 6);

            Stream.WriteBoolean(false);             // optional LogicLong

            if (GameMode.DeckSelection == "Draft")
            {
                if (Draft == null || Draft.PlayerCount != (Team?.Length ?? 2)) throw new System.ArgumentException("Draft selections missing");
                for (int i=0;i<Draft.PlayerCount;i++) Draft.Encode(Stream,i);
                Stream.WriteVInt(0); // accumulated draft ticks, native battle +0x94
            }

            if (GameMode.DeckSelection == "Pick")
            {
                if (Pick == null || Team != null) throw new System.ArgumentException("Pick mode requires two selection states");
                Pick.Encode(Stream,0); Pick.Encode(Stream,1); Stream.WriteVInt(0);
            }
            if (touchdown)
            {
                // Native 0x32069e: round interval tick, scoring side, paused clock, round flag.
                Stream.WriteVInt(0); Stream.WriteVInt(-1); Stream.WriteVInt(0); Stream.WriteBoolean(true);
            }

            Stream.WriteVInt(0);

            if (BattleStateEncoder.BattleQuests)
            {
                // Per player: quest progress list (0x15f4f6), empty.
                for (int I = 0; I < (Team?.Length ?? BattleStateEncoder.PlayerCount); I++)
                {
                    Stream.WriteVInt(0);
                }
            }
        }

        // Towers in the order of an official capture: 4 princess towers, then the 2 king towers (summoners).
        // Player 0 is at the bottom of the arena (small y).
        private static readonly int[][] Towers =
        {
            // building instance (0 = KingTower, 1 = PrincessTower), owner, x, y
            new[] { 1, 1, 14500, 25500 },
            new[] { 1, 0, 3500, 6500 },
            new[] { 1, 1, 3500, 25500 },
            new[] { 1, 0, 14500, 6500 },
            new[] { 0, 0, 9000, 3000 },
            new[] { 0, 1, 9000, 29000 },
        };

        private static readonly int[][] TeamTowers = {
            new[] {1, 1, 14500, 25500}, new[] {1, 2, 3500, 6500},
            new[] {1, 3, 3500, 25500}, new[] {1, 0, 14500, 6500},
            new[] {0, 0, 11000, 3000}, new[] {0, 1, 11000, 29000},
            new[] {0, 2, 7000, 3000}, new[] {0, 3, 7000, 29000},
            // Shared 2v2 king visuals: native 0x2ded30 requires one per side.
            new[] {16, 0, 9000, 3000}, new[] {16, 1, 9000, 29000}
        };

        private static readonly int[][] TouchdownSummoners = {
            new[] {20,0,9000,3000}, new[] {20,1,9000,29000}
        };
        private static readonly int[][] TeamTouchdownSummoners = {
            new[] {20,0,11000,3000}, new[] {20,1,11000,29000},
            new[] {20,2,7000,3000}, new[] {20,3,7000,29000}
        };

        private const int PlayerKingTower   = 4;
        private const int OpponentKingTower = 5;

        /// <summary>
        /// Game object manager (0x25a48c) with the 6 towers. The client keeps its own map objects (type 1).
        /// </summary>
        private static void EncodeTowers(ChecksumEncoder Stream, int[] Levels = null, int[][] Layout = null, int StartingElixir = 5, bool Touchdown = false, bool Draft = false, GameModeData Mode = null)
        {
            var Towers = Layout ?? BattleStateEncoder.Towers;
            // Id counters per object type (delta-encoded): 142 map objects (type 1), 6 characters (type 5).
            int[] Counters = { 0, 142, 0, 0, 0, Towers.Length, Touchdown ? 1 : 0 };
            int Previous = 0;

            foreach (int Counter in Counters)
            {
                Stream.WriteVInt(Counter - Previous);
                Previous = Counter;
            }

            Stream.WriteVInt(Towers.Length + (Touchdown ? 1 : 0));

            foreach (int[] Tower in Towers)
            {
                Stream.EncodeData(CsvFiles.Get(Gamefile.Buildings).GetWithInstanceId<CharacterData>(Tower[0]));
            }

            if (Touchdown) Stream.EncodeData(CsvFiles.Get(Gamefile.Decos).GetData<DecoData>("GoblinRef"));

            foreach (int[] Tower in Towers)
            {
                Stream.WriteVInt(Tower[1]);
            }
            if (Touchdown) Stream.WriteVInt(0); // Native unowned deco defaults to side 0.

            for (int I = 0; I < Towers.Length; I++)
            {
                Stream.WriteVInt(5);                // object global id class (characters)
                Stream.WriteVInt(I);
            }

            if (Touchdown) { Stream.WriteVInt(6); Stream.WriteVInt(0); }

            foreach (int[] Tower in Towers)
            {
                BattleStateEncoder.EncodeTowerCharacter(Stream, Tower[2], Tower[3], Levels == null ? 0 : Levels[Tower[1]], Tower[0] == 20 ? 8 : Tower[0] == 16 ? 9 : Tower[0] == 0 ? 12 : 13, Touchdown);

                if (Tower[0] == 0 || Tower[0] == 20)
                {
                    BattleStateEncoder.EncodeSummonerState(Stream, Levels == null && Tower[1] == 1, StartingElixir);
                }
            }

            if (Touchdown) EncodeReferee(Stream, Towers.Length == 4 && !Draft);

            // Components are decoded in passes over all objects: combat, hitpoint, buff (towers have no movement).
            foreach (int[] Tower in Towers)
            {
                // Native 0x290c8a allocates combat for every character, including the shared center.
                // LogicCombatComponent (0x291880); every data-dependent field is off for towers.
                Stream.WriteBoolean(false);
                Stream.WriteBoolean(false);
                Stream.WriteVInt(0);
                Stream.WriteVInt(0);                // position
                Stream.WriteVInt(0);
                Stream.WriteVInt(0);
                Stream.WriteVInt(0);
                Stream.WriteVInt(0);
                Stream.WriteVInt(0);                // target: null game object reference
            }

            foreach (int[] Tower in Towers)
            {
                if (Tower[0] == 16 || Tower[0] == 20) continue; // 0x290c68 creates HP only when base hitpoints > 0.
                // LogicHitpointComponent (0x2f84e8): hitpoints, then one value for stationary objects (Speed 0).
                Stream.WriteVInt(GetTowerHitpoints(Tower[0], Levels == null ? 0 : Levels[Tower[1]], Layout != null));
                Stream.WriteVInt(0);
            }

            foreach (int[] Tower in Towers)
            {
                // LogicCharacterBuffComponent (0x2dc40c): no buffs, 4 values.
                Stream.WriteVInt(0);
                Stream.WriteVInt(0);
                Stream.WriteVInt(0);
                Stream.WriteVInt(0);
                Stream.WriteVInt(0);
            }
        }

        private static void EncodeReferee(ChecksumEncoder stream, bool teamCollection)
        {
            // Native GoblinRef (type 6), decode 0x1bdd88: base object, appeared, appear/run delays.
            stream.WriteVInt(0); stream.WriteVInt(9000); stream.WriteVInt(16000);
            stream.WriteVInt(0); stream.WriteVInt(0); stream.WriteVInt(-1);
            stream.WriteBoolean(false);
            var globals=CsvFiles.Get(Gamefile.Globals);
            stream.WriteVInt(globals.GetData<GlobalData>(teamCollection ? "TOUCHDOWN_REF_APPEAR_DELAY_INITIAL_2V2" : "TOUCHDOWN_REF_APPEAR_DELAY_INITIAL").NumberValue);
            stream.WriteVInt(globals.GetData<GlobalData>("TOUCHDOWN_REF_RUN_DELAY_INITIAL").NumberValue);
        }

        // Native 0x250eb4 scales an integer percentage per level, truncating after each step.
        public static int GetTowerHitpoints(int building, int level, bool team = false)
        {
            var globals = CsvFiles.Get(Gamefile.Globals);
            string key = "HITPOINT_INCREASE_PERCENT_PER_" + (building == 0 ? "KING" : "TOWER") + "_LEVEL";
            int normal = globals.GetData<GlobalData>(key).NumberValue;
            int atCap = globals.GetData<GlobalData>(key + "_AT_TOURNAMENTCAP").NumberValue;
            int afterCap = globals.GetData<GlobalData>(key + "_AFTER_TOURNAMENTCAP").NumberValue;
            int cap = globals.GetData<GlobalData>("TOURNAMENT_MAX_EXP_LEVEL").NumberValue;
            int percentage = 100;
            for (int i = 0; i < System.Math.Clamp(level, 0, 12); i++)
                percentage = percentage * (100 + (i >= cap ? afterCap : i == cap - 1 ? atCap : normal)) / 100;
            int hitpoints = CsvFiles.Get(Gamefile.Buildings).GetWithInstanceId<CharacterData>(building).Hitpoints * percentage / 100;
            // Native 0x2c6634..0x2c6692: apply the TvT bonus after level scaling, rounding up.
            if (team && (building == 0 || building == 1))
            {
                string teamHealthKey = building == 0 ? "TEAM_VS_TEAM_SUMMONER_HP_INCREASE_PERCENTAGE" : "TEAM_VS_TEAM_PRINCESS_TOWER_HP_INCREASE_PERCENTAGE";
                hitpoints = (hitpoints * (100 + globals.GetData<GlobalData>(teamHealthKey).NumberValue) + 99) / 100;
            }
            return hitpoints;
        }

        /// <summary>
        /// LogicCharacter::decode (0x1ef900) for a tower: every data-dependent field is off for towers.
        /// </summary>
        private static void EncodeTowerCharacter(ChecksumEncoder Stream, int X, int Y, int Level = 0, int Components = 13, bool Touchdown = false)
        {
            Stream.WriteVInt(Level);                // tower level index

            // LogicGameObject (0x707b4). The first value is the component mask the client looks components up with:
            // 13 = combat (1) + hitpoint (4) + buff (8), as in an official capture (towers have no movement, 2).
            Stream.WriteVInt(Components);
            Stream.WriteVInt(X);
            Stream.WriteVInt(Y);
            Stream.WriteVInt(0);
            Stream.WriteVInt(0);                    // global id list
            Stream.WriteVInt(0);

            Stream.WriteVInt(0);                    // second position
            Stream.WriteVInt(0);
            Stream.WriteVInt(0);
            Stream.WriteVInt(0);

            Stream.WriteVInt(0);
            Stream.WriteVInt(0);

            Stream.WriteVInt(0);
            Stream.WriteVInt(0);

            Stream.WriteBoolean(false);             // no object reference
            Stream.WriteVInt(0);

            if (Touchdown) Stream.WriteBoolean(false); // native character touchdown flag
            Stream.WriteBoolean(false); // No troop skin; tower skins belong to the avatar resources.
        }

        /// <summary>
        /// LogicSummoner::decode (0x2f280c) after the character: deck/hand state, elixir, AI blackboard.
        /// </summary>
        private static void EncodeSummoner(ChecksumEncoder Stream, bool ArtificialIntelligence) => EncodeSummonerState(Stream, ArtificialIntelligence, 5);

        private static void EncodeSummonerState(ChecksumEncoder Stream, bool ArtificialIntelligence, int StartingElixir)
        {
            Stream.WriteBoolean(true);              // has deck
            Stream.WriteBoolean(false);
            Stream.WriteBoolean(ArtificialIntelligence); // +0x15c: the client runs the AI for this summoner (the NPC)
            // +0x174: LogicSummoner target eligibility (vtable +0x54, native 0x8bb0e).
            // False prevents troops from acquiring the king after a princess tower falls.
            Stream.WriteBoolean(true);

            // Hand: deck indices 0..3 (delta-encoded), queue: the other 4 cards in order (as in an official capture).
            Stream.WriteVInt(4);
            Stream.WriteVInt(0);
            Stream.WriteVInt(1);
            Stream.WriteVInt(1);
            Stream.WriteVInt(1);

            Stream.WriteVInt(4);
            Stream.WriteVInt(4);
            Stream.WriteVInt(5);
            Stream.WriteVInt(6);
            Stream.WriteVInt(7);

            Stream.WriteVInt(0);                    // second queue: empty

            Stream.WriteVInt(-1);
            Stream.WriteVInt(-1);
            Stream.WriteVInt(0);
            Stream.WriteVInt(0);
            Stream.WriteVInt(-1);                   // +0x154: hand slot in use (skipped by the hand), none

            // Elixir / regeneration state.
            Stream.WriteVInt(0);
            Stream.WriteVInt(0);
            Stream.WriteVInt(StartingElixir);
            Stream.WriteVInt(0);
            Stream.WriteVInt(0);
            Stream.WriteVInt(0);

            Stream.WriteVInt(0);                    // AI blackboard
            Stream.WriteVInt(0);

            for (int I = 0; I < 8; I++)
            {
                Stream.WriteVInt(-1);
            }

            Stream.WriteBoolean(false);
            Stream.WriteBoolean(false);             // no optional object

            Stream.WriteVInt(0);
            Stream.WriteVInt(0);
            Stream.WriteVInt(0);
        }

        /// <summary>
        /// A side's deck (0x3365ba): 8 slot flags, a card per used slot (0x15d4b8: data, level), then one value.
        /// </summary>
        private static void EncodeCardCycle(ChecksumEncoder Stream, (SpellData Data, int Level)[] Deck, bool Presence = true)
        {
            if (Presence) Stream.WriteBoolean(true);

            for (int I = 0; I < 8; I++)
            {
                Stream.WriteBoolean(I < Deck.Length);
            }

            for (int I = 0; I < 8 && I < Deck.Length; I++)
            {
                Stream.EncodeLogicData(Deck[I].Data, 26);
                Stream.WriteVInt(Deck[I].Level);
            }

            Stream.WriteVInt(0);
        }

        private static (SpellData Data, int Level)[] GetPlayerDeck(Player Player)
        {
            var Deck = new System.Collections.Generic.List<(SpellData, int)>();

            for (int I = 0; I < 8; I++)
            {
                var Spell = Player.Home.SpellDeck[I];

                if (Spell?.Data != null)
                {
                    Deck.Add((Spell.Data, Spell.Level));
                }
            }

            return Deck.ToArray();
        }

        private static (SpellData Data, int Level)[] GetNpcDeck(NpcData Npc)
        {
            var Deck = new System.Collections.Generic.List<(SpellData, int)>();
            PredefinedDeckData Predefined = CsvFiles.Get(Gamefile.PredefinedDecks).GetData<PredefinedDeckData>(Npc.PredefinedDeck);

            if (Predefined?.Spells != null)
            {
                for (int I = 0; I < Predefined.Spells.Length && Deck.Count < 8; I++)
                {
                    SpellData Data = CsvFiles.GetSpellDataByName(Predefined.Spells[I]);

                    if (Data != null)
                    {
                        int Level = Predefined.SpellLevel != null && I < Predefined.SpellLevel.Length ? Predefined.SpellLevel[I] : 1;
                        Deck.Add((Data, System.Math.Max(0, Level - 1)));
                    }
                }
            }

            return Deck.ToArray();
        }

        private static void WriteVIntArray(ChecksumEncoder Stream, int Count)
        {
            // Delta-encoded VInt array (0x20e6a2).
            for (int I = 0; I < Count; I++)
            {
                Stream.WriteVInt(0);
            }
        }
    }
}

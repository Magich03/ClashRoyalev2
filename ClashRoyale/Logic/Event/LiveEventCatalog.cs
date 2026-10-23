namespace ClashRoyale.Logic.Event
{
    using System;
    using System.Linq;
    using ClashRoyale.Enums;
    using ClashRoyale.Extensions;
    using ClashRoyale.Files.Csv;
    using ClashRoyale.Files.Csv.Logic;
    using Newtonsoft.Json;

    // Native LogicChronosEventManager catalog. No APK CSV changes are required.
    public static class LiveEventCatalog
    {
        public sealed class Definition
        {
            public readonly int Id;
            public readonly string Mode, Title, FeaturedCard;
            public int GlobalId => 73000000 + Id;
            public Definition(int id, string mode, string title, string featuredCard=null) { Id=id; Mode=mode; Title=title; FeaturedCard=featuredCard; }
            public SurvivalModeData CreateData()
            {
                var bronze = CsvFiles.Get(Gamefile.SurvivalModes).GetData<SurvivalModeData>("Bronze");
                return new SurvivalModeData(bronze.CsvRow, bronze.CsvTable) {
                    GameMode=Mode, Enabled=true, EventOnly=false, FreePass=0,
                    JoinCost=0, JoinCostResource="Diamonds", MaxWins=1, MaxLoss=1
                };
            }
        }
        public static readonly Definition[] Events = {
            new Definition(2001, "DoubleElixir", "Double Elixir"),
            new Definition(2002, "BlindDeck", "Random Deck"),
            new Definition(2003, "DraftMode", "Draft"),
            new Definition(2004, "DraftModeInsane", "Insane Draft"),
            new Definition(2005, "TeamVsTeam", "2v2"),
            new Definition(2006, "RetroRoyale", "Retro Royale"),
            new Definition(2007, "TripleElixir_Tournament", "Triple Elixir"),
            new Definition(2008, "RampUpElixir_Tournament", "Ramp Up Elixir"),
            new Definition(2009, "Overtime_Tournament", "Sudden Death"),
            new Definition(2010, "TeamVsTeamDraft", "2v2 Draft"),
            new Definition(2011, "TeamVsTeam_DoubleElixir_Tournament", "2v2 Double Elixir"),
            new Definition(2012, "TeamVsTeam_Overtime_Tournament", "2v2 Sudden Death"),
            new Definition(2013, "TeamVsTeam_RampUpElixir_Tournament", "2v2 Ramp Up Elixir"),
            new Definition(2014, "MirrorDeck", "Mirror"),
            new Definition(2015, "TeamVsTeam_MirrorDeck", "2v2 Mirror"),
            new Definition(2016, "ModernRoyale", "Modern Royale"),
            new Definition(2017, "Touchdown_Draft", "Touchdown Draft"),
            new Definition(2018, "TeamVsTeam_Touchdown_Draft", "2v2 Touchdown Draft"),
            new Definition(2019, "Touchdown", "Touchdown"),
            new Definition(2020, "TeamVsTeam_Touchdown", "2v2 Touchdown"),
            new Definition(2021, "GoldRush_Tournament", "Gold Rush"),
            new Definition(2022, "TeamVsTeamLadder_GoldRush", "2v2 Gold Rush"),
            new Definition(2023, "GemRush_Tournament", "Gem Rush"),
            new Definition(2024, "TeamVsTeamLadder_GemRush", "2v2 Gem Rush"),
            new Definition(2025, "ElectroWiz", "Electro Wizard", "ElectroWizard"),
            new Definition(2026, "BattleRam", "Battle Ram", "BattleRam"),
            new Definition(2027, "TeamVsTeam_DraftModeInsane_Friendly", "2v2 Insane Draft"),
            new Definition(2028, "Friendly_FixedDeckOrder", "Fixed Deck Order"),
            new Definition(2029, "TeamVsTeam_FixedDeckOrder", "2v2 Fixed Deck Order"),
            new Definition(2030, "MirrorDeck_H", "Mirror (alternate decks)"),
            new Definition(2031, "TeamVsTeam_MirrorDeck_H", "2v2 Mirror (alternate decks)"),
            new Definition(2032, "CardRelease", "Royal Ghost Release", "Ghost"),
            new Definition(2033, "CardReleaseDraft", "Royal Ghost Draft", "Ghost"),
            new Definition(2034, "CardRelease", "Mega Knight Release", "MegaKnight"),
            new Definition(2035, "CardReleaseDraft", "Mega Knight Draft", "MegaKnight"),
            new Definition(2036, "CardRelease", "Electro Wizard Release", "ElectroWizard"),
            new Definition(2037, "CardReleaseDraft", "Electro Wizard Draft", "ElectroWizard"),
            new Definition(2038, "CardRelease", "Battle Ram Release", "BattleRam"),
            new Definition(2039, "CardReleaseDraft", "Battle Ram Draft", "BattleRam"),
        };
        public static string IconFor(string mode)
        {
            if (mode.Contains("Rush") || mode == "CardRelease" || mode == "BattleRam" || mode == "ElectroWiz") return "icon_tournament_special_grand";
            if (mode.Contains("Touchdown")) return "icon_mode_touchdown";
            if (mode.Contains("Overtime")) return "icon_tournament_suddendeath_grand";
            if (mode.Contains("RampUp")) return "icon_tournament_ramp_up_grand";
            if (mode.Contains("TripleElixir")) return "icon_tournament_3x_elixir_grand";
            if (mode.Contains("DoubleElixir")) return "icon_tournament_2x_elixir_grand";
            if (mode.Contains("Draft")) return "icon_tournament_draft_grand";
            if (mode.Contains("Mirror")) return "icon_tournament_mirror_grand";
            if (mode == "BlindDeck") return "icon_tournament_blind_deck_grand";
            if (mode == "RetroRoyale") return "icon_tournament_retro_grand";
            if (mode == "ModernRoyale") return "icon_tournament_special_grand";
            return "icon_tournament_2v2_grand";
        }
        public static object BackgroundFor(string mode, bool completed)
        {
            // Original 2.1.x challenge banners. Other modes keep the native background
            // until their matching artwork is recovered; friendly banners have different dimensions.
            if (mode.Contains("Overtime")) return new {
                Path=completed ? "/441bb812-1f3c-48cd-aad7-859a506ef80d_suddendeath_challenge_01.png" : "/fac10e48-ec49-432c-88c9-7e924ec96457_suddendeath_challenge_01.png",
                Checksum="ef3f243f10f600394351db917b554317", File="suddendeath_challenge_01.png"
            };
            if (mode.Contains("Draft") && !mode.Contains("Touchdown")) return new {
                Path=completed ? "/ed12d4b0-be3d-4f62-adb7-36d8deb93687_zappies_hunter_challenge_01.png" : "/fe2184d9-e9b1-47e9-809d-e9ffec59ec0a_zappies_hunter_challenge_01.png",
                Checksum="871e6e6ca79c73423409b415cee1332b", File="zappies_hunter_challenge_01.png"
            };
            return null;
        }
        public static SurvivalModeData Resolve(int id)
        {
            if (id / 1000000 == 73) return Events.FirstOrDefault(e => e.GlobalId == id)?.CreateData();
            if (id / 1000000 != 65) return null;
            return CsvFiles.Get(Gamefile.SurvivalModes).GetWithGlobalId<SurvivalModeData>(id);
        }
        private static void EncodeEvent(ChecksumEncoder s, int id, string title, int category, string json)
        {
            s.WriteVInt(id); s.WriteString(title); s.WriteVInt(category);
            s.WriteVInt(1767225600); // 2026-01-01: stable start so a relog does not reset participation.
            s.WriteVInt(1893456000); // 2030-01-01.
            s.WriteVInt(1767225600);
            s.WriteString(""); s.WriteString(""); s.WriteString(title); s.WriteString(json); s.WriteVInt(0);
        }
        public static void EncodeBattleEvent(ChecksumEncoder stream, int eventId)
        {
            var definition=Events.FirstOrDefault(e=>e.GlobalId==eventId);
            stream.WriteBoolean(definition != null);
            if(definition != null) EncodeDefinition(stream,definition);
        }
        private static void EncodeDefinition(ChecksumEncoder s, Definition e)
        {
                var d=e.CreateData();
                var rewards=Enumerable.Range(0,d.MaxWins+1).Select(i => new {Gold=d.RewardGold[i], Cards=d.RewardCards[i]}).ToArray();
                EncodeEvent(s,e.Id,e.Title,2,JsonConvert.SerializeObject(new {
                    Title=e.Title, GameMode=e.Mode, CardTheme=e.FeaturedCard, FreePass=0, JoinCost=d.JoinCost,
                    JoinCostResource="Gems", MaxLosses=d.MaxLoss, Rewards=rewards,
                    IconExportName=IconFor(e.Mode), Background=BackgroundFor(e.Mode,false), Background_Complete=BackgroundFor(e.Mode,true), WinIconExportName="tournament_open_wins_badge_bronze",
                    Arena=e.Mode.Contains("Touchdown") ? "Arena_TouchdownTest" : "All", UnlockedForXP="Experienced", HideTimer=true,
                    Subtitle="Free single match", Description=(e.FeaturedCard == null ? "" : "Featured card: " + e.FeaturedCard + ". ") + (e.Mode.Contains("GoldRush") ? "Tower bonuses: 100, 200, 500 gold. " : e.Mode.Contains("GemRush") ? "Tower bonuses: 3, 5, 10 gems. " : "") + $"One free battle. Win: {d.RewardGold[1]} gold and {d.RewardCards[1]} cards. Other results: {d.RewardGold[0]} gold and {d.RewardCards[0]} cards.",
                    DraftDeck=e.Mode.Contains("Touchdown") ? "Draft_Touchdown_v1" : "Draft_v1", Casual=false
                }, new JsonSerializerSettings { NullValueHandling=NullValueHandling.Ignore }));
        }
        public static void Encode(ChecksumEncoder s, ClashRoyale.Logic.Home.Home home = null)
        {
            // Three Home fields preceding its event manager; preserve the existing values.
            s.WriteVInt(1108); s.WriteVInt(52); s.WriteVInt(-1);
            s.WriteVInt((int)DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            s.WriteBoolean(true); s.WriteVInt(1); s.WriteVInt(Events.Length + 1);
            EncodeEvent(s, 1109, "2v2", 8, "{\"HideTimer\":true,\"HidePopupTimer\":true,\"ExtraGameModeChance\":0,\"GameMode\":\"TeamVsTeamLadder\",\"ExtraGameMode\":\"None\"}");
            foreach (var e in Events) EncodeDefinition(s,e);
            var current = Events.FirstOrDefault(e => e.GlobalId == home?.Challenge?.ModeId);
            // Native start-survival saves a copy of the entered Chronos event (+0xc).
            // The client needs this alongside SurvivalState to restore the event's joined view.
            s.WriteBoolean(current != null);
            if (current != null) EncodeDefinition(s,current);
            s.WriteVInt(0); // long array
            var entries = home?.EventEntries == null
                ? new System.Collections.Generic.Dictionary<int,int>()
                : new System.Collections.Generic.Dictionary<int,int>(home.EventEntries);
            if (current != null && !entries.ContainsKey(current.Id)) entries[current.Id]=1;
            s.WriteVInt(entries.Count);
            foreach(var entry in entries.OrderBy(e=>e.Key)) {
                s.WriteVInt(entry.Key); s.WriteVInt(entry.Value); s.WriteVInt(0); s.WriteVInt(0); s.WriteBoolean(false);
            }
            s.WriteVInt(0); // pending milestones
            s.WriteVInt(entries.Values.Sum()); s.WriteVInt(0);
            s.WriteVInt(1767225600); s.WriteVInt(0); s.WriteVInt(0);
            s.WriteVInt(0); s.WriteVInt(0); s.WriteVInt(0);
            s.WriteVInt(Events.Length+1);
            s.WriteVInt(1109); s.WriteBoolean(true);
            foreach(var e in Events) { s.WriteVInt(e.Id); s.WriteBoolean(true); }
            s.WriteVInt(Events.Length+1);
            s.WriteVInt(1109); s.WriteVInt(2); s.WriteVInt(0);
            foreach(var e in Events) { s.WriteVInt(e.Id); s.WriteVInt(2); s.WriteVInt(0); }
            // Preserve card-release and clan-chest configuration following the event manager.
            s.AddRange(Convert.FromBase64String("AgAAAGR7IklEIjoiQ0FSRF9SRUxFQVNFX1YyIiwiUGFyYW1zIjp7IkNhcmRzIjpbeyJTaG93QXNTb29uIjpmYWxzZSwiU3BlbGwiOiJHaG9zdCIsIkRhdGUiOiIyMDE4MDEwNCJ9XX19BAAAAJJ7IklEIjoiQ0xBTl9DSEVTVCIsIlBhcmFtcyI6eyJTdGFydFRpbWUiOiIyMDE3MDMxN1QwNzAwMDAuMDAwWiIsIkFjdGl2ZUR1cmF0aW9uIjoiUDNkVDBoIiwiSW5hY3RpdmVEdXJhdGlvbiI6IlA0ZFQwaCIsIkNoZXN0VHlwZSI6WyJDbGFuQ3Jvd25zIl19fQA="));
        }
    }
}

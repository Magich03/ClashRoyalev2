namespace ClashRoyale.Logic.Commands
{
    using System.Collections.Generic;

    using ClashRoyale.Extensions;
    using ClashRoyale.Extensions.Helper;
    using ClashRoyale.Files.Csv;
    using ClashRoyale.Logic.Mode;

    /// <summary>
    /// A client command the server has no logic for yet. It is decoded with the exact field layout of client 3.830
    /// (extracted from each Logic*Command::decode in libg.so) so the rest of the EndClientTurn message stays readable,
    /// and executes as a no-op.
    /// Layout codes: B = command base, v = VInt, b = bool, i = int, l = LogicLong (2 ints), s = string, d = data reference.
    /// </summary>
    public class LayoutCommand : Command
    {
        public static readonly Dictionary<int, string> Layouts = new Dictionary<int, string>
        {
            { 500, "Bv" },      // UpdateFlagSeen
            { 501, "Bvvv" },    // EventSeen
            { 514, "lbsB" },    // ShareReplay
            { 516, "B" },       // CollectFreeChest
            { 519, "B" },       // UnlockGameRoom
            { 521, "B" },       // BuySpecialOfferChest
            { 523, "Bivvv" },   // SwapSpellsEventDeck
            { 528, "B" },       // ClaimNewLevelsReward
            { 534, "B" },       // ClaimSurvivalReward
            { 537, "Bsl" },     // KickAllianceMember
            { 541, "Bv" },      // StartSurvival
            { 542, "Bv" },      // StartExploring
            { 543, "Bvb" },     // SpeedUpExploring
            { 545, "vvB" },     // ShopItemPurchaseNonIAP
            { 551, "Bd" },      // RequestSpells
            { 552, "Bv" },      // SelectSkin
            { 558, "B" },       // HelpOpened
            { 560, "Bvv" },     // BuyBooster
            { 561, "B" },       // UpdateHomeSurvivalState
            { 563, "Bblsv" },   // JoinGameRoom
            { 564, "B" },       // UpdateLastTournament
            { 566, "B" },       // UpdateLastSeenSpellDate
            { 568, "Bv" },      // ClearChestSource
            { 570, "Bvv" },     // EventShopOfferSeen
            { 571, "Bv" },      // NewShopChestSeen
            { 574, "Bl" },      // SpeedUpGameRoomReward
            { 578, "Bvv" },     // SelectHeroAbility
            { 579, "dB" },      // ClaimAchievementReward
            { 582, "Bvv" },     // UpdateSurvivalEventAnimationState
            { 583, "Bvv" },     // CollectSurvivalMilestoneReward
            { 586, "B" },       // ProductRedPopupSeen
            { 587, "Bb" },      // StartGameRoomMatchmake
            { 591, "B" },       // CollectClanChest
            { 593, "Bvv" },     // TvReplaySeen
            { 595, "B" },       // CollectMultiWinChest
            { 596, "dlvbsB" },  // ShareTvReplay
            { 597, "Bv" },      // StartRewardClaim
            { 598, "B" },       // ClaimGameRoomReward
        };

        private readonly int CommandType;
        private readonly string Layout;

        public override int Type
        {
            get
            {
                return this.CommandType;
            }
        }

        public LayoutCommand(int Type, string Layout)
        {
            this.CommandType = Type;
            this.Layout = Layout;
        }

        public override void Decode(ByteStream Stream)
        {
            foreach (char Field in this.Layout)
            {
                switch (Field)
                {
                    case 'B':
                        base.Decode(Stream);
                        break;
                    case 'v':
                        Stream.ReadVInt();
                        break;
                    case 'b':
                        Stream.ReadBoolean();
                        break;
                    case 'i':
                        Stream.ReadInt();
                        break;
                    case 'l':
                        Stream.ReadInt();
                        Stream.ReadInt();
                        break;
                    case 's':
                        Stream.ReadString();
                        break;
                    case 'd':
                        Stream.DecodeData<CsvData>();
                        break;
                }
            }
        }

        public override byte Execute(GameMode GameMode)
        {
            return 0;
        }
    }
}

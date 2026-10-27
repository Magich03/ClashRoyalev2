namespace ClashRoyale.Logic.Battle
{
    using System;
    using ClashRoyale.Logic.Player;
    using ClashRoyale.Logic.Home;
    using ClashRoyale.Messages.Server.Avatar;

    public static class MatchReward
    {
        // Private-server ladder policy: equal-strength wins/losses are +/-30; draws award no gold.
        public static BattleResultMessage Apply(Player player, string matchId, int own, int enemy, bool team = false)
        {
            if (player.LastRewardedMatch == matchId) return null;
            bool win = own > enemy;
            int delta = team ? 0 : win ? 30 : own < enemy ? -Math.Min(30, player.Score) : 0;
            var arena = player.Arena;
            int gold = win ? Math.Min(arena.BattleRewardGold, Math.Max(0, player.MaxGold-player.Gold)) : 0;
            player.ScoreChanged(delta);
            if (win && !team) player.PvPWins++;
            else if (own < enemy && !team) player.PvPLosses++;
            if (own == 3) player.IncreaseThreeCrownWins();
            if (gold > 0) player.AddFreeGold(gold);
            var result = new BattleResultMessage { Result = win ? 1 : own < enemy ? 2 : 3,
                TrophyDelta = delta, Gold = gold, OwnCrowns = own, OpponentCrowns = enemy };
            if (win && arena.SilverChestData != null)
            {
                var home = player.Home;
                if (home.ChestSlotCount == 0) home.ChestSlotCount = 4;
                while (home.Chests.Count < home.ChestSlotCount) home.Chests.Add(null);
                int slot = home.Chests.FindIndex(c => c == null);
                if (slot >= 0 && slot < home.ChestSlotCount)
                {
                    var chest = new Chest(arena.SilverChestData);
                    chest.SetNew(true); chest.SetSource(1); chest.SetSlot(slot);
                    chest.SetId(++home.RunningChestId);
                    home.Chests[slot] = chest;
                    result.ChestData = arena.SilverChestData;
                }
            }
            // Native home updateStarsFromAvatar (0x8e2da) consumes this resource
            // and calls increaseStarsToStarChest (0x15595c), which emits the crown animation.
            // Keep the home counter unchanged in the return-home snapshot.
            player.CommoditySlots.SetCommodityCount(ClashRoyale.Logic.Player.Enums.CommodityType.Resource,
                ClashRoyale.Files.Csv.CsvFiles.StarCountData, own);
            player.Home.Quests.Battle(player, own, enemy, team);
            player.LastRewardedMatch = matchId;
            player.RefreshArena();
            return result;
        }
    }
}

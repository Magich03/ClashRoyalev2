namespace ClashRoyale.Logic.Battle
{
    using System;
    using ClashRoyale.Enums;
    using ClashRoyale.Files.Csv;
    using ClashRoyale.Files.Csv.Logic;
    using ClashRoyale.Logic.Home;
    using ClashRoyale.Logic.Player;
    using ClashRoyale.Messages.Server.Avatar;

    public static class ChallengeBattle
    {
        // Battle-only snapshots: tournament caps must never downgrade the saved collection.
        public static Player Snapshot(Player source)
        {
            var copy = new Player(source.HighId, source.LowId);
            copy.Initialize();
            copy.Name = source.Name;
            copy.ExpLevel = Math.Min(9, source.ExpLevel);
            copy.Arena = source.Arena;
            copy.Score = source.Score;
            for (int i = 0; i < 8; i++)
            {
                var card = source.Home.SpellDeck[i]?.Clone();
                if (card == null) throw new InvalidOperationException("Challenge deck needs eight cards");
                card.Level = Math.Min(card.Level, Math.Max(0, 8 - card.Data.RarityData.RelativeLevel));
                copy.Home.SpellDeck.SetSpell(i, card);
            }
            return copy;
        }

        public static void EnsurePendingReward(Player player)
        {
            var run=player.Home.Challenge;
            if(run == null || !run.Finished || run.RewardClaimed || player.Home.ChallengeReward != null) return;
            {
                var data = run.Data;
                int index = Math.Clamp(run.Wins, 0, data.MaxWins);
                var chestData = CsvFiles.Get(Gamefile.TreasureChests).GetData<TreasureChestData>(
                    (data.Name == "Gold" ? "Survival_Gold_" : "Survival_Bronze_") + player.Arena.Name)
                    ?? CsvFiles.Get(Gamefile.TreasureChests).GetData<TreasureChestData>(data.Name == "Gold" ? "Survival_Gold" : "Survival_Bronze");
                player.Home.ChallengeReward = new Chest(chestData) {
                    OverrideGold = data.RewardGold[Math.Min(index, data.RewardGold.Length - 1)],
                    OverrideCards = data.RewardCards[Math.Min(index, data.RewardCards.Length - 1)]
                };
            }
        }

        public static BattleResultMessage Apply(Player player, string matchId, int own, int enemy)
        {
            var run = player.Home.Challenge;
            if (run == null || !run.RecordResult(matchId, own, enemy)) return null;
            EnsurePendingReward(player);
            var mode=run.GameMode;
            int gold=own >= 1 ? mode.GoldPerTower1 : 0, gems=own >= 1 ? mode.GemsPerTower1 : 0;
            if (own >= 2) { gold+=mode.GoldPerTower2; gems+=mode.GemsPerTower2; }
            if (own >= 3) { gold+=mode.GoldPerTower3; gems+=mode.GemsPerTower3; }
            gold=Math.Min(Math.Max(0,gold), Math.Max(0,player.MaxGold-player.Gold));
            gems=Math.Min(Math.Max(0,gems), Math.Max(0,int.MaxValue-Math.Max(player.Diamonds,player.FreeDiamonds)));
            if (gold > 0) player.AddFreeGold(gold);
            if (gems > 0) player.AddFreeDiamonds(gems);
            Console.WriteLine($"[EVENT] result event={run.ModeId} mode={run.GameMode.Name} score={own}-{enemy} wins={run.Wins} losses={run.Losses} reward={player.Home.ChallengeReward != null}");
            player.Home.IncreaseStarsToStarChest(own);
            player.CommoditySlots.SetCommodityCount(ClashRoyale.Logic.Player.Enums.CommodityType.Resource, CsvFiles.StarCountData, own);
            player.LastRewardedMatch = matchId;
            return new BattleResultMessage { Result = own > enemy ? 1 : own < enemy ? 2 : 3,
                OwnCrowns = own, OpponentCrowns = enemy, TrophyDelta = 0, Gold = gold };
        }
    }
}

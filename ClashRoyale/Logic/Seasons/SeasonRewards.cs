namespace ClashRoyale.Logic.Seasons
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using ClashRoyale.Crypto.Randomizers;
    using ClashRoyale.Logic.Home.Spells;
    using ClashRoyale.Logic.Mode;
    using ClashRoyale.Logic.Reward;
    using ClashRoyale.Logic.Commands.Server;

    public static class SeasonRewards
    {
        public static DraftReward Create(SeasonPrize prize, GameMode mode)
        {
            var basic = RewardRandomizer.RandomizeReward(prize.Chest, mode.Home);
            var pool = new SpellSet(prize.Arena, null);
            var pairs = new List<Spell>();
            foreach (var spell in basic.Spells)
            {
                var alternatives = pool.Spells[spell.Data.RarityData.Instance].Where(s => s != spell.Data).ToArray();
                if (alternatives.Length == 0) throw new InvalidOperationException("Missing draft alternative");
                var alternative = RewardRandomizer.CreateSpell(alternatives[XorShift.Next(alternatives.Length)]);
                alternative.SetMaterialCount(spell.Count);
                pairs.Add(spell); pairs.Add(alternative);
            }
            return new DraftReward { Spells = pairs, DiscardedSpells = new List<Spell>(), Gold = basic.Gold,
                Diamonds = basic.Diamonds, ChestId = prize.Chest.Id, ChestGlobalId = prize.Chest.Data.GlobalId, ChestType = 10 };
        }
        public static byte Claim(GameMode mode)
        {
            var prize = mode?.Player?.Season?.Pending;
            if (prize == null || prize.ClaimStarted || mode.Home.IsClaimingReward) return 3;
            prize.Draft ??= Create(prize, mode);
            prize.ClaimStarted = true;
            mode.Home.SetClaimingReward(true);
            mode.CommandManager.AddAvailableServerCommand(new ClaimRewardCommand(prize.Draft, 10, prize.Chest.Id));
            return 0;
        }
        public static void Resume(GameMode mode)
        {
            var prize = mode?.Player?.Season?.Pending;
            if (prize?.ClaimStarted != true || (prize.Draft?.Spells?.Count ?? 0) < 2) return;
            mode.Home.SetClaimingReward(true);
            mode.CommandManager.AddAvailableServerCommand(new ClaimRewardCommand(prize.Draft, 10, prize.Chest.Id));
        }
        public static byte Choose(GameMode mode, int choice)
        {
            var prize = mode?.Player?.Season?.Pending;
            if (choice < 0 || choice > 1 || prize?.ClaimStarted != true || (prize.Draft?.Spells?.Count ?? 0) < 2) return 3;
            var draft = prize.Draft;
            prize.Chosen.Add(draft.Spells[choice]);
            prize.Discarded.Add(draft.Spells[1 - choice]);
            draft.Spells.RemoveRange(0, 2);
            if (draft.Spells.Count == 0)
            {
                var reward = new Reward { Spells = prize.Chosen, DiscardedSpells = prize.Discarded,
                    Gold = draft.Gold, Diamonds = draft.Diamonds };
                return new ClaimRewardCommand(reward, 10, prize.Chest.Id).Execute(mode);
            }
            return 0;
        }
    }
}

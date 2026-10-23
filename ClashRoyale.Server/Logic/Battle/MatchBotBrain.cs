namespace ClashRoyale.Logic.Battle
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using ClashRoyale.Enums;
    using ClashRoyale.Files.Csv;
    using ClashRoyale.Files.Csv.Logic;
    using ClashRoyale.Logic.Commands;
    using ClashRoyale.Logic.Player;
    using LogicLong = ClashRoyale.Maths.LogicLong;

    // Tactical estimates from public deployments, not authoritative troop positions or hidden hand data.
    public sealed class MatchBotBrain
    {
        private readonly Player Bot;
        private readonly Random Random;
        private readonly int[] Hand = { 0, 1, 2, 3 };
        private readonly Queue<int> Cycle = new Queue<int>(new[] { 4, 5, 6, 7 });
        private readonly List<Deployment> Enemies = new List<Deployment>();
        private readonly List<Deployment> Allies = new List<Deployment>();
        private double Elixir = 5;
        private readonly ClashRoyale.Logic.Event.EventBattleRules Rules;
        private readonly double NormalMultiplier, OvertimeMultiplier;
        private int NextDecision = 6;
        private int LastTurn = -1;
        private int AttackLane;
        private sealed class Deployment
        {
            public SpellData Card;
            public CharacterData Character;
            public int X, Y, Turn;
            public double Covered;
            public int Lane => X < 9000 ? 0 : 1;
            // Approximate unobstructed travel; observations expire because deaths are not observable here.
            public int Position(int now, bool enemy) => Math.Clamp(Y + (enemy ? 1 : -1) *
                (int)(Math.Max(0, (now - Turn) * 0.5 - 1) * (Character?.Speed ?? 0) * 10), 6000, 25000);
        }
        public MatchBotBrain(Player bot, int seed, double normalMultiplier = 1.0, double overtimeMultiplier = 1.0, ClashRoyale.Logic.Event.EventBattleRules rules = null)
        {
            Rules = rules; Elixir = rules?.StartingElixir ?? 5;
            NormalMultiplier = normalMultiplier; OvertimeMultiplier = overtimeMultiplier; Bot = bot;
            Random = new Random(seed);
            AttackLane = Random.Next(2);
        }
        public void ResetTouchdownRound(int turn)
        {
            if (Rules?.Touchdown != true) return;
            Elixir=Rules.StartingElixir;
            LastTurn=turn;
            NextDecision=turn+4;
            Enemies.Clear(); Allies.Clear();
        }
        private static CharacterData Character(SpellData card) => string.IsNullOrEmpty(card.SummonCharacter)
            ? null : CsvFiles.Get(Gamefile.Characters).GetData<CharacterData>(card.SummonCharacter);
        public void Observe(PlaceCardCommand command, SpellData card, int turn)
        {
            if (card.Type == 28 || card.Type == 27) return;
            Enemies.Add(new Deployment { Card = card, Character = Character(card), X = command.X, Y = command.Y, Turn = turn });
            if (Enemies.Count > 32) Enemies.RemoveAt(0);
        }
        public PlaceCardCommand Think(int turn)
        {
            if (turn <= LastTurn) return null;
            for (int t = LastTurn + 1; t <= turn; t++)
                Elixir = Math.Min(10, Elixir + (Rules == null ? (t >= 240 ? OvertimeMultiplier : NormalMultiplier) * 0.5 / (t >= 240 ? 1.4 : 2.8) : Rules.ElixirPerSecond(t * 0.5) * 0.5));
            LastTurn = turn;
            Enemies.RemoveAll(d => turn - d.Turn > 44);
            Allies.RemoveAll(d => turn - d.Turn > 32);
            if (turn < NextDecision) return null;
            NextDecision = turn + Random.Next(2, 5); // Human-sized reaction pauses, never every frame.
            var threat = Enemies.Where(d => turn - d.Turn >= 2 && d.Covered < d.Card.ManaCost && d.Position(turn, true) > 13000)
                .OrderByDescending(d => d.Card.ManaCost - d.Covered + (d.Position(turn, true) - 13000) / 3000.0).FirstOrDefault();
            int lane = threat?.Lane ?? AttackLane;
            var push = Allies.LastOrDefault(d => d.Lane == lane && d.Character != null &&
                (d.Character.TargetOnlyBuildings || d.Character.Hitpoints >= 900) && turn - d.Turn < 20);
            int bestSlot = -1, x = 0, y = 0;
            double bestScore = double.NegativeInfinity;
            for (int slot = 0; slot < 4; slot++)
            {
                var card = Bot.Home.SpellDeck[Hand[slot]].Data;
                if (card.ManaCost > Elixir || card.Mirror || card.ManaCostFromSummonerMana) continue;
                var unit = Character(card);
                bool spell = card.Type == 28;
                bool building = card.Type == 27;
                bool siege = card.Name == "Mortar" || card.Name == "Xbow";
                bool pump = card.Name == "Elixir Collector";
                int px = lane == 0 ? 3500 : 14500, py = 22500;
                double score = Random.NextDouble() * 0.6;
                if (card.Name == "GoblinBarrel")
                {
                    if (threat != null || Elixir < 6) continue;
                    py = 6500;
                    score += 5;
                }
                else if (pump)
                {
                    if (threat != null || Elixir < 9 || turn >= 220) continue;
                    px = 9000; py = 28500; score += 4;
                }
                else if (spell)
                {
                    if (card.OnlyOwnTroops)
                    {
                        if (push == null || Allies.Count(d => d.Lane == lane) < 2) continue;
                        py = push.Position(turn, false);
                        score += 3;
                    }
                    else
                    {
                        var cluster = Enemies.Where(d => turn - d.Turn >= 2 && turn - d.Turn <= 26 &&
                            d.Covered < d.Card.ManaCost && d.Lane == lane).ToArray();
                        if (cluster.Length == 0 || cluster.Sum(d => Math.Max(1, d.Card.SummonNumber)) < 3) continue;
                        // Hold spells for observed groups instead of repeatedly hitting an empty tower.
                        py = (int)cluster.Average(d => d.Position(turn, true));
                        score += 5 + cluster.Length;
                    }
                }
                else if (threat != null)
                {
                    if (threat.Character?.FlyingHeight > 0 && unit?.AttacksAir != true) continue;
                    py = Math.Clamp(threat.Position(turn, true) + (unit?.Range >= 4000 ? 3500 : 1200), 18500, 24000);
                    score += 8 - card.ManaCost * 0.35;
                    if (unit?.TargetOnlyBuildings == true) score -= 8;
                    if (threat.Card.SummonNumber >= 3 && unit?.AreaDamageRadius > 0) score += 3;
                    if (building) { px = lane == 0 ? 7500 : 10500; py = 20500; score += 2; }
                }
                else
                {
                    if (siege)
                    {
                        if (Elixir < 8) continue;
                        py = 17500; score += 6;
                    }
                    else if (building) continue; // Save defensive buildings for an actual threat.
                    else if (push != null)
                    {
                        if (Elixir < card.ManaCost + 1) continue;
                        py = Math.Clamp(push.Position(turn, false) + 3000, 18500, 24000);
                        score += unit?.Range >= 4000 || unit?.AreaDamageRadius > 0 ? 6 : 2;
                    }
                    else
                    {
                        if (Elixir < 8) continue; // Build elixir before committing to a new push.
                        py = unit?.TargetOnlyBuildings == true && unit.Speed >= 100 ? 18500 : 27500;
                        score += unit?.TargetOnlyBuildings == true || unit?.Hitpoints >= 900 ? 5 : 1;
                    }
                }
                if (score > bestScore) { bestScore = score; bestSlot = slot; x = px; y = py; }
            }
            if (bestSlot < 0) return null;
            int index = Hand[bestSlot];
            var chosen = Bot.Home.SpellDeck[index].Data;
            Elixir -= chosen.ManaCost;
            Hand[bestSlot] = Cycle.Dequeue();
            Cycle.Enqueue(index);
            if (chosen.Type != 28)
                Allies.Add(new Deployment { Card = chosen, Character = Character(chosen), X = x, Y = y, Turn = turn });
            if (threat != null) threat.Covered += chosen.ManaCost;
            if (threat == null && push == null) AttackLane = lane;
            return new PlaceCardCommand { ExecutorId = new LogicLong(Bot.HighId, Bot.LowId), TickWhenGiven = turn * 10,
                HandIndex = index, DataClass = chosen.GlobalId / 1000000, DataInstance = chosen.Instance,
                HasCard = false, X = x, Y = y };
        }
    }
}

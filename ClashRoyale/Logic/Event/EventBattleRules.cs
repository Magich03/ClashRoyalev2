namespace ClashRoyale.Logic.Event
{
    using System;
    using ClashRoyale.Files.Csv.Logic;

    // Shared by matchmaking, battle encoding, bot estimates and result validation.
    public sealed class EventBattleRules
    {
        public GameModeData Mode { get; }
        public bool Team => Mode.Players == "TvT";
        public bool SuddenDeath => Mode.Name.Contains("Overtime", StringComparison.Ordinal);
        public bool RampUp => Mode.Name.Contains("RampUpElixir", StringComparison.Ordinal);
        public bool Touchdown => Mode.TargetTouchdowns > 0;
        public int StartingElixir => Mode.UseStartingElixir ? Math.Clamp(Mode.StartingElixir, 0, 10) : 5;
        public int RegulationSeconds { get; }
        public int OvertimeSeconds => Math.Max(0, Mode.OvertimeSeconds);
        public int MaxSeconds => RegulationSeconds + OvertimeSeconds;

        public EventBattleRules(GameModeData mode, LocationData location)
        {
            Mode = mode ?? throw new ArgumentNullException(nameof(mode));
            RegulationSeconds = SuddenDeath ? 0 : Math.Max(0, location.MatchLength);
        }

        // The normal final-minute doubling also applies to the CSV production multiplier.
        public double ElixirPerSecond(double seconds)
        {
            double normal = Mode.ElixirProductionMultiplier > 0 ? Mode.ElixirProductionMultiplier / 100.0 : 1;
            double late = Mode.ElixirProductionOvertimeMultiplier > 0 ? Mode.ElixirProductionOvertimeMultiplier / 100.0 : 1;
            // Native 2b39ae steps at tick 1220; 37464a uses 150% of
            // the doubled regeneration rate in the final minute and overtime.
            if (RampUp) return seconds >= Math.Max(0, RegulationSeconds - 60)
                ? late * 1.5 / 1.4
                : normal * (seconds >= 61 ? 2 : 1) / 2.8;
            bool doubled = SuddenDeath || seconds >= Math.Max(0, RegulationSeconds - 60);
            return doubled ? late / 1.4 : normal / 2.8;
        }

        public bool IsFinished(int tick, int first, int second)
        {
            if (tick < 0 || first < 0 || second < 0 || first > 3 || second > 3 || (first == 3 && second == 3)) return false;
            int target = Touchdown ? Mode.TargetTouchdowns : 3;
            return Math.Max(first, second) >= target ||
                (tick >= RegulationSeconds * 20 && first != second) || tick >= MaxSeconds * 20;
        }
    }
}

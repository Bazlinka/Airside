using System;
using Airside.Domain;

namespace Airside.Simulation
{
    /// <summary>
    /// Real-time research progression. The first project, Operations Efficiency,
    /// takes one simulated day and permanently trims the base daily running cost.
    /// Progress is reconstructed by replaying the <c>start-research</c> command —
    /// no save-schema field.
    /// </summary>
    public sealed class AirportResearch
    {
        public const string OperationsEfficiencyId = "ops-efficiency";
        public const string OperationsEfficiencyName = "Operations Efficiency";
        public const long OperationsEfficiencyCost = 2500;
        public const long OperationsEfficiencyDurationSeconds = DayCycle.DaySeconds;
        public const long OperationsEfficiencyDailyDiscount = 100;

        public bool IsResearching { get; private set; }
        public bool OperationsEfficiencyComplete { get; private set; }
        public SimulationTime ResearchStartedAt { get; private set; }
        public string ActiveProjectId { get; private set; } = string.Empty;

        public long DailyOperatingDiscount =>
            OperationsEfficiencyComplete ? OperationsEfficiencyDailyDiscount : 0;

        public bool CanStartOperationsEfficiency =>
            !OperationsEfficiencyComplete && !IsResearching;

        public double Progress01(SimulationTime now)
        {
            if (OperationsEfficiencyComplete)
                return 1.0;
            if (!IsResearching)
                return 0.0;

            var elapsed = now.ElapsedSeconds - ResearchStartedAt.ElapsedSeconds;
            if (elapsed <= 0)
                return 0.0;
            if (elapsed >= OperationsEfficiencyDurationSeconds)
                return 1.0;
            return (double)elapsed / OperationsEfficiencyDurationSeconds;
        }

        public long SecondsRemaining(SimulationTime now)
        {
            if (!IsResearching || OperationsEfficiencyComplete)
                return 0;
            var remaining = OperationsEfficiencyDurationSeconds
                - (now.ElapsedSeconds - ResearchStartedAt.ElapsedSeconds);
            return Math.Max(0, remaining);
        }

        public bool StartOperationsEfficiency(SimulationTime now)
        {
            if (!CanStartOperationsEfficiency)
                return false;

            IsResearching = true;
            ActiveProjectId = OperationsEfficiencyId;
            ResearchStartedAt = now;
            return true;
        }

        /// <summary>
        /// Advances research. Returns true on the tick the active project completes.
        /// </summary>
        public bool Update(SimulationTime now)
        {
            if (!IsResearching || OperationsEfficiencyComplete)
                return false;

            if (now.ElapsedSeconds - ResearchStartedAt.ElapsedSeconds
                < OperationsEfficiencyDurationSeconds)
                return false;

            IsResearching = false;
            ActiveProjectId = string.Empty;
            OperationsEfficiencyComplete = true;
            return true;
        }
    }
}

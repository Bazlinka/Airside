using System;
using Airside.Domain;

namespace Airside.Simulation
{
    /// <summary>
    /// Real-time research progression. Projects take one simulated day and unlock
    /// permanent economy bonuses. Progress is reconstructed by replaying
    /// <c>start-research</c> / <c>start-research-passenger-services</c> commands —
    /// no save-schema field.
    /// </summary>
    public sealed class AirportResearch
    {
        public const string OperationsEfficiencyId = "ops-efficiency";
        public const string OperationsEfficiencyName = "Operations Efficiency";
        public const long OperationsEfficiencyCost = 2500;
        public const long OperationsEfficiencyDurationSeconds = DayCycle.DaySeconds;
        public const long OperationsEfficiencyDailyDiscount = 100;

        public const string PassengerServicesId = "passenger-services";
        public const string PassengerServicesName = "Passenger Services";
        public const long PassengerServicesCost = 3500;
        public const long PassengerServicesDurationSeconds = DayCycle.DaySeconds;
        public const long PassengerServicesRouteBonus = 75;

        public bool IsResearching { get; private set; }
        public bool OperationsEfficiencyComplete { get; private set; }
        public bool PassengerServicesComplete { get; private set; }
        public SimulationTime ResearchStartedAt { get; private set; }
        public string ActiveProjectId { get; private set; } = string.Empty;

        public long DailyOperatingDiscount =>
            OperationsEfficiencyComplete ? OperationsEfficiencyDailyDiscount : 0;

        public long RouteIncomeBonus =>
            PassengerServicesComplete ? PassengerServicesRouteBonus : 0;

        public bool CanStartOperationsEfficiency =>
            !OperationsEfficiencyComplete && !IsResearching;

        public bool CanStartPassengerServices =>
            OperationsEfficiencyComplete && !PassengerServicesComplete && !IsResearching;

        public string ActiveProjectName =>
            ActiveProjectId == PassengerServicesId ? PassengerServicesName
            : ActiveProjectId == OperationsEfficiencyId ? OperationsEfficiencyName
            : string.Empty;

        public double Progress01(SimulationTime now)
        {
            if (!IsResearching)
                return PassengerServicesComplete && OperationsEfficiencyComplete ? 1.0 : 0.0;

            var duration = DurationFor(ActiveProjectId);
            var elapsed = now.ElapsedSeconds - ResearchStartedAt.ElapsedSeconds;
            if (elapsed <= 0)
                return 0.0;
            if (elapsed >= duration)
                return 1.0;
            return (double)elapsed / duration;
        }

        public long SecondsRemaining(SimulationTime now)
        {
            if (!IsResearching)
                return 0;
            var remaining = DurationFor(ActiveProjectId)
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

        public bool StartPassengerServices(SimulationTime now)
        {
            if (!CanStartPassengerServices)
                return false;

            IsResearching = true;
            ActiveProjectId = PassengerServicesId;
            ResearchStartedAt = now;
            return true;
        }

        public string LastCompletedProjectId { get; private set; } = string.Empty;

        /// <summary>
        /// Advances research. Returns true on the tick the active project completes.
        /// </summary>
        public bool Update(SimulationTime now)
        {
            LastCompletedProjectId = string.Empty;
            if (!IsResearching)
                return false;

            if (now.ElapsedSeconds - ResearchStartedAt.ElapsedSeconds < DurationFor(ActiveProjectId))
                return false;

            var finished = ActiveProjectId;
            IsResearching = false;
            ActiveProjectId = string.Empty;
            if (finished == PassengerServicesId)
                PassengerServicesComplete = true;
            else
                OperationsEfficiencyComplete = true;
            LastCompletedProjectId = finished;
            return true;
        }

        private static long DurationFor(string projectId) =>
            projectId == PassengerServicesId
                ? PassengerServicesDurationSeconds
                : OperationsEfficiencyDurationSeconds;
    }
}

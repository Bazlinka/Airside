using System;

namespace Airside.Simulation
{
    /// <summary>
    /// Ground crew the airport employs. The baseline headcount runs turnarounds at
    /// the normal rate; hiring extra shortens them, letting crew fall below the
    /// baseline stretches them and produces delays. Crew are paid a daily wage
    /// settled with the other running costs.
    /// </summary>
    public sealed class AirportStaffing
    {
        public const int BaselineGroundCrew = 4;
        public const int MinimumGroundCrew = 2;
        public const int MaximumGroundCrew = 8;
        public const long DailyWagePerCrew = 55;
        public const long HireCost = 120;

        public int GroundCrew { get; private set; } = BaselineGroundCrew;

        public long DailyWage => GroundCrew * DailyWagePerCrew;

        public bool IsUnderstaffed => GroundCrew < BaselineGroundCrew;

        /// <summary>
        /// Multiplier on turnaround task durations. Exactly 1.0 at the baseline
        /// headcount, so a game that never touches staffing behaves as before.
        /// </summary>
        public double TurnaroundSpeedFactor
        {
            get
            {
                if (GroundCrew >= BaselineGroundCrew)
                    return Math.Max(0.8, 1.0 - (GroundCrew - BaselineGroundCrew) * 0.06);

                return 1.0 + (BaselineGroundCrew - GroundCrew) * 0.18;
            }
        }

        public bool Hire()
        {
            if (GroundCrew >= MaximumGroundCrew)
                return false;

            GroundCrew++;
            return true;
        }

        public bool Release()
        {
            if (GroundCrew <= MinimumGroundCrew)
                return false;

            GroundCrew--;
            return true;
        }
    }
}

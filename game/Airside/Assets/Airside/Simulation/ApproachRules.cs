using System;
using Airside.Domain;

namespace Airside.Simulation
{
    /// <summary>
    /// The rule an arrival on final is held to: it keeps moving and either lands or goes around.
    /// It is never parked on the approach waiting for ground traffic (ADR 2026-10-07).
    /// Sources: ICAO Doc 4444 (PANS-ATM) 6.7 and 7.4 runway separation and landing clearance, ICAO
    /// Doc 8168 (PANS-OPS) stabilised approach, CASA MOS Part 172 §10.12 wake minima (see
    /// <see cref="WakeSeparation"/>). Figures are gameplay-scaled from those, as ADR 0243 does.
    /// </summary>
    public static class ApproachRules
    {
        /// <summary>Start of the displayed extended final, shared with arrival presentation.</summary>
        public const float ExtendedFinalMetres = 32_000f;

        /// <summary>
        /// An inbound already flying the extended final before a storm is committed to its
        /// arrival. Derive entry from its existing leg deadline so live play, catch-up and
        /// restored saves agree without a presentation callback or new persisted flag.
        /// </summary>
        public static bool EnteredFinalBeforeStorm(FleetAircraft aircraft, SimulationTime now,
            Func<SimulationTime, WeatherKind> weatherAt = null)
        {
            if (aircraft == null || aircraft.Type.IsRotorcraft)
                return false;
            if (aircraft.State == FleetState.HoldingForLanding)
                return true;
            if (aircraft.State != FleetState.Inbound || !aircraft.StateEndsAt.HasValue)
                return false;
            if (aircraft.ArrivalCommittedBeforeStorm) return true;
            var speed = CircuitProfile.Knots(AircraftPerformance.For(aircraft.Type).ApproachKnots);
            var entrySeconds = Math.Max(aircraft.StateStartedAt.ElapsedSeconds,
                aircraft.StateEndsAt.Value.ElapsedSeconds - (long)Math.Ceiling(ExtendedFinalMetres / speed));
            return entrySeconds <= now.ElapsedSeconds
                && (weatherAt ?? Weather.At)(new SimulationTime(entrySeconds)) != WeatherKind.Storm;
        }

        /// <summary>
        /// Longest an arrival may be established on final without a landing clearance. About the time to fly
        /// the last ~8 NM at reference speed; a stabilised approach is flown from ~1000 ft (PANS-OPS), so an
        /// aircraft still without clearance at this point goes around instead of waiting on the approach.
        /// </summary>
        public const long FinalHoldLimitSeconds = 4 * 60;

        /// <summary>
        /// An inbound joins final only when its landing is expected within this long, so a queue of
        /// arrivals is metered out in the circuit rather than stacked on short final. Kept under
        /// <see cref="FinalHoldLimitSeconds"/> so a normal clearance beats the decision point.
        /// </summary>
        public const long MeterHorizonSeconds = 3 * 60;

        /// <summary>The moment an arrival that joined final at <paramref name="joinedAtSeconds"/> must go around.</summary>
        public static long DecisionPointAt(long joinedAtSeconds) => joinedAtSeconds + FinalHoldLimitSeconds;
    }
}

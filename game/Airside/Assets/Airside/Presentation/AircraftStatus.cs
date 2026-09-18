using Airside.Domain;
using Airside.Simulation;

namespace Airside.Presentation
{
    /// <summary>How much an aircraft's current state needs the player's eye.</summary>
    public enum StatusSeverity
    {
        Normal,
        Attention,
        Warning
    }

    /// <summary>
    /// Presentation rules for aircraft status: short tag words, how long an untimed state has
    /// been waiting and how loudly to show it. Reads simulation state only; never changes it.
    /// </summary>
    public static class AircraftStatus
    {
        /// <summary>Holding this long is worth a glance.</summary>
        public const long HoldAttentionSeconds = 3 * 60;

        /// <summary>Holding this long is a problem.</summary>
        public const long HoldWarningSeconds = 10 * 60;

        /// <summary>Waits shorter than this are not worth printing.</summary>
        public const long ShowWaitAfterSeconds = 60;

        /// <summary>States with no end time: the aircraft waits for something else to free up.</summary>
        public static bool IsWaiting(FleetAircraft aircraft) =>
            aircraft != null
            && !aircraft.StateEndsAt.HasValue
            && aircraft.State is FleetState.HoldingShort or FleetState.HoldingForLanding or FleetState.AwaitingStand;

        /// <summary>Seconds spent in a waiting state, or 0 for any other state.</summary>
        public static long WaitingSeconds(FleetAircraft aircraft, SimulationTime now)
        {
            if (!IsWaiting(aircraft))
                return 0;
            var waited = now.ElapsedSeconds - aircraft.StateStartedAt.ElapsedSeconds;
            return waited > 0 ? waited : 0;
        }

        public static StatusSeverity Severity(FleetAircraft aircraft, SimulationTime now)
        {
            if (aircraft == null)
                return StatusSeverity.Normal;

            var departureDelay = FlightBoard.DepartureDelayMinutes(aircraft, now) * 60;
            if (departureDelay >= HoldWarningSeconds)
                return StatusSeverity.Warning;
            if (departureDelay >= HoldAttentionSeconds)
                return StatusSeverity.Attention;

            if (aircraft.State == FleetState.AwaitingStand)
                // The player must choose a bay; other operators pick their own.
                return aircraft.Airline.IsPlayer ? StatusSeverity.Warning : StatusSeverity.Attention;

            if (!IsWaiting(aircraft))
                return StatusSeverity.Normal;

            var waited = WaitingSeconds(aircraft, now);
            if (waited >= HoldWarningSeconds)
                return StatusSeverity.Warning;
            return waited >= HoldAttentionSeconds ? StatusSeverity.Attention : StatusSeverity.Normal;
        }

        /// <summary>" · waiting 4 min" once a waiting state has lasted a minute; empty otherwise.</summary>
        public static string WaitSuffix(FleetAircraft aircraft, SimulationTime now)
        {
            var waited = WaitingSeconds(aircraft, now);
            return waited < ShowWaitAfterSeconds ? string.Empty : $" · waiting {waited / 60} min";
        }

        /// <summary>0..1 towards the warning threshold, for the bar under a waiting row.</summary>
        public static float WaitProgress(FleetAircraft aircraft, SimulationTime now) =>
            System.Math.Min(1f, WaitingSeconds(aircraft, now) / (float)HoldWarningSeconds);

        /// <summary>The one or two words on a player's field tag.</summary>
        public static string TagPhase(FleetAircraft aircraft) => TagPhase(aircraft, default);

        /// <summary>Field-tag words, including how far through fuel / catering / boarding.</summary>
        public static string TagPhase(FleetAircraft aircraft, SimulationTime now)
        {
            if (aircraft == null)
                return string.Empty;
            if (aircraft.State == FleetState.AtStand && aircraft.Scheduled.HasValue && aircraft.Airline.IsPlayer)
            {
                var prep = DeparturePrep.For(aircraft, now);
                return prep.Ready ? "ready" : prep.Label.ToLowerInvariant();
            }

            return aircraft.State switch
            {
                FleetState.AtStand => aircraft.Scheduled.HasValue ? "planned" : "free",
                FleetState.TaxiOut => "taxiing",
                FleetState.HoldingShort => "holding",
                FleetState.TakingOff => "takeoff",
                FleetState.HoldingForLanding => "circuit",
                FleetState.Landing => "landing",
                FleetState.AwaitingStand => "needs stand",
                FleetState.TaxiIn => "taxiing in",
                _ => "away"
            };
        }
    }
}

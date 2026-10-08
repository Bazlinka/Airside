using System;
using System.Collections.Generic;
using Airside.Domain;

namespace Airside.Simulation
{
    /// <summary>
    /// Helicopter operations inside the airline simulation (ADR 0207). A helicopter is an ordinary fleet aircraft
    /// (stand, schedule, round trip, save) whose movements skip the runway and the taxi network: it departs
    /// straight from its helipad spot into <see cref="FleetState.TakingOff"/>, flies the leg, and arrives
    /// straight into <see cref="FleetState.Landing"/> onto a spot. The pad is the only shared resource, and
    /// weather is its only other gate.
    /// </summary>
    public sealed partial class AirlineOperations
    {
        private static IReadOnlyList<StableId> HelipadStandIds()
        {
            var ids = new StableId[AdelaideHelipad.Spots.Length];
            for (var i = 0; i < ids.Length; i++)
                ids[i] = AdelaideHelipad.Spots[i].Id;
            return ids;
        }

        /// <summary>
        /// Gap between a rescue crew's call-outs, in seconds. Deterministic per airframe and trip so a saved game
        /// and a game that never stopped fly the same day, and it never touches the gameplay random source.
        /// </summary>
        public const long RescueFirstCalloutSeconds = 4 * 60;
        public const long RescueFirstCalloutSpreadSeconds = 4 * 60;
        public const long RescueMinimumGapSeconds = 35 * 60;
        public const long RescueGapSpreadSeconds = 80 * 60;

        /// <summary>Time the crew spends at the scene or the hospital before flying home.</summary>
        public const long RescueTurnaroundMinimumSeconds = 8 * 60;
        public const long RescueTurnaroundSpreadSeconds = 14 * 60;

        public static readonly IReadOnlyList<(string Code, int Weight)> RescueNetwork = new[]
        {
            ("RAH", 4), ("FMC", 3), ("LMH", 2), ("MTB", 1), ("GAW", 1), ("VHB", 1)
        };

        /// <summary>
        /// When the pad can take another movement. Derived from the helicopters' own state (not stored), so
        /// it is right after a load and after any size of clock step.
        /// </summary>
        internal SimulationTime HelipadFreeAt()
        {
            var free = new SimulationTime(0);
            foreach (var aircraft in _fleet)
            {
                if (!aircraft.Type.IsRotorcraft)
                    continue;
                var profile = RotorcraftPerformance.For(aircraft.Type);
                SimulationTime? until = null;
                if (aircraft.State == FleetState.TakingOff)
                    until = aircraft.StateStartedAt.Advance(RotorcraftRules.PadSecondsForTakeoff(profile));
                else if (aircraft.State == FleetState.Landing)
                    until = aircraft.StateStartedAt.Advance(RotorcraftRules.PadSecondsForLanding(profile));
                if (until.HasValue && until.Value.CompareTo(free) > 0)
                    free = until.Value;
            }

            return free;
        }

        /// <summary>Weather and pad gate for a helicopter movement at <paramref name="now"/>.</summary>
        private bool RotorcraftMayMoveNow(FleetAircraft aircraft, SimulationTime now) =>
            RotorcraftRules.MayMove(WeatherAt(now), WindAt(now).Knots, aircraft.Airline.IsEmergency);

        private bool DepartRotorcraft(FleetAircraft aircraft, SimulationTime now)
        {
            var readyAt = DepartureReadyAt(aircraft);
            if (!RotorcraftMayMoveNow(aircraft, now))
                return NoteDelay(aircraft, now, readyAt, DelayCause.Weather);
            if (HelipadFreeAt().CompareTo(now) > 0)
                return NoteDelay(aircraft, now, readyAt, DelayCause.Pad);

            if (aircraft.Airline.IsPlayer)
            {
                var departAt = aircraft.Scheduled.Value.DepartAt.ElapsedSeconds;
                aircraft.PushbackLatenessSeconds = (int)(now.ElapsedSeconds - departAt);
                aircraft.PushbackDelay = DelayLedger.Close(aircraft.DelayLedger, departAt,
                    readyAt.ElapsedSeconds, now.ElapsedSeconds);
            }

            aircraft.DelayLedger = null;
            aircraft.CurrentDestination = aircraft.Scheduled.Value.Destination;
            aircraft.PublishedDepartureAt = aircraft.Scheduled.Value.PublishedAt;
            aircraft.PushedBackAt = now;
            aircraft.Scheduled = null;
            aircraft.PrepStartedAt = null;
            aircraft.DepartureStand = aircraft.Stand;
            aircraft.Stand = default;
            aircraft.WentAroundThisTrip = false;
            Transition(aircraft, FleetState.TakingOff, now,
                (long)Math.Round(RotorcraftPerformance.For(aircraft.Type).TakeoffSeconds));
            return true;
        }

        private bool ArriveRotorcraft(FleetAircraft aircraft, SimulationTime now)
        {
            // Weather holds the arrival out at its leg's end, as a storm holds a jet's final.
            if (!RotorcraftMayMoveNow(aircraft, now))
            {
                aircraft.ExtendUntil(Weather.NextBlock(now));
                return false;
            }

            var padFree = HelipadFreeAt();
            if (padFree.CompareTo(now) > 0)
            {
                aircraft.ExtendUntil(padFree);
                return false;
            }

            // Reserve a spot before committing to the approach: a helicopter cannot circle for one.
            var spot = SuggestStand(aircraft);
            if (spot == null)
            {
                aircraft.ExtendUntil(now.Advance(2 * 60));
                return false;
            }

            aircraft.Stand = spot.Value;
            Transition(aircraft, FleetState.Landing, now,
                (long)Math.Round(RotorcraftPerformance.For(aircraft.Type).LandingSeconds));
            return true;
        }

        /// <summary>
        /// The end of any arrival: back on a stand, trip counted, and the next flight settled or booked.
        /// Shared by a jet's taxi-in and a helicopter's landing.
        /// </summary>
        private bool FinishArrival(FleetAircraft aircraft, SimulationTime now)
        {
            if (aircraft.IsFerry)
            {
                // A ferry between bases (ADR 0239) is a repositioning, not a service: park it with no
                // revenue, no rotation and no wear, so its next real flight settles as CompletedTrips + 1.
                aircraft.IsFerry = false;
                aircraft.WentAroundThisTrip = false;
                Transition(aircraft, FleetState.AtStand, now, null);
                aircraft.CurrentDestination = null;
                return true;
            }

            var justFlown = aircraft.CurrentDestination;
            aircraft.CompletedTrips++;
            aircraft.RotationsSinceCheck++;
            // A rotation begun with the check already overdue (ADR 0085).
            if (aircraft.Airline.IsPlayer && aircraft.RotationsSinceCheck > Maintenance.IntervalRotations)
                CareerState?.ApplyPunctuality(-Maintenance.OverduePenalty);
            aircraft.WentAroundThisTrip = false;
            // Transition before clearing the destination so the AtStand event still
            // carries the route for the Operations board history strip.
            Transition(aircraft, FleetState.AtStand, now, null);
            aircraft.CurrentDestination = null;
            if (aircraft.Airline.IsPlayer)
                TrySettleFlight(aircraft, justFlown, now);
            else
                ScheduleAiDeparture(aircraft, now);
            return true;
        }

        /// <summary>
        /// Books the rescue crew's next call-out: a hospital picked by weight from the airframe and trip, at a
        /// gap that varies through the day. Emergency flights are exempt from the curfew, so the crew flies at
        /// night too. A call-out never uses the random source, so it cannot shift any other timeline.
        /// </summary>
        private void ScheduleRescueMission(FleetAircraft aircraft, SimulationTime now)
        {
            var seed = RescueSeed(aircraft, now);
            var total = 0;
            foreach (var (_, weight) in RescueNetwork)
                total += weight;
            var roll = Math.Abs(seed % total);
            Destination pick = default;
            foreach (var (code, weight) in RescueNetwork)
            {
                if (roll < weight)
                {
                    DestinationCatalogue.TryFind(code, out pick);
                    break;
                }

                roll -= weight;
            }

            if (pick.Code == null || !CanReach(aircraft, pick))
                return;
            // The first call-out of a new game (or of a save that has just gained the helicopter) comes soon, so
            // the crew is seen working rather than sitting for the better part of an hour.
            var gap = aircraft.CompletedTrips == 0
                ? RescueFirstCalloutSeconds + Math.Abs((seed / 7) % RescueFirstCalloutSpreadSeconds)
                : RescueMinimumGapSeconds + Math.Abs((seed / 7) % RescueGapSpreadSeconds);
            var at = now.Advance(gap - gap % 60);
            aircraft.Scheduled = new ScheduledDeparture(pick, at);
        }

        private static int RescueSeed(FleetAircraft aircraft, SimulationTime now)
        {
            unchecked
            {
                var hash = 41;
                foreach (var ch in aircraft.Registration)
                    hash = hash * 31 + ch;
                hash = hash * 31 + aircraft.CompletedTrips * 97;
                hash = hash * 31 + (int)(now.ElapsedSeconds / 3600);
                return hash & int.MaxValue;
            }
        }

        /// <summary>Time a rescue crew spends on the ground at the far end of a call-out.</summary>
        internal static long RescueTurnaroundSeconds(FleetAircraft aircraft)
        {
            unchecked
            {
                var hash = 17;
                foreach (var ch in aircraft.Registration)
                    hash = hash * 31 + ch;
                hash = hash * 31 + aircraft.CompletedTrips;
                return RescueTurnaroundMinimumSeconds + Math.Abs(hash % RescueTurnaroundSpreadSeconds);
            }
        }
    }
}

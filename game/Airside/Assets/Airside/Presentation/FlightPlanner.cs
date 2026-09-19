using System;
using System.Collections.Generic;
using Airside.Domain;
using Airside.Simulation;

namespace Airside.Presentation
{
    /// <summary>One destination as the flight planner lists it for a given aircraft.</summary>
    public readonly struct PlannerDestination
    {
        public PlannerDestination(Destination destination, double distanceKm, long airborneSeconds, bool reachable)
        {
            Destination = destination;
            DistanceKm = distanceKm;
            AirborneSeconds = airborneSeconds;
            Reachable = reachable;
        }

        public Destination Destination { get; }
        public double DistanceKm { get; }
        public long AirborneSeconds { get; }
        public bool Reachable { get; }
    }

    /// <summary>The timeline a planned trip is expected to follow, for the planner summary.</summary>
    public readonly struct PlannedTrip
    {
        public PlannedTrip(SimulationTime departStand, SimulationTime airborne, SimulationTime arriveDestination,
            SimulationTime leaveDestination, SimulationTime backAtAdelaide)
        {
            DepartStand = departStand;
            Airborne = airborne;
            ArriveDestination = arriveDestination;
            LeaveDestination = leaveDestination;
            BackAtAdelaide = backAtAdelaide;
        }

        public SimulationTime DepartStand { get; }
        public SimulationTime Airborne { get; }
        public SimulationTime ArriveDestination { get; }
        public SimulationTime LeaveDestination { get; }
        public SimulationTime BackAtAdelaide { get; }
    }

    /// <summary>
    /// Pure rules behind the flight planner HUD: which aircraft it plans, the destination
    /// list, the departure-time picker and map click hit-testing. No UnityEngine, so the
    /// EditMode suite covers it; never schedules anything itself.
    /// </summary>
    public static class FlightPlanner
    {
        public const long DepartureStepSeconds = 5 * 60;
        public const long MaxDepartureDelaySeconds = 12 * 3600;

        /// <summary>Map dots are small; clicks within this many GUI points still pick one.</summary>
        public const float DestinationHitRadius = 16f;

        public static readonly (string label, long seconds)[] QuickDepartures =
        {
            // The soonest option still leaves time to board, close up and start both engines.
            ("5 min", 5 * 60), ("15 min", 15 * 60), ("30 min", 30 * 60),
            ("1 h", 3600), ("2 h", 7200), ("4 h", 4 * 3600)
        };

        /// <summary>Seconds until a booked pushback, never negative. Does not raise to prep lead.</summary>
        public static long RemainingUntil(SimulationTime deadline, SimulationTime now)
        {
            var left = deadline.ElapsedSeconds - now.ElapsedSeconds;
            return left > 0 ? left : 0;
        }

        /// <summary>Clamp a departure delay to what the planner offers, including prep lead for a type.</summary>
        public static long ClampDelay(long seconds) => ClampDelay(seconds, null);

        public static long ClampDelay(long seconds, AircraftType type)
        {
            var floor = type == null
                ? EngineStartSequence.MinimumDepartureLeadSeconds
                : DeparturePrep.LeadSeconds(type);
            return Math.Max(floor, Math.Min(MaxDepartureDelaySeconds, seconds));
        }

        /// <summary>Nudge a delay by whole steps, snapping to the step grid (except the 3 min floor).</summary>
        public static long StepDelay(long seconds, int steps)
        {
            if (steps == 0)
                return ClampDelay(seconds);
            var snapped = steps > 0
                ? (seconds / DepartureStepSeconds + steps) * DepartureStepSeconds
                : ((seconds + DepartureStepSeconds - 1) / DepartureStepSeconds + steps) * DepartureStepSeconds;
            return ClampDelay(snapped);
        }

        /// <summary>
        /// The player aircraft the planner should open on: the selected one if it is the
        /// player's, else the first parked with nothing planned, else the first parked,
        /// else the first in the fleet.
        /// </summary>
        public static FleetAircraft ChoosePlanningAircraft(IReadOnlyList<FleetAircraft> playerFleet, string selectedId)
        {
            if (playerFleet == null || playerFleet.Count == 0)
                return null;
            if (!string.IsNullOrEmpty(selectedId))
                foreach (var aircraft in playerFleet)
                    if (aircraft.Registration == selectedId)
                        return aircraft;
            foreach (var aircraft in playerFleet)
                if (aircraft.State == FleetState.AtStand && !aircraft.Scheduled.HasValue)
                    return aircraft;
            foreach (var aircraft in playerFleet)
                if (aircraft.State == FleetState.AtStand)
                    return aircraft;
            return playerFleet[0];
        }

        /// <summary>Step through a list of aircraft, wrapping; an unknown current starts from the ends.</summary>
        public static FleetAircraft Cycle(IReadOnlyList<FleetAircraft> aircraft, string currentId, int delta)
        {
            if (aircraft == null || aircraft.Count == 0)
                return null;
            var index = -1;
            for (var i = 0; i < aircraft.Count; i++)
                if (aircraft[i].Registration == currentId)
                    index = i;
            if (index < 0)
                return delta >= 0 ? aircraft[0] : aircraft[aircraft.Count - 1];
            var next = ((index + delta) % aircraft.Count + aircraft.Count) % aircraft.Count;
            return aircraft[next];
        }

        /// <summary>Destinations for this aircraft: reachable first, each group nearest first.</summary>
        public static List<PlannerDestination> DestinationsFor(AirlineOperations operations, FleetAircraft aircraft)
        {
            var list = new List<PlannerDestination>();
            DestinationsFor(operations, aircraft, list);
            return list;
        }

        /// <summary>Same as above, refilling <paramref name="list"/> so the HUD does not allocate each frame.</summary>
        public static void DestinationsFor(AirlineOperations operations, FleetAircraft aircraft, List<PlannerDestination> list)
        {
            list.Clear();
            foreach (var destination in operations.MapDestinations())
            {
                var reachable = aircraft != null && operations.CanOperate(aircraft, destination);
                var airborne = aircraft != null ? operations.AirborneSeconds(aircraft, destination) : 0L;
                list.Add(new PlannerDestination(destination, operations.DistanceKm(destination), airborne, reachable));
            }

            list.Sort(ByReachThenDistance);
        }

        private static readonly Comparison<PlannerDestination> ByReachThenDistance = (a, b) =>
            {
                if (a.Reachable != b.Reachable)
                    return a.Reachable ? -1 : 1;
                var byDistance = a.DistanceKm.CompareTo(b.DistanceKm);
                return byDistance != 0 ? byDistance : string.CompareOrdinal(a.Destination.Code, b.Destination.Code);
            };

        /// <summary>Expected trip timeline if pushed back at <paramref name="departAt"/>, before any runway queue.</summary>
        public static PlannedTrip Estimate(FleetAircraft aircraft, long airborneSeconds, SimulationTime departAt)
        {
            // The untyped overloads default to an ATR 42, so every aircraft's departure
            // preview showed the same taxi/takeoff time regardless of what was actually
            // parked on the stand — a 787 planned exactly like an ATR 42.
            var airborne = departAt.Advance(AirlineOperations.TaxiOutSecondsFrom(aircraft.Stand, aircraft.Type)
                + AirlineOperations.TakeoffRunwaySecondsFor(aircraft.Type));
            var arrive = airborne.Advance(airborneSeconds);
            var leave = arrive.Advance(AirlineOperations.DestinationTurnaroundSeconds);
            var back = leave.Advance(airborneSeconds);
            return new PlannedTrip(departAt, airborne, arrive, leave, back);
        }

        /// <summary>A parked player aircraft with nothing booked, other than <paramref name="excludeId"/>.</summary>
        public static FleetAircraft NextFreeAircraft(IReadOnlyList<FleetAircraft> playerFleet, string excludeId)
        {
            if (playerFleet == null)
                return null;
            foreach (var aircraft in playerFleet)
                if (aircraft.Registration != excludeId && aircraft.State == FleetState.AtStand && !aircraft.Scheduled.HasValue)
                    return aircraft;
            return null;
        }

        /// <summary>
        /// When a busy aircraft should be back in the Adelaide circuit, before any landing
        /// queue — null when it is already parked. <paramref name="airborneSeconds"/> is one
        /// leg of its current trip.
        /// </summary>
        public static SimulationTime? ExpectedBackAt(FleetAircraft aircraft, long airborneSeconds, SimulationTime now)
        {
            var endsAt = aircraft.StateEndsAt ?? now;
            if (endsAt.CompareTo(now) < 0)
                endsAt = now;
            var turnaround = AirlineOperations.DestinationTurnaroundSeconds;
            var takeoffRunwaySeconds = AirlineOperations.TakeoffRunwaySecondsFor(aircraft.Type);
            return aircraft.State switch
            {
                FleetState.AtStand => null,
                FleetState.TaxiOut => endsAt.Advance(takeoffRunwaySeconds + airborneSeconds * 2 + turnaround),
                FleetState.HoldingShort or FleetState.TakingOff =>
                    now.Advance(takeoffRunwaySeconds + airborneSeconds * 2 + turnaround),
                FleetState.Outbound => endsAt.Advance(turnaround + airborneSeconds),
                FleetState.AtDestination => endsAt.Advance(airborneSeconds),
                FleetState.Inbound => endsAt,
                _ => now
            };
        }

        /// <summary>
        /// Index of the point nearest <paramref name="clickX"/>,<paramref name="clickY"/>
        /// within <paramref name="radius"/>, or -1. Nearest wins, so crowded dots near
        /// Adelaide (Kingscote, Port Lincoln) are still individually pickable.
        /// </summary>
        public static int NearestWithin(IReadOnlyList<(float x, float y)> points, float clickX, float clickY,
            float radius = DestinationHitRadius)
        {
            var best = -1;
            var bestSq = radius * radius;
            for (var i = 0; i < points.Count; i++)
            {
                var dx = points[i].x - clickX;
                var dy = points[i].y - clickY;
                var sq = dx * dx + dy * dy;
                if (sq <= bestSq)
                {
                    bestSq = sq;
                    best = i;
                }
            }

            return best;
        }
    }
}

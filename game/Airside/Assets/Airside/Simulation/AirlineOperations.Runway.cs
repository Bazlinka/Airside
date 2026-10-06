using System;
using System.Collections.Generic;
using Airside.Domain;

namespace Airside.Simulation
{
    public sealed partial class AirlineOperations
    {
        /// <summary>Pushback, tug disconnect and taxi from <paramref name="stand"/> to the runway 05 holding point.</summary>
        public static long TaxiOutSecondsFrom(StableId stand) => AdelaideGround.TaxiOut(stand).WholeSeconds;

        public static long TaxiOutSecondsFrom(StableId stand, AircraftType type) => AdelaideGround.TaxiOut(stand, type).WholeSeconds;

        public static long TaxiOutSecondsFrom(StableId stand, AircraftType type, RunwayDirection runway) =>
            AdelaideGround.TaxiOut(stand, type, runway).WholeSeconds;

        /// <summary>
        /// How long the next same-apron pushback must wait: the first aircraft's
        /// pushback, tug disconnect, and enough taxi to leave the stands. Sixty
        /// seconds used to release the neighbour while the first was still on T4.
        /// </summary>
        public static long TaxiClearSecondsFrom(StableId stand) =>
            TaxiClearSecondsFrom(stand, AdelaideGround.IsTerminalGate(stand) ? AircraftType.Boeing7378 : AircraftType.Atr42);

        public static long TaxiClearSecondsFrom(StableId stand, AircraftType type) =>
            TaxiClearSecondsFrom(stand, type, RunwayDirection.Runway05);

        public static long TaxiClearSecondsFrom(StableId stand, AircraftType type, RunwayDirection runway)
        {
            var leg = AdelaideGround.TaxiOut(stand, type, runway);
            if (leg.Parts.Count < 2)
                return Math.Max(TaxiReleaseSeparationSeconds, (long)Math.Ceiling(leg.Seconds * 0.4));

            var push = leg.Parts[0];
            var taxi = leg.Parts[1];
            var clearMetres = Math.Min(180f, taxi.Path.Length * 0.35f);
            var alongTaxi = taxi.Path.SecondsAtDistance(clearMetres);
            var wait = push.Seconds + taxi.PauseBeforeSeconds + Math.Max(90.0, alongTaxi);
            return Math.Max(TaxiReleaseSeparationSeconds, (long)Math.Ceiling(wait));
        }

        /// <summary>Taxi from the E2 holding point into <paramref name="stand"/>.</summary>
        public static long TaxiInSecondsTo(StableId stand) => AdelaideGround.TaxiIn(stand).WholeSeconds;

        public static long TaxiInSecondsTo(StableId stand, AircraftType type) => AdelaideGround.TaxiIn(stand, type).WholeSeconds;

        public static long TaxiInSecondsTo(StableId stand, AircraftType type, RunwayDirection runway) =>
            AdelaideGround.TaxiIn(stand, type, runway).WholeSeconds;

        public static long TakeoffRunwaySecondsFor(AircraftType type) =>
            TakeoffRunwaySecondsFor(type, RunwayDirection.Runway05);

        public static long TakeoffRunwaySecondsFor(AircraftType type, RunwayDirection runway) =>
            AdelaideGround.LineupFor(runway, type).WholeSeconds + AircraftPerformance.For(type).TakeoffSeconds;

        public static long LandingRunwaySecondsFor(AircraftType type) =>
            LandingRunwaySecondsFor(type, RunwayDirection.Runway05);

        public static long LandingRunwaySecondsFor(AircraftType type, RunwayDirection runway)
        {
            var profile = AircraftPerformance.For(type);
            return profile.ApproachSeconds + profile.LandingSeconds
                   + AdelaideGround.VacateFor(type, runway).WholeSeconds;
        }

        /// <summary>The runway this aircraft should use right now, given wind, type and destination.</summary>
        public RunwayDirection RunwayFor(FleetAircraft aircraft) =>
            RunwayWeather.Select(Wind, aircraft?.Type, DestinationForRunwayChoice(aircraft), Home);

        /// <summary>
        /// Dest-aligned runway preference is for departures (Perth jets take 23 in a
        /// light easterly). Arrivals and go-around rejoins follow the wind only —
        /// otherwise light wind lands them on the end they would take off toward.
        /// </summary>
        private static Destination? DestinationForRunwayChoice(FleetAircraft aircraft)
        {
            if (aircraft == null)
                return null;
            if (aircraft.State is FleetState.Inbound or FleetState.GoAround
                or FleetState.HoldingForLanding or FleetState.Landing
                or FleetState.AwaitingStand or FleetState.TaxiIn
                or FleetState.AtDestination)
                return null;
            return aircraft.CurrentDestination ?? aircraft.Scheduled?.Destination;
        }

        /// <summary>
        /// True while this aircraft still occupies its assigned strip. Landing
        /// keeps the long taxi to E2 in its state duration so the drawing does
        /// not teleport; the strip itself is free once the aircraft is clear
        /// of the pavement.
        /// </summary>
        public bool IsOccupyingRunway(FleetAircraft aircraft)
        {
            if (aircraft == null)
                return false;
            if (aircraft.State is not (FleetState.TakingOff or FleetState.Landing))
                return false;
            // A helicopter uses its pad, never a strip (ADR 0207).
            if (aircraft.Type.IsRotorcraft)
                return false;
            var until = StripBusyUntil(aircraft);
            return until.HasValue && until.Value.CompareTo(_clock.Now) > 0;
        }

        /// <summary>
        /// When the strip may accept the next movement. Landing frees at clear-of-runway,
        /// not after the long taxi to E2 that still belongs to the Landing state.
        /// </summary>
        private static SimulationTime? StripBusyUntil(FleetAircraft aircraft)
        {
            if (aircraft.State == FleetState.TakingOff && aircraft.StateEndsAt.HasValue)
            {
                // Same rule the tower uses when it clears the takeoff (RunTowerOnStrip): the strip
                // is held through lineup and the ground roll, not the climb-out. Using the whole
                // TakingOff state here pushed the free time back by the climb whenever a save was
                // loaded mid-takeoff, so a resumed game drifted from one that never stopped.
                var lineup = AdelaideGround.LineupFor(aircraft.AssignedRunway, aircraft.Type).WholeSeconds;
                var roll = (long)Math.Round(AircraftPerformance.For(aircraft.Type).TakeoffRollExactSeconds);
                var rolled = aircraft.StateStartedAt.Advance(lineup + roll);
                return rolled.CompareTo(aircraft.StateEndsAt.Value) < 0 ? rolled : aircraft.StateEndsAt;
            }

            if (aircraft.State != FleetState.Landing || !aircraft.StateEndsAt.HasValue)
                return aircraft.StateEndsAt;

            if (IsMissedApproachLanding(aircraft))
                return aircraft.StateEndsAt;

            var vacate = AdelaideGround.VacateFor(aircraft.Type, aircraft.AssignedRunway).WholeSeconds;
            var clear = AdelaideGround.ClearOfRunwaySeconds(aircraft.Type, aircraft.AssignedRunway);
            var duration = aircraft.StateEndsAt.Value.ElapsedSeconds - aircraft.StateStartedAt.ElapsedSeconds;
            if (duration <= vacate)
                return aircraft.StateEndsAt;

            // duration = final + roll + vacate; free when clear, which is earlier than vacate end.
            return aircraft.StateStartedAt.Advance(duration - vacate + clear);
        }

        private static SimulationTime GridBefore(SimulationTime now) =>
            new(Math.Max(0, now.ElapsedSeconds - GroundTraffic.GridSeconds));

        /// <summary>How long a movement cleared now would have the strip.</summary>
        internal long RunwayBusySeconds(FleetAircraft aircraft, bool landing)
        {
            var profile = AircraftPerformance.For(aircraft.Type);
            if (landing)
                return ApproachHold.RemainingFinalSeconds(profile.ApproachSeconds, aircraft.Registration)
                       + profile.LandingSeconds + AdelaideGround.ClearOfRunwaySeconds(aircraft.Type, aircraft.AssignedRunway);
            return AdelaideGround.LineupFor(aircraft.AssignedRunway, aircraft.Type).WholeSeconds
                   + (long)Math.Round(profile.TakeoffRollExactSeconds);
        }

        /// <summary>A taxiing aircraft due on <paramref name="mainStrip"/> between the two times, or null.</summary>
        internal FleetAircraft CrossingDue(bool mainStrip, SimulationTime from, SimulationTime until)
        {
            foreach (var aircraft in _fleet)
            {
                GroundLeg leg;
                if (aircraft.State == FleetState.TaxiOut)
                    leg = AdelaideGround.TaxiOut(aircraft.DepartureStand, aircraft.Type, aircraft.AssignedRunway);
                else if (aircraft.State == FleetState.TaxiIn)
                    leg = AdelaideGround.TaxiIn(aircraft.Stand, aircraft.Type, aircraft.AssignedRunway);
                else
                    continue;
                foreach (var crossing in RunwayCrossings.For(leg, aircraft.AssignedRunway))
                {
                    if (crossing.MainStrip != mainStrip)
                        continue;
                    var enter = aircraft.StateStartedAt.ElapsedSeconds + (long)Math.Floor(crossing.EnterSeconds);
                    var exit = aircraft.StateStartedAt.ElapsedSeconds + (long)Math.Ceiling(crossing.ExitSeconds);
                    if (exit > from.ElapsedSeconds && enter < until.ElapsedSeconds)
                        return aircraft;
                }
            }

            return null;
        }

        /// <summary>The first crossing on <paramref name="leg"/>, started now, that would meet a busy strip.</summary>
        internal RunwayCrossing? CrossingIntoBusyStrip(GroundLeg leg, RunwayDirection ownRunway, SimulationTime start)
        {
            foreach (var crossing in RunwayCrossings.For(leg, ownRunway))
            {
                var freeAt = crossing.MainStrip ? _mainRunwayFreeAt : _crossRunwayFreeAt;
                if (freeAt.ElapsedSeconds > start.ElapsedSeconds + (long)Math.Floor(crossing.EnterSeconds))
                    return crossing;
            }

            return null;
        }

        /// <summary>
        /// Next time ground can issue another pushback clearance on the same apron as
        /// <paramref name="terminalGate"/>. It is derived from active taxi-out state, so
        /// save files and event-driven catch-up remain deterministic.
        ///
        /// Regional bays and terminal gates sit on separate aprons with their own taxi
        /// routes (<see cref="AirportTaxiNetwork"/>) and never share pavement, so this used
        /// to serialise every pushback in the fleet through one global 60 s gate — a Rex
        /// Saab pushing back from a regional bay held up an unrelated Virgin 737 push from
        /// the terminal gates for no physical reason.
        /// </summary>
        private SimulationTime? NextTaxiReleaseAt(SimulationTime now, bool terminalGate)
        {
            SimulationTime? earliest = null;
            var rolling = 0;
            foreach (var aircraft in _fleet)
            {
                if (aircraft.State != FleetState.TaxiOut)
                    continue;
                if (AdelaideGround.IsTerminalGate(aircraft.DepartureStand) != terminalGate)
                    continue;
                rolling++;
                var candidate = aircraft.StateStartedAt.Advance(
                    TaxiClearSecondsFrom(aircraft.DepartureStand, aircraft.Type, aircraft.AssignedRunway));
                if (candidate.CompareTo(now) > 0 && (earliest == null || candidate.CompareTo(earliest.Value) < 0))
                    earliest = candidate;
            }

            return rolling >= MaxSimultaneousTaxiOutsPerApron ? earliest : null;
        }

        public CommandResult ScheduleDeparture(FleetAircraft aircraft, Destination destination, SimulationTime departAt)
        {
            if (aircraft == null || !_fleet.Contains(aircraft))
                return CommandResult.Refused("Unknown aircraft.");
            if (aircraft.State != FleetState.AtStand)
                return CommandResult.Refused($"{aircraft.Registration} is not parked on a stand.");
            if (destination.Equals(Home))
                return CommandResult.Refused($"{aircraft.Registration} is already at {Home.Name}.");
            if (!CanReach(aircraft, destination))
                return CommandResult.Refused(
                    $"{destination.Name} is {DistanceKm(destination):N0} km away. The {aircraft.Type.Name} only reaches {aircraft.Type.PracticalRangeKm:N0} km.");
            if (aircraft.CheckUntil is { } checkEnds && departAt.CompareTo(checkEnds) < 0)
                return CommandResult.Refused(
                    $"{aircraft.Registration} is in its check until {Clock.TimeText(checkEnds)}. Pick a later time.");
            if (aircraft.Airline.IsPlayer && !RouteAccess.Allows(aircraft.Type, destination))
                return CommandResult.Refused(
                    $"{Article.CapitalA(aircraft.Type.Name)} only flies {RouteAccess.Label(RouteAccess.Ceiling(aircraft.Type))} routes. {destination.Name} is {RouteAccess.Label(RouteAccess.BandOf(destination))}.");
            if (aircraft.Airline.IsPlayer && CareerState != null
                && CareerState.Tier < RouteAccess.RequiredTier(RouteAccess.BandOf(destination)))
                return CommandResult.Refused(
                    $"{destination.Name} is international. You need the International tier to fly there.");
            if (departAt.CompareTo(_processedTo) < 0)
                return CommandResult.Refused("Departure time is in the past.");

            if (aircraft.Airline.IsPlayer)
            {
                var cost = DispatchCost(aircraft.Type, DistanceKm(destination));
                var alreadyPaid = aircraft.Scheduled.HasValue
                    ? DispatchCost(aircraft.Type, DistanceKm(aircraft.Scheduled.Value.Destination))
                    : 0;
                var recoveryCredit = CareerState.Funds + alreadyPaid < cost
                    && alreadyPaid == 0
                    && CareerState.TryChargeRecoveryDispatch(cost, destination.Code);
                if (CareerState.Funds + alreadyPaid < cost && !recoveryCredit)
                    return CommandResult.Refused(
                        $"This flight costs ${cost:N0}. You have ${CareerState.Funds:N0}.");
                if (!recoveryCredit)
                {
                    if (alreadyPaid > 0) CareerState.RefundDispatch(alreadyPaid);
                    CareerState.TryChargeDispatch(cost);
                }
                // Align prep with the booked pushback: done in time for the departure countdown
                // (ADR 0177), or starting now if that is already later. Recompute on every book so
                // an earlier rebook cannot leave a future PrepStartedAt that blocks pushback forever.
                var total = DeparturePrep.TotalSeconds(aircraft.Type, CareerState.BaseLevel);
                var start = departAt.ElapsedSeconds - total - (long)DepartureCountdown.PrepEndsBeforeSeconds;
                if (start < _processedTo.ElapsedSeconds)
                    start = _processedTo.ElapsedSeconds;
                if (start < 0)
                    start = 0;
                // Keep progress already pumped when the new start is not later than the old one.
                if (!aircraft.PrepStartedAt.HasValue
                    || aircraft.PrepStartedAt.Value.ElapsedSeconds > start)
                    aircraft.PrepStartedAt = new SimulationTime(start);
            }

            aircraft.Scheduled = new ScheduledDeparture(destination, departAt);
            return CommandResult.Ok;
        }

        public CommandResult CancelDeparture(FleetAircraft aircraft)
        {
            if (aircraft == null || !_fleet.Contains(aircraft))
                return CommandResult.Refused("Unknown aircraft.");
            if (aircraft.State != FleetState.AtStand || !aircraft.Scheduled.HasValue)
                return CommandResult.Refused($"{aircraft.Registration} has no flight waiting to start.");

            if (aircraft.Airline.IsPlayer && aircraft.Scheduled.HasValue)
                CareerState.RefundDispatch(DispatchCost(aircraft.Type,
                    DistanceKm(aircraft.Scheduled.Value.Destination)));

            // ADR 0053: a broken commitment against the active career contract costs
            // reliability — only when the cancelled flight would actually have counted
            // (right airline, aircraft, route); an unrelated cancellation is free.
            if (aircraft.Airline.IsPlayer && CareerState.ActiveContract != null
                && CareerState.TryFindDefinition(CareerState.ActiveContract.DefinitionId, out var contract)
                && contract.MatchesAircraft(aircraft.Type, aircraft.IsFreighter)
                && contract.MatchesRoute(Home.Code, aircraft.Scheduled.Value.Destination.Code))
                CareerState.PenalizeCancellation(contract.Id, contract.ReliabilityLossOnCancel);

            aircraft.Scheduled = null;
            aircraft.PrepStartedAt = null;
            return CommandResult.Ok;
        }

        /// <summary>
        /// One movement per physical strip at a time. Arrivals normally go first on
        /// that strip; a departure that has held short past <see cref="DepartureMaxHoldSeconds"/>,
        /// and longer than the first arrival has circled, goes instead. Derived from
        /// state times only, so saves need nothing new beyond the per-strip free times.
        /// </summary>
        private bool RunTower(SimulationTime now)
        {
            var changed = false;
            changed |= RunTowerOnStrip(now, mainStrip: true);
            changed |= RunTowerOnStrip(now, mainStrip: false);
            return changed;
        }

        private bool RunTowerOnStrip(SimulationTime now, bool mainStrip)
        {
            var freeAt = mainStrip ? _mainRunwayFreeAt : _crossRunwayFreeAt;
            if (freeAt.CompareTo(now) > 0)
                return false;

            // The longest-waiting arrival that may land now. One the curfew holds is sent to the
            // opening; it used to block every arrival behind it, including ones allowed to land.
            FleetAircraft arrival = null;
            foreach (var waiting in _fleet)
            {
                if (waiting.State != FleetState.HoldingForLanding
                    || RunwayWeather.IsMainRunway(waiting.AssignedRunway) != mainStrip)
                    continue;
                if (!MayUseRunwayDuringCurfew(waiting, now))
                {
                    if (!ExemptFromCurfew(waiting))
                        waiting.ExtendUntil(AirportCurfew.OpensAt(now, Clock));
                    continue;
                }
                if (arrival == null || waiting.StateStartedAt.CompareTo(arrival.StateStartedAt) < 0)
                    arrival = waiting;
            }
            var departure = LongestWaiting(FleetState.HoldingShort, mainStrip);
            if (departure != null && !MayUseRunwayDuringCurfew(departure, now))
                departure = null;
            // ADR 0058 / 0190: a storm holds every new clearance. An aircraft already on
            // final is a movement underway and lands; departures stay at the hold.
            var storm = Weather.At(now) == WeatherKind.Storm;
            var launch = storm ? null : departure;
            if (storm && arrival == null)
                return false;
            var next = arrival ?? launch;
            if (arrival != null && launch != null
                && now.ElapsedSeconds - launch.StateStartedAt.ElapsedSeconds >= DepartureMaxHoldSeconds
                && launch.StateStartedAt.CompareTo(arrival.StateStartedAt) < 0)
                next = launch;
            // An arrival whose vacate runs through an aircraft holding short (12's exit passes the
            // 30 hold) would drive through it: send the holder first, then land the arrival.
            // During a storm the holder cannot be sent, so the arrival waits rather than driving through.
            if (next == null)
                return false;
            if (next == arrival && departure != null && VacateCrossesHolder(arrival, mainStrip))
            {
                if (launch == null)
                    return false;
                next = departure;
            }
            // Nor may its vacate run into traffic already taxiing. If it would, a waiting
            // departure goes first; otherwise the arrival holds a little longer, re-checked on
            // the ground-control grid so the result does not depend on how the clock steps.
            if (next == arrival && !VacateClearOfTaxiing(arrival, now))
            {
                if (launch != null)
                    next = departure;
                else
                    return false;
            }
            if (next == arrival && !now.Equals(freeAt) && !now.Equals(arrival.StateStartedAt)
                && !GroundTraffic.OnGrid(now))
                return false;
            if (next == null)
                return false;

            // Keep the end they taxied to / held final on. Refreshing 05↔23 (or 12↔30)
            // here teleported lined-up and short-final traffic across the strip when the
            // wind was near a tie. Go-around rejoin still reassigns via AdvanceAircraft.
            var landing = next.State == FleetState.HoldingForLanding;
            var profile = AircraftPerformance.For(next.Type);
            // ADR 0126: no landing or takeoff while taxiing traffic is due across this strip. The
            // crossing aircraft is already moving, so the wait is short; re-checked on the grid.
            if (CrossingDue(mainStrip, now, now.Advance(RunwayBusySeconds(next, landing))) != null)
                return false;
            // …and once it is across, the clearance waits for the grid, so it does not depend on how
            // the clock was stepped past the moment the crossing ended.
            if (!GroundTraffic.OnGrid(now) && CrossingDue(mainStrip, GridBefore(now), now) != null)
                return false;
            if (landing && ShouldGoAround(next, now, mainStrip))
            {
                // Abort off short final — same remaining final the holder is already flying —
                // so the strip frees quickly and the aircraft does not teleport 4 km out.
                next.WentAroundThisTrip = true;
                var missedFinal = ApproachHold.RemainingFinalSeconds(profile.ApproachSeconds, next.Registration);
                Transition(next, FleetState.Landing, now, missedFinal);
                SetStripFreeAt(mainStrip, now.Advance(missedFinal + WakeSeparationSeconds(next.Type)));
                return true;
            }

            if (landing)
            {
                // Match the short-final visual budget so the strip is not locked for a
                // full long-final that the aircraft is not flying.
                var finalSeconds = ApproachHold.RemainingFinalSeconds(profile.ApproachSeconds, next.Registration);
                var vacateSeconds = AdelaideGround.VacateFor(next.Type, next.AssignedRunway).WholeSeconds;
                var clearSeconds = AdelaideGround.ClearOfRunwaySeconds(next.Type, next.AssignedRunway);
                var runwaySeconds = finalSeconds + profile.LandingSeconds + vacateSeconds;
                Transition(next, FleetState.Landing, now, runwaySeconds);
                SetStripFreeAt(mainStrip,
                    now.Advance(finalSeconds + profile.LandingSeconds + clearSeconds
                                + WakeSeparationSeconds(next.Type)));
                return true;
            }

            var lineupSeconds = AdelaideGround.LineupFor(next.AssignedRunway, next.Type).WholeSeconds;
            var takeoffSeconds = lineupSeconds + profile.TakeoffSeconds;
            Transition(next, FleetState.TakingOff, now, takeoffSeconds);
            // The strip itself is only occupied through the ground roll to rotation — the
            // arrival side already draws this distinction (ClearOfRunwaySeconds vs. the fuller
            // vacate/landing duration used for the visible state); this used to reuse the whole
            // TakingOff *state* duration (roll + the initial climb already well clear of the
            // tarmac) instead, so the next holder waited through the departing aircraft's climb
            // as well as its wake separation — the unrealistically long gap a real play session
            // reported before the next departure gets moving.
            var rollSeconds = (long)Math.Round(profile.TakeoffRollExactSeconds);
            SetStripFreeAt(mainStrip,
                now.Advance(lineupSeconds + rollSeconds + WakeSeparationSeconds(next.Type)));
            return true;
        }

        /// <summary>When a departure at its stand became ready to push: booked time, or prep end.</summary>
        private SimulationTime DepartureReadyAt(FleetAircraft aircraft)
        {
            var ready = aircraft.Scheduled.Value.DepartAt;
            if (aircraft.Airline.IsPlayer && aircraft.PrepStartedAt.HasValue)
            {
                var prepEnd = aircraft.PrepStartedAt.Value.Advance(DeparturePrep.TotalSeconds(aircraft.Type, CareerState.BaseLevel));
                if (prepEnd.CompareTo(ready) > 0)
                    ready = prepEnd;
            }

            return ready;
        }

        public SimulationTime? ExpectedLandingClearance(FleetAircraft aircraft, out RunwayDirection runway)
        {
            var queued = ExpectedLandingQueueTime(aircraft, out runway);
            return queued.HasValue ? GroundClearanceAt(aircraft, queued.Value) : null;
        }

        /// <summary>
        /// The runway-queue part of <see cref="ExpectedLandingClearance"/>, before the tower's ground
        /// check. Cheap; the ground check is the expensive part (up to 144 path tests), so the
        /// presentation runs it a few steps per frame with <see cref="LandingGroundClear"/>.
        /// </summary>
        public SimulationTime? ExpectedLandingQueueTime(FleetAircraft aircraft, out RunwayDirection runway)
        {
            runway = RunwayDirection.Runway05;
            if (aircraft == null)
                return null;
            SimulationTime joins;
            if (aircraft.State == FleetState.HoldingForLanding)
            {
                runway = aircraft.AssignedRunway;
                joins = aircraft.StateStartedAt;
            }
            else if (aircraft.State == FleetState.Inbound && aircraft.StateEndsAt.HasValue)
            {
                // Not assigned yet: the end the tower would give it now.
                runway = RunwayFor(aircraft);
                joins = aircraft.StateEndsAt.Value;
            }
            else
            {
                return null;
            }

            var mainStrip = RunwayWeather.IsMainRunway(runway);
            // Already on final: the tower will land it through a storm (ADR 0190), so the
            // estimate must not park it until the weather block ends.
            var established = aircraft.State == FleetState.HoldingForLanding;
            var arrivals = new List<(FleetAircraft Aircraft, SimulationTime Joined)>();
            var departures = new List<FleetAircraft>();
            foreach (var other in _fleet)
            {
                if (ReferenceEquals(other, aircraft))
                    continue;
                // Another inbound that joins first lands first. Leaving those out gave arrivals
                // due close together the same estimate, drawn nose to tail on the final.
                if (!established && other.State == FleetState.Inbound && other.StateEndsAt.HasValue)
                {
                    if (RunwayWeather.IsMainRunway(RunwayFor(other)) == mainStrip
                        && Before(other, other.StateEndsAt.Value, aircraft, joins))
                        arrivals.Add((other, other.StateEndsAt.Value));
                    continue;
                }

                if (RunwayWeather.IsMainRunway(other.AssignedRunway) != mainStrip)
                    continue;
                if (other.State == FleetState.HoldingForLanding && Before(other, other.StateStartedAt, aircraft, joins))
                    arrivals.Add((other, other.StateStartedAt));
                else if (other.State == FleetState.HoldingShort)
                    departures.Add(other);
            }

            arrivals.Sort((a, b) => Before(a.Aircraft, a.Joined, b.Aircraft, b.Joined) ? -1 : 1);
            departures.Sort((a, b) => a.StateStartedAt.CompareTo(b.StateStartedAt));
            if (established && Weather.At(_clock.Now) == WeatherKind.Storm)
                departures.Clear();

            var firstJoins = joins;
            foreach (var arrival in arrivals)
                if (arrival.Aircraft.State == FleetState.Inbound && arrival.Joined.CompareTo(firstJoins) < 0)
                    firstJoins = arrival.Joined;

            var at = mainStrip ? _mainRunwayFreeAt : _crossRunwayFreeAt;
            if (at.CompareTo(_clock.Now) < 0)
                at = _clock.Now;
            // Until an inbound joins the queue nothing is waiting to land, so holders depart freely.
            while (aircraft.State == FleetState.Inbound && departures.Count > 0 && at.CompareTo(firstJoins) < 0)
            {
                at = AfterStorms(at);
                if (at.CompareTo(firstJoins) >= 0)
                    break;
                at = at.Advance(DepartureRunwaySeconds(departures[0]));
                departures.RemoveAt(0);
            }

            if (at.CompareTo(firstJoins) < 0 && aircraft.State == FleetState.Inbound)
                at = firstJoins;

            for (var guard = 0; guard < 256; guard++)
            {
                if (!established)
                    at = AfterStorms(at);
                var nextArrivalJoined = arrivals.Count > 0 ? arrivals[0].Joined : joins;
                if (departures.Count > 0
                    && departures[0].StateStartedAt.CompareTo(nextArrivalJoined) < 0
                    && at.ElapsedSeconds - departures[0].StateStartedAt.ElapsedSeconds >= DepartureMaxHoldSeconds)
                {
                    at = at.Advance(DepartureRunwaySeconds(departures[0]));
                    departures.RemoveAt(0);
                    continue;
                }

                if (arrivals.Count == 0)
                    return at.CompareTo(joins) < 0 && aircraft.State == FleetState.Inbound ? joins : at;

                var (ahead, aheadJoined) = arrivals[0];
                arrivals.RemoveAt(0);
                if (at.CompareTo(aheadJoined) < 0)
                    at = aheadJoined;
                var landing = AircraftPerformance.For(ahead.Type);
                at = at.Advance(ApproachHold.RemainingFinalSeconds(landing.ApproachSeconds, ahead.Registration)
                                + landing.LandingSeconds
                                + AdelaideGround.ClearOfRunwaySeconds(ahead.Type, ahead.AssignedRunway)
                                + WakeSeparationSeconds(ahead.Type));
            }

            return at;
        }

        /// <summary>
        /// One step of the tower's ground check on a displayed landing estimate: true when an
        /// arrival cleared at <paramref name="at"/> would vacate clear of taxiing traffic and of
        /// any crossing. <see cref="ExpectedLandingClearance"/> walks this over the five-second grid.
        /// </summary>
        public bool LandingGroundClear(FleetAircraft aircraft, SimulationTime at)
        {
            var main = RunwayWeather.IsMainRunway(aircraft.AssignedRunway);
            var busy = RunwayBusySeconds(aircraft, landing: true);
            return VacateClearOfTaxiing(aircraft, at)
                   && CrossingDue(main, at, at.Advance(busy)) == null
                   && (GroundTraffic.OnGrid(at) || CrossingDue(main, GridBefore(at), at) == null);
        }

        /// <summary>Apply the tower's taxi/vacate check to a displayed landing estimate.</summary>
        private SimulationTime GroundClearanceAt(FleetAircraft aircraft, SimulationTime at)
        {
            // Ground releases are checked on the five-second grid; twelve minutes is a generous
            // bounded horizon. Each step tests paths against the whole fleet, so the drawn final
            // spreads these steps across frames (ADR 0173). Like the tower, wait out taxiing
            // traffic due across the strip (ADR 0126).
            for (var i = 0; i < LandingGroundCheckSteps && !LandingGroundClear(aircraft, at); i++)
                at = GroundTraffic.NextGrid(at);
            return at;
        }

        /// <summary>Its vacate, flown from now, stays clear of every aircraft already moving on the ground.</summary>
        private bool VacateClearOfTaxiing(FleetAircraft arrival, SimulationTime now)
        {
            var profile = AircraftPerformance.For(arrival.Type);
            var touchdownIn = ApproachHold.RemainingFinalSeconds(profile.ApproachSeconds, arrival.Registration)
                              + profile.LandingSeconds;
            var vacate = AdelaideGround.VacateFor(arrival.Type, arrival.AssignedRunway);
            if (!GroundTraffic.PathClear(_fleet, arrival, vacate, arrival.AssignedRunway, taxiOut: false,
                    now.Advance(touchdownIn), includeStationary: false))
                return false;

            // A reserved stand lets the tower validate the route beyond the runway exit too.
            // Checking only the vacate allowed an outbound aircraft to cross that waiting point
            // a few seconds after the arrival stopped there. Only the first stretch though (ADR 0180):
            // the rest is checked by ground control before the taxi-in starts.
            if (string.IsNullOrEmpty(arrival.Stand.Value))
                return true;
            var taxiIn = AdelaideGround.TaxiIn(arrival.Stand, arrival.Type, arrival.AssignedRunway);
            return GroundTraffic.PathClear(_fleet, arrival, taxiIn, arrival.AssignedRunway, taxiOut: false,
                now.Advance(touchdownIn + vacate.WholeSeconds), includeStationary: false, out _,
                GroundTraffic.LandingTaxiInHorizonSeconds);
        }

        private bool VacateCrossesHolder(FleetAircraft arrival, bool mainStrip)
        {
            var vacate = AdelaideGround.VacateFor(arrival.Type, arrival.AssignedRunway);
            var half = GroundTraffic.HalfSpan(arrival.Type);
            foreach (var holder in _fleet)
            {
                if (holder.State != FleetState.HoldingShort || RunwayWeather.IsMainRunway(holder.AssignedRunway) != mainStrip)
                    continue;
                var pose = AdelaideGround.HoldingShortPose(holder.DepartureStand,
                    FleetVisual.QueueSlot(_fleet, holder, _processedTo), holder.AssignedRunway, holder.Type);
                for (var s = 0.0; s <= vacate.Seconds; s += 2.0)
                    if (GroundTraffic.TooClose(vacate.PoseAt(s), half, pose, GroundTraffic.HalfSpan(holder.Type)))
                        return true;
            }

            return false;
        }

        /// <summary>Player and RFDS may use the runway during the 23:00–05:00 curfew.</summary>
        public static bool ExemptFromCurfew(FleetAircraft aircraft) =>
            aircraft?.Airline != null && (aircraft.Airline.IsPlayer || aircraft.Airline.IsEmergency);

        private bool MayUseRunwayDuringCurfew(FleetAircraft aircraft, SimulationTime now)
        {
            if (ExemptFromCurfew(aircraft) || !AirportCurfew.IsClosed(now, Clock))
                return true;
            // Already moving: taxi-before-23:00 may take off; an arrival already on
            // short final may land. New commercial inbounds wait until 05:00.
            if (aircraft.State is FleetState.HoldingShort or FleetState.TaxiOut or FleetState.TakingOff)
                return true;
            // A go-around was already cleared to land, so it was in the sequence before curfew.
            // Its rejoin restarts StateStartedAt, and a missed approach just before 23:00 used to
            // read as a new curfew arrival: a jet circled Adelaide until the 05:00 opening.
            return aircraft.State == FleetState.HoldingForLanding
                && (aircraft.WentAroundThisTrip || !AirportCurfew.IsClosed(aircraft.StateStartedAt, Clock));
        }

        /// <summary>How long a departure keeps its strip from the tower: lineup, roll, wake.</summary>
        private static long DepartureRunwaySeconds(FleetAircraft departure) =>
            AdelaideGround.LineupFor(departure.AssignedRunway, departure.Type).WholeSeconds
            + (long)Math.Round(AircraftPerformance.For(departure.Type).TakeoffRollExactSeconds)
            + WakeSeparationSeconds(departure.Type);

        /// <summary>The first moment at or after <paramref name="at"/> that is not in a storm hold.</summary>
        private static SimulationTime AfterStorms(SimulationTime at)
        {
            for (var i = 0; i < 48 && Weather.At(at) == WeatherKind.Storm; i++)
                at = Weather.NextBlock(at);
            return at;
        }

        private void SetStripFreeAt(bool mainStrip, SimulationTime at)
        {
            if (mainStrip)
                _mainRunwayFreeAt = at;
            else
                _crossRunwayFreeAt = at;
        }

        private bool ShouldGoAround(FleetAircraft aircraft, SimulationTime now, bool mainStrip)
        {
            if (aircraft.WentAroundThisTrip)
                return false;
            var arrivals = 0;
            foreach (var candidate in _fleet)
            {
                if (candidate.State != FleetState.HoldingForLanding)
                    continue;
                if (RunwayWeather.IsMainRunway(candidate.AssignedRunway) != mainStrip)
                    continue;
                arrivals++;
            }

            var seed = GoAroundSeed(aircraft.CompletedTrips, aircraft.Registration, now.ElapsedSeconds);
            return arrivals >= 2 && Math.Abs(seed % 11) == 0;
        }

        /// <summary>
        /// Wake-turbulence separation owed to whoever uses the runway next, derived from the
        /// leading aircraft's own wingspan (<see cref="AircraftCatalogue"/>) rather than a
        /// hand-picked list of type IDs — a second, disconnected classification that would
        /// silently give any newly added heavy jet only the smallest 90 s separation if its
        /// ID were never added here to match.
        /// </summary>
        public static long WakeSeparationSeconds(AircraftType type)
        {
            var wingspan = AircraftCatalogue.TryFor(type, out var spec) ? spec.WingspanMetres : 0;
            if (wingspan >= HeavyWakeWingspanMetres)
                return 180;
            return wingspan >= MediumWakeWingspanMetres ? 120 : RunwaySeparationSeconds;
        }

        private long LegAirborne(FleetAircraft aircraft) =>
            aircraft.CurrentDestination.HasValue ? AirborneSeconds(aircraft, aircraft.CurrentDestination.Value) : 0;

        /// <summary>
        /// The tower stores a missed approach as <see cref="FleetState.Landing"/> lasting only
        /// the approach. A subsequent real landing is longer, so <see cref="FleetAircraft.WentAroundThisTrip"/>
        /// can stay set (no second go-around) without trapping the aircraft in the circuit.
        /// </summary>
        public static bool IsMissedApproachLanding(FleetAircraft aircraft)
        {
            if (aircraft == null || !aircraft.StateEndsAt.HasValue)
                return false;
            var duration = aircraft.StateEndsAt.Value.ElapsedSeconds - aircraft.StateStartedAt.ElapsedSeconds;
            return duration <= AircraftPerformance.For(aircraft.Type).ApproachSeconds;
        }

        private void ScheduleAiDeparture(FleetAircraft aircraft, SimulationTime now)
        {
            if (aircraft.Type.IsRotorcraft)
            {
                ScheduleRescueMission(aircraft, now);
                return;
            }

            if (aircraft.Airline.Id.Value == "VOZ")
            {
                // Rotation, not a random draw, so the mainline timetable is stable.
                var code = VirginRotation[RotationIndex(aircraft, VirginRotation.Count)];
                if (DestinationCatalogue.TryFind(code, out var next) && CanReach(aircraft, next))
                    BookAiDeparture(aircraft, next, now);
                return;
            }

            if (aircraft.Airline.Id.Value == "QFA")
            {
                var code = QantasRotation[RotationIndex(aircraft, QantasRotation.Count)];
                if (DestinationCatalogue.TryFind(code, out var next) && CanReach(aircraft, next))
                    BookAiDeparture(aircraft, next, now);
                return;
            }

            if (aircraft.Airline.Id.Value == "JST")
            {
                var code = JetstarRotation[RotationIndex(aircraft, JetstarRotation.Count)];
                if (DestinationCatalogue.TryFind(code, out var next) && CanReach(aircraft, next))
                    BookAiDeparture(aircraft, next, now);
                return;
            }

            if (aircraft.Airline.Id.Value == "ANZ")
            {
                // Rotation, not a random draw, so international traffic is stable too.
                var code = AirNewZealandRotation[RotationIndex(aircraft, AirNewZealandRotation.Count)];
                if (DestinationCatalogue.TryFind(code, out var next) && CanReach(aircraft, next))
                    BookAiDeparture(aircraft, next, now);
                return;
            }
            var longHaulHome = LongHaulHomeOf(aircraft.Airline.Id.Value);
            if (longHaulHome != null)
            {
                if (DestinationCatalogue.TryFind(longHaulHome, out var next) && CanReach(aircraft, next))
                    BookAiDeparture(aircraft, next, now);
                return;
            }

            var total = 0;
            var candidates = new List<(Destination destination, int weight)>();
            foreach (var (code, weight) in AiNetworkFor(aircraft.Airline))
            {
                if (!DestinationCatalogue.TryFind(code, out var destination) || !CanReach(aircraft, destination))
                    continue;
                candidates.Add((destination, weight));
                total += weight;
            }

            if (total == 0)
                return;

            // One draw, as before, so a timeline stays reproducible from its seed.
            var roll = _random.NextInt(0, total);
            var pick = candidates[0].destination;
            foreach (var (destination, weight) in candidates)
            {
                if (roll < weight)
                {
                    pick = destination;
                    break;
                }
                roll -= weight;
            }

            BookAiDeparture(aircraft, pick, now);
        }

        private void BookAiDeparture(FleetAircraft aircraft, Destination destination, SimulationTime now)
        {
            var planned = AiDepartureWithinHours(now.Advance(AiTurnaroundSeconds(aircraft)), aircraft);
            var disruption = FlightDisruption.For(DisruptionKey(aircraft, destination), planned, Clock);
            // The published time is the timetable slot; a delay moves pushback, never the slot, so
            // the board reads "10:05 · Delayed +20" rather than a late time plus a second delay
            // (ADR 0137).
            var published = WholeMinute(PinLongHaulEvening(aircraft, SnapCommercialDeparture(aircraft, planned)));
            if (disruption.Cancelled)
            {
                aircraft.Scheduled = new ScheduledDeparture(destination, published, cancelled: true);
                return;
            }

            if (ShouldNightStopHere(aircraft, destination, published))
            {
                published = WholeMinute(FirstWaveAfter(aircraft, published));
                aircraft.Scheduled = new ScheduledDeparture(destination, published);
                return;
            }

            var departAt = published;
            if (disruption.Delayed)
            {
                departAt = WholeMinute(AiDepartureWithinHours(published.Advance(disruption.DelayMinutes * 60L), aircraft));
                // A delay that runs into the curfew becomes tomorrow's flight, not a 9-hour delay.
                if (departAt.ElapsedSeconds - published.ElapsedSeconds > (disruption.DelayMinutes + 1) * 60L)
                    published = departAt;
            }

            aircraft.Scheduled = new ScheduledDeparture(destination, departAt, publishedAt: published);
        }
    }
}

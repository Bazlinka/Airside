using System;
using System.Collections.Generic;
using Airside.Domain;

namespace Airside.Simulation
{
    /// <summary>Why an aircraft is waiting right now (ADR 0124). Derived, never saved.</summary>
    public enum HoldKind
    {
        /// <summary>Not waiting on anything — moving, timed, or free.</summary>
        None,
        Cancelled,
        /// <summary>Booked for later; the departure time has not come.</summary>
        NotYetDue,
        Curfew,
        /// <summary>Turnaround still running (fuel, catering, baggage, boarding).</summary>
        Turnaround,
        /// <summary>Two aircraft already taxiing out from this apron.</summary>
        ApronBusy,
        /// <summary>The gate lead-in is in use.</summary>
        LeadInBlocked,
        /// <summary>The route is not clear of other ground traffic.</summary>
        TaxiwayBlocked,
        /// <summary>An aircraft is landing or taking off on the strip.</summary>
        RunwayOccupied,
        /// <summary>The strip is clear but separation behind the last movement is still running.</summary>
        WakeSeparation,
        /// <summary>Storm: the tower is not clearing anything (ADR 0058).</summary>
        GroundStop,
        /// <summary>Another aircraft in the same queue goes first.</summary>
        Queued,
        /// <summary>Holding short while an arrival lands first.</summary>
        ArrivalFirst,
        /// <summary>An arrival holding while a long-held departure goes first.</summary>
        DepartureFirst,
        /// <summary>No stand is free for this type.</summary>
        NoStandFree,
        /// <summary>The player chooses the stand (or the tower parks it after 90 s).</summary>
        ChooseStand,
        /// <summary>Held airborne: curfew, or no stand to taxi to.</summary>
        HeldAirborne,
        /// <summary>Held for taxiing traffic crossing the runway, or a taxi held because its crossing is busy (ADR 0126).</summary>
        CrossingRunway,
        /// <summary>Routine maintenance check.</summary>
        InCheck
    }

    /// <summary>The reason plus whatever names it: the aircraft in the way, the runway, how long.</summary>
    public readonly struct HoldReason
    {
        public HoldReason(HoldKind kind, FleetAircraft blocker = null, RunwayDirection? runway = null,
            SimulationTime? until = null, int position = 0, string detail = null,
            IReadOnlyList<FleetAircraft> others = null)
        {
            Kind = kind;
            Blocker = blocker;
            Runway = runway;
            Until = until;
            Position = position;
            Detail = detail ?? string.Empty;
            Others = others ?? Array.Empty<FleetAircraft>();
        }

        public HoldKind Kind { get; }

        /// <summary>The aircraft being waited for, when one is known.</summary>
        public FleetAircraft Blocker { get; }
        public RunwayDirection? Runway { get; }

        /// <summary>When the hold is expected to lift, when known.</summary>
        public SimulationTime? Until { get; }

        /// <summary>Place in a queue (2 = one ahead), when queued.</summary>
        public int Position { get; }

        /// <summary>A short fact to show (the turnaround stage, the stand limit).</summary>
        public string Detail { get; }

        /// <summary>Further aircraft involved (the taxi-outs filling the apron).</summary>
        public IReadOnlyList<FleetAircraft> Others { get; }

        public bool IsHolding => Kind != HoldKind.None && Kind != HoldKind.NotYetDue;

        public static HoldReason Nothing => new(HoldKind.None);
    }

    public sealed partial class AirlineOperations
    {
        /// <summary>
        /// Why <paramref name="aircraft"/> is waiting (ADR 0124): the same checks, in the same
        /// order, as the pushback, stand and tower decisions, asked without changing anything. So
        /// the HUD can say "holding short 23 — Qantas 737-8 landing" instead of just "holding".
        /// </summary>
        public HoldReason Why(FleetAircraft aircraft)
        {
            if (aircraft == null)
                return HoldReason.Nothing;
            // The HUD asks several times a frame; the answer only changes when the simulation
            // steps or the aircraft changes state, so it is kept for the current step.
            var key = (aircraft.Registration, _processedTo.ElapsedSeconds, aircraft.State, CareerState?.Funds ?? 0);
            if (_holdReasons.TryGetValue(aircraft.Registration, out var cached) && cached.Key.Equals(key))
                return cached.Reason;
            var reason = WhyNow(aircraft);
            _holdReasons[aircraft.Registration] = (key, reason);
            return reason;
        }

        private readonly Dictionary<string, ((string, long, FleetState, long) Key, HoldReason Reason)> _holdReasons =
            new(StringComparer.Ordinal);

        private HoldReason WhyNow(FleetAircraft aircraft)
        {
            var now = _processedTo;
            switch (aircraft.State)
            {
                case FleetState.AtStand:
                    return WhyAtStand(aircraft, now);
                case FleetState.HoldingShort:
                    return WhyOnRunwayQueue(aircraft, now, departure: true);
                case FleetState.HoldingForLanding:
                    return WhyOnRunwayQueue(aircraft, now, departure: false);
                case FleetState.AwaitingStand:
                    return WhyAwaitingStand(aircraft, now);
                case FleetState.Inbound when aircraft.StateEndsAt.HasValue
                                             && aircraft.StateEndsAt.Value.CompareTo(now) > 0:
                    if (Weather.At(now) == WeatherKind.Storm)
                        return new HoldReason(HoldKind.GroundStop, until: aircraft.StateEndsAt, detail: "storm");
                    if (!ExemptFromCurfew(aircraft) && AirportCurfew.IsClosed(now, Clock))
                        return new HoldReason(HoldKind.HeldAirborne, until: aircraft.StateEndsAt, detail: "curfew");
                    return HoldReason.Nothing;
                default:
                    return HoldReason.Nothing;
            }
        }

        private HoldReason WhyAtStand(FleetAircraft aircraft, SimulationTime now)
        {
            if (Maintenance.InCheck(aircraft, now))
                return new HoldReason(HoldKind.InCheck, until: aircraft.CheckUntil);
            if (aircraft.Scheduled is { Cancelled: true })
                return new HoldReason(HoldKind.Cancelled);
            if (!aircraft.Scheduled.HasValue)
                return HoldReason.Nothing;
            var booked = aircraft.Scheduled.Value;
            if (booked.DepartAt.CompareTo(now) > 0)
            {
                if (aircraft.Airline.IsPlayer && !DeparturePrep.IsReady(aircraft, now, CareerState.BaseLevel))
                    return HoldReason.Nothing;
                return new HoldReason(HoldKind.NotYetDue, until: booked.DepartAt);
            }
            if (!ExemptFromCurfew(aircraft) && AirportCurfew.IsClosed(now, Clock))
                return new HoldReason(HoldKind.Curfew, until: AirportCurfew.OpensAt(now, Clock));
            if (aircraft.Airline.IsPlayer && !DeparturePrep.IsReady(aircraft, now, CareerState.BaseLevel))
                return new HoldReason(HoldKind.Turnaround,
                    detail: DeparturePrep.For(aircraft, now, CareerState.BaseLevel).Label);

            var gate = AdelaideGround.IsTerminalGate(aircraft.Stand);
            var release = NextTaxiReleaseAt(now, gate);
            if (release.HasValue)
            {
                var rolling = new List<FleetAircraft>();
                foreach (var other in _fleet)
                    if (other.State == FleetState.TaxiOut && AdelaideGround.IsTerminalGate(other.DepartureStand) == gate)
                        rolling.Add(other);
                return new HoldReason(HoldKind.ApronBusy, rolling.Count > 0 ? rolling[0] : null,
                    until: release, others: rolling);
            }
            if (gate && !IsLeadInFree(aircraft.Stand, aircraft))
                return new HoldReason(HoldKind.LeadInBlocked,
                    GroundResourceHolder(AdelaideGround.LeadInResource(aircraft.Stand)));

            var runway = RunwayFor(aircraft);
            var readyAt = DepartureReadyAt(aircraft);
            if (!GroundTraffic.PathClear(_fleet, aircraft, AdelaideGround.TaxiOut(aircraft.Stand, aircraft.Type, runway),
                    runway, taxiOut: true, now,
                    includeStationary: now.ElapsedSeconds - readyAt.ElapsedSeconds < GroundTraffic.MaxWaitSeconds,
                    out var blocker))
                return new HoldReason(HoldKind.TaxiwayBlocked, blocker, runway);
            var busy = CrossingIntoBusyStrip(AdelaideGround.TaxiOut(aircraft.Stand, aircraft.Type, runway), runway, now);
            if (busy.HasValue)
                return new HoldReason(HoldKind.CrossingRunway, runway: runway,
                    detail: busy.Value.MainStrip ? "05/23" : "12/30");
            // Everything is clear: it pushes on the next ground-control tick.
            return HoldReason.Nothing;
        }

        private HoldReason WhyAwaitingStand(FleetAircraft aircraft, SimulationTime now)
        {
            var chosen = SuggestStand(aircraft);
            if (chosen == null)
                return new HoldReason(HoldKind.NoStandFree, detail: NoStandDetail(aircraft));
            if (aircraft.Airline.IsPlayer
                && now.ElapsedSeconds - aircraft.StateStartedAt.ElapsedSeconds < PlayerStandAutoSeconds)
                return new HoldReason(HoldKind.ChooseStand,
                    until: aircraft.StateStartedAt.Advance(PlayerStandAutoSeconds));
            if (!GroundTraffic.PathClear(_fleet, aircraft,
                    AdelaideGround.TaxiIn(chosen.Value, aircraft.Type, aircraft.AssignedRunway),
                    aircraft.AssignedRunway, taxiOut: false, now,
                    includeStationary: now.ElapsedSeconds - aircraft.StateStartedAt.ElapsedSeconds < GroundTraffic.MaxWaitSeconds,
                    out var blocker))
                return new HoldReason(HoldKind.TaxiwayBlocked, blocker, aircraft.AssignedRunway);
            var busy = CrossingIntoBusyStrip(AdelaideGround.TaxiIn(chosen.Value, aircraft.Type, aircraft.AssignedRunway),
                aircraft.AssignedRunway, now);
            if (busy.HasValue)
                return new HoldReason(HoldKind.CrossingRunway, runway: aircraft.AssignedRunway,
                    detail: busy.Value.MainStrip ? "05/23" : "12/30");
            return HoldReason.Nothing;
        }

        private string NoStandDetail(FleetAircraft aircraft)
        {
            if (aircraft.Airline.IsPlayer && CareerState != null)
            {
                var anyFree = false;
                foreach (var _ in FreeStandsFor(aircraft.Type))
                {
                    anyFree = true;
                    break;
                }
                if (anyFree)
                    return $"{CareerState.Base.Title} has no free stand for it";
            }
            return NeedsTerminalGate(aircraft.Type) ? "every gate is in use" : "every regional bay is in use";
        }

        private HoldReason WhyOnRunwayQueue(FleetAircraft aircraft, SimulationTime now, bool departure)
        {
            var runway = aircraft.AssignedRunway;
            var main = RunwayWeather.IsMainRunway(runway);
            // Departures stay on the ground stop. An arrival already on final is cleared
            // through the storm (ADR 0190), so its card names whatever else is in the way.
            if (departure && Weather.At(now) == WeatherKind.Storm)
                return new HoldReason(HoldKind.GroundStop, runway: runway);
            if (!departure && aircraft.StateEndsAt.HasValue && aircraft.StateEndsAt.Value.CompareTo(now) > 0)
                return new HoldReason(HoldKind.HeldAirborne, runway: runway, until: aircraft.StateEndsAt, detail: "curfew");

            var freeAt = main ? _mainRunwayFreeAt : _crossRunwayFreeAt;
            if (freeAt.CompareTo(now) > 0)
            {
                FleetAircraft occupier = null;
                FleetAircraft latest = null;
                foreach (var other in _fleet)
                {
                    if (other.State is not (FleetState.TakingOff or FleetState.Landing)
                        || RunwayWeather.IsMainRunway(other.AssignedRunway) != main)
                        continue;
                    if (IsOccupyingRunway(other))
                        occupier ??= other;
                    if (latest == null || other.StateStartedAt.CompareTo(latest.StateStartedAt) > 0)
                        latest = other;
                }
                return occupier != null
                    ? new HoldReason(HoldKind.RunwayOccupied, occupier, runway, freeAt)
                    : new HoldReason(HoldKind.WakeSeparation, latest, runway, freeAt);
            }

            var sameState = departure ? FleetState.HoldingShort : FleetState.HoldingForLanding;
            var first = LongestWaiting(sameState, main);
            if (first != null && !ReferenceEquals(first, aircraft))
            {
                var position = 1;
                foreach (var other in _fleet)
                    if (other.State == sameState && RunwayWeather.IsMainRunway(other.AssignedRunway) == main
                        && other.StateStartedAt.CompareTo(aircraft.StateStartedAt) < 0)
                        position++;
                return new HoldReason(HoldKind.Queued, first, runway, position: position);
            }

            var arrival = LongestWaiting(FleetState.HoldingForLanding, main);
            var holder = LongestWaiting(FleetState.HoldingShort, main);
            if (departure && arrival != null)
            {
                var releasedFirst = now.ElapsedSeconds - aircraft.StateStartedAt.ElapsedSeconds >= DepartureMaxHoldSeconds
                                    && aircraft.StateStartedAt.CompareTo(arrival.StateStartedAt) < 0;
                if (!releasedFirst && !VacateCrossesHolder(arrival, main))
                    return new HoldReason(HoldKind.ArrivalFirst, arrival, runway);
            }
            if (!departure && holder != null)
            {
                var releasedFirst = now.ElapsedSeconds - holder.StateStartedAt.ElapsedSeconds >= DepartureMaxHoldSeconds
                                    && holder.StateStartedAt.CompareTo(aircraft.StateStartedAt) < 0;
                if (releasedFirst || VacateCrossesHolder(aircraft, main))
                    return new HoldReason(HoldKind.DepartureFirst, holder, runway);
            }
            if (!departure && !VacateClearOfTaxiing(aircraft, now))
                return new HoldReason(HoldKind.TaxiwayBlocked, runway: runway, detail: "the runway exit");
            var crossing = CrossingDue(main, now, now.Advance(RunwayBusySeconds(aircraft, landing: !departure)));
            if (crossing != null)
                return new HoldReason(HoldKind.CrossingRunway, crossing, runway);
            // Clear to go on the next tower tick.
            return HoldReason.Nothing;
        }
    }
}

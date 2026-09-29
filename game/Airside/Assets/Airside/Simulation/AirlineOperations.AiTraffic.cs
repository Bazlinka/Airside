using System;
using System.Collections.Generic;
using Airside.Domain;

namespace Airside.Simulation
{
    public sealed partial class AirlineOperations
    {
        /// <summary>
        /// Short opening arrival bank so most authored metal stays on stands (ADR 0100).
        /// Morning: ~7 inbound over ~40 minutes. Evening: stretch those onto the remaining
        /// time before 22:50. Curfew: no commercial opening peak.
        /// </summary>
        private void SeedOpeningTraffic(List<FleetAircraft> regionalFleet, List<FleetAircraft> terminalFleet)
        {
            var local = Clock.LocalAt(_processedTo);
            if (AirportCurfew.IsClosed(local))
                return;

            var evening = local.Hour >= 19;
            // Keep a readable short-final stream; leave the rest of T1 / the bays parked.
            TrySeedOpeningInbound(regionalFleet, "QLK", "PLO", FitOpeningSeconds(local, 3 * 60, evening));
            TrySeedOpeningInbound(regionalFleet, "REX", "MGB", FitOpeningSeconds(local, 8 * 60, evening));
            TrySeedOpeningInbound(terminalFleet, "VOZ", "MEL", FitOpeningSeconds(local, 12 * 60, evening));
            TrySeedOpeningInbound(terminalFleet, "QFA", "SYD", FitOpeningSeconds(local, 18 * 60, evening));
            TrySeedOpeningInbound(terminalFleet, "JST", "MEL", FitOpeningSeconds(local, 24 * 60, evening));
            TrySeedOpeningInbound(terminalFleet, "ANZ", "AKL", FitOpeningSeconds(local, 30 * 60, evening));
            TrySeedOpeningInbound(terminalFleet, "SIA", "SIN", FitOpeningSeconds(local, 40 * 60, evening));

            if (evening)
                return;

            var departureIndex = 0;
            foreach (var aircraft in _fleet)
            {
                if (departureIndex >= AiOpeningDepartureSeconds.Length)
                    break;
                if (aircraft.Airline.IsPlayer || aircraft.Airline.IsEmergency
                    || aircraft.State != FleetState.AtStand || aircraft.Scheduled is not { } first)
                    continue;
                aircraft.Scheduled = new ScheduledDeparture(first.Destination,
                    OpeningDepartureAt(AiOpeningDepartureSeconds[departureIndex++]));
            }
        }

        /// <summary>
        /// An opening pushback published on the next 5-minute clock mark at or after the
        /// ladder offset, so the board reads 07:05 / 07:05 / 07:10 like a real departures
        /// screen rather than launch-relative odd minutes (ADR 0110).
        /// </summary>
        private SimulationTime OpeningDepartureAt(long offsetSeconds)
        {
            var at = _processedTo.Advance(offsetSeconds);
            var local = Clock.LocalAt(at);
            var minutes = local.Hour * 60 + local.Minute + (local.Second > 0 || local.Millisecond > 0 ? 1 : 0);
            var mark = (minutes + 4) / 5 * 5;
            var published = Clock.AtLocal(local.Date.AddMinutes(mark));
            return published.CompareTo(at) >= 0 ? published : at;
        }

        private long FitOpeningSeconds(DateTime local, long nominalSeconds, bool evening)
        {
            if (!evening)
                return nominalSeconds;
            var last = Clock.AtLocal(local.Date.AddMinutes(LastOpeningArrivalMinute));
            var remaining = last.ElapsedSeconds - _processedTo.ElapsedSeconds;
            if (remaining < 8 * 60)
                return -1;
            const long nominalLast = 40 * 60;
            const long nominalFirst = 3 * 60;
            var span = Math.Max(1, remaining - nominalFirst);
            var t = (nominalSeconds - nominalFirst) / (double)(nominalLast - nominalFirst);
            return nominalFirst + (long)Math.Round(t * span);
        }

        private void TrySeedOpeningInbound(List<FleetAircraft> fleet, string airlineId, string destinationCode,
            long secondsToCircuit)
        {
            if (secondsToCircuit < 0 || fleet == null
                || !DestinationCatalogue.TryFind(destinationCode, out var destination))
                return;
            for (var i = fleet.Count - 1; i >= 0; i--)
            {
                var aircraft = fleet[i];
                if (aircraft.Airline.Id.Value != airlineId || aircraft.State != FleetState.AtStand)
                    continue;
                SeedOpeningInbound(aircraft, destination, secondsToCircuit);
                return;
            }
        }

        private void SeedOpeningInbound(FleetAircraft aircraft, Destination destination, long secondsToCircuit)
        {
            aircraft.DepartureStand = aircraft.Stand;
            aircraft.Stand = default;
            aircraft.Scheduled = null;
            aircraft.CurrentDestination = destination;
            aircraft.Restore(FleetState.Inbound, _processedTo, _processedTo.Advance(secondsToCircuit));
        }

        /// <summary>
        /// Add any <see cref="RegionalCarriers"/> aircraft that is not flying here yet, parking
        /// each on a free stand (skipped if none is free) with an AI departure booked. An
        /// airline is added only when at least one of its aircraft can park, and a later load
        /// still fills remaining registrations — same idea as
        /// <see cref="AddMissingTerminalOperators"/>. Returns how many aircraft joined.
        /// </summary>
        public int AddMissingRegionalCarriers(List<FleetAircraft> added = null)
        {
            var count = 0;
            foreach (var (make, fleet) in RegionalCarriers)
            {
                var template = make();
                var airline = _airlines.Find(a => a.Id.Equals(template.Id));
                foreach (var (registration, type) in fleet)
                {
                    if (_fleet.Exists(a => string.Equals(a.Registration, registration, StringComparison.OrdinalIgnoreCase)))
                        continue;
                    var stand = SuggestStandFor(type);
                    if (!stand.HasValue && !HasStandFor(type))
                        break;
                    if (airline == null)
                    {
                        airline = template;
                        AddAirline(airline);
                    }

                    var aircraft = stand.HasValue
                        ? AddAircraft(airline, registration, type, stand.Value)
                        : AddAircraftAway(airline, registration, type);
                    added?.Add(aircraft);
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// Add the RFDS aircraft if this field still has a free regional bay. Safe on every load.
        /// </summary>
        public int AddMissingEmergencyOperators(List<FleetAircraft> added = null)
        {
            var count = 0;
            foreach (var (make, fleet) in EmergencyOperators)
            {
                var template = make();
                var airline = _airlines.Find(a => a.Id.Equals(template.Id));
                foreach (var (registration, type) in fleet)
                {
                    if (_fleet.Exists(a => string.Equals(a.Registration, registration, StringComparison.OrdinalIgnoreCase)))
                        continue;
                    var stand = SuggestStandFor(type);
                    if (!stand.HasValue)
                        break;
                    if (airline == null)
                    {
                        airline = template;
                        AddAirline(airline);
                    }

                    var aircraft = AddAircraft(airline, registration, type, stand.Value);
                    added?.Add(aircraft);
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// Add any <see cref="TerminalOperators"/> airline and aircraft missing here, parked at its
        /// own gate with its first rotation flight booked. Skipped when this airport has no such
        /// gate or it is taken. Returns how many aircraft joined; safe to call on every load.
        /// </summary>
        /// <param name="at">The time to evaluate seasonal operators (e.g. Cathay) against.
        /// Defaults to <see cref="_processedTo"/> for the load/startup call sites, which are
        /// evaluating "now". <see cref="Update"/> must pass its own target time explicitly —
        /// it calls this before advancing <see cref="_processedTo"/>, so the default would
        /// check the time being advanced *from*, not the time being advanced *to*.</param>
        public int AddMissingTerminalOperators(List<FleetAircraft> added = null, SimulationTime? at = null)
        {
            var asOf = at ?? _processedTo;
            var count = 0;
            // Pass 0 parks the aircraft that own a gate; pass 1 the extra frames, which take
            // only a spare gate their size (never a widebody's code E gate) or start away.
            for (var pass = 0; pass < 2; pass++)
            foreach (var (make, fleet) in TerminalOperators)
            {
                var template = make();
                if (template.Id.Value == "CPA" && !IsCathaySeason(asOf))
                    continue;
                var airline = _airlines.Find(a => a.Id.Equals(template.Id));
                foreach (var (registration, type, gate) in fleet)
                {
                    if (string.IsNullOrEmpty(gate.Value) != (pass == 1))
                        continue;
                    if (_fleet.Exists(a => string.Equals(a.Registration, registration, StringComparison.OrdinalIgnoreCase)))
                        continue;
                    StableId? stand = gate;
                    if (string.IsNullOrEmpty(gate.Value) || !_stands.Contains(gate) || !IsStandFree(gate)
                        || !StandFits(type, gate))
                    {
                        // Seasonal Cathay used to vanish for the whole summer when GATE-18
                        // (or its pier sibling) was taken. Fall back to any free fitting gate;
                        // with none, the aircraft is away and flies in (ADR 0111).
                        stand = SuggestStandFor(type);
                        if (pass == 1 && stand.HasValue && IsOversized(type, stand.Value))
                            stand = null;
                    }

                    // An airport with no stand this type could ever use does not get it at all.
                    if (!stand.HasValue && !HasStandFor(type))
                        continue;

                    if (airline == null)
                    {
                        airline = template;
                        AddAirline(airline);
                    }

                    var aircraft = stand.HasValue
                        ? AddAircraft(airline, registration, type, stand.Value)
                        : AddAircraftAway(airline, registration, type);
                    added?.Add(aircraft);
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// Cathay's summer service leaves when the season ends. A parked A350 keeps its
        /// gate until its booked departure, flies home, and is removed while away (off the
        /// map) so GATE-18 is not held through winter and no aircraft vanishes from a stand.
        /// </summary>
        public int RetireOutOfSeasonOperators(SimulationTime? at = null)
        {
            var asOf = at ?? _processedTo;
            if (IsCathaySeason(asOf))
                return 0;

            var removed = 0;
            for (var i = _fleet.Count - 1; i >= 0; i--)
            {
                var aircraft = _fleet[i];
                if (aircraft.Airline.Id.Value != "CPA")
                    continue;
                // The apron keeps its aircraft until they depart (ADR 0110): a parked A350
                // flies its last rotation home and is retired off-map, never lifted off GATE-18.
                // Only a parked jet with nothing booked (nothing left to fly) is removed here.
                var offMap = aircraft.State is FleetState.AtDestination or FleetState.Inbound;
                var idleOnStand = aircraft.State == FleetState.AtStand && !aircraft.Scheduled.HasValue;
                if (!offMap && !idleOnStand)
                    continue;
                aircraft.Scheduled = null;
                aircraft.PrepStartedAt = null;
                _fleet.RemoveAt(i);
                removed++;
            }

            if (removed > 0 && !_fleet.Exists(a => a.Airline.Id.Value == "CPA"))
            {
                for (var i = _airlines.Count - 1; i >= 0; i--)
                    if (_airlines[i].Id.Value == "CPA")
                        _airlines.RemoveAt(i);
            }

            return removed;
        }

        /// <summary>Cathay's Adelaide service operates through the southern summer season.</summary>
        public bool IsCathaySeason(SimulationTime at)
        {
            var local = Clock.LocalAt(at);
            return local.Month == 11 && local.Day >= 10
                   || local.Month == 12
                   || local.Month <= 2
                   || local.Month == 3 && local.Day <= 27;
        }

        /// <summary>
        /// Add an AI aircraft that has no free stand here: it is at its outstation and flies
        /// in on a real arrival — soon if the field is open, otherwise with tomorrow's
        /// 06:00–08:30 morning arrivals — so the apron never holds more than it has room
        /// for (ADR 0111). Draws no random numbers.
        /// </summary>
        private FleetAircraft AddAircraftAway(Airline airline, string registration, AircraftType type)
        {
            if (!_airlines.Contains(airline))
                throw new InvalidOperationException("Add the airline before its aircraft.");
            var aircraft = new FleetAircraft(registration, airline, type, default, _processedTo);
            aircraft.Owner = this;
            aircraft.CurrentDestination = AwayBaseFor(airline, type);
            var key = FirstWaveKey(aircraft);
            var local = Clock.LocalAt(_processedTo);
            // After the ADR 0100 opening bank (first 45 min), so the start is not a pile-up.
            var soon = _processedTo.Advance((50 + key % 120) * 60L);
            var landsAt = AirportCurfew.IsClosed(Clock.LocalAt(soon)) || AirportCurfew.IsClosed(local)
                ? MorningArrivalAt(aircraft, _processedTo)
                : soon;
            aircraft.Restore(FleetState.Inbound, _processedTo, landsAt);
            _fleet.Add(aircraft);
            return aircraft;
        }

        /// <summary>The airline's busiest reachable city — where a spare frame night-stops.</summary>
        private Destination AwayBaseFor(Airline airline, AircraftType type)
        {
            var longHaul = LongHaulHomeOf(airline.Id.Value);
            if (longHaul != null && DestinationCatalogue.TryFind(longHaul, out var home))
                return home;
            Destination? best = null;
            var bestWeight = -1;
            foreach (var (code, weight) in AiNetworkFor(airline))
            {
                if (!DestinationCatalogue.TryFind(code, out var destination) || destination.Equals(Home))
                    continue;
                if (!type.CanReach(DistanceKm(destination)) || weight <= bestWeight)
                    continue;
                best = destination;
                bestWeight = weight;
            }

            return best ?? DeliveryOrigin(type);
        }

        /// <summary>
        /// Next morning arrival for an aircraft kept out overnight: 06:00–08:30, spread by
        /// registration, never earlier than the 05:00 opening (ADR 0111). Replaces landing
        /// every overnight inbound at 05:00 exactly.
        /// </summary>
        internal SimulationTime MorningArrivalAt(FleetAircraft aircraft, SimulationTime now)
        {
            var local = Clock.LocalAt(now);
            var day = local.Hour < AirportCurfew.OpensAtHour ? local.Date : local.Date.AddDays(1);
            var at = Clock.AtLocal(day.AddHours(6).AddMinutes(FirstWaveKey(aircraft) % 150));
            return at.CompareTo(now) > 0 ? at : now;
        }

        public long AirborneSeconds(FleetAircraft aircraft, Destination destination) =>
            LegTiming.AirborneSeconds(DistanceKm(destination), aircraft.Type);

        private void SeedCareerAnnouncements()
        {
            if (_announcedTier.HasValue) return;
            _announcedTier = CareerState.Tier;
            foreach (var goal in CareerGoals())
                if (goal.Complete && goal.Stage <= CareerState.Tier)
                    _announcedGoals.Add(goal.Id);
            foreach (var milestone in CareerMilestones.Reached(CareerState, PlayerFleetCount(), PlayerOwnedTypes()))
                if (milestone.Reached)
                    _announcedMilestones.Add(milestone.Id);
        }

        private void AddDeliveryInbound(Airline airline, string registration, AircraftType type)
        {
            if (_fleet.Exists(a => string.Equals(a.Registration, registration, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException($"{registration} is already registered.");

            var from = DeliveryOrigin(type);
            var aircraft = new FleetAircraft(registration, airline, type, default, _processedTo);
            aircraft.Owner = this;
            aircraft.CurrentDestination = from;
            aircraft.Restore(FleetState.Inbound, _processedTo, _processedTo.Advance(8 * 60));
            _fleet.Add(aircraft);
        }

        private Destination DeliveryOrigin(AircraftType type)
        {
            foreach (var destination in DestinationCatalogue.Australia)
            {
                if (destination.Equals(Home))
                    continue;
                if (type.CanReach(DistanceKm(destination)) && RouteAccess.Allows(type, destination))
                    return destination;
            }

            DestinationCatalogue.TryFind("KGC", out var kingscote);
            return kingscote;
        }

        /// <summary>
        /// Deterministic per-aircraft, per-hour go-around seed. Every registration in the
        /// fleet follows the same "VH-XXX" format (<see cref="StableRegistrationHash"/>
        /// exists because of this): a registration's *length* is therefore the same for
        /// every aircraft in the game and contributed nothing to this seed, so any two
        /// aircraft with the same completed-trip count in the same hour (e.g. sister ships
        /// fresh out of the gate) went around, or didn't, in lockstep forever. Hashing the
        /// whole registration instead of just its length gives each aircraft its own draw.
        /// </summary>
        internal static int GoAroundSeed(int completedTrips, string registration, long nowSeconds) =>
            unchecked(completedTrips * 17 + StableRegistrationHash(registration) + (int)(nowSeconds / 3600));

        public static IReadOnlyList<(string Code, int Weight)> AiNetworkFor(Airline airline) => airline.Id.Value switch
        {
            "REX" => RexNetwork,
            "QLK" => QantasLinkNetwork,
            "VOZ" => VirginNetwork,
            "QFA" => QantasNetwork,
            "JST" => JetstarNetwork,
            "ANZ" => AirNewZealandNetwork,
            "SIA" => SingaporeNetwork,
            "CPA" => CathayNetwork,
            "MAS" => MalaysiaNetwork,
            "UAE" => EmiratesNetwork,
            "QTR" => QatarNetwork,
            "FJI" => FijiNetwork,
            "RFDS" => RfdsNetwork,
            _ => AiNetwork
        };

        /// <summary>Single-city international home for operators that only fly one Adelaide route.</summary>
        public static string LongHaulHomeOf(string airlineId) => airlineId switch
        {
            "SIA" => "SIN",
            "CPA" => "HKG",
            "MAS" => "KUL",
            "UAE" => "DXB",
            "QTR" => "DOH",
            "FJI" => "NAN",
            _ => null
        };

        /// <summary>
        /// Where this airframe is in its airline's rotation. The registration offsets the start
        /// so six Qantas 737s do not all fly the same first city (ADR 0111).
        /// </summary>
        private static int RotationIndex(FleetAircraft aircraft, int count) =>
            (aircraft.CompletedTrips + FirstWaveKey(aircraft) % count) % count;

        /// <summary>
        /// The key a flight's disruption is drawn from. The day plan uses the same one, so the plan's
        /// "Delayed +N" is the delay the aircraft really flies (ADR 0137).
        /// </summary>
        internal static string DisruptionKey(FleetAircraft aircraft, Destination destination) =>
            $"{aircraft.Registration}:{aircraft.CompletedTrips}:{destination.Code}";

        /// <summary>Rounds up to the next whole minute, so HH:mm and "+N min" always agree (ADR 0137).</summary>
        public static SimulationTime WholeMinute(SimulationTime at)
        {
            var rest = at.ElapsedSeconds % 60;
            return rest == 0 ? at : new SimulationTime(at.ElapsedSeconds + 60 - rest);
        }

        /// <summary>
        /// Most Adelaide-based domestic and regional frames stay the night on the apron
        /// rather than fly an evening (18:00+) hop they cannot get back from before 23:00 — that is
        /// what fills the 05:00 first wave (ADR 0111). One in three still flies out and
        /// night-stops at the other end, so the late board is not empty. Long-haul,
        /// player and RFDS are never held.
        /// </summary>
        private bool ShouldNightStopHere(FleetAircraft aircraft, Destination destination, SimulationTime departAt)
        {
            if (ExemptFromCurfew(aircraft) || LongHaulHomeOf(aircraft.Airline.Id.Value) != null
                || FirstWaveKey(aircraft) % 3 == 0)
                return false;
            var local = Clock.LocalAt(departAt);
            if (local.Hour < 18 || AirportCurfew.IsClosed(local))
                return false;
            // Only short domestic / regional hops: a trans-Tasman or Bali leg is a
            // different operation, not an Adelaide-based evening shuttle.
            var leg = AirborneSeconds(aircraft, destination);
            if (leg > 150 * 60)
                return false;
            var backAt = departAt.Advance(2 * leg + DestinationTurnaroundSeconds + 15 * 60);
            var closes = Clock.AtLocal(local.Date.AddMinutes(AirportCurfew.LastMovementMinute));
            return backAt.CompareTo(closes) > 0;
        }

        private SimulationTime FirstWaveAfter(FleetAircraft aircraft, SimulationTime departAt)
        {
            var local = Clock.LocalAt(departAt);
            var day = local.Hour < AirportCurfew.OpensAtHour ? local.Date : local.Date.AddDays(1);
            var at = Clock.AtLocal(AdelaideHourProfile.FirstWaveLocal(day, FirstWaveKey(aircraft)));
            return at.CompareTo(departAt) > 0 ? at : departAt;
        }

        /// <summary>
        /// Cluster commercial AI onto 5-minute ADL bank marks so turns that finish a
        /// minute apart can share a departure time (ADR 0100). Player and RFDS untouched.
        /// </summary>
        private SimulationTime SnapCommercialDeparture(FleetAircraft aircraft, SimulationTime departAt)
        {
            if (aircraft == null || aircraft.Airline.IsPlayer || aircraft.Airline.IsEmergency)
                return departAt;
            var local = Clock.LocalAt(departAt);
            var snapped = AdelaideHourProfile.SnapToBankLocal(local, AiFirstDepartureHour, AiLastDepartureHour,
                FirstWaveKey(aircraft));
            var at = Clock.AtLocal(snapped);
            return at.CompareTo(departAt) > 0 ? at : departAt;
        }

        /// <summary>
        /// Repeatable turnarounds sized like a real Adelaide gate dwell, not a
        /// game-speed hop. Domestics sit 28–44 minutes (turboprop) or 40–58
        /// (narrowbody); widebodies 55–89 like a T1 international turn. Short
        /// enough that the field stays busy across a soak day; long enough that
        /// "due to depart" means a real turnaround, not a ten-minute flip.
        /// </summary>
        private static long AiTurnaroundSeconds(FleetAircraft aircraft)
        {
            unchecked
            {
                var hash = 17;
                foreach (var ch in aircraft.Registration)
                    hash = hash * 31 + ch;
                hash = hash * 31 + aircraft.CompletedTrips;
                var wide = AircraftCatalogue.IsWidebody(aircraft.Type);
                var jet = NeedsTerminalGate(aircraft.Type);
                var minutes = wide ? 55 + Math.Abs(hash % 35)
                    : jet ? 40 + Math.Abs(hash % 19)
                    : 28 + Math.Abs(hash % 17);
                return minutes * 60L;
            }
        }

        /// <summary>
        /// Regional ready-times jump the afternoon hole onto the next bank.
        /// Jets keep the 05:00–23:00 window — a 787 ready at 21:40 still goes.
        /// Player and RFDS skip the window entirely.
        /// </summary>
        internal SimulationTime AiDepartureWithinHours(SimulationTime readyAt, FleetAircraft aircraft)
        {
            if (aircraft != null && aircraft.Airline.IsEmergency)
            {
                var local = Clock.LocalAt(readyAt);
                if (AirportCurfew.IsClosed(local))
                    return readyAt;
                var tonight = local.Date.AddHours(23).AddMinutes(30);
                if (local >= tonight)
                    tonight = tonight.AddDays(1);
                var at = Clock.AtLocal(tonight);
                return at.CompareTo(readyAt) > 0 ? at : readyAt;
            }

            if (aircraft != null && ExemptFromCurfew(aircraft))
                return readyAt;
            return PinLongHaulEvening(aircraft,
                AiDepartureWithinHours(readyAt, aircraft?.Type, aircraft == null ? 0 : FirstWaveKey(aircraft)));
        }

        /// <summary>
        /// Stable per-airframe key that spreads night-stopped aircraft across the 05:00–06:30
        /// first wave instead of stacking every one on 05:00 (ADR 0110).
        /// </summary>
        internal static int FirstWaveKey(FleetAircraft aircraft)
        {
            unchecked
            {
                var hash = 23;
                foreach (var ch in aircraft.Registration)
                    hash = hash * 31 + ch;
                return hash & int.MaxValue;
            }
        }

        /// <summary>
        /// Time on the ground at the far end. Emirates and Qatar wait at home long enough to land
        /// back at 20:30 Adelaide time: their ~27 h round trip otherwise brought them back just
        /// after the 23:00 curfew every night, held off-map until a 07:15 landing, so the real
        /// evening departure (<see cref="PinLongHaulEvening"/>) never happened.
        /// </summary>
        private long AwayTurnaroundSeconds(FleetAircraft aircraft, SimulationTime now)
        {
            var turn = (long)DestinationTurnaroundSeconds;
            var id = aircraft.Airline.Id.Value;
            if (id != "UAE" && id != "QTR")
                return turn;
            var landsAt = now.Advance(turn + LegAirborne(aircraft));
            var lands = Clock.LocalAt(landsAt);
            var target = lands.Date.AddMinutes(EveningLongHaulArrivalMinute);
            if (lands > target)
                target = target.AddDays(1);
            return turn + Math.Max(0L, Clock.AtLocal(target).ElapsedSeconds - landsAt.ElapsedSeconds);
        }

        /// <summary>
        /// Qatar and Emirates keep the real ~22:00 Adelaide departure when they
        /// are already in the evening window. A morning-ready jet still turns
        /// normally so it does not occupy a gate until night.
        /// </summary>
        private SimulationTime PinLongHaulEvening(FleetAircraft aircraft, SimulationTime departAt)
        {
            if (aircraft == null)
                return departAt;
            var id = aircraft.Airline.Id.Value;
            if (id != "UAE" && id != "QTR")
                return departAt;
            var local = Clock.LocalAt(departAt);
            if (local.Hour < 18 || local.Hour >= 22 || AirportCurfew.IsClosed(local))
                return departAt;
            var at = Clock.AtLocal(local.Date.AddHours(22));
            return at.CompareTo(departAt) > 0 ? at : departAt;
        }

        internal SimulationTime AiDepartureWithinHours(SimulationTime readyAt, AircraftType type = null,
            int firstWaveKey = 0)
        {
            var local = Clock.LocalAt(readyAt);
            DateTime useful;
            if (type != null && NeedsTerminalGate(type))
            {
                if (!AirportCurfew.IsClosed(local))
                    return readyAt;
                // Night-stop: the jet stays on its gate and leads tomorrow's first wave.
                var day = local.Hour < AirportCurfew.OpensAtHour ? local.Date : local.Date.AddDays(1);
                useful = AdelaideHourProfile.FirstWaveLocal(day, firstWaveKey);
            }
            else
            {
                useful = AdelaideHourProfile.NextUsefulLocal(local, AiFirstDepartureHour, AiLastDepartureHour);
                if (useful == local)
                    return readyAt;
                // Rolled past the evening: night-stop on the bay, out with the first wave.
                if (useful.Date != local.Date || local.Hour < AiFirstDepartureHour)
                    useful = AdelaideHourProfile.FirstWaveLocal(useful.Date, firstWaveKey);
            }

            var at = Clock.AtLocal(useful);
            return at.CompareTo(readyAt) > 0 ? at : readyAt;
        }
    }
}

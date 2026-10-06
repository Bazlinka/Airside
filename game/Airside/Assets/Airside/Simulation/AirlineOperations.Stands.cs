using System;
using System.Collections.Generic;
using Airside.Domain;

namespace Airside.Simulation
{
    public sealed partial class AirlineOperations
    {
        private static IReadOnlyList<StableId> StandIds(AdelaideBay[] bays)
        {
            var ids = new StableId[bays.Length];
            for (var i = 0; i < bays.Length; i++)
                ids[i] = new StableId(bays[i].Id);
            return ids;
        }

        private static IReadOnlyList<StableId> StandIds(AdelaideTerminalGate[] gates)
        {
            var ids = new StableId[gates.Length];
            for (var i = 0; i < gates.Length; i++)
                ids[i] = new StableId(gates[i].Id);
            return ids;
        }

        private static IReadOnlyList<StableId> CombinedStands()
        {
            var all = new StableId[AdelaideRegionalBays.Count + AdelaideTerminalGates.Count + AdelaideHelipadStands.Count];
            for (var i = 0; i < AdelaideRegionalBays.Count; i++)
                all[i] = AdelaideRegionalBays[i];
            for (var i = 0; i < AdelaideTerminalGates.Count; i++)
                all[AdelaideRegionalBays.Count + i] = AdelaideTerminalGates[i];
            for (var i = 0; i < AdelaideHelipadStands.Count; i++)
                all[AdelaideRegionalBays.Count + AdelaideTerminalGates.Count + i] = AdelaideHelipadStands[i];
            return all;
        }

        /// <summary>Jets use terminal gates; turboprops use the regional bays. Never the other way.</summary>
        public static bool NeedsTerminalGate(AircraftType type) =>
            AircraftCatalogue.TryFor(type, out var spec) && spec.StandClass == StandClass.TerminalGate;

        /// <summary>AIP walk-out stands 2A and 10A–10D are SF340 / marshaller only.</summary>
        public static bool IsWalkOutStand(StableId stand)
        {
            foreach (var bay in AdelaideLayout.Bays)
            {
                if (bay.Id != stand.Value)
                    continue;
                return bay.Reference is "2A" or "10A" or "10B" or "10C" or "10D";
            }

            return false;
        }

        /// <summary>Largest ICAO code letter this stand can take (regional bays and most gates: C).</summary>
        public static char StandCodeLetter(StableId stand)
        {
            foreach (var gate in CodeEGates)
                if (gate.Equals(stand))
                    return 'E';
            return 'C';
        }

        /// <summary>
        /// Gate or bay suits the aircraft: terminal vs regional class, the ICAO code letter
        /// (no A350 on a narrowbody gate), and the SF340-only walk-outs.
        /// </summary>
        public static bool StandFits(AircraftType type, StableId stand)
        {
            if (!StandClassFits(type, stand))
                return false;
            return AircraftCatalogue.CodeLetter(type) <= StandCodeLetter(stand);
        }

        /// <summary>
        /// The pre-ADR 0110 fit (stand class and walk-outs, no code letter). Only used when
        /// restoring a save, so an older game with a widebody parked on a code C gate still
        /// loads; the aircraft keeps that gate until it departs.
        /// </summary>
        public static bool StandClassFits(AircraftType type, StableId stand)
        {
            // Helicopters park on the helipad and nothing else does (ADR 0207).
            if (AdelaideHelipad.IsHelipadStand(stand) != (type != null && type.IsRotorcraft))
                return false;
            if (type != null && type.IsRotorcraft)
                return true;
            if (AdelaideGround.IsTerminalGate(stand) != NeedsTerminalGate(type))
                return false;
            // AIP walk-outs are SF340 / marshaller only — not ATR or Dash 8.
            return !IsWalkOutStand(stand) || ReferenceEquals(type, AircraftType.Saab340);
        }

        /// <summary>
        /// True when parking here would take a position another type needs more: a code C jet
        /// on a code E gate or on the half of a shared pier that blocks one, or a Saab on a
        /// 50-series bay while a walk-out could take it.
        /// </summary>
        public static bool WastesStand(AircraftType type, StableId stand) =>
            IsOversized(type, stand)
            || BlocksLargerSibling(type, stand)
            || ReferenceEquals(type, AircraftType.Saab340) && !AdelaideGround.IsTerminalGate(stand)
               && !IsWalkOutStand(stand);

        /// <summary>
        /// A code C jet on 20R, 22R or 28R closes the code E gate it shares a pier with just as
        /// surely as parking on it. Ranking only the E gate itself as wasteful sent jets to those
        /// halves while plain code C gates stood empty, and a 787-10 then had nowhere to park.
        /// </summary>
        private static bool BlocksLargerSibling(AircraftType type, StableId stand)
        {
            var sibling = PierSibling(stand);
            return !string.IsNullOrEmpty(sibling.Value) && IsOversized(type, sibling);
        }

        /// <summary>True when a smaller aircraft would take a gate a bigger one needs.</summary>
        public static bool IsOversized(AircraftType type, StableId stand) =>
            StandCodeLetter(stand) > 'C' && AircraftCatalogue.CodeLetter(type) < StandCodeLetter(stand);

        /// <summary>True when this airport has at least one stand the type could ever use.</summary>
        private bool HasStandFor(AircraftType type)
        {
            foreach (var stand in _stands)
                if (StandFits(type, stand))
                    return true;
            return false;
        }

        public bool IsStandFree(StableId stand) => IsStandFree(stand, null);

        private bool IsStandFree(StableId stand, FleetAircraft except)
        {
            if (!_stands.Contains(stand))
                return false;
            foreach (var aircraft in _fleet)
            {
                if (ReferenceEquals(aircraft, except))
                    continue;
                if (StandHolder(aircraft, stand) || StandHolder(aircraft, PierSibling(stand)))
                    return false;
            }

            return true;
        }

        private static StableId PierSibling(StableId stand)
        {
            foreach (var (a, b) in SharedPierPairs)
            {
                if (stand.Equals(a))
                    return b;
                if (stand.Equals(b))
                    return a;
            }

            return default;
        }

        /// <summary>
        /// Free regional bays — the stands a turboprop (every player aircraft) can use. Terminal
        /// gates are never offered here; see <see cref="FreeStandsFor"/>.
        /// </summary>
        public IEnumerable<StableId> FreeStands()
        {
            foreach (var stand in _stands)
                if (!AdelaideGround.IsTerminalGate(stand) && !AdelaideHelipad.IsHelipadStand(stand) && IsStandFree(stand))
                    yield return stand;
        }

        /// <summary>Free stands this aircraft type may use: terminal gates for jets, bays otherwise.</summary>
        public IEnumerable<StableId> FreeStandsFor(AircraftType type)
        {
            foreach (var stand in _stands)
                if (StandFits(type, stand) && IsStandFree(stand))
                    yield return stand;
        }

        /// <summary>
        /// Stands the player (or any owner) may assign right now: free, type-fit, lead-in clear,
        /// and inside the player's base allocation when that applies. Best/suggested stand is
        /// listed first when it is among them.
        /// </summary>
        public IReadOnlyList<StableId> AssignableStands(FleetAircraft aircraft)
        {
            var list = new List<StableId>();
            if (aircraft == null || aircraft.State != FleetState.AwaitingStand)
                return list;

            foreach (var stand in FreeStandsFor(aircraft.Type))
            {
                if (aircraft.Airline.IsPlayer && CareerState != null
                    && !PlayerBase.CanUseStand(CareerState.BaseLevel, aircraft.Type, stand))
                    continue;
                if (AdelaideGround.IsTerminalGate(stand) && !IsLeadInFree(stand, aircraft))
                    continue;
                list.Add(stand);
            }

            list.Sort((a, b) =>
            {
                var byTaxi = TaxiInSecondsTo(a, aircraft.Type, aircraft.AssignedRunway)
                    .CompareTo(TaxiInSecondsTo(b, aircraft.Type, aircraft.AssignedRunway));
                return byTaxi != 0 ? byTaxi : string.CompareOrdinal(a.Value, b.Value);
            });

            var suggested = SuggestStand(aircraft);
            if (suggested.HasValue)
            {
                for (var i = 0; i < list.Count; i++)
                {
                    if (!list[i].Equals(suggested.Value))
                        continue;
                    if (i > 0)
                    {
                        list.RemoveAt(i);
                        list.Insert(0, suggested.Value);
                    }
                    break;
                }
            }

            return list;
        }

        /// <summary>
        /// Stands stay held through taxi-out (pushback and the first apron chord) and
        /// while taxiing in, so a second aircraft cannot take the bay mid-push.
        /// </summary>
        private static bool StandHolder(FleetAircraft aircraft, StableId stand)
        {
            if (HoldsStand(aircraft) && aircraft.Stand.Equals(stand))
                return true;
            // A helicopter lifting off holds its spot until it is away (no taxi-out state for it).
            return aircraft.State == FleetState.TaxiOut && aircraft.DepartureStand.Equals(stand)
                   || aircraft.State == FleetState.TakingOff && aircraft.Type.IsRotorcraft
                   && aircraft.DepartureStand.Equals(stand);
        }

        /// <summary>A gate's lead-in is in use while an aircraft taxis in to it or pushes
        /// back from it. It is free once the departure reaches Holding Short.</summary>
        private static bool UsesLeadIn(FleetAircraft aircraft, out StableId gate)
        {
            gate = aircraft.State switch
            {
                FleetState.TaxiIn => aircraft.Stand,
                FleetState.TaxiOut => aircraft.DepartureStand,
                _ => default
            };
            return gate.Value != null && AdelaideGround.IsTerminalGate(gate);
        }

        private bool IsLeadInFree(StableId gate, FleetAircraft except)
        {
            var holder = GroundResourceHolder(AdelaideGround.LeadInResource(gate));
            return holder == null || ReferenceEquals(holder, except);
        }

        public CommandResult AssignStand(FleetAircraft aircraft, StableId stand)
        {
            if (aircraft == null || !_fleet.Contains(aircraft))
                return CommandResult.Refused("Unknown aircraft.");
            if (aircraft.State != FleetState.AwaitingStand)
                return CommandResult.Refused($"{aircraft.Registration} is not waiting for a stand.");
            if (!_stands.Contains(stand))
                return CommandResult.Refused($"{stand} is not a stand here.");
            if (!StandFits(aircraft.Type, stand))
                return CommandResult.Refused($"{Article.CapitalA(aircraft.Type.Name)} cannot use {AdelaideGround.StandLabel(stand)}.");
            if (aircraft.Airline.IsPlayer && CareerState != null
                && !PlayerBase.CanUseStand(CareerState.BaseLevel, aircraft.Type, stand))
                return CommandResult.Refused(
                    $"{AdelaideGround.StandLabel(stand)} isn't part of your {CareerState.Base.Title}.");
            if (!IsStandFree(stand))
                return CommandResult.Refused($"{AdelaideGround.StandLabel(stand)} is taken.");
            if (AdelaideGround.IsTerminalGate(stand) && !IsLeadInFree(stand, aircraft))
                return CommandResult.Refused($"Someone is on the {AdelaideGround.StandLabel(stand)} lead-in.");

            aircraft.Stand = stand;
            Transition(aircraft, FleetState.TaxiIn, _processedTo, TaxiInSecondsTo(stand, aircraft.Type, aircraft.AssignedRunway));
            return CommandResult.Ok;
        }

        /// <summary>
        /// True when parking <paramref name="type"/> on <paramref name="stand"/> would put a
        /// Dash 8-400 next to another aircraft on a <see cref="TightBayPairs"/> neighbour.
        /// </summary>
        public bool CrowdsNeighbour(AircraftType type, StableId stand)
        {
            foreach (var (a, b) in TightBayPairs)
            {
                StableId other;
                if (stand.Equals(a))
                    other = b;
                else if (stand.Equals(b))
                    other = a;
                else
                    continue;

                foreach (var aircraft in _fleet)
                    if (StandHolder(aircraft, other)
                        && (ReferenceEquals(type, AircraftType.Dash8Q400) || ReferenceEquals(aircraft.Type, AircraftType.Dash8Q400)))
                        return true;
            }

            // Any stand: a wide jet next to a parked 737 at gates 43 m apart overlapped wingtips.
            var here = AdelaideGround.StandPose(stand);
            var half = GroundTraffic.HalfSpan(type);
            foreach (var aircraft in _fleet)
            {
                var held = HoldsStand(aircraft) ? aircraft.Stand
                    : aircraft.State == FleetState.TaxiOut ? aircraft.DepartureStand
                    : default;
                if (string.IsNullOrEmpty(held.Value) || held.Equals(stand))
                    continue;
                if (GroundTraffic.TooClose(here, half, AdelaideGround.StandPose(held), GroundTraffic.HalfSpan(aircraft.Type)))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// The stand an aircraft waiting for one should take — other operators taxi straight to
        /// it, and the player's "Quickest" button offers it: a free stand that fits, preferring
        /// one that does not crowd a tight neighbour, then the shortest taxi in, then list
        /// order. Never refuses a free stand only for clearance, so nobody is stranded.
        /// </summary>
        public StableId? SuggestStand(FleetAircraft aircraft)
        {
            if (aircraft == null)
                return null;

            // An AI arrival reserves its stand before joining final. Keep that reservation
            // through a go-around and the runway roll, then taxi to the same position.
            if (!string.IsNullOrEmpty(aircraft.Stand.Value)
                && _stands.Contains(aircraft.Stand)
                && StandFits(aircraft.Type, aircraft.Stand)
                && IsStandFree(aircraft.Stand, aircraft))
                return aircraft.Stand;

            // Scheduled terminal operators return to their own gate when it is available.
            // This keeps the Air New Zealand and Virgin streams operationally legible while
            // still allowing an alternate compatible gate if the home position is occupied.
            if (NeedsTerminalGate(aircraft.Type)
                && !string.IsNullOrEmpty(aircraft.DepartureStand.Value)
                && _stands.Contains(aircraft.DepartureStand)
                && StandFits(aircraft.Type, aircraft.DepartureStand)
                && IsStandFree(aircraft.DepartureStand)
                && IsLeadInFree(aircraft.DepartureStand, aircraft)
                && (!aircraft.Airline.IsPlayer || CareerState == null
                    || PlayerBase.CanUseStand(CareerState.BaseLevel, aircraft.Type, aircraft.DepartureStand))
                // A gate the player now leases is no longer an AI operator's home (ADR 0125):
                // Qantas returning to 28R blocked pier 28's widebody line under the player's 787.
                && (aircraft.Airline.IsPlayer || CareerState == null || PlayerAirline == null
                    || !PlayerBase.IsDedicatedStand(CareerState.BaseLevel, aircraft.DepartureStand)))
                return aircraft.DepartureStand;

            // Player turboprops that are away reserve that many regional bays, so a second
            // ATR coming home is not stranded by AI filling the apron (ADR 0056).
            if (!aircraft.Airline.IsPlayer && !NeedsTerminalGate(aircraft.Type) && !aircraft.Type.IsRotorcraft
                && PlayerAirline != null)
            {
                var reserved = PlayerTurbopropsNeedingABay();
                var free = 0;
                foreach (var stand in FreeStandsFor(aircraft.Type))
                    free++;
                if (free <= reserved)
                    return null;
            }

            if (aircraft.Airline.IsPlayer && CareerState != null)
                return SuggestPlayerStandFor(aircraft.Type, aircraft);

            return SuggestStandFor(aircraft.Type, aircraft);
        }

        private StableId? SuggestPlayerStandFor(AircraftType type, FleetAircraft except = null)
        {
            if (type == null || CareerState == null || !PlayerBase.Supports(CareerState.BaseLevel, type))
                return null;

            // First use the player's actual leased positions. This makes the base visible in
            // day-to-day operations and keeps jet access tied to gates 27/29 (plus pier 28 at
            // International) instead of silently using any terminal gate.
            foreach (var stand in PlayerBase.DedicatedStands(CareerState.BaseLevel, type))
            {
                if (!_stands.Contains(stand) || !StandFits(type, stand) || !IsStandFree(stand))
                    continue;
                if (AdelaideGround.IsTerminalGate(stand) && !IsLeadInFree(stand, except))
                    continue;
                return stand;
            }

            // Expanded regional operations lease capacity across the shared regional apron:
            // if the authored dedicated bay does not fit (e.g. ATR/Dash on walk-out 10A),
            // use another free regional bay. Existing away-aircraft reservation logic still
            // keeps enough shared capacity available for the player's fleet.
            if (!NeedsTerminalGate(type) && !type.IsRotorcraft && CareerState.BaseLevel >= PlayerBaseLevel.ExpandedRegional)
                return SuggestStandFor(type, except, allowPlayerDedicated: true);

            return null;
        }

        /// <summary>
        /// Same ranking as <see cref="SuggestStand"/> for a type that is not yet on the field
        /// (regional backfill). <paramref name="except"/> is the aircraft already allowed to
        /// use a busy gate lead-in; null means the lead-in must be empty.
        /// </summary>
        public StableId? SuggestStandFor(AircraftType type, FleetAircraft except = null,
            bool allowPlayerDedicated = false)
        {
            if (type == null)
                return null;

            StableId? best = null;
            var bestCrowds = true;
            var bestOversized = true;
            var bestSeconds = long.MaxValue;
            foreach (var stand in _stands)
            {
                if (!StandFits(type, stand) || !IsStandFree(stand))
                    continue;
                if (!allowPlayerDedicated && PlayerAirline != null && CareerState != null
                    && PlayerBase.IsDedicatedStand(CareerState.BaseLevel, stand))
                    continue;
                if (AdelaideGround.IsTerminalGate(stand) && !IsLeadInFree(stand, except))
                    continue;
                var crowds = CrowdsNeighbour(type, stand);
                // Keep code E gates for the widebodies that need them (ADR 0110), and the
                // 50-series for the ATR / Q400s that cannot use a Saab walk-out (ADR 0111).
                var oversized = WastesStand(type, stand);
                var seconds = type.IsRotorcraft ? 0L : TaxiInSecondsTo(stand, type);
                // Wasting a stand another type needs outranks a tight neighbour: crowding is
                // cosmetic, but a Q400 with every 50-series bay full of Saabs cannot park.
                if (best != null)
                {
                    if (oversized != bestOversized)
                    {
                        if (oversized)
                            continue;
                    }
                    else if (crowds != bestCrowds)
                    {
                        if (crowds)
                            continue;
                    }
                    else if (seconds >= bestSeconds)
                        continue;
                }

                best = stand;
                bestCrowds = crowds;
                bestOversized = oversized;
                bestSeconds = seconds;
            }

            return best;
        }

        private static bool HoldsStand(FleetAircraft aircraft) => aircraft.State == FleetState.Maintenance
            && !string.IsNullOrEmpty(aircraft.Stand.Value) ||
            !string.IsNullOrEmpty(aircraft.Stand.Value)
            && (aircraft.State is FleetState.AtStand or FleetState.HoldingForLanding or FleetState.Landing
                or FleetState.GoAround or FleetState.AwaitingStand or FleetState.TaxiIn);

        /// <summary>Outbound ground states must remember which stand they left.</summary>
        internal static bool RequiresDepartureStand(FleetState state) => state is
            FleetState.TaxiOut or FleetState.HoldingShort or FleetState.TakingOff;
    }
}

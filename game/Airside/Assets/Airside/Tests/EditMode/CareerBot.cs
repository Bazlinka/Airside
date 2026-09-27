using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Airside.Domain;
using Airside.Simulation;

namespace Airside.Tests
{
    /// <summary>How a simulated player plays (ADR 0125).</summary>
    public enum CareerPlayStyle
    {
        /// <summary>Acts on every aircraft at once, parks on landing, plans checks, reinvests.</summary>
        Competent,
        /// <summary>Reacts 10–40 min late, lets the tower park, takes the first usable offer, checks late.</summary>
        Casual
    }

    /// <summary>One hourly sample of a simulated career.</summary>
    public readonly struct CareerSample
    {
        public CareerSample(double hours, double openHours, long funds, int reliability, int fleet, OperatingTier tier,
            int rotations)
        {
            Hours = hours;
            OpenHours = openHours;
            Funds = funds;
            Reliability = reliability;
            Fleet = fleet;
            Tier = tier;
            Rotations = rotations;
        }

        public double Hours { get; }
        public double OpenHours { get; }
        public long Funds { get; }
        public int Reliability { get; }
        public int Fleet { get; }
        public OperatingTier Tier { get; }
        public int Rotations { get; }
    }

    public sealed class CareerRunResult
    {
        public CareerDifficulty Difficulty;
        public CareerPlayStyle Style;
        public int Seed;
        /// <summary>Open (non-curfew) hours when each tier was reached — the stand-in for active play hours.</summary>
        public readonly Dictionary<OperatingTier, double> TierAtOpenHours = new();
        public readonly Dictionary<OperatingTier, int> TierAtRotations = new();
        public readonly Dictionary<string, double> GoalDoneAtOpenHours = new();
        public double? FinaleAtOpenHours;
        public double OpenHours;
        public double SimHours;
        public long MinFunds = long.MaxValue;
        public double MinFundsAtOpenHours;
        public long FinalFunds;
        public int MinReliability = int.MaxValue;
        public int FinalReliability;
        public OperatingTier FinalTier;
        public int FinalFleet;
        public int FinalRotations;
        public int RecoveryContracts;
        public readonly List<string> OpenGoalsAtEnd = new();
        public readonly List<string> Flags = new();
        public readonly Dictionary<string, int> Refusals = new();
        public readonly List<CareerSample> Samples = new();
    }

    /// <summary>
    /// ADR 0125 — a simulated player for the career balance runs. Uses only the public commands
    /// the HUD uses, and only destinations the flight planner offers (<see cref="AirlineOperations.MapDestinations"/>
    /// for Adelaide aircraft), so anything it cannot do a real player cannot do either.
    /// </summary>
    public sealed class CareerBot
    {
        private readonly AirlineOperations _ops;
        private readonly CareerPlayStyle _style;
        private readonly SeededRandomSource _random;
        private readonly CareerRunResult _result;
        private readonly Dictionary<string, long> _readyAt = new(StringComparer.Ordinal);
        private long _nextGrowthAt;

        public CareerBot(AirlineOperations ops, CareerPlayStyle style, int seed, CareerRunResult result)
        {
            _ops = ops;
            _style = style;
            _random = new SeededRandomSource((uint)(seed * 7919 + 17));
            _result = result;
        }

        private bool Competent => _style == CareerPlayStyle.Competent;
        private AirlineCareerState Career => _ops.CareerState;

        public void Tick(SimulationTime now)
        {
            var goals = OpenGoals();
            TakeContract(goals);
            if (now.ElapsedSeconds >= _nextGrowthAt)
            {
                Grow(goals);
                _nextGrowthAt = now.ElapsedSeconds + (Competent ? 15 * 60 : 2 * 3600);
            }

            foreach (var aircraft in _ops.Fleet.Where(a => a.Airline.IsPlayer).ToList())
            {
                if (aircraft.State == FleetState.AwaitingStand && Competent)
                {
                    var stand = _ops.SuggestStand(aircraft);
                    if (stand.HasValue)
                        Record(_ops.AssignStand(aircraft, stand.Value));
                    continue;
                }

                if (aircraft.State != FleetState.AtStand || aircraft.Scheduled.HasValue || Maintenance.InCheck(aircraft, now))
                {
                    _readyAt.Remove(aircraft.Registration);
                    continue;
                }

                if (!Ready(aircraft.Registration, now))
                    continue;
                if (Maintenance.RotationsUntilDue(aircraft) <= (Competent ? 0 : -1))
                {
                    Record(_ops.StartCheck(aircraft));
                    continue;
                }

                var destination = ChooseDestination(aircraft, goals);
                if (destination.HasValue)
                {
                    var lead = DeparturePrep.TotalSeconds(aircraft.Type, Career.BaseLevel) + 60;
                    Record(_ops.ScheduleDeparture(aircraft, destination.Value, now.Advance(lead)));
                }
            }

            foreach (var aircraft in _ops.OutstationFleet.ToList())
            {
                if (aircraft.HasFlight || aircraft.InCheck(now.ElapsedSeconds))
                {
                    _readyAt.Remove(aircraft.Registration);
                    continue;
                }

                if (!Ready(aircraft.Registration, now))
                    continue;
                if (aircraft.CheckDue)
                {
                    Record(_ops.StartOutstationCheck(aircraft.Registration));
                    continue;
                }

                var destination = ChooseNetworkDestination(aircraft, goals);
                if (destination != null)
                    Record(_ops.ScheduleOutstationService(aircraft.Registration, destination, now.Advance(60)));
            }
        }

        /// <summary>Casual players notice an idle aircraft 10–40 minutes late.</summary>
        private bool Ready(string registration, SimulationTime now)
        {
            if (Competent)
                return true;
            if (!_readyAt.TryGetValue(registration, out var at))
            {
                at = now.ElapsedSeconds + _random.NextInt(600, 2400);
                _readyAt[registration] = at;
            }

            return now.ElapsedSeconds >= at;
        }

        private HashSet<string> OpenGoals()
        {
            var open = new HashSet<string>(StringComparer.Ordinal);
            foreach (var goal in _ops.CareerGoals())
                if (!goal.Complete && goal.Stage == Career.Tier)
                    open.Add(goal.Id);
            return open;
        }

        // ---- Contracts -------------------------------------------------------------

        private void TakeContract(HashSet<string> goals)
        {
            if (Career.ActiveContract != null)
                return;
            var owned = _ops.Fleet.Where(a => a.Airline.IsPlayer).Select(a => a.Type.Id).ToHashSet();
            var usable = _ops.MarketOffers()
                .Where(o => !Career.HasCompleted(o.Id) && Career.Tier >= o.RequiredTier && owned.Contains(o.EligibleType.Id)
                            && DestinationCatalogue.TryFind(o.DestinationCode, out var d) && Plannable(d))
                .ToList();
            if (usable.Count == 0)
                return;
            var pick = Competent
                ? usable.OrderByDescending(o => ContractValue(o, goals)).First()
                : usable[0];
            if (pick.Id.StartsWith("REC-", StringComparison.Ordinal))
                _result.RecoveryContracts++;
            Record(_ops.AcceptContract(pick));
        }

        private static double ContractValue(RouteContractDefinition offer, HashSet<string> goals)
        {
            var value = (offer.PaymentPerRotation * offer.RequiredRotations + offer.CompletionReward)
                        / (double)Math.Max(1, offer.RequiredRotations);
            // A contract is worth what it pays per hour of flying it.
            if (DestinationCatalogue.TryFind(offer.DestinationCode, out var destination))
                value /= Math.Max(0.5, 2 * LegTiming.AirborneSeconds(DestinationCatalogue.Adelaide.DistanceKmTo(destination),
                    offer.EligibleType) / 3600.0 + 1.2);
            if (goals.Contains("prove-service") && RouteAccess.BandOf(offer.DestinationCode) == RouteBand.Regional)
                value += 100_000;
            return value;
        }

        // ---- Destinations ----------------------------------------------------------

        /// <summary>What the flight planner lists for an Adelaide aircraft.</summary>
        private bool Plannable(Destination destination)
        {
            foreach (var listed in _ops.MapDestinations())
                if (listed.Code == destination.Code)
                    return true;
            return false;
        }

        private Destination? ChooseDestination(FleetAircraft aircraft, HashSet<string> goals)
        {
            var candidates = new List<(Destination destination, double score)>();
            foreach (var destination in _ops.MapDestinations())
            {
                if (!_ops.CanOperate(aircraft, destination)
                    || Career.Tier < RouteAccess.RequiredTier(RouteAccess.BandOf(destination)))
                    continue;
                var forecast = _ops.Forecast(_ops.Home, destination, aircraft.Type);
                // Value per hour of the aircraft's time: margin and goal progress over the
                // round trip (both legs plus the outstation turn and the prep before pushback).
                var blockHours = (2 * _ops.AirborneSeconds(aircraft, destination) + 40 * 60
                                  + DeparturePrep.TotalSeconds(aircraft.Type, Career.BaseLevel)) / 3600.0;
                var value = (double)forecast.Margin + GoalBonus(destination, goals);
                if (goals.Contains("first-rotations") || goals.Contains("regional-service") || goals.Contains("domestic-service"))
                    value += 1500;
                var score = value / Math.Max(0.5, blockHours);
                if (Career.ActiveContract != null
                    && Career.TryFindDefinition(Career.ActiveContract.DefinitionId, out var contract)
                    && contract.EligibleType.Id == aircraft.Type.Id
                    && contract.MatchesRoute(_ops.Home.Code, destination.Code))
                    score += 1_000_000;
                candidates.Add((destination, score));
            }

            if (candidates.Count == 0)
                return null;
            candidates.Sort((a, b) => b.score.CompareTo(a.score));
            // Anyone flies the contract they accepted; casual players are only loose about the rest.
            var index = Competent || candidates[0].score >= 1_000_000 ? 0 : _random.NextInt(0, Math.Min(3, candidates.Count));
            return candidates[index].destination;
        }

        private string ChooseNetworkDestination(OutstationAircraft aircraft, HashSet<string> goals)
        {
            if (!DestinationCatalogue.TryFind(aircraft.BaseCode, out var origin))
                return null;
            var candidates = new List<(string code, double score)>();
            foreach (var destination in DestinationCatalogue.All)
            {
                if (destination.Code == aircraft.BaseCode || destination.Code == _ops.Home.Code)
                    continue;
                if (!aircraft.Type.CanReach(origin.DistanceKmTo(destination)) || !RouteAccess.Allows(aircraft.Type, destination)
                    || Career.Tier < RouteAccess.RequiredTier(RouteAccess.BandOf(destination)))
                    continue;
                var forecast = _ops.Forecast(origin, destination, aircraft.Type);
                var blockHours = (2 * LegTiming.AirborneSeconds(origin.DistanceKmTo(destination), aircraft.Type) + 45 * 60) / 3600.0;
                candidates.Add((destination.Code, (forecast.Margin + GoalBonus(destination, goals)) / Math.Max(0.5, blockHours)));
            }

            if (candidates.Count == 0)
                return null;
            candidates.Sort((a, b) => b.score.CompareTo(a.score));
            return candidates[Competent ? 0 : _random.NextInt(0, Math.Min(3, candidates.Count))].code;
        }

        private double GoalBonus(Destination destination, HashSet<string> goals)
        {
            if (Career.ServedDestinations.Contains(destination.Code))
                return 0;
            var band = RouteAccess.BandOf(destination);
            var bonus = 0.0;
            if (goals.Contains("regional-network") && band == RouteBand.Regional)
                bonus += 4000;
            if (goals.Contains("domestic-network") && destination.State != "SA" && band >= RouteBand.Domestic
                && band <= RouteBand.National)
                bonus += 6000;
            if (goals.Contains("international-network") && RouteAccess.IsInternational(destination.Code))
                bonus += 12000;
            if (goals.Contains("established-network"))
                bonus += 3000;
            return bonus;
        }

        // ---- Growth ----------------------------------------------------------------

        /// <summary>Cash kept back after a purchase: enough to dispatch every aircraft once more.</summary>
        private long Reserve(int fleet)
        {
            var reserve = Competent ? 1_000L : 2_500L;
            foreach (var aircraft in _ops.Fleet.Where(a => a.Airline.IsPlayer))
                reserve += _ops.DispatchCost(aircraft.Type, Math.Min(aircraft.Type.PracticalRangeKm, 1500));
            foreach (var aircraft in _ops.OutstationFleet)
                reserve += _ops.DispatchCost(aircraft.Type, Math.Min(aircraft.Type.PracticalRangeKm, 1500));
            return reserve;
        }

        private void Grow(HashSet<string> goals)
        {
            var adelaide = _ops.Fleet.Count(a => a.Airline.IsPlayer);
            var fleet = _ops.PlayerFleetCount();
            var types = _ops.PlayerOwnedTypes();
            var reserve = Reserve(fleet);
            var wantType = DesiredType(goals, types);
            bool OwnsReach(RouteBand band) => types.Any(t => RouteAccess.Ceiling(t) >= band);
            var wantAircraft = goals.Contains("regional-fleet") || goals.Contains("established-fleet")
                               || goals.Contains("domestic-jet") || goals.Contains("international-widebody")
                               || (goals.Contains("domestic-network") && !OwnsReach(RouteBand.Domestic))
                               || (goals.Contains("international-network") && !OwnsReach(RouteBand.Tasman))
                               || (wantType != null && AircraftAcquisition.TryFor(wantType, out var growth)
                                   && Career.Funds >= growth.Price * 2 + reserve);

            // Base: the goal asks for it, the fleet has outgrown it, or the wanted type needs it.
            if (PlayerBase.TryNext(Career.BaseLevel, out var next)
                && (goals.Contains("regional-base")
                    || (wantAircraft && adelaide >= Career.Base.FleetCapacity)
                    || (wantType != null && !PlayerBase.Supports(Career.BaseLevel, wantType)))
                && Career.Tier >= next.RequiredTier && Career.CompletedPlayerRotations >= next.RequiredRotations
                && Career.Funds >= next.UpgradeCost + reserve)
            {
                Record(_ops.UpgradePlayerBase());
                return;
            }

            if ((goals.Contains("domestic-base") || goals.Contains("international-bases")
                 || (goals.Contains("established-fleet") && adelaide >= Career.Base.FleetCapacity))
                && Career.Tier >= OperatingTier.Domestic && Career.OutstationBases.Count < 3
                && Career.Funds >= _ops.NextOutstationCost + reserve)
            {
                var code = new[] { "MEL", "SYD", "BNE", "PER" }.First(c => !Career.HasOutstationBase(c));
                Record(_ops.OpenOutstationBase(code));
                return;
            }

            if (!wantAircraft || wantType == null || !AircraftAcquisition.TryFor(wantType, out var offer)
                || Career.Funds < offer.Price + reserve || Career.Tier < offer.RequiredTier
                || Career.Reliability < offer.RequiredReliability || Career.CompletedPlayerRotations < offer.RequiredRotations)
                return;
            if (adelaide < Career.Base.FleetCapacity && PlayerBase.Supports(Career.BaseLevel, wantType))
            {
                Record(_ops.BuyAircraft(wantType));
                return;
            }

            foreach (var code in Career.OutstationBases)
            {
                if (_ops.OutstationFleet.Count(a => a.BaseCode == code) >= AirlineOperations.OutstationCapacity
                    || !_ops.HasOutstationRoute(wantType, code))
                    continue;
                Record(_ops.BuyAircraftAtOutstation(wantType, code));
                return;
            }
        }

        /// <summary>The type a sensible player buys next: whatever the open goal needs, else the tier's workhorse.</summary>
        private AircraftType DesiredType(HashSet<string> goals, IReadOnlyList<AircraftType> owned)
        {
            bool Allowed(AircraftOffer offer) => Career.Tier >= offer.RequiredTier
                                                 && Career.Reliability >= offer.RequiredReliability
                                                 && Career.CompletedPlayerRotations >= offer.RequiredRotations;
            if (goals.Contains("international-widebody") && Allowed(AircraftAcquisition.Boeing78710))
                return AircraftType.Boeing78710;
            if (goals.Contains("domestic-jet") && Allowed(AircraftAcquisition.Boeing7378))
                return AircraftType.Boeing7378;
            bool OwnsReach(RouteBand band) => owned.Any(t => RouteAccess.Ceiling(t) >= band);
            if (goals.Contains("international-network") && !OwnsReach(RouteBand.Tasman) && Allowed(AircraftAcquisition.AirbusA321Neo))
                return AircraftType.AirbusA321Neo;
            if (goals.Contains("domestic-network") && !OwnsReach(RouteBand.Domestic) && Allowed(AircraftAcquisition.Dash8Q400))
                return AircraftType.Dash8Q400;
            if (Career.Tier >= OperatingTier.International && Allowed(AircraftAcquisition.AirbusA321Neo))
                return AircraftType.AirbusA321Neo;
            if (Career.Tier >= OperatingTier.Domestic && Allowed(AircraftAcquisition.Boeing7378))
                return AircraftType.Boeing7378;
            if (Career.Tier >= OperatingTier.Regional && Allowed(AircraftAcquisition.Dash8Q400))
                return AircraftType.Dash8Q400;
            return Allowed(AircraftAcquisition.Atr42) ? AircraftType.Atr42 : null;
        }

        // ---- Bookkeeping -----------------------------------------------------------

        private static readonly Regex Numbers = new(@"\$?[\d,]+(\.\d+)?", RegexOptions.Compiled);
        private static readonly Regex Registrations = new(@"VH-[A-Z0-9]+", RegexOptions.Compiled);

        private void Record(CommandResult result)
        {
            if (result.Accepted)
                return;
            var key = Numbers.Replace(Registrations.Replace(result.Reason ?? string.Empty, "VH-…"), "#");
            _result.Refusals[key] = _result.Refusals.TryGetValue(key, out var n) ? n + 1 : 1;
        }
    }

    /// <summary>Runs one simulated career and measures it (ADR 0125).</summary>
    public static class CareerSimulation
    {
        /// <summary>An aircraft idle at its stand this long (open hours) with the bot trying counts as stuck.</summary>
        public const double IdleStuckHours = 6;
        public const double BrokeStuckHours = 12;

        public static CareerRunResult Run(CareerDifficulty difficulty, CareerPlayStyle style, int seed, double maxOpenHours)
        {
            var result = new CareerRunResult { Difficulty = difficulty, Style = style, Seed = seed };
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = AirlineOperations.StartAtAdelaide(clock, new SeededRandomSource((uint)seed), Airline.Player("Balance Air", "#39708A"),
                difficulty: difficulty, firstFlightCoaching: false);
            var bot = new CareerBot(ops, style, seed, result);
            var goalsDone = new HashSet<string>(StringComparer.Ordinal);
            var idleSince = new Dictionary<string, double>(StringComparer.Ordinal);
            var flagged = new HashSet<string>(StringComparer.Ordinal);
            var tier = ops.CareerState.Tier;
            var open = 0.0;
            var nextSample = 0.0;
            double? brokeSince = null;
            var cheapest = CheapestDispatch(ops);

            void Flag(string key, string text)
            {
                if (flagged.Add(key))
                    result.Flags.Add($"{open:0.0} h: {text}");
            }

            while (open < maxOpenHours && !ops.CareerState.FinaleReached)
            {
                bot.Tick(clock.Now);
                var now = clock.Now.ElapsedSeconds;
                var next = ops.NextEventAt()?.ElapsedSeconds ?? now + 60;
                var target = Math.Max(now + 1, Math.Min(next, now + 60));
                var openStep = AirportCurfew.IsClosed(clock.Now, ops.Clock) ? 0 : target - now;
                clock.Set(new SimulationTime(target));
                ops.Update();
                open += openStep / 3600.0;

                var career = ops.CareerState;
                if (career.Tier != tier)
                {
                    for (var t = tier + 1; t <= career.Tier; t++)
                    {
                        result.TierAtOpenHours[t] = open;
                        result.TierAtRotations[t] = career.CompletedPlayerRotations;
                    }

                    tier = career.Tier;
                }

                foreach (var goal in ops.CareerGoals())
                    if (goal.Complete && goalsDone.Add(goal.Id))
                        result.GoalDoneAtOpenHours[goal.Id] = open;

                if (career.Funds < result.MinFunds)
                {
                    result.MinFunds = career.Funds;
                    result.MinFundsAtOpenHours = open;
                }

                result.MinReliability = Math.Min(result.MinReliability, career.Reliability);

                // Stuck: an Adelaide aircraft idle at its stand for hours while the bot keeps trying.
                foreach (var aircraft in ops.Fleet.Where(a => a.Airline.IsPlayer))
                {
                    var idle = aircraft.State == FleetState.AtStand && !aircraft.Scheduled.HasValue
                               && !Maintenance.InCheck(aircraft, clock.Now);
                    var waiting = aircraft.State == FleetState.AwaitingStand;
                    if (!idle && !waiting)
                    {
                        idleSince.Remove(aircraft.Registration);
                        continue;
                    }

                    if (!idleSince.TryGetValue(aircraft.Registration, out var since))
                        idleSince[aircraft.Registration] = since = open;
                    var limit = waiting ? 1.0 : IdleStuckHours;
                    if (open - since >= limit)
                        Flag($"idle:{aircraft.Registration}:{(int)(since / 24)}",
                            $"{aircraft.Registration} ({aircraft.Type.Name}) {(waiting ? "waiting for a stand" : "idle at its stand")} for {open - since:0.0} h"
                            + (waiting ? $" — {ops.Why(aircraft).Kind} {ops.Why(aircraft).Detail}" : string.Empty));
                }

                if (career.Funds < cheapest)
                {
                    brokeSince ??= open;
                    if (open - brokeSince.Value >= BrokeStuckHours)
                        Flag($"broke:{(int)(brokeSince.Value / 24)}", $"cash ${career.Funds:N0} below the cheapest flight for {open - brokeSince.Value:0} h");
                }
                else
                    brokeSince = null;

                if (open >= nextSample)
                {
                    result.Samples.Add(new CareerSample(clock.Now.ElapsedSeconds / 3600.0, open, career.Funds, career.Reliability,
                        ops.PlayerFleetCount(), career.Tier, career.CompletedPlayerRotations));
                    nextSample += 1.0;
                }
            }

            var final = ops.CareerState;
            if (final.FinaleReached)
                result.FinaleAtOpenHours = open;
            result.OpenHours = open;
            result.SimHours = clock.Now.ElapsedSeconds / 3600.0;
            result.FinalFunds = final.Funds;
            result.FinalReliability = final.Reliability;
            result.FinalTier = final.Tier;
            result.FinalFleet = ops.PlayerFleetCount();
            result.FinalRotations = final.CompletedPlayerRotations;
            foreach (var goal in ops.CareerGoals())
                if (!goal.Complete && goal.Stage == final.Tier)
                    result.OpenGoalsAtEnd.Add($"{goal.Title} {goal.ProgressText}");
            return result;
        }

        private static long CheapestDispatch(AirlineOperations ops)
        {
            var cheapest = long.MaxValue;
            foreach (var aircraft in ops.Fleet.Where(a => a.Airline.IsPlayer))
            foreach (var destination in ops.MapDestinations())
                if (ops.CanOperate(aircraft, destination))
                    cheapest = Math.Min(cheapest, ops.DispatchCost(aircraft.Type, ops.DistanceKm(destination)));
            return cheapest == long.MaxValue ? 0 : cheapest;
        }
    }
}

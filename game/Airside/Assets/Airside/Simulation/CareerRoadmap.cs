using System;
using System.Collections.Generic;
using System.Linq;
using Airside.Domain;

namespace Airside.Simulation
{
    /// <summary>A player-selected objective. Progress is measured from airline facts, not elapsed time.</summary>
    public readonly struct CareerGoalStatus
    {
        public CareerGoalStatus(string id, string title, OperatingTier stage, int progress, int target)
        {
            Id = id;
            Title = title;
            Stage = stage;
            Progress = Math.Min(Math.Max(0, progress), target);
            Target = target;
        }

        public string Id { get; }
        public string Title { get; }
        public OperatingTier Stage { get; }
        public int Progress { get; }
        public int Target { get; }
        public bool Complete => Progress >= Target;
        public string ProgressText => $"{Progress}/{Target}";
    }

    /// <summary>
    /// Long-form, self-led career requirements. Goals within an operating tier can be completed
    /// in any order. Tier changes use the same facts as the roadmap, so the HUD cannot advertise
    /// a different unlock rule. No requirement reads wall-clock time or app-open time.
    /// </summary>
    public static class CareerRoadmap
    {
        public const string FinaleKey = "career:established-airline";
        public const int FinalFleet = 18;
        public const int FinalBases = 3;
        public const int FinalDestinations = 12;
        public const int FinalInternationalDestinations = 3;
        public const int FinalMarginServices = 30;

        public static IReadOnlyList<CareerGoalStatus> Evaluate(AirlineCareerState career,
            IReadOnlyList<AircraftType> fleet, int fleetCount = 0)
        {
            var goals = new List<CareerGoalStatus>();
            if (career == null) return goals;
            fleet ??= Array.Empty<AircraftType>();
            if (fleetCount <= 0) fleetCount = fleet.Count;
            var regional = CountBand(career, RouteBand.Regional);
            var domestic = CountDomestic(career);
            var international = CountInternational(career);
            var regionalContracts = CountRegionalContracts(career);
            var hasJet = AirlineCareerState.OwnsAnyJet(fleet) ? 1 : 0;
            var hasWidebody = OwnsWidebody(fleet) ? 1 : 0;
            var rel = career.Reliability;
            goals.Add(new CareerGoalStatus("prove-service", "Fulfil a regional contract", OperatingTier.Provisional, regionalContracts, 1));
            goals.Add(new CareerGoalStatus("first-rotations", "Complete five services", OperatingTier.Provisional, career.CompletedPlayerRotations, 5));
            goals.Add(new CareerGoalStatus("regional-reliability", "Reach 70% reliability", OperatingTier.Provisional, rel, 70));
            goals.Add(new CareerGoalStatus("regional-network", "Serve four regional destinations", OperatingTier.Regional, regional, 4));
            goals.Add(new CareerGoalStatus("regional-fleet", "Operate three aircraft", OperatingTier.Regional, fleetCount, 3));
            goals.Add(new CareerGoalStatus("regional-base", "Expand the Adelaide regional base", OperatingTier.Regional,
                career.BaseLevel >= PlayerBaseLevel.ExpandedRegional ? 1 : 0, 1));
            goals.Add(new CareerGoalStatus("regional-service", "Complete 30 services", OperatingTier.Regional,
                career.CompletedPlayerRotations, 30));
            goals.Add(new CareerGoalStatus("domestic-reliability", "Reach 80% reliability", OperatingTier.Regional, rel, 80));
            goals.Add(new CareerGoalStatus("domestic-network", "Serve three domestic destinations", OperatingTier.Domestic, domestic, 3));
            goals.Add(new CareerGoalStatus("domestic-jet", "Operate a jet", OperatingTier.Domestic, hasJet, 1));
            goals.Add(new CareerGoalStatus("domestic-base", "Open an outstation base", OperatingTier.Domestic, career.BaseCount, 2));
            goals.Add(new CareerGoalStatus("domestic-service", "Complete 75 services", OperatingTier.Domestic,
                career.CompletedPlayerRotations, 75));
            goals.Add(new CareerGoalStatus("international-reliability", "Reach 88% reliability", OperatingTier.Domestic, rel, 88));
            goals.Add(new CareerGoalStatus("international-network", "Serve three international destinations",
                OperatingTier.International, international, FinalInternationalDestinations));
            goals.Add(new CareerGoalStatus("international-widebody", "Operate a widebody", OperatingTier.International, hasWidebody, 1));
            goals.Add(new CareerGoalStatus("international-bases", "Operate three bases", OperatingTier.International,
                career.BaseCount, FinalBases));
            goals.Add(new CareerGoalStatus("established-fleet", "Operate 18 aircraft", OperatingTier.International,
                fleetCount, FinalFleet));
            goals.Add(new CareerGoalStatus("established-network", "Serve 12 destinations", OperatingTier.International,
                career.ServedDestinations.Count, FinalDestinations));
            goals.Add(new CareerGoalStatus("established-margin", "Earn a positive margin across 30 services",
                OperatingTier.International,
                career.RecentServiceMargins.Count >= FinalMarginServices && career.RecentOperatingMargin > 0 ? 1 : 0, 1));
            goals.Add(new CareerGoalStatus("established-reliability", "Reach 90% reliability", OperatingTier.International, rel, 90));
            return goals;
        }

        public static bool CanReach(AirlineCareerState career, IReadOnlyList<AircraftType> fleet, OperatingTier tier, int fleetCount = 0)
        {
            if (career == null) return false;
            if (tier == OperatingTier.Provisional) return true;
            var goals = Evaluate(career, fleet, fleetCount);
            foreach (var goal in goals)
            {
                if (tier == OperatingTier.Regional && goal.Stage == OperatingTier.Provisional && !goal.Complete)
                    return false;
                if (tier == OperatingTier.Domestic && goal.Stage == OperatingTier.Regional && !goal.Complete)
                    return false;
                if (tier == OperatingTier.International && goal.Stage == OperatingTier.Domestic && !goal.Complete)
                    return false;
            }
            return true;
        }

        public static bool FinaleReady(AirlineCareerState career, IReadOnlyList<AircraftType> fleet, int fleetCount = 0)
        {
            if (career == null || career.Tier < OperatingTier.International) return false;
            if (fleetCount <= 0) fleetCount = fleet?.Count ?? 0;
            if (career.ProcessedSettlementKeys.Contains(FinaleKey)) return true;
            if (career.BaseCount < FinalBases || career.Reliability < 90
                || career.ServedDestinations.Count < FinalDestinations
                || CountInternational(career) < FinalInternationalDestinations
                || career.RecentServiceMargins.Count < FinalMarginServices
                || career.RecentOperatingMargin <= 0 || fleetCount < FinalFleet)
                return false;
            return true;
        }

        public static CareerGoalStatus Pinned(AirlineCareerState career, IReadOnlyList<AircraftType> fleet, int fleetCount = 0)
        {
            var goals = Evaluate(career, fleet, fleetCount);
            foreach (var goal in goals)
                if (goal.Id == career.PinnedGoalId && goal.Stage <= career.Tier)
                    return goal;
            foreach (var goal in goals)
                if (goal.Stage <= career.Tier && !goal.Complete)
                    return goal;
            return goals.Count > 0 ? goals[goals.Count - 1] : default;
        }

        public static string RouteGuidance(AirlineCareerState career,
            IReadOnlyList<AircraftType> fleet, Destination destination)
        {
            if (career == null || string.IsNullOrEmpty(destination.Code)
                || career.ServedDestinations.Contains(destination.Code)) return string.Empty;
            var goal = Pinned(career, fleet);
            var band = RouteAccess.BandOf(destination);
            return goal.Id switch
            {
                "prove-service" when band == RouteBand.Regional =>
                    "Career goal · a regional contract here can prove service",
                "regional-network" when band == RouteBand.Regional =>
                    "Career goal · a new regional destination counts",
                "domestic-network" when band is RouteBand.Domestic or RouteBand.National =>
                    "Career goal · a new domestic destination counts",
                "international-network" when band >= RouteBand.Tasman =>
                    "Career goal · a new international destination counts",
                "established-network" => "Career goal · a new destination counts",
                _ => string.Empty
            };
        }

        private static int CountBand(AirlineCareerState career, RouteBand band)
        {
            var count = 0;
            foreach (var code in career.ServedDestinations)
                if (DestinationCatalogue.TryFind(code, out var destination) && RouteAccess.BandOf(destination) == band)
                    count++;
            return count;
        }

        private static int CountDomestic(AirlineCareerState career)
        {
            var count = 0;
            foreach (var code in career.ServedDestinations)
                if (DestinationCatalogue.TryFind(code, out var destination)
                    && destination.State != "SA" && destination.State != "New Zealand"
                    && RouteAccess.BandOf(destination) >= RouteBand.Domestic
                    && RouteAccess.BandOf(destination) <= RouteBand.National)
                    count++;
            return count;
        }

        private static int CountInternational(AirlineCareerState career)
        {
            var count = 0;
            foreach (var code in career.ServedDestinations)
                if (DestinationCatalogue.TryFind(code, out var destination)
                    && RouteAccess.BandOf(destination) >= RouteBand.Tasman)
                    count++;
            return count;
        }

        private static int CountRegionalContracts(AirlineCareerState career)
        {
            var count = 0;
            foreach (var id in career.CompletedContractIds)
            {
                if (id == null) continue;
                if (id.StartsWith("REC-", StringComparison.Ordinal)) { count++; continue; }
                if (RouteContractCatalogue.TryFind(id, out var authored)
                    && RouteAccess.BandOf(authored.DestinationCode) == RouteBand.Regional)
                {
                    count++;
                    continue;
                }
                var parts = id.Split('-');
                if (parts.Length >= 5 && parts[0] == "MKT"
                    && RouteAccess.BandOf(parts[3]) == RouteBand.Regional)
                    count++;
            }
            return count;
        }

        private static bool OwnsWidebody(IReadOnlyList<AircraftType> fleet)
        {
            foreach (var type in fleet)
                if (type != null && AircraftCatalogue.IsWidebody(type)) return true;
            return false;
        }
    }
}

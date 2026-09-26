using System;
using System.Collections.Generic;
using System.Linq;
using Airside.Domain;

namespace Airside.Simulation
{
    /// <summary>A player-selected objective. Progress is measured from airline facts, not elapsed time.</summary>
    public readonly struct CareerGoalStatus
    {
        public CareerGoalStatus(string id, string title, OperatingTier stage, int progress, int target,
            string detail = null)
        {
            Id = id;
            Title = title;
            Stage = stage;
            Progress = Math.Min(Math.Max(0, progress), target);
            Target = target;
            Detail = detail ?? string.Empty;
        }

        public string Id { get; }
        public string Title { get; }

        /// <summary>The tier the goal is worked in. Completing every goal of a stage earns the next tier.</summary>
        public OperatingTier Stage { get; }
        public int Progress { get; }
        public int Target { get; }

        /// <summary>Optional extra fact shown beside the count (the margin goal's running total).</summary>
        public string Detail { get; }
        public bool Complete => Progress >= Target;
        public bool IsFinalStage => Stage == OperatingTier.International;

        /// <summary>What finishing this goal's stage earns: the next tier, or the established-airline finale.</summary>
        public string UnlocksLabel => IsFinalStage ? "Established airline" : (Stage + 1).ToString();
        public string ProgressText => string.IsNullOrEmpty(Detail) ? $"{Progress}/{Target}" : $"{Progress}/{Target} · {Detail}";
    }

    /// <summary>How far the current stage is toward the next tier (or the finale), for the career ring.</summary>
    public readonly struct CareerStageProgress
    {
        public CareerStageProgress(OperatingTier stage, int done, int total, bool finaleReached)
        {
            Stage = stage;
            Done = done;
            Total = total;
            FinaleReached = finaleReached;
        }

        public OperatingTier Stage { get; }
        public int Done { get; }
        public int Total { get; }
        public bool FinaleReached { get; }
        public float Fraction => Total <= 0 ? 1f : Math.Min(1f, Done / (float)Total);
        public string TargetLabel => FinaleReached ? "Sandbox"
            : Stage == OperatingTier.International ? "Established airline" : (Stage + 1).ToString();
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
            var margins = career.RecentServiceMargins.Count;
            var marginPositive = career.RecentOperatingMargin > 0;
            var marginProgress = marginPositive ? margins : Math.Min(margins, FinalMarginServices - 1);
            var marginDetail = margins == 0 ? string.Empty
                : (career.RecentOperatingMargin >= 0 ? "+$" : "−$") + Math.Abs(career.RecentOperatingMargin).ToString("N0");

            // Provisional → Regional: prove the airline can keep a commitment.
            goals.Add(new CareerGoalStatus("prove-service", "Fulfil a regional contract", OperatingTier.Provisional, regionalContracts, 1));
            goals.Add(new CareerGoalStatus("first-rotations", "Fly five services", OperatingTier.Provisional, career.CompletedPlayerRotations, 5));
            goals.Add(new CareerGoalStatus("regional-reliability", "Hold reliability at 70%+", OperatingTier.Provisional, rel, 70));
            // Regional → Domestic: a small regional network with its own base.
            goals.Add(new CareerGoalStatus("regional-network", "Serve four regional destinations", OperatingTier.Regional, regional, 4));
            goals.Add(new CareerGoalStatus("regional-fleet", "Operate three aircraft", OperatingTier.Regional, fleetCount, 3));
            goals.Add(new CareerGoalStatus("regional-base", "Expand the Adelaide base", OperatingTier.Regional,
                career.BaseLevel >= PlayerBaseLevel.ExpandedRegional ? 1 : 0, 1));
            goals.Add(new CareerGoalStatus("regional-service", "Fly 30 services", OperatingTier.Regional,
                career.CompletedPlayerRotations, 30));
            goals.Add(new CareerGoalStatus("domestic-reliability", "Hold reliability at 80%+", OperatingTier.Regional, rel, 80));
            // Domestic → International: jets, interstate cities and a second base.
            goals.Add(new CareerGoalStatus("domestic-network", "Serve three interstate destinations", OperatingTier.Domestic, domestic, 3));
            goals.Add(new CareerGoalStatus("domestic-jet", "Operate a jet", OperatingTier.Domestic, hasJet, 1));
            goals.Add(new CareerGoalStatus("domestic-base", "Open an outstation base", OperatingTier.Domestic,
                career.OutstationBases.Count, 1));
            goals.Add(new CareerGoalStatus("domestic-service", "Fly 75 services", OperatingTier.Domestic,
                career.CompletedPlayerRotations, 75));
            goals.Add(new CareerGoalStatus("international-reliability", "Hold reliability at 88%+", OperatingTier.Domestic, rel, 88));
            // International → established airline (the finale; see FinaleReady).
            goals.Add(new CareerGoalStatus("international-network", "Serve three international destinations",
                OperatingTier.International, international, FinalInternationalDestinations));
            goals.Add(new CareerGoalStatus("international-widebody", "Operate a widebody", OperatingTier.International, hasWidebody, 1));
            goals.Add(new CareerGoalStatus("international-bases", "Run three bases", OperatingTier.International,
                career.BaseCount, FinalBases));
            goals.Add(new CareerGoalStatus("established-fleet", "Operate 18 aircraft", OperatingTier.International,
                fleetCount, FinalFleet));
            goals.Add(new CareerGoalStatus("established-network", "Serve 12 destinations", OperatingTier.International,
                career.ServedDestinations.Count, FinalDestinations));
            goals.Add(new CareerGoalStatus("established-margin", "Stay profitable over 30 services",
                OperatingTier.International, marginProgress, FinalMarginServices, marginDetail));
            goals.Add(new CareerGoalStatus("established-reliability", "Hold reliability at 90%+", OperatingTier.International, rel, 90));
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

        /// <summary>
        /// The established-airline finale: International tier plus every International-stage goal,
        /// so the Career track and the ending can never disagree about what is left.
        /// </summary>
        public static bool FinaleReady(AirlineCareerState career, IReadOnlyList<AircraftType> fleet, int fleetCount = 0)
        {
            if (career == null || career.Tier < OperatingTier.International) return false;
            if (career.FinaleReached) return true;
            foreach (var goal in Evaluate(career, fleet, fleetCount))
                if (goal.Stage == OperatingTier.International && !goal.Complete)
                    return false;
            return true;
        }

        /// <summary>Goals done / total in the airline's current stage — the career ring.</summary>
        public static CareerStageProgress StageProgress(AirlineCareerState career,
            IReadOnlyList<AircraftType> fleet, int fleetCount = 0)
        {
            if (career == null) return new CareerStageProgress(OperatingTier.Provisional, 0, 0, false);
            var done = 0;
            var total = 0;
            foreach (var goal in Evaluate(career, fleet, fleetCount))
            {
                if (goal.Stage != career.Tier) continue;
                total++;
                if (goal.Complete) done++;
            }
            return new CareerStageProgress(career.Tier, done, total,
                career.FinaleReached);
        }

        /// <summary>
        /// The goal the HUD leads with: the player's pin while it is still open, otherwise the first
        /// open goal of the current stage. A finished pin releases itself, so the card never sits on
        /// "5/5". After the finale, the last goal (all complete) is returned.
        /// </summary>
        public static CareerGoalStatus Pinned(AirlineCareerState career, IReadOnlyList<AircraftType> fleet, int fleetCount = 0)
        {
            var goals = Evaluate(career, fleet, fleetCount);
            foreach (var goal in goals)
                if (goal.Id == career.PinnedGoalId && goal.Stage <= career.Tier && !goal.Complete)
                    return goal;
            foreach (var goal in goals)
                if (goal.Stage == career.Tier && !goal.Complete)
                    return goal;
            foreach (var goal in goals)
                if (goal.Stage <= career.Tier && !goal.Complete)
                    return goal;
            return goals.Count > 0 ? goals[goals.Count - 1] : default;
        }

        public static string RouteGuidance(AirlineCareerState career,
            IReadOnlyList<AircraftType> fleet, Destination destination, int fleetCount = 0)
        {
            if (career == null || string.IsNullOrEmpty(destination.Code)
                || career.ServedDestinations.Contains(destination.Code)) return string.Empty;
            var goal = Pinned(career, fleet, fleetCount);
            var band = RouteAccess.BandOf(destination);
            return goal.Id switch
            {
                "prove-service" when band == RouteBand.Regional =>
                    "Career goal · a regional contract here can prove service",
                "regional-network" when band == RouteBand.Regional =>
                    "Career goal · a new regional destination counts",
                "domestic-network" when band is RouteBand.Domestic or RouteBand.National =>
                    "Career goal · a new interstate destination counts",
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
                if (RouteAccess.IsInternational(code))
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

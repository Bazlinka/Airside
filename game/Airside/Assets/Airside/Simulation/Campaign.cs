using System;
using System.Collections.Generic;
using System.Linq;
using Airside.Domain;

namespace Airside.Simulation
{
    /// <summary>One thing a chapter asks for, and whether the airline has done it.</summary>
    public readonly struct CampaignGoal
    {
        public CampaignGoal(string text, bool done)
        {
            Text = text;
            Done = done;
        }

        public string Text { get; }
        public bool Done { get; }
    }

    /// <summary>
    /// How inspecting a destination relates to the chapter the player is currently working on.
    /// This is derived campaign truth: the map may present it, but never invents progression.
    /// </summary>
    public readonly struct CampaignRouteGuidance
    {
        internal CampaignRouteGuidance(bool advancesCurrentChapter, string text)
        {
            AdvancesCurrentChapter = advancesCurrentChapter;
            Text = text ?? string.Empty;
        }

        public bool AdvancesCurrentChapter { get; }
        public string Text { get; }

        public static CampaignRouteGuidance None => new(false, string.Empty);
    }

    /// <summary>A chapter of the career campaign, evaluated against the airline as it stands.</summary>
    public sealed class CampaignChapter
    {
        internal CampaignChapter(int number, string title, string story, long reward, IReadOnlyList<CampaignGoal> goals,
            bool complete, bool rewarded)
        {
            Number = number;
            Title = title;
            Story = story;
            Reward = reward;
            Goals = goals;
            Complete = complete;
            Rewarded = rewarded;
        }

        public int Number { get; }
        public string Title { get; }
        public string Story { get; }
        public long Reward { get; }
        public IReadOnlyList<CampaignGoal> Goals { get; }

        /// <summary>Every goal met, and every earlier chapter complete too.</summary>
        public bool Complete { get; }

        /// <summary>Its reward has been paid (once, ever).</summary>
        public bool Rewarded { get; }

        public int GoalsDone
        {
            get
            {
                var done = 0;
                foreach (var goal in Goals)
                    if (goal.Done)
                        done++;
                return done;
            }
        }

        /// <summary>"CHAPTER 2 · EYRE PENINSULA" — the objective card's caption.</summary>
        public string Caption => $"CHAPTER {Number} · {Title.ToUpperInvariant()}";
    }

    /// <summary>
    /// The career as a story in five chapters (ADR 0083), from one Saab and an island run to a
    /// widebody on international routes. Each chapter is a short checklist built only from
    /// what the airline has actually done — contracts fulfilled and where, tier, fleet,
    /// reliability — so it needs no save data of its own. A completed chapter pays its reward
    /// exactly once through the career's settlement keys, which saves already carry.
    /// </summary>
    public static class Campaign
    {
        public const int ChapterCount = 5;

        private static readonly string[] Interstate = { "MEL", "SYD", "CBR", "HBA", "BNE", "OOL", "PER", "ASP" };
        private static readonly string[] International = { "AKL", "CHC", "DPS", "SIN", "HKG", "KUL", "NAN", "DOH", "DXB" };

        public static string RewardKey(int chapter) => $"campaign:{chapter}";

        public static IReadOnlyList<CampaignChapter> Evaluate(AirlineCareerState career,
            IReadOnlyList<AircraftType> ownedTypes)
        {
            var chapters = new List<CampaignChapter>(ChapterCount);
            if (career == null)
                return chapters;
            ownedTypes ??= Array.Empty<AircraftType>();

            var destinations = FulfilledDestinations(career);
            var contracts = career.CompletedContractIds.Count;
            var fleet = ownedTypes.Count;
            var rotations = career.CompletedPlayerRotations;
            var reliability = career.Reliability;
            var tier = career.Tier;
            var ownsDash8 = Owns(ownedTypes, t => ReferenceEquals(t, AircraftType.Dash8Q400));
            var ownsJet = AirlineCareerState.OwnsAnyJet(ownedTypes);
            var ownsWidebody = Owns(ownedTypes, IsWidebody);

            var previousComplete = true;
            void Add(int number, string title, string story, long reward, params CampaignGoal[] goals)
            {
                var met = true;
                foreach (var goal in goals)
                    met &= goal.Done;
                var complete = met && previousComplete;
                chapters.Add(new CampaignChapter(number, title, story, reward, goals, complete,
                    career.ProcessedSettlementKeys.Contains(RewardKey(number))));
                previousComplete = complete;
            }

            Add(1, "Island Hopper", "One Saab, one island. Prove the airline can keep a promise.", 1_500,
                new CampaignGoal("Fulfil a Kingscote contract", destinations.Contains("KGC")),
                new CampaignGoal($"Fly 3 rotations ({Math.Min(rotations, 3)}/3)", rotations >= 3),
                new CampaignGoal($"Keep reliability at 60% or better ({reliability}%)", reliability >= 60));

            Add(2, "Eyre Peninsula", "Take the network across the gulf and grow the fleet.", 3_000,
                new CampaignGoal($"Fulfil contracts to 2 different towns ({Math.Min(destinations.Count, 2)}/2)",
                    destinations.Count >= 2),
                new CampaignGoal($"Own 2 aircraft ({Math.Min(fleet, 2)}/2)", fleet >= 2),
                new CampaignGoal("Reach the Regional tier", tier >= OperatingTier.Regional));

            Add(3, "Regional Network", "Become the airline country South Australia relies on.", 6_000,
                new CampaignGoal($"Fulfil contracts to 4 different towns ({Math.Min(destinations.Count, 4)}/4)",
                    destinations.Count >= 4),
                new CampaignGoal("Own a Dash 8-400", ownsDash8),
                new CampaignGoal($"Fly 18 rotations ({Math.Min(rotations, 18)}/18)", rotations >= 18),
                new CampaignGoal($"Keep reliability at 80% or better ({reliability}%)", reliability >= 80));

            Add(4, "Interstate", "Jets, capital cities and the big carriers' routes.", 12_000,
                new CampaignGoal("Reach the Domestic tier", tier >= OperatingTier.Domestic),
                new CampaignGoal("Own a jet", ownsJet),
                new CampaignGoal("Fulfil an interstate contract", ContainsAny(destinations, Interstate)),
                new CampaignGoal($"Fulfil 6 contracts in all ({Math.Min(contracts, 6)}/6)", contracts >= 6));

            Add(5, "Going Global", "A widebody on the long-haul board: Adelaide to the world.", 25_000,
                new CampaignGoal("Reach the International tier", tier >= OperatingTier.International),
                new CampaignGoal("Own a widebody", ownsWidebody),
                new CampaignGoal("Fulfil an international contract", ContainsAny(destinations, International)),
                new CampaignGoal($"Keep reliability at 90% or better ({reliability}%)", reliability >= 90));

            return chapters;
        }

        /// <summary>The chapter being played: the first not yet complete, or the last one.</summary>
        public static CampaignChapter Current(IReadOnlyList<CampaignChapter> chapters)
        {
            if (chapters == null || chapters.Count == 0)
                return null;
            foreach (var chapter in chapters)
                if (!chapter.Complete)
                    return chapter;
            return chapters[chapters.Count - 1];
        }

        /// <summary>
        /// Explains whether a route would move the current campaign chapter forward. The route
        /// itself does not settle progress: the relevant authored contract still has to be
        /// accepted and fulfilled.
        /// </summary>
        public static CampaignRouteGuidance RouteGuidance(AirlineCareerState career,
            IReadOnlyList<AircraftType> ownedTypes, Destination destination)
        {
            if (career == null)
                return CampaignRouteGuidance.None;

            var chapter = Current(Evaluate(career, ownedTypes));
            if (chapter == null || chapter.Complete)
                return CampaignRouteGuidance.None;

            var completed = FulfilledDestinations(career);
            var code = destination.Code ?? string.Empty;
            switch (chapter.Number)
            {
                case 1 when code == "KGC" && !completed.Contains("KGC"):
                    return new CampaignRouteGuidance(true,
                        "Chapter 1 target · fulfil the Kingscote contract");
                case 2 when RouteAccess.BandOf(destination) == RouteBand.Regional && !completed.Contains(code):
                    return new CampaignRouteGuidance(true,
                        "Chapter 2 progress · a new regional contract here would count");
                case 3 when RouteAccess.BandOf(destination) == RouteBand.Regional && !completed.Contains(code):
                    return new CampaignRouteGuidance(true,
                        "Chapter 3 progress · a new regional contract here would count");
                case 4 when !ContainsAny(completed, Interstate) && Interstate.Contains(code):
                    return new CampaignRouteGuidance(true,
                        "Chapter 4 target · an interstate contract here would count");
                case 5 when !ContainsAny(completed, International) && International.Contains(code):
                    return new CampaignRouteGuidance(true,
                        "Chapter 5 target · an international contract here would count");
                default:
                    return CampaignRouteGuidance.None;
            }
        }

        public static bool IsWidebody(AircraftType type) =>
            type != null && type.Id is "A359" or "B78X" or "B789" or "A339";

        private static HashSet<string> FulfilledDestinations(AirlineCareerState career)
        {
            var codes = new HashSet<string>(StringComparer.Ordinal);
            foreach (var id in career.CompletedContractIds)
                if (career.TryFindDefinition(id, out var definition) && !string.IsNullOrEmpty(definition.DestinationCode))
                    codes.Add(definition.DestinationCode);
            foreach (var record in career.ContractHistory)
                if (!string.IsNullOrEmpty(record.DestinationCode))
                    codes.Add(record.DestinationCode);
            return codes;
        }

        private static bool Owns(IReadOnlyList<AircraftType> types, Func<AircraftType, bool> match)
        {
            foreach (var type in types)
                if (match(type))
                    return true;
            return false;
        }

        private static bool ContainsAny(HashSet<string> codes, string[] wanted)
        {
            foreach (var code in wanted)
                if (codes.Contains(code))
                    return true;
            return false;
        }
    }
}

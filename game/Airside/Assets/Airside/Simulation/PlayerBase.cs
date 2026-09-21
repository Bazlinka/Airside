using System;
using System.Collections.Generic;
using Airside.Domain;

namespace Airside.Simulation
{
    public enum PlayerBaseLevel { Starter = 0, ExpandedRegional = 1, JetGate = 2, International = 3 }

    public readonly struct PlayerBaseSpec
    {
        public PlayerBaseSpec(PlayerBaseLevel level, string title, string detail, int fleetCapacity,
            long upgradeCost, int requiredRotations, OperatingTier requiredTier, bool terminalJets, bool widebodies)
        {
            Level = level; Title = title ?? string.Empty; Detail = detail ?? string.Empty;
            FleetCapacity = fleetCapacity; UpgradeCost = upgradeCost; RequiredRotations = requiredRotations;
            RequiredTier = requiredTier; TerminalJets = terminalJets; Widebodies = widebodies;
        }
        public PlayerBaseLevel Level { get; }
        public string Title { get; }
        public string Detail { get; }
        public int FleetCapacity { get; }
        public long UpgradeCost { get; }
        public int RequiredRotations { get; }
        public OperatingTier RequiredTier { get; }
        public bool TerminalJets { get; }
        public bool Widebodies { get; }
    }

    /// <summary>The player's leased Adelaide operating footprint (ADR 0091), not Adelaide Airport itself.</summary>
    public static class PlayerBase
    {
        public static PlayerBaseSpec For(PlayerBaseLevel level) => level switch
        {
            PlayerBaseLevel.ExpandedRegional => new PlayerBaseSpec(level,
                "Expanded regional base", "3 aircraft · regional apron and maintenance space",
                3, 1_500, 4, OperatingTier.Provisional, false, false),
            PlayerBaseLevel.JetGate => new PlayerBaseSpec(level,
                "Jet-gate base", "5 aircraft · terminal-gate jet handling",
                5, 8_000, 12, OperatingTier.Regional, true, false),
            PlayerBaseLevel.International => new PlayerBaseSpec(level,
                "International base", "6 aircraft · widebody and long-haul handling",
                6, 20_000, 24, OperatingTier.Domestic, true, true),
            _ => new PlayerBaseSpec(PlayerBaseLevel.Starter,
                "Regional starter base", "1 aircraft · regional apron operation",
                1, 0, 0, OperatingTier.Provisional, false, false)
        };

        public static bool TryNext(PlayerBaseLevel current, out PlayerBaseSpec next)
        {
            if (current >= PlayerBaseLevel.International) { next = default; return false; }
            next = For((PlayerBaseLevel)((int)current + 1));
            return true;
        }

        public static bool Supports(PlayerBaseLevel level, AircraftType type)
        {
            if (type == null) return false;
            var spec = For(level);
            if (AircraftCatalogue.IsWidebody(type)) return spec.Widebodies;
            if (AirlineOperations.NeedsTerminalGate(type)) return spec.TerminalJets;
            return true;
        }

        public static PlayerBaseLevel MinimumFor(IEnumerable<AircraftType> ownedTypes, int ownedCount)
        {
            var level = ownedCount switch
            {
                >= 6 => PlayerBaseLevel.International,
                >= 4 => PlayerBaseLevel.JetGate,
                >= 2 => PlayerBaseLevel.ExpandedRegional,
                _ => PlayerBaseLevel.Starter
            };
            if (ownedTypes == null) return level;
            foreach (var type in ownedTypes)
            {
                if (type == null) continue;
                if (AircraftCatalogue.IsWidebody(type)) return PlayerBaseLevel.International;
                if (AirlineOperations.NeedsTerminalGate(type)) level = PlayerBaseLevel.JetGate;
            }
            return level;
        }

        public static string Requirement(PlayerBaseSpec next, AirlineCareerState career)
        {
            if (career == null) return "No career.";
            var parts = new List<string>();
            if (career.Tier < next.RequiredTier) parts.Add($"{next.RequiredTier} tier");
            if (career.CompletedPlayerRotations < next.RequiredRotations)
            {
                var remaining = next.RequiredRotations - career.CompletedPlayerRotations;
                parts.Add($"{remaining} more rotation" + (remaining == 1 ? "" : "s"));
            }
            if (career.Funds < next.UpgradeCost)
                parts.Add("$" + (next.UpgradeCost - career.Funds).ToString("N0") + " more funds");
            return parts.Count == 0 ? "Ready to expand." : "Needs " + string.Join(", ", parts) + ".";
        }
    }
}

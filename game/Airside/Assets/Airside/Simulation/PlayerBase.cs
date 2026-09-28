using System;
using System.Collections.Generic;
using Airside.Domain;

namespace Airside.Simulation
{
    public enum PlayerBaseLevel { Starter = 0, ExpandedRegional = 1, JetGate = 2, International = 3 }

    public readonly struct PlayerBaseSpec
    {
        public PlayerBaseSpec(PlayerBaseLevel level, string title, string detail, int fleetCapacity,
            long upgradeCost, int requiredRotations, OperatingTier requiredTier, bool terminalJets, bool widebodies,
            int requiredReliability = 0)
        {
            RequiredReliability = requiredReliability;
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

        /// <summary>Reliability needed to open this base (ADR 0139).</summary>
        public int RequiredReliability { get; }
        public bool TerminalJets { get; }
        public bool Widebodies { get; }
    }

    /// <summary>The player's leased Adelaide operating footprint (ADR 0091), not Adelaide Airport itself.</summary>
    public static class PlayerBase
    {
        /// <summary>True when every gate but money is met (ADR 0139).</summary>
        public static bool GatesMet(PlayerBaseSpec next, AirlineCareerState career) =>
            career != null && career.Tier >= next.RequiredTier
                           && career.CompletedPlayerRotations >= next.RequiredRotations
                           && career.Reliability >= next.RequiredReliability;

        private static readonly StableId[] StarterRegionalStands =
        {
            new("BAY-1"), new("BAY-7")
        };

        private static readonly StableId[] ExpandedRegionalStands =
        {
            new("BAY-1"), new("BAY-7"), new("BAY-10A")
        };

        private static readonly StableId[] JetGateStands =
        {
            new("GATE-27"), new("GATE-29")
        };

        // 28L last: it is the only widebody line, so narrowbodies take it only when nothing
        // else is free — otherwise the player's own 737 locked their 787 out (ADR 0125).
        private static readonly StableId[] InternationalGateStands =
        {
            new("GATE-27"), new("GATE-29"), new("GATE-28R"), new("GATE-28L")
        };

        // 28L is the pier's code E centre line; 28R is a code C narrowbody line (ADR 0110).
        private static readonly StableId[] WidebodyGateStands =
        {
            new("GATE-28L")
        };

        public static PlayerBaseSpec For(PlayerBaseLevel level) => level switch
        {
            PlayerBaseLevel.ExpandedRegional => new PlayerBaseSpec(level,
                "Expanded regional base", "3 aircraft · regional apron and maintenance space",
                3, 1_500, 4, OperatingTier.Provisional, false, false, requiredReliability: 75),
            PlayerBaseLevel.JetGate => new PlayerBaseSpec(level,
                "Jet-gate base", "5 aircraft · terminal-gate jet handling",
                5, 6_000, 16, OperatingTier.Regional, true, false, requiredReliability: 80),
            PlayerBaseLevel.International => new PlayerBaseSpec(level,
                "International base", "6 aircraft · widebody and long-haul handling",
                6, 20_000, 50, OperatingTier.Domestic, true, true, requiredReliability: 88),
            _ => new PlayerBaseSpec(PlayerBaseLevel.Starter,
                "Regional starter base", "2 aircraft · a second Saab from the opening cash",
                2, 0, 0, OperatingTier.Provisional, false, false)
        };

        public static bool TryNext(PlayerBaseLevel current, out PlayerBaseSpec next)
        {
            if (current >= PlayerBaseLevel.International) { next = default; return false; }
            next = For((PlayerBaseLevel)((int)current + 1));
            return true;
        }

        /// <summary>
        /// The smallest Adelaide base that can operate this type. A second Saab fits the starter
        /// base; anything larger waits for the expanded regional apron (ADR 0164).
        /// </summary>
        public static PlayerBaseLevel RequiredLevel(AircraftType type)
        {
            if (type == null)
                return PlayerBaseLevel.Starter;
            if (AircraftCatalogue.IsWidebody(type))
                return PlayerBaseLevel.International;
            if (AirlineOperations.NeedsTerminalGate(type))
                return PlayerBaseLevel.JetGate;
            if (type.Id != AircraftType.Saab340.Id)
                return PlayerBaseLevel.ExpandedRegional;
            return PlayerBaseLevel.Starter;
        }

        public static bool Supports(PlayerBaseLevel level, AircraftType type) =>
            type != null && level >= RequiredLevel(type);

        /// <summary>
        /// Dedicated Adelaide positions leased with the base. Regional aircraft may still use
        /// other compatible bays once Expanded Regional is open; the dedicated positions stay
        /// protected from AI so the player always retains a real home footprint. Jets are
        /// restricted to their leased gates so terminal growth is a physical capability.
        /// </summary>
        public static IReadOnlyList<StableId> DedicatedStands(PlayerBaseLevel level, AircraftType type)
        {
            if (type == null)
                return Array.Empty<StableId>();
            if (!AirlineOperations.NeedsTerminalGate(type))
                return level >= PlayerBaseLevel.ExpandedRegional ? ExpandedRegionalStands : StarterRegionalStands;
            if (level < PlayerBaseLevel.JetGate)
                return Array.Empty<StableId>();
            if (AircraftCatalogue.IsWidebody(type))
                return level >= PlayerBaseLevel.International ? WidebodyGateStands : Array.Empty<StableId>();
            return level >= PlayerBaseLevel.International ? InternationalGateStands : JetGateStands;
        }

        public static bool IsDedicatedStand(PlayerBaseLevel level, StableId stand)
        {
            if (string.IsNullOrEmpty(stand.Value))
                return false;
            foreach (var candidate in ExpandedRegionalStands)
                if (level >= PlayerBaseLevel.ExpandedRegional && candidate.Equals(stand))
                    return true;
            if (level == PlayerBaseLevel.Starter)
                foreach (var candidate in StarterRegionalStands)
                    if (candidate.Equals(stand))
                        return true;
            if (level >= PlayerBaseLevel.JetGate)
                foreach (var candidate in JetGateStands)
                    if (candidate.Equals(stand))
                        return true;
            if (level >= PlayerBaseLevel.International)
                foreach (var candidate in InternationalGateStands)
                    if (candidate.Equals(stand))
                        return true;
            return false;
        }

        public static bool CanUseStand(PlayerBaseLevel level, AircraftType type, StableId stand)
        {
            if (type == null || string.IsNullOrEmpty(stand.Value) || !Supports(level, type))
                return false;

            if (!AirlineOperations.NeedsTerminalGate(type))
            {
                if (level == PlayerBaseLevel.Starter)
                {
                    foreach (var candidate in StarterRegionalStands)
                        if (candidate.Equals(stand))
                            return true;
                    return false;
                }
                return !AdelaideGround.IsTerminalGate(stand);
            }

            foreach (var candidate in DedicatedStands(level, type))
                if (candidate.Equals(stand))
                    return true;
            return false;
        }

        public static string StandAccessLine(PlayerBaseLevel level)
        {
            var regional = level >= PlayerBaseLevel.ExpandedRegional ? "50D · 50G · 10A + shared regional apron" : "50D · 50G";
            return level switch
            {
                PlayerBaseLevel.JetGate => regional + " · gates 27/29",
                PlayerBaseLevel.International => regional + " · gates 27/29 · pier 28",
                _ => regional
            };
        }

        public static bool HasLocalMaintenance(PlayerBaseLevel level, AircraftType type)
        {
            if (type == null || level < PlayerBaseLevel.ExpandedRegional)
                return false;
            if (!AirlineOperations.NeedsTerminalGate(type))
                return true;
            if (AircraftCatalogue.IsWidebody(type))
                return level >= PlayerBaseLevel.International;
            return level >= PlayerBaseLevel.JetGate;
        }

        public static string MaintenanceLine(PlayerBaseLevel level, AircraftType type)
        {
            if (type == null)
                return string.Empty;
            if (HasLocalMaintenance(level, type))
                return AircraftCatalogue.IsWidebody(type)
                    ? "Local widebody maintenance"
                    : AirlineOperations.NeedsTerminalGate(type)
                        ? "Local jet maintenance"
                        : "Local regional maintenance";
            return "Maintenance outsourced";
        }

        public static int TurnaroundSpeedGainPercent(PlayerBaseLevel level) => level switch
        {
            PlayerBaseLevel.International => 30,
            PlayerBaseLevel.JetGate => 20,
            PlayerBaseLevel.ExpandedRegional => 10,
            _ => 0
        };

        public static string TurnaroundLine(PlayerBaseLevel level)
        {
            var gain = TurnaroundSpeedGainPercent(level);
            return gain <= 0 ? "standard turnaround speed" : gain + "% faster turnarounds";
        }

        public static string MaintenanceCapabilityLine(PlayerBaseLevel level) => level switch
        {
            PlayerBaseLevel.International => "local widebody maintenance",
            PlayerBaseLevel.JetGate => "local jet maintenance",
            PlayerBaseLevel.ExpandedRegional => "local regional maintenance",
            _ => "outsourced maintenance"
        };

        public static string UpgradeBenefitLine(PlayerBaseLevel level) => level switch
        {
            PlayerBaseLevel.International => "pier 28 · local widebody maintenance · 30% faster turns",
            PlayerBaseLevel.JetGate => "gates 27/29 · local jet maintenance · 20% faster turns",
            PlayerBaseLevel.ExpandedRegional => "regional apron · local regional maintenance · 10% faster turns",
            _ => "50D · 50G · outsourced maintenance · baseline turns"
        };

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
            if (career.Reliability < next.RequiredReliability) parts.Add($"{next.RequiredReliability}% reliability");
            if (career.CompletedPlayerRotations < next.RequiredRotations)
            {
                var remaining = next.RequiredRotations - career.CompletedPlayerRotations;
                parts.Add($"{remaining} more flight" + (remaining == 1 ? "" : "s"));
            }
            if (career.Funds < next.UpgradeCost)
                parts.Add("$" + (next.UpgradeCost - career.Funds).ToString("N0") + " more");
            return parts.Count == 0 ? "Ready to expand." : "Needs " + string.Join(", ", parts) + ".";
        }
    }
}

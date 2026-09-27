using System;
using System.Collections.Generic;
using Airside.Domain;

namespace Airside.Simulation
{
    /// <summary>
    /// Rotating player contract offers (ADR 0056). Pure function of simulation time and the
    /// airline's owned types / reliability — does not consume the operations RNG, so AI
    /// traffic stays on its own sequence.
    /// </summary>
    public static class ContractMarket
    {
        public const long WindowSeconds = 6 * 3600;
        public const int OffersPerWindow = 3;
        public const int RegionalReliabilityFloor = 0;
        public const int DomesticReliabilityFloor = 70;

        private static readonly string[] Regional = { "KGC", "PLO", "WYA", "MGB", "CED", "CPD", "MQL", "BHQ" };
        private static readonly string[] Domestic = { "MEL", "CBR", "SYD", "HBA" };
        private static readonly string[] National = { "BNE", "OOL", "ASP", "PER", "CNS", "DRW" };
        private static readonly string[] Tasman = { "AKL", "CHC" };
        private static readonly string[] LongHaul = { "DPS", "SIN", "HKG", "KUL", "NAN", "DOH", "DXB" };

        public static IReadOnlyList<RouteContractDefinition> At(
            SimulationTime now, IReadOnlyList<AircraftType> ownedTypes, int reliability, OperatingTier tier)
        {
            var window = now.ElapsedSeconds < 0 ? 0 : now.ElapsedSeconds / WindowSeconds;
            var offers = new List<RouteContractDefinition>(OffersPerWindow);
            if (ownedTypes == null || ownedTypes.Count == 0)
                return offers;

            var rng = new SeededRandomSource((uint)(window * 1_000_003 + 97));
            var attempts = 0;
            while (offers.Count < OffersPerWindow && attempts < 24)
            {
                attempts++;
                var type = ownedTypes[rng.NextInt(0, ownedTypes.Count)];
                var dest = PickDestination(type, reliability, tier, rng);
                if (dest == null)
                    continue;
                var km = DestinationCatalogue.Adelaide.DistanceKmTo(dest.Value);
                if (!type.CanReach(km) || !RouteAccess.Allows(type, dest.Value))
                    continue;

                var basePay = FlightEconomics.FlightPay(type, km, RouteAccess.BandOf(dest.Value));
                var terms = Terms(KindFor(type, dest.Value, rng), basePay, rng);
                var id = $"MKT-{window}-{offers.Count}-{dest.Value.Code}-{type.Id}{terms.Suffix}";
                var duplicate = false;
                foreach (var existing in offers)
                    if (existing.DestinationCode == dest.Value.Code && existing.EligibleType.Id == type.Id)
                        duplicate = true;
                if (duplicate)
                    continue;

                offers.Add(new RouteContractDefinition(
                    id, "ADL", dest.Value.Code, type, terms.Rotations, terms.Bonus, terms.Reward,
                    reliabilityGainPerRotation: terms.Gain,
                    requiredTier: OperatingTier.Provisional,
                    reliabilityLossOnCancel: terms.Loss,
                    kind: terms.Kind,
                    deadlineSeconds: terms.Deadline));
            }

            return offers;
        }

        /// <summary>
        /// ADR 0127: the market is not only "fly A–B N times". About one offer in six is a charter (one
        /// well-paid flight, due within 6 h), one in ten an urgent medical flight to a regional town
        /// (3 h, a big reliability gain), and turboprops also see freight runs. Scheduled work now
        /// has a generous deadline too, so an accepted contract is a commitment.
        /// </summary>
        public static ContractKind KindFor(AircraftType type, Destination destination, SeededRandomSource rng)
        {
            var roll = rng.NextInt(0, 100);
            var turboprop = AircraftCatalogue.For(type).StandClass == StandClass.RegionalBay;
            if (roll < 16)
                return ContractKind.Charter;
            if (roll < 26 && turboprop && RouteAccess.BandOf(destination) == RouteBand.Regional)
                return ContractKind.Medical;
            if (roll < 42 && turboprop)
                return ContractKind.Freight;
            return ContractKind.Scheduled;
        }

        public readonly struct ContractTerms
        {
            public ContractTerms(ContractKind kind, int rotations, long bonus, long reward, int gain, int loss, long deadline,
                string suffix)
            {
                Kind = kind;
                Rotations = rotations;
                Bonus = bonus;
                Reward = reward;
                Gain = gain;
                Loss = loss;
                Deadline = deadline;
                Suffix = suffix;
            }

            public ContractKind Kind { get; }
            public int Rotations { get; }
            public long Bonus { get; }
            public long Reward { get; }
            public int Gain { get; }
            public int Loss { get; }
            public long Deadline { get; }
            public string Suffix { get; }
        }

        public static ContractTerms Terms(ContractKind kind, long basePay, SeededRandomSource rng)
        {
            switch (kind)
            {
                case ContractKind.Charter:
                    return new ContractTerms(kind, 1, Math.Max(300, (long)Math.Round(basePay * 1.3)),
                        Math.Max(200, basePay / 2), 1, 4, 6 * 3600, "-CH");
                case ContractKind.Medical:
                    return new ContractTerms(kind, 1, Math.Max(250, (long)Math.Round(basePay * 0.8)),
                        Math.Max(150, (long)Math.Round(basePay * 0.4)), 4, 5, 3 * 3600, "-MED");
                case ContractKind.Freight:
                {
                    var rotations = 2 + rng.NextInt(0, 2);
                    var bonus = Math.Max(180, (long)Math.Round(basePay * 0.45));
                    return new ContractTerms(kind, rotations, bonus, bonus * rotations * 6 / 10, 1, 2,
                        rotations * 12 * 3600L, "-FRT");
                }
                default:
                {
                    var rotations = 2 + rng.NextInt(0, 3);
                    var bonus = Math.Max(200, (long)Math.Round(basePay * 0.55));
                    return new ContractTerms(ContractKind.Scheduled, rotations, bonus, bonus * rotations / 2, 1, 3,
                        rotations * 10 * 3600L, string.Empty);
                }
            }
        }

        public static SimulationTime WindowEnd(SimulationTime now)
        {
            var window = now.ElapsedSeconds < 0 ? 0 : now.ElapsedSeconds / WindowSeconds;
            return new SimulationTime((window + 1) * WindowSeconds);
        }

        private static Destination? PickDestination(AircraftType type, int reliability, OperatingTier tier,
            SeededRandomSource rng)
        {
            var ceiling = RouteAccess.Ceiling(type);
            // International routes need the International tier, the same rule as filing one.
            if (tier < OperatingTier.International && ceiling > RouteBand.National)
                ceiling = RouteBand.National;
            if (reliability < DomesticReliabilityFloor && ceiling > RouteBand.Regional)
                ceiling = RouteBand.Regional;

            string[] pool;
            switch (ceiling)
            {
                case RouteBand.LongHaul:
                    pool = rng.NextInt(0, 4) == 0 ? LongHaul : National;
                    break;
                case RouteBand.Tasman:
                    pool = rng.NextInt(0, 3) == 0 ? Tasman : Domestic;
                    break;
                case RouteBand.National:
                    pool = rng.NextInt(0, 2) == 0 ? National : Domestic;
                    break;
                case RouteBand.Domestic:
                    pool = rng.NextInt(0, 3) == 0 ? Domestic : Regional;
                    break;
                default:
                    pool = Regional;
                    break;
            }

            var code = pool[rng.NextInt(0, pool.Length)];
            return DestinationCatalogue.TryFind(code, out var dest) ? dest : null;
        }
    }
}

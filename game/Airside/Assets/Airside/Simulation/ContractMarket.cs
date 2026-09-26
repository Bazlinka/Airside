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

                var rotations = 2 + rng.NextInt(0, 3);
                var basePay = FlightEconomics.FlightPay(type, km, RouteAccess.BandOf(dest.Value));
                var bonus = Math.Max(200, (long)Math.Round(basePay * 0.55));
                var reward = bonus * rotations / 2;
                var id = $"MKT-{window}-{offers.Count}-{dest.Value.Code}-{type.Id}";
                var duplicate = false;
                foreach (var existing in offers)
                    if (existing.DestinationCode == dest.Value.Code && existing.EligibleType.Id == type.Id)
                        duplicate = true;
                if (duplicate)
                    continue;

                offers.Add(new RouteContractDefinition(
                    id, "ADL", dest.Value.Code, type, rotations, bonus, reward,
                    reliabilityGainPerRotation: 1,
                    requiredTier: OperatingTier.Provisional,
                    reliabilityLossOnCancel: 3));
            }

            return offers;
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

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
        private static readonly string[] Pacific = { "NAN", "NOU", "POM", "DPS" };
        private static readonly string[] LongHaul =
        {
            "SIN", "HKG", "KUL", "CGK", "BKK", "SGN", "MNL", "PVG", "ICN", "KIX", "NRT", "HNL", "LAX", "DOH", "DXB"
        };

        /// <summary>A medical call has to be flown within this, so it only goes where that is possible.</summary>
        public const long MedicalDeadlineSeconds = 3 * 3600;

        /// <summary>One window in this many (once a day) always has a medical call for a turboprop owner.</summary>
        public const int MedicalEveryWindows = 4;

        public static IReadOnlyList<RouteContractDefinition> At(
            SimulationTime now, IReadOnlyList<AircraftType> ownedTypes, int reliability, OperatingTier tier) =>
            At(now, ownedTypes, reliability, tier, PlayerBaseLevel.Starter);

        public static IReadOnlyList<RouteContractDefinition> At(
            SimulationTime now, IReadOnlyList<AircraftType> ownedTypes, int reliability, OperatingTier tier,
            PlayerBaseLevel baseLevel)
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
                var kind = KindFor(type, dest.Value, rng);
                // ADR 0138: a medical call the aircraft cannot fly in 3 hours is offered as a charter.
                if (kind == ContractKind.Medical && !MedicalFits(type, dest.Value, baseLevel))
                    kind = ContractKind.Charter;
                var terms = FitDeadline(Terms(kind, basePay, rng), type, dest.Value, baseLevel);
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

            if (window % MedicalEveryWindows == 0)
                GuaranteeMedical(offers, window, ownedTypes, baseLevel, rng);
            return offers;
        }

        /// <summary>A medical call is flown out and back within 3 hours, with the market's margin.</summary>
        public static bool MedicalFits(AircraftType type, Destination destination, PlayerBaseLevel baseLevel) =>
            ContractFeasibility.MinimumSeconds(type, destination, 1, baseLevel) * ContractFeasibility.Margin
            <= MedicalDeadlineSeconds;

        /// <summary>
        /// ADR 0138: every deadline leaves the margin over the fastest possible flying. A deadline too
        /// tight for the aircraft (a widebody charter to Dubai "within 6 h") is lengthened, never offered
        /// impossible. Medical calls keep their 3 hours: they are only drawn where that fits.
        /// </summary>
        public static ContractTerms FitDeadline(ContractTerms terms, AircraftType type, Destination destination,
            PlayerBaseLevel baseLevel)
        {
            if (terms.Deadline <= 0 || terms.Kind == ContractKind.Medical)
                return terms;
            var fair = ContractFeasibility.FairDeadlineSeconds(type, destination, terms.Rotations, baseLevel);
            return fair <= terms.Deadline
                ? terms
                : new ContractTerms(terms.Kind, terms.Rotations, terms.Bonus, terms.Reward, terms.Gain, terms.Loss,
                    fair, terms.Suffix);
        }

        /// <summary>
        /// Once a day a turboprop owner is offered a medical call (ADR 0138), so the "fly 2 medical
        /// calls" challenge never waits on luck. It replaces the last offer.
        /// </summary>
        private static void GuaranteeMedical(List<RouteContractDefinition> offers, long window,
            IReadOnlyList<AircraftType> ownedTypes, PlayerBaseLevel baseLevel, SeededRandomSource rng)
        {
            foreach (var offer in offers)
                if (offer.Kind == ContractKind.Medical)
                    return;
            AircraftType turboprop = null;
            foreach (var type in ownedTypes)
                if (AircraftCatalogue.For(type).StandClass == StandClass.RegionalBay)
                {
                    turboprop = type;
                    break;
                }

            if (turboprop == null)
                return;
            var start = rng.NextInt(0, Regional.Length);
            for (var i = 0; i < Regional.Length; i++)
            {
                if (!DestinationCatalogue.TryFind(Regional[(start + i) % Regional.Length], out var town))
                    continue;
                var km = DestinationCatalogue.Adelaide.DistanceKmTo(town);
                if (!turboprop.CanReach(km) || !RouteAccess.Allows(turboprop, town)
                    || !MedicalFits(turboprop, town, baseLevel))
                    continue;
                var terms = Terms(ContractKind.Medical, FlightEconomics.FlightPay(turboprop, km, RouteBand.Regional), rng);
                var index = Math.Min(offers.Count, OffersPerWindow - 1);
                var medical = new RouteContractDefinition(
                    $"MKT-{window}-{index}-{town.Code}-{turboprop.Id}{terms.Suffix}", "ADL", town.Code, turboprop,
                    terms.Rotations, terms.Bonus, terms.Reward, reliabilityGainPerRotation: terms.Gain,
                    requiredTier: OperatingTier.Provisional, reliabilityLossOnCancel: terms.Loss, kind: terms.Kind,
                    deadlineSeconds: terms.Deadline);
                if (index < offers.Count)
                    offers[index] = medical;
                else
                    offers.Add(medical);
                return;
            }
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
                    pool = rng.NextInt(0, 4) switch { 0 => LongHaul, 1 => Pacific, _ => National };
                    break;
                case RouteBand.Pacific:
                    pool = rng.NextInt(0, 4) switch { 0 => Pacific, 1 => Tasman, 2 => National, _ => Domestic };
                    break;
                case RouteBand.Tasman:
                    // ADR 0138: a Tasman-capable jet also flies the long national legs.
                    pool = rng.NextInt(0, 3) switch { 0 => Tasman, 1 => National, _ => Domestic };
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

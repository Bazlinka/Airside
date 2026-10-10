using System;
using Airside.Domain;

namespace Airside.Simulation
{
    /// <summary>
    /// Economy v2 cost and revenue model in real Australian dollars (ADR 2026-10-09-economy-v2-real-dollar-scale).
    /// Pure functions of aircraft type, leg length, route band and fuel price: no clock, no randomness, no Unity types.
    /// Not yet wired into <see cref="FlightEconomics"/>; the live game still uses the game-dollar formulas until the
    /// save migration lands.
    ///
    /// Source tiers refer to docs/data/AIRLINE_OPERATING_COSTS.md. Tier A (airport and Airservices charges) is quoted from
    /// the charging bodies. Everything marked "design" is a tier D placeholder that must be replaced or kept as a labelled
    /// design constant before it ships; the tests pin behaviour (ordering, affordability), not those exact numbers.
    /// </summary>
    public static class FlightCostModel
    {
        // ---- Tier A: Airservices Australia determination (effective 1 Aug 2025), Adelaide ------------------------------

        /// <summary>Terminal navigation, A$ per tonne of Chargeable Weight per landing at Adelaide.</summary>
        public const double TerminalNavPerTonne = 12.78;
        /// <summary>Minimum terminal navigation charge per activity at Adelaide.</summary>
        public const double TerminalNavMinimum = 21.00;
        /// <summary>En route charge rates per 100 km: up to 20 t uses weight, 20 t or more uses the square root of weight.</summary>
        public const double EnRouteLightRate = 0.90;
        public const double EnRouteHeavyRate = 4.04;
        /// <summary>ARFF applies to aircraft of at least this Chargeable Weight (tonnes).</summary>
        public const double ArffThresholdTonnes = 5.7;

        // ---- Tier A: Adelaide Airport Ltd schedule, effective 1 Jul 2026 (GST-inclusive, government fee excluded) -------

        /// <summary>Per-passenger airport fee (landing + terminal) for one arrival or one departure, by service.</summary>
        public static double PassengerFee(RouteBand band) => band switch
        {
            RouteBand.Regional => 6.13,
            RouteBand.Domestic or RouteBand.National => 14.71,
            _ => 38.85
        };

        /// <summary>
        /// Airservices ARFF rate (A$ per tonne) at Adelaide. The four printed columns are 2.46, 3.46, 5.59 and 8.87; which
        /// aircraft category each belongs to has not been verified, so this maps them by weight (design, tier D).
        /// </summary>
        public static double ArffPerTonne(double tonnes) =>
            tonnes < ArffThresholdTonnes ? 0
            : tonnes <= 30 ? 2.46
            : tonnes <= 80 ? 3.46
            : tonnes <= 230 ? 5.59
            : 8.87;

        public static double TerminalNavigation(double tonnes) =>
            Math.Max(TerminalNavMinimum, tonnes * TerminalNavPerTonne);

        public static double Arff(double tonnes) => tonnes * ArffPerTonne(tonnes);

        public static double EnRoute(double tonnes, double km)
        {
            var distance = Math.Max(0, km) / 100.0;
            return tonnes < 20.0
                ? EnRouteLightRate * distance * tonnes
                : EnRouteHeavyRate * distance * Math.Sqrt(tonnes);
        }

        // ---- Design constants (tier D) ------------------------------------------------------------------------------------

        /// <summary>Baseline jet fuel, A$ per kg (about US$90 per barrel, the 2025 average). The live price will walk around this.</summary>
        public const double BaselineFuelPerKg = 0.72;

        /// <summary>Time on the ground and in the circuit that a leg adds to airborne time (taxi, climb, approach), hours.</summary>
        public const double GroundAndCircuitHours = 0.35;

        /// <summary>Sales, admin, insurance, IT and similar costs as a share of revenue.</summary>
        public const double OverheadRate = 0.30;

        /// <summary>Crew on-costs on top of salary: super, allowances, training, rostering slack (design).</summary>
        public const double CrewOnCostFactor = 1.35;

        /// <summary>Turnaround handling per departure: a flat part plus a weight part.</summary>
        public static double Handling(double tonnes) => 150.0 + 12.0 * tonnes;

        /// <summary>Monthly lease rate as a share of the aircraft's market value, and the deposit in months.</summary>
        public const double LeaseRatePerMonth = 0.009;
        public const int LeaseDepositMonths = 3;
        /// <summary>Annual insurance as a share of value, paid while the aircraft is on the books.</summary>
        public const double InsuranceRatePerYear = 0.004;
        /// <summary>Annual interest on the bank loan.</summary>
        public const double LoanInterestPerYear = 0.085;

        /// <summary>Opening operating cash for a new Provisional airline, on top of the owned Saab 340 (design).</summary>
        public const long StartingCash = 1_500_000;

        /// <summary>Highest bank loan balance an airline may carry at each tier (design).</summary>
        public static long LoanCap(OperatingTier tier) => tier switch
        {
            OperatingTier.Provisional => 2_000_000L,
            OperatingTier.Regional => 15_000_000L,
            OperatingTier.Domestic => 80_000_000L,
            _ => 300_000_000L
        };

        /// <summary>One type's cost inputs. Value is the market value that leases and insurance scale from.</summary>
        public readonly struct Profile
        {
            public Profile(double burnKgPerBlockHour, double tonnes, double crewPerBlockHour,
                double maintenancePerBlockHour, long valueAud)
            {
                BurnKgPerBlockHour = burnKgPerBlockHour;
                Tonnes = tonnes;
                CrewPerBlockHour = crewPerBlockHour;
                MaintenancePerBlockHour = maintenancePerBlockHour;
                ValueAud = valueAud;
            }

            public double BurnKgPerBlockHour { get; }
            public double Tonnes { get; }
            public double CrewPerBlockHour { get; }
            public double MaintenancePerBlockHour { get; }
            public long ValueAud { get; }
        }

        /// <summary>
        /// Per-type inputs. Burn: ATR 42 from ATR's factsheet (tier B), Saab 340 and 737-800 from broker/summary data (tier C),
        /// the rest design. Chargeable tonnes: ATR 42-500, Dash 8-400 and A320/A321 from the Airservices table (tier A); the
        /// rest approximate MTOW (design). Crew, maintenance and value are design.
        /// </summary>
        public static Profile ProfileFor(AircraftType type)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            return type.Id switch
            {
                "SF34" => new Profile(540, 13.2, 520, 350, 2_000_000),
                "ATR42" => new Profile(590, 18.6, 540, 350, 10_000_000),
                "DH8D" => new Profile(1_000, 28.15, 700, 450, 18_000_000),
                "B412" => new Profile(300, 5.4, 400, 250, 7_000_000),
                "E190" => new Profile(1_800, 51, 1_000, 700, 25_000_000),
                "A223" => new Profile(2_100, 70, 1_050, 800, 45_000_000),
                "A320" => new Profile(2_400, 73.5, 1_100, 900, 28_000_000),
                "B738" => new Profile(2_500, 79, 1_100, 900, 32_000_000),
                "B38M" => new Profile(2_300, 82, 1_100, 850, 75_000_000),
                "A21N" => new Profile(2_500, 91.89, 1_150, 900, 85_000_000),
                "A339" => new Profile(5_500, 251, 2_600, 2_100, 140_000_000),
                "B789" => new Profile(5_000, 254, 2_600, 2_000, 170_000_000),
                "B78X" => new Profile(5_300, 254, 2_700, 2_100, 190_000_000),
                "B748" => new Profile(10_500, 447.7, 4_000, 4_000, 180_000_000),
                "A388" => new Profile(12_000, 575, 4_500, 5_000, 150_000_000),
                "A359" => new Profile(5_600, 280, 2_700, 2_200, 200_000_000),
                _ => throw new ArgumentException($"{type.Id} has no cost profile.", nameof(type))
            };
        }

        // ---- Demand (design; load factors informed by BITRE network 80.4%, tier C) -------------------------------------

        public static double LoadFactor(RouteBand band) => band switch
        {
            RouteBand.Regional => 0.65,
            RouteBand.Domestic => 0.80,
            RouteBand.National => 0.82,
            RouteBand.Tasman => 0.82,
            RouteBand.Pacific => 0.80,
            _ => 0.84
        };

        /// <summary>Helicopter seats are charter-priced: a 13-seat rescue/utility type cannot earn airline fares (design).</summary>
        public const double RotorcraftFareMultiplier = 4.0;

        /// <summary>Hours an aircraft can be worked in a day, and the ground time between legs (design, for planning a day).</summary>
        public const double WorkingHoursPerDay = 14.0;
        public const double TurnaroundHours = 0.75;

        /// <summary>One-way average fare: base + coefficient x sqrt(km), so short legs earn more per km (design).</summary>
        public static double Fare(RouteBand band, double km)
        {
            var root = Math.Sqrt(Math.Max(0, km));
            return band switch
            {
                RouteBand.Regional => 60 + 7.5 * root,
                RouteBand.Domestic => 50 + 4.6 * root,
                RouteBand.National => 50 + 4.8 * root,
                RouteBand.Tasman => 120 + 7.0 * root,
                RouteBand.Pacific => 150 + 8.0 * root,
                _ => 250 + 11.5 * root
            };
        }

        // ---- A single leg -----------------------------------------------------------------------------------------------------

        public readonly struct LegResult
        {
            public LegResult(double blockHours, double revenue, double fuel, double crew, double maintenance,
                double airport, double navigation, double handling, double overhead)
            {
                BlockHours = blockHours;
                Revenue = revenue;
                Fuel = fuel;
                Crew = crew;
                Maintenance = maintenance;
                Airport = airport;
                Navigation = navigation;
                Handling = handling;
                Overhead = overhead;
            }

            public double BlockHours { get; }
            public double Revenue { get; }
            public double Fuel { get; }
            public double Crew { get; }
            public double Maintenance { get; }
            public double Airport { get; }
            public double Navigation { get; }
            public double Handling { get; }
            public double Overhead { get; }
            public double Cost => Fuel + Crew + Maintenance + Airport + Navigation + Handling + Overhead;
            public double Profit => Revenue - Cost;
            public double Margin => Revenue <= 0 ? 0 : Profit / Revenue;
        }

        /// <summary>
        /// One flight of <paramref name="oneWayKm"/>. Passenger fees are charged for one departure and one arrival (the other
        /// end is assumed to charge like Adelaide until its schedule is sourced). Helicopters pay no ARFF below the threshold.
        /// </summary>
        public static LegResult Leg(AircraftType type, double oneWayKm, RouteBand band, double fuelPerKg = BaselineFuelPerKg)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            if (fuelPerKg <= 0) throw new ArgumentOutOfRangeException(nameof(fuelPerKg));
            var profile = ProfileFor(type);
            var km = Math.Max(0, oneWayKm);
            var blockHours = km / type.CruiseKmh + GroundAndCircuitHours;
            var seats = AircraftCatalogue.TypicalSeats(type);
            var passengers = seats * LoadFactor(band);
            var revenue = passengers * Fare(band, km) * (type.IsRotorcraft ? RotorcraftFareMultiplier : 1.0);

            var fuel = blockHours * profile.BurnKgPerBlockHour * fuelPerKg;
            var crew = blockHours * profile.CrewPerBlockHour * CrewOnCostFactor;
            var maintenance = blockHours * profile.MaintenancePerBlockHour;
            var airport = passengers * PassengerFee(band) * 2.0;
            var navigation = TerminalNavigation(profile.Tonnes) + Arff(profile.Tonnes) + EnRoute(profile.Tonnes, km);
            var handling = Handling(profile.Tonnes);
            var overhead = revenue * OverheadRate;
            return new LegResult(blockHours, revenue, fuel, crew, maintenance, airport, navigation, handling, overhead);
        }

        /// <summary>How many legs of this length one aircraft can fly in a working day, at least one.</summary>
        public static int LegsPerWorkingDay(AircraftType type, double oneWayKm)
        {
            var legHours = Leg(type, oneWayKm, RouteBand.Regional).BlockHours + TurnaroundHours;
            return Math.Max(1, (int)Math.Floor(WorkingHoursPerDay / legHours));
        }

        // ---- Standing costs and finance ------------------------------------------------------------------------------------

        public static double LeasePerDay(AircraftType type) =>
            ProfileFor(type).ValueAud * LeaseRatePerMonth * 12.0 / 365.0;

        public static double InsurancePerDay(AircraftType type) =>
            ProfileFor(type).ValueAud * InsuranceRatePerYear / 365.0;

        /// <summary>Standing cost of holding a leased aircraft for one day, flying or not.</summary>
        public static double StandingPerDay(AircraftType type) => LeasePerDay(type) + InsurancePerDay(type);

        public static long LeaseDeposit(AircraftType type) =>
            (long)Math.Round(ProfileFor(type).ValueAud * LeaseRatePerMonth * LeaseDepositMonths);

        public static double LoanInterestPerDay(long balance) =>
            Math.Max(0, balance) * LoanInterestPerYear / 365.0;
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Airside.Domain;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>
    /// Economy v2 pacing check (ADR 2026-10-09-economy-v2-real-dollar-scale). A deterministic, economy-only player leases and
    /// finances aircraft through the ADR 0120 stage hours (8 / 35 / 70 / 110 / 150 open hours) using only FlightCostModel. It
    /// is a tuning instrument for the tier D design constants, not a gameplay replay: CareerBot still has to confirm the
    /// real career once the model is wired into AirlineOperations.
    /// </summary>
    public sealed class EconomyPacingTests
    {
        private const double OpenHoursPerDay = 17.0;   // outside the 23:00-06:00 curfew
        private const double Utilisation = 0.75;       // share of open hours an aircraft is actually earning
        private const int MaxFleet = 25;
        /// <summary>Each extra aircraft of one type finds thinner demand (stand-in for route saturation).</summary>
        private const double SaturationPerSameType = 0.80;

        /// <summary>Stand-in for stands and bases: Adelaide's six-aircraft allocation, then outstation bases as the career advances.</summary>
        private static int FleetCap(OperatingTier tier) => tier switch
        {
            OperatingTier.Provisional => 3,
            OperatingTier.Regional => 8,
            OperatingTier.Domestic => 14,
            _ => 20
        };

        private sealed class Plane
        {
            public AircraftType Type;
            public bool Owned;
            public double ProfitPerOpenHour;
            public double StandingPerOpenHour;
        }

        private sealed class Snapshot
        {
            public int Hour;
            public int Fleet;
            public double Cash;
            public double Loan;
            public double NetPerHour;
            public string Newest;
            public string Mix;
        }

        private static OperatingTier TierAt(double hour) =>
            hour < 8 ? OperatingTier.Provisional : hour < 35 ? OperatingTier.Regional
            : hour < 70 ? OperatingTier.Domestic : OperatingTier.International;

        private static double ProfitPerOpenHour(AircraftType type)
        {
            var km = FlightEconomics.TypicalLegKm(type);
            var leg = FlightCostModel.Leg(type, km, RouteAccess.Ceiling(type));
            return leg.Profit / (leg.BlockHours + FlightCostModel.TurnaroundHours) * Utilisation;
        }

        private static Plane Make(AircraftType type, bool owned) => new Plane
        {
            Type = type,
            Owned = owned,
            ProfitPerOpenHour = ProfitPerOpenHour(type),
            StandingPerOpenHour = (owned ? 0 : FlightCostModel.StandingPerDay(type)) / OpenHoursPerDay
        };

        private static List<Snapshot> Run(double fuelPerKg, double fareScale = 1.0)
        {
            var fleet = new List<Plane> { Make(AircraftType.Saab340, true) };
            double cash = FlightCostModel.StartingCash;
            double loan = 0;
            var tier = OperatingTier.Provisional;
            var log = new List<Snapshot>();
            string newest = "Saab 340 (owned)";
            var checkpoints = new HashSet<int> { 8, 35, 70, 110, 150 };

            for (var hour = 1; hour <= 150; hour++)
            {
                tier = TierAt(hour - 1);
                // Earn and pay for one open hour.
                double earn = 0, hold = 0;
                var seen = new Dictionary<string, int>();
                foreach (var plane in fleet)
                {
                    seen.TryGetValue(plane.Type.Id, out var sameBefore);
                    seen[plane.Type.Id] = sameBefore + 1;
                    var leg = FlightCostModel.Leg(plane.Type, FlightEconomics.TypicalLegKm(plane.Type), RouteAccess.Ceiling(plane.Type), fuelPerKg);
                    var perHour = leg.Profit / (leg.BlockHours + FlightCostModel.TurnaroundHours) * Utilisation;
                    earn += perHour * fareScale * Math.Pow(SaturationPerSameType, sameBefore);
                    hold += plane.StandingPerOpenHour;
                }
                var interest = FlightCostModel.LoanInterestPerDay((long)loan) / OpenHoursPerDay;
                cash += earn - hold - interest;

                // Repay debt from surplus, keeping a three-day cushion of standing costs.
                var cushion = (hold + interest) * OpenHoursPerDay * 3;
                if (loan > 0 && cash > cushion)
                {
                    var repay = Math.Min(loan, cash - cushion);
                    loan -= repay;
                    cash -= repay;
                }

                // Grow: lease the aircraft with the best net hourly profit per deposit that the tier allows and the cash/loan can fund.
                while (fleet.Count < FleetCap(tier))
                {
                    var best = AircraftAcquisition.All
                        .Where(o => o.RequiredTier <= tier && !o.Type.IsRotorcraft)
                        .Select(o => new { o, plane = Make(o.Type, false), deposit = FlightCostModel.LeaseDeposit(o.Type) })
                        .Where(x => x.plane.ProfitPerOpenHour * fareScale * Math.Pow(SaturationPerSameType, fleet.Count(f => f.Type.Id == x.plane.Type.Id))
                                    > x.plane.StandingPerOpenHour * 1.5)
                        .Where(x => cash - x.deposit >= cushion * 0.5 || FundableByLoan(x.deposit, cash, loan, tier, cushion))
                        .OrderByDescending(x => x.plane.ProfitPerOpenHour * fareScale * Math.Pow(SaturationPerSameType, fleet.Count(f => f.Type.Id == x.plane.Type.Id))
                                                - x.plane.StandingPerOpenHour)
                        .FirstOrDefault();
                    if (best == null) break;
                    var shortfall = best.deposit - Math.Max(0, cash - cushion * 0.5);
                    if (shortfall > 0)
                    {
                        loan += shortfall;
                        cash += shortfall;
                    }
                    cash -= best.deposit;
                    fleet.Add(best.plane);
                    newest = best.plane.Type.Id;
                }

                if (checkpoints.Contains(hour))
                    log.Add(new Snapshot
                    {
                        Hour = hour, Fleet = fleet.Count, Cash = cash, Loan = loan, Newest = newest,
                        Mix = string.Join(" ", fleet.GroupBy(f => f.Type.Id).Select(g => g.Key + "x" + g.Count())),
                        NetPerHour = earn - hold - interest
                    });
            }

            return log;
        }

        private static bool FundableByLoan(long deposit, double cash, double loan, OperatingTier tier, double cushion) =>
            loan + Math.Max(0, deposit - Math.Max(0, cash - cushion * 0.5)) <= FlightCostModel.LoanCap(tier);

        [Test]
        public void CompetentPlayerReachesAnEstablishedFleetWithoutGoingBroke()
        {
            var log = Run(FlightCostModel.BaselineFuelPerKg);
            foreach (var s in log)
                TestContext.Progress.WriteLine(
                    $"PACING h{s.Hour,3} fleet {s.Fleet,2} cash {s.Cash,13:N0} loan {s.Loan,13:N0} net/h {s.NetPerHour,11:N0} mix {s.Mix}");

            Assert.That(log.All(s => s.Cash >= 0), Is.True, "never negative cash");
            Assert.That(log.Last().Fleet, Is.InRange(12, MaxFleet), "an established fleet by 150 open hours (ADR 0120: roughly 18-20)");
            Assert.That(log.Last().Mix, Does.Contain("A339").Or.Contain("B789").Or.Contain("B78X").Or.Contain("A359"),
                "an ambitious player can reach a widebody inside the career");
            Assert.That(log.First(s => s.Hour == 8).Fleet, Is.InRange(1, 4), "the Provisional stage stays a small operation");
        }

        [Test]
        public void RevenueSensitivity_IsReported()
        {
            foreach (var scale in new[] { 1.0, 0.8, 0.65, 0.5 })
            {
                var log = Run(FlightCostModel.BaselineFuelPerKg, scale);
                var h35 = log.First(s => s.Hour == 35);
                var end = log.Last();
                TestContext.Progress.WriteLine($"PACING fare x{scale:0.00}: h35 fleet {h35.Fleet}, h150 fleet {end.Fleet} cash {end.Cash:N0} loan {end.Loan:N0} net/h {end.NetPerHour:N0}");
                Assert.That(log.All(s => s.Cash >= 0), Is.True, $"fare x{scale}");
            }
        }

        [Test]
        public void ADoubledFuelPriceSlowsGrowthButStillWorks()
        {
            var calm = Run(FlightCostModel.BaselineFuelPerKg).Last();
            var spike = Run(FlightCostModel.BaselineFuelPerKg * 2.0).Last();
            TestContext.Progress.WriteLine($"PACING fuel x2: fleet {spike.Fleet} cash {spike.Cash:N0} loan {spike.Loan:N0} (calm fleet {calm.Fleet})");
            Assert.That(spike.Cash, Is.GreaterThanOrEqualTo(0));
            Assert.That(spike.Fleet, Is.LessThanOrEqualTo(calm.Fleet), "fuel pressure must matter");
        }
    }
}

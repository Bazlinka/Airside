using System.Linq;
using Airside.Domain;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>
    /// ADR 0125 — a fast guard on career pacing. The full matrix lives in
    /// scripts/career-balance.sh; these runs are short enough for every test pass.
    /// </summary>
    public sealed class CareerBalanceTests
    {
        [Test]
        public void StandardCompetentPlayer_ReachesRegionalInsideTheFirstSessionWithoutStalling()
        {
            var run = CareerSimulation.Run(CareerDifficulty.Standard, CareerPlayStyle.Competent, seed: 1, maxOpenHours: 14);
            Assert.That(run.TierAtOpenHours.ContainsKey(OperatingTier.Regional), Is.True, "Regional within 14 open hours");
            // ADR 0120 aims for Regional by ~8 h; the balance pass lands it near 9.5 h.
            Assert.That(run.TierAtOpenHours[OperatingTier.Regional], Is.LessThan(12.0));
            Assert.That(run.Flags, Is.Empty, string.Join("; ", run.Flags));
        }

        [Test]
        public void CasualPlayer_NeverDeadEndsInTheFirstDays()
        {
            var run = CareerSimulation.Run(CareerDifficulty.Standard, CareerPlayStyle.Casual, seed: 1, maxOpenHours: 40);
            Assert.That(run.Flags, Is.Empty, string.Join("; ", run.Flags));
            Assert.That(run.FinalRotations, Is.GreaterThan(10), "still flying");
            Assert.That(run.FinalTier, Is.GreaterThanOrEqualTo(OperatingTier.Regional));
        }

        [Test]
        public void TheSameSeed_PlaysTheSameCareer()
        {
            var a = CareerSimulation.Run(CareerDifficulty.Standard, CareerPlayStyle.Casual, seed: 3, maxOpenHours: 10);
            var b = CareerSimulation.Run(CareerDifficulty.Standard, CareerPlayStyle.Casual, seed: 3, maxOpenHours: 10);
            Assert.That(b.Samples.Select(s => (s.Funds, s.Reliability, s.Fleet, s.Rotations)),
                Is.EqualTo(a.Samples.Select(s => (s.Funds, s.Reliability, s.Fleet, s.Rotations))));
        }

        [Test]
        public void AdelaideAircraft_CanBePlannedToInternationalCities()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = AirlineOperations.StartAtAdelaide(clock, new SeededRandomSource(1), Airline.Player("Test Air", "#39708A"));
            Assert.That(ops.PlannableDestinations().Select(d => d.Code), Does.Contain("AKL").And.Contain("SIN"));
            Assert.That(ops.PlannableDestinations().Any(d => d.Code == ops.Home.Code), Is.False);
        }
    }
}

using System;
using System.Linq;
using Airside.Domain;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>ADR 0139 — tighter progress gates: bases, outstations, aircraft and held reliability.</summary>
    public sealed class ProgressGateTests
    {
        private static AirlineOperations Start()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            return AirlineOperations.StartAtAdelaide(clock, new SeededRandomSource(5), Airline.Player("Gate Air", "#1F3A93"),
                firstFlightCoaching: false);
        }

        private static void Career(AirlineOperations ops, OperatingTier tier, int reliability, int flights,
            PlayerBaseLevel baseLevel, string[] outstations = null) =>
            ops.RestoreCareerState(5_000_000, reliability, tier.ToString(), null, 0, 0, Array.Empty<string>(),
                Array.Empty<string>(), flights, baseLevel: baseLevel, outstationBases: outstations);

        [Test]
        public void BaseUpgrades_NeedReliabilityAsWellAsFlights()
        {
            var ops = Start();
            Career(ops, OperatingTier.Regional, 79, 16, PlayerBaseLevel.ExpandedRegional);
            var refused = ops.UpgradePlayerBase();
            Assert.That(refused.Accepted, Is.False);
            Assert.That(refused.Reason, Is.EqualTo("The Jet-gate base needs 80% reliability. You have 79%."));
            Assert.That(PlayerBase.Requirement(PlayerBase.For(PlayerBaseLevel.JetGate), ops.CareerState),
                Does.Contain("80% reliability"));

            Career(ops, OperatingTier.Regional, 80, 15, PlayerBaseLevel.ExpandedRegional);
            Assert.That(ops.UpgradePlayerBase().Accepted, Is.False, "16 flights, not 12");
            Career(ops, OperatingTier.Regional, 80, 16, PlayerBaseLevel.ExpandedRegional);
            Assert.That(ops.UpgradePlayerBase().Accepted, Is.True);

            Career(ops, OperatingTier.Domestic, 88, 49, PlayerBaseLevel.JetGate);
            Assert.That(ops.UpgradePlayerBase().Accepted, Is.False, "the International base needs 50 flights");
            Career(ops, OperatingTier.Domestic, 87, 50, PlayerBaseLevel.JetGate);
            Assert.That(ops.UpgradePlayerBase().Accepted, Is.False, "and 88% reliability");
            Career(ops, OperatingTier.Domestic, 88, 50, PlayerBaseLevel.JetGate);
            Assert.That(ops.UpgradePlayerBase().Accepted, Is.True);
        }

        [Test]
        public void Outstations_AreEarnedOneAtATime()
        {
            var ops = Start();
            Career(ops, OperatingTier.Domestic, 84, 40, PlayerBaseLevel.JetGate);
            Assert.That(ops.OpenOutstationBase("MEL").Reason, Does.Contain("85% reliability"));
            Assert.That(ops.NextOutstationRequirement(), Is.EqualTo("Needs 85% reliability."));
            Career(ops, OperatingTier.Domestic, 85, 39, PlayerBaseLevel.JetGate);
            Assert.That(ops.OpenOutstationBase("MEL").Reason, Does.Contain("40 flights"));
            Career(ops, OperatingTier.Domestic, 85, 40, PlayerBaseLevel.JetGate);
            Assert.That(ops.OpenOutstationBase("MEL").Accepted, Is.True);

            Career(ops, OperatingTier.Domestic, 88, 69, PlayerBaseLevel.JetGate, new[] { "MEL" });
            Assert.That(ops.NextOutstationRequirement(), Is.EqualTo("Needs 1 more flight."));
            Assert.That(ops.OpenOutstationBase("SYD").Accepted, Is.False);
            Career(ops, OperatingTier.Domestic, 88, 70, PlayerBaseLevel.JetGate, new[] { "MEL" });
            Assert.That(ops.OpenOutstationBase("SYD").Accepted, Is.True);

            Career(ops, OperatingTier.International, 90, 110, PlayerBaseLevel.JetGate, new[] { "MEL", "SYD" });
            Assert.That(ops.OpenOutstationBase("BNE").Accepted, Is.True);
            Assert.That(ops.NextOutstationGate, Is.Null);
            Assert.That(ops.NextOutstationRequirement(), Is.Empty);
        }

        [Test]
        public void AircraftFlightGates_SitAboveTheirTiersFlightGoal()
        {
            // Domestic opens at 30 flights and International at 75 (CareerRoadmap). A type's own gate
            // above that is what makes it a later step, not a reward the tier already gave.
            foreach (var offer in AircraftAcquisition.All)
            {
                var tierFlights = offer.RequiredTier switch
                {
                    OperatingTier.Domestic => 30,
                    OperatingTier.International => 75,
                    _ => 0
                };
                if (tierFlights > 0)
                    Assert.That(offer.RequiredRotations, Is.GreaterThan(tierFlights), offer.Type.Name);
            }

            Assert.That(AircraftAcquisition.EmbraerE190.RequiredRotations, Is.LessThan(AircraftAcquisition.Boeing7378.RequiredRotations));
            Assert.That(AircraftAcquisition.AirbusA330900.RequiredRotations, Is.LessThan(AircraftAcquisition.AirbusA350900.RequiredRotations));
        }

        [Test]
        public void ReliabilityGoals_AreHeldForRecentFlights()
        {
            var career = new AirlineCareerState(completedPlayerRotations: 0);
            career.RestoreReliabilityHistory(0, new[] { 95, 95, 95, 95, 95, 95, 79, 85, 85, 85 });
            var goal = CareerRoadmap.Evaluate(career, new[] { AircraftType.Saab340 })
                .Single(g => g.Id == "domestic-reliability");
            Assert.That(goal.Title, Is.EqualTo("Keep reliability at 80% for 10 flights"));
            Assert.That(goal.Progress, Is.EqualTo(3), "the dip to 79% restarts the count");
            Assert.That(goal.Complete, Is.False);

            career.RestoreReliabilityHistory(0, Enumerable.Repeat(81, 10));
            goal = CareerRoadmap.Evaluate(career, new[] { AircraftType.Saab340 }).Single(g => g.Id == "domestic-reliability");
            Assert.That(goal.Complete, Is.True);

            var first = CareerRoadmap.Evaluate(career, new[] { AircraftType.Saab340 }).Single(g => g.Id == "regional-reliability");
            Assert.That(first.Target, Is.EqualTo(CareerRoadmap.ProvisionalHeldFlights),
                "the first tier asks for no more flights than its own flight goal");
        }

        [Test]
        public void ARestoredCareerKeepsTheReliabilityGoalsItHadMet()
        {
            var career = new AirlineCareerState(reliability: 92, completedPlayerRotations: 80);
            Assert.That(career.FlightsHeldAtOrAbove(88), Is.EqualTo(AirlineCareerState.ReliabilityHistoryLength));
            Assert.That(career.HighReliabilityStreak, Is.Zero, "92% is not the 95% streak");
        }
    }
}

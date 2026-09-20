using System.Linq;
using Airside.Domain;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class CareerMilestonesTests
    {
        [Test]
        public void FreshCareer_OnlyStartingReliabilityIsAlreadyReached()
        {
            var career = new AirlineCareerState();
            var milestones = CareerMilestones.Reached(career, fleetSize: 1, new[] { AircraftType.Atr42 });

            Assert.That(milestones, Is.Not.Empty);
            // A fresh career starts at 100% reliability (StartingReliability), so the
            // reliability milestone is trivially already met — everything earned through
            // actually flying (rotations, contracts, tier, fleet growth, jets) is not.
            foreach (var milestone in milestones)
                Assert.That(milestone.Reached, Is.EqualTo(milestone.Id == "elite-reliability"), milestone.Id);
        }

        [Test]
        public void EveryMilestoneHasAUniqueId()
        {
            var career = new AirlineCareerState();
            var milestones = CareerMilestones.Reached(career, fleetSize: 1, new[] { AircraftType.Atr42 });
            Assert.That(milestones.Select(m => m.Id).Distinct().Count(), Is.EqualTo(milestones.Count));
        }

        [Test]
        public void NullCareer_ReturnsAnEmptyListRatherThanThrowing()
        {
            Assert.That(CareerMilestones.Reached(null, 0, null), Is.Empty);
        }

        [Test]
        public void RotationAndFleetMilestones_FlipOnAsThresholdsAreCrossed()
        {
            var career = new AirlineCareerState(completedPlayerRotations: 10, reliability: 95);
            var milestones = CareerMilestones.Reached(career, fleetSize: 4, new[]
            {
                AircraftType.Atr42, AircraftType.Boeing7378
            });

            bool Reached(string id) => milestones.Single(m => m.Id == id).Reached;
            Assert.That(Reached("first-rotation"), Is.True);
            Assert.That(Reached("ten-rotations"), Is.True);
            Assert.That(Reached("twenty-five-rotations"), Is.False);
            Assert.That(Reached("full-fleet"), Is.True);
            Assert.That(Reached("jet-operator"), Is.True);
            Assert.That(Reached("elite-reliability"), Is.True);
        }
    }
}

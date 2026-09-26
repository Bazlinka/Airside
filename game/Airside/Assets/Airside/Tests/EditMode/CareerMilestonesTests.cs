using System.Linq;
using Airside.Domain;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class CareerMilestonesTests
    {
        [Test]
        public void FreshCareer_HasEarnedNothingYet()
        {
            var career = new AirlineCareerState();
            var milestones = CareerMilestones.Reached(career, fleetSize: 1, new[] { AircraftType.Saab340 });

            Assert.That(milestones, Is.Not.Empty);
            // Reliability starts at 100%, so the reliability keepsake also needs 50 services flown:
            // nothing is handed out before the player has actually operated.
            foreach (var milestone in milestones)
                Assert.That(milestone.Reached, Is.False, milestone.Id);
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
            var career = new AirlineCareerState(completedPlayerRotations: 60, reliability: 95,
                outstationBases: new[] { "MEL" });
            var milestones = CareerMilestones.Reached(career, fleetSize: AircraftAcquisition.MaxPlayerAircraft, new[]
            {
                AircraftType.Atr42, AircraftType.Boeing7378
            });

            bool Reached(string id) => milestones.Single(m => m.Id == id).Reached;
            Assert.That(Reached("first-rotation"), Is.True);
            Assert.That(Reached("century"), Is.False);
            Assert.That(Reached("full-fleet"), Is.True, "fleet size is the whole airline, outstations included");
            Assert.That(Reached("jet-operator"), Is.True);
            Assert.That(Reached("widebody-operator"), Is.False);
            Assert.That(Reached("first-outstation"), Is.True);
            Assert.That(Reached("elite-reliability"), Is.True);
            Assert.That(Reached("established"), Is.False);
        }
    }
}

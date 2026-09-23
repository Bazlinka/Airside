using System.Collections.Generic;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>
    /// ADR 0114 imported two hi-vis ramp characters and never placed them, so the only people
    /// ever visible were boarding passengers — and those are drawn for stairs boarding only.
    /// At an aerobridge gate, which is where the player's jets park, the apron was empty.
    /// </summary>
    public sealed class RampCrewTests
    {
        private readonly List<RampCrewMember> _crew = new();

        private static DeparturePrepStatus Prep(DeparturePrepStage stage) =>
            new(stage, 0.5, false, stage.ToString(), 0, 0, 0, 0, 600);

        private void Crew(DeparturePrepStage stage) =>
            RampCrew.For(Prep(stage), RampCrew.VehicleFor(stage), _crew);

        [TestCase(DeparturePrepStage.Fuel)]
        [TestCase(DeparturePrepStage.Catering)]
        [TestCase(DeparturePrepStage.Baggage)]
        public void EveryServicedStagePutsCrewOnTheApron(DeparturePrepStage stage)
        {
            Crew(stage);
            Assert.That(_crew, Is.Not.Empty, $"{stage} is worked by people, not just a vehicle");
            Assert.That(_crew.Count, Is.LessThanOrEqualTo(RampCrew.MaxPerAircraft),
                "only two ramp characters exist");
        }

        [TestCase(DeparturePrepStage.Idle)]
        [TestCase(DeparturePrepStage.Ready)]
        public void NobodyStandsAroundWhenTheAircraftIsNotBeingWorked(DeparturePrepStage stage)
        {
            Crew(stage);
            Assert.That(_crew, Is.Empty);
        }

        [Test]
        public void BoardingKeepsOneMarshallerClearOfTheDoor()
        {
            Crew(DeparturePrepStage.Boarding);
            Assert.That(_crew.Count, Is.EqualTo(1), "boarding is the passengers' business");
            Assert.That(_crew[0].Role, Is.EqualTo(RampRole.Marshalling));
            Assert.That(_crew[0].AlongMetres, Is.GreaterThan(0f),
                "the marshaller stands ahead of the aircraft, not in the boarding path");
        }

        [Test]
        public void CrewStandBesideTheAircraftNotInsideIt()
        {
            foreach (var stage in new[] { DeparturePrepStage.Fuel, DeparturePrepStage.Catering,
                         DeparturePrepStage.Baggage, DeparturePrepStage.Boarding })
            {
                Crew(stage);
                foreach (var member in _crew)
                {
                    Assert.That(System.Math.Abs(member.AcrossMetres), Is.GreaterThan(2.5f),
                        $"{stage}: a worker on the centreline would stand inside the fuselage");
                    Assert.That(System.Math.Abs(member.AcrossMetres), Is.LessThan(25f),
                        $"{stage}: a worker this far out is off the stand");
                    Assert.That(System.Math.Abs(member.AlongMetres), Is.LessThan(40f),
                        $"{stage}: a worker this far fore/aft is not on this turnaround");
                }
            }
        }

        [Test]
        public void CrewWorkOnTheSameSideAsTheirVehicle()
        {
            // Fuel serves from the aircraft's right, catering and baggage from its left, matching
            // the service positions UpdatePlayerTurnaroundServicing already uses.
            Crew(DeparturePrepStage.Fuel);
            foreach (var m in _crew)
                Assert.That(m.AcrossMetres, Is.GreaterThan(0f), "fuel works the right-hand side");

            Crew(DeparturePrepStage.Catering);
            foreach (var m in _crew)
                Assert.That(m.AcrossMetres, Is.LessThan(0f), "catering works the left-hand side");

            Crew(DeparturePrepStage.Baggage);
            foreach (var m in _crew)
                Assert.That(m.AcrossMetres, Is.LessThan(0f), "baggage works the left-hand side");
        }

        [Test]
        public void TwoWorkersBesideOneAircraftAreNotTheSamePerson()
        {
            Crew(DeparturePrepStage.Fuel);
            Assume.That(_crew.Count, Is.EqualTo(2));
            Assert.That(RampCrew.IsFemale(0), Is.Not.EqualTo(RampCrew.IsFemale(1)),
                "both imported ramp characters should be used, not one twice");
        }

        [Test]
        public void EachServicedStageNamesTheVehicleItsCrewAttend()
        {
            Assert.That(RampCrew.VehicleFor(DeparturePrepStage.Fuel),
                Is.EqualTo(GroundServiceKind.Fuel));
            Assert.That(RampCrew.VehicleFor(DeparturePrepStage.Catering),
                Is.EqualTo(GroundServiceKind.Catering));
            Assert.That(RampCrew.VehicleFor(DeparturePrepStage.Baggage),
                Is.EqualTo(GroundServiceKind.Baggage));
            Assert.That(RampCrew.VehicleFor(DeparturePrepStage.Boarding), Is.Null,
                "no service vehicle works the boarding stage");
            Assert.That(RampCrew.VehicleFor(DeparturePrepStage.Idle), Is.Null);
        }
    }
}

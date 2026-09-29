using System.Collections.Generic;
using System.Linq;
using Airside.Domain;
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
            var door = AircraftLayout.For(AircraftType.Boeing7378).PassengerDoor;
            var dx = _crew[0].AcrossMetres - door.X;
            var dz = _crew[0].AlongMetres - door.Z;
            Assert.That(System.Math.Sqrt(dx * dx + dz * dz), Is.GreaterThan(3.0),
                "the supervisor stands beside the boarding path, not in it");
            Assert.That(_crew[0].AcrossMetres, Is.LessThan(door.X), "on the door's side of the aircraft");
        }

        [Test]
        public void CrewStandBesideTheAircraftNotInsideIt()
        {
            var layout = AircraftLayout.For(AircraftType.Boeing7378);
            var footprint = new List<LayoutRect>();
            layout.Footprint(footprint, forVehicles: false);
            foreach (var stage in new[] { DeparturePrepStage.Fuel, DeparturePrepStage.Catering,
                         DeparturePrepStage.Baggage, DeparturePrepStage.Boarding })
            {
                Crew(stage);
                foreach (var member in _crew)
                {
                    Assert.That(footprint.Any(r => r.Contains(member.AcrossMetres, member.AlongMetres, 0.5f)), Is.False,
                        $"{stage}: {member.Task} stands inside the fuselage or a nacelle");
                    Assert.That(System.Math.Abs(member.AcrossMetres), Is.LessThan(25f),
                        $"{stage}: a worker this far out is off the stand");
                    Assert.That(System.Math.Abs(member.AlongMetres), Is.LessThan(45f),
                        $"{stage}: a worker this far fore/aft is not on this turnaround");
                }
            }
        }

        [Test]
        public void CrewWorkOnTheSameSideAsTheirVehicle()
        {
            // A 737's refuel coupling, aft service door and hold doors are all on its right, so
            // fuel, catering and baggage all work that side (the passenger doors are on the left).
            Crew(DeparturePrepStage.Fuel);
            foreach (var m in _crew)
                Assert.That(m.AcrossMetres, Is.GreaterThan(0f), "fuel works the right-hand side");

            Crew(DeparturePrepStage.Catering);
            foreach (var m in _crew)
                Assert.That(m.AcrossMetres, Is.GreaterThan(0f), "catering works the service-door side");

            Crew(DeparturePrepStage.Baggage);
            foreach (var m in _crew)
                Assert.That(m.AcrossMetres, Is.GreaterThan(0f), "baggage works the hold-door side");
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

        [Test]
        public void EveryCrewMemberHasTheJobTheyAreVisiblyPerforming()
        {
            Crew(DeparturePrepStage.Fuel);
            Assert.That(_crew[0].Task, Is.EqualTo(RampTask.FuelPanel));
            Assert.That(_crew[1].Task, Is.EqualTo(RampTask.FuelCoupling));
            Crew(DeparturePrepStage.Catering);
            Assert.That(_crew[0].Task, Is.EqualTo(RampTask.CateringLoader));
            Assert.That(_crew[1].Task, Is.EqualTo(RampTask.CateringDoor));
            Crew(DeparturePrepStage.Baggage);
            Assert.That(_crew[0].Task, Is.EqualTo(RampTask.BaggageHold));
            Assert.That(_crew[1].Task, Is.EqualTo(RampTask.BaggageCart));
        }

        [TestCase(RampActivity.Arrival, RampTask.MarshalArrival, 2)]
        [TestCase(RampActivity.Pushback, RampTask.PushbackHeadset, 2)]
        [TestCase(RampActivity.Boarding, RampTask.BoardingSupervision, 1)]
        public void WholeTurnActivitiesProduceAReadableTeam(RampActivity activity, RampTask firstTask, int count)
        {
            RampCrew.ForActivity(activity, 0.5, _crew);
            Assert.That(_crew.Count, Is.EqualTo(count));
            Assert.That(_crew[0].Task, Is.EqualTo(firstTask));
            Assert.That(_crew.All(member => member.Progress01 == 0.5f), Is.True);
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using Airside.Domain;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>
    /// Turnaround work done item by item: bags walked to the hold, the fuel nozzle walked out on
    /// its hose and back, a hi-loader raised to a jet's service door, galley boxes carried up a
    /// turboprop's stairs, and turboprop passengers' roller bags loaded from planeside.
    /// </summary>
    public sealed class ServiceChoreographyTests
    {
        private readonly List<RampCrewMember> _crew = new();
        private readonly List<CrewAction> _actions = new();
        private readonly ServiceScene _scene = new();

        private void Act(RampActivity activity, AircraftType type, double elapsed, double seconds,
            IReadOnlyList<double> drops = null, (float X, float Z)? cart = null)
        {
            RampCrew.ForActivity(activity, elapsed / seconds, AircraftLayout.For(type), _crew);
            ServiceChoreography.Act(activity, type, elapsed, seconds, _crew, _actions, _scene, drops, cart);
        }

        private CrewAction Worker(RampTask task) => _actions[_crew.FindIndex(m => m.Task == task)];

        [TestCase("SF34")]
        [TestCase("ATR42")]
        [TestCase("A320")]
        public void TheBaggageTrainEmptiesBagByBagIntoTheHold(string id)
        {
            AircraftType.TryFromId(id, out var type);
            var duration = TurnaroundCrewWork.BaggageSeconds(type);
            Act(RampActivity.Baggage, type, 0, duration);
            Assert.That(_scene.TrainLoad, Is.EqualTo(1f), "full before the first bag");
            Act(RampActivity.Baggage, type, duration, duration);
            Assert.That(_scene.TrainLoad, Is.LessThan(0.05f), "empty at the end");

            // Somewhere in the stage a bag is carried, and later one is going in the door.
            var carried = false;
            var goingIn = false;
            var door = AircraftLayout.For(type).CargoDoor;
            for (var t = 0.0; t < duration; t += 0.25)
            {
                Act(RampActivity.Baggage, type, t, duration);
                carried |= Worker(RampTask.BaggageCart).Item == CarriedItem.Bag;
                foreach (var item in _scene.Transit)
                    if (item.Kind == CarriedItem.Bag && System.Math.Abs(item.Z - door.Z) < 1.5f
                        && item.Height > DoorSills.For(type).Cargo - 0.2f)
                        goingIn = true;
            }

            Assert.That(carried, Is.True, "the handler carries bags");
            Assert.That(goingIn, Is.True, "bags reach the hold door at its sill");
            Assert.That(_scene.BeltLoader, Is.EqualTo(!AircraftLayout.For(type).IsTurboprop),
                "jets load by belt; turboprops by hand");
        }

        [Test]
        public void TheFuelNozzleIsWalkedOutConnectedAndBroughtBack()
        {
            var type = AircraftType.Atr42;
            var coupling = AircraftLayout.For(type).FuelCoupling;
            Act(RampActivity.Fuel, type, 0, 90);
            Assert.That(_scene.NozzleOut, Is.False);
            Act(RampActivity.Fuel, type, 45, 90);
            Assert.That(_scene.NozzleOut, Is.True);
            var worker = Worker(RampTask.FuelCoupling);
            Assert.That(worker.X, Is.EqualTo(coupling.X).Within(0.05f));
            Assert.That(worker.Z, Is.EqualTo(coupling.Z).Within(0.05f));
            Assert.That(worker.Item, Is.EqualTo(CarriedItem.Nozzle));
            Act(RampActivity.Fuel, type, 90, 90);
            Assert.That(_scene.NozzleOut, Is.False, "stowed at the end");
        }

        [Test]
        public void AJetHiLoaderRisesToTheServiceDoorAndComesDown()
        {
            var type = AircraftType.AirbusA320200;
            Act(RampActivity.Catering, type, 0, 110);
            Assert.That(_scene.LiftHeight, Is.EqualTo(0f).Within(0.01f));
            Act(RampActivity.Catering, type, 55, 110);
            Assert.That(_scene.LiftHeight + ServiceChoreography.HiLoaderFloorMetres,
                Is.EqualTo(DoorSills.For(type).Catering).Within(0.05f));
            Assert.That(Worker(RampTask.CateringDoor).Height, Is.EqualTo(DoorSills.For(type).Catering).Within(0.05f),
                "the platform worker is at door level");
            Act(RampActivity.Catering, type, 110, 110);
            Assert.That(_scene.LiftHeight, Is.EqualTo(0f).Within(0.01f));
        }

        [TestCase("SF34")]
        [TestCase("ATR42")]
        [TestCase("DH8D")]
        public void TurbopropGalleyBoxesAreCarriedUpTheStairs(string id)
        {
            AircraftType.TryFromId(id, out var type);
            var sill = DoorSills.For(type).Passenger;
            var highest = 0f;
            var carried = false;
            for (var t = 0.0; t < 75; t += 0.25)
            {
                Act(RampActivity.Catering, type, t, 75);
                var worker = Worker(RampTask.CateringDoor);
                highest = System.Math.Max(highest, worker.Height);
                carried |= worker.Item == CarriedItem.Canister;
                Assert.That(_scene.LiftHeight, Is.EqualTo(0f), "no hi-loader on a turboprop");
            }

            Assert.That(carried, Is.True);
            Assert.That(highest, Is.EqualTo(sill).Within(0.05f), "up the airstair to the door");
        }

        [Test]
        public void PlanesideBagsPileUpAndAreLoadedInArrivalOrder()
        {
            var type = AircraftType.Atr42;
            var drops = new List<double> { 10, 12, 14, 40 };
            var cart = (-6f, -4f);
            Act(RampActivity.Boarding, type, 5, 180, drops, cart);
            Assert.That(_scene.PlanesideCart, Is.True);
            Assert.That(_scene.PlanesideBags, Is.EqualTo(0));
            Act(RampActivity.Boarding, type, 14.5, 180, drops, cart);
            Assert.That(_scene.PlanesideBags, Is.GreaterThanOrEqualTo(2), "bags wait on the cart");
            Act(RampActivity.Boarding, type, 179, 180, drops, cart);
            Assert.That(_scene.PlanesideBags, Is.EqualTo(0), "all loaded by the end");
            Assert.That(_crew.Any(m => m.Task == RampTask.BaggageHold), Is.True, "a handler works planeside");
        }

        [Test]
        public void QueueTripsNeverStartBeforeTheirBagArrives()
        {
            var arrivals = new List<double> { 5, 6, 30 };
            Assert.That(ServiceChoreography.Queue(arrivals, 10, 4).Picked, Is.EqualTo(0));
            Assert.That(ServiceChoreography.Queue(arrivals, 10, 16).Picked, Is.EqualTo(2), "second trip starts when the first ends");
            Assert.That(ServiceChoreography.Queue(arrivals, 10, 29).Delivered, Is.EqualTo(2), "waits for the third bag");
            Assert.That(ServiceChoreography.Queue(arrivals, 10, 41).Delivered, Is.EqualTo(3));
        }

        [Test]
        public void GroundWorkersNeverWalkThroughTheAirframe()
        {
            var footprint = new List<LayoutRect>();
            foreach (var spec in AircraftCatalogue.All)
            {
                var type = spec.Type;
                var layout = AircraftLayout.For(type);
                layout.Footprint(footprint, forVehicles: false);
                foreach (var activity in new[] { RampActivity.Fuel, RampActivity.Baggage, RampActivity.Catering })
                    for (var t = 0.0; t <= 90; t += 0.5)
                    {
                        Act(activity, type, t, 90);
                        foreach (var a in _actions.Where(a => a.Height < 0.05f))
                            Assert.That(footprint.Any(r => r.Contains(a.X, a.Z, 0.2f)), Is.False,
                                $"{type.Id} {activity} t={t}: worker at ({a.X:0.0}, {a.Z:0.0}) inside the airframe");
                    }
            }
        }
    }
}

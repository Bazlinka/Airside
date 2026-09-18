using System.Linq;
using Airside.Domain;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class EngineStartSequenceTests
    {
        private static (ManualSimulationClock clock, AirlineOperations ops, FleetAircraft plane) Parked()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = new AirlineOperations(clock, new SeededRandomSource(2), DestinationCatalogue.Adelaide, AirlineOperations.AdelaideRegionalBays);
            var player = Airline.Player("Start Air", "#C8102E");
            ops.AddAirline(player);
            return (clock, ops, ops.AddAircraft(player, "VH-STA", AircraftType.Atr42, AirlineOperations.AdelaideRegionalBays[0]));
        }

        [Test]
        public void NewAircraft_IsColdWithDoorsOpenUntilADepartureIsNear()
        {
            var (_, ops, plane) = Parked();
            Assert.That(EngineStartSequence.For(plane, 0).AnyRunning, Is.False);
            Assert.That(EngineStartSequence.For(plane, 0).DoorsOpen, Is.True);

            DestinationCatalogue.TryFind("KGC", out var kgc);
            ops.ScheduleDeparture(plane, kgc, new SimulationTime(3600));
            Assert.That(EngineStartSequence.For(plane, 3600 - 200).AnyRunning, Is.False, "an hour out, still cold");
        }

        [Test]
        public void Start_BeaconDoorsThenRightEngineThenLeft()
        {
            var (_, ops, plane) = Parked();
            DestinationCatalogue.TryFind("KGC", out var kgc);
            const long depart = 1000;
            ops.ScheduleDeparture(plane, kgc, new SimulationTime(depart));

            EngineState At(double before) => EngineStartSequence.For(plane, depart - before);

            Assert.That(At(170).Beacon, Is.True);
            Assert.That(At(170).DoorsOpen, Is.True);
            Assert.That(At(150).DoorsOpen, Is.False);
            Assert.That(At(110).Right, Is.GreaterThan(0f));
            Assert.That(At(110).Left, Is.Zero, "No.2 first");
            Assert.That(At(60).Left, Is.GreaterThan(0f));
            Assert.That(At(30).Right, Is.EqualTo(1f));
            Assert.That(At(0).Left, Is.EqualTo(1f).Within(0.001f), "both running by pushback");

            // Spool is monotonic through the start.
            var last = 0f;
            for (var before = 125.0; before >= 0; before -= 1)
            {
                var right = At(before).Right;
                Assert.That(right, Is.GreaterThanOrEqualTo(last));
                last = right;
            }
        }

        [Test]
        public void Shutdown_AfterParkingThenDoorsOpen()
        {
            var (clock, ops, plane) = Parked();
            DestinationCatalogue.TryFind("KGC", out var kgc);
            ops.ScheduleDeparture(plane, kgc, new SimulationTime(400));
            for (var t = 0L; t < 3 * 3600 && plane.CompletedTrips == 0; t += 5)
            {
                clock.Set(new SimulationTime(t));
                ops.Update();
            }

            Assert.That(plane.State, Is.EqualTo(FleetState.AtStand));
            var parkedAt = plane.StateStartedAt.ElapsedSeconds;
            EngineState After(double s) => EngineStartSequence.For(plane, parkedAt + s);

            Assert.That(After(0).Left, Is.EqualTo(1f));
            Assert.That(After(30).Left, Is.LessThan(1f));
            Assert.That(After(30).Right, Is.EqualTo(1f), "No.1 shuts down first");
            Assert.That(After(80).AnyRunning, Is.False);
            Assert.That(After(80).Beacon, Is.False);
            Assert.That(After(80).DoorsOpen, Is.False);
            Assert.That(After(95).DoorsOpen, Is.True);
        }
    }
}

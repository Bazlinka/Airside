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
            // ADR 0164: the starter base operates Saabs only; an ATR needs the expanded regional apron.
            ops.RestoreCareerState(ops.CareerState.Funds, ops.CareerState.Reliability, nameof(OperatingTier.Provisional),
                null, 0, 0, System.Array.Empty<string>(), baseLevel: PlayerBaseLevel.ExpandedRegional);
            return (clock, ops, ops.AddAircraft(player, "VH-STA", AircraftType.Atr42, AirlineOperations.AdelaideRegionalBays[0]));
        }

        [Test]
        public void NewAircraft_IsColdAndShutUntilBoarding()
        {
            var (_, ops, plane) = Parked();
            Assert.That(EngineStartSequence.For(plane, 0).AnyRunning, Is.False);
            Assert.That(EngineStartSequence.For(plane, 0).DoorsOpen, Is.False,
                "nobody to board: a parked aircraft is shut, not left open on the apron");

            DestinationCatalogue.TryFind("KGC", out var kgc);
            ops.ScheduleDeparture(plane, kgc, new SimulationTime(3600));
            Assert.That(EngineStartSequence.For(plane, 3600 - 200).AnyRunning, Is.False, "an hour out, still cold");
            Assert.That(EngineStartSequence.For(plane, 60).DoorsOpen, Is.False, "an hour before boarding, still shut");
        }

        [Test]
        public void ArrivedAircraft_ShutsItsDoorOnceEveryoneIsOff_AndStaysShutOvernight()
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
            plane.Scheduled = null; // no onward flight booked
            var parkedAt = plane.StateStartedAt.ElapsedSeconds;
            Assert.That(EngineStartSequence.For(plane, parkedAt + 120).DoorsOpen, Is.True, "passengers getting off");
            Assert.That(EngineStartSequence.For(plane, parkedAt + 3600).DoorsOpen, Is.False, "an hour later, shut");
            Assert.That(EngineStartSequence.For(plane, parkedAt + 10 * 3600).DoorsOpen, Is.False, "overnight, shut");
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
            // The door opens for boarding, not before: the player's Boarding stage ends at pushback.
            var boarding = DeparturePrep.BoardingSecondsFor(plane.Type, PlayerBaseLevel.Starter);
            Assert.That(At(boarding - 10).DoorsOpen, Is.True, "doors stay open through boarding");
            Assert.That(At(20).DoorsOpen, Is.True, "boarding still open twenty seconds out");
            Assert.That(At(0).DoorsOpen, Is.False, "doors close when ready for pushback");
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

        [Test]
        public void CancelledDeparture_DoesNotSpoolEngines()
        {
            var (_, ops, plane) = Parked();
            DestinationCatalogue.TryFind("KGC", out var kgc);
            const long depart = 1000;
            Assert.That(ops.ScheduleDeparture(plane, kgc, new SimulationTime(depart)).Accepted, Is.True);
            plane.Scheduled = new ScheduledDeparture(kgc, new SimulationTime(depart), 0, cancelled: true);

            var state = EngineStartSequence.For(plane, depart - 60);
            Assert.That(state.AnyRunning, Is.False, "cancelled bookings stay cold");
            Assert.That(state.Beacon, Is.False);
        }
    }
}

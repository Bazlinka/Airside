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
        public void AircraftInACheck_IsTowedColdWithNobodyBoarding()
        {
            var (clock, ops, plane) = Parked();
            DestinationCatalogue.TryFind("KGC", out var kgc);
            ops.ScheduleDeparture(plane, kgc, new SimulationTime(400));
            for (var t = 0L; t < 3 * 3600 && plane.CompletedTrips == 0; t += 5)
            {
                clock.Set(new SimulationTime(t));
                ops.Update();
            }

            plane.Scheduled = null;
            var parkedAt = plane.StateStartedAt.ElapsedSeconds;
            // Sent for its check while the engines are still winding down and the passengers are still getting off.
            clock.Set(new SimulationTime(parkedAt + 20));
            ops.Update();
            Assert.That(EngineStartSequence.For(plane, parkedAt + 20).AnyRunning, Is.True);
            Assert.That(ops.StartCheck(plane).Accepted, Is.True);

            var now = parkedAt + 25;
            var state = EngineStartSequence.For(plane, now);
            Assert.That(state.AnyRunning, Is.False, "towed with the engines off");
            Assert.That(state.DoorsOpen, Is.False);
            Assert.That(state.Beacon, Is.False);
            var moves = new System.Collections.Generic.List<PassengerMove>();
            BoardingFlow.Moves(plane, now, moves, 900);
            Assert.That(moves, Is.Empty, "nobody deplanes or boards an aircraft on its way to the hangar");
            Assert.That(BoardingFlow.CargoDoorOpen(plane, now), Is.EqualTo(0f));

            var after = plane.CheckUntil.Value.ElapsedSeconds + 1;
            Assert.That(BoardingFlow.InCheck(plane, after), Is.False);
        }

        [Test]
        public void Start_BeaconDoorsThenRightEngineThenLeft()
        {
            var (_, ops, plane) = Parked();
            DestinationCatalogue.TryFind("KGC", out var kgc);
            const long depart = 1000;
            ops.ScheduleDeparture(plane, kgc, new SimulationTime(depart));

            EngineState At(double before) => EngineStartSequence.For(plane, depart - before);

            // The departure countdown (ADR 0177): boarding done at T-3:00, door shut by T-2:30,
            // beacon at T-1:50, then No.2 and No.1 — never an engine with the door open.
            var boarding = DeparturePrep.BoardingSecondsFor(plane.Type, PlayerBaseLevel.Starter);
            Assert.That(At(DepartureCountdown.PrepEndsBeforeSeconds + boarding - 10).DoorsOpen, Is.True,
                "doors open through boarding");
            Assert.That(At(DepartureCountdown.DoorsClosedBeforeSeconds).PassengerDoor, Is.EqualTo(0f),
                "door shut two and a half minutes out");
            Assert.That(At(DepartureCountdown.DoorsClosedBeforeSeconds + 4).PassengerDoor, Is.InRange(0.01f, 0.99f),
                "the door is seen closing, not snapping");
            Assert.That(At(DepartureCountdown.TurbopropBeaconBeforeSeconds + 1).Beacon, Is.False);
            Assert.That(At(DepartureCountdown.TurbopropBeaconBeforeSeconds - 1).Beacon, Is.True);
            Assert.That(At(80).Right, Is.GreaterThan(0f));
            Assert.That(At(80).Left, Is.Zero, "No.2 first");
            Assert.That(At(40).Left, Is.GreaterThan(0f));
            Assert.That(At(20).Right, Is.EqualTo(1f));
            Assert.That(At(0).Left, Is.EqualTo(1f).Within(0.001f), "both running by pushback");
            for (var before = 600.0; before >= 0; before -= 1)
                if (At(before).AnyRunning)
                    Assert.That(At(before).PassengerDoor, Is.EqualTo(0f), $"engine running with the door open at T-{before}");

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
            Assert.That(After(EngineStartSequence.DoorsOpenAfterSeconds + DepartureCountdown.AirstairSeconds).DoorsOpen,
                Is.True, "the airstair has finished unfolding");
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

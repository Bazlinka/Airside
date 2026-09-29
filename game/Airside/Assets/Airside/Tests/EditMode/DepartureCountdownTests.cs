using System.Collections.Generic;
using System.Linq;
using Airside.Domain;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>
    /// The last minutes before a push run in one order on every kind of stand (ADR 0177):
    /// hold shut, boarding done, door shut, bridge or stairs away, beacon, engines. Before, a
    /// turboprop's engines started while its door was open and passengers were boarding, the
    /// three stand types closed their doors minutes apart, and jets started at the gate.
    /// </summary>
    public sealed class DepartureCountdownTests
    {
        private static IEnumerable<TestCaseData> Stands()
        {
            yield return new TestCaseData(AircraftType.Saab340, "BAY-10A").SetName("Saab airstair");
            yield return new TestCaseData(AircraftType.Atr42, "BAY-3").SetName("ATR airstair");
            yield return new TestCaseData(AircraftType.Boeing737800, "GATE-27").SetName("737 stair truck");
            yield return new TestCaseData(AircraftType.Boeing737800, "GATE-21").SetName("737 aerobridge");
        }

        private static (AirlineOperations Ops, FleetAircraft Aircraft, double Push) Booked(AircraftType type, string stand,
            bool player)
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = new AirlineOperations(clock, new SeededRandomSource(3), DestinationCatalogue.Adelaide,
                AirlineOperations.AdelaideStands);
            var airline = player ? Airline.Player("Test Air", "#1F3A93") : Airline.Qantas();
            ops.AddAirline(airline);
            var aircraft = ops.AddAircraft(airline, "VH-TST", type, new StableId(stand));
            aircraft.CompletedTrips = 1;
            // Regional turboprops fly to Kingscote; jets to Melbourne.
            DestinationCatalogue.TryFind(AirlineOperations.NeedsTerminalGate(type) ? "MEL" : "KGC", out var destination);
            var push = 4 * 3600;
            if (player)
                Assert.That(ops.ScheduleDeparture(aircraft, destination, new SimulationTime(push)).Accepted, Is.True,
                    $"{type.Id} booking accepted");
            else
                aircraft.Scheduled = new ScheduledDeparture(destination, new SimulationTime(push));
            return (ops, aircraft, push);
        }

        [TestCaseSource(nameof(Stands))]
        public void NothingHappensOutOfOrder(AircraftType type, string stand)
        {
            foreach (var player in new[] { true, false })
            {
                var (_, aircraft, push) = Booked(type, stand, player);
                Assume.That(aircraft.State, Is.EqualTo(FleetState.AtStand));
                var bridged = AdelaideAerobridges.Serves(aircraft.Stand);
                var stairTruck = BoardingFlow.UsesStairTruck(BoardingFlow.ModeFor(aircraft));
                var moves = new List<PassengerMove>();
                BoardingFlow.Moves(aircraft, push, moves, lookBackSeconds: 4 * 3600);
                var boarding = moves.Where(m => m.Boarding).ToList();
                var firstBoarder = boarding.Min(m => m.StartSeconds);
                var lastBoarder = boarding.Max(m => m.StartSeconds) + BoardingFlow.WalkBudgetSeconds;

                for (var t = push - 900.0; t <= push; t += 0.5)
                {
                    var state = EngineStartSequence.For(aircraft, t);
                    var where = $"{type.Id} {stand} player={player} T-{push - t:0.0}";
                    if (state.AnyRunning || state.Beacon)
                    {
                        Assert.That(state.PassengerDoor, Is.EqualTo(0f), where + ": beacon or engine with the door open");
                        Assert.That(state.CargoDoor, Is.EqualTo(0f), where + ": beacon or engine with the hold open");
                        if (bridged)
                            Assert.That(AerobridgeTimeline.DockedFraction(aircraft, t), Is.EqualTo(0f), where + ": bridge still on");
                        if (stairTruck)
                            Assert.That(BoardingFlow.StairTruckFraction(aircraft, t), Is.EqualTo(0f), where + ": stairs still on");
                    }

                    if (bridged && AerobridgeTimeline.DockedFraction(aircraft, t) < 1f)
                        Assert.That(state.PassengerDoor, Is.EqualTo(0f), where + ": bridge moving with the door open");
                    if (stairTruck && BoardingFlow.StairTruckFraction(aircraft, t) < 1f)
                        Assert.That(state.PassengerDoor, Is.EqualTo(0f), where + ": stairs moving with the door open");
                    if (t >= firstBoarder && t < lastBoarder)
                        Assert.That(state.PassengerDoor, Is.GreaterThan(0.99f), where + ": door shut on a boarding passenger");
                }

                Assert.That(EngineStartSequence.For(aircraft, push).Beacon, Is.True, "beacon on for the push");
            }
        }

        [Test]
        public void ATurbopropStartsOnTheStandAndAJetDuringThePush()
        {
            var (_, saab, push) = Booked(AircraftType.Saab340, "BAY-10A", false);
            var state = EngineStartSequence.For(saab, push);
            Assert.That(state.Left, Is.EqualTo(1f).Within(0.001f));
            Assert.That(state.Right, Is.EqualTo(1f).Within(0.001f));

            var (_, jet, jetPush) = Booked(AircraftType.Boeing737800, "GATE-21", false);
            Assert.That(EngineStartSequence.For(jet, jetPush).AnyRunning, Is.False, "a jet is towed back with its engines off");
            jet.Restore(FleetState.TaxiOut, new SimulationTime((long)jetPush), null);
            var early = EngineStartSequence.For(jet, jetPush + DepartureCountdown.JetRightStartAfterPushSeconds + 10);
            Assert.That(early.Right, Is.GreaterThan(0f), "No.2 starts during the push");
            Assert.That(early.Left, Is.Zero, "No.1 after it");
            Assert.That(EngineStartSequence.For(jet, jetPush + 120).Left, Is.EqualTo(1f));
        }

        [Test]
        public void DoorsMoveOverTheirOwnTimeAndTheHoldShutsBeforeThePassengerDoor()
        {
            var (_, aircraft, push) = Booked(AircraftType.Atr42, "BAY-3", true);
            var countdown = DepartureCountdown.For(aircraft).Value;
            Assert.That(countdown.CargoClosed, Is.LessThan(countdown.DoorsClosed));
            Assert.That(countdown.DoorsClosed, Is.LessThan(countdown.EquipmentAway));
            Assert.That(countdown.EquipmentAway, Is.LessThan(countdown.BeaconOn));
            Assert.That(countdown.BeaconOn, Is.LessThan(countdown.RightStart));
            Assert.That(countdown.RightStart, Is.LessThan(countdown.LeftStart));
            Assert.That(countdown.LeftStart + EngineStartSequence.SpoolSeconds, Is.LessThanOrEqualTo(push));

            var closingStarts = countdown.DoorsClosed - DepartureCountdown.AirstairSeconds;
            var half = EngineStartSequence.For(aircraft, closingStarts + DepartureCountdown.AirstairSeconds / 2).PassengerDoor;
            Assert.That(half, Is.InRange(0.3f, 0.7f), "halfway through folding up");

            var loading = EngineStartSequence.For(aircraft, DepartureCountdown.BaggageStageStart(aircraft) + 30);
            Assert.That(loading.CargoDoor, Is.EqualTo(1f), "hold open while bags go in");
            Assert.That(EngineStartSequence.For(aircraft, countdown.CargoClosed + 1).CargoDoor, Is.EqualTo(0f));
        }

        [Test]
        public void ALatePlayerTurnaroundClosesUpBehindBoardingRatherThanOverlapping()
        {
            var (_, aircraft, push) = Booked(AircraftType.Saab340, "BAY-10A", true);
            // Prep started four minutes late: it now ends a minute after the booked push.
            aircraft.PrepStartedAt = aircraft.PrepStartedAt.Value.Advance(240);
            var ready = DeparturePrep.ReadyAtSeconds(aircraft);
            var countdown = DepartureCountdown.For(aircraft).Value;
            Assert.That(countdown.DoorsClosed, Is.GreaterThanOrEqualTo(ready + BoardingFlow.WalkBudgetSeconds * 0.5
                + DepartureCountdown.HeadcountSeconds), "the last passenger is aboard before the door shuts");
            Assert.That(countdown.BeaconOn, Is.GreaterThan(countdown.DoorsClosed));
            Assert.That(countdown.RightStart, Is.GreaterThan(countdown.EquipmentAway));
            Assert.That(EngineStartSequence.For(aircraft, ready - 1).AnyRunning, Is.False, "no engine while still boarding");
        }
    }
}

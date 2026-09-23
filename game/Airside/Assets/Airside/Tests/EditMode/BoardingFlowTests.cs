using System.Collections.Generic;
using System.Linq;
using Airside.Domain;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>Passengers, airstairs and stair trucks as pure timelines (ADR 0114).</summary>
    public sealed class BoardingFlowTests
    {
        private static (AirlineOperations Ops, FleetAircraft Aircraft) Parked(AircraftType type, string stand, Airline airline)
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = new AirlineOperations(clock, new SeededRandomSource(3), DestinationCatalogue.Adelaide,
                AirlineOperations.AdelaideStands);
            ops.AddAirline(airline);
            var aircraft = ops.AddAircraft(airline, "VH-TST", type, new StableId(stand));
            aircraft.Scheduled = null;
            return (ops, aircraft);
        }

        [Test]
        public void EachStandBoardsTheWayItDoesAtAdelaide()
        {
            Assert.That(BoardingFlow.ModeFor(Parked(AircraftType.Saab340, "BAY-10A", Airline.Rex()).Aircraft),
                Is.EqualTo(BoardingMode.IntegralAirstair), "Saab walk-out: its own airstair door");
            Assert.That(BoardingFlow.ModeFor(Parked(AircraftType.Dash8Q400, "BAY-3", Airline.QantasLink()).Aircraft),
                Is.EqualTo(BoardingMode.IntegralAirstair));
            Assert.That(BoardingFlow.ModeFor(Parked(AircraftType.Boeing737800, "GATE-21", Airline.Qantas()).Aircraft),
                Is.EqualTo(BoardingMode.Aerobridge));
            Assert.That(BoardingFlow.ModeFor(Parked(AircraftType.Boeing737800, "GATE-27", Airline.Qantas()).Aircraft),
                Is.EqualTo(BoardingMode.StairTruck), "a jet off the terminal face gets a stair truck");
        }

        [Test]
        public void SeatsComeFromTheCatalogue()
        {
            Assert.That(AircraftCatalogue.TypicalSeats(AircraftType.Saab340), Is.EqualTo(34));
            Assert.That(AircraftCatalogue.TypicalSeats(AircraftType.Dash8Q400), Is.EqualTo(82));
            Assert.That(AircraftCatalogue.TypicalSeats(AircraftType.Boeing737800), Is.EqualTo(174));
            var (_, saab) = Parked(AircraftType.Saab340, "BAY-10A", Airline.Rex());
            Assert.That(BoardingFlow.PassengerCount(saab), Is.InRange(21, 32));
        }

        [Test]
        public void Arrival_PassengersStepOffOnlyOnceTheDoorIsOpen()
        {
            var (_, saab) = Parked(AircraftType.Saab340, "BAY-10A", Airline.Rex());
            saab.CompletedTrips = 3;
            var moves = new List<PassengerMove>();
            var parkedAt = saab.StateStartedAt.ElapsedSeconds;
            BoardingFlow.Moves(saab, parkedAt + EngineStartSequence.DoorsOpenAfterSeconds - 1, moves);
            Assert.That(moves, Is.Empty, "nobody walks through a shut door");
            BoardingFlow.Moves(saab, parkedAt + 400, moves, lookBackSeconds: 1000);
            Assert.That(moves, Is.Not.Empty);
            Assert.That(moves.All(m => !m.Boarding), Is.True);
            Assert.That(moves.All(m => m.StartSeconds >= parkedAt + EngineStartSequence.DoorsOpenAfterSeconds), Is.True);
        }

        [Test]
        public void Departure_EveryoneIsAboardBeforeTheDoorCloses()
        {
            foreach (var (type, stand, airline) in new[]
                     {
                         (AircraftType.Saab340, "BAY-10A", Airline.Rex()),
                         (AircraftType.Dash8Q400, "BAY-3", Airline.QantasLink()),
                         (AircraftType.Boeing737800, "GATE-27", Airline.Qantas())
                     })
            {
                var (_, aircraft) = Parked(type, stand, airline);
                DestinationCatalogue.TryFind("MEL", out var melbourne);
                var push = aircraft.StateStartedAt.Advance(3 * 3600);
                aircraft.Scheduled = new ScheduledDeparture(melbourne, push);
                var moves = new List<PassengerMove>();
                BoardingFlow.Moves(aircraft, push.ElapsedSeconds, moves, lookBackSeconds: 3 * 3600);
                var boarding = moves.Where(m => m.Boarding).ToList();
                Assert.That(boarding.Count, Is.EqualTo(BoardingFlow.PassengerCount(aircraft)), type.Name);
                var closeBefore = BoardingFlow.ModeFor(aircraft) == BoardingMode.StairTruck
                    ? BoardingFlow.StairTruckDoorsCloseBeforePushSeconds
                    : EngineStartSequence.DoorsCloseBeforeSeconds;
                var doorsClose = push.ElapsedSeconds - closeBefore;
                Assert.That(boarding.Max(m => m.StartSeconds) + BoardingFlow.WalkBudgetSeconds, Is.LessThanOrEqualTo(doorsClose + 0.01),
                    $"{type.Name}: last passenger reaches the door before it shuts");
                Assert.That(boarding.All(m => m.SpeedMetresPerSecond is > 1.0f and < 1.6f), Is.True, "walking pace");
            }
        }

        [Test]
        public void BridgedGatePassengersStayInsideTheTunnel()
        {
            var (_, jet) = Parked(AircraftType.Boeing737800, "GATE-21", Airline.Qantas());
            jet.CompletedTrips = 2;
            var moves = new List<PassengerMove>();
            BoardingFlow.Moves(jet, jet.StateStartedAt.ElapsedSeconds + 600, moves, lookBackSeconds: 1000);
            Assert.That(moves, Is.Empty);
        }

        [Test]
        public void StairTruck_OnBeforeTheDoorOpens_DoorShutBeforeItLeaves_GoneBeforeTheBeacon()
        {
            var (_, jet) = Parked(AircraftType.Boeing737800, "GATE-27", Airline.Qantas());
            DestinationCatalogue.TryFind("SYD", out var sydney);
            var parkedAt = jet.StateStartedAt.ElapsedSeconds;
            var push = jet.StateStartedAt.Advance(3600);
            jet.Scheduled = new ScheduledDeparture(sydney, push);
            Assert.That(BoardingFlow.StairTruckFraction(jet, parkedAt + EngineStartSequence.DoorsOpenAfterSeconds), Is.EqualTo(1f),
                "stairs in place before the door opens");
            Assert.That(EngineStartSequence.For(jet, parkedAt + EngineStartSequence.DoorsOpenAfterSeconds + 1).DoorsOpen, Is.True);
            var leave = push.ElapsedSeconds - BoardingFlow.StairTruckLeaveBeforePushSeconds;
            Assert.That(EngineStartSequence.For(jet, leave - 1).DoorsOpen, Is.False, "door shut before the stairs pull away");
            Assert.That(BoardingFlow.StairTruckFraction(jet, push.ElapsedSeconds - EngineStartSequence.BeaconOnBeforeSeconds),
                Is.EqualTo(0f), "clear before the beacon");
            var (_, saab) = Parked(AircraftType.Saab340, "BAY-10A", Airline.Rex());
            Assert.That(BoardingFlow.StairTruckFraction(saab, 500), Is.EqualTo(0f), "a Saab uses its own airstair");
        }
    }
}

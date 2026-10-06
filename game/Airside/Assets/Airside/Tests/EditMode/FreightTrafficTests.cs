using System;
using System.Linq;
using Airside.Domain;
using Airside.Simulation;
using Airside.Presentation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class FreightTrafficTests
    {
        [TestCase("QFR")]
        [TestCase("DHL")]
        public void CargoCarrierCodes_AreReservedFromPlayerSetup(string code)
        {
            Assert.That(AirlineSetupModel.IsTakenCode(code), Is.True);
        }

        private static AirlineOperations NewGame(out ManualSimulationClock clock)
        {
            clock = new ManualSimulationClock(new SimulationTime(0));
            return AirlineOperations.StartAtAdelaide(clock, new SeededRandomSource(7),
                Airline.Player("Test Air", "#123456"));
        }

        [Test]
        public void NewGame_AddsTwoDedicatedFreightersWithoutDisplacingPassengerTraffic()
        {
            var ops = NewGame(out _);
            var freight = ops.Fleet.Where(a => a.Airline.IsFreightCarrier).ToList();
            Assert.That(freight.Select(a => a.Airline.Name), Is.EquivalentTo(new[] { "Qantas Freight", "DHL Air" }));
            Assert.That(freight.All(a => a.IsFreighter), Is.True);
            Assert.That(freight.All(a => !AirlineOperations.ExemptFromCurfew(a)), Is.True);
            Assert.That(ops.Fleet.Count(a => a.Airline.Id.Value == "QFA"), Is.EqualTo(9));
            var parked = ops.Fleet.Where(a => a.State == FleetState.AtStand).ToList();
            Assert.That(parked.Select(a => a.Stand).Distinct().Count(), Is.EqualTo(parked.Count));
            Assert.That(parked.All(a => AirlineOperations.StandFits(a.Type, a.Stand)), Is.True);
            foreach (var aircraft in freight)
            {
                var at = aircraft.Scheduled?.DepartAt ?? aircraft.StateEndsAt;
                var local = ops.Clock.LocalAt(at.Value);
                Assert.That(AirportCurfew.IsClosed(local), Is.False);
                Assert.That(local.Hour < 7 || local.Hour >= 19, Is.True, "cargo starts on its own bank");
            }
        }

        [Test]
        public void OldSave_GainsFreightOnce_AndRoundTripsItsRoleAndTimetable()
        {
            var ops = NewGame(out var clock);
            var data = AirlineSave.Capture(ops);
            data.Fleet.RemoveAll(a => a.AirlineId is "QFR" or "DHL");
            data.Airlines.RemoveAll(a => a.Id is "QFR" or "DHL");
            var restored = AirlineSave.Restore(data, clock);
            var playerFunds = restored.CareerState.Funds;
            Assert.That(restored.AddMissingFreightOperators(), Is.EqualTo(2));
            Assert.That(restored.AddMissingFreightOperators(), Is.Zero);
            Assert.That(restored.CareerState.Funds, Is.EqualTo(playerFunds), "AI cargo incurs no player refit fee");
            var again = AirlineSave.Restore(AirlineSave.Capture(restored), clock);
            Assert.That(again.AddMissingFreightOperators(), Is.Zero);
            foreach (var aircraft in again.Fleet.Where(a => a.Airline.IsFreightCarrier))
            {
                var original = restored.Fleet.Single(a => a.Registration == aircraft.Registration);
                Assert.That(aircraft.IsFreighter, Is.True);
                Assert.That(aircraft.State, Is.EqualTo(original.State));
                Assert.That(aircraft.StateEndsAt, Is.EqualTo(original.StateEndsAt));
                Assert.That(aircraft.Scheduled?.DepartAt, Is.EqualTo(original.Scheduled?.DepartAt));
            }
        }

        [Test]
        public void FullApron_StartsFreightersAwayWithoutTakingAStand()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var stand = new StableId("GATE-13");
            var ops = new AirlineOperations(clock, new SeededRandomSource(7), DestinationCatalogue.Adelaide, new[] { stand });
            var player = Airline.Player("Test Air", "#123456");
            ops.AddAirline(player);
            var parked = ops.AddAircraft(player, "VH-PAX", AircraftType.Boeing7378, stand);
            Assert.That(ops.AddMissingFreightOperators(), Is.EqualTo(2));
            Assert.That(parked.Stand, Is.EqualTo(stand));
            foreach (var aircraft in ops.Fleet.Where(a => a.Airline.IsFreightCarrier))
            {
                Assert.That(aircraft.State, Is.EqualTo(FleetState.Inbound));
                Assert.That(string.IsNullOrEmpty(aircraft.Stand.Value), Is.True);
                Assert.That(aircraft.IsFreighter, Is.True);
            }
        }

        [Test]
        public void RegionalOnlyAirport_DoesNotGainCargoAirlinesItCannotAccommodate()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = new AirlineOperations(clock, new SeededRandomSource(7), DestinationCatalogue.Adelaide,
                AirlineOperations.AdelaideRegionalBays);
            Assert.That(ops.AddMissingFreightOperators(), Is.Zero);
            Assert.That(ops.Airlines, Is.Empty);
        }

        [TestCase("GATE-13")]
        [TestCase("GATE-27")]
        public void ParkedCargo_HasNoPassengerBoardingEquipmentOrOpenPassengerDoors(string gate)
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = new AirlineOperations(clock, new SeededRandomSource(7), DestinationCatalogue.Adelaide,
                AirlineOperations.AdelaideStands);
            var airline = Airline.DhlAir();
            ops.AddAirline(airline);
            var aircraft = ops.AddAircraft(airline, "VH-TEST", AircraftType.Boeing7378, new StableId(gate));
            aircraft.CompletedTrips = 1; // An arriving freighter unloads during this five-minute window.
            Assert.That(BoardingFlow.PassengerCount(aircraft), Is.Zero);
            Assert.That(BoardingFlow.ModeFor(aircraft), Is.EqualTo(BoardingMode.None));
            Assert.That(BoardingFlow.RemoteBusFraction(aircraft, 5 * 60), Is.Zero);
            Assert.That(BoardingFlow.StairTruckFraction(aircraft, 5 * 60), Is.Zero);
            Assert.That(BoardingFlow.PassengerDoorOpen(aircraft, 5 * 60), Is.Zero);
            Assert.That(AerobridgeTimeline.DockedFraction(aircraft, 5 * 60), Is.Zero);
            Assert.That(AerobridgeTimeline.DoorsOpen(aircraft, 5 * 60), Is.False);
            Assert.That(BoardingFlow.CargoDoorOpen(aircraft, 5 * 60), Is.GreaterThan(0));
        }

        [TestCase(2026, 10, 4)]
        [TestCase(2027, 4, 4)]
        public void FreightBank_UsesAdelaideWallTimeAcrossDaylightSavingChanges(int year, int month, int day)
        {
            var ops = NewGame(out _);
            var aircraft = ops.Fleet.First(a => a.Airline.IsFreightCarrier);
            var local = new DateTime(year, month, day, 4, 50, 0);
            var ready = ops.Clock.AtLocal(local);
            var bank = ops.AiDepartureWithinHours(ready, aircraft);
            var at = ops.Clock.LocalAt(bank);
            Assert.That(at.Date, Is.EqualTo(local.Date));
            Assert.That(at.Hour, Is.EqualTo(5));
            Assert.That(bank.CompareTo(ready), Is.GreaterThanOrEqualTo(0));
        }

        [TestCase(4, 59)]
        [TestCase(5, 10)]
        [TestCase(6, 0)]
        [TestCase(12, 0)]
        [TestCase(20, 35)]
        [TestCase(22, 11)]
        [TestCase(23, 0)]
        public void CargoDeparture_IsNeverEarlyOrDuringCurfew_AndBankSnappingIsStable(int hour, int minute)
        {
            var ops = NewGame(out _);
            var aircraft = ops.Fleet.First(a => a.Airline.IsFreightCarrier);
            var local = ops.Clock.LocalAt(new SimulationTime(0)).Date.AddDays(1).AddHours(hour).AddMinutes(minute);
            var ready = ops.Clock.AtLocal(local);
            var at = ops.AiDepartureWithinHours(ready, aircraft);
            var bank = ops.Clock.LocalAt(at);
            Assert.That(at.CompareTo(ready), Is.GreaterThanOrEqualTo(0));
            Assert.That(AirportCurfew.IsClosed(bank), Is.False);
            Assert.That(bank.Hour == 5 || bank.Hour >= 20 && bank.Hour <= 22, Is.True);
            Assert.That(ops.AiDepartureWithinHours(at, aircraft), Is.EqualTo(at));
            if (hour >= 6 && hour < 20)
                Assert.That(bank.Hour, Is.EqualTo(20), "daytime-ready cargo waits for the evening bank");
            if (hour >= 23 || hour == 22 && minute > 10)
                Assert.That(bank.Date, Is.EqualTo(local.Date.AddDays(1)));
        }

        [Test]
        public void CargoSoak_FliesItsNetworkAcrossTwoNights_WithCompatibleUniqueStandsAndNoPlayerSettlement()
        {
            var ops = NewGame(out var clock);
            var funds = ops.CareerState.Funds;
            for (var t = 0; t < 48 * 3600; t += 120)
            {
                clock.Advance(120);
                ops.Update();
                var parked = ops.Fleet.Where(a => a.State == FleetState.AtStand).ToList();
                Assert.That(parked.Select(a => a.Stand).Distinct().Count(), Is.EqualTo(parked.Count));
                foreach (var aircraft in ops.Fleet.Where(a => a.Airline.IsFreightCarrier))
                {
                    var destination = aircraft.CurrentDestination ?? aircraft.Scheduled?.Destination;
                    if (destination.HasValue)
                    {
                        Assert.That(AirlineOperations.AiNetworkFor(aircraft.Airline).Select(n => n.Code),
                            Does.Contain(destination.Value.Code));
                        Assert.That(ops.CanReach(aircraft, destination.Value), Is.True);
                    }
                    if (aircraft.Scheduled.HasValue)
                        Assert.That(AirportCurfew.IsClosed(ops.Clock.LocalAt(aircraft.Scheduled.Value.DepartAt)), Is.False);
                    if (aircraft.State == FleetState.AtStand)
                        Assert.That(AirlineOperations.StandFits(aircraft.Type, aircraft.Stand), Is.True);
                }
            }
            Assert.That(ops.Fleet.Where(a => a.Airline.IsFreightCarrier).All(a => a.CompletedTrips > 0), Is.True);
            Assert.That(ops.TotalSettlements, Is.Zero);
            Assert.That(ops.CareerState.Funds, Is.EqualTo(funds));
        }
    }
}

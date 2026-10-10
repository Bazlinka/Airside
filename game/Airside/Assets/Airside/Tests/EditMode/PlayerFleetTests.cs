using System;
using System.Collections.Generic;
using System.Linq;
using Airside.Domain;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>The unified view of the player's fleet and the sell rules around it (ADR 0239).</summary>
    public sealed class PlayerFleetTests
    {
        private static (ManualSimulationClock Clock, AirlineOperations Operations) NewAirline()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var operations = new AirlineOperations(clock, new SeededRandomSource(91),
                DestinationCatalogue.Adelaide, AirlineOperations.AdelaideStands);
            var player = Airline.Player("Test Regional", "#245AA6");
            operations.AddAirline(player);
            operations.AddAircraft(player, "VH-TST", AircraftType.Saab340,
                AirlineOperations.AdelaideRegionalBays[0]);
            return (clock, operations);
        }

        /// <summary>A Domestic-tier airline with a Melbourne base and one Dash 8-400 (VH-O01) based there.</summary>
        private static (ManualSimulationClock Clock, AirlineOperations Operations) WithMelbourneDash()
        {
            var (clock, operations) = NewAirline();
            operations.RestoreCareerState(500_000 * FlightCostModel.LegacySaveMoneyScale, 95, nameof(OperatingTier.Domestic), null, 0, 0,
                Array.Empty<string>(), Array.Empty<string>(), 100,
                baseLevel: PlayerBaseLevel.JetGate, manualRotations: 12);
            Assert.That(operations.OpenOutstationBase("MEL").Accepted, Is.True);
            var bought = operations.BuyAircraftAtOutstation(AircraftType.Dash8Q400, "MEL");
            Assert.That(bought.Accepted, Is.True, bought.Reason);
            return (clock, operations);
        }

        private static void Fly(ManualSimulationClock clock, AirlineOperations operations, string registration,
            string destination)
        {
            var plan = operations.ScheduleOutstationService(registration, destination, new SimulationTime(1800));
            Assert.That(plan.Accepted, Is.True, plan.Reason);
            var returnAt = operations.OutstationFleet.Single(a => a.Registration == registration).ReturnAtSeconds;
            clock.Set(new SimulationTime(returnAt));
            operations.Update();
        }

        [Test]
        public void Entries_ListLiveAircraftThenOutstationAircraftWithTheirBase()
        {
            var (_, operations) = WithMelbourneDash();
            var entries = new List<PlayerFleetEntry>();

            PlayerFleet.Entries(operations, new SimulationTime(0), entries);

            Assert.That(entries.Count, Is.EqualTo(operations.PlayerFleetCount()));
            Assert.That(entries[0].Registration, Is.EqualTo("VH-TST"));
            Assert.That(entries[0].IsOutstation, Is.False);
            Assert.That(entries[0].BaseCode, Is.EqualTo("ADL"));
            Assert.That(entries[0].Kind, Is.EqualTo(PlayerFleetKind.Parked));
            Assert.That(entries[1].Registration, Is.EqualTo("VH-O01"));
            Assert.That(entries[1].IsOutstation, Is.True);
            Assert.That(entries[1].BaseCode, Is.EqualTo("MEL"));
            Assert.That(entries[1].Kind, Is.EqualTo(PlayerFleetKind.Parked));
            Assert.That(entries[1].ResaleValue, Is.EqualTo(AirlineOperations.ResaleValue(AircraftType.Dash8Q400)));
            Assert.That(entries[1].CanSell, Is.True);
        }

        [Test]
        public void Find_ReturnsEitherKindOfAircraftAndNullForAnUnknownMark()
        {
            var (_, operations) = WithMelbourneDash();
            var now = new SimulationTime(0);

            Assert.That(PlayerFleet.Find(operations, now, "VH-TST")?.IsOutstation, Is.False);
            Assert.That(PlayerFleet.Find(operations, now, "VH-O01")?.IsOutstation, Is.True);
            Assert.That(PlayerFleet.Find(operations, now, "VH-NOPE"), Is.Null);
            Assert.That(PlayerFleet.Find(operations, now, null), Is.Null);
        }

        [Test]
        public void OutstationSummary_CountsAircraftPerBaseAndIsEmptyWithNone()
        {
            var (_, none) = NewAirline();
            Assert.That(PlayerFleet.OutstationSummary(none), Is.Empty);

            var (_, operations) = WithMelbourneDash();
            Assert.That(PlayerFleet.OutstationSummary(operations), Is.EqualTo("1 at MEL"));
        }

        [Test]
        public void Entries_ShowABookedServiceThenAnAirborneOne()
        {
            var (_, operations) = WithMelbourneDash();
            Assert.That(operations.ScheduleOutstationService("VH-O01", "SYD", new SimulationTime(1800)).Accepted, Is.True);
            var entries = new List<PlayerFleetEntry>();

            PlayerFleet.Entries(operations, new SimulationTime(600), entries);
            var booked = entries.Single(e => e.IsOutstation);
            Assert.That(booked.Kind, Is.EqualTo(PlayerFleetKind.Booked));
            Assert.That(booked.DestinationCode, Is.EqualTo("SYD"));
            Assert.That(booked.NextAtSeconds, Is.EqualTo(1800));
            Assert.That(booked.CanSell, Is.False, "a planned flight has to finish before a sale");

            PlayerFleet.Entries(operations, new SimulationTime(2400), entries);
            var airborne = entries.Single(e => e.IsOutstation);
            Assert.That(airborne.Kind, Is.EqualTo(PlayerFleetKind.Airborne));
            Assert.That(airborne.NextAtSeconds, Is.EqualTo(operations.OutstationFleet.Single().ReturnAtSeconds));
        }

        [Test]
        public void TryPosition_ParksThenFliesOutTurnsRoundAndComesBack()
        {
            var (_, operations) = WithMelbourneDash();
            var aircraft = operations.OutstationFleet.Single();
            Assert.That(DestinationCatalogue.TryFind("MEL", out var melbourne), Is.True);
            Assert.That(DestinationCatalogue.TryFind("SYD", out var sydney), Is.True);

            Assert.That(PlayerFleet.TryPosition(aircraft, new SimulationTime(0), out var lat, out var lon,
                out var phase, out _), Is.True);
            Assert.That(phase, Is.EqualTo(OutstationPhase.Parked));
            Assert.That(lat, Is.EqualTo(melbourne.Latitude).Within(1e-9));
            Assert.That(lon, Is.EqualTo(melbourne.Longitude).Within(1e-9));

            Assert.That(operations.ScheduleOutstationService("VH-O01", "SYD", new SimulationTime(1800)).Accepted, Is.True);
            var depart = aircraft.DepartAtSeconds;
            var airborne = (aircraft.ReturnAtSeconds - depart - PlayerFleet.TurnaroundSeconds) / 2;

            PlayerFleet.TryPosition(aircraft, new SimulationTime(depart - 60), out _, out _, out phase, out _);
            Assert.That(phase, Is.EqualTo(OutstationPhase.Parked), "a booked service is still on the ground");

            PlayerFleet.TryPosition(aircraft, new SimulationTime(depart + airborne / 2), out _, out _, out phase,
                out var progress);
            Assert.That(phase, Is.EqualTo(OutstationPhase.Outbound));
            Assert.That(progress, Is.EqualTo(0.5).Within(0.01));

            PlayerFleet.TryPosition(aircraft, new SimulationTime(depart + airborne + 60), out lat, out lon,
                out phase, out _);
            Assert.That(phase, Is.EqualTo(OutstationPhase.Turnaround));
            Assert.That(lat, Is.EqualTo(sydney.Latitude).Within(1e-9));
            Assert.That(lon, Is.EqualTo(sydney.Longitude).Within(1e-9));

            PlayerFleet.TryPosition(aircraft, new SimulationTime(aircraft.ReturnAtSeconds - 60), out _, out _,
                out phase, out progress);
            Assert.That(phase, Is.EqualTo(OutstationPhase.Inbound));
            Assert.That(progress, Is.GreaterThan(0.9));
        }

        [Test]
        public void SellOutstationAircraft_RefundsOnceAndRefusesAPlannedFlight()
        {
            var (_, operations) = WithMelbourneDash();
            Assert.That(operations.ScheduleOutstationService("VH-O01", "SYD", new SimulationTime(1800)).Accepted, Is.True);
            Assert.That(operations.SellOutstationAircraft("VH-O01").Accepted, Is.False,
                "a planned flight would be left pointing at a registration that no longer exists");
            Assert.That(operations.OutstationFleet.Count, Is.EqualTo(1));

            Assert.That(operations.SellOutstationAircraft("VH-NOPE").Accepted, Is.False);
        }

        [Test]
        public void SellOutstationAircraft_PaysTheResaleFractionAndDropsItsRepeatPlan()
        {
            var (_, operations) = WithMelbourneDash();
            Assert.That(operations.SetRepeatSchedule("VH-O01", "SYD", 12).Accepted, Is.True);
            var funds = operations.CareerState.Funds;

            var result = operations.SellOutstationAircraft("VH-O01");

            Assert.That(result.Accepted, Is.True, result.Reason);
            Assert.That(operations.OutstationFleet, Is.Empty);
            Assert.That(operations.RepeatSchedules, Is.Empty);
            Assert.That(operations.CareerState.Funds,
                Is.EqualTo(funds + AirlineOperations.ResaleValue(AircraftType.Dash8Q400)));
            Assert.That(AirlineOperations.ResaleValue(AircraftType.Dash8Q400),
                Is.LessThan(AircraftAcquisition.Dash8Q400.Price), "reselling is always a net loss");
        }

        [Test]
        public void SoldOutstationMark_IsNotReissuedOnceItHasFlown()
        {
            var (clock, operations) = WithMelbourneDash();
            Fly(clock, operations, "VH-O01", "SYD");
            Assert.That(operations.CareerState.HasSettlementHistory("VH-O01"), Is.True);
            Assert.That(operations.SellOutstationAircraft("VH-O01").Accepted, Is.True);

            var again = operations.BuyAircraftAtOutstation(AircraftType.Dash8Q400, "MEL");

            Assert.That(again.Accepted, Is.True, again.Reason);
            Assert.That(operations.OutstationFleet.Single().Registration, Is.EqualTo("VH-O02"),
                "a reissued mark would restart at trip 1 and its first flights would never pay");
        }

        [Test]
        public void SellAircraft_RefusesABookedFlightAndKeepsTheAircraft()
        {
            var (_, operations) = NewAirline();
            operations.RestoreCareerState(500_000 * FlightCostModel.LegacySaveMoneyScale, 95, nameof(OperatingTier.Domestic), null, 0, 0,
                Array.Empty<string>(), Array.Empty<string>(), 100, baseLevel: PlayerBaseLevel.Starter);
            Assert.That(operations.BuyAircraft(AircraftType.Saab340).Accepted, Is.True);
            var second = operations.Fleet.Single(a => a.Registration != "VH-TST");
            Assert.That(DestinationCatalogue.TryFind("KGC", out var island), Is.True);
            Assert.That(operations.ScheduleDeparture(second, island, new SimulationTime(3600)).Accepted, Is.True);
            var funds = operations.CareerState.Funds;

            var sale = operations.SellAircraft(second);

            Assert.That(sale.Accepted, Is.False);
            Assert.That(operations.Fleet.Contains(second), Is.True);
            Assert.That(operations.CareerState.Funds, Is.EqualTo(funds), "a refused sale moves no money");
        }

        [Test]
        public void SellAircraft_DropsTheRepeatPlanOfTheAircraftItSells()
        {
            var (_, operations) = NewAirline();
            operations.RestoreCareerState(500_000 * FlightCostModel.LegacySaveMoneyScale, 95, nameof(OperatingTier.Domestic), null, 0, 0,
                Array.Empty<string>(), Array.Empty<string>(), 100, baseLevel: PlayerBaseLevel.Starter,
                manualRotations: 12);
            Assert.That(operations.BuyAircraft(AircraftType.Saab340).Accepted, Is.True);
            var second = operations.Fleet.Single(a => a.Registration != "VH-TST");
            Assert.That(operations.SetRepeatSchedule(second.Registration, "KGC", 12).Accepted, Is.True);

            Assert.That(operations.SellAircraft(second).Accepted, Is.True);

            Assert.That(operations.RepeatSchedules, Is.Empty);
        }

        [Test]
        public void OutstationLogbook_RecordsRevenueAndRoutesAndSurvivesSave()
        {
            var (clock, operations) = WithMelbourneDash();
            Fly(clock, operations, "VH-O01", "SYD");
            var aircraft = operations.OutstationFleet.Single();
            Assert.That(aircraft.HistoryFlights, Is.EqualTo(1));
            Assert.That(aircraft.LifetimeRevenue, Is.GreaterThan(0));
            Assert.That(aircraft.RouteHistory.Single().DestinationCode, Is.EqualTo("SYD"));

            var restored = AirlineSave.Restore(AirlineSave.Capture(operations), clock);
            var copy = restored.OutstationFleet.Single();

            Assert.That(copy.HistoryFlights, Is.EqualTo(aircraft.HistoryFlights));
            Assert.That(copy.LifetimeRevenue, Is.EqualTo(aircraft.LifetimeRevenue));
            Assert.That(copy.JoinedAtSeconds, Is.EqualTo(aircraft.JoinedAtSeconds));
            Assert.That(copy.FavouriteRoute.DestinationCode, Is.EqualTo("SYD"));
        }

        [Test]
        public void InvalidOutstationLogbook_IsRejected()
        {
            var (clock, operations) = WithMelbourneDash();
            Fly(clock, operations, "VH-O01", "SYD");
            var saved = AirlineSave.Capture(operations);
            saved.OutstationFleet[0].HistoryFlights = 5;
            Assert.Throws<FormatException>(() => AirlineSave.Restore(saved, clock),
                "route totals must match the recorded flights");

            saved = AirlineSave.Capture(operations);
            saved.OutstationFleet[0].JoinedAtSeconds = saved.ClockSeconds + 1000;
            Assert.Throws<FormatException>(() => AirlineSave.Restore(saved, clock),
                "an aircraft cannot have joined the airline in the future");
        }

        [Test]
        public void AVersion19Save_LoadsOutstationAircraftWithAnEmptyLogbook()
        {
            var (clock, operations) = WithMelbourneDash();
            var saved = AirlineSave.Capture(operations);
            saved.Version = 19;
            saved.OutstationFleet[0].HistoryFlights = 3;

            var restored = AirlineSave.Restore(saved, clock);

            Assert.That(restored.OutstationFleet.Single().HistoryFlights, Is.EqualTo(0),
                "pre-v20 saves never had an outstation logbook, so the record is ignored");
        }
    }
}

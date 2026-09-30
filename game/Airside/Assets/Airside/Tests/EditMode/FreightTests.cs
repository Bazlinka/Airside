using System.Linq;
using Airside.Domain;
using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>Freight flights (ADR 0194): a refit, a second way to earn, a cargo livery.</summary>
    public sealed class FreightTests
    {
        private static Destination Code(string code)
        {
            Assert.That(DestinationCatalogue.TryFind(code, out var destination), Is.True, code);
            return destination;
        }

        private static (ManualSimulationClock clock, AirlineOperations ops, FleetAircraft plane) PlayerOnly()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = new AirlineOperations(clock, new SeededRandomSource(7), DestinationCatalogue.Adelaide,
                AirlineOperations.AdelaideRegionalBays);
            var player = Airline.Player("Soak Air", "#1F3A93");
            ops.AddAirline(player);
            var plane = ops.AddAircraft(player, "VH-PAX", AircraftType.Saab340,
                AirlineOperations.AdelaideRegionalBays[0]);
            return (clock, ops, plane);
        }

        private static void RunTo(ManualSimulationClock clock, AirlineOperations ops, long seconds)
        {
            clock.Set(new SimulationTime(seconds));
            ops.Update();
        }

        [Test]
        public void EveryType_HasAPayloadAndAPositiveRefitCost()
        {
            foreach (var spec in AircraftCatalogue.All)
            {
                Assert.That(FreightRates.CapacityTonnes(spec.Type), Is.GreaterThan(0), spec.Id);
                Assert.That(FreightRates.ConversionCost(spec.Type), Is.GreaterThan(0), spec.Id);
            }

            Assert.That(FreightRates.ConversionCost(AircraftType.Saab340),
                Is.LessThan(FreightRates.ConversionCost(AircraftType.Boeing7378)));
            Assert.That(FreightRates.ConversionCost(AircraftType.Boeing7378),
                Is.LessThan(FreightRates.ConversionCost(AircraftType.AirbusA350900)));
        }

        [Test]
        public void Refit_ChargesTheFeeAndTurnsTheAircraftIntoAFreighterAndBack()
        {
            var (_, ops, plane) = PlayerOnly();
            var funds = ops.CareerState.Funds;
            var fee = FreightRates.ConversionCost(plane.Type);

            Assert.That(ops.SetFreighter(plane, true).Accepted, Is.True);
            Assert.That(plane.IsFreighter, Is.True);
            Assert.That(ops.CareerState.Funds, Is.EqualTo(funds - fee));

            Assert.That(ops.SetFreighter(plane, true).Accepted, Is.False, "already a freighter");

            Assert.That(ops.SetFreighter(plane, false).Accepted, Is.True);
            Assert.That(plane.IsFreighter, Is.False);
            Assert.That(ops.CareerState.Funds, Is.EqualTo(funds - 2 * fee));
        }

        [Test]
        public void Refit_IsRefusedWithABookedFlightOrTooLittleCash()
        {
            var (_, ops, plane) = PlayerOnly();
            var when = new SimulationTime(DeparturePrep.LeadSeconds(plane.Type) + 600);
            Assert.That(ops.ScheduleDeparture(plane, Code("KGC"), when).Accepted, Is.True);
            Assert.That(ops.SetFreighter(plane, true).Accepted, Is.False);
            Assert.That(plane.IsFreighter, Is.False);

            Assert.That(ops.CancelDeparture(plane).Accepted, Is.True);
            ops.RestoreCareerState(FreightRates.ConversionCost(plane.Type) - 1, 100, nameof(OperatingTier.Provisional),
                null, 0, 0, System.Array.Empty<string>(), System.Array.Empty<string>(), 0);
            var refused = ops.SetFreighter(plane, true);
            Assert.That(refused.Accepted, Is.False);
            Assert.That(refused.Reason, Does.Contain("refit costs"));
        }

        [Test]
        public void AiAircraft_CannotBeRefitted()
        {
            var (_, ops, _) = PlayerOnly();
            var rex = Airline.Rex();
            ops.AddAirline(rex);
            var saab = ops.AddAircraft(rex, "VH-ZRC", AircraftType.Saab340, AirlineOperations.AdelaideRegionalBays[1]);
            Assert.That(ops.SetFreighter(saab, true).Accepted, Is.False);
        }

        [Test]
        public void FreighterForecast_IsInTonnesAndPaysFromTheFreightThePlaceOffers()
        {
            var (_, ops, plane) = PlayerOnly();
            var passengers = ops.Forecast(ops.Home, Code("KGC"), plane);
            Assert.That(passengers.IsFreight, Is.False);
            Assert.That(passengers.LoadText, Does.Contain("seats"));

            Assert.That(ops.SetFreighter(plane, true).Accepted, Is.True);
            var freight = ops.Forecast(ops.Home, Code("KGC"), plane);
            Assert.That(freight.IsFreight, Is.True);
            Assert.That(freight.LoadText, Does.Contain("t freight"));
            Assert.That(freight.FreightTonnes, Is.EqualTo(FreightRates.DemandTonnes("KGC")).Within(1e-9));
            Assert.That(freight.FreightCapacityTonnes, Is.EqualTo(FreightRates.CapacityTonnes(plane.Type)));
            Assert.That(freight.Cost, Is.EqualTo(passengers.Cost), "same dispatch cost, so a cancel refunds exactly");
            Assert.That(freight.Revenue, Is.GreaterThan(0));
        }

        [Test]
        public void FreightDemand_IsCappedAtThePayload()
        {
            var forecast = RouteForecast.ForFreight(DestinationCatalogue.Adelaide, Code("SIN"), AircraftType.Saab340);
            Assert.That(forecast.FreightTonnes, Is.EqualTo(FreightRates.CapacityTonnes(AircraftType.Saab340)));
            var full = FlightEconomics.FlightPay(AircraftType.Saab340,
                DestinationCatalogue.Adelaide.DistanceKmTo(Code("SIN")), RouteAccess.BandOf(Code("SIN")));
            Assert.That(forecast.Revenue, Is.EqualTo((long)System.Math.Round(full
                * (FreightRates.BasePayFloor + FreightRates.FillGain))));
        }

        [Test]
        public void ThinPassengerRoutes_CanPayBetterByFreight()
        {
            // Coober Pedy: opal and mining stores, few passengers for a Dash 8-sized hold.
            var dash = AircraftType.Dash8Q400;
            var ped = Code("CPD");
            var seats = RouteForecast.For(DestinationCatalogue.Adelaide, ped, dash);
            var tonnes = RouteForecast.ForFreight(DestinationCatalogue.Adelaide, ped, dash);
            Assert.That(tonnes.Revenue, Is.GreaterThanOrEqualTo(seats.Revenue));
        }

        [Test]
        public void FreighterRotation_PaysItsFreightForecastOnReturn()
        {
            var (clock, ops, plane) = PlayerOnly();
            Assert.That(ops.SetFreighter(plane, true).Accepted, Is.True);
            var expected = ops.Forecast(ops.Home, Code("KGC"), plane).Revenue;
            var departAt = DeparturePrep.LeadSeconds(plane.Type);
            Assert.That(ops.ScheduleDeparture(plane, Code("KGC"), new SimulationTime(departAt)).Accepted, Is.True);
            RunTo(clock, ops, departAt);
            var parkedBy = clock.Now.ElapsedSeconds + 6 * 3600;
            while (plane.CompletedTrips == 0 && clock.Now.ElapsedSeconds < parkedBy)
                RunTo(clock, ops, (ops.NextEventAt() ?? clock.Now.Advance(60)).ElapsedSeconds);
            Assert.That(plane.CompletedTrips, Is.EqualTo(1));
            Assert.That(ops.RecentSettlements.Last().Payment, Is.EqualTo(expected));
        }

        [Test]
        public void Freighter_CarriesNoPassengers()
        {
            var (_, ops, plane) = PlayerOnly();
            Assert.That(BoardingFlow.PassengerCount(plane), Is.GreaterThan(0));
            Assert.That(ops.SetFreighter(plane, true).Accepted, Is.True);
            Assert.That(BoardingFlow.PassengerCount(plane), Is.EqualTo(0));
        }

        [Test]
        public void Save_RemembersWhichAircraftAreFreighters()
        {
            var (clock, ops, plane) = PlayerOnly();
            Assert.That(ops.SetFreighter(plane, true).Accepted, Is.True);
            var json = AirlineSave.Capture(ops);
            Assert.That(json.Version, Is.EqualTo(AirlineSaveData.CurrentVersion));
            Assert.That(json.Fleet.Single(f => f.Registration == "VH-PAX").IsFreighter, Is.True);

            var restored = AirlineSave.Restore(json, new ManualSimulationClock(clock.Now));
            Assert.That(restored.FleetOf(restored.PlayerAirline).Single().IsFreighter, Is.True);
        }

        [Test]
        public void OlderSaves_LoadAsAllPassenger()
        {
            var (clock, ops, plane) = PlayerOnly();
            Assert.That(ops.SetFreighter(plane, true).Accepted, Is.True);
            var json = AirlineSave.Capture(ops);
            json.Version = 18;

            var restored = AirlineSave.Restore(json, new ManualSimulationClock(clock.Now));
            Assert.That(restored.FleetOf(restored.PlayerAirline).Single().IsFreighter, Is.False);
        }

        [Test]
        public void CargoTitle_IsTheWordmarkPlusCargo_AndFitsTheFuselage()
        {
            Assert.That(Airline.Rex().FreightTitle, Is.EqualTo("REX CARGO"));
            Assert.That(Airline.VirginAustralia().FreightTitle, Is.EqualTo("VIRGIN CARGO"));
            Assert.That(Airline.Player("Southern Cross Regional Air", "#123456").FreightTitle.Length,
                Is.LessThanOrEqualTo(18));
            Assert.That(Airline.Player("Skyline Cargo", "#123456").FreightTitle, Is.EqualTo("SKYLINE CARGO"));
        }
    }
}

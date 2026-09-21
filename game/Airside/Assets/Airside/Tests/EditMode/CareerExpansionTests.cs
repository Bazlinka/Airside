using System;
using System.Linq;
using Airside.Domain;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>ADR 0056: route bands, rotating contracts, purchase, auto-stand, departure prep.</summary>
    public sealed class CareerExpansionTests
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
            var player = Airline.Player("Southern Cross Regional", "#C8102E");
            ops.AddAirline(player);
            var plane = ops.AddAircraft(player, "VH-PAX", AircraftType.Saab340, AirlineOperations.AdelaideRegionalBays[0]);
            return (clock, ops, plane);
        }

        [Test]
        public void RouteAccess_LinksTypesToBands()
        {
            Assert.That(RouteAccess.BandOf("KGC"), Is.EqualTo(RouteBand.Regional));
            Assert.That(RouteAccess.BandOf("MEL"), Is.EqualTo(RouteBand.Domestic));
            Assert.That(RouteAccess.BandOf("PER"), Is.EqualTo(RouteBand.National));
            Assert.That(RouteAccess.BandOf("AKL"), Is.EqualTo(RouteBand.Tasman));
            Assert.That(RouteAccess.BandOf("SIN"), Is.EqualTo(RouteBand.LongHaul));
            Assert.That(RouteAccess.Ceiling(AircraftType.Atr42), Is.EqualTo(RouteBand.Regional));
            Assert.That(RouteAccess.Ceiling(AircraftType.Dash8Q400), Is.EqualTo(RouteBand.Domestic));
            Assert.That(RouteAccess.Allows(AircraftType.Atr42, Code("KGC")), Is.True);
            Assert.That(RouteAccess.Allows(AircraftType.Atr42, Code("MEL")), Is.False);
            Assert.That(RouteAccess.Allows(AircraftType.Dash8Q400, Code("MEL")), Is.True);
            Assert.That(RouteAccess.Allows(AircraftType.Dash8Q400, Code("PER")), Is.False);
        }

        /// <summary>
        /// "Requires Regional tier" (an OperatingTier career milestone) and "flies Regional
        /// routes" (a RouteBand) share the word "Regional" for two different systems — a real
        /// source of player confusion the Fleet market/detail copy fixes by naming actual
        /// destinations instead of leaving the band as an abstract label. Every band needs one,
        /// and no two bands should silently end up with the same string (a copy-paste bug that
        /// would make two different capability levels read as identical).
        /// </summary>
        [Test]
        public void ExampleDestinations_NamesRealPlacesForEveryBand()
        {
            var seen = new System.Collections.Generic.HashSet<string>();
            foreach (RouteBand band in Enum.GetValues(typeof(RouteBand)))
            {
                var examples = RouteAccess.ExampleDestinations(band);
                Assert.That(examples, Is.Not.Null.And.Not.Empty, band.ToString());
                Assert.That(seen.Add(examples), Is.True,
                    $"{band} repeats another band's example text: \"{examples}\"");
            }
        }

        [Test]
        public void DeparturePrep_RunsFuelThenCateringThenBoardingBeforePushback()
        {
            var (clock, ops, plane) = PlayerOnly();
            var departAt = new SimulationTime(DeparturePrep.TotalSeconds(plane.Type));
            Assert.That(ops.ScheduleDeparture(plane, Code("KGC"), departAt).Accepted, Is.True);
            Assert.That(plane.PrepStartedAt, Is.EqualTo(new SimulationTime(0)),
                "booking exactly one prep-window ahead starts fuelling now");

            var start = DeparturePrep.For(plane, new SimulationTime(0));
            Assert.That(start.Stage, Is.EqualTo(DeparturePrepStage.Fuel));
            Assert.That(start.FuelProgress, Is.EqualTo(0));
            Assert.That(start.CateringProgress, Is.EqualTo(0));
            Assert.That(start.BoardingProgress, Is.EqualTo(0));
            Assert.That(start.Label, Is.EqualTo("Fuelling 0%"));
            Assert.That(start.RemainingSeconds, Is.EqualTo(DeparturePrep.FuelSeconds));

            var midFuel = DeparturePrep.For(plane, new SimulationTime(DeparturePrep.FuelSeconds / 2));
            Assert.That(midFuel.Stage, Is.EqualTo(DeparturePrepStage.Fuel));
            Assert.That(midFuel.FuelProgress, Is.EqualTo(0.5).Within(0.001));
            Assert.That(midFuel.CateringProgress, Is.EqualTo(0));
            Assert.That(midFuel.BoardingProgress, Is.EqualTo(0));
            Assert.That(midFuel.Label, Is.EqualTo("Fuelling 50%"));
            Assert.That(midFuel.RemainingSeconds, Is.EqualTo(DeparturePrep.FuelSeconds / 2));

            var catering = DeparturePrep.For(plane, new SimulationTime(DeparturePrep.FuelSeconds));
            Assert.That(catering.Stage, Is.EqualTo(DeparturePrepStage.Catering));
            Assert.That(catering.FuelProgress, Is.EqualTo(1));
            Assert.That(catering.CateringProgress, Is.EqualTo(0));
            Assert.That(catering.BoardingProgress, Is.EqualTo(0));
            Assert.That(catering.Label, Is.EqualTo("Catering 0%"));

            var midCatering = DeparturePrep.For(plane,
                new SimulationTime(DeparturePrep.FuelSeconds + 30));
            Assert.That(midCatering.Stage, Is.EqualTo(DeparturePrepStage.Catering));
            Assert.That(midCatering.FuelProgress, Is.EqualTo(1));
            Assert.That(midCatering.CateringProgress, Is.EqualTo(0.4).Within(0.001));
            Assert.That(midCatering.BoardingProgress, Is.EqualTo(0));
            Assert.That(midCatering.Label, Is.EqualTo("Catering 40%"));

            var boarding = DeparturePrep.For(plane,
                new SimulationTime(DeparturePrep.FuelSeconds + DeparturePrep.CateringSeconds));
            Assert.That(boarding.Stage, Is.EqualTo(DeparturePrepStage.Boarding));
            Assert.That(boarding.FuelProgress, Is.EqualTo(1));
            Assert.That(boarding.CateringProgress, Is.EqualTo(1));
            Assert.That(boarding.BoardingProgress, Is.EqualTo(0));
            Assert.That(boarding.Label, Is.EqualTo("Boarding 0%"));

            var midBoard = DeparturePrep.For(plane,
                new SimulationTime(DeparturePrep.FuelSeconds + DeparturePrep.CateringSeconds
                    + DeparturePrep.BoardingSeconds / 2));
            Assert.That(midBoard.Stage, Is.EqualTo(DeparturePrepStage.Boarding));
            Assert.That(midBoard.FuelProgress, Is.EqualTo(1));
            Assert.That(midBoard.CateringProgress, Is.EqualTo(1));
            Assert.That(midBoard.BoardingProgress, Is.EqualTo(0.5).Within(0.001));
            Assert.That(midBoard.Label, Is.EqualTo("Boarding 50%"));

            var readyAt = new SimulationTime(DeparturePrep.TotalSeconds(plane.Type));
            var ready = DeparturePrep.For(plane, readyAt);
            Assert.That(DeparturePrep.IsReady(plane, readyAt), Is.True);
            Assert.That(ready.FuelProgress, Is.EqualTo(1));
            Assert.That(ready.CateringProgress, Is.EqualTo(1));
            Assert.That(ready.BoardingProgress, Is.EqualTo(1));
            Assert.That(ready.Label, Is.EqualTo("Ready for pushback"));

            clock.Set(new SimulationTime(DeparturePrep.TotalSeconds(plane.Type) - 1));
            ops.Update();
            Assert.That(plane.State, Is.EqualTo(FleetState.AtStand), "prep not finished — no pushback");

            clock.Set(departAt);
            ops.Update();
            Assert.That(plane.State, Is.EqualTo(FleetState.TaxiOut));
            Assert.That(plane.PrepStartedAt, Is.Null);
        }

        [Test]
        public void FarAheadBooking_DoesNotStartPrepUntilTheLeadWindow()
        {
            var (_, ops, plane) = PlayerOnly();
            var departAt = new SimulationTime(4 * 3600);
            Assert.That(ops.ScheduleDeparture(plane, Code("KGC"), departAt).Accepted, Is.True);
            var expectedStart = departAt.ElapsedSeconds - DeparturePrep.TotalSeconds(plane.Type);
            Assert.That(plane.PrepStartedAt!.Value.ElapsedSeconds, Is.EqualTo(expectedStart));
            Assert.That(DeparturePrep.For(plane, new SimulationTime(0)).Stage, Is.EqualTo(DeparturePrepStage.Fuel));
            Assert.That(DeparturePrep.For(plane, new SimulationTime(0)).FuelProgress, Is.EqualTo(0),
                "fuelling has not started four hours before pushback");
            Assert.That(DeparturePrep.IsReady(plane, new SimulationTime(expectedStart - 1)), Is.False);
            Assert.That(DeparturePrep.IsReady(plane, departAt), Is.True);
        }

        [Test]
        public void PrepRemaining_CountsDownOnEveryStageAndThenTheFlightLeaves()
        {
            var (clock, ops, plane) = PlayerOnly();
            var departAt = new SimulationTime(DeparturePrep.TotalSeconds(plane.Type));
            Assert.That(ops.ScheduleDeparture(plane, Code("KGC"), departAt).Accepted, Is.True);

            var fuelStart = DeparturePrep.For(plane, new SimulationTime(0)).RemainingSeconds;
            var fuelLater = DeparturePrep.For(plane, new SimulationTime(20)).RemainingSeconds;
            Assert.That(fuelLater, Is.LessThan(fuelStart));
            Assert.That(DeparturePrep.For(plane, new SimulationTime(20)).Stage, Is.EqualTo(DeparturePrepStage.Fuel));

            var cateringAt = DeparturePrep.FuelSeconds;
            var cateringStart = DeparturePrep.For(plane, new SimulationTime(cateringAt)).RemainingSeconds;
            var cateringLater = DeparturePrep.For(plane, new SimulationTime(cateringAt + 20)).RemainingSeconds;
            Assert.That(cateringLater, Is.LessThan(cateringStart));
            Assert.That(DeparturePrep.For(plane, new SimulationTime(cateringAt + 20)).Stage,
                Is.EqualTo(DeparturePrepStage.Catering));

            var boardingAt = DeparturePrep.FuelSeconds + DeparturePrep.CateringSeconds;
            var boardingStart = DeparturePrep.For(plane, new SimulationTime(boardingAt)).RemainingSeconds;
            var boardingLater = DeparturePrep.For(plane, new SimulationTime(boardingAt + 20)).RemainingSeconds;
            Assert.That(boardingLater, Is.LessThan(boardingStart));
            Assert.That(DeparturePrep.For(plane, new SimulationTime(boardingAt + 20)).Stage,
                Is.EqualTo(DeparturePrepStage.Boarding));

            clock.Set(departAt);
            ops.Update();
            Assert.That(plane.State, Is.EqualTo(FleetState.TaxiOut));
        }

        [Test]
        public void MissingPrepStart_StillFinishesInTimeForTheBookedPushback()
        {
            var (clock, ops, plane) = PlayerOnly();
            Assert.That(ops.ScheduleDeparture(plane, Code("KGC"), new SimulationTime(600)).Accepted, Is.True);
            plane.PrepStartedAt = null;

            Assert.That(DeparturePrep.IsReady(plane, new SimulationTime(600)), Is.True,
                "infer start from the booked slot so fuelling cannot sit at 0% forever");
            clock.Set(new SimulationTime(600));
            ops.Update();
            Assert.That(plane.State, Is.EqualTo(FleetState.TaxiOut));
        }

        [Test]
        public void UpdatingAPlan_DoesNotRestartFuelAlreadyPumped()
        {
            var (clock, ops, plane) = PlayerOnly();
            var departAt = new SimulationTime(DeparturePrep.TotalSeconds(plane.Type) + 60);
            Assert.That(ops.ScheduleDeparture(plane, Code("KGC"), departAt).Accepted, Is.True);
            var started = plane.PrepStartedAt;
            clock.Set(new SimulationTime(started!.Value.ElapsedSeconds + DeparturePrep.FuelSeconds + 10));
            ops.Update();
            Assert.That(DeparturePrep.For(plane, clock.Now).Stage, Is.EqualTo(DeparturePrepStage.Catering));

            Assert.That(ops.ScheduleDeparture(plane, Code("KGC"), new SimulationTime(1_200)).Accepted, Is.True);
            Assert.That(plane.PrepStartedAt, Is.EqualTo(started));
            Assert.That(DeparturePrep.For(plane, clock.Now).Stage, Is.EqualTo(DeparturePrepStage.Catering));
        }

        [Test]
        public void DestinationTurnaround_EndsAndTheAircraftComesHome()
        {
            var (clock, ops, plane) = PlayerOnly();
            Assert.That(ops.ScheduleDeparture(plane, Code("KGC"), new SimulationTime(600)).Accepted, Is.True);
            clock.Set(new SimulationTime(20_000));
            ops.Update();
            Assert.That(plane.State, Is.EqualTo(FleetState.AtStand));
            Assert.That(plane.CompletedTrips, Is.EqualTo(1));
        }

        [Test]
        public void PlayerLanding_AutoParksWhenAStandIsFree()
        {
            var (clock, ops, plane) = PlayerOnly();
            Assert.That(ops.ScheduleDeparture(plane, Code("KGC"), new SimulationTime(600)).Accepted, Is.True);
            clock.Set(new SimulationTime(20_000));
            ops.Update();
            Assert.That(plane.State, Is.EqualTo(FleetState.AtStand));
            Assert.That(plane.CompletedTrips, Is.EqualTo(1));
            Assert.That(ops.CareerState.Funds, Is.GreaterThan(AirlineCareerState.StartingFunds
                - FlightEconomics.DispatchCost(plane.Type, ops.DistanceKm(Code("KGC")))));
        }

        [Test]
        public void BuyAircraft_ChargesAndAddsAParkedTypeWhenGatesClear()
        {
            var (_, ops, _) = PlayerOnly();
            Assert.That(ops.BuyAircraft(AircraftType.Atr42).Accepted, Is.False, "starter has not met the gates");

            ops.RestoreCareerState(20_000, 80, nameof(OperatingTier.Provisional), null, 0, 0, Array.Empty<string>(),
                Array.Empty<string>(), 6);
            Assert.That(ops.BuyAircraft(AircraftType.Atr42).Accepted, Is.True);
            Assert.That(ops.CareerState.Funds, Is.EqualTo(20_000 - AircraftAcquisition.Atr42.Price));
            var atr = ops.FleetOf(ops.PlayerAirline).First(a => a.Type.Id == AircraftType.Atr42.Id);
            Assert.That(atr.State, Is.EqualTo(FleetState.AtStand));
            Assert.That(ops.CanOperate(atr, Code("KGC")), Is.True);
            Assert.That(ops.CanOperate(atr, Code("MEL")), Is.False);
        }

        [Test]
        public void ContractMarket_DrawsFromOwnedTypesAndChangesWithTheWindow()
        {
            var (clock, ops, _) = PlayerOnly();
            // The rotating market only; authored career contracts lead the offers (ADR 0084).
            var first = ops.MarketOffers().Where(o => o.Id.StartsWith("MKT-")).ToList();
            Assert.That(first.Count, Is.EqualTo(ContractMarket.OffersPerWindow));
            foreach (var offer in first)
            {
                Assert.That(offer.EligibleType.Id, Is.EqualTo(AircraftType.Saab340.Id));
                Assert.That(RouteAccess.BandOf(offer.DestinationCode), Is.EqualTo(RouteBand.Regional));
                Assert.That(offer.Id, Does.StartWith("MKT-"));
            }

            clock.Set(new SimulationTime(ContractMarket.WindowSeconds));
            ops.Update();
            var later = ops.MarketOffers().Where(o => o.Id.StartsWith("MKT-")).ToList();
            Assert.That(later.Count, Is.EqualTo(ContractMarket.OffersPerWindow));
            Assert.That(later.Select(o => o.Id), Is.Not.EqualTo(first.Select(o => o.Id)),
                "a new six-hour window is a new draw");
        }

        [Test]
        public void MarketContract_SurvivesSaveAfterTheWindowRolls()
        {
            var (clock, ops, plane) = PlayerOnly();
            var offer = ops.MarketOffers()[0];
            Assert.That(ops.AcceptContract(offer).Accepted, Is.True);

            var data = AirlineSave.Capture(ops);
            Assert.That(data.Version, Is.EqualTo(AirlineSaveData.CurrentVersion));
            Assert.That(data.HasContractSnapshot, Is.True);
            Assert.That(data.ContractDefinitionId, Is.EqualTo(offer.Id));

            clock.Set(new SimulationTime(ContractMarket.WindowSeconds));
            ops.Update();
            Assert.That(ops.MarketOffers().Select(o => o.Id), Does.Not.Contain(offer.Id));

            var restored = AirlineSave.Restore(data, new ManualSimulationClock(new SimulationTime(data.ClockSeconds)));
            Assert.That(restored.CareerState.ActiveContract.DefinitionId, Is.EqualTo(offer.Id));
            Assert.That(restored.CareerState.TryFindDefinition(offer.Id, out var definition), Is.True);
            Assert.That(definition.DestinationCode, Is.EqualTo(offer.DestinationCode));

            var dest = Code(offer.DestinationCode);
            Assert.That(restored.ScheduleDeparture(restored.FleetOf(restored.PlayerAirline).Single(), dest,
                new SimulationTime(600)).Accepted, Is.True);
            Assert.That(plane.Registration, Is.EqualTo("VH-PAX"));
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Airside.Domain;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class FourEngineFleetTests
    {
        [TestCase("B748", WakeCategory.Heavy)]
        [TestCase("A388", WakeCategory.Super)]
        public void LargeJetsUseTheirOwnProfilesAndCodeFStands(string id, WakeCategory wake)
        {
            AircraftType.TryFromId(id, out var type);
            Assert.That(LiveTraffic.ModelFor(id), Is.SameAs(type));
            Assert.That(AircraftCatalogue.CodeLetter(type), Is.EqualTo('F'));
            Assert.That(WakeSeparation.Category(type), Is.EqualTo(wake));
            Assert.That(AircraftPerformance.For(type).TakeoffRollMetres, Is.GreaterThan(2500));
            Assert.That(AirlineOperations.StandFits(type, new StableId("GATE-28L")), Is.False);
            Assert.That(PlayerBase.DedicatedStands(PlayerBaseLevel.JetGate, type), Is.Empty);
            var stands = PlayerBase.DedicatedStands(PlayerBaseLevel.International, type);
            Assert.That(stands.Select(s => s.Value), Is.EquivalentTo(new[] { "GATE-18R" }));
            foreach (var stand in stands)
            {
                Assert.That(AirlineOperations.StandFits(type, stand), Is.True);
                foreach (var runway in new[] { RunwayDirection.Runway05, RunwayDirection.Runway23 })
                {
                    Assert.That(AdelaideGroundPolicy.RouteAvailable(stand, type, runway, true), Is.True);
                    Assert.That(AdelaideGroundPolicy.RouteAvailable(stand, type, runway, false), Is.True);
                }
            }
            Assert.That(AircraftAcquisition.TryFor(type, out _), Is.True);
            Assert.That(FlightCostModel.ProfileFor(type).BurnKgPerBlockHour, Is.GreaterThan(9000));
        }

        [TestCase("B748")]
        [TestCase("A388")]
        public void SaveRetainsNewIdentityAndServiceRoutingAvoidsOuterEngines(string id)
        {
            AircraftType.TryFromId(id, out var type);
            var clock = new ManualSimulationClock(new SimulationTime(9 * 3600));
            var ops = new AirlineOperations(clock, new SeededRandomSource(5), DestinationCatalogue.Adelaide,
                AirlineOperations.AdelaideStands);
            var player = Airline.Player("Large jet test", "#338899");
            ops.AddAirline(player);
            ops.AddAircraft(player, "VH-LRG", type, new StableId("GATE-18R"));
            var restored = AirlineSave.Restore(AirlineSave.Capture(ops), clock);
            Assert.That(restored.Fleet.Single(a => a.Registration == "VH-LRG").Type, Is.SameAs(type));
            Assert.That(restored.Fleet.Single(a => a.Registration == "VH-LRG").Stand.Value, Is.EqualTo("GATE-18R"));
            var layout = AircraftLayout.For(type);
            var outer = layout.OuterEngine.Value;
            var footprint = new List<LayoutRect>();
            layout.Footprint(footprint, true);
            foreach (var side in new[] { -1f, 1f })
            {
                var z = (outer.ZBack + outer.ZFront) * .5f;
                Assert.That(footprint.Any(r => r.Contains(side * outer.X, z, 0)), Is.True);
                var clear = layout.Clear(side * outer.X, z, 1, true);
                Assert.That(footprint.Any(r => r.Contains(clear.X, clear.Z, 1)), Is.False);
            }
        }

        [TestCase("B748")]
        [TestCase("A388")]
        public void InternationalCareerCanBuyAndRestoreLargeJet(string id)
        {
            var clock = new ManualSimulationClock(new SimulationTime(9 * 3600));
            var ops = new AirlineOperations(clock, new SeededRandomSource(12), DestinationCatalogue.Adelaide,
                AirlineOperations.AdelaideStands);
            var player = Airline.Player("Large jet purchase", "#338899");
            ops.AddAirline(player);
            ops.AddAircraft(player, "VH-ST1", AircraftType.Saab340, new StableId("BAY-1"));
            ops.RestoreCareerState(20_000_000, 99, nameof(OperatingTier.International), null, 0, 0,
                Array.Empty<string>(), Array.Empty<string>(), 200,
                baseLevel: PlayerBaseLevel.International, manualRotations: 200);
            AircraftType.TryFromId(id, out var type);
            var result = ops.BuyAircraft(type);
            Assert.That(result.Accepted, Is.True, result.Reason);
            Assert.That(ops.CareerState.Funds, Is.EqualTo(20_000_000 - LeaseTerms.Deposit(type)));
            var bought = ops.FleetOf(player).Single(a => a.Type.Id == id);
            Assert.That(bought.Stand.Value, Is.EqualTo("GATE-18R"));
            var restored = AirlineSave.Restore(AirlineSave.Capture(ops), clock);
            Assert.That(restored.Fleet.Single(a => a.Registration == bought.Registration).Type, Is.SameAs(type));
        }

        [Test]
        public void LargeStandClearanceBlocksNeighbourInBothDirections()
        {
            var clock = new ManualSimulationClock(new SimulationTime(9 * 3600));
            AirlineOperations Empty()
            {
                var o = new AirlineOperations(clock, new SeededRandomSource(4), DestinationCatalogue.Adelaide,
                    AirlineOperations.AdelaideStands);
                o.AddAirline(Airline.Rex());
                return o;
            }
            var ops = Empty();
            ops.AddAircraft(ops.Airlines.First(), "VH-SML", AircraftType.Boeing7378, new StableId("GATE-17"));
            Assert.That(ops.FreeStandsFor(AircraftType.AirbusA380800).Any(s => s.Value == "GATE-18R"), Is.False);
            ops = Empty();
            ops.AddAircraft(ops.Airlines.First(), "VH-LRG", AircraftType.AirbusA380800, new StableId("GATE-18R"));
            Assert.That(ops.FreeStandsFor(AircraftType.Boeing7378).Any(s => s.Value == "GATE-17"), Is.False);
        }

        [Test]
        public void InternationalLargeJetUsesCompatibleSharedOverflowWhenItsPierIsHeld()
        {
            var clock = new ManualSimulationClock(new SimulationTime(9 * 3600));
            var ops = new AirlineOperations(clock, new SeededRandomSource(12), DestinationCatalogue.Adelaide,
                AirlineOperations.AdelaideStands);
            var player = Airline.Player("Overflow test", "#338899");
            var operatorAirline = Airline.Rex();
            ops.AddAirline(player); ops.AddAirline(operatorAirline);
            ops.AddAircraft(player, "VH-ST1", AircraftType.Saab340, new StableId("BAY-1"));
            ops.AddAircraft(operatorAirline, "VH-HLD", AircraftType.AirbusA350900, new StableId("GATE-18"));
            ops.RestoreCareerState(20_000_000, 99, nameof(OperatingTier.International), null, 0, 0,
                Array.Empty<string>(), Array.Empty<string>(), 200,
                baseLevel: PlayerBaseLevel.International, manualRotations: 200);
            Assert.That(ops.BuyAircraft(AircraftType.AirbusA380800).Accepted, Is.True);
            var bought = ops.FleetOf(player).Single(a => a.Type.Id == "A388");
            Assert.That(bought.Stand.Value, Is.Not.EqualTo("GATE-18R"));
            Assert.That(AirlineOperations.StandFits(bought.Type, bought.Stand), Is.True);
            Assert.That(PlayerBase.CanUseStand(PlayerBaseLevel.International, bought.Type, bought.Stand), Is.True);
            Assert.That(PlayerBase.CanUseStand(PlayerBaseLevel.JetGate, bought.Type, bought.Stand), Is.False);
        }

        [TestCase("B748")]
        [TestCase("A388")]
        public void ReturnStandReservationSurvivesSaveAndNeverBlocksItsOwner(string id)
        {
            var clock = new ManualSimulationClock(new SimulationTime(9 * 3600));
            var ops = new AirlineOperations(clock, new SeededRandomSource(12), DestinationCatalogue.Adelaide,
                AirlineOperations.AdelaideStands);
            var player = Airline.Player("Return reservation", "#338899");
            ops.AddAirline(player);
            AircraftType.TryFromId(id, out var type);
            var stand = new StableId("GATE-20R");
            var aircraft = ops.AddAircraft(player, "VH-LRG", type, stand);
            aircraft.DepartureStand = stand;
            aircraft.Stand = default;
            DestinationCatalogue.TryFind("KGC", out var destination);
            aircraft.CurrentDestination = destination;
            aircraft.Restore(FleetState.Outbound, clock.Now, clock.Now.Advance(1800));
            Assert.That(ops.IsStandFree(stand), Is.False);
            Assert.That(ops.IsStandFree(new StableId("GATE-20")), Is.False);
            Assert.That(ops.FreeStandsFor(type, aircraft).Any(s => s.Equals(stand)), Is.True);
            Assert.That(ops.SuggestStandFor(type, aircraft).HasValue, Is.True);
            Assert.That(ops.FreeStandsFor(AircraftType.Boeing7378).Any(s => s.Equals(stand)), Is.False);
            var restored = AirlineSave.Restore(AirlineSave.Capture(ops), clock);
            var owner = restored.Fleet.Single(a => a.Registration == "VH-LRG");
            Assert.That(restored.IsStandFree(stand), Is.False);
            Assert.That(restored.FreeStandsFor(type, owner).Any(s => s.Equals(stand)), Is.True);
        }

        [Test]
        public void A380LeavesSuperWakeBehindIt()
        {
            Assert.That(WakeSeparation.Seconds(AircraftType.AirbusA380800, AircraftType.Boeing7478, true), Is.EqualTo(180));
            Assert.That(WakeSeparation.Seconds(AircraftType.AirbusA380800, AircraftType.Saab340, false), Is.EqualTo(180));
        }
    }
}

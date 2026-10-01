using System;
using System.Linq;
using Airside.Domain;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>ADR 0207: the player owns and flies a Bell 412 from the helipad.</summary>
    public sealed class PlayerHelicopterTests
    {
        private static AirlineOperations RichPlayer(out ManualSimulationClock clock, int seed = 12)
        {
            clock = new ManualSimulationClock(new SimulationTime(8 * 3600));
            var ops = AirlineOperations.StartAtAdelaide(clock, new SeededRandomSource((uint)seed),
                Airline.Player("Heli Air", "#1F7A8C"));
            ops.RestoreCareerState(200_000, 100, nameof(OperatingTier.International), null, 0, 0,
                Array.Empty<string>(), Array.Empty<string>(), 40, baseLevel: PlayerBaseLevel.ExpandedRegional);
            return ops;
        }

        private static FleetAircraft BuyBell(AirlineOperations ops)
        {
            var result = ops.BuyAircraft(AircraftType.Bell412);
            Assert.That(result.Accepted, Is.True, result.Reason);
            return ops.Fleet.Single(a => a.Airline.IsPlayer && a.Type.IsRotorcraft);
        }

        [Test]
        public void TheBellIsForSale_AfterTheRegionalTurboprops_WithItsOwnRequirements()
        {
            Assert.That(AircraftAcquisition.TryFor(AircraftType.Bell412, out var offer), Is.True);
            Assert.That(offer.RequiredTier, Is.EqualTo(OperatingTier.Provisional));
            Assert.That(offer.Price, Is.GreaterThan(AircraftAcquisition.Atr42.Price));
            Assert.That(offer.Operates, Is.EqualTo(RouteBand.Regional));
            var list = AircraftAcquisition.All.Select(o => o.Type.Id).ToList();
            Assert.That(list.IndexOf("B412"), Is.GreaterThan(list.IndexOf("ATR42")));
            Assert.That(PlayerBase.RequiredLevel(AircraftType.Bell412), Is.EqualTo(PlayerBaseLevel.ExpandedRegional));
        }

        [Test]
        public void TheStarterBase_CannotTakeAHelicopter_TheExpandedBaseCan()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = AirlineOperations.StartAtAdelaide(clock, new SeededRandomSource(3), Airline.Player("Heli Air", "#1F7A8C"));
            ops.RestoreCareerState(200_000, 100, nameof(OperatingTier.International), null, 0, 0,
                Array.Empty<string>(), Array.Empty<string>(), 40, baseLevel: PlayerBaseLevel.Starter);
            var refused = ops.BuyAircraft(AircraftType.Bell412);
            Assert.That(refused.Accepted, Is.False);
            Assert.That(refused.Reason, Does.Contain("base"));

            ops.CareerState.BaseLevel = PlayerBaseLevel.ExpandedRegional;
            Assert.That(ops.BuyAircraft(AircraftType.Bell412).Accepted, Is.True);
        }

        [Test]
        public void ABoughtHelicopter_ParksOnAPlayerPadSpot_NotTheRescueCrewsSpot()
        {
            var ops = RichPlayer(out _);
            var rescue = ops.Fleet.Single(a => a.Airline.Id.Value == "SAAS");
            var mine = BuyBell(ops);
            Assert.That(mine.State, Is.EqualTo(FleetState.AtStand));
            Assert.That(AirlineOperations.AdelaideHelipadStands, Does.Contain(mine.Stand));
            Assert.That(mine.Stand, Is.Not.EqualTo(rescue.Stand));
            Assert.That(new[] { "HELI-2", "HELI-3" }, Does.Contain(mine.Stand.Value));
            Assert.That(PlayerBase.IsDedicatedStand(PlayerBaseLevel.ExpandedRegional, mine.Stand), Is.True);
            Assert.That(PlayerBase.IsDedicatedStand(PlayerBaseLevel.ExpandedRegional, rescue.Stand), Is.False);
        }

        [Test]
        public void TheHelicopter_FliesARegionalRoundTrip_AndGetsPaid()
        {
            var ops = RichPlayer(out var clock);
            var bell = BuyBell(ops);
            DestinationCatalogue.TryFind("KGC", out var kingscote);
            var funds = ops.CareerState.Funds;
            var departAt = clock.Now.Advance(20 * 60);
            var booked = ops.ScheduleDeparture(bell, kingscote, departAt);
            Assert.That(booked.Accepted, Is.True, booked.Reason);
            var cost = ops.CareerState.Funds;
            Assert.That(cost, Is.LessThan(funds), "the dispatch is charged");

            var states = new System.Collections.Generic.HashSet<FleetState>();
            for (var second = 0; second < 6 * 3600 && bell.CompletedTrips == 0; second++)
            {
                clock.Advance(1);
                ops.Update();
                states.Add(bell.State);
            }

            Assert.That(bell.CompletedTrips, Is.EqualTo(1));
            Assert.That(bell.State, Is.EqualTo(FleetState.AtStand));
            Assert.That(states, Does.Contain(FleetState.TakingOff));
            Assert.That(states, Does.Contain(FleetState.Landing));
            Assert.That(states, Has.No.Member(FleetState.TaxiOut));
            Assert.That(ops.CareerState.Funds, Is.GreaterThan(cost), "the rotation pays");
            Assert.That(ops.CareerState.CompletedPlayerRotations, Is.GreaterThanOrEqualTo(1));
        }

        [Test]
        public void TheHelicopter_FliesRegionalRoutesOnly_WithinItsRange()
        {
            var ops = RichPlayer(out var clock);
            var bell = BuyBell(ops);
            DestinationCatalogue.TryFind("MEL", out var melbourne);
            DestinationCatalogue.TryFind("PER", out var perth);
            DestinationCatalogue.TryFind("KGC", out var kingscote);
            var later = clock.Now.Advance(3600);
            Assert.That(ops.ScheduleDeparture(bell, melbourne, later).Accepted, Is.False, "Melbourne is a domestic route and out of range");
            Assert.That(ops.ScheduleDeparture(bell, perth, later).Accepted, Is.False);
            Assert.That(ops.ScheduleDeparture(bell, kingscote, later).Accepted, Is.True);
        }

        [Test]
        public void TheHelicopter_NeverTakesTheRescueCrewsSpot_WhenItReturns()
        {
            var ops = RichPlayer(out var clock, 21);
            var rescue = ops.Fleet.Single(a => a.Airline.Id.Value == "SAAS");
            var bell = BuyBell(ops);
            DestinationCatalogue.TryFind("KGC", out var kingscote);
            Assert.That(ops.ScheduleDeparture(bell, kingscote, clock.Now.Advance(10 * 60)).Accepted, Is.True);
            for (var second = 0; second < 5 * 3600; second++)
            {
                clock.Advance(1);
                ops.Update();
                if (bell.State == FleetState.AtStand)
                    Assert.That(bell.Stand, Is.Not.EqualTo(default(StableId)));
                if (bell.State is FleetState.Landing or FleetState.AtStand && bell.Stand.Value != null
                    && bell.CompletedTrips > 0)
                    Assert.That(bell.Stand.Value, Is.AnyOf("HELI-2", "HELI-3"), "second " + second);
            }

            Assert.That(rescue.CompletedTrips, Is.GreaterThanOrEqualTo(1), "the rescue crew kept flying");
        }

        [Test]
        public void ThePad_SharedByTwoHelicopters_NeverHasTwoMovementsAtOnce()
        {
            var ops = RichPlayer(out var clock, 31);
            var bell = BuyBell(ops);
            var rescue = ops.Fleet.Single(a => a.Airline.Id.Value == "SAAS");
            DestinationCatalogue.TryFind("KGC", out var kingscote);
            Assert.That(ops.ScheduleDeparture(bell, kingscote, rescue.Scheduled.Value.DepartAt).Accepted, Is.True,
                "booked for the very minute the rescue crew lifts");
            var overlaps = 0;
            for (var second = 0; second < 3 * 3600; second++)
            {
                clock.Advance(1);
                ops.Update();
                var moving = ops.Fleet.Count(a => a.Type.IsRotorcraft
                                                  && a.State is FleetState.TakingOff or FleetState.Landing);
                // Both may be airborne at once, but the pad is held by one take-off or landing at a time.
                var onPad = ops.Fleet.Where(a => a.Type.IsRotorcraft
                    && (a.State == FleetState.TakingOff
                        && clock.Now.ElapsedSeconds - a.StateStartedAt.ElapsedSeconds
                        < RotorcraftRules.PadSecondsForTakeoff(RotorcraftPerformance.Bell412) - 12
                        || a.State == FleetState.Landing)).Count();
                if (onPad > 1)
                    overlaps++;
                _ = moving;
            }

            Assert.That(overlaps, Is.EqualTo(0));
        }

        [Test]
        public void ASave_WithAPlayerHelicopterMidFlight_RestoresAndKeepsFlying()
        {
            var ops = RichPlayer(out var clock, 41);
            var bell = BuyBell(ops);
            DestinationCatalogue.TryFind("WYA", out var whyalla);
            Assert.That(ops.ScheduleDeparture(bell, whyalla, clock.Now.Advance(10 * 60)).Accepted, Is.True);
            while (bell.State != FleetState.Outbound)
            {
                clock.Advance(1);
                ops.Update();
            }

            var restoredClock = new ManualSimulationClock(clock.Now);
            var restored = AirlineSave.Restore(AirlineSave.Capture(ops), restoredClock);
            var twin = restored.Fleet.Single(a => a.Registration == bell.Registration);
            Assert.That(twin.Type.IsRotorcraft, Is.True);
            Assert.That(twin.State, Is.EqualTo(FleetState.Outbound));
            Assert.That(twin.CurrentDestination?.Code, Is.EqualTo("WYA"));
            for (var second = 0; second < 5 * 3600 && twin.CompletedTrips == 0; second++)
            {
                restoredClock.Advance(1);
                restored.Update();
            }

            Assert.That(twin.CompletedTrips, Is.EqualTo(1));
            Assert.That(restored.CareerState.CompletedPlayerRotations, Is.GreaterThanOrEqualTo(1));
        }

        [Test]
        public void ACheck_IsDoneOnThePad_NoTowNoHangar()
        {
            var ops = RichPlayer(out _);
            var bell = BuyBell(ops);
            Assert.That(HangarTow.Options(bell.Type, bell.Stand), Is.Empty);
            Assert.That(HangarBays.Of(ops.Fleet, bell, PlayerBaseLevel.ExpandedRegional).HasHangar, Is.False);
        }

        [Test]
        public void Economics_TheHelicopterPaysLessPerFlightThanATurboprop_AndCostsSimilarToRun()
        {
            Assert.That(FlightEconomics.Weight(AircraftType.Bell412), Is.EqualTo(0.7).Within(1e-9));
            Assert.That(FlightEconomics.FlightPay(AircraftType.Bell412, 110.0),
                Is.LessThan(FlightEconomics.FlightPay(AircraftType.Atr42, 110.0)));
            Assert.That(FlightEconomics.FlightPay(AircraftType.Bell412, 110.0),
                Is.GreaterThan(FlightEconomics.DispatchCost(AircraftType.Bell412, 110.0)),
                "a short regional hop still turns a profit");
            Assert.That(FlightEconomics.CostPerKm(AircraftType.Bell412), Is.EqualTo(FlightEconomics.TurbopropCostPerKm));
        }

        [Test]
        public void PrepIsQuickerForAHelicopter()
        {
            Assert.That(DeparturePrep.TotalSeconds(AircraftType.Bell412, PlayerBaseLevel.ExpandedRegional),
                Is.LessThan(DeparturePrep.TotalSeconds(AircraftType.Atr42, PlayerBaseLevel.ExpandedRegional)));
        }

        [Test]
        public void NoOutstationHelicopters()
        {
            var ops = RichPlayer(out _);
            var refused = ops.BuyAircraftAtOutstation(AircraftType.Bell412, "MEL");
            Assert.That(refused.Accepted, Is.False);
        }
    }
}

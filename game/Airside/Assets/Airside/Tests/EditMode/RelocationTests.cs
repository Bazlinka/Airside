using System;
using System.Linq;
using Airside.Domain;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>Flying an outstation aircraft in to Adelaide and the ferry flag that keeps it honest (ADR 0239).</summary>
    public sealed class RelocationTests
    {
        private static (ManualSimulationClock Clock, AirlineOperations Operations) NewAirline(
            PlayerBaseLevel baseLevel = PlayerBaseLevel.JetGate)
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var operations = new AirlineOperations(clock, new SeededRandomSource(91),
                DestinationCatalogue.Adelaide, AirlineOperations.AdelaideStands);
            var player = Airline.Player("Test Regional", "#245AA6");
            operations.AddAirline(player);
            operations.AddAircraft(player, "VH-TST", AircraftType.Saab340,
                AirlineOperations.AdelaideRegionalBays[0]);
            operations.RestoreCareerState(500_000, 95, nameof(OperatingTier.Domestic), null, 0, 0,
                Array.Empty<string>(), Array.Empty<string>(), 100,
                baseLevel: baseLevel, manualRotations: 12);
            Assert.That(operations.OpenOutstationBase("MEL").Accepted, Is.True);
            var bought = operations.BuyAircraftAtOutstation(AircraftType.Dash8Q400, "MEL");
            Assert.That(bought.Accepted, Is.True, bought.Reason);
            return (clock, operations);
        }

        private static void FlyOneService(ManualSimulationClock clock, AirlineOperations operations)
        {
            var plan = operations.ScheduleOutstationService("VH-O01", "SYD", new SimulationTime(1800));
            Assert.That(plan.Accepted, Is.True, plan.Reason);
            clock.Set(new SimulationTime(operations.OutstationFleet.Single().ReturnAtSeconds));
            operations.Update();
        }

        /// <summary>Steps the clock until the aircraft is parked again, up to eight hours.</summary>
        private static FleetAircraft FlyUntilParked(ManualSimulationClock clock, AirlineOperations operations,
            string registration)
        {
            var start = clock.Now.ElapsedSeconds;
            for (var step = 1; step <= 8 * 3600 / 30; step++)
            {
                clock.Set(new SimulationTime(start + step * 30L));
                operations.Update();
                var aircraft = operations.Fleet.Single(a => a.Registration == registration);
                if (aircraft.State == FleetState.AtStand)
                    return aircraft;
            }

            Assert.Fail($"{registration} never parked.");
            return null;
        }

        [Test]
        public void RelocateToAdelaide_FliesTheAircraftInAsAFerry()
        {
            var (_, operations) = NewAirline();
            Assert.That(operations.CanRelocateToAdelaide("VH-O01", out var reason), Is.True, reason);
            var count = operations.PlayerFleetCount();
            var funds = operations.CareerState.Funds;
            var cost = operations.RelocationCost(operations.OutstationFleet.Single());

            var result = operations.RelocateToAdelaide("VH-O01");

            Assert.That(result.Accepted, Is.True, result.Reason);
            Assert.That(operations.OutstationFleet, Is.Empty);
            Assert.That(operations.PlayerFleetCount(), Is.EqualTo(count), "the aircraft moves, it is not added or lost");
            var ferry = operations.Fleet.Single(a => a.Registration == "VH-O01");
            Assert.That(ferry.IsFerry, Is.True);
            Assert.That(ferry.State, Is.EqualTo(FleetState.Inbound));
            Assert.That(ferry.CurrentDestination?.Code, Is.EqualTo("MEL"));
            Assert.That(cost, Is.GreaterThan(0));
            Assert.That(operations.CareerState.Funds, Is.EqualTo(funds - cost));
            var entry = PlayerFleet.Find(operations, new SimulationTime(0), "VH-O01");
            Assert.That(entry.Kind, Is.EqualTo(PlayerFleetKind.Ferry));
            Assert.That(entry.IsOutstation, Is.False);
            Assert.That(entry.BaseCode, Is.EqualTo("ADL"));
        }

        [Test]
        public void ArrivalOfAFerry_EarnsNothingAndCountsNoFlight()
        {
            var (clock, operations) = NewAirline();
            FlyOneService(clock, operations);
            var rotations = operations.CareerState.CompletedPlayerRotations;
            var aircraft = operations.OutstationFleet.Single();
            var flights = aircraft.CompletedServices;
            var revenue = aircraft.LifetimeRevenue;
            var history = aircraft.HistoryFlights;
            Assert.That(operations.RelocateToAdelaide("VH-O01").Accepted, Is.True);

            var parked = FlyUntilParked(clock, operations, "VH-O01");

            Assert.That(parked.IsFerry, Is.False);
            Assert.That(parked.CurrentDestination, Is.Null);
            Assert.That(parked.CompletedTrips, Is.EqualTo(flights),
                "the ferry adds no trip, so its next settlement is CompletedTrips + 1");
            Assert.That(parked.HistoryFlights, Is.EqualTo(history));
            Assert.That(parked.LifetimeRevenue, Is.EqualTo(revenue));
            Assert.That(operations.CareerState.CompletedPlayerRotations, Is.EqualTo(rotations),
                "repositioning is not a service");
            Assert.That(PlayerFleet.Find(operations, clock.Now, "VH-O01").Kind, Is.EqualTo(PlayerFleetKind.Parked));
        }

        [Test]
        public void Relocation_KeepsTheLogbookWearAndMark()
        {
            var (clock, operations) = NewAirline();
            FlyOneService(clock, operations);
            var outstation = operations.OutstationFleet.Single();
            var wear = outstation.RotationsSinceCheck;
            var revenue = outstation.LifetimeRevenue;

            Assert.That(operations.RelocateToAdelaide("VH-O01").Accepted, Is.True);

            var live = operations.Fleet.Single(a => a.Registration == "VH-O01");
            Assert.That(live.CompletedTrips, Is.EqualTo(1));
            Assert.That(live.RotationsSinceCheck, Is.EqualTo(wear));
            Assert.That(live.HistoryFlights, Is.EqualTo(1));
            Assert.That(live.LifetimeRevenue, Is.EqualTo(revenue));
            Assert.That(live.RouteHistory.Single().DestinationCode, Is.EqualTo("SYD"));
            Assert.That(live.IsFoundingAircraft, Is.False);
            Assert.That(live.Airline.IsPlayer, Is.True);
        }

        [Test]
        public void Relocation_RemovesTheAircraftsRepeatPlan()
        {
            var (_, operations) = NewAirline();
            Assert.That(operations.SetRepeatSchedule("VH-O01", "SYD", 12).Accepted, Is.True);

            Assert.That(operations.RelocateToAdelaide("VH-O01").Accepted, Is.True);

            Assert.That(operations.RepeatSchedules, Is.Empty);
        }

        [Test]
        public void Relocation_IsRefusedForEachReasonItWouldBeUnsafe()
        {
            var (_, operations) = NewAirline();

            Assert.That(operations.CanRelocateToAdelaide("VH-NOPE", out var reason), Is.False);
            Assert.That(reason, Is.EqualTo("No such aircraft."));

            Assert.That(operations.ScheduleOutstationService("VH-O01", "SYD", new SimulationTime(1800)).Accepted, Is.True);
            Assert.That(operations.CanRelocateToAdelaide("VH-O01", out reason), Is.False);
            Assert.That(reason, Does.Contain("flight planned"));
            Assert.That(operations.RelocateToAdelaide("VH-O01").Accepted, Is.False);
            Assert.That(operations.OutstationFleet.Count, Is.EqualTo(1), "a refused move changes nothing");
        }

        [Test]
        public void Relocation_IsRefusedWhenAdelaideIsAtItsAircraftCapacity()
        {
            var (_, operations) = NewAirline(PlayerBaseLevel.ExpandedRegional);
            // The expanded regional base holds three aircraft: VH-TST plus two more fills it.
            Assert.That(operations.BuyAircraft(AircraftType.Atr42).Accepted, Is.True);
            Assert.That(operations.BuyAircraft(AircraftType.Atr42).Accepted, Is.True);

            Assert.That(operations.CanRelocateToAdelaide("VH-O01", out var reason), Is.False);

            Assert.That(reason, Does.Contain("holds 3 aircraft"));
        }

        [Test]
        public void AFerryInTheAir_SurvivesSaveAndStillEarnsNothing()
        {
            var (clock, operations) = NewAirline();
            Assert.That(operations.RelocateToAdelaide("VH-O01").Accepted, Is.True);

            var saved = AirlineSave.Capture(operations);
            Assert.That(saved.Version, Is.EqualTo(AirlineSaveData.CurrentVersion));
            Assert.That(saved.Fleet.Single(r => r.Registration == "VH-O01").IsFerry, Is.True);
            var restored = AirlineSave.Restore(saved, clock);

            var ferry = restored.Fleet.Single(a => a.Registration == "VH-O01");
            Assert.That(ferry.IsFerry, Is.True);
            Assert.That(ferry.State, Is.EqualTo(FleetState.Inbound));
            var rotations = restored.CareerState.CompletedPlayerRotations;
            var parked = FlyUntilParked(clock, restored, "VH-O01");
            Assert.That(parked.IsFerry, Is.False);
            Assert.That(restored.CareerState.CompletedPlayerRotations, Is.EqualTo(rotations));
        }

        [Test]
        public void AFerryFlagOnAParkedAircraft_IsAnInvalidSave()
        {
            var (clock, operations) = NewAirline();
            var saved = AirlineSave.Capture(operations);
            saved.Fleet.Single(r => r.Registration == "VH-TST").IsFerry = true;

            Assert.Throws<FormatException>(() => AirlineSave.Restore(saved, clock));
        }

        [Test]
        public void AVersion19Save_IgnoresAFerryFlagItCouldNotHaveWritten()
        {
            var (clock, operations) = NewAirline();
            var saved = AirlineSave.Capture(operations);
            saved.Version = 19;
            saved.Fleet.Single(r => r.Registration == "VH-TST").IsFerry = true;

            var restored = AirlineSave.Restore(saved, clock);

            Assert.That(restored.Fleet.Single(a => a.Registration == "VH-TST").IsFerry, Is.False);
        }

        [Test]
        public void ARelocatedAircraft_CanBeBookedAndSoldLikeAnyOtherAdelaideAircraft()
        {
            var (clock, operations) = NewAirline();
            Assert.That(operations.RelocateToAdelaide("VH-O01").Accepted, Is.True);
            var parked = FlyUntilParked(clock, operations, "VH-O01");

            Assert.That(operations.CanResell(parked), Is.True);
            Assert.That(operations.SellAircraft(parked).Accepted, Is.True);
            Assert.That(operations.Fleet.Any(a => a.Registration == "VH-O01"), Is.False);
        }
    }
}

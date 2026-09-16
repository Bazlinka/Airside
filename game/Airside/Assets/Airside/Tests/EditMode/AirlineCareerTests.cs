using System;
using Airside.Domain;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>Airline career progress: contracts, funds, reliability and settlement idempotency (ADR 0053).</summary>
    public sealed class AirlineCareerTests
    {
        private static Destination Code(string code)
        {
            Assert.That(DestinationCatalogue.TryFind(code, out var destination), Is.True, code);
            return destination;
        }

        private static Airline Player() => Airline.Player("Southern Cross Regional", "#C8102E");

        /// <summary>Home with only the player's airline, so nothing moves unless asked.</summary>
        private static (ManualSimulationClock clock, AirlineOperations ops, FleetAircraft plane) PlayerOnly()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = new AirlineOperations(clock, new SeededRandomSource(7), DestinationCatalogue.Adelaide,
                AirlineOperations.AdelaideRegionalBays);
            var player = Player();
            ops.AddAirline(player);
            var plane = ops.AddAircraft(player, "VH-PAA", AircraftType.Atr42, AirlineOperations.AdelaideRegionalBays[0]);
            return (clock, ops, plane);
        }

        private static void RunTo(ManualSimulationClock clock, AirlineOperations ops, long seconds)
        {
            clock.Set(new SimulationTime(seconds));
            ops.Update();
        }

        /// <summary>
        /// One full Adelaide round trip — schedule, taxi, fly, land, take a stand — mirroring
        /// AirlineOperationsTests.PlayerRoundTrip_GoesOutAndBackThenWaitsForAStand, generalised
        /// to run more than once so contract rotations can be driven end to end.
        /// </summary>
        private static void FlyRoundTrip(ManualSimulationClock clock, AirlineOperations ops, FleetAircraft plane,
            Destination destination, long departAtSeconds, StableId returnStand)
        {
            Assert.That(ops.ScheduleDeparture(plane, destination, new SimulationTime(departAtSeconds)).Accepted, Is.True);
            RunTo(clock, ops, departAtSeconds);
            Assert.That(plane.State, Is.EqualTo(FleetState.TaxiOut));

            var takeoffAt = departAtSeconds
                + AirlineOperations.TaxiOutSecondsFrom(plane.DepartureStand, plane.Type, plane.AssignedRunway);
            RunTo(clock, ops, takeoffAt);
            Assert.That(plane.State, Is.EqualTo(FleetState.TakingOff), "empty runway: no hold");

            var outboundAt = takeoffAt + AirlineOperations.TakeoffRunwaySecondsFor(plane.Type);
            RunTo(clock, ops, outboundAt + 1);
            Assert.That(plane.State, Is.EqualTo(FleetState.Outbound));

            var airborne = ops.AirborneSeconds(plane, destination);
            var landingAt = outboundAt + airborne + AirlineOperations.DestinationTurnaroundSeconds + airborne;
            RunTo(clock, ops, landingAt);
            Assert.That(plane.State, Is.EqualTo(FleetState.Landing));

            RunTo(clock, ops, landingAt + AirlineOperations.LandingRunwaySecondsFor(plane.Type) + 3600);
            Assert.That(plane.State, Is.EqualTo(FleetState.AwaitingStand));

            Assert.That(ops.AssignStand(plane, returnStand).Accepted, Is.True);
            RunTo(clock, ops, clock.Now.ElapsedSeconds + AirlineOperations.TaxiInSecondsTo(returnStand, plane.Type));
            Assert.That(plane.State, Is.EqualTo(FleetState.AtStand));
        }

        [Test]
        public void NewCareer_StartsProvisionalWithNoContract()
        {
            var (_, ops, _) = PlayerOnly();
            Assert.That(ops.CareerState.Tier, Is.EqualTo(OperatingTier.Provisional));
            Assert.That(ops.CareerState.Funds, Is.Zero);
            Assert.That(ops.CareerState.Reliability, Is.EqualTo(AirlineCareerState.StartingReliability));
            Assert.That(ops.CareerState.ActiveContract, Is.Null);
        }

        [Test]
        public void AcceptContract_RefusesASecondContractAndAnUnreachedTier()
        {
            var (_, ops, _) = PlayerOnly();
            Assert.That(ops.AcceptContract(RouteContractCatalogue.RegionalKingscoteIntro).Accepted, Is.True);
            Assert.That(ops.AcceptContract(RouteContractCatalogue.RegionalKingscoteIntro).Accepted, Is.False,
                "already operating a contract");

            var (_, freshOps, _) = PlayerOnly();
            var domesticOnly = new RouteContractDefinition(
                "REG-TEST-DOMESTIC", "ADL", "MEL", AircraftType.Atr42, 1, 100, 0, 0, OperatingTier.Domestic);
            Assert.That(freshOps.AcceptContract(domesticOnly).Accepted, Is.False, "tier not reached");
        }

        [Test]
        public void CompletingRequiredRotations_PaysEachOneAndFulfilsOnTheLast()
        {
            var (clock, ops, plane) = PlayerOnly();
            var definition = RouteContractCatalogue.RegionalKingscoteIntro;
            Assert.That(ops.AcceptContract(definition).Accepted, Is.True);
            var kingscote = Code("KGC");

            var departAt = 600L;
            for (var rotation = 1; rotation <= definition.RequiredRotations; rotation++)
            {
                var stand = AirlineOperations.AdelaideRegionalBays[rotation % AirlineOperations.AdelaideRegionalBays.Count];
                FlyRoundTrip(clock, ops, plane, kingscote, departAt, stand);

                Assert.That(plane.CompletedTrips, Is.EqualTo(rotation));
                var fulfilled = rotation == definition.RequiredRotations;
                var expectedFunds = (long)rotation * definition.PaymentPerRotation
                    + (fulfilled ? definition.CompletionReward : 0);
                Assert.That(ops.CareerState.Funds, Is.EqualTo(expectedFunds), $"funds after rotation {rotation}");
                Assert.That(ops.CareerState.Reliability, Is.EqualTo(Math.Min(100,
                    AirlineCareerState.StartingReliability + rotation * definition.ReliabilityGainPerRotation)));

                if (fulfilled)
                    Assert.That(ops.CareerState.ActiveContract, Is.Null, "contract fulfilled and cleared");
                else
                    Assert.That(ops.CareerState.ActiveContract?.CompletedRotations, Is.EqualTo(rotation));

                departAt = clock.Now.ElapsedSeconds + 300;
            }
        }

        /// <summary>
        /// The core acceptance bar: a settlement is safe to apply exactly once. The player
        /// aircraft state machine never actually re-enters TaxiIn for a rotation already
        /// settled (a new rotation always carries a new, higher CompletedTrips), so this
        /// goes straight at the guard itself — the one place a future change (e.g. deriving
        /// "what to pay" from CompletedTrips instead of replaying exact history) could
        /// otherwise reintroduce a double payment.
        /// </summary>
        [Test]
        public void TryApplySettlement_SecondCallWithTheSameIdIsRefused()
        {
            var definition = RouteContractCatalogue.RegionalKingscoteIntro;
            var contract = new ActiveRouteContract(definition.Id, new SimulationTime(0));
            var career = new AirlineCareerState(activeContract: contract);
            var id = new SettlementId("VH-PAA", 1);

            var first = career.TryApplySettlement(id, definition);
            Assert.That(first, Is.Not.Null);
            Assert.That(first.Value.Payment, Is.EqualTo(definition.PaymentPerRotation));
            var fundsAfterFirst = career.Funds;
            var reliabilityAfterFirst = career.Reliability;

            var second = career.TryApplySettlement(id, definition);
            Assert.That(second, Is.Null, "a repeat settlement id must be refused, not paid again");
            Assert.That(career.Funds, Is.EqualTo(fundsAfterFirst));
            Assert.That(career.Reliability, Is.EqualTo(reliabilityAfterFirst));
            Assert.That(career.ActiveContract.CompletedRotations, Is.EqualTo(1), "rotation count must not double-advance either");
        }

        [Test]
        public void SaveAndRestore_DoesNotRepaySettlementsAlreadyApplied()
        {
            var (clock, ops, plane) = PlayerOnly();
            var definition = RouteContractCatalogue.RegionalKingscoteIntro;
            Assert.That(ops.AcceptContract(definition).Accepted, Is.True);
            FlyRoundTrip(clock, ops, plane, Code("KGC"), 600, AirlineOperations.AdelaideRegionalBays[1]);

            var fundsBeforeSave = ops.CareerState.Funds;
            var data = AirlineSave.Capture(ops);
            Assert.That(data.Version, Is.EqualTo(6));
            Assert.That(data.ProcessedSettlementKeys, Has.Count.EqualTo(1));

            var resumedClock = new ManualSimulationClock(clock.Now);
            var resumed = AirlineSave.Restore(data, resumedClock);

            Assert.That(resumed.CareerState.Funds, Is.EqualTo(fundsBeforeSave));
            Assert.That(resumed.CareerState.ActiveContract.CompletedRotations, Is.EqualTo(1));
            // The exact key already settled must survive the round trip — this is what stops a
            // future rotation from ever being confused for one already paid before the save.
            Assert.That(resumed.CareerState.HasSettled(new SettlementId(plane.Registration, 1)), Is.True);
        }

        [Test]
        public void V5Save_MigratesToFreshProvisionalCareerWithoutLosingFleetState()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var data = new AirlineSaveData
            {
                Version = 5,
                HomeCode = "ADL",
                ClockSeconds = 0,
                RandomState = 12345,
            };
            data.Airlines.Add(new AirlineRecord
            {
                Id = "PLAYER", Name = "Southern Cross Regional", LiveryHex = "#C8102E", IsPlayer = true
            });
            data.Fleet.Add(new AircraftRecord
            {
                Registration = "VH-PAA",
                AirlineId = "PLAYER",
                TypeId = AircraftType.Atr42.Id,
                State = FleetState.AtStand.ToString(),
                Stand = AirlineOperations.AdelaideRegionalBays[0].Value,
                CompletedTrips = 3,
                AssignedRunway = RunwayDirection.Runway05.ToString()
            });

            var restored = AirlineSave.Restore(data, clock);

            Assert.That(restored.CareerState.Tier, Is.EqualTo(OperatingTier.Provisional));
            Assert.That(restored.CareerState.Funds, Is.Zero, "no retroactive payment for pre-career trips");
            Assert.That(restored.CareerState.Reliability, Is.EqualTo(AirlineCareerState.StartingReliability));
            Assert.That(restored.CareerState.ActiveContract, Is.Null);

            var restoredPlane = restored.Fleet[0];
            Assert.That(restoredPlane.Registration, Is.EqualTo("VH-PAA"));
            Assert.That(restoredPlane.Stand, Is.EqualTo(AirlineOperations.AdelaideRegionalBays[0]));
            Assert.That(restoredPlane.CompletedTrips, Is.EqualTo(3), "existing operational state is preserved");
        }

        [Test]
        public void MismatchedRoute_DoesNotSettleAgainstTheActiveContract()
        {
            var (clock, ops, plane) = PlayerOnly();
            Assert.That(ops.AcceptContract(RouteContractCatalogue.RegionalKingscoteIntro).Accepted, Is.True);

            // Port Lincoln is within the ATR's range but is not the accepted KGC contract's route.
            FlyRoundTrip(clock, ops, plane, Code("PLO"), 600, AirlineOperations.AdelaideRegionalBays[1]);

            Assert.That(ops.CareerState.Funds, Is.Zero);
            Assert.That(ops.CareerState.ActiveContract, Is.Not.Null);
            Assert.That(ops.CareerState.ActiveContract.CompletedRotations, Is.Zero);
        }
    }
}

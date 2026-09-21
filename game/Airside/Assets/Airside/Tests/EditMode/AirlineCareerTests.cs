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
            var plane = ops.AddAircraft(player, "VH-PAA", AircraftType.Saab340, AirlineOperations.AdelaideRegionalBays[0]);
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

            while (plane.State is FleetState.TaxiOut or FleetState.HoldingShort)
            {
                var next = plane.StateEndsAt ?? clock.Now.Advance(1);
                RunTo(clock, ops, next.ElapsedSeconds);
            }

            Assert.That(plane.State, Is.EqualTo(FleetState.TakingOff), "empty runway: no hold");
            Assert.That(plane.StateEndsAt, Is.Not.Null);
            RunTo(clock, ops, plane.StateEndsAt.Value.ElapsedSeconds + 1);
            Assert.That(plane.State, Is.EqualTo(FleetState.Outbound));

            while (plane.State is FleetState.Outbound or FleetState.AtDestination or FleetState.Inbound)
            {
                Assert.That(plane.StateEndsAt, Is.Not.Null, plane.State.ToString());
                RunTo(clock, ops, plane.StateEndsAt.Value.ElapsedSeconds);
            }

            var parkedBy = clock.Now.ElapsedSeconds + AirlineOperations.LandingRunwaySecondsFor(plane.Type) + 3600;
            while (plane.State != FleetState.AtStand && clock.Now.ElapsedSeconds < parkedBy)
            {
                var next = ops.NextEventAt() ?? clock.Now.Advance(60);
                RunTo(clock, ops, next.ElapsedSeconds);
            }

            Assert.That(plane.State, Is.EqualTo(FleetState.AtStand));
        }

        [Test]
        public void OnTimePushback_RecordsLatenessAndKeepsReliabilityCapped()
        {
            var (clock, ops, plane) = PlayerOnly();
            var kingscote = Code("KGC");
            var departAt = DeparturePrep.LeadSeconds(plane.Type);
            Assert.That(ops.ScheduleDeparture(plane, kingscote, new SimulationTime(departAt)).Accepted, Is.True);
            RunTo(clock, ops, departAt);
            Assert.That(plane.State, Is.EqualTo(FleetState.TaxiOut));
            Assert.That(plane.PushbackLatenessSeconds, Is.Not.Null);
            Assert.That(plane.PushbackLatenessSeconds.Value, Is.LessThanOrEqualTo(FlightEconomics.OnTimeGraceSeconds));

            while (plane.State is FleetState.TaxiOut or FleetState.HoldingShort)
                RunTo(clock, ops, (plane.StateEndsAt ?? clock.Now.Advance(1)).ElapsedSeconds);
            Assert.That(plane.StateEndsAt, Is.Not.Null);
            RunTo(clock, ops, plane.StateEndsAt.Value.ElapsedSeconds + 1);
            while (plane.State is FleetState.Outbound or FleetState.AtDestination or FleetState.Inbound)
                RunTo(clock, ops, plane.StateEndsAt.Value.ElapsedSeconds);
            var parkedBy = clock.Now.ElapsedSeconds + AirlineOperations.LandingRunwaySecondsFor(plane.Type) + 3600;
            while (plane.State != FleetState.AtStand && clock.Now.ElapsedSeconds < parkedBy)
                RunTo(clock, ops, (ops.NextEventAt() ?? clock.Now.Advance(60)).ElapsedSeconds);

            Assert.That(plane.State, Is.EqualTo(FleetState.AtStand));
            Assert.That(plane.PushbackLatenessSeconds, Is.Null);
            Assert.That(ops.CareerState.Reliability, Is.EqualTo(100), "on-time bonus cannot push past 100");
            Assert.That(ops.CareerState.CompletedPlayerRotations, Is.EqualTo(1));
        }

        [Test]
        public void LatePushback_DropsReliabilityBeforeSettlementPay()
        {
            var (clock, ops, plane) = PlayerOnly();
            ops.RestoreCareerState(AirlineCareerState.StartingFunds, 90, nameof(OperatingTier.Provisional),
                null, 0, 0, Array.Empty<string>(), Array.Empty<string>(), 0);
            var kingscote = Code("KGC");
            var departAt = DeparturePrep.LeadSeconds(plane.Type);
            Assert.That(ops.ScheduleDeparture(plane, kingscote, new SimulationTime(departAt)).Accepted, Is.True);
            RunTo(clock, ops, departAt);
            Assert.That(plane.State, Is.EqualTo(FleetState.TaxiOut));
            plane.PushbackLatenessSeconds = FlightEconomics.HardLateSeconds + 30;

            while (plane.State is FleetState.TaxiOut or FleetState.HoldingShort)
                RunTo(clock, ops, (plane.StateEndsAt ?? clock.Now.Advance(1)).ElapsedSeconds);
            Assert.That(plane.StateEndsAt, Is.Not.Null);
            RunTo(clock, ops, plane.StateEndsAt.Value.ElapsedSeconds + 1);
            while (plane.State is FleetState.Outbound or FleetState.AtDestination or FleetState.Inbound)
                RunTo(clock, ops, plane.StateEndsAt.Value.ElapsedSeconds);
            var parkedBy = clock.Now.ElapsedSeconds + AirlineOperations.LandingRunwaySecondsFor(plane.Type) + 3600;
            while (plane.State != FleetState.AtStand && clock.Now.ElapsedSeconds < parkedBy)
                RunTo(clock, ops, (ops.NextEventAt() ?? clock.Now.Advance(60)).ElapsedSeconds);

            Assert.That(plane.State, Is.EqualTo(FleetState.AtStand));
            Assert.That(plane.PushbackLatenessSeconds, Is.Null, "consumed at settlement");
            Assert.That(ops.CareerState.Reliability, Is.EqualTo(88),
                "90 start − 2 hard-late penalty, no contract bonus");
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
                var cost = FlightEconomics.DispatchCost(plane.Type, ops.DistanceKm(kingscote));
                var pay = FlightEconomics.FlightPay(plane.Type, ops.DistanceKm(kingscote));
                var expectedFunds = AirlineCareerState.StartingFunds
                    + (long)rotation * (pay - cost + definition.PaymentPerRotation)
                    + (fulfilled ? definition.CompletionReward : 0)
                    // Fulfilling Kingscote after 3+ rotations also completes campaign chapter 1 (ADR 0083).
                    + (fulfilled ? Campaign.Evaluate(ops.CareerState, ops.PlayerOwnedTypes())[0].Reward : 0);
                Assert.That(ops.CareerState.Funds, Is.EqualTo(expectedFunds), $"funds after rotation {rotation}");
                Assert.That(ops.CareerState.Reliability, Is.EqualTo(Math.Min(100,
                    AirlineCareerState.StartingReliability + rotation * definition.ReliabilityGainPerRotation)));

                if (fulfilled)
                {
                    Assert.That(ops.CareerState.ActiveContract, Is.Null, "contract fulfilled and cleared");
                    Assert.That(ops.CareerState.HasCompleted(definition.Id), Is.True);
                    Assert.That(ops.CareerState.Tier, Is.EqualTo(OperatingTier.Regional));
                }
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
            Assert.That(data.Version, Is.EqualTo(AirlineSaveData.CurrentVersion));
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
            Assert.That(restored.CareerState.Funds, Is.EqualTo(AirlineCareerState.StartingFunds),
                "no retroactive payment for pre-career trips — opening float only");
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

            var cost = FlightEconomics.DispatchCost(plane.Type, ops.DistanceKm(Code("PLO")));
            var pay = FlightEconomics.FlightPay(plane.Type, ops.DistanceKm(Code("PLO")));
            Assert.That(ops.CareerState.Funds, Is.EqualTo(AirlineCareerState.StartingFunds - cost + pay),
                "an unmatched route still pays the flight, just not the contract bonus");
            Assert.That(ops.CareerState.ActiveContract, Is.Not.Null);
            Assert.That(ops.CareerState.ActiveContract.CompletedRotations, Is.Zero);
        }

        [Test]
        public void CancellingAContractFlight_CostsReliability()
        {
            var (_, ops, plane) = PlayerOnly();
            var definition = RouteContractCatalogue.RegionalKingscoteIntro;
            Assert.That(ops.AcceptContract(definition).Accepted, Is.True);
            Assert.That(ops.ScheduleDeparture(plane, Code("KGC"), new SimulationTime(600)).Accepted, Is.True);

            Assert.That(ops.CancelDeparture(plane).Accepted, Is.True);

            Assert.That(ops.CareerState.Reliability,
                Is.EqualTo(AirlineCareerState.StartingReliability - definition.ReliabilityLossOnCancel));
            Assert.That(ops.CareerState.ActiveContract.CompletedRotations, Is.Zero, "a cancellation is not a rotation");
        }

        [Test]
        public void CancellingAnUnrelatedFlight_CostsNothing()
        {
            var (_, ops, plane) = PlayerOnly();
            Assert.That(ops.AcceptContract(RouteContractCatalogue.RegionalKingscoteIntro).Accepted, Is.True);
            // Port Lincoln has nothing to do with the accepted KGC contract.
            Assert.That(ops.ScheduleDeparture(plane, Code("PLO"), new SimulationTime(600)).Accepted, Is.True);

            Assert.That(ops.CancelDeparture(plane).Accepted, Is.True);

            Assert.That(ops.CareerState.Reliability, Is.EqualTo(AirlineCareerState.StartingReliability));
        }

        [Test]
        public void Scheduling_ChargesDispatchAndRefusesWhenBroke()
        {
            var (_, ops, plane) = PlayerOnly();
            var kingscote = Code("KGC");
            var cost = FlightEconomics.DispatchCost(plane.Type, ops.DistanceKm(kingscote));
            Assert.That(ops.ScheduleDeparture(plane, kingscote, new SimulationTime(600)).Accepted, Is.True);
            Assert.That(ops.CareerState.Funds, Is.EqualTo(AirlineCareerState.StartingFunds - cost));

            Assert.That(ops.CancelDeparture(plane).Accepted, Is.True);
            Assert.That(ops.CareerState.Funds, Is.EqualTo(AirlineCareerState.StartingFunds), "cancel refunds the dispatch cost");

            ops.CareerState.TryChargeDispatch(AirlineCareerState.StartingFunds);
            Assert.That(ops.ScheduleDeparture(plane, kingscote, new SimulationTime(600)).Accepted, Is.False);
            Assert.That(ops.CareerState.Funds, Is.Zero);
        }

        [Test]
        public void CompletingTheStarterContract_UnlocksRegionalAndRefusesARepeat()
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
                departAt = clock.Now.ElapsedSeconds + 300;
            }

            Assert.That(ops.CareerState.Tier, Is.EqualTo(OperatingTier.Regional));
            Assert.That(ops.AcceptContract(definition).Accepted, Is.False, "cannot farm a completed contract");
            Assert.That(ops.AcceptContract(RouteContractCatalogue.RegionalWhyallaIntro).Accepted, Is.True,
                "Regional unlocks the Whyalla contract");
        }

        [Test]
        public void MelbourneContract_NeedsADash8AndPaysTheDomesticBand()
        {
            var (clock, ops, plane) = PlayerOnly();
            Assert.That(ops.ScheduleDeparture(plane, Code("MEL"), new SimulationTime(600)).Accepted, Is.False,
                "the starter Saab is Regional only");
            Assert.That(ops.CanOperate(plane, Code("MEL")), Is.False);
            Assert.That(ops.CanReach(plane, Code("MEL")), Is.True, "range still reaches Melbourne; the band does not");

            ops.RestoreCareerState(50_000, 90, nameof(OperatingTier.Regional), null, 0, 0, Array.Empty<string>(),
                Array.Empty<string>(), 12);
            Assert.That(ops.BuyAircraft(AircraftType.Dash8Q400).Accepted, Is.True);
            FleetAircraft dash = null;
            foreach (var aircraft in ops.FleetOf(ops.PlayerAirline))
                if (aircraft.Type.Id == AircraftType.Dash8Q400.Id)
                    dash = aircraft;
            Assert.That(dash, Is.Not.Null);
            Assert.That(ops.CanOperate(dash, Code("MEL")), Is.True);

            var definition = RouteContractCatalogue.DomesticMelbourneIntro;
            Assert.That(ops.AcceptContract(definition).Accepted, Is.True);
            var melbourne = Code("MEL");
            var departAt = 600L;
            FlyRoundTrip(clock, ops, dash, melbourne, departAt, AirlineOperations.AdelaideRegionalBays[2]);

            var cost = FlightEconomics.DispatchCost(dash.Type, ops.DistanceKm(melbourne));
            var pay = FlightEconomics.FlightPay(dash.Type, ops.DistanceKm(melbourne), RouteBand.Domestic);
            Assert.That(ops.CareerState.Funds, Is.EqualTo(50_000 - AircraftAcquisition.Dash8Q400.Price - cost + pay
                + definition.PaymentPerRotation));
            Assert.That(pay, Is.GreaterThan(FlightEconomics.FlightPay(AircraftType.Atr42,
                ops.DistanceKm(Code("KGC")), RouteBand.Regional)));
        }

        [Test]
        public void PortLincolnContract_IsOfferedFromTheStart()
        {
            var (_, ops, _) = PlayerOnly();
            Assert.That(ops.AcceptContract(RouteContractCatalogue.RegionalPortLincolnIntro).Accepted, Is.True);
            Assert.That(ops.AcceptContract(RouteContractCatalogue.RegionalKingscoteIntro).Accepted, Is.False,
                "one active contract");
        }

        [Test]
        public void InternationalTier_UnlocksWithAJetNotAWidebody()
        {
            Assert.That(AircraftAcquisition.AirbusA321Neo.RequiredTier, Is.EqualTo(OperatingTier.Domestic),
                "A321 is the Tasman step after Domestic, not locked behind International");
            Assert.That(AircraftAcquisition.AirbusA350900.RequiredTier, Is.EqualTo(OperatingTier.International));

            var (_, ops, _) = PlayerOnly();
            ops.RestoreCareerState(200_000, 90, nameof(OperatingTier.Domestic), null, 0, 0, Array.Empty<string>(),
                Array.Empty<string>(), 28);
            Assert.That(ops.BuyAircraft(AircraftType.Boeing7378).Accepted, Is.True);
            Assert.That(ops.CareerState.Tier, Is.EqualTo(OperatingTier.International),
                "28 rotations + jet ownership unlocks International without owning a widebody");

            // A350 still needs its own reliability/rotation floor on top of the tier.
            ops.RestoreCareerState(ops.CareerState.Funds, 95, nameof(OperatingTier.International), null, 0, 0,
                Array.Empty<string>(), Array.Empty<string>(), 40);
            Assert.That(ops.BuyAircraft(AircraftType.AirbusA350900).Accepted, Is.True,
                "widebodies become buyable once International is open");
        }

        [Test]
        public void LifetimeRevenue_AccumulatesAndNeverDropsWhenFundsAreSpent()
        {
            var (clock, ops, plane) = PlayerOnly();
            var definition = RouteContractCatalogue.RegionalKingscoteIntro;
            Assert.That(ops.AcceptContract(definition).Accepted, Is.True);
            var kingscote = Code("KGC");
            FlyRoundTrip(clock, ops, plane, kingscote, 600, AirlineOperations.AdelaideRegionalBays[1]);

            var pay = FlightEconomics.FlightPay(plane.Type, ops.DistanceKm(kingscote));
            var expectedRevenue = pay + definition.PaymentPerRotation;
            Assert.That(ops.CareerState.LifetimeRevenue, Is.EqualTo(expectedRevenue));

            var fundsBefore = ops.CareerState.Funds;
            Assert.That(ops.CareerState.TryChargeDispatch(fundsBefore), Is.True, "spend everything on hand");
            Assert.That(ops.CareerState.Funds, Is.Zero);
            Assert.That(ops.CareerState.LifetimeRevenue, Is.EqualTo(expectedRevenue),
                "spending funds does not undo lifetime revenue already earned");
        }

        [Test]
        public void ContractHistory_RecordsAFulfilledContractOnce()
        {
            var (clock, ops, plane) = PlayerOnly();
            var definition = RouteContractCatalogue.RegionalKingscoteIntro;
            Assert.That(ops.AcceptContract(definition).Accepted, Is.True);
            Assert.That(ops.CareerState.ContractHistory, Is.Empty, "nothing fulfilled yet");
            var kingscote = Code("KGC");

            var departAt = 600L;
            for (var rotation = 1; rotation <= definition.RequiredRotations; rotation++)
            {
                var stand = AirlineOperations.AdelaideRegionalBays[rotation % AirlineOperations.AdelaideRegionalBays.Count];
                FlyRoundTrip(clock, ops, plane, kingscote, departAt, stand);
                departAt = clock.Now.ElapsedSeconds + 300;
            }

            Assert.That(ops.CareerState.ContractHistory.Count, Is.EqualTo(1));
            var record = ops.CareerState.ContractHistory[0];
            Assert.That(record.DefinitionId, Is.EqualTo(definition.Id));
            Assert.That(record.OriginCode, Is.EqualTo(definition.OriginCode));
            Assert.That(record.DestinationCode, Is.EqualTo(definition.DestinationCode));
            var expectedTotalPaid = (long)definition.RequiredRotations * definition.PaymentPerRotation
                                     + definition.CompletionReward;
            Assert.That(record.TotalPaid, Is.EqualTo(expectedTotalPaid));
        }

        [Test]
        public void ContractHistory_CapsAtMaxAndKeepsTheMostRecentFirst()
        {
            var (clock, ops, plane) = PlayerOnly();
            var kingscote = Code("KGC");
            var departAt = 600L;
            var completed = AirlineCareerState.MaxContractHistory + 2;
            string lastId = null;
            for (var i = 0; i < completed; i++)
            {
                var definition = new RouteContractDefinition(
                    $"HIST-TEST-{i}", "ADL", "KGC", AircraftType.Saab340, 1, 50, 25, 0, OperatingTier.Provisional);
                Assert.That(ops.AcceptContract(definition).Accepted, Is.True, $"accept #{i}");
                var stand = AirlineOperations.AdelaideRegionalBays[i % AirlineOperations.AdelaideRegionalBays.Count];
                FlyRoundTrip(clock, ops, plane, kingscote, departAt, stand);
                departAt = clock.Now.ElapsedSeconds + 300;
                lastId = definition.Id;
            }

            Assert.That(ops.CareerState.ContractHistory.Count, Is.EqualTo(AirlineCareerState.MaxContractHistory),
                "capped, oldest dropped");
            Assert.That(ops.CareerState.ContractHistory[0].DefinitionId, Is.EqualTo(lastId),
                "most recently fulfilled contract is first");
        }

        [Test]
        public void Save_RoundTripsLifetimeRevenueAndContractHistory()
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
                departAt = clock.Now.ElapsedSeconds + 300;
            }

            var data = AirlineSave.Capture(ops);
            Assert.That(data.CareerLifetimeRevenue, Is.EqualTo(ops.CareerState.LifetimeRevenue));
            Assert.That(data.ContractHistory.Count, Is.EqualTo(1));

            var restored = AirlineSave.Restore(data, new ManualSimulationClock(new SimulationTime(data.ClockSeconds)));
            Assert.That(restored.CareerState.LifetimeRevenue, Is.EqualTo(ops.CareerState.LifetimeRevenue));
            Assert.That(restored.CareerState.ContractHistory.Count, Is.EqualTo(1));
            Assert.That(restored.CareerState.ContractHistory[0].DefinitionId, Is.EqualTo(definition.Id));
            Assert.That(restored.CareerState.ContractHistory[0].TotalPaid,
                Is.EqualTo(ops.CareerState.ContractHistory[0].TotalPaid));
        }

        [Test]
        public void LowReliability_PaysLessOnTheFlatRateButNeverTouchesContractPay()
        {
            var (clock, ops, plane) = PlayerOnly();
            ops.RestoreCareerState(AirlineCareerState.StartingFunds, 40, nameof(OperatingTier.Provisional),
                null, 0, 0, Array.Empty<string>());
            Assert.That(FlightEconomics.ReliabilityMultiplier(40), Is.EqualTo(0.8));

            var kingscote = Code("KGC");
            FlyRoundTrip(clock, ops, plane, kingscote, 600, AirlineOperations.AdelaideRegionalBays[1]);

            var cost = FlightEconomics.DispatchCost(plane.Type, ops.DistanceKm(kingscote));
            var pay = FlightEconomics.FlightPay(plane.Type, ops.DistanceKm(kingscote));
            var scaledPay = (long)Math.Round(pay * 0.8);
            Assert.That(ops.CareerState.Funds,
                Is.EqualTo(AirlineCareerState.StartingFunds - cost + scaledPay),
                "flat-rate pay scaled down for a poor reliability record");
        }
    }
}

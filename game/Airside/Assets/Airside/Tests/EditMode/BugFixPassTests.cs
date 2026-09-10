using System;
using System.Linq;
using Airside.Domain;
using Airside.Persistence;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>Focused regressions for the 2026-09-08 simulation/domain bugfix pass.</summary>
    public sealed class BugfixPassTests
    {
        [Test]
        public void HoldShort_SetsTrafficWaitForRunwayWhenTakeoffBlocked()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var table = new ReservationTable();
            var simulation = new AirportSimulation(clock, new SeededRandomSource(3), table);
            var flight = simulation.Flights[0];

            CommercialFlight taxiOut = null;
            for (var second = 1; second <= 5000 && taxiOut == null; second++)
            {
                clock.Advance(1);
                simulation.Update();
                if (flight.Operation.Phase == AircraftPhase.TaxiOut)
                    taxiOut = flight;
            }

            Assert.That(taxiOut, Is.Not.Null);
            Assert.That(table.TryReplace(new StableId("BLOCKER"), new[] { AirportSimulation.Runway }, out _), Is.True);

            // Exhaust TaxiOut while the runway stays blocked, then one more tick.
            for (var second = 1; second <= 40; second++)
            {
                clock.Advance(1);
                simulation.Update();
                if (flight.Operation.Phase != AircraftPhase.TaxiOut)
                    break;
            }

            Assert.That(flight.Operation.Phase, Is.EqualTo(AircraftPhase.TaxiOut), "must remain holding short");
            Assert.That(simulation.TrafficWaits.TryGetWaitStart(flight.OwnerId, out _), Is.True);
            var wait = simulation.TrafficWaits.ActiveWaits.Single(w => w.Aircraft.Equals(flight.OwnerId));
            Assert.That(wait.Resource, Is.EqualTo(AirportSimulation.Runway));
        }

        [Test]
        public void MaxConcurrentCommercialFlights_TracksStandCount()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var simulation = new AirportSimulation(clock, new SeededRandomSource(1), new ReservationTable());
            Assert.That(simulation.MaxConcurrentCommercialFlights, Is.EqualTo(2));
            Assert.That(simulation.BuildThirdStand(), Is.True);
            Assert.That(simulation.MaxConcurrentCommercialFlights, Is.EqualTo(3));
        }

        [Test]
        public void ExtraCrew_LeavesAtStandWhenTurnaroundCompletesEarly()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var simulation = new AirportSimulation(clock, new SeededRandomSource(1), new ReservationTable());
            while (simulation.HireGroundCrew()) { }

            CommercialFlight atStand = null;
            for (var second = 1; second <= 5000 && atStand == null; second++)
            {
                clock.Advance(1);
                simulation.Update();
                foreach (var flight in simulation.Flights)
                {
                    if (flight.Operation.Phase == AircraftPhase.AtStand && flight.Turnaround != null)
                    {
                        atStand = flight;
                        break;
                    }
                }
            }

            Assert.That(atStand, Is.Not.Null);
            var phaseStart = atStand.Operation.PhaseStartedAt.ElapsedSeconds;

            // Wait until turnaround is complete but under the 45s phase window if possible.
            for (var second = 1; second <= 120; second++)
            {
                clock.Advance(1);
                simulation.Update();
                if (atStand.Operation.Phase != AircraftPhase.AtStand)
                    break;
            }

            var elapsedInPhase = clock.Now.ElapsedSeconds - phaseStart;
            Assert.That(atStand.Operation.Phase, Is.Not.EqualTo(AircraftPhase.AtStand),
                "extra crew should leave AtStand once turnaround completes");
            Assert.That(elapsedInPhase, Is.LessThanOrEqualTo(TurnaroundWorkflow.ScheduledWindowSeconds + 2),
                "should not be forced to wait out a late turnaround");
        }

        [Test]
        public void HiringMidTurnaround_SpeedsRemainingWork()
        {
            var staffing = new AirportStaffing();
            Assert.That(staffing.Release(), Is.True);
            Assert.That(staffing.Release(), Is.True);

            var live = new TurnaroundWorkflow(new SimulationTime(0), false, () => staffing.TurnaroundSpeedFactor);
            var frozenSlow = new TurnaroundWorkflow(new SimulationTime(0), false, staffing.TurnaroundSpeedFactor);

            // Mid-turnaround: hire back to baseline.
            staffing.Hire();
            staffing.Hire();
            Assert.That(staffing.TurnaroundSpeedFactor, Is.EqualTo(1.0));

            var liveDone = CompletionSecond(live);
            var frozenDone = CompletionSecond(frozenSlow);
            Assert.That(liveDone, Is.LessThan(frozenDone),
                "live staffing factor should shorten remaining work after hire");
        }

        [Test]
        public void Accept_RefusesExpiredOffer()
        {
            var routes = new AirportRoutes(new SimulationTime(0));
            routes.Update(new SimulationTime(AirportRoutes.FirstOfferAfterSeconds));
            var expiresAt = routes.Pending.ExpiresAt;
            Assert.That(routes.Accept(expiresAt, reputationScore: 100), Is.False);
        }

        [Test]
        public void ExpiredOffer_IncrementsMissedOffers()
        {
            var routes = new AirportRoutes(new SimulationTime(0));
            routes.Update(new SimulationTime(AirportRoutes.FirstOfferAfterSeconds));
            Assert.That(routes.OffersDeclined, Is.EqualTo(0));
            routes.Update(new SimulationTime(AirportRoutes.FirstOfferAfterSeconds + AirportRoutes.OfferWindowSeconds));
            Assert.That(routes.Pending, Is.Null);
            Assert.That(routes.MissedOffers, Is.EqualTo(1));
            Assert.That(routes.OffersDeclined, Is.EqualTo(0));
        }

        [Test]
        public void ExpiredOffers_MatchAcrossLargeAndSmallSteps()
        {
            var small = new AirportRoutes(new SimulationTime(0));
            for (long t = 1; t <= 400; t++)
                small.Update(new SimulationTime(t));

            var large = new AirportRoutes(new SimulationTime(0));
            large.Update(new SimulationTime(400));

            Assert.That(large.OffersMade, Is.EqualTo(small.OffersMade));
            Assert.That(large.OffersDeclined, Is.EqualTo(small.OffersDeclined));
            Assert.That(large.Pending?.Id, Is.EqualTo(small.Pending?.Id));
        }

        [Test]
        public void GroundTrafficYield_KeepsLegProgress()
        {
            var table = new ReservationTable();
            var aircraft = new GroundTrafficAircraft(new StableId("GT-201"), table, GroundTrafficRole.ArriveDepart, 0);
            var monitor = new TrafficWaitMonitor();

            // Enter first corridor leg.
            for (var i = 0; i < 5; i++)
                aircraft.Reposition(new SimulationTime(i + 1), monitor, AirportSimulation.StandOne, 2, true);

            var progressBefore = aircraft.Progress;
            Assert.That(progressBefore, Is.GreaterThan(0));

            aircraft.Yield(new[] { AirportTaxiNetwork.Corridor });
            Assert.That(aircraft.Progress, Is.EqualTo(progressBefore).Within(0.0001),
                "Yield must keep mid-leg progress so Position does not snap to the leg start");

            // Re-acquire and continue — progress must not restart from zero.
            aircraft.Reposition(new SimulationTime(10), monitor, AirportSimulation.StandOne, 2, true);
            Assert.That(aircraft.Progress, Is.GreaterThanOrEqualTo(progressBefore));
        }

        [Test]
        public void PhaseProgress_AtStand_UsesTurnaroundProgress()
        {
            var started = new SimulationTime(0);
            var operation = new AircraftOperation("AS-101", started);
            AdvanceOperationToPhase(operation, AircraftPhase.AtStand);
            Assert.That(operation.Phase, Is.EqualTo(AircraftPhase.AtStand));

            var turnaround = new TurnaroundWorkflow(operation.PhaseStartedAt, false);
            operation.BindAtStandProgress(t => turnaround.Progress01(t));

            var mid = operation.PhaseStartedAt.Advance(20);
            Assert.That(operation.PhaseProgress(mid), Is.EqualTo(turnaround.Progress01(mid)).Within(0.0001));
        }

        [Test]
        public void Approach_ReservesAssignedStand()
        {
            var flight = new CommercialFlight("AS-101", new SimulationTime(0), AirportSimulation.StandTwo,
                new AirportTaxiNetwork().RoutesTo(AirportSimulation.StandTwo));
            var resources = flight.ResourcesForPhase(AircraftPhase.Approach, new SimulationTime(0)).ToArray();
            Assert.That(resources, Does.Contain(AirportSimulation.StandTwo));
            Assert.That(resources.Any(resource => resource.Equals(AirportSimulation.Runway)), Is.False);

            var landing = flight.ResourcesForPhase(AircraftPhase.Landing, new SimulationTime(0)).ToArray();
            Assert.That(landing, Does.Contain(AirportSimulation.StandTwo));
            Assert.That(landing, Does.Contain(AirportSimulation.Runway));
        }

        [Test]
        public void StableId_GetHashCode_IsNullSafeForDefault()
        {
            var id = default(StableId);
            Assert.DoesNotThrow(() => _ = id.GetHashCode());
            Assert.That(id.GetHashCode(), Is.EqualTo(0));
        }

        [Test]
        public void OperationalEventLog_PinsInsolventWhenTrimming()
        {
            var log = new OperationalEventLog(capacity: 3);
            log.Add(new OperationalEvent(new SimulationTime(1), "", "Insolvent", "cash gone"));
            for (var i = 0; i < 10; i++)
                log.Add(new OperationalEvent(new SimulationTime(i + 2), "AS-101", $"Noise {i}", "x"));

            Assert.That(log.Events.Any(e => e.Title == "Insolvent"), Is.True);
            Assert.That(log.Events.Count, Is.LessThanOrEqualTo(3));
        }

        [Test]
        public void ZeroSeed_MatchesBetweenSessionAndRandomSource()
        {
            var fromSource = new SeededRandomSource(0);
            var fromSubstitute = new SeededRandomSource(SeededRandomSource.ZeroSeedSubstitute);
            Assert.That(fromSource.NextInt(0, 1000), Is.EqualTo(fromSubstitute.NextInt(0, 1000)));
            Assert.That(SeededRandomSource.ZeroSeedSubstitute, Is.EqualTo(0x6D2B79F5u));
        }

        [Test]
        public void SaveValidate_RejectsUnknownLocationId()
        {
            var save = new AirsideSaveData
            {
                schemaVersion = AirsideSaveData.CurrentSchemaVersion,
                randomSeed = 42,
                locationId = "NOPE"
            };
            Assert.Throws<InvalidOperationException>(() => save.Validate());
        }

        [Test]
        public void PriorityCrew_MidWorkflow_DoesNotInstantlyComplete()
        {
            var workflow = new TurnaroundWorkflow(new SimulationTime(0), cleaningDisruption: true);
            var mid = new SimulationTime(30);
            Assert.That(workflow.IsComplete(mid), Is.False);
            workflow.EnablePriorityCrew(mid);
            Assert.That(workflow.IsComplete(mid), Is.False,
                "hiring priority mid-turnaround must not instantly finish elapsed work");
            Assert.That(workflow.IsComplete(new SimulationTime(80)), Is.True);
        }

        [Test]
        public void RecordAirportWideEvents_UseEmptyFlightId()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var simulation = new AirportSimulation(clock, new SeededRandomSource(1), new ReservationTable());
            Assert.That(simulation.BuildThirdStand(), Is.True);
            var built = simulation.EventLog.Events.Last(e => e.Title == "Stand 3 built");
            Assert.That(built.FlightId, Is.Empty);
        }

        private static long CompletionSecond(TurnaroundWorkflow workflow)
        {
            for (long t = 0; t <= 400; t++)
                if (workflow.IsComplete(new SimulationTime(t)))
                    return t;
            return -1;
        }

        /// <summary>
        /// Gated <see cref="AircraftOperation.AdvanceTo"/> only moves one phase per call when
        /// catching up (PhaseStartedAt snaps to <c>now</c>). Step phase-by-phase for tests.
        /// </summary>
        private static void AdvanceOperationToPhase(AircraftOperation operation, AircraftPhase target)
        {
            var guard = 0;
            while (operation.Phase < target && guard++ < 16)
            {
                var end = operation.PhaseStartedAt.Advance(operation.PhaseDurationSeconds);
                Assert.That(operation.AdvanceTo(end, phase => phase < target), Is.True);
            }

            Assert.That(operation.Phase, Is.EqualTo(target));
        }
    }
}

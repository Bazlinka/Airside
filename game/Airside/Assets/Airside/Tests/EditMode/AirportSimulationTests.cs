using Airside.Domain;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class AirportSimulationTests
    {
        [Test]
        public void FiftyCycles_CompleteWithoutReservationConflicts()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var simulation = new AirportSimulation(clock, new SeededRandomSource(42), new ReservationTable());

            for (var second = 1; second <= 10000 && simulation.CompletedCycles < 50; second++)
            {
                clock.Advance(1);
                simulation.Update();
            }

            Assert.That(simulation.CompletedCycles, Is.EqualTo(50));
            Assert.That(simulation.ReservationConflicts, Is.Zero);
            Assert.That(simulation.ActiveAircraft.AircraftId, Is.EqualTo("AS-151"));
        }

        [Test]
        public void PriorityCrew_CostsOnceAndIsAppliedToTheActiveTurnaround()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var simulation = new AirportSimulation(clock, new SeededRandomSource(42), new ReservationTable());
            clock.Advance(57);
            simulation.Update();

            Assert.That(simulation.ActiveAircraft.Phase, Is.EqualTo(AircraftPhase.AtStand));
            Assert.That(simulation.EnablePriorityCrew(), Is.True);
            Assert.That(simulation.EnablePriorityCrew(), Is.False);
            Assert.That(simulation.ActiveTurnaround.PriorityCrewEnabled, Is.True);
            Assert.That(simulation.Economy.Cash, Is.EqualTo(AirportEconomy.StartingCash - AirportEconomy.PriorityCrewCost));
        }

        [Test]
        public void DelayedFlight_ReconcilesRevenueAndDelayInTheLiveSimulation()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var simulation = new AirportSimulation(clock, new SeededRandomSource(24031996), new ReservationTable());
            clock.Advance(160);
            simulation.Update();

            Assert.That(simulation.ActiveAircraft.Phase, Is.EqualTo(AircraftPhase.Departed));
            Assert.That(simulation.LastDelaySeconds, Is.EqualTo(6));
            Assert.That(simulation.LastDelayCause, Is.EqualTo("Cabin cleaning disruption"));
            Assert.That(simulation.Economy.Cash, Is.EqualTo(25960));
        }

        [Test]
        public void LargeAndSmallTimeSteps_ProduceTheSameSimulationState()
        {
            var smallClock = new ManualSimulationClock(new SimulationTime(0));
            var small = new AirportSimulation(smallClock, new SeededRandomSource(99), new ReservationTable());
            for (var second = 1; second <= 3217; second++)
            {
                smallClock.Advance(1);
                small.Update();
            }

            var largeClock = new ManualSimulationClock(new SimulationTime(0));
            var large = new AirportSimulation(largeClock, new SeededRandomSource(99), new ReservationTable());
            largeClock.Advance(3217);
            large.Update();

            Assert.That(large.CompletedCycles, Is.EqualTo(small.CompletedCycles));
            Assert.That(large.ActiveAircraft.Phase, Is.EqualTo(small.ActiveAircraft.Phase));
            Assert.That(large.AssignedStand, Is.EqualTo(small.AssignedStand));
            Assert.That(large.Reservations.OccupiedResources, Is.EquivalentTo(small.Reservations.OccupiedResources));
        }

        [Test]
        public void ReservationTable_BlocksASecondOwnerAtomically()
        {
            var table = new ReservationTable();
            var first = new StableId("AS-101");
            var second = new StableId("AS-102");

            Assert.That(table.TryReplace(first, new[] { AirportSimulation.Runway }, out _), Is.True);
            Assert.That(table.TryReplace(second, new[] { AirportSimulation.Runway, AirportTaxiNetwork.AlphaOne }, out var blocked), Is.False);
            Assert.That(blocked, Is.EqualTo(AirportSimulation.Runway));
            Assert.That(table.IsReserved(AirportTaxiNetwork.AlphaOne), Is.False);
        }

        [Test]
        public void SeededRandomSource_RepeatsItsSequence()
        {
            var first = new SeededRandomSource(1234);
            var second = new SeededRandomSource(1234);

            for (var index = 0; index < 100; index++)
                Assert.That(first.NextInt(0, 10000), Is.EqualTo(second.NextInt(0, 10000)));
        }

        [Test]
        public void TaxiRoutes_UseNamedSharedSegmentsAndAStandSpecificLeadIn()
        {
            var network = new AirportTaxiNetwork();
            var standOne = network.RouteTo(AirportSimulation.StandOne);
            var standTwo = network.RouteTo(AirportSimulation.StandTwo);

            Assert.That(standOne.SegmentIds[0], Is.EqualTo(AirportTaxiNetwork.AlphaOne));
            Assert.That(standOne.SegmentIds[1], Is.EqualTo(AirportTaxiNetwork.AlphaTwo));
            Assert.That(standOne.SegmentIds[2], Is.EqualTo(AirportTaxiNetwork.StandOneLeadIn));
            Assert.That(standTwo.SegmentIds[2], Is.EqualTo(AirportTaxiNetwork.StandTwoLeadIn));
            Assert.That(standOne.Points.Count, Is.EqualTo(standOne.SegmentIds.Count + 1));
        }

        [Test]
        public void OperationalHistory_RecordsAnExplainableFlightSequence()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var simulation = new AirportSimulation(clock, new SeededRandomSource(24031996), new ReservationTable());
            clock.Advance(160);
            simulation.Update();

            var titles = System.Linq.Enumerable.Select(simulation.EventLog.Events, entry => entry.Title);
            Assert.That(titles, Does.Contain("Flight inbound"));
            Assert.That(titles, Does.Contain("Landing"));
            Assert.That(titles, Does.Contain("On stand"));
            Assert.That(titles, Does.Contain("Delayed 6s"));
            Assert.That(titles, Does.Contain("Departed"));
        }

        [Test]
        public void Taxiing_ReleasesEachSegmentBeforeReservingTheNext()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var simulation = new AirportSimulation(clock, new SeededRandomSource(24031996), new ReservationTable());

            clock.Advance(33);
            simulation.Update();
            Assert.That(FlightOwns(simulation, AirportTaxiNetwork.AlphaOne), Is.True);

            clock.Advance(9);
            simulation.Update();
            Assert.That(FlightOwns(simulation, AirportTaxiNetwork.AlphaOne), Is.False);
            Assert.That(FlightOwns(simulation, AirportTaxiNetwork.AlphaTwo), Is.True);

            clock.Advance(8);
            simulation.Update();
            Assert.That(FlightOwns(simulation, AirportTaxiNetwork.AlphaTwo), Is.False);
            Assert.That(FlightOwns(simulation, AirportTaxiNetwork.StandOneLeadIn), Is.True);
        }

        private static bool FlightOwns(AirportSimulation simulation, StableId segment)
        {
            return simulation.Reservations.TryGetOwner(segment, out var owner) &&
                   owner.Value == simulation.ActiveAircraft.AircraftId;
        }

        [Test]
        public void SecondAircraft_SharesTheTaxiwayWithoutBlockingTheArrivingFlight()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var simulation = new AirportSimulation(clock, new SeededRandomSource(42), new ReservationTable());

            var groundTrafficUsedAlpha = false;
            for (var second = 1; second <= 2000 && simulation.CompletedCycles < 8; second++)
            {
                clock.Advance(1);
                simulation.Update();

                var segment = simulation.GroundTraffic.CurrentSegment;
                if (segment.Equals(AirportTaxiNetwork.AlphaOne) || segment.Equals(AirportTaxiNetwork.AlphaTwo))
                    groundTrafficUsedAlpha = true;
            }

            Assert.That(groundTrafficUsedAlpha, Is.True, "ground traffic should occupy the shared segments");
            Assert.That(simulation.ReservationConflicts, Is.Zero, "the arriving flight must never be blocked");
            Assert.That(simulation.CompletedCycles, Is.EqualTo(8));
        }

        [Test]
        public void SecondAircraft_MovesIdenticallyUnderLargeAndSmallTimeSteps()
        {
            var smallClock = new ManualSimulationClock(new SimulationTime(0));
            var small = new AirportSimulation(smallClock, new SeededRandomSource(99), new ReservationTable());
            for (var second = 1; second <= 1234; second++)
            {
                smallClock.Advance(1);
                small.Update();
            }

            var largeClock = new ManualSimulationClock(new SimulationTime(0));
            var large = new AirportSimulation(largeClock, new SeededRandomSource(99), new ReservationTable());
            largeClock.Advance(1234);
            large.Update();

            Assert.That(large.GroundTraffic.CurrentPhase, Is.EqualTo(small.GroundTraffic.CurrentPhase));
            Assert.That(large.GroundTraffic.CurrentSegment, Is.EqualTo(small.GroundTraffic.CurrentSegment));
            Assert.That(large.GroundTraffic.Progress, Is.EqualTo(small.GroundTraffic.Progress).Within(0.0001));
            Assert.That(large.GroundTraffic.IsHolding, Is.EqualTo(small.GroundTraffic.IsHolding));
        }

        [Test]
        public void SecondAircraft_RunsAnArrivalStandAndDepartureSchedule()
        {
            var table = new ReservationTable();
            var monitor = new TrafficWaitMonitor();
            var groundTraffic = new GroundTrafficAircraft(table);
            // Primary flight is on Stand 1, so the second aircraft should take Stand 2.
            var primaryStand = AirportSimulation.StandOne;

            var reachedStand = false;
            var releasedStandOnDeparture = false;
            var departed = false;
            for (long second = 1; second <= 150; second++)
            {
                groundTraffic.Reposition(new SimulationTime(second), monitor, primaryStand);
                if (groundTraffic.IsAtStand)
                {
                    reachedStand = true;
                    Assert.That(groundTraffic.TargetStand, Is.EqualTo(AirportSimulation.StandTwo));
                    Assert.That(table.IsReserved(AirportSimulation.StandTwo), Is.True);
                }

                if (reachedStand && !groundTraffic.IsAtStand && !table.IsReserved(AirportSimulation.StandTwo))
                    releasedStandOnDeparture = true;

                if (reachedStand && groundTraffic.CurrentPhase == "Away")
                    departed = true;
            }

            Assert.That(reachedStand, Is.True, "the second aircraft should park on a stand");
            Assert.That(releasedStandOnDeparture, Is.True, "and release the stand as it taxis out");
            Assert.That(departed, Is.True, "and then depart before repeating the schedule");
        }

        [Test]
        public void SecondAircraft_ChoosesItsStandFromThePrimaryFlightAssignment()
        {
            // At the moment a fresh arrival begins, it must target the other stand.
            var toStandTwo = new GroundTrafficAircraft(new ReservationTable());
            toStandTwo.Reposition(new SimulationTime(1), new TrafficWaitMonitor(), AirportSimulation.StandOne);
            Assert.That(toStandTwo.TargetStand, Is.EqualTo(AirportSimulation.StandTwo));

            var toStandOne = new GroundTrafficAircraft(new ReservationTable());
            toStandOne.Reposition(new SimulationTime(1), new TrafficWaitMonitor(), AirportSimulation.StandTwo);
            Assert.That(toStandOne.TargetStand, Is.EqualTo(AirportSimulation.StandOne));
        }

        [Test]
        public void SecondAircraft_AdaptsAcrossManyCyclesWithoutBlockingOrDeadlock()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var simulation = new AirportSimulation(clock, new SeededRandomSource(7), new ReservationTable());

            var standsVisited = new System.Collections.Generic.HashSet<string>();
            for (var second = 1; second <= 6000 && simulation.CompletedCycles < 25; second++)
            {
                clock.Advance(1);
                simulation.Update();
                if (simulation.GroundTraffic.IsAtStand)
                    standsVisited.Add(simulation.GroundTraffic.TargetStand.Value);
            }

            Assert.That(simulation.CompletedCycles, Is.EqualTo(25), "the primary flight keeps cycling");
            Assert.That(simulation.ReservationConflicts, Is.Zero, "and is never blocked by the second aircraft");
            Assert.That(standsVisited.Count, Is.EqualTo(2), "the second aircraft uses both stands as the primary's assignment changes");
        }

        [Test]
        public void SecondAircraft_ProlongedYieldIsExplainedByTheTrafficMonitor()
        {
            var table = new ReservationTable();
            var monitor = new TrafficWaitMonitor();
            var groundTraffic = new GroundTrafficAircraft(table);

            // An arriving flight holds A1 while the second aircraft wants to taxi in on it.
            Assert.That(table.TryReplace(new StableId("AS-101"), new[] { AirportTaxiNetwork.AlphaOne }, out _), Is.True);
            for (long second = 1; second <= 45; second++)
                groundTraffic.Reposition(new SimulationTime(second), monitor, AirportSimulation.StandOne);

            Assert.That(groundTraffic.IsHolding, Is.True);
            Assert.That(groundTraffic.DesiredSegment, Is.EqualTo(AirportTaxiNetwork.AlphaOne));
            Assert.That(monitor.HasWarning(new SimulationTime(45)), Is.True);
            Assert.That(monitor.Describe(new SimulationTime(45)), Does.Contain(GroundTrafficAircraft.Id.Value));

            table.Release(new StableId("AS-101"));
            groundTraffic.Reposition(new SimulationTime(46), monitor, AirportSimulation.StandOne);
            Assert.That(groundTraffic.CurrentSegment, Is.EqualTo(AirportTaxiNetwork.AlphaOne));
            Assert.That(groundTraffic.IsHolding, Is.False);
        }

        [Test]
        public void TrafficWaitMonitor_ExplainsAProlongedResourceWait()
        {
            var table = new ReservationTable();
            var first = new StableId("AS-101");
            var second = new StableId("AS-102");
            var monitor = new TrafficWaitMonitor();
            Assert.That(table.TryReplace(first, new[] { AirportTaxiNetwork.AlphaOne }, out _), Is.True);
            Assert.That(table.TryReplace(second, new[] { AirportTaxiNetwork.AlphaOne }, out var blocked), Is.False);
            monitor.SetWaiting(second, blocked, new SimulationTime(20));

            Assert.That(monitor.HasWarning(new SimulationTime(29)), Is.False);
            Assert.That(monitor.HasWarning(new SimulationTime(30)), Is.True);
            Assert.That(monitor.Describe(new SimulationTime(30)), Is.EqualTo("AS-102 waiting 10s for TAXI-A1"));

            table.Release(first);
            Assert.That(table.TryReplace(second, new[] { AirportTaxiNetwork.AlphaOne }, out _), Is.True);
            monitor.Clear(second);
            Assert.That(monitor.HasWarning(new SimulationTime(31)), Is.False);
        }
    }
}

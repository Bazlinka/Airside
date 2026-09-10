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
            // CompletedCycles increments on Departed; the 50th departure is still AS-150
            // until the departure-reset window respawns AS-151.
            Assert.That(simulation.ActiveAircraft.AircraftId, Is.EqualTo("AS-150"));
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
            var standThree = network.RouteTo(AirportSimulation.StandThree);

            Assert.That(standOne.SegmentIds[0], Is.EqualTo(AirportTaxiNetwork.AlphaOne));
            Assert.That(standOne.SegmentIds[1], Is.EqualTo(AirportTaxiNetwork.AlphaTwo));
            Assert.That(standOne.SegmentIds[2], Is.EqualTo(AirportTaxiNetwork.ApronThroat));
            Assert.That(standOne.SegmentIds[3], Is.EqualTo(AirportTaxiNetwork.StandOneLeadIn));
            Assert.That(standTwo.SegmentIds[2], Is.EqualTo(AirportTaxiNetwork.ApronThroat));
            Assert.That(standTwo.SegmentIds[3], Is.EqualTo(AirportTaxiNetwork.StandTwoLeadIn));
            Assert.That(standThree.SegmentIds[2], Is.EqualTo(AirportTaxiNetwork.ApronThroat));
            Assert.That(standThree.SegmentIds[3], Is.EqualTo(AirportTaxiNetwork.StandThreeLeadIn));
            Assert.That(standOne.Points.Count, Is.EqualTo(standOne.SegmentIds.Count + 1));
            Assert.That(standThree.Points[standThree.Points.Count - 1].Z, Is.EqualTo(34f));
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

            var sawA1 = false;
            var sawA2 = false;
            var sawThroat = false;
            var sawLead = false;
            StableId previous = default;
            for (var second = 1; second <= 80; second++)
            {
                clock.Advance(1);
                simulation.Update();
                if (simulation.ActiveAircraft.Phase != AircraftPhase.TaxiIn)
                    continue;

                var segment = simulation.CurrentTaxiSegment;
                if (segment.Equals(AirportTaxiNetwork.AlphaOne)) sawA1 = true;
                if (segment.Equals(AirportTaxiNetwork.AlphaTwo)) sawA2 = true;
                if (segment.Equals(AirportTaxiNetwork.ApronThroat)) sawThroat = true;
                if (segment.Equals(AirportTaxiNetwork.StandOneLeadIn)
                    || segment.Equals(AirportTaxiNetwork.StandTwoLeadIn)
                    || segment.Equals(AirportTaxiNetwork.StandThreeLeadIn))
                    sawLead = true;

                if (!previous.Equals(default(StableId))
                    && !previous.Equals(segment)
                    && !segment.Equals(default(StableId)))
                {
                    // Apron throat is intentionally co-held with the stand lead-in.
                    if (!(previous.Equals(AirportTaxiNetwork.ApronThroat)
                        && (segment.Equals(AirportTaxiNetwork.StandOneLeadIn)
                            || segment.Equals(AirportTaxiNetwork.StandTwoLeadIn)
                            || segment.Equals(AirportTaxiNetwork.StandThreeLeadIn))))
                    {
                        Assert.That(FlightOwns(simulation, previous), Is.False,
                            $"previous segment {previous.Value} should release before {segment.Value}");
                    }
                }

                if (!segment.Equals(default(StableId)))
                    previous = segment;
            }

            Assert.That(sawA1 && sawA2 && sawThroat && sawLead, Is.True,
                "taxi-in should visit A1, A2, throat and lead-in in order");
        }

        [Test]
        public void ArrivingFlight_KeepsTheRunwayUntilItIsPastTheHoldingPosition()
        {
            // The runway used to be released the instant the rollout ended, while the
            // aircraft was still on the centreline, so a waiting departure could be
            // cleared and start its roll straight through it.
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var simulation = new AirportSimulation(clock, new SeededRandomSource(42), new ReservationTable());

            var sawHeldWhileTaxiing = false;
            var releasedBeforeTheLine = false;
            for (var second = 1; second <= 400; second++)
            {
                clock.Advance(1);
                simulation.Update();

                var flight = simulation.Flights[0];
                if (flight.Operation.Phase != AircraftPhase.TaxiIn)
                    continue;

                var ownsRunway = FlightOwns(simulation, AirportSimulation.Runway);
                if (flight.HasVacatedRunway(clock.Now))
                {
                    if (ownsRunway)
                        Assert.Fail("the runway is still held after the holding position");
                }
                else if (ownsRunway)
                {
                    sawHeldWhileTaxiing = true;
                }
                else
                {
                    releasedBeforeTheLine = true;
                }
            }

            Assert.That(sawHeldWhileTaxiing, Is.True, "runway not held while inside the strip");
            Assert.That(releasedBeforeTheLine, Is.False, "runway released before the holding position");
        }

        [Test]
        public void RunwayHoldingPosition_IsClearOfTheRunwayStripOnEveryStandRoute()
        {
            var network = new AirportTaxiNetwork();
            foreach (var stand in new[]
                     {
                         AirportSimulation.StandOne, AirportSimulation.StandTwo, AirportSimulation.StandThree
                     })
            {
                var route = network.RouteTo(stand);
                var progress = AirportTaxiNetwork.RunwayHoldingProgress(route);

                Assert.That(progress, Is.GreaterThan(0f), $"{stand.Value} holding line is at the runway");
                Assert.That(progress, Is.LessThan(1f), $"{stand.Value} holding line is past the stand");
                // It must land on the first segment, the one that leaves the runway.
                Assert.That(route.ForwardSegmentIndex(progress), Is.Zero, $"{stand.Value} holding line left A1");
            }
        }

        [Test]
        public void TwoFlights_NeverBothHoldTheRunway()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var simulation = new AirportSimulation(clock, new SeededRandomSource(24031996), new ReservationTable());

            var sawTwoFlights = false;
            for (var second = 1; second <= 20000 && simulation.CompletedCycles < 40; second++)
            {
                clock.Advance(1);
                simulation.Update();
                // Accepting routes is what unlocks the second concurrent flight.
                if (simulation.Routes.Pending != null)
                    simulation.AcceptPendingRoute();
                sawTwoFlights |= simulation.Flights.Count >= 2;

                var onRunway = 0;
                for (var i = 0; i < simulation.Flights.Count; i++)
                {
                    var flight = simulation.Flights[i];
                    var phase = flight.Operation.Phase;
                    var occupying = phase == AircraftPhase.Landing
                                    || phase == AircraftPhase.Takeoff
                                    || (phase == AircraftPhase.TaxiIn && !flight.HasVacatedRunway(clock.Now));
                    if (occupying)
                        onRunway++;
                }

                Assert.That(onRunway, Is.LessThanOrEqualTo(1),
                    $"{onRunway} aircraft on the runway at second {second}");
            }

            Assert.That(sawTwoFlights, Is.True, "never reached two concurrent flights");
            Assert.That(simulation.ReservationConflicts, Is.Zero);
            Assert.That(simulation.CompletedCycles, Is.EqualTo(40), "the loop deadlocked");
        }

        private static bool FlightOwns(AirportSimulation simulation, StableId segment)
        {
            return simulation.Reservations.TryGetOwner(segment, out var owner) &&
                   owner.Value == simulation.ActiveAircraft.AircraftId;
        }

        private static GroundTrafficAircraft NewArrival(ReservationTable table, long startDelay = 0)
        {
            return new GroundTrafficAircraft(new StableId("GT-201"), table, GroundTrafficRole.ArriveDepart, startDelay);
        }

        [Test]
        public void GroundTraffic_SharesTheTaxiwayWithoutBlockingTheArrivingFlight()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var simulation = new AirportSimulation(clock, new SeededRandomSource(42), new ReservationTable());

            var groundTrafficUsedAlpha = false;
            for (var second = 1; second <= 2000 && simulation.CompletedCycles < 8; second++)
            {
                clock.Advance(1);
                simulation.Update();

                foreach (var aircraft in simulation.GroundTraffic)
                {
                    var segment = aircraft.CurrentSegment;
                    if (segment.Equals(AirportTaxiNetwork.AlphaOne) || segment.Equals(AirportTaxiNetwork.AlphaTwo))
                        groundTrafficUsedAlpha = true;
                }
            }

            Assert.That(groundTrafficUsedAlpha, Is.True, "ground traffic should occupy the shared segments");
            Assert.That(simulation.ReservationConflicts, Is.Zero, "the arriving flight must never be blocked");
            Assert.That(simulation.CompletedCycles, Is.EqualTo(8));
        }

        [Test]
        public void GroundTraffic_MovesIdenticallyUnderLargeAndSmallTimeSteps()
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

            for (var index = 0; index < small.GroundTraffic.Count; index++)
            {
                Assert.That(large.GroundTraffic[index].CurrentPhase, Is.EqualTo(small.GroundTraffic[index].CurrentPhase));
                Assert.That(large.GroundTraffic[index].CurrentSegment, Is.EqualTo(small.GroundTraffic[index].CurrentSegment));
                Assert.That(large.GroundTraffic[index].Progress, Is.EqualTo(small.GroundTraffic[index].Progress).Within(0.0001));
                Assert.That(large.GroundTraffic[index].IsHolding, Is.EqualTo(small.GroundTraffic[index].IsHolding));
            }
        }

        [Test]
        public void GroundTraffic_RunsAnArrivalStandAndDepartureSchedule()
        {
            var table = new ReservationTable();
            var monitor = new TrafficWaitMonitor();
            var groundTraffic = NewArrival(table);
            // Primary flight is on Stand 1, so this aircraft should take Stand 2.
            var primaryStand = AirportSimulation.StandOne;

            var reachedStand = false;
            var releasedStandOnDeparture = false;
            var departed = false;
            for (long second = 1; second <= 150; second++)
            {
                groundTraffic.Reposition(new SimulationTime(second), monitor, primaryStand, AirportCapacity.BaselineStands, true);
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

            Assert.That(reachedStand, Is.True, "the aircraft should park on a stand");
            Assert.That(releasedStandOnDeparture, Is.True, "and release the stand as it taxis out");
            Assert.That(departed, Is.True, "and then depart before repeating the schedule");
        }

        [Test]
        public void GroundTraffic_ChoosesItsStandFromThePrimaryFlightAssignment()
        {
            var toStandTwo = NewArrival(new ReservationTable());
            toStandTwo.Reposition(new SimulationTime(1), new TrafficWaitMonitor(), AirportSimulation.StandOne, AirportCapacity.BaselineStands, true);
            Assert.That(toStandTwo.TargetStand, Is.EqualTo(AirportSimulation.StandTwo));

            var toStandOne = NewArrival(new ReservationTable());
            toStandOne.Reposition(new SimulationTime(1), new TrafficWaitMonitor(), AirportSimulation.StandTwo, AirportCapacity.BaselineStands, true);
            Assert.That(toStandOne.TargetStand, Is.EqualTo(AirportSimulation.StandOne));
        }

        [Test]
        public void GroundTraffic_WhenStandsOneAndTwoAreBusy_UsesStandThreeLeadInAndPosition()
        {
            var table = new ReservationTable();
            var monitor = new TrafficWaitMonitor();
            var groundTraffic = NewArrival(table);

            // Both baseline stands occupied by commercial traffic and Stand 3 built
            // → fleet takes Stand 3.
            var busy = new[] { AirportSimulation.StandOne, AirportSimulation.StandTwo };
            groundTraffic.Reposition(new SimulationTime(1), monitor, busy, AirportCapacity.MaximumStands, true);
            Assert.That(groundTraffic.TargetStand, Is.EqualTo(AirportSimulation.StandThree));

            var usedStandThreeLeadIn = false;
            var parkedOnStandThree = false;
            for (long second = 2; second <= 400; second++)
            {
                groundTraffic.Reposition(new SimulationTime(second), monitor, busy, AirportCapacity.MaximumStands, true);
                if (table.TryGetOwner(AirportTaxiNetwork.StandThreeLeadIn, out var leadOwner) &&
                    leadOwner.Equals(groundTraffic.Id))
                    usedStandThreeLeadIn = true;
                if (groundTraffic.IsAtStand && groundTraffic.TargetStand.Equals(AirportSimulation.StandThree))
                {
                    parkedOnStandThree = true;
                    Assert.That(groundTraffic.Position.Z, Is.EqualTo(AirportTaxiNetwork.StandZ(AirportSimulation.StandThree)).Within(0.01f));
                    break;
                }
            }

            Assert.That(usedStandThreeLeadIn, Is.True, "ground traffic should reserve the Stand 3 lead-in");
            Assert.That(parkedOnStandThree, Is.True, "and park on Stand 3 at the correct apron Z");
        }

        [Test]
        public void GroundTraffic_WithEveryBuiltStandBusy_HoldsInsteadOfUsingAnUnbuiltStand()
        {
            var table = new ReservationTable();
            var monitor = new TrafficWaitMonitor();
            var groundTraffic = NewArrival(table);

            // Two commercials fill the only two stands the airport has built. Stand 3
            // does not exist yet, so the fleet must wait rather than taxi to it.
            var busy = new[] { AirportSimulation.StandOne, AirportSimulation.StandTwo };
            for (long second = 1; second <= 400; second++)
            {
                groundTraffic.Reposition(new SimulationTime(second), monitor, busy, AirportCapacity.BaselineStands, true);

                Assert.That(groundTraffic.TargetStand, Is.Not.EqualTo(AirportSimulation.StandThree),
                    "the fleet must not target a stand the airport has not built");
                Assert.That(table.IsReserved(AirportSimulation.StandThree), Is.False);
                Assert.That(table.IsReserved(AirportTaxiNetwork.StandThreeLeadIn), Is.False);
                Assert.That(groundTraffic.IsHolding, Is.True);
                // Holding off-field must leave the shared corridor free for the rest of the fleet.
                Assert.That(table.IsReserved(AirportTaxiNetwork.Corridor), Is.False);
            }
        }

        [Test]
        public void GroundTraffic_NeverTouchesStandThree_BeforeItIsBuilt()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var simulation = new AirportSimulation(clock, new SeededRandomSource(11), new ReservationTable());

            var sawTwoCommercials = false;
            for (var second = 1; second <= 6000; second++)
            {
                clock.Advance(1);
                simulation.Update();
                if (simulation.Routes.Pending != null)
                    simulation.AcceptPendingRoute();

                sawTwoCommercials |= simulation.Flights.Count > 1;

                Assert.That(simulation.Capacity.HasThirdStand, Is.False);
                Assert.That(simulation.Reservations.IsReserved(AirportSimulation.StandThree), Is.False,
                    "Stand 3 is not built, so nothing may reserve it");
                Assert.That(simulation.Reservations.IsReserved(AirportTaxiNetwork.StandThreeLeadIn), Is.False);
                foreach (var traffic in simulation.GroundTraffic)
                    Assert.That(traffic.TargetStand, Is.Not.EqualTo(AirportSimulation.StandThree));
            }

            Assert.That(sawTwoCommercials, Is.True,
                "the run needs both stands occupied by commercials to exercise the case");
        }

        [Test]
        public void GroundTraffic_AdaptsAcrossManyCyclesWithoutBlockingOrDeadlock()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var simulation = new AirportSimulation(clock, new SeededRandomSource(7), new ReservationTable());

            var standsVisited = new System.Collections.Generic.HashSet<string>();
            for (var second = 1; second <= 8000 && simulation.CompletedCycles < 30; second++)
            {
                clock.Advance(1);
                simulation.Update();
                foreach (var aircraft in simulation.GroundTraffic)
                {
                    if (aircraft.IsAtStand)
                        standsVisited.Add(aircraft.TargetStand.Value);
                }
            }

            Assert.That(simulation.CompletedCycles, Is.EqualTo(30), "the primary flight keeps cycling");
            Assert.That(simulation.ReservationConflicts, Is.Zero, "and is never blocked by ground traffic");
            Assert.That(standsVisited.Count, Is.EqualTo(2), "the arriving aircraft uses both stands as the primary's assignment changes");
        }

        [Test]
        public void GroundTraffic_ProlongedYieldIsExplainedByTheTrafficMonitor()
        {
            var table = new ReservationTable();
            var monitor = new TrafficWaitMonitor();
            var groundTraffic = NewArrival(table);

            // An arriving flight holds A1 while this aircraft wants to taxi in on it.
            Assert.That(table.TryReplace(new StableId("AS-101"), new[] { AirportTaxiNetwork.AlphaOne }, out _), Is.True);
            for (long second = 1; second <= 45; second++)
                groundTraffic.Reposition(new SimulationTime(second), monitor, AirportSimulation.StandOne, AirportCapacity.BaselineStands, true);

            Assert.That(groundTraffic.IsHolding, Is.True);
            Assert.That(groundTraffic.DesiredSegment, Is.EqualTo(AirportTaxiNetwork.AlphaOne));
            Assert.That(monitor.HasWarning(new SimulationTime(45)), Is.True);
            Assert.That(monitor.Describe(new SimulationTime(45)), Does.Contain("GT-201"));
            Assert.That(monitor.Describe(new SimulationTime(45)), Does.Contain(AirportTaxiNetwork.AlphaOne.Value));

            table.Release(new StableId("AS-101"));
            groundTraffic.Reposition(new SimulationTime(46), monitor, AirportSimulation.StandOne, AirportCapacity.BaselineStands, true);
            Assert.That(groundTraffic.CurrentSegment, Is.EqualTo(AirportTaxiNetwork.AlphaOne));
            Assert.That(groundTraffic.OnCorridor, Is.True);
            Assert.That(groundTraffic.IsHolding, Is.False);
        }

        [Test]
        public void GroundTrafficFleet_NeverPutsTwoAircraftOnTheCorridorAtOnce()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var simulation = new AirportSimulation(clock, new SeededRandomSource(24031996), new ReservationTable());

            var sawRepositioningAircraftMove = false;
            for (var second = 1; second <= 6000 && simulation.CompletedCycles < 25; second++)
            {
                clock.Advance(1);
                simulation.Update();

                var onCorridor = 0;
                foreach (var aircraft in simulation.GroundTraffic)
                {
                    if (aircraft.OnCorridor)
                        onCorridor++;
                    if (aircraft.Role == GroundTrafficRole.Reposition && aircraft.OnCorridor)
                        sawRepositioningAircraftMove = true;
                }

                Assert.That(onCorridor, Is.LessThanOrEqualTo(1), $"two aircraft on the corridor at T+{second}s");
            }

            Assert.That(simulation.CompletedCycles, Is.EqualTo(25));
            Assert.That(simulation.ReservationConflicts, Is.Zero);
            Assert.That(sawRepositioningAircraftMove, Is.True, "the repositioning aircraft should get its turn on the corridor");
        }

        [Test]
        public void GroundTraffic_HoldsShortWhenNotGrantedTheCorridor()
        {
            var table = new ReservationTable();
            var monitor = new TrafficWaitMonitor();
            var aircraft = NewArrival(table);

            // Corridor is free, but the fleet has not granted it to this aircraft.
            for (long second = 1; second <= 20; second++)
                aircraft.Reposition(new SimulationTime(second), monitor, AirportSimulation.StandOne, AirportCapacity.BaselineStands, mayEnterCorridor: false);

            Assert.That(aircraft.OnCorridor, Is.False);
            Assert.That(aircraft.IsHolding, Is.True);
            Assert.That(aircraft.WantsCorridorNow, Is.True);
            Assert.That(monitor.TryGetWaitStart(new StableId("GT-201"), out var since), Is.True);
            Assert.That(since.ElapsedSeconds, Is.EqualTo(1), "the wait is timed from when it first wanted the corridor");

            // Once granted, it enters.
            aircraft.Reposition(new SimulationTime(21), monitor, AirportSimulation.StandOne, AirportCapacity.BaselineStands, mayEnterCorridor: true);
            Assert.That(aircraft.OnCorridor, Is.True);
            Assert.That(aircraft.IsHolding, Is.False);
        }

        [Test]
        public void GroundTrafficFleet_ServesEveryAircraftWithoutStarvationOverALongRun()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var simulation = new AirportSimulation(clock, new SeededRandomSource(24031996), new ReservationTable());

            var circuitsCompleted = new System.Collections.Generic.Dictionary<string, int>();
            var wasAway = new System.Collections.Generic.Dictionary<string, bool>();
            foreach (var aircraft in simulation.GroundTraffic)
            {
                circuitsCompleted[aircraft.Id.Value] = 0;
                wasAway[aircraft.Id.Value] = false;
            }

            for (var second = 1; second <= 12000 && simulation.CompletedCycles < 40; second++)
            {
                clock.Advance(1);
                simulation.Update();

                foreach (var aircraft in simulation.GroundTraffic)
                {
                    var away = aircraft.CurrentPhase == "Away";
                    if (away && !wasAway[aircraft.Id.Value])
                        circuitsCompleted[aircraft.Id.Value]++;
                    wasAway[aircraft.Id.Value] = away;
                }
            }

            Assert.That(simulation.CompletedCycles, Is.EqualTo(40), "the primary flight is never blocked");
            Assert.That(simulation.ReservationConflicts, Is.Zero);
            foreach (var pair in circuitsCompleted)
                Assert.That(pair.Value, Is.GreaterThanOrEqualTo(3),
                    $"{pair.Key} only completed {pair.Value} circuits — starved for the corridor");
        }

        [Test]
        public void RepositioningAircraft_TransitsTheCorridorWithoutUsingAStand()
        {
            var table = new ReservationTable();
            var monitor = new TrafficWaitMonitor();
            var repositioning = new GroundTrafficAircraft(
                new StableId("GT-202"), table, GroundTrafficRole.Reposition, 0);

            var everParked = false;
            var reachedRunUp = false;
            var departed = false;
            for (long second = 1; second <= 130; second++)
            {
                repositioning.Reposition(new SimulationTime(second), monitor, AirportSimulation.StandOne, AirportCapacity.BaselineStands, true);
                everParked |= repositioning.IsAtStand;
                reachedRunUp |= repositioning.CurrentPhase == "Run-up hold";
                departed |= repositioning.CurrentPhase == "Away";
            }

            Assert.That(everParked, Is.False, "a repositioning aircraft never parks on a stand");
            Assert.That(repositioning.TargetStand.Value, Is.Null);
            Assert.That(reachedRunUp, Is.True);
            Assert.That(departed, Is.True);
            Assert.That(table.IsReserved(AirportSimulation.StandOne), Is.False);
            Assert.That(table.IsReserved(AirportSimulation.StandTwo), Is.False);
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

        [Test]
        public void TrafficWaitMonitor_UpdatesBlockedResourceWithoutResettingWaitStart()
        {
            var monitor = new TrafficWaitMonitor();
            var aircraft = new StableId("AS-101");
            monitor.SetWaiting(aircraft, AirportTaxiNetwork.AlphaOne, new SimulationTime(10));
            monitor.SetWaiting(aircraft, AirportTaxiNetwork.AlphaTwo, new SimulationTime(15));

            Assert.That(monitor.Describe(new SimulationTime(20)), Is.EqualTo("AS-101 waiting 10s for TAXI-A2"));
        }

        [Test]
        public void DepartedFlight_ReleasesRunwayDuringResetWindow()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var simulation = new AirportSimulation(clock, new SeededRandomSource(7), new ReservationTable());

            for (var second = 1; second <= 200; second++)
            {
                clock.Advance(1);
                simulation.Update();
                foreach (var flight in simulation.Flights)
                {
                    if (!flight.Operation.IsComplete)
                        continue;
                    Assert.That(simulation.Reservations.TryGetOwner(AirportSimulation.Runway, out var owner)
                                && owner.Equals(flight.OwnerId),
                        Is.False,
                        $"departed {flight.AircraftId} still held the runway at t={clock.Now.ElapsedSeconds}");
                }
            }
        }
    }
}

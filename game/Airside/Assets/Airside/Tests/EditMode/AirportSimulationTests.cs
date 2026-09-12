using Airside.Domain;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class AirportSimulationTests
    {
        [SetUp]
        public void SetUp() => TaxiLoopFixture.EnableFullTaxiLoop();

        [TearDown]
        public void TearDown() => TaxiLoopFixture.RestoreCircuit();

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
            var standOne = network.RoutesTo(AirportSimulation.StandOne);
            var standTwo = network.RoutesTo(AirportSimulation.StandTwo);
            var standThree = network.RoutesTo(AirportSimulation.StandThree);

            Assert.That(standOne.Arrival.SegmentIds[0], Is.EqualTo(AirportTaxiNetwork.BravoExit));
            Assert.That(standOne.Arrival.SegmentIds[1], Is.EqualTo(AirportTaxiNetwork.BravoOne));
            Assert.That(standOne.Arrival.SegmentIds[2], Is.EqualTo(AirportTaxiNetwork.ApronThroat));
            Assert.That(standOne.Arrival.SegmentIds[3], Is.EqualTo(AirportTaxiNetwork.StandOneLeadIn));
            Assert.That(standOne.Departure.SegmentIds[0], Is.EqualTo(AirportTaxiNetwork.AlphaOne));
            Assert.That(standOne.Departure.SegmentIds[1], Is.EqualTo(AirportTaxiNetwork.AlphaTwo));
            Assert.That(standTwo.Arrival.SegmentIds[2], Is.EqualTo(AirportTaxiNetwork.ApronThroat));
            Assert.That(standTwo.Arrival.SegmentIds[3], Is.EqualTo(AirportTaxiNetwork.StandTwoLeadIn));
            Assert.That(standThree.Arrival.SegmentIds[2], Is.EqualTo(AirportTaxiNetwork.ApronThroat));
            Assert.That(standThree.Arrival.SegmentIds[3], Is.EqualTo(AirportTaxiNetwork.StandThreeLeadIn));
            Assert.That(standOne.Arrival.Points.Count, Is.EqualTo(standOne.Arrival.SegmentIds.Count + 1));
            Assert.That(standThree.Arrival.Points[standThree.Arrival.Points.Count - 1].Z, Is.EqualTo(34f));
        }

        [Test]
        public void Taxiing_ReleasesEachSegmentBeforeReservingTheNext()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var simulation = new AirportSimulation(clock, new SeededRandomSource(24031996), new ReservationTable());

            var sawBravoExit = false;
            var sawBravoOne = false;
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
                if (segment.Equals(AirportTaxiNetwork.BravoExit)) sawBravoExit = true;
                if (segment.Equals(AirportTaxiNetwork.BravoOne)) sawBravoOne = true;
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

            Assert.That(sawBravoExit && sawBravoOne && sawThroat && sawLead, Is.True,
                "taxi-in should visit the B exit, B1, throat and lead-in in order");
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
        public void FutureTaxiIn_ReservesRunwayBeforeEnteringThePhase()
        {
            var now = new SimulationTime(0);
            var flight = new CommercialFlight("TEST", now, AirportSimulation.StandOne,
                new AirportTaxiNetwork().RoutesTo(AirportSimulation.StandOne));
            Assert.That(flight.ResourcesForPhase(AircraftPhase.TaxiIn, now),
                Does.Contain(AirportSimulation.Runway));
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
                var route = network.RoutesTo(stand).Departure;
                var progress = AirportTaxiNetwork.RunwayHoldingProgress(route);

                Assert.That(progress, Is.GreaterThan(0f), $"{stand.Value} holding line is at the runway");
                Assert.That(progress, Is.LessThan(1f), $"{stand.Value} holding line is past the stand");
                // It must land on the first segment, the A1 chord that leaves the runway.
                Assert.That(route.ForwardSegmentIndex(progress), Is.Zero, $"{stand.Value} holding line left A1");
            }
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

        private static bool FlightOwns(AirportSimulation simulation, StableId segment)
        {
            return simulation.Reservations.TryGetOwner(segment, out var owner) &&
                   owner.Value == simulation.ActiveAircraft.AircraftId;
        }
    }
}

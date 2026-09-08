using Airside.Domain;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class ConcurrentFlightsTests
    {
        [Test]
        public void BelowThreshold_KeepsASingleCommercialFlight()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var simulation = new AirportSimulation(clock, new SeededRandomSource(42), new ReservationTable());

            for (var second = 1; second <= 2000; second++)
            {
                clock.Advance(1);
                simulation.Update();
                if (simulation.Routes.Pending != null)
                    simulation.DeclinePendingRoute();
            }

            Assert.That(simulation.Routes.ScheduledFlightsPerDay, Is.EqualTo(0));
            Assert.That(simulation.Flights.Count, Is.EqualTo(1));
            Assert.That(simulation.ActiveAircraft.AircraftId, Is.EqualTo("AS-113"));
        }

        [Test]
        public void AtOrAboveThreshold_SpawnsASecondCommercialOnHalfCycleStagger()
        {
            var simulation = RunUntilDual(out _);

            Assert.That(simulation.Routes.ScheduledFlightsPerDay,
                Is.GreaterThanOrEqualTo(AirportSimulation.SecondFlightThreshold));
            Assert.That(simulation.Flights.Count, Is.EqualTo(2));

            var primary = simulation.Flights[0];
            var secondary = simulation.Flights[1];
            var earliest = primary.CycleStartedAt.ElapsedSeconds + AirportSimulation.CycleLengthSeconds / 2;
            Assert.That(secondary.SpawnedAt.ElapsedSeconds, Is.GreaterThanOrEqualTo(earliest));
            Assert.That(secondary.AssignedStand.Equals(primary.AssignedStand), Is.False);
        }

        [Test]
        public void DualFlightSoak_CompletesFiftyCyclesWithoutReservationConflicts()
        {
            var simulation = RunUntilDual(out var clock);

            for (var second = 1; second <= 20000 && simulation.CompletedCycles < 50; second++)
            {
                clock.Advance(1);
                simulation.Update();
                if (simulation.Routes.Pending != null)
                    simulation.DeclinePendingRoute();
                Assert.That(simulation.Flights.Count,
                    Is.LessThanOrEqualTo(AirportSimulation.MaxConcurrentCommercialFlights));
            }

            Assert.That(simulation.CompletedCycles, Is.EqualTo(50));
            Assert.That(simulation.ReservationConflicts, Is.Zero);
        }

        [Test]
        public void DualFlight_NeverSharesATaxiSegment()
        {
            var simulation = RunUntilDual(out var clock);
            for (var second = 1; second <= 15000; second++)
            {
                clock.Advance(1);
                simulation.Update();
                if (simulation.Routes.Pending != null)
                    simulation.DeclinePendingRoute();

                StableId? occupied = null;
                foreach (var flight in simulation.Flights)
                {
                    if (flight.Operation.IsComplete)
                        continue;
                    var segment = flight.SegmentFor(clock.Now);
                    if (segment.Equals(default(StableId)))
                        continue;
                    if (occupied.HasValue && occupied.Value.Equals(segment))
                        Assert.Fail($"{flight.AircraftId} shares {segment.Value} at t={clock.Now.ElapsedSeconds}");
                    occupied = segment;
                }
            }
        }

        [Test]
        public void PriorityCrew_AppliesToSecondaryWhenPrimaryIsNotAtStand()
        {
            var simulation = RunUntilDual(out var clock);
            for (var second = 1; second <= 8000; second++)
            {
                clock.Advance(1);
                simulation.Update();
                if (simulation.Routes.Pending != null)
                    simulation.DeclinePendingRoute();

                var primary = simulation.Flights[0];
                CommercialFlight atStand = null;
                foreach (var flight in simulation.Flights)
                {
                    if (flight.Operation.Phase == AircraftPhase.AtStand && flight.Turnaround != null)
                        atStand = flight;
                }

                if (atStand == null || primary.Operation.Phase == AircraftPhase.AtStand)
                    continue;
                if (ReferenceEquals(atStand, primary))
                    continue;

                var cashBefore = simulation.Economy.Cash;
                Assert.That(simulation.EnablePriorityCrew(), Is.True,
                    $"P should target {atStand.AircraftId} at stand while primary is {primary.Operation.Phase}");
                Assert.That(atStand.Turnaround.PriorityCrewEnabled, Is.True);
                Assert.That(simulation.Economy.Cash, Is.EqualTo(cashBefore - AirportEconomy.PriorityCrewCost));
                return;
            }

            Assert.Fail("expected a tick where primary was not at stand but another commercial was");
        }

        [Test]
        public void DualFlight_LargeAndSmallTimeSteps_Match()
        {
            var small = RunDualComparable(4217, step: 1);
            var large = RunDualComparable(4217, step: 400);

            Assert.That(large.CompletedCycles, Is.EqualTo(small.CompletedCycles));
            Assert.That(large.Flights.Count, Is.EqualTo(small.Flights.Count));
            Assert.That(large.Economy.Cash, Is.EqualTo(small.Economy.Cash));
            Assert.That(large.Reputation.Score, Is.EqualTo(small.Reputation.Score));
            Assert.That(large.ActiveAircraft.Phase, Is.EqualTo(small.ActiveAircraft.Phase));
            Assert.That(large.Reservations.OccupiedResources, Is.EquivalentTo(small.Reservations.OccupiedResources));
        }

        [Test]
        public void DualFlight_EachDepartureSettlesIndependently()
        {
            var simulation = RunUntilDual(out var clock);
            var cashBefore = simulation.Economy.Cash;
            var routeIncomeBefore = simulation.Economy.TotalRouteIncome;
            var completedBefore = simulation.CompletedCycles;

            for (var second = 1; second <= 8000 && simulation.CompletedCycles < completedBefore + 4; second++)
            {
                clock.Advance(1);
                simulation.Update();
                if (simulation.Routes.Pending != null)
                    simulation.DeclinePendingRoute();
            }

            Assert.That(simulation.CompletedCycles, Is.GreaterThanOrEqualTo(completedBefore + 4));
            Assert.That(simulation.Economy.Cash, Is.GreaterThan(cashBefore));
            Assert.That(simulation.Economy.TotalRouteIncome, Is.GreaterThan(routeIncomeBefore));
        }

        private static AirportSimulation RunUntilDual(out ManualSimulationClock clock)
        {
            clock = new ManualSimulationClock(new SimulationTime(0));
            var simulation = new AirportSimulation(clock, new SeededRandomSource(24031996), new ReservationTable());

            for (var second = 1; second <= 20000; second++)
            {
                clock.Advance(1);
                simulation.Update();
                if (simulation.Routes.Pending != null)
                    simulation.AcceptPendingRoute();
                if (simulation.Flights.Count >= 2)
                    return simulation;
            }

            Assert.Fail("expected a second commercial flight once schedule demand reached the threshold");
            return simulation;
        }

        private static AirportSimulation RunDualComparable(long targetSeconds, long step)
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var simulation = new AirportSimulation(clock, new SeededRandomSource(99), new ReservationTable());
            long now = 0;

            while (now < 2500 && simulation.Flights.Count < 2)
            {
                clock.Advance(1);
                simulation.Update();
                now++;
                if (simulation.Routes.Pending != null)
                    simulation.AcceptPendingRoute();
            }

            Assert.That(simulation.Flights.Count, Is.EqualTo(2), "warmup should reach dual commercial ops");

            while (now < targetSeconds)
            {
                var advance = step;
                if (now + advance > targetSeconds)
                    advance = targetSeconds - now;
                clock.Advance(advance);
                simulation.Update();
                now += advance;
                if (simulation.Routes.Pending != null)
                    simulation.DeclinePendingRoute();
            }

            return simulation;
        }
    }
}

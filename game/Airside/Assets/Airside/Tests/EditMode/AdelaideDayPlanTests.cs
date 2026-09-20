using System.Collections.Generic;
using System.Linq;
using Airside.Domain;
using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class AdelaideDayPlanTests
    {
        [Test]
        public void NewGame_BoardListsTheWholeOperatingDay()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = AirlineOperations.StartAtAdelaide(clock, new SeededRandomSource(5),
                Airline.Player("Day Air", "#1F3A93"));

            var plan = AdelaideDayPlan.ForLocalDay(ops, clock.Now);
            Assert.That(plan.Count(p => p.Arrival), Is.GreaterThan(20), "a day's arrivals, not one per aircraft");
            Assert.That(plan.Count(p => !p.Arrival), Is.GreaterThan(20), "a day's departures");
            Assert.That(plan.Select(p => p.AirlineId).Distinct().Count(), Is.GreaterThan(3));
            foreach (var movement in plan)
            {
                var local = ops.Clock.LocalAt(movement.ScheduledAt);
                Assert.That(local.Hour, Is.InRange(AirlineOperations.AiFirstDepartureHour,
                    AirlineOperations.AiLastDepartureHour));
                Assert.That(movement.Arrival ? movement.Destination : movement.Origin, Is.EqualTo("ADL"));
            }

            var arrivals = new OperationsWorkspaceModel();
            arrivals.Rebuild(ops, clock.Now, OperationsBoardTab.Arrivals, null, null);
            var departures = new OperationsWorkspaceModel();
            departures.Rebuild(ops, clock.Now, OperationsBoardTab.Departures, null, null);
            Assert.That(arrivals.Rows.Count, Is.GreaterThan(ops.Fleet.Count(FlightBoard.IsArrival)),
                "the arrivals board includes the rest of the day's planned inbound");
            Assert.That(departures.Rows.Count, Is.GreaterThan(ops.Fleet.Count(FlightBoard.IsDeparture)),
                "the departures board includes the rest of the day's planned outbound");
        }

        [Test]
        public void AirbornePlan_IsDeterministicAndNeverUsesALiveRegistration()
        {
            var clock = new ManualSimulationClock(new SimulationTime(3 * 3600));
            var ops = AirlineOperations.StartAtAdelaide(clock, new SeededRandomSource(5),
                Airline.Player("Day Air", "#1F3A93"));
            var a = AdelaideDayPlan.AirborneAt(ops, clock.Now);
            var b = AdelaideDayPlan.AirborneAt(ops, clock.Now);
            Assert.That(a.Count, Is.EqualTo(b.Count));
            Assert.That(a.Count, Is.GreaterThan(0), "some of the day's published flights are in the air at 11:00");
            var live = new HashSet<string>(ops.Fleet.Select(f => f.Registration));
            foreach (var flight in a)
            {
                Assert.That(live, Does.Not.Contain(flight.Callsign));
                Assert.That(flight.From.Code == "ADL" || flight.To.Code == "ADL", Is.True);
            }
        }

        [Test]
        public void Plan_IsDeterministicForTheSameClock()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = AirlineOperations.StartAtAdelaide(clock, new SeededRandomSource(5),
                Airline.Player("Day Air", "#1F3A93"));
            var a = AdelaideDayPlan.ForLocalDay(ops, clock.Now);
            var b = AdelaideDayPlan.ForLocalDay(ops, clock.Now);
            Assert.That(a.Select(p => $"{p.FlightNumber}:{p.ScheduledAt.ElapsedSeconds}"),
                Is.EqualTo(b.Select(p => $"{p.FlightNumber}:{p.ScheduledAt.ElapsedSeconds}")));
        }

        [Test]
        public void CoveredBy_DoesNotLetAnOutboundSuppressAPlannedArrival()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = AirlineOperations.StartAtAdelaide(clock, new SeededRandomSource(5),
                Airline.Player("Day Air", "#1F3A93"));
            var rex = ops.Airlines.First(a => a.Id.Value == "REX");
            DestinationCatalogue.TryFind("PLO", out var portLincoln);

            // Force an outbound Rex toward Port Lincoln while a planned arrival from
            // Port Lincoln exists on the same airline.
            var bay = AirlineOperations.AdelaideRegionalBays.First(ops.IsStandFree);
            var outbound = ops.AddAircraft(rex, "VH-OUT", AircraftType.Saab340, bay);
            Assert.That(ops.ScheduleDeparture(outbound, portLincoln, new SimulationTime(600)).Accepted, Is.True);
            clock.Set(new SimulationTime(600));
            ops.Update();
            Assert.That(outbound.State, Is.EqualTo(FleetState.TaxiOut).Or.EqualTo(FleetState.HoldingShort)
                .Or.EqualTo(FleetState.TakingOff).Or.EqualTo(FleetState.Outbound));

            var arrivals = AdelaideDayPlan.ForLocalDay(ops, clock.Now)
                .Where(p => p.Arrival && p.AirlineId == "REX" && p.Origin == "PLO")
                .ToList();
            Assert.That(arrivals, Is.Not.Empty);
            foreach (var planned in arrivals)
                Assert.That(AdelaideDayPlan.CoveredBy(planned, outbound), Is.False,
                    "an outbound to PLO must not hide the inbound from PLO");
        }

        [Test]
        public void Plan_BunchesMovementsOnTheBusyBanks()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = AirlineOperations.StartAtAdelaide(clock, new SeededRandomSource(5),
                Airline.Player("Day Air", "#1F3A93"));
            var peak = 0;
            var quiet = 0;
            foreach (var movement in AdelaideDayPlan.ForLocalDay(ops, clock.Now))
            {
                var hour = ops.Clock.LocalAt(movement.ScheduledAt).Hour;
                if (hour is >= 6 and <= 8 or >= 16 and <= 18)
                    peak++;
                if (hour is >= 13 and <= 15)
                    quiet++;
            }

            Assert.That(peak, Is.GreaterThan(quiet * 2), "the board is busy at the banks, not flat all day");
        }

        [Test]
        public void Plan_MarksSomeSlotsDelayedOrCancelled()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = AirlineOperations.StartAtAdelaide(clock, new SeededRandomSource(5),
                Airline.Player("Day Air", "#1F3A93"));
            var plan = AdelaideDayPlan.ForLocalDay(ops, clock.Now);
            var disrupted = 0;
            foreach (var movement in plan)
            {
                var again = FlightDisruption.For(movement.FlightNumber, movement.ScheduledAt, ops.Clock);
                Assert.That(movement.Disruption.Cancelled, Is.EqualTo(again.Cancelled));
                Assert.That(movement.Disruption.DelayMinutes, Is.EqualTo(again.DelayMinutes));
                if (movement.Disruption.Cancelled || movement.Disruption.Delayed)
                    disrupted++;
            }

            Assert.That(disrupted, Is.GreaterThan(0), "a published day includes delays and cancellations");
        }
    }
}

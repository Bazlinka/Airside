using Airside.Domain;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class AirportRoutesTests
    {
        [Test]
        public void Proposals_AppearOnAScheduleAndExpireUnaccepted()
        {
            var routes = new AirportRoutes(new SimulationTime(0));

            routes.Update(new SimulationTime(AirportRoutes.FirstOfferAfterSeconds - 1));
            Assert.That(routes.Pending, Is.Null);

            routes.Update(new SimulationTime(AirportRoutes.FirstOfferAfterSeconds));
            Assert.That(routes.Pending, Is.Not.Null);
            var firstId = routes.Pending.Id;

            // It lapses once the offer window closes.
            routes.Update(new SimulationTime(AirportRoutes.FirstOfferAfterSeconds + AirportRoutes.OfferWindowSeconds));
            Assert.That(routes.Pending, Is.Null);

            // A fresh, different proposal follows after the interval.
            routes.Update(new SimulationTime(AirportRoutes.FirstOfferAfterSeconds + AirportRoutes.OfferIntervalSeconds));
            Assert.That(routes.Pending, Is.Not.Null);
            Assert.That(routes.Pending.Id, Is.Not.EqualTo(firstId));
        }

        [Test]
        public void AcceptingAProposal_AddsRecurringIncomeAndClearsTheOffer()
        {
            var routes = new AirportRoutes(new SimulationTime(0));
            routes.Update(new SimulationTime(AirportRoutes.FirstOfferAfterSeconds));

            var offered = routes.Pending.IncomePerFlight;
            Assert.That(routes.Accept(new SimulationTime(30), reputationScore: 100), Is.True);
            Assert.That(routes.Pending, Is.Null);
            Assert.That(routes.Accepted, Has.Count.EqualTo(1));
            Assert.That(routes.IncomePerFlight, Is.EqualTo(offered));

            // Nothing to accept now.
            Assert.That(routes.Accept(new SimulationTime(31), reputationScore: 100), Is.False);
        }

        [Test]
        public void DecliningAProposal_ClearsItAndIsCounted()
        {
            var routes = new AirportRoutes(new SimulationTime(0));
            routes.Update(new SimulationTime(AirportRoutes.FirstOfferAfterSeconds));
            Assert.That(routes.Pending, Is.Not.Null);

            Assert.That(routes.Decline(), Is.True);
            Assert.That(routes.Pending, Is.Null);
            Assert.That(routes.OffersDeclined, Is.EqualTo(1));
            Assert.That(routes.Accepted, Is.Empty);

            Assert.That(routes.Decline(), Is.False);
        }

        [Test]
        public void ScheduledFlightsPerDay_SumsAcceptedRoutes()
        {
            var routes = new AirportRoutes(new SimulationTime(0));
            Assert.That(routes.ScheduledFlightsPerDay, Is.Zero);

            var t = AirportRoutes.FirstOfferAfterSeconds;
            routes.Update(new SimulationTime(t));
            var first = routes.Pending.FlightsPerDay;
            routes.Accept(new SimulationTime(t), reputationScore: 100);

            routes.Update(new SimulationTime(t + AirportRoutes.OfferIntervalSeconds));
            var second = routes.Pending.FlightsPerDay;
            routes.Accept(new SimulationTime(t + AirportRoutes.OfferIntervalSeconds), reputationScore: 100);

            Assert.That(routes.ScheduledFlightsPerDay, Is.EqualTo(first + second));
        }

        [Test]
        public void Proposals_AreIdenticalAcrossLargeAndSmallTimeSteps()
        {
            var small = new AirportRoutes(new SimulationTime(0));
            for (long t = 1; t <= 400; t++)
                small.Update(new SimulationTime(t));

            var large = new AirportRoutes(new SimulationTime(0));
            large.Update(new SimulationTime(400));

            Assert.That(large.OffersMade, Is.EqualTo(small.OffersMade));
            Assert.That(large.Pending?.Id, Is.EqualTo(small.Pending?.Id));
        }

        [Test]
        public void Simulation_PaysAcceptedRouteIncomeOnEveryCompletedFlight()
        {
            var withRoute = Run(accept: true);
            var control = Run(accept: false);

            Assert.That(withRoute.CompletedCycles, Is.EqualTo(control.CompletedCycles));
            Assert.That(withRoute.Economy.Cash, Is.GreaterThan(control.Economy.Cash),
                "an accepted route should have paid out over several completed flights");
            Assert.That(withRoute.Routes.Accepted, Has.Count.EqualTo(1));
        }

        private static AirportSimulation Run(bool accept)
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var simulation = new AirportSimulation(clock, new SeededRandomSource(24031996), new ReservationTable());
            var accepted = false;

            for (var second = 1; second <= 2000 && simulation.CompletedCycles < 6; second++)
            {
                clock.Advance(1);
                simulation.Update();
                if (accept && !accepted && simulation.Routes.Pending != null)
                    accepted = simulation.AcceptPendingRoute();
            }

            if (accept)
                Assert.That(accepted, Is.True, "a proposal should have been available to accept");
            return simulation;
        }
    }
}

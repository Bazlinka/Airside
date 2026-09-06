using Airside.Domain;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class ReputationTests
    {
        [Test]
        public void Reputation_RisesWithOnTimeDeparturesAndFallsWithDelays()
        {
            var reputation = new AirportReputation();
            Assert.That(reputation.Score, Is.EqualTo(AirportReputation.Starting));

            reputation.RecordDeparture(0);
            reputation.RecordDeparture(0);
            Assert.That(reputation.Score, Is.EqualTo(AirportReputation.Starting + 4));
            Assert.That(reputation.OnTimeDepartures, Is.EqualTo(2));

            reputation.RecordDeparture(20);
            Assert.That(reputation.Score, Is.LessThan(AirportReputation.Starting + 4));
            Assert.That(reputation.DelayedDepartures, Is.EqualTo(1));
        }

        [Test]
        public void Reputation_StaysWithinBounds()
        {
            var high = new AirportReputation();
            for (var i = 0; i < 200; i++) high.RecordDeparture(0);
            Assert.That(high.Score, Is.EqualTo(AirportReputation.Maximum));

            var low = new AirportReputation();
            for (var i = 0; i < 200; i++) low.RecordDeparture(120);
            Assert.That(low.Score, Is.EqualTo(AirportReputation.Minimum));
        }

        [Test]
        public void Simulation_MovesReputationAsFlightsComplete()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var simulation = new AirportSimulation(clock, new SeededRandomSource(24031996), new ReservationTable());

            for (var second = 1; second <= 2000 && simulation.CompletedCycles < 5; second++)
            {
                clock.Advance(1);
                simulation.Update();
            }

            Assert.That(simulation.CompletedCycles, Is.EqualTo(5));
            Assert.That(simulation.Reputation.OnTimeDepartures + simulation.Reputation.DelayedDepartures,
                Is.EqualTo(5));
            Assert.That(simulation.Reputation.Score, Is.Not.EqualTo(AirportReputation.Starting));
        }

        [Test]
        public void Route_RequiringMoreReputationThanTheAirportHas_CannotBeAccepted()
        {
            var routes = new AirportRoutes(new SimulationTime(0));
            // Walk forward until a proposal needs more than the starting reputation.
            RouteProposal demanding = null;
            for (long t = AirportRoutes.FirstOfferAfterSeconds; t <= 5000 && demanding == null; t++)
            {
                routes.Update(new SimulationTime(t));
                if (routes.Pending != null && routes.Pending.ReputationRequired > AirportReputation.Starting)
                    demanding = routes.Pending;
                else if (routes.Pending != null)
                    routes.Accept(new SimulationTime(t), reputationScore: 100); // clear easy offers
            }

            Assert.That(demanding, Is.Not.Null);
            Assert.That(routes.Accept(new SimulationTime(5001), reputationScore: AirportReputation.Starting), Is.False);
            Assert.That(routes.Accept(new SimulationTime(5001), reputationScore: demanding.ReputationRequired), Is.True);
        }

        [Test]
        public void HigherReputation_LocksInMoreRouteIncome()
        {
            var atFifty = FreshProposalIncome(reputationScore: 50, bonus: 0);
            var atNinety = FreshProposalIncome(reputationScore: 90, bonus: (90 - AirportReputation.Starting) * 3L);

            Assert.That(atNinety, Is.GreaterThan(atFifty));
        }

        private static long FreshProposalIncome(int reputationScore, long bonus)
        {
            var routes = new AirportRoutes(new SimulationTime(0));
            routes.Update(new SimulationTime(AirportRoutes.FirstOfferAfterSeconds));
            routes.Accept(new SimulationTime(30), reputationScore, bonus);
            return routes.IncomePerFlight;
        }
    }
}

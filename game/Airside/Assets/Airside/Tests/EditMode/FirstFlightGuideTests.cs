using System.Collections.Generic;
using System.Linq;
using Airside.Domain;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class FirstFlightGuideTests
    {
        [Test]
        public void Guide_FollowsTheFirstTripAndThenGetsOutOfTheWay()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = AirlineOperations.StartAtAdelaide(clock, new SeededRandomSource(5), Airline.Player("First Air", "#C8102E"));
            var steps = new List<GuideStep>();

            void Record()
            {
                var step = FirstFlightGuide.For(ops, out var aircraft);
                if (step != GuideStep.Complete)
                    Assert.That(aircraft, Is.Not.Null, "every active step names the aircraft it is about");
                if (steps.Count == 0 || steps[^1] != step)
                    steps.Add(step);
            }

            Record();
            var mine = ops.FleetOf(ops.PlayerAirline).Single();
            DestinationCatalogue.TryFind("KGC", out var kingscote);
            ops.ScheduleDeparture(mine, kingscote, new SimulationTime(600));
            Record();

            for (var t = 1L; t < 4 * 3600; t += 5)
            {
                clock.Set(new SimulationTime(t));
                ops.Update();
                Record();
                if (mine.State == FleetState.AwaitingStand)
                    ops.AssignStand(mine, ops.FreeStands().First());
            }

            Assert.That(steps, Is.EqualTo(new[]
            {
                GuideStep.PlanFirstFlight, GuideStep.WaitForDeparture, GuideStep.Departing, GuideStep.Away,
                GuideStep.Landing, GuideStep.ChooseStand, GuideStep.TaxiingIn, GuideStep.Complete
            }));

            // A second flight never brings the guide back.
            ops.ScheduleDeparture(mine, kingscote, clock.Now.Advance(60));
            Assert.That(FirstFlightGuide.For(ops, out _), Is.EqualTo(GuideStep.Complete));
        }

        [Test]
        public void AwayText_MatchesTheLegInsteadOfAlwaysSayingOnItsWayOut()
        {
            var outbound = FirstFlightGuide.AwayText(FleetState.Outbound, "VH-PAX", "Kingscote");
            var turnaround = FirstFlightGuide.AwayText(FleetState.AtDestination, "VH-PAX", "Kingscote");
            var inbound = FirstFlightGuide.AwayText(FleetState.Inbound, "VH-PAX", "Kingscote");

            Assert.That(outbound.hint, Does.Contain("On its way to Kingscote"));
            Assert.That(turnaround.heading, Does.Not.Contain("return"));
            Assert.That(turnaround.hint, Does.Not.Contain("On its way to Kingscote"));
            Assert.That(inbound.heading, Does.Contain("return"));
            Assert.That(inbound.hint, Does.Not.Contain("On its way to Kingscote"));
            Assert.That(inbound.hint, Does.Contain("Adelaide"));
        }
    }
}

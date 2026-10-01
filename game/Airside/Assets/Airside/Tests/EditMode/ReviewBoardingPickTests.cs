using System.Linq;
using Airside.Domain;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>
    /// Locks <c>-airsideReviewBoarding</c> Stage C pick against soak seed 20260913
    /// so a Mac remaining run cannot miss boarding-tape stills for lack of a regional.
    /// </summary>
    public sealed class ReviewBoardingPickTests
    {
        [Test]
        public void PickBest_SkipsJetsFreightersAndInCheck()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = new AirlineOperations(clock, new SeededRandomSource(7), DestinationCatalogue.Adelaide,
                AirlineOperations.AdelaideStands);
            var player = Airline.Player("Soak Air", "#6A3FA0");
            ops.AddAirline(player);
            var jet = ops.AddAircraft(player, "VH-JET", AircraftType.Boeing7378,
                new StableId("GATE-18"));
            var cargo = ops.AddAircraft(player, "VH-CRG", AircraftType.Saab340,
                AirlineOperations.AdelaideRegionalBays[0]);
            var checking = ops.AddAircraft(player, "VH-CHK", AircraftType.Saab340,
                AirlineOperations.AdelaideRegionalBays[1]);
            var regional = ops.AddAircraft(player, "VH-REG", AircraftType.Saab340,
                AirlineOperations.AdelaideRegionalBays[2]);

            Assert.That(ops.SetFreighter(cargo, true).Accepted, Is.True);
            Assert.That(ops.StartCheck(checking).Accepted, Is.True);

            var pick = ReviewBoardingPick.PickBest(ops.FleetOf(player), clock.Now);
            Assert.That(pick, Is.SameAs(regional));
            Assert.That(pick, Is.Not.SameAs(jet));
        }

        [Test]
        public void SoakSeed_HasBookableRegionalAtBoardingStillDelay()
        {
            // remaining.sh boarding batch ~320/323s — must have a regional at T+0
            // that ScheduleDeparture accepts after cancel (packaged soak seed).
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = AirlineOperations.StartAtAdelaide(clock, new SeededRandomSource(20260913),
                Airline.Player("Soak Air", "#6A3FA0"));

            var atStart = ReviewBoardingPick.PickBest(ops.FleetOf(ops.PlayerAirline), clock.Now);
            Assert.That(atStart, Is.Not.Null,
                "soak T+0 must have a parked regional for -airsideReviewBoarding");
            if (atStart.Scheduled.HasValue)
                ops.CancelDeparture(atStart);

            var reachable = ops.MapDestinations().Where(d => ops.CanOperate(atStart, d)
                && ops.CareerState.CanAfford(ops.DispatchCost(atStart.Type, ops.DistanceKm(d)))).ToList();
            Assert.That(reachable, Is.Not.Empty, "boarding pick must reach an affordable destination");
            var lead = DeparturePrep.LeadSeconds(atStart.Type, atStart.BaseLevel);
            Assert.That(ops.ScheduleDeparture(atStart, reachable[0], clock.Now.Advance(lead)).Accepted, Is.True,
                "ScheduleDeparture must accept the Stage C boarding pick");

            clock.Set(new SimulationTime(320));
            ops.Update();
            Assert.That(atStart.Scheduled.HasValue, Is.True,
                "boarding subject must stay booked through the 320s still delay");
        }
    }
}

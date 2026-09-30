using Airside.Domain;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>
    /// Locks <c>-airsideReviewHangarCheck</c> Stage C pick against soak seed 20260913
    /// so a Mac remaining run cannot miss the hangar-tow still for lack of a candidate.
    /// </summary>
    public sealed class ReviewHangarPickTests
    {
        [Test]
        public void PickBest_PrefersNonFoundingWhenBothEligible()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = new AirlineOperations(clock, new SeededRandomSource(7), DestinationCatalogue.Adelaide,
                AirlineOperations.AdelaideRegionalBays);
            var player = Airline.Player("Soak Air", "#6A3FA0");
            ops.AddAirline(player);
            var founding = ops.AddAircraft(player, "VH-FND", AircraftType.Saab340,
                AirlineOperations.AdelaideRegionalBays[0]);
            founding.IsFoundingAircraft = true;
            var other = ops.AddAircraft(player, "VH-OTH", AircraftType.Saab340,
                AirlineOperations.AdelaideRegionalBays[1]);

            var pick = ReviewHangarPick.PickBest(ops.FleetOf(player), clock.Now);
            Assert.That(pick, Is.SameAs(other), "non-founding preferred so founding Saab stays for soak");
            Assert.That(pick, Is.Not.SameAs(founding));
        }

        [Test]
        public void PickBest_SkipsBookedFreighterAndInCheck()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = new AirlineOperations(clock, new SeededRandomSource(7), DestinationCatalogue.Adelaide,
                AirlineOperations.AdelaideRegionalBays);
            var player = Airline.Player("Soak Air", "#6A3FA0");
            ops.AddAirline(player);
            var booked = ops.AddAircraft(player, "VH-BOOK", AircraftType.Saab340,
                AirlineOperations.AdelaideRegionalBays[0]);
            var cargo = ops.AddAircraft(player, "VH-CRG", AircraftType.Saab340,
                AirlineOperations.AdelaideRegionalBays[1]);
            var checking = ops.AddAircraft(player, "VH-CHK", AircraftType.Saab340,
                AirlineOperations.AdelaideRegionalBays[2]);
            var free = ops.AddAircraft(player, "VH-FREE", AircraftType.Saab340,
                AirlineOperations.AdelaideRegionalBays[3]);

            Assert.That(DestinationCatalogue.TryFind("KGC", out var kgc), Is.True);
            var when = new SimulationTime(DeparturePrep.LeadSeconds(booked.Type) + 600);
            Assert.That(ops.ScheduleDeparture(booked, kgc, when).Accepted, Is.True);
            Assert.That(ops.SetFreighter(cargo, true).Accepted, Is.True);
            Assert.That(ops.StartCheck(checking).Accepted, Is.True);

            var pick = ReviewHangarPick.PickBest(ops.FleetOf(player), clock.Now);
            Assert.That(pick, Is.SameAs(free));
        }

        [Test]
        public void SoakSeed_HasCheckableParkedPlayerAtHangarStillDelay()
        {
            // remaining.sh follow-hangar-tow CAPTURE_DELAY=90 — must have a candidate
            // at T+0 (apply) that StartCheck accepts on the packaged soak seed.
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = AirlineOperations.StartAtAdelaide(clock, new SeededRandomSource(20260913),
                Airline.Player("Soak Air", "#6A3FA0"));

            var atStart = ReviewHangarPick.PickBest(ops.FleetOf(ops.PlayerAirline), clock.Now);
            Assert.That(atStart, Is.Not.Null,
                "soak T+0 must have a parked unbooked player aircraft for -airsideReviewHangarCheck");
            Assert.That(ops.StartCheck(atStart).Accepted, Is.True,
                "StartCheck must accept the Stage C hangar pick");

            clock.Set(new SimulationTime(90));
            ops.Update();
            Assert.That(Maintenance.InCheck(atStart, clock.Now), Is.True,
                "hangar subject must still be in check through the 90s still delay");
        }
    }
}

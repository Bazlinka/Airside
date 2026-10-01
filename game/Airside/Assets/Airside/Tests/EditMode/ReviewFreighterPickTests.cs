using System.Linq;
using Airside.Domain;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>
    /// Locks <c>-airsideReviewFreighter</c> Stage A pick against soak seed 20260913
    /// so a Mac remaining run cannot miss the freighter still for lack of a candidate.
    /// </summary>
    public sealed class ReviewFreighterPickTests
    {
        [Test]
        public void PickBest_PrefersJetOverTurbopropWhenBothParkedUnbooked()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            // Full Adelaide stand list so terminal gates are claimable (regional-only ops refuse gates).
            var ops = new AirlineOperations(clock, new SeededRandomSource(7), DestinationCatalogue.Adelaide,
                AirlineOperations.AdelaideStands);
            var player = Airline.Player("Soak Air", "#6A3FA0");
            ops.AddAirline(player);
            var prop = ops.AddAircraft(player, "VH-PROP", AircraftType.Saab340,
                AirlineOperations.AdelaideRegionalBays[0]);
            var jet = ops.AddAircraft(player, "VH-JET", AircraftType.Boeing7378,
                new StableId("GATE-18"));

            var pick = ReviewFreighterPick.PickBest(ops.FleetOf(player));
            Assert.That(pick, Is.SameAs(jet), "jet preferred for cargo-title still readability");
            Assert.That(pick, Is.Not.SameAs(prop));
        }

        [Test]
        public void PickBest_SkipsBookedAndAlreadyFreighter()
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
            var free = ops.AddAircraft(player, "VH-FREE", AircraftType.Saab340,
                AirlineOperations.AdelaideRegionalBays[2]);

            Assert.That(DestinationCatalogue.TryFind("KGC", out var kgc), Is.True);
            var when = new SimulationTime(DeparturePrep.LeadSeconds(booked.Type) + 600);
            Assert.That(ops.ScheduleDeparture(booked, kgc, when).Accepted, Is.True);
            Assert.That(ops.SetFreighter(cargo, true).Accepted, Is.True);

            var pick = ReviewFreighterPick.PickBest(ops.FleetOf(player));
            Assert.That(pick, Is.SameAs(free));
        }

        [Test]
        public void SoakSeed_HasRefittableParkedPlayerAtStageAFreighterDelay()
        {
            // remaining.sh follow-freighter CAPTURE_DELAY=28 — must have a candidate
            // at T+0 (apply) and still at T+28 (shot) on the packaged soak seed.
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = AirlineOperations.StartAtAdelaide(clock, new SeededRandomSource(20260913),
                Airline.Player("Soak Air", "#6A3FA0"));

            var atStart = ReviewFreighterPick.PickBest(ops.FleetOf(ops.PlayerAirline));
            Assert.That(atStart, Is.Not.Null,
                "soak T+0 must have a parked unbooked player aircraft for -airsideReviewFreighter");
            Assert.That(ops.SetFreighter(atStart, true).Accepted, Is.True,
                "founding funds must cover the Stage A freighter refit");

            clock.Set(new SimulationTime(28));
            ops.Update();
            Assert.That(atStart.IsFreighter, Is.True);
            Assert.That(atStart.State, Is.EqualTo(FleetState.AtStand),
                "review freighter hold subject must stay on stand through the 28s still delay");
        }
    }
}

using System;
using System.Linq;
using Airside.Domain;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class CathaySeasonTests
    {
        [Test]
        public void OutOfSeason_RemovesParkedCathayAndFreesTheGate()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = new AirlineOperations(clock, new SeededRandomSource(3), DestinationCatalogue.Adelaide,
                AirlineOperations.AdelaideStands);
            // Mid-January Adelaide is in Cathay season.
            ops.Clock = AirlineClock.Aligned(new SimulationTime(0),
                new DateTime(2027, 1, 15, 12, 0, 0, DateTimeKind.Utc));
            Assert.That(ops.IsCathaySeason(clock.Now), Is.True);
            Assert.That(ops.AddMissingTerminalOperators(at: clock.Now), Is.GreaterThan(0));
            Assert.That(ops.Fleet.Any(a => a.Airline.Id.Value == "CPA"), Is.True);
            Assert.That(ops.IsStandFree(new StableId("GATE-18")), Is.False);

            // Jump to May — season over.
            var may = ops.Clock.AtLocal(ops.Clock.LocalAt(clock.Now).Date.AddMonths(4));
            clock.Set(may);
            ops.Update();

            Assert.That(ops.IsCathaySeason(clock.Now), Is.False);
            Assert.That(ops.Fleet.Any(a => a.Airline.Id.Value == "CPA"), Is.False,
                "Cathay flies its last rotation home and is retired while away (ADR 0110)");
            // GATE-18 no longer belongs to Cathay; another widebody (or a jet short of a gate)
            // may be using it by now in a busy Adelaide day.
            Assert.That(ops.Fleet.Any(a => a.Stand.Equals(new StableId("GATE-18")) && a.Airline.Id.Value == "CPA"), Is.False);
            Assert.That(ops.Airlines.Any(a => a.Id.Value == "CPA"), Is.False);
        }

        [Test]
        public void OutOfSeason_ParkedCathayKeepsItsGateUntilItsBookedDeparture()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = new AirlineOperations(clock, new SeededRandomSource(3), DestinationCatalogue.Adelaide,
                AirlineOperations.AdelaideStands);
            ops.Clock = AirlineClock.Aligned(new SimulationTime(0),
                new DateTime(2027, 1, 15, 12, 0, 0, DateTimeKind.Utc));
            ops.AddMissingTerminalOperators(at: clock.Now);
            var cathay = ops.Fleet.Single(a => a.Airline.Id.Value == "CPA");
            Assert.That(cathay.State, Is.EqualTo(FleetState.AtStand));
            Assert.That(cathay.Scheduled.HasValue, Is.True);

            Assert.That(ops.RetireOutOfSeasonOperators(ops.Clock.AtLocal(new DateTime(2027, 5, 1, 12, 0, 0))),
                Is.EqualTo(0), "the apron keeps its aircraft until they depart");
            Assert.That(ops.IsStandFree(new StableId("GATE-18")), Is.False);
        }

        [Test]
        public void OutOfSeason_AirborneCathayFinishesItsTripAndIsRetiredAway()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = new AirlineOperations(clock, new SeededRandomSource(3), DestinationCatalogue.Adelaide,
                AirlineOperations.AdelaideStands);
            ops.Clock = AirlineClock.Aligned(new SimulationTime(0),
                new DateTime(2027, 5, 1, 2, 0, 0, DateTimeKind.Utc));
            Assert.That(ops.IsCathaySeason(clock.Now), Is.False);
            DestinationCatalogue.TryFind("HKG", out var hongKong);
            ops.AddAirline(Airline.CathayPacific());
            var restore = typeof(AirlineOperations).GetMethod("RestoreAircraft",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            restore!.Invoke(ops, new object[]
            {
                "B-LRB", ops.Airlines.First(a => a.Id.Value == "CPA"), AircraftType.AirbusA350900,
                FleetState.Outbound, clock.Now, clock.Now.Advance(7200), default(StableId),
                new StableId("GATE-18"), hongKong, null, 0
            });

            Assert.That(ops.RetireOutOfSeasonOperators(clock.Now), Is.EqualTo(0),
                "airborne Cathay is not yanked mid-trip");
            Assert.That(ops.Fleet.Any(a => a.Registration == "B-LRB"), Is.True);

            clock.Set(new SimulationTime(7200 + 60));
            ops.Update();
            Assert.That(ops.Fleet.Any(a => a.Registration == "B-LRB"), Is.False,
                "retired at Hong Kong, never returning to a gate out of season");
            Assert.That(ops.Airlines.Any(a => a.Id.Value == "CPA"), Is.False);
        }
    }
}

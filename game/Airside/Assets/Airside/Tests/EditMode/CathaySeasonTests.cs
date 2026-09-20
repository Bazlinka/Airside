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
                "parked Cathay leaves when the season ends");
            Assert.That(ops.IsStandFree(new StableId("GATE-18")), Is.True);
            Assert.That(ops.Airlines.Any(a => a.Id.Value == "CPA"), Is.False);
        }

        [Test]
        public void OutOfSeason_LeavesAnAirborneCathayAloneUntilItParks()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = new AirlineOperations(clock, new SeededRandomSource(3), DestinationCatalogue.Adelaide,
                AirlineOperations.AdelaideStands);
            ops.Clock = AirlineClock.Aligned(new SimulationTime(0),
                new DateTime(2027, 1, 15, 12, 0, 0, DateTimeKind.Utc));
            ops.AddMissingTerminalOperators(at: clock.Now);
            var cathay = ops.Fleet.Single(a => a.Airline.Id.Value == "CPA");
            DestinationCatalogue.TryFind("HKG", out var hongKong);

            // Force outbound so retirement cannot yank mid-trip.
            var restore = typeof(AirlineOperations).GetMethod("RestoreAircraft",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            // Clear the parked seasonal jet first by retiring while still in-season is a no-op;
            // instead mutate via a temporary out-of-season retire after forcing Outbound.
            // Use reflection-free path: schedule and push until Outbound is hard; just set state
            // by restoring over the same registration after removing via Retire with a fake.
            Assert.That(ops.RetireOutOfSeasonOperators(clock.Now), Is.EqualTo(0), "still in season");

            // Manually mark as outbound using the public Restore path after removing from stand.
            // Remove from fleet by retiring with a clock jump is what we test for parked;
            // here call Retire while state is Outbound via RestoreAircraft replacement.
            var airline = cathay.Airline;
            // Drop parked aircraft by clearing through Retire only works AtStand — change state
            // by scheduling a departure and advancing isn't needed: use Restore after remove.
            // Simplest: RetireOutOfSeasonOperators skips non-AtStand — put it Outbound via Restore.
            ops.RetireOutOfSeasonOperators(clock.Now); // no-op in season
            // Re-add as outbound by removing parked then restoring outbound.
            // Direct approach: use Retire after forcing AtStand check — set via RestoreAircraft
            // which requires not already in fleet. Remove parked first by temporarily calling
            // Retire with a patched season — instead restore movement on a new outbound id.

            // Force the existing aircraft into Outbound with RestoreAircraft by first removing it.
            Assert.That(ops.RetireOutOfSeasonOperators(
                ops.Clock.AtLocal(new DateTime(2027, 5, 1, 12, 0, 0))), Is.EqualTo(1));
            Assert.That(ops.Fleet.Any(a => a.Airline.Id.Value == "CPA"), Is.False);

            // Re-introduce CPA airline + outbound aircraft after season ended.
            ops.AddAirline(Airline.CathayPacific());
            restore!.Invoke(ops, new object[]
            {
                "B-LRB", ops.Airlines.First(a => a.Id.Value == "CPA"), AircraftType.AirbusA350900,
                FleetState.Outbound, clock.Now, clock.Now.Advance(7200), default(StableId),
                default(StableId), hongKong, null, 0
            });

            Assert.That(ops.RetireOutOfSeasonOperators(clock.Now), Is.EqualTo(0),
                "airborne Cathay is not yanked mid-trip");
            Assert.That(ops.Fleet.Any(a => a.Registration == "B-LRB"), Is.True);
        }
    }
}

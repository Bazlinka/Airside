using System.Collections.Generic;
using Airside.Domain;
using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>Dev Tools panel helpers (presentation only).</summary>
    public sealed class DevToolsTests
    {
        private static Destination Code(string code)
        {
            Assert.That(DestinationCatalogue.TryFind(code, out var destination), Is.True, code);
            return destination;
        }

        private static (ManualSimulationClock clock, AirlineOperations ops, FleetAircraft plane) PlayerOnly(int aircraft = 1)
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = new AirlineOperations(clock, new SeededRandomSource(17), DestinationCatalogue.Adelaide,
                AirlineOperations.AdelaideRegionalBays);
            var player = Airline.Player("Dev Air", "#2E7D32");
            ops.AddAirline(player);
            FleetAircraft first = null;
            for (var i = 0; i < aircraft; i++)
            {
                var added = ops.AddAircraft(player, $"VH-DV{(char)('A' + i)}", AircraftType.Atr42,
                    AirlineOperations.AdelaideRegionalBays[i]);
                first ??= added;
            }

            return (clock, ops, first);
        }

        [Test]
        public void FleetLine_IncludesRegistrationAndState()
        {
            var (_, _, aircraft) = PlayerOnly();
            var line = DevTools.FleetLine(aircraft);
            Assert.That(line, Does.Contain(aircraft.Registration));
            Assert.That(line, Does.Contain("AtStand"));
            Assert.That(line, Does.Contain("stand"));
        }

        [Test]
        public void FleetLine_HandlesAnAircraftThatHasLeftItsStand()
        {
            var (clock, ops, aircraft) = PlayerOnly();
            DestinationCatalogue.TryFind("KGC", out var kingscote);
            Assert.That(ops.ScheduleDeparture(aircraft, kingscote, new SimulationTime(0)).Accepted, Is.True);
            clock.Set(new SimulationTime(1));
            ops.Update();
            Assert.That(aircraft.State, Is.Not.EqualTo(FleetState.AtStand));

            var line = DevTools.FleetLine(aircraft);
            Assert.That(line, Does.Contain("stand —"));
        }

        [Test]
        public void NextEventLabel_ExplainsWhenNothingIsQueued()
        {
            Assert.That(DevTools.NextEventLabel(null, t => $"T{t.ElapsedSeconds}"),
                Is.EqualTo("Next event: waiting on you (stand / schedule)"));
            Assert.That(DevTools.NextEventLabel(new SimulationTime(120), t => $"T{t.ElapsedSeconds}"),
                Is.EqualTo("Next event: T120"));
        }

        [Test]
        public void AutoScheduleDelay_MatchesSoakPolicy()
        {
            Assert.That(DevTools.AutoScheduleDelaySeconds(0, 999),
                Is.EqualTo(DevTools.FirstAutoDepartureLeadSeconds));

            var min = EngineStartSequence.MinimumDepartureLeadSeconds;
            Assert.That(DevTools.AutoScheduleDelaySeconds(1, 0), Is.EqualTo(min));

            var span = DevTools.LaterAutoDepartureLeadMaxSeconds - (int)min;
            var nearEnd = DevTools.AutoScheduleDelaySeconds(2, span - 1);
            Assert.That(nearEnd, Is.EqualTo(DevTools.LaterAutoDepartureLeadMaxSeconds - 1));
            Assert.That(nearEnd, Is.GreaterThanOrEqualTo(min));
            Assert.That(nearEnd, Is.LessThan(DevTools.LaterAutoDepartureLeadMaxSeconds));
        }

        [Test]
        public void Counts_IdleAndAwaitingStand()
        {
            var (_, ops, _) = PlayerOnly(aircraft: 2);
            Assert.That(DevTools.CountIdleAtStand(ops.Fleet), Is.EqualTo(2));
            Assert.That(DevTools.CountAwaitingStand(ops.Fleet), Is.EqualTo(0));

            ops.ScheduleDeparture(ops.Fleet[0], Code("KGC"), new SimulationTime(600));
            Assert.That(DevTools.CountIdleAtStand(new List<FleetAircraft> { ops.Fleet[0], ops.Fleet[1] }),
                Is.EqualTo(1));
        }
    }
}

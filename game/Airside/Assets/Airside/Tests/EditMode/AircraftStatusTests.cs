using System.Collections.Generic;
using System.Reflection;
using Airside.Domain;
using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>Status severity, waiting time and the stacked toast queue (presentation helpers only).</summary>
    public sealed class AircraftStatusTests
    {
        private static FleetAircraft Aircraft(bool player)
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = new AirlineOperations(clock, new SeededRandomSource(3), DestinationCatalogue.Adelaide,
                AirlineOperations.AdelaideRegionalBays);
            var airline = player ? Airline.Player("Test Air", "#39708A") : new Airline("OTH", "Other Air", "#C95D50", isPlayer: false);
            ops.AddAirline(airline);
            return ops.AddAircraft(airline, "VH-TST", AircraftType.Atr42, AirlineOperations.AdelaideRegionalBays[0]);
        }

        private static void Enter(FleetAircraft aircraft, FleetState state, long at, long? duration = null)
        {
            var enter = typeof(FleetAircraft).GetMethod("Enter", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(enter, Is.Not.Null);
            enter.Invoke(aircraft, new object[] { state, new SimulationTime(at), duration });
        }

        [Test]
        public void HoldingShort_EscalatesWithTimeWaited()
        {
            var aircraft = Aircraft(player: true);
            Enter(aircraft, FleetState.HoldingShort, 1_000);

            Assert.That(AircraftStatus.IsWaiting(aircraft), Is.True);
            Assert.That(AircraftStatus.Severity(aircraft, new SimulationTime(1_030)), Is.EqualTo(StatusSeverity.Normal));
            Assert.That(AircraftStatus.WaitSuffix(aircraft, new SimulationTime(1_030)), Is.Empty);
            Assert.That(AircraftStatus.Severity(aircraft, new SimulationTime(1_000 + AircraftStatus.HoldAttentionSeconds)),
                Is.EqualTo(StatusSeverity.Attention));
            Assert.That(AircraftStatus.Severity(aircraft, new SimulationTime(1_000 + AircraftStatus.HoldWarningSeconds)),
                Is.EqualTo(StatusSeverity.Warning));
            Assert.That(AircraftStatus.WaitSuffix(aircraft, new SimulationTime(1_000 + 4 * 60 + 20)), Is.EqualTo(" · waiting 4 min"));
            Assert.That(AircraftStatus.WaitProgress(aircraft, new SimulationTime(1_000 + AircraftStatus.HoldWarningSeconds * 2)),
                Is.EqualTo(1f));
        }

        [Test]
        public void AwaitingStand_IsAWarningOnlyForThePlayer()
        {
            var mine = Aircraft(player: true);
            var theirs = Aircraft(player: false);
            Enter(mine, FleetState.AwaitingStand, 0);
            Enter(theirs, FleetState.AwaitingStand, 0);

            Assert.That(AircraftStatus.Severity(mine, new SimulationTime(1)), Is.EqualTo(StatusSeverity.Warning));
            Assert.That(AircraftStatus.Severity(theirs, new SimulationTime(1)), Is.EqualTo(StatusSeverity.Attention));
        }

        [Test]
        public void TimedStates_AreNeverWaitingOrAlarming()
        {
            var aircraft = Aircraft(player: true);
            Enter(aircraft, FleetState.TaxiOut, 0, 300);

            Assert.That(AircraftStatus.IsWaiting(aircraft), Is.False);
            Assert.That(AircraftStatus.WaitingSeconds(aircraft, new SimulationTime(99_999)), Is.EqualTo(0));
            Assert.That(AircraftStatus.Severity(aircraft, new SimulationTime(99_999)), Is.EqualTo(StatusSeverity.Normal));
            Assert.That(AircraftStatus.TagPhase(aircraft), Is.EqualTo("taxiing"));
        }

        [Test]
        public void TagPhase_ShowsPrepPercentWhileOnStand()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = new AirlineOperations(clock, new SeededRandomSource(3), DestinationCatalogue.Adelaide,
                AirlineOperations.AdelaideRegionalBays);
            var player = Airline.Player("Test Air", "#39708A");
            ops.AddAirline(player);
            var plane = ops.AddAircraft(player, "VH-PRP", AircraftType.Atr42, AirlineOperations.AdelaideRegionalBays[0]);
            Assert.That(DestinationCatalogue.TryFind("KGC", out var kgc), Is.True);
            Assert.That(ops.ScheduleDeparture(plane, kgc, new SimulationTime(600)).Accepted, Is.True);

            Assert.That(AircraftStatus.TagPhase(plane, new SimulationTime(0)), Is.EqualTo("fuelling 0%"));
            Assert.That(AircraftStatus.TagPhase(plane, new SimulationTime(DeparturePrep.FuelSeconds / 2)),
                Is.EqualTo("fuelling 50%"));
            Assert.That(AircraftStatus.TagPhase(plane, new SimulationTime(DeparturePrep.TotalSeconds(plane.Type))),
                Is.EqualTo("ready"));
        }

        [Test]
        public void ToastQueue_StacksNewestFirstAndCapsVisible()
        {
            var queue = new ToastQueue();
            var visible = new List<ToastEntry>();
            queue.Push("one", 0f);
            queue.Push("two", 1f);
            queue.Push("three", 2f);
            queue.Push("four", 3f);

            queue.Visible(3.5f, visible);
            Assert.That(visible.ConvertAll(e => e.Message), Is.EqualTo(new[] { "four", "three", "two" }));

            queue.Visible(ToastQueue.LifetimeSeconds + 2.5f, visible);
            Assert.That(visible.ConvertAll(e => e.Message), Is.EqualTo(new[] { "four" }));
            Assert.That(queue.History.Count, Is.EqualTo(4));
        }

        [Test]
        public void ToastQueue_RepeatRestartsTimerAndCountsInsteadOfStacking()
        {
            var queue = new ToastQueue();
            queue.Push("Runway busy", 0f);
            queue.Push("Runway busy", 5f);

            Assert.That(queue.History.Count, Is.EqualTo(1));
            Assert.That(queue.History[0].Repeats, Is.EqualTo(2));
            Assert.That(ToastQueue.Alpha(queue.History[0], 10f), Is.EqualTo(1f));
        }

        [Test]
        public void ToastQueue_KeepsABoundedHistoryAndFadesOut()
        {
            var queue = new ToastQueue();
            for (var i = 0; i < ToastQueue.HistoryLength + 5; i++)
                queue.Push($"m{i}", i * 10f);

            Assert.That(queue.History.Count, Is.EqualTo(ToastQueue.HistoryLength));
            Assert.That(queue.History[0].Message, Is.EqualTo($"m{ToastQueue.HistoryLength + 4}"));
            var entry = new ToastEntry("x", 0f);
            Assert.That(ToastQueue.Alpha(entry, ToastQueue.LifetimeSeconds - ToastQueue.FadeSeconds * 0.5f), Is.EqualTo(0.5f).Within(0.01f));
            Assert.That(ToastQueue.Alpha(entry, ToastQueue.LifetimeSeconds + 1f), Is.EqualTo(0f));
        }
    }
}

using System;
using System.Linq;
using Airside.Domain;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>The airport keeps running while the game is closed (ADR 0045).</summary>
    public sealed class AwayCatchUpTests
    {
        private static readonly DateTime SavedAt = new(2026, 9, 14, 9, 0, 0, DateTimeKind.Utc);

        private static (ManualSimulationClock clock, AirlineOperations ops) Game()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = AirlineOperations.StartAtAdelaide(clock, new SeededRandomSource(77), Airline.Player("Bight Air", "#1F3A93"));
            DestinationCatalogue.TryFind("KGC", out var kingscote);
            ops.ScheduleDeparture(ops.FleetOf(ops.PlayerAirline).Single(), kingscote, new SimulationTime(300));
            clock.Set(new SimulationTime(120));
            ops.Update();
            return (clock, ops);
        }

        [Test]
        public void SecondsAway_IsRealTimeSinceTheSaveCappedAtAWeek()
        {
            var data = new AirlineSaveData { SavedAtUtcTicks = SavedAt.Ticks };
            Assert.That(AwayCatchUp.SecondsAway(data, SavedAt.AddSeconds(30)), Is.Zero, "a quick relaunch");
            Assert.That(AwayCatchUp.SecondsAway(data, SavedAt.AddHours(2)), Is.EqualTo(7200));
            Assert.That(AwayCatchUp.SecondsAway(data, SavedAt.AddDays(30)), Is.EqualTo(AwayCatchUp.MaxSeconds));
            Assert.That(AwayCatchUp.SecondsAway(data, SavedAt.AddHours(-3)), Is.Zero, "device clock moved backwards");
            Assert.That(AwayCatchUp.SecondsAway(new AirlineSaveData(), SavedAt), Is.Zero, "version 1 saves have no timestamp");
        }

        [Test]
        public void CatchUp_ReachesTheSameStateAsPlayingLive()
        {
            var (liveClock, live) = Game();
            var saved = AirlineSave.Capture(live, SavedAt);
            var away = AwayCatchUp.SecondsAway(saved, SavedAt.AddHours(5).AddMinutes(17));

            var resumedClock = new ManualSimulationClock(new SimulationTime(saved.ClockSeconds));
            var resumed = AirlineSave.Restore(saved, resumedClock);
            resumedClock.Set(resumedClock.Now.Advance(away));
            resumed.Update();

            for (var t = liveClock.Now.ElapsedSeconds; t <= saved.ClockSeconds + away; t += 13)
            {
                liveClock.Set(new SimulationTime(t));
                live.Update();
            }

            liveClock.Set(resumedClock.Now);
            live.Update();

            Assert.That(Describe(resumed), Is.EqualTo(Describe(live)));
        }

        [Test]
        public void Summary_SaysWhatHappenedAndWhatNeedsYou()
        {
            var (clock, ops) = Game();
            var saved = AirlineSave.Capture(ops, SavedAt);

            clock.Set(clock.Now.Advance(3 * 3600));
            ops.Update();
            var summary = AwaySummary.Build(saved, ops, 3 * 3600);

            Assert.That(summary.Title, Is.EqualTo("You were away 3 h 00 min"));
            Assert.That(summary.Lines[0], Does.StartWith("VH-PAX"));
            Assert.That(summary.Lines[0], Does.Contain("taxiing to a stand").Or.Contain("parked").Or.Contain("no flight planned").Or.Contain("departing"),
                "a Kingscote round trip is back well inside three hours and auto-parks");
            Assert.That(summary.Lines.Any(l => (l.StartsWith("Rex flew") || l.StartsWith("QantasLink flew")) && l.Contains("trip")), Is.True);
        }

        [Test]
        public void Summary_ReportsCareerEarningsWhileAway()
        {
            var (clock, ops) = Game();
            Assert.That(ops.AcceptContract(RouteContractCatalogue.RegionalKingscoteIntro).Accepted, Is.True);
            var saved = AirlineSave.Capture(ops, SavedAt);

            clock.Set(clock.Now.Advance(3 * 3600));
            ops.Update();
            var plane = ops.FleetOf(ops.PlayerAirline).Single();
            Assert.That(plane.State, Is.EqualTo(FleetState.AtStand), "auto-park settles the rotation");
            Assert.That(plane.CompletedTrips, Is.GreaterThanOrEqualTo(1));

            var summary = AwaySummary.Build(saved, ops, 3 * 3600);
            Assert.That(summary.Lines.Any(l => l.Contains("earned $")), Is.True);
        }

        [Test]
        public void Summary_MigratingAPre6Save_DoesNotMisreportTheFreshCareerAsChangedWhileAway()
        {
            var (clock, ops) = Game();
            var saved = AirlineSave.Capture(ops, SavedAt);
            // A real pre-6 save never wrote these fields — JsonUtility leaves them at the
            // field's default, not at the fresh-Provisional starting values.
            saved.Version = 5;
            saved.CareerFunds = 0;
            saved.CareerReliability = 0;

            clock.Set(clock.Now.Advance(3 * 3600));
            ops.Update();

            // No contract was ever active, so nothing should read as having happened. In
            // particular, comparing the pre-6 default (0% reliability) against the actually
            // fresh-migrated Provisional career (100%) must not misreport that gap as
            // something that occurred while the player was away.
            var summary = AwaySummary.Build(saved, ops, 3 * 3600);
            Assert.That(summary.Lines.Any(l => l.Contains("Reliability rose")), Is.False);
            Assert.That(summary.Lines.Any(l => l.Contains("earned $")), Is.False);
        }

        [Test]
        public void VersionOneSave_StillLoads()
        {
            var (clock, ops) = Game();
            var saved = AirlineSave.Capture(ops);
            saved.Version = 1;
            saved.SavedAtUtcTicks = 0;
            var restored = AirlineSave.Restore(saved, new ManualSimulationClock(clock.Now));
            Assert.That(restored.PlayerAirline.Name, Is.EqualTo("Bight Air"));
            Assert.That(AwayCatchUp.SecondsAway(saved, DateTime.UtcNow), Is.Zero);
        }

        private static string Describe(AirlineOperations ops) =>
            string.Join("\n", ops.Fleet.Select(a =>
                $"{a.Registration} {a.State} {a.StateStartedAt.ElapsedSeconds} {a.Stand.Value} {a.CurrentDestination?.Code} " +
                $"{a.Scheduled?.Destination.Code}@{a.Scheduled?.DepartAt.ElapsedSeconds} {a.CompletedTrips}"))
            + $"\nrunway {ops.RunwayFreeAt.ElapsedSeconds}/{ops.CrossRunwayFreeAt.ElapsedSeconds} rng {ops.RandomState}";
    }
}

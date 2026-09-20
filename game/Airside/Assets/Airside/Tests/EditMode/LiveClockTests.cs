using System;
using System.Linq;
using Airside.Domain;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>Live real time at Adelaide (ADR 0045, Bailey 2026-09-14).</summary>
    public sealed class LiveClockTests
    {
        [Test]
        public void Clock_ReadsAdelaideLocalTimeIncludingDaylightSaving()
        {
            // 22:30 UTC on 13 Sep is 08:00 ACST (+9:30) on 14 Sep.
            var winter = new AirlineClock(new DateTime(2026, 9, 13, 22, 30, 0, DateTimeKind.Utc).Ticks);
            Assert.That(winter.TimeText(new SimulationTime(0)), Is.EqualTo("08:00"));
            Assert.That(winter.DateText(new SimulationTime(0)), Is.EqualTo("Mon 14 Sep"));
            Assert.That(winter.StampText(new SimulationTime(0)), Is.EqualTo("Mon 14 Sep  08:00"));
            Assert.That(winter.TimeText(new SimulationTime(90 * 60)), Is.EqualTo("09:30"));

            // 22:30 UTC on 14 Dec is 09:00 ACDT (+10:30) on 15 Dec.
            var summer = new AirlineClock(new DateTime(2026, 12, 14, 22, 30, 0, DateTimeKind.Utc).Ticks);
            Assert.That(summer.TimeText(new SimulationTime(0)), Is.EqualTo("09:00"));
        }

        [Test]
        public void Clock_OneSimulatedSecondIsOneRealSecond()
        {
            var start = new DateTime(2026, 9, 14, 0, 0, 0, DateTimeKind.Utc);
            var clock = AirlineClock.Aligned(new SimulationTime(500), start);
            Assert.That(clock.At(start).ElapsedSeconds, Is.EqualTo(500));
            Assert.That(clock.At(start.AddHours(2)).ElapsedSeconds, Is.EqualTo(500 + 7200));
            Assert.That(clock.SecondsAt(start.AddMilliseconds(250)), Is.EqualTo(500.25).Within(0.001));
        }

        [Test]
        public void LiveTarget_CatchesUpToNowButRealignsPastTheCapOrABackwardsClock()
        {
            var savedAt = new DateTime(2026, 9, 14, 0, 0, 0, DateTimeKind.Utc);

            AirlineOperations Restored()
            {
                var clock = new ManualSimulationClock(new SimulationTime(1000));
                return AirlineOperations.StartAtAdelaide(clock, new SeededRandomSource(1), Airline.Player("Live Air", "#2E7D32"),
                    AirlineClock.Aligned(new SimulationTime(1000), savedAt));
            }

            var normal = Restored();
            Assert.That(AwayCatchUp.LiveTarget(normal, savedAt.AddHours(3)).ElapsedSeconds, Is.EqualTo(1000 + 3 * 3600));

            var longAway = Restored();
            var now = savedAt.AddDays(40);
            var capped = AwayCatchUp.LiveTarget(longAway, now);
            Assert.That(capped.ElapsedSeconds, Is.EqualTo(1000 + AwayCatchUp.MaxSeconds));
            Assert.That(longAway.Clock.At(now), Is.EqualTo(capped), "the clock reads real time from here on");

            var backwards = Restored();
            Assert.That(AwayCatchUp.LiveTarget(backwards, savedAt.AddHours(-5)).ElapsedSeconds, Is.EqualTo(1000));
        }

        [Test]
        public void Saves_KeepTheEpochAndMigrateOlderVersions()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var epoch = new DateTime(2026, 9, 14, 1, 2, 3, DateTimeKind.Utc);
            var ops = AirlineOperations.StartAtAdelaide(clock, new SeededRandomSource(3), Airline.Player("Live Air", "#2E7D32"),
                new AirlineClock(epoch.Ticks));
            clock.Set(new SimulationTime(600));
            ops.Update();

            var saved = AirlineSave.Capture(ops, epoch.AddSeconds(600));
            Assert.That(saved.Version, Is.EqualTo(AirlineSaveData.CurrentVersion));
            var restored = AirlineSave.Restore(saved, new ManualSimulationClock(clock.Now));
            Assert.That(restored.Clock.EpochUtcTicks, Is.EqualTo(epoch.Ticks));

            saved.Version = 2;
            saved.EpochUtcTicks = 0;
            var fromV2 = AirlineSave.Restore(saved, new ManualSimulationClock(clock.Now));
            Assert.That(fromV2.Clock.EpochUtcTicks, Is.EqualTo(epoch.Ticks), "v2: aligned so the save moment reads as when it was saved");
        }

        [Test]
        public void NewGame_HasAnArrivalAndStaggeredDepartures()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = AirlineOperations.StartAtAdelaide(clock, new SeededRandomSource(8), Airline.Player("Live Air", "#2E7D32"));
            // Regional openings only; the Gate 13 jet (ADR 0047) keeps its own timetable.
            var departures = ops.Fleet.Where(a => !a.Airline.IsPlayer && a.State == FleetState.AtStand && a.Scheduled.HasValue)
                .Select(a => a.Scheduled.Value.DepartAt.ElapsedSeconds).OrderBy(t => t).ToArray();
            Assert.That(departures.Length, Is.GreaterThanOrEqualTo(3), "several aircraft push in the opening bank");
            Assert.That(departures[0], Is.EqualTo(AirlineOperations.AiOpeningDepartureSeconds[0]));
            Assert.That(ops.Fleet.Count(a => a.State == FleetState.Inbound), Is.GreaterThanOrEqualTo(11),
                "regional and jet arrivals already in the opening bank");
            Assert.That(departures.Max(), Is.LessThanOrEqualTo(40 * 60), "departures are spread across the opening bank");
        }
    }
}

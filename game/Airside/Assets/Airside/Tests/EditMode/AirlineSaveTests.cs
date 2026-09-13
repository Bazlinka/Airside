using System;
using System.Linq;
using System.Text;
using Airside.Domain;
using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace Airside.Tests
{
    /// <summary>Saving and resuming an airline game (ADR 0045).</summary>
    public sealed class AirlineSaveTests
    {
        private static Destination Code(string code)
        {
            DestinationCatalogue.TryFind(code, out var destination);
            return destination;
        }

        /// <summary>A busy mid-game moment: the player away, Emu Air mid-rotation.</summary>
        private static (ManualSimulationClock clock, AirlineOperations ops) MidGame()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = AirlineOperations.StartAtAdelaide(clock, new SeededRandomSource(99), Airline.Player("Gulf Air Link", "#2E7D32"));
            var mine = ops.FleetOf(ops.PlayerAirline).Single();
            ops.ScheduleDeparture(mine, Code("MEL"), new SimulationTime(1200));
            clock.Set(new SimulationTime(3 * 3600 + 17));
            ops.Update();
            return (clock, ops);
        }

        [Test]
        public void ResumedGame_ContinuesExactlyLikeOneThatNeverStopped()
        {
            var (clock, original) = MidGame();
            var json = JsonUtility.ToJson(AirlineSave.Capture(original));

            var resumedClock = new ManualSimulationClock(clock.Now);
            var resumed = AirlineSave.Restore(JsonUtility.FromJson<AirlineSaveData>(json), resumedClock);
            Assert.That(Snapshot(resumed), Is.EqualTo(Snapshot(original)), "identical at the moment of saving");

            var savedAt = clock.Now.ElapsedSeconds;
            foreach (var (c, ops) in new[] { (clock, original), (resumedClock, resumed) })
            {
                for (var t = savedAt; t <= 30 * 3600; t += 97)
                {
                    c.Set(new SimulationTime(t));
                    ops.Update();
                    var mine = ops.FleetOf(ops.PlayerAirline).Single();
                    if (mine.State == FleetState.AwaitingStand)
                        ops.AssignStand(mine, ops.FreeStands().Last());
                }
            }

            Assert.That(Snapshot(resumed), Is.EqualTo(Snapshot(original)), "and 27 hours later, AI choices included");
            Assert.That(resumed.TotalEvents, Is.EqualTo(original.TotalEvents));
        }

        [Test]
        public void Restore_RejectsSavesItCannotTrust()
        {
            var (clock, ops) = MidGame();

            AirlineSaveData Fresh() => JsonUtility.FromJson<AirlineSaveData>(JsonUtility.ToJson(AirlineSave.Capture(ops)));
            ManualSimulationClock At() => new(clock.Now);

            var future = Fresh(); future.Version = 99;
            Assert.Throws<FormatException>(() => AirlineSave.Restore(future, At()));

            var badType = Fresh(); badType.Fleet[0].TypeId = "B747";
            Assert.Throws<FormatException>(() => AirlineSave.Restore(badType, At()));

            var badDestination = Fresh();
            badDestination.Fleet.First(a => a.HasScheduled || !string.IsNullOrEmpty(a.Destination)).Destination = "ZZZ";
            badDestination.Fleet.ForEach(a => { if (a.HasScheduled) a.ScheduledDestination = "ZZZ"; });
            Assert.Throws<FormatException>(() => AirlineSave.Restore(badDestination, At()));

            var noPlayer = Fresh(); noPlayer.Airlines.RemoveAll(a => a.IsPlayer);
            Assert.Throws<FormatException>(() => AirlineSave.Restore(noPlayer, At()));

            var doubleParked = Fresh();
            var parked = doubleParked.Fleet.Where(a => a.State == nameof(FleetState.AtStand)).ToList();
            if (parked.Count >= 2)
            {
                parked[1].Stand = parked[0].Stand;
                Assert.Throws<FormatException>(() => AirlineSave.Restore(doubleParked, At()));
            }

            Assert.Throws<ArgumentException>(() => AirlineSave.Restore(Fresh(), new ManualSimulationClock(new SimulationTime(5))),
                "clock must already be at the saved time");
            Assert.Throws<FormatException>(() => AirlineSave.Restore(null, At()));
        }

        [Test]
        public void SaveFile_WritesAtomicallyAndReadsBack()
        {
            var (_, ops) = MidGame();
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"airside-save-test-{Guid.NewGuid():N}.json");
            try
            {
                Assert.That(AirlineSaveFile.TryRead(path, out _, out _), Is.False, "no file yet");
                AirlineSaveFile.Write(path, AirlineSave.Capture(ops));
                AirlineSaveFile.Write(path, AirlineSave.Capture(ops));
                Assert.That(System.IO.File.Exists(path + ".tmp"), Is.False);
                Assert.That(AirlineSaveFile.TryRead(path, out var data, out var error), Is.True, error);
                Assert.That(data.Airlines.Single(a => a.IsPlayer).Name, Is.EqualTo("Gulf Air Link"));

                System.IO.File.WriteAllText(path, "{ not json");
                Assert.That(AirlineSaveFile.TryRead(path, out _, out error), Is.False);
                Assert.That(error, Is.Not.Empty);
            }
            finally
            {
                System.IO.File.Delete(path);
                System.IO.File.Delete(path + ".tmp");
            }
        }

        private static string Snapshot(AirlineOperations ops)
        {
            var text = new StringBuilder();
            text.Append(ops.ProcessedTo.ElapsedSeconds).Append(" runway ").Append(ops.RunwayFreeAt.ElapsedSeconds)
                .Append(" rng ").Append(ops.RandomState).AppendLine();
            foreach (var a in ops.Fleet)
            {
                text.Append(a.Registration).Append(' ').Append(a.Airline.Name).Append(' ').Append(a.State)
                    .Append(' ').Append(a.StateStartedAt.ElapsedSeconds).Append('-').Append(a.StateEndsAt?.ElapsedSeconds)
                    .Append(" stand ").Append(a.Stand.Value).Append(" from ").Append(a.DepartureStand.Value)
                    .Append(" to ").Append(a.CurrentDestination?.Code)
                    .Append(" next ").Append(a.Scheduled?.Destination.Code).Append('@').Append(a.Scheduled?.DepartAt.ElapsedSeconds)
                    .Append(" trips ").Append(a.CompletedTrips).AppendLine();
            }

            return text.ToString();
        }
    }
}

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

        /// <summary>A busy mid-game moment: the player away, AI traffic mid-rotation.</summary>
        private static (ManualSimulationClock clock, AirlineOperations ops) MidGame()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = AirlineOperations.StartAtAdelaide(clock, new SeededRandomSource(99), Airline.Player("Gulf Air Link", "#2E7D32"));
            var mine = ops.FleetOf(ops.PlayerAirline).Single();
            ops.ScheduleDeparture(mine, Code("KGC"), new SimulationTime(1200));
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
                    {
                        var stand = ops.SuggestStand(mine);
                        if (stand.HasValue)
                            ops.AssignStand(mine, stand.Value);
                    }
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

                Assert.That(System.IO.File.Exists(path + ".bak"), Is.True);
                System.IO.File.WriteAllText(path, "{ not json");
                Assert.That(AirlineSaveFile.TryRead(path, out _, out error, out var recovered), Is.True, error);
                Assert.That(recovered, Is.True);
                System.IO.File.WriteAllText(path + ".bak", "{ not json");
                Assert.That(AirlineSaveFile.TryRead(path, out _, out error), Is.False);
                Assert.That(error, Is.Not.Empty);
            }
            finally
            {
                System.IO.File.Delete(path);
                System.IO.File.Delete(path + ".tmp");
                System.IO.File.Delete(path + ".bak");
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SaveFile_RecoversPreviousSaveWithoutOverwritingItsBackup(bool missingPrimary)
        {
            var (_, ops) = MidGame();
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"airside-recovery-test-{Guid.NewGuid():N}.json");
            try
            {
                var previous = AirlineSave.Capture(ops);
                AirlineSaveFile.Write(path, previous);
                var current = AirlineSave.Capture(ops);
                current.Airlines.Single(a => a.IsPlayer).Name = "Latest airline";
                AirlineSaveFile.Write(path, current);
                // Rotate an existing backup too, rather than only testing its first creation.
                current.Airlines.Single(a => a.IsPlayer).Name = "Newest airline";
                AirlineSaveFile.Write(path, current);
                var backupText = System.IO.File.ReadAllText(path + ".bak");
                Assert.That(AirlineSaveFile.TryRead(path, out var healthy, out _, out var recovered), Is.True);
                Assert.That(recovered, Is.False);
                Assert.That(healthy.Airlines.Single(a => a.IsPlayer).Name, Is.EqualTo("Newest airline"));
                if (missingPrimary)
                    System.IO.File.Delete(path);
                else
                    System.IO.File.WriteAllText(path, "{ not json");

                Assert.That(AirlineSaveFile.TryRead(path, out var restored, out var error, out recovered), Is.True, error);
                Assert.That(recovered, Is.True);
                Assert.That(restored.Airlines.Single(a => a.IsPlayer).Name, Is.EqualTo("Latest airline"));
                restored.Airlines.Single(a => a.IsPlayer).Name = "Recovered airline";
                AirlineSaveFile.Write(path, restored);
                Assert.That(System.IO.File.ReadAllText(path + ".bak"), Is.EqualTo(backupText));
                Assert.That(AirlineSaveFile.TryRead(path, out var written, out _, out recovered), Is.True);
                Assert.That(recovered, Is.False);
                Assert.That(written.Airlines.Single(a => a.IsPlayer).Name, Is.EqualTo("Recovered airline"));
            }
            finally
            {
                System.IO.File.Delete(path);
                System.IO.File.Delete(path + ".tmp");
                System.IO.File.Delete(path + ".bak");
            }
        }

        [Test]
        public void VersionThreeSave_ReplacesFictionalOperatorsWithRealTraffic()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = AirlineOperations.StartAtAdelaide(clock, new SeededRandomSource(31),
                Airline.Player("Keep My Name", "#2E7D32"));
            var data = AirlineSave.Capture(ops);
            data.Version = 3;
            data.Airlines.RemoveAll(a => !a.IsPlayer);
            data.Fleet.RemoveAll(a => a.AirlineId != "PLAYER");
            data.Airlines.Add(new AirlineRecord { Id = "EMU", Name = "Legacy Regional", LiveryHex = "#A66F32" });
            data.Airlines.Add(new AirlineRecord { Id = "WTB", Name = "Legacy Jet", LiveryHex = "#2F7F86" });
            data.Fleet.Add(new AircraftRecord
            {
                Registration = "VH-EMA", AirlineId = "EMU", TypeId = "ATR42", State = nameof(FleetState.AtStand),
                Stand = "BAY-2", StateStartedAt = 0
            });
            data.Fleet.Add(new AircraftRecord
            {
                Registration = "VH-WTJ", AirlineId = "WTB", TypeId = "B38M", State = nameof(FleetState.AtStand),
                Stand = "GATE-13", StateStartedAt = 0
            });

            var restored = AirlineSave.Restore(data, new ManualSimulationClock(clock.Now));

            Assert.That(restored.PlayerAirline.Name, Is.EqualTo("Keep My Name"));
            Assert.That(restored.Airlines.Select(a => a.Name),
                Does.Contain("Rex").And.Contain("QantasLink").And.Contain("Virgin Australia")
                    .And.Contain("Qantas").And.Contain("Jetstar")
                    .And.Contain("Malaysia Airlines").And.Contain("Emirates")
                    .And.Contain("Qatar Airways").And.Contain("Fiji Airways"));
            Assert.That(restored.Airlines.Any(a => a.Id.Value is "EMU" or "WTB"), Is.False);
            Assert.That(restored.Fleet.Any(a => a.Registration is "VH-EMA" or "VH-WTJ"), Is.False);
            Assert.That(restored.Fleet.Any(a => a.Registration == "VH-8IA"), Is.True);
        }

        [Test]
        public void PlayerBase_RoundTripsAndV11InfersEnoughCapacityForExistingFleet()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = AirlineOperations.StartAtAdelaide(clock, new SeededRandomSource(73),
                Airline.Player("Base Test", "#2E7D32"));
            ops.RestoreCareerState(50_000, 95, nameof(OperatingTier.Regional), null, 0, 0,
                Array.Empty<string>(), Array.Empty<string>(), 12, baseLevel: PlayerBaseLevel.ExpandedRegional);
            Assert.That(ops.BuyAircraft(AircraftType.Atr42).Accepted, Is.True);

            var data = AirlineSave.Capture(ops);
            Assert.That(data.Version, Is.EqualTo(AirlineSaveData.CurrentVersion));
            Assert.That(data.PlayerBaseLevel, Is.EqualTo(nameof(PlayerBaseLevel.ExpandedRegional)));

            var restored = AirlineSave.Restore(data, new ManualSimulationClock(clock.Now));
            Assert.That(restored.CareerState.BaseLevel, Is.EqualTo(PlayerBaseLevel.ExpandedRegional));

            data.Version = 11;
            data.PlayerBaseLevel = string.Empty;
            var migrated = AirlineSave.Restore(data, new ManualSimulationClock(clock.Now));
            Assert.That(migrated.CareerState.Base.FleetCapacity, Is.GreaterThanOrEqualTo(2));
        }

        [Test]
        public void V11SixAircraftSave_MigratesToABaseThatCanHoldTheExistingFleet()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = new AirlineOperations(clock, new SeededRandomSource(73), DestinationCatalogue.Adelaide,
                AirlineOperations.AdelaideRegionalBays);
            ops.AddAirline(Airline.Player("Legacy Fleet", "#2E7D32"));
            ops.RestoreCareerState(200_000, 95, nameof(OperatingTier.International), null, 0, 0,
                Array.Empty<string>(), Array.Empty<string>(), 40, baseLevel: PlayerBaseLevel.International);

            var player = ops.PlayerAirline;
            var stands = AirlineOperations.AdelaideRegionalBays;
            for (var i = 0; i < PlayerBase.For(PlayerBaseLevel.International).FleetCapacity; i++)
                ops.AddAircraft(player, $"VH-LG{i}", AircraftType.Saab340, stands[i]);

            var data = AirlineSave.Capture(ops);
            data.Version = 11;
            data.PlayerBaseLevel = string.Empty;

            var restored = AirlineSave.Restore(data, new ManualSimulationClock(clock.Now));

            Assert.That(restored.FleetOf(restored.PlayerAirline).Count(),
                Is.EqualTo(PlayerBase.For(PlayerBaseLevel.International).FleetCapacity));
            Assert.That(restored.CareerState.Base.FleetCapacity,
                Is.GreaterThanOrEqualTo(restored.FleetOf(restored.PlayerAirline).Count()));
            Assert.That(restored.CareerState.BaseLevel, Is.EqualTo(PlayerBaseLevel.International));
        }

        [Test]
        public void V12UnknownPlayerBaseLevel_IsRejectedInsteadOfSilentlyInferred()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = AirlineOperations.StartAtAdelaide(clock, new SeededRandomSource(73),
                Airline.Player("Bad Base", "#2E7D32"));
            var data = AirlineSave.Capture(ops);
            data.PlayerBaseLevel = "MegaAirport";

            var ex = Assert.Throws<FormatException>(() =>
                AirlineSave.Restore(data, new ManualSimulationClock(clock.Now)));
            Assert.That(ex.Message, Does.Contain("Unknown player base level"));
        }

        [Test]
        public void AircraftLogbook_RoundTripsAndV17StartsAnHonestFreshRecord()
        {
            var clock = new ManualSimulationClock(new SimulationTime(12_345));
            var ops = AirlineOperations.StartAtAdelaide(clock, new SeededRandomSource(73),
                Airline.Player("Logbook Air", "#2E7D32"));
            var plane = ops.FleetOf(ops.PlayerAirline).Single();
            plane.CompletedTrips = 3;
            plane.RecordHistory(Code("KGC"), 900);
            plane.RecordHistory(Code("KGC"), 1_100);
            plane.RecordHistory(Code("PLO"), 1_400);

            var data = AirlineSave.Capture(ops);
            var restored = AirlineSave.Restore(data, new ManualSimulationClock(clock.Now));
            var copy = restored.FleetOf(restored.PlayerAirline).Single();
            Assert.That(copy.IsFoundingAircraft, Is.True);
            Assert.That(copy.JoinedAirlineAt.ElapsedSeconds, Is.EqualTo(12_345));
            Assert.That(copy.HistoryFlights, Is.EqualTo(3));
            Assert.That(copy.LifetimeRevenue, Is.EqualTo(3_400));
            Assert.That(copy.FavouriteRoute.DestinationCode, Is.EqualTo("KGC"));
            Assert.That(copy.FavouriteRoute.Flights, Is.EqualTo(2));

            data.Version = 17;
            var migrated = AirlineSave.Restore(data, new ManualSimulationClock(clock.Now));
            var old = migrated.FleetOf(migrated.PlayerAirline).Single();
            Assert.That(old.CompletedTrips, Is.EqualTo(3), "the established flight count is preserved");
            Assert.That(old.IsFoundingAircraft, Is.True);
            Assert.That(old.HistoryFlights, Is.Zero, "detailed history is not invented for an old save");
            Assert.That(old.LifetimeRevenue, Is.Zero);
        }

        [Test]
        public void VersionFiveSave_MigratesSingaporePlaceholderTo787WithoutLosingRotation()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = AirlineOperations.StartAtAdelaide(clock, new SeededRandomSource(73),
                Airline.Player("Keep My Airline", "#2E7D32"));
            var data = AirlineSave.Capture(ops);
            var singapore = data.Fleet.Single(a => a.AirlineId == "SIA");
            singapore.Registration = "9V-SMA";
            singapore.TypeId = "A359";
            singapore.CompletedTrips = 7;

            var restored = AirlineSave.Restore(data, new ManualSimulationClock(clock.Now));
            var migrated = restored.Fleet.Single(a => a.Airline.Id.Value == "SIA");

            Assert.That(migrated.Registration, Is.EqualTo("9V-SCA"));
            Assert.That(migrated.Type, Is.EqualTo(AircraftType.Boeing78710));
            // Compare in the save's own form: it writes "no stand / nothing booked" as "" and 0,
            // which restore reads back as none. Comparing the record to the live object only
            // held while seed 73 happened to leave the jet at a gate with a flight booked.
            var resaved = AirlineSave.Capture(restored).Fleet.Single(a => a.AirlineId == "SIA");
            Assert.That(resaved.Stand, Is.EqualTo(singapore.Stand));
            Assert.That(resaved.ScheduledDestination, Is.EqualTo(singapore.ScheduledDestination));
            Assert.That(resaved.ScheduledDepartAt, Is.EqualTo(singapore.ScheduledDepartAt));
            Assert.That(resaved.State, Is.EqualTo(singapore.State));
            Assert.That(migrated.CompletedTrips, Is.EqualTo(7));
            Assert.That(restored.AddMissingTerminalOperators(), Is.EqualTo(0), "migration must not backfill a duplicate");
            Assert.That(restored.Fleet.Any(a => a.Registration == "9V-SMA"), Is.False);
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

using System;
using System.IO;
using Airside.Persistence;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class PersistenceTests
    {
        private string _directory;
        private string _path;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "airside-tests-" + Guid.NewGuid().ToString("N"));
            _path = Path.Combine(_directory, "save.json");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_directory))
                Directory.Delete(_directory, true);
        }

        [Test]
        public void OfflineCatchUp_MatchesContinuousSimulationIncludingPlayerCommands()
        {
            const long savedWallTime = 100000;
            var continuous = PersistentAirportSession.LoadOrCreate(_path, savedWallTime, 24031996);
            continuous.AdvanceTo(80);
            Assert.That(continuous.EnablePriorityCrew(), Is.True);
            continuous.Save(savedWallTime);
            continuous.AdvanceTo(320);

            var restored = PersistentAirportSession.LoadOrCreate(_path, savedWallTime + 240, 1);

            Assert.That(restored.Clock.Now.ElapsedSeconds, Is.EqualTo(continuous.Clock.Now.ElapsedSeconds));
            Assert.That(restored.Simulation.CompletedCycles, Is.EqualTo(continuous.Simulation.CompletedCycles));
            Assert.That(restored.Simulation.ActiveAircraft.Phase, Is.EqualTo(continuous.Simulation.ActiveAircraft.Phase));
            Assert.That(restored.Simulation.AssignedStand, Is.EqualTo(continuous.Simulation.AssignedStand));
            Assert.That(restored.Simulation.Economy.Cash, Is.EqualTo(continuous.Simulation.Economy.Cash));
            Assert.That(restored.LastAwaySummary.AwaySeconds, Is.EqualTo(240));
        }

        [Test]
        public void CorruptLatestSave_RecoversPreviousCompleteSnapshot()
        {
            var session = PersistentAirportSession.LoadOrCreate(_path, 1000, 42);
            session.AdvanceTo(20);
            session.Save(1000);
            session.AdvanceTo(60);
            session.Save(1010);
            File.WriteAllText(_path, "not valid json");

            var recovered = PersistentAirportSession.LoadOrCreate(_path, 1010, 99);

            Assert.That(recovered.LastAwaySummary.RecoveredPreviousSave, Is.True);
            Assert.That(recovered.Clock.Now.ElapsedSeconds, Is.EqualTo(30));
        }

        [Test]
        public void BackwardsDeviceClock_DoesNotReverseOrAdvanceSimulation()
        {
            var session = PersistentAirportSession.LoadOrCreate(_path, 1000, 42);
            session.AdvanceTo(75);
            session.Save(1000);

            var restored = PersistentAirportSession.LoadOrCreate(_path, 900, 99);

            Assert.That(restored.Clock.Now.ElapsedSeconds, Is.EqualTo(75));
            Assert.That(restored.LastAwaySummary.ClockMovedBackwards, Is.True);
            Assert.That(restored.LastAwaySummary.AwaySeconds, Is.Zero);
        }

        [Test]
        public void SchemaOneSave_MigratesToTheCurrentSchemaWithTheDefaultLocation()
        {
            Directory.CreateDirectory(_directory);
            File.WriteAllText(_path,
                "{\"schemaVersion\":1,\"revision\":3,\"simulatedSeconds\":40," +
                "\"savedUnixSeconds\":1000,\"randomSeed\":42,\"commands\":[]}");

            var restored = PersistentAirportSession.LoadOrCreate(_path, 1000, 99);

            Assert.That(restored.Clock.Now.ElapsedSeconds, Is.EqualTo(40), "the migrated save still restores its timeline");
            Assert.That(restored.Simulation.Location, Is.EqualTo(Airside.Domain.AirportLocation.Default));

            // Saving again writes the current schema, and it reloads cleanly.
            restored.Save(1000);
            var reloaded = PersistentAirportSession.LoadOrCreate(_path, 1000, 1);
            Assert.That(reloaded.Simulation.Location, Is.EqualTo(Airside.Domain.AirportLocation.Default));
        }

        [Test]
        public void VeryLongAbsence_IsCappedAtThirtyDays()
        {
            var session = PersistentAirportSession.LoadOrCreate(_path, 1000, 42);
            session.Save(1000);

            var restored = PersistentAirportSession.LoadOrCreate(
                _path,
                1000 + PersistentAirportSession.MaximumCatchUpSeconds + 500,
                99);

            Assert.That(restored.LastAwaySummary.AwaySeconds, Is.EqualTo(PersistentAirportSession.MaximumCatchUpSeconds));
            Assert.That(restored.Clock.Now.ElapsedSeconds, Is.EqualTo(PersistentAirportSession.MaximumCatchUpSeconds));
        }
    }
}

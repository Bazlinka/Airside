using System;
using System.IO;
using Airside.Persistence;
using Airside.Simulation;
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
            TaxiLoopFixture.EnableFullTaxiLoop();
            _directory = Path.Combine(Path.GetTempPath(), "airside-tests-" + Guid.NewGuid().ToString("N"));
            _path = Path.Combine(_directory, "save.json");
        }

        [TearDown]
        public void TearDown()
        {
            TaxiLoopFixture.RestoreCircuit();
            if (Directory.Exists(_directory))
                Directory.Delete(_directory, true);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SameSecondCommands_ReplayInPlayerOrderAtStaffingLimit(bool legacyIds)
        {
            var session = PersistentAirportSession.LoadOrCreate(_path, 1000, 42);
            Assert.That(session.ReleaseGroundCrew(), Is.True);
            Assert.That(session.ReleaseGroundCrew(), Is.True);
            for (var i = 0; i < 6; i++)
                Assert.That(session.HireGroundCrew(), Is.True);
            session.Save(1000);

            if (legacyIds)
            {
                var repository = new AirsideSaveRepository(_path);
                Assert.That(repository.TryLoad(out var save, out _), Is.True);
                foreach (var command in save.commands)
                    command.commandId = command.commandType + "-1-0";
                repository.Write(save);
            }

            var restored = PersistentAirportSession.LoadOrCreate(_path, 1000, 99);
            Assert.That(restored.Simulation.Staffing.GroundCrew,
                Is.EqualTo(session.Simulation.Staffing.GroundCrew));
            Assert.That(restored.Simulation.Economy.Cash, Is.EqualTo(session.Simulation.Economy.Cash));
        }

        [Test]
        public void RepeatedSameSecondCommands_HaveDistinctPersistentIds()
        {
            var session = PersistentAirportSession.LoadOrCreate(_path, 1000, 42);
            Assert.That(session.HireGroundCrew(), Is.True);
            Assert.That(session.HireGroundCrew(), Is.True);
            session.Save(1000);

            Assert.That(new AirsideSaveRepository(_path).TryLoad(out var save, out _), Is.True);
            Assert.That(save.commands[0].commandId, Is.Not.EqualTo(save.commands[1].commandId));
            var restored = PersistentAirportSession.LoadOrCreate(_path, 1000, 99);
            Assert.That(restored.Simulation.Staffing.GroundCrew, Is.EqualTo(6));
        }

        [Test]
        public void SavingAfterRecovery_PreservesTheLastValidBackup()
        {
            var session = PersistentAirportSession.LoadOrCreate(_path, 1000, 42);
            session.AdvanceTo(20);
            session.Save(1000);
            session.AdvanceTo(60);
            session.Save(1000);
            File.WriteAllText(_path, "broken latest save");

            var recovered = PersistentAirportSession.LoadOrCreate(_path, 1000, 99);
            Assert.That(recovered.LastAwaySummary.RecoveredPreviousSave, Is.True);
            recovered.Save(1000);
            File.WriteAllText(_path, "another interrupted save");

            var recoveredAgain = PersistentAirportSession.LoadOrCreate(_path, 1000, 99);
            Assert.That(recoveredAgain.LastAwaySummary.RecoveredPreviousSave, Is.True);
            Assert.That(recoveredAgain.Clock.Now.ElapsedSeconds, Is.EqualTo(20));
        }

        [TestCase(0)]
        [TestCase(-1)]
        [TestCase(3)]
        public void UnsupportedSchema_RecoversBackupInsteadOfMigratingInvalidData(int version)
        {
            var session = PersistentAirportSession.LoadOrCreate(_path, 1000, 42);
            session.AdvanceTo(20);
            session.Save(1000);
            session.AdvanceTo(60);
            session.Save(1000);
            var invalid = File.ReadAllText(_path).Replace("\"schemaVersion\": 2", "\"schemaVersion\": " + version)
                .Replace("\"schemaVersion\":2", "\"schemaVersion\":" + version);
            File.WriteAllText(_path, invalid);

            var restored = PersistentAirportSession.LoadOrCreate(_path, 1000, 99);
            Assert.That(restored.LastAwaySummary.RecoveredPreviousSave, Is.True);
            Assert.That(restored.Clock.Now.ElapsedSeconds, Is.EqualTo(20));
        }

        [Test]
        public void CommandIssuedBeforeTheFirstTick_SurvivesReload()
        {
            const long savedWallTime = 100000;
            var session = PersistentAirportSession.LoadOrCreate(_path, savedWallTime, 24031996);

            // The player can act on the very first frame, before the clock has ticked.
            Assert.That(session.Clock.Now.ElapsedSeconds, Is.Zero);
            Assert.That(session.HireGroundCrew(), Is.True);
            var crewAfterHire = session.Simulation.Staffing.GroundCrew;
            session.Save(savedWallTime);

            var restored = PersistentAirportSession.LoadOrCreate(_path, savedWallTime, 1);

            Assert.That(restored.Simulation.Staffing.GroundCrew, Is.EqualTo(crewAfterHire),
                "a command recorded at simulation second 0 must be replayed on load");
            Assert.That(restored.Simulation.Economy.Cash, Is.EqualTo(session.Simulation.Economy.Cash));
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
        public void DeclinedRoute_StaysDeclinedAfterReload()
        {
            const long wall = 100000;
            var session = PersistentAirportSession.LoadOrCreate(_path, wall, 24031996);
            session.AdvanceTo(28);
            Assert.That(session.Simulation.Routes.Pending, Is.Not.Null);
            Assert.That(session.DeclineRoute(), Is.True);
            session.Save(wall);

            // Reload at a moment after the declined offer but before the next one.
            var restored = PersistentAirportSession.LoadOrCreate(_path, wall + 60, 1);

            Assert.That(restored.Simulation.Routes.Pending, Is.Null);
            Assert.That(restored.Simulation.Routes.OffersDeclined, Is.EqualTo(1));
            Assert.That(restored.Simulation.Routes.Accepted, Is.Empty);
        }

        [Test]
        public void AwaySummary_ReportsRouteIncomeAndNetReputationOutcome()
        {
            const long wall = 100000;
            var session = PersistentAirportSession.LoadOrCreate(_path, wall, 24031996);
            session.AdvanceTo(28);
            Assert.That(session.AcceptRoute(), Is.True);
            session.Save(wall);

            // Come back after several flights have completed while away.
            var restored = PersistentAirportSession.LoadOrCreate(_path, wall + 900, 1);
            var summary = restored.LastAwaySummary;

            Assert.That(summary.HasReport, Is.True);
            Assert.That(summary.FlightsCompleted, Is.GreaterThan(0));
            Assert.That(summary.RouteIncome, Is.GreaterThan(0), "the accepted route paid out while away");
            Assert.That(restored.Simulation.Reputation.OnTimeDepartures
                + restored.Simulation.Reputation.DelayedDepartures, Is.GreaterThan(0),
                "completed departures should be reflected in reputation history");
            Assert.That(summary.ReputationChange,
                Is.EqualTo(restored.Simulation.Reputation.Score - AirportReputation.Starting),
                "the summary should report the net result, including a legitimate zero when gains and losses cancel");
        }

        [Test]
        public void AcceptedRoute_SurvivesSaveAndOfflineCatchUp()
        {
            const long wall = 100000;
            var continuous = PersistentAirportSession.LoadOrCreate(_path, wall, 24031996);

            // First proposal is offered at second 25; accept it just after.
            continuous.AdvanceTo(28);
            Assert.That(continuous.AcceptRoute(), Is.True);
            continuous.Save(wall);
            continuous.AdvanceTo(600);

            var restored = PersistentAirportSession.LoadOrCreate(_path, wall + 572, 1);

            Assert.That(restored.Simulation.Routes.Accepted, Has.Count.EqualTo(1));
            Assert.That(restored.Simulation.Routes.IncomePerFlight,
                Is.EqualTo(continuous.Simulation.Routes.IncomePerFlight));
            Assert.That(restored.Simulation.Economy.Cash, Is.EqualTo(continuous.Simulation.Economy.Cash));
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

        [Test]
        public void NewGameWithZeroSeed_SavesWithoutThrowingAndPersistsANonZeroSeed()
        {
            // AirsideSaveData.Validate() treats randomSeed == 0 as corruption, so a
            // caller-supplied 0 must never reach disk (regression: it used to, and the
            // very next Save() would throw InvalidOperationException).
            var session = PersistentAirportSession.LoadOrCreate(_path, 1000, 0);

            Assert.DoesNotThrow(() => session.Save(1000));

            var restored = PersistentAirportSession.LoadOrCreate(_path, 1000, 0);
            Assert.That(restored.Clock.Now.ElapsedSeconds, Is.EqualTo(0));
        }
    }
}

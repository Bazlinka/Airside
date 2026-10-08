using System;
using Airside.Domain;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>
    /// Save restore rules that do not need Unity's JsonUtility, so the headless harness covers them.
    /// </summary>
    public sealed class AirlineSaveRestoreTests
    {
        [Test]
        public void Restore_InlineNullPlaceholdersKeepFreshSaveAndContinuation()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var live = AirlineOperations.StartAtAdelaide(clock, new SeededRandomSource(99),
                Airline.Player("Test Air", "#123456"));
            var data = AirlineSave.Capture(live);
            // Unity serializes inline classes by value: an absent optional record may
            // deserialize as its all-default instance, rather than a CLR null.
            data.MainWake = new RunwayWakeRecord();
            data.CrossWake = new RunwayWakeRecord();
            foreach (var record in data.Fleet)
                record.MaintenanceJob = new MaintenanceJob();
            var resumedClock = new ManualSimulationClock(clock.Now);
            var resumed = AirlineSave.Restore(data, resumedClock);
            Assert.That(resumed.MainWake, Is.Null);
            Assert.That(resumed.CrossWake, Is.Null);
            Assert.That(System.Linq.Enumerable.All(resumed.Fleet, a => a.MaintenanceJob == null), Is.True);
            for (var t = 0L; t <= 3 * 3600; t += 97)
            {
                clock.Set(new SimulationTime(t)); resumedClock.Set(clock.Now);
                live.Update(); resumed.Update();
                Assert.That(resumed.RandomState, Is.EqualTo(live.RandomState));
                Assert.That(resumed.TotalEvents, Is.EqualTo(live.TotalEvents));
                Assert.That(resumed.RunwayFreeAt, Is.EqualTo(live.RunwayFreeAt));
                Assert.That(resumed.CrossRunwayFreeAt, Is.EqualTo(live.CrossRunwayFreeAt));
                for (var i = 0; i < live.Fleet.Count; i++)
                {
                    Assert.That(resumed.Fleet[i].State, Is.EqualTo(live.Fleet[i].State));
                    Assert.That(resumed.Fleet[i].StateStartedAt, Is.EqualTo(live.Fleet[i].StateStartedAt));
                    Assert.That(resumed.Fleet[i].StateEndsAt, Is.EqualTo(live.Fleet[i].StateEndsAt));
                    Assert.That(resumed.Fleet[i].Stand, Is.EqualTo(live.Fleet[i].Stand));
                    Assert.That(resumed.Fleet[i].CompletedTrips, Is.EqualTo(live.Fleet[i].CompletedTrips));
                }
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Restore_PartialWakePlaceholderStillFails(bool departure)
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var live = AirlineOperations.StartAtAdelaide(clock, new SeededRandomSource(3),
                Airline.Player("Test Air", "#123456"));
            var data = AirlineSave.Capture(live);
            data.MainWake = new RunwayWakeRecord { Departure = departure, EventAtSeconds = departure ? 0 : 1 };
            Assert.Throws<FormatException>(() => AirlineSave.Restore(data, clock));
        }

        [TestCase("origin")]
        [TestCase("hangar")]
        [TestCase("return")]
        [TestCase("phase")]
        [TestCase("requested")]
        [TestCase("started")]
        [TestCase("ended")]
        [TestCase("repair")]
        [TestCase("completed")]
        public void Restore_PartialMaintenancePlaceholderStillFails(string field)
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var live = AirlineOperations.StartAtAdelaide(clock, new SeededRandomSource(3),
                Airline.Player("Test Air", "#123456"));
            var data = AirlineSave.Capture(live);
            var job = new MaintenanceJob();
            switch (field)
            {
                case "origin": job.OriginStand = "BAY-1"; break;
                case "hangar": job.HangarId = "shed"; break;
                case "return": job.ReturnStand = "BAY-1"; break;
                case "phase": job.Phase = MaintenancePhase.Starting; break;
                case "requested": job.RequestedAt = 1; break;
                case "started": job.PhaseStartedAt = 1; break;
                case "ended": job.PhaseEndsAt = 1; break;
                case "repair": job.RepairSeconds = 1; break;
                case "completed": job.RepairCompleted = true; break;
            }
            data.Fleet[0].MaintenanceJob = job;
            Assert.Throws<FormatException>(() => AirlineSave.Restore(data, clock));
        }

        [Test]
        public void Restore_MaintenanceStateStillRequiresAnActualJob()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var live = AirlineOperations.StartAtAdelaide(clock, new SeededRandomSource(3),
                Airline.Player("Test Air", "#123456"));
            var data = AirlineSave.Capture(live);
            data.Fleet[0].State = nameof(FleetState.Maintenance);
            data.Fleet[0].MaintenanceJob = new MaintenanceJob();
            Assert.Throws<FormatException>(() => AirlineSave.Restore(data, clock));
        }

        [Test]
        public void Restore_RejectsAMidTripAircraftWithNoDestination()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = AirlineOperations.StartAtAdelaide(clock, new SeededRandomSource(3),
                Airline.Player("Test Air", "#123456"));
            var data = AirlineSave.Capture(ops);
            var record = data.Fleet[0];
            record.State = nameof(FleetState.TakingOff);
            record.Destination = "";
            record.Stand = "";

            var error = Assert.Throws<FormatException>(() => AirlineSave.Restore(data, clock));
            Assert.That(error.Message, Does.Contain("no destination"));
        }

        [Test]
        public void Restore_StillAcceptsAParkedAircraftWithNoDestination()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = AirlineOperations.StartAtAdelaide(clock, new SeededRandomSource(3),
                Airline.Player("Test Air", "#123456"));
            var restored = AirlineSave.Restore(AirlineSave.Capture(ops), clock);
            Assert.That(restored.Fleet.Count, Is.EqualTo(ops.Fleet.Count));
            Assert.That(restored.PlayerAirline.Name, Is.EqualTo("Test Air"));
        }

        [Test]
        public void Restore_AcceptsUnityInlineNullPlaceholdersWithoutInventingJobsOrWake()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = AirlineOperations.StartAtAdelaide(clock, new SeededRandomSource(3),
                Airline.Player("Test Air", "#123456"));
            var data = AirlineSave.Capture(ops);
            foreach (var record in data.Fleet) record.MaintenanceJob = new MaintenanceJob();
            data.MainWake = new RunwayWakeRecord();
            data.CrossWake = new RunwayWakeRecord { TypeId = "" };
            var restored = AirlineSave.Restore(data, clock);
            Assert.That(restored.Fleet.Count, Is.EqualTo(ops.Fleet.Count));
            foreach (var aircraft in restored.Fleet) Assert.That(aircraft.MaintenanceJob, Is.Null);
            var again = AirlineSave.Capture(restored);
            Assert.That(again.MainWake, Is.Null);
            Assert.That(again.CrossWake, Is.Null);
        }

        [Test]
        public void Restore_RejectsPartiallyPopulatedInlineRecords()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = AirlineOperations.StartAtAdelaide(clock, new SeededRandomSource(3),
                Airline.Player("Test Air", "#123456"));
            var data = AirlineSave.Capture(ops);
            data.Fleet[0].MaintenanceJob = new MaintenanceJob { RepairSeconds = 1 };
            Assert.Throws<FormatException>(() => AirlineSave.Restore(data, clock));
            data.Fleet[0].MaintenanceJob = null;
            data.MainWake = new RunwayWakeRecord { EventAtSeconds = 1 };
            Assert.Throws<FormatException>(() => AirlineSave.Restore(data, clock));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Restore_MaintenanceStateStillRequiresARealJob(bool unityPlaceholder)
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = AirlineOperations.StartAtAdelaide(clock, new SeededRandomSource(3),
                Airline.Player("Test Air", "#123456"));
            var data = AirlineSave.Capture(ops);
            var record = data.Fleet.Find(r => r.AirlineId == ops.PlayerAirline.Id.Value);
            record.State = nameof(FleetState.Maintenance);
            record.MaintenanceJob = unityPlaceholder ? new MaintenanceJob() : null;
            var error = Assert.Throws<FormatException>(() => AirlineSave.Restore(data, clock));
            Assert.That(error.Message, Does.Contain("no saved job"));
        }

        [TestCase("99")]
        [TestCase("AtStand, TaxiOut")]
        [TestCase("atstand")]
        [TestCase("")]
        public void Restore_RejectsAStateThatIsNotADeclaredName(string state)
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = AirlineOperations.StartAtAdelaide(clock, new SeededRandomSource(3),
                Airline.Player("Test Air", "#123456"));
            var data = AirlineSave.Capture(ops);
            data.Fleet[0].State = state;

            var error = Assert.Throws<FormatException>(() => AirlineSave.Restore(data, clock));
            Assert.That(error.Message, Does.Contain("unknown state"));
        }
    }
}

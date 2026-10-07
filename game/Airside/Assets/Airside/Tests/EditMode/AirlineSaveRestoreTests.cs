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

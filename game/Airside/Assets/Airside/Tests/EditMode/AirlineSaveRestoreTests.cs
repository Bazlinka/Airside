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

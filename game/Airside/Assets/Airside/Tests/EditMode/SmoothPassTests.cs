using Airside.Domain;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>Regressions for the 2026-09-08 ground / vehicle / smoothness pass.</summary>
    public sealed class SmoothPassTests
    {
        [Test]
        public void GroundTrafficYield_PreservesExactMidLegProgressAndPosition()
        {
            var table = new ReservationTable();
            var aircraft = new GroundTrafficAircraft(
                new StableId("GT-201"), table, GroundTrafficRole.ArriveDepart, 0);
            var monitor = new TrafficWaitMonitor();

            for (var i = 0; i < 8; i++)
                aircraft.Reposition(new SimulationTime(i + 1), monitor, AirportSimulation.StandOne, 2, true);

            var before = aircraft.Progress;
            Assert.That(before, Is.GreaterThan(0.05));
            var posBefore = aircraft.Position;

            aircraft.Yield(new[] { AirportTaxiNetwork.Corridor });
            Assert.That(aircraft.Progress, Is.EqualTo(before).Within(0.0001));
            Assert.That(aircraft.Position.X, Is.EqualTo(posBefore.X).Within(0.001f));
            Assert.That(aircraft.Position.Z, Is.EqualTo(posBefore.Z).Within(0.001f));
            Assert.That(aircraft.IsHolding, Is.True);
        }
    }
}

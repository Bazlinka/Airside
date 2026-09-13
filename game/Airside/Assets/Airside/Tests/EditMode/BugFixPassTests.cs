using System.Linq;
using Airside.Domain;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>Focused regressions for the simulation/domain bugfix pass.</summary>
    public sealed class BugfixPassTests
    {
        [SetUp]
        public void SetUp() => TaxiLoopFixture.EnableFullTaxiLoop();

        [TearDown]
        public void TearDown() => TaxiLoopFixture.RestoreCircuit();

        [Test]
        public void Approach_ReservesAssignedStand()
        {
            var flight = new CommercialFlight("AS-101", new SimulationTime(0), AirportSimulation.StandTwo,
                new AirportTaxiNetwork().RoutesTo(AirportSimulation.StandTwo));
            var resources = flight.ResourcesForPhase(AircraftPhase.Approach, new SimulationTime(0)).ToArray();
            Assert.That(resources, Does.Contain(AirportSimulation.StandTwo));
            Assert.That(resources.Any(resource => resource.Equals(AirportSimulation.Runway)), Is.False);

            var landing = flight.ResourcesForPhase(AircraftPhase.Landing, new SimulationTime(0)).ToArray();
            Assert.That(landing, Does.Contain(AirportSimulation.StandTwo));
            Assert.That(landing, Does.Contain(AirportSimulation.Runway));
        }

        [Test]
        public void StableId_GetHashCode_IsNullSafeForDefault()
        {
            var id = default(StableId);
            Assert.DoesNotThrow(() => _ = id.GetHashCode());
            Assert.That(id.GetHashCode(), Is.EqualTo(0));
        }
    }
}

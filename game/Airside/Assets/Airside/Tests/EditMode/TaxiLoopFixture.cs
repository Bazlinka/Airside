using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>
    /// Production runs the bare-field circuit (no taxi). Tests that still exercise
    /// stand / taxi / turnaround restore that loop for the duration of the fixture.
    /// </summary>
    internal static class TaxiLoopFixture
    {
        public static void EnableFullTaxiLoop() => AirportCircuit.SkipGroundTaxi = false;

        public static void RestoreCircuit() => AirportCircuit.SkipGroundTaxi = true;
    }
}

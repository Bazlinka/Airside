using System.Linq;
using Airside.Domain;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class AircraftPerformanceTests
    {
        [Test]
        public void EveryFleetType_HasItsOwnTakeoffAndApproachProfile()
        {
            var types = AircraftCatalogue.All.Select(spec => spec.Type).ToArray();
            var profiles = types.Select(AircraftPerformance.For).ToArray();

            Assert.That(profiles.Select(p => p.TakeoffRollMetres).Distinct().Count(), Is.EqualTo(types.Length));
            Assert.That(profiles.Select(p => p.ApproachKnots).Distinct().Count(), Is.EqualTo(types.Length));
            Assert.That(AircraftPerformance.Boeing7378.RotateX, Is.GreaterThan(AircraftPerformance.Dash8Q400.RotateX));
            Assert.That(AircraftPerformance.Dash8Q400.RotateX, Is.GreaterThan(AircraftPerformance.Atr42.RotateX));
        }

        [Test]
        public void RunwayOccupancy_IsCalculatedFromTheAircraftActuallyUsingIt()
        {
            Assert.That(AirlineOperations.TakeoffRunwaySecondsFor(AircraftType.Boeing7378),
                Is.GreaterThan(AirlineOperations.TakeoffRunwaySecondsFor(AircraftType.Atr42)));
            Assert.That(AirlineOperations.LandingRunwaySecondsFor(AircraftType.Boeing7378),
                Is.LessThan(AirlineOperations.LandingRunwaySecondsFor(AircraftType.Saab340)));
        }

        [Test]
        public void TaxiSpeedAndCruiseCeiling_AreTypeAware()
        {
            var stand = new StableId("BAY-2");
            Assert.That(AdelaideGround.TaxiOut(stand, AircraftType.Dash8Q400).WholeSeconds,
                Is.LessThan(AdelaideGround.TaxiOut(stand, AircraftType.Saab340).WholeSeconds));
            Assert.That(EnrouteProfile.PlannedCruiseFeet(1200, AircraftType.Boeing7378),
                Is.GreaterThan(EnrouteProfile.PlannedCruiseFeet(1200, AircraftType.Atr42)));
            Assert.That(EnrouteProfile.PlannedCruiseFeet(4000, AircraftType.Dash8Q400), Is.EqualTo(25000));
        }

        [Test]
        public void TypicalSpeedsStayInCredibleOrderedBands()
        {
            foreach (var spec in AircraftCatalogue.All)
            {
                var p = AircraftPerformance.For(spec.Type);
                Assert.That(p.TouchdownKnots, Is.LessThan(p.ApproachKnots), spec.Name);
                Assert.That(p.ApproachKnots, Is.LessThan(p.ApproachEntryKnots), spec.Name);
                Assert.That(p.RotateKnots, Is.LessThan(p.InitialClimbKnots), spec.Name);
                Assert.That(p.InitialClimbKnots, Is.LessThan(p.ClimbOutKnots), spec.Name);
                Assert.That(p.RotateX, Is.LessThan(1550f), $"{spec.Name} rotates before the runway end");
            }
        }
    }
}

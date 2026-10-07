using System;
using System.Linq;
using Airside.Domain;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class RunwayCrossingsCacheTests
    {
        private static GroundLeg AcrossRunways() => new GroundLeg(new GroundLegPart(
            new GroundPath(new[] { 0f, -200f, 0f, 200f }, GroundSpeedLimits.TaxiFor(AircraftType.AirbusA350900)), false));

        [TestCase(RunwayDirection.Runway05)]
        [TestCase(RunwayDirection.Runway23)]
        [TestCase(RunwayDirection.Runway12)]
        [TestCase(RunwayDirection.Runway30)]
        public void FilteredCrossings_MatchTheFullScanAndReuseTheCachedResult(RunwayDirection runway)
        {
            var leg = new GroundLeg(new GroundLegPart(new GroundPath(new[]
            {
                AdelaideLayout.CrossRunwayCenterX, -200f, AdelaideLayout.CrossRunwayCenterX, 700f
            }, GroundSpeedLimits.TaxiFor(AircraftType.AirbusA350900)), false));
            var all = RunwayCrossings.For(leg, runway, AircraftType.AirbusA350900, includeOwnRunway: true);
            var result = RunwayCrossings.For(leg, runway, AircraftType.AirbusA350900);
            Assert.That(all.Any(c => c.MainStrip), Is.True, "fixture crosses the main strip");
            Assert.That(all.Any(c => !c.MainStrip), Is.True, "fixture crosses the cross strip");
            Assert.That(result, Is.EqualTo(all.Where(c => c.MainStrip != RunwayWeather.IsMainRunway(runway))));
            Assert.That(RunwayCrossings.For(leg, runway, AircraftType.AirbusA350900), Is.SameAs(result));
            var oppositeEnd = runway == RunwayDirection.Runway05 ? RunwayDirection.Runway23
                : runway == RunwayDirection.Runway23 ? RunwayDirection.Runway05
                : runway == RunwayDirection.Runway12 ? RunwayDirection.Runway30 : RunwayDirection.Runway12;
            Assert.That(RunwayCrossings.For(leg, oppositeEnd, AircraftType.AirbusA350900), Is.SameAs(result));
        }

        [Test]
        public void AircraftEnvelopes_KeepTheirOwnCachedCrossingTimes()
        {
            var leg = AcrossRunways();
            var centre = RunwayCrossings.For(leg, RunwayDirection.Runway12);
            var large = RunwayCrossings.For(leg, RunwayDirection.Runway12, AircraftType.AirbusA350900);
            Assert.That(large.Single(c => c.MainStrip).EnterSeconds, Is.LessThan(centre.Single(c => c.MainStrip).EnterSeconds));
            Assert.That(large.Single(c => c.MainStrip).ExitSeconds, Is.GreaterThan(centre.Single(c => c.MainStrip).ExitSeconds));
            Assert.That(RunwayCrossings.For(leg, RunwayDirection.Runway12), Is.SameAs(centre));
        }

        [Test]
        public void IndexedCatalogueLookup_PreservesEveryFixedWingAndRotorcraftMatch()
        {
            foreach (var expected in AircraftCatalogue.All.Concat(AircraftCatalogue.Rotorcraft))
            {
                Assert.That(AircraftCatalogue.TryFor(expected.Type, out var actual), Is.True);
                Assert.That(actual, Is.SameAs(expected));
            }
            Assert.That(AircraftCatalogue.TryFor(null, out var missing), Is.False);
            Assert.That(missing, Is.Null);
        }

#if NET8_0
        [Test]
        public void RepeatedWarmQueries_AllocateNoFilteredArrays()
        {
            var leg = AcrossRunways();
            for (var i = 0; i < 100; i++)
                RunwayCrossings.For(leg, RunwayDirection.Runway05, AircraftType.AirbusA350900);
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 10000; i++)
                RunwayCrossings.For(leg, RunwayDirection.Runway05, AircraftType.AirbusA350900);
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            TestContext.WriteLine($"10,000 warm crossing queries allocated {allocated} bytes");
            Assert.That(allocated, Is.Zero);
        }
#endif
    }
}

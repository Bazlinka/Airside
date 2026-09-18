using System.Linq;
using Airside.Domain;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class AircraftPerformanceTests
    {
        [Test]
        public void CatalogueIds_AreAllUnique()
        {
            // AircraftType.TryFromId and AircraftCatalogue.TryFor both used to scan every
            // entry without returning on the first match, silently keeping the *last*
            // match instead of the first - harmless only as long as every id really is
            // unique. Both now return immediately, but that only matters if this
            // invariant ever breaks, so it is worth locking in directly.
            var ids = AircraftCatalogue.All.Select(spec => spec.Id).ToList();
            Assert.That(ids.Distinct().Count(), Is.EqualTo(ids.Count));
        }

        [Test]
        public void TryFromId_ReturnsTheMatchingTypeForEveryCatalogueEntry()
        {
            foreach (var spec in AircraftCatalogue.All)
            {
                Assert.That(AircraftType.TryFromId(spec.Id, out var type), Is.True);
                Assert.That(type, Is.SameAs(spec.Type));
            }

            Assert.That(AircraftType.TryFromId("NOPE", out _), Is.False);
        }

        [Test]
        public void EveryFleetType_HasItsOwnTakeoffAndApproachProfile()
        {
            var types = AircraftCatalogue.All.Select(spec => spec.Type).ToArray();
            var profiles = types.Select(AircraftPerformance.For).ToArray();

            Assert.That(profiles.Select(p => p.TakeoffRollMetres).Distinct().Count(), Is.EqualTo(types.Length));
            Assert.That(profiles.Select(p => p.ApproachKnots).Distinct().Count(), Is.EqualTo(types.Length));
            Assert.That(AircraftPerformance.Boeing7378.RotateX, Is.GreaterThan(AircraftPerformance.Dash8Q400.RotateX));
            Assert.That(AircraftPerformance.Dash8Q400.RotateX, Is.GreaterThan(AircraftPerformance.Atr42.RotateX));
            Assert.That(AircraftPerformance.Boeing7378.RunwayExitKnots,
                Is.GreaterThan(AircraftPerformance.Atr42.RunwayExitKnots));
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
            Assert.That(AdelaideGround.VacateFor(AircraftType.Boeing7378).PoseAt(0).Speed,
                Is.EqualTo(CircuitProfile.Knots(AircraftPerformance.Boeing7378.RunwayExitKnots)).Within(0.15f));
            Assert.That(EnrouteProfile.PlannedCruiseFeet(1200, AircraftType.Boeing7378),
                Is.GreaterThan(EnrouteProfile.PlannedCruiseFeet(1200, AircraftType.Atr42)));
            Assert.That(EnrouteProfile.PlannedCruiseFeet(4000, AircraftType.Dash8Q400), Is.EqualTo(25000));
        }

        [Test]
        public void GateTaxi_SteersEachJetsMainGearByItsOwnWheelbaseNotAFlatConstant()
        {
            // AdelaideGround.GateTaxiOut/In used to steer every jet's main gear through
            // turns with one flat 19 m constant (the 737's own figure) regardless of type,
            // so a widebody with a materially longer wheelbase tracked a corner as if it
            // had a narrowbody's gear geometry. Each jet now supplies its own
            // AircraftPerformanceProfile.NoseToMainGearMetres.
            var gate = new StableId("GATE-13");
            var jetTypes = new[]
            {
                AircraftType.Boeing7378, AircraftType.AirbusA321Neo,
                AircraftType.AirbusA350900, AircraftType.Boeing78710
            };
            foreach (var type in jetTypes)
            {
                var expected = AircraftPerformance.For(type).NoseToMainGearMetres;
                Assert.That(expected, Is.GreaterThan(0f), $"{type.Id} should have a real wheelbase");

                var outLeg = AdelaideGround.TaxiOut(gate, type);
                Assert.That(outLeg.Parts[outLeg.Parts.Count - 1].TrackMetres, Is.EqualTo(expected),
                    $"{type.Id} taxi-out should steer its main gear by its own wheelbase");

                var inLeg = AdelaideGround.TaxiIn(gate, type);
                Assert.That(inLeg.Parts[0].TrackMetres, Is.EqualTo(expected),
                    $"{type.Id} taxi-in should steer its main gear by its own wheelbase");
            }

            var wheelbases = jetTypes.Select(type => AircraftPerformance.For(type).NoseToMainGearMetres).ToArray();
            Assert.That(wheelbases.Distinct().Count(), Is.EqualTo(wheelbases.Length),
                "the four jets must not all share one flat wheelbase any more");
        }

        [Test]
        public void PlannedCruiseFeet_UsesTheJetFormulaForEveryAuthoredJetNotJustThe737()
        {
            // A321neo, A350-900 and 787-10 used to fall through to the turboprop climb
            // formula (6000 ft base, 25 ft/km) because only "B38M" was checked, planning a
            // widebody international service at roughly turboprop altitude on a medium leg
            // instead of the ~33,000 ft a jet formula gives - directly visible in the HUD's
            // "FLxxx" cruise readout.
            var mediumLegKm = 650.0;
            var turboprop = EnrouteProfile.PlannedCruiseFeet(mediumLegKm, AircraftType.Atr42);
            foreach (var jetType in new[]
                     {
                         AircraftType.Boeing7378, AircraftType.AirbusA321Neo,
                         AircraftType.AirbusA350900, AircraftType.Boeing78710
                     })
            {
                Assert.That(EnrouteProfile.PlannedCruiseFeet(mediumLegKm, jetType), Is.GreaterThan(turboprop),
                    $"{jetType.Id} should plan well above a turboprop's cruise level");
            }
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

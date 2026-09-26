using System;
using System.IO;
using System.Linq;
using Airside.Domain;
using Airside.Presentation;
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
        public void EveryGroundLeg_SteersTheRuntimeMainGearForEveryAircraft()
        {
            var mainRunways = new[] { RunwayDirection.Runway05, RunwayDirection.Runway23 };
            var crossRunways = new[] { RunwayDirection.Runway12, RunwayDirection.Runway30 };
            foreach (var spec in AircraftCatalogue.All)
            {
                var type = spec.Type;
                var expected = AircraftPerformance.For(type).NoseToMainGearMetres;
                Assert.That(expected, Is.GreaterThan(0f), $"{type.Id} should have measured gear geometry");

                foreach (var runway in mainRunways)
                {
                    AssertTracked(AdelaideGround.VacateFor(type, runway), expected, $"{type.Id} vacate {runway}");
                    AssertTracked(AdelaideGround.LineupFor(runway, type), expected, $"{type.Id} lineup {runway}");
                }
                if (spec.StandClass == StandClass.RegionalBay)
                    foreach (var runway in crossRunways)
                    {
                        AssertTracked(AdelaideGround.VacateFor(type, runway), expected, $"{type.Id} vacate {runway}");
                        AssertTracked(AdelaideGround.LineupFor(runway, type), expected, $"{type.Id} lineup {runway}");
                    }

                foreach (var stand in AirlineOperations.AdelaideStands)
                {
                    if (!AirlineOperations.StandFits(type, stand))
                        continue;
                    AssertTracked(AdelaideGround.TaxiIn(stand, type), expected, $"{type.Id} taxi-in {stand}");
                    foreach (var runway in mainRunways)
                        AssertTracked(AdelaideGround.TaxiOut(stand, type, runway), expected,
                            $"{type.Id} taxi-out {stand} {runway}");
                    if (spec.StandClass == StandClass.RegionalBay)
                        foreach (var runway in crossRunways)
                        {
                            AssertTracked(AdelaideGround.TaxiIn(stand, type, runway), expected,
                                $"{type.Id} taxi-in {stand} {runway}");
                            AssertTracked(AdelaideGround.TaxiOut(stand, type, runway), expected,
                                $"{type.Id} taxi-out {stand} {runway}");
                        }
                }
            }
        }

        private static void AssertTracked(GroundLeg leg, float expected, string label)
        {
            foreach (var part in leg.Parts)
                Assert.That(part.TrackMetres, Is.EqualTo(expected), $"{label} part should steer the visible main gear");
        }

        [Test]
        public void EveryTaxiWheelbase_MatchesTheShippingRuntimeGearGeometry()
        {
            foreach (var spec in AircraftCatalogue.All)
            {
                var path = ArtRuntimePaths.ResolveExisting(spec.RuntimeModelPath);
                Assert.That(path, Is.Not.Null, $"{spec.Id} runtime model must exist");
                var json = File.ReadAllText(path);
                Assert.That(AircraftModelBounds.TryMeasurePart(json, "gear_nose", out var noseMin, out var noseMax),
                    Is.True, $"{spec.Id} nose gear must be measurable");
                Assert.That(AircraftModelBounds.TryMeasurePart(json, "gear_left", out var leftMin, out var leftMax),
                    Is.True, $"{spec.Id} left main gear must be measurable");
                Assert.That(AircraftModelBounds.TryMeasurePart(json, "gear_right", out var rightMin, out var rightMax),
                    Is.True, $"{spec.Id} right main gear must be measurable");

                var noseZ = (noseMin.z + noseMax.z) * 0.5f;
                var leftZ = (leftMin.z + leftMax.z) * 0.5f;
                var rightZ = (rightMin.z + rightMax.z) * 0.5f;
                Assert.That(leftZ, Is.EqualTo(rightZ).Within(0.02f),
                    $"{spec.Id} main gear should share one longitudinal pivot");
                var modelWheelbase = Math.Abs(noseZ - (leftZ + rightZ) * 0.5f);
                Assert.That(AircraftPerformance.For(spec.Type).NoseToMainGearMetres,
                    Is.EqualTo(modelWheelbase).Within(0.02f),
                    $"{spec.Id} taxi solver must match its {modelWheelbase:0.00} m visible gear spacing");
            }
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
            foreach (var jetType in AircraftCatalogue.All
                         .Where(spec => spec.StandClass == StandClass.TerminalGate)
                         .Select(spec => spec.Type))
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

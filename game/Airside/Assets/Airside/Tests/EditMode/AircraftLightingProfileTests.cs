using System.Linq;
using Airside.Domain;
using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>Each aircraft type is lit the way its kind of airframe is, and the old fleet-wide behaviour is the fallback.</summary>
    public sealed class AircraftLightingProfileTests
    {
        private static readonly AircraftType[] Fixed =
        {
            AircraftType.Atr42, AircraftType.Saab340, AircraftType.Dash8Q400, AircraftType.EmbraerE190,
            AircraftType.AirbusA220300, AircraftType.AirbusA320200, AircraftType.Boeing737800, AircraftType.Boeing7378,
            AircraftType.AirbusA321Neo, AircraftType.AirbusA350900, AircraftType.Boeing78710, AircraftType.AirbusA330900,
            AircraftType.Boeing7879
        };

        [Test]
        public void EveryCataloguedType_HasAKnownFamily()
        {
            foreach (var spec in AircraftCatalogue.All.Concat(AircraftCatalogue.Rotorcraft))
                Assert.That(AircraftLightingProfile.FamilyOf(spec.Type), Is.Not.EqualTo(AircraftLightingFamily.Generic),
                    spec.Id + " has no lighting family");
        }

        [Test]
        public void UnknownOrMissingType_GetsTheFallbackLamps()
        {
            var generic = AircraftLightingProfile.For(null);
            Assert.That(generic.LandingRange, Is.EqualTo(90f));
            Assert.That(generic.LandingSpotAngle, Is.EqualTo(24f));
            Assert.That(generic.StrobeFlashes, Is.EqualTo(2));
            Assert.That(generic.StrobeCycleSeconds, Is.EqualTo(1.2f));
            Assert.That(generic.BeaconHz, Is.EqualTo(1.4f));
            Assert.That(generic.TailStrobe, Is.False);
            Assert.That(AircraftLightingProfile.For(new AircraftType("XXXX", "Mystery", 500, 1000)),
                Is.SameAs(AircraftLightingProfile.Generic));
        }

        [Test]
        public void Jets_ThrowANarrowerLongerLandingBeamThanTurboprops()
        {
            var turboprop = AircraftLightingProfile.For(AircraftType.Atr42);
            var narrow = AircraftLightingProfile.For(AircraftType.AirbusA320200);
            var wide = AircraftLightingProfile.For(AircraftType.Boeing7879);
            Assert.That(narrow.LandingSpotAngle, Is.LessThan(turboprop.LandingSpotAngle));
            Assert.That(wide.LandingSpotAngle, Is.LessThan(narrow.LandingSpotAngle));
            Assert.That(narrow.LandingRange, Is.GreaterThan(turboprop.LandingRange));
            Assert.That(wide.LandingRange, Is.GreaterThan(narrow.LandingRange));
            Assert.That(wide.TaxiRange, Is.GreaterThan(turboprop.TaxiRange));
        }

        [Test]
        public void EveryProfile_HasAConsistentBeam()
        {
            foreach (var type in Fixed.Concat(new[] { AircraftType.Bell412 }))
            {
                var p = AircraftLightingProfile.For(type);
                Assert.That(p.LandingInnerAngle, Is.LessThan(p.LandingSpotAngle), type.Id);
                Assert.That(p.TaxiInnerAngle, Is.LessThan(p.TaxiSpotAngle), type.Id);
                Assert.That(p.LandingPitchDownDegrees, Is.InRange(0f, 30f), type.Id);
                Assert.That(p.LandingToeOutDegrees, Is.InRange(0f, 6f), type.Id);
                Assert.That(p.BeaconHz, Is.InRange(40f / 60f, 100f / 60f), type.Id + " beacon outside 40-100 flashes a minute");
                Assert.That(p.StrobeFlashSeconds * p.StrobeFlashes, Is.LessThan(p.StrobeCycleSeconds), type.Id);
            }
        }

        [Test]
        public void TailStrobe_OnTheJetsOnly()
        {
            foreach (var type in Fixed)
            {
                var jet = AircraftLightingProfile.FamilyOf(type) != AircraftLightingFamily.Turboprop;
                Assert.That(AircraftLightingProfile.For(type).TailStrobe, Is.EqualTo(jet), type.Id);
            }
            Assert.That(AircraftLightingProfile.For(AircraftType.Bell412).TailStrobe, Is.False);
        }

        [Test]
        public void StrobePattern_DistinguishesBoeingFromAirbus()
        {
            Assert.That(FlashesPerCycle(AircraftLightingProfile.For(AircraftType.Saab340)), Is.EqualTo(1));
            Assert.That(FlashesPerCycle(AircraftLightingProfile.For(AircraftType.AirbusA320200)), Is.EqualTo(2));
            Assert.That(FlashesPerCycle(AircraftLightingProfile.For(AircraftType.Boeing78710)), Is.EqualTo(1));
            Assert.That(FlashesPerCycle(AircraftLightingProfile.For(null)), Is.EqualTo(2));
        }

        [Test]
        public void GenericStrobe_MatchesTheOriginalTimings()
        {
            var generic = AircraftLightingProfile.For(null);
            Assert.That(generic.StrobeLevel(0.02f), Is.EqualTo(1f));
            Assert.That(generic.StrobeLevel(0.10f), Is.Zero);
            Assert.That(generic.StrobeLevel(0.18f), Is.EqualTo(1f));
            Assert.That(generic.StrobeLevel(1.2f + 0.02f), Is.EqualTo(1f), "repeats each cycle");
        }

        [Test]
        public void Beacon_IsOffUnlessCommandedAndPulsesAtTheTypesRate()
        {
            var p = AircraftLightingProfile.For(AircraftType.AirbusA350900);
            Assert.That(p.BeaconLevel(false, 0.2f), Is.Zero);
            var peak = 0.25f / p.BeaconHz;
            Assert.That(p.BeaconLevel(true, peak), Is.EqualTo(1f).Within(0.001f));
            Assert.That(p.BeaconLevel(true, 0.75f / p.BeaconHz), Is.Zero, "dark half of the cycle");
        }

        [Test]
        public void TaxiLamp_LitWhileTaxiingForwardUnderPower_OnEveryType()
        {
            foreach (var type in Fixed)
            {
                var p = AircraftLightingProfile.For(type);
                Assert.That(p.TaxiLampOn(AircraftPhase.TaxiOut, false, true, true), Is.True, type.Id);
                Assert.That(p.TaxiLampOn(AircraftPhase.TaxiIn, false, true, true), Is.True, type.Id);
                Assert.That(p.TaxiLampOn(AircraftPhase.Pushback, false, true, false), Is.False, type.Id + " tail-first push");
                Assert.That(p.TaxiLampOn(AircraftPhase.TaxiOut, false, true, false), Is.False, type.Id + " stopped");
                Assert.That(p.TaxiLampOn(AircraftPhase.TaxiOut, false, false, true), Is.False, type.Id + " engines off");
                Assert.That(p.TaxiLampOn(AircraftPhase.AtStand, false, true, true), Is.False, type.Id + " at stand");
            }
        }

        [Test]
        public void NoseLamp_DoublesAsTheTakeoffLightOnJetsOnly()
        {
            var jet = AircraftLightingProfile.For(AircraftType.Boeing737800);
            Assert.That(jet.TaxiLampOn(AircraftPhase.Takeoff, airborne: false, enginesOn: true, movingForwardOnGround: true), Is.True);
            Assert.That(jet.TaxiLampOn(AircraftPhase.Takeoff, airborne: true, enginesOn: true, movingForwardOnGround: true), Is.False,
                "gear is coming up");
            Assert.That(jet.TaxiLampOn(AircraftPhase.Landing, airborne: false, enginesOn: true, movingForwardOnGround: true), Is.True);
            Assert.That(jet.TaxiLampOn(AircraftPhase.Departed, airborne: true, enginesOn: true, movingForwardOnGround: true), Is.False);

            var turboprop = AircraftLightingProfile.For(AircraftType.Atr42);
            Assert.That(turboprop.TaxiLampOn(AircraftPhase.Takeoff, false, true, true), Is.False);
            Assert.That(turboprop.TaxiLampOn(AircraftPhase.Landing, false, true, true), Is.False);
        }

        [Test]
        public void AircraftFlashOffset_IsStableAndIndependent()
        {
            Assert.That(AircraftLightingProfile.ClockOffsetSeconds("Commercial VH-A01"),
                Is.EqualTo(AircraftLightingProfile.ClockOffsetSeconds("Commercial VH-A01")));
            Assert.That(AircraftLightingProfile.ClockOffsetSeconds("Commercial VH-A01"),
                Is.Not.EqualTo(AircraftLightingProfile.ClockOffsetSeconds("Commercial VH-A02")));
            Assert.That(AircraftLightingProfile.ClockOffsetSeconds(null), Is.Zero);
        }

        [Test]
        public void LandingLamps_GoDarkInCruiseAndReturnBelowTheCeiling()
        {
            foreach (var type in Fixed.Concat(new[] { AircraftType.Bell412 }))
            {
                var p = AircraftLightingProfile.For(type);
                foreach (var phase in new[] { AircraftPhase.Departed, AircraftPhase.Circuit, AircraftPhase.Approach })
                {
                    Assert.That(p.LandingLampOn(phase, 3047f), Is.True, type.Id);
                    Assert.That(p.LandingLampOn(phase, 3048f), Is.False, type.Id);
                    Assert.That(p.LandingLampOn(phase, 10000f), Is.False, type.Id);
                }
                Assert.That(p.LandingLampOn(AircraftPhase.AtStand, 0f), Is.False, type.Id);
                Assert.That(p.LandingLampOn(AircraftPhase.TaxiOut, 0f), Is.False, type.Id);
                Assert.That(p.LandingLampOn(AircraftPhase.Takeoff, 0f), Is.True, type.Id);
                Assert.That(p.LandingLampOn(AircraftPhase.GoAround, 100f), Is.True, type.Id);
                Assert.That(p.TaxiLampOn(AircraftPhase.Landing, false, false, true), Is.False, type.Id);
            }
        }

        [TestCase(AircraftNavigationLight.Left, -90f, 1f)]
        [TestCase(AircraftNavigationLight.Left, 90f, 0f)]
        [TestCase(AircraftNavigationLight.Right, 90f, 1f)]
        [TestCase(AircraftNavigationLight.Right, -90f, 0f)]
        [TestCase(AircraftNavigationLight.Tail, 180f, 1f)]
        [TestCase(AircraftNavigationLight.Tail, 0f, 0f)]
        [TestCase(AircraftNavigationLight.Left, -180f, 0f)]
        [TestCase(AircraftNavigationLight.Right, 450f, 1f)]
        public void NavigationLenses_RespectTheirHorizontalSector(AircraftNavigationLight kind, float bearing, float expected)
        {
            Assert.That(AircraftLightingProfile.NavigationVisibility(kind, bearing), Is.EqualTo(expected));
        }

        // Wing-root landing lamps and the nose-gear lamp sit about this high on the airliners (metres).
        private static float LandingLampHeight(AircraftLightingFamily f) => f switch
        {
            AircraftLightingFamily.Widebody => 4.5f,
            AircraftLightingFamily.Turboprop => 2.6f,
            AircraftLightingFamily.Helicopter => 2.0f,
            _ => 3.2f
        };

        [Test]
        public void LandingBeam_MiddleLandsOnTheGroundAheadAndInsideTheLampRange()
        {
            foreach (var type in Fixed.Concat(new[] { AircraftType.Bell412 }))
            {
                var p = AircraftLightingProfile.For(type);
                var hit = p.LandingAimGroundHitMetres(LandingLampHeight(p.Family));
                Assert.That(hit, Is.GreaterThan(3f), type.Id + " beam would land under the nose");
                Assert.That(hit, Is.LessThan(p.LandingRange * 0.9f), type.Id + " beam axis never reaches the ground");
            }
        }

        [Test]
        public void TaxiBeam_MiddleLandsOnTheApronJustAhead()
        {
            foreach (var type in Fixed)
            {
                var p = AircraftLightingProfile.For(type);
                var hit = p.TaxiAimGroundHitMetres(1.6f);
                Assert.That(hit, Is.InRange(10f, p.TaxiRange * 0.8f), type.Id);
            }
        }

        [Test]
        public void AimGroundHit_IsInfiniteWhenNotAimedDown()
        {
            Assert.That(float.IsPositiveInfinity(AircraftLightingProfile.AimGroundHitMetres(3f, 0f)));
            Assert.That(AircraftLightingProfile.AimGroundHitMetres(3f, 3f), Is.EqualTo(57.2f).Within(0.2f));
        }

        private static int FlashesPerCycle(AircraftLightingProfile p)
        {
            var flashes = 0;
            var lit = false;
            for (var t = 0f; t < p.StrobeCycleSeconds; t += 0.005f)
            {
                var on = p.StrobeLevel(t) > 0.5f;
                if (on && !lit)
                    flashes++;
                lit = on;
            }
            return flashes;
        }
    }
}

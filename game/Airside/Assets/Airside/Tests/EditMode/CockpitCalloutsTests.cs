using System.Collections.Generic;
using Airside.Presentation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class CockpitCalloutsTests
    {
        private static List<string> Takeoff(bool jet)
        {
            var c = new CockpitCallouts { DeltaSeconds = 0.1f }; c.Reset();
            var calls = new List<string>();
            void Go(float knots, float feet, float fpm)
            {
                var call = c.Step(new CockpitCallouts.Sample { GroundKnots = knots, RotateKnots = 145f, HeightFeet = feet,
                    VerticalFeetPerMinute = fpm, Jet = jet });
                if (call != null) calls.Add(call);
            }
            for (var k = 0f; k <= 150f; k += 0.5f) Go(k, 0f, 0f);
            for (var h = 0f; h <= 400f; h += 2f) Go(150f + h * 0.1f, h, 2500f);
            return calls;
        }

        [Test] public void TakeoffCallsComeInOrderOnce()
        {
            Assert.That(Takeoff(true), Is.EqualTo(new[] { "80 KNOTS", "V1", "ROTATE", "POSITIVE RATE", "GEAR UP" }));
        }

        [Test] public void ApproachAndLandingCallsComeInOrderOnce()
        {
            var c = new CockpitCallouts { DeltaSeconds = 0.1f }; c.Reset();
            var calls = new List<string>();
            void Go(float knots, float feet, float fpm)
            {
                var call = c.Step(new CockpitCallouts.Sample { GroundKnots = knots, RotateKnots = 145f, HeightFeet = feet,
                    VerticalFeetPerMinute = fpm, Jet = true });
                if (call != null) calls.Add(call);
            }
            for (var h = 2500f; h > 0.5f; h -= 4f) Go(140f, h, -700f);
            Go(135f, 0f, 0f);                                    // touchdown
            for (var i = 0; i < 20; i++) Go(135f - i * 2f, 0f, 0f);
            for (var k = 100f; k > 60f; k -= 1f) Go(k, 0f, 0f);
            Assert.That(calls, Is.EqualTo(new[] { "1,000", "500", "100 ABOVE", "50", "40", "30", "20 RETARD", "10",
                "SPOILERS", "REVERSE GREEN", "80 KNOTS" }));
        }

        [Test] public void ShallowFlareStillGetsEveryLowCall()
        {
            var c = new CockpitCallouts { DeltaSeconds = 0.1f }; c.Reset();
            var calls = new List<string>();
            for (var h = 1500f; h > 0.5f; h -= h > 60f ? 4f : 0.5f)
            {
                var x = c.Step(new CockpitCallouts.Sample { GroundKnots = 140f, RotateKnots = 145f, HeightFeet = h,
                    VerticalFeetPerMinute = h > 60f ? -700f : -90f, Jet = true });
                if (x != null) calls.Add(x);
            }
            Assert.That(calls, Is.EqualTo(new[] { "1,000", "500", "100 ABOVE", "50", "40", "30", "20 RETARD", "10" }));
        }

        [Test] public void GoAroundResetsApproachCalls()
        {
            var c = new CockpitCallouts { DeltaSeconds = 0.1f }; c.Reset();
            var calls = new List<string>();
            void Go(float feet, float fpm) { var x = c.Step(new CockpitCallouts.Sample { GroundKnots = 140f, RotateKnots = 145f,
                HeightFeet = feet, VerticalFeetPerMinute = fpm, Jet = true }); if (x != null) calls.Add(x); }
            for (var h = 2000f; h > 150f; h -= 4f) Go(h, -700f);
            for (var h = 150f; h < 2200f; h += 8f) Go(h, 2000f);          // go-around
            for (var h = 2200f; h > 900f; h -= 4f) Go(h, -700f);          // second approach
            Assert.That(calls.FindAll(x => x == "1,000").Count, Is.EqualTo(2));
            Assert.That(calls.FindAll(x => x == "500").Count, Is.EqualTo(1).Or.EqualTo(2));
        }

        [Test] public void TouchAndGoGetsTakeoffCallsAgain()
        {
            var c = new CockpitCallouts { DeltaSeconds = 0.1f }; c.Reset();
            var calls = new List<string>();
            void Go(float knots, float feet, float fpm) { var x = c.Step(new CockpitCallouts.Sample { GroundKnots = knots,
                RotateKnots = 145f, HeightFeet = feet, VerticalFeetPerMinute = fpm, Jet = true }); if (x != null) calls.Add(x); }
            for (var h = 1500f; h > 0.5f; h -= 4f) Go(140f, h, -700f);
            for (var i = 0; i < 20; i++) Go(135f - i, 0f, 0f);             // touchdown and brief rollout
            for (var k = 116f; k <= 150f; k += 0.5f) Go(k, 0f, 0f);        // power up again
            for (var h = 0f; h <= 200f; h += 2f) Go(150f, h, 2500f);
            Assert.That(calls, Does.Contain("V1"));
            Assert.That(calls, Does.Contain("ROTATE"));
            Assert.That(calls, Does.Contain("POSITIVE RATE"));
        }

        [Test] public void NothingSaidOnTheTaxiOrInCruise()
        {
            var c = new CockpitCallouts(); c.Reset();
            for (var i = 0; i < 600; i++)
                Assert.That(c.Step(new CockpitCallouts.Sample { GroundKnots = 15f, RotateKnots = 145f, HeightFeet = 0f }), Is.Null);
            c.Reset();
            for (var i = 0; i < 600; i++)
                Assert.That(c.Step(new CockpitCallouts.Sample { GroundKnots = 300f, RotateKnots = 145f, HeightFeet = 30000f }), Is.Null);
        }

        [Test] public void TurbopropsSkipJetOnlyCalls()
        {
            Assert.That(Takeoff(false), Does.Not.Contain("SPOILERS"));
            var c = new CockpitCallouts { DeltaSeconds = 0.1f }; c.Reset();
            var calls = new List<string>();
            for (var h = 2000f; h > 0.5f; h -= 4f)
                { var x = c.Step(new CockpitCallouts.Sample { GroundKnots = 110f, RotateKnots = 104f, HeightFeet = h, VerticalFeetPerMinute = -600f }); if (x != null) calls.Add(x); }
            Assert.That(calls, Does.Contain("20"));
            Assert.That(calls, Does.Not.Contain("20 RETARD"));
        }
    }
}
namespace Airside.Tests
{
    public sealed class CockpitLookPresetsTests
    {
        [Test] public void EveryGlanceIsInsideTheLookLimits()
        {
            foreach (var (name, yaw, pitch) in CockpitLookPresets.All)
            {
                Assert.That(System.Math.Abs(yaw), Is.LessThanOrEqualTo(CockpitLookPresets.MaxYaw), name);
                Assert.That(pitch, Is.InRange(CockpitLookPresets.MinPitch, CockpitLookPresets.MaxPitch), name);
            }
            Assert.That(CockpitLookPresets.All.Length, Is.EqualTo(5));
        }

        [Test] public void EaseApproachesTheTargetWithoutOvershootAtAnyFrameRate()
        {
            foreach (var dt in new[] { 1f / 30f, 1f / 60f, 1f / 144f })
            {
                var value = 0f;
                for (var i = 0; i < (int)(1.5f / dt); i++)
                {
                    value = CockpitLookPresets.Ease(value, 78f, 7f, dt);
                    Assert.That(value, Is.LessThanOrEqualTo(78f));
                }
                Assert.That(value, Is.EqualTo(78f).Within(0.5f));
            }
        }
    }
}
namespace Airside.Tests
{
    public sealed class AtrAttitudeParityTests
    {
        [Test] public void AtrProfileProgressMatchesTheReferenceCircuit()
        {
            Assert.That(Airside.Simulation.AircraftPerformance.Atr42.RotateProgress,
                Is.EqualTo(Airside.Simulation.CircuitProfile.RotateProgress).Within(1e-4f));
            Assert.That(Airside.Simulation.AircraftPerformance.Atr42.FlareProgress,
                Is.EqualTo(Airside.Simulation.CircuitProfile.FlareProgress).Within(1e-4f));
            Assert.That(Airside.Simulation.AircraftPerformance.Atr42.TouchdownProgress,
                Is.EqualTo(Airside.Simulation.CircuitProfile.TouchdownProgress).Within(1e-4f));
        }
    }
}

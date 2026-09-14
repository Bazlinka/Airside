using System;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>
    /// Locks the flown circuit to real ATR 42 figures.
    ///
    /// Phase durations used to be hand-picked and the speeds fell out of them, which
    /// is how the takeoff roll came to pass rotate at 179 kt and leave the field at
    /// 257 kt without anything failing. These tests assert the speeds directly, so a
    /// duration can no longer drift away from the performance it is supposed to
    /// represent.
    /// </summary>
    public sealed class CircuitProfileTests
    {
        private const float Kt = CircuitProfile.KnotsToMetresPerSecond;

        [Test]
        public void Glideslope_IsThreeDegrees()
        {
            Assert.That(CircuitProfile.GlideslopeDegrees, Is.EqualTo(3f));
            // tan 3° to four places.
            Assert.That(CircuitProfile.GlideslopeTangent, Is.EqualTo(0.05241f).Within(0.0002f));

            // Height must fall linearly at that gradient all the way down the slope.
            foreach (var x in new[] { -4200f, -3000f, -2000f, -1600f })
            {
                var expected = (CircuitProfile.AimPointX - x) * CircuitProfile.GlideslopeTangent;
                Assert.That(CircuitProfile.GlideslopeHeight(x), Is.EqualTo(expected).Within(0.01f),
                    $"off the glideslope at x={x}");
            }
        }

        [Test]
        public void ApproachDescentRate_MatchesThreeDegreesAtVapp()
        {
            // 110 kt on 3° is about 584 ft/min.
            var feetPerMinute = CircuitProfile.ApproachDescentRate * 196.85f;
            Assert.That(feetPerMinute, Is.EqualTo(584f).Within(15f));
        }

        [Test]
        public void TheProfileThresholdMatchesTheBuiltRunway()
        {
            // CircuitProfile cannot reference Presentation, so the threshold station is
            // duplicated. Lock the two together rather than let them drift apart.
            Assert.That(CircuitProfile.WestThresholdX,
                Is.EqualTo(-Airside.Presentation.AirsideBareField.RunwayHalfLength).Within(0.01f));
        }

        [Test]
        public void TheAircraftLandsOnTheThreeHundredMetreMarkings()
        {
            Assert.That(CircuitProfile.TouchdownX - CircuitProfile.WestThresholdX,
                Is.EqualTo(300f).Within(0.5f));
        }

        [Test]
        public void FlareBeginsAtThirtyFeetAndFloatsToTheTouchdownPoint()
        {
            Assert.That(CircuitProfile.FlareHeightMetres * 3.281f, Is.EqualTo(30f).Within(1f),
                "round-out starts at about 30 ft");
            Assert.That(CircuitProfile.TouchdownX - CircuitProfile.FlareStartX,
                Is.EqualTo(CircuitProfile.FlareFloatMetres).Within(0.01f));

            // The aim point is short of the touchdown point: the aircraft floats past it.
            Assert.That(CircuitProfile.AimPointX, Is.LessThan(CircuitProfile.TouchdownX));

            // And the slope really does pass through the flare height at the flare start.
            Assert.That(CircuitProfile.GlideslopeHeight(CircuitProfile.FlareStartX),
                Is.EqualTo(CircuitProfile.FlareHeightMetres).Within(0.01f));
        }

        [Test]
        public void ThresholdCrossingIsLowButDeliberate()
        {
            // Landing on the paint with a 300 m float costs threshold height. Locked so
            // the trade-off cannot drift unnoticed; see FlareFloatMetres.
            var feet = CircuitProfile.ThresholdCrossingHeight * 3.281f;
            Assert.That(feet, Is.EqualTo(30f).Within(2f));
        }

        [Test]
        public void FlareIsSlowEnoughToArrestTheSinkMonotonically()
        {
            // The mean sink demanded by the flare must be below the rate flown onto it,
            // or the aircraft has to sink *faster* mid-flare to arrive on time - which
            // is what a 172 m flare would have required.
            var meanSink = CircuitProfile.FlareHeightMetres / CircuitProfile.FlareExactSeconds;
            Assert.That(meanSink, Is.LessThan(CircuitProfile.ApproachDescentRate),
                "the flare cannot arrest the sink in the distance it is given");
            Assert.That(CircuitProfile.TouchdownSinkMetresPerSecond,
                Is.LessThan(CircuitProfile.ApproachDescentRate));
        }

        [Test]
        public void TouchdownIsSofterThanTheApproachDescent()
        {
            var fpm = CircuitProfile.TouchdownSinkMetresPerSecond * 196.85f;
            // Well under the 600 ft/min that counts as a hard landing.
            Assert.That(fpm, Is.LessThan(200f));
            Assert.That(fpm, Is.GreaterThan(0f));
        }

        [Test]
        public void PhaseDurationsComeFromDistanceAndSpeed()
        {
            // Each derived duration must equal distance / mean speed for its segments.
            Assert.That(CircuitProfile.ApproachExactSeconds,
                Is.EqualTo((CircuitProfile.ShortFinalX - CircuitProfile.ApproachStartX)
                           / (0.5f * (CircuitProfile.ApproachEntryKnots + CircuitProfile.ApproachKnots) * Kt))
                    .Within(0.01f));

            Assert.That(CircuitProfile.RolloutExactSeconds,
                Is.EqualTo((CircuitProfile.RolloutEndX - CircuitProfile.TouchdownX)
                           / (0.5f * (CircuitProfile.TouchdownKnots + CircuitProfile.RunwayExitKnots) * Kt)).Within(0.01f));

            Assert.That(CircuitProfile.TakeoffRollExactSeconds,
                Is.EqualTo((CircuitProfile.RotateX - CircuitProfile.TakeoffStartX)
                           / (0.5f * CircuitProfile.RotateKnots * Kt)).Within(0.01f));

            Assert.That(CircuitProfile.LandingExactSeconds,
                Is.EqualTo(CircuitProfile.FinalGlideExactSeconds
                           + CircuitProfile.FlareExactSeconds
                           + CircuitProfile.RolloutExactSeconds).Within(0.01f));
        }

        [Test]
        public void WholeSecondDurationsStayCloseToTheDerivedTime()
        {
            // The clock ticks in whole seconds, so every phase is stretched or squeezed
            // by up to half a second. The shortest phase here is the ~31.5 s fly-out,
            // which bounds the worst-case distortion at about 1.6%.
            AssertRounding(CircuitProfile.ApproachExactSeconds, CircuitProfile.ApproachSeconds, "approach");
            AssertRounding(CircuitProfile.LandingExactSeconds, CircuitProfile.LandingSeconds, "landing");
            AssertRounding(CircuitProfile.TakeoffExactSeconds, CircuitProfile.TakeoffSeconds, "takeoff");
            AssertRounding(CircuitProfile.DepartedExactSeconds, CircuitProfile.DepartedSeconds, "departed");
        }

        private static void AssertRounding(float exact, long whole, string label)
        {
            var error = Math.Abs(exact - whole) / exact;
            Assert.That(error, Is.LessThan(0.02f), $"{label} rounds {exact:F2}s to {whole}s");
            // And the bound must stay meaningful: half a second over the phase length.
            Assert.That(error, Is.LessThanOrEqualTo(0.5f / exact + 1e-4f), label);
        }

        [Test]
        public void AirportCircuitUsesTheDerivedDurations()
        {
            Assert.That(AirportCircuit.ApproachSeconds, Is.EqualTo(CircuitProfile.ApproachSeconds));
            Assert.That(AirportCircuit.LandingSeconds, Is.EqualTo(CircuitProfile.LandingSeconds));
            Assert.That(AirportCircuit.TakeoffSeconds, Is.EqualTo(CircuitProfile.TakeoffSeconds));
            Assert.That(AirportCircuit.DepartureFlyOutSeconds, Is.EqualTo(CircuitProfile.DepartedSeconds));
        }

        [Test]
        public void TakeoffRollReachesVrAndNotTwiceIt()
        {
            // The regression this whole model exists to prevent: the old curve passed
            // rotate at 179 kt and left the field at 257 kt.
            var rollMetres = CircuitProfile.RotateX - CircuitProfile.TakeoffStartX;
            var accel = CircuitProfile.Knots(CircuitProfile.RotateKnots) / CircuitProfile.TakeoffRollExactSeconds;
            var covered = 0.5f * accel * CircuitProfile.TakeoffRollExactSeconds * CircuitProfile.TakeoffRollExactSeconds;

            Assert.That(covered, Is.EqualTo(rollMetres).Within(1f),
                "the roll must cover its distance at the acceleration that reaches Vr");
            Assert.That(CircuitProfile.RotateKnots, Is.InRange(90f, 110f));
            // A turboprop of this size uses about a kilometre and roughly 1.5 m/s².
            Assert.That(rollMetres, Is.InRange(700f, 1200f));
            Assert.That(accel, Is.InRange(1.0f, 2.0f));
        }

        [Test]
        public void RolloutDecelerationIsPlausibleBraking()
        {
            var decel = CircuitProfile.Knots(CircuitProfile.TouchdownKnots - CircuitProfile.RunwayExitKnots) / CircuitProfile.RolloutExactSeconds;
            // Gentle: a long strip and no reason to stand on the brakes, but real.
            Assert.That(decel, Is.InRange(0.8f, 2.5f));
            Assert.That(CircuitProfile.RolloutEndX - CircuitProfile.TouchdownX, Is.InRange(800f, 1300f));
        }

        [Test]
        public void ClimbRatesAreRealisticForTheType()
        {
            var initialFpm = CircuitProfile.InitialClimbRateMetresPerSecond * 196.85f;
            var climbOutFpm = CircuitProfile.ClimbOutRateMetresPerSecond * 196.85f;
            Assert.That(initialFpm, Is.InRange(1000f, 1800f));
            Assert.That(climbOutFpm, Is.InRange(800f, 1600f));

            // And the geometry must actually deliver them over the distance flown.
            var initialGradient = CircuitProfile.TakeoffEndHeight / (CircuitProfile.TakeoffEndX - CircuitProfile.RotateX);
            Assert.That(initialGradient, Is.InRange(0.06f, 0.15f), "initial climb gradient");
            Assert.That(CircuitProfile.DepartedEndHeight, Is.GreaterThan(CircuitProfile.TakeoffEndHeight));
        }

        [Test]
        public void ReferenceSpeedsAreOrderedTheWayTheTypeFliesThem()
        {
            Assert.That(CircuitProfile.TouchdownKnots, Is.LessThan(CircuitProfile.ApproachKnots),
                "the flare scrubs speed off before the wheels touch");
            Assert.That(CircuitProfile.ApproachKnots, Is.LessThan(CircuitProfile.ApproachEntryKnots),
                "still slowing to Vapp on the straight-in");
            Assert.That(CircuitProfile.RotateKnots, Is.LessThan(CircuitProfile.InitialClimbKnots),
                "accelerate after rotate");
            Assert.That(CircuitProfile.InitialClimbKnots, Is.LessThan(CircuitProfile.ClimbOutKnots),
                "keep accelerating once clear");
        }

        [Test]
        public void TheAirspeedScheduleMatchesTheReferenceFigures()
        {
            // This is the number shown on the HUD, so it is worth asserting directly.
            Assert.That(CircuitProfile.AirspeedKnots(AircraftPhase.Approach, 0f),
                Is.EqualTo(CircuitProfile.ApproachEntryKnots).Within(0.1f));
            Assert.That(CircuitProfile.AirspeedKnots(AircraftPhase.Approach, 1f),
                Is.EqualTo(CircuitProfile.ApproachKnots).Within(0.1f));

            // Vapp is held all the way down the slope to the round-out.
            Assert.That(CircuitProfile.AirspeedKnots(AircraftPhase.Landing, 0f),
                Is.EqualTo(CircuitProfile.ApproachKnots).Within(0.1f));
            Assert.That(CircuitProfile.AirspeedKnots(AircraftPhase.Landing, CircuitProfile.FlareProgress * 0.99f),
                Is.EqualTo(CircuitProfile.ApproachKnots).Within(0.1f));

            // The flare scrubs Vapp off, and the wheels touch at touchdown speed.
            Assert.That(CircuitProfile.AirspeedKnots(AircraftPhase.Landing, CircuitProfile.TouchdownProgress),
                Is.EqualTo(CircuitProfile.TouchdownKnots).Within(0.5f));
            // Then the rollout brakes to runway exit speed and turns off without stopping.
            Assert.That(CircuitProfile.AirspeedKnots(AircraftPhase.Landing, 1f),
                Is.EqualTo(CircuitProfile.RunwayExitKnots).Within(0.1f));

            // The regression that started all this: rotate at Vr, not 179 kt.
            Assert.That(CircuitProfile.AirspeedKnots(AircraftPhase.Takeoff, 0f), Is.EqualTo(0f).Within(0.1f));
            Assert.That(CircuitProfile.AirspeedKnots(AircraftPhase.Takeoff, CircuitProfile.RotateProgress),
                Is.EqualTo(CircuitProfile.RotateKnots).Within(0.5f));
            Assert.That(CircuitProfile.AirspeedKnots(AircraftPhase.Takeoff, 1f),
                Is.EqualTo(CircuitProfile.InitialClimbKnots).Within(0.5f));

            Assert.That(CircuitProfile.AirspeedKnots(AircraftPhase.Departed, 1f),
                Is.EqualTo(CircuitProfile.ClimbOutKnots).Within(0.5f));

            // Stopped on the rollout end through the skipped ground phases.
            Assert.That(CircuitProfile.AirspeedKnots(AircraftPhase.AtStand, 0.5f), Is.EqualTo(0f));
        }

        [Test]
        public void TheAirspeedScheduleIsContinuousAcrossPhaseSeams()
        {
            // A jump here would show as the readout flicking by tens of knots in a frame.
            Assert.That(CircuitProfile.AirspeedKnots(AircraftPhase.Approach, 1f),
                Is.EqualTo(CircuitProfile.AirspeedKnots(AircraftPhase.Landing, 0f)).Within(0.1f),
                "approach into landing");
            Assert.That(CircuitProfile.AirspeedKnots(AircraftPhase.Takeoff, 1f),
                Is.EqualTo(CircuitProfile.AirspeedKnots(AircraftPhase.Departed, 0f)).Within(0.1f),
                "takeoff into departed");
            // Landing hands over to the runway-exit taxi at the speed the vacate path starts at.
            Assert.That(CircuitProfile.Knots(CircuitProfile.AirspeedKnots(AircraftPhase.Landing, 1f)),
                Is.EqualTo(AdelaideGround.Vacate.PoseAt(0.0).Speed).Within(0.05f), "landing into vacate");
            Assert.That(CircuitProfile.AirspeedKnots(AircraftPhase.Takeoff, 0f), Is.EqualTo(0f).Within(0.1f));
        }

        [Test]
        public void TheAirspeedScheduleNeverGoesBackwardsWhereItShouldNot()
        {
            // Accelerating phases must only accelerate; the rollout must only slow.
            AssertMonotonic(AircraftPhase.Takeoff, rising: true);
            AssertMonotonic(AircraftPhase.Departed, rising: true);
            for (var i = 1; i <= 50; i++)
            {
                var a = CircuitProfile.TouchdownProgress + (1f - CircuitProfile.TouchdownProgress) * (i - 1) / 50f;
                var b = CircuitProfile.TouchdownProgress + (1f - CircuitProfile.TouchdownProgress) * i / 50f;
                Assert.That(CircuitProfile.AirspeedKnots(AircraftPhase.Landing, b),
                    Is.LessThanOrEqualTo(CircuitProfile.AirspeedKnots(AircraftPhase.Landing, a) + 0.01f),
                    "the rollout must only slow down");
            }
        }

        private static void AssertMonotonic(AircraftPhase phase, bool rising)
        {
            for (var i = 1; i <= 100; i++)
            {
                var previous = CircuitProfile.AirspeedKnots(phase, (i - 1) / 100f);
                var current = CircuitProfile.AirspeedKnots(phase, i / 100f);
                if (rising)
                    Assert.That(current, Is.GreaterThanOrEqualTo(previous - 0.01f), $"{phase} at {i / 100f}");
                else
                    Assert.That(current, Is.LessThanOrEqualTo(previous + 0.01f), $"{phase} at {i / 100f}");
            }
        }

        [Test]
        public void ProgressFractionsSplitEachPhaseInOrder()
        {
            Assert.That(CircuitProfile.FlareProgress, Is.GreaterThan(0f));
            Assert.That(CircuitProfile.FlareProgress, Is.LessThan(CircuitProfile.TouchdownProgress));
            Assert.That(CircuitProfile.TouchdownProgress, Is.LessThan(1f));
            Assert.That(CircuitProfile.RotateProgress, Is.InRange(0.4f, 0.9f));

            // Most of the landing phase is the rollout, not the approach to it.
            Assert.That(CircuitProfile.TouchdownProgress, Is.LessThan(0.35f));
        }
    }
}

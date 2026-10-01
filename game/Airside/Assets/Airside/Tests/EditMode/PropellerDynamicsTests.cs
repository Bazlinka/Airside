using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace Airside.Tests
{
    /// <summary>
    /// ADR 0151 — the constant-speed propeller and turbofan model. These cover the behaviour a
    /// player can see: feathered when parked, a start that motors before it lights, a governor
    /// that holds the speed while the blades carry the power, reverse on the rollout, and a disc
    /// that fades edge-on.
    /// </summary>
    public sealed class PropellerDynamicsTests
    {
        [Test]
        public void ShutDownEngine_FeathersItsBlades()
        {
            Assert.That(AirsidePropellerDynamics.BladePitchDegrees(0f, 0f, 0f, 0f),
                Is.EqualTo(AirsidePropellerDynamics.FeatherDegrees),
                "a parked turboprop stands with its blades feathered");
            Assert.That(AirsidePropellerDynamics.EngineRpm(0f, AirsidePropellerDynamics.TaxiNp), Is.Zero);
        }

        [Test]
        public void Start_UnfeathersThenMotorsThenLightsUpToGroundIdle()
        {
            var unfeathered = AirsidePropellerDynamics.BladePitchDegrees(
                AirsidePropellerDynamics.LightOffFraction * 0.9f, 0f, 0f, 0f);
            Assert.That(unfeathered, Is.LessThan(AirsidePropellerDynamics.FeatherDegrees * 0.25f),
                "the blades come off the feather stop before the turbine lights");

            var motoring = AirsidePropellerDynamics.EngineRpm(
                AirsidePropellerDynamics.LightOffFraction * 0.5f, AirsidePropellerDynamics.TaxiNp);
            Assert.That(AirsidePropellerDynamics.Motoring(AirsidePropellerDynamics.LightOffFraction * 0.5f), Is.True);
            Assert.That(motoring, Is.GreaterThan(0f).And.LessThan(AirsidePropellerDynamics.MotoringRpm + 1f),
                "the starter turns it slowly enough to count the blades");

            var idle = AirsidePropellerDynamics.EngineRpm(
                AirsidePropellerDynamics.GovernorCaptureFraction, AirsidePropellerDynamics.TaxiNp);
            Assert.That(idle, Is.EqualTo(
                AirsidePropellerDynamics.GovernedRpm * AirsidePropellerDynamics.GroundIdleNp).Within(1f),
                "light-off carries the propeller to ground idle, not to the governed speed");
            Assert.That(AirsidePropellerDynamics.EngineRpm(1f, AirsidePropellerDynamics.TakeoffNp),
                Is.EqualTo(AirsidePropellerDynamics.GovernedRpm).Within(0.01f));
        }

        [Test]
        public void Governor_HoldsSpeedWhileBladePitchCarriesThePower()
        {
            var taxiRpm = AirsideReusableMotion.PropRpmForPhase(AircraftPhase.TaxiOut);
            var takeoffRpm = AirsideReusableMotion.PropRpmForPhase(AircraftPhase.Takeoff);
            Assert.That(takeoffRpm / taxiRpm, Is.LessThan(1.6f),
                "a governed propeller does not treble its speed between taxi and takeoff");

            var taxiPitch = PitchForPhase(AircraftPhase.TaxiOut);
            var rollPitch = PitchForPhase(AircraftPhase.Takeoff, 0.2f);
            var rotatePitch = PitchForPhase(AircraftPhase.Takeoff, AirsideFlightPath.RotateProgress);
            var cruisePitch = PitchForPhase(AircraftPhase.Circuit);
            Assert.That(rollPitch, Is.GreaterThan(taxiPitch + 6f),
                "the power difference shows as blade angle instead");
            Assert.That(rotatePitch, Is.GreaterThan(rollPitch + 2f),
                "the blades coarsen through the roll as the speed builds");
            Assert.That(cruisePitch, Is.GreaterThan(rotatePitch),
                "cruise is the coarsest normal setting, though it uses less power than takeoff");
            Assert.That(AirsideReusableMotion.PropPowerForPhase(AircraftPhase.AtStand),
                Is.LessThan(AirsideReusableMotion.PropPowerForPhase(AircraftPhase.Takeoff)));
        }

        [Test]
        public void LandingRollout_SelectsReverseThenStowsIt()
        {
            var touchdown = AirsideFlightPath.TouchdownProgress;
            Assert.That(AirsidePropellerDynamics.ReverseBlend(touchdown * 0.5f), Is.Zero,
                "no reverse before the wheels are down");
            var justAfter = Mathf.Lerp(touchdown, 1f, 0.1f);
            Assert.That(AirsidePropellerDynamics.ReverseBlend(justAfter), Is.GreaterThan(0.5f));
            Assert.That(AirsidePropellerDynamics.ReverseBlend(1f), Is.Zero,
                "reverse is stowed before the aircraft leaves the runway");
            Assert.That(AirsidePropellerDynamics.BladePitchDegrees(1f, 0.4f, 0f, 1f), Is.Negative,
                "beta range puts the blades through flat pitch");
        }

        [Test]
        public void Disc_FadesEdgeOnAndThickensWithCoarsePitch()
        {
            var faceOn = AirsidePropellerDynamics.DiscViewFade(1f);
            var edgeOn = AirsidePropellerDynamics.DiscViewFade(0f);
            Assert.That(faceOn, Is.EqualTo(1f).Within(0.001f));
            Assert.That(edgeOn, Is.LessThan(0.2f),
                "edge-on there is almost nothing in the line of sight");
            Assert.That(AirsidePropellerDynamics.DiscViewFade(-1f), Is.EqualTo(faceOn).Within(0.001f),
                "the far propeller reads the same as the near one");
            Assert.That(AirsidePropellerDynamics.DiscPitchDensity(AirsidePropellerDynamics.CruiseDegrees),
                Is.GreaterThan(AirsidePropellerDynamics.DiscPitchDensity(
                    AirsidePropellerDynamics.GroundIdleDegrees)));
        }

        [Test]
        public void FeatheredPropeller_BarelyWindmills()
        {
            var feathered = AirsidePropellerDynamics.WindmillRpm(30f, AirsidePropellerDynamics.FeatherDegrees);
            var fine = AirsidePropellerDynamics.WindmillRpm(30f, AirsidePropellerDynamics.StartFineDegrees);
            Assert.That(feathered, Is.LessThan(1f));
            Assert.That(fine, Is.GreaterThan(feathered * 20f));
            Assert.That(AirsidePropellerDynamics.WindmillRpm(0f, 0f), Is.Zero);
        }

        [Test]
        public void JetSpool_IsSlowestOutOfTheIdleRange()
        {
            var fromIdle = AirsidePropellerDynamics.JetSpoolSeconds(
                AirsidePropellerDynamics.JetIdleN1, AirsidePropellerDynamics.JetTakeoffN1);
            var fromHigh = AirsidePropellerDynamics.JetSpoolSeconds(
                AirsidePropellerDynamics.JetClimbN1, AirsidePropellerDynamics.JetTakeoffN1);
            Assert.That(fromIdle, Is.GreaterThan(fromHigh * 2f),
                "the long spool-up from idle is the whole reason a go-around takes so long");
            Assert.That(AirsidePropellerDynamics.JetN1ForPhase(AircraftPhase.Approach),
                Is.LessThan(AirsidePropellerDynamics.JetN1ForPhase(AircraftPhase.TaxiOut) + 0.3f));
            Assert.That(AirsidePropellerDynamics.JetN1ForPhase(AircraftPhase.Takeoff, 1f),
                Is.GreaterThan(AirsidePropellerDynamics.JetN1ForPhase(AircraftPhase.Takeoff, 0f)),
                "takeoff power comes up over the roll rather than arriving whole");
        }

        [Test]
        public void GovernorHunt_StaysWithinAFractionOfAPercent()
        {
            for (var t = 0f; t < 20f; t += 0.37f)
                Assert.That(AirsidePropellerDynamics.GovernorHunt(t, 1.3f), Is.EqualTo(1f).Within(0.006f));
        }

        private static float PitchForPhase(AircraftPhase phase, float progress01 = 1f) =>
            AirsidePropellerDynamics.BladePitchDegrees(1f,
                AirsideReusableMotion.PropPowerForPhase(phase, progress01),
                AirsidePropellerDynamics.Advance01ForPhase(phase, progress01), 0f);
        [Test]
        public void PropDisc_AllButDisappearsAtFullPower()
        {
            // ADR 0168: the disc is the blades' real coverage. It painted blade ghosts at 30 %
            // before, so a propeller at full power never went away.
            foreach (var blades in new[] { 4, 6 })
            {
                var peak = 0f;
                var weighted = 0f;
                var area = 0f;
                for (var i = 0; i < 200; i++)
                {
                    var r = (i + 0.5f) / 200f;
                    var alpha = AirsidePropellerDynamics.PropDiscAlpha(r, blades);
                    peak = Mathf.Max(peak, alpha);
                    weighted += alpha * r;
                    area += r;
                }

                Assert.That(peak, Is.LessThan(0.9f), $"{blades} blades: translucent, not a solid disc");
                Assert.That(weighted / area, Is.InRange(0.12f, 0.4f), $"{blades} blades: clearly visible blur");
                Assert.That(AirsidePropellerDynamics.PropDiscAlpha(0.1f, blades), Is.Zero, "clear over the spinner");
                Assert.That(AirsidePropellerDynamics.PropDiscAlpha(1f, blades), Is.Zero);
                Assert.That(AirsidePropellerDynamics.PropDiscAlpha(0.955f, blades),
                    Is.GreaterThan(AirsidePropellerDynamics.PropDiscAlpha(0.9f, blades)), "painted tips trace a ring");
            }

            Assert.That(AirsidePropellerDynamics.PropDiscAlpha(0.5f, 6),
                Is.GreaterThan(AirsidePropellerDynamics.PropDiscAlpha(0.5f, 4)), "more blades, denser haze");
        }

        [Test]
        public void DiscSpeedLook_KeepsGrowingUntilThePropellerIsUpToSpeed()
        {
            var idle = AirsidePropellerDynamics.DiscSpeedLook(AirsideReusableMotion.PropRpmGroundIdle);
            var governed = AirsidePropellerDynamics.DiscSpeedLook(AirsidePropellerDynamics.GovernedRpm);
            Assert.That(governed, Is.EqualTo(1f).Within(1e-4f));
            Assert.That(idle, Is.LessThan(governed - 0.1f), "idle is visibly thinner than full speed");
            var last = 0f;
            for (var rpm = 0f; rpm <= AirsidePropellerDynamics.GovernedRpm; rpm += 25f)
            {
                var look = AirsidePropellerDynamics.DiscSpeedLook(rpm);
                Assert.That(look, Is.GreaterThanOrEqualTo(last), "monotonic: never dims as it speeds up");
                last = look;
            }
        }

        [Test]
        public void SpoolResponse_NeverOvershoots()
        {
            Assert.That(4f * AirsidePropellerDynamics.SpoolRateLagSeconds * AirsidePropellerDynamics.SpoolGain,
                Is.LessThanOrEqualTo(1f));
        }

        [Test]
        public void JetFanDisc_IsASolidFaceWithTheSpinnerShowing()
        {
            Assert.That(AirsidePropellerDynamics.JetFanDiscAlpha(0.6f), Is.GreaterThan(0.9f), "no see-through intake");
            Assert.That(AirsidePropellerDynamics.JetFanDiscAlpha(0.1f), Is.Zero);
            Assert.That(AirsidePropellerDynamics.JetFanDiscAlpha(1f), Is.Zero);
        }

        [Test]
        public void Blur_DoesNotPopBackOnAShortFrame()
        {
            var at60 = AirsidePropellerDynamics.BlurStepDegrees(1200f, 1f / 60f);
            Assert.That(AirsidePropellerDynamics.BlurStepDegrees(1200f, 1f / 120f), Is.EqualTo(at60).Within(1e-3f));
            Assert.That(AirsidePropellerDynamics.BlurStepDegrees(1200f, 0f), Is.EqualTo(at60).Within(1e-3f),
                "a frame where the clock did not move still reads as spinning");
            Assert.That(AirsideReusableMotion.PropBlurForStep(
                AirsidePropellerDynamics.BlurStepDegrees(AirsideReusableMotion.PropRpmGroundIdle, 0f), 4), Is.EqualTo(1f));
            Assert.That(AirsideReusableMotion.PropBlurForStep(
                AirsidePropellerDynamics.BlurStepDegrees(AirsidePropellerDynamics.MotoringRpm, 1f / 60f), 4), Is.LessThan(1f),
                "a starting propeller still shows its blades");
        }

        [Test]
        public void Spinner_StaysWhenTheBladesBlur()
        {
            foreach (var solid in new[] { "Spinner", "Hub", "Hub cap", "Stripe", "Prop hub L" })
                Assert.That(AirsidePropellerDynamics.BlursAtSpeed(solid), Is.False, solid);
            foreach (var blade in new[] { "Blade", "Blade 3", "Tip", "Tip 2" })
                Assert.That(AirsidePropellerDynamics.BlursAtSpeed(blade), Is.True, blade);
        }
        [Test]
        public void Exhaust_IsAHazeNotAGlow_AndAStartThrowsAPuff()
        {
            // ADR 0170: the plume was orange and brightest at takeoff, like an afterburner.
            foreach (var power in new[] { 0f, 0.5f, 1f })
            {
                var tint = AirsidePropellerDynamics.ExhaustTint(power, 0f);
                Assert.That(tint.r - tint.b, Is.LessThan(0.12f), "neutral, not orange");
                Assert.That(tint.a, Is.LessThan(0.1f), "running exhaust is barely visible");
            }

            Assert.That(AirsidePropellerDynamics.ExhaustTint(1f, 0f).a,
                Is.GreaterThan(AirsidePropellerDynamics.ExhaustTint(0f, 0f).a), "thickens a little with power");
            var puff = AirsidePropellerDynamics.ExhaustTint(0f, 1f);
            Assert.That(puff.a, Is.GreaterThan(0.25f), "the light-off puff is the visible moment");
            var puffSize = AirsidePropellerDynamics.ExhaustScale(0f, 1f);
            var takeoffSize = AirsidePropellerDynamics.ExhaustScale(1f, 0f);
            Assert.That(puffSize.x, Is.GreaterThan(takeoffSize.x), "a puff billows wide");
            Assert.That(takeoffSize.y, Is.GreaterThan(puffSize.y), "power stretches the plume aft");
        }
    }
}

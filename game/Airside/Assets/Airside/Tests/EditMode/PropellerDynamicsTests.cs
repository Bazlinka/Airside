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
    }
}

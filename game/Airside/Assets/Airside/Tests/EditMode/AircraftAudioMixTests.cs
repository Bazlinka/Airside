using System;
using System.Collections.Generic;
using Airside.Domain;
using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class AircraftAudioMixTests
    {
        [Test]
        public void StartsWindUpButShutdownAndGovernedRunningDoNotReplayStarter()
        {
            Assert.That(AircraftAudioDynamics.Starter(0.4f, true), Is.GreaterThan(0.1f));
            Assert.That(AircraftAudioDynamics.Starter(0.4f, false), Is.Zero);
            Assert.That(AircraftAudioDynamics.Starter(0f, true), Is.Zero);
            Assert.That(AircraftAudioDynamics.Starter(1f, true), Is.Zero);
            Assert.That(AircraftAudioDynamics.CorePitch(EngineClass.RegionalJet, 0.95f, 1f),
                Is.GreaterThan(AircraftAudioDynamics.CorePitch(EngineClass.RegionalJet, 0.26f, 1f) * 1.8f));
            Assert.That(AircraftAudioDynamics.InteriorEngineGain(true, true),
                Is.GreaterThan(AircraftAudioDynamics.InteriorEngineGain(true, false)));
        }

        [Test]
        public void EveryFlyingTypeHasItsOwnVoiceAndStartsCold()
        {
            var keys = new HashSet<string>();
            foreach (var spec in AircraftCatalogue.All)
            {
                var profile = AircraftAudioProfiles.For(spec.Type);
                Assert.That(keys.Add(profile.Key), Is.True, spec.Id);
                var cold = AircraftAudioMix.For(spec.Type, "test", 0f, 0f, 0f, 0f, 0f, 0f, true);
                Assert.That(cold.Idle + cold.Power + cold.Reverse + cold.Wheels, Is.Zero, spec.Id);
                var idle = AircraftAudioMix.For(spec.Type, "test", 0.06f, 0.7f, 1f, 1f, 0f, 0f, true);
                var loaded = AircraftAudioMix.For(spec.Type, "test", 1f, 1f, 1f, 1f, 0f, 0f, true);
                Assert.That(loaded.Power, Is.GreaterThan(idle.Idle * 2f), spec.Id);
                Assert.That(loaded.Pitch, Is.InRange(0.6f, 1.5f), spec.Id);
                Assert.That(float.IsNaN(loaded.Power), Is.False);
            }
            Assert.That(keys.Count, Is.EqualTo(13));
        }

        [Test]
        public void SecondEngineStartAddsEnergyAndFinalShutdownIsSilent()
        {
            var single = AircraftAudioMix.For(AircraftType.Atr42, "test", 0.06f, 0.7f, 0f, 1f, 0f, 0f, true);
            var twin = AircraftAudioMix.For(AircraftType.Atr42, "test", 0.06f, 0.7f, 1f, 1f, 0f, 0f, true);
            Assert.That(twin.Idle / single.Idle, Is.EqualTo(Math.Sqrt(2)).Within(0.0001));
            var partial = AircraftAudioMix.For(AircraftType.Atr42, "test", 0.06f, 0.3f, 0f, 0.1f, 0f, 0f, true);
            Assert.That(partial.Idle, Is.LessThan(single.Idle / 5f));
        }

        [Test]
        public void PropGovernorHoldsTheNoteWhileJetRevsRise()
        {
            var taxi = AircraftAudioMix.For(AircraftType.Saab340, "test", 0.14f, 0.9f, 1f, 1f, 0f, 0f, true);
            var takeoff = AircraftAudioMix.For(AircraftType.Saab340, "test", 1f, 0.9f, 1f, 1f, 0f, 0f, true);
            var propRatio = takeoff.Pitch / taxi.Pitch;
            Assert.That(propRatio, Is.LessThan(1.10f));
            Assert.That(takeoff.Power, Is.GreaterThan(taxi.Power * 8f));
            var jetTaxi = AircraftAudioMix.For(AircraftType.Boeing7378, "test", 0.26f, 0.26f, 1f, 1f, 0f, 0f, true);
            var jetTakeoff = AircraftAudioMix.For(AircraftType.Boeing7378, "test", 0.95f, 0.95f, 1f, 1f, 0f, 0f, true);
            // #507 narrowed the jet bed sweep so throttle changes do not read as a full spool-up;
            // jets still rise more than a governed prop, but stay well under the old ~1.37 ratio.
            var jetRatio = jetTakeoff.Pitch / jetTaxi.Pitch;
            Assert.That(jetRatio, Is.GreaterThan(1.15f));
            Assert.That(jetRatio, Is.LessThan(1.28f));
            Assert.That(jetRatio, Is.GreaterThan(propRatio));
        }

        [Test]
        public void GroundSoundsFollowContactSpeedAndEveryTypesActualRotatePoint()
        {
            foreach (var spec in AircraftCatalogue.All)
            {
                var p = AircraftPerformance.For(spec.Type);
                Assert.That(AircraftAudioMix.Grounded(spec.Type, AircraftPhase.Landing, p.TouchdownProgress - 0.001f), Is.False);
                Assert.That(AircraftAudioMix.ReverseDemand(spec.Type, AircraftPhase.Landing, p.TouchdownProgress - 0.001f), Is.Zero);
                Assert.That(AircraftAudioMix.ReverseDemand(spec.Type, AircraftPhase.Landing, p.TouchdownProgress + (1f - p.TouchdownProgress) * 0.2f), Is.GreaterThan(0.9f));
                Assert.That(AircraftAudioMix.ReverseDemand(spec.Type, AircraftPhase.Landing, 1f), Is.Zero);
                Assert.That(AircraftAudioMix.ReverseDemand(spec.Type, AircraftPhase.GoAround, 0.5f), Is.Zero);
                Assert.That(AircraftAudioMix.Grounded(spec.Type, AircraftPhase.Takeoff, p.RotateProgress - 0.001f), Is.True);
                Assert.That(AircraftAudioMix.Grounded(spec.Type, AircraftPhase.Takeoff, p.RotateProgress + 0.001f), Is.False);
            }
            var air = AircraftAudioMix.For(AircraftType.Boeing7378, "test", 1f, 1f, 1f, 1f, 1f, 65f, false);
            Assert.That(air.Reverse + air.Wheels, Is.Zero);
            var stopped = AircraftAudioMix.For(AircraftType.Boeing7378, "test", 0.26f, 0.26f, 1f, 1f, 0f, 0f, true);
            Assert.That(stopped.Wheels, Is.Zero);
            var fast = AircraftAudioMix.For(AircraftType.Boeing7378, "test", 0.26f, 0.26f, 1f, 1f, 0f, 50f, true);
            Assert.That(fast.Wheels, Is.GreaterThan(0.04f));
        }

        [Test]
        public void TouchdownFiresOnceAndLateRestoreOrUnmuteDoesNotReplayIt()
        {
            var edge = new AircraftAudioContact();
            Assert.That(edge.Observe(false, false), Is.False);
            Assert.That(edge.Observe(true, false), Is.False, "phase entry is still airborne");
            Assert.That(edge.Observe(true, true), Is.True);
            for (var frame = 0; frame < 120; frame++)
                Assert.That(edge.Observe(true, true), Is.False, "also consumed when muted");
            Assert.That(edge.Observe(false, true), Is.False);
            Assert.That(edge.Observe(true, false), Is.False);
            Assert.That(edge.Observe(true, true), Is.True, "next flight re-arms");
            Assert.That(new AircraftAudioContact().Observe(true, true), Is.False, "restored late on rollout");
        }

        [Test]
        public void OverviewZoomStillHearsAircraftNearTheFocusPoint()
        {
            Assert.That(AircraftAudioMix.ZoomGain(36f), Is.EqualTo(1f));
            Assert.That(AircraftAudioMix.ZoomGain(300f), Is.EqualTo(1f));
            Assert.That(AircraftAudioMix.ZoomGain(2400f), Is.InRange(0.25f, 0.35f));
            Assert.That(AircraftAudioMix.ZoomGain(45000f), Is.GreaterThan(0f).And.LessThan(0.1f));
            Assert.That(AircraftAudioMix.ZoomGain(1200f), Is.LessThan(AircraftAudioMix.ZoomGain(600f)));
            // At the overview the listener hangs just above the ground focus, well inside
            // every type's hearing range, so an aircraft at the focus is not culled.
            var lift = AircraftAudioMix.ListenerLift(2400f);
            Assert.That(lift, Is.LessThan(AircraftAudioMix.AudibleDistance(EngineClass.Turboprop) / 10f));
            Assert.That(AircraftAudioMix.ListenerLift(20f), Is.GreaterThanOrEqualTo(6f));
        }

        [Test]
        public void ThrottleSmoothingMatchesAtThirtyAndOneHundredTwentyFps()
        {
            float thirty = 0f, fast = 0f;
            for (var i = 0; i < 30; i++) thirty = AircraftAudioMix.SmoothTowards(thirty, 1f, 1f / 30f, 0.65f);
            for (var i = 0; i < 120; i++) fast = AircraftAudioMix.SmoothTowards(fast, 1f, 1f / 120f, 0.65f);
            Assert.That(thirty, Is.EqualTo(fast).Within(0.00001f));
            Assert.That(thirty, Is.InRange(0.75f, 0.85f));
        }
    }
}

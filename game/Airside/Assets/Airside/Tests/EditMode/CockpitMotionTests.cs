using System;
using Airside.Domain;
using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class CockpitMotionTests
    {
        private const float Dt = 1f / 60f;

        private static CockpitMotion.Sample Ground(float time, float speed, float spool = 1f, bool turboprop = false) => new()
        {
            DeltaSeconds = Dt, SimRate = 1f, Time = time, GroundSpeed = speed, HeightAgl = 0f,
            PitchUpDegrees = 0f, Spool = spool, Turboprop = turboprop,
        };

        private static float PeakHeave(CockpitMotion motion, Func<int, CockpitMotion.Sample> sample, int frames)
        {
            var peak = 0f;
            for (var i = 0; i < frames; i++) peak = Math.Max(peak, Math.Abs(motion.Step(sample(i)).Up));
            return peak;
        }

        private static float Rms(CockpitMotion motion, Func<int, CockpitMotion.Sample> sample, int frames)
        {
            double sum = 0;
            for (var i = 0; i < frames; i++) { var p = motion.Step(sample(i)).Up; sum += p * p; }
            return (float)Math.Sqrt(sum / frames);
        }

        /// <summary>Descends at the given rate, touches down and rolls out; returns the peak heave after contact.</summary>
        private static float TouchdownPeak(int seed, float dt = Dt)
        {
            var m = new CockpitMotion(); m.Reset(seed);
            var t = 0f; var peak = 0f;
            for (var i = 0; i < 240; i++)
            {
                t += dt;
                m.Step(new CockpitMotion.Sample { DeltaSeconds = dt, SimRate = 1f, Time = t, GroundSpeed = 70f,
                    HeightAgl = Math.Max(0.5f, 6f - i * 0.025f), VerticalSpeed = -1f, PitchUpDegrees = 5f, Spool = 0.6f });
            }
            for (var i = 0; i < 90; i++)
            {
                t += dt;
                var up = m.Step(new CockpitMotion.Sample { DeltaSeconds = dt, SimRate = 1f, Time = t, GroundSpeed = 68f,
                    HeightAgl = 0f, PitchUpDegrees = 4f, Spool = 0.6f }).Up;
                peak = Math.Max(peak, Math.Abs(up));
            }
            return peak;
        }

        [Test] public void TouchdownProducesAJoltThatFirmerLandingsMakeBigger()
        {
            var peaks = new float[40];
            for (var seed = 0; seed < peaks.Length; seed++) peaks[seed] = TouchdownPeak(seed);
            Assert.That(Array.TrueForAll(peaks, p => p > 0.008f), Is.True, "even a smooth landing is felt");
            Assert.That(Array.TrueForAll(peaks, p => p <= 0.14f), Is.True, "never throws the head across the deck");
            Array.Sort(peaks);
            Assert.That(peaks[^1], Is.GreaterThan(peaks[0] * 1.4f), "landings vary in firmness");
        }

        private static float RolloutRms(int seed, bool decelerating, bool turboprop)
        {
            var m = new CockpitMotion(); m.Reset(seed);
            var t = 0f;
            for (var i = 0; i < 240; i++)
            { t += Dt; m.Step(new CockpitMotion.Sample { DeltaSeconds = Dt, SimRate = 1f, Time = t, GroundSpeed = 70f,
                HeightAgl = Math.Max(0.5f, 6f - i * 0.025f), VerticalSpeed = -1f, PitchUpDegrees = 5f, Spool = 0.6f, Turboprop = turboprop }); }
            var speed = 68f; double sum = 0; var n = 0;
            for (var i = 0; i < 360; i++)
            {
                t += Dt; if (decelerating) speed = Math.Max(30f, speed - 3f * Dt);
                var up = m.Step(new CockpitMotion.Sample { DeltaSeconds = Dt, SimRate = 1f, Time = t, GroundSpeed = speed,
                    HeightAgl = 0f, PitchUpDegrees = 3f, Spool = 0.6f, Turboprop = turboprop }).Up;
                if (i > 180) { sum += up * up; n++; }
            }
            return (float)Math.Sqrt(sum / n);
        }

        [Test] public void ReverseThrustRoarsThroughTheSeatDuringHardBraking()
        {
            Assert.That(RolloutRms(2, true, false), Is.GreaterThan(RolloutRms(2, false, false) * 1.05f));
            Assert.That(RolloutRms(2, true, true), Is.GreaterThan(RolloutRms(2, true, false)), "beta roars harder than jet reverse");
        }

        [Test] public void OnlyFirmLandingsSkipOnce()
        {
            // Smooth seed vs firm seed: find each class by their touchdown jolt, then compare the second rise.
            float SecondRise(int seed)
            {
                var m = new CockpitMotion(); m.Reset(seed); var t = 0f; var best = -1f;
                for (var i = 0; i < 240; i++)
                { t += Dt; m.Step(new CockpitMotion.Sample { DeltaSeconds = Dt, SimRate = 1f, Time = t, GroundSpeed = 70f,
                    HeightAgl = Math.Max(0.5f, 6f - i * 0.025f), VerticalSpeed = -1f, PitchUpDegrees = 5f, Spool = 0.6f }); }
                for (var i = 0; i < 150; i++)
                { t += Dt; var up = m.Step(new CockpitMotion.Sample { DeltaSeconds = Dt, SimRate = 1f, Time = t, GroundSpeed = 68f,
                    HeightAgl = 0f, PitchUpDegrees = 4f, Spool = 0.6f }).Up; if (i > 45) best = Math.Max(best, up); }
                return best;
            }
            var rises = new float[60];
            for (var seed = 0; seed < rises.Length; seed++) rises[seed] = SecondRise(seed);
            Array.Sort(rises);
            Assert.That(rises[^1], Is.GreaterThan(rises[0] + 0.004f), "some landings bounce, most do not");
        }

        [Test] public void TouchdownIsTheSameAtAnyFrameRate()
        {
            var a = TouchdownPeak(7, 1f / 30f); var b = TouchdownPeak(7, 1f / 120f);
            Assert.That(a, Is.EqualTo(b).Within(Math.Max(a, b) * 0.45f));
        }

        [Test] public void RunwayRumbleGrowsWithSpeedAndStopsInTheAir()
        {
            float RmsAt(float speed, float height)
            {
                var m = new CockpitMotion(); m.Reset(1);
                return Rms(m, i => { var s = Ground(i * Dt, speed); s.HeightAgl = height; s.VerticalSpeed = 0f; return s; }, 600);
            }
            Assert.That(RmsAt(70f, 0f), Is.GreaterThan(RmsAt(8f, 0f)));
            Assert.That(RmsAt(70f, 0f), Is.GreaterThan(RmsAt(75f, 3000f) * 1.2f), "smooth air is calmer than a fast roll");
        }

        [Test] public void LowLevelAirIsBumpierThanCruise()
        {
            float Air(float height)
            {
                var m = new CockpitMotion(); m.Reset(1);
                return Rms(m, i => new CockpitMotion.Sample { DeltaSeconds = Dt, SimRate = 1f, Time = i * Dt, GroundSpeed = 70f,
                    HeightAgl = height, VerticalSpeed = 0f, PitchUpDegrees = 2f, Spool = 0.5f }, 1800);
            }
            Assert.That(Air(80f), Is.GreaterThan(Air(5000f)));
        }

        [Test] public void ClearCruiseIsSteadyAndStormGustsAreSlowAndBounded()
        {
            float Peak(float turbulence)
            {
                var motion = new CockpitMotion(); motion.Reset(1);
                var peak = 0f;
                for (var i = 0; i < 1800; i++)
                {
                    var pose = motion.Step(new CockpitMotion.Sample { DeltaSeconds = Dt, SimRate = 1f,
                        Time = i * Dt, GroundSpeed = 150f, HeightAgl = 5000f, Spool = 0.6f,
                        WeatherTurbulence = turbulence });
                    peak = Math.Max(peak, Math.Abs(pose.RollDegrees));
                    Assert.That(motion.Rumble01, Is.Zero);
                }
                return peak;
            }
            Assert.That(Peak(0f), Is.LessThan(0.01f));
            Assert.That(Peak(1f), Is.InRange(0.04f, 0.3f));
        }

        [Test] public void TurbopropsShakeMoreThanJetsAtTheSamePower()
        {
            float At(bool prop)
            {
                var m = new CockpitMotion(); m.Reset(3);
                return Rms(m, i => Ground(i * Dt, 0.1f, 1f, prop), 900);
            }
            Assert.That(At(true), Is.GreaterThan(At(false)));
        }

        [Test] public void ThrustPushesTheHeadBackAndBrakingPitchesItForward()
        {
            CockpitMotion.Pose Run(float accel)
            {
                var m = new CockpitMotion(); m.Reset(1);
                var speed = 40f; CockpitMotion.Pose pose = default;
                for (var i = 0; i < 180; i++)
                {
                    speed += accel * Dt;
                    var s = Ground(i * Dt, speed, 0f); pose = m.Step(s);
                }
                return pose;
            }
            Assert.That(Run(3f).Forward, Is.LessThan(-0.01f));
            Assert.That(Run(-3f).Forward, Is.GreaterThan(0.01f));
            Assert.That(Run(-3f).PitchDownDegrees, Is.GreaterThan(Run(3f).PitchDownDegrees));
        }

        [Test] public void GazeLeadsIntoTheTurn()
        {
            var m = new CockpitMotion(); m.Reset(1);
            CockpitMotion.Pose pose = default;
            for (var i = 0; i < 240; i++)
                pose = m.Step(new CockpitMotion.Sample { DeltaSeconds = Dt, SimRate = 1f, Time = i * Dt, GroundSpeed = 70f,
                    HeightAgl = 500f, BankLeftDegrees = 20f, Spool = 0.5f });
            Assert.That(pose.YawDegrees, Is.LessThan(-3f), "left bank (Unity +z) looks left");
            Assert.That(pose.YawDegrees, Is.GreaterThan(-10f));
        }

        [Test] public void PausedSimulationSettlesAndStaysStill()
        {
            var m = new CockpitMotion(); m.Reset(1);
            for (var i = 0; i < 60; i++) m.Step(Ground(i * Dt, 60f));
            CockpitMotion.Pose pose = default;
            for (var i = 0; i < 240; i++)
            {
                var s = Ground(i * Dt, 0f); s.SimRate = 0f; pose = m.Step(s);
            }
            Assert.That(Math.Abs(pose.Up), Is.LessThan(0.002f));
            Assert.That(Math.Abs(pose.PitchDownDegrees), Is.LessThan(0.05f));
        }

        [Test] public void GearThumpsOnceEachWayAndPoseStaysBounded()
        {
            var m = new CockpitMotion(); m.Reset(5);
            var t = 0f; var worst = 0f;
            void Step(float height, float vs, float speed)
            {
                t += Dt;
                var p = m.Step(new CockpitMotion.Sample { DeltaSeconds = Dt, SimRate = 1f, Time = t, GroundSpeed = speed,
                    HeightAgl = height, VerticalSpeed = vs, PitchUpDegrees = 10f, Spool = 1f });
                worst = Math.Max(worst, Math.Max(Math.Abs(p.Up), Math.Abs(p.Right)));
                Assert.That(Math.Abs(p.PitchDownDegrees), Is.LessThanOrEqualTo(4.001f));
                Assert.That(Math.Abs(p.RollDegrees), Is.LessThanOrEqualTo(3.001f));
            }
            for (var i = 0; i < 300; i++) Step(0f, 0f, i * 0.25f);               // takeoff roll
            for (var i = 0; i < 1500; i++) Step(10f + i * 0.4f, 8f, 80f);        // climb, gear retract
            for (var i = 0; i < 1500; i++) Step(Math.Max(0.5f, 700f - i * 0.5f), -4f, 70f); // descent, gear extend
            Assert.That(worst, Is.LessThanOrEqualTo(0.14f + 1e-4f));
        }

        [Test] public void JetsRotateToAFifteenDegreeClimbAndTurbopropsStayFlatter()
        {
            Assert.That(AircraftType.TryFromId("B738", out var jet), Is.True);
            Assert.That(AircraftType.TryFromId("ATR42", out var prop), Is.True);
            float Climb(AircraftType t) => -AircraftAttitude.For(t).PitchDegrees(AircraftPhase.Takeoff, 1f, AircraftPerformance.For(t));
            Assert.That(Climb(jet), Is.InRange(13f, 18f));
            Assert.That(Climb(prop), Is.InRange(6f, 10f));
        }

        [Test] public void EveryTypeFlaresNoseUpAndNeverExceedsTailClearance()
        {
            foreach (var id in new[] { "SF34", "ATR42", "DH8D", "B738", "B38M", "A320", "A21N", "E190", "A223", "A359", "A339", "B789", "B78X" })
            {
                Assert.That(AircraftType.TryFromId(id, out var type), Is.True, id);
                var attitude = AircraftAttitude.For(type); var profile = AircraftPerformance.For(type);
                float P(AircraftPhase phase, float t) => -attitude.PitchDegrees(phase, t, profile);
                Assert.That(P(AircraftPhase.Landing, profile.TouchdownProgress - 0.001f), Is.InRange(4f, 8f), id + " touchdown attitude");
                Assert.That(P(AircraftPhase.Landing, profile.FlareProgress * 0.5f), Is.LessThan(P(AircraftPhase.Landing, profile.TouchdownProgress - 0.001f)), id);
                var worst = 0f;
                for (var i = 0; i <= 100; i++)
                {
                    worst = Math.Max(worst, P(AircraftPhase.Takeoff, i / 100f));
                    worst = Math.Max(worst, P(AircraftPhase.Departed, i / 100f));
                }
                Assert.That(worst, Is.LessThanOrEqualTo(17.5f), id + " climb pitch");
                Assert.That(P(AircraftPhase.Takeoff, profile.RotateProgress * 0.99f), Is.Zero, id + " stays level on the roll");
                Assert.That(P(AircraftPhase.Takeoff, 1f), Is.EqualTo(P(AircraftPhase.Departed, 0f)).Within(0.01f), id + " phase handoff");
            }
        }
    }
}

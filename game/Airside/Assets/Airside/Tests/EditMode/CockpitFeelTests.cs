using Airside.Presentation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>Trackpad haptics, look input and control hints for the aircraft views. Unity-free.</summary>
    public sealed class CockpitFeelTests
    {
        private const float Dt = 1f / 60f;

        [Test]
        public void Scheduler_DoesNothingWhenDisabledOrQuiet()
        {
            var scheduler = new CockpitHapticScheduler();
            scheduler.Post(HapticKind.Heavy);
            Assert.That(scheduler.Step(Dt, 1f, enabled: false), Is.EqualTo(HapticKind.None));
            // Disabling drops what was queued rather than replaying it when switched back on.
            Assert.That(scheduler.Step(Dt, 0f, enabled: true), Is.EqualTo(HapticKind.None));
        }

        [Test]
        public void Scheduler_KeepsOnlyTheStrongestQueuedTapAndSpacesThemOut()
        {
            var scheduler = new CockpitHapticScheduler();
            scheduler.Post(HapticKind.Tick);
            scheduler.Post(HapticKind.Medium);
            scheduler.Post(HapticKind.Tick);
            Assert.That(scheduler.Step(Dt, 0f, true), Is.EqualTo(HapticKind.Medium));
            scheduler.Post(HapticKind.Tick);
            Assert.That(scheduler.Step(Dt, 0f, true), Is.EqualTo(HapticKind.None),
                "a second tap inside the minimum gap waits");
            var waited = 0f;
            var next = HapticKind.None;
            while (next == HapticKind.None && waited < 0.5f)
            {
                next = scheduler.Step(Dt, 0f, true);
                waited += Dt;
            }
            Assert.That(next, Is.EqualTo(HapticKind.Tick));
            Assert.That(waited, Is.GreaterThanOrEqualTo(CockpitHapticScheduler.MinGapSeconds - Dt));
        }

        [Test]
        public void Scheduler_HardLandingThumpsTwice()
        {
            var scheduler = new CockpitHapticScheduler();
            scheduler.Post(HapticKind.Heavy);
            var taps = new System.Collections.Generic.List<HapticKind>();
            for (var i = 0; i < 60; i++)
            {
                var tap = scheduler.Step(Dt, 0f, true);
                if (tap != HapticKind.None) taps.Add(tap);
            }
            Assert.That(taps, Is.EqualTo(new[] { HapticKind.Heavy, HapticKind.Medium }));
        }

        [Test]
        public void Scheduler_RumbleTicksFasterAsItBuildsAndStopsBelowTheFloor()
        {
            int Ticks(float rumble)
            {
                var scheduler = new CockpitHapticScheduler();
                var count = 0;
                for (var i = 0; i < 600; i++)
                    if (scheduler.Step(Dt, rumble, true) == HapticKind.Tick) count++;
                return count;
            }
            Assert.That(Ticks(CockpitHapticScheduler.RumbleFloor * 0.5f), Is.Zero);
            Assert.That(Ticks(1f), Is.GreaterThan(Ticks(0.4f)));
            Assert.That(Ticks(1f) / 10f, Is.LessThanOrEqualTo(CockpitHapticScheduler.RumbleMaxHz + 1f),
                "never faster than the actuator can resolve");
        }

        [Test]
        public void Scheduler_HugeFrameTimeCannotBurstTaps()
        {
            var scheduler = new CockpitHapticScheduler();
            var taps = 0;
            for (var i = 0; i < 5; i++)
                if (scheduler.Step(30f, 1f, true) != HapticKind.None) taps++;
            Assert.That(taps, Is.LessThanOrEqualTo(5));
            Assert.That(scheduler.Step(30f, 1f, true), Is.Not.EqualTo(HapticKind.Heavy));
        }

        [Test]
        public void MacPatterns_FollowTheStrength()
        {
            Assert.That(MacTrackpadHaptics.PatternFor(HapticKind.Tick), Is.EqualTo(0));
            Assert.That(MacTrackpadHaptics.PatternFor(HapticKind.Medium), Is.EqualTo(1));
            Assert.That(MacTrackpadHaptics.PatternFor(HapticKind.Heavy), Is.EqualTo(2));
            Assert.DoesNotThrow(() => MacTrackpadHaptics.Perform(HapticKind.Heavy),
                "a no-op or a safe call on any platform, never an exception");
        }

        private static CockpitMotion.Sample Air(float t, float height, float vertical = -2f) => new()
        {
            DeltaSeconds = Dt, SimRate = 1f, Time = t, GroundSpeed = 70f, VerticalSpeed = vertical,
            HeightAgl = height, Spool = 0.6f,
        };

        [Test]
        public void Motion_TouchdownRaisesATrackpadThumpOnce()
        {
            var motion = new CockpitMotion();
            motion.Reset(7);
            var t = 0f;
            for (var i = 0; i < 30; i++, t += Dt) motion.Step(Air(t, 8f));
            motion.TakeHaptic();
            motion.Step(new CockpitMotion.Sample
            {
                DeltaSeconds = Dt, SimRate = 1f, Time = t, GroundSpeed = 70f, HeightAgl = 0f, Spool = 0.5f,
            });
            Assert.That(motion.TakeHaptic(), Is.EqualTo(HapticKind.Medium).Or.EqualTo(HapticKind.Heavy));
            Assert.That(motion.TakeHaptic(), Is.EqualTo(HapticKind.None), "taking clears it");
        }

        [Test]
        public void Motion_TurbulenceRumblesTheTrackpadButAPausedSimDoesNot()
        {
            var motion = new CockpitMotion();
            motion.Reset(3);
            var t = 0f;
            for (var i = 0; i < 20; i++, t += Dt) motion.Step(Air(t, 60f, 1f));
            Assert.That(motion.Rumble01, Is.GreaterThan(CockpitHapticScheduler.RumbleFloor));
            var paused = Air(t, 60f, 1f);
            paused.SimRate = 0f;
            motion.Step(paused);
            Assert.That(motion.Rumble01, Is.Zero);
        }

        [Test]
        public void Motion_RolloutJointsTickUnlessFastForwarding()
        {
            HapticKind Run(float simRate)
            {
                var motion = new CockpitMotion();
                motion.Reset(1);
                var strongest = HapticKind.None;
                for (var i = 0; i < 120; i++)
                {
                    motion.Step(new CockpitMotion.Sample
                    {
                        DeltaSeconds = Dt, SimRate = simRate, Time = i * Dt, GroundSpeed = 40f, HeightAgl = 0f, Spool = 1f,
                    });
                    var tap = motion.TakeHaptic();
                    if (tap > strongest) strongest = tap;
                }
                return strongest;
            }
            Assert.That(Run(1f), Is.EqualTo(HapticKind.Tick));
            Assert.That(Run(4f), Is.EqualTo(HapticKind.None));
        }

        [Test]
        public void LookInput_BoundsSpikesAndIgnoresNaN()
        {
            Assert.That(CockpitLookInput.ClampPixels(5000f), Is.EqualTo(CockpitLookInput.MaxPixelsPerFrame));
            Assert.That(CockpitLookInput.ClampPixels(-5000f), Is.EqualTo(-CockpitLookInput.MaxPixelsPerFrame));
            Assert.That(CockpitLookInput.ClampPixels(12f), Is.EqualTo(12f));
            Assert.That(CockpitLookInput.ClampPixels(float.NaN), Is.Zero);
            Assert.That(CockpitLookInput.ClampScroll(1e6f), Is.EqualTo(CockpitLookInput.MaxScrollPerFrame));
        }

        [Test]
        public void LookInput_ZoomedInTurnsFinerAndTopSpeedKeepsTheOldRate()
        {
            var wide = CockpitLookInput.DegreesPerPixel(CockpitLookInput.CockpitDegreesPerPixel, 85f, 1f);
            var narrow = CockpitLookInput.DegreesPerPixel(CockpitLookInput.CockpitDegreesPerPixel, 35f, 1f);
            Assert.That(narrow, Is.LessThan(wide));
            Assert.That(CockpitLookInput.DegreesPerPixel(CockpitLookInput.CockpitDegreesPerPixel,
                CockpitLookInput.ReferenceFov, 1f), Is.EqualTo(CockpitLookInput.CockpitDegreesPerPixel).Within(1e-5f));
            Assert.That(CockpitLookInput.DegreesPerPixel(0.15f, 65f, 0.4f), Is.LessThan(0.15f));
            Assert.That(CockpitLookInput.DegreesPerPixel(0.15f, 65f, 0f), Is.GreaterThan(0f));
        }

        [Test]
        public void LookInput_TransitionEasesFromZeroToOneWithoutOvershoot()
        {
            Assert.That(CockpitLookInput.Ease(0f), Is.Zero);
            Assert.That(CockpitLookInput.Ease(CockpitLookInput.TransitionSeconds), Is.EqualTo(1f));
            Assert.That(CockpitLookInput.Ease(100f), Is.EqualTo(1f));
            Assert.That(CockpitLookInput.Ease(-1f), Is.Zero);
            var previous = 0f;
            for (var i = 1; i <= 20; i++)
            {
                var value = CockpitLookInput.Ease(i / 20f * CockpitLookInput.TransitionSeconds);
                Assert.That(value, Is.GreaterThanOrEqualTo(previous));
                previous = value;
            }
        }

        [Test]
        public void Hints_CoverEveryViewAndFitANarrowDock()
        {
            for (var view = 0; view < 4; view++)
            {
                Assert.That(CockpitControlHints.Dock(view, narrow: false), Is.Not.Empty);
                Assert.That(CockpitControlHints.Dock(view, narrow: true).Length,
                    Is.LessThanOrEqualTo(CockpitControlHints.Dock(view, narrow: false).Length));
                Assert.That(CockpitControlHints.EntryToast(view), Does.Contain("Esc"));
            }
            Assert.That(CockpitControlHints.Dock(0, false), Does.Contain("either button"));
            Assert.That(CockpitControlHints.EntryToast(9), Is.Null);
        }

        [Test]
        public void Help_ListsTheAircraftViewControls()
        {
            Assert.That(ControlsHelp.IncludesKey("In an aircraft"), Is.True);
        }
    }
}

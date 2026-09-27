using System;
using System.Linq;
using Airside.Presentation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>ADR 0133 — the moment sounds are short, never clip, and are the same every run.</summary>
    public sealed class HudSoundsTests
    {
        private static readonly (string Name, Func<float[]> Make, float MaxSeconds)[] Sounds =
        {
            ("flap rattle", HudSounds.FlapRattle, 0.6f),
            ("cash ching", HudSounds.CashChing, 0.7f),
            ("tier sting", HudSounds.TierSting, 1.5f),
            ("contract chime", HudSounds.ContractChime, 1.0f)
        };

        [Test]
        public void EverySound_IsShortAudibleAndNeverClips()
        {
            foreach (var (name, make, maxSeconds) in Sounds)
            {
                var samples = make();
                Assert.That(samples.Length, Is.GreaterThan(HudSounds.SampleRate / 10), name);
                Assert.That(samples.Length / (float)HudSounds.SampleRate, Is.LessThanOrEqualTo(maxSeconds), name);
                Assert.That(samples.All(s => !float.IsNaN(s) && !float.IsInfinity(s)), Is.True, name);
                var peak = samples.Max(Math.Abs);
                Assert.That(peak, Is.GreaterThan(0.3f).And.LessThanOrEqualTo(0.75f), name);
                Assert.That(Math.Abs(samples[^1]), Is.LessThan(0.05f), $"{name} fades out rather than cutting off");
            }
        }

        [Test]
        public void Sounds_AreDeterministic()
        {
            foreach (var (name, make, _) in Sounds)
                Assert.That(make(), Is.EqualTo(make()), name);
        }
    }
}

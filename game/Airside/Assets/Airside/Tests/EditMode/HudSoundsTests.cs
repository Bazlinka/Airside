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
            ("contract chime", HudSounds.ContractChime, 1.0f),
            ("panel whoosh", HudSounds.PanelWhoosh, 0.3f),
            ("ui click", HudSounds.UiClick, 0.2f),
            ("PA chime", HudSounds.PaChime, 2.3f)
        };

        [Test]
        public void UiClick_HasASoftAttackAndQuietPeak()
        {
            var click = HudSounds.UiClick();
            Assert.That(click[0], Is.Zero, "no first-sample discontinuity");
            Assert.That(click.Max(Math.Abs), Is.LessThanOrEqualTo(0.35f));
            Assert.That(Math.Abs(click[^1]), Is.LessThan(0.001f), "no abrupt tail cutoff");
        }

        [Test]
        public void UiClick_IsATick()
        {
            var click = HudSounds.UiClick();
            var early = (int)(HudSounds.SampleRate * 0.04f);
            double earlyEnergy = 0;
            double rest = 0;
            for (var i = 0; i < click.Length; i++)
            {
                var e = click[i] * click[i];
                if (i < early)
                    earlyEnergy += e;
                else
                    rest += e;
            }

            Assert.That(earlyEnergy, Is.GreaterThan(rest * 3), "the tick is over before it can read as a tone");
        }

        [Test]
        public void ApronBed_IsAQuietSeamlessLoop()
        {
            var bed = HudSounds.ApronBed();
            Assert.That(bed.Length / (float)HudSounds.SampleRate, Is.InRange(10f, 12f));
            Assert.That(bed.All(v => !float.IsNaN(v)), Is.True);
            var typicalStep = Enumerable.Range(0, bed.Length - 1).Average(i => Math.Abs(bed[i + 1] - bed[i]));
            Assert.That(Math.Abs(bed[^1] - bed[0]), Is.LessThan(typicalStep * 4 + 0.01), "no click at the wrap");
            Assert.That(bed, Is.EqualTo(HudSounds.ApronBed()), "deterministic");
        }

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

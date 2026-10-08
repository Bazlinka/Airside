using System;
using Airside.Presentation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class AudioPeakLimiterTests
    {
        [Test]
        public void OverlappingBusesStayFiniteBelowCeilingAndKeepStereoImage()
        {
            var limiter = new AudioPeakLimiter(48000);
            var data = new float[9600];
            for (var i = 0; i < data.Length; i += 2)
            {
                data[i] = (float)Math.Sin(i * 0.037) * 3.5f;
                data[i + 1] = data[i] * 0.4f;
            }
            limiter.Process(data, 2);
            for (var i = 0; i < data.Length; i += 2)
            {
                Assert.That(Math.Abs(data[i]), Is.LessThanOrEqualTo(AudioPeakLimiter.Ceiling + 0.00001f));
                Assert.That(data[i + 1], Is.EqualTo(data[i] * 0.4f).Within(0.00001f));
            }
            var corrupt = new[] { float.NaN, float.PositiveInfinity, 2f, -2f };
            limiter.Process(corrupt, 2);
            foreach (var sample in corrupt)
                Assert.That(float.IsNaN(sample) || float.IsInfinity(sample), Is.False);
        }

        [Test]
        public void NormalAudioIsUnchangedAndReleaseRestoresItsLevel()
        {
            var limiter = new AudioPeakLimiter(22050);
            var ordinary = new[] { 0.2f, -0.1f, 0.7f, -0.6f };
            limiter.Process(ordinary, 2);
            Assert.That(ordinary, Is.EqualTo(new[] { 0.2f, -0.1f, 0.7f, -0.6f }));
            limiter.Process(new[] { 4f, 4f }, 2);
            var recovery = new float[22050];
            for (var i = 0; i < recovery.Length; i++) recovery[i] = 0.2f;
            limiter.Process(recovery, 1);
            Assert.That(recovery[recovery.Length - 1], Is.EqualTo(0.2f).Within(0.0001f));
        }
    }
}

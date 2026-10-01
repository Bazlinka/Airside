using System;
using System.Linq;
using Airside.Presentation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class CockpitAirflowTests
    {
        [Test] public void AirflowLoopIsDeterministicBoundedAndHasNoWrapTransient()
        {
            var samples = CockpitAirflow.Samples();
            Assert.That(samples.Length, Is.EqualTo(CockpitAirflow.SampleRate * 4));
            Assert.That(samples, Is.EqualTo(CockpitAirflow.Samples()));
            Assert.That(samples.All(s => !float.IsNaN(s) && !float.IsInfinity(s) && Math.Abs(s) <= 0.551f), Is.True);
            Assert.That(Math.Sqrt(samples.Average(s => (double)s * s)), Is.InRange(0.05, 0.25));
            var step = Enumerable.Range(1, samples.Length - 1).Average(i => Math.Abs(samples[i] - samples[i - 1]));
            Assert.That(Math.Abs(samples[0] - samples[^1]), Is.LessThan(step * 4f), "Wrap behaves like the filtered noise, not an attack");
        }

        [Test] public void AirflowBuildsWithSpeedWithoutOverwhelmingEngines()
        {
            Assert.That(CockpitAirflow.Volume(-20f), Is.EqualTo(CockpitAirflow.Volume(0f)));
            Assert.That(CockpitAirflow.Volume(20f), Is.LessThan(0.02f));
            Assert.That(CockpitAirflow.Volume(150f), Is.GreaterThan(CockpitAirflow.Volume(20f)));
            Assert.That(CockpitAirflow.Volume(300f), Is.EqualTo(0.12f).Within(0.0001f));
            Assert.That(CockpitAirflow.Volume(2000f), Is.EqualTo(CockpitAirflow.Volume(300f)));
        }
    }
}

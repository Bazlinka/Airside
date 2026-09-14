using System;
using System.Linq;
using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class AdelaideStandMarkingTests
    {
        [Test]
        public void EveryRegionalBayGetsItsRealReference()
        {
            var markings = AdelaideStandMarkings.All();

            Assert.That(markings.Length, Is.EqualTo(6));
            Assert.That(markings.Select(marking => marking.BayId),
                Is.EqualTo(new[] { "BAY-1", "BAY-2", "BAY-3", "BAY-4", "BAY-5", "BAY-6" }));
            Assert.That(markings.Select(marking => marking.Reference),
                Is.EqualTo(new[] { "50D", "50C", "50B", "50A", "50E", "50F" }));
        }

        [Test]
        public void LeadInsAndStopBarsTerminateAtGeneratedBayStops()
        {
            var markings = AdelaideStandMarkings.All();
            for (var i = 0; i < markings.Length; i++)
            {
                var marking = markings[i];
                var bay = AdelaideLayout.Bays[i];

                Assert.That(marking.LeadIn[^2], Is.EqualTo(bay.StopX).Within(0.001f));
                Assert.That(marking.LeadIn[^1], Is.EqualTo(bay.StopZ).Within(0.001f));
                Assert.That((marking.StopBar[0] + marking.StopBar[2]) * 0.5f,
                    Is.EqualTo(bay.StopX).Within(0.001f));
                Assert.That((marking.StopBar[1] + marking.StopBar[3]) * 0.5f,
                    Is.EqualTo(bay.StopZ).Within(0.001f));
                Assert.That(float.IsFinite(marking.LabelX), Is.True);
                Assert.That(float.IsFinite(marking.LabelZ), Is.True);
                Assert.That(float.IsFinite(marking.LabelYawDegrees), Is.True);
            }
        }

        [Test]
        public void MarkingGeometryIsCachedForTheStaticGeneratedLayout()
        {
            Assert.That(AdelaideStandMarkings.All(), Is.SameAs(AdelaideStandMarkings.All()));
        }
    }
}

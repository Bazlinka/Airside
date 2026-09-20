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
        public void EveryOperationalStandGetsItsRealReference()
        {
            var markings = AdelaideStandMarkings.All();

            Assert.That(markings.Length,
                Is.EqualTo(AdelaideLayout.Bays.Length + AdelaideLayout.TerminalGates.Length));
            Assert.That(markings.Select(marking => marking.StandId),
                Is.EqualTo(AdelaideLayout.Bays.Select(b => b.Id)
                    .Concat(AdelaideLayout.TerminalGates.Select(g => g.Id))));
            Assert.That(markings.Select(marking => marking.Reference),
                Is.EqualTo(AdelaideLayout.Bays.Select(b => b.Reference)
                    .Concat(AdelaideLayout.TerminalGates.Select(g => g.Reference))));
        }

        [Test]
        public void LeadInsAndStopBarsTerminateAtGeneratedBayStops()
        {
            var markings = AdelaideStandMarkings.All();
            for (var i = 0; i < AdelaideLayout.Bays.Length; i++)
            {
                var marking = markings[i];
                var bay = AdelaideLayout.Bays[i];

                Assert.That(marking.LeadIn[^2], Is.EqualTo(bay.StopX).Within(0.001f));
                Assert.That(marking.LeadIn[^1], Is.EqualTo(bay.StopZ).Within(0.001f));
                Assert.That((marking.StopBar[0] + marking.StopBar[2]) * 0.5f,
                    Is.EqualTo(bay.StopX).Within(0.001f));
                Assert.That((marking.StopBar[1] + marking.StopBar[3]) * 0.5f,
                    Is.EqualTo(bay.StopZ).Within(0.001f));
                Assert.That(marking.Envelope.Length, Is.EqualTo(8));
                Assert.That(AdelaideStandMarkings.PolylineLength(marking.LeadIn),
                    Is.GreaterThan(AdelaideStandMarkings.LeadInLengthMetres * 0.8f));
                Assert.That(float.IsFinite(marking.LabelX), Is.True);
                Assert.That(float.IsFinite(marking.LabelZ), Is.True);
                Assert.That(float.IsFinite(marking.LabelYawDegrees), Is.True);
                Assert.That(marking.LabelCharacterSize, Is.EqualTo(AdelaideStandMarkings.RegionalLabelSize));
            }
        }

        [Test]
        public void Gate13_GetsAJetSizedLeadInStopBarAndIdentifier()
        {
            var marking = AdelaideStandMarkings.All().Single(item => item.StandId == "GATE-13");
            var gate = AdelaideLayout.TerminalGates.Single(item => item.Id == "GATE-13");

            Assert.That(marking.LeadIn[^2], Is.EqualTo(gate.NoseX).Within(0.001f));
            Assert.That(marking.LeadIn[^1], Is.EqualTo(gate.NoseZ).Within(0.001f));
            var leadLength = AdelaideStandMarkings.PolylineLength(marking.LeadIn);
            var barLength = Math.Sqrt(
                Math.Pow(marking.StopBar[2] - marking.StopBar[0], 2)
                + Math.Pow(marking.StopBar[3] - marking.StopBar[1], 2));
            Assert.That(leadLength, Is.EqualTo(AdelaideStandMarkings.TerminalLeadInLengthMetres).Within(1.0));
            Assert.That(barLength, Is.EqualTo(AdelaideStandMarkings.TerminalStopBarWidthMetres).Within(0.1));
            Assert.That(marking.Envelope.Length, Is.EqualTo(8));
            Assert.That(marking.LabelCharacterSize, Is.EqualTo(AdelaideStandMarkings.TerminalLabelSize));
            Assert.That(float.IsFinite(marking.LabelX) && float.IsFinite(marking.LabelZ), Is.True);
        }

        [Test]
        public void MarkingGeometryIsCachedForTheStaticGeneratedLayout()
        {
            Assert.That(AdelaideStandMarkings.All(), Is.SameAs(AdelaideStandMarkings.All()));
        }
    }
}

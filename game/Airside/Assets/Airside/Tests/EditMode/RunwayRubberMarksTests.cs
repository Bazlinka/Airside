using System.Linq;
using Airside.Presentation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class RunwayRubberMarksTests
    {
        [Test]
        public void Streaks_StayOnTheRunwayInsideTheEdgeLines()
        {
            var edge = AirsideRunwayMarkings.EdgeOuterZ - AirsideRunwayMarkings.EdgeWidth;
            foreach (var s in RunwayRubberMarks.All())
            {
                Assert.That(System.Math.Abs(s.CentreZ) + s.Width * 0.5f, Is.LessThan(edge));
                Assert.That(s.CentreX - s.Length * 0.5f, Is.GreaterThan(AirsideRunwayMarkings.WestThresholdX));
                Assert.That(s.CentreX + s.Length * 0.5f, Is.LessThan(AirsideRunwayMarkings.EastThresholdX));
            }
        }

        [Test]
        public void Streaks_ClusterInTheTouchdownZonesNotMidRunway()
        {
            var all = RunwayRubberMarks.All();
            Assert.That(all.Count, Is.EqualTo(RunwayRubberMarks.StreaksAtWestEnd + RunwayRubberMarks.StreaksAtEastEnd));
            var mid = all.Count(s => System.Math.Abs(s.CentreX) < 300f);
            Assert.That(mid, Is.EqualTo(0));
            var west = all.Count(s => s.CentreX < 0f);
            Assert.That(west, Is.EqualTo(RunwayRubberMarks.StreaksAtWestEnd), "05 is the busier touchdown end");
            // Not painted over the centreline: gear tracks sit either side of it.
            Assert.That(all.Count(s => System.Math.Abs(s.CentreZ) < 0.5f), Is.LessThan(all.Count / 10));
            Assert.That(all.Any(s => s.Heavy) && all.Any(s => !s.Heavy), Is.True);
        }

        [Test]
        public void Streaks_AreTheSameEveryRun()
        {
            var a = RunwayRubberMarks.Generate();
            var b = RunwayRubberMarks.Generate();
            Assert.That(a.Length, Is.EqualTo(b.Length));
            for (var i = 0; i < a.Length; i++)
            {
                Assert.That(a[i].CentreX, Is.EqualTo(b[i].CentreX));
                Assert.That(a[i].CentreZ, Is.EqualTo(b[i].CentreZ));
                Assert.That(a[i].Length, Is.EqualTo(b[i].Length));
            }
        }
    }
}

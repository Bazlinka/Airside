using System.Linq;
using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class StandOilStainsTests
    {
        [Test]
        public void Stains_CoverEveryBayAndGate()
        {
            var all = StandOilStains.All();
            var expected = AdelaideLayout.Bays.Length * StandOilStains.PatchesPerRegional
                + AdelaideGateAlignment.Gates.Length * StandOilStains.PatchesPerTerminal;
            Assert.That(all.Count, Is.EqualTo(expected));
            Assert.That(all.Any(s => s.Heavy) && all.Any(s => !s.Heavy), Is.True);
        }

        [Test]
        public void Stains_SitNearAStandStopNotOnTheRunway()
        {
            foreach (var stain in StandOilStains.All())
            {
                Assert.That(System.Math.Abs(stain.CentreZ), Is.GreaterThan(80f),
                    "oil stains belong on the apron, not the 05/23 strip");
                Assert.That(stain.RadiusAlong, Is.GreaterThan(0.5f).And.LessThan(12f));
                Assert.That(stain.RadiusAcross, Is.GreaterThan(0.4f).And.LessThan(10f));

                var nearStand = false;
                foreach (var bay in AdelaideLayout.Bays)
                {
                    var dx = stain.CentreX - bay.StopX;
                    var dz = stain.CentreZ - bay.StopZ;
                    if (dx * dx + dz * dz < 45f * 45f)
                    {
                        nearStand = true;
                        break;
                    }
                }

                if (!nearStand)
                {
                    foreach (var gate in AdelaideGateAlignment.Gates)
                    {
                        var dx = stain.CentreX - gate.NoseX;
                        var dz = stain.CentreZ - gate.NoseZ;
                        if (dx * dx + dz * dz < 70f * 70f)
                        {
                            nearStand = true;
                            break;
                        }
                    }
                }

                Assert.That(nearStand, Is.True, $"stain at ({stain.CentreX:0},{stain.CentreZ:0}) is orphaned");
            }
        }

        [Test]
        public void Stains_AreDeterministic()
        {
            var a = StandOilStains.Generate();
            var b = StandOilStains.Generate();
            Assert.That(a.Length, Is.EqualTo(b.Length));
            for (var i = 0; i < a.Length; i++)
            {
                Assert.That(a[i].CentreX, Is.EqualTo(b[i].CentreX));
                Assert.That(a[i].CentreZ, Is.EqualTo(b[i].CentreZ));
                Assert.That(a[i].Heavy, Is.EqualTo(b[i].Heavy));
            }
        }

        [Test]
        public void EllipseCorners_ReturnAClosedLoopAroundTheCentre()
        {
            var stain = new OilStain(100f, 200f, 3f, 2f, 30f, true);
            var corners = StandOilStains.EllipseCorners(stain);
            Assert.That(corners.Length, Is.EqualTo(StandOilStains.EllipseSides * 2));
            var minX = float.MaxValue;
            var maxX = float.MinValue;
            var minZ = float.MaxValue;
            var maxZ = float.MinValue;
            for (var i = 0; i < corners.Length; i += 2)
            {
                minX = System.Math.Min(minX, corners[i]);
                maxX = System.Math.Max(maxX, corners[i]);
                minZ = System.Math.Min(minZ, corners[i + 1]);
                maxZ = System.Math.Max(maxZ, corners[i + 1]);
            }

            Assert.That(minX, Is.LessThan(stain.CentreX));
            Assert.That(maxX, Is.GreaterThan(stain.CentreX));
            Assert.That(minZ, Is.LessThan(stain.CentreZ));
            Assert.That(maxZ, Is.GreaterThan(stain.CentreZ));
            Assert.That(maxX - minX, Is.GreaterThan(2f).And.LessThan(8f));
        }
    }
}

using System.Linq;
using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class ApronSurfaceWearTests
    {
        [Test]
        public void Marks_IncludePatchesAndDrainageOnEveryApronAttempt()
        {
            var all = ApronSurfaceWear.All();
            Assert.That(all.Count, Is.GreaterThan(0));
            Assert.That(all.Any(m => m.Drainage), Is.True);
            Assert.That(all.Any(m => !m.Drainage), Is.True);
            Assert.That(all.Count(m => m.Drainage),
                Is.LessThanOrEqualTo(AdelaideLayout.Aprons.Length * ApronSurfaceWear.PitsPerApron));
            Assert.That(all.Count(m => !m.Drainage),
                Is.LessThanOrEqualTo(AdelaideLayout.Aprons.Length * ApronSurfaceWear.PatchesPerApron));
        }

        [Test]
        public void Marks_StayInsideAnApronWithEdgeInset()
        {
            foreach (var mark in ApronSurfaceWear.All())
            {
                var inside = false;
                foreach (var apron in AdelaideLayout.Aprons)
                {
                    if (!ApronSurfaceWear.Contains(apron.Xz, mark.CentreX, mark.CentreZ))
                        continue;
                    Assert.That(ApronSurfaceWear.DistanceToEdge(apron.Xz, mark.CentreX, mark.CentreZ),
                        Is.GreaterThanOrEqualTo(ApronSurfaceWear.EdgeInsetMetres - 0.05f));
                    var corners = ApronSurfaceWear.Corners(mark);
                    for (var i = 0; i < corners.Length; i += 2)
                    {
                        Assert.That(ApronSurfaceWear.Contains(apron.Xz,corners[i],corners[i+1]),
                            Is.True, "rotated repair corner left its apron");
                        Assert.That(ApronSurfaceWear.DistanceToEdge(apron.Xz,corners[i],corners[i+1]),
                            Is.GreaterThanOrEqualTo(.95f));
                    }
                    inside = true;
                    break;
                }

                Assert.That(inside, Is.True, $"mark at ({mark.CentreX:0},{mark.CentreZ:0}) left the aprons");
            }
        }

        [Test]
        public void Marks_AreDeterministic()
        {
            var a = ApronSurfaceWear.Generate();
            var b = ApronSurfaceWear.Generate();
            Assert.That(a.Length, Is.EqualTo(b.Length));
            for (var i = 0; i < a.Length; i++)
            {
                Assert.That(a[i].CentreX, Is.EqualTo(b[i].CentreX));
                Assert.That(a[i].CentreZ, Is.EqualTo(b[i].CentreZ));
                Assert.That(a[i].Drainage, Is.EqualTo(b[i].Drainage));
            }
        }

        [Test]
        public void DrainagePits_AreSmallerThanPatchRepairs()
        {
            var pits = ApronSurfaceWear.All().Where(m => m.Drainage).ToList();
            var patches = ApronSurfaceWear.All().Where(m => !m.Drainage).ToList();
            Assert.That(pits, Is.Not.Empty);
            Assert.That(patches, Is.Not.Empty);
            Assert.That(pits.Max(p => p.HalfX), Is.LessThan(patches.Min(p => p.HalfX)));
        }
    }
}

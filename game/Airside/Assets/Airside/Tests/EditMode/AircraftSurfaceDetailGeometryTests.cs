using System;
using Airside.Presentation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class AircraftSurfaceDetailGeometryTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void HingeSeamFollowsTaperAndCantWithoutPaintingTheOppositeFace(bool vertical)
        {
            var skin = new[] { 0f,.2f,0f, 0f,.2f,2f, 4f,.6f,-1f, 4f,.6f,0f,
                0f,-.2f,0f, 0f,-.2f,2f, 4f,.2f,-1f, 4f,.2f,0f };
            var triangles = new[] { 0,1,3, 0,3,2, 4,7,5, 4,6,7,
                0,4,5, 0,5,1, 2,3,7, 2,7,6, 0,2,6, 0,6,4, 1,5,7, 1,7,3 };
            if (vertical)
                for (var i = 0; i < skin.Length; i += 3)
                { var x = skin[i]; skin[i] = skin[i+1]; skin[i+1] = x; }
            var seam = AircraftSurfaceDetailGeometry.ControlSeam(skin, triangles, vertical);
            Assert.That(seam.Triangles.Length, Is.GreaterThan(0));
            for (var i = 0; i < seam.Positions.Length; i += 3)
            {
                var span = seam.Positions[i + (vertical ? 1 : 0)];
                var depth = seam.Positions[i + (vertical ? 0 : 1)];
                var top = Math.Abs(depth - (.204f + .1f*span)) < .00001f;
                var bottom = Math.Abs(depth - (-.204f + .1f*span)) < .00001f;
                Assert.That(top || (vertical && bottom), Is.True, "Seam must hug its actual face.");
                Assert.That(seam.Positions[i+2], Is.LessThan(2f-.5f*span));
            }
        }

        [TestCase(-1)]
        [TestCase(1)]
        public void ClipsToCurvedFrontFaceAndNeverIncludesTheBack(int side)
        {
            // Two sloped skin facets and an inward-facing back layer. A flat
            // bounding-box decal would miss the crease by 10 cm at its centre.
            var skin = new[] {
                1.8f * side, -1f, -1f, 1.8f * side, -1f, 1f,
                2f * side, 0f, -1f, 2f * side, 0f, 1f,
                1.8f * side, 1f, -1f, 1.8f * side, 1f, 1f,
                1.7f * side, -1f, -1f, 1.7f * side, -1f, 1f,
                1.7f * side, 1f, -1f, 1.7f * side, 1f, 1f };
            var triangles = new[] { 0, 2, 3, 0, 3, 1, 2, 4, 5, 2, 5, 3, 6, 9, 8, 6, 7, 9 };
            if (side < 0)
                for (var i = 0; i < triangles.Length; i += 3)
                {
                    var t = triangles[i + 1]; triangles[i + 1] = triangles[i + 2]; triangles[i + 2] = t;
                }
            var patch = AircraftSurfaceDetailGeometry.Rectangle(skin, triangles, side, -0.5f, 0.5f, -0.3f, 0.3f);
            Assert.That(patch.Triangles.Length, Is.GreaterThan(0));
            Assert.That(patch.Triangles, Is.All.InRange(0, patch.Positions.Length / 3 - 1));
            for (var i = 0; i < patch.Positions.Length; i += 3)
            {
                var y = patch.Positions[i + 1]; var z = patch.Positions[i + 2];
                Assert.That(y, Is.InRange(-0.500001f, 0.500001f));
                Assert.That(z, Is.InRange(-0.300001f, 0.300001f));
                Assert.That(patch.Positions[i] * side,
                    Is.EqualTo(2f - Math.Abs(y) * 0.2f + 0.006f).Within(0.00001f));
            }
            var area = 0f;
            for (var i = 0; i < patch.Triangles.Length; i += 3)
            {
                var a = patch.Triangles[i] * 3; var b = patch.Triangles[i + 1] * 3; var c = patch.Triangles[i + 2] * 3;
                var nx = (patch.Positions[b + 1] - patch.Positions[a + 1]) * (patch.Positions[c + 2] - patch.Positions[a + 2])
                    - (patch.Positions[b + 2] - patch.Positions[a + 2]) * (patch.Positions[c + 1] - patch.Positions[a + 1]);
                Assert.That(nx * side, Is.GreaterThan(0f));
                area += Math.Abs(nx) * 0.5f;
            }
            Assert.That(area, Is.EqualTo(0.6f).Within(0.00001f), "No duplicate back-face paint.");
        }

        [Test]
        public void InwardWoundClosedPanelStillGetsDetailsOnItsOutside()
        {
            var skin = new[] { 1f, -1f, -1f, 1f, -1f, 1f, 1f, 1f, -1f, 1f, 1f, 1f,
                0.9f, -1f, -1f, 0.9f, -1f, 1f, 0.9f, 1f, -1f, 0.9f, 1f, 1f };
            // Inward normals on all six box faces.
            var triangles = new[] { 0, 3, 2, 0, 1, 3, 4, 6, 7, 4, 7, 5,
                0, 2, 6, 0, 6, 4, 1, 5, 7, 1, 7, 3, 0, 4, 5, 0, 5, 1, 2, 3, 7, 2, 7, 6 };
            var patch = AircraftSurfaceDetailGeometry.Rectangle(skin, triangles, 1, -0.5f, 0.5f, -0.5f, 0.5f);
            Assert.That(patch.Triangles.Length, Is.GreaterThan(0));
            for (var i = 0; i < patch.Positions.Length; i += 3)
                Assert.That(patch.Positions[i], Is.EqualTo(1.006f).Within(0.00001f));
        }

        [Test]
        public void APanelOutsideTheSkinProducesNoFloatingGeometry()
        {
            var patch = AircraftSurfaceDetailGeometry.Rectangle(new[] { 1f, 0f, 0f, 1f, 1f, 0f, 1f, 1f, 1f },
                new[] { 0, 1, 2 }, 1, 5f, 6f, 5f, 6f);
            Assert.That(patch.Triangles, Is.Empty);
        }

        [Test]
        public void InvalidIndicesAreRejectedBeforeBuildingAMesh()
        {
            Assert.Throws<ArgumentException>(() => AircraftSurfaceDetailGeometry.Rectangle(
                new float[9], new[] { 0, 1, 3 }, 1, 0f, 1f, 0f, 1f));
        }
    }
}

using Airside.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Airside.Tests
{
    /// <summary>
    /// Every case here builds its triangles with separate, unwelded vertex instances at
    /// shared positions — exactly how this project's procedural generators build adjacent
    /// quad/triangle faces (see <see cref="MeshNormalSmoothing"/>'s own doc comment) —
    /// rather than relying on Unity's default index-sharing behaviour.
    /// </summary>
    public sealed class MeshNormalSmoothingTests
    {
        [Test]
        public void Compute_WeldsAndBlendsNormalsWithinTheAngleThreshold()
        {
            // Two triangles folded 30 degrees along a shared edge - well inside the
            // 60 degree default, so the shared-edge vertices should end up with one
            // blended normal instead of each triangle's own flat face normal.
            var angle = 30f * Mathf.Deg2Rad;
            var vertices = new[]
            {
                new Vector3(0f, 0f, 0f), new Vector3(1f, 0f, 0f), new Vector3(0f, 1f, 0f),
                new Vector3(0f, 0f, 0f), new Vector3(1f, 0f, 0f),
                new Vector3(0f, Mathf.Cos(angle), Mathf.Sin(angle)),
            };
            var triangles = new[] { 0, 1, 2, 3, 4, 5 };

            var normals = MeshNormalSmoothing.Compute(vertices, triangles, 60f);

            Assert.That(Vector3.Angle(normals[0], normals[3]), Is.LessThan(0.01f),
                "the two unwelded copies of the same shared-edge position must agree");
            Assert.That(Vector3.Angle(normals[0], Vector3.forward), Is.GreaterThan(1f)
                .And.LessThan(angle * Mathf.Rad2Deg),
                "the blended normal should sit between the two faces' own normals, not equal either one");
        }

        [Test]
        public void Compute_KeepsAHardEdgeCrispPastTheAngleThreshold()
        {
            // Same shape folded 90 degrees instead - a genuine hard edge (a wingtip
            // against a fuselage, a panel break) that must not be smoothed away.
            var angle = 90f * Mathf.Deg2Rad;
            var vertices = new[]
            {
                new Vector3(0f, 0f, 0f), new Vector3(1f, 0f, 0f), new Vector3(0f, 1f, 0f),
                new Vector3(0f, 0f, 0f), new Vector3(1f, 0f, 0f),
                new Vector3(0f, Mathf.Cos(angle), Mathf.Sin(angle)),
            };
            var triangles = new[] { 0, 1, 2, 3, 4, 5 };

            var normals = MeshNormalSmoothing.Compute(vertices, triangles, 60f);

            Assert.That(Vector3.Angle(normals[0], Vector3.forward), Is.LessThan(0.01f),
                "triangle A's own face normal should be untouched");
            Assert.That(Vector3.Angle(normals[3], normals[0]), Is.GreaterThan(45f),
                "a 90 degree fold must not blend across the shared edge");
        }

        [Test]
        public void Compute_LeavesAnAlreadyWeldedFlatQuadUnchanged()
        {
            // A single quad built the way RecalculateNormals already handles correctly
            // (two triangles sharing real, welded indices) - the smoothing pass must not
            // regress this baseline case.
            var vertices = new[]
            {
                new Vector3(0f, 0f, 0f), new Vector3(1f, 0f, 0f),
                new Vector3(1f, 1f, 0f), new Vector3(0f, 1f, 0f),
            };
            var triangles = new[] { 0, 1, 2, 0, 2, 3 };

            var normals = MeshNormalSmoothing.Compute(vertices, triangles, 60f);

            foreach (var normal in normals)
                Assert.That(Vector3.Angle(normal, Vector3.forward), Is.LessThan(0.01f));
        }
    }
}

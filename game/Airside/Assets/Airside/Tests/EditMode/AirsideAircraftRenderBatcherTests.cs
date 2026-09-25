using System.Linq;
using Airside.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Airside.Tests
{
    public sealed class AirsideAircraftRenderBatcherTests
    {
        [Test]
        public void WidebodyGlazing_IsBatchedWithoutRemovingNamedSourceParts()
        {
            var parent = new GameObject("aircraft-render-batch-test").transform;
            try
            {
                Assert.That(ArtPresentationLoader.TryInstantiate(
                    AircraftVisualProfiles.Boeing78710.ArtRelativePath,
                    parent,
                    out var root,
                    localPosition: Vector3.zero), Is.True);

                var sources = root.GetComponentsInChildren<MeshRenderer>(true)
                    .Where(AirsideAircraftRenderBatcher.IsStaticGlazing)
                    .ToArray();
                Assert.That(sources.Length, Is.GreaterThan(80),
                    "widebody source has the reproduced transparent draw-call fan-out");
                var sourceTriangles = sources.Sum(r => r.GetComponent<MeshFilter>().sharedMesh.triangles.Length / 3);

                var batches = AirsideAircraftRenderBatcher.CombineStaticGlazing(root);

                Assert.That(batches, Is.GreaterThan(0));
                Assert.That(sources.All(r => r.forceRenderingOff), Is.True,
                    "named source renderers remain inspectable but cannot be submitted twice");
                Assert.That(root.GetComponentsInChildren<Transform>(true)
                    .Any(t => t.name == "cabin_window_1"), Is.True, "source transform names survive batching");
                var batchRenderers = root.GetComponentsInChildren<MeshRenderer>(true)
                    .Where(r => r.name.StartsWith(AirsideAircraftRenderBatcher.BatchNamePrefix))
                    .ToArray();
                Assert.That(batchRenderers.Length, Is.EqualTo(batches));
                Assert.That(batchRenderers.All(r => !r.forceRenderingOff
                    && r.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.Off), Is.True);
                Assert.That(batchRenderers.Sum(r => (int)r.GetComponent<MeshFilter>().sharedMesh.GetIndexCount(0) / 3),
                    Is.EqualTo(sourceTriangles), "batch preserves every glazing triangle");
                Assert.That(batchRenderers.Length, Is.LessThan(sources.Length / 10),
                    "transparent submissions fall by at least an order of magnitude");
            }
            finally
            {
                Object.DestroyImmediate(parent.gameObject);
            }
        }

        [Test]
        public void Batching_IsIdempotent()
        {
            var parent = new GameObject("aircraft-render-batch-idempotence").transform;
            try
            {
                Assert.That(ArtPresentationLoader.TryInstantiate(
                    AircraftVisualProfiles.Boeing78710.ArtRelativePath,
                    parent,
                    out var root,
                    localPosition: Vector3.zero), Is.True);
                var first = AirsideAircraftRenderBatcher.CombineStaticGlazing(root);
                var second = AirsideAircraftRenderBatcher.CombineStaticGlazing(root);
                Assert.That(first, Is.GreaterThan(0));
                Assert.That(second, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(parent.gameObject);
            }
        }

        [Test]
        public void DestroyingAircraft_ReleasesGeneratedBatchMeshes()
        {
            var parent = new GameObject("aircraft-render-batch-cleanup").transform;
            try
            {
                Assert.That(ArtPresentationLoader.TryInstantiate(
                    AircraftVisualProfiles.Boeing78710.ArtRelativePath,
                    parent,
                    out var root,
                    localPosition: Vector3.zero), Is.True);
                Assert.That(AirsideAircraftRenderBatcher.CombineStaticGlazing(root), Is.GreaterThan(0));
                var meshes = root.GetComponentsInChildren<MeshRenderer>(true)
                    .Where(r => r.name.StartsWith(AirsideAircraftRenderBatcher.BatchNamePrefix))
                    .Select(r => r.GetComponent<MeshFilter>().sharedMesh)
                    .ToArray();

                Object.DestroyImmediate(parent.gameObject);

                Assert.That(meshes.All(mesh => mesh == null), Is.True,
                    "runtime-generated glazing meshes are released with their aircraft");
            }
            finally
            {
                if (parent != null)
                    Object.DestroyImmediate(parent.gameObject);
            }
        }
    }
}

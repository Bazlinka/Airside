using System.Linq;
using System.Reflection;
using Airside.Domain;
using Airside.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Airside.Tests
{
    /// <summary>
    /// Every aircraft door has a hollow behind it, so a folded-away door shows a doorway
    /// rather than bare fuselage, and the hollow is the same shape as the door it sits behind.
    /// </summary>
    public sealed class AircraftDoorwayTests
    {
        private static Transform Build(AircraftType type)
        {
            var method = typeof(AirsidePrototype).GetMethod("BuildAircraftForType", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null, "BuildAircraftForType dispatch");
            return (Transform)method.Invoke(null, new object[] { $"Doorway {type.Id}", type, Color.white, null });
        }

        private static Bounds WorldBounds(Transform root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers)
                bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        [Test]
        public void EveryAirliner_HasAHiddenHollowBehindItsPassengerDoor()
        {
            foreach (var spec in AircraftCatalogue.All)
            {
                var root = Build(spec.Type);
                try
                {
                    var doorways = root.GetComponentsInChildren<Transform>(true)
                        .Where(t => t.name.StartsWith("CabinDoor") && t.GetComponent<MeshFilter>() != null)
                        .ToArray();
                    Assert.That(doorways, Is.Not.Empty, spec.Name + " has a passenger door leaf");
                    foreach (var leaf in doorways)
                    {
                        var doorway = leaf.GetComponent<AircraftDoorway>();
                        Assert.That(doorway, Is.Not.Null, $"{spec.Name} {leaf.name} has a doorway behind it");
                        Assert.That(doorway.Shell.activeSelf, Is.False, "hidden while the door is shut");
                        var layers = doorway.Shell.GetComponentsInChildren<MeshFilter>(true);
                        Assert.That(layers, Has.Length.EqualTo(3), "reveal, cabin and light");

                        var leafBounds = leaf.GetComponent<Renderer>().bounds;
                        leafBounds.Expand(0.05f);
                        foreach (var layer in layers)
                        {
                            var bounds = layer.GetComponent<Renderer>().bounds;
                            Assert.That(leafBounds.Contains(bounds.min) && leafBounds.Contains(bounds.max), Is.True,
                                $"{spec.Name} {layer.name} lies within its door ({bounds} vs {leafBounds})");
                            Assert.That(layer.sharedMesh.vertexCount, Is.GreaterThan(20));
                        }
                    }
                }
                finally
                {
                    Object.DestroyImmediate(root.gameObject);
                }
            }
        }

        [Test]
        public void TheHollowAddsNothingToTheAircraftsOutline()
        {
            foreach (var spec in AircraftCatalogue.All)
            {
                var root = Build(spec.Type);
                try
                {
                    var withHollow = WorldBounds(root);
                    foreach (var shell in root.GetComponentsInChildren<AircraftDoorway>(true).Select(d => d.Shell))
                        shell.SetActive(false);
                    foreach (var layer in root.GetComponentsInChildren<MeshRenderer>(true)
                                 .Where(r => r.name.StartsWith("Doorway ")))
                        layer.enabled = false;
                    var without = WorldBounds(root);
                    Assert.That(withHollow.size.x, Is.EqualTo(without.size.x).Within(0.05f), spec.Name);
                    Assert.That(withHollow.size.z, Is.EqualTo(without.size.z).Within(0.05f), spec.Name);
                }
                finally
                {
                    Object.DestroyImmediate(root.gameObject);
                }
            }
        }
    }
}

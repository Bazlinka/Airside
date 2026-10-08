using System.Linq;
using System.Reflection;
using Airside.Domain;
using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Airside.Tests
{
    public sealed class AircraftLightingInstallationTests
    {
        [TestCase("ATR42")]
        [TestCase("SF34")]
        [TestCase("DH8D")]
        [TestCase("E190")]
        [TestCase("A223")]
        [TestCase("A320")]
        [TestCase("A21N")]
        [TestCase("B738")]
        [TestCase("B38M")]
        [TestCase("A359")]
        [TestCase("A339")]
        [TestCase("B789")]
        [TestCase("B78X")]
        [TestCase("B412")]
        public void BuiltAirframe_HasCompletePositionLightsAndMovingTaxiInstallation(string id)
        {
            var type = AircraftCatalogue.All.Concat(AircraftCatalogue.Rotorcraft).First(s => s.Id == id).Type;
            var root = (Transform)typeof(AirsidePrototype).GetMethod("BuildAircraftForType", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { "Lighting check", type, Color.blue, null });
            try
            {
                var children = root.GetComponentsInChildren<Transform>(true);
                foreach (var kind in new[] { AircraftNavigationLight.Left, AircraftNavigationLight.Right, AircraftNavigationLight.Tail })
                    Assert.That(children.Any(t => AirsideAircraftParts.NavigationLightFor(t.name) == kind), Is.True, id + " " + kind);
                if (!type.IsRotorcraft)
                {
                    var taxi = children.First(t => t.name == "TaxiLight");
                    var nose = children.First(t => t.name == "Gear nose");
                    Assert.That(taxi.IsChildOf(nose), Is.True, id + " taxi lamp must steer and retract with nose gear");
                }
                if (AircraftLightingProfile.FamilyOf(type) is AircraftLightingFamily.RegionalJet
                    or AircraftLightingFamily.Narrowbody or AircraftLightingFamily.Widebody)
                {
                    Assert.That(children.Any(t => t.name == "Beacon bottom"), Is.True, id + " belly beacon");
                    var top = children.First(t => t.name == "Beacon");
                    var lampY = top.TransformPoint(top.GetComponent<MeshFilter>().sharedMesh.bounds.center).y;
                    var bodyRoof = children.Where(t => t.name == "Fuselage" || t.name == "Fuselage mid" || t.name == "FuselageMid")
                        .Max(t => t.GetComponent<Renderer>().bounds.max.y);
                    Assert.That(lampY, Is.EqualTo(bodyRoof + 0.07f).Within(0.05f), id + " crown beacon");
                }
                var partsType = typeof(AirsidePrototype).GetNestedType("AircraftViewParts", BindingFlags.NonPublic);
                var parts = System.Activator.CreateInstance(partsType);
                var classify = new object[] { AirsideNamedChildren.Get(root), AirsideNamedChildren.Names(root), parts };
                typeof(AirsidePrototype).GetMethod("ClassifyAnimatedParts", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, classify);
                parts = classify[2];
                var lamps = partsType.GetField("LightsAndGear").GetValue(parts);
                var update = typeof(AirsidePrototype).GetMethod("UpdateAircraftLightsAndGear", BindingFlags.Static | BindingFlags.NonPublic);
                root.position = Vector3.up * AirsideFlightPath.GroundY;
                update.Invoke(null, new object[] { lamps, AircraftPhase.Landing, 0.1f, 0.9f, 0f, 0.02f, EngineState.Running, null, type });
                var landing = root.GetComponentsInChildren<Light>(true).Where(l => l.type == LightType.Spot && l.transform.parent.name.StartsWith("LandingLight")).ToArray();
                Assert.That(landing.Length, Is.GreaterThan(0), id);
                foreach (var l in landing)
                {
                    Assert.That(l.enabled, Is.True, id);
                    Assert.That(Vector3.Dot(l.transform.forward, root.forward), Is.GreaterThan(0.8f), id + " forward beam");
                    Assert.That(l.transform.forward.y, Is.LessThan(0f), id + " downward beam");
                }
                var strobes = root.GetComponentsInChildren<Transform>(true).Where(t => t.name == "White strobe").ToArray();
                Assert.That(strobes.Length, Is.GreaterThanOrEqualTo(2), id + " wingtip strobes");
                Assert.That(strobes.All(t => t.GetComponent<Renderer>() != null), Is.True, id + " separate clear strobe lenses");
                root.position += Vector3.up * 4000f;
                update.Invoke(null, new object[] { lamps, AircraftPhase.Departed, 0.1f, 1f, 0f, 0.02f, EngineState.Running, null, type });
                Assert.That(landing.All(l => !l.enabled), Is.True, id + " cruise beams off");
                root.position = Vector3.up * AirsideFlightPath.GroundY;
                update.Invoke(null, new object[] { lamps, AircraftPhase.AtStand, 0.1f, 1f, 0f, 0.02f, EngineState.Running, null, type });
                Assert.That(landing.All(l => !l.enabled && l.transform.parent.parent.gameObject.activeSelf), Is.True, id + " dark lamps remain fitted");

                // The fitting pass must be safe to repeat without duplicate lamps.
                var beforeRefit = root.GetComponentsInChildren<Transform>(true).Length;
                typeof(AirsidePrototype).GetMethod("FitAircraftLighting", BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, new object[] { root, type });
                Assert.That(root.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(beforeRefit));
            }
            finally { Object.DestroyImmediate(root.gameObject); }
        }
    }
}

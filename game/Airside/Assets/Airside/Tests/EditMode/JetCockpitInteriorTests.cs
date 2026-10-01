using System.Linq;
using Airside.Domain;
using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Airside.Tests
{
    public sealed class JetCockpitInteriorTests
    {
        [TestCase("B738")]
        [TestCase("B38M")]
        [TestCase("A320")]
        [TestCase("A21N")]
        [TestCase("E190")]
        [TestCase("A223")]
        [TestCase("A359")]
        [TestCase("A339")]
        [TestCase("B789")]
        [TestCase("B78X")]
        public void EveryJetBuildsClosedShellAndRestoresExterior(string id)
        {
            Assert.That(AircraftType.TryFromId(id, out var type), Is.True);
            var root = new GameObject("Aircraft");
            var shell = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shell.transform.SetParent(root.transform, false);
            var renderer = shell.GetComponent<Renderer>();
            var hidden = GameObject.CreatePrimitive(PrimitiveType.Cube);
            hidden.transform.SetParent(root.transform, false);
            hidden.GetComponent<Renderer>().forceRenderingOff = true;
            var wing = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wing.name = "WingLeft"; wing.transform.SetParent(root.transform, false);
            try
            {
                var rig = JetCockpitInterior.Build(root.transform, type);
                Assert.That(rig.Seat.IsChildOf(rig.transform), Is.True);
                Assert.That(rig.Seat.localPosition.x, Is.LessThan(0f));
                var parts = rig.GetComponentsInChildren<Transform>();
                foreach (var name in new[] { "Closed cockpit shell", "Overhead panel", "Centre pedestal" })
                    Assert.That(parts.Any(p => p.name == name), Is.True, id + ": " + name);
                Assert.That(parts.Count(p => p.name.EndsWith(" screen")), Is.EqualTo(rig.Profile.DisplayCount));
                Assert.That(parts.Any(p => p.name == "Sidestick grip"), Is.EqualTo(rig.Profile.Sidestick));
                Assert.That(rig.GetComponentsInChildren<Collider>().Length, Is.Zero);
                rig.Enter();
                rig.Enter(); // Repeated entry must not replace the original visibility snapshot.
                Assert.That(renderer.forceRenderingOff, Is.True);
                Assert.That(wing.GetComponent<Renderer>().forceRenderingOff, Is.False);
                rig.SetReadout("GS 12 kt\nHEIGHT 0 ft\nHDG 050°");
                rig.SetFlightState(root.transform, EngineState.Running, "TAXI");
                rig.Leave();
                Assert.That(renderer.forceRenderingOff, Is.False);
                Assert.That(hidden.GetComponent<Renderer>().forceRenderingOff, Is.True);
                rig.Enter();
                Object.DestroyImmediate(rig.gameObject);
                Assert.That(renderer.forceRenderingOff, Is.False, "Destroy must restore the shell even without Leave");
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}

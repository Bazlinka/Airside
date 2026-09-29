using System.Linq;
using System.Reflection;
using Airside.Domain;
using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace Airside.Tests
{
    /// <summary>
    /// The real ATR model, as the game builds it: its airstair must be the aft-left door and its
    /// hold door forward left, where <see cref="AircraftLayout"/> — and so the boarding path,
    /// crew and baggage train — expects them. Unity-only (it builds the art).
    /// </summary>
    public sealed class PropTurnaroundTests
    {
        private static Transform Build(AircraftType type)
        {
            var method = typeof(AirsidePrototype).GetMethod("BuildAircraftForType",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);
            var args = method.GetParameters().Select(p => p.HasDefaultValue ? p.DefaultValue : null).ToArray();
            args[0] = "Test " + type.Id;
            args[1] = type;
            args[2] = Color.white;
            return (Transform)method.Invoke(null, args);
        }

        [Test]
        public void TheAtrAirstairIsItsAftLeftDoor()
        {
            var atr = Build(AircraftType.Atr42);
            try
            {
                var door = atr.GetComponentsInChildren<Component>(true)
                    .FirstOrDefault(c => c.GetType().Name == "AirstairDoor");
                Assert.That(door, Is.Not.Null, "the ATR has an integral airstair");
                var hinge = atr.InverseTransformPoint(door.transform.position);
                var expected = AircraftLayout.For(AircraftType.Atr42).PassengerDoor;
                Assert.That(hinge.x, Is.LessThan(0f), "left side");
                Assert.That(hinge.z, Is.EqualTo(expected.Z).Within(0.8f), "aft door, as the layout says");
            }
            finally
            {
                Object.DestroyImmediate(atr.gameObject);
            }
        }

        [Test]
        public void TheAtrHoldDoorIsForwardLeft()
        {
            var atr = Build(AircraftType.Atr42);
            try
            {
                var cargo = atr.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Cargo door");
                Assert.That(cargo, Is.Not.Null);
                var renderer = cargo.GetComponentInChildren<Renderer>(true);
                var centre = atr.InverseTransformPoint(renderer.bounds.center);
                var expected = AircraftLayout.For(AircraftType.Atr42).CargoDoor;
                Assert.That(centre.x, Is.LessThan(0f), "left side");
                Assert.That(centre.z, Is.EqualTo(expected.Z).Within(0.8f), "forward, as the layout says");
            }
            finally
            {
                Object.DestroyImmediate(atr.gameObject);
            }
        }

        [TestCase("SF34")]
        [TestCase("DH8D")]
        [TestCase("ATR42")]
        public void PropellersAreWhereTheLayoutSays(string id)
        {
            Assert.That(AircraftType.TryFromId(id, out var type), Is.True);
            var aircraft = Build(type);
            try
            {
                var layout = AircraftLayout.For(type);
                var props = aircraft.GetComponentsInChildren<Transform>(true)
                    .Where(t => t.name.StartsWith("Propeller") && t.GetComponentInChildren<Renderer>(true) != null)
                    .ToArray();
                Assert.That(props, Is.Not.Empty, "propellers are named in the built aircraft");
                foreach (var prop in props)
                {
                    var centre = aircraft.InverseTransformPoint(prop.GetComponentInChildren<Renderer>(true).bounds.center);
                    Assert.That(Mathf.Abs(Mathf.Abs(centre.x) - layout.Engine.X), Is.LessThan(0.9f), $"{id} {prop.name} x");
                    Assert.That(Mathf.Abs(centre.z - layout.Engine.PropellerZ), Is.LessThan(0.9f), $"{id} {prop.name} z");
                }
            }
            finally
            {
                Object.DestroyImmediate(aircraft.gameObject);
            }
        }
    }
}

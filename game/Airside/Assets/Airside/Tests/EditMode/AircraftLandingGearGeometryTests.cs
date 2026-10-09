using System;
using System.Reflection;
using Airside.Domain;
using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Airside.Tests
{
    public sealed class AircraftLandingGearGeometryTests
    {
        private const BindingFlags StaticPrivate = BindingFlags.Static | BindingFlags.NonPublic;

        [Test]
        public void CantedLeg_PivotIsOnTheTopAttachment_NotTheBoundsCentre()
        {
            var leg = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                leg.transform.localScale = new Vector3(0.15f, 1.5f, 0.2f);
                leg.transform.rotation = Quaternion.Euler(0f, 0f, 15f);
                var bounds = leg.GetComponent<Renderer>().bounds;
                var top = AircraftLandingGearGeometry.TopAttachment(leg.transform, bounds);
                Assert.That(Mathf.Abs(top.x - bounds.center.x), Is.GreaterThan(0.1f));
                Assert.That(top.y, Is.EqualTo(bounds.max.y).Within(0.03f));
            }
            finally { Object.DestroyImmediate(leg); }
        }

        [TestCase("ATR42")]
        [TestCase("SF34")]
        [TestCase("DH8D")]
        [TestCase("E190")]
        [TestCase("A223")]
        [TestCase("A320")]
        [TestCase("B738")]
        [TestCase("B38M")]
        [TestCase("A21N")]
        [TestCase("A359")]
        [TestCase("A339")]
        [TestCase("B789")]
        [TestCase("B78X")]
        public void FleetCycle_CacheRebuildAndPauseDoNotJumpTheMetal(string id)
        {
            Assert.That(AircraftType.TryFromId(id, out var aircraftType), Is.True);
            var proto = typeof(AirsidePrototype);
            var root = (Transform)proto.GetMethod("BuildAircraftForType", StaticPrivate).Invoke(null,
                new object[] { "Gear test " + id, aircraftType, Color.blue, null });
            try
            {
                object Classify()
                {
                    var type = proto.GetNestedType("AircraftViewParts", BindingFlags.NonPublic);
                    var parts = Activator.CreateInstance(type);
                    type.GetField("Owner").SetValue(parts, root);
                    var args = new object[] { AirsideNamedChildren.Get(root), AirsideNamedChildren.Names(root), parts };
                    proto.GetMethod("ClassifyAnimatedParts", StaticPrivate).Invoke(null, args);
                    return type.GetField("LightsAndGear").GetValue(args[2]);
                }
                void Step(object parts, AircraftPhase phase, float dt) =>
                    proto.GetMethod("UpdateAircraftLightsAndGear", StaticPrivate).Invoke(null,
                        new object[] { parts, phase, 1f, 0.5f, dt, 0f, null, null, aircraftType });
                var original = Classify();
                Step(original, AircraftPhase.Landing, 0f);
                Step(original, AircraftPhase.Circuit, 3.5f);
                var transforms = root.GetComponentsInChildren<Transform>();
                var positions = new Vector3[transforms.Length];
                var rotations = new Quaternion[transforms.Length];
                for (var i = 0; i < transforms.Length; i++)
                {
                    positions[i] = transforms[i].localPosition;
                    rotations[i] = transforms[i].localRotation;
                }
                var rebuilt = Classify();
                Step(rebuilt, AircraftPhase.Circuit, 0f);
                for (var i = 0; i < transforms.Length; i++)
                {
                    if (!AirsideAircraftParts.IsGearStrut(transforms[i].name)
                        && !AirsideAircraftParts.IsGearDoor(transforms[i].name)
                        && !transforms[i].name.StartsWith("Truck ", StringComparison.Ordinal)) continue;
                    Assert.That(Vector3.Distance(transforms[i].localPosition, positions[i]), Is.LessThan(0.00001f));
                    Assert.That(Quaternion.Angle(transforms[i].localRotation, rotations[i]), Is.LessThan(0.01f), transforms[i].name);
                }
                foreach (var truck in transforms)
                    if (truck.name.StartsWith("Truck ", StringComparison.Ordinal))
                        Assert.That(truck.Find("Bogie beam " + truck.name), Is.Not.Null, "the wheels must carry their axles");
                if (id == "ATR42")
                    foreach (var part in transforms)
                        if (part.name is "Gear L" or "Gear R" or "Gear fairing L" or "Gear fairing R")
                            Assert.That(part.parent.name.StartsWith("Wing ", StringComparison.Ordinal), Is.False,
                                "fuselage sponsons must not follow high-wing flex");
                if (id is "A359" or "A339" or "B789" or "B78X")
                {
                    Assert.That(Array.Exists(transforms, t => t.name == "gear_door_nose_l"), Is.True);
                    Assert.That(Array.Exists(transforms, t => t.name == "Gear door L"), Is.True);
                }
            }
            finally { Object.DestroyImmediate(root.gameObject); }
        }
    }
}

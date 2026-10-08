using System.Reflection;
using Airside.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Airside.Tests
{
    /// <summary>The ATR hold door is flipped to the left side; its animation must not undo the flip.</summary>
    public sealed class AtrCargoDoorTests
    {
        [Test]
        public void RebakingToTheAircraftRotation_KeepsTheDoorWhereItIs_AndSurvivesYawBeingSetToZero()
        {
            var aircraft = new GameObject("Aircraft").transform;
            var door = new GameObject("Cargo door");
            try
            {
                door.transform.SetParent(aircraft, false);
                var filter = door.AddComponent<MeshFilter>();
                filter.sharedMesh = new Mesh
                {
                    vertices = new[] { new Vector3(1f, 0f, 0f), new Vector3(1f, 1f, 0f), new Vector3(1f, 0f, 1f) },
                    triangles = new[] { 0, 1, 2 }
                };
                // What RelocateAtrDoors does: half a turn about the vertical axis, then a shift.
                door.transform.RotateAround(aircraft.position, aircraft.up, 180f);
                door.transform.position += new Vector3(0f, 0f, 2f);
                var before = WorldVertices(door);

                typeof(AirsidePrototype)
                    .GetMethod("RebakePartPivot", BindingFlags.NonPublic | BindingFlags.Static)
                    .Invoke(null, new object[] { door.transform, door.transform.position, (Quaternion?)aircraft.rotation });

                Assert.That(door.transform.localEulerAngles.y, Is.EqualTo(0f).Within(0.01f).Or.EqualTo(360f).Within(0.01f));
                AssertSame(before, WorldVertices(door), "baking the half turn into the mesh moves nothing");

                // UpdateCabinDoor writes the door's Y rotation outright; shut it must leave the door in place.
                var euler = door.transform.localEulerAngles;
                euler.y = 0f;
                door.transform.localEulerAngles = euler;
                AssertSame(before, WorldVertices(door), "a shut door stays in its slot");
            }
            finally
            {
                Object.DestroyImmediate(door.GetComponent<MeshFilter>().sharedMesh);
                Object.DestroyImmediate(aircraft.gameObject);
                Object.DestroyImmediate(door);
            }
        }

        private static Vector3[] WorldVertices(GameObject part)
        {
            var vertices = part.GetComponent<MeshFilter>().sharedMesh.vertices;
            var world = new Vector3[vertices.Length];
            for (var i = 0; i < vertices.Length; i++)
                world[i] = part.transform.TransformPoint(vertices[i]);
            return world;
        }

        private static void AssertSame(Vector3[] expected, Vector3[] actual, string message)
        {
            for (var i = 0; i < expected.Length; i++)
                Assert.That((expected[i] - actual[i]).magnitude, Is.LessThan(0.001f), message);
        }
    }
}

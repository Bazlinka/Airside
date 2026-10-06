using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using Airside.Presentation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>Headless geometry/role checks; does not claim Unity hierarchy or posed feet validation.</summary>
    public sealed class ServiceWheelAndBoardingContactTests
    {
        [TestCase("Fuel truck spare_wheel")]
        [TestCase("Baggage cart tug_steering_wheel")]
        [TestCase("Bus wheel_arch_fl")]
        [TestCase("Wheel chocks")]
        [TestCase("Towbar wheel")]
        public void FixedAccessoriesAreNotRoadWheels(string name) =>
            Assert.That(ServiceVehicleWheelGeometry.IsRoadWheel(name), Is.False);

        [TestCase("Fuel truck wheel_fl")]
        [TestCase("Fuel truck (AI) wheel_mr")]
        [TestCase("Baggage cart cart_wheel_3r")]
        [TestCase("Bus wheel_hub_rr")]
        [TestCase("Fuel truck wheel FL")]
        [TestCase("GPU wheel L")]
        public void AuthoredAndFallbackRoadWheelRolesAreIncluded(string name) =>
            Assert.That(ServiceVehicleWheelGeometry.IsRoadWheel(name), Is.True);

        [TestCase("mdl_fuel_truck_small_v06", 6)]
        [TestCase("mdl_baggage_tug_train_v06", 10)]
        [TestCase("mdl_passenger_bus_apron_v06", 8)]
        public void ShippedWheelPositionsHaveFixedAxleCentresAndRadialDimensions(string asset, int expectedParts)
        {
            var dir = FindArtDirectory();
            using var stream = File.OpenRead(Path.Combine(dir, asset + ".gltf"));
            var gltf = (Gltf)new DataContractJsonSerializer(typeof(Gltf)).ReadObject(stream);
            var selected = 0;
            foreach (var node in gltf.Nodes)
            {
                if (!ServiceVehicleWheelGeometry.IsRoadWheel(node.Name)) continue;
                selected++;
                var accessor = gltf.Accessors[gltf.Meshes[node.Mesh].Primitives[0].Attributes.Position];
                var view = gltf.Views[accessor.View];
                var bytes = File.ReadAllBytes(Path.Combine(dir, gltf.Buffers[view.Buffer].Uri));
                Assert.That(accessor.Component, Is.EqualTo(5126));
                var centre = new float[3];
                var span = new float[3];
                for (var a = 0; a < 3; a++)
                {
                    centre[a] = ServiceVehicleWheelGeometry.Centre(accessor.Min[a], accessor.Max[a]);
                    span[a] = accessor.Max[a] - accessor.Min[a];
                }
                Assert.That(ServiceVehicleWheelGeometry.AxleAxis(span[0], span[1], span[2]), Is.EqualTo(2), node.Name);
                foreach (var degrees in new[] { 0, 90, 180, 360 })
                {
                    var angle = degrees * Math.PI / 180;
                    var low = new[] { float.MaxValue, float.MaxValue, float.MaxValue };
                    var high = new[] { float.MinValue, float.MinValue, float.MinValue };
                    for (var i = 0; i < accessor.Count; i++)
                    {
                        var offset = view.Offset + accessor.Offset + i * (view.Stride == 0 ? 12 : view.Stride);
                        var x = BitConverter.ToSingle(bytes, offset) - centre[0];
                        var y = BitConverter.ToSingle(bytes, offset + 4) - centre[1];
                        var z = BitConverter.ToSingle(bytes, offset + 8) - centre[2];
                        var rotated = new[] { centre[0] + (float)(x * Math.Cos(angle) - y * Math.Sin(angle)),
                            centre[1] + (float)(x * Math.Sin(angle) + y * Math.Cos(angle)), centre[2] + z };
                        Assert.That(Math.Sqrt(Math.Pow(rotated[0] - centre[0], 2) + Math.Pow(rotated[1] - centre[1], 2)),
                            Is.EqualTo(Math.Sqrt(x * x + y * y)).Within(0.00001), node.Name);
                        for (var a = 0; a < 3; a++) { low[a] = Math.Min(low[a], rotated[a]); high[a] = Math.Max(high[a], rotated[a]); }
                    }
                    for (var a = 0; a < 3; a++)
                        Assert.That((low[a] + high[a]) * 0.5f, Is.EqualTo(centre[a]).Within(0.00001), node.Name);
                    Assert.That(high[2] - low[2], Is.EqualTo(span[2]).Within(0.00001));
                    if (degrees == 90 || degrees == 180)
                        Assert.That(high[0] - low[0], Is.EqualTo(degrees == 90 ? span[1] : span[0]).Within(0.00001));
                }
            }
            Assert.That(selected, Is.EqualTo(expectedParts));
        }

        [TestCase(0.039f)]
        [TestCase(0.06f)]
        [TestCase(0.095f)]
        public void StairContactAndPlatformUsePavementRatherThanFlightRoot(float pavement)
        {
            const float motionRoot = 0.72f;
            const float doorAboveMotionRoot = 2.6f;
            var door = motionRoot + doorAboveMotionRoot;
            var rise = BoardingGroundContact.StairRise(door, pavement);
            Assert.That(pavement + 0.42f - 0.84f * 0.5f, Is.EqualTo(pavement).Within(0.00001), "stair tyre contact");
            Assert.That(pavement + rise, Is.EqualTo(door).Within(0.00001), "platform top");
            Assert.That(rise, Is.GreaterThan(doorAboveMotionRoot));
            Assert.That(BoardingGroundContact.StairRun(rise), Is.EqualTo(rise * 1.45f).Within(0.00001));
        }

        [Test]
        public void AdelaidePavementDatumUsesActualRunwaySlabAndApronOffset() =>
            Assert.That(BoardingGroundContact.AdelaideApronY, Is.EqualTo(0.039f).Within(0.000001));

        [Test]
        public void PrefabAxesFollowScaledWheelDimensions()
        {
            Assert.That(ServiceVehicleWheelGeometry.AxleAxis(0.18f, 0.35f, 0.28f), Is.EqualTo(0));
            Assert.That(ServiceVehicleWheelGeometry.AxleAxis(0.84f, 0.16f, 0.84f), Is.EqualTo(1));
            Assert.That(ServiceVehicleWheelGeometry.AxleAxis(0.28f, 0.35f, 0.18f), Is.EqualTo(2));
        }

        [Test]
        public void InsertedPivotPreservesInitialVerticesWithRotatedScaledPrefabAndParent()
        {
            // The runtime inherits the original part scale/rotation onto the pivot, then
            // translates its child by -centre. Model a 90-degree part Z rotation below
            // a -90-degree kit Y rotation with nonuniform scales at both levels.
            var centre = new[] { 1.4f, 0.28f, 0.6f };
            var scale = new[] { 2f, 3f, 0.5f };
            var position = new[] { 4f, -0.55f, 6f };
            foreach (var vertex in new[] { new[] { 1.1f, -0.02f, 0.48f }, new[] { 1.7f, 0.58f, 0.72f } })
            {
                var original = PartToParent(vertex, scale, position);
                var pivotPosition = PartToParent(centre, scale, position);
                var centred = new[] { vertex[0] - centre[0], vertex[1] - centre[1], vertex[2] - centre[2] };
                var composed = PartToParent(centred, scale, pivotPosition);
                // Parent applies -90 Y yaw and its own nonuniform scale.
                for (var a = 0; a < 3; a++)
                    Assert.That(ParentToWorld(composed)[a], Is.EqualTo(ParentToWorld(original)[a]).Within(0.00001));
            }
        }

        private static float[] PartToParent(float[] v, float[] scale, float[] position) =>
            new[] { position[0] - v[1] * scale[1], position[1] + v[0] * scale[0], position[2] + v[2] * scale[2] };
        private static float[] ParentToWorld(float[] v) => new[] { -v[2] * 0.5f, v[1] * 2f, v[0] * 3f };

        private static string FindArtDirectory()
        {
            foreach (var start in new[] { Environment.CurrentDirectory, TestContext.CurrentContext.TestDirectory })
                for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
                {
                    var candidate = Path.Combine(dir.FullName, "game/Airside/Assets/Airside/Art/Models/Vehicles");
                    if (Directory.Exists(candidate)) return candidate;
                    candidate = Path.Combine(dir.FullName, "Assets/Airside/Art/Models/Vehicles");
                    if (Directory.Exists(candidate)) return candidate;
                }
            throw new DirectoryNotFoundException("Shipped service kits are required for this geometry probe.");
        }

        [DataContract] private sealed class Gltf
        {
            [DataMember(Name = "nodes")] public Node[] Nodes;
            [DataMember(Name = "meshes")] public Mesh[] Meshes;
            [DataMember(Name = "accessors")] public Accessor[] Accessors;
            [DataMember(Name = "bufferViews")] public View[] Views;
            [DataMember(Name = "buffers")] public Buffer[] Buffers;
        }
        [DataContract] private sealed class Node { [DataMember(Name = "name")] public string Name; [DataMember(Name = "mesh")] public int Mesh; }
        [DataContract] private sealed class Mesh { [DataMember(Name = "primitives")] public Primitive[] Primitives; }
        [DataContract] private sealed class Primitive { [DataMember(Name = "attributes")] public Attributes Attributes; }
        [DataContract] private sealed class Attributes { [DataMember(Name = "POSITION")] public int Position; }
        [DataContract] private sealed class Accessor
        {
            [DataMember(Name = "bufferView")] public int View;
            [DataMember(Name = "byteOffset")] public int Offset;
            [DataMember(Name = "componentType")] public int Component;
            [DataMember(Name = "count")] public int Count;
            [DataMember(Name = "min")] public float[] Min;
            [DataMember(Name = "max")] public float[] Max;
        }
        [DataContract] private sealed class View
        {
            [DataMember(Name = "buffer")] public int Buffer;
            [DataMember(Name = "byteOffset")] public int Offset;
            [DataMember(Name = "byteStride")] public int Stride;
        }
        [DataContract] private sealed class Buffer { [DataMember(Name = "uri")] public string Uri; }
    }
}

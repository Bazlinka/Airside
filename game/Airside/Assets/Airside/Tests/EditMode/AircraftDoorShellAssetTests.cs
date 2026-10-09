using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using Airside.Presentation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>
    /// The doorway hollow is built from the shipped door leaves' own vertices at runtime. This reads those
    /// vertices straight from the packaged glTFs (the runtime keeps the vertex order and count), so a regenerated
    /// model whose door is not a front/back shell, or sits a hair under the thickness limit, fails here instead of
    /// logging "opens onto bare hull" in the player.
    /// </summary>
    public sealed class AircraftDoorShellAssetTests
    {
        [TestCase("mdl_saab_340b_v01")]
        [TestCase("mdl_atr42_starter_v03")]
        [TestCase("mdl_737_800_v01")]
        [TestCase("mdl_737_8_narrowbody_v01")]
        [TestCase("mdl_dash8_q400_v01")]
        [TestCase("mdl_e190_v01")]
        [TestCase("mdl_a220_300_v01")]
        [TestCase("mdl_a320_200_v02")]
        [TestCase("mdl_a321neo_v01")]
        public void EveryPassengerAndCargoDoorLeafIsADoorShell(string asset)
        {
            var dir = FindArtDirectory();
            var path = Path.Combine(dir, asset + ".gltf");
            using var stream = File.OpenRead(path);
            var gltf = (Gltf)new DataContractJsonSerializer(typeof(Gltf)).ReadObject(stream);
            var found = 0;
            foreach (var node in gltf.Nodes)
            {
                if (node.Name != "door_fwd" && node.Name != "cargo_door") continue;
                var positions = ReadPositions(gltf, dir, node);
                Assert.That(AircraftDoorwayGeometry.TryBuild(positions, 1f, 0.002f, out _, out _), Is.True,
                    asset + " " + node.Name + " (" + positions.Length / 3 + " vertices) must be a front/back door shell");
                found++;
            }
            Assert.That(found, Is.GreaterThanOrEqualTo(1), asset + " has a passenger door leaf");
        }

        private static float[] ReadPositions(Gltf gltf, string dir, Node node)
        {
            var accessor = gltf.Accessors[gltf.Meshes[node.Mesh].Primitives[0].Attributes.Position];
            var view = gltf.Views[accessor.View];
            var bytes = File.ReadAllBytes(Path.Combine(dir, gltf.Buffers[view.Buffer].Uri));
            var stride = view.Stride > 0 ? view.Stride : 12;
            var offset = view.Offset + accessor.Offset;
            var result = new float[accessor.Count * 3];
            for (var i = 0; i < accessor.Count; i++)
                for (var c = 0; c < 3; c++)
                    result[i * 3 + c] = BitConverter.ToSingle(bytes, offset + i * stride + c * 4);
            return result;
        }

        private static string FindArtDirectory()
        {
            foreach (var start in new[] { Environment.CurrentDirectory, TestContext.CurrentContext.TestDirectory })
                for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
                {
                    var candidate = Path.Combine(dir.FullName, "game/Airside/Assets/Airside/Art/Models/Aircraft");
                    if (Directory.Exists(candidate)) return candidate;
                    candidate = Path.Combine(dir.FullName, "Assets/Airside/Art/Models/Aircraft");
                    if (Directory.Exists(candidate)) return candidate;
                }
            throw new DirectoryNotFoundException("Shipped aircraft kits are required for this geometry probe.");
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
            [DataMember(Name = "count")] public int Count;
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

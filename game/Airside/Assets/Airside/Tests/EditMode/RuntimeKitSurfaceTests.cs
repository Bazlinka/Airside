using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using Airside.Presentation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class RuntimeKitSurfaceTests
    {
        [Test]
        public void ShippedOperationsShedHasNoncollapsedUvAndSoundTangentFramesOnEveryBodyFace()
        {
            var path = FindAsset("Models/Buildings/mdl_operations_shed_v05.gltf");
            using var stream = File.OpenRead(path);
            var data = (Gltf)new DataContractJsonSerializer(typeof(Gltf)).ReadObject(stream);
            var body = data.Meshes[0];
            Assert.That(body.Name, Is.EqualTo("shed_body"));
            var position = data.Accessors[body.Primitives[0].Attributes.Position];
            var index = data.Accessors[body.Primitives[0].Indices];
            var vertices = ReadPositions(data, position, path);
            var triangles = ReadIndices(data, index, path);
            var original = (float[])vertices.Clone();
            var originalIndices = (int[])triangles.Clone();
            var unwrap = BoxKitSurfaceGeometry.Build(vertices, triangles);
            var expanded = Remap(vertices, unwrap.SourceIndices);
            Assert.That(vertices, Is.EqualTo(original), "shared geometry stays untouched");
            Assert.That(triangles, Is.EqualTo(originalIndices), "source index winding stays untouched");
            Assert.That(unwrap.Triangles.Length, Is.EqualTo(36));
            AssertTriangles(expanded, unwrap.Triangles, unwrap.Uvs);
            var normals = FaceNormals(expanded, unwrap.Triangles);
            AssertFrames(normals, RuntimeKitTangents.Build(expanded, normals, unwrap.Uvs, unwrap.Triangles));
            for (var i = 0; i < triangles.Length; i++)
                Assert.That(unwrap.SourceIndices[unwrap.Triangles[i]], Is.EqualTo(triangles[i]), "source winding/triangle order");
        }

        [Test]
        public void SharedBoxCornersSplitAtAllSixFaceProjectionSeams()
        {
            var vertices = new float[] { -3,-1,-2, 3,-1,-2, 3,1,-2, -3,1,-2, -3,-1,2, 3,-1,2, 3,1,2, -3,1,2 };
            var indices = new[] { 0,2,1, 0,3,2, 4,5,6, 4,6,7, 0,4,7, 0,7,3, 1,2,6, 1,6,5, 3,7,6, 3,6,2, 0,1,5, 0,5,4 };
            var result = BoxKitSurfaceGeometry.Build(vertices, indices);
            Assert.That(result.SourceIndices.Length, Is.EqualTo(24));
            AssertTriangles(Remap(vertices, result.SourceIndices), result.Triangles, result.Uvs);
        }

        [Test]
        public void ValidUnsignedShortSourceCanExpandBeyondUnsignedShortVertexBudget()
        {
            const int boxes = 3000;
            var cube = new float[] { -3,-1,-2, 3,-1,-2, 3,1,-2, -3,1,-2, -3,-1,2, 3,-1,2, 3,1,2, -3,1,2 };
            var faces = new[] { 0,2,1, 0,3,2, 4,5,6, 4,6,7, 0,4,7, 0,7,3, 1,2,6, 1,6,5, 3,7,6, 3,6,2, 0,1,5, 0,5,4 };
            var vertices = new float[boxes * cube.Length];
            var triangles = new int[boxes * faces.Length];
            for (var box = 0; box < boxes; box++)
            {
                Array.Copy(cube, 0, vertices, box * cube.Length, cube.Length);
                for (var i = 0; i < faces.Length; i++) triangles[box * faces.Length + i] = box * 8 + faces[i];
            }
            Assert.That(vertices.Length / 3, Is.LessThan(ushort.MaxValue));
            var result = BoxKitSurfaceGeometry.Build(vertices, triangles);
            Assert.That(result.SourceIndices.Length, Is.EqualTo(boxes * 24));
            Assert.That(result.SourceIndices.Length, Is.GreaterThan(ushort.MaxValue));
            Assert.That(result.Triangles[result.Triangles.Length - 1], Is.GreaterThan(ushort.MaxValue));
        }

        [Test]
        public void SlopingRoofAndVerticalWallRetainTwoDimensionalTextureVariation()
        {
            foreach (var vertices in new[] { new float[] { 0,1,0, 6,1,0, 6,3,4, 0,3,4 },
                new float[] { 0,0,0, 6,0,0, 6,2.8f,0, 0,2.8f,0 } })
            {
                var result = BoxKitSurfaceGeometry.Build(vertices, new[] { 0,1,2, 0,2,3 });
                AssertTriangles(Remap(vertices, result.SourceIndices), result.Triangles, result.Uvs);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TangentsFollowTextureDerivativeAndMirroredHandedness(bool mirror)
        {
            var vertices = new float[] { 0,0,0, 2,0,0, 2,3,0, 0,3,0 };
            var normals = new float[] { 0,0,1, 0,0,1, 0,0,1, 0,0,1 };
            var uv = mirror ? new float[] { 1,0, 0,0, 0,1, 1,1 } : new float[] { 0,0, 1,0, 1,1, 0,1 };
            var tangents = RuntimeKitTangents.Build(vertices, normals, uv, new[] { 0,1,2, 0,2,3 });
            AssertFrames(normals, tangents);
            for (var i = 0; i < 4; i++)
            {
                Assert.That(tangents[i * 4], Is.EqualTo(mirror ? -1 : 1).Within(0.00001));
                Assert.That(tangents[i * 4 + 3], Is.EqualTo(mirror ? -1 : 1));
            }
        }

        [Test]
        public void CombinedTranslatedPartsKeepIndependentNormalAndUvFrames()
        {
            var positions = new float[] { 0,0,0, 2,0,0, 0,3,0, 100,0,0, 100,0,2, 100,3,0 };
            var normals = new float[] { 0,0,1, 0,0,1, 0,0,1, -1,0,0, -1,0,0, -1,0,0 };
            var uv = new float[] { 0,0, 1,0, 0,1, 0,0, 1,0, 0,1 };
            var tangents = RuntimeKitTangents.Build(positions, normals, uv, new[] { 0,1,2, 3,4,5 });
            AssertFrames(normals, tangents);
            Assert.That(tangents[0], Is.EqualTo(1f).Within(0.00001));
            Assert.That(tangents[3 * 4 + 2], Is.EqualTo(1f).Within(0.00001));
        }

        [Test]
        public void DegenerateUvProducesFiniteOrthogonalFallbackWithoutClaimingRepairedMapping()
        {
            var positions = new float[] { 0,0,0, 2,0,0, 0,3,0 };
            var normals = new float[] { 0,0,1, 0,0,1, 0,0,1 };
            AssertFrames(normals, RuntimeKitTangents.Build(positions, normals, new float[6], new[] { 0,1,2 }));
        }

        private static void AssertTriangles(float[] vertices, int[] triangles, float[] uv)
        {
            foreach (var value in uv)
                Assert.That(value, Is.InRange(0f, 1f), "finite bounded UV");
            for (var t = 0; t < triangles.Length; t += 3)
            {
                var a = triangles[t]; var b = triangles[t + 1]; var c = triangles[t + 2];
                var area = (uv[b * 2] - uv[a * 2]) * (uv[c * 2 + 1] - uv[a * 2 + 1])
                    - (uv[c * 2] - uv[a * 2]) * (uv[b * 2 + 1] - uv[a * 2 + 1]);
                Assert.That(Math.Abs(area), Is.GreaterThan(0.00001), "nondegenerate face " + t / 3);

            }
        }

        private static float[] FaceNormals(float[] vertices, int[] triangles)
        {
            var normals = new float[vertices.Length];
            for (var t = 0; t < triangles.Length; t += 3)
            {
                var a = triangles[t] * 3; var b = triangles[t + 1] * 3; var c = triangles[t + 2] * 3;
                var ux = vertices[b] - vertices[a]; var uy = vertices[b + 1] - vertices[a + 1]; var uz = vertices[b + 2] - vertices[a + 2];
                var vx = vertices[c] - vertices[a]; var vy = vertices[c + 1] - vertices[a + 1]; var vz = vertices[c + 2] - vertices[a + 2];
                var n = new[] { uy * vz - uz * vy, uz * vx - ux * vz, ux * vy - uy * vx };
                var length = Math.Sqrt(n[0] * n[0] + n[1] * n[1] + n[2] * n[2]);
                foreach (var vertex in new[] { a,b,c }) for (var axis = 0; axis < 3; axis++) normals[vertex + axis] = (float)(n[axis] / length);
            }
            return normals;
        }

        private static void AssertFrames(float[] normals, float[] tangents)
        {
            Assert.That(tangents.Length, Is.EqualTo(normals.Length / 3 * 4));
            for (var i = 0; i < normals.Length / 3; i++)
            {
                var length = 0f; var dot = 0f;
                for (var a = 0; a < 3; a++)
                {
                    var value = tangents[i * 4 + a];
                    Assert.That(float.IsNaN(value) || float.IsInfinity(value), Is.False);
                    length += value * value; dot += value * normals[i * 3 + a];
                }
                Assert.That(length, Is.EqualTo(1f).Within(0.00001));
                Assert.That(dot, Is.EqualTo(0f).Within(0.00001));
                Assert.That(Math.Abs(tangents[i * 4 + 3]), Is.EqualTo(1f));
            }
        }

        private static float[] Remap(float[] positions, int[] indices)
        {
            var result = new float[indices.Length * 3];
            for (var i = 0; i < indices.Length; i++) Array.Copy(positions, indices[i] * 3, result, i * 3, 3);
            return result;
        }

        private static float[] ReadPositions(Gltf gltf, Accessor accessor, string path)
        {
            var view = gltf.Views[accessor.View];
            var bytes = File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(path), gltf.Buffers[view.Buffer].Uri));
            Assert.That(accessor.Component, Is.EqualTo(5126));
            var values = new float[accessor.Count * 3];
            for (var i = 0; i < accessor.Count; i++) for (var a = 0; a < 3; a++)
                values[i * 3 + a] = BitConverter.ToSingle(bytes, view.Offset + accessor.Offset + i * (view.Stride == 0 ? 12 : view.Stride) + a * 4);
            return values;
        }
        private static int[] ReadIndices(Gltf gltf, Accessor accessor, string path)
        {
            var view = gltf.Views[accessor.View];
            var bytes = File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(path), gltf.Buffers[view.Buffer].Uri));
            Assert.That(accessor.Component, Is.EqualTo(5123));
            var values = new int[accessor.Count];
            for (var i = 0; i < accessor.Count; i++) values[i] = BitConverter.ToUInt16(bytes, view.Offset + accessor.Offset + i * 2);
            return values;
        }
        private static string FindAsset(string relative)
        {
            foreach (var start in new[] { Environment.CurrentDirectory, TestContext.CurrentContext.TestDirectory })
                for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
                    foreach (var prefix in new[] { "game/Airside/Assets/Airside/Art", "Assets/Airside/Art" })
                    {
                        var path = Path.Combine(dir.FullName, prefix, relative);
                        if (File.Exists(path)) return path;
                    }
            throw new FileNotFoundException(relative);
        }
        [DataContract] private sealed class Gltf
        {
            [DataMember(Name = "meshes")] public Mesh[] Meshes { get; set; }
            [DataMember(Name = "accessors")] public Accessor[] Accessors { get; set; }
            [DataMember(Name = "bufferViews")] public View[] Views { get; set; }
            [DataMember(Name = "buffers")] public Buffer[] Buffers { get; set; }
        }
        [DataContract] private sealed class Mesh { [DataMember(Name = "name")] public string Name { get; set; } [DataMember(Name = "primitives")] public Primitive[] Primitives { get; set; } }
        [DataContract] private sealed class Primitive { [DataMember(Name = "attributes")] public Attributes Attributes { get; set; } [DataMember(Name = "indices")] public int Indices { get; set; } }
        [DataContract] private sealed class Attributes { [DataMember(Name = "POSITION")] public int Position { get; set; } }
        [DataContract] private sealed class Accessor
        {
            [DataMember(Name = "bufferView")] public int View { get; set; }
            [DataMember(Name = "byteOffset")] public int Offset { get; set; }
            [DataMember(Name = "componentType")] public int Component { get; set; }
            [DataMember(Name = "count")] public int Count { get; set; }
        }
        [DataContract] private sealed class View
        {
            [DataMember(Name = "buffer")] public int Buffer { get; set; }
            [DataMember(Name = "byteOffset")] public int Offset { get; set; }
            [DataMember(Name = "byteStride")] public int Stride { get; set; }
        }
        [DataContract] private sealed class Buffer { [DataMember(Name = "uri")] public string Uri { get; set; } }
    }
}

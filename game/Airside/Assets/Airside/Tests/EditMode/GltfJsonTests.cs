using Airside.Presentation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>
    /// Covers the reader that replaced ArtGltfLoader's regex parsing.
    /// </summary>
    /// <remarks>
    /// The old parser assumed exactly two accessors per mesh, POSITION then indices,
    /// packed in that order, and read the .bin by walking a running offset. Adding
    /// NORMAL, TEXCOORD_0 and TANGENT broke every one of those assumptions, so the
    /// rules that replaced them are pinned here.
    /// </remarks>
    public sealed class GltfJsonTests
    {
        [Test]
        public void Int_DistinguishesAMissingKeyFromAccessorZero()
        {
            // This is the whole reason the kits are not read with JsonUtility: glTF's
            // attributes map is optional per key, and a missing NORMAL must not be
            // mistaken for a genuine reference to accessor 0.
            var root = GltfJson.AsObject(GltfJson.Parse("{\"POSITION\": 0}"));

            Assert.That(GltfJson.Int(root, "POSITION", -1), Is.EqualTo(0));
            Assert.That(GltfJson.Int(root, "NORMAL", -1), Is.EqualTo(-1));
        }

        [Test]
        public void Int_ReturnsFallbackForAnAbsentByteOffset()
        {
            // byteOffset is optional in glTF and defaults to zero; bufferView offsets
            // are only correct if that default is honoured.
            var view = GltfJson.AsObject(GltfJson.Parse("{\"buffer\": 0, \"byteLength\": 48}"));

            Assert.That(GltfJson.Int(view, "byteOffset", 0), Is.EqualTo(0));
            Assert.That(GltfJson.Int(view, "byteLength", -1), Is.EqualTo(48));
        }

        [Test]
        public void Parse_ReadsTheNestedShapeAKitActuallyUses()
        {
            const string json = @"{
                ""buffers"": [{ ""uri"": ""kit.bin"", ""byteLength"": 96 }],
                ""meshes"": [{
                    ""name"": ""fuselage"",
                    ""primitives"": [{
                        ""attributes"": { ""POSITION"": 0, ""NORMAL"": 1, ""TANGENT"": 3 },
                        ""indices"": 4,
                        ""mode"": 4
                    }]
                }]
            }";

            var root = GltfJson.AsObject(GltfJson.Parse(json));
            var buffers = GltfJson.Array(root, "buffers");
            Assert.That(GltfJson.String(GltfJson.ObjectAt(buffers, 0), "uri"), Is.EqualTo("kit.bin"));

            var mesh = GltfJson.ObjectAt(GltfJson.Array(root, "meshes"), 0);
            Assert.That(GltfJson.String(mesh, "name"), Is.EqualTo("fuselage"));

            var primitive = GltfJson.ObjectAt(GltfJson.Array(mesh, "primitives"), 0);
            Assert.That(GltfJson.Int(primitive, "indices", -1), Is.EqualTo(4));

            var attributes = GltfJson.AsObject(primitive["attributes"]);
            Assert.That(GltfJson.Int(attributes, "POSITION", -1), Is.EqualTo(0));
            Assert.That(GltfJson.Int(attributes, "NORMAL", -1), Is.EqualTo(1));
            Assert.That(GltfJson.Int(attributes, "TANGENT", -1), Is.EqualTo(3));
            // Absent, so the loader falls back to its own planar projection.
            Assert.That(GltfJson.Int(attributes, "TEXCOORD_0", -1), Is.EqualTo(-1));
        }

        [Test]
        public void Parse_ReadsNegativeAndExponentNumbers()
        {
            // Accessor min/max carry negative and scientific-notation values.
            var root = GltfJson.AsObject(GltfJson.Parse("{\"min\": [-2.5, 1e-3, -1.5E2]}"));
            var min = GltfJson.Array(root, "min");

            Assert.That((double)min[0], Is.EqualTo(-2.5).Within(1e-9));
            Assert.That((double)min[1], Is.EqualTo(0.001).Within(1e-9));
            Assert.That((double)min[2], Is.EqualTo(-150.0).Within(1e-9));
        }

        [Test]
        public void Parse_HandlesEmptyContainersAndLiterals()
        {
            var root = GltfJson.AsObject(GltfJson.Parse(
                "{\"a\": {}, \"b\": [], \"c\": true, \"d\": null}"));

            Assert.That(GltfJson.AsObject(root["a"]), Is.Empty);
            Assert.That(GltfJson.AsArray(root["b"]), Is.Empty);
            Assert.That(root["c"], Is.True);
            Assert.That(root["d"], Is.Null);
        }

        [Test]
        public void Parse_ReturnsNullRatherThanThrowingOnBadInput()
        {
            // ParseKit is wrapped in a catch-all that drops the kit to a greybox
            // fallback, so malformed input has to fail quietly rather than explode.
            Assert.That(GltfJson.Parse("{\"unterminated\": "), Is.Null);
            Assert.That(GltfJson.Parse("not json"), Is.Null);
            Assert.That(GltfJson.Parse("{} trailing"), Is.Null);
            Assert.That(GltfJson.Parse(""), Is.Null);
            Assert.That(GltfJson.Parse(null), Is.Null);
        }

        [Test]
        public void Accessors_ReturnNullRatherThanThrowingOnMissingOrMistypedFields()
        {
            var root = GltfJson.AsObject(GltfJson.Parse("{\"meshes\": [{\"name\": \"wing\"}]}"));

            Assert.That(GltfJson.Array(root, "accessors"), Is.Null);
            Assert.That(GltfJson.ObjectAt(GltfJson.Array(root, "meshes"), 7), Is.Null);
            Assert.That(GltfJson.ObjectAt(null, 0), Is.Null);
            Assert.That(GltfJson.String(root, "meshes"), Is.Null, "an array is not a string");
            Assert.That(GltfJson.Int(root, "meshes", -1), Is.EqualTo(-1), "an array is not an int");
        }
    }
}

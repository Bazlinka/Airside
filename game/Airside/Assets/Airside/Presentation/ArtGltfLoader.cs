using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Airside.Presentation
{
    /// <summary>
    /// Loads Airside Batch B/C procedural glTF kits from disk at runtime.
    /// Packaged builds read <c>StreamingAssets/Airside/Art</c> (see
    /// <see cref="ArtRuntimePaths"/> and <c>scripts/sync-art-streaming-assets.sh</c>).
    /// Reads the project's own kits only — not a general glTF importer. Meshes take
    /// POSITION plus, when the writer supplied them, NORMAL, TEXCOORD_0 and TANGENT;
    /// kits generated before those existed still load, with the attributes derived
    /// here as they always were. Missing files return false so callers keep primitive
    /// greybox fallbacks. This is an interim pipeline until Unity-imported
    /// prefabs/Addressables.
    /// </summary>
    public static class ArtGltfLoader
    {
        private static readonly Dictionary<string, GltfKit> KitCache = new(StringComparer.Ordinal);

        /// <summary>
        /// Instantiates every mesh node in the kit as a child of <paramref name="parent"/>.
        /// Optional <paramref name="rename"/> maps kit node names to presentation names.
        /// Optional <paramref name="colorFor"/> supplies per-node colours (null → Concrete).
        /// </summary>
        public static bool TryInstantiate(
            string artRelativePath,
            Transform parent,
            out Transform root,
            Func<string, string> rename = null,
            Func<string, Color?> colorFor = null,
            Vector3 localPosition = default,
            bool preferProceduralMaterials = false)
        {
            root = null;
            if (!TryLoadKit(artRelativePath, out var kit) || kit.Meshes.Count == 0)
                return false;

            var holder = new GameObject(Path.GetFileNameWithoutExtension(artRelativePath)).transform;
            holder.SetParent(parent, false);
            holder.localPosition = localPosition;
            holder.localRotation = Quaternion.identity;
            holder.localScale = Vector3.one;

            foreach (var entry in kit.Meshes)
            {
                var name = rename != null ? rename(entry.Name) : entry.Name;
                var color = colorFor?.Invoke(entry.Name) ?? new Color(0.61f, 0.64f, 0.63f);
                CreateMeshObject(name, entry.Mesh, color, holder, Vector3.zero, Quaternion.identity,
                    preferProceduralMaterials);
            }

            root = holder;
            return true;
        }

        /// <summary>
        /// Places one named mesh from a kit at a world pose. Used for WLD lighting/prop
        /// templates and PRP service gear. Returns false when the kit or mesh is missing.
        /// </summary>
        public static bool TryPlaceNamedMesh(
            string artRelativePath,
            string meshName,
            Vector3 worldPosition,
            Quaternion worldRotation,
            Color color,
            out Transform instance,
            Vector3? localScale = null)
        {
            instance = null;
            if (!TryLoadKit(artRelativePath, out var kit))
                return false;
            if (!kit.ByName.TryGetValue(meshName, out var entry))
                return false;

            instance = CreateMeshObject(meshName, entry.Mesh, color, null, worldPosition, worldRotation);
            if (localScale.HasValue)
                instance.localScale = localScale.Value;
            return true;
        }

        public static bool HasKit(string artRelativePath) =>
            TryLoadKit(artRelativePath, out var kit) && kit.Meshes.Count > 0;

        private static Transform CreateMeshObject(
            string name,
            Mesh mesh,
            Color color,
            Transform parent,
            Vector3 position,
            Quaternion rotation,
            bool preferProcedural = false)
        {
            var go = new GameObject(name);
            var transform = go.transform;
            if (parent != null)
                transform.SetParent(parent, false);
            transform.localPosition = parent != null ? Vector3.zero : position;
            transform.localRotation = parent != null ? Quaternion.identity : rotation;
            if (parent == null)
            {
                transform.position = position;
                transform.rotation = rotation;
            }

            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            var kind = AirsideMaterialLibrary.InferFromMeshName(name);
            // Shared: kits build one renderer per mesh, and identical (colour, kind)
            // pairs are overwhelmingly common. Runtime tinting clones via .material.
            renderer.sharedMaterial = AirsideMaterialLibrary.CreateShared(
                color, kind, preferProcedural: preferProcedural);
            return transform;
        }

        private static bool TryLoadKit(string artRelativePath, out GltfKit kit)
        {
            kit = null;
            if (string.IsNullOrEmpty(artRelativePath))
                return false;
            if (KitCache.TryGetValue(artRelativePath, out kit))
                return kit != null;

            var fullPath = ArtRuntimePaths.ResolveExisting(artRelativePath);
            if (fullPath == null)
            {
                KitCache[artRelativePath] = null;
                return false;
            }

            try
            {
                kit = ParseKit(fullPath);
                KitCache[artRelativePath] = kit;
                return kit != null;
            }
            catch (Exception)
            {
                KitCache[artRelativePath] = null;
                kit = null;
                return false;
            }
        }

        private static GltfKit ParseKit(string gltfPath)
        {
            var root = GltfJson.AsObject(GltfJson.Parse(File.ReadAllText(gltfPath)));
            if (root == null)
                return null;

            var buffers = GltfJson.Array(root, "buffers");
            var binUri = GltfJson.String(GltfJson.ObjectAt(buffers, 0), "uri");
            if (string.IsNullOrEmpty(binUri))
                return null;

            var binPath = Path.Combine(Path.GetDirectoryName(gltfPath) ?? string.Empty, binUri);
            if (!File.Exists(binPath))
                return null;

            var blob = File.ReadAllBytes(binPath);
            var bufferViews = GltfJson.Array(root, "bufferViews");
            var accessors = GltfJson.Array(root, "accessors");
            var meshes = GltfJson.Array(root, "meshes");
            if (bufferViews == null || accessors == null || meshes == null)
                return null;

            var kit = new GltfKit();
            foreach (var meshValue in meshes)
            {
                var meshObject = GltfJson.AsObject(meshValue);
                var name = GltfJson.String(meshObject, "name");
                var primitives = GltfJson.Array(meshObject, "primitives");
                var primitive = GltfJson.ObjectAt(primitives, 0);
                if (string.IsNullOrEmpty(name) || primitive == null)
                    continue;

                var attributes = GltfJson.AsObject(
                    primitive.TryGetValue("attributes", out var a) ? a : null);
                var positionIndex = GltfJson.Int(attributes, "POSITION", -1);
                var indicesIndex = GltfJson.Int(primitive, "indices", -1);

                var vertices = ReadVector3(blob, bufferViews, accessors, positionIndex);
                var indices = ReadIndices(blob, bufferViews, accessors, indicesIndex);
                if (vertices == null || indices == null)
                    continue;

                // Kits written before the generator emitted a full vertex format carry
                // POSITION only; they still load, on the old derive-everything path.
                var normals = ReadVector3(blob, bufferViews, accessors,
                    GltfJson.Int(attributes, "NORMAL", -1));
                var uvs = ReadVector2(blob, bufferViews, accessors,
                    GltfJson.Int(attributes, "TEXCOORD_0", -1));
                var tangents = ReadVector4(blob, bufferViews, accessors,
                    GltfJson.Int(attributes, "TANGENT", -1));
                if (normals != null && normals.Length != vertices.Length)
                    normals = null;
                if (uvs != null && uvs.Length != vertices.Length)
                    uvs = null;
                if (tangents != null && tangents.Length != vertices.Length)
                    tangents = null;

                // The generator does not guarantee outward winding per part, which is
                // what left AIR-001's hull hollow. Supplied normals are derived from
                // that same winding, so a part that needs flipping needs its normals
                // and tangent handedness flipped with it -- correcting the triangles
                // alone would light the surface from the inside.
                // Flat apron/runway paint quads are already outward-up; the centroid test
                // flips them face-down and they vanish under backface culling (black voids).
                if (!IsFlatDecalMesh(vertices) && ShouldFlipWinding(vertices, indices))
                {
                    for (var t = 0; t + 2 < indices.Length; t += 3)
                        (indices[t], indices[t + 1]) = (indices[t + 1], indices[t]);

                    if (normals != null)
                        for (var v = 0; v < normals.Length; v++)
                            normals[v] = -normals[v];

                    if (tangents != null)
                        for (var v = 0; v < tangents.Length; v++)
                            tangents[v].w = -tangents[v].w;
                }

                var mesh = new Mesh { name = name };
                mesh.SetVertices(vertices);
                mesh.SetTriangles(indices, 0);

                if (normals != null)
                    mesh.SetNormals(normals);
                else
                    mesh.RecalculateNormals();

                mesh.SetUVs(0, uvs ?? BuildPlanarUvs(vertices));

                if (tangents != null)
                    mesh.SetTangents(tangents);

                mesh.RecalculateBounds();

                var entry = new MeshEntry(name, mesh);
                kit.Meshes.Add(entry);
                kit.ByName[name] = entry;
            }

            return kit.Meshes.Count > 0 ? kit : null;
        }

        /// <summary>
        /// Resolves an accessor to its slice of the binary blob, or null when the
        /// accessor is absent, malformed, or would read past the end of the buffer.
        /// </summary>
        private static bool TryResolve(
            byte[] blob,
            List<object> bufferViews,
            List<object> accessors,
            int accessorIndex,
            int expectedComponentType,
            int componentsPerElement,
            int componentSize,
            out int start,
            out int count)
        {
            start = 0;
            count = 0;
            var accessor = GltfJson.ObjectAt(accessors, accessorIndex);
            if (accessor == null)
                return false;
            if (GltfJson.Int(accessor, "componentType", 0) != expectedComponentType)
                return false;

            var view = GltfJson.ObjectAt(bufferViews, GltfJson.Int(accessor, "bufferView", -1));
            if (view == null)
                return false;

            count = GltfJson.Int(accessor, "count", 0);
            start = GltfJson.Int(view, "byteOffset", 0) + GltfJson.Int(accessor, "byteOffset", 0);
            var length = (long)count * componentsPerElement * componentSize;
            return count > 0 && start >= 0 && start + length <= blob.Length;
        }

        private static Vector3[] ReadVector3(
            byte[] blob, List<object> bufferViews, List<object> accessors, int accessorIndex)
        {
            if (!TryResolve(blob, bufferViews, accessors, accessorIndex, 5126, 3, 4,
                    out var start, out var count))
                return null;

            var result = new Vector3[count];
            for (var i = 0; i < count; i++)
            {
                var o = start + i * 12;
                result[i] = new Vector3(
                    BitConverter.ToSingle(blob, o),
                    BitConverter.ToSingle(blob, o + 4),
                    BitConverter.ToSingle(blob, o + 8));
            }

            return result;
        }

        private static Vector2[] ReadVector2(
            byte[] blob, List<object> bufferViews, List<object> accessors, int accessorIndex)
        {
            if (!TryResolve(blob, bufferViews, accessors, accessorIndex, 5126, 2, 4,
                    out var start, out var count))
                return null;

            var result = new Vector2[count];
            for (var i = 0; i < count; i++)
            {
                var o = start + i * 8;
                result[i] = new Vector2(
                    BitConverter.ToSingle(blob, o),
                    BitConverter.ToSingle(blob, o + 4));
            }

            return result;
        }

        private static Vector4[] ReadVector4(
            byte[] blob, List<object> bufferViews, List<object> accessors, int accessorIndex)
        {
            if (!TryResolve(blob, bufferViews, accessors, accessorIndex, 5126, 4, 4,
                    out var start, out var count))
                return null;

            var result = new Vector4[count];
            for (var i = 0; i < count; i++)
            {
                var o = start + i * 16;
                result[i] = new Vector4(
                    BitConverter.ToSingle(blob, o),
                    BitConverter.ToSingle(blob, o + 4),
                    BitConverter.ToSingle(blob, o + 8),
                    BitConverter.ToSingle(blob, o + 12));
            }

            return result;
        }

        /// <summary>
        /// Reads triangle indices, accepting both widths the writer emits: uint16 for
        /// the common small mesh, uint32 once splitting pushes a part past 65535 verts.
        /// </summary>
        private static int[] ReadIndices(
            byte[] blob, List<object> bufferViews, List<object> accessors, int accessorIndex)
        {
            if (TryResolve(blob, bufferViews, accessors, accessorIndex, 5123, 1, 2,
                    out var start, out var count))
            {
                var shortResult = new int[count];
                for (var i = 0; i < count; i++)
                    shortResult[i] = BitConverter.ToUInt16(blob, start + i * 2);
                return shortResult;
            }

            if (TryResolve(blob, bufferViews, accessors, accessorIndex, 5125, 1, 4,
                    out start, out count))
            {
                var intResult = new int[count];
                for (var i = 0; i < count; i++)
                    intResult[i] = (int)BitConverter.ToUInt32(blob, start + i * 4);
                return intResult;
            }

            return null;
        }

        private static bool IsFlatDecalMesh(Vector3[] vertices)
        {
            if (vertices == null || vertices.Length == 0)
                return false;
            var min = vertices[0];
            var max = vertices[0];
            for (var i = 1; i < vertices.Length; i++)
            {
                min = Vector3.Min(min, vertices[i]);
                max = Vector3.Max(max, vertices[i]);
            }

            var size = max - min;
            // Ground markings / lead-ins are thin in Y relative to footprint.
            return size.y < 0.18f && size.x * size.z > 0.25f;
        }

        /// <summary>
        /// AIR-001 / BLD authored kits sometimes store inverted winding. With URP
        /// backface culling that hollows fuselage roofs and tires from overview.
        /// Flip triangles when a majority of face normals point toward the centroid.
        /// </summary>
        /// <summary>
        /// True when most of a part's triangles face its own centre, i.e. the generator
        /// wound it inside out. Callers flip the winding and every attribute derived
        /// from it. Kept as a safety net: the fix belongs in the generator, but a part
        /// that slips through would otherwise vanish under backface culling.
        /// </summary>
        private static bool ShouldFlipWinding(Vector3[] vertices, int[] indices)
        {
            if (vertices == null || vertices.Length < 3 || indices == null || indices.Length < 3)
                return false;

            var center = Vector3.zero;
            for (var i = 0; i < vertices.Length; i++)
                center += vertices[i];
            center /= vertices.Length;

            var outward = 0;
            var inward = 0;
            for (var i = 0; i + 2 < indices.Length; i += 3)
            {
                var i0 = indices[i];
                var i1 = indices[i + 1];
                var i2 = indices[i + 2];
                if (i0 >= vertices.Length || i1 >= vertices.Length || i2 >= vertices.Length)
                    continue;
                var v0 = vertices[i0];
                var v1 = vertices[i1];
                var v2 = vertices[i2];
                var normal = Vector3.Cross(v1 - v0, v2 - v0);
                var centroid = (v0 + v1 + v2) * (1f / 3f);
                if (Vector3.Dot(normal, centroid - center) >= 0f)
                    outward++;
                else
                    inward++;
            }

            return inward > outward;
        }

        /// <summary>
        /// Simple planar UVs from dominant axes so Batch B basecolours tile on
        /// box kits (ArtGltfLoader has no TEXCOORD0). Presentation only.
        /// </summary>
        private static Vector2[] BuildPlanarUvs(Vector3[] vertices)
        {
            if (vertices == null || vertices.Length == 0)
                return Array.Empty<Vector2>();

            var min = vertices[0];
            var max = vertices[0];
            for (var i = 1; i < vertices.Length; i++)
            {
                min = Vector3.Min(min, vertices[i]);
                max = Vector3.Max(max, vertices[i]);
            }

            var size = max - min;
            // Prefer the two largest axes for unwrap (walls/roofs/aprons).
            var abs = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
            var uAxis = 0;
            var vAxis = 2;
            if (abs.y >= abs.x && abs.y >= abs.z)
            {
                // Tallest span is Y → use XZ (top-down) or XY for walls later per-vertex.
                uAxis = 0;
                vAxis = 2;
            }
            else if (abs.z >= abs.x)
            {
                uAxis = 0;
                vAxis = 1;
            }
            else
            {
                uAxis = 2;
                vAxis = 1;
            }

            var uSize = Mathf.Max(0.0001f, abs[uAxis]);
            var vSize = Mathf.Max(0.0001f, abs[vAxis]);
            var uvs = new Vector2[vertices.Length];
            for (var i = 0; i < vertices.Length; i++)
            {
                var p = vertices[i];
                uvs[i] = new Vector2(
                    (p[uAxis] - min[uAxis]) / uSize,
                    (p[vAxis] - min[vAxis]) / vSize);
            }

            return uvs;
        }

        private sealed class GltfKit
        {
            public List<MeshEntry> Meshes { get; } = new();
            public Dictionary<string, MeshEntry> ByName { get; } = new(StringComparer.Ordinal);
        }

        private sealed class MeshEntry
        {
            public MeshEntry(string name, Mesh mesh)
            {
                Name = name;
                Mesh = mesh;
            }

            public string Name { get; }
            public Mesh Mesh { get; }
        }
    }
}

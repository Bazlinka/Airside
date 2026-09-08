using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Airside.Presentation
{
    /// <summary>
    /// Loads Airside Batch B/C procedural glTF kits from disk at runtime.
    /// Packaged builds read <c>StreamingAssets/Airside/Art</c> (see
    /// <see cref="ArtRuntimePaths"/> and <c>scripts/sync-art-streaming-assets.sh</c>).
    /// Supports the project's POSITION+indices box/quad kits only — not a general
    /// glTF importer. Missing files return false so callers keep primitive greybox
    /// fallbacks. This is an interim pipeline until Unity-imported prefabs/Addressables.
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
            var json = File.ReadAllText(gltfPath);
            var binUri = MatchFirst(json, "\"uri\"\\s*:\\s*\"([^\"]+)\"");
            if (string.IsNullOrEmpty(binUri))
                return null;

            var binPath = Path.Combine(Path.GetDirectoryName(gltfPath) ?? string.Empty, binUri);
            if (!File.Exists(binPath))
                return null;

            var blob = File.ReadAllBytes(binPath);

            // Mesh "name" fields appear before node/scene names in our kits.
            var allNames = MatchAll(json, "\"name\"\\s*:\\s*\"([^\"]+)\"");
            var meshCount = allNames.Count / 2;
            if (meshCount <= 0)
                return null;

            // Accessors alternate POSITION count then indices count per mesh.
            var counts = MatchAllInts(json, "\"count\"\\s*:\\s*(\\d+)");
            if (counts.Count < meshCount * 2)
                return null;

            var kit = new GltfKit();
            var offset = 0;
            for (var i = 0; i < meshCount; i++)
            {
                var name = allNames[i];
                var vertCount = counts[i * 2];
                var indexCount = counts[i * 2 + 1];

                var vertByteLen = vertCount * 12;
                var vertPadded = vertByteLen + ((4 - (vertByteLen % 4)) % 4);
                var indexByteLen = indexCount * 2;
                var indexPadded = indexByteLen + ((4 - (indexByteLen % 4)) % 4);
                if (offset + vertPadded + indexPadded > blob.Length)
                    break;

                var vertices = new Vector3[vertCount];
                for (var v = 0; v < vertCount; v++)
                {
                    var o = offset + v * 12;
                    vertices[v] = new Vector3(
                        BitConverter.ToSingle(blob, o),
                        BitConverter.ToSingle(blob, o + 4),
                        BitConverter.ToSingle(blob, o + 8));
                }

                offset += vertPadded;
                var indices = new int[indexCount];
                for (var n = 0; n < indexCount; n++)
                    indices[n] = BitConverter.ToUInt16(blob, offset + n * 2);
                offset += indexPadded;

                var mesh = new Mesh { name = name };
                mesh.SetVertices(vertices);
                mesh.SetTriangles(indices, 0);
                // Flat apron/runway paint quads are already outward-up; the centroid test
                // flips them face-down and they vanish under backface culling (black voids).
                if (!IsFlatDecalMesh(vertices))
                    EnsureOutwardWinding(mesh, vertices);
                mesh.RecalculateNormals();
                mesh.SetUVs(0, BuildPlanarUvs(vertices));
                mesh.RecalculateBounds();

                var entry = new MeshEntry(name, mesh);
                kit.Meshes.Add(entry);
                kit.ByName[name] = entry;
            }

            return kit.Meshes.Count > 0 ? kit : null;
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
        private static void EnsureOutwardWinding(Mesh mesh, Vector3[] vertices)
        {
            if (mesh == null || vertices == null || vertices.Length < 3)
                return;

            var tris = mesh.triangles;
            if (tris == null || tris.Length < 3)
                return;

            var center = Vector3.zero;
            for (var i = 0; i < vertices.Length; i++)
                center += vertices[i];
            center /= vertices.Length;

            var outward = 0;
            var inward = 0;
            for (var i = 0; i < tris.Length; i += 3)
            {
                var i0 = tris[i];
                var i1 = tris[i + 1];
                var i2 = tris[i + 2];
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

            if (inward <= outward)
                return;

            for (var i = 0; i < tris.Length; i += 3)
            {
                var swap = tris[i];
                tris[i] = tris[i + 1];
                tris[i + 1] = swap;
            }

            mesh.SetTriangles(tris, 0);
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

        private static string MatchFirst(string input, string pattern)
        {
            var match = Regex.Match(input, pattern);
            return match.Success ? match.Groups[1].Value : null;
        }

        private static List<string> MatchAll(string input, string pattern)
        {
            var list = new List<string>();
            foreach (Match match in Regex.Matches(input, pattern))
                list.Add(match.Groups[1].Value);
            return list;
        }

        private static List<int> MatchAllInts(string input, string pattern)
        {
            var list = new List<int>();
            foreach (Match match in Regex.Matches(input, pattern))
                list.Add(int.Parse(match.Groups[1].Value));
            return list;
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

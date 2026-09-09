using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
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
        private static readonly Dictionary<string, CombinedTemplate> CombinedCache = new(StringComparer.Ordinal);

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
            Vector3 localPosition = default)
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
                CreateMeshObject(name, entry.Mesh, color, holder, Vector3.zero, Quaternion.identity);
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

        /// <summary>
        /// One GameObject for co-located kit parts that share a pose (fence bays,
        /// edge lamps, scrub clumps). Combined meshes are cached per kit+part set
        /// so High stamps the same silhouette without 8 GameObjects per bay.
        /// </summary>
        public static bool TryPlaceCombined(
            string artRelativePath,
            (string Name, Color Color)[] parts,
            Vector3 worldPosition,
            Quaternion worldRotation,
            string instanceName,
            out Transform instance,
            Vector3? localScale = null)
        {
            instance = null;
            if (parts == null || parts.Length == 0)
                return false;
            if (!TryLoadKit(artRelativePath, out var kit))
                return false;

            var key = CombinedKey(artRelativePath, parts);
            if (!CombinedCache.TryGetValue(key, out var template) || template == null)
            {
                template = BuildCombined(kit, parts);
                CombinedCache[key] = template;
            }

            if (template == null || template.Mesh == null)
                return false;

            var go = new GameObject(string.IsNullOrEmpty(instanceName) ? "Combined kit" : instanceName);
            var transform = go.transform;
            var parent = AirsideStaticWorld.WorldRoot;
            if (parent != null)
                transform.SetParent(parent, false);
            transform.position = worldPosition;
            transform.rotation = worldRotation;
            if (localScale.HasValue)
                transform.localScale = localScale.Value;

            go.AddComponent<MeshFilter>().sharedMesh = template.Mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = template.Materials;
            AirsideSceneIndex.Remember(transform);
            instance = transform;
            return true;
        }

        public static bool HasKit(string artRelativePath) =>
            ArtRuntimePaths.ResolveExisting(artRelativePath) != null;

        private static Transform CreateMeshObject(
            string name,
            Mesh mesh,
            Color color,
            Transform parent,
            Vector3 position,
            Quaternion rotation)
        {
            var go = new GameObject(name);
            var transform = go.transform;
            var worldPose = parent == null;
            if (parent == null)
                parent = AirsideStaticWorld.WorldRoot;
            if (parent != null)
                transform.SetParent(parent, false);
            if (worldPose)
            {
                transform.position = position;
                transform.rotation = rotation;
            }
            else
            {
                transform.localPosition = Vector3.zero;
                transform.localRotation = Quaternion.identity;
            }

            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            var kind = AirsideMaterialLibrary.InferFromMeshName(name);
            // Shared: kits build one renderer per mesh, and identical (colour, kind)
            // pairs are overwhelmingly common. Runtime tinting clones via .material.
            var hasUsableUvs = AirsideMeshUtil.HasUsableUvs(mesh);
            renderer.sharedMaterial = AirsideMaterialLibrary.CreateShared(
                color, kind, useTextures: hasUsableUvs);
            AirsideSceneIndex.Remember(transform);
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
                mesh.RecalculateNormals();
                var uvs = BuildPlanarUvs(vertices);
                mesh.SetUVs(0, uvs);
                mesh.RecalculateBounds();
                AirsideMeshUtil.UploadStatic(mesh);

                var entry = new MeshEntry(name, mesh, vertices, indices, uvs);
                kit.Meshes.Add(entry);
                kit.ByName[name] = entry;
            }

            return kit.Meshes.Count > 0 ? kit : null;
        }

        private static string CombinedKey(string artRelativePath, (string Name, Color Color)[] parts)
        {
            var sb = new StringBuilder(artRelativePath.Length + parts.Length * 24);
            sb.Append(artRelativePath);
            for (var i = 0; i < parts.Length; i++)
            {
                var part = parts[i];
                sb.Append('|').Append(part.Name).Append('#');
                sb.Append(part.Color.r).Append(',').Append(part.Color.g).Append(',')
                    .Append(part.Color.b).Append(',').Append(part.Color.a);
            }

            return sb.ToString();
        }

        private static CombinedTemplate BuildCombined(GltfKit kit, (string Name, Color Color)[] parts)
        {
            var groups = new List<CombineGroup>(4);
            for (var i = 0; i < parts.Length; i++)
            {
                var part = parts[i];
                if (string.IsNullOrEmpty(part.Name) || !kit.ByName.TryGetValue(part.Name, out var entry))
                    continue;
                var kind = AirsideMaterialLibrary.InferFromMeshName(part.Name);
                CombineGroup group = null;
                for (var g = 0; g < groups.Count; g++)
                {
                    if (SameColor(groups[g].Color, part.Color) && groups[g].Kind == kind)
                    {
                        group = groups[g];
                        break;
                    }
                }

                if (group == null)
                {
                    group = new CombineGroup(part.Color, kind);
                    groups.Add(group);
                }

                group.Entries.Add(entry);
            }

            if (groups.Count == 0)
                return null;

            var vertCount = 0;
            for (var g = 0; g < groups.Count; g++)
            {
                for (var e = 0; e < groups[g].Entries.Count; e++)
                    vertCount += groups[g].Entries[e].Vertices.Length;
            }

            var vertices = new Vector3[vertCount];
            var uvs = new Vector2[vertCount];
            var materials = new Material[groups.Count];
            var trianglesPerGroup = new int[groups.Count][];
            var vertOffset = 0;
            for (var g = 0; g < groups.Count; g++)
            {
                var group = groups[g];
                var triCount = 0;
                for (var e = 0; e < group.Entries.Count; e++)
                    triCount += group.Entries[e].Indices.Length;
                var triangles = new int[triCount];
                var triOffset = 0;
                for (var e = 0; e < group.Entries.Count; e++)
                {
                    var entry = group.Entries[e];
                    var count = entry.Vertices.Length;
                    Array.Copy(entry.Vertices, 0, vertices, vertOffset, count);
                    if (entry.Uvs != null && entry.Uvs.Length == count)
                        Array.Copy(entry.Uvs, 0, uvs, vertOffset, count);
                    var indices = entry.Indices;
                    for (var t = 0; t < indices.Length; t++)
                        triangles[triOffset + t] = indices[t] + vertOffset;
                    triOffset += indices.Length;
                    vertOffset += count;
                }

                trianglesPerGroup[g] = triangles;
                materials[g] = AirsideMaterialLibrary.CreateShared(group.Color, group.Kind);
            }

            var combined = new Mesh { name = "Combined kit" };
            combined.SetVertices(vertices);
            combined.SetUVs(0, uvs);
            combined.subMeshCount = groups.Count;
            for (var g = 0; g < groups.Count; g++)
                combined.SetTriangles(trianglesPerGroup[g], g);
            combined.RecalculateNormals();
            combined.RecalculateBounds();
            AirsideMeshUtil.UploadStatic(combined);
            return new CombinedTemplate(combined, materials);
        }

        private static bool SameColor(Color a, Color b) =>
            a.r.Equals(b.r) && a.g.Equals(b.g) && a.b.Equals(b.b) && a.a.Equals(b.a);

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

        private sealed class CombinedTemplate
        {
            public CombinedTemplate(Mesh mesh, Material[] materials)
            {
                Mesh = mesh;
                Materials = materials;
            }

            public Mesh Mesh { get; }
            public Material[] Materials { get; }
        }

        private sealed class CombineGroup
        {
            public CombineGroup(Color color, AirsideMaterialLibrary.SurfaceKind kind)
            {
                Color = color;
                Kind = kind;
            }

            public Color Color { get; }
            public AirsideMaterialLibrary.SurfaceKind Kind { get; }
            public List<MeshEntry> Entries { get; } = new();
        }

        private sealed class MeshEntry
        {
            public MeshEntry(string name, Mesh mesh, Vector3[] vertices, int[] indices, Vector2[] uvs)
            {
                Name = name;
                Mesh = mesh;
                Vertices = vertices;
                Indices = indices;
                Uvs = uvs;
            }

            public string Name { get; }
            public Mesh Mesh { get; }
            public Vector3[] Vertices { get; }
            public int[] Indices { get; }
            public Vector2[] Uvs { get; }
        }
    }
}

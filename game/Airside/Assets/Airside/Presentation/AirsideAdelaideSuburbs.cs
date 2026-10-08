using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Airside.Presentation
{
    /// <summary>
    /// ADR 0159 (plan P5) — the suburbs around the airfield as low buildings: OpenStreetMap houses,
    /// shops and sheds within 2 km, and street-front houses where OSM's footprints run out
    /// (<see cref="AdelaideSuburbData"/>). Houses are boxes with hipped roofs, everything else its
    /// footprint extruded with a flat roof. Walls carry an Adelaide palette; roofs are half their own
    /// colour, half the satellite image at that spot (<c>Airside/SuburbBuildings</c>), so the extruded
    /// suburb matches the imagery it stands on. One mesh per kilometre tile, no shadows.
    /// </summary>
    public static class AirsideAdelaideSuburbs
    {
        public const string ObjectName = "Adelaide Suburbs";
        public const string ShaderName = "Airside/SuburbBuildings";
        public const float TileMetres = 1000f;

        /// <summary>Buildings sit this far into the ground so no gap shows on a slope.</summary>
        public const float SinkMetres = 0.4f;

        /// <summary>How much of a roof's colour comes from the satellite image under it.</summary>
        public const float RoofSatelliteShare = 0.5f;

        // Index order must match scripts/generate-adelaide-suburbs.py.
        public static readonly Color[] WallColours =
        {
            new(0.80f, 0.76f, 0.66f), // cream render
            new(0.56f, 0.34f, 0.27f), // red brick
            new(0.53f, 0.54f, 0.53f), // bluestone
            new(0.86f, 0.86f, 0.83f), // white
            new(0.70f, 0.70f, 0.68f), // pale grey
            new(0.64f, 0.60f, 0.54f), // warm grey
            new(0.55f, 0.60f, 0.64f)  // blue-grey
        };

        /// <summary>Eucalypt and street-tree greens (ADR 0160); index order matches the tree generator.</summary>
        public static readonly Color[] TreeColours =
        {
            new(0.29f, 0.35f, 0.22f),
            new(0.33f, 0.38f, 0.24f),
            new(0.25f, 0.32f, 0.22f),
            new(0.36f, 0.37f, 0.26f)
        };

        private static readonly Color Bark = new(0.42f, 0.37f, 0.31f);

        public static readonly Color[] RoofColours =
        {
            new(0.62f, 0.33f, 0.24f), // terracotta tile
            new(0.26f, 0.27f, 0.29f), // charcoal steel
            new(0.74f, 0.75f, 0.74f), // pale steel
            new(0.46f, 0.46f, 0.45f), // grey concrete tile
            new(0.66f, 0.68f, 0.69f)  // galvanised
        };

        public static bool TryBuild(Transform root, float horizonFadeStart, float horizonFadeEnd)
        {
            if (root == null || !AirsideSettings.Current.SuburbBuildings)
                return false;
            try
            {
                if (!TryPrepare(root, horizonFadeStart, horizonFadeEnd, out var parent, out var material, out var data, out var trees))
                    return false;
                foreach (var (tile, mesh) in BuildMeshes(data, trees, AirsideAdelaideSurroundings.LandHeight, sourcedTreeBudget:128))
                    AttachTile(parent, material, tile, mesh);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Airside] Suburbs failed to build: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Same suburbs as <see cref="TryBuild"/>, spread across frames. Building every house
        /// and tree inside Awake froze the first picture (ADR 0162). Each slice stays under
        /// a few milliseconds, then one tile mesh is uploaded per frame.
        /// </summary>
        public static IEnumerator BuildGradually(Transform root, float horizonFadeStart, float horizonFadeEnd)
        {
            if (root == null || !AirsideSettings.Current.SuburbBuildings)
                yield break;

            Transform parent = null;
            Material material = null;
            AdelaideSuburbData data = null;
            AdelaideTreeData trees = null;
            var prepared = false;
            try
            {
                prepared = TryPrepare(root, horizonFadeStart, horizonFadeEnd, out parent, out material, out data, out trees);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Airside] Suburbs failed to build: {e.Message}");
            }

            if (!prepared)
                yield break;

            var tiles = new Dictionary<Vector2Int, MeshParts>();
            Func<float, float, float> ground = AirsideAdelaideSurroundings.LandHeight;
            var corners = new float[8];
            var clock = System.Diagnostics.Stopwatch.StartNew();
            const double budgetMs = 2.5;

            var sourcedRemaining = 128;
            var treeList = trees?.Trees;
            var treeCount = treeList?.Count ?? 0;
            for (var i = 0; i < treeCount; i++)
            {
                AppendTree(tiles, treeList[i], ground, ref sourcedRemaining);
                if (clock.Elapsed.TotalMilliseconds < budgetMs)
                    continue;
                yield return null;
                clock.Restart();
            }

            var buildings = data?.Buildings;
            var buildingCount = buildings?.Count ?? 0;
            for (var i = 0; i < buildingCount; i++)
            {
                AppendBuilding(tiles, buildings[i], ground, corners);
                if (clock.Elapsed.TotalMilliseconds < budgetMs)
                    continue;
                yield return null;
                clock.Restart();
            }

            foreach (var pair in tiles)
            {
                AttachTile(parent, material, pair.Key, pair.Value);
                yield return null;
            }
        }

        private static bool TryPrepare(Transform root, float horizonFadeStart, float horizonFadeEnd,
            out Transform parent, out Material material, out AdelaideSuburbData data, out AdelaideTreeData trees)
        {
            parent = null;
            material = null;
            data = null;
            trees = null;
            var shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                Debug.LogWarning($"[Airside] {ShaderName} not in build; suburbs skipped.");
                return false;
            }

            var path = ArtRuntimePaths.ResolveExisting(AdelaideSuburbData.ArtPath);
            data = path != null ? AdelaideSuburbData.Parse(System.IO.File.ReadAllBytes(path)) : null;
            var treePath = ArtRuntimePaths.ResolveExisting(AdelaideTreeData.ArtPath);
            trees = treePath != null ? AdelaideTreeData.Parse(System.IO.File.ReadAllBytes(treePath)) : null;
            if ((data == null || data.Buildings.Count == 0) && (trees == null || trees.Trees.Count == 0))
                return false;

            material = new Material(shader) { name = "mat_adelaide_suburbs_v01", enableInstancing = true };
            var satellite = AirsideArtTextures.Load(AirsideAdelaideSurroundings.SatelliteTexturePath,
                wrap: TextureWrapMode.Clamp);
            if (satellite != null)
                material.SetTexture("_SatelliteAlbedo", satellite);
            material.SetFloat("_SatelliteExtent", AirsideAdelaideSurroundings.SatelliteExtentMetres);
            material.SetColor("_SatelliteTint", new Color(0.56f, 0.58f, 0.56f, 1f));
            material.SetFloat("_HorizonFadeStart", horizonFadeStart);
            material.SetFloat("_HorizonFadeEnd", horizonFadeEnd);

            parent = new GameObject(ObjectName).transform;
            parent.SetParent(root, false);
            return true;
        }

        private static void AttachTile(Transform parent, Material material, Vector2Int tile, Mesh mesh)
        {
            var go = new GameObject($"{ObjectName} {tile.x},{tile.y}");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private static void AttachTile(Transform parent, Material material, Vector2Int tile, MeshParts parts) =>
            AttachTile(parent, material, tile, parts.ToMesh($"{ObjectName} {tile.x},{tile.y}"));

        /// <summary>One mesh per <see cref="TileMetres"/> tile. <paramref name="groundHeight"/> is the world y of the land.</summary>
        public static List<(Vector2Int Tile, Mesh Mesh)> BuildMeshes(AdelaideSuburbData data,
            AdelaideTreeData trees, Func<float, float, float> groundHeight, int sourcedTreeBudget = 0)
        {
            var tiles = new Dictionary<Vector2Int, MeshParts>();
            var corners = new float[8];
            if (trees != null)
                foreach (var tree in trees.Trees)
                    AppendTree(tiles, tree, groundHeight, ref sourcedTreeBudget);

            var buildings = data?.Buildings ?? Array.Empty<AdelaideSuburbData.Building>();
            foreach (var b in buildings)
                AppendBuilding(tiles, b, groundHeight, corners);

            var meshes = new List<(Vector2Int, Mesh)>(tiles.Count);
            foreach (var pair in tiles)
                meshes.Add((pair.Key, pair.Value.ToMesh($"{ObjectName} {pair.Key.x},{pair.Key.y}")));
            return meshes;
        }

        private static void AppendTree(Dictionary<Vector2Int, MeshParts> tiles, AdelaideTreeData.Tree tree,
            Func<float, float, float> groundHeight, ref int sourcedRemaining)
        {
            var key = new Vector2Int(Mathf.FloorToInt(tree.X / TileMetres), Mathf.FloorToInt(tree.Z / TileMetres));
            if (!tiles.TryGetValue(key, out var parts))
                tiles[key] = parts = new MeshParts();
            var baseY = groundHeight(tree.X, tree.Z) - SinkMetres;
            if (sourcedRemaining > 0 && tree.X*tree.X + tree.Z*tree.Z < 1100f*1100f &&
                AddSourcedTree(parts, tree, baseY))
                sourcedRemaining--;
            else AddTree(parts, tree, baseY);
        }

        private static void AppendBuilding(Dictionary<Vector2Int, MeshParts> tiles, AdelaideSuburbData.Building b,
            Func<float, float, float> groundHeight, float[] corners)
        {
            // Open canopies (taxi and bus ranks, car-park entrances) are drawn on posts by the precinct (ADR 0184),
            // not as a solid block that would bury the road and cars under them.
            if (!b.IsHipped && AdelaidePrecinctGeometry.IsCanopyFootprint(b.Footprint))
                return;
            var key = new Vector2Int(Mathf.FloorToInt(b.CentreX / TileMetres), Mathf.FloorToInt(b.CentreZ / TileMetres));
            if (!tiles.TryGetValue(key, out var parts))
                tiles[key] = parts = new MeshParts();

            float[] footprint;
            if (b.IsHipped)
            {
                AdelaideSuburbData.HouseCorners(b, corners);
                footprint = corners;
            }
            else
            {
                footprint = b.Footprint;
            }

            var n = footprint.Length / 2;
            var baseY = float.MaxValue;
            for (var i = 0; i < n; i++)
                baseY = Mathf.Min(baseY, groundHeight(footprint[i * 2], footprint[i * 2 + 1]));
            baseY -= SinkMetres;
            var eaves = baseY + SinkMetres + b.WallHeight;
            var wall = WallColours[Mathf.Clamp(b.Wall, 0, WallColours.Length - 1)].linear;
            wall.a = 0f;
            var roof = RoofColours[Mathf.Clamp(b.Roof, 0, RoofColours.Length - 1)].linear;
            roof.a = RoofSatelliteShare;
            var centre = new Vector3(b.CentreX, 0f, b.CentreZ);

            for (var i = 0; i < n; i++)
            {
                var j = (i + 1) % n;
                var p = new Vector3(footprint[i * 2], 0f, footprint[i * 2 + 1]);
                var q = new Vector3(footprint[j * 2], 0f, footprint[j * 2 + 1]);
                var mid = (p + q) * 0.5f;
                var outward = new Vector3(q.z - p.z, 0f, p.x - q.x);
                if (Vector3.Dot(outward, mid - centre) < 0f)
                    outward = -outward;
                parts.Quad(new Vector3(p.x, baseY, p.z), new Vector3(q.x, baseY, q.z),
                    new Vector3(q.x, eaves, q.z), new Vector3(p.x, eaves, p.z), outward, wall);
            }

            if (b.IsHipped)
            {
                // Ridge along the long axis, hips at both ends (a pyramid when square).
                var ux = Mathf.Cos(b.Angle);
                var uz = Mathf.Sin(b.Angle);
                var ridgeHalf = Mathf.Max(0f, b.HalfLength - b.HalfWidth);
                var top = eaves + b.RoofRise;
                var r0 = new Vector3(b.CentreX - ux * ridgeHalf, top, b.CentreZ - uz * ridgeHalf);
                var r1 = new Vector3(b.CentreX + ux * ridgeHalf, top, b.CentreZ + uz * ridgeHalf);
                var c0 = new Vector3(corners[0], eaves, corners[1]);
                var c1 = new Vector3(corners[2], eaves, corners[3]);
                var c2 = new Vector3(corners[4], eaves, corners[5]);
                var c3 = new Vector3(corners[6], eaves, corners[7]);
                parts.Quad(c0, c1, r1, r0, Vector3.up, roof);
                parts.Quad(c2, c3, r0, r1, Vector3.up, roof);
                parts.Triangle(c1, c2, r1, Vector3.up, roof);
                parts.Triangle(c3, c0, r0, Vector3.up, roof);
            }
            else
            {
                var tris = b.RoofTriangles;
                for (var t = 0; t + 2 < tris.Length; t += 3)
                {
                    Vector3 At(int k) => new(footprint[k * 2], eaves, footprint[k * 2 + 1]);
                    parts.Triangle(At(tris[t]), At(tris[t + 1]), At(tris[t + 2]), Vector3.up, roof);
                }
            }
        }

        /// <summary>
        /// A low-poly eucalypt with bake-time LOD (ADR 0212 + 0220): Full 3-lobe near
        /// the field, primary lobe mid-range, crossed billboard cards far out.
        /// </summary>
        private sealed class FoliagePart
        {
            public Vector3[] Vertices;
            public int[] Triangles;
            public bool Bark;
        }
        private static List<FoliagePart> _foliage;
        private static Bounds _foliageBounds;
        private static bool _foliageResolved;

        private static bool AddSourcedTree(MeshParts target, AdelaideTreeData.Tree tree, float baseY)
        {
            if (!_foliageResolved)
            {
                _foliageResolved = true;
                _foliage = new List<FoliagePart>();
                foreach (var name in new[]{"tree_trunk_0","tree_foliage_0","tree_trunk_1","tree_foliage_1","tree_trunk_2","tree_foliage_2"})
                    if (ArtGltfLoader.TryGetSharedMesh("Models/Environment/mdl_local_foliage_v01.gltf", name, out var mesh))
                    {
                        if (_foliage.Count == 0) _foliageBounds = mesh.bounds;
                        else _foliageBounds.Encapsulate(mesh.bounds);
                        _foliage.Add(new FoliagePart { Vertices=mesh.vertices, Triangles=mesh.triangles, Bark=name.Contains("trunk") });
                    }
            }
            if (_foliage.Count == 0) return false;
            var size = _foliageBounds.size;
            var scale = new Vector3(tree.CrownRadius*2/Mathf.Max(size.x,.1f),tree.Height/Mathf.Max(size.y,.1f),tree.CrownRadius*2/Mathf.Max(size.z,.1f));
            var yaw = Quaternion.Euler(0,(tree.X*.137f+tree.Z*.071f)*Mathf.Rad2Deg,0);
            var offset = new Vector3(_foliageBounds.center.x,_foliageBounds.min.y,_foliageBounds.center.z);
            foreach (var part in _foliage)
            {
                var colour = (part.Bark ? Bark : TreeColours[Mathf.Clamp(tree.Colour,0,TreeColours.Length-1)]).linear;
                colour.a=0; // Foliage carries its own colour, rather than sampling roofs from the satellite.
                for (var i=0;i<part.Triangles.Length;i+=3)
                {
                    var a=new Vector3(tree.X,baseY,tree.Z)+yaw*Vector3.Scale(part.Vertices[part.Triangles[i]]-offset,scale);
                    var b=new Vector3(tree.X,baseY,tree.Z)+yaw*Vector3.Scale(part.Vertices[part.Triangles[i+1]]-offset,scale);
                    var c=new Vector3(tree.X,baseY,tree.Z)+yaw*Vector3.Scale(part.Vertices[part.Triangles[i+2]]-offset,scale);
                    target.Triangle(a,b,c,Vector3.Cross(b-a,c-a),colour);
                }
            }
            return true;
        }

        private static void AddTree(MeshParts parts, AdelaideTreeData.Tree tree, float baseY)
        {
            var spin = (tree.X * 0.137f + tree.Z * 0.071f) % (Mathf.PI * 2f);
            var leaf = TreeColours[Mathf.Clamp(tree.Colour, 0, TreeColours.Length - 1)].linear;
            leaf.a = 0f;
            var bark = Bark.linear;
            bark.a = 0f;
            var detail = AdelaideTreeLod.ForPosition(tree.X, tree.Z);

            var trunkTopFrac = 0.32f;
            var trunkTop = baseY + tree.Height * trunkTopFrac;
            var trunkR = Mathf.Max(0.18f, tree.CrownRadius * 0.08f);
            for (var k = 0; k < 3; k++)
            {
                var a0 = spin + k * Mathf.PI * 2f / 3f;
                var a1 = spin + (k + 1) * Mathf.PI * 2f / 3f;
                var p = new Vector3(tree.X + Mathf.Cos(a0) * trunkR, baseY, tree.Z + Mathf.Sin(a0) * trunkR);
                var q = new Vector3(tree.X + Mathf.Cos(a1) * trunkR, baseY, tree.Z + Mathf.Sin(a1) * trunkR);
                var outward = new Vector3(Mathf.Cos((a0 + a1) * 0.5f), 0f, Mathf.Sin((a0 + a1) * 0.5f));
                parts.Quad(p, q, new Vector3(q.x, trunkTop + 0.5f, q.z), new Vector3(p.x, trunkTop + 0.5f, p.z),
                    outward, bark);
            }

            if (detail == AdelaideTreeLod.Detail.Billboard)
            {
                // Two crossed vertical cards — 4 tris — readable as canopy from overview.
                var halfW = tree.CrownRadius;
                var top = baseY + tree.Height;
                var mid = baseY + tree.Height * 0.55f;
                var shade = leaf * 0.9f;
                for (var c = 0; c < 2; c++)
                {
                    var yaw = spin + c * Mathf.PI * 0.5f;
                    var dx = Mathf.Cos(yaw) * halfW;
                    var dz = Mathf.Sin(yaw) * halfW;
                    var a = new Vector3(tree.X - dx, mid, tree.Z - dz);
                    var b = new Vector3(tree.X + dx, mid, tree.Z + dz);
                    var apex = new Vector3(tree.X, top, tree.Z);
                    var low = new Vector3(tree.X, trunkTop, tree.Z);
                    var side = new Vector3(-Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
                    parts.Triangle(a, b, apex, side + Vector3.up * 0.5f, leaf);
                    parts.Triangle(a, b, low, side - Vector3.up * 0.3f, shade);
                }

                return;
            }

            var lobes = AdelaideTreeLod.LobesForDetail(tree.X, tree.Z, detail);
            for (var i = 0; i < lobes.Length; i++)
            {
                var lobe = lobes[i];
                var cx = tree.X + lobe.OffsetXFrac * tree.CrownRadius;
                var cz = tree.Z + lobe.OffsetZFrac * tree.CrownRadius;
                var radius = tree.CrownRadius * lobe.RadiusScale;
                var bottom = baseY + tree.Height * lobe.BottomHeightFrac;
                var ring = baseY + tree.Height * lobe.RingHeightFrac;
                var apex = new Vector3(cx, baseY + tree.Height * lobe.ApexHeightFrac, cz);
                var low = new Vector3(cx, bottom, cz);
                var lobeLeaf = i == 0 ? leaf : leaf * (0.92f - i * 0.04f);
                var lobeShade = lobeLeaf * 0.85f;
                var sides = lobe.SideCount;
                var step = Mathf.PI * 2f / sides;
                for (var k = 0; k < sides; k++)
                {
                    var a0 = spin + k * step;
                    var a1 = spin + (k + 1) * step;
                    // Alternate ring radii break regular polygons into looser crowns.
                    var r0 = radius * (k % 2 == 0 ? 1f : 0.82f);
                    var r1 = radius * ((k + 1) % 2 == 0 ? 1f : 0.82f);
                    var p = new Vector3(cx + Mathf.Cos(a0) * r0, ring, cz + Mathf.Sin(a0) * r0);
                    var q = new Vector3(cx + Mathf.Cos(a1) * r1, ring, cz + Mathf.Sin(a1) * r1);
                    var side = new Vector3(Mathf.Cos((a0 + a1) * 0.5f), 0f, Mathf.Sin((a0 + a1) * 0.5f));
                    parts.Triangle(p, q, apex, side + Vector3.up * 0.8f, lobeLeaf);
                    parts.Triangle(p, q, low, side - Vector3.up * 0.5f, lobeShade);
                }
            }
        }

        /// <summary>Flat-shaded triangles, each wound to face <c>facing</c> (Unity: normal = (b-a)×(c-a)).</summary>
        private sealed class MeshParts
        {
            private readonly List<Vector3> _vertices = new();
            private readonly List<Vector3> _normals = new();
            private readonly List<Color32> _colours = new();
            private readonly List<int> _triangles = new();

            /// <summary>A planar quad a-b-c-d (in order round its edge) on four shared vertices.</summary>
            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 facing, Color colour)
            {
                var normal = Vector3.Cross(b - a, c - a);
                if (normal.sqrMagnitude < 1e-8f)
                {
                    Triangle(a, c, d, facing, colour);
                    return;
                }

                var flip = Vector3.Dot(normal, facing) < 0f;
                if (flip)
                    normal = -normal;
                normal.Normalize();
                var i = _vertices.Count;
                _vertices.Add(a);
                _vertices.Add(b);
                _vertices.Add(c);
                _vertices.Add(d);
                Color32 packed = colour;
                for (var k = 0; k < 4; k++)
                {
                    _normals.Add(normal);
                    _colours.Add(packed);
                }

                if (flip)
                {
                    _triangles.Add(i); _triangles.Add(i + 2); _triangles.Add(i + 1);
                    _triangles.Add(i); _triangles.Add(i + 3); _triangles.Add(i + 2);
                }
                else
                {
                    _triangles.Add(i); _triangles.Add(i + 1); _triangles.Add(i + 2);
                    _triangles.Add(i); _triangles.Add(i + 2); _triangles.Add(i + 3);
                }
            }

            public void Triangle(Vector3 a, Vector3 b, Vector3 c, Vector3 facing, Color colour)
            {
                var normal = Vector3.Cross(b - a, c - a);
                if (normal.sqrMagnitude < 1e-8f)
                    return;
                if (Vector3.Dot(normal, facing) < 0f)
                {
                    (b, c) = (c, b);
                    normal = -normal;
                }

                normal.Normalize();
                var i = _vertices.Count;
                _vertices.Add(a);
                _vertices.Add(b);
                _vertices.Add(c);
                Color32 packed = colour;
                for (var k = 0; k < 3; k++)
                {
                    _normals.Add(normal);
                    _colours.Add(packed);
                }

                _triangles.Add(i);
                _triangles.Add(i + 1);
                _triangles.Add(i + 2);
            }

            public Mesh ToMesh(string name)
            {
                var mesh = new Mesh
                {
                    name = name,
                    indexFormat = _vertices.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16
                };
                mesh.SetVertices(_vertices);
                mesh.SetNormals(_normals);
                mesh.SetColors(_colours);
                mesh.SetTriangles(_triangles, 0);
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}

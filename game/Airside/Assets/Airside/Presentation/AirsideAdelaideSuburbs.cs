using System;
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
                var shader = Shader.Find(ShaderName);
                if (shader == null)
                {
                    Debug.LogWarning($"[Airside] {ShaderName} not in build; suburbs skipped.");
                    return false;
                }

                var path = ArtRuntimePaths.ResolveExisting(AdelaideSuburbData.ArtPath);
                var data = path != null ? AdelaideSuburbData.Parse(System.IO.File.ReadAllBytes(path)) : null;
                if (data == null || data.Buildings.Count == 0)
                    return false;

                var material = new Material(shader) { name = "mat_adelaide_suburbs_v01", enableInstancing = true };
                var satellite = AirsideArtTextures.Load(AirsideAdelaideSurroundings.SatelliteTexturePath,
                    wrap: TextureWrapMode.Clamp);
                if (satellite != null)
                    material.SetTexture("_SatelliteAlbedo", satellite);
                material.SetFloat("_SatelliteExtent", AirsideAdelaideSurroundings.SatelliteExtentMetres);
                material.SetColor("_SatelliteTint", new Color(0.56f, 0.58f, 0.56f, 1f));
                material.SetFloat("_HorizonFadeStart", horizonFadeStart);
                material.SetFloat("_HorizonFadeEnd", horizonFadeEnd);

                var parent = new GameObject(ObjectName).transform;
                parent.SetParent(root, false);
                foreach (var (tile, mesh) in BuildMeshes(data, AirsideAdelaideSurroundings.LandHeight))
                {
                    var go = new GameObject($"{ObjectName} {tile.x},{tile.y}");
                    go.transform.SetParent(parent, false);
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var renderer = go.AddComponent<MeshRenderer>();
                    renderer.sharedMaterial = material;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = true;
                }

                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Airside] Suburbs failed to build: {e.Message}");
                return false;
            }
        }

        /// <summary>One mesh per <see cref="TileMetres"/> tile. <paramref name="groundHeight"/> is the world y of the land.</summary>
        public static List<(Vector2Int Tile, Mesh Mesh)> BuildMeshes(AdelaideSuburbData data,
            Func<float, float, float> groundHeight)
        {
            var tiles = new Dictionary<Vector2Int, MeshParts>();
            var corners = new float[8];
            foreach (var b in data.Buildings)
            {
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

            var meshes = new List<(Vector2Int, Mesh)>(tiles.Count);
            foreach (var pair in tiles)
                meshes.Add((pair.Key, pair.Value.ToMesh($"{ObjectName} {pair.Key.x},{pair.Key.y}")));
            return meshes;
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

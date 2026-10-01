using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Airside.Simulation;
using UnityEngine;
using UnityEngine.Rendering;

namespace Airside.Presentation
{
    /// <summary>
    /// ADR 0184 — draws the complete OSM road network (<see cref="AdelaideRoadNetwork"/>): every road at its
    /// real width, junction fills, lane and edge paint, zebra crossings, airside roads on the apron and
    /// plateau. Geometry comes from the pure <see cref="AdelaideRoadGeometry"/>; this only copies each
    /// 1.5 km tile into a mesh. Fails soft, in which case the legacy arterial ribbons draw as before.
    /// </summary>
    public static class AirsideAdelaideRoadNetworkMesh
    {
        public const string ObjectName = "Adelaide Road Network";
        public const string ShaderName = "Airside/Surroundings";
        private static readonly Color PaintColour = new Color(0.88f, 0.88f, 0.85f);

        /// <summary>True when the network can be drawn: data present, the shader available, not switched off.</summary>
        public static bool CanBuild() =>
            AdelaideRoadNetwork.Roads.Length > 0
            && Shader.Find(ShaderName) != null;

        private sealed class Sinks
        {
            public RoadMeshSink Asphalt, Paint, Props;
        }

        /// <summary>
        /// Builds the network's geometry on a worker thread (it is pure maths over static data), then copies it into
        /// meshes a few milliseconds a frame, so the first picture does not wait for it. Logs a warning if the geometry cannot be built.
        /// </summary>
        public static IEnumerator BuildAsync(Transform root, float pavementWorldY, Func<float, float, float> groundHeight)
        {
            var shader = Shader.Find(ShaderName);
            if (shader == null)
                yield break;

            // Anything lazily loaded on the main thread is touched here, before the worker starts.
            _ = AdelaideLayout.Taxiways.Length;
            _ = AirsideAdelaideSurroundings.Terrain;
            var options = new RoadBuildOptions
            {
                GroundHeight = groundHeight,
                BaseY = pavementWorldY,
                AirsideRule = new PavementRule().Evaluate
            };
            var task = Task.Run(() => BuildSinks(options));
            while (!task.IsCompleted)
                yield return null;
            if (task.IsFaulted || task.Result == null || task.Result.Asphalt.VertexCount < 3)
            {
                Debug.LogWarning($"[Airside] Road network failed to build: {task.Exception?.GetBaseException().Message}");
                yield break;
            }

            if (root == null)
                yield break;
            var sinks = task.Result;
            var parent = new GameObject(ObjectName);
            parent.transform.SetParent(root, false);
            var asphaltMaterial = new Material(shader)
            {
                name = "mat_adelaide_road_network_v01",
                enableInstancing = true
            };
            var paintMaterial = AirsideMaterialLibrary.CreateShared(PaintColour, AirsideMaterialLibrary.SurfaceKind.PaintedLine);
            var clock = System.Diagnostics.Stopwatch.StartNew();
            const double budgetMs = 3.0;
            foreach (var (sink, material, colours, label) in new[]
            {
                (sinks.Asphalt, asphaltMaterial, true, "Roads"),
                (sinks.Paint, paintMaterial, false, "Road paint"),
                (sinks.Props, asphaltMaterial, true, "Car parks")
            })
            {
                foreach (var tile in sink.Tiles)
                {
                    try
                    {
                        AddTile(parent.transform, $"{label} {tile.Key}", tile.Value, material, colours);
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[Airside] Road tile {label} {tile.Key} failed: {e.Message}");
                    }

                    if (clock.Elapsed.TotalMilliseconds < budgetMs)
                        continue;
                    yield return null;
                    clock.Restart();
                }
            }
        }

        private static Sinks BuildSinks(RoadBuildOptions options)
        {
            var asphalt = new RoadMeshSink();
            AdelaideRoadGeometry.BuildAsphalt(asphalt, options);
            AdelaideCarParkGeometry.BuildSurfaces(asphalt, options);
            AdelaideEmergencyAviationGeometry.BuildSurface(asphalt, options);
            AdelaidePrecinctGeometry.BuildPaths(asphalt, options);
            var paint = new RoadMeshSink();
            AdelaideRoadGeometry.BuildMarkings(paint, options);
            AdelaideCarParkGeometry.BuildBayLines(paint, options);
            AdelaideRoadFurnitureGeometry.BuildRoadPaint(paint, options);
            AdelaideEmergencyAviationGeometry.BuildPaint(paint, options);
            // Parked cars, street lamps, canopies, solar arrays, tanks, masts and bus stops: solid, lit,
            // vertex-coloured (alpha 1 keeps the satellite out of them).
            var props = new RoadMeshSink();
            AdelaideCarParkGeometry.BuildCars(props, options);
            AdelaideCarParkGeometry.BuildLamps(props, options);
            AdelaideCarParkGeometry.BuildPalms(props, options);
            AdelaideWindbreakGeometry.Build(props, options);
            AdelaideAvenueGeometry.Build(props, options);
            AdelaideDuneScrubGeometry.Build(props, options);
            AdelaideNorfolkPineGeometry.Build(props, options);
            AdelaidePrecinctGeometry.BuildCanopies(props, options);
            AdelaidePrecinctGeometry.BuildSolar(props, options);
            AdelaidePrecinctGeometry.BuildTanks(props, options);
            AdelaidePrecinctGeometry.BuildMasts(props, options);
            AdelaidePrecinctGeometry.BuildBusStops(props, options);
            AdelaidePrecinctGeometry.BuildHoldSigns(props, options);
            AdelaideRoadFurnitureGeometry.BuildSignals(props, options);
            AdelaideRoadFurnitureGeometry.BuildSigns(props, options);
            AdelaideEmergencyAviationGeometry.BuildFixtures(props, options);
            return new Sinks { Asphalt = asphalt, Paint = paint, Props = props };
        }

        // Compact vertices: position as floats, the normal as four signed bytes (unit axes map to exactly +-127), the
        // colour as four 16-bit unorm channels (no visible banding on dark asphalt). 24 bytes, or 16 without a colour,
        // against 40 for the plain Vector3 + Vector3 + Color layout: half the memory and bandwidth for the same picture.
        [StructLayout(LayoutKind.Sequential)]
        private struct ColouredVertex
        {
            public Vector3 Position;
            public sbyte Nx, Ny, Nz, Nw;
            public ushort R, G, B, A;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PlainVertex
        {
            public Vector3 Position;
            public sbyte Nx, Ny, Nz, Nw;
        }

        private static sbyte Snorm(float v) => (sbyte)Mathf.Clamp(Mathf.RoundToInt(v * 127f), -127, 127);

        private static ushort Unorm16(float v) => (ushort)Mathf.Clamp(Mathf.RoundToInt(v * 65535f), 0, 65535);

        private static Mesh BuildMesh(string name, RoadMeshTile tile, bool withColours)
        {
            var count = tile.VertexCount;
            var p = tile.Positions;
            var n = tile.Normals;
            var c = tile.Colors;
            var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            for (var i = 0; i < count; i++)
            {
                var v = new Vector3(p[i * 3], p[i * 3 + 1], p[i * 3 + 2]);
                min = Vector3.Min(min, v);
                max = Vector3.Max(max, v);
            }

            if (withColours)
            {
                var vertices = new ColouredVertex[count];
                for (var i = 0; i < count; i++)
                    vertices[i] = new ColouredVertex
                    {
                        Position = new Vector3(p[i * 3], p[i * 3 + 1], p[i * 3 + 2]),
                        Nx = Snorm(n[i * 3]), Ny = Snorm(n[i * 3 + 1]), Nz = Snorm(n[i * 3 + 2]),
                        R = Unorm16(c[i * 4]), G = Unorm16(c[i * 4 + 1]), B = Unorm16(c[i * 4 + 2]), A = Unorm16(c[i * 4 + 3])
                    };
                mesh.SetVertexBufferParams(count,
                    new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
                    new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.SNorm8, 4),
                    new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UNorm16, 4));
                mesh.SetVertexBufferData(vertices, 0, 0, count);
            }
            else
            {
                var vertices = new PlainVertex[count];
                for (var i = 0; i < count; i++)
                    vertices[i] = new PlainVertex
                    {
                        Position = new Vector3(p[i * 3], p[i * 3 + 1], p[i * 3 + 2]),
                        Nx = Snorm(n[i * 3]), Ny = Snorm(n[i * 3 + 1]), Nz = Snorm(n[i * 3 + 2])
                    };
                mesh.SetVertexBufferParams(count,
                    new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
                    new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.SNorm8, 4));
                mesh.SetVertexBufferData(vertices, 0, 0, count);
            }

            var indices = tile.Triangles.ToArray();
            mesh.SetIndexBufferParams(indices.Length, IndexFormat.UInt32);
            mesh.SetIndexBufferData(indices, 0, 0, indices.Length);
            mesh.subMeshCount = 1;
            mesh.SetSubMesh(0, new SubMeshDescriptor(0, indices.Length, MeshTopology.Triangles),
                MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);
            mesh.bounds = new Bounds((min + max) * 0.5f, max - min);
            // Nothing reads these meshes on the CPU again, so let Unity drop its copy.
            mesh.UploadMeshData(true);
            return mesh;
        }

        private static Mesh BuildMeshPlain(string name, RoadMeshTile tile, bool withColours)
        {
            var count = tile.VertexCount;
            var vertices = new Vector3[count];
            var normals = new Vector3[count];
            var p = tile.Positions;
            var nrm = tile.Normals;
            for (var i = 0; i < count; i++)
            {
                vertices[i] = new Vector3(p[i * 3], p[i * 3 + 1], p[i * 3 + 2]);
                normals[i] = new Vector3(nrm[i * 3], nrm[i * 3 + 1], nrm[i * 3 + 2]);
            }

            var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            mesh.vertices = vertices;
            mesh.normals = normals;
            if (withColours)
            {
                var colours = new Color[count];
                var c = tile.Colors;
                for (var i = 0; i < count; i++)
                    colours[i] = new Color(c[i * 4], c[i * 4 + 1], c[i * 4 + 2], c[i * 4 + 3]);
                mesh.colors = colours;
            }

            mesh.triangles = tile.Triangles.ToArray();
            mesh.RecalculateBounds();
            mesh.UploadMeshData(true);
            return mesh;
        }

        private static Material _propMaterial;

        /// <summary>
        /// A solid, vertex-coloured prop mesh built at run time (the temporary boarding tape). Null when the road shader
        /// is unavailable, in which case nothing is drawn.
        /// </summary>
        internal static GameObject BuildProps(Transform parent, string name, RoadMeshSink sink)
        {
            if (sink == null || sink.VertexCount < 3)
                return null;
            if (_propMaterial == null)
            {
                var shader = Shader.Find(ShaderName);
                if (shader == null)
                    return null;
                _propMaterial = new Material(shader) { name = "mat_boarding_tape_v01", enableInstancing = true };
            }

            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            foreach (var tile in sink.Tiles)
                AddTile(root.transform, $"{name} {tile.Key}", tile.Value, _propMaterial, true);
            return root;
        }

        private static void AddTile(Transform parent, string name, RoadMeshTile tile, Material material, bool withColours)
        {
            if (tile.VertexCount < 3)
                return;
            Mesh mesh;
            try
            {
                mesh = BuildMesh(name, tile, withColours);
            }
            catch (Exception e)
            {
                // A platform without these vertex formats still draws the same picture from the plain layout.
                Debug.LogWarning($"[Airside] Compact road mesh unavailable ({e.Message}); using the plain layout.");
                mesh = BuildMeshPlain(name, tile, withColours);
            }

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        /// <summary>
        /// What an airside road does at a point: nothing on a runway or taxiway, paint only on apron concrete,
        /// asphalt elsewhere. One instance per build, so nothing is shared between builds or threads: it caches on a
        /// 3 m grid and skips the pavement's bounding box test, because the distance queries walk every taxiway.
        /// </summary>
        private sealed class PavementRule
        {
            private readonly Dictionary<long, RoadSurfaceUse> _cache = new Dictionary<long, RoadSurfaceUse>();
            private readonly float _minX, _maxX, _minZ, _maxZ;

            public PavementRule()
            {
                float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue;
                void Grow(float[] xz)
                {
                    for (var i = 0; i + 1 < xz.Length; i += 2)
                    {
                        minX = Mathf.Min(minX, xz[i]);
                        maxX = Mathf.Max(maxX, xz[i]);
                        minZ = Mathf.Min(minZ, xz[i + 1]);
                        maxZ = Mathf.Max(maxZ, xz[i + 1]);
                    }
                }

                foreach (var taxiway in AdelaideLayout.Taxiways)
                    Grow(taxiway.Xz);
                foreach (var apron in AdelaideLayout.Aprons)
                    Grow(apron.Xz);
                // The main runway and the cross runway, plus room for shoulders.
                minX = Mathf.Min(minX, -AirsideAdelaidePavement.MainHalfLength) - 40f;
                maxX = Mathf.Max(maxX, AirsideAdelaidePavement.MainHalfLength) + 40f;
                _minX = minX;
                _maxX = maxX;
                _minZ = minZ - 40f;
                _maxZ = maxZ + 40f;
            }

            public RoadSurfaceUse Evaluate(float x, float z)
            {
                if (x < _minX || x > _maxX || z < _minZ || z > _maxZ)
                    return RoadSurfaceUse.Asphalt;
                var key = (long)Mathf.Round(x / 3f) * 100003L + (long)Mathf.Round(z / 3f);
                if (_cache.TryGetValue(key, out var cached))
                    return cached;

                RoadSurfaceUse use;
                if (AirsideAdelaidePavement.DistanceToRunwayPavement(x, z) < 1f
                    || AirsideAdelaidePavement.DistanceToTaxiway(x, z) <= 0f)
                    use = RoadSurfaceUse.Skip;
                else if (AirsideAdelaidePavement.ContainsApron(x, z))
                    use = RoadSurfaceUse.PaintOnly;
                else
                    use = RoadSurfaceUse.Asphalt;
                _cache[key] = use;
                return use;
            }
        }
    }
}

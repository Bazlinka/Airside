using System;
using System.Collections;
using System.Collections.Generic;
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
        public const string LegacyFlag = "-airsideLegacyRoads";
        private static readonly Color PaintColour = new Color(0.88f, 0.88f, 0.85f);

        // The pavement rule is asked about the same spots by ribbons, paint, junctions and crossings; cache it.
        private static readonly Dictionary<long, RoadSurfaceUse> RuleCache = new Dictionary<long, RoadSurfaceUse>();
        private static float _minX, _maxX, _minZ, _maxZ;
        private static bool _boundsReady;

        /// <summary>True when the network can be drawn: data present, the shader available, not switched off.</summary>
        public static bool CanBuild() =>
            !AirsideBareField.HasLaunchFlag(LegacyFlag)
            && AdelaideRoadNetwork.Roads.Length > 0
            && Shader.Find(AirsideAdelaideRoads.ShaderName) != null;

        private sealed class Sinks
        {
            public RoadMeshSink Asphalt, Paint, Props;
        }

        /// <summary>
        /// Builds the network's geometry on a worker thread (it is pure maths over static data), then copies it into
        /// meshes a few milliseconds a frame, so the first picture does not wait for it. Calls
        /// <paramref name="onFailed"/> if the geometry cannot be built, so the caller can fall back to the old roads.
        /// </summary>
        public static IEnumerator BuildAsync(Transform root, float pavementWorldY, Func<float, float, float> groundHeight,
            Action onFailed)
        {
            var shader = Shader.Find(AirsideAdelaideRoads.ShaderName);
            if (shader == null)
            {
                onFailed?.Invoke();
                yield break;
            }

            RuleCache.Clear();
            var options = new RoadBuildOptions
            {
                GroundHeight = groundHeight,
                BaseY = pavementWorldY,
                AirsideRule = PavementRule
            };
            var task = Task.Run(() => BuildSinks(options));
            while (!task.IsCompleted)
                yield return null;
            RuleCache.Clear();
            if (task.IsFaulted || task.Result == null || task.Result.Asphalt.VertexCount < 3)
            {
                Debug.LogWarning($"[Airside] Road network failed to build: {task.Exception?.GetBaseException().Message}");
                onFailed?.Invoke();
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
            AdelaidePrecinctGeometry.BuildPaths(asphalt, options);
            var paint = new RoadMeshSink();
            AdelaideRoadGeometry.BuildMarkings(paint, options);
            AdelaideCarParkGeometry.BuildBayLines(paint, options);
            // Parked cars, street lamps, canopies, solar arrays, tanks, masts and bus stops: solid, lit,
            // vertex-coloured (alpha 1 keeps the satellite out of them).
            var props = new RoadMeshSink();
            AdelaideCarParkGeometry.BuildCars(props, options);
            AdelaideCarParkGeometry.BuildLamps(props, options);
            AdelaidePrecinctGeometry.BuildCanopies(props, options);
            AdelaidePrecinctGeometry.BuildSolar(props, options);
            AdelaidePrecinctGeometry.BuildTanks(props, options);
            AdelaidePrecinctGeometry.BuildMasts(props, options);
            AdelaidePrecinctGeometry.BuildBusStops(props, options);
            AdelaidePrecinctGeometry.BuildHoldSigns(props, options);
            return new Sinks { Asphalt = asphalt, Paint = paint, Props = props };
        }

        private static void AddTile(Transform parent, string name, RoadMeshTile tile, Material material, bool withColours)
        {
            var count = tile.VertexCount;
            if (count < 3)
                return;
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
        /// asphalt elsewhere. Cached on a 3 m grid and skipped outside the pavement's bounding box, because the
        /// distance queries walk every taxiway.
        /// </summary>
        public static RoadSurfaceUse PavementRule(float x, float z)
        {
            EnsureBounds();
            if (x < _minX || x > _maxX || z < _minZ || z > _maxZ)
                return RoadSurfaceUse.Asphalt;
            var key = (long)Mathf.Round(x / 3f) * 100003L + (long)Mathf.Round(z / 3f);
            if (RuleCache.TryGetValue(key, out var cached))
                return cached;

            RoadSurfaceUse use;
            if (AirsideAdelaidePavement.DistanceToRunwayPavement(x, z) < 1f || AirsideAdelaidePavement.DistanceToTaxiway(x, z) <= 0f)
                use = RoadSurfaceUse.Skip;
            else if (AirsideAdelaidePavement.ContainsApron(x, z))
                use = RoadSurfaceUse.PaintOnly;
            else
                use = RoadSurfaceUse.Asphalt;
            RuleCache[key] = use;
            return use;
        }

        private static void EnsureBounds()
        {
            if (_boundsReady)
                return;
            _minX = _minZ = float.MaxValue;
            _maxX = _maxZ = float.MinValue;
            void Grow(float[] xz)
            {
                for (var i = 0; i + 1 < xz.Length; i += 2)
                {
                    _minX = Mathf.Min(_minX, xz[i]);
                    _maxX = Mathf.Max(_maxX, xz[i]);
                    _minZ = Mathf.Min(_minZ, xz[i + 1]);
                    _maxZ = Mathf.Max(_maxZ, xz[i + 1]);
                }
            }

            foreach (var taxiway in AdelaideLayout.Taxiways)
                Grow(taxiway.Xz);
            foreach (var apron in AdelaideLayout.Aprons)
                Grow(apron.Xz);
            // The main runway and the cross runway, plus room for shoulders.
            _minX = Mathf.Min(_minX, -AirsideAdelaidePavement.MainHalfLength);
            _maxX = Mathf.Max(_maxX, AirsideAdelaidePavement.MainHalfLength);
            _minX -= 40f;
            _maxX += 40f;
            _minZ -= 40f;
            _maxZ += 40f;
            _boundsReady = true;
        }
    }
}

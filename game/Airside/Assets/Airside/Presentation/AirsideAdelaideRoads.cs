using System.Collections.Generic;
using Airside.Simulation;
using UnityEngine;
using UnityEngine.Rendering;

namespace Airside.Presentation
{
    /// <summary>
    /// Arterial road ribbons around YPAD from <see cref="AdelaideLandCover.Roads"/> —
    /// motorway / trunk / primary / secondary centreline strips in the Airside palette.
    /// Presentation only; fails soft when the shader or road data is missing.
    /// </summary>
    public static class AirsideAdelaideRoads
    {
        public const string ObjectName = "Adelaide Roads";
        public const string ShaderName = "Airside/Surroundings";

        private const int MaxRoads = 720;
        private const float MinWidthMetres = 9f;
        private const float LandsideMinWidthMetres = 6f;
        private const float YOffsetMetres = 0.08f;

        // A flat asphalt ribbon read as a runway-grey band cut straight out of the satellite
        // photo it sits on — no lane discipline, nothing to say "road" up close the way the
        // strip's own edge/centreline paint says "runway". A dashed centreline is the single
        // cheapest, most legible cue a real arterial road has that this one lacked; drawn only
        // on roads wide enough to already need one (the same threshold that gates the road
        // ribbon itself), not on every landside laneway.
        private const float LaneMarkingMinWidthMetres = MinWidthMetres;
        private const float LaneMarkingWidthMetres = 0.32f;
        private const float LaneMarkingDashLength = 5f;
        private const float LaneMarkingGapLength = 5f;
        private const float LaneMarkingYOffsetMetres = YOffsetMetres + 0.01f;

        private static readonly Color Asphalt = new(0.28f, 0.30f, 0.31f);
        // White, not yellow: Australian roads mark lane dividers white (yellow here is an
        // edge-line/no-stopping colour, not a centreline one).
        private static readonly Color LaneMarkingPaint = new(0.88f, 0.88f, 0.85f);

        public static bool TryBuild(Transform root, float pavementWorldY, System.Func<float, float, float> groundHeight = null)
        {
            try
            {
                var shader = Shader.Find(ShaderName);
                if (shader == null || AdelaideLandCover.Roads == null || AdelaideLandCover.Roads.Length < 3)
                    return false;

                var mesh = BuildMesh(pavementWorldY, groundHeight);
                if (mesh == null || mesh.vertexCount < 3)
                    return false;

                var go = new GameObject(ObjectName);
                go.transform.SetParent(root, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = new Material(shader)
                {
                    name = "mat_adelaide_roads_v01",
                    enableInstancing = true
                };
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;

                var laneMesh = BuildLaneMarkingMesh(pavementWorldY, groundHeight);
                if (laneMesh != null && laneMesh.vertexCount >= 3)
                {
                    var lanes = new GameObject(ObjectName + " Lane Markings");
                    lanes.transform.SetParent(root, false);
                    lanes.AddComponent<MeshFilter>().sharedMesh = laneMesh;
                    var laneRenderer = lanes.AddComponent<MeshRenderer>();
                    laneRenderer.sharedMaterial =
                        AirsideMaterialLibrary.CreateShared(LaneMarkingPaint, AirsideMaterialLibrary.SurfaceKind.PaintedLine);
                    laneRenderer.shadowCastingMode = ShadowCastingMode.Off;
                    laneRenderer.receiveShadows = false;
                }

                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Airside] Roads failed to build: {e.Message}");
                return false;
            }
        }

        /// <param name="groundHeight">
        /// Surface height at a world x,z. Without it roads lie on one fixed plane, which put
        /// them metres above the beach and inland water and under parts of the airfield lip.
        /// </param>
        public static Mesh BuildMesh(float pavementWorldY, System.Func<float, float, float> groundHeight = null)
        {
            var roads = AdelaideLandCover.Roads;
            var vertices = new List<Vector3>(4096);
            var colors = new List<Color>(4096);
            var triangles = new List<int>(8192);
            var y = pavementWorldY + YOffsetMetres;
            var asphalt = Asphalt.linear;
            asphalt.a = 0f;

            var i = 0;
            var drawn = 0;
            while (i < roads.Length)
            {
                var width = roads[i++];
                if (width <= 0f)
                    break;
                if (i >= roads.Length)
                    break;
                var count = (int)roads[i++];
                if (count < 2 || i + count * 2 > roads.Length)
                    break;

                var landside = RoadTouchesLandside(roads, i, count);
                var minWidth = landside ? LandsideMinWidthMetres : MinWidthMetres;
                if (width >= minWidth && drawn < MaxRoads)
                {
                    var half = width * 0.5f;
                    var start = vertices.Count;
                    Vector3? prevLeft = null;
                    Vector3? prevRight = null;
                    for (var p = 0; p < count; p++)
                    {
                        var x = roads[i + p * 2];
                        var z = roads[i + p * 2 + 1];
                        // Skip the operational core — pavement owns the strips and
                        // aprons — but keep the T1 traffic-side notch (drop-off /
                        // Sir Richard Williams) so aerial landside is not a blank lawn.
                        if (AdelaideLandCover.InOperationalCore(x, z) && !AdelaideLandside.Contains(x, z))
                        {
                            prevLeft = prevRight = null;
                            continue;
                        }

                        float dx, dz;
                        if (p + 1 < count)
                        {
                            dx = roads[i + (p + 1) * 2] - x;
                            dz = roads[i + (p + 1) * 2 + 1] - z;
                        }
                        else
                        {
                            dx = x - roads[i + (p - 1) * 2];
                            dz = z - roads[i + (p - 1) * 2 + 1];
                        }
                        var len = Mathf.Sqrt(dx * dx + dz * dz);
                        if (len < 1e-3f)
                        {
                            dx = 1f;
                            dz = 0f;
                            len = 1f;
                        }
                        dx /= len;
                        dz /= len;
                        // Perpendicular in XZ.
                        var px = -dz * half;
                        var pz = dx * half;
                        var ly = groundHeight != null
                            ? Mathf.Max(groundHeight(x + px, z + pz), groundHeight(x, z)) + YOffsetMetres
                            : y;
                        var ry = groundHeight != null
                            ? Mathf.Max(groundHeight(x - px, z - pz), groundHeight(x, z)) + YOffsetMetres
                            : y;
                        var left = new Vector3(x + px, ly, z + pz);
                        var right = new Vector3(x - px, ry, z - pz);
                        if (prevLeft.HasValue)
                        {
                            var a = vertices.Count;
                            vertices.Add(prevLeft.Value);
                            vertices.Add(prevRight.Value);
                            vertices.Add(left);
                            vertices.Add(right);
                            colors.Add(asphalt);
                            colors.Add(asphalt);
                            colors.Add(asphalt);
                            colors.Add(asphalt);
                            triangles.Add(a);
                            triangles.Add(a + 2);
                            triangles.Add(a + 1);
                            triangles.Add(a + 1);
                            triangles.Add(a + 2);
                            triangles.Add(a + 3);
                        }
                        prevLeft = left;
                        prevRight = right;
                    }
                    if (vertices.Count > start)
                        drawn++;
                }

                i += count * 2;
            }

            if (vertices.Count < 3)
                return null;

            var mesh = new Mesh
            {
                name = "Adelaide Roads",
                indexFormat = IndexFormat.UInt32
            };
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// A thin dashed centreline down each road wide enough to get one, so the flat
        /// asphalt ribbon reads as a marked road rather than a grey band cut out of the
        /// satellite photo underneath it. Same source data and operational-core skip as
        /// <see cref="BuildMesh"/>; a separate mesh/material because painted-line colour must
        /// not be diluted by the road ribbon's satellite-image blend the way the asphalt
        /// colour deliberately is.
        /// </summary>
        public static Mesh BuildLaneMarkingMesh(float pavementWorldY, System.Func<float, float, float> groundHeight = null)
        {
            var roads = AdelaideLandCover.Roads;
            var vertices = new List<Vector3>(4096);
            var triangles = new List<int>(8192);
            var y = pavementWorldY + LaneMarkingYOffsetMetres;
            var half = LaneMarkingWidthMetres * 0.5f;
            var period = LaneMarkingDashLength + LaneMarkingGapLength;

            float HeightAt(float x, float z) =>
                groundHeight != null ? groundHeight(x, z) + LaneMarkingYOffsetMetres : y;

            void EmitDash(Vector2 from, Vector2 to)
            {
                var dx = to.x - from.x;
                var dz = to.y - from.y;
                var len = Mathf.Sqrt(dx * dx + dz * dz);
                if (len < 1e-3f)
                    return;
                dx /= len;
                dz /= len;
                var px = -dz * half;
                var pz = dx * half;
                var a = vertices.Count;
                vertices.Add(new Vector3(from.x + px, HeightAt(from.x + px, from.y + pz), from.y + pz));
                vertices.Add(new Vector3(from.x - px, HeightAt(from.x - px, from.y - pz), from.y - pz));
                vertices.Add(new Vector3(to.x + px, HeightAt(to.x + px, to.y + pz), to.y + pz));
                vertices.Add(new Vector3(to.x - px, HeightAt(to.x - px, to.y - pz), to.y - pz));
                triangles.Add(a); triangles.Add(a + 2); triangles.Add(a + 1);
                triangles.Add(a + 1); triangles.Add(a + 2); triangles.Add(a + 3);
            }

            // Walks one unbroken run of points, laying dashes end to end so the on/off phase
            // stays continuous across the run's original OSM vertices instead of resetting
            // (and so looking inconsistent) at each one.
            void WalkRun(List<Vector2> run)
            {
                if (run.Count < 2)
                    return;
                var traveled = 0f;
                for (var p = 1; p < run.Count; p++)
                {
                    var from = run[p - 1];
                    var to = run[p];
                    var segment = Vector2.Distance(from, to);
                    if (segment < 1e-3f)
                        continue;
                    var walked = 0f;
                    while (walked < segment)
                    {
                        var phase = traveled % period;
                        var onRemaining = LaneMarkingDashLength - phase;
                        if (onRemaining > 0f)
                        {
                            var step = Mathf.Min(onRemaining, segment - walked);
                            EmitDash(Vector2.Lerp(from, to, walked / segment),
                                Vector2.Lerp(from, to, (walked + step) / segment));
                            walked += step;
                            traveled += step;
                        }
                        else
                        {
                            var step = Mathf.Min(period - phase, segment - walked);
                            walked += step;
                            traveled += step;
                        }
                    }
                }
            }

            var i = 0;
            var drawn = 0;
            while (i < roads.Length)
            {
                var width = roads[i++];
                if (width <= 0f)
                    break;
                if (i >= roads.Length)
                    break;
                var count = (int)roads[i++];
                if (count < 2 || i + count * 2 > roads.Length)
                    break;

                var landside = RoadTouchesLandside(roads, i, count);
                var minWidth = landside ? LandsideMinWidthMetres : LaneMarkingMinWidthMetres;
                if (width >= minWidth && drawn < MaxRoads)
                {
                    var run = new List<Vector2>(count);
                    for (var p = 0; p < count; p++)
                    {
                        var x = roads[i + p * 2];
                        var z = roads[i + p * 2 + 1];
                        if (AdelaideLandCover.InOperationalCore(x, z) && !AdelaideLandside.Contains(x, z))
                        {
                            WalkRun(run);
                            run.Clear();
                            continue;
                        }
                        run.Add(new Vector2(x, z));
                    }
                    WalkRun(run);
                    if (vertices.Count > 0)
                        drawn++;
                }

                i += count * 2;
            }

            if (vertices.Count < 3)
                return null;

            var mesh = new Mesh
            {
                name = "Adelaide Roads Lane Markings",
                indexFormat = IndexFormat.UInt32
            };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static bool RoadTouchesLandside(float[] roads, int pointStart, int count)
        {
            for (var p = 0; p < count; p++)
            {
                if (AdelaideLandside.Contains(roads[pointStart + p * 2], roads[pointStart + p * 2 + 1]))
                    return true;
            }

            return false;
        }
    }
}

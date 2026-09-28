using System;
using System.Collections.Generic;
using Airside.Simulation;
using UnityEngine;
using UnityEngine.Rendering;

namespace Airside.Presentation
{
    /// <summary>
    /// Published Adelaide lighting facts used by the true-scale field. Source:
    /// Airservices Australia ERSA FAC YPAD, effective 19 March 2026.
    /// </summary>
    public static class AdelaideAirfieldLighting
    {
        public const float MainRunwayEdgeSpacingMetres = 57f;
        public const float CrossRunwayEdgeSpacingMetres = 59f;
        public const float Runway23HialLengthMetres = 801f;
        public const float PapiSlopeDegrees = 3f;
        public const float Runway05PapiThresholdHeightFeet = 61f;
        public const float Runway23PapiThresholdHeightFeet = 59f;
        public const float CrossRunwayPapiThresholdHeightFeet = 51f;
        public const float TaxiCentrelineVisualSpacingMetres = 45f;

        public static int EvenStationCount(float lengthMetres, float nominalSpacingMetres) =>
            Math.Max(2, (int)Math.Round(lengthMetres / nominalSpacingMetres) + 1);

        public static float EvenStation(float halfLengthMetres, int index, int count)
        {
            if (count <= 1)
                return 0f;
            return -halfLengthMetres + 2f * halfLengthMetres * index / (count - 1f);
        }

        public static bool IsMainRunwayGuardPosition(float x, float z) =>
            Math.Abs(x) <= AirsideBareField.RunwayHalfLength + 25f && Math.Abs(z) <= 125f;
    }

    public sealed partial class AirsidePrototype
    {
        /// <summary>
        /// Real-metre 05/23 HIRL/MIRL and 12/30 MIRL. Every published fixture is an
        /// emissive lens; only a sparse subset casts a PointLight, keeping the overview
        /// readable without asking URP to shade the field with hundreds of lights.
        /// </summary>
        private static Light[] BuildYpadRunwayEdgeLights()
        {
            var lights = new List<Light>();
            var white = new Color(1f, 0.96f, 0.82f);
            var caution = new Color(1f, 0.67f, 0.10f);

            var mainCount = AdelaideAirfieldLighting.EvenStationCount(
                AirsideBareField.RunwayLengthMetres,
                AdelaideAirfieldLighting.MainRunwayEdgeSpacingMetres);
            for (var i = 0; i < mainCount; i++)
            {
                var x = AdelaideAirfieldLighting.EvenStation(AirsideBareField.RunwayHalfLength, i, mainCount);
                // The final 600 m caution zones read amber from the overhead game camera.
                // Real fittings are directional; this is the honest top-down equivalent.
                var edgeColour = Mathf.Abs(x) >= AirsideBareField.RunwayHalfLength - 600f ? caution : white;
                PlaceYpadLens($"Runway edge 05/23 N {i:00}", new Vector3(x, 0.22f, AirsideBareField.RunwayHalfWidth + 0.65f), edgeColour, 0.24f);
                PlaceYpadLens($"Runway edge 05/23 S {i:00}", new Vector3(x, 0.22f, -AirsideBareField.RunwayHalfWidth - 0.65f), edgeColour, 0.24f);
                if (i % 2 == 0 || i == mainCount - 1)
                {
                    lights.Add(CreateYpadPointLight($"Runway edge point 05/23 N {i:00}",
                        new Vector3(x, 0.35f, AirsideBareField.RunwayHalfWidth + 0.65f), edgeColour, 18f));
                    lights.Add(CreateYpadPointLight($"Runway edge point 05/23 S {i:00}",
                        new Vector3(x, 0.35f, -AirsideBareField.RunwayHalfWidth - 0.65f), edgeColour, 18f));
                }
            }

            var crossCount = AdelaideAirfieldLighting.EvenStationCount(
                AirsideAdelaidePavement.CrossLengthMetres,
                AdelaideAirfieldLighting.CrossRunwayEdgeSpacingMetres);
            for (var i = 0; i < crossCount; i++)
            {
                var along = AdelaideAirfieldLighting.EvenStation(AirsideAdelaidePavement.CrossHalfLength, i, crossCount);
                for (var side = -1; side <= 1; side += 2)
                {
                    var world = CrossRunwayWorld(along, side * (AirsideAdelaidePavement.CrossHalfWidth + 0.65f));
                    PlaceYpadLens($"Runway edge 12/30 {(side < 0 ? "L" : "R")} {i:00}",
                        world + Vector3.up * 0.22f, white, 0.24f);
                    if (i % 2 == 0 || i == crossCount - 1)
                        lights.Add(CreateYpadPointLight(
                            $"Runway edge point 12/30 {(side < 0 ? "L" : "R")} {i:00}",
                            world + Vector3.up * 0.35f, white, 18f));
                }
            }


            AddYpadTaxiCentrelineLights(lights);
            AddYpadRunwayGuardLights(lights);

            return lights.ToArray();
        }

        private static void AddYpadTaxiCentrelineLights(List<Light> lights)
        {
            var green = new Color(0.12f, 1f, 0.42f);
            var blue = new Color(0.18f, 0.42f, 1f);
            var occupied = new HashSet<long>();
            var fixture = 0;
            foreach (var taxiway in AdelaideLayout.Taxiways)
            {
                var path = taxiway.Xz;
                var distanceUntilFixture = 0f;
                for (var i = 0; i + 3 < path.Length; i += 2)
                {
                    var start = new Vector2(path[i], path[i + 1]);
                    var end = new Vector2(path[i + 2], path[i + 3]);
                    var segment = end - start;
                    var length = segment.magnitude;
                    if (length < 0.01f)
                        continue;

                    while (distanceUntilFixture <= length)
                    {
                        var point = start + segment * (distanceUntilFixture / length);
                        // OSM contains overlapping named fragments. A coarse spatial key
                        // keeps one physical fixture at each shared centreline location.
                        var gx = Mathf.RoundToInt(point.x / 10f);
                        var gz = Mathf.RoundToInt(point.y / 10f);
                        var key = ((long)gx << 32) ^ (uint)gz;
                        if (occupied.Add(key))
                        {
                            var position = new Vector3(point.x, 0.19f, point.y);
                            PlaceYpadLens($"Taxi CL green {fixture:000}", position, green, 0.25f);
                            if (TakesTaxiEdgeLights(point))
                            {
                                var across = new Vector2(-segment.y, segment.x) / length
                                             * (taxiway.Width * 0.5f + AirsideAdelaidePavement.TaxiSealedShoulderMetres + 0.8f);
                                PlaceYpadLens($"Taxi edge blue {fixture:000} L", new Vector3(point.x + across.x, 0.2f, point.y + across.y), blue, 0.22f);
                                PlaceYpadLens($"Taxi edge blue {fixture:000} R", new Vector3(point.x - across.x, 0.2f, point.y - across.y), blue, 0.22f);
                            }
                            if (fixture % 8 == 0)
                                lights.Add(CreateYpadPointLight($"Taxi CL point {fixture:000}",
                                    position + Vector3.up * 0.12f, green, 10f));
                            fixture++;
                        }
                        distanceUntilFixture += AdelaideAirfieldLighting.TaxiCentrelineVisualSpacingMetres;
                    }
                    distanceUntilFixture -= length;
                }
            }
        }

        /// <summary>
        /// Blue edge lights line the taxiways between aprons and runways (ADR 0124): not on an
        /// apron (stand lighting covers those) and not inside a runway strip, where they would
        /// clutter the runway edge and guard lights.
        /// </summary>
        private static bool TakesTaxiEdgeLights(Vector2 point)
        {
            if (Mathf.Abs(point.y) < AirsideBareField.RunwayHalfWidth + 45f
                && Mathf.Abs(point.x) < AirsideBareField.RunwayHalfLength + 60f)
                return false;
            var radians = AirsideAdelaidePavement.CrossYawRadians;
            var dx = point.x - AirsideAdelaidePavement.CrossCenterX;
            var dz = point.y - AirsideAdelaidePavement.CrossCenterZ;
            var alongCross = Mathf.Cos(radians) * dx - Mathf.Sin(radians) * dz;
            var acrossCross = Mathf.Sin(radians) * dx + Mathf.Cos(radians) * dz;
            if (Mathf.Abs(acrossCross) < AirsideAdelaidePavement.CrossHalfWidth + 45f
                && Mathf.Abs(alongCross) < AirsideAdelaidePavement.CrossHalfLength + 60f)
                return false;
            foreach (var apron in AdelaideLayout.Aprons)
                if (BuildingDetail.Contains(apron.Xz, point.x, point.y))
                    return false;
            return true;
        }

        private static void AddYpadRunwayGuardLights(List<Light> lights)
        {
            var yellow = new Color(1f, 0.72f, 0.08f);
            var hold = AdelaideLayout.HoldingPositions;
            for (var i = 0; i + 1 < hold.Length; i += 2)
            {
                var x = hold[i];
                var z = hold[i + 1];
                if (!AdelaideAirfieldLighting.IsMainRunwayGuardPosition(x, z))
                    continue;

                // Red stop bar across the taxiway at the holding position (ADR 0124).
                var (direction, width) = NearestTaxiwayAt(x, z);
                var across = new Vector3(-direction.z, 0f, direction.x);
                // Just short of the painted bars, on the side away from the runway (z = 0).
                var away = Vector3.Dot(direction, new Vector3(0f, 0f, Mathf.Sign(z))) >= 0f ? direction : -direction;
                var bar = Mathf.Max(2, Mathf.RoundToInt(width / 3f));
                for (var b = 0; b <= bar; b++)
                    PlaceYpadLens($"Stopbar {i / 2:00} {b:00}",
                        new Vector3(x, 0.2f, z) + across * ((b / (float)bar - 0.5f) * width) + away * 1.9f,
                        new Color(1f, 0.1f, 0.08f), 0.24f);

                for (var side = -1; side <= 1; side += 2)
                {
                    var position = new Vector3(x + side * 5f, 0.28f, z);
                    PlaceYpadLens($"Runway guard {i / 2:00} {(side < 0 ? "L" : "R")}",
                        position, yellow, 0.48f);
                    lights.Add(CreateYpadPointLight($"Runway guard point {i / 2:00} {(side < 0 ? "L" : "R")}",
                        position + Vector3.up * 0.15f, yellow, 12f));
                }
            }
        }

        /// <summary>
        /// A lit marker at every terminal gate and regional bay stop, plus a short blue
        /// lead-in trail along the final approach into each stand. Apron illumination
        /// before this was 7 uniform roof floods with no way to tell one parking position
        /// from another at night; real stands are individually marked this way so a gate
        /// reads as its own spot, not just part of a floodlit apron.
        /// </summary>
        private static Light[] BuildStandLighting()
        {
            var lights = new List<Light>();
            var marker = new Color(1f, 0.86f, 0.55f);
            var leadIn = new Color(0.25f, 0.55f, 1f);

            foreach (var gate in AdelaideGateAlignment.Gates)
            {
                var stop = new Vector3(gate.NoseX, 0.24f, gate.NoseZ);
                PlaceYpadLens($"Stand marker {gate.Id}", stop, marker, 0.5f);
                lights.Add(CreateYpadPointLight($"Stand marker point {gate.Id}", stop + Vector3.up * 0.3f, marker, 14f));
                AddStandLeadIn(lights, gate.Id, gate.TaxiIn, leadIn);
            }

            foreach (var bay in AdelaideLayout.Bays)
            {
                var stop = new Vector3(bay.StopX, 0.24f, bay.StopZ);
                PlaceYpadLens($"Stand marker {bay.Id}", stop, marker, 0.42f);
                lights.Add(CreateYpadPointLight($"Stand marker point {bay.Id}", stop + Vector3.up * 0.3f, marker, 12f));
                AddStandLeadIn(lights, bay.Id, bay.TaxiIn, leadIn);
            }

            return lights.ToArray();
        }

        /// <summary>
        /// Walks a stand's nose-first TaxiIn polyline backward from its stop (toward the
        /// holding point) placing a lens fixture every 8 m for a short ~32-40 m trail —
        /// the same distance-accumulator walk as <see cref="AddYpadTaxiCentrelineLights"/>,
        /// just reversed and capped by fixture count instead of running the full taxiway.
        /// </summary>
        private static void AddStandLeadIn(List<Light> lights, string standId, float[] path, Color colour)
        {
            if (path == null || path.Length < 4)
                return;

            const float spacingMetres = 8f;
            const int maxFixtures = 5;
            var distanceUntilFixture = spacingMetres;
            var fixture = 0;

            for (var i = path.Length - 2; i >= 2 && fixture < maxFixtures; i -= 2)
            {
                var from = new Vector2(path[i], path[i + 1]);
                var to = new Vector2(path[i - 2], path[i - 1]);
                var segment = to - from;
                var length = segment.magnitude;
                if (length < 0.01f)
                    continue;
                var direction = segment / length;

                while (distanceUntilFixture <= length && fixture < maxFixtures)
                {
                    var point = from + direction * distanceUntilFixture;
                    var position = new Vector3(point.x, 0.19f, point.y);
                    PlaceYpadLens($"Stand lead-in {standId} {fixture:00}", position, colour, 0.22f);
                    if (fixture % 2 == 1)
                        lights.Add(CreateYpadPointLight($"Stand lead-in point {standId} {fixture:00}",
                            position + Vector3.up * 0.12f, colour, 8f));
                    fixture++;
                    distanceUntilFixture += spacingMetres;
                }
                distanceUntilFixture -= length;
            }
        }

        /// <summary>
        /// Green thresholds, red runway ends, PAPI at both ends of both strips, and
        /// the published 801 m distance-coded CAT-I centreline for runway 23 only.
        /// No REIL is added: ERSA does not list RTIL/REIL for YPAD.
        /// </summary>
        private static Light[] BuildYpadThresholdPapiAndApproachLights()
        {
            var lights = new List<Light>();
            var green = new Color(0.20f, 1f, 0.45f);
            var red = new Color(1f, 0.16f, 0.12f);
            var white = new Color(1f, 0.96f, 0.84f);

            for (var end = -1; end <= 1; end += 2)
            {
                var thresholdX = end * AirsideBareField.RunwayHalfLength;
                for (var i = -3; i <= 3; i++)
                {
                    var z = i * 6f;
                    PlaceYpadLens($"Threshold light 05/23 {end} {i + 3}",
                        new Vector3(thresholdX - end * 1.2f, 0.24f, z), green, 0.42f);
                    PlaceYpadLens($"Runway end light 05/23 {end} {i + 3}",
                        new Vector3(thresholdX + end * 1.2f, 0.24f, z), red, 0.42f);
                    if (i is -3 or 0 or 3)
                        lights.Add(CreateYpadPointLight($"Threshold point 05/23 {end} {i + 3}",
                            new Vector3(thresholdX - end * 1.2f, 0.38f, z), green, 16f));
                }
            }

            // PAPI 05: left of an eastbound pilot (+Z). PAPI 23: left of a westbound pilot (-Z).
            AddPapiBar(lights, "PAPI 05", new Vector3(-1250f, 0.28f, 29f), Vector3.forward);
            AddPapiBar(lights, "PAPI 23", new Vector3(1250f, 0.28f, -29f), Vector3.back);

            // 12/30 PAPI at both ends in the cross-runway's local coordinates.
            AddCrossPapiBar(lights, "PAPI 12", -AirsideAdelaidePavement.CrossHalfLength + 300f, 29f);
            AddCrossPapiBar(lights, "PAPI 30", AirsideAdelaidePavement.CrossHalfLength - 300f, -29f);

            for (var end = -1; end <= 1; end += 2)
            {
                var localX = end * AirsideAdelaidePavement.CrossHalfLength;
                for (var i = -3; i <= 3; i++)
                {
                    var localZ = i * 6f;
                    var threshold = CrossRunwayWorld(localX - end * 1.2f, localZ);
                    var stop = CrossRunwayWorld(localX + end * 1.2f, localZ);
                    PlaceYpadLens($"Threshold light 12/30 {end} {i + 3}",
                        threshold + Vector3.up * 0.24f, green, 0.42f);
                    PlaceYpadLens($"Runway end light 12/30 {end} {i + 3}",
                        stop + Vector3.up * 0.24f, red, 0.42f);
                    if (i is -3 or 0 or 3)
                        lights.Add(CreateYpadPointLight($"Threshold point 12/30 {end} {i + 3}",
                            threshold + Vector3.up * 0.38f, green, 16f));
                }

                for (var s = 1; s <= 10; s++)
                {
                    var along = localX + end * s * 30f;
                    var world = CrossRunwayWorld(along, 0f);
                    PlaceYpadLens($"ALS 12/30 {end} {s:00}", world + Vector3.up * 0.25f, white, 0.36f);
                    if (s % 3 == 0)
                        lights.Add(CreateYpadPointLight($"ALS 12/30 point {end} {s:00}",
                            world + Vector3.up * 0.42f, white, 16f));
                }
            }

            // Runway 23 HIAL-CAT I: approach lies beyond the east threshold. Keep the
            // full published 801 m length and a 30 m visual station rhythm.
            const float hialStep = 30f;
            var hialStart = AirsideBareField.RunwayHalfLength + hialStep;
            var hialEnd = AirsideBareField.RunwayHalfLength + AdelaideAirfieldLighting.Runway23HialLengthMetres;
            var station = 0;
            for (var x = hialStart; x < hialEnd; x += hialStep, station++)
            {
                PlaceYpadLens($"ALS 23 centre {station:00}", new Vector3(x, 0.25f, 0f), white, 0.40f);
                if (station % 3 == 0)
                    lights.Add(CreateYpadPointLight($"HIAL 23 point {station:00}",
                        new Vector3(x, 0.42f, 0f), white, 20f));
            }
            PlaceYpadLens("ALS 23 centre end", new Vector3(hialEnd, 0.25f, 0f), white, 0.40f);

            // The main 300 m crossbar makes distance coding legible from the approach.
            var crossbarX = AirsideBareField.RunwayHalfLength + 300f;
            for (var z = -15f; z <= 15f; z += 3f)
                PlaceYpadLens($"ALS 23 crossbar {z:00}", new Vector3(crossbarX, 0.25f, z), white, 0.38f);

            return lights.ToArray();
        }

        private static void AddPapiBar(List<Light> lights, string name, Vector3 origin, Vector3 lateral)
        {
            for (var i = 0; i < 4; i++)
            {
                var colour = i < 2 ? new Color(1f, 0.16f, 0.12f) : new Color(1f, 0.96f, 0.82f);
                var position = origin + lateral * (i * 3f);
                PlaceYpadLens($"{name} unit {i + 1}", position, colour, 0.55f);
                lights.Add(CreateYpadPointLight($"{name} point {i + 1}", position + Vector3.up * 0.18f,
                    colour, 12f));
            }
        }

        private static void AddCrossPapiBar(List<Light> lights, string name, float localX, float localZ)
        {
            for (var i = 0; i < 4; i++)
            {
                var colour = i < 2 ? new Color(1f, 0.16f, 0.12f) : new Color(1f, 0.96f, 0.82f);
                var direction = Mathf.Sign(localZ);
                var position = CrossRunwayWorld(localX, localZ + direction * i * 3f) + Vector3.up * 0.28f;
                PlaceYpadLens($"{name} unit {i + 1}", position, colour, 0.55f);
                lights.Add(CreateYpadPointLight($"{name} point {i + 1}", position + Vector3.up * 0.18f,
                    colour, 12f));
            }
        }

        private static Vector3 CrossRunwayWorld(float localX, float localZ)
        {
            var radians = AirsideAdelaidePavement.CrossYawRadians;
            var sin = Mathf.Sin(radians);
            var cos = Mathf.Cos(radians);
            return new Vector3(
                AirsideAdelaidePavement.CrossCenterX + cos * localX + sin * localZ,
                AirsideBareField.RunwayCenterY - AirsideAdelaidePavement.CrossRunwayDropMetres,
                AirsideAdelaidePavement.CrossCenterZ - sin * localX + cos * localZ);
        }

        // ---- Merged fixtures (ADR 0124) ----------------------------------------------------
        // Every lens used to be its own cube GameObject (hundreds of them). Lenses now append a
        // domed fixture into one mesh per colour group, the metal bases into one shared mesh,
        // and a soft additive halo per lens into one halo mesh per group. The night pass tints
        // a dozen group renderers instead of every lens.

        private sealed class LensGroup
        {
            public string Name;
            public Color Colour;
            public LensDayResponse Response;
            public readonly List<Vector3> Vertices = new();
            public readonly List<Vector3> Normals = new();
            public readonly List<int> Triangles = new();
            public readonly List<Vector3> HaloVertices = new();
            public readonly List<Vector2> HaloUvs = new();
            public readonly List<int> HaloTriangles = new();
            public readonly List<Vector3> PointVertices = new();
            public readonly List<Vector2> PointCorners = new();
            public readonly List<Vector2> PointSizePhase = new();
            public readonly List<int> PointTriangles = new();
            public Renderer Lens;
            public Renderer Halo;
            public Renderer Points;
            public Material PointMaterial;
            public Renderer Reflection;
            public Material ReflectionMaterial;
            public float HaloGain = 0.55f;
        }

        private static readonly Dictionary<string, LensGroup> LensGroups = new();
        private static readonly List<LensGroup> BuiltLensGroups = new();
        private static readonly List<Vector3> FixtureBaseVertices = new();
        private static readonly List<Vector3> FixtureBaseNormals = new();
        private static readonly List<int> FixtureBaseTriangles = new();
        private static AirfieldFixture.Geometry _fixtureBase;
        private static AirfieldFixture.Geometry _fixtureLens;
        private static Material _haloMaterial;
        public const string LensGroupPrefix = "Airfield lenses ";
        public const string HaloGroupPrefix = "Airfield halos ";

        private static void PlaceYpadLens(string name, Vector3 position, Color colour, float diameter = 0.34f)
        {
            var response = AirfieldFixture.ResponseFor(name);
            var key = $"{response} {ColorUtility.ToHtmlStringRGB(colour)}";
            if (!LensGroups.TryGetValue(key, out var group))
            {
                group = new LensGroup { Name = key, Colour = colour, Response = response };
                LensGroups[key] = group;
            }

            _fixtureBase ??= AirfieldFixture.Base();
            _fixtureLens ??= AirfieldFixture.Lens();
            // Same footprint as the old lens cube: `diameter` across, 0.16 m tall, centred on y.
            var scale = new Vector3(diameter, 0.16f, diameter);
            var origin = position - Vector3.up * 0.08f;
            AppendFixture(_fixtureLens, origin, scale, group.Vertices, group.Normals, group.Triangles);
            AppendFixture(_fixtureBase, origin, scale, FixtureBaseVertices, FixtureBaseNormals, FixtureBaseTriangles);
            AddHalo(group, position, AirfieldFixture.HaloSize(diameter, response));
            AddLightPoint(group, position + Vector3.up * 0.06f, AirfieldFixture.PointWorldSize(diameter),
                AirfieldFixture.FlashPhase(name));
        }

        /// <summary>Four vertices at the lens centre; Airside/AirfieldLightPoint opens them towards the camera (ADR 0167).</summary>
        private static void AddLightPoint(LensGroup group, Vector3 centre, float worldSize, float flashPhase)
        {
            var first = group.PointVertices.Count;
            for (var i = 0; i < 4; i++)
            {
                group.PointVertices.Add(centre);
                group.PointSizePhase.Add(new Vector2(worldSize, flashPhase));
            }

            group.PointCorners.Add(new Vector2(-1f, -1f));
            group.PointCorners.Add(new Vector2(1f, -1f));
            group.PointCorners.Add(new Vector2(1f, 1f));
            group.PointCorners.Add(new Vector2(-1f, 1f));
            group.PointTriangles.AddRange(new[] { first, first + 2, first + 1, first, first + 3, first + 2 });
        }

        private static void AppendFixture(AirfieldFixture.Geometry g, Vector3 origin, Vector3 scale,
            List<Vector3> vertices, List<Vector3> normals, List<int> triangles)
        {
            var first = vertices.Count;
            for (var i = 0; i < g.VertexCount; i++)
            {
                vertices.Add(origin + Vector3.Scale(new Vector3(g.Positions[i * 3], g.Positions[i * 3 + 1], g.Positions[i * 3 + 2]), scale));
                normals.Add(new Vector3(g.Normals[i * 3] / scale.x, g.Normals[i * 3 + 1] / scale.y,
                    g.Normals[i * 3 + 2] / scale.z).normalized);
            }

            foreach (var t in g.Triangles)
                triangles.Add(first + t);
        }

        /// <summary>A ground pool plus two crossed upright cards: reads as glow from the overview and at eye level.</summary>
        private static void AddHalo(LensGroup group, Vector3 centre, float size, Vector3? cardCentre = null,
            float cardSize = 0f)
        {
            void Quad(Vector3 c, Vector3 u, Vector3 v)
            {
                var first = group.HaloVertices.Count;
                group.HaloVertices.Add(c - u - v);
                group.HaloVertices.Add(c + u - v);
                group.HaloVertices.Add(c + u + v);
                group.HaloVertices.Add(c - u + v);
                group.HaloUvs.Add(new Vector2(0f, 0f));
                group.HaloUvs.Add(new Vector2(1f, 0f));
                group.HaloUvs.Add(new Vector2(1f, 1f));
                group.HaloUvs.Add(new Vector2(0f, 1f));
                group.HaloTriangles.AddRange(new[] { first, first + 2, first + 1, first, first + 3, first + 2 });
            }

            var half = size * 0.5f;
            Quad(centre + Vector3.up * 0.03f, Vector3.right * half, Vector3.forward * half);
            var card = cardCentre.HasValue ? cardSize * 0.5f : half * 0.55f;
            var at = cardCentre ?? centre + Vector3.up * card * 0.6f;
            Quad(at, Vector3.right * card, Vector3.up * card);
            Quad(at, Vector3.forward * card, Vector3.up * card);
        }

        /// <summary>
        /// A streetlight's night glow (ADR 0124): a warm pool on the ground under the head and a
        /// soft card round the lamp. No real light — the landside keeps its point-light budget.
        /// </summary>
        private static void AddStreetlightGlow(Vector3 lamp, float groundY)
        {
            const string key = "Streetlights";
            if (!LensGroups.TryGetValue(key, out var group))
            {
                group = new LensGroup
                {
                    Name = key, Colour = new Color(1f, 0.82f, 0.55f), Response = LensDayResponse.Guidance, HaloGain = 0.32f
                };
                LensGroups[key] = group;
            }

            AddHalo(group, new Vector3(lamp.x, groundY, lamp.z), 13f, cardCentre: lamp, cardSize: 1.8f);
        }

        /// <summary>Builds the merged fixture, lens and halo meshes queued by <see cref="PlaceYpadLens"/>.</summary>
        private static void FlushYpadLenses()
        {
            // Statics outlive a play session when domain reload is off; drop destroyed groups.
            BuiltLensGroups.RemoveAll(g => g.Lens == null && g.Halo == null);
            if (LensGroups.Count == 0)
                return;
            var root = new GameObject("Airfield light fixtures").transform;
            if (_airfieldRoot != null)
                root.SetParent(_airfieldRoot, false);

            SpawnFixtureMesh(root, "Airfield light fixture bases", FixtureBaseVertices, FixtureBaseNormals,
                FixtureBaseTriangles, AirsideMaterialLibrary.CreateShared(new Color(0.42f, 0.43f, 0.44f),
                    AirsideMaterialLibrary.SurfaceKind.Metal, null, Vector2.one, useTextures: false));
            foreach (var group in LensGroups.Values)
            {
                group.Lens = SpawnFixtureMesh(root, LensGroupPrefix + group.Name, group.Vertices, group.Normals,
                    group.Triangles, AirsideMaterialLibrary.CreateShared(group.Colour,
                        AirsideMaterialLibrary.SurfaceKind.Default, null, Vector2.one, useTextures: false));
                group.Halo = SpawnHaloMesh(root, HaloGroupPrefix + group.Name, group);
                SpawnLightPoints(root, group);
                BuiltLensGroups.Add(group);
            }

            LensGroups.Clear();
            FixtureBaseVertices.Clear();
            FixtureBaseNormals.Clear();
            FixtureBaseTriangles.Clear();
        }

        private static Renderer SpawnFixtureMesh(Transform root, string name, List<Vector3> vertices, List<Vector3> normals,
            List<int> triangles, Material material)
        {
            if (triangles.Count == 0)
                return null;
            var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            AirsideMeshUtil.UploadStatic(mesh);
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            AirsideSceneIndex.Remember(go);
            return renderer;
        }

        private static Renderer SpawnHaloMesh(Transform root, string name, LensGroup group)
        {
            var material = HaloMaterial();
            if (material == null || group.HaloTriangles.Count == 0)
                return null;
            var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(group.HaloVertices);
            mesh.SetUVs(0, group.HaloUvs);
            mesh.SetTriangles(group.HaloTriangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            AirsideMeshUtil.UploadStatic(mesh);
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.enabled = false;
            AirsideSceneIndex.Remember(go);
            return renderer;
        }

        public const string PointGroupPrefix = "Airfield light points ";
        private static Shader _lightPointShader;

        private static void SpawnLightPoints(Transform root, LensGroup group)
        {
            if (group.PointTriangles.Count == 0)
                return;
            _lightPointShader ??= Shader.Find("Airside/AirfieldLightPoint");
            if (_lightPointShader == null)
                return;
            var mesh = new Mesh { name = PointGroupPrefix + group.Name, indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(group.PointVertices);
            mesh.SetUVs(0, group.PointCorners);
            mesh.SetUVs(1, group.PointSizePhase);
            mesh.SetTriangles(group.PointTriangles, 0);
            mesh.RecalculateBounds();
            // Every vertex sits at a lens centre; the quads open in the shader, so pad the bounds
            // or a group along one runway edge would be culled while its points are on screen.
            var bounds = mesh.bounds;
            bounds.Expand(new Vector3(60f, 60f, 60f));
            mesh.bounds = bounds;
            AirsideMeshUtil.UploadStatic(mesh);
            var go = new GameObject(PointGroupPrefix + group.Name);
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            var material = new Material(_lightPointShader) { name = "mat_airfield_light_point " + group.Name };
            material.SetFloat("_MinPixels", AirfieldFixture.PointMinPixels(group.Response));
            material.SetFloat("_HalfMetres", AirfieldFixture.PointHalfBrightnessMetres);
            material.SetFloat("_DistanceFloor", AirfieldFixture.PointDistanceFloor);
            material.SetFloat("_HazeExponent", AirfieldFixture.PointHazeExponent);
            material.SetFloat("_FlashHz", AirfieldFixture.GuardFlashHz);
            material.SetColor("_BaseColor", Color.black);
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.enabled = false;
            AirsideSceneIndex.Remember(go);
            group.Points = renderer;
            group.PointMaterial = material;

            // ADR 0169: the same points, drawn as streaks on wet pavement. Off until it rains at night.
            var wetGo = new GameObject(ReflectionGroupPrefix + group.Name);
            wetGo.transform.SetParent(root, false);
            wetGo.AddComponent<MeshFilter>().sharedMesh = mesh;
            var wetRenderer = wetGo.AddComponent<MeshRenderer>();
            var wetMaterial = new Material(material) { name = "mat_airfield_light_reflection " + group.Name };
            wetMaterial.SetFloat("_Reflect", 1f);
            wetMaterial.SetFloat("_ReflectStretch", AirfieldFixture.ReflectionStretch);
            wetMaterial.renderQueue = material.renderQueue - 1;
            wetRenderer.sharedMaterial = wetMaterial;
            wetRenderer.shadowCastingMode = ShadowCastingMode.Off;
            wetRenderer.receiveShadows = false;
            wetRenderer.enabled = false;
            AirsideSceneIndex.Remember(wetGo);
            group.Reflection = wetRenderer;
            group.ReflectionMaterial = wetMaterial;
        }

        public const string ReflectionGroupPrefix = "Airfield light reflections ";

        /// <summary>Rain wetness last applied to the lens groups (ADR 0169).</summary>
        private static float _lensWetness;

        /// <summary>Called when the weather's wetness moves: refreshes the wet-runway reflections.</summary>
        private static void SetLensWetness(float wetness, float daylight)
        {
            if (Mathf.Approximately(_lensWetness, wetness))
                return;
            _lensWetness = wetness;
            UpdateLensGroups(daylight);
        }

        /// <summary>URP Unlit, additive, depth-tested, no depth write, with a generated soft radial falloff.</summary>
        private static Material HaloMaterial()
        {
            if (_haloMaterial != null)
                return _haloMaterial;
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                return null;
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "airside_halo_falloff", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear
            };
            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var dx = (x + 0.5f) / size * 2f - 1f;
                var dy = (y + 0.5f) / size * 2f - 1f;
                var r = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
                // Bright core, long soft tail, exactly zero at the card edge.
                var v = Mathf.Pow(1f - r, 2.6f) * 0.85f + Mathf.Pow(1f - r, 12f) * 0.15f;
                var b = (byte)Mathf.RoundToInt(Mathf.Clamp01(v) * 255f);
                pixels[y * size + x] = new Color32(b, b, b, b);
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            var material = new Material(shader) { name = "mat_airfield_halo_additive" };
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", Color.black);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 2f);
            material.SetInt("_SrcBlend", (int)BlendMode.One);
            material.SetInt("_DstBlend", (int)BlendMode.One);
            material.SetInt("_ZWrite", 0);
            material.SetInt("_Cull", (int)CullMode.Off);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            _haloMaterial = material;
            return material;
        }

        /// <summary>
        /// Day/night for the merged lens groups (ADR 0124): every group keeps its own colour —
        /// amber caution edges stay amber — and dims by day by its response; halos fade in at dusk.
        /// </summary>
        private static void UpdateLensGroups(float daylight)
        {
            var night = 1f - daylight;
            var halo = AirfieldFixture.HaloStrength(night);
            foreach (var group in BuiltLensGroups)
            {
                if (group.Lens != null)
                {
                    var glow = AirfieldFixture.LensEmission(group.Response, night);
                    var body = Color.Lerp(group.Colour * 0.5f, group.Colour, Mathf.Clamp01(glow));
                    body.a = 1f;
                    SetRendererColor(group.Lens, body, group.Colour * glow);
                }

                if (group.Points != null)
                {
                    var strength = AirfieldFixture.PointStrength(group.Response, night);
                    group.Points.enabled = strength > 0.01f;
                    if (group.PointMaterial != null)
                        group.PointMaterial.SetColor(BaseColorId, group.Colour * strength);
                    if (group.Reflection != null)
                    {
                        var wet = strength * AirfieldFixture.ReflectionGain
                                  * AirfieldFixture.ReflectionStrength(_lensWetness, night);
                        group.Reflection.enabled = wet > 0.01f;
                        if (group.ReflectionMaterial != null)
                            group.ReflectionMaterial.SetColor(BaseColorId, group.Colour * wet);
                    }
                }

                if (group.Halo == null)
                    continue;
                group.Halo.enabled = halo > 0f;
                if (halo > 0f)
                    SetRendererColor(group.Halo, group.Colour * (group.HaloGain * halo));
            }
        }

        private static Light CreateYpadPointLight(
            string name, Vector3 position, Color colour, float range)
        {
            var go = new GameObject(name);
            go.transform.position = position;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = colour;
            light.range = range;
            light.intensity = 0.02f;
            light.shadows = LightShadows.None;
            AirsideSceneIndex.Remember(go);
            return light;
        }
    }
}

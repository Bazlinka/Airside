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
                if (i % 4 == 0 || i == mainCount - 1)
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
                    if (i % 4 == 0 || i == crossCount - 1)
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
                            if (fixture % 18 == 0)
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

            foreach (var gate in AdelaideLayout.TerminalGates)
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

        private static void PlaceYpadLens(string name, Vector3 position, Color colour, float diameter = 0.34f)
        {
            var lens = CreateBlock(name, position, new Vector3(diameter, 0.16f, diameter), colour);
            var renderer = lens.GetComponent<Renderer>();
            if (renderer == null)
                return;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            // Restrained emission preserves individual fixtures; a stronger value blooms
            // neighbouring stations into the continuous neon rails seen in the old build.
            SetRendererColor(renderer, colour, colour * 1.25f);
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

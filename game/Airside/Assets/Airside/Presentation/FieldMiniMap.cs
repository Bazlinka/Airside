using System;
using System.Collections.Generic;
using Airside.Simulation;
using UnityEngine;

namespace Airside.Presentation
{
    /// <summary>
    /// The Adelaide airfield mini-map: the OSM layout drawn once into a small texture, plus the
    /// maths that places world points on it and turns clicks back into ground positions.
    /// Runway frame: x along 05/23 (towards 23 positive), z towards the terminal. North-west,
    /// the terminal side, is up on the map. No scene access, so EditMode tests cover it.
    /// </summary>
    public static class FieldMiniMap
    {
        public const float PanelWidth = 300f;
        public const float PanelHeight = 164f;
        public const float HeaderHeight = 20f;
        public const float PaddingMetres = 90f;
        public const float DotHitRadius = 10f;

        /// <summary>Texture pixels per GUI point, so the baked map stays crisp on Retina.</summary>
        public const int TextureScale = 2;

        public static readonly Color32 Grass = new(46, 64, 54, 235);
        public static readonly Color32 Apron = new(112, 118, 118, 255);
        public static readonly Color32 Taxiway = new(140, 146, 145, 255);
        public static readonly Color32 Runway = new(214, 218, 212, 255);
        public static readonly Color32 Building = new(200, 178, 134, 255);

        private static float _minX, _maxX, _minZ, _maxZ;
        private static bool _boundsReady;

        /// <summary>World-space box (x, z) that holds every runway, taxiway, apron and terminal, padded.</summary>
        public static void WorldBounds(out float minX, out float maxX, out float minZ, out float maxZ)
        {
            if (!_boundsReady)
            {
                _minX = _minZ = float.MaxValue;
                _maxX = _maxZ = float.MinValue;
                foreach (var line in RunwayCentrelines())
                    Grow(line);
                foreach (var taxiway in AdelaideLayout.Taxiways)
                    Grow(taxiway.Xz);
                foreach (var apron in AdelaideLayout.Aprons)
                    Grow(apron.Xz);
                foreach (var terminal in AdelaideLayout.Terminals)
                    Grow(terminal.Xz);
                _minX -= PaddingMetres;
                _maxX += PaddingMetres;
                _minZ -= PaddingMetres;
                _maxZ += PaddingMetres;
                _boundsReady = true;
            }

            minX = _minX;
            maxX = _maxX;
            minZ = _minZ;
            maxZ = _maxZ;
        }

        /// <summary>The largest centred rect inside <paramref name="area"/> with the field's aspect ratio.</summary>
        public static Rect FitMap(Rect area)
        {
            WorldBounds(out var minX, out var maxX, out var minZ, out var maxZ);
            var aspect = (maxX - minX) / (maxZ - minZ);
            var width = area.width;
            var height = width / aspect;
            if (height > area.height)
            {
                height = area.height;
                width = height * aspect;
            }

            return new Rect(area.x + (area.width - width) * 0.5f, area.y + (area.height - height) * 0.5f, width, height);
        }

        /// <summary>World x,z to a point in <paramref name="map"/> (GUI space, y down).</summary>
        public static Vector2 WorldToMap(Rect map, float x, float z)
        {
            WorldBounds(out var minX, out var maxX, out var minZ, out var maxZ);
            return new Vector2(
                map.x + (x - minX) / (maxX - minX) * map.width,
                map.yMax - (z - minZ) / (maxZ - minZ) * map.height);
        }

        /// <summary>A point in <paramref name="map"/> back to world x,z, clamped to the field.</summary>
        public static Vector2 MapToWorld(Rect map, Vector2 point)
        {
            WorldBounds(out var minX, out var maxX, out var minZ, out var maxZ);
            var u = Mathf.Clamp01((point.x - map.x) / Mathf.Max(1f, map.width));
            var v = Mathf.Clamp01((map.yMax - point.y) / Mathf.Max(1f, map.height));
            return new Vector2(Mathf.Lerp(minX, maxX, u), Mathf.Lerp(minZ, maxZ, v));
        }

        /// <summary>
        /// The mini-map panel: bottom-left, clear of the control bar, speed readout, toast and
        /// the left column. Zero-sized when the window has no room for it.
        /// </summary>
        public static Rect PanelFor(HudLayout hud, AirlineHudLayout airline)
        {
            var margin = AirlineHudLayout.Margin;
            var width = Mathf.Min(PanelWidth, hud.Viewport.x - margin * 2f);
            var rect = new Rect(margin, hud.Viewport.y - margin - PanelHeight, width, PanelHeight);
            var leftColumnBottom = Mathf.Max(airline.Clock.yMax, airline.Guide.yMax);
            if (width < PanelWidth * 0.8f
                || rect.y < leftColumnBottom + margin
                || rect.Overlaps(hud.ControlBar)
                || rect.Overlaps(hud.SpeedReadout)
                || rect.Overlaps(airline.Toast)
                || rect.Overlaps(airline.FleetArea))
                return new Rect(margin, hud.Viewport.y - margin, 0f, 0f);
            return rect;
        }

        /// <summary>
        /// Where a camera ray meets the ground plane (y = 0) as x,z. A ray at or above the
        /// horizon is cut off <paramref name="farMetres"/> out along its flat direction.
        /// </summary>
        public static Vector2 GroundPoint(Vector3 origin, Vector3 direction, float farMetres)
        {
            if (direction.y < -0.0001f)
            {
                var distance = -origin.y / direction.y;
                if (distance >= 0f && distance <= farMetres)
                {
                    var hit = origin + direction * distance;
                    return new Vector2(hit.x, hit.z);
                }
            }

            var flat = new Vector2(direction.x, direction.z);
            flat = flat.sqrMagnitude > 0.0001f ? flat.normalized : Vector2.zero;
            return new Vector2(origin.x, origin.z) + flat * farMetres;
        }

        /// <summary>Index of the dot nearest <paramref name="click"/> within <paramref name="radius"/>, or -1.</summary>
        public static int NearestDot(IReadOnlyList<Vector2> dots, Vector2 click, float radius = DotHitRadius)
        {
            var best = -1;
            var bestSq = radius * radius;
            for (var i = 0; i < dots.Count; i++)
            {
                var sq = (dots[i] - click).sqrMagnitude;
                if (sq <= bestSq)
                {
                    bestSq = sq;
                    best = i;
                }
            }

            return best;
        }

        /// <summary>
        /// Paint the airfield into a <paramref name="width"/> × <paramref name="height"/> pixel
        /// buffer (row 0 at the bottom, as Texture2D.SetPixels32 expects): grass, aprons,
        /// taxiways, runways, then buildings.
        /// </summary>
        public static Color32[] Bake(int width, int height)
        {
            var pixels = new Color32[width * height];
            for (var i = 0; i < pixels.Length; i++)
                pixels[i] = Grass;

            // Pixel space: x right, y up, so the buffer needs no flip.
            var map = new Rect(0f, 0f, width, height);
            WorldBounds(out var minX, out var maxX, out _, out _);
            var pixelsPerMetre = width / (maxX - minX);

            foreach (var apron in AdelaideLayout.Aprons)
                FillPolygon(pixels, width, height, ToPixels(map, apron.Xz), Apron);
            foreach (var taxiway in AdelaideLayout.Taxiways)
                StrokePolyline(pixels, width, height, ToPixels(map, taxiway.Xz),
                    Mathf.Max(0.6f, taxiway.Width * 0.5f * pixelsPerMetre), Taxiway);
            foreach (var line in RunwayCentrelines())
                StrokePolyline(pixels, width, height, ToPixels(map, line),
                    Mathf.Max(1.2f, AirsideAdelaidePavement.MainWidthMetres * 0.5f * pixelsPerMetre), Runway);
            foreach (var terminal in AdelaideLayout.Terminals)
                FillPolygon(pixels, width, height, ToPixels(map, terminal.Xz), Building);
            return pixels;
        }

        /// <summary>05/23 and 12/30 as two-point centrelines in world x,z.</summary>
        public static IEnumerable<float[]> RunwayCentrelines()
        {
            var half = AdelaideLayout.MainRunwayLengthMetres * 0.5f;
            yield return new[] { -half, 0f, half, 0f };

            // Unity yaw turns the slab's local +x to (cos, -sin) in x,z.
            var yaw = AdelaideLayout.CrossRunwayYawDegrees * Mathf.Deg2Rad;
            var crossHalf = AdelaideLayout.CrossRunwayLengthMetres * 0.5f;
            var dx = Mathf.Cos(yaw) * crossHalf;
            var dz = -Mathf.Sin(yaw) * crossHalf;
            yield return new[]
            {
                AdelaideLayout.CrossRunwayCenterX - dx, AdelaideLayout.CrossRunwayCenterZ - dz,
                AdelaideLayout.CrossRunwayCenterX + dx, AdelaideLayout.CrossRunwayCenterZ + dz
            };
        }

        // Pixel buffer origin is bottom-left, so a world point maps with y up.
        private static List<Vector2> ToPixels(Rect pixelMap, float[] xz)
        {
            WorldBounds(out var minX, out var maxX, out var minZ, out var maxZ);
            var points = new List<Vector2>(xz.Length / 2);
            for (var i = 0; i + 1 < xz.Length; i += 2)
                points.Add(new Vector2(
                    (xz[i] - minX) / (maxX - minX) * pixelMap.width,
                    (xz[i + 1] - minZ) / (maxZ - minZ) * pixelMap.height));
            return points;
        }

        /// <summary>Even-odd scanline fill; the outline is treated as closed.</summary>
        public static void FillPolygon(Color32[] pixels, int width, int height, IReadOnlyList<Vector2> points, Color32 colour)
        {
            if (points.Count < 3)
                return;
            var crossings = new List<float>();
            for (var row = 0; row < height; row++)
            {
                var y = row + 0.5f;
                crossings.Clear();
                for (int i = 0, j = points.Count - 1; i < points.Count; j = i++)
                {
                    var a = points[i];
                    var b = points[j];
                    if ((a.y > y) == (b.y > y))
                        continue;
                    crossings.Add(a.x + (y - a.y) / (b.y - a.y) * (b.x - a.x));
                }

                crossings.Sort();
                for (var k = 0; k + 1 < crossings.Count; k += 2)
                {
                    var from = Math.Max(0, (int)Math.Ceiling(crossings[k] - 0.5f));
                    var to = Math.Min(width - 1, (int)Math.Floor(crossings[k + 1] - 0.5f));
                    for (var x = from; x <= to; x++)
                        pixels[row * width + x] = colour;
                }
            }
        }

        /// <summary>A thick polyline: every pixel within <paramref name="halfWidth"/> of a segment.</summary>
        public static void StrokePolyline(Color32[] pixels, int width, int height, IReadOnlyList<Vector2> points, float halfWidth, Color32 colour)
        {
            for (var i = 0; i + 1 < points.Count; i++)
            {
                var a = points[i];
                var b = points[i + 1];
                var minX = Math.Max(0, (int)Math.Floor(Math.Min(a.x, b.x) - halfWidth));
                var maxX = Math.Min(width - 1, (int)Math.Ceiling(Math.Max(a.x, b.x) + halfWidth));
                var minY = Math.Max(0, (int)Math.Floor(Math.Min(a.y, b.y) - halfWidth));
                var maxY = Math.Min(height - 1, (int)Math.Ceiling(Math.Max(a.y, b.y) + halfWidth));
                var ab = b - a;
                var lengthSq = Mathf.Max(0.0001f, ab.sqrMagnitude);
                var limitSq = halfWidth * halfWidth;
                for (var y = minY; y <= maxY; y++)
                for (var x = minX; x <= maxX; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    var t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / lengthSq);
                    if ((a + ab * t - p).sqrMagnitude <= limitSq)
                        pixels[y * width + x] = colour;
                }
            }
        }

        private static void Grow(float[] xz)
        {
            for (var i = 0; i + 1 < xz.Length; i += 2)
            {
                _minX = Math.Min(_minX, xz[i]);
                _maxX = Math.Max(_maxX, xz[i]);
                _minZ = Math.Min(_minZ, xz[i + 1]);
                _maxZ = Math.Max(_maxZ, xz[i + 1]);
            }
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Airside.Presentation
{
    /// <summary>South Australian flight scope, including coastal routes. Coordinates stay geographic across origin shifts.</summary>
    public static class RegionalMiniMap
    {
        public const double West = 129, East = 141, South = -38.5, North = -26;
        public static bool Contains(double latitude, double longitude) => double.IsFinite(latitude)
            && double.IsFinite(longitude) && latitude >= South && latitude <= North && longitude >= West && longitude <= East;
        public static Vector2 Point(Rect map, double latitude, double longitude) => new(
            (float)(map.x + (longitude - West) / (East - West) * map.width),
            (float)(map.yMax - (latitude - South) / (North - South) * map.height));
        public static Rect Fit(Rect area)
        {
            var aspect = (float)((East - West) * Math.Cos((North + South) * Math.PI / 360) / (North - South));
            var width = Mathf.Min(area.width, area.height * aspect);
            var height = width / aspect;
            return new Rect(area.center.x - width / 2, area.center.y - height / 2, width, height);
        }
        public static Color32[] Bake(int width, int height) =>
            Bake(width, height, West, East, South, North, new Color32(35, 48, 49, 255), false);

        /// <summary>
        /// Bakes the coast for any latitude/longitude window (row 0 is the southern edge). Pure and
        /// thread-safe: the moving map bakes a window around the aircraft on a worker thread, so the
        /// coast stays crisp at every scale. <paramref name="smoothEdges"/> blends the shoreline pixels
        /// by how much of each is land instead of leaving a stair-stepped edge.
        /// </summary>
        public static Color32[] Bake(int width, int height, double west, double east, double south, double north,
            Color32 land, bool smoothEdges)
        {
            var pixels = new Color32[width * height];
            Array.Fill(pixels, FieldMiniMap.Water);
            var intersections = new List<double>();
            // Scan each coastline once per row rather than testing every pixel against it.
            foreach (var ring in MapGeographyData.DetailCoasts)
            {
                var bounds = MapGeography.BoundsOf(ring);
                if (bounds.E < west || bounds.W > east || bounds.N < south || bounds.S > north) continue;
                var rowFrom = Math.Max(0, (int)Math.Floor((bounds.S - south) / (north - south) * height - 1));
                var rowTo = Math.Min(height - 1, (int)Math.Ceiling((bounds.N - south) / (north - south) * height + 1));
                for (var y = rowFrom; y <= rowTo; y++)
                {
                    var latitude = south + (y + .5) / height * (north - south);
                    if (latitude < bounds.S || latitude > bounds.N) continue;
                    intersections.Clear();
                    for (int i = 0, j = ring.Length - 2; i < ring.Length; j = i, i += 2)
                    {
                        var yi = ring[i + 1]; var yj = ring[j + 1];
                        if ((yi > latitude) == (yj > latitude)) continue;
                        intersections.Add(ring[i] + (ring[j] - ring[i]) * (latitude - yi) / (yj - yi));
                    }
                    intersections.Sort();
                    for (var i = 0; i + 1 < intersections.Count; i += 2)
                    {
                        var left = (intersections[i] - west) / (east - west) * width;
                        var right = (intersections[i + 1] - west) / (east - west) * width;
                        if (!smoothEdges)
                        {
                            var from = Math.Max(0, (int)Math.Ceiling(left - .5));
                            var to = Math.Min(width - 1, (int)Math.Floor(right - .5));
                            for (var x = from; x <= to; x++) pixels[y * width + x] = land;
                            continue;
                        }

                        var first = Math.Max(0, (int)Math.Floor(left));
                        var last = Math.Min(width - 1, (int)Math.Ceiling(right) - 1);
                        for (var x = first; x <= last; x++)
                        {
                            var cover = Math.Min(x + 1.0, right) - Math.Max(x, left);
                            if (cover <= 0) continue;
                            pixels[y * width + x] = cover >= 1.0 ? land : Blend(pixels[y * width + x], land, (float)cover);
                        }
                    }
                }
            }
            return pixels;
        }

        private static Color32 Blend(Color32 a, Color32 b, float t) => new(
            (byte)(a.r + (b.r - a.r) * t), (byte)(a.g + (b.g - a.g) * t),
            (byte)(a.b + (b.b - a.b) * t), (byte)(a.a + (b.a - a.a) * t));

        /// <summary>Repeated clicks cycle overlapping registrations, independent of paint order.</summary>
        public static int Pick(IReadOnlyList<Vector2> points, IReadOnlyList<string> ids, Vector2 click, string selected, float radius = FieldMiniMap.DotHitRadius)
        {
            var nearest = FieldMiniMap.NearestDot(points, click, radius);
            if (nearest < 0) return -1;
            var selectedAt = -1;
            for (var i = 0; i < ids.Count; i++) if (ids[i] == selected) selectedAt = i;
            if (selectedAt < 0 || (points[selectedAt] - click).sqrMagnitude > radius * radius)
                return nearest;
            var next = -1; var first = -1;
            for (var i = 0; i < points.Count; i++)
            {
                if ((points[i] - click).sqrMagnitude > radius * radius) continue;
                if (first < 0 || string.CompareOrdinal(ids[i], ids[first]) < 0) first = i;
                if (string.CompareOrdinal(ids[i], selected) > 0 && (next < 0 || string.CompareOrdinal(ids[i], ids[next]) < 0)) next = i;
            }
            return next >= 0 ? next : first;
        }
    }
}

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
        public static Color32[] Bake(int width, int height)
        {
            var pixels = new Color32[width * height];
            Array.Fill(pixels, FieldMiniMap.Water);
            var intersections = new List<double>();
            // Scan each coastline once per row rather than testing every pixel against it.
            foreach (var ring in MapGeographyData.DetailCoasts)
            {
                var bounds = MapGeography.BoundsOf(ring);
                if (bounds.E < West || bounds.W > East || bounds.N < South || bounds.S > North) continue;
                for (var y = 0; y < height; y++)
                {
                    var latitude = South + (y + .5) / height * (North - South);
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
                        var from = Math.Max(0, (int)Math.Ceiling((intersections[i] - West) / (East - West) * width - .5));
                        var to = Math.Min(width - 1, (int)Math.Floor((intersections[i + 1] - West) / (East - West) * width - .5));
                        for (var x = from; x <= to; x++) pixels[y * width + x] = new Color32(35, 48, 49, 255);
                    }
                }
            }
            return pixels;
        }
        /// <summary>Repeated clicks cycle overlapping registrations, independent of paint order.</summary>
        public static int Pick(IReadOnlyList<Vector2> points, IReadOnlyList<string> ids, Vector2 click, string selected)
        {
            var nearest = FieldMiniMap.NearestDot(points, click);
            if (nearest < 0) return -1;
            var selectedAt = -1;
            for (var i = 0; i < ids.Count; i++) if (ids[i] == selected) selectedAt = i;
            if (selectedAt < 0 || (points[selectedAt] - click).sqrMagnitude > FieldMiniMap.DotHitRadius * FieldMiniMap.DotHitRadius)
                return nearest;
            var next = -1; var first = -1;
            for (var i = 0; i < points.Count; i++)
            {
                if ((points[i] - click).sqrMagnitude > FieldMiniMap.DotHitRadius * FieldMiniMap.DotHitRadius) continue;
                if (first < 0 || string.CompareOrdinal(ids[i], ids[first]) < 0) first = i;
                if (string.CompareOrdinal(ids[i], selected) > 0 && (next < 0 || string.CompareOrdinal(ids[i], ids[next]) < 0)) next = i;
            }
            return next >= 0 ? next : first;
        }
    }
}

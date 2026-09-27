using System;
using System.Collections.Generic;
using System.Linq;
using Airside.Domain;

namespace Airside.Presentation
{
    /// <summary>
    /// ADR 0140 — the route map's geography as the painter reads it: which coastline to draw at
    /// a zoom, each ring's bounds for culling, and the runways and elevation of the airports the
    /// game serves. Data comes from <see cref="MapGeographyData"/> (generated). Pure.
    /// </summary>
    public static class MapGeography
    {
        /// <summary>The detail coastline covers this box (unwrapped lon, lat); outside it the region set is used.</summary>
        public const float DetailWest = 110f, DetailEast = 180f, DetailSouth = -48f, DetailNorth = -8f;

        private static readonly Dictionary<float[][], (float W, float E, float S, float N)[]> Bounds = new();

        /// <summary>The rings to draw at <paramref name="detail"/>.</summary>
        public static IEnumerable<float[]> Coasts(MapDetail detail)
        {
            switch (detail)
            {
                case MapDetail.World:
                    foreach (var ring in MapGeographyData.WorldCoasts)
                        yield return ring;
                    break;
                case MapDetail.Region:
                    foreach (var ring in MapGeographyData.RegionCoasts)
                        yield return ring;
                    break;
                default:
                    foreach (var ring in MapGeographyData.DetailCoasts)
                        yield return ring;
                    // Asia beyond the detail box still needs a coast.
                    var region = MapGeographyData.RegionCoasts;
                    var bounds = BoundsOf(region);
                    for (var i = 0; i < region.Length; i++)
                        if (!Inside(bounds[i]))
                            yield return region[i];
                    break;
            }
        }

        /// <summary>Zoomed in this far, the fine coast around each airport replaces the detail coast there.</summary>
        public const float AirportCoastZoom = 10f;

        /// <summary>Half-size (degrees) of the box around each airport the fine coast covers.</summary>
        public const float AirportCoastRadius = 0.75f;

        /// <summary>True when a point is close enough to a served airport to be drawn from the fine coast.</summary>
        public static bool NearAirport(float longitude, float latitude)
        {
            foreach (var d in DestinationCatalogue.All)
                if (Math.Abs(AustraliaMapLens.Unwrap(d.Longitude) - longitude) < AirportCoastRadius
                    && Math.Abs(d.Latitude - latitude) < AirportCoastRadius)
                    return true;
            return false;
        }

        /// <summary>True when a town shares a served airport's place (no second label for Port Lincoln).</summary>
        public static bool IsAirportTown(string name, float longitude, float latitude)
        {
            foreach (var d in DestinationCatalogue.All)
            {
                if (Math.Abs(AustraliaMapLens.Unwrap(d.Longitude) - longitude) > 0.3 || Math.Abs(d.Latitude - latitude) > 0.3)
                    continue;
                if (d.Name.StartsWith(name, StringComparison.OrdinalIgnoreCase)
                    || name.StartsWith(d.Name, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        /// <summary>West, east, south, north of each ring, computed once per set.</summary>
        public static (float W, float E, float S, float N)[] BoundsOf(float[][] rings)
        {
            lock (Bounds)
            {
                if (Bounds.TryGetValue(rings, out var cached))
                    return cached;
                var result = new (float, float, float, float)[rings.Length];
                for (var i = 0; i < rings.Length; i++)
                    result[i] = BoundsOf(rings[i]);
                Bounds[rings] = result;
                return result;
            }
        }

        public static (float W, float E, float S, float N) BoundsOf(float[] ring)
        {
            float w = float.MaxValue, e = float.MinValue, s = float.MaxValue, n = float.MinValue;
            for (var i = 0; i + 1 < ring.Length; i += 2)
            {
                w = Math.Min(w, ring[i]);
                e = Math.Max(e, ring[i]);
                s = Math.Min(s, ring[i + 1]);
                n = Math.Max(n, ring[i + 1]);
            }

            return (w, e, s, n);
        }

        private static bool Inside((float W, float E, float S, float N) b) =>
            b.W >= DetailWest && b.E <= DetailEast && b.S >= DetailSouth && b.N <= DetailNorth;

        /// <summary>True when a point (unwrapped lon, lat) is on land in the detail coastline (ray casting).</summary>
        public static bool OnLand(double longitude, double latitude)
        {
            var lon = (float)AustraliaMapLens.Unwrap(longitude);
            var lat = (float)latitude;
            var set = lon >= DetailWest && lon <= DetailEast && lat >= DetailSouth && lat <= DetailNorth
                ? MapGeographyData.DetailCoasts
                : MapGeographyData.RegionCoasts;
            var bounds = BoundsOf(set);
            for (var r = 0; r < set.Length; r++)
            {
                var b = bounds[r];
                if (lon < b.W || lon > b.E || lat < b.S || lat > b.N)
                    continue;
                if (Contains(set[r], lon, lat))
                    return true;
            }

            return false;
        }

        private static bool Contains(float[] ring, float x, float y)
        {
            var inside = false;
            var count = ring.Length / 2;
            for (int i = 0, j = count - 1; i < count; j = i++)
            {
                float xi = ring[i * 2], yi = ring[i * 2 + 1], xj = ring[j * 2], yj = ring[j * 2 + 1];
                if ((yi > y) != (yj > y) && x < (xj - xi) * (y - yi) / (yj - yi) + xi)
                    inside = !inside;
            }

            return inside;
        }

        /// <summary>Distance in kilometres from a point to the nearest detail-coast vertex (for tests).</summary>
        public static double KmToCoast(double longitude, double latitude)
        {
            var best = double.MaxValue;
            var here = new Destination("XXX", string.Empty, string.Empty, latitude, longitude);
            foreach (var ring in MapGeographyData.DetailCoasts.Concat(MapGeographyData.AirportCoasts))
                for (var i = 0; i + 1 < ring.Length; i += 2)
                {
                    var dLat = ring[i + 1] - latitude;
                    var dLon = (ring[i] - AustraliaMapLens.Unwrap(longitude)) * Math.Cos(latitude * Math.PI / 180.0);
                    if (Math.Abs(dLat) > 3 || Math.Abs(dLon) > 3)
                        continue;
                    best = Math.Min(best, here.DistanceKmTo(new Destination("YYY", string.Empty, string.Empty,
                        ring[i + 1], ring[i] > 180 ? ring[i] - 360 : ring[i])));
                }

            return best;
        }

        /// <summary>A served airport's runways (ADR 0140).</summary>
        public static IEnumerable<(string End1, string End2, double Lat1, double Lon1, double Lat2, double Lon2, int LengthM, int WidthM)>
            RunwaysAt(string code)
        {
            foreach (var r in MapGeographyData.Runways)
                if (r.Code == code)
                    yield return (r.End1, r.End2, r.Lat1, r.Lon1, r.Lat2, r.Lon2, r.LengthM, r.WidthM);
        }

        /// <summary>Field elevation in feet, or null when unknown.</summary>
        public static int? ElevationFt(string code)
        {
            foreach (var a in MapGeographyData.Airports)
                if (a.Code == code)
                    return a.ElevationFt > 0 ? a.ElevationFt : (int?)null;
            return null;
        }

        /// <summary>The airport's own name ("Kingscote Airport"), or the code.</summary>
        public static string AirportName(string code)
        {
            foreach (var a in MapGeographyData.Airports)
                if (a.Code == code)
                    return a.Name;
            return code;
        }
    }
}

using System;

namespace Airside.Presentation
{
    /// <summary>
    /// The flight-view moving map: which scale it shows, and the projection between latitude/longitude
    /// and panel pixels. Pure maths, no UnityEngine, so the headless harness checks it. The map is
    /// centred on the aircraft; <see cref="RangesKm"/> is the distance from the centre to the panel edge.
    /// </summary>
    public static class CockpitMovingMap
    {
        public static readonly float[] RangesKm = { 2f, 6f, 25f, 80f, 250f, 800f };

        /// <summary>Largest range that still shows the baked airfield layout instead of the coast.</summary>
        public const float FieldDetailMaxKm = 6f;
        public const float NearAirportKm = 12f;
        public const double KmPerDegree = 111.195;

        public const int Auto = -1;

        public static int Clamp(int index) => Math.Max(0, Math.Min(RangesKm.Length - 1, index));

        /// <summary>Range index when the player has not chosen one: airfield detail near an airport,
        /// otherwise the smallest scale that holds about half the leg either side.</summary>
        public static int AutoRange(double kmToHome, double kmToTarget, double legKm, bool onOrNearGround)
        {
            if (Math.Min(kmToHome, kmToTarget) < NearAirportKm)
                return onOrNearGround ? 0 : 1;
            var wanted = Math.Max(0.55 * legKm, 60.0);
            for (var i = 3; i < RangesKm.Length; i++)
                if (RangesKm[i] >= wanted) return i;
            return RangesKm.Length - 1;
        }

        public static string RangeLabel(float km) => km < 10f ? km.ToString("0.#") + " km" : km.ToString("0") + " km";

        /// <summary>Pixels per degree of latitude when the half-panel (<paramref name="halfPanel"/> px) spans the range.</summary>
        public static double PixelsPerDegreeLatitude(double halfPanel, float rangeKm) => halfPanel / (rangeKm / KmPerDegree);

        /// <summary>Panel offset from the centre (x right, y down) of a point, equirectangular about the centre latitude.</summary>
        public static void Offset(double centreLat, double centreLon, double lat, double lon, double pxPerDegLat,
            out double x, out double y)
        {
            var dLon = lon - centreLon;
            if (dLon > 180) dLon -= 360;
            else if (dLon < -180) dLon += 360;
            x = dLon * pxPerDegLat * Math.Cos(centreLat * Math.PI / 180.0);
            y = -(lat - centreLat) * pxPerDegLat;
        }

        /// <summary>Screen direction (x right, y down) for a true heading on a north-up map.</summary>
        public static void HeadingDirection(double headingDegrees, out double x, out double y)
        {
            var r = headingDegrees * Math.PI / 180.0;
            x = Math.Sin(r);
            y = -Math.Cos(r);
        }

        /// <summary>Square coast texture size, and how many ranges either side of the aircraft it covers.</summary>
        public const int CoastWindowPixels = 512;
        public const double CoastWindowSpan = 2.0;

        /// <summary>The latitude/longitude window to bake around the aircraft for a scale (equirectangular).</summary>
        public static void CoastWindow(double lat, double lon, float rangeKm,
            out double south, out double north, out double west, out double east)
        {
            var halfLat = rangeKm * CoastWindowSpan / KmPerDegree;
            var halfLon = halfLat / Math.Max(0.2, Math.Cos(lat * Math.PI / 180.0));
            south = lat - halfLat; north = lat + halfLat;
            west = lon - halfLon; east = lon + halfLon;
        }

        /// <summary>
        /// Whether a baked coast window still serves the view: it must hold the whole panel (including
        /// the corners) and not be so much coarser or finer than the current scale that it looks soft.
        /// </summary>
        public static bool CoastWindowServes(double south, double north, double west, double east, float bakedRangeKm,
            double lat, double lon, float rangeKm)
        {
            if (rangeKm < bakedRangeKm * 0.55f || rangeKm > bakedRangeKm * 1.15f)
                return false;
            var halfLat = rangeKm * 1.42 / KmPerDegree;
            var halfLon = halfLat / Math.Max(0.2, Math.Cos(lat * Math.PI / 180.0));
            return lat - halfLat >= south && lat + halfLat <= north && lon - halfLon >= west && lon + halfLon <= east;
        }

        /// <summary>Bearing and distance text for the footer, e.g. "MEL 412 km".</summary>
        public static string DistanceText(string code, double km) =>
            code + "  " + (km < 10 ? km.ToString("0.0") : km.ToString("0")) + " km";
    }
}

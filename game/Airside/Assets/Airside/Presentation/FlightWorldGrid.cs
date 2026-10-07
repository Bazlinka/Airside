using System;

namespace Airside.Presentation
{
    /// <summary>Double precision travel coordinates and bounded resident terrain. ADR 0215.</summary>
    public static class FlightWorldGrid
    {
        public const int TileMetres = 16000;
        public const int Cells = 16;
        public const int RadiusTiles = 3;
        public const int MaxTiles = (RadiusTiles * 2 + 1) * (RadiusTiles * 2 + 1);
        public const double OriginStepMetres = 8000;
        public static int Tile(double metres) => checked((int)Math.Floor(metres / TileMetres));
        public static double Origin(double metres) => Math.Round(metres / OriginStepMetres) * OriginStepMetres;
        // ADR 0251 — the wide overview. Past WideMapStartMetres the overview camera drives the terrain: the 16 km ring stays
        // round the focus and a coarse ring of 64 km tiles (2 km cells, 121 resident) fills the view out to ~350 km.
        public const int CoarseTileMetres = 64000;
        public const int CoarseCells = 32;
        public const int CoarseRadiusTiles = 5;
        /// <summary>
        /// Zoomed right out the camera stands up to its orbit distance behind the focus, past a 5-tile ring, so the ring
        /// grows with the zoom up to this (289 tiles, every one reaching 512 km from the focus).
        /// </summary>
        public const int CoarseMaxRadiusTiles = 8;
        public const int CoarseMaxTiles = (CoarseMaxRadiusTiles * 2 + 1) * (CoarseMaxRadiusTiles * 2 + 1);
        /// <summary>Coarse ring radius (tiles) that keeps the ground under the camera covered at this orbit distance.</summary>
        public static int CoarseRadiusFor(double orbitDistance) =>
            Math.Max(CoarseRadiusTiles, Math.Min(CoarseMaxRadiusTiles,
                (int)Math.Ceiling((Math.Max(0, orbitDistance) + CoarseTileMetres) / CoarseTileMetres)));
        /// <summary>
        /// Horizon fade for the wide overview, in shader units (metres before <paramref name="horizonScale"/>): clear to just
        /// past the focus, then gone into the sky before the camera can see the edge of the coarse ring beside it.
        /// </summary>
        public static void WideHorizonFade(double orbitDistance, float horizonScale, out float start, out float end)
        {
            var reach = (double)CoarseRadiusFor(orbitDistance) * CoarseTileMetres;
            var startMetres = orbitDistance * 1.1;
            var endMetres = Math.Max(startMetres * 1.15, 0.95 * Math.Sqrt(reach * reach + orbitDistance * orbitDistance));
            var scale = Math.Max(1.0, horizonScale);
            start = (float)(startMetres / scale);
            end = (float)(endMetres / scale);
        }
        /// <summary>The coarse ring sits this far under the fine one, so where both exist the fine tile wins.</summary>
        public const float CoarseDropMetres = 40f;
        /// <summary>Orbit distance above which the overview camera streams terrain (the classic zoom ends at 45 km).</summary>
        public const double WideMapStartMetres = 60000;
        public static bool WideMap(double orbitDistance) => orbitDistance > WideMapStartMetres;
        public static int CoarseTile(double metres) => checked((int)Math.Floor(metres / CoarseTileMetres));
        public static bool CoarseResident(int tileX, int tileZ, int centreX, int centreZ, int radius = CoarseRadiusTiles) =>
            Math.Abs((long)tileX - centreX) <= radius && Math.Abs((long)tileZ - centreZ) <= radius;
        public static bool Covered(double latitude, double longitude) =>
            latitude >= -39 && latitude <= -25 && longitude >= 128 && longitude <= 142;
        public static bool Resident(int tileX, int tileZ, int centreX, int centreZ) =>
            Math.Abs((long)tileX - centreX) <= RadiusTiles && Math.Abs((long)tileZ - centreZ) <= RadiusTiles;
    }
}

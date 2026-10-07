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
        public const int CoarseMaxTiles = (CoarseRadiusTiles * 2 + 1) * (CoarseRadiusTiles * 2 + 1);
        /// <summary>The coarse ring sits this far under the fine one, so where both exist the fine tile wins.</summary>
        public const float CoarseDropMetres = 40f;
        /// <summary>Orbit distance above which the overview camera streams terrain (the classic zoom ends at 45 km).</summary>
        public const double WideMapStartMetres = 60000;
        public static bool WideMap(double orbitDistance) => orbitDistance > WideMapStartMetres;
        public static int CoarseTile(double metres) => checked((int)Math.Floor(metres / CoarseTileMetres));
        public static bool CoarseResident(int tileX, int tileZ, int centreX, int centreZ) =>
            Math.Abs((long)tileX - centreX) <= CoarseRadiusTiles && Math.Abs((long)tileZ - centreZ) <= CoarseRadiusTiles;
        public static bool Covered(double latitude, double longitude) =>
            latitude >= -39 && latitude <= -25 && longitude >= 128 && longitude <= 142;
        public static bool Resident(int tileX, int tileZ, int centreX, int centreZ) =>
            Math.Abs((long)tileX - centreX) <= RadiusTiles && Math.Abs((long)tileZ - centreZ) <= RadiusTiles;
    }
}

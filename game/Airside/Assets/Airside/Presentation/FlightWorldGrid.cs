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
        public static bool Covered(double latitude, double longitude) =>
            latitude >= -39 && latitude <= -25 && longitude >= 128 && longitude <= 142;
        public static bool Resident(int tileX, int tileZ, int centreX, int centreZ) =>
            Math.Abs((long)tileX - centreX) <= RadiusTiles && Math.Abs((long)tileZ - centreZ) <= RadiusTiles;
    }
}

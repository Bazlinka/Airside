using System;

namespace Airside.Presentation
{
    /// <summary>Presentation budgets in metres; no schedules, persistence or frame-dependent simulation.</summary>
    public static class FlightWorldDetail
    {
        public const double AirportLoadMetres = 70000;
        public const double AirportUnloadMetres = 80000;
        public const double CruiseEnterMetres = 4500;
        public const double CruiseExitMetres = 3500;
        public static bool Cruise(double altitude, bool wasCruise) =>
            double.IsFinite(altitude) && altitude >= (wasCruise ? CruiseExitMetres : CruiseEnterMetres);
        public static int Cells(bool cruise, bool approach) => approach ? 64 : cruise ? 8 : FlightWorldGrid.Cells;
        public static bool ApproachTile(int tx, int tz, double airportX, double airportZ) =>
            Math.Abs(tx - FlightWorldGrid.Tile(airportX)) <= 1 && Math.Abs(tz - FlightWorldGrid.Tile(airportZ)) <= 1;
        public static double RunwayDistance(double x, double z, RegionalRunway runway)
        {
            var dx=runway.Bx-runway.Ax; var dz=runway.Bz-runway.Az;
            var t=Math.Clamp(((x-runway.Ax)*dx+(z-runway.Az)*dz)/(dx*dx+dz*dz),0,1);
            return Math.Sqrt(Math.Pow(x-runway.Ax-t*dx,2)+Math.Pow(z-runway.Az-t*dz,2));
        }
    }
}

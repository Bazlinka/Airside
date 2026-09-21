using Airside.Domain;

namespace Airside.Simulation
{
    /// <summary>
    /// Celestial east/north/up in the field's world frame: +x along 05 → 23, +z left of
    /// 05 (terminal). Same rotation as every OSM/live-traffic placement, so noon sits to
    /// the true north of the runway rather than a fake Unity yaw sweep.
    /// </summary>
    public static class SkyDirection
    {
        public static void ToWorld(in CelestialBody body, out double x, out double y, out double z)
        {
            ToWorld(body.AzimuthDegrees, body.ElevationDegrees, out x, out y, out z);
        }

        public static void ToWorld(double azimuthDegrees, double elevationDegrees,
            out double x, out double y, out double z)
        {
            var probe = new CelestialBody(azimuthDegrees, elevationDegrees);
            probe.Horizontal(out var east, out var north, out y);
            YpadFrame.FromEastNorth(east, north, out x, out z);
        }
    }
}

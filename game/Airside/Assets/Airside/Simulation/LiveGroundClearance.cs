using System;
using System.Collections.Generic;
using Airside.Domain;

namespace Airside.Simulation
{
    /// <summary>A game aircraft a real one must keep clear of: where it is drawn and half its span.</summary>
    public readonly struct GroundObstacle
    {
        public GroundObstacle(double x, double z, double halfSpanMetres)
        {
            X = x;
            Z = z;
            HalfSpanMetres = halfSpanMetres;
        }

        public double X { get; }
        public double Z { get; }
        public double HalfSpanMetres { get; }
    }

    /// <summary>
    /// Real aircraft on the ground are drawn only where they cannot clash with the game (ADR
    /// 0082). The simulation owns every stand and runway it uses and never hears about live
    /// traffic, so a real aircraft stands aside — is not drawn — while it would overlap one of
    /// the game's aircraft, or while it is on a runway strip the game is using.
    /// </summary>
    public static class LiveGroundClearance
    {
        /// <summary>Wingtip-to-wingtip margin kept between a real aircraft and a game one.</summary>
        public const double MarginMetres = 10.0;

        /// <summary>Runway pavement plus its shoulders, each side of the centreline.</summary>
        public const double StripHalfWidthMetres = 45.0;

        /// <summary>Beyond the runway ends: overrun and the lineup/vacate turn-offs.</summary>
        public const double StripEndMetres = 80.0;

        public static double HalfSpan(AircraftType type) =>
            type != null && AircraftCatalogue.TryFor(type, out var spec) ? spec.WingspanMetres * 0.5 : 18.0;

        public static bool Clashes(double x, double z, double halfSpanMetres, IReadOnlyList<GroundObstacle> gameAircraft,
            bool mainStripInUse, bool crossStripInUse)
        {
            if (gameAircraft != null)
            {
                foreach (var other in gameAircraft)
                {
                    var dx = other.X - x;
                    var dz = other.Z - z;
                    var clear = halfSpanMetres + other.HalfSpanMetres + MarginMetres;
                    if (dx * dx + dz * dz < clear * clear)
                        return true;
                }
            }

            if (mainStripInUse && Math.Abs(z) < StripHalfWidthMetres
                               && Math.Abs(x) < RunwayFrame.MainHalfLength + StripEndMetres)
                return true;

            if (crossStripInUse)
            {
                AdelaideCrossRoutes.WorldToLocal((float)x, (float)z, out var along, out var across);
                if (Math.Abs(across) < StripHalfWidthMetres
                    && Math.Abs(along) < AdelaideCrossRoutes.HalfLength + StripEndMetres)
                    return true;
            }

            return false;
        }
    }
}

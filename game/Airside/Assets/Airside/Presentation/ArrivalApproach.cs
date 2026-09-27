using System;
using Airside.Domain;
using Airside.Simulation;

namespace Airside.Presentation
{
    /// <summary>
    /// ADR 0142 — where an arrival is drawn before the tower clears it. Arrivals used to pop into
    /// view 18 km out on a dead-straight final at 3,000 ft. Now they appear 32 km out, descend from
    /// up to 6,000 ft on the 3° path, and beyond 12 km curve in from the side their origin city is
    /// on, the way a real arrival joins the final. Pure (no UnityEngine), so the path is tested.
    /// </summary>
    public static class ArrivalApproach
    {
        /// <summary>Beyond this an inbound is not drawn yet.</summary>
        public const float ShowMetres = 32_000f;

        /// <summary>6,000 ft: the highest an arrival is drawn on the approach.</summary>
        public const float CapMetres = 1_830f;

        /// <summary>Inside this the final is straight in along the centre line.</summary>
        public const float StraightInMetres = 12_000f;

        /// <summary>How far to one side the joining curve starts, at the show distance.</summary>
        public const float JoinOffsetMetres = 6_500f;

        /// <summary>Beyond this distance from the camera an aircraft also shows a light (its landing lights).</summary>
        public const float BeaconFromMetres = 6_000f;

        /// <summary>Height above the ground: the glideslope, capped at 6,000 ft.</summary>
        public static float Height(float glideslopeHeight) => Math.Min(glideslopeHeight, CapMetres);

        /// <summary>True heading (degrees) an aircraft lands on this runway.</summary>
        public static int LandingHeading(RunwayDirection runway) => runway switch
        {
            RunwayDirection.Runway23 => RunwayWeather.Heading23,
            RunwayDirection.Runway12 => RunwayWeather.Heading12,
            RunwayDirection.Runway30 => RunwayWeather.Heading30,
            _ => RunwayWeather.Heading05
        };

        /// <summary>Initial great-circle bearing (degrees true) from one airport to another.</summary>
        public static double Bearing(Destination from, Destination to)
        {
            var lat1 = from.Latitude * Math.PI / 180.0;
            var lat2 = to.Latitude * Math.PI / 180.0;
            var dLon = (to.Longitude - from.Longitude) * Math.PI / 180.0;
            var y = Math.Sin(dLon) * Math.Cos(lat2);
            var x = Math.Cos(lat1) * Math.Sin(lat2) - Math.Sin(lat1) * Math.Cos(lat2) * Math.Cos(dLon);
            var bearing = Math.Atan2(y, x) * 180.0 / Math.PI;
            return (bearing + 360.0) % 360.0;
        }

        /// <summary>
        /// Which side, and how much, the arrival joins from: −1..1 across the final, positive to the
        /// left of the landing direction (the runway frame's +z). An origin straight behind the
        /// approach gives 0 (straight in); one off to a side, or ahead, gives up to ±1.
        /// </summary>
        public static float LateralFactor(double originBearing, int landingHeading)
        {
            var behind = landingHeading + 180.0;
            var a = ((originBearing - behind) % 360.0 + 540.0) % 360.0 - 180.0;
            // Positive a is clockwise from the approach axis; the left of the landing direction
            // (heading − 90) is that way, so it maps to +z.
            if (Math.Abs(a) <= 90.0)
                return (float)Math.Sin(a * Math.PI / 180.0);
            return a > 0 ? 1f : -1f;
        }

        /// <summary>
        /// Sideways offset from the centre line (metres, +z) at <paramref name="metresOut"/> from the
        /// hold point. Zero, with zero slope, at the straight-in point, so the curve joins the final
        /// without a kink, growing with the square of the distance beyond it.
        /// </summary>
        public static float LateralOffset(float metresOut, float lateralFactor)
        {
            if (metresOut <= StraightInMetres)
                return 0f;
            var s = Math.Min(1f, (metresOut - StraightInMetres) / (ShowMetres - StraightInMetres));
            return lateralFactor * JoinOffsetMetres * s * s;
        }

        /// <summary>The same factor for an inbound aircraft (0 when it has no origin).</summary>
        public static float LateralFactor(FleetAircraft aircraft, RunwayDirection runway)
        {
            if (aircraft?.CurrentDestination is not { } origin)
                return 0f;
            return LateralFactor(Bearing(DestinationCatalogue.Adelaide, origin), LandingHeading(runway));
        }

        /// <summary>
        /// How bright a distant aircraft's light should be (0..1): nothing close in, where the model
        /// reads by itself, full from twice <see cref="BeaconFromMetres"/>.
        /// </summary>
        public static float BeaconStrength(float distanceMetres)
        {
            var t = (distanceMetres - BeaconFromMetres) / BeaconFromMetres;
            return t <= 0f ? 0f : t >= 1f ? 1f : t * t * (3f - 2f * t);
        }
    }
}

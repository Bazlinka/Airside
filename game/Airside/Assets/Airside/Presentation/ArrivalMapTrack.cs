using System;
using Airside.Domain;
using Airside.Simulation;

namespace Airside.Presentation
{
    /// <summary>
    /// Where an inbound flight is on the Australia map, agreeing with where the field draws it. The
    /// field flies an arrival down the extended final at approach speed for its last
    /// <see cref="ArrivalApproach.ShowMetres"/>, curving in from its origin's side (ADR 0142). The
    /// map used to run the same leg at descent-profile speed along the great circle, so the two
    /// showed one aircraft in different places: tens of kilometres apart, and in different
    /// directions from the field. Now the last stretch is the same distance out at the same time
    /// and follows the same ground track, and the great circle bends onto it over
    /// <see cref="BlendMetres"/> so nothing jumps. Depends on the flight-path model for the hold
    /// point, otherwise plain numbers.
    /// </summary>
    public static class ArrivalMapTrack
    {
        /// <summary>Beyond the drawn final, over this much further the map track bends from the final onto the great circle.</summary>
        public const float BlendMetres = 40_000f;

        /// <summary>
        /// Metres out from the hold point after <paramref name="legSeconds"/> less
        /// <paramref name="remainingSeconds"/> of an inbound leg. The last
        /// <see cref="ArrivalApproach.ShowMetres"/> are flown at approach speed (as the field does),
        /// the rest on the descent profile, so the distance is continuous and always falling.
        /// </summary>
        public static double DistanceOutMetres(double legKm, double legSeconds, double remainingSeconds, AircraftType type)
        {
            var legMetres = Math.Max(0.0, legKm * 1000.0);
            var remaining = Math.Max(0.0, Math.Min(remainingSeconds, legSeconds));
            var speed = CircuitProfile.Knots(AircraftPerformance.For(type).ApproachKnots);
            var show = (double)ArrivalApproach.ShowMetres;
            var finalSeconds = speed > 0f ? show / speed : 0.0;
            if (legMetres <= show * 1.5 || legSeconds <= finalSeconds * 1.25)
            {
                // Too short a leg to have a separate final: the plain profile.
                var profile = new EnrouteProfile(legKm, legSeconds, type);
                return legMetres * (1.0 - profile.DistanceFractionAt(legSeconds - remaining));
            }

            if (remaining <= finalSeconds)
                return speed * remaining;
            var bodyMetres = legMetres - show;
            var bodySeconds = legSeconds - finalSeconds;
            var body = new EnrouteProfile(bodyMetres / 1000.0, bodySeconds, type);
            var flown = bodySeconds - (remaining - finalSeconds);
            return show + bodyMetres * (1.0 - body.DistanceFractionAt(flown));
        }

        /// <summary>
        /// The field's world x/z for an arrival <paramref name="metresOut"/> from the hold point on the
        /// extended final of <paramref name="runway"/>, joining from <paramref name="lateral"/>
        /// (<see cref="ArrivalApproach.LateralFactor(FleetAircraft, RunwayDirection)"/>).
        /// <paramref name="extraAcross"/> is any extra sideways metres in the runway frame.
        /// One implementation for the drawn aircraft and the map.
        /// </summary>
        public static void FinalWorldXZ(RunwayDirection runway, AircraftType type, float metresOut, float lateral,
            float extraAcross, out float worldX, out float worldZ)
        {
            var hold = AirsideFlightPath.Approach((float)ApproachHold.HoldingFinalProgress(0), 0f, type);
            var metres = Math.Max(0f, metresOut);
            var join = ArrivalApproach.LateralOffset(metres, lateral);
            RunwayFrame.ToWorld(runway, hold.x - metres, 0f, hold.z + extraAcross + join, out worldX, out _, out worldZ);
        }

        /// <summary>
        /// Latitude and longitude of an inbound flight <paramref name="metresOut"/> from the hold
        /// point: on the field's final inside <see cref="ArrivalApproach.ShowMetres"/>, then the
        /// great-circle point bent onto it. <paramref name="legMetres"/> is the whole leg.
        /// </summary>
        public static void LatLon(Destination origin, Destination home, string routeKey, double legMetres,
            double metresOut, RunwayDirection runway, AircraftType type, float lateral,
            out double latitude, out double longitude)
        {
            var show = (double)ArrivalApproach.ShowMetres;
            if (metresOut <= show)
            {
                FinalWorldXZ(runway, type, (float)metresOut, lateral, 0f, out var fx, out var fz);
                YpadFrame.ToLatLon(fx, fz, out latitude, out longitude);
                return;
            }

            GreatCircleWorld(origin, home, routeKey, legMetres, metresOut, out var gx, out var gz);
            GreatCircleWorld(origin, home, routeKey, legMetres, show, out var sx, out var sz);
            FinalWorldXZ(runway, type, (float)show, lateral, 0f, out var ex, out var ez);
            var s = Math.Min(1.0, (metresOut - show) / BlendMetres);
            var keep = 1.0 - s * s * (3.0 - 2.0 * s);
            YpadFrame.ToLatLon(gx + (ex - sx) * keep, gz + (ez - sz) * keep, out latitude, out longitude);
        }

        private static void GreatCircleWorld(Destination origin, Destination home, string routeKey, double legMetres,
            double metresOut, out double x, out double z)
        {
            var t = legMetres > 0.0 ? Math.Max(0.0, Math.Min(1.0, 1.0 - metresOut / legMetres)) : 1.0;
            RouteMap.FlightPoint(origin.Latitude, origin.Longitude, home.Latitude, home.Longitude, t, routeKey,
                out var lat, out var lon);
            YpadFrame.ToWorld(lat, lon, out x, out z);
        }
    }
}

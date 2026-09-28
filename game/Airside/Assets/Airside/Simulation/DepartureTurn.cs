using System;
using Airside.Domain;

namespace Airside.Simulation
{
    /// <summary>
    /// Climb-out stays on the runway heading until the far threshold, then banks
    /// onto the destination track. Takeoff itself is wings-level over the strip.
    /// </summary>
    public static class DepartureTurn
    {
        /// <summary>Published 05/23 true headings, degrees clockwise from north.</summary>
        public const double Runway05Degrees = RunwayWeather.Heading05;
        public const double Runway23Degrees = RunwayWeather.Heading23;

        /// <summary>
        /// Departed progress at which the aircraft has flown the remaining runway
        /// heading and may start the SID turn. Takeoff itself stays wings-level
        /// over the strip.
        /// </summary>
        public const float TurnStartProgress = 0.52f;

        /// <summary>Departed progress at which the heading change is established.</summary>
        public const float TurnEstablishedProgress = 0.88f;

        /// <summary>
        /// How far the SID turn is established (0 on the roll and the straight
        /// climb-out, 1 once the aircraft has cleared the far threshold and turned).
        /// </summary>
        public static float Blend(AircraftPhase phase, float progress)
        {
            if (phase != AircraftPhase.Departed)
                return 0f;
            if (progress <= TurnStartProgress)
                return 0f;
            return Smooth01((progress - TurnStartProgress)
                / Math.Max(0.05f, TurnEstablishedProgress - TurnStartProgress));
        }

        /// <summary>
        /// Extra yaw, degrees, from the runway heading toward <paramref name="destination"/>.
        /// Positive is a right turn in the runway frame — which, given Unity's rotation
        /// handedness applied on top of a departing-05 forward of world +X (see
        /// <see cref="RunwayFrame.Forward"/>), works out to −Z, the same sign
        /// <see cref="LateralMetres"/> already uses; it is not +Z as an earlier version of
        /// this comment claimed.
        /// </summary>
        public static float YawDegrees(RunwayDirection runway, Destination home, Destination destination,
            AircraftPhase phase, float progress)
        {
            var blend = Blend(phase, progress);
            if (blend <= 0f)
                return 0f;
            return (float)(RelativeRadians(runway, home, destination) * (180.0 / Math.PI) * 0.72 * blend);
        }

        /// <summary>Metres off the runway centreline as the turn develops.</summary>
        public static float LateralMetres(RunwayDirection runway, Destination home, Destination destination,
            AircraftPhase phase, float progress)
        {
            var blend = Blend(phase, progress);
            if (blend <= 0f)
                return 0f;
            // World +Z is left of +X (runway 05). A destination to the right of the
            // runway heading therefore moves toward −Z in the 05 frame; Runway 23
            // is applied later by flipping that frame.
            var relative = RelativeRadians(runway, home, destination);
            return (float)(-Math.Sin(relative) * 380.0 * blend * blend);
        }

        /// <summary>Forward in the runway frame after the turn: +X along 05, +Z across.</summary>
        public static (float x, float z) Forward(RunwayDirection runway, Destination home, Destination destination,
            AircraftPhase phase, float progress)
        {
            var yaw = YawDegrees(runway, home, destination, phase, progress) * (float)(Math.PI / 180.0);
            var along = (float)Math.Cos(yaw);
            // Matches LateralMetres's sign (see YawDegrees), not a naive +sin(yaw): a
            // positive yaw is a −Z turn in the runway-05 frame.
            var across = (float)-Math.Sin(yaw);
            if (runway == RunwayDirection.Runway23 || runway == RunwayDirection.Runway30)
                return (-along, -across);
            return (along, across);
        }

        /// <summary>
        /// Once the SID turn is established (<see cref="Blend"/> reaches 1), any further
        /// along-track distance is flown on the established heading rather than straight down
        /// the original runway line — without this, a departure that had turned onto its
        /// destination track would keep translating parallel to the runway forever afterward,
        /// the nose pointed one way and the ground track going another (the classic
        /// crabbing/drifting look, rather than an aircraft that has actually turned).
        ///
        /// Returns the (forward, sideways) decomposition of <paramref name="extraAlongMetres"/>
        /// of further travel on that established heading, in the same runway-local frame as
        /// <see cref="LateralMetres"/> (sideways carries its sign, not a raw +sin(yaw)).
        /// cos/sin keeps this bounded and correctly signed for any turn angle — including the
        /// rare near-reversal a regional taking whichever of 12/30 the wind favours, rather
        /// than the end that favours its destination, can produce — where projecting forward
        /// distance through tan(yaw) instead would blow up approaching a 90° turn.
        /// </summary>
        public static (float forward, float sideways) EstablishedTrackMetres(
            RunwayDirection runway, Destination home, Destination destination, float extraAlongMetres)
        {
            var yawRadians = YawDegrees(runway, home, destination, AircraftPhase.Departed, 1f)
                              * (float)(Math.PI / 180.0);
            return (extraAlongMetres * (float)Math.Cos(yawRadians), -extraAlongMetres * (float)Math.Sin(yawRadians));
        }

        /// <summary>Bank of a normal airline departure turn.</summary>
        public const float ArcBankDegrees = 25f;

        /// <summary>Metres over which the wings roll into, and out of, the turn.</summary>
        public const float RollMetres = 180f;

        /// <summary>
        /// Radius of a coordinated turn at <paramref name="knots"/> and <see cref="ArcBankDegrees"/>:
        /// r = v² / (g·tan φ). About 1 km for a turboprop at 140 kt, 2 km for a jet at 190 kt.
        /// </summary>
        public static float TurnRadiusMetres(float knots)
        {
            var v = Math.Max(40.0, knots) * 0.514444;
            var r = v * v / (9.80665 * Math.Tan(ArcBankDegrees * Math.PI / 180.0));
            return (float)Math.Max(500.0, Math.Min(4000.0, r));
        }

        /// <summary>
        /// The departure as a flown turn (ADR 0161): straight until <paramref name="alongMetres"/>
        /// reaches 0 (the turn start past the far threshold), then a constant-radius arc onto the
        /// full bearing to the destination, then straight on it. Returns the offset from the turn
        /// start in the runway-local frame: forward along the takeoff direction, sideways with
        /// <see cref="LateralMetres"/>'s sign (a right turn is −Z), and the yaw off the runway
        /// heading (positive = right). Position and yaw come from one curve, so the nose always
        /// points along the path.
        ///
        /// It replaces a turn that yawed the nose (to 72 % of the bearing) while the position only
        /// slid up to 380 m sideways and kept flying down the extended runway line: the aircraft
        /// pointed one way and moved another, which read as drifting rather than turning.
        /// </summary>
        public static (float forward, float sideways, float yawDegrees) Arc(double relativeRadians, float radius,
            float alongMetres)
        {
            if (alongMetres <= 0f)
                return (alongMetres, 0f, 0f);
            var sign = relativeRadians < 0 ? -1.0 : 1.0;
            var total = Math.Min(Math.Abs(relativeRadians), Math.PI * 0.95);
            var arc = total * radius;
            double forward, side, turned;
            if (alongMetres <= arc)
            {
                turned = alongMetres / radius;
                forward = radius * Math.Sin(turned);
                side = radius * (1.0 - Math.Cos(turned));
            }
            else
            {
                turned = total;
                var beyond = alongMetres - arc;
                forward = radius * Math.Sin(total) + beyond * Math.Cos(total);
                side = radius * (1.0 - Math.Cos(total)) + beyond * Math.Sin(total);
            }

            return ((float)forward, (float)(-sign * side), (float)(sign * turned * 180.0 / Math.PI));
        }

        public static (float forward, float sideways, float yawDegrees) Arc(RunwayDirection runway, Destination home,
            Destination destination, float radius, float alongMetres) =>
            Arc(RelativeRadians(runway, home, destination), radius, alongMetres);

        /// <summary>
        /// Bank through the arc: rolled in over <see cref="RollMetres"/>, held at
        /// <see cref="ArcBankDegrees"/>, rolled out as the heading arrives. Negative for a right turn
        /// (the sign the presentation's bank has always used).
        /// </summary>
        public static float ArcBank(double relativeRadians, float radius, float alongMetres)
        {
            if (alongMetres <= 0f)
                return 0f;
            var total = Math.Min(Math.Abs(relativeRadians), Math.PI * 0.95);
            var arc = (float)(total * radius);
            if (arc < 1f)
                return 0f;
            var roll = Math.Min(RollMetres, arc * 0.5f);
            var rollIn = Smooth01(alongMetres / roll);
            var rollOut = Smooth01((arc - alongMetres) / roll + 0.0f);
            var amount = Math.Min(rollIn, alongMetres >= arc ? 0f : rollOut);
            return (float)(-(relativeRadians < 0 ? -1.0 : 1.0) * ArcBankDegrees * amount);
        }

        public static float ArcBank(RunwayDirection runway, Destination home, Destination destination, float radius,
            float alongMetres) => ArcBank(RelativeRadians(runway, home, destination), radius, alongMetres);

        /// <summary>Bearing to the destination relative to the runway heading, radians (positive right).</summary>
        public static double RelativeRadiansFor(RunwayDirection runway, Destination home, Destination destination) =>
            RelativeRadians(runway, home, destination);

        internal static double RelativeRadians(RunwayDirection runway, Destination home, Destination destination)
        {
            var heading = HeadingRadians(home.Latitude, home.Longitude, destination.Latitude, destination.Longitude);
            var runwayHeading = RunwayWeather.HeadingDegrees(runway) * Math.PI / 180.0;
            var delta = heading - runwayHeading;
            while (delta > Math.PI)
                delta -= 2 * Math.PI;
            while (delta < -Math.PI)
                delta += 2 * Math.PI;
            return delta;
        }

        private static double HeadingRadians(double lat1, double lon1, double lat2, double lon2)
        {
            var phi1 = lat1 * Math.PI / 180.0;
            var phi2 = lat2 * Math.PI / 180.0;
            var dLon = (lon2 - lon1) * Math.PI / 180.0;
            var y = Math.Sin(dLon) * Math.Cos(phi2);
            var x = Math.Cos(phi1) * Math.Sin(phi2) - Math.Sin(phi1) * Math.Cos(phi2) * Math.Cos(dLon);
            return Math.Atan2(y, x);
        }

        private static float Smooth01(float t)
        {
            t = t < 0f ? 0f : t > 1f ? 1f : t;
            return t * t * (3f - 2f * t);
        }
    }
}

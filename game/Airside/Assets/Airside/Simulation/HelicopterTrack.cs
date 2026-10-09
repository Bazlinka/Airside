using System;
using Airside.Domain;

namespace Airside.Simulation
{
    /// <summary>
    /// Where a helicopter is, drawn in the world frame (+X along runway 05, +Z its left): metres on the ground
    /// plane, height above the pad, heading as a Unity yaw, attitude and rotor power (ADR 0207).
    /// </summary>
    public readonly struct HelicopterWorldPose
    {
        public HelicopterWorldPose(bool visible, float x, float z, float heightMetres, float yawDegrees,
            float pitchDegrees, float bankDegrees, float speedMetresPerSecond, float verticalMetresPerSecond,
            float rotorLoad01, float rotorSpeed01, HelicopterSegment segment)
        {
            Visible = visible;
            X = x;
            Z = z;
            HeightMetres = heightMetres;
            YawDegrees = yawDegrees;
            PitchDegrees = pitchDegrees;
            BankDegrees = bankDegrees;
            SpeedMetresPerSecond = speedMetresPerSecond;
            VerticalMetresPerSecond = verticalMetresPerSecond;
            RotorLoad01 = rotorLoad01;
            RotorSpeed01 = rotorSpeed01;
            Segment = segment;
        }

        public bool Visible { get; }
        public float X { get; }
        public float Z { get; }
        public float HeightMetres { get; }

        /// <summary>Unity yaw: 0 faces +Z, 90 faces +X.</summary>
        public float YawDegrees { get; }

        /// <summary>Positive nose-down.</summary>
        public float PitchDegrees { get; }

        /// <summary>Positive rolls to the right.</summary>
        public float BankDegrees { get; }
        public float SpeedMetresPerSecond { get; }
        public float VerticalMetresPerSecond { get; }

        /// <summary>Power the rotor is working at (0..1): sound level and blade pitch.</summary>
        public float RotorLoad01 { get; }

        /// <summary>How fast the rotor turns as a fraction of governed speed: 0 stopped, 1 up to speed.</summary>
        public float RotorSpeed01 { get; }

        public HelicopterSegment Segment { get; }
        public bool OnGround => HeightMetres < 0.05f && SpeedMetresPerSecond < 0.1f;
    }

    public enum HelicopterSegment
    {
        Hidden,
        Parked,
        Takeoff,
        Outbound,
        Inbound,
        Landing,

        /// <summary>Held over a far-off destination (beyond the airfield) for its turnaround.</summary>
        AtSite
    }

    /// <summary>
    /// A helicopter's whole movement as a pure function of its fleet state and the clock (ADR 0207): from the
    /// pad spot, up and round into its departure heading, out along a straight line to the hospital, and the
    /// same line back to a landing on its spot. The whole line is drawn at true geographical scale in the
    /// field's own frame (<see cref="YpadFrame"/>, the frame the flight world, tracker map and regional runways
    /// use), so a helicopter booked to a far destination such as Kingscote really reaches it. The compressed
    /// sky-traffic projection that overflights use would stop it a few tens of kilometres short.
    /// </summary>
    public static class HelicopterTrack
    {
        /// <summary>Cruise height cap above the pad: a helicopter on a short hop stays low.</summary>
        public const float MaxCruiseHeightMetres = 457f;

        /// <summary>Height above the pad at the far end, over the hospital roof.</summary>
        public const float SiteHeightMetres = 35f;

        /// <summary>
        /// A site farther than this from the pad is beyond the airfield (the old 1:1 near field of the sky-traffic
        /// projection): its helicopter is held over it for the turnaround, so the flight world still has a pose at
        /// the geographical destination. Nearer hospitals stay out of sight while the crew is on the ground.
        /// </summary>
        public const float RemoteSiteMetres = (float)(SkyTraffic.NearFieldKm * 1000.0);

        /// <summary>A held helicopter finishes its pedal turn onto the return heading this long before it leaves.</summary>
        private const double SiteTurnLeadSeconds = 20;
        private const double SiteTurnSeconds = 12;

        public static HelicopterWorldPose For(FleetAircraft aircraft, double nowSeconds)
        {
            var spot = SpotOf(aircraft);
            var profile = RotorcraftPerformance.For(aircraft.Type);
            var rotor = RotorcraftEngines.RotorSpeed01(aircraft, nowSeconds);
            var parked = spot.HeadingDegrees;

            switch (aircraft.State)
            {
                case FleetState.AtStand:
                    return Pose(true, spot.X, spot.Z, 0f, parked, 0f, 0f, 0f, 0f, RotorcraftEngines.Idle01(aircraft, nowSeconds),
                        rotor, HelicopterSegment.Parked);

                case FleetState.TakingOff:
                {
                    var heading = FlightHeadingDegrees(spot, SiteWorld(aircraft), outbound: true);
                    var seconds = (float)(nowSeconds - aircraft.StateStartedAt.ElapsedSeconds);
                    var pose = HelicopterFlight.TakeoffPose(profile, seconds);
                    var yaw = LerpAngle(parked, heading, pose.Turn01);
                    var (x, z) = Along(spot, heading, pose.AlongMetres);
                    return Pose(true, x, z, pose.HeightMetres, yaw, pose.PitchDegrees, 0f, pose.SpeedMetresPerSecond,
                        pose.VerticalMetresPerSecond, pose.RotorLoad01, 1f, HelicopterSegment.Takeoff);
                }

                case FleetState.Outbound:
                    return Enroute(aircraft, spot, profile, nowSeconds, outbound: true);

                case FleetState.Inbound:
                    return Enroute(aircraft, spot, profile, nowSeconds, outbound: false);

                case FleetState.Landing:
                {
                    var heading = FlightHeadingDegrees(spot, SiteWorld(aircraft), outbound: false);
                    var seconds = (float)(nowSeconds - aircraft.StateStartedAt.ElapsedSeconds);
                    var pose = HelicopterFlight.LandingPose(profile, seconds);
                    var yaw = LerpAngle(heading, parked, pose.Turn01);
                    var (x, z) = Along(spot, heading, pose.AlongMetres);
                    return Pose(true, x, z, pose.HeightMetres, yaw, pose.PitchDegrees, 0f, pose.SpeedMetresPerSecond,
                        pose.VerticalMetresPerSecond, pose.RotorLoad01, 1f, HelicopterSegment.Landing);
                }

                case FleetState.AtDestination when HoldsAtSite(aircraft):
                {
                    var site = SiteWorld(aircraft);
                    // Facing out along the line it arrived on, then a pedal turn just before the return leg.
                    var arrival = FlightHeadingDegrees(spot, site, outbound: true);
                    var yaw = arrival;
                    if (aircraft.StateEndsAt.HasValue)
                    {
                        var untilLeaving = aircraft.StateEndsAt.Value.ElapsedSeconds - nowSeconds;
                        yaw = LerpAngle(arrival, FlightHeadingDegrees(spot, site, outbound: false),
                            (float)((SiteTurnLeadSeconds - untilLeaving) / SiteTurnSeconds));
                    }

                    return Pose(true, site.X, site.Z, SiteHeightMetres, yaw, 0f, 0f, 0f, 0f, 0.5f, 1f,
                        HelicopterSegment.AtSite);
                }

                default:
                    return Pose(false, spot.X, spot.Z, 0f, parked, 0f, 0f, 0f, 0f, 0f, 0f, HelicopterSegment.Hidden);
            }
        }

        /// <summary>
        /// True while a helicopter turns round at a destination beyond the airfield (a regional town rather than
        /// a nearby hospital): it hovers over the real place, so the flight world keeps a geographical pose.
        /// </summary>
        public static bool HoldsAtSite(FleetAircraft aircraft)
        {
            if (aircraft == null || aircraft.State != FleetState.AtDestination)
                return false;
            var destination = aircraft.CurrentDestination ?? aircraft.Scheduled?.Destination;
            if (!destination.HasValue)
                return false;
            var spot = SpotOf(aircraft);
            var site = SiteWorld(destination.Value);
            var dx = site.X - spot.X;
            var dz = site.Z - spot.Z;
            return dx * dx + dz * dz > RemoteSiteMetres * RemoteSiteMetres;
        }

        /// <summary>The pad spot a helicopter lifts from or lands on; its stand while parked, its departure stand in the air.</summary>
        public static HelipadSpot SpotOf(FleetAircraft aircraft)
        {
            if (AdelaideHelipad.TryGetSpot(aircraft.Stand, out var held))
                return held;
            if (AdelaideHelipad.TryGetSpot(aircraft.DepartureStand, out var left))
                return left;
            return AdelaideHelipad.Spots[0];
        }

        /// <summary>The far end of the leg in the world frame (the hospital or regional town), at true scale.</summary>
        public static (float X, float Z) SiteWorld(FleetAircraft aircraft)
        {
            var destination = aircraft.CurrentDestination ?? aircraft.Scheduled?.Destination;
            if (!destination.HasValue)
                return (AdelaideHelipad.PadCentreX, AdelaideHelipad.PadCentreZ);
            return SiteWorld(destination.Value);
        }

        public static (float X, float Z) SiteWorld(Destination site)
        {
            // The field's own frame, like the pad spots, the regional runways and the tracker map. Not
            // SkyTraffic.ProjectLocal: that compresses everything past 12 km into the draw radius.
            YpadFrame.ToWorld(site.Latitude, site.Longitude, out var x, out var z);
            return ((float)x, (float)z);
        }

        /// <summary>Unity yaw along the flight line: pad toward the site when outbound, site toward the pad inbound.</summary>
        public static float FlightHeadingDegrees(HelipadSpot spot, (float X, float Z) site, bool outbound)
        {
            var dx = site.X - spot.X;
            var dz = site.Z - spot.Z;
            if (dx * dx + dz * dz < 1f)
                return spot.HeadingDegrees;
            var toSite = (float)(Math.Atan2(dx, dz) * 180.0 / Math.PI);
            return outbound ? Normalise(toSite) : Normalise(toSite + 180f);
        }

        private static HelicopterWorldPose Enroute(FleetAircraft aircraft, HelipadSpot spot,
            RotorcraftPerformanceProfile profile, double nowSeconds, bool outbound)
        {
            var site = SiteWorld(aircraft);
            var dx = site.X - spot.X;
            var dz = site.Z - spot.Z;
            var length = (float)Math.Sqrt(dx * dx + dz * dz);
            var headingOut = FlightHeadingDegrees(spot, site, outbound: true);
            var heading = outbound ? headingOut : Normalise(headingOut + 180f);
            if (!aircraft.StateEndsAt.HasValue || length < 1f)
                return Pose(false, spot.X, spot.Z, 0f, heading, 0f, 0f, 0f, 0f, 0f, 1f, HelicopterSegment.Hidden);

            var started = aircraft.StateStartedAt.ElapsedSeconds;
            var duration = Math.Max(1.0, aircraft.StateEndsAt.Value.ElapsedSeconds - started);
            var f = (float)Math.Min(1.0, Math.Max(0.0, (nowSeconds - started) / duration));

            // Outbound: climb-out end to the site. Inbound: the site to where the approach begins, which is
            // ApproachDistance short of the pad on the same line.
            var startAlong = outbound ? profile.ClimbOutDistanceMetres : length;
            var endAlong = outbound ? length : profile.ApproachDistanceMetres;
            var along = startAlong + (endAlong - startAlong) * f;
            var (x, z) = Along(spot, headingOut, along);

            var cruise = Math.Min(MaxCruiseHeightMetres, profile.ClimbOutHeightMetres + 0.03f * length);
            var startHeight = outbound ? profile.ClimbOutHeightMetres : SiteHeightMetres;
            var endHeight = outbound ? SiteHeightMetres : profile.ApproachHeightMetres;
            var height = EnrouteHeight(f, startHeight, endHeight, cruise, duration);
            var vertical = (EnrouteHeight(Math.Min(1f, f + 0.01f), startHeight, endHeight, cruise, duration)
                            - EnrouteHeight(Math.Max(0f, f - 0.01f), startHeight, endHeight, cruise, duration))
                           / (float)(duration * (Math.Min(1f, f + 0.01f) - Math.Max(0f, f - 0.01f)));
            var speed = (float)(Math.Abs(endAlong - startAlong) / duration);
            // Lean into the climb and the descent, a shallow nose-down in the cruise.
            var pitch = Clamp(4f - vertical * 0.9f, -6f, 9f);
            return Pose(true, x, z, height, heading, pitch, 0f, speed, vertical, 0.75f, 1f,
                outbound ? HelicopterSegment.Outbound : HelicopterSegment.Inbound);
        }

        /// <summary>Climb to the cruise height over the first fifth of the leg, descend over the last quarter.</summary>
        private static float EnrouteHeight(float f, float startHeight, float endHeight, float cruise, double duration)
        {
            const float climbEnd = 0.2f;
            const float descentStart = 0.75f;
            if (f < climbEnd)
                return startHeight + (cruise - startHeight) * HelicopterFlight.Smooth(f / climbEnd);
            if (f > descentStart)
                return cruise + (endHeight - cruise) * HelicopterFlight.Smooth((f - descentStart) / (1f - descentStart));
            return cruise;
        }

        private static (float X, float Z) Along(HelipadSpot spot, float yawDegrees, float metres)
        {
            var rad = yawDegrees * Math.PI / 180.0;
            return (spot.X + (float)Math.Sin(rad) * metres, spot.Z + (float)Math.Cos(rad) * metres);
        }

        private static HelicopterWorldPose Pose(bool visible, float x, float z, float height, float yaw, float pitch,
            float bank, float speed, float vertical, float load, float rotor, HelicopterSegment segment) =>
            new(visible, x, z, height, Normalise(yaw), pitch, bank, speed, vertical, load, rotor, segment);

        /// <summary>Interpolates between two compass headings the short way round.</summary>
        public static float LerpAngle(float from, float to, float t)
        {
            var delta = Normalise(to - from + 180f) - 180f;
            return Normalise(from + delta * Clamp(t, 0f, 1f));
        }

        private static float Normalise(float degrees)
        {
            var value = degrees % 360f;
            return value < 0f ? value + 360f : value;
        }

        private static float Clamp(float value, float min, float max) => value < min ? min : value > max ? max : value;
    }

    /// <summary>
    /// Rotor and turbine state for a helicopter on its pad (ADR 0207): spooling up before a departure, running
    /// through a short turnaround, winding down once parked for a while. A pure function of fleet state.
    /// </summary>
    public static class RotorcraftEngines
    {
        /// <summary>Seconds the rotor takes to come up to speed before lift-off.</summary>
        public const double SpoolUpSeconds = 75;

        /// <summary>Seconds the rotor takes to wind down after the engines are shut.</summary>
        public const double SpoolDownSeconds = 60;

        /// <summary>Seconds after landing the crew keeps the engines running before shutting down.</summary>
        public const double CoolDownSeconds = 90;

        /// <summary>Rotor speed as a fraction of governed speed (0 stopped, 1 flying speed).</summary>
        public static float RotorSpeed01(FleetAircraft aircraft, double nowSeconds)
        {
            switch (aircraft.State)
            {
                case FleetState.TakingOff:
                case FleetState.Outbound:
                case FleetState.Inbound:
                case FleetState.Landing:
                    return 1f;
                case FleetState.AtStand:
                    return AtStandRotor(aircraft, nowSeconds);
                case FleetState.AtDestination:
                    return HelicopterTrack.HoldsAtSite(aircraft) ? 1f : 0f;
                default:
                    return 0f;
            }
        }

        /// <summary>Engine power on the pad: idle once the rotor turns, a little more while it spools or winds down.</summary>
        public static float Idle01(FleetAircraft aircraft, double nowSeconds)
        {
            var rotor = RotorSpeed01(aircraft, nowSeconds);
            return rotor <= 0f ? 0f : 0.15f + 0.1f * Math.Min(1f, rotor);
        }

        /// <summary>The fixed-wing engine-state type the lights, beacon and audio passes already understand.</summary>
        public static EngineState StateFor(FleetAircraft aircraft, double nowSeconds)
        {
            var rotor = RotorSpeed01(aircraft, nowSeconds);
            return new EngineState(rotor, rotor, beacon: rotor > 0.05f, doorsOpen: false);
        }

        private static float AtStandRotor(FleetAircraft aircraft, double nowSeconds)
        {
            // Spooling up for a departure that is due.
            if (aircraft.Scheduled is { Cancelled: false } scheduled)
            {
                var untilDeparture = scheduled.DepartAt.ElapsedSeconds - nowSeconds;
                if (untilDeparture <= SpoolUpSeconds)
                    return Smooth((float)(1.0 - Math.Max(0.0, untilDeparture) / SpoolUpSeconds));
            }

            // Just landed: engines still running, then wound down.
            var sinceParked = nowSeconds - aircraft.StateStartedAt.ElapsedSeconds;
            if (aircraft.CompletedTrips > 0 && sinceParked < CoolDownSeconds)
                return 1f;
            if (aircraft.CompletedTrips > 0 && sinceParked < CoolDownSeconds + SpoolDownSeconds)
                return Smooth((float)(1.0 - (sinceParked - CoolDownSeconds) / SpoolDownSeconds));
            return 0f;
        }

        private static float Smooth(float x)
        {
            var t = x < 0f ? 0f : x > 1f ? 1f : x;
            return t * t * (3f - 2f * t);
        }
    }
}

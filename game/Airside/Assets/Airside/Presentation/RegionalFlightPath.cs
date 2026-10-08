using System;
using Airside.Domain;
using Airside.Simulation;

namespace Airside.Presentation
{
    /// <summary>A lightweight distant approach/rollout; no resource reservations or simulation changes.</summary>
    public static class RegionalFlightPath
    {
        /// <summary>
        /// Visual phase of the journey currently being drawn. Inbound simulation state also
        /// describes cruise back to Adelaide; it must not inherit the local approach's flaps/gear.
        /// </summary>
        public static AircraftPhase JourneyPhase(FleetState state, double elapsedSeconds, double legSeconds, double rotateSeconds = 40)
        {
            if (state == FleetState.AtDestination) return AircraftPhase.AtStand;
            if (state == FleetState.Inbound)
                return elapsedSeconds < rotateSeconds ? AircraftPhase.Takeoff : AircraftPhase.Departed;
            if (state == FleetState.Outbound)
            {
                var remaining = legSeconds - elapsedSeconds;
                if (remaining <= RolloutSeconds) return AircraftPhase.Landing;
                if (remaining <= TerminalSeconds) return AircraftPhase.Approach;
            }
            return AircraftPhase.Departed;
        }

        public const double TerminalSeconds=180;
        /// <summary>Distance from touchdown where the glideslope rounds out to zero sink (about 7 s).</summary>
        public const double FlareMetres=300;
        /// <summary>Seconds over which the route's path angle hands over to the authored attitude (and back).</summary>
        public const double AttitudeBlendSeconds=25;
        public const double RolloutSeconds=40;
        public static void Landing(RegionalRunway runway,double homeX,double homeZ,double remaining,
            out double x,out double y,out double z)
        {
            var ax=runway.Ax;var az=runway.Az;var bx=runway.Bx;var bz=runway.Bz;
            // Prefer the threshold nearest home. Weather/runway management remains Adelaide-only.
            if(Math.Pow(bx-homeX,2)+Math.Pow(bz-homeZ,2)<Math.Pow(ax-homeX,2)+Math.Pow(az-homeZ,2))
            { (ax,bx)=(bx,ax);(az,bz)=(bz,az); }
            var length=runway.Length;var dx=(bx-ax)/length;var dz=(bz-az)/length;
            var seconds=Math.Clamp(remaining,0,TerminalSeconds);
            var touchdown=Math.Min(450,length*.3);
            double along,height;
            if(seconds>RolloutSeconds)
            {
                var t=(TerminalSeconds-seconds)/(TerminalSeconds-RolloutSeconds);
                along=-6000+(6000+touchdown)*t;
                // Smooth flare to zero sink at touchdown.
                var distance=6000*(1-t);
                var u=Math.Clamp(distance/FlareMetres,0,1);
                height=distance>=FlareMetres ? distance*CircuitProfile.GlideslopeTangent
                    : FlareMetres*CircuitProfile.GlideslopeTangent*(2*u*u-u*u*u);
            }
            else
            {
                var t=1-seconds/RolloutSeconds;
                along=touchdown+(Math.Min(length-150,touchdown+700)-touchdown)*(2*t-t*t);
                height=0;
            }
            x=ax+dx*along;z=az+dz*along;y=runway.Elevation+AirsideFlightPathDatum+height;
        }

        /// <summary>
        /// Body pitch (codebase sign: negative is nose-up) for the final approach of a regional arrival.
        /// The route's path angle alone drops the nose to ~0 at touchdown, so the flare never showed;
        /// this follows the authored approach attitude, rounds out to the flare attitude over the
        /// same distance the height flares, and fades in from <paramref name="routePitch"/> at the
        /// start of the terminal leg so the cruise-to-approach seam does not snap.
        /// </summary>
        public static float ApproachPitchDegrees(AircraftAttitude attitude, double remaining, float routePitch)
        {
            var seconds=Math.Clamp(remaining,RolloutSeconds,TerminalSeconds);
            var t=(float)((TerminalSeconds-seconds)/(TerminalSeconds-RolloutSeconds));
            var pitch=attitude.ApproachStart+(attitude.ApproachEnd-attitude.ApproachStart)*t;
            var round=1-Math.Clamp((6000*(1-t))/FlareMetres,0,1);
            var f=(float)(round*round*(3-2*round));
            pitch+=(attitude.Flare-pitch)*f;
            var fade=(float)Math.Clamp((TerminalSeconds-remaining)/AttitudeBlendSeconds,0,1);
            fade=fade*fade*(3-2*fade);
            return routePitch+(pitch-routePitch)*fade;
        }

        /// <summary>
        /// Body pitch for a regional departure from the destination: level for the roll, a smooth
        /// rotation to the climb attitude, then a hand-over to the route's own path angle.
        /// </summary>
        public static float DeparturePitchDegrees(AircraftAttitude attitude,AircraftPerformanceProfile profile,
            double seconds,double rotateSeconds,float routePitch)
        {
            if(seconds<=rotateSeconds)return 0;
            var span=Math.Max(1,DepartureSeconds-rotateSeconds);
            var progress=profile.RotateProgress+(1-profile.RotateProgress)*Math.Clamp((seconds-rotateSeconds)/span,0,1);
            var pitch=attitude.PitchDegrees(AircraftPhase.Takeoff,(float)progress,profile);
            var fade=(float)Math.Clamp((seconds-DepartureSeconds)/AttitudeBlendSeconds,0,1);
            fade=fade*fade*(3-2*fade);
            return pitch+(routePitch-pitch)*fade;
        }

        public const double DepartureSeconds=120;
        public static void Departure(RegionalRunway runway,double seconds,double exitHeight,
            out double x,out double y,out double z)
        {
            Landing(runway,0,0,0,out var px,out var py,out var pz);
            // Roll back along the runway toward Adelaide, after the destination turnaround.
            var ax=runway.Ax;var az=runway.Az;var bx=runway.Bx;var bz=runway.Bz;
            if(bx*bx+bz*bz<ax*ax+az*az){(ax,bx)=(bx,ax);(az,bz)=(bz,az);}
            var dx=(ax-bx)/runway.Length;var dz=(az-bz)/runway.Length;
            var t=Math.Clamp(seconds,0,DepartureSeconds);
            var roll=Math.Min(800,runway.Length*.55);
            var along=t<40 ? roll*Math.Pow(t/40,2) : roll+(t-40)*(roll/20);
            var climb=Math.Clamp((t-40)/(DepartureSeconds-40),0,1);
            x=px+dx*along;z=pz+dz*along;
            y=py+(Math.Max(py,exitHeight)-py)*climb*climb*(3-2*climb);
        }
        /// <summary>Type-aware regional takeoff; runway roll integrates that type's Vr, then climb speed
        /// increases to its climb-out target. Existing fixed review/handoff duration remains unchanged.</summary>
        public static double RotateSeconds(AircraftType type) => AircraftPerformance.For(type).TakeoffRollExactSeconds;
        public static void Departure(RegionalRunway runway,double seconds,double exitHeight,AircraftType type,
            out double x,out double y,out double z)
        {
            Landing(runway,0,0,0,out var px,out var py,out var pz);
            var ax=runway.Ax;var az=runway.Az;var bx=runway.Bx;var bz=runway.Bz;
            if(bx*bx+bz*bz<ax*ax+az*az){(ax,bx)=(bx,ax);(az,bz)=(bz,az);}
            var dx=(ax-bx)/runway.Length;var dz=(az-bz)/runway.Length;
            var p=AircraftPerformance.For(type);
            var t=Math.Clamp(seconds,0,DepartureSeconds);var rotate=RotateSeconds(type);
            double along,height;
            if(t<=rotate)
            {
                along=p.TakeoffRollMetres*AircraftPerformanceProfile.TakeoffRollDistance01((float)(t/rotate));height=0;
            }
            else
            {
                var duration=DepartureSeconds-rotate;var elapsed=t-rotate;var u=elapsed/duration;
                var v0=CircuitProfile.Knots(p.RotateKnots);var v1=CircuitProfile.Knots(p.ClimbOutKnots);
                along=p.TakeoffRollMetres+v0*elapsed+.5*(v1-v0)*elapsed*elapsed/duration;
                var rise=Math.Max(0,exitHeight-py);
                // Zero sink at rotation; capture the researched route height smoothly at the join.
                var endRate=Math.Min(p.ClimbOutRateMetresPerSecond,2*rise/duration);
                height=(-2*u*u*u+3*u*u)*rise+(u*u*u-u*u)*duration*endRate;
            }
            x=px+dx*along;z=pz+dz*along;y=py+height;
        }

        public const double AirsideFlightPathDatum=.72;
    }
}

using System;
using System.Collections.Generic;
using Airside.Domain;

namespace Airside.Simulation
{
    /// <summary>
    /// Where every fleet aircraft on the ground is at a given moment, exactly as it is drawn,
    /// and a ground controller check built on it. Taxi legs are fixed paths on fixed clocks, so
    /// before this nothing stopped a pushback taxiing head-on into an arrival on the same
    /// taxiway, or a taxi-out driving into the aircraft already holding short. Now a pushback
    /// or a taxi-in waits (at the stand, or at the runway exit) until its whole route is clear
    /// of everything else moving, and a taxi-out stops behind the holding-point queue.
    /// </summary>
    public static class GroundTraffic
    {
        /// <summary>A pushback or taxi-in that had to wait moves only on this grid, so the
        /// outcome does not depend on how often the simulation is stepped.</summary>
        public const long GridSeconds = 5;

        /// <summary>After this long, stop waiting for aircraft that are standing still (see <see cref="PathClear"/>).</summary>
        public const long MaxWaitSeconds = 180;

        private const double SampleSeconds = 2.0;

        /// <summary>The same metres the queue positions step back by.</summary>
        public static float QueueSpacingMetres => AdelaideGround.AwaitingSpacingMetres;

        public static bool OnGrid(SimulationTime now) => now.ElapsedSeconds % GridSeconds == 0;

        public static SimulationTime NextGrid(SimulationTime now) =>
            new((now.ElapsedSeconds / GridSeconds + 1) * GridSeconds);

        /// <summary>Two aircraft this close would be drawn with overlapping airframes.</summary>
        public static bool TooClose(GroundPose a, double aHalfSpan, GroundPose b, double bHalfSpan)
        {
            var dx = a.X - b.X;
            var dz = a.Z - b.Z;
            // Not the full wingspans: aircraft pass each other on parallel taxiways and stand
            // at neighbouring stops. This catches airframes actually driving through each other.
            var clear = 0.85 * (aHalfSpan + bHalfSpan) + 2.0;
            return dx * dx + dz * dz < clear * clear;
        }

        public static double HalfSpan(AircraftType type) =>
            type != null && AircraftCatalogue.TryFor(type, out var spec) ? spec.WingspanMetres * 0.5 : 14.0;

        /// <summary>
        /// Ground pose of <paramref name="aircraft"/> at <paramref name="seconds"/>, or false when it is
        /// parked, airborne or away. Legs past their end hold their final pose.
        /// </summary>
        public static bool TryPose(IReadOnlyList<FleetAircraft> fleet, FleetAircraft aircraft, double seconds,
            out GroundPose pose, out FleetGroundLeg leg) =>
            TryPose(fleet, aircraft, seconds, null, out pose, out leg);

        /// <summary>Queue positions worked out once for a whole <see cref="PathClear"/> check.</summary>
        private sealed class Track
        {
            public FleetAircraft Aircraft;
            public int Slot;
            public int Ahead;
            public int ExitAhead;
            public double HalfSpan;
            public bool SameQueue;
            // State reused across samples: recomputed only when the leg it was built for ends.
            public FleetVisual? Visual;
            public double ValidUntil;
            public GroundPose? StandingPose;
            public readonly Dictionary<FleetGroundLeg, GroundLeg> Legs = new();
        }

        private static bool TryPose(IReadOnlyList<FleetAircraft> fleet, FleetAircraft aircraft, double seconds,
            Track track, out GroundPose pose, out FleetGroundLeg leg)
        {
            pose = default;
            var at = new SimulationTime((long)Math.Floor(seconds));
            FleetVisual visual;
            if (track != null && track.Visual.HasValue && seconds < track.ValidUntil)
            {
                visual = track.Visual.Value;
            }
            else
            {
                visual = FleetVisual.For(aircraft, at);
                if (track != null)
                {
                    track.Visual = visual;
                    track.StandingPose = null;
                    track.ValidUntil = visual.Leg switch
                    {
                        FleetGroundLeg.HoldingShort or FleetGroundLeg.AwaitingStand or FleetGroundLeg.Parked => double.MaxValue,
                        FleetGroundLeg.None => seconds + 4.0,
                        _ => visual.LegStartedAt.ElapsedSeconds + visual.LegSeconds
                    };
                }
            }

            leg = visual.Leg;
            if (!visual.Visible)
                return false;
            if (track?.StandingPose is { } standing)
            {
                pose = standing;
                return true;
            }
            switch (visual.Leg)
            {
                case FleetGroundLeg.None:
                    return TryRunwayGroundPose(aircraft, visual, seconds, out pose);
                case FleetGroundLeg.Parked:
                    return false;
                case FleetGroundLeg.HoldingShort:
                    pose = AdelaideGround.HoldingShortPose(aircraft.DepartureStand,
                        track?.Slot ?? FleetVisual.QueueSlot(fleet, aircraft, at), aircraft.AssignedRunway, aircraft.Type);
                    if (track != null)
                        track.StandingPose = pose;
                    return true;
                case FleetGroundLeg.AwaitingStand:
                    pose = AdelaideGround.AwaitingPose(track?.Slot ?? FleetVisual.QueueSlot(fleet, aircraft, at),
                        aircraft.Type, aircraft.AssignedRunway);
                    if (track != null)
                        track.StandingPose = pose;
                    return true;
                default:
                {
                    GroundLeg ground;
                    if (track == null)
                        ground = LegFor(aircraft, visual.Leg);
                    else if (!track.Legs.TryGetValue(visual.Leg, out ground))
                        track.Legs[visual.Leg] = ground = LegFor(aircraft, visual.Leg);
                    var elapsed = seconds - visual.LegStartedAt.ElapsedSeconds;
                    var scale = visual.LegSeconds > 0 ? ground.Seconds / visual.LegSeconds : 1.0;
                    var t = elapsed * scale;
                    if (visual.Leg == FleetGroundLeg.TaxiOut)
                        t = Math.Min(t, QueuedSeconds(ground, track?.Ahead ?? FleetVisual.QueueAhead(fleet, aircraft, at)));
                    else if (visual.Leg == FleetGroundLeg.Vacate)
                        t = Math.Min(t, QueuedSeconds(ground,
                            track?.ExitAhead ?? FleetVisual.ExitQueueAhead(fleet, aircraft, at)));
                    if (track != null)
                    {
                        // Positions only for conflict checks: the table is far cheaper than a pose.
                        var (px, pz) = ground.PositionAt(t);
                        pose = new GroundPose(px, pz, 0f, 1f, 0f, false);
                    }
                    else
                    {
                        pose = ground.PoseAt(t);
                    }
                    return true;
                }
            }
        }

        /// <summary>
        /// A takeoff or landing is still ground traffic until rotation or touchdown/rollout ends.
        /// FleetVisual deliberately describes those parts as airborne phases, so ground control
        /// reconstructs just the wheels-on-runway portion here without depending on Presentation.
        /// </summary>
        private static bool TryRunwayGroundPose(FleetAircraft aircraft, FleetVisual visual, double seconds,
            out GroundPose pose)
        {
            pose = default;
            if (visual.Phase is not (AircraftPhase.Landing or AircraftPhase.Takeoff))
                return false;

            var profile = AircraftPerformance.For(aircraft.Type);
            var duration = visual.Phase == AircraftPhase.Landing ? profile.LandingSeconds : profile.TakeoffSeconds;
            if (duration <= 0)
                return false;
            var progress = Clamp01((seconds - visual.PhaseStartedAt.ElapsedSeconds) / duration);
            float localX;
            if (visual.Phase == AircraftPhase.Landing)
            {
                if (progress < profile.TouchdownProgress)
                    return false;
                var rollout = DistanceFraction(Local(progress, profile.TouchdownProgress, 1.0),
                    profile.TouchdownKnots, profile.RunwayExitKnots);
                localX = Lerp(CircuitProfile.TouchdownX, CircuitProfile.RolloutEndX, rollout);
            }
            else
            {
                if (progress > profile.RotateProgress)
                    return false;
                var roll = DistanceFraction(Local(progress, 0.0, profile.RotateProgress), 0.0,
                    profile.RotateKnots);
                localX = Lerp(CircuitProfile.TakeoffStartX, profile.RotateX, roll);
            }

            RunwayFrame.ToWorld(aircraft.AssignedRunway, localX, 0f, 0f, out var x, out _, out var z);
            RunwayFrame.Forward(aircraft.AssignedRunway, out var fx, out var fz);
            pose = new GroundPose(x, z, fx, fz, 0f, false);
            return true;
        }

        private static double DistanceFraction(double progress, double startSpeed, double endSpeed)
        {
            var mean = (startSpeed + endSpeed) * 0.5;
            return mean <= 0.0001
                ? Clamp01(progress)
                : Clamp01((startSpeed * progress + (endSpeed - startSpeed) * progress * progress * 0.5) / mean);
        }

        private static double Local(double value, double from, double to) =>
            to <= from ? 0.0 : Clamp01((value - from) / (to - from));

        private static double Clamp01(double value) => value < 0.0 ? 0.0 : value > 1.0 ? 1.0 : value;
        private static float Lerp(float a, float b, double t) => (float)(a + (b - a) * Clamp01(t));

        /// <summary>How far into a taxi-out it goes before stopping behind <paramref name="ahead"/> aircraft.</summary>
        public static double QueuedSeconds(GroundLeg taxiOut, int ahead) =>
            ahead <= 0 ? taxiOut.Seconds : taxiOut.SecondsShortOfEnd(QueueSpacingMetres * ahead);

        public static GroundLeg LegFor(FleetAircraft aircraft, FleetGroundLeg leg) => leg switch
        {
            FleetGroundLeg.TaxiOut => AdelaideGround.TaxiOut(aircraft.DepartureStand, aircraft.Type, aircraft.AssignedRunway),
            FleetGroundLeg.Lineup => AdelaideGround.LineupFor(aircraft.AssignedRunway),
            FleetGroundLeg.Vacate => AdelaideGround.VacateFor(aircraft.Type, aircraft.AssignedRunway),
            _ => AdelaideGround.TaxiIn(aircraft.Stand, aircraft.Type, aircraft.AssignedRunway)
        };

        /// <summary>
        /// True when <paramref name="leg"/>, started by <paramref name="candidate"/> at
        /// <paramref name="start"/>, stays clear of every other aircraft on the ground for its whole
        /// length. A taxi-out ignores the queue for its own runway: it stops behind it instead.
        /// </summary>
        /// <param name="includeStationary">
        /// Also keep clear of aircraft standing still (holding short, waiting for a stand). Those can
        /// wait indefinitely — a player's aircraft with no stand — so after
        /// <see cref="MaxWaitSeconds"/> the caller stops waiting for them. Moving traffic always
        /// finishes its leg, so it is always respected.
        /// </param>
        public static bool PathClear(IReadOnlyList<FleetAircraft> fleet, FleetAircraft candidate, GroundLeg leg,
            RunwayDirection candidateRunway, bool taxiOut, SimulationTime start, bool includeStationary = true)
        {
            if (fleet == null || candidate == null || leg == null)
                return true;
            var half = HalfSpan(candidate.Type);
            var others = new List<Track>();
            var sameQueueCount = 0;
            // Only aircraft whose whole route comes anywhere near this one can clash with it.
            var mineBox = Grow(leg.Bounds, 80f);
            foreach (var other in fleet)
            {
                if (ReferenceEquals(other, candidate) || !OnTheGround(other))
                    continue;
                if (!Overlaps(mineBox, RouteBounds(fleet, other, start)))
                    continue;
                if (!includeStationary && other.State is FleetState.HoldingShort or FleetState.AwaitingStand)
                    continue;
                var sameQueue = taxiOut && other.AssignedRunway == candidateRunway
                                && other.State is FleetState.TaxiOut or FleetState.HoldingShort or FleetState.TakingOff;
                if (sameQueue)
                    sameQueueCount++;
                others.Add(new Track
                {
                    Aircraft = other,
                    Slot = other.State is FleetState.HoldingShort or FleetState.AwaitingStand
                        ? FleetVisual.QueueSlot(fleet, other, start) : 0,
                    Ahead = other.State == FleetState.TaxiOut ? FleetVisual.QueueAhead(fleet, other, start) : 0,
                    ExitAhead = other.State == FleetState.Landing ? FleetVisual.ExitQueueAhead(fleet, other, start) : 0,
                    HalfSpan = HalfSpan(other.Type),
                    SameQueue = sameQueue
                });
            }

            if (others.Count == 0)
                return true;
            // Taxi-outs to the same runway end up queued nose-to-tail at its holding point; that
            // last stretch is the queue's business, not a conflict. Before it, a later pushback
            // must not catch up with or push into one already on the way.
            var queueZoneFrom = taxiOut
                ? leg.SecondsShortOfEnd(QueueSpacingMetres * (sameQueueCount + 1))
                : double.MaxValue;
            for (var s = 0.0; s <= leg.Seconds + 1e-6; s += SampleSeconds)
            {
                var (mx, mz) = leg.PositionAt(s);
                var mine = new GroundPose(mx, mz, 0f, 1f, 0f, false);
                var when = start.ElapsedSeconds + s;
                foreach (var other in others)
                {
                    if (s >= queueZoneFrom && other.SameQueue)
                        continue;
                    var got = TryPose(fleet, other.Aircraft, when, other, out var theirs, out _);
                    if (!got)
                        continue;
                    if (TooClose(mine, half, theirs, other.HalfSpan))
                        return false;
                }
            }

            return true;
        }

        /// <summary>Box around everywhere <paramref name="aircraft"/> can be on the ground in its current state.</summary>
        private static (float MinX, float MinZ, float MaxX, float MaxZ) RouteBounds(IReadOnlyList<FleetAircraft> fleet,
            FleetAircraft aircraft, SimulationTime at)
        {
            var leg = aircraft.State switch
            {
                FleetState.TaxiOut => FleetGroundLeg.TaxiOut,
                FleetState.TaxiIn => FleetGroundLeg.TaxiIn,
                _ => FleetGroundLeg.None
            };
            if (leg != FleetGroundLeg.None)
                return LegFor(aircraft, leg).Bounds;
            // These states move between a runway roll and a ground leg. Keep them in the
            // candidate set; TryPose supplies the exact position for every sample.
            if (aircraft.State is FleetState.TakingOff or FleetState.Landing)
                return (-2200f, -2200f, 2200f, 2200f);
            // Standing still: its queue place, with room for the queue to shuffle up.
            if (!TryPose(fleet, aircraft, at.ElapsedSeconds, out var pose, out _))
                return (float.MaxValue, float.MaxValue, float.MinValue, float.MinValue);
            return Grow((pose.X, pose.Z, pose.X, pose.Z), QueueSpacingMetres * 3f);
        }

        private static (float MinX, float MinZ, float MaxX, float MaxZ) Grow(
            (float MinX, float MinZ, float MaxX, float MaxZ) box, float by) =>
            (box.MinX - by, box.MinZ - by, box.MaxX + by, box.MaxZ + by);

        private static bool Overlaps((float MinX, float MinZ, float MaxX, float MaxZ) a,
            (float MinX, float MinZ, float MaxX, float MaxZ) b) =>
            a.MinX <= b.MaxX && b.MinX <= a.MaxX && a.MinZ <= b.MaxZ && b.MinZ <= a.MaxZ;

        private static bool OnTheGround(FleetAircraft aircraft) => aircraft.State is FleetState.TaxiOut
            or FleetState.HoldingShort or FleetState.TakingOff or FleetState.Landing
            or FleetState.AwaitingStand or FleetState.TaxiIn;
    }
}

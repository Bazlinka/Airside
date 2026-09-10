using Airside.Simulation;
using UnityEngine;

namespace Airside.Presentation
{
    /// <summary>
    /// Maps a taxi phase onto the same length-weighted segment windows used by
    /// CommercialFlight.SegmentFor. Geometry only affects movement inside the
    /// currently reserved segment; it cannot make a model visually cross early.
    ///
    /// Position is parameterised by distance along the route, in both directions, so
    /// taxi speed is constant. Mirroring only the segment index used to spend the long
    /// Alpha leg's share of the phase crawling the short lead-in and then race the rest.
    /// Corners are filleted so an aircraft arcs through a turn instead of pivoting on
    /// the spot at every waypoint.
    /// </summary>
    public static class TaxiVisualPath
    {
        /// <summary>Largest turn radius used to round a route corner, in metres.</summary>
        public const float MaxCornerFillet = 3f;

        public static Vector3 PositionAt(TaxiRoute route, float phaseProgress, bool reverse, float height = 0.7f)
        {
            var clamped = Mathf.Clamp01(phaseProgress);
            return PositionAtForward(route, reverse ? 1f - clamped : clamped, height);
        }

        /// <summary>Position at a 0..1 distance fraction measured from the route start.</summary>
        public static Vector3 PositionAtForward(TaxiRoute route, float forwardProgress, float height = 0.7f)
        {
            return PositionAtDistance(route, Mathf.Clamp01(forwardProgress) * route.TotalLength, height);
        }

        /// <summary>
        /// Where taxi-out begins: the apron throat, because pushback has already moved
        /// the aircraft off the stand. Replaying the lead-in teleported it back into the
        /// bay at the start of every departure.
        /// </summary>
        public static float TaxiOutStartProgress(TaxiRoute route)
        {
            var last = route.SegmentLengths[route.SegmentLengths.Count - 1];
            return Mathf.Clamp01((route.TotalLength - last) / route.TotalLength);
        }

        /// <summary>
        /// Where a departure holds short: on the first segment, at the point that clears
        /// the runway edge. Taxi-out used to run all the way onto the runway centreline,
        /// so a departure waiting for clearance stood in the next arrival's rollout.
        /// </summary>
        public static float HoldShortProgress(TaxiRoute route)
        {
            return AirportTaxiNetwork.RunwayHoldingProgress(route);
        }

        /// <summary>
        /// Pushback runs from the stand box back onto the apron throat, easing to a stop
        /// before the tug disconnects. Both endpoints come from the taxi path itself so
        /// the handover into taxi-out cannot open a gap: the throat is a route corner,
        /// and the filleted path rounds it a little short of the raw waypoint.
        /// </summary>
        public static Vector3 PushbackPosition(TaxiRoute route, float phaseProgress, float height = 0.7f)
        {
            var from = PositionAtForward(route, 1f, height);
            var to = PositionAtForward(route, TaxiOutStartProgress(route), height);
            return Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(phaseProgress)));
        }

        /// <summary>Where an aircraft sits parked on stand, on the taxi path.</summary>
        public static Vector3 StandPosition(TaxiRoute route, float height = 0.7f)
        {
            return PositionAtForward(route, 1f, height);
        }

        /// <summary>Taxi-out runs from the throat back to the runway hold-short point.</summary>
        public static Vector3 TaxiOutPosition(TaxiRoute route, float phaseProgress, float height = 0.7f)
        {
            var forward = Mathf.Lerp(
                TaxiOutStartProgress(route),
                HoldShortProgress(route),
                Mathf.Clamp01(phaseProgress));
            return PositionAtForward(route, forward, height);
        }

        public static Vector3 PositionAtDistance(TaxiRoute route, float distance, float height = 0.7f)
        {
            var count = route.SegmentLengths.Count;
            var travelled = Mathf.Clamp(distance, 0f, route.TotalLength);

            var index = 0;
            var accrued = 0f;
            while (index < count - 1 && travelled >= accrued + route.SegmentLengths[index])
            {
                accrued += route.SegmentLengths[index];
                index++;
            }

            var local = travelled - accrued;
            var length = route.SegmentLengths[index];
            var from = Point(route, index, height);
            var to = Point(route, index + 1, height);
            var fillet = FilletFor(route);

            // Second half of the turn at the waypoint this segment starts from.
            if (index > 0 && local < fillet)
                return CornerArc(Point(route, index - 1, height), from, to, fillet,
                    0.5f + 0.5f * local / fillet);

            // First half of the turn at the waypoint this segment ends on.
            if (index < count - 1 && local > length - fillet)
                return CornerArc(from, to, Point(route, index + 2, height), fillet,
                    0.5f * (local - (length - fillet)) / fillet);

            return Vector3.Lerp(from, to, length <= 0f ? 0f : local / length);
        }

        public static Vector3 MoveGroundTraffic(Vector3 previous, Vector3 target, bool isHolding, float maxDistanceDelta)
        {
            // A yielded aircraft no longer owns the path between these points, so
            // interpolation would be visually unsafe even though the simulation is safe.
            // Large retargets (stand change / off-field snap) also snap rather than cut grass.
            if (isHolding)
                return target;

            if ((target - previous).sqrMagnitude > maxDistanceDelta * maxDistanceDelta * 64f)
                return target;

            return Vector3.MoveTowards(previous, target, maxDistanceDelta);
        }

        private static Vector3 Point(TaxiRoute route, int index, float height)
        {
            var p = route.Points[index];
            return new Vector3(p.X, height, p.Z);
        }

        /// <summary>Never more than 40% of the shortest leg, so two fillets cannot overlap.</summary>
        private static float FilletFor(TaxiRoute route)
        {
            var shortest = float.MaxValue;
            for (var i = 0; i < route.SegmentLengths.Count; i++)
                shortest = Mathf.Min(shortest, route.SegmentLengths[i]);
            return Mathf.Min(MaxCornerFillet, shortest * 0.4f);
        }

        private static Vector3 CornerArc(Vector3 from, Vector3 corner, Vector3 to, float fillet, float w)
        {
            var entry = corner + (from - corner).normalized * fillet;
            var exit = corner + (to - corner).normalized * fillet;
            var t = Mathf.Clamp01(w);
            var omt = 1f - t;
            return omt * omt * entry + 2f * omt * t * corner + t * t * exit;
        }
    }
}

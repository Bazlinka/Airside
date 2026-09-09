using Airside.Simulation;
using UnityEngine;

namespace Airside.Presentation
{
    /// <summary>
    /// Maps a taxi phase onto the same length-weighted segment windows used by
    /// CommercialFlight.SegmentFor. Geometry only affects movement inside the
    /// currently reserved segment; it cannot make a model visually cross early.
    /// Ground progress is linear along the chord so centreline paint stays honest.
    /// </summary>
    public static class TaxiVisualPath
    {
        public static Vector3 PositionAt(TaxiRoute route, float phaseProgress, bool reverse, float height = 0.7f)
        {
            var clamped = Mathf.Clamp01(phaseProgress);
            var forwardIndex = route.ForwardSegmentIndex(clamped);
            var localT = route.LocalT(clamped);
            var segmentIndex = reverse ? route.SegmentIds.Count - 1 - forwardIndex : forwardIndex;

            var from = route.Points[reverse ? segmentIndex + 1 : segmentIndex];
            var to = route.Points[reverse ? segmentIndex : segmentIndex + 1];

            return Vector3.Lerp(
                new Vector3(from.X, height, from.Z),
                new Vector3(to.X, height, to.Z),
                localT);
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
    }
}

using Airside.Simulation;
using UnityEngine;

namespace Airside.Presentation
{
    /// <summary>
    /// Maps a taxi phase onto the same equal-duration segment windows used by
    /// CommercialFlight.SegmentFor. Geometry only affects movement inside the
    /// currently reserved segment; it cannot make a model visually cross early.
    /// </summary>
    public static class TaxiVisualPath
    {
        public static Vector3 PositionAt(TaxiRoute route, float phaseProgress, bool reverse, float height = 0.7f)
        {
            var segmentCount = route.SegmentIds.Count;
            var scaled = Mathf.Clamp01(phaseProgress) * segmentCount;
            var travelIndex = Mathf.Min(segmentCount - 1, Mathf.FloorToInt(scaled));
            var localProgress = Mathf.Clamp01(scaled - travelIndex);

            var fromIndex = reverse ? segmentCount - travelIndex : travelIndex;
            var toIndex = reverse ? fromIndex - 1 : fromIndex + 1;
            var from = route.Points[fromIndex];
            var to = route.Points[toIndex];
            var eased = Mathf.SmoothStep(0f, 1f, localProgress);

            return Vector3.Lerp(
                new Vector3(from.X, height, from.Z),
                new Vector3(to.X, height, to.Z),
                eased);
        }

        public static Vector3 MoveGroundTraffic(Vector3 previous, Vector3 target, bool isHolding, float maxDistanceDelta)
        {
            // A yielded aircraft no longer owns the path between these points, so
            // interpolation would be visually unsafe even though the simulation is safe.
            return isHolding ? target : Vector3.MoveTowards(previous, target, maxDistanceDelta);
        }
    }
}

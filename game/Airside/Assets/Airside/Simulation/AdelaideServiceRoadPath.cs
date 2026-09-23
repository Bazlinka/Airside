using System;

namespace Airside.Simulation
{
    /// <summary>
    /// Path queries over the baked <see cref="AdelaideServiceRoads.ApronFrontage"/> polyline.
    ///
    /// The road itself is generated from OpenStreetMap; this is the arithmetic a vehicle
    /// needs to drive it — nearest entry point, distance along, and where a given distance
    /// lands. Separate from the generated file so regenerating the road never overwrites it.
    ///
    /// No UnityEngine types: the headless harness checks the routing contract.
    /// </summary>
    public static class AdelaideServiceRoadPath
    {
        public static int PointCount => AdelaideServiceRoads.ApronFrontage.Length / 2;

        public static void PointAt(int index, out float x, out float z)
        {
            var road = AdelaideServiceRoads.ApronFrontage;
            var i = Math.Max(0, Math.Min(PointCount - 1, index)) * 2;
            x = road[i];
            z = road[i + 1];
        }

        /// <summary>
        /// The frontage never passes under anything. It runs about 91 m off the terminal's
        /// airside wall — 44 m clear even of a fully extended aerobridge — so this is always
        /// false, and is kept only so callers can ask the same question of either road.
        /// </summary>
        public static bool IsUndercroft(int index) => false;

        // ---- the authored undercroft spur ------------------------------------------------

        public static int SpurPointCount => AdelaideServiceRoads.UndercroftSpur.Length / 2;

        public static void SpurPointAt(int index, out float x, out float z)
        {
            var spur = AdelaideServiceRoads.UndercroftSpur;
            var i = Math.Max(0, Math.Min(SpurPointCount - 1, index)) * 2;
            x = spur[i];
            z = spur[i + 1];
        }

        /// <summary>True where the spur has passed beneath the terminal's airside wall.</summary>
        public static bool IsSpurUndercroft(int index) =>
            index >= AdelaideServiceRoads.UndercroftFirstIndex
            && index <= AdelaideServiceRoads.UndercroftLastIndex;

        /// <summary>Metres along the spur between two indices.</summary>
        public static float SpurDistanceBetween(int fromIndex, int toIndex)
        {
            var lo = Math.Min(fromIndex, toIndex);
            var hi = Math.Max(fromIndex, toIndex);
            var total = 0f;
            for (var i = lo; i < hi; i++)
            {
                SpurPointAt(i, out var ax, out var az);
                SpurPointAt(i + 1, out var bx, out var bz);
                var dx = bx - ax;
                var dz = bz - az;
                total += (float)Math.Sqrt(dx * dx + dz * dz);
            }

            return total;
        }

        /// <summary>Position a fraction of the way along the spur, following it exactly.</summary>
        public static void TravelSpur(int fromIndex, int toIndex, float t,
            out float x, out float z, out bool underTerminal)
        {
            t = t < 0f ? 0f : t > 1f ? 1f : t;
            var span = SpurDistanceBetween(fromIndex, toIndex);
            var target = span * t;
            var step = toIndex >= fromIndex ? 1 : -1;
            var travelled = 0f;
            var index = fromIndex;

            while (index != toIndex)
            {
                var next = index + step;
                SpurPointAt(index, out var ax, out var az);
                SpurPointAt(next, out var bx, out var bz);
                var dx = bx - ax;
                var dz = bz - az;
                var seg = (float)Math.Sqrt(dx * dx + dz * dz);
                if (travelled + seg >= target || seg <= 1e-6f)
                {
                    var k = seg <= 1e-6f ? 0f : (target - travelled) / seg;
                    x = ax + dx * k;
                    z = az + dz * k;
                    underTerminal = IsSpurUndercroft(index) || IsSpurUndercroft(next);
                    return;
                }

                travelled += seg;
                index = next;
            }

            SpurPointAt(toIndex, out x, out z);
            underTerminal = IsSpurUndercroft(toIndex);
        }

        /// <summary>Index of the frontage point closest to a world position.</summary>
        public static int NearestIndex(float x, float z)
        {
            var best = 0;
            var bestSq = float.MaxValue;
            for (var i = 0; i < PointCount; i++)
            {
                PointAt(i, out var px, out var pz);
                var dx = px - x;
                var dz = pz - z;
                var sq = dx * dx + dz * dz;
                if (sq >= bestSq)
                    continue;
                bestSq = sq;
                best = i;
            }

            return best;
        }

        /// <summary>Metres along the frontage between two point indices.</summary>
        public static float DistanceBetween(int fromIndex, int toIndex)
        {
            var lo = Math.Min(fromIndex, toIndex);
            var hi = Math.Max(fromIndex, toIndex);
            var total = 0f;
            for (var i = lo; i < hi; i++)
            {
                PointAt(i, out var ax, out var az);
                PointAt(i + 1, out var bx, out var bz);
                var dx = bx - ax;
                var dz = bz - az;
                total += (float)Math.Sqrt(dx * dx + dz * dz);
            }

            return total;
        }

        /// <summary>
        /// Position a fraction of the way from one frontage index to another, following the
        /// road rather than cutting the corner. <paramref name="t"/> is clamped to 0..1.
        /// </summary>
        public static void Travel(int fromIndex, int toIndex, float t,
            out float x, out float z, out bool underTerminal)
        {
            t = t < 0f ? 0f : t > 1f ? 1f : t;
            var span = DistanceBetween(fromIndex, toIndex);
            var target = span * t;
            var step = toIndex >= fromIndex ? 1 : -1;
            var travelled = 0f;
            var index = fromIndex;

            while (index != toIndex)
            {
                var next = index + step;
                PointAt(index, out var ax, out var az);
                PointAt(next, out var bx, out var bz);
                var dx = bx - ax;
                var dz = bz - az;
                var seg = (float)Math.Sqrt(dx * dx + dz * dz);
                if (travelled + seg >= target || seg <= 1e-6f)
                {
                    var k = seg <= 1e-6f ? 0f : (target - travelled) / seg;
                    x = ax + dx * k;
                    z = az + dz * k;
                    underTerminal = IsUndercroft(index) || IsUndercroft(next);
                    return;
                }

                travelled += seg;
                index = next;
            }

            PointAt(toIndex, out x, out z);
            underTerminal = IsUndercroft(toIndex);
        }
    }
}

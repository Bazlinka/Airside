using System;

namespace Airside.Simulation
{
    /// <summary>
    /// OSM-derived taxi-out polylines sometimes start with a short hook that runs
    /// the wrong way along the taxilane — into the neighbouring stand — before
    /// turning toward the hold. Strip that hook so pushback hands off forward.
    /// </summary>
    public static class TaxiPathCleanup
    {
        /// <summary>
        /// Drop an initial out-and-back along the start of the path. The first and
        /// last vertices stay put so the pushback still meets the taxi-out.
        /// </summary>
        public static float[] WithoutInitialHook(float[] xz)
        {
            if (xz == null || xz.Length < 8)
                return xz;

            var startX = xz[0];
            var startZ = xz[1];
            var endX = xz[xz.Length - 2];
            var endZ = xz[xz.Length - 1];
            var vx = endX - startX;
            var vz = endZ - startZ;
            var v2 = vx * vx + vz * vz;
            if (v2 < 1f)
                return xz;

            var keep = new float[xz.Length];
            keep[0] = startX;
            keep[1] = startZ;
            var written = 2;
            var maxAlong = 0f;
            for (var i = 2; i + 1 < xz.Length; i += 2)
            {
                var dx = xz[i] - startX;
                var dz = xz[i + 1] - startZ;
                var along = (dx * vx + dz * vz) / v2 * Length(vx, vz);
                var fromStart = Length(dx, dz);
                var isEnd = i + 2 >= xz.Length;
                if (!isEnd && fromStart < 80f && along < maxAlong - 0.5f)
                    continue;

                keep[written++] = xz[i];
                keep[written++] = xz[i + 1];
                if (along > maxAlong)
                    maxAlong = along;
            }

            if (written == xz.Length)
                return xz;
            if (written < 4)
                return xz;

            var trimmed = new float[written];
            Array.Copy(keep, trimmed, written);
            return trimmed;
        }

        private static float Length(float x, float z) => (float)Math.Sqrt(x * x + z * z);
    }
}

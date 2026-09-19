using System;

namespace Airside.Simulation
{
    /// <summary>
    /// Maps the 05-authored circuit (x along the strip, z across) onto the
    /// assigned runway. 23 is a 180° flip; 12/30 is a rotate-and-shift onto
    /// the cross strip, with stations remapped so a 3 100 m takeoff roll
    /// still starts at the threshold of the 1 650 m pavement.
    /// </summary>
    public static class RunwayFrame
    {
        public const float MainHalfLength = 1550f;

        public static void ToWorld(RunwayDirection runway, float x, float y, float z,
            out float worldX, out float worldY, out float worldZ)
        {
            worldY = y;
            switch (runway)
            {
                case RunwayDirection.Runway23:
                    worldX = -x;
                    worldZ = -z;
                    return;
                case RunwayDirection.Runway12:
                    AdelaideCrossRoutes.LocalToWorld(RemapAlong(x), z, out worldX, out worldZ);
                    return;
                case RunwayDirection.Runway30:
                    AdelaideCrossRoutes.LocalToWorld(-RemapAlong(x), -z, out worldX, out worldZ);
                    return;
                default:
                    worldX = x;
                    worldZ = z;
                    return;
            }
        }

        /// <summary>Unit takeoff direction in world XZ for this runway end.</summary>
        public static void Forward(RunwayDirection runway, out float x, out float z)
        {
            switch (runway)
            {
                case RunwayDirection.Runway23:
                    x = -1f;
                    z = 0f;
                    return;
                case RunwayDirection.Runway12:
                    AdelaideCrossRoutes.LocalToWorld(1f, 0f, out var x12, out var z12);
                    AdelaideCrossRoutes.LocalToWorld(0f, 0f, out var c12x, out var c12z);
                    x = x12 - c12x;
                    z = z12 - c12z;
                    Normalize(ref x, ref z);
                    return;
                case RunwayDirection.Runway30:
                    AdelaideCrossRoutes.LocalToWorld(-1f, 0f, out var x30, out var z30);
                    AdelaideCrossRoutes.LocalToWorld(0f, 0f, out var c30x, out var c30z);
                    x = x30 - c30x;
                    z = z30 - c30z;
                    Normalize(ref x, ref z);
                    return;
                default:
                    x = 1f;
                    z = 0f;
                    return;
            }
        }

        private static void Normalize(ref float x, ref float z)
        {
            var length = (float)Math.Sqrt(x * x + z * z);
            if (length < 1e-5f)
            {
                x = 1f;
                z = 0f;
                return;
            }

            x /= length;
            z /= length;
        }

        /// <summary>
        /// Metres along 12/30 for a 05-frame station. Past either threshold the
        /// extra distance is kept so a 4 km final stays a 4 km final.
        /// </summary>
        public static float RemapAlong(float mainX)
        {
            var half = AdelaideCrossRoutes.HalfLength;
            if (mainX < -MainHalfLength)
                return -half + (mainX + MainHalfLength);
            if (mainX > MainHalfLength)
                return half + (mainX - MainHalfLength);
            return mainX / MainHalfLength * half;
        }
    }
}

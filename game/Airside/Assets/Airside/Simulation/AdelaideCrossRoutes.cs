using System;

namespace Airside.Simulation
{
    /// <summary>
    /// Taxi, lineup and vacate polylines for the 12/30 strip. Jets stay on
    /// 05/23; regionals use these so they actually reach the short runway
    /// instead of the 05/23 holds.
    /// </summary>
    public static class AdelaideCrossRoutes
    {
        /// <summary>G1 holding point at the 12 threshold (north-east end).</summary>
        public static readonly float[] Hold12 = { 102.1f, 1194.9f };

        /// <summary>D2 holding point at the 30 threshold (south-west end).</summary>
        public static readonly float[] Hold30 = { 563.2f, -348.6f };

        public static float[] Lineup(RunwayDirection runway) =>
            runway == RunwayDirection.Runway30 ? Lineup30() : Lineup12();

        public static float[] Vacate(RunwayDirection runway) =>
            runway == RunwayDirection.Runway30 ? Vacate30() : Vacate12();

        public static float[] TaxiOutFrom(float startX, float startZ, RunwayDirection runway) =>
            runway == RunwayDirection.Runway30
                ? Route(startX, startZ, To30)
                : Route(startX, startZ, To12);

        public static void LocalToWorld(float localX, float localZ, out float x, out float z)
        {
            var yaw = AdelaideLayout.CrossRunwayYawDegrees * Math.PI / 180.0;
            var cos = Math.Cos(yaw);
            var sin = Math.Sin(yaw);
            x = (float)(AdelaideLayout.CrossRunwayCenterX + cos * localX + sin * localZ);
            z = (float)(AdelaideLayout.CrossRunwayCenterZ - sin * localX + cos * localZ);
        }

        public static float HalfLength => AdelaideLayout.CrossRunwayLengthMetres * 0.5f;

        private static readonly float[] To12 =
        {
            920f, 526f,
            507f, 554f,
            488f, 703f,
            331f, 1001f,
            205f, 1174f,
            102.1f, 1194.9f
        };

        private static readonly float[] To30 =
        {
            995f, 294f,
            640f, 196f,
            636f, -3f,
            654f, -94f,
            563.2f, -348.6f
        };

        private static float[] Lineup12()
        {
            Threshold(RunwayDirection.Runway12, 0f, out var tx, out var tz);
            Threshold(RunwayDirection.Runway12, 55f, out var sx, out var sz);
            return GroundPathSmoothing.FilletAndDensify(new[]
            {
                Hold12[0], Hold12[1],
                tx + 28f, tz - 18f,
                sx, sz
            }, 16f, 8f);
        }

        private static float[] Lineup30()
        {
            Threshold(RunwayDirection.Runway30, 0f, out var tx, out var tz);
            Threshold(RunwayDirection.Runway30, 55f, out var sx, out var sz);
            return GroundPathSmoothing.FilletAndDensify(new[]
            {
                Hold30[0], Hold30[1],
                tx - 22f, tz + 16f,
                sx, sz
            }, 16f, 8f);
        }

        private static float[] Vacate12()
        {
            Threshold(RunwayDirection.Runway12, 520f, out var rx, out var rz);
            return GroundPathSmoothing.FilletAndDensify(new[]
            {
                rx, rz,
                rx + 40f, rz - 70f,
                237f, 199f
            }, 20f, 8f);
        }

        private static float[] Vacate30()
        {
            Threshold(RunwayDirection.Runway30, 520f, out var rx, out var rz);
            return GroundPathSmoothing.FilletAndDensify(new[]
            {
                rx, rz,
                rx - 50f, rz + 80f,
                237f, 199f
            }, 20f, 8f);
        }

        private static void Threshold(RunwayDirection runway, float ontoMetres, out float x, out float z)
        {
            var along = runway == RunwayDirection.Runway30
                ? HalfLength - ontoMetres
                : -HalfLength + ontoMetres;
            LocalToWorld(along, 0f, out x, out z);
        }

        private static float[] Route(float startX, float startZ, float[] via)
        {
            var path = new float[2 + via.Length];
            path[0] = startX;
            path[1] = startZ;
            Array.Copy(via, 0, path, 2, via.Length);
            return GroundPathSmoothing.FilletAndDensify(path);
        }
    }
}

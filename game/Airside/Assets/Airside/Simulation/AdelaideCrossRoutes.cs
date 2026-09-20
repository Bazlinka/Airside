using System;

namespace Airside.Simulation
{
    /// <summary>
    /// Taxi, lineup and vacate polylines for the 12/30 strip. Jets stay on
    /// 05/23; regionals use these so they actually reach the short runway
    /// instead of the 05/23 holds. Taxi-out and vacate walk the OSM taxiway
    /// graph — they do not cut a five-point chord across the grass.
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
                ? AdelaideTaxiRouter.Route(startX, startZ, Hold30[0], Hold30[1])
                : AdelaideTaxiRouter.Route(startX, startZ, Hold12[0], Hold12[1]);

        public static void LocalToWorld(float localX, float localZ, out float x, out float z)
        {
            var yaw = AdelaideLayout.CrossRunwayYawDegrees * Math.PI / 180.0;
            var cos = Math.Cos(yaw);
            var sin = Math.Sin(yaw);
            x = (float)(AdelaideLayout.CrossRunwayCenterX + cos * localX + sin * localZ);
            z = (float)(AdelaideLayout.CrossRunwayCenterZ - sin * localX + cos * localZ);
        }

        public static float HalfLength => AdelaideLayout.CrossRunwayLengthMetres * 0.5f;

        /// <summary>
        /// Metres past the arrival threshold where rollout ends — matches
        /// <see cref="RunwayFrame.RemapAlong"/> of <see cref="CircuitProfile.RolloutEndX"/>
        /// so Landing → Vacate does not teleport.
        /// </summary>
        public static float VacateOntoMetres =>
            RunwayFrame.RemapAlong(CircuitProfile.RolloutEndX) + HalfLength;

        /// <summary>
        /// Metres past the departure threshold where the takeoff roll starts —
        /// matches remapped <see cref="CircuitProfile.TakeoffStartX"/>.
        /// </summary>
        public static float LineupOntoMetres =>
            RunwayFrame.RemapAlong(CircuitProfile.TakeoffStartX) + HalfLength;

        private static float[] Lineup12()
        {
            Threshold(RunwayDirection.Runway12, 0f, out var tx, out var tz);
            Threshold(RunwayDirection.Runway12, LineupOntoMetres, out var sx, out var sz);
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
            Threshold(RunwayDirection.Runway30, LineupOntoMetres, out var sx, out var sz);
            return GroundPathSmoothing.FilletAndDensify(new[]
            {
                Hold30[0], Hold30[1],
                tx - 22f, tz + 16f,
                sx, sz
            }, 16f, 8f);
        }

        private static float[] Vacate12()
        {
            Threshold(RunwayDirection.Runway12, VacateOntoMetres, out var rx, out var rz);
            return AdelaideTaxiRouter.Route(rx, rz, AdelaideLayout.E2Hold[0], AdelaideLayout.E2Hold[1]);
        }

        private static float[] Vacate30()
        {
            Threshold(RunwayDirection.Runway30, VacateOntoMetres, out var rx, out var rz);
            return AdelaideTaxiRouter.Route(rx, rz, AdelaideLayout.E2Hold[0], AdelaideLayout.E2Hold[1]);
        }

        private static void Threshold(RunwayDirection runway, float ontoMetres, out float x, out float z)
        {
            var along = runway == RunwayDirection.Runway30
                ? HalfLength - ontoMetres
                : -HalfLength + ontoMetres;
            LocalToWorld(along, 0f, out x, out z);
        }
    }
}

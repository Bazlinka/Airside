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

        /// <summary>Inverse of <see cref="LocalToWorld"/>: metres along and across 12/30.</summary>
        public static void WorldToLocal(float x, float z, out float localX, out float localZ)
        {
            var yaw = AdelaideLayout.CrossRunwayYawDegrees * Math.PI / 180.0;
            var cos = Math.Cos(yaw);
            var sin = Math.Sin(yaw);
            var dx = x - AdelaideLayout.CrossRunwayCenterX;
            var dz = z - AdelaideLayout.CrossRunwayCenterZ;
            localX = (float)(cos * dx - sin * dz);
            localZ = (float)(sin * dx + cos * dz);
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

        /// <summary>
        /// Centreline run-up before the takeoff start. Cutting straight from the entry point
        /// to the start left the nose 30–41° off the runway, then the roll snapped it straight.
        /// </summary>
        private const float LineupStraightMetres = 20f;

        private static float[] Lineup12()
        {
            Threshold(RunwayDirection.Runway12, 0f, out var tx, out var tz);
            Threshold(RunwayDirection.Runway12, LineupOntoMetres - LineupStraightMetres, out var cx, out var cz);
            Threshold(RunwayDirection.Runway12, LineupOntoMetres, out var sx, out var sz);
            return GroundPathSmoothing.FilletAndDensify(new[]
            {
                Hold12[0], Hold12[1],
                tx + 28f, tz - 18f,
                cx, cz,
                sx, sz
            }, 16f, 8f);
        }

        private static float[] Lineup30()
        {
            Threshold(RunwayDirection.Runway30, 0f, out var tx, out var tz);
            Threshold(RunwayDirection.Runway30, LineupOntoMetres - LineupStraightMetres, out var cx, out var cz);
            Threshold(RunwayDirection.Runway30, LineupOntoMetres, out var sx, out var sz);
            return GroundPathSmoothing.FilletAndDensify(new[]
            {
                Hold30[0], Hold30[1],
                tx - 22f, tz + 16f,
                cx, cz,
                sx, sz
            }, 16f, 8f);
        }

        private static float[] _vacate12;
        private static float[] _vacate30;
        private static float[] _corridor;

        private static float[] Vacate12() => _vacate12 ??= VacateToCorridor(RunwayDirection.Runway12);

        private static float[] Vacate30() => _vacate30 ??= VacateToCorridor(RunwayDirection.Runway30);

        /// <summary>
        /// Metres the cross-strip vacate runs on along the shared bay corridor past the
        /// junction, so it ends rolling the way the taxi-in continues.
        /// </summary>
        private const float CorridorLeadMetres = 90f;

        private const float CornerClearMetres = 30f;

        private const float OnCorridorMetres = 3f;

        /// <summary>
        /// Rollout end → onto the E2 → bays corridor, stopping just past the junction.
        /// The route used to run on to the E2 hold itself — back down the very taxiway
        /// every bay's taxi-in then climbs — so a 12/30 arrival drove a few hundred metres
        /// the wrong way, swung its nose through 180° at E2 and drove straight back.
        /// </summary>
        private static float[] VacateToCorridor(RunwayDirection runway)
        {
            Threshold(runway, VacateOntoMetres, out var rx, out var rz);
            var route = AdelaideTaxiRouter.Route(rx, rz, AdelaideLayout.E2Hold[0], AdelaideLayout.E2Hold[1]);
            var corridor = ArrivalCorridor();
            if (corridor.Length < 4)
                return route;

            // First route point from which the rest of the route lies on the corridor.
            var join = route.Length / 2;
            for (var i = route.Length / 2 - 1; i >= 0; i--)
            {
                if (DistanceToPolyline(corridor, route[i * 2], route[i * 2 + 1], out _) > OnCorridorMetres)
                    break;
                join = i;
            }

            if (join >= route.Length / 2 - 1 || join == 0)
                return route;

            DistanceToPolyline(corridor, route[join * 2], route[join * 2 + 1], out var joinAlong);
            var corridorLength = PolylineLength(corridor);
            var endAlong = Math.Min(joinAlong + CorridorLeadMetres, corridorLength - 1f);
            if (endAlong <= joinAlong + 1f)
                return route;

            // The exit meets the corridor angled toward E2, so turning for the bays is a
            // sharp corner. Keep the points either side of it sparse or the fillet collapses
            // to the 8 m point spacing and the aircraft pivots on the spot.
            var jx = route[join * 2];
            var jz = route[join * 2 + 1];
            var points = new System.Collections.Generic.List<float>(join * 2 + 64);
            for (var i = 0; i < join; i++)
            {
                if (i > 0 && Hypot(route[i * 2] - jx, route[i * 2 + 1] - jz) < CornerClearMetres)
                    continue;
                points.Add(route[i * 2]);
                points.Add(route[i * 2 + 1]);
            }

            points.Add(jx);
            points.Add(jz);
            var along = 0f;
            for (var i = 1; i < corridor.Length / 2; i++)
            {
                along += Hypot(corridor[i * 2] - corridor[i * 2 - 2], corridor[i * 2 + 1] - corridor[i * 2 - 1]);
                if (along <= joinAlong + CornerClearMetres)
                    continue;
                if (along >= endAlong - 1f)
                    break;
                points.Add(corridor[i * 2]);
                points.Add(corridor[i * 2 + 1]);
            }

            PointAlong(corridor, endAlong, out var ex, out var ez);
            points.Add(ex);
            points.Add(ez);
            return GroundPathSmoothing.RelaxTightTurns(
                GroundPathSmoothing.FilletAndDensify(points.ToArray(), 24f, 8f));
        }

        /// <summary>
        /// Where a 12/30 arrival's taxi-in starts: the end of its vacate. Null for 05/23,
        /// whose vacates end at the E2 hold every taxi-in already starts from.
        /// </summary>
        public static bool TryArrivalJoin(RunwayDirection runway, out float x, out float z)
        {
            x = z = 0f;
            if (runway is not (RunwayDirection.Runway12 or RunwayDirection.Runway30))
                return false;
            var vacate = Vacate(runway);
            if (vacate.Length < 2)
                return false;
            x = vacate[vacate.Length - 2];
            z = vacate[vacate.Length - 1];
            return Hypot(x - AdelaideLayout.E2Hold[0], z - AdelaideLayout.E2Hold[1]) > 1f;
        }

        /// <summary>
        /// A taxi-in polyline cut to start at the 12/30 arrival join. Unchanged when the
        /// route does not pass the join (a stand reached another way).
        /// </summary>
        public static float[] TrimToArrivalJoin(float[] taxiIn, RunwayDirection runway)
        {
            if (taxiIn == null || taxiIn.Length < 4 || !TryArrivalJoin(runway, out var jx, out var jz))
                return taxiIn;
            if (DistanceToPolyline(taxiIn, jx, jz, out var along) > OnCorridorMetres)
                return taxiIn;

            var points = new System.Collections.Generic.List<float>(taxiIn.Length) { jx, jz };
            var walked = 0f;
            for (var i = 1; i < taxiIn.Length / 2; i++)
            {
                walked += Hypot(taxiIn[i * 2] - taxiIn[i * 2 - 2], taxiIn[i * 2 + 1] - taxiIn[i * 2 - 1]);
                if (walked <= along + 0.5f)
                    continue;
                points.Add(taxiIn[i * 2]);
                points.Add(taxiIn[i * 2 + 1]);
            }

            return points.Count >= 4 ? points.ToArray() : taxiIn;
        }

        /// <summary>The stretch from the E2 hold that every regional bay's taxi-in shares.</summary>
        private static float[] ArrivalCorridor()
        {
            if (_corridor != null)
                return _corridor;
            var bays = AdelaideLayout.Bays;
            if (bays.Length == 0)
                return _corridor = Array.Empty<float>();
            var first = bays[0].TaxiIn;
            var count = 0;
            for (var i = 0; i < first.Length / 2; i++)
            {
                var shared = true;
                for (var b = 1; b < bays.Length && shared; b++)
                    shared = DistanceToPolyline(bays[b].TaxiIn, first[i * 2], first[i * 2 + 1], out _) <= 0.5f;
                if (!shared)
                    break;
                count = i + 1;
            }

            var corridor = new float[count * 2];
            Array.Copy(first, corridor, corridor.Length);
            return _corridor = corridor;
        }

        internal static float DistanceToPolyline(float[] xz, float px, float pz, out float alongMetres)
        {
            var best = float.MaxValue;
            alongMetres = 0f;
            var walked = 0f;
            for (var i = 1; i < xz.Length / 2; i++)
            {
                float ax = xz[i * 2 - 2], az = xz[i * 2 - 1], bx = xz[i * 2], bz = xz[i * 2 + 1];
                var dx = bx - ax;
                var dz = bz - az;
                var length = Hypot(dx, dz);
                var t = length < 1e-4f ? 0f : Math.Max(0f, Math.Min(1f, ((px - ax) * dx + (pz - az) * dz) / (length * length)));
                var d = Hypot(ax + dx * t - px, az + dz * t - pz);
                if (d < best)
                {
                    best = d;
                    alongMetres = walked + length * t;
                }

                walked += length;
            }

            return best;
        }

        private static float PolylineLength(float[] xz)
        {
            var length = 0f;
            for (var i = 1; i < xz.Length / 2; i++)
                length += Hypot(xz[i * 2] - xz[i * 2 - 2], xz[i * 2 + 1] - xz[i * 2 - 1]);
            return length;
        }

        private static void PointAlong(float[] xz, float metres, out float x, out float z)
        {
            var walked = 0f;
            for (var i = 1; i < xz.Length / 2; i++)
            {
                var length = Hypot(xz[i * 2] - xz[i * 2 - 2], xz[i * 2 + 1] - xz[i * 2 - 1]);
                if (walked + length >= metres && length > 1e-4f)
                {
                    var t = (metres - walked) / length;
                    x = xz[i * 2 - 2] + (xz[i * 2] - xz[i * 2 - 2]) * t;
                    z = xz[i * 2 - 1] + (xz[i * 2 + 1] - xz[i * 2 - 1]) * t;
                    return;
                }

                walked += length;
            }

            x = xz[xz.Length - 2];
            z = xz[xz.Length - 1];
        }

        private static float Hypot(float x, float z) => (float)Math.Sqrt(x * x + z * z);

        private static void Threshold(RunwayDirection runway, float ontoMetres, out float x, out float z)
        {
            var along = runway == RunwayDirection.Runway30
                ? HalfLength - ontoMetres
                : -HalfLength + ontoMetres;
            LocalToWorld(along, 0f, out x, out z);
        }
    }
}

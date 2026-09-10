using System.Collections.Generic;

namespace Airside.Presentation
{
    /// <summary>
    /// ICAO-ish paint on the real-metre Adelaide 05/23 strip. No UnityEngine types:
    /// <see cref="AirsidePrototype"/> spawns from these rectangles, and the headless
    /// tests sample the same functions, so the painted runway and the thing under
    /// test cannot drift.
    ///
    /// West-to-east operations. Marks fit <c>AirsideFlightPath</c>; the path does
    /// not move to fit the marks. The 300 m touchdown-zone pair is centred on
    /// the existing touchdown (west threshold + 300 m).
    /// </summary>
    public static class AirsideRunwayMarkings
    {
        public const float PaintLiftMetres = 0.03f;
        public const float PaintHeight = 0.02f;

        public const float EdgeWidth = 0.90f;
        public const float EdgeOuterInset = 0.75f;

        public const float CentrelineWidth = 0.90f;
        public const float CentrelineDashLength = 30f;
        public const float CentrelineGap = 20f;

        public const int ThresholdStripeCount = 12;
        public const float ThresholdStripeLength = 30f;
        public const float ThresholdStripeWidth = 1.80f;
        public const float ThresholdStripeGap = 1.80f;

        /// <summary>LDA ≥ 2 400 m: aiming point begins 400 m past the threshold.</summary>
        public const float AimingPointFromThreshold = 400f;
        public const float AimingLength = 60f;
        public const float AimingWidth = 10f;
        public const float AimingInnerSpacing = 18f;

        public const float TouchdownZoneLength = 22.5f;
        public const float TouchdownZoneWidth = 3f;
        public const float TouchdownZoneInnerSpacing = 18f;

        /// <summary>
        /// Distances from each threshold. 400 m is the aiming point and is not
        /// repeated here.
        /// </summary>
        public static readonly float[] TouchdownZoneDistances =
        {
            150f, 300f, 600f, 750f, 900f
        };

        public static float WestThresholdX => -AirsideBareField.RunwayHalfLength;

        public static float EastThresholdX => AirsideBareField.RunwayHalfLength;

        public static float PaintCenterY =>
            AirsideBareField.RunwayCenterY
            + AirsideBareField.RunwayHeightMetres * 0.5f
            + PaintLiftMetres;

        public static float EdgeCenterZ =>
            AirsideBareField.RunwayHalfWidth - EdgeOuterInset - EdgeWidth * 0.5f;

        public static float EdgeOuterZ => EdgeCenterZ + EdgeWidth * 0.5f;

        public static float WestAimingStartX => WestThresholdX + AimingPointFromThreshold;

        public static float EastAimingStartX => EastThresholdX - AimingPointFromThreshold;

        public static float WestTouchdownZoneX(float distanceFromThreshold) =>
            WestThresholdX + distanceFromThreshold;

        public static float EastTouchdownZoneX(float distanceFromThreshold) =>
            EastThresholdX - distanceFromThreshold;

        public static bool HasTouchdownZoneAt(float distanceFromThreshold)
        {
            for (var i = 0; i < TouchdownZoneDistances.Length; i++)
            {
                if (TouchdownZoneDistances[i] == distanceFromThreshold)
                    return true;
            }

            return false;
        }

        public static RunwayMark EdgeLeft =>
            new(0f, -EdgeCenterZ, AirsideBareField.RunwayLengthMetres, EdgeWidth);

        public static RunwayMark EdgeRight =>
            new(0f, EdgeCenterZ, AirsideBareField.RunwayLengthMetres, EdgeWidth);

        public static RunwayMark[] Edges() => new[] { EdgeLeft, EdgeRight };

        public static RunwayMark[] CentrelineDashes()
        {
            var start = WestThresholdX + ThresholdStripeLength;
            var end = EastThresholdX - ThresholdStripeLength;
            var step = CentrelineDashLength + CentrelineGap;
            var list = new List<RunwayMark>(64);
            for (var x0 = start; x0 + CentrelineDashLength <= end + 0.001f; x0 += step)
            {
                list.Add(new RunwayMark(
                    x0 + CentrelineDashLength * 0.5f,
                    0f,
                    CentrelineDashLength,
                    CentrelineWidth));
            }

            return list.ToArray();
        }

        public static RunwayMark[] ThresholdStripes()
        {
            var perSide = ThresholdStripeCount / 2;
            var list = new List<RunwayMark>(ThresholdStripeCount * 2);
            AddThresholdEnd(list, WestThresholdX + ThresholdStripeLength * 0.5f, perSide);
            AddThresholdEnd(list, EastThresholdX - ThresholdStripeLength * 0.5f, perSide);
            return list.ToArray();
        }

        public static RunwayMark[] AimingPoints()
        {
            var z = AimingInnerSpacing * 0.5f + AimingWidth * 0.5f;
            var westX = WestAimingStartX + AimingLength * 0.5f;
            var eastX = EastAimingStartX - AimingLength * 0.5f;
            return new[]
            {
                new RunwayMark(westX, -z, AimingLength, AimingWidth),
                new RunwayMark(westX, z, AimingLength, AimingWidth),
                new RunwayMark(eastX, -z, AimingLength, AimingWidth),
                new RunwayMark(eastX, z, AimingLength, AimingWidth)
            };
        }

        public static RunwayMark[] TouchdownZones()
        {
            var z = TouchdownZoneInnerSpacing * 0.5f + TouchdownZoneWidth * 0.5f;
            var list = new List<RunwayMark>(TouchdownZoneDistances.Length * 4);
            for (var i = 0; i < TouchdownZoneDistances.Length; i++)
            {
                var d = TouchdownZoneDistances[i];
                var westX = WestTouchdownZoneX(d);
                var eastX = EastTouchdownZoneX(d);
                list.Add(new RunwayMark(westX, -z, TouchdownZoneLength, TouchdownZoneWidth));
                list.Add(new RunwayMark(westX, z, TouchdownZoneLength, TouchdownZoneWidth));
                list.Add(new RunwayMark(eastX, -z, TouchdownZoneLength, TouchdownZoneWidth));
                list.Add(new RunwayMark(eastX, z, TouchdownZoneLength, TouchdownZoneWidth));
            }

            return list.ToArray();
        }

        public static RunwayMark[] All()
        {
            var edges = Edges();
            var dashes = CentrelineDashes();
            var threshold = ThresholdStripes();
            var aiming = AimingPoints();
            var tdz = TouchdownZones();
            var all = new RunwayMark[edges.Length + dashes.Length + threshold.Length
                                     + aiming.Length + tdz.Length];
            var n = 0;
            Copy(edges, all, ref n);
            Copy(dashes, all, ref n);
            Copy(threshold, all, ref n);
            Copy(aiming, all, ref n);
            Copy(tdz, all, ref n);
            return all;
        }

        private static void AddThresholdEnd(List<RunwayMark> list, float centerX, int perSide)
        {
            var firstCenterZ = ThresholdStripeGap * 0.5f + ThresholdStripeWidth * 0.5f;
            var pitch = ThresholdStripeWidth + ThresholdStripeGap;
            for (var i = 0; i < perSide; i++)
            {
                var z = firstCenterZ + i * pitch;
                list.Add(new RunwayMark(centerX, -z, ThresholdStripeLength, ThresholdStripeWidth));
                list.Add(new RunwayMark(centerX, z, ThresholdStripeLength, ThresholdStripeWidth));
            }
        }

        private static void Copy(RunwayMark[] from, RunwayMark[] to, ref int n)
        {
            for (var i = 0; i < from.Length; i++)
                to[n++] = from[i];
        }

        public readonly struct RunwayMark
        {
            public RunwayMark(float centerX, float centerZ, float lengthX, float widthZ)
            {
                CenterX = centerX;
                CenterZ = centerZ;
                LengthX = lengthX;
                WidthZ = widthZ;
            }

            public float CenterX { get; }
            public float CenterZ { get; }
            public float LengthX { get; }
            public float WidthZ { get; }

            public float MinX => CenterX - LengthX * 0.5f;
            public float MaxX => CenterX + LengthX * 0.5f;
            public float MinZ => CenterZ - WidthZ * 0.5f;
            public float MaxZ => CenterZ + WidthZ * 0.5f;

            public bool OnPavement =>
                AirsideBareField.ContainsRunway(MinX, MinZ)
                && AirsideBareField.ContainsRunway(MinX, MaxZ)
                && AirsideBareField.ContainsRunway(MaxX, MinZ)
                && AirsideBareField.ContainsRunway(MaxX, MaxZ);
        }
    }
}

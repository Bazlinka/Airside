using System.Collections.Generic;

namespace Airside.Presentation
{
    /// <summary>
    /// Parameterised ICAO-ish strip paint in local strip coordinates (X along the
    /// centreline, Z across). Used for 05/23, 12/30 and taxi edges. No UnityEngine.
    /// </summary>
    public static class AirsideStripMarkings
    {
        public const float EdgeWidth = 0.90f;
        public const float EdgeOuterInset = 0.75f;

        public const float CentrelineWidth = 0.90f;
        public const float CentrelineDashLength = 30f;
        public const float CentrelineGap = 20f;

        public const int ThresholdStripeCount = 12;
        public const float ThresholdStripeLength = 30f;
        public const float ThresholdStripeWidth = 1.80f;
        public const float ThresholdStripeGap = 1.80f;

        public const float AimingLength = 60f;
        public const float AimingWidth = 10f;
        public const float AimingInnerSpacing = 18f;

        public const float TouchdownZoneLength = 22.5f;
        public const float TouchdownZoneWidth = 3f;
        public const float TouchdownZoneInnerSpacing = 18f;

        /// <summary>LDA ≥ 2 400 m aiming / TDZ layout (main 05/23).</summary>
        public static readonly float[] LongStripTouchdownDistances =
            { 150f, 300f, 600f, 750f, 900f };

        /// <summary>Shorter strip (12/30): aiming at 300 m, fewer TDZ pairs.</summary>
        public static readonly float[] ShortStripTouchdownDistances =
            { 150f, 300f, 600f };

        public const float LongStripAimingFromThreshold = 400f;
        public const float ShortStripAimingFromThreshold = 300f;

        public static float EdgeCenterZ(float stripWidth) =>
            stripWidth * 0.5f - EdgeOuterInset - EdgeWidth * 0.5f;

        public static Mark EdgeLeft(float stripLength, float stripWidth) =>
            new(0f, -EdgeCenterZ(stripWidth), stripLength, EdgeWidth);

        public static Mark EdgeRight(float stripLength, float stripWidth) =>
            new(0f, EdgeCenterZ(stripWidth), stripLength, EdgeWidth);

        public static Mark[] Edges(float stripLength, float stripWidth) =>
            new[] { EdgeLeft(stripLength, stripWidth), EdgeRight(stripLength, stripWidth) };

        public static Mark[] CentrelineDashes(float stripLength)
        {
            var half = stripLength * 0.5f;
            var start = -half + ThresholdStripeLength;
            var end = half - ThresholdStripeLength;
            var step = CentrelineDashLength + CentrelineGap;
            var list = new List<Mark>(64);
            for (var x0 = start; x0 + CentrelineDashLength <= end + 0.001f; x0 += step)
            {
                list.Add(new Mark(
                    x0 + CentrelineDashLength * 0.5f,
                    0f,
                    CentrelineDashLength,
                    CentrelineWidth));
            }

            return list.ToArray();
        }

        public static Mark[] ThresholdStripes(float stripLength)
        {
            var half = stripLength * 0.5f;
            var perSide = ThresholdStripeCount / 2;
            var list = new List<Mark>(ThresholdStripeCount * 2);
            AddThresholdEnd(list, -half + ThresholdStripeLength * 0.5f, perSide);
            AddThresholdEnd(list, half - ThresholdStripeLength * 0.5f, perSide);
            return list.ToArray();
        }

        public static Mark[] AimingPoints(float stripLength, float aimingFromThreshold)
        {
            var half = stripLength * 0.5f;
            var z = AimingInnerSpacing * 0.5f + AimingWidth * 0.5f;
            var westX = -half + aimingFromThreshold + AimingLength * 0.5f;
            var eastX = half - aimingFromThreshold - AimingLength * 0.5f;
            return new[]
            {
                new Mark(westX, -z, AimingLength, AimingWidth),
                new Mark(westX, z, AimingLength, AimingWidth),
                new Mark(eastX, -z, AimingLength, AimingWidth),
                new Mark(eastX, z, AimingLength, AimingWidth)
            };
        }

        public static Mark[] TouchdownZones(float stripLength, float[] distancesFromThreshold)
        {
            var half = stripLength * 0.5f;
            var z = TouchdownZoneInnerSpacing * 0.5f + TouchdownZoneWidth * 0.5f;
            var list = new List<Mark>(distancesFromThreshold.Length * 4);
            for (var i = 0; i < distancesFromThreshold.Length; i++)
            {
                var d = distancesFromThreshold[i];
                var westX = -half + d;
                var eastX = half - d;
                list.Add(new Mark(westX, -z, TouchdownZoneLength, TouchdownZoneWidth));
                list.Add(new Mark(westX, z, TouchdownZoneLength, TouchdownZoneWidth));
                list.Add(new Mark(eastX, -z, TouchdownZoneLength, TouchdownZoneWidth));
                list.Add(new Mark(eastX, z, TouchdownZoneLength, TouchdownZoneWidth));
            }

            return list.ToArray();
        }

        /// <summary>Full paint set for the main 05/23 strip (local = world for that strip).</summary>
        public static Mark[] MainRunwayAll()
        {
            var length = AirsideAdelaidePavement.MainLengthMetres;
            var width = AirsideAdelaidePavement.MainWidthMetres;
            return Combine(
                Edges(length, width),
                CentrelineDashes(length),
                ThresholdStripes(length),
                AimingPoints(length, LongStripAimingFromThreshold),
                TouchdownZones(length, LongStripTouchdownDistances));
        }

        /// <summary>Full paint set for 12/30 in local strip coordinates.</summary>
        public static Mark[] CrossRunwayAll()
        {
            var length = AirsideAdelaidePavement.CrossLengthMetres;
            var width = AirsideAdelaidePavement.CrossWidthMetres;
            return Combine(
                Edges(length, width),
                CentrelineDashes(length),
                ThresholdStripes(length),
                AimingPoints(length, ShortStripAimingFromThreshold),
                TouchdownZones(length, ShortStripTouchdownDistances));
        }

        /// <summary>Taxi edge + dashed centreline in local strip coordinates.</summary>
        public static Mark[] TaxiwayGuide(float stripLength, float stripWidth)
        {
            return Combine(
                Edges(stripLength, stripWidth),
                CentrelineDashes(stripLength));
        }

        /// <summary>
        /// Hold-short bar pair across a taxi link (local: X across taxi, Z along link).
        /// Placed near the runway end of the link.
        /// </summary>
        public static Mark[] HoldShortBars(float taxiWidth, float alongLinkFromRunwayEdge)
        {
            const float barWidth = 0.9f;
            const float barGap = 0.9f;
            var z = alongLinkFromRunwayEdge;
            return new[]
            {
                new Mark(0f, z - (barGap + barWidth) * 0.5f, taxiWidth - 2f, barWidth),
                new Mark(0f, z + (barGap + barWidth) * 0.5f, taxiWidth - 2f, barWidth)
            };
        }

        private static void AddThresholdEnd(List<Mark> list, float centerX, int perSide)
        {
            var firstCenterZ = ThresholdStripeGap * 0.5f + ThresholdStripeWidth * 0.5f;
            var pitch = ThresholdStripeWidth + ThresholdStripeGap;
            for (var i = 0; i < perSide; i++)
            {
                var z = firstCenterZ + i * pitch;
                list.Add(new Mark(centerX, -z, ThresholdStripeLength, ThresholdStripeWidth));
                list.Add(new Mark(centerX, z, ThresholdStripeLength, ThresholdStripeWidth));
            }
        }

        private static Mark[] Combine(params Mark[][] groups)
        {
            var total = 0;
            for (var g = 0; g < groups.Length; g++)
                total += groups[g].Length;
            var all = new Mark[total];
            var n = 0;
            for (var g = 0; g < groups.Length; g++)
            {
                for (var i = 0; i < groups[g].Length; i++)
                    all[n++] = groups[g][i];
            }

            return all;
        }

        public readonly struct Mark
        {
            public Mark(float centerX, float centerZ, float lengthX, float widthZ)
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
        }
    }
}

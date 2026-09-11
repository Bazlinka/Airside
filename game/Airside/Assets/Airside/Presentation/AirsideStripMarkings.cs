using System;
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
        // --- Taxiway paint (yellow) ---
        //
        // A taxiway is not a small runway: the centreline is one CONTINUOUS line,
        // and the edge marking is a pair of narrow lines, not a single wide stripe.

        /// <summary>Continuous yellow taxiway centreline width (metres).</summary>
        public const float TaxiCentrelineWidth = 0.15f;

        /// <summary>Each line of the double yellow taxiway edge marking (metres).</summary>
        public const float TaxiEdgeLineWidth = 0.15f;

        /// <summary>Clear gap between the two lines of the edge marking (metres).</summary>
        public const float TaxiEdgeLineGap = 0.15f;

        /// <summary>Inset of the outer edge line from the pavement edge (metres).</summary>
        public const float TaxiEdgeInset = 0.15f;

        /// <summary>Continuous centreline running the full length of the taxiway.</summary>
        public static Mark TaxiwayCentreline(float stripLength) =>
            new(0f, 0f, stripLength, TaxiCentrelineWidth);

        /// <summary>Double yellow edge marking, both sides (4 lines).</summary>
        public static Mark[] TaxiwayEdges(float stripLength, float stripWidth)
        {
            var outer = stripWidth * 0.5f - TaxiEdgeInset - TaxiEdgeLineWidth * 0.5f;
            var inner = outer - TaxiEdgeLineWidth - TaxiEdgeLineGap;
            return new[]
            {
                new Mark(0f, -outer, stripLength, TaxiEdgeLineWidth),
                new Mark(0f, -inner, stripLength, TaxiEdgeLineWidth),
                new Mark(0f, inner, stripLength, TaxiEdgeLineWidth),
                new Mark(0f, outer, stripLength, TaxiEdgeLineWidth)
            };
        }

        public static Mark[] TaxiwayGuide(float stripLength, float stripWidth)
        {
            return Combine(
                TaxiwayEdges(stripLength, stripWidth),
                new[] { TaxiwayCentreline(stripLength) });
        }

        /// <summary>
        /// ICAO Annex 14 pattern A runway-holding position across a taxi link
        /// (local: X across taxi, Z along link, +Z away from the runway).
        ///
        /// Four bars: the two nearest the runway are solid, the two beyond are
        /// dashed. <paramref name="alongLinkFromRunwayEdge"/> is measured from the
        /// runway pavement edge — see
        /// <c>AirsideAdelaidePavement.HoldShortFromRunwayEdgeMetres</c>, which puts
        /// the pattern at the code E holding position of 90 m from the centreline.
        /// </summary>
        public static Mark[] HoldShortBars(float taxiWidth, float alongLinkFromRunwayEdge)
        {
            const float barWidth = 0.3f;
            const float barGap = 0.3f;
            const float dashLength = 0.9f;
            const float dashGap = 0.9f;

            var pitch = barWidth + barGap;
            var z = alongLinkFromRunwayEdge;
            var across = taxiWidth - 2f;
            if (across < 1f)
                across = taxiWidth;

            // Solid pair sits on the runway side (smaller Z), dashed pair beyond.
            var solidNear = z - pitch * 1.5f;
            var solidFar = z - pitch * 0.5f;
            var dashedNear = z + pitch * 0.5f;
            var dashedFar = z + pitch * 1.5f;

            var list = new List<Mark>(8);
            list.Add(new Mark(0f, solidNear, across, barWidth));
            list.Add(new Mark(0f, solidFar, across, barWidth));
            AddDashedBar(list, dashedNear, across, barWidth, dashLength, dashGap);
            AddDashedBar(list, dashedFar, across, barWidth, dashLength, dashGap);
            return list.ToArray();
        }

        /// <summary>
        /// The four bars of <see cref="HoldShortBars"/> collapsed to one Mark each,
        /// for callers that only need the pattern's footprint rather than the
        /// individual dashes.
        /// </summary>
        public static Mark[] HoldShortBarLanes(float taxiWidth, float alongLinkFromRunwayEdge)
        {
            const float barWidth = 0.3f;
            const float barGap = 0.3f;
            var pitch = barWidth + barGap;
            var z = alongLinkFromRunwayEdge;
            var across = taxiWidth - 2f;
            if (across < 1f)
                across = taxiWidth;
            return new[]
            {
                new Mark(0f, z - pitch * 1.5f, across, barWidth),
                new Mark(0f, z - pitch * 0.5f, across, barWidth),
                new Mark(0f, z + pitch * 0.5f, across, barWidth),
                new Mark(0f, z + pitch * 1.5f, across, barWidth)
            };
        }

        private static void AddDashedBar(
            List<Mark> list, float centerZ, float across, float barWidth,
            float dashLength, float dashGap)
        {
            var step = dashLength + dashGap;
            var count = (int)Math.Floor((across + dashGap) / step);
            if (count < 1)
            {
                list.Add(new Mark(0f, centerZ, across, barWidth));
                return;
            }

            var span = count * step - dashGap;
            var start = -span * 0.5f + dashLength * 0.5f;
            for (var i = 0; i < count; i++)
                list.Add(new Mark(start + i * step, centerZ, dashLength, barWidth));
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

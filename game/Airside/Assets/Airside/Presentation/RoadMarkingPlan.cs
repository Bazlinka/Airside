using System;

namespace Airside.Presentation
{
    /// <summary>One painted line along a road: sideways offset from the centreline, and its look.</summary>
    public readonly struct MarkingLine
    {
        public MarkingLine(float offset, bool dashed, float width)
        {
            Offset = offset;
            Dashed = dashed;
            Width = width;
        }

        /// <summary>Metres to the left (+) or right (-) of the road's centreline.</summary>
        public float Offset { get; }
        public bool Dashed { get; }
        public float Width { get; }
    }

    /// <summary>
    /// ADR 0182 — which lines a road of a given width carries. Lanes are about 3.4 m, so 9 m is two
    /// lanes (a dashed centreline, as before), 14 m four (a solid double centre line and a dashed lane
    /// line each side) and so on; every marked road gets a solid white edge line just inside the kerb.
    /// The map data carries a width, not a lane count, so lanes are derived. Pure, so it is tested.
    /// </summary>
    public static class RoadMarkingPlan
    {
        public const float LaneMetres = 3.4f;
        public const float EdgeInsetMetres = 0.45f;
        public const float CentreLineWidthMetres = 0.32f;
        public const float EdgeLineWidthMetres = 0.18f;
        public const float DoubleLineGapMetres = 0.45f;
        /// <summary>Roads narrower than this get no edge lines: laneways and driveways.</summary>
        public const float EdgeLineMinWidthMetres = 8f;

        public static int Lanes(float roadWidth) =>
            Math.Max(2, Math.Min(10, (int)Math.Round(roadWidth / LaneMetres)));

        public static MarkingLine[] For(float roadWidth)
        {
            var lines = new System.Collections.Generic.List<MarkingLine>(8);
            var lanes = Lanes(roadWidth);
            if (lanes >= 4)
            {
                // A divided or multi-lane road: no crossing the middle.
                lines.Add(new MarkingLine(DoubleLineGapMetres * 0.5f, false, CentreLineWidthMetres * 0.6f));
                lines.Add(new MarkingLine(-DoubleLineGapMetres * 0.5f, false, CentreLineWidthMetres * 0.6f));
                var laneWidth = roadWidth / lanes;
                for (var k = 1; k < lanes / 2; k++)
                {
                    lines.Add(new MarkingLine(k * laneWidth, true, CentreLineWidthMetres));
                    lines.Add(new MarkingLine(-k * laneWidth, true, CentreLineWidthMetres));
                }
            }
            else
            {
                lines.Add(new MarkingLine(0f, true, CentreLineWidthMetres));
            }

            if (roadWidth >= EdgeLineMinWidthMetres)
            {
                var edge = roadWidth * 0.5f - EdgeInsetMetres;
                lines.Add(new MarkingLine(edge, false, EdgeLineWidthMetres));
                lines.Add(new MarkingLine(-edge, false, EdgeLineWidthMetres));
            }

            return lines.ToArray();
        }
    }
}

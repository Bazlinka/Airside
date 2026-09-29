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

        /// <summary>
        /// Lines for a mapped road, using what OSM knows (ADR 0184): its lane count when tagged and whether it is
        /// one-way. A one-way road with several lanes gets a dashed line between each pair; a tagged two- or
        /// three-lane road keeps a dashed centre line however wide it is (parking lanes); untagged wide roads
        /// fall back to <see cref="For"/>. Airside service roads get edge lines and, if two-way, a dashed centre.
        /// </summary>
        public static MarkingLine[] ForRoad(float roadWidth, int lanes, bool oneWay, bool airside)
        {
            var lines = new System.Collections.Generic.List<MarkingLine>(8);
            if (airside)
            {
                if (roadWidth >= 5f)
                {
                    var edge = roadWidth * 0.5f - 0.3f;
                    lines.Add(new MarkingLine(edge, false, 0.15f));
                    lines.Add(new MarkingLine(-edge, false, 0.15f));
                }

                if (!oneWay && roadWidth >= 6.5f)
                    lines.Add(new MarkingLine(0f, true, CentreLineWidthMetres));
                return lines.ToArray();
            }

            if (oneWay)
            {
                var n = Math.Max(1, Math.Min(10, lanes > 0 ? lanes : Lanes(roadWidth)));
                for (var k = 1; k < n; k++)
                    lines.Add(new MarkingLine(-roadWidth * 0.5f + k * roadWidth / n, true, CentreLineWidthMetres));
                if (roadWidth >= 6f)
                {
                    var edge = roadWidth * 0.5f - EdgeInsetMetres;
                    lines.Add(new MarkingLine(edge, false, EdgeLineWidthMetres));
                    lines.Add(new MarkingLine(-edge, false, EdgeLineWidthMetres));
                }

                return lines.ToArray();
            }

            if (lanes >= 1 && lanes <= 3)
            {
                if (roadWidth >= 5.5f)
                    lines.Add(new MarkingLine(0f, true, CentreLineWidthMetres));
                if (roadWidth >= 6.5f)
                {
                    var edge = roadWidth * 0.5f - EdgeInsetMetres;
                    lines.Add(new MarkingLine(edge, false, EdgeLineWidthMetres));
                    lines.Add(new MarkingLine(-edge, false, EdgeLineWidthMetres));
                }

                return lines.ToArray();
            }

            return For(roadWidth);
        }
    }
}

using System.Collections.Generic;

namespace Airside.Presentation
{
    /// <summary>
    /// Places short labels beside dense map markers without letting a busy airport turn into
    /// an unreadable stack. Pure HUD geometry keeps the policy testable outside Unity.
    /// </summary>
    public static class MapLabelLayout
    {
        private const float EdgeInset = 4f;
        private const float VerticalGap = 3f;
        private const int MaxRowsEachSide = 18;

        /// <summary>
        /// Finds the first readable sidecar position for a marker. Labels fan above and below
        /// the marker on its right first, then its left; when the local cluster is saturated the
        /// caller leaves the low-priority label out rather than drawing illegible text.
        /// </summary>
        public static bool TryPlace(HudBox bounds, float markerX, float markerY, float markerRadius,
            float width, float height, IReadOnlyList<HudBox> occupied, out HudBox placement)
        {
            for (var side = 0; side < 2; side++)
            {
                var x = side == 0
                    ? markerX + markerRadius + EdgeInset
                    : markerX - markerRadius - EdgeInset - width;
                for (var row = 0; row <= MaxRowsEachSide * 2; row++)
                {
                    var verticalStep = row == 0 ? 0 : (row + 1) / 2 * (row % 2 == 0 ? 1 : -1);
                    var y = markerY - height * 0.5f + verticalStep * (height + VerticalGap);
                    var candidate = new HudBox(x, y, width, height);
                    if (!Fits(bounds, candidate) || OverlapsAny(candidate, occupied))
                        continue;
                    placement = candidate;
                    return true;
                }
            }

            placement = HudBox.Empty;
            return false;
        }

        private static bool Fits(HudBox bounds, HudBox candidate) =>
            candidate.X >= bounds.X + EdgeInset
            && candidate.Y >= bounds.Y + EdgeInset
            && candidate.Right <= bounds.Right - EdgeInset
            && candidate.Bottom <= bounds.Bottom - EdgeInset;

        private static bool OverlapsAny(HudBox candidate, IReadOnlyList<HudBox> occupied)
        {
            if (occupied == null)
                return false;
            for (var i = 0; i < occupied.Count; i++)
                if (candidate.Overlaps(occupied[i]))
                    return true;
            return false;
        }
    }
}

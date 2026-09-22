using System;

namespace Airside.Presentation
{
    /// <summary>
    /// Cylindrical unwrap, in metres, for aircraft kit parts.
    ///
    /// The aircraft glTFs carry POSITION only — no TEXCOORD_0 — so the loader generates UVs.
    /// The generic kit unwrap normalises each part's own bounding box to 0..1, which on an
    /// airframe means a 39 m fuselage gets one texture repeat over its whole length while a
    /// 0.3 m window gets one across 30 cm: a texel density difference of over a hundred
    /// times. Its axis choice also drops the part's *longest* axis, so a fuselage was
    /// unwrapped looking straight down its own length and every ring of the tube collapsed
    /// onto the same UV. No authored skin could read as panels through that.
    ///
    /// Aircraft parts are overwhelmingly bodies of revolution or extrusions, so one rule
    /// covers them: V is position along the longest axis in metres, U is arc length in
    /// metres around it. Texel density is then the same on every part of every aircraft,
    /// which is what lets one tiling skin carry real frame, stringer and rivet spacing.
    ///
    /// No UnityEngine types on purpose: the headless harness checks the density contract.
    /// </summary>
    public static class AircraftSkinUv
    {
        /// <summary>Index of the longest edge of the bounding box: 0 = X, 1 = Y, 2 = Z.</summary>
        public static int LongestAxis(float sizeX, float sizeY, float sizeZ)
        {
            var x = Math.Abs(sizeX);
            var y = Math.Abs(sizeY);
            var z = Math.Abs(sizeZ);
            if (x >= y && x >= z)
                return 0;
            return y >= z ? 1 : 2;
        }

        /// <summary>
        /// Mean cross-section radius used to turn the sweep angle into metres of arc. Floored
        /// so a perfectly flat part (a paint strip, a door skin) cannot collapse U to zero.
        /// </summary>
        public static float ArcRadiusMetres(float minorSpanA, float minorSpanB) =>
            Math.Max(0.02f, (Math.Abs(minorSpanA) + Math.Abs(minorSpanB)) * 0.25f);

        /// <summary>
        /// UV for one vertex. <paramref name="alongMetres"/> is its position on the longest
        /// axis; the two minor offsets are measured from the part's cross-section centre.
        /// </summary>
        public static void Unwrap(
            float alongMetres,
            float minorOffsetA,
            float minorOffsetB,
            float arcRadiusMetres,
            out float u,
            out float v)
        {
            var angle = (float)Math.Atan2(minorOffsetB, minorOffsetA);
            u = angle * arcRadiusMetres;
            v = alongMetres;
        }
    }
}

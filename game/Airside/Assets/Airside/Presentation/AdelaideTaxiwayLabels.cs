using System;
using System.Collections.Generic;
using Airside.Simulation;

namespace Airside.Presentation
{
    /// <summary>One painted taxiway designator: the reference and where it lies on the surface.</summary>
    public readonly struct TaxiwayLabel
    {
        public TaxiwayLabel(string reference, float x, float z, float yawDegrees)
        {
            Reference = reference; X = x; Z = z; YawDegrees = yawDegrees;
        }

        public string Reference { get; }
        public float X { get; }
        public float Z { get; }

        /// <summary>Heading of the taxiway at the label; the glyph tops point this way (see AddStandLabel).</summary>
        public float YawDegrees { get; }
    }

    /// <summary>
    /// Where the taxiway designators (A2, F3, T4 …) are painted. Each named centreline carries one
    /// at its middle and one more every <see cref="SpacingMetres"/> on long taxiways, set to the
    /// side of the centreline so the yellow line stays unbroken. Names come from the OSM
    /// <c>ref</c> tags already baked into <see cref="AdelaideLayout.Taxiways"/>. Pure geometry.
    /// </summary>
    public static class AdelaideTaxiwayLabels
    {
        /// <summary>Glyph scale on <see cref="AirsideStripMarkings.DesignationDigitHeight"/> (16 m): ≈ 4.8 m letters.</summary>
        public const float GlyphScale = 0.3f;
        /// <summary>Centreline pieces shorter than this are junction stubs and stay unlabelled.</summary>
        public const float MinLengthMetres = 60f;
        public const float SpacingMetres = 450f;
        /// <summary>Distance from the centreline to the label centre.</summary>
        public const float SideOffsetMetres = 5f;

        private static TaxiwayLabel[] _all;

        public static TaxiwayLabel[] All() => _all ??= Build(AdelaideLayout.Taxiways);

        public static TaxiwayLabel[] Build(IReadOnlyList<AdelaideTaxiway> taxiways)
        {
            var labels = new List<TaxiwayLabel>();
            foreach (var taxiway in taxiways)
            {
                if (string.IsNullOrEmpty(taxiway.Reference))
                    continue;
                var xz = taxiway.Xz;
                var total = Length(xz);
                if (total < MinLengthMetres)
                    continue;
                var count = Math.Max(1, (int)Math.Round(total / SpacingMetres));
                for (var i = 0; i < count; i++)
                {
                    var (x, z, dx, dz) = PointAt(xz, total * (i + 0.5f) / count);
                    var yaw = (float)(Math.Atan2(dx, dz) * 180.0 / Math.PI);
                    // Left of the direction of travel, clear of the centreline.
                    labels.Add(new TaxiwayLabel(taxiway.Reference,
                        x + dz * SideOffsetMetres, z - dx * SideOffsetMetres, yaw));
                }
            }

            return labels.ToArray();
        }

        private static float Length(float[] xz)
        {
            var total = 0f;
            for (var i = 0; i + 3 < xz.Length; i += 2)
                total += (float)Math.Sqrt(Sq(xz[i + 2] - xz[i]) + Sq(xz[i + 3] - xz[i + 1]));
            return total;
        }

        private static (float x, float z, float dx, float dz) PointAt(float[] xz, float distance)
        {
            for (var i = 0; i + 3 < xz.Length; i += 2)
            {
                var vx = xz[i + 2] - xz[i];
                var vz = xz[i + 3] - xz[i + 1];
                var len = (float)Math.Sqrt(vx * vx + vz * vz);
                if (len < 1e-4f)
                    continue;
                if (distance <= len || i + 5 >= xz.Length)
                {
                    var t = Math.Min(1f, distance / len);
                    return (xz[i] + vx * t, xz[i + 1] + vz * t, vx / len, vz / len);
                }

                distance -= len;
            }

            return (xz[0], xz[1], 1f, 0f);
        }

        private static float Sq(float v) => v * v;
    }
}

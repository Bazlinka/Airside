using System;
using Airside.Simulation;

namespace Airside.Presentation
{
    /// <summary>Procedural apron paint derived from the generated YPAD regional bays.</summary>
    public readonly struct AdelaideStandMarking
    {
        public AdelaideStandMarking(
            string bayId,
            string reference,
            float[] leadIn,
            float[] stopBar,
            float labelX,
            float labelZ,
            float labelYawDegrees)
        {
            BayId = bayId;
            Reference = reference;
            LeadIn = leadIn;
            StopBar = stopBar;
            LabelX = labelX;
            LabelZ = labelZ;
            LabelYawDegrees = labelYawDegrees;
        }

        public string BayId { get; }
        public string Reference { get; }
        public float[] LeadIn { get; }
        public float[] StopBar { get; }
        public float LabelX { get; }
        public float LabelZ { get; }
        public float LabelYawDegrees { get; }
    }

    public static class AdelaideStandMarkings
    {
        public const float LeadInLengthMetres = 42f;
        public const float StopBarWidthMetres = 12f;
        public const float LabelBeforeStopMetres = 18f;

        private static AdelaideStandMarking[] _all;

        public static AdelaideStandMarking[] All()
        {
            if (_all != null)
                return _all;

            _all = new AdelaideStandMarking[AdelaideLayout.Bays.Length];
            for (var i = 0; i < AdelaideLayout.Bays.Length; i++)
                _all[i] = For(AdelaideLayout.Bays[i]);
            return _all;
        }

        private static AdelaideStandMarking For(AdelaideBay bay)
        {
            var stopX = bay.StopX;
            var stopZ = bay.StopZ;
            PointBeforeEnd(bay.TaxiIn, LeadInLengthMetres, out var startX, out var startZ);

            var approachX = stopX - startX;
            var approachZ = stopZ - startZ;
            var approachLength = Math.Max(0.001f, (float)Math.Sqrt(approachX * approachX + approachZ * approachZ));
            approachX /= approachLength;
            approachZ /= approachLength;

            var heading = bay.HeadingDegrees * Math.PI / 180d;
            var noseX = (float)Math.Sin(heading);
            var noseZ = (float)Math.Cos(heading);
            var acrossX = -noseZ * StopBarWidthMetres * 0.5f;
            var acrossZ = noseX * StopBarWidthMetres * 0.5f;

            return new AdelaideStandMarking(
                bay.Id,
                bay.Reference,
                new[] { startX, startZ, stopX, stopZ },
                new[] { stopX - acrossX, stopZ - acrossZ, stopX + acrossX, stopZ + acrossZ },
                stopX - approachX * LabelBeforeStopMetres,
                stopZ - approachZ * LabelBeforeStopMetres,
                (float)(Math.Atan2(approachX, approachZ) * 180d / Math.PI));
        }

        private static void PointBeforeEnd(float[] xz, float distance, out float x, out float z)
        {
            var last = xz.Length - 2;
            x = xz[last];
            z = xz[last + 1];
            var remaining = distance;

            for (var i = last; i >= 2; i -= 2)
            {
                var toX = xz[i];
                var toZ = xz[i + 1];
                var fromX = xz[i - 2];
                var fromZ = xz[i - 1];
                var dx = toX - fromX;
                var dz = toZ - fromZ;
                var segment = (float)Math.Sqrt(dx * dx + dz * dz);
                if (segment >= remaining && segment > 0.001f)
                {
                    var t = (segment - remaining) / segment;
                    x = fromX + dx * t;
                    z = fromZ + dz * t;
                    return;
                }

                remaining -= segment;
                x = fromX;
                z = fromZ;
            }
        }
    }
}

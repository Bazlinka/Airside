using System;
using Airside.Simulation;

namespace Airside.Presentation
{
    /// <summary>Procedural apron paint derived from the generated YPAD aircraft stands.</summary>
    public readonly struct AdelaideStandMarking
    {
        public AdelaideStandMarking(
            string standId,
            string reference,
            float[] leadIn,
            float[] stopBar,
            float labelX,
            float labelZ,
            float labelYawDegrees)
        {
            StandId = standId;
            Reference = reference;
            LeadIn = leadIn;
            StopBar = stopBar;
            LabelX = labelX;
            LabelZ = labelZ;
            LabelYawDegrees = labelYawDegrees;
        }

        public string StandId { get; }
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
        public const float TerminalLeadInLengthMetres = 65f;
        public const float TerminalStopBarWidthMetres = 20f;
        public const float TerminalLabelBeforeStopMetres = 28f;

        private static AdelaideStandMarking[] _all;

        public static AdelaideStandMarking[] All()
        {
            if (_all != null)
                return _all;

            _all = new AdelaideStandMarking[AdelaideLayout.Bays.Length + AdelaideLayout.TerminalGates.Length];
            for (var i = 0; i < AdelaideLayout.Bays.Length; i++)
                _all[i] = For(AdelaideLayout.Bays[i]);
            for (var i = 0; i < AdelaideLayout.TerminalGates.Length; i++)
                _all[AdelaideLayout.Bays.Length + i] = For(AdelaideLayout.TerminalGates[i]);
            return _all;
        }

        private static AdelaideStandMarking For(AdelaideBay bay) => Create(
            bay.Id, bay.Reference, bay.StopX, bay.StopZ, bay.HeadingDegrees, bay.TaxiIn,
            LeadInLengthMetres, StopBarWidthMetres, LabelBeforeStopMetres);

        private static AdelaideStandMarking For(AdelaideTerminalGate gate) => Create(
            gate.Id, gate.Reference, gate.NoseX, gate.NoseZ, gate.HeadingDegrees, gate.TaxiIn,
            TerminalLeadInLengthMetres, TerminalStopBarWidthMetres, TerminalLabelBeforeStopMetres);

        private static AdelaideStandMarking Create(string standId, string reference, float stopX, float stopZ,
            float headingDegrees, float[] taxiIn, float leadInLength, float stopBarWidth, float labelBeforeStop)
        {
            PointBeforeEnd(taxiIn, leadInLength, out var startX, out var startZ);

            var approachX = stopX - startX;
            var approachZ = stopZ - startZ;
            var approachLength = Math.Max(0.001f, (float)Math.Sqrt(approachX * approachX + approachZ * approachZ));
            approachX /= approachLength;
            approachZ /= approachLength;

            var heading = headingDegrees * Math.PI / 180d;
            var noseX = (float)Math.Sin(heading);
            var noseZ = (float)Math.Cos(heading);
            var acrossX = -noseZ * stopBarWidth * 0.5f;
            var acrossZ = noseX * stopBarWidth * 0.5f;

            return new AdelaideStandMarking(
                standId,
                reference,
                new[] { startX, startZ, stopX, stopZ },
                new[] { stopX - acrossX, stopZ - acrossZ, stopX + acrossX, stopZ + acrossZ },
                stopX - approachX * labelBeforeStop,
                stopZ - approachZ * labelBeforeStop,
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

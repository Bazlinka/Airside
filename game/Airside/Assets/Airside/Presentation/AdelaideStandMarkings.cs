using System;
using System.Collections.Generic;
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
            float[] envelope,
            float[] leftShoulder,
            float[] rightShoulder,
            float labelX,
            float labelZ,
            float labelYawDegrees,
            float labelCharacterSize)
        {
            StandId = standId;
            Reference = reference;
            LeadIn = leadIn;
            StopBar = stopBar;
            Envelope = envelope;
            LeftShoulder = leftShoulder;
            RightShoulder = rightShoulder;
            LabelX = labelX;
            LabelZ = labelZ;
            LabelYawDegrees = labelYawDegrees;
            LabelCharacterSize = labelCharacterSize;
        }

        public string StandId { get; }
        public string Reference { get; }
        public float[] LeadIn { get; }
        public float[] StopBar { get; }
        /// <summary>Four-corner stand box, x,z pairs, not closed.</summary>
        public float[] Envelope { get; }
        public float[] LeftShoulder { get; }
        public float[] RightShoulder { get; }
        public float LabelX { get; }
        public float LabelZ { get; }
        public float LabelYawDegrees { get; }
        public float LabelCharacterSize { get; }
    }

    public static class AdelaideStandMarkings
    {
        public const float LeadInLengthMetres = 70f;
        public const float StopBarWidthMetres = 14f;
        public const float LabelBeforeStopMetres = 16f;
        public const float RegionalEnvelopeWidthMetres = 18f;
        public const float RegionalEnvelopeLengthMetres = 28f;
        public const float RegionalLabelSize = 0.48f;
        public const float TerminalLeadInLengthMetres = 110f;
        public const float TerminalStopBarWidthMetres = 22f;
        public const float TerminalLabelBeforeStopMetres = 24f;
        public const float TerminalEnvelopeWidthMetres = 30f;
        public const float TerminalEnvelopeLengthMetres = 44f;
        public const float TerminalLabelSize = 0.62f;
        public const float ShoulderLengthMetres = 4.5f;

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
            LeadInLengthMetres, StopBarWidthMetres, LabelBeforeStopMetres,
            RegionalEnvelopeWidthMetres, RegionalEnvelopeLengthMetres, RegionalLabelSize);

        private static AdelaideStandMarking For(AdelaideTerminalGate gate) => Create(
            gate.Id, gate.Reference, gate.NoseX, gate.NoseZ, gate.HeadingDegrees, gate.TaxiIn,
            TerminalLeadInLengthMetres, TerminalStopBarWidthMetres, TerminalLabelBeforeStopMetres,
            TerminalEnvelopeWidthMetres, TerminalEnvelopeLengthMetres, TerminalLabelSize);

        private static AdelaideStandMarking Create(string standId, string reference, float stopX, float stopZ,
            float headingDegrees, float[] taxiIn, float leadInLength, float stopBarWidth, float labelBeforeStop,
            float envelopeWidth, float envelopeLength, float labelSize)
        {
            var leadIn = PolylineBeforeEnd(taxiIn, leadInLength, stopX, stopZ);

            var heading = headingDegrees * Math.PI / 180d;
            var noseX = (float)Math.Sin(heading);
            var noseZ = (float)Math.Cos(heading);
            var acrossX = -noseZ;
            var acrossZ = noseX;

            var halfBarX = acrossX * stopBarWidth * 0.5f;
            var halfBarZ = acrossZ * stopBarWidth * 0.5f;
            var approachX = 0f;
            var approachZ = 1f;
            if (leadIn.Length >= 4)
            {
                approachX = stopX - leadIn[leadIn.Length - 4];
                approachZ = stopZ - leadIn[leadIn.Length - 3];
                var approachLength = Math.Max(0.001f, (float)Math.Sqrt(approachX * approachX + approachZ * approachZ));
                approachX /= approachLength;
                approachZ /= approachLength;
            }

            var halfEnvX = acrossX * envelopeWidth * 0.5f;
            var halfEnvZ = acrossZ * envelopeWidth * 0.5f;
            var tailX = stopX - noseX * envelopeLength;
            var tailZ = stopZ - noseZ * envelopeLength;
            var shoulderX = -noseX * ShoulderLengthMetres;
            var shoulderZ = -noseZ * ShoulderLengthMetres;

            return new AdelaideStandMarking(
                standId,
                reference,
                leadIn,
                new[] { stopX - halfBarX, stopZ - halfBarZ, stopX + halfBarX, stopZ + halfBarZ },
                new[]
                {
                    stopX - halfEnvX, stopZ - halfEnvZ,
                    stopX + halfEnvX, stopZ + halfEnvZ,
                    tailX + halfEnvX, tailZ + halfEnvZ,
                    tailX - halfEnvX, tailZ - halfEnvZ
                },
                new[] { stopX - halfBarX, stopZ - halfBarZ, stopX - halfBarX + shoulderX, stopZ - halfBarZ + shoulderZ },
                new[] { stopX + halfBarX, stopZ + halfBarZ, stopX + halfBarX + shoulderX, stopZ + halfBarZ + shoulderZ },
                stopX - approachX * labelBeforeStop,
                stopZ - approachZ * labelBeforeStop,
                (float)(Math.Atan2(approachX, approachZ) * 180d / Math.PI),
                labelSize);
        }

        /// <summary>
        /// The last <paramref name="distance"/> metres of <paramref name="xz"/>, ending on
        /// the stand stop. Intermediate vertices stay so the paint follows the taxi-in
        /// instead of a straight T that dies in the middle of the apron.
        /// </summary>
        internal static float[] PolylineBeforeEnd(float[] xz, float distance, float endX, float endZ)
        {
            var reverse = new List<float> { endX, endZ };
            if (xz == null || xz.Length < 4 || distance <= 0f)
                return reverse.ToArray();

            var remaining = distance;
            var last = xz.Length - 2;
            for (var i = last; i >= 2; i -= 2)
            {
                var toX = xz[i];
                var toZ = xz[i + 1];
                var fromX = xz[i - 2];
                var fromZ = xz[i - 1];
                var dx = toX - fromX;
                var dz = toZ - fromZ;
                var segment = (float)Math.Sqrt(dx * dx + dz * dz);
                if (segment < 0.001f)
                    continue;
                if (segment >= remaining)
                {
                    var t = (segment - remaining) / segment;
                    reverse.Add(fromX + dx * t);
                    reverse.Add(fromZ + dz * t);
                    remaining = 0f;
                    break;
                }

                remaining -= segment;
                reverse.Add(fromX);
                reverse.Add(fromZ);
            }

            var path = new float[reverse.Count];
            for (var i = 0; i < reverse.Count; i += 2)
            {
                path[i] = reverse[reverse.Count - 2 - i];
                path[i + 1] = reverse[reverse.Count - 1 - i];
            }

            return path;
        }

        internal static float PolylineLength(float[] xz)
        {
            if (xz == null || xz.Length < 4)
                return 0f;
            var length = 0.0;
            for (var i = 2; i < xz.Length; i += 2)
            {
                var dx = xz[i] - xz[i - 2];
                var dz = xz[i + 1] - xz[i - 1];
                length += Math.Sqrt(dx * dx + dz * dz);
            }

            return (float)length;
        }
    }
}

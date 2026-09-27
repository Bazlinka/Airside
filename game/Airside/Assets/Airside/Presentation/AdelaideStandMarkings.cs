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
        /// <summary>
        /// ADR 0141: a code E (widebody) box, sized for an A330 or 787 rather than a 737. 42 m wide is
        /// as much as gates 25 and 26L, 42.8 m apart, allow without their boxes crossing.
        /// </summary>
        public const float CodeEEnvelopeWidthMetres = 42f;
        public const float CodeEEnvelopeLengthMetres = 72f;
        public const float TerminalLabelSize = 0.62f;
        public const float ShoulderLengthMetres = 4.5f;

        // The retired stand TextMesh used fontSize 64 and Unity's dynamic-font scale of
        // ten font pixels per world metre. Keep the replacement stroke glyphs at the same
        // physical height so this is a readability upgrade, not an arbitrary resize.
        public static float StrokeLabelScale(float textMeshCharacterSize) =>
            textMeshCharacterSize * 64f / (10f * AirsideStripMarkings.DesignationDigitHeight);

        private static AdelaideStandMarking[] _all;

        public static AdelaideStandMarking[] All()
        {
            if (_all != null)
                return _all;

            _all = new AdelaideStandMarking[AdelaideLayout.Bays.Length + AdelaideGateAlignment.Gates.Length];
            for (var i = 0; i < AdelaideLayout.Bays.Length; i++)
                _all[i] = For(AdelaideLayout.Bays[i]);
            for (var i = 0; i < AdelaideGateAlignment.Gates.Length; i++)
                _all[AdelaideLayout.Bays.Length + i] = For(AdelaideGateAlignment.Gates[i]);
            ShareBoxes(_all);
            return _all;
        }

        /// <summary>
        /// ADR 0141: the L/R lines of one pier (16, 18, 20, 22, 28) are one gate with two stop bars,
        /// painted as one shared box, the way a real MARS stand is, instead of two overlapping boxes.
        /// The first of each pair carries the box; the second paints none.
        /// </summary>
        private static void ShareBoxes(AdelaideStandMarking[] all)
        {
            foreach (var (a, b) in AirlineOperations.SharedPierPairs)
            {
                var ia = Array.FindIndex(all, m => m.StandId == a.Value);
                var ib = Array.FindIndex(all, m => m.StandId == b.Value);
                if (ia < 0 || ib < 0 || IsRemote(a.Value) || IsRemote(b.Value))
                    continue;
                float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
                foreach (var envelope in new[] { all[ia].Envelope, all[ib].Envelope })
                    for (var i = 0; i + 1 < envelope.Length; i += 2)
                    {
                        minX = Math.Min(minX, envelope[i]);
                        maxX = Math.Max(maxX, envelope[i]);
                        minZ = Math.Min(minZ, envelope[i + 1]);
                        maxZ = Math.Max(maxZ, envelope[i + 1]);
                    }

                all[ia] = WithEnvelope(all[ia], new[] { minX, maxZ, maxX, maxZ, maxX, minZ, minX, minZ });
                all[ib] = WithEnvelope(all[ib], Array.Empty<float>());
            }
        }

        /// <summary>A remote (bus) stand keeps its own box: sharing would swallow the next gate's.</summary>
        private static bool IsRemote(string standId)
        {
            foreach (var gate in AdelaideGateAlignment.Gates)
                if (gate.Id == standId)
                    return AdelaideGateAlignment.IsRemote(gate);
            return false;
        }

        /// <summary>True when the stand's box is painted with its pier partner's (ADR 0141).</summary>
        public static bool SharesPartnerBox(string standId)
        {
            foreach (var (a, b) in AirlineOperations.SharedPierPairs)
                if (b.Value == standId)
                    return !IsRemote(a.Value) && !IsRemote(b.Value);
            return false;
        }

        private static AdelaideStandMarking WithEnvelope(AdelaideStandMarking m, float[] envelope) =>
            new(m.StandId, m.Reference, m.LeadIn, m.StopBar, envelope, m.LeftShoulder, m.RightShoulder, m.LabelX,
                m.LabelZ, m.LabelYawDegrees, m.LabelCharacterSize);

        private static AdelaideStandMarking For(AdelaideBay bay) => Create(
            bay.Id, bay.Reference, bay.StopX, bay.StopZ, bay.HeadingDegrees, bay.TaxiIn,
            LeadInLengthMetres, StopBarWidthMetres, LabelBeforeStopMetres,
            RegionalEnvelopeWidthMetres, RegionalEnvelopeLengthMetres, RegionalLabelSize);

        private static AdelaideStandMarking For(AdelaideTerminalGate gate)
        {
            var codeE = AdelaideGateAlignment.SetbackFor(gate.Id) == AdelaideGateAlignment.CodeESetbackMetres;
            var width = Math.Min(codeE ? CodeEEnvelopeWidthMetres : TerminalEnvelopeWidthMetres, RoomBetween(gate));
            return Create(
                gate.Id, gate.Reference, gate.NoseX, gate.NoseZ, gate.HeadingDegrees, gate.TaxiIn,
                TerminalLeadInLengthMetres, TerminalStopBarWidthMetres, TerminalLabelBeforeStopMetres,
                width, codeE ? CodeEEnvelopeLengthMetres : TerminalEnvelopeLengthMetres, TerminalLabelSize);
        }

        /// <summary>
        /// Twice the gap to the nearest gate that is not this one's pier partner, less a line's width:
        /// where two gates are closer than a box is wide (15 and 16R, 25.5 m), each box stops halfway.
        /// </summary>
        private static float RoomBetween(AdelaideTerminalGate gate)
        {
            var room = float.MaxValue;
            foreach (var other in AdelaideGateAlignment.Gates)
            {
                if (other.Id == gate.Id || PierPartners(gate.Id, other.Id))
                    continue;
                // Only neighbours on the same row compete for width.
                if (Math.Abs(other.NoseZ - gate.NoseZ) > 30f)
                    continue;
                room = Math.Min(room, Math.Abs(other.NoseX - gate.NoseX));
            }

            return room - 1f;
        }

        private static bool PierPartners(string a, string b)
        {
            foreach (var (x, y) in AirlineOperations.SharedPierPairs)
                if ((x.Value == a && y.Value == b) || (x.Value == b && y.Value == a))
                    return true;
            return false;
        }

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
        public static float[] PolylineBeforeEnd(float[] xz, float distance, float endX, float endZ)
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

        public static float PolylineLength(float[] xz)
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

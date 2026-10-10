using System;
using System.Collections.Generic;

namespace Airside.Simulation
{
    /// <summary>
    /// ADR 0141 — terminal gates lined up along T1. The OSM-derived stops (ADR 0047) sat anywhere
    /// from 10 to 38 m off the terminal wall, so the parked jets and their boxes read as a ragged row
    /// half out on the apron. Here every contact gate's nose stop moves along its own lead-in to a
    /// common setback from the wall (code C 11 m, code E 14 m), its heading is squared to the wall,
    /// and the last 40 m of its taxi-in and the first metres of its pushback are rebuilt to meet the new
    /// stop. The two remote bus stands (20R, 22R) keep their place but are squared up the same way.
    /// Nothing else about a gate changes: its id, its taxi-out and its routes to the runway stay as
    /// generated. Pure and deterministic.
    /// </summary>
    public static class AdelaideGateAlignment
    {
        public const float CodeCSetbackMetres = 11f;
        public const float CodeESetbackMetres = 14f;

        /// <summary>A stop further than this from the wall is a remote stand, not a contact gate.</summary>
        public const float RemoteStandMetres = 60f;

        /// <summary>The lead-in straight rebuilt before each stop.</summary>
        public const float LeadInMetres = 40f;

        private const float StepMetres = 3f;

        private static AdelaideTerminalGate[] _aligned;
        private static (float X, float Z)[] _face;

        /// <summary>Every terminal gate, aligned. Same order and ids as <see cref="AdelaideLayout.TerminalGates"/>.</summary>
        public static AdelaideTerminalGate[] Gates
        {
            get
            {
                if (_aligned != null)
                    return _aligned;
                var raw = AdelaideLayout.TerminalGates;
                var aligned = new AdelaideTerminalGate[raw.Length];
                for (var i = 0; i < raw.Length; i++)
                    aligned[i] = Align(raw[i]);
                return _aligned = aligned;
            }
        }

        /// <summary>
        /// The code E contact lines, the same as <see cref="AirlineOperations.CodeEGates"/> (a test holds
        /// them equal). Kept here so building the gates never waits on that class's static fields.
        /// </summary>
        public static readonly string[] CodeEGateIds = { "GATE-18", "GATE-20", "GATE-22L", "GATE-25", "GATE-26L", "GATE-28L" };

        public static readonly string[] CodeFGateIds = { "GATE-16R", "GATE-18R", "GATE-20R", "GATE-22R" };

        public static float SetbackFor(string gateId) =>
            Array.IndexOf(CodeEGateIds, gateId) >= 0 || Array.IndexOf(CodeFGateIds, gateId) >= 0 ? CodeESetbackMetres : CodeCSetbackMetres;

        /// <summary>
        /// The T1 airside wall as an x → z line, read from the terminal footprint itself (its
        /// apron-facing vertices), clamped at both ends so the western gates share its line.
        /// </summary>
        public static float WallZAt(float x)
        {
            var face = Face();
            if (x <= face[0].X)
                return face[0].Z;
            for (var i = 0; i < face.Length - 1; i++)
                if (x <= face[i + 1].X)
                {
                    var t = (x - face[i].X) / Math.Max(0.001f, face[i + 1].X - face[i].X);
                    return face[i].Z + (face[i + 1].Z - face[i].Z) * t;
                }

            return face[face.Length - 1].Z;
        }

        /// <summary>Nose heading (degrees) square to the wall at <paramref name="x"/>.</summary>
        public static float SquareHeadingAt(float x)
        {
            var slope = (WallZAt(x + 20f) - WallZAt(x - 20f)) / 40f;
            return (float)(-Math.Atan(slope) * 180.0 / Math.PI);
        }

        private static (float X, float Z)[] Face()
        {
            if (_face != null)
                return _face;
            var points = new List<(float X, float Z)>();
            foreach (var outline in AdelaideLayout.Terminals)
            {
                var xz = outline.Xz;
                var candidate = new List<(float, float)>();
                for (var i = 0; i + 1 < xz.Length; i += 2)
                    candidate.Add((xz[i], xz[i + 1]));
                // The apron side of T1: the outline's lowest-z vertices across its width.
                if (candidate.Count > points.Count)
                    points = candidate;
            }

            var byX = new SortedDictionary<float, float>();
            var minZ = float.MaxValue;
            foreach (var (x, z) in points)
                minZ = Math.Min(minZ, z);
            foreach (var (x, z) in points)
                if (z <= minZ + 3f && (!byX.TryGetValue(x, out var existing) || z < existing))
                    byX[x] = z;
            var face = new List<(float, float)>();
            foreach (var pair in byX)
                face.Add((pair.Key, pair.Value));
            return _face = face.ToArray();
        }

        /// <summary>A bus stand well out on the apron (20R, 22R): it keeps its place.</summary>
        public static bool IsRemote(AdelaideTerminalGate gate) => WallZAt(gate.NoseX) - gate.NoseZ > RemoteStandMetres;

        private static AdelaideTerminalGate Align(AdelaideTerminalGate gate)
        {
            var wall = WallZAt(gate.NoseX);
            var remote = wall - gate.NoseZ > RemoteStandMetres;
            var stopZ = remote ? gate.NoseZ : wall - SetbackFor(gate.Id);
            var heading = SquareHeadingAt(gate.NoseX);
            var taxiIn = RebuildTaxiIn(gate.TaxiIn, gate.NoseX, stopZ);
            var pushback = RebuildPushback(gate.Pushback, gate.NoseX, stopZ);
            return new AdelaideTerminalGate(gate.Id, gate.Reference, gate.NoseX, stopZ, heading,
                taxiIn, pushback, gate.TaxiOut, gate.TaxiOut23);
        }

        /// <summary>The generated route up to the lead-in, then a straight, square run to the new stop.</summary>
        internal static float[] RebuildTaxiIn(float[] route, float stopX, float stopZ)
        {
            var points = ToPoints(route);
            // Drop the old tail from where it passes the new stop.
            while (points.Count > 1 && points[points.Count - 1].Z > stopZ - 0.5f)
                points.RemoveAt(points.Count - 1);
            var (lastX, lastZ) = points[points.Count - 1];
            for (var z = lastZ + StepMetres; z < stopZ - 0.25f; z += StepMetres)
                points.Add((lastX, z));
            points.Add((stopX, stopZ));
            BlendOntoCentreline(points, stopX, stopZ, fromEnd: true);
            return ToArray(points);
        }

        /// <summary>A straight push back from the new stop that rejoins the generated pushback.</summary>
        internal static float[] RebuildPushback(float[] route, float stopX, float stopZ)
        {
            var points = ToPoints(route);
            while (points.Count > 1 && points[0].Z > stopZ - 0.5f)
                points.RemoveAt(0);
            var (firstX, firstZ) = points[0];
            var lead = new List<(float X, float Z)> { (stopX, stopZ) };
            for (var z = stopZ - StepMetres; z > firstZ + 0.25f; z -= StepMetres)
                lead.Add((firstX, z));
            lead.AddRange(points);
            BlendOntoCentreline(lead, stopX, stopZ, fromEnd: false);
            return ToArray(lead);
        }

        /// <summary>
        /// Within <see cref="LeadInMetres"/> of the stop, ease x onto the stand centre line
        /// (smoothstep), so the last stretch is straight and square and the path has no kink.
        /// </summary>
        private static void BlendOntoCentreline(List<(float X, float Z)> points, float stopX, float stopZ, bool fromEnd)
        {
            for (var i = 0; i < points.Count; i++)
            {
                var (x, z) = points[i];
                var toStop = stopZ - z;
                if (toStop < 0f || toStop > LeadInMetres)
                    continue;
                var t = 1f - toStop / LeadInMetres;
                t = t * t * (3f - 2f * t);
                points[i] = (x + (stopX - x) * t, z);
            }
        }

        private static List<(float X, float Z)> ToPoints(float[] xz)
        {
            var points = new List<(float X, float Z)>(xz.Length / 2);
            for (var i = 0; i + 1 < xz.Length; i += 2)
                points.Add((xz[i], xz[i + 1]));
            return points;
        }

        private static float[] ToArray(List<(float X, float Z)> points)
        {
            var xz = new float[points.Count * 2];
            for (var i = 0; i < points.Count; i++)
            {
                xz[i * 2] = points[i].X;
                xz[i * 2 + 1] = points[i].Z;
            }

            return xz;
        }
    }
}

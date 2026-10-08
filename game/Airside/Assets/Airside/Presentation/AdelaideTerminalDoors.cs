using System;
using System.Collections.Generic;
using Airside.Simulation;

namespace Airside.Presentation
{
    /// <summary>One airside door of the terminal: where it is on the building outline and which way it faces.</summary>
    public readonly struct TerminalDoor
    {
        public TerminalDoor(string id, float x, float z, float normalX, float normalZ)
        {
            Id = id; X = x; Z = z; NormalX = normalX; NormalZ = normalZ;
        }

        public string Id { get; }

        /// <summary>The door's centre on the terminal wall, in runway-frame metres.</summary>
        public float X { get; }
        public float Z { get; }

        /// <summary>Unit vector pointing out of the building, onto the apron.</summary>
        public float NormalX { get; }
        public float NormalZ { get; }

        public float ThresholdX => X + NormalX * AdelaideTerminalDoors.ThresholdMetres;
        public float ThresholdZ => Z + NormalZ * AdelaideTerminalDoors.ThresholdMetres;
    }

    /// <summary>
    /// The doors people use to walk out to a stand and back (ADR: terminal doors and people). OpenStreetMap carries the
    /// terminal's outline but not its airside doors, so each regional bay gets one door on the real outline, on the
    /// wall edge nearest it, facing the bay and kept clear of the building's corners. Bays that share a stretch of
    /// wall share a door. Pure (no UnityEngine); the walkway corridors and the boarding walk both start at a door.
    /// </summary>
    public static class AdelaideTerminalDoors
    {
        /// <summary>A door sits at least this far from a wall corner (less on a short wall).</summary>
        public const float CornerClearanceMetres = 12f;

        /// <summary>Doors for bays closer together than this, on the same wall, are one door.</summary>
        public const float MergeMetres = 16f;

        /// <summary>People step out to this far in front of the door before the walk begins.</summary>
        public const float ThresholdMetres = 1.6f;

        public const float WidthMetres = 3.6f;
        public const string TerminalName = "Domestic & International Terminal";

        private static TerminalDoor[] _doors;
        private static Dictionary<string, int> _byBay;

        public static IReadOnlyList<TerminalDoor> Doors
        {
            get { Ensure(); return _doors; }
        }

        /// <summary>The door a regional bay's passengers use.</summary>
        public static bool TryForBay(string bayId, out TerminalDoor door)
        {
            door = default;
            if (string.IsNullOrEmpty(bayId))
                return false;
            Ensure();
            if (!_byBay.TryGetValue(bayId, out var index))
                return false;
            door = _doors[index];
            return true;
        }

        /// <summary>The door nearest a point (for a stand with no door of its own).</summary>
        public static bool TryNearest(float x, float z, out TerminalDoor door)
        {
            Ensure();
            door = default;
            var best = float.MaxValue;
            foreach (var candidate in _doors)
            {
                var d = (candidate.X - x) * (candidate.X - x) + (candidate.Z - z) * (candidate.Z - z);
                if (d >= best)
                    continue;
                best = d;
                door = candidate;
            }

            return best < float.MaxValue;
        }

        private static void Ensure()
        {
            if (_doors != null)
                return;
            float[] outline = null;
            foreach (var terminal in AdelaideLayout.Terminals)
                if (terminal.Name == TerminalName)
                    outline = terminal.Xz;
            var sites = new List<(string Bay, float X, float Z, float Nx, float Nz)>();
            if (outline != null)
                foreach (var bay in AdelaideLayout.Bays)
                    if (SiteFor(outline, bay.StopX, bay.StopZ, out var x, out var z, out var nx, out var nz))
                        sites.Add((bay.Id, x, z, nx, nz));

            var doors = new List<TerminalDoor>();
            var members = new List<List<int>>();
            for (var i = 0; i < sites.Count; i++)
            {
                var joined = -1;
                for (var d = 0; d < doors.Count && joined < 0; d++)
                {
                    var door = doors[d];
                    var dx = door.X - sites[i].X;
                    var dz = door.Z - sites[i].Z;
                    if (dx * dx + dz * dz <= MergeMetres * MergeMetres
                        && door.NormalX * sites[i].Nx + door.NormalZ * sites[i].Nz > 0.9f)
                        joined = d;
                }

                if (joined < 0)
                {
                    doors.Add(new TerminalDoor("DOOR-" + (doors.Count + 1), sites[i].X, sites[i].Z, sites[i].Nx, sites[i].Nz));
                    members.Add(new List<int> { i });
                }
                else
                    members[joined].Add(i);
            }

            // A shared door sits at the middle of the sites it serves.
            _byBay = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var d = 0; d < doors.Count; d++)
            {
                float sx = 0f, sz = 0f;
                foreach (var m in members[d])
                {
                    sx += sites[m].X;
                    sz += sites[m].Z;
                    _byBay[sites[m].Bay] = d;
                }

                var n = members[d].Count;
                doors[d] = new TerminalDoor(doors[d].Id, sx / n, sz / n, doors[d].NormalX, doors[d].NormalZ);
            }

            _doors = doors.ToArray();
        }

        /// <summary>
        /// The point on the outline's nearest wall edge to (<paramref name="tx"/>, <paramref name="tz"/>), pushed in from the
        /// corners, and the unit normal of that edge on the side facing the target.
        /// </summary>
        private static bool SiteFor(float[] outline, float tx, float tz, out float x, out float z, out float nx, out float nz)
        {
            x = z = nx = nz = 0f;
            var n = outline.Length / 2;
            if (n < 3)
                return false;
            var best = float.MaxValue;
            var bestEdge = -1;
            var bestT = 0f;
            for (var i = 0; i < n; i++)
            {
                var j = (i + 1) % n;
                var ax = outline[i * 2];
                var az = outline[i * 2 + 1];
                var bx = outline[j * 2] - ax;
                var bz = outline[j * 2 + 1] - az;
                var l2 = bx * bx + bz * bz;
                if (l2 < 4f)
                    continue;
                var t = Math.Max(0f, Math.Min(1f, ((tx - ax) * bx + (tz - az) * bz) / l2));
                var px = ax + bx * t;
                var pz = az + bz * t;
                var d = (tx - px) * (tx - px) + (tz - pz) * (tz - pz);
                if (d >= best)
                    continue;
                best = d;
                bestEdge = i;
                bestT = t;
            }

            if (bestEdge < 0)
                return false;
            var k = (bestEdge + 1) % n;
            var cx = outline[bestEdge * 2];
            var cz = outline[bestEdge * 2 + 1];
            var ex = outline[k * 2] - cx;
            var ez = outline[k * 2 + 1] - cz;
            var length = (float)Math.Sqrt(ex * ex + ez * ez);
            var margin = Math.Min(CornerClearanceMetres, length * 0.35f);
            var along = Math.Max(margin, Math.Min(length - margin, bestT * length));
            x = cx + ex / length * along;
            z = cz + ez / length * along;
            nx = -ez / length;
            nz = ex / length;
            if (nx * (tx - x) + nz * (tz - z) < 0f)
            {
                nx = -nx;
                nz = -nz;
            }

            return true;
        }
    }
}

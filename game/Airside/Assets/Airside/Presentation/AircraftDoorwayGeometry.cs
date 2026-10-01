using System;

namespace Airside.Presentation
{
    /// <summary>
    /// The shape of the hollow behind an aircraft door (presentation only, no UnityEngine).
    ///
    /// A door leaf is a thin closed shell cut from the fuselage skin by the aircraft generators
    /// (aircraft_skin.skin_patch): its front face is a stack of rounded outlines, each a scaled copy of
    /// the one before, ending in a centre vertex, and every vertex lies on the real skin. The back face
    /// repeats those points a few millimetres below. Folding the leaf away used to leave bare hull behind
    /// it, so a passenger walking up the steps vanished into a solid wall.
    ///
    /// This builds smaller copies of that front face — interpolated between the leaf's own rings, so they
    /// follow whatever cross-section the fuselage has — and lifts them a little along the outward
    /// direction. The runtime stacks a dark reveal, a black cabin and a lit vestibule so the open door
    /// reads as a doorway.
    /// </summary>
    public static class AircraftDoorwayGeometry
    {
        /// <summary>A leaf's front and back faces are this close (metres); anything else is not a door shell.</summary>
        public const float MinShellThickness = 0.004f;
        public const float MaxShellThickness = 0.05f;

        /// <summary>Rings the generators give every door (the outline, three inner copies, then the centre).</summary>
        public const int DoorRings = 3;

        /// <summary>Outlines in each layer: the layer's own edge and one halfway to the centre.</summary>
        private const int LayerRings = 2;

        /// <summary>
        /// A layer covering <paramref name="scale"/> of the leaf's outline (1 = all of it), raised
        /// <paramref name="lift"/> metres along the outward direction from the leaf's front face.
        /// Returns false when the vertices are not a front/back door shell.
        /// </summary>
        /// <param name="positions">x, y, z per vertex: front face first, back face second.</param>
        public static bool TryBuild(float[] positions, float scale, float lift, out float[] vertices, out int[] triangles)
        {
            vertices = null;
            triangles = null;
            if (positions == null || positions.Length % 6 != 0 || scale <= 0f || scale > 1f)
                return false;

            var half = positions.Length / 6;
            if (half < 1 + (DoorRings + 1) * 8 || (half - 1) % (DoorRings + 1) != 0)
                return false;
            var n = (half - 1) / (DoorRings + 1);

            // Outward direction at each front vertex: front minus back.
            var outward = new float[half * 3];
            for (var i = 0; i < half; i++)
            {
                var dx = positions[i * 3] - positions[(i + half) * 3];
                var dy = positions[i * 3 + 1] - positions[(i + half) * 3 + 1];
                var dz = positions[i * 3 + 2] - positions[(i + half) * 3 + 2];
                var length = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
                if (length < MinShellThickness || length > MaxShellThickness)
                    return false;
                outward[i * 3] = dx / length;
                outward[i * 3 + 1] = dy / length;
                outward[i * 3 + 2] = dz / length;
            }

            var centre = half - 1;
            // Vertex i of ring k (k = DoorRings + 1 is the centre, the same for every i).
            int Index(int ring, int i) => ring > DoorRings ? centre : ring * n + i;

            var count = LayerRings * n + 1;
            vertices = new float[count * 3];
            for (var m = 0; m < LayerRings; m++)
            {
                var sigma = scale * (1f - m / (float)LayerRings);
                // Leaf ring k sits at scale 1 - k/(DoorRings + 1); find the pair this scale falls between.
                var span = DoorRings + 1;
                var k = Math.Min(DoorRings, (int)Math.Floor((1f - sigma) * span + 1e-4f));
                var f = (1f - sigma) * span - k;
                for (var i = 0; i < n; i++)
                {
                    var a = Index(k, i) * 3;
                    var b = Index(k + 1, i) * 3;
                    var v = (m * n + i) * 3;
                    var ox = outward[a] + (outward[b] - outward[a]) * f;
                    var oy = outward[a + 1] + (outward[b + 1] - outward[a + 1]) * f;
                    var oz = outward[a + 2] + (outward[b + 2] - outward[a + 2]) * f;
                    var ol = (float)Math.Sqrt(ox * ox + oy * oy + oz * oz);
                    for (var c = 0; c < 3; c++)
                    {
                        var o = (c == 0 ? ox : c == 1 ? oy : oz) / ol;
                        vertices[v + c] = positions[a + c] + (positions[b + c] - positions[a + c]) * f + o * lift;
                    }
                }
            }

            var last = LayerRings * n;
            for (var c = 0; c < 3; c++)
                vertices[last * 3 + c] = positions[centre * 3 + c] + outward[centre * 3 + c] * lift;

            var tris = new System.Collections.Generic.List<int>();
            for (var m = 0; m < LayerRings - 1; m++)
                for (var i = 0; i < n; i++)
                {
                    var j = (i + 1) % n;
                    int a = m * n + i, b = m * n + j, c = (m + 1) * n + j, d = (m + 1) * n + i;
                    tris.AddRange(new[] { a, b, c, a, c, d });
                }
            for (var i = 0; i < n; i++)
                tris.AddRange(new[] { (LayerRings - 1) * n + i, (LayerRings - 1) * n + (i + 1) % n, last });

            // Face the same way as the skin: the first triangle's normal must agree with the outward direction.
            var t0 = tris[0] * 3; var t1 = tris[1] * 3; var t2 = tris[2] * 3;
            var e1x = vertices[t1] - vertices[t0]; var e1y = vertices[t1 + 1] - vertices[t0 + 1]; var e1z = vertices[t1 + 2] - vertices[t0 + 2];
            var e2x = vertices[t2] - vertices[t0]; var e2y = vertices[t2 + 1] - vertices[t0 + 1]; var e2z = vertices[t2 + 2] - vertices[t0 + 2];
            var nx = e1y * e2z - e1z * e2y; var ny = e1z * e2x - e1x * e2z; var nz = e1x * e2y - e1y * e2x;
            var o0 = Index(0, 0) * 3;
            if (nx * outward[o0] + ny * outward[o0 + 1] + nz * outward[o0 + 2] < 0f)
                for (var t = 0; t + 2 < tris.Count; t += 3)
                    (tris[t + 1], tris[t + 2]) = (tris[t + 2], tris[t + 1]);

            triangles = tris.ToArray();
            return true;
        }
    }
}

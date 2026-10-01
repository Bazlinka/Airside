using System;

namespace Airside.Simulation
{
    /// <summary>
    /// The open front of a hangar (ADR 0213). A hangar is drawn hollow, with a real doorway through its front wall, so the
    /// aircraft towed in for a check is seen going in at the door the building actually has. Both the building mesh and
    /// <see cref="HangarTow"/> ask this class where that door is, so they cannot disagree. Pure maths over the footprint.
    /// </summary>
    public readonly struct HangarFrontage
    {
        public HangarFrontage(int edgeIndex, float outX, float outZ, float edgeLength, float doorWidth, float doorHeight)
        {
            EdgeIndex = edgeIndex;
            OutX = outX;
            OutZ = outZ;
            EdgeLength = edgeLength;
            DoorWidth = doorWidth;
            DoorHeight = doorHeight;
        }

        /// <summary>Index of the footprint edge (point i to point i + 1) the door is in.</summary>
        public int EdgeIndex { get; }

        /// <summary>Axis-aligned outward direction of the front wall: (±1, 0) or (0, ±1).</summary>
        public float OutX { get; }

        public float OutZ { get; }
        public float EdgeLength { get; }

        /// <summary>Clear width of the opening, centred on the front wall.</summary>
        public float DoorWidth { get; }

        /// <summary>Clear height of the opening.</summary>
        public float DoorHeight { get; }
    }

    public static class HangarFront
    {
        /// <summary>The door leaves stack on the wall either side of the opening, at least this wide each.</summary>
        public const float StackMetres = 3.2f;

        public const float MaxDoorWidthMetres = 64f;
        public const float MinFrontMetres = 14f;
        public const float MinHeightMetres = 6f;

        /// <summary>The opening is this far below the eaves, leaving a header beam.</summary>
        public const float HeaderMetres = 1.2f;

        /// <summary>The ceiling hangs this far under the roof.</summary>
        public const float CeilingDropMetres = 0.35f;

        /// <summary>
        /// The longest wall that faces the field centre (the runways sit round the world origin), else the longest wall:
        /// the side doors go on. -1 when the footprint has no edge.
        /// </summary>
        public static int FrontEdge(float[] xz)
        {
            var count = xz == null ? 0 : xz.Length / 2;
            if (count < 3)
                return -1;
            var winding = SignedArea(xz) >= 0f ? 1f : -1f;
            var cx = 0f;
            var cz = 0f;
            for (var i = 0; i < count; i++)
            {
                cx += xz[i * 2];
                cz += xz[i * 2 + 1];
            }

            cx /= count;
            cz /= count;
            var best = -1;
            var bestFacing = -1;
            var bestLength = 0f;
            var bestAny = -1;
            var bestAnyLength = 0f;
            for (var i = 0; i < count; i++)
            {
                var j = (i + 1) % count;
                var dx = xz[j * 2] - xz[i * 2];
                var dz = xz[j * 2 + 1] - xz[i * 2 + 1];
                var length = (float)Math.Sqrt(dx * dx + dz * dz);
                if (length > bestAnyLength)
                {
                    bestAny = i;
                    bestAnyLength = length;
                }

                if (length < 1e-5f)
                    continue;
                var outX = dz / length * winding;
                var outZ = -dx / length * winding;
                var facing = outX * -cx + outZ * -cz > 0f ? 1 : 0;
                if (length < 8f)
                    continue;
                if (facing > bestFacing || (facing == bestFacing && length > bestLength))
                {
                    best = i;
                    bestFacing = facing;
                    bestLength = length;
                }
            }

            return best >= 0 ? best : bestAny;
        }

        /// <summary>
        /// A hangar can be drawn open when its front wall is straight, square to the axes and spans the building, so the
        /// doorway lines up with the tow route down the middle of that side. False for the awkward footprints, which keep
        /// a closed door and are not used for tows.
        /// </summary>
        public static bool TryFor(float[] xz, float heightMetres, out HangarFrontage frontage)
        {
            frontage = default;
            if (xz == null || xz.Length < 8 || heightMetres < MinHeightMetres)
                return false;
            var edge = FrontEdge(xz);
            if (edge < 0)
                return false;
            var count = xz.Length / 2;
            var j = (edge + 1) % count;
            var ax = xz[edge * 2];
            var az = xz[edge * 2 + 1];
            var dx = xz[j * 2] - ax;
            var dz = xz[j * 2 + 1] - az;
            var length = (float)Math.Sqrt(dx * dx + dz * dz);
            if (length < MinFrontMetres)
                return false;
            dx /= length;
            dz /= length;
            var winding = SignedArea(xz) >= 0f ? 1f : -1f;
            var outX = dz * winding;
            var outZ = -dx * winding;
            // Square to the axes, because the tow brings an aircraft in straight along one of them.
            float snapX, snapZ;
            if (Math.Abs(outX) >= 0.985f)
            {
                snapX = Math.Sign(outX);
                snapZ = 0f;
            }
            else if (Math.Abs(outZ) >= 0.985f)
            {
                snapX = 0f;
                snapZ = Math.Sign(outZ);
            }
            else
                return false;

            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            for (var i = 0; i < count; i++)
            {
                minX = Math.Min(minX, xz[i * 2]);
                maxX = Math.Max(maxX, xz[i * 2]);
                minZ = Math.Min(minZ, xz[i * 2 + 1]);
                maxZ = Math.Max(maxZ, xz[i * 2 + 1]);
            }

            // The wall must sit on the building's outer side and run most of its width, so the middle of the wall is the
            // middle of the building.
            var midX = ax + (xz[j * 2] - ax) * 0.5f;
            var midZ = az + (xz[j * 2 + 1] - az) * 0.5f;
            float side, across, extent, centre;
            if (snapX != 0f)
            {
                side = snapX > 0f ? maxX - midX : midX - minX;
                extent = maxZ - minZ;
                centre = (minZ + maxZ) * 0.5f;
                across = midZ;
            }
            else
            {
                side = snapZ > 0f ? maxZ - midZ : midZ - minZ;
                extent = maxX - minX;
                centre = (minX + maxX) * 0.5f;
                across = midX;
            }

            if (side > 0.8f || length < extent * 0.85f || Math.Abs(across - centre) > 1.5f)
                return false;

            var width = Math.Min(MaxDoorWidthMetres, length - 2f * StackMetres);
            var height = heightMetres - HeaderMetres;
            if (width < 8f)
                return false;
            frontage = new HangarFrontage(edge, snapX, snapZ, length, width, height);
            return true;
        }

        private static float SignedArea(float[] xz)
        {
            var count = xz.Length / 2;
            var area = 0f;
            for (var i = 0; i < count; i++)
            {
                var j = (i + 1) % count;
                area += xz[i * 2] * xz[j * 2 + 1] - xz[j * 2] * xz[i * 2 + 1];
            }

            return area * 0.5f;
        }
    }
}

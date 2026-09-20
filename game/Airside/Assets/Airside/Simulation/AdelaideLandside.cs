namespace Airside.Simulation
{
    /// <summary>
    /// Authored T1 traffic-side geometry in the runway frame. The OSM road dump
    /// already has Sir Richard Williams and the car parks, but
    /// <see cref="AdelaideLandCover.InOperationalCore"/> swallows the terminal
    /// north face (z 430–550). These ribbons fill that notch and the drop-off
    /// that aerial view was missing.
    /// </summary>
    public static class AdelaideLandside
    {
        public const float PrecinctMinX = 750f;
        public const float PrecinctMaxX = 1750f;
        public const float PrecinctMinZ = 430f;
        public const float PrecinctMaxZ = 920f;

        public const float DropOffWidth = 14f;
        public const float LoopWidth = 10f;
        public const float ConnectorWidth = 9f;

        /// <summary>Kerbside boulevard just north of the T1 roof (roof ends ~z 474).</summary>
        public static readonly float[] DropOff = { 900f, 508f, 1650f, 508f };

        /// <summary>Return lane through the short-stay / pick-up loop.</summary>
        public static readonly float[] ReturnLoop =
        {
            1650f, 508f, 1695f, 555f, 1695f, 595f, 900f, 595f, 855f, 555f, 855f, 508f, 900f, 508f
        };

        /// <summary>West stub toward Tapleys / Harbour Town.</summary>
        public static readonly float[] WestConnector = { 855f, 530f, 620f, 560f, 280f, 640f };

        /// <summary>East stub toward James Melrose / Sir Richard Williams.</summary>
        public static readonly float[] EastConnector = { 1695f, 575f, 1860f, 650f, 2140f, 790f };

        public const float CarParkCentreX = 1275f;
        public const float CarParkCentreZ = 700f;
        public const float CarParkHalfX = 240f;
        public const float CarParkHalfZ = 85f;

        public static bool Contains(float x, float z) =>
            x >= PrecinctMinX && x <= PrecinctMaxX && z >= PrecinctMinZ && z <= PrecinctMaxZ;

        /// <summary>Centreline ribbons drawn even inside the operational core.</summary>
        public static readonly (float Width, float[] Xz)[] Ribbons =
        {
            (DropOffWidth, DropOff),
            (LoopWidth, ReturnLoop),
            (ConnectorWidth, WestConnector),
            (ConnectorWidth, EastConnector)
        };

        /// <summary>Parked-car slots in the short-stay pad, nose toward the aisle.</summary>
        public static LandsideSlot[] CarParkSlots()
        {
            const int cols = 8;
            const int rows = 3;
            var slots = new LandsideSlot[cols * rows + 6];
            var i = 0;
            for (var row = 0; row < rows; row++)
            {
                for (var col = 0; col < cols; col++)
                {
                    var x = CarParkCentreX - 150f + col * 42f;
                    var z = CarParkCentreZ - 40f + row * 38f;
                    slots[i++] = new LandsideSlot(x, z, row == 1 ? 0f : 180f);
                }
            }

            // Kerbside drop-off, facing along the boulevard.
            float[] dropX = { 980f, 1085f, 1190f, 1295f, 1400f, 1505f };
            for (var d = 0; d < dropX.Length; d++)
                slots[i++] = new LandsideSlot(dropX[d], 498f, 90f);
            return slots;
        }

        /// <summary>Lamp posts along the drop-off and the return loop.</summary>
        public static LandsideSlot[] Streetlights() => new[]
        {
            new LandsideSlot(940f, 518f, 0f),
            new LandsideSlot(1060f, 518f, 0f),
            new LandsideSlot(1180f, 518f, 0f),
            new LandsideSlot(1300f, 518f, 0f),
            new LandsideSlot(1420f, 518f, 0f),
            new LandsideSlot(1540f, 518f, 0f),
            new LandsideSlot(1660f, 518f, 0f),
            new LandsideSlot(980f, 585f, 180f),
            new LandsideSlot(1220f, 585f, 180f),
            new LandsideSlot(1460f, 585f, 180f),
            new LandsideSlot(1680f, 585f, 180f)
        };

        public static int CountOsmRoadPointsInPrecinct()
        {
            var roads = AdelaideLandCover.Roads;
            if (roads == null || roads.Length < 3)
                return 0;
            var i = 0;
            var n = 0;
            while (i < roads.Length)
            {
                var width = roads[i++];
                if (width <= 0f || i >= roads.Length)
                    break;
                var count = (int)roads[i++];
                if (count < 2 || i + count * 2 > roads.Length)
                    break;
                for (var p = 0; p < count; p++)
                {
                    var x = roads[i + p * 2];
                    var z = roads[i + p * 2 + 1];
                    if (Contains(x, z))
                        n++;
                }

                i += count * 2;
            }

            return n;
        }
    }

    public readonly struct LandsideSlot
    {
        public LandsideSlot(float x, float z, float yawDegrees)
        {
            X = x;
            Z = z;
            YawDegrees = yawDegrees;
        }

        public float X { get; }
        public float Z { get; }
        public float YawDegrees { get; }
    }
}

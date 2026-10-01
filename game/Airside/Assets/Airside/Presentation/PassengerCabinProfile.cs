namespace Airside.Presentation
{
    // Window stations fit the existing metre-authored exterior kits; seat groups are
    // representative economy layouts, not a specific airline's configurable cabin.
    public sealed class PassengerCabinProfile
    {
        public string TypeId { get; }
        public float HalfWidth { get; }
        public float WindowY { get; }
        public float WindowZ { get; }
        public float Pitch { get; }
        public int[] SeatGroups { get; }
        private PassengerCabinProfile(string id, float width, float y, float z, float pitch, int[] groups)
        { TypeId=id; HalfWidth=width; WindowY=y; WindowZ=z; Pitch=pitch; SeatGroups=groups; }
        public static readonly PassengerCabinProfile[] All =
        {
            new("SF34", 1.080f, 2.288f, 1.682f, 0.76f, new[] { 1,2 }),
            new("ATR42", 1.340f, 2.168f, 2.048f, 0.76f, new[] { 2,2 }),
            new("DH8D", 1.295f, 2.566f, 3.650f, 0.76f, new[] { 2,2 }),
            new("B738", 1.795f, 4.833f, -14.517f, 0.79f, new[] { 3,3 }),
            new("B38M", 1.795f, 4.802f, -14.517f, 0.79f, new[] { 3,3 }),
            new("A320", 1.892f, 4.642f, -13.960f, 0.79f, new[] { 3,3 }),
            new("A21N", 1.789f, 4.547f, -16.371f, 0.79f, new[] { 3,3 }),
            new("E190", 1.430f, 3.947f, -12.710f, 0.79f, new[] { 2,2 }),
            new("A223", 1.673f, 3.800f, -14.714f, 0.79f, new[] { 2,3 }),
            new("A359", 2.836f, 7.133f, -26.026f, 0.79f, new[] { 3,3,3 }),
            new("A339", 2.683f, 7.025f, -24.803f, 0.79f, new[] { 2,4,2 }),
            new("B789", 2.745f, 7.121f, -24.471f, 0.79f, new[] { 3,3,3 }),
            new("B78X", 2.745f, 7.121f, -26.610f, 0.79f, new[] { 3,3,3 }),
        };
        public static bool TryFor(string id, out PassengerCabinProfile profile)
        {
            foreach (var candidate in All) if (candidate.TypeId == id) { profile=candidate; return true; }
            profile=null; return false;
        }
    }
}

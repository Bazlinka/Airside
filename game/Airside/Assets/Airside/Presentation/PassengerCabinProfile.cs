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
        // Seat pitch is independent of the exterior pane spacing below.
        public float Pitch { get; }
        public float WindowPitch { get; }
        // Full aperture dimensions in the cabin Y/Z plane, metres.
        public float WindowWidth { get; }
        public float WindowHeight { get; }
        public float RevealDepth { get; }
        // Lining sits inboard of the exterior pane datum; the reveal returns to that plane.
        public float LiningHalfWidth => HalfWidth - RevealDepth;
        // Superellipse exponent: 2 is an ellipse; larger values square the shoulders.
        public float WindowSquareness { get; }
        // Relative to the retained window-eye datum, not world/aircraft ground Y.
        public float FloorY { get; }
        public float CeilingY { get; }
        // Bounded visual continuation, not an operator's full cabin or seat capacity.
        public float CabinLength { get; }
        public float AisleWidth { get; }
        public bool HasElectronicDimming { get; }
        public float SeatEyeInset { get; }
        public int[] SeatGroups { get; }
        private PassengerCabinProfile(string id, float width, float y, float z, float pitch, int[] groups,
            float windowPitch, float windowWidth, float windowHeight, float cabinLength,
            float revealDepth = .065f, float windowSquareness = 2.6f,
            float floorY = -1.02f, float ceilingY = .99f, float aisleWidth = .44f,
            bool electronicDimming = false, float seatEyeInset = .43f)
        {
            TypeId=id; HalfWidth=width; WindowY=y; WindowZ=z; Pitch=pitch; SeatGroups=groups;
            WindowPitch=windowPitch; WindowWidth=windowWidth; WindowHeight=windowHeight;
            RevealDepth=revealDepth; WindowSquareness=windowSquareness; FloorY=floorY;
            CeilingY=ceilingY; CabinLength=cabinLength; AisleWidth=aisleWidth;
            HasElectronicDimming=electronicDimming; SeatEyeInset=seatEyeInset;
        }
        // Pane spacing/bounds were measured from separate connected panes in the shipped
        // glTFs near each existing station, corroborated with their source generators.
        // Y heights are projected skin bounds, not manufacturer-certified dimensions.
        // Reveals, squareness, section heights, aisles and continuation lengths are authored
        // lining choices. Existing lateral/eye fit and representative seat layouts remain.
        // See docs/art/aircraft-immersion-audit-2026-10-06/aircraft/{ATR42,A320,B789}.md.
        // IMPORTANT: the 787 kit inherits scaled A350 panes. Its small apertures and
        // different -9/-10 longitudinal scale are source limitations; a real large-window
        // upgrade requires a coordinated exterior/interior change, beyond this slice.
        // Hero WindowZ now selects an individual left pane rather than the old pair midpoint:
        // ATR42 cabin_window_4: 2.048 -> 1.794158 (-.253842 m);
        // A320 cabin_window_7: -13.960 -> -14.295 (-.335 m);
        // B789 cabin_window_17: -24.471 -> -24.232627 (+.238373 m).
        // Remaining WindowZ stations select their nearest individual left pane (mesh components,
        // not merged-node bounds). X/Y retain the fitted eye datum: the pane X centre is
        // .015 m farther out and every retained Y is within .0005 m of the pane centre.
        // Uniform repeats match local pane pitch; curved/end sections are not certified
        // by this central-pane fit. Symmetric continuation fits within each measured pane belt:
        // SF34/ATR42 shorten to 7 m and E190 to 14 m to avoid crossing its forward end.
        // A21N extends to 19 m versus A320's 16; B78X to 24 m versus B789's 20.
        // These are bounded visual sections, not full-cabin/operator dimensions.
        public static readonly PassengerCabinProfile[] All =
        {
            new("SF34", 1.080f, 2.288f, 1.936f, 0.76f, new[] { 1,2 }, .508f, .240f, .329f, 7f),
            new("ATR42", 1.340f, 2.168f, 1.794158f, 0.76f, new[] { 2,2 }, .508f, .240f, .330f, 7f, revealDepth: .075f, windowSquareness: 2.6f, ceilingY: .90f),
            new("DH8D", 1.295f, 2.566f, 3.396f, 0.76f, new[] { 2,2 }, .508f, .240f, .329f, 12f),
            new("B738", 1.795f, 4.833f, -14.263f, 0.79f, new[] { 3,3 }, .508f, .250f, .350f, 16f),
            new("B38M", 1.795f, 4.802f, -14.263f, 0.79f, new[] { 3,3 }, .508f, .250f, .348f, 16f),
            new("A320", 1.892f, 4.642f, -14.295f, 0.79f, new[] { 3,3 }, .670f, .220f, .329f, 16f, revealDepth: .085f, windowSquareness: 2.8f),
            new("A21N", 1.789f, 4.547f, -16.657136f, 0.79f, new[] { 3,3 }, .572866f, .281923f, .329f, 19f),
            new("E190", 1.430f, 3.947f, -13.103001f, 0.79f, new[] { 2,2 }, .787f, .230f, .319f, 14f),
            new("A223", 1.673f, 3.800f, -15.107f, 0.79f, new[] { 2,3 }, .787f, .250f, .349f, 16f),
            new("A359", 2.836f, 7.133f, -25.772001f, 0.79f, new[] { 3,3,3 }, .508f, .250f, .345f, 20f),
            new("A339", 2.683f, 7.025f, -25.044684f, 0.79f, new[] { 2,4,2 }, .484120f, .238249f, .340f, 20f),
            new("B789", 2.745f, 7.121f, -24.232627f, 0.79f, new[] { 3,3,3 }, .477656f, .235067f, .345f, 20f, revealDepth: .12f, windowSquareness: 2.6f, ceilingY: 1.15f, aisleWidth: .46f, electronicDimming: true),
            new("B78X", 2.745f, 7.121f, -26.350715f, 0.79f, new[] { 3,3,3 }, .519407f, .255613f, .345f, 24f, revealDepth: .12f, windowSquareness: 2.6f, ceilingY: 1.15f, aisleWidth: .46f, electronicDimming: true),
        };
        public static bool TryFor(string id, out PassengerCabinProfile profile)
        {
            foreach (var candidate in All) if (candidate.TypeId == id) { profile=candidate; return true; }
            profile=null; return false;
        }
    }
}

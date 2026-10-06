namespace Airside.Presentation
{
    public enum JetFlightDeck { Boeing737Ng, Boeing737Max, AirbusClassic, AirbusA350, Boeing787, AirbusA220, Embraer }

    /// <summary>Explicit metre-authored fits to each runtime kit's nose-stop datum.
    /// Eye height/Z come from inspected windscreen mesh bounds, not aircraft length scaling.</summary>
    public sealed class JetCockpitProfile
    {
        public string TypeId { get; }
        public JetFlightDeck Deck { get; }
        public float EyeY { get; }
        public float EyeZ { get; }
        public float HalfWidth { get; }
        public bool Sidestick => Deck == JetFlightDeck.AirbusClassic || Deck == JetFlightDeck.AirbusA350 || Deck == JetFlightDeck.AirbusA220;
        public int DisplayCount => Deck == JetFlightDeck.Boeing737Max ? 4 :
            Deck == JetFlightDeck.Boeing787 || Deck == JetFlightDeck.AirbusA220 || Deck == JetFlightDeck.Embraer ? 5 : 6;
        // Simplified visual stations, not certified aircraft dimensions. All six A350
        // units share one format; lateral OIS units turn toward the seated pilots.
        public readonly struct DisplayStation
        {
            public readonly float X, Y, Width, Height, Yaw;
            public readonly string Title;
            public readonly int Pilot;
            public DisplayStation(float x, float y, string title, int pilot = -1, float yaw = 0f)
            { X = x; Y = y; Width = 0.46f; Height = 0.32f; Title = title; Pilot = pilot; Yaw = yaw; }
        }
        public static readonly System.Collections.Generic.IReadOnlyList<DisplayStation> A350Displays =
            System.Array.AsReadOnly(new[]
            {
                new DisplayStation(-1.04f, -0.40f, "OIS", yaw: -18f),
                new DisplayStation(-0.51f, -0.40f, "PFD / ND", 0),
                new DisplayStation(0f, -0.40f, "ECAM"),
                new DisplayStation(0.51f, -0.40f, "PFD / ND", 1),
                new DisplayStation(1.04f, -0.40f, "OIS", yaw: 18f),
                new DisplayStation(0f, -0.74f, "SYSTEM"),
            });
        private JetCockpitProfile(string id, JetFlightDeck deck, float y, float z, float width)
        { TypeId = id; Deck = deck; EyeY = y; EyeZ = z; HalfWidth = width; }

        public static readonly JetCockpitProfile[] All =
        {
            new("B738", JetFlightDeck.Boeing737Ng, 5.62f, -2.65f, 1.15f),
            new("B38M", JetFlightDeck.Boeing737Max, 5.59f, -2.65f, 1.15f),
            new("A320", JetFlightDeck.AirbusClassic, 5.46f, -2.95f, 1.24f),
            new("A21N", JetFlightDeck.AirbusClassic, 5.32f, -2.85f, 1.24f),
            new("E190", JetFlightDeck.Embraer, 4.52f, -2.70f, 1.05f),
            new("A223", JetFlightDeck.AirbusA220, 4.38f, -3.05f, 1.14f),
            new("A359", JetFlightDeck.AirbusA350, 7.92f, -4.45f, 1.66f),
            new("A339", JetFlightDeck.AirbusClassic, 7.92f, -3.55f, 1.62f),
            new("B789", JetFlightDeck.Boeing787, 7.70f, -4.30f, 1.63f),
            new("B78X", JetFlightDeck.Boeing787, 7.70f, -4.65f, 1.63f),
        };
        public static bool TryFor(string id, out JetCockpitProfile profile)
        {
            foreach (var candidate in All)
                if (candidate.TypeId == id) { profile = candidate; return true; }
            profile = null; return false;
        }
    }
}

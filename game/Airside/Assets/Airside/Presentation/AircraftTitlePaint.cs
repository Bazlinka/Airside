using Airside.Domain;

namespace Airside.Presentation
{
    /// <summary>
    /// How big the painted operator title and registration on a fuselage side should be.
    ///
    /// Unity renders a dynamic-font <c>TextMesh</c> at ten font pixels per world unit, so a
    /// line's world height is <c>fontSize * characterSize / 10</c> — at font size 64 that is
    /// 6.4x the character size, not the character size itself. Getting that factor wrong is
    /// how the titles ended up with a dark backing plate a fraction of their own size sitting
    /// in the middle of the airline name.
    ///
    /// UnityEngine-free so the sizing is covered by the headless harness.
    /// </summary>
    public static class AircraftTitlePaint
    {
        /// <summary>Font pixel size every fuselage mark is rendered at.</summary>
        public const int FontPixelSize = 64;

        /// <summary>Unity's TextMesh scale: ten font pixels to one world unit at character size 1.</summary>
        public const float FontPixelsPerMetre = 10f;

        /// <summary>
        /// Average advance of an upper-case glyph as a fraction of the line height, for the
        /// sans face TextMesh falls back to. An estimate — it sizes paint, not a hit target.
        /// </summary>
        public const float AverageAdvanceFraction = 0.62f;

        /// <summary>
        /// Airline titles sit forward of the wing and stop well short of the tail. Real
        /// narrowbody titles run about a third of the fuselage; this is the ceiling, and a
        /// long airline name is shrunk to fit rather than painted off the end of the aeroplane.
        /// </summary>
        public const float TitleLengthFraction = 0.34f;

        /// <summary>World height of one line at <paramref name="characterSize"/>.</summary>
        public static float LineHeightMetres(float characterSize) =>
            FontPixelSize * characterSize / FontPixelsPerMetre;

        /// <summary>Estimated painted length of <paramref name="text"/>, in metres.</summary>
        public static float WidthMetres(string text, float characterSize) =>
            string.IsNullOrEmpty(text)
                ? 0f
                : text.Length * LineHeightMetres(characterSize) * AverageAdvanceFraction;

        /// <summary>
        /// <paramref name="preferred"/>, reduced just enough that <paramref name="text"/> fits
        /// inside <paramref name="maxLengthMetres"/>. Never enlarges a short name.
        /// </summary>
        public static float FitCharacterSize(string text, float preferred, float maxLengthMetres)
        {
            if (string.IsNullOrEmpty(text) || preferred <= 0f || maxLengthMetres <= 0f)
                return preferred;
            var width = WidthMetres(text, preferred);
            if (width <= maxLengthMetres)
                return preferred;
            return preferred * (maxLengthMetres / width);
        }

        /// <summary>How long a title may be on this type, from its real fuselage length.</summary>
        public static float TitleLengthBudgetMetres(AircraftType type)
        {
            if (type == null)
                return 8f;
            return AircraftCatalogue.TryFor(type, out var spec)
                ? (float)spec.LengthMetres * TitleLengthFraction
                : 8f;
        }

        /// <summary>
        /// The character size to paint an airline's titles at on this type: the authored
        /// size, shrunk if the name is long enough to run past the fuselage.
        /// </summary>
        public static float OperatorCharacterSize(AircraftType type, string operatorTitle, float authored) =>
            FitCharacterSize(operatorTitle, authored, TitleLengthBudgetMetres(type));

        /// <summary>
        /// Titles sit on white metal. Pale accents (yellow, ice-blue) vanish on
        /// that skin; near-black still reads as a dark wordmark. Mid-range
        /// operator colours stay as painted.
        /// </summary>
        public static bool AccentReadsOnWhiteMetal(byte r, byte g, byte b)
        {
            var luminance = (0.2126 * r + 0.7152 * g + 0.0722 * b) / 255.0;
            return luminance >= 0.14 && luminance <= 0.72;
        }
    }

    /// <summary>
    /// Local-space placement for the painted operator title and registration on an
    /// aircraft's two fuselage sides. The authored 737 uses a nose-stop origin while
    /// the turboprops use a centred origin, so one generic offset cannot fit them all.
    /// </summary>
    public readonly struct AircraftIdentityMarkingLayout
    {
        public AircraftIdentityMarkingLayout(
            float sideX,
            float operatorY,
            float operatorZ,
            float registrationY,
            float registrationZ,
            float operatorCharacterSize,
            float registrationCharacterSize)
        {
            SideX = sideX;
            OperatorY = operatorY;
            OperatorZ = operatorZ;
            RegistrationY = registrationY;
            RegistrationZ = registrationZ;
            OperatorCharacterSize = operatorCharacterSize;
            RegistrationCharacterSize = registrationCharacterSize;
        }

        public float SideX { get; }
        public float OperatorY { get; }
        public float OperatorZ { get; }
        public float RegistrationY { get; }
        public float RegistrationZ { get; }
        public float OperatorCharacterSize { get; }
        public float RegistrationCharacterSize { get; }
    }

    /// <summary>
    /// Where each type's titles sit. Free of UnityEngine — it is only floats — so the
    /// harness can check every authored size against <see cref="AircraftTitlePaint"/>.
    /// </summary>
    public static class AircraftIdentityMarkings
    {
        public static AircraftIdentityMarkingLayout For(AircraftType type)
        {
            if (Is(type, AircraftType.Boeing7378))
                return new AircraftIdentityMarkingLayout(1.98f, 4.05f, -9.0f, 3.92f, -31.0f, 0.22f, 0.13f);
            if (Is(type, AircraftType.Boeing737800))
                return new AircraftIdentityMarkingLayout(1.98f, 4.08f, -9.0f, 3.95f, -31.0f, 0.22f, 0.13f);
            if (Is(type, AircraftType.AirbusA320200))
                return new AircraftIdentityMarkingLayout(2.06f, 3.90f, -8.2f, 3.78f, -29.2f, 0.21f, 0.13f);
            if (Is(type, AircraftType.EmbraerE190))
                return new AircraftIdentityMarkingLayout(1.58f, 3.58f, -7.6f, 3.47f, -28.0f, 0.19f, 0.12f);
            if (Is(type, AircraftType.AirbusA220300))
                return new AircraftIdentityMarkingLayout(1.82f, 3.76f, -8.2f, 3.64f, -30.0f, 0.20f, 0.12f);
            if (Is(type, AircraftType.AirbusA321Neo))
                return new AircraftIdentityMarkingLayout(1.96f, 3.85f, -10.0f, 3.74f, -35.2f, 0.22f, 0.13f);
            if (Is(type, AircraftType.AirbusA350900))
                return new AircraftIdentityMarkingLayout(3.04f, 6.58f, -13.0f, 6.40f, -55.0f, 0.30f, 0.17f);
            if (Is(type, AircraftType.Boeing78710))
                return new AircraftIdentityMarkingLayout(2.94f, 6.45f, -13.5f, 6.28f, -56.0f, 0.30f, 0.17f);
            if (Is(type, AircraftType.AirbusA330900))
                return new AircraftIdentityMarkingLayout(2.88f, 6.35f, -12.5f, 6.18f, -51.8f, 0.29f, 0.17f);
            if (Is(type, AircraftType.Boeing7879))
                return new AircraftIdentityMarkingLayout(2.94f, 6.45f, -12.5f, 6.28f, -51.0f, 0.29f, 0.17f);
            if (Is(type, AircraftType.Dash8Q400))
                return new AircraftIdentityMarkingLayout(1.44f, 1.78f, 7.0f, 1.70f, -10.4f, 0.15f, 0.10f);
            if (Is(type, AircraftType.Saab340))
                return new AircraftIdentityMarkingLayout(1.22f, 1.48f, 3.1f, 1.42f, -5.7f, 0.13f, 0.085f);
            return new AircraftIdentityMarkingLayout(0.88f, 1.48f, 3.3f, 1.40f, -5.2f, 0.13f, 0.085f);
        }

        private static bool Is(AircraftType type, AircraftType candidate) =>
            type != null && type.Id == candidate.Id;
    }
}

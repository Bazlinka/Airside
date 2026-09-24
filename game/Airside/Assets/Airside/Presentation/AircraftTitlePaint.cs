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
            var fraction = AircraftCatalogue.TryFor(type, out var spec)
                ? (float)spec.LengthMetres * TitleLengthFraction
                : 8f;
            // The fitted layout also knows where a high wing root or the aft taper stops the
            // paint (ADR 0112); a title never runs past that.
            var fitted = AircraftIdentityMarkings.For(type).TitleMaxLengthMetres;
            return fitted > 0f ? System.Math.Min(fraction, fitted) : fraction;
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
    /// aircraft's two fuselage sides, in the art root's frame (nose +Z). Generated from each
    /// type's mesh by scripts/generate-aircraft-title-layout.py (ADR 0112): the title sits
    /// above the cabin windows, tangent to the skin, starting just aft of the flight deck
    /// (<see cref="OperatorZ"/> is its forward end) and stopping short of a high wing root;
    /// the registration is small on the aft fuselage (<see cref="RegistrationZ"/> is its
    /// aft end). X is the right side; the left side mirrors it.
    /// </summary>
    public readonly struct AircraftIdentityMarkingLayout
    {
        public AircraftIdentityMarkingLayout(
            float sideX,
            float operatorY,
            float operatorZ,
            float registrationX,
            float registrationY,
            float registrationZ,
            float operatorCharacterSize,
            float registrationCharacterSize,
            float operatorTiltDegrees,
            float registrationTiltDegrees,
            float titleMaxLengthMetres)
        {
            SideX = sideX;
            OperatorY = operatorY;
            OperatorZ = operatorZ;
            RegistrationX = registrationX;
            RegistrationY = registrationY;
            RegistrationZ = registrationZ;
            OperatorCharacterSize = operatorCharacterSize;
            RegistrationCharacterSize = registrationCharacterSize;
            OperatorTiltDegrees = operatorTiltDegrees;
            RegistrationTiltDegrees = registrationTiltDegrees;
            TitleMaxLengthMetres = titleMaxLengthMetres;
        }

        /// <summary>Title's distance out from the centreline: skin plus a coat of paint.</summary>
        public float SideX { get; }
        public float OperatorY { get; }
        /// <summary>Forward end of the title; it reads aft from here on both sides.</summary>
        public float OperatorZ { get; }
        public float RegistrationX { get; }
        public float RegistrationY { get; }
        /// <summary>Aft end of the registration.</summary>
        public float RegistrationZ { get; }
        public float OperatorCharacterSize { get; }
        public float RegistrationCharacterSize { get; }
        /// <summary>How far the title leans back to lie on the upper fuselage, from vertical.</summary>
        public float OperatorTiltDegrees { get; }
        public float RegistrationTiltDegrees { get; }
        /// <summary>Longest title this fuselage has room for; 0 means use the length fraction.</summary>
        public float TitleMaxLengthMetres { get; }
    }

    /// <summary>
    /// Where each type's titles sit. Free of UnityEngine — it is only floats — so the
    /// harness can check every authored size against <see cref="AircraftTitlePaint"/>;
    /// scripts/generate-aircraft-title-layout.py --check keeps it matched to the meshes.
    /// </summary>
    public static class AircraftIdentityMarkings
    {
        public static AircraftIdentityMarkingLayout For(AircraftType type)
        {
            // <generated title layout>
            if (Is(type, AircraftType.Atr42))
                return new AircraftIdentityMarkingLayout(1.16f, 1.94f, 7.22f, 1.23f, 1.84f, -4.68f, 0.092f, 0.049f, 36.29f, 31.28f, 4.55f);
            if (Is(type, AircraftType.Saab340))
                return new AircraftIdentityMarkingLayout(0.91f, 2.02f, 3.69f, 0.98f, 1.93f, -3.71f, 0.078f, 0.040f, 39.23f, 33.61f, 6.71f);
            if (Is(type, AircraftType.Dash8Q400))
                return new AircraftIdentityMarkingLayout(1.11f, 2.34f, 10.13f, 1.18f, 2.23f, -9.27f, 0.092f, 0.047f, 37.17f, 31.91f, 5.98f);
            if (Is(type, AircraftType.EmbraerE190))
                return new AircraftIdentityMarkingLayout(1.21f, 3.80f, -4.39f, 1.31f, 3.66f, -27.69f, 0.113f, 0.053f, 36.79f, 30.64f, 12.32f);
            if (Is(type, AircraftType.AirbusA220300))
                return new AircraftIdentityMarkingLayout(1.41f, 3.75f, -5.85f, 1.52f, 3.59f, -28.15f, 0.132f, 0.061f, 36.72f, 30.48f, 13.16f);
            if (Is(type, AircraftType.AirbusA320200))
                return new AircraftIdentityMarkingLayout(1.63f, 4.62f, -4.72f, 1.75f, 4.42f, -27.52f, 0.155f, 0.069f, 35.47f, 28.73f, 12.77f);
            if (Is(type, AircraftType.Boeing737800))
                return new AircraftIdentityMarkingLayout(1.53f, 4.78f, -4.12f, 1.65f, 4.60f, -28.42f, 0.143f, 0.066f, 36.31f, 29.85f, 13.42f);
            if (Is(type, AircraftType.Boeing7378))
                return new AircraftIdentityMarkingLayout(1.53f, 4.75f, -4.12f, 1.65f, 4.57f, -28.42f, 0.142f, 0.066f, 36.50f, 30.07f, 13.42f);
            if (Is(type, AircraftType.AirbusA321Neo))
                return new AircraftIdentityMarkingLayout(1.52f, 4.47f, -4.16f, 1.63f, 4.31f, -31.96f, 0.134f, 0.065f, 38.12f, 31.91f, 15.13f);
            if (Is(type, AircraftType.AirbusA350900))
                return new AircraftIdentityMarkingLayout(2.43f, 7.36f, -9.95f, 2.62f, 7.06f, -54.75f, 0.233f, 0.104f, 35.66f, 29.05f, 22.71f);
            if (Is(type, AircraftType.AirbusA330900))
                return new AircraftIdentityMarkingLayout(2.30f, 7.23f, -6.32f, 2.49f, 6.93f, -52.62f, 0.230f, 0.098f, 34.48f, 27.75f, 21.65f);
            if (Is(type, AircraftType.Boeing7879))
                return new AircraftIdentityMarkingLayout(2.36f, 7.34f, -9.46f, 2.54f, 7.04f, -51.26f, 0.233f, 0.101f, 34.75f, 28.05f, 21.36f);
            if (Is(type, AircraftType.Boeing78710))
                return new AircraftIdentityMarkingLayout(2.36f, 7.34f, -9.95f, 2.54f, 7.04f, -55.75f, 0.233f, 0.101f, 34.75f, 28.05f, 23.22f);
            // </generated title layout>
            // Unknown or primitive-fallback types: the ATR's regional fuselage.
            return For(AircraftType.Atr42);
        }

        private static bool Is(AircraftType type, AircraftType candidate) =>
            type != null && type.Id == candidate.Id;
    }
}

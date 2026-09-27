using System;
using Airside.Simulation;

namespace Airside.Presentation
{
    /// <summary>A plain colour for the pure atmosphere maths (no UnityEngine).</summary>
    public readonly struct Rgb
    {
        public Rgb(float r, float g, float b)
        {
            R = r;
            G = g;
            B = b;
        }

        public float R { get; }
        public float G { get; }
        public float B { get; }

        public float Luma => 0.2126f * R + 0.7152f * G + 0.0722f * B;

        /// <summary>Spread between the strongest and weakest channel: 0 for a pure grey.</summary>
        public float Saturation => Math.Max(R, Math.Max(G, B)) - Math.Min(R, Math.Min(G, B));

        public static Rgb Lerp(Rgb a, Rgb b, float t)
        {
            t = t < 0f ? 0f : t > 1f ? 1f : t;
            return new Rgb(a.R + (b.R - a.R) * t, a.G + (b.G - a.G) * t, a.B + (b.B - a.B) * t);
        }
    }

    /// <summary>
    /// ADR 0143 — one sky for the whole frame. The camera background, the horizon dome and the fog
    /// all come from the same colour, which now carries the weather: blue on a clear day, grey-blue
    /// under overcast and rain, dark slate in a storm, pale grey in fog, deep blue at night. Fog
    /// density comes straight from the weather's visibility, eased off for a high camera so the
    /// field stays readable from the overview while the ground still looks misty. The same look
    /// drives the overcast sheet, the horizon band and the low mist. Pure (no UnityEngine).
    /// </summary>
    public readonly struct AtmosphereLook
    {
        public static readonly Rgb ClearDay = new(0.55f, 0.68f, 0.82f);
        public static readonly Rgb OvercastDay = new(0.60f, 0.64f, 0.69f);
        public static readonly Rgb RainDay = new(0.47f, 0.52f, 0.57f);
        public static readonly Rgb StormDay = new(0.26f, 0.29f, 0.33f);
        public static readonly Rgb FogDay = new(0.80f, 0.81f, 0.80f);
        public static readonly Rgb Dusk = new(0.62f, 0.38f, 0.30f);
        public static readonly Rgb Night = new(0.04f, 0.055f, 0.10f);

        private AtmosphereLook(Rgb sky, Rgb fog, float fogDensity, float mist, float stratus, float horizonBand,
            float cloudShade)
        {
            Sky = sky;
            Fog = fog;
            FogDensity = fogDensity;
            Mist = mist;
            Stratus = stratus;
            HorizonBand = horizonBand;
            CloudShade = cloudShade;
        }

        /// <summary>Camera background and horizon dome.</summary>
        public Rgb Sky { get; }

        /// <summary>Fog colour: the sky, a touch paler toward the horizon haze.</summary>
        public Rgb Fog { get; }

        /// <summary>Exponential-squared density for the camera's height.</summary>
        public float FogDensity { get; }

        /// <summary>0..1 how strongly the low mist layer shows (fog, rain, dawn).</summary>
        public float Mist { get; }

        /// <summary>0..1 the overcast sheet's opacity, already faded out for a camera above it.</summary>
        public float Stratus { get; }

        /// <summary>0..1 the distant horizon cloud band.</summary>
        public float HorizonBand { get; }

        /// <summary>1 bright cloud … 0.45 storm-dark base: multiplies the cloud card tint.</summary>
        public float CloudShade { get; }

        /// <summary>The overcast sheet's height above the field.</summary>
        public const float StratusHeightMetres = 1_100f;

        /// <summary>From this camera height the fog eases off (fully by <see cref="HighCameraMetres"/>).</summary>
        public const float LowCameraMetres = 150f;
        public const float HighCameraMetres = 1_500f;

        /// <summary>A high camera sees through at most this share of the ground-level fog density.</summary>
        public const float HighCameraFogShare = 0.2f;

        /// <param name="daylight">0 night … 1 full day.</param>
        /// <param name="warm">0 … 1 dusk and dawn warmth.</param>
        /// <param name="dawn">True in the morning half of the day (mist forms at dawn, not dusk).</param>
        public static AtmosphereLook For(WeatherLook weather, float daylight, float warm, bool dawn,
            float cameraHeightMetres)
        {
            daylight = Clamp01(daylight);
            warm = Clamp01(warm);
            var cover = Clamp01(weather.CloudCover);
            var gloom = Clamp01(weather.Gloom);
            var visibilityLoss = 1f - Clamp01(weather.Visibility);

            // Daytime sky by weather: blue → grey-blue with cover → rain grey with precipitation →
            // storm slate with gloom → pale haze where visibility is lost to fog.
            var day = Rgb.Lerp(ClearDay, OvercastDay, Smooth(0.3f, 0.9f, cover));
            day = Rgb.Lerp(day, RainDay, Clamp01(weather.Precipitation * 1.4f));
            day = Rgb.Lerp(day, StormDay, Smooth(0.35f, 0.6f, gloom));
            var foggy = weather.Precipitation < 0.05f ? Smooth(0.45f, 0.75f, visibilityLoss) : 0f;
            day = Rgb.Lerp(day, FogDay, foggy);

            var night = Rgb.Lerp(Night, new Rgb(0.07f, 0.08f, 0.10f), cover * 0.6f);
            var sky = Rgb.Lerp(night, day, daylight);
            // Dusk colour shows through a clear sky and is smothered by cloud.
            sky = Rgb.Lerp(sky, Dusk, warm * 0.55f * (1f - cover * 0.75f));

            var haze = Rgb.Lerp(sky, new Rgb(0.86f, 0.87f, 0.87f), daylight * 0.2f * (1f - gloom));
            var fog = Rgb.Lerp(sky, haze, 0.5f);

            var ground = weather.FogDensity * Lerp(1.3f, 1f, daylight);
            var lift = Smooth(LowCameraMetres, HighCameraMetres, cameraHeightMetres);
            var density = ground * Lerp(1f, HighCameraFogShare, lift);

            var fogMist = Smooth(0.35f, 0.7f, visibilityLoss);
            var rainMist = Clamp01(weather.Precipitation) * 0.35f;
            var dawnMist = dawn ? warm * 0.3f * (1f - cover * 0.5f) : 0f;
            var mist = Clamp01(Math.Max(fogMist, Math.Max(rainMist, dawnMist)));

            var stratus = Smooth(0.55f, 0.95f, cover) * 0.8f
                          * (1f - Smooth(StratusHeightMetres - 400f, StratusHeightMetres - 50f, cameraHeightMetres));
            var band = Lerp(0.25f, 0.8f, cover) * (1f - foggy * 0.7f);
            var shade = Lerp(1f, 0.45f, Smooth(0.2f, 0.6f, gloom));
            return new AtmosphereLook(sky, fog, density, mist, stratus, band, shade);
        }

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
        private static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);

        private static float Smooth(float from, float to, float x)
        {
            var t = Clamp01((x - from) / (to - from));
            return t * t * (3f - 2f * t);
        }
    }
}

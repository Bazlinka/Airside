using System.Collections.Generic;
using System.IO;
using Airside.Simulation;
using UnityEngine;

namespace Airside.Presentation
{
    /// <summary>
    /// The approved Airside palette (docs/art/ART_DIRECTION_AND_ASSET_SPEC.md), applied
    /// to the runtime HUD: translucent Runway Ink panels, Cloud text, Coastal Blue for
    /// selection/accent, Safety Yellow for caution, Clear Green for on-time and Signal
    /// Red reserved for delay — matching the approved REF-004 operations HUD reference.
    /// Presentation only; colour never drives simulation state.
    /// </summary>
    public static class AirsideTheme
    {
        public static readonly Color RunwayInk = FromHex("#17242A");
        public static readonly Color Tarmac = FromHex("#343B40");
        public static readonly Color Concrete = FromHex("#9CA3A2");
        public static readonly Color Eucalyptus = FromHex("#4F6F60");
        public static readonly Color DryGrass = FromHex("#8A8A58");
        public static readonly Color Sand = FromHex("#C8B286");
        public static readonly Color CoastalBlue = FromHex("#39708A");
        public static readonly Color SafetyYellow = FromHex("#F2C14B");
        public static readonly Color SignalRed = FromHex("#C95D50");
        public static readonly Color ClearGreen = FromHex("#5F8B68");
        public static readonly Color Cloud = FromHex("#EEF1EC");
        public static readonly Color OpenSky = FromHex("#A7C9D9");

        private static Texture2D _panelBackground;

        /// <summary>Translucent Runway Ink, 1x1 stretched to fill any panel rect.</summary>
        public static Texture2D PanelBackground
        {
            get
            {
                if (_panelBackground == null)
                {
                    var ink = RunwayInk;
                    ink.a = 0.88f;
                    _panelBackground = new Texture2D(1, 1, TextureFormat.RGBA32, mipChain: false);
                    _panelBackground.SetPixel(0, 0, ink);
                    _panelBackground.Apply();
                }

                return _panelBackground;
            }
        }

        /// <summary>A box/panel style on the given basis, themed with the Runway Ink panel and Cloud text.</summary>
        public static GUIStyle PanelStyle(GUIStyle basis)
        {
            var style = new GUIStyle(basis);
            style.normal.background = PanelBackground;
            style.normal.textColor = Cloud;
            return style;
        }

        /// <summary>A label/button style on the given basis with themed text colour (Cloud by default).</summary>
        public static GUIStyle TextStyle(GUIStyle basis, Color? textColor = null)
        {
            var style = new GUIStyle(basis);
            style.normal.textColor = textColor ?? Cloud;
            return style;
        }

        private static Color FromHex(string hex) =>
            ColorUtility.TryParseHtmlString(hex, out var color) ? color : Color.magenta;

        // --- Batch E UI candidates (docs/art/prompts/batch-e-ui-generation-2026-09-06.md) ---
        // Generated candidates, not yet Bailey-approved; wired here as the documented
        // fallback-safe pattern requires (a missing or not-yet-drawn file degrades to
        // the existing procedural/text-only presentation, never to a broken HUD).

        private static readonly Dictionary<string, Texture2D> IconCache = new();
        private static Texture2D _alertStripe;

        /// <summary>
        /// A Batch E icon by category/name (e.g. "weather", "clear"), or null if the
        /// candidate file isn't present — callers must keep working without it.
        /// </summary>
        public static Texture2D Icon(string category, string name)
        {
            var key = $"{category}/{name}";
            if (IconCache.TryGetValue(key, out var cached))
                return cached;

            var texture = LoadArtTexture($"UI/Icons/ui_{category}_{name}_v01.png");
            IconCache[key] = texture;
            return texture;
        }

        /// <summary>The weather icon for this condition, or null (no "cloudy" candidate; falls back to "overcast").</summary>
        public static Texture2D WeatherIcon(WeatherKind kind) => kind switch
        {
            WeatherKind.Clear => Icon("weather", "clear"),
            WeatherKind.Cloudy => Icon("weather", "overcast"),
            WeatherKind.Overcast => Icon("weather", "overcast"),
            WeatherKind.Rain => Icon("weather", "rain"),
            WeatherKind.Fog => Icon("weather", "fog"),
            WeatherKind.Storm => Icon("weather", "storm"),
            _ => null
        };

        /// <summary>
        /// UI-PNL-003 caution stripe, or null. NOT the same candidate as the dark panel
        /// (UI-PNL-002): that one's measured alpha averages ~9%, too faint to serve as a
        /// readable panel background, so it stays out of runtime use until regenerated.
        /// </summary>
        public static Texture2D AlertStripeBackground
        {
            get
            {
                if (_alertStripe == null)
                    _alertStripe = LoadArtTexture("UI/Panels/ui_alert_stripe_v01.png");
                return _alertStripe;
            }
        }

        /// <summary>A caution-style label with the alert stripe behind Safety Yellow text, or a flat fallback.</summary>
        public static GUIStyle CautionStyle(GUIStyle basis)
        {
            var style = TextStyle(basis, SafetyYellow);
            if (AlertStripeBackground != null)
                style.normal.background = AlertStripeBackground;
            return style;
        }

        private static Texture2D LoadArtTexture(string artRelativePath)
        {
            var fullPath = Path.Combine(Application.dataPath, "Airside", "Art", artRelativePath);
            if (!File.Exists(fullPath))
                return null;

            var bytes = File.ReadAllBytes(fullPath);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: true);
            if (!texture.LoadImage(bytes))
                return null;

            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            return texture;
        }
    }
}

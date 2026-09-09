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
        private static Texture2D _panelBackgroundLight;
        private static Texture2D _solidWhite;
        private static Texture2D _wordmarkLight;
        private static Texture2D _splashDawn;
        private static bool _panelBackgroundResolved;
        private static bool _panelBackgroundLightResolved;
        private static bool _wordmarkResolved;
        private static bool _splashResolved;

        /// <summary>BRD-001 light wordmark (transparent). Null when the art file is missing.</summary>
        public static Texture2D WordmarkLight
        {
            get
            {
                if (!_wordmarkResolved)
                {
                    _wordmarkResolved = true;
                    _wordmarkLight = LoadArtTexture("Brand/airside_wordmark_light_v01.png");
                }

                return _wordmarkLight;
            }
        }

        /// <summary>UI-ILL-001 dawn splash illustration. Null when the art file is missing.</summary>
        public static Texture2D SplashDawn
        {
            get
            {
                if (!_splashResolved)
                {
                    _splashResolved = true;
                    _splashDawn = LoadArtTexture("UI/Illustrations/ui_splash_airport_dawn_v01.png");
                }

                return _splashDawn;
            }
        }

        /// <summary>
        /// Translucent Runway Ink panel. Prefers UI-PNL-002 dark nine-slice when the
        /// regenerated asset has usable opacity; otherwise a solid 88%-alpha fill.
        /// </summary>
        public static Texture2D PanelBackground
        {
            get
            {
                if (!_panelBackgroundResolved)
                {
                    _panelBackgroundResolved = true;
                    var art = LoadArtTexture("UI/Panels/ui_panel_9slice_dark_v01.png");
                    if (art != null && MeanAlpha(art) >= 0.5f)
                        _panelBackground = art;
                    else
                    {
                        var ink = RunwayInk;
                        ink.a = 0.94f;
                        _panelBackground = new Texture2D(1, 1, TextureFormat.RGBA32, mipChain: false);
                        _panelBackground.SetPixel(0, 0, ink);
                        _panelBackground.Apply();
                    }
                }

                return _panelBackground;
            }
        }

        /// <summary>
        /// UI-PNL-001 light nine-slice for Toolkit chrome (status / ops / offer / economy / speed).
        /// Falls back to a soft Cloud fill when the art file is missing.
        /// </summary>
        public static Texture2D PanelBackgroundLight
        {
            get
            {
                if (!_panelBackgroundLightResolved)
                {
                    _panelBackgroundLightResolved = true;
                    var art = LoadArtTexture("UI/Panels/ui_panel_9slice_light_v01.png");
                    if (art != null && MeanAlpha(art) >= 0.35f)
                        _panelBackgroundLight = art;
                    else
                    {
                        var fill = Cloud;
                        fill.a = 0.92f;
                        _panelBackgroundLight = new Texture2D(1, 1, TextureFormat.RGBA32, mipChain: false);
                        _panelBackgroundLight.SetPixel(0, 0, fill);
                        _panelBackgroundLight.Apply();
                    }
                }

                return _panelBackgroundLight;
            }
        }

        /// <summary>Draw a thin frame around a panel for separation from the 3D world.</summary>
        public static void DrawPanelFrame(Rect rect, Color? edge = null)
        {
            var previous = GUI.color;
            GUI.color = edge ?? new Color(CoastalBlue.r, CoastalBlue.g, CoastalBlue.b, 0.55f);
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 2f), SolidWhite);
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - 2f, rect.width, 2f), SolidWhite);
            GUI.DrawTexture(new Rect(rect.x, rect.y, 2f, rect.height), SolidWhite);
            GUI.DrawTexture(new Rect(rect.xMax - 2f, rect.y, 2f, rect.height), SolidWhite);
            GUI.color = previous;
        }

        /// <summary>Opaque white 1x1 for tinted progress fills and tracks.</summary>
        public static Texture2D SolidWhite
        {
            get
            {
                if (_solidWhite == null)
                {
                    _solidWhite = new Texture2D(1, 1, TextureFormat.RGBA32, mipChain: false);
                    _solidWhite.SetPixel(0, 0, Color.white);
                    _solidWhite.Apply();
                }

                return _solidWhite;
            }
        }

        /// <summary>
        /// Draws a horizontal progress bar. Presentation only — <paramref name="progress01"/>
        /// is supplied by the caller from simulation state.
        /// </summary>
        public static void DrawProgressBar(Rect rect, float progress01, Color fill, Color track)
        {
            var previous = GUI.color;
            GUI.color = track;
            GUI.DrawTexture(rect, SolidWhite);
            var fillWidth = rect.width * Mathf.Clamp01(progress01);
            if (fillWidth > 0.5f)
            {
                GUI.color = fill;
                GUI.DrawTexture(new Rect(rect.x, rect.y, fillWidth, rect.height), SolidWhite);
            }

            GUI.color = previous;
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

        // --- Batch E UI (docs/art/prompts/batch-e-ui-generation-2026-09-06.md) ---
        // Bailey Approved 2026-09-06. Missing files degrade to procedural/text-only HUD.

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

        /// <summary>Operation-phase icon for the HUD, or null.</summary>
        public static Texture2D OperationIcon(AircraftPhase phase) => phase switch
        {
            AircraftPhase.Approach or AircraftPhase.Landing => Icon("operation", "arrival"),
            AircraftPhase.TaxiIn or AircraftPhase.TaxiOut => Icon("operation", "taxi"),
            AircraftPhase.AtStand => Icon("operation", "turnaround"),
            AircraftPhase.Pushback => Icon("operation", "hold"),
            AircraftPhase.Takeoff => Icon("operation", "departure"),
            AircraftPhase.Departed => Icon("operation", "completed"),
            _ => Icon("operation", "stand")
        };

        /// <summary>Batch F4 UI-ICO-005 system-control icon, or null.</summary>
        public static Texture2D SystemIcon(string name) => Icon("system", name);

        /// <summary>Maps a turnaround task name to a service icon when available.</summary>
        public static Texture2D ServiceIconForTask(string taskName)
        {
            if (string.IsNullOrEmpty(taskName))
                return null;
            var key = taskName.ToLowerInvariant();
            if (key.Contains("fuel"))
                return Icon("service", "fuel");
            if (key.Contains("bag"))
                return Icon("service", "baggage");
            if (key.Contains("pass") || key.Contains("board"))
                return Icon("service", "passengers");
            if (key.Contains("clean"))
                return Icon("service", "cleaning");
            if (key.Contains("cater"))
                return Icon("service", "catering");
            if (key.Contains("inspect") || key.Contains("tech"))
                return Icon("service", "inspection");
            if (key.Contains("priority"))
                return Icon("service", "priority");
            return null;
        }

        /// <summary>
        /// UI-PNL-003 caution stripe, or null.
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

        private static float MeanAlpha(Texture2D texture)
        {
            try
            {
                var pixels = texture.GetPixels32();
                if (pixels.Length == 0)
                    return 0f;
                var sum = 0;
                for (var i = 0; i < pixels.Length; i++)
                    sum += pixels[i].a;
                return sum / (255f * pixels.Length);
            }
            catch
            {
                return 0f;
            }
        }

        private static Texture2D LoadArtTexture(string artRelativePath)
        {
            return AirsideArtTextures.Load(artRelativePath, linear: false, wrap: TextureWrapMode.Clamp);
        }
    }
}

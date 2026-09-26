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
        // Parsed from AirsidePalette, which is UnityEngine-free, so the runtime HUD, the
        // headless workspace layer and the offline mockup renderer cannot drift apart.
        public static readonly Color RunwayInk = FromHex(AirsidePalette.RunwayInkHex);
        public static readonly Color Tarmac = FromHex(AirsidePalette.TarmacHex);
        public static readonly Color Concrete = FromHex(AirsidePalette.ConcreteHex);
        public static readonly Color Eucalyptus = FromHex(AirsidePalette.EucalyptusHex);
        public static readonly Color DryGrass = FromHex(AirsidePalette.DryGrassHex);
        public static readonly Color Sand = FromHex(AirsidePalette.SandHex);
        public static readonly Color CoastalBlue = FromHex(AirsidePalette.CoastalBlueHex);
        public static readonly Color SafetyYellow = FromHex(AirsidePalette.SafetyYellowHex);
        public static readonly Color SignalRed = FromHex(AirsidePalette.SignalRedHex);
        public static readonly Color ClearGreen = FromHex(AirsidePalette.ClearGreenHex);
        public static readonly Color Cloud = FromHex(AirsidePalette.CloudHex);
        public static readonly Color OpenSky = FromHex(AirsidePalette.OpenSkyHex);

        // Glass Cockpit HUD (ADR 0122).
        public static readonly Color Glass = FromHex(AirsidePalette.GlassHex);
        public static readonly Color GlassRaised = FromHex(AirsidePalette.GlassRaisedHex);
        public static readonly Color InstrumentText = FromHex(AirsidePalette.InstrumentTextHex);
        public static readonly Color InstrumentMuted = FromHex(AirsidePalette.InstrumentMutedHex);
        public static readonly Color Aqua = FromHex(AirsidePalette.AquaHex);
        public static readonly Color Amber = FromHex(AirsidePalette.AmberHex);
        public static readonly Color GoGreen = FromHex(AirsidePalette.GoGreenHex);
        public static readonly Color WarnRed = FromHex(AirsidePalette.WarnRedHex);
        public static readonly Color RouteMagenta = FromHex(AirsidePalette.RouteMagentaHex);
        public static readonly Color OnAccent = FromHex(AirsidePalette.OnAccentHex);

        public const float PanelRadius = 14f;
        public const float CardRadius = 10f;
        public const float ControlRadius = 8f;

        public static Color WithAlpha(Color colour, float alpha) => new(colour.r, colour.g, colour.b, Mathf.Clamp01(alpha));

        /// <summary>
        /// A rounded rectangle — filled, or an outline when <paramref name="borderWidth"/> is set —
        /// drawn by IMGUI's own rounded-texture path, so corners stay crisp at every HUD scale.
        /// </summary>
        public static void DrawRounded(Rect rect, Color colour, float radius, float borderWidth = 0f)
        {
            if (rect.width <= 0.5f || rect.height <= 0.5f || colour.a <= 0.001f)
                return;
            radius = Mathf.Min(radius, rect.width * 0.5f, rect.height * 0.5f);
            GUI.DrawTexture(rect, SolidWhite, ScaleMode.StretchToFill, true, 0f, colour, borderWidth, radius);
        }

        /// <summary>
        /// A floating graphite-glass panel: a soft three-step drop shadow, the translucent glass
        /// body and a faint 1 pt inner edge. Every HUD surface is one of these.
        /// </summary>
        public static void DrawGlass(Rect rect, float alpha = 0.9f, float radius = PanelRadius)
        {
            if (rect.width <= 1f || rect.height <= 1f)
                return;
            for (var i = 3; i >= 1; i--)
            {
                var spread = i * 3.5f;
                DrawRounded(new Rect(rect.x - spread * 0.4f, rect.y + spread * 0.25f, rect.width + spread * 0.8f,
                    rect.height + spread), new Color(0f, 0f, 0f, 0.07f * alpha), radius + spread);
            }
            DrawRounded(rect, WithAlpha(Glass, alpha), radius);
            DrawRounded(rect, new Color(1f, 1f, 1f, 0.09f * alpha), radius, 1f);
        }

        /// <summary>A raised glass sub-card inside a panel.</summary>
        public static void DrawCard(Rect rect, float alpha = 1f)
        {
            DrawRounded(rect, WithAlpha(GlassRaised, 0.9f * alpha), CardRadius);
            DrawRounded(rect, new Color(1f, 1f, 1f, 0.06f * alpha), CardRadius, 1f);
        }

        private static readonly Dictionary<string, Texture2D> RoundedTextures = new();

        /// <summary>
        /// A 9-slice rounded texture for GUIStyle backgrounds (raw-IMGUI panels and buttons), baked
        /// once per colour: a signed-distance rounded rectangle with an anti-aliased edge and an
        /// optional 1 px rim.
        /// </summary>
        public static Texture2D RoundedTexture(Color fill, Color rim, int radius)
        {
            var key = $"{ColorUtility.ToHtmlStringRGBA(fill)}:{ColorUtility.ToHtmlStringRGBA(rim)}:{radius}";
            if (RoundedTextures.TryGetValue(key, out var cached) && cached != null)
                return cached;
            var size = radius * 2 + 4;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            var pixels = new Color[size * size];
            var half = size * 0.5f;
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                // Distance outside the rounded rectangle (negative inside).
                var qx = Mathf.Abs(x + 0.5f - half) - (half - radius);
                var qy = Mathf.Abs(y + 0.5f - half) - (half - radius);
                var outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude
                              + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
                var coverage = Mathf.Clamp01(0.5f - outside);
                var rimMix = Mathf.Clamp01(1.5f - Mathf.Abs(outside + 0.75f)) * rim.a;
                var colour = Color.Lerp(fill, new Color(rim.r, rim.g, rim.b, Mathf.Max(fill.a, rim.a)), rimMix);
                colour.a *= coverage;
                pixels[y * size + x] = colour;
            }
            texture.SetPixels(pixels);
            texture.Apply(false, false);
            RoundedTextures[key] = texture;
            return texture;
        }

        private static Texture2D _fadeRight;

        /// <summary>White, opaque on the left and clear on the right with an ease-out curve — tinted by GUI.color.</summary>
        public static Texture2D FadeRight
        {
            get
            {
                if (_fadeRight != null)
                    return _fadeRight;
                const int width = 256;
                _fadeRight = new Texture2D(width, 1, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave
                };
                for (var x = 0; x < width; x++)
                {
                    var t = x / (width - 1f);
                    _fadeRight.SetPixel(x, 0, new Color(1f, 1f, 1f, 1f - t * t));
                }
                _fadeRight.Apply(false, true);
                return _fadeRight;
            }
        }

        private static readonly Dictionary<string, Texture2D> ArtTextures = new();

        /// <summary>An approved art texture by Art-relative path (splash, wordmark), cached; null when missing.</summary>
        public static Texture2D ArtTexture(string artRelativePath)
        {
            if (string.IsNullOrEmpty(artRelativePath))
                return null;
            if (artRelativePath == "UI/Illustrations/ui_splash_airport_dawn_v01.png")
                return SplashDawn;
            if (artRelativePath == "Brand/airside_wordmark_light_v01.png")
                return WordmarkLight;
            if (ArtTextures.TryGetValue(artRelativePath, out var cached))
                return cached;
            var texture = LoadArtTexture(artRelativePath);
            ArtTextures[artRelativePath] = texture;
            return texture;
        }

        private static readonly Dictionary<string, Texture2D> IconMasks = new();

        /// <summary>
        /// The approved line icon as a white alpha mask, so GUI.color can tint it to any HUD tone.
        /// The source art is black line work on transparency. Null when the file is missing.
        /// </summary>
        public static Texture2D IconMask(string category, string name)
        {
            var key = category + "/" + name;
            if (IconMasks.TryGetValue(key, out var cached))
                return cached;
            var source = Icon(category, name);
            Texture2D mask = null;
            if (source != null)
            {
                try
                {
                    var pixels = source.GetPixels32();
                    for (var i = 0; i < pixels.Length; i++)
                        pixels[i] = new Color32(255, 255, 255, pixels[i].a);
                    mask = new Texture2D(source.width, source.height, TextureFormat.RGBA32, true)
                    {
                        filterMode = FilterMode.Trilinear,
                        wrapMode = TextureWrapMode.Clamp,
                        hideFlags = HideFlags.HideAndDontSave
                    };
                    mask.SetPixels32(pixels);
                    mask.Apply(true, false);
                }
                catch (UnityException)
                {
                    mask = null;
                }
            }
            IconMasks[key] = mask;
            return mask;
        }

        private static Texture2D _panelBackground;
        private static Texture2D _panelBackgroundLight;
        private static Texture2D _solidWhite;
        private static Texture2D _buttonNormal;
        private static Texture2D _buttonHover;
        private static Texture2D _buttonActive;
        private static Texture2D _wordmarkLight;
        private static Texture2D _appMarkLight;
        private static Texture2D _splashDawn;
        private static bool _panelBackgroundResolved;
        private static bool _panelBackgroundLightResolved;
        private static bool _wordmarkResolved;
        private static bool _appMarkResolved;
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

        /// <summary>BRD-003 transparent approach-runway mark for the launch sequence.</summary>
        public static Texture2D AppMarkLight
        {
            get
            {
                if (!_appMarkResolved)
                {
                    _appMarkResolved = true;
                    _appMarkLight = LoadArtTexture("Brand/airside_brand_mark_v02.png");
                }

                return _appMarkLight;
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

        /// <summary>A rounded 1.5 pt outline — selection, the current priority, a guided control.</summary>
        public static void DrawPanelFrame(Rect rect, Color? edge = null) =>
            DrawRounded(rect, edge ?? new Color(1f, 1f, 1f, 0.12f), CardRadius, 1.5f);

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
            var radius = rect.height * 0.5f;
            DrawRounded(rect, track, radius);
            var fillWidth = rect.width * Mathf.Clamp01(progress01);
            if (fillWidth > 0.5f)
                DrawRounded(new Rect(rect.x, rect.y, Mathf.Max(rect.height, fillWidth), rect.height), fill, radius);
        }

        private static readonly RectOffset PanelBorder = new(16, 16, 16, 16);
        private static readonly RectOffset ControlBorder = new(10, 10, 10, 10);

        /// <summary>A rounded graphite-glass box style — every raw-IMGUI panel (menus, help, dev tools).</summary>
        public static GUIStyle PanelStyle(GUIStyle basis)
        {
            var style = new GUIStyle(basis);
            style.normal.background = RoundedTexture(WithAlpha(Glass, 0.93f), new Color(1f, 1f, 1f, 0.10f), 14);
            style.normal.textColor = InstrumentText;
            style.border = PanelBorder;
            return style;
        }

        /// <summary>A label/button style on the given basis with themed text colour (instrument text by default).</summary>
        public static GUIStyle TextStyle(GUIStyle basis, Color? textColor = null)
        {
            var style = new GUIStyle(basis);
            style.normal.textColor = textColor ?? InstrumentText;
            return style;
        }

        private static void SetButtonStates(GUIStyle style, Texture2D normal, Texture2D hover, Texture2D active, Color text,
            Color? hoverText = null)
        {
            style.normal.background = normal;
            style.normal.textColor = text;
            style.hover.background = hover;
            style.hover.textColor = hoverText ?? text;
            style.active.background = active;
            style.active.textColor = hoverText ?? text;
            style.focused.background = normal;
            style.focused.textColor = text;
            style.onNormal.background = active;
            style.onNormal.textColor = hoverText ?? text;
            style.border = ControlBorder;
            style.alignment = TextAnchor.MiddleCenter;
        }

        /// <summary>A recessed glass text field with an aqua rim while focused.</summary>
        public static GUIStyle TextFieldStyle(GUIStyle basis)
        {
            var style = new GUIStyle(basis)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(10, 10, 3, 3),
                border = ControlBorder
            };
            var rest = RoundedTexture(WithAlpha(Glass, 0.95f), new Color(1f, 1f, 1f, 0.14f), 9);
            var focus = RoundedTexture(WithAlpha(Glass, 0.98f), Aqua, 9);
            style.normal.background = rest;
            style.hover.background = rest;
            style.focused.background = focus;
            style.active.background = focus;
            style.normal.textColor = style.hover.textColor = style.focused.textColor = style.active.textColor = InstrumentText;
            return style;
        }

        /// <summary>A raised glass pill button; hover lifts it with an aqua rim.</summary>
        public static GUIStyle ButtonStyle(GUIStyle basis, Color? textColor = null)
        {
            var style = new GUIStyle(basis);
            SetButtonStates(style, ButtonNormal, ButtonHover, ButtonActive, textColor ?? InstrumentText);
            return style;
        }

        /// <summary>Filled avionics-amber primary action — one dominant button per card.</summary>
        public static GUIStyle PrimaryButtonStyle(GUIStyle basis)
        {
            var style = new GUIStyle(basis);
            SetButtonStates(style,
                RoundedTexture(Amber, new Color(1f, 1f, 1f, 0.18f), 9),
                RoundedTexture(Color.Lerp(Amber, Color.white, 0.18f), new Color(1f, 1f, 1f, 0.3f), 9),
                RoundedTexture(Color.Lerp(Amber, Color.black, 0.12f), new Color(1f, 1f, 1f, 0.1f), 9),
                OnAccent);
            style.fontStyle = FontStyle.Bold;
            return style;
        }

        /// <summary>Red-rimmed destructive pill, visually below the primary.</summary>
        public static GUIStyle DestructiveButtonStyle(GUIStyle basis)
        {
            var style = new GUIStyle(basis);
            SetButtonStates(style,
                RoundedTexture(WithAlpha(WarnRed, 0.10f), WithAlpha(WarnRed, 0.85f), 9),
                RoundedTexture(WithAlpha(WarnRed, 0.22f), WarnRed, 9),
                RoundedTexture(WithAlpha(WarnRed, 0.32f), WarnRed, 9),
                WarnRed);
            return style;
        }

        /// <summary>A graphite-glass panel behind raw-IMGUI content, so runway markings never wash out text.</summary>
        public static void DrawOpaquePanel(Rect rect, float alpha = 0.94f) => DrawGlass(rect, alpha);

        private static Texture2D Solid(Color colour)
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, mipChain: false);
            texture.SetPixel(0, 0, colour);
            texture.Apply();
            return texture;
        }

        /// <summary>Resting button — raised glass with a faint rim, so it reads as clickable.</summary>
        public static Texture2D ButtonNormal => _buttonNormal ??= RoundedTexture(GlassRaised, new Color(1f, 1f, 1f, 0.16f), 9);

        /// <summary>Hover fill — Coastal Blue, the approved accent for interaction.</summary>
        public static Texture2D ButtonHover => _buttonHover ??=
            RoundedTexture(Color.Lerp(GlassRaised, Aqua, 0.18f), WithAlpha(Aqua, 0.8f), 9);

        /// <summary>Pressed — full aqua.</summary>
        public static Texture2D ButtonActive => _buttonActive ??= RoundedTexture(WithAlpha(Aqua, 0.85f), Aqua, 9);

        internal static Color FromHex(string hex) =>
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
            AircraftPhase.Circuit => Icon("operation", "hold"),
            AircraftPhase.GoAround => Icon("operation", "departure"),
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
            var style = TextStyle(basis, Amber);
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
            // MeanAlpha reads these panels' pixels to decide whether the art is usable.
            return AirsideArtTextures.Load(artRelativePath, linear: false, wrap: TextureWrapMode.Clamp,
                keepReadable: true);
        }
    }
}

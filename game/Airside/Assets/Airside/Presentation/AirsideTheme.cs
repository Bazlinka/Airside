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
    }
}

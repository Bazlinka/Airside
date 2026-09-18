using System.Collections.Generic;
using UnityEngine;

namespace Airside.Presentation
{
    /// <summary>
    /// Rasterises a <see cref="HudDrawList"/> through IMGUI and reports which action the
    /// player clicked. This is the only Unity code between a workspace and the screen: the
    /// layout, the copy and the ordering are all decided by the UnityEngine-free painters,
    /// which is what lets scripts/render-hud-mockups.py show the same surface offline.
    /// </summary>
    public sealed class HudPainter
    {
        private readonly Dictionary<int, GUIStyle> _textStyles = new();
        private readonly Dictionary<int, GUIStyle> _buttonStyles = new();

        /// <summary>
        /// Draws every command in order. Returns the action id of the control clicked this
        /// event, or null. Only one action can fire per event, as IMGUI intends.
        /// </summary>
        public string Draw(HudDrawList list)
        {
            if (list == null)
                return null;

            string clicked = null;
            for (var i = 0; i < list.Count; i++)
            {
                var command = list[i];
                var rect = ToRect(command.Box);
                switch (command.Kind)
                {
                    case HudDrawKind.Surface:
                        AirsideTheme.DrawOpaquePanel(rect, command.Value);
                        AirsideTheme.DrawPanelFrame(rect,
                            new Color(AirsideTheme.CoastalBlue.r, AirsideTheme.CoastalBlue.g,
                                AirsideTheme.CoastalBlue.b, 0.5f));
                        break;

                    case HudDrawKind.Fill:
                    case HudDrawKind.Hairline:
                        DrawSolid(rect, WithAlpha(Colour(command), command.Value));
                        break;

                    case HudDrawKind.Outline:
                        AirsideTheme.DrawPanelFrame(rect, WithAlpha(Colour(command), command.Value));
                        break;

                    case HudDrawKind.Text:
                        DrawText(command, rect);
                        break;

                    case HudDrawKind.Bar:
                        AirsideTheme.DrawProgressBar(rect, command.Value, Colour(command), AirsideTheme.Tarmac);
                        break;

                    case HudDrawKind.Button:
                        if (DrawButton(command, rect))
                            clicked = command.ActionId;
                        break;

                    case HudDrawKind.Hotspot:
                        if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
                            clicked = command.ActionId;
                        break;

                    case HudDrawKind.Dot:
                        DrawDot(rect, Colour(command));
                        break;

                    case HudDrawKind.Line:
                        DrawLine(new Vector2(command.Box.X, command.Box.Y),
                            new Vector2(command.Box.X + command.Box.Width, command.Box.Y + command.Box.Height),
                            Colour(command), command.Value);
                        break;
                }
            }

            return clicked;
        }

        public static Rect ToRect(HudBox box) => new(box.X, box.Y, box.Width, box.Height);

        public static Color Colour(HudTone tone) => tone switch
        {
            HudTone.Muted => AirsideTheme.Concrete,
            HudTone.Accent => AirsideTheme.FromHex(AirsidePalette.CoastalBlueStrongHex),
            HudTone.Caution => AirsideTheme.SafetyYellow,
            HudTone.Positive => AirsideTheme.ClearGreen,
            HudTone.Negative => AirsideTheme.SignalRed,
            _ => AirsideTheme.Cloud
        };

        private static Color Colour(HudDrawCommand command) =>
            command.ColourHex != null ? AirsideTheme.FromHex(command.ColourHex) : Colour(command.Tone);

        private static Color WithAlpha(Color colour, float alpha) =>
            new(colour.r, colour.g, colour.b, Mathf.Clamp01(alpha <= 0f ? 1f : alpha));

        private void DrawText(HudDrawCommand command, Rect rect)
        {
            var style = TextStyle(command);
            var previous = GUI.color;
            var colour = Colour(command);
            GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01(command.Value <= 0f ? 1f : command.Value));
            style.normal.textColor = colour;
            GUI.Label(rect, command.Text, style);
            GUI.color = previous;
        }

        private bool DrawButton(HudDrawCommand command, Rect rect)
        {
            var enabled = GUI.enabled;
            GUI.enabled = enabled && command.Enabled;
            var style = ButtonStyle(command.ButtonStyle);
            if (command.ButtonStyle == HudButtonStyle.Destructive)
                AirsideTheme.DrawPanelFrame(rect, AirsideTheme.SignalRed);
            var pressed = GUI.Button(rect, command.Text, style);
            GUI.enabled = enabled;
            return pressed;
        }

        private GUIStyle TextStyle(HudDrawCommand command)
        {
            // One style per (size, style flags, alignment) triple, built once and reused:
            // OnGUI runs several times a frame and a new GUIStyle per label is pure garbage.
            var size = Mathf.Max(8, Mathf.RoundToInt(command.FontSize));
            var key = size * 100 + (int)command.Style * 4 + (int)command.Align;
            if (_textStyles.TryGetValue(key, out var cached))
                return cached;

            var bold = (command.Style & HudTextStyle.Bold) != 0;
            var caption = (command.Style & HudTextStyle.Caption) != 0;
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = size,
                fontStyle = bold || caption ? FontStyle.Bold : FontStyle.Normal,
                wordWrap = (command.Style & HudTextStyle.Wrap) != 0,
                clipping = TextClipping.Clip,
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0),
                alignment = command.Align switch
                {
                    HudAlign.Center => TextAnchor.UpperCenter,
                    HudAlign.Right => TextAnchor.UpperRight,
                    _ => TextAnchor.UpperLeft
                }
            };
            _textStyles[key] = style;
            return style;
        }

        private GUIStyle ButtonStyle(HudButtonStyle kind)
        {
            if (_buttonStyles.TryGetValue((int)kind, out var cached))
                return cached;

            var basis = new GUIStyle(GUI.skin.button)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = false,
                clipping = TextClipping.Clip,
                padding = new RectOffset(6, 6, 2, 2)
            };

            var style = kind switch
            {
                HudButtonStyle.Primary => AirsideTheme.PrimaryButtonStyle(basis),
                HudButtonStyle.Destructive => AirsideTheme.DestructiveButtonStyle(basis),
                _ => AirsideTheme.ButtonStyle(basis, AirsideTheme.Cloud)
            };
            _buttonStyles[(int)kind] = style;
            return style;
        }

        private static void DrawSolid(Rect rect, Color colour)
        {
            var previous = GUI.color;
            GUI.color = colour;
            GUI.DrawTexture(rect, AirsideTheme.SolidWhite);
            GUI.color = previous;
        }

        private static Texture2D _dot;

        private static void DrawDot(Rect rect, Color colour)
        {
            if (_dot == null)
            {
                const int n = 32;
                var texture = new Texture2D(n, n, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave
                };
                var pixels = new Color32[n * n];
                var centre = (n - 1) * 0.5f;
                for (var y = 0; y < n; y++)
                for (var x = 0; x < n; x++)
                {
                    var dx = (x - centre) / centre;
                    var dy = (y - centre) / centre;
                    var r = Mathf.Sqrt(dx * dx + dy * dy);
                    pixels[y * n + x] = new Color32(255, 255, 255, (byte)(255f * Mathf.Clamp01((0.94f - r) / 0.16f)));
                }

                texture.SetPixels32(pixels);
                texture.Apply(false, true);
                _dot = texture;
            }

            var previous = GUI.color;
            GUI.color = colour;
            GUI.DrawTexture(rect, _dot);
            GUI.color = previous;
        }

        private static void DrawLine(Vector2 from, Vector2 to, Color colour, float thickness)
        {
            var delta = to - from;
            var length = delta.magnitude;
            if (length < 0.5f)
                return;
            if (thickness <= 0f)
                thickness = 1f;

            var matrix = GUI.matrix;
            GUI.matrix = matrix
                         * Matrix4x4.Translate(from)
                         * Matrix4x4.Rotate(Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg))
                         * Matrix4x4.Translate(-from);
            DrawSolid(new Rect(from.x, from.y - thickness * 0.5f, length, thickness), colour);
            GUI.matrix = matrix;
        }
    }
}

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
        /// <summary>ADR 0135: shifts everything drawn (and its click areas) while a sheet slides in.</summary>
        public Vector2 Offset { get; set; }

        public string Draw(HudDrawList list)
        {
            if (list == null)
                return null;
            if (Offset != Vector2.zero)
            {
                var matrix = GUI.matrix;
                GUI.matrix = matrix * Matrix4x4.Translate(new Vector3(Offset.x, Offset.y, 0f));
                try
                {
                    return DrawList(list);
                }
                finally
                {
                    GUI.matrix = matrix;
                }
            }

            return DrawList(list);
        }

        private string DrawList(HudDrawList list)
        {

            string clicked = null;
            for (var i = 0; i < list.Count; i++)
            {
                var command = list[i];
                var rect = ToRect(command.Box);
                switch (command.Kind)
                {
                    case HudDrawKind.Surface:
                        AirsideTheme.DrawGlass(rect, command.Value <= 0f ? 0.9f : command.Value);
                        break;

                    case HudDrawKind.Card:
                        AirsideTheme.DrawCard(rect, command.Value <= 0f ? 1f : command.Value);
                        break;

                    case HudDrawKind.Fill:
                        // Fills of real size are softly rounded; thin rules and ticks stay crisp.
                        AirsideTheme.DrawRounded(rect, WithAlpha(Colour(command), command.Value),
                            rect.width < 4f || rect.height < 4f ? 0f : Mathf.Min(6f, rect.height * 0.5f));
                        break;

                    case HudDrawKind.Hairline:
                        DrawSolid(rect, WithAlpha(Colour(command), command.Value));
                        break;

                    case HudDrawKind.Outline:
                        AirsideTheme.DrawRounded(rect, WithAlpha(Colour(command), command.Value),
                            Mathf.Min(AirsideTheme.CardRadius, rect.height * 0.5f), 1.5f);
                        break;

                    case HudDrawKind.Text:
                        DrawText(command, rect);
                        break;

                    case HudDrawKind.Bar:
                        AirsideTheme.DrawProgressBar(rect, command.Value, Colour(command), new Color(1f, 1f, 1f, 0.10f));
                        break;

                    case HudDrawKind.Button:
                        if (DrawButton(command, rect))
                            clicked = command.ActionId;
                        break;

                    case HudDrawKind.Hotspot:
                        // A faint wash under the pointer so rows and cards read as clickable (ADR 0135).
                        if (Event.current != null && rect.Contains(Event.current.mousePosition))
                            AirsideTheme.DrawRounded(rect, new Color(1f, 1f, 1f, 0.04f), Mathf.Min(6f, rect.height * 0.5f));
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

                    case HudDrawKind.Pill:
                        DrawPill(command, rect);
                        break;

                    case HudDrawKind.Ring:
                        DrawRing(rect, Colour(command), command.Value, command.FontSize,
                            command.Tone == HudTone.Muted ? 0.35f : 1f);
                        break;

                    case HudDrawKind.Icon:
                        DrawIcon(command, rect);
                        break;

                    case HudDrawKind.Gradient:
                        var tint = Colour(command);
                        var fadeBefore = GUI.color;
                        GUI.color = new Color(tint.r, tint.g, tint.b, Mathf.Clamp01(command.Value));
                        GUI.DrawTexture(rect, AirsideTheme.FadeRight, ScaleMode.StretchToFill, true);
                        GUI.color = fadeBefore;
                        break;

                    case HudDrawKind.Image:
                        var image = AirsideTheme.ArtTexture(command.Text);
                        if (image != null)
                        {
                            var before = GUI.color;
                            GUI.color = new Color(1f, 1f, 1f, command.Value <= 0f ? 1f : command.Value);
                            // Transparent brand lockups must keep their entire silhouette.
                            GUI.DrawTexture(rect, image, command.Text.StartsWith("Brand/", System.StringComparison.Ordinal)
                                ? ScaleMode.ScaleToFit : ScaleMode.ScaleAndCrop, true);
                            GUI.color = before;
                        }
                        else if (command.Text == SplashLayout.WordmarkArt)
                        {
                            // A missing/undecodable PNG must never leave the game's name blank.
                            DrawText(new HudDrawCommand(HudDrawKind.Text, command.Box, "AIRSIDE", HudTone.Default,
                                Mathf.Min(58f, command.Box.Height * 0.72f), HudTextStyle.Bold, HudAlign.Left,
                                command.Value, null, null, true), rect);
                        }
                        break;
                }
            }

            return clicked;
        }

        public static Rect ToRect(HudBox box) => new(box.X, box.Y, box.Width, box.Height);

        public static Color Colour(HudTone tone) => tone switch
        {
            HudTone.Muted => AirsideTheme.InstrumentMuted,
            HudTone.Accent => AirsideTheme.Aqua,
            HudTone.Caution => AirsideTheme.Amber,
            HudTone.Positive => AirsideTheme.GoGreen,
            HudTone.Negative => AirsideTheme.WarnRed,
            HudTone.Route => AirsideTheme.RouteMagenta,
            _ => AirsideTheme.InstrumentText
        };

        private static Color Colour(HudDrawCommand command) =>
            command.ColourHex != null ? AirsideTheme.FromHex(command.ColourHex) : Colour(command.Tone);

        private static Color WithAlpha(Color colour, float alpha) =>
            new(colour.r, colour.g, colour.b, Mathf.Clamp01(alpha <= 0f ? 1f : alpha));

        /// <summary>
        /// Device pixels per HUD point: the uniform scale the OnGUI matrix applies (1 when the HUD is not enlarged).
        /// Text and button art are drawn at this size so they are rasterised at real pixels, not stretched.
        /// </summary>
        private static float DeviceScale()
        {
            var scale = GUI.matrix.lossyScale.x;
            return scale > 1.01f ? scale : 1f;
        }

        /// <summary>
        /// Switches to pixel-sized drawing: the rect grows by <paramref name="scale"/> and the matrix shrinks by it, so the
        /// content lands in the same place at 1:1. Pair with <c>GUI.matrix = saved</c>.
        /// </summary>
        private static Rect EnterDeviceSpace(Rect rect, float scale)
        {
            if (scale <= 1f)
                return rect;
            GUI.matrix = GUI.matrix * Matrix4x4.Scale(new Vector3(1f / scale, 1f / scale, 1f));
            return new Rect(rect.x * scale, rect.y * scale, rect.width * scale, rect.height * scale);
        }

        private void DrawText(HudDrawCommand command, Rect rect)
        {
            var scale = DeviceScale();
            var style = TextStyle(command, scale);
            var previous = GUI.color;
            var saved = GUI.matrix;
            var colour = Colour(command);
            GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01(command.Value <= 0f ? 1f : command.Value));
            style.normal.textColor = colour;
            GUI.Label(EnterDeviceSpace(rect, scale), command.Text, style);
            GUI.matrix = saved;
            GUI.color = previous;
        }

        private bool DrawButton(HudDrawCommand command, Rect rect)
        {
            var enabled = GUI.enabled;
            GUI.enabled = enabled && command.Enabled;
            var scale = DeviceScale();
            var style = ButtonStyle(command.ButtonStyle, command.Text == "×" || command.Text == "?", scale);
            var saved = GUI.matrix;
            // Stable geometry on hover keeps neighbouring targets from appearing to shift.
            var pressed = GUI.Button(EnterDeviceSpace(rect, scale), command.Text, style);
            GUI.matrix = saved;
            GUI.enabled = enabled;
            return pressed;
        }

        private void DrawPill(HudDrawCommand command, Rect rect)
        {
            var colour = Colour(command);
            var filled = command.Value >= 0.5f;
            AirsideTheme.DrawRounded(rect, filled ? colour : AirsideTheme.WithAlpha(colour, 0.16f), rect.height * 0.5f);
            var scale = DeviceScale();
            var style = TextStyle(new HudDrawCommand(HudDrawKind.Text, command.Box, command.Text, command.Tone,
                command.FontSize, HudTextStyle.Bold | HudTextStyle.Caption, HudAlign.Center, 1f, null, null, true), scale);
            style.normal.textColor = filled ? AirsideTheme.OnAccent : colour;
            var height = command.FontSize * 1.35f;
            var saved = GUI.matrix;
            GUI.Label(EnterDeviceSpace(new Rect(rect.x, rect.y + (rect.height - height) * 0.5f, rect.width, height), scale),
                command.Text, style);
            GUI.matrix = saved;
        }

        private static void DrawIcon(HudDrawCommand command, Rect rect)
        {
            var slash = command.Text.IndexOf('/');
            if (slash <= 0)
                return;
            var mask = AirsideTheme.IconMask(command.Text.Substring(0, slash), command.Text.Substring(slash + 1));
            if (mask == null)
                return;
            var previous = GUI.color;
            GUI.color = WithAlpha(Colour(command), command.Value <= 0f ? 1f : command.Value);
            GUI.DrawTexture(rect, mask, ScaleMode.ScaleToFit, true);
            GUI.color = previous;
        }

        /// <summary>
        /// A circular gauge built from short rotated segments, clockwise from twelve o'clock. Cheap
        /// enough for the handful of rings on screen, and needs no custom shader.
        /// </summary>
        private static void DrawRing(Rect rect, Color colour, float progress01, float thickness, float alpha)
        {
            if (progress01 <= 0f)
                return;
            if (thickness <= 0f)
                thickness = 4f;
            colour.a *= alpha;
            var centre = rect.center;
            var radius = rect.width * 0.5f - thickness * 0.5f;
            const int segments = 72;
            var count = Mathf.CeilToInt(segments * Mathf.Clamp01(progress01));
            var step = Mathf.PI * 2f / segments;
            for (var i = 0; i < count; i++)
            {
                var a0 = -Mathf.PI * 0.5f + i * step;
                var a1 = Mathf.Min(a0 + step * 1.15f, -Mathf.PI * 0.5f + Mathf.PI * 2f * progress01);
                var from = centre + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * radius;
                var to = centre + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * radius;
                DrawLine(from, to, colour, thickness);
            }
        }

        private GUIStyle TextStyle(HudDrawCommand command, float scale = 1f)
        {
            // One style per (size, style flags, alignment) triple, built once and reused:
            // OnGUI runs several times a frame and a new GUIStyle per label is pure garbage.
            // The size is in device pixels (HUD points x the HUD scale) so glyphs are not stretched.
            var size = Mathf.Max(8, Mathf.RoundToInt(Mathf.Max(8, Mathf.RoundToInt(command.FontSize)) * scale));
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

        private GUIStyle ButtonStyle(HudButtonStyle kind, bool glyph, float scale = 1f)
        {
            var key = ((int)kind * 2 + (glyph ? 1 : 0)) * 10000 + Mathf.RoundToInt(scale * 100f);
            if (_buttonStyles.TryGetValue(key, out var cached))
                return cached;

            RectOffset Scaled(int left, int right, int top, int bottom) => new RectOffset(
                Mathf.RoundToInt(left * scale), Mathf.RoundToInt(right * scale),
                Mathf.RoundToInt(top * scale), Mathf.RoundToInt(bottom * scale));
            var basis = new GUIStyle(GUI.skin.button)
            {
                fontSize = Mathf.RoundToInt((glyph ? 18 : 12) * scale),
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = false,
                clipping = TextClipping.Clip,
                padding = Scaled(8, 8, 2, glyph ? 4 : 2)
            };

            var style = kind switch
            {
                HudButtonStyle.Primary => AirsideTheme.PrimaryButtonStyle(basis, scale),
                HudButtonStyle.Destructive => AirsideTheme.DestructiveButtonStyle(basis, scale),
                _ => AirsideTheme.ButtonStyle(basis, AirsideTheme.InstrumentText, scale)
            };
            _buttonStyles[key] = style;
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

using System;
using Airside.Domain;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Airside.Presentation
{
    /// <summary>
    /// Launch intro: a brief runway-signal hand-off. The new approach mark appears before
    /// the airport is revealed, then leaves the player in the playable overview. Any key
    /// or click skips it. Soak runs skip it entirely.
    /// </summary>
    public sealed partial class AirsidePrototype
    {
        public const float IntroSeconds = 4.8f;
        public const float IntroMarkRevealSeconds = 0.8f;

        private bool IntroActive => _cameraController != null && _cameraController.IsPlayingIntro;

        private void StartIntro()
        {
            if (SoakMode || _cameraController == null)
                return;
            _cameraController.PlayIntro(IntroSeconds);
        }

        private bool ReadIntroSkip(Keyboard keyboard)
        {
            if (!IntroActive)
                return false;

            var mouse = Mouse.current;
            if (keyboard.anyKey.wasPressedThisFrame
                || (mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame)))
                _cameraController.SkipIntro();
            return true;
        }

        // OnGUI runs several times a frame; build the intro styles once.
        private GUIStyle _introTitleStyle, _introFallbackMarkStyle, _introSubtitleStyle, _introHintStyle;

        private static float Smooth01(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }

        private void DrawIntro(HudLayout layout)
        {
            var elapsed = _cameraController.IntroElapsed;
            var markIn = Smooth01((elapsed - 0.12f) / IntroMarkRevealSeconds);
            var handoff = Smooth01((elapsed - 3.55f) / 1.05f);
            var alpha = markIn * (1f - handoff);
            if (alpha <= 0f)
                return;

            var width = layout.Viewport.x;
            var height = layout.Viewport.y;
            var previous = GUI.color;
            // The world is visible from the first frame, but starts under an ink wash so the
            // mark leads the eye instead of fighting the camera movement.
            GUI.color = new Color(AirsideTheme.RunwayInk.r, AirsideTheme.RunwayInk.g, AirsideTheme.RunwayInk.b,
                Mathf.Lerp(0.82f, 0.04f, Smooth01((elapsed - 1.3f) / 2.6f)) * alpha);
            GUI.DrawTexture(new Rect(0f, 0f, width, height), AirsideTheme.SolidWhite);

            var centreY = height * 0.34f;
            var mark = AirsideTheme.AppMarkLight;
            if (mark != null)
            {
                var size = Mathf.Lerp(156f, 112f, Smooth01((elapsed - 0.78f) / 0.7f));
                GUI.color = new Color(1f, 1f, 1f, alpha);
                GUI.DrawTexture(new Rect((width - size) * 0.5f, centreY - size * 0.5f, size, size), mark, ScaleMode.ScaleToFit, true);
                centreY += size * 0.5f + 20f;
            }
            else
            {
                var fallbackTitleStyle = _introFallbackMarkStyle ??= AirsideTheme.TextStyle(new GUIStyle(GUI.skin.label)
                    { fontSize = 56, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter });
                GUI.color = new Color(1f, 1f, 1f, alpha);
                GUI.Label(new Rect(0f, centreY - 40f, width, 80f), "A", fallbackTitleStyle);
                centreY += 44f;
            }

            var titleStyle = _introTitleStyle ??= AirsideTheme.TextStyle(new GUIStyle(GUI.skin.label)
                { fontSize = 42, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter });
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.Label(new Rect(0f, centreY, width, 54f), "AIRSIDE", titleStyle);
            centreY += 46f;

            var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, AirlineClock.Adelaide);
            var subtitle = _introSubtitleStyle ??= AirsideTheme.TextStyle(new GUIStyle(GUI.skin.label)
                { fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter }, AirsideTheme.OpenSky);
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.Label(new Rect(0f, centreY, width, 28f), "ADELAIDE AIRPORT", subtitle);
            centreY += 25f;
            GUI.Label(new Rect(0f, centreY, width, 28f), $"ADELAIDE  ·  {local:HH:mm}  ·  LIVE", subtitle);
            centreY += 28f;
            GUI.Label(new Rect(0f, centreY, width, 28f), "SAAB 340  →  ATR  →  DASH 8  →  JETS", subtitle);

            var hint = _introHintStyle ??= AirsideTheme.TextStyle(new GUIStyle(GUI.skin.label)
                { fontSize = 13, alignment = TextAnchor.MiddleCenter }, AirsideTheme.OpenSky);
            GUI.color = new Color(1f, 1f, 1f, alpha * 0.88f);
            GUI.Label(new Rect(0f, height - 54f, width, 22f), "Press any key to enter", hint);

            GUI.color = previous;
        }
    }
}

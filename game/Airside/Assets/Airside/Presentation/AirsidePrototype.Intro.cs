using System;
using Airside.Domain;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Airside.Presentation
{
    /// <summary>
    /// Launch intro (Bailey 2026-09-14): the camera glides in over Adelaide while the
    /// wordmark and the live local time fade up, then the start panel appears. Any key
    /// or click skips it. Soak runs skip it entirely.
    /// </summary>
    public sealed partial class AirsidePrototype
    {
        public const float IntroSeconds = 7f;
        private const float IntroFadeSeconds = 1.2f;

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
        private GUIStyle _introTitleStyle, _introSubtitleStyle, _introHintStyle;

        private void DrawIntro(HudLayout layout)
        {
            var elapsed = _cameraController.IntroElapsed;
            var fadeIn = Mathf.Clamp01((elapsed - 0.4f) / IntroFadeSeconds);
            var fadeOut = Mathf.Clamp01((IntroSeconds - elapsed) / IntroFadeSeconds);
            var alpha = Mathf.Min(fadeIn, fadeOut);
            if (alpha <= 0f)
                return;

            var width = layout.Viewport.x;
            var height = layout.Viewport.y;
            var previous = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, alpha);

            var centreY = height * 0.38f;
            var wordmark = AirsideTheme.WordmarkLight;
            if (wordmark != null)
            {
                var w = Mathf.Min(520f, width * 0.6f);
                var h = w * wordmark.height / Mathf.Max(1f, wordmark.width);
                GUI.DrawTexture(new Rect((width - w) * 0.5f, centreY - h * 0.5f, w, h), wordmark, ScaleMode.ScaleToFit);
                centreY += h * 0.5f + 18f;
            }
            else
            {
                var titleStyle = _introTitleStyle ??= AirsideTheme.TextStyle(new GUIStyle(GUI.skin.label)
                    { fontSize = 56, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter });
                GUI.Label(new Rect(0f, centreY - 40f, width, 80f), "AIRSIDE", titleStyle);
                centreY += 52f;
            }

            var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, AirlineClock.Adelaide);
            var subtitle = _introSubtitleStyle ??= AirsideTheme.TextStyle(new GUIStyle(GUI.skin.label)
                { fontSize = 18, alignment = TextAnchor.MiddleCenter }, AirsideTheme.Cloud);
            GUI.Label(new Rect(0f, centreY, width, 28f), $"Adelaide  ·  {local:HH:mm}  ·  live", subtitle);

            var hint = _introHintStyle ??= AirsideTheme.TextStyle(new GUIStyle(GUI.skin.label)
                { fontSize = 13, alignment = TextAnchor.MiddleCenter }, AirsideTheme.OpenSky);
            GUI.Label(new Rect(0f, height - 60f, width, 22f), "Press any key to skip", hint);

            GUI.color = previous;
        }
    }
}

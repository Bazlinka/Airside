using System;
using Airside.Domain;
using Airside.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Airside.Presentation
{
    /// <summary>
    /// The opening (ADR 0122). The game opens on Adelaide T1 at dawn with a dependable
    /// AIRSIDE lockup, live Adelaide clock and departure card to Continue or start a New
    /// airline. Choosing one opens the illustration around the live airport
    /// while the camera glides down to the overview. Any key or click skips the glide. Soak runs
    /// skip both.
    /// </summary>
    public sealed partial class AirsidePrototype
    {
        public const float IntroSeconds = 2.8f;

        /// <summary>How long the title art takes to dissolve into the live airport.</summary>
        public const float IntroMarkRevealSeconds = 1.4f;

        private bool IntroActive => _cameraController != null && _cameraController.IsPlayingIntro;

        // The welcome illustration is a separate surface, not a camera inside live weather.
        private bool WorldWeatherVisible => !AirlineSetupOpen && AirsideSettings.Current.WeatherLayers;

        private readonly SplashModel _splash = new();
        private readonly HudDrawList _splashDrawList = new();
        private string _introGreeting = string.Empty;
        private float _introArtPush = 1f;

        private void StartIntro(string greeting = null)
        {
            if (SoakMode || _cameraController == null || !AirsideSettings.Current.OpeningAnimation)
                return;
            _introGreeting = greeting ?? string.Empty;
            _introArtPush = 1f + 0.06f * Mathf.Clamp01(Time.unscaledTime / 40f);
            _cameraController.PlayIntro(IntroSeconds);
        }

        private bool ReadIntroSkip(Keyboard keyboard)
        {
            if (!IntroActive)
                return false;

            var mouse = Mouse.current;
            if ((keyboard != null && keyboard.anyKey.wasPressedThisFrame)
                || (mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame)))
                _cameraController.SkipIntro();
            return true;
        }

        private static float Smooth01(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }

        /// <summary>A gate-opening reveal: title wings part around the live airport's camera glide.</summary>
        private void DrawIntro(HudLayout layout)
        {
            var elapsed = _cameraController.IntroElapsed;
            var width = layout.Viewport.x;
            var height = layout.Viewport.y;
            var reveal = Smooth01((elapsed - 0.12f) / IntroMarkRevealSeconds);
            var artAlpha = 1f - Smooth01((elapsed - 0.5f) / IntroMarkRevealSeconds);

            _splashDrawList.Clear();
            var wingWidth = width * 0.5f * (1f - reveal);
            if (wingWidth > 0.5f && artAlpha > 0.01f)
            {
                DrawIntroWing(new Rect(0f, 0f, wingWidth, height), width, height, artAlpha);
                DrawIntroWing(new Rect(width - wingWidth, 0f, wingWidth, height), width, height, artAlpha);
                _splashDrawList.Hairline(new HudBox(wingWidth, 0f, 1f, height), HudTone.Accent, 0.3f * artAlpha);
                _splashDrawList.Hairline(new HudBox(width - wingWidth, 0f, 1f, height), HudTone.Accent, 0.3f * artAlpha);
            }

            // The opening frame retains the identity; it lifts away before the live HUD arrives.
            var identityAlpha = 1f - Smooth01((elapsed - 0.1f) / 0.65f);
            if (identityAlpha > 0.01f)
            {
                var title = SplashLayout.Create(width, height, SplashStep.Menu, _splash.HasSave).Title;
                var logoWidth = Mathf.Min(400f, title.Width);
                _splashDrawList.Image(new HudBox(title.X, title.Y + 24f - 18f * (1f - identityAlpha),
                    logoWidth, logoWidth * 0.2f), SplashLayout.WordmarkArt, identityAlpha);
            }
            var letterbox = 28f * (1f - Smooth01((elapsed - 0.5f) / 1.6f));
            if (letterbox > 0.5f)
            {
                _splashDrawList.Hairline(new HudBox(0f, 0f, width, letterbox), HudTone.Default, 1f, AirsidePalette.GlassHex);
                _splashDrawList.Hairline(new HudBox(0f, height - letterbox, width, letterbox), HudTone.Default, 1f, AirsidePalette.GlassHex);
            }
            var greetingIn = Smooth01((elapsed - 0.6f) / 0.6f);
            var greetingOut = Smooth01((elapsed - (IntroSeconds - 1.1f)) / 0.9f);
            var greetingAlpha = greetingIn * (1f - greetingOut);
            if (greetingAlpha > 0.01f && !string.IsNullOrEmpty(_introGreeting))
            {
                var box = HudShell.CentredPanel(new HudBox(0f, height - 152f + 10f * (1f - greetingIn), width, 60f), 460f, 52f);
                ToastPainter.Paint(_splashDrawList, box, _introGreeting, HudTone.Accent, greetingAlpha);
            }
            _hudPainter.Draw(_splashDrawList);

            var hintAlpha = Smooth01((elapsed - 0.15f) / 0.3f) * (1f - greetingOut);
            if (hintAlpha > 0.01f)
            {
                _splashDrawList.Clear();
                _splashDrawList.Text(new HudBox(0f, height - 58f, width, 18f), "PRESS ANY KEY TO SKIP", 10f,
                    HudTone.Default, HudTextStyle.Bold | HudTextStyle.Caption, HudAlign.Center, null, 0.7f * hintAlpha);
                _hudPainter.Draw(_splashDrawList);
            }
        }

        /// <summary>Clip the same full illustration at each edge, avoiding a stretched two-image wipe.</summary>
        private void DrawIntroWing(Rect clip, float width, float height, float alpha)
        {
            var before = GUI.color;
            GUI.BeginGroup(clip);
            try
            {
                var full = new Rect(-clip.x, 0f, width, height);
                var art = AirsideTheme.SplashDawn;
                GUI.color = new Color(1f, 1f, 1f, alpha);
                if (art != null)
                {
                    var artWidth = width * _introArtPush;
                    var artHeight = height * _introArtPush;
                    GUI.DrawTexture(new Rect(full.x - (artWidth - width) * 0.35f,
                        -(artHeight - height) * 0.5f, artWidth, artHeight), art, ScaleMode.ScaleAndCrop, true);
                }
                else
                {
                    var ink = AirsideTheme.RunwayInk;
                    GUI.color = new Color(ink.r, ink.g, ink.b, alpha);
                    GUI.DrawTexture(full, AirsideTheme.SolidWhite);
                }
                var tint = AirsideTheme.RunwayInk;
                GUI.color = new Color(tint.r, tint.g, tint.b, 0.94f * alpha);
                var card = SplashLayout.Create(width, height, SplashStep.Menu, _splash.HasSave).Card;
                GUI.DrawTexture(new Rect(full.x, 0f, Mathf.Min(width, Mathf.Max(card.Right + 260f, width * 0.58f)), height),
                    AirsideTheme.FadeRight, ScaleMode.StretchToFill, true);
            }
            finally
            {
                GUI.EndGroup();
                GUI.color = before;
            }
        }

        // ---- Title screen and first-time setup (ADR 0122 / 0123) ---------------------------

        private GUIStyle _setupFieldStyle;
        private GUIStyle _setupCodeStyle;

        /// <summary>The title screen: Continue / New airline / How to play / Options / Quit, or the setup wizard.</summary>
        private void DrawSplash(HudLayout layout, bool interactive = true)
        {
            ProbeSavedAirline();
            FillSplashModel();
            var splashLayout = SplashLayout.Create(layout.Viewport.x, layout.Viewport.y, _splash.Step, _splash.HasSave,
                !string.IsNullOrEmpty(_splash.SaveError));
            SplashPainter.Paint(_splashDrawList, splashLayout, _splash, AirsideSettings.Current.OpeningAnimation ? Mathf.Clamp01(Time.unscaledTime / 40f) : 0f);

            var enabled = GUI.enabled;
            GUI.enabled = enabled && interactive && !_controlsHelpOpen;
            var clicked = _hudPainter.Draw(_splashDrawList);
            if (_splash.Step == SplashStep.NewAirline && _splash.Setup.Step == SetupStep.Identity)
                DrawSetupFields(splashLayout.Setup, interactive && !_controlsHelpOpen);
            GUI.enabled = enabled;

            if (_controlsHelpOpen)
            {
                DrawControlsHelp(layout);
                return;
            }
            if (interactive && clicked != null)
                RunSplashAction(clicked);
        }

        /// <summary>The two editable fields on the Identity card: airline name and flight code.</summary>
        private void DrawSetupFields(AirlineSetupLayout setupLayout, bool interactive)
        {
            var setup = _splash.Setup;
            var field = _setupFieldStyle ??= new GUIStyle(GUI.skin.textField)
            {
                fontSize = 17,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(14, 14, 4, 4),
                normal = { background = null, textColor = AirsideTheme.InstrumentText },
                focused = { background = null, textColor = AirsideTheme.InstrumentText },
                hover = { background = null, textColor = AirsideTheme.InstrumentText }
            };
            var codeStyle = _setupCodeStyle ??= new GUIStyle(field) { alignment = TextAnchor.MiddleCenter, fontSize = 18 };

            GUI.SetNextControlName("airline-name");
            setup.Name = GUI.TextField(HudPainter.ToRect(setupLayout.NameField), setup.Name ?? string.Empty,
                AirlineSetupModel.NameLimit, field);

            var shown = setup.EffectiveCode;
            GUI.SetNextControlName("airline-code");
            var typed = GUI.TextField(HudPainter.ToRect(setupLayout.CodeField), shown, 3, codeStyle);
            if (typed != shown)
            {
                setup.EditCode(typed);
            }

            if (interactive && Event.current.type == EventType.Repaint && string.IsNullOrEmpty(GUI.GetNameOfFocusedControl()))
                GUI.FocusControl("airline-name");
        }

        private void FillSplashModel()
        {
            _splash.HasSave = _savedAirline != null;
            _splash.SoundOn = !_audioMuted;
            _splash.SaveError = _saveRecoveredFromBackup
                ? "Recovered your previous save. Continue to resume that version."
                : _saveError ?? string.Empty;
            _splash.ClockText = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, AirlineClock.Adelaide).ToString("HH:mm");
            if (_savedAirline == null)
                return;
            _splash.SaveName = SavedAirlineName();
            _splash.SaveLiveryHex = null;
            foreach (var airline in _savedAirline.Airlines)
                if (airline.IsPlayer)
                    _splash.SaveLiveryHex = airline.LiveryHex;
            _splash.SaveTier = string.IsNullOrEmpty(_savedAirline.CareerTier) ? "Provisional" : _savedAirline.CareerTier;
            var fleet = SavedPlayerFleetCount();
            _splash.SaveSummary = $"{fleet} aircraft  ·  ${_savedAirline.CareerFunds:N0}  ·  "
                                  + $"{_savedAirline.CareerReliability}% reliability";
            _splash.SavedWhen = SavedAirlineSummary();
        }

        private int SavedPlayerFleetCount()
        {
            var count = _savedAirline.OutstationFleet?.Count ?? 0;
            foreach (var aircraft in _savedAirline.Fleet)
            foreach (var airline in _savedAirline.Airlines)
                if (airline.IsPlayer && airline.Id == aircraft.AirlineId)
                    count++;
            return count;
        }

        private void RunSplashAction(string action)
        {
            switch (action)
            {
                case SplashPainter.Continue:
                    ContinueAirline();
                    return;
                case SplashPainter.NewAirline:
                    _splash.Step = SplashStep.NewAirline;
                    _splash.Setup.Step = SetupStep.Identity;
                    PlayUiClick();
                    return;
                case SplashPainter.HowToPlay:
                    OpenManual(0);
                    return;
                case SplashPainter.Options:
                    OpenOptionsMenu();
                    return;
                case SplashPainter.Quit:
                    QuitGame();
                    return;
            }

            if (!_splash.Setup.Apply(action, out var outcome))
                return;
            PlayUiClick();
            switch (outcome)
            {
                case SetupOutcome.Start:
                    TryStartFromSplash();
                    break;
                case SetupOutcome.Leave:
                    _splash.Step = SplashStep.Menu;
                    GUI.FocusControl(null);
                    break;
                case SetupOutcome.OpenManual:
                    OpenManual(0);
                    break;
                default:
                    if (_splash.Setup.Step != SetupStep.Identity)
                        GUI.FocusControl(null);
                    break;
            }
        }

        private void OpenManual(int page)
        {
            _manualPage = Mathf.Clamp(page, 0, FlightManual.Pages.Count - 1);
            if (!_controlsHelpOpen)
                ToggleControlsHelp();
        }

        private void TryStartFromSplash()
        {
            if (!_splash.Setup.CanStart)
                return;
            GUI.FocusControl(null);
            _splash.Step = SplashStep.Menu;
            StartAirline(_splash.Setup);
        }

        /// <summary>Enter steps the title screen forward; arrows page the manual while it is open.</summary>
        private void ReadSplashKeys(Keyboard keyboard)
        {
            if (!AirlineSetupOpen || _menuOpen)
                return;
            if (keyboard.f1Key.wasPressedThisFrame)
            {
                if (_controlsHelpOpen) ToggleControlsHelp(); else OpenManual(0);
                return;
            }
            if (_controlsHelpOpen)
            {
                if (keyboard.rightArrowKey.wasPressedThisFrame)
                    PageManual(1);
                if (keyboard.leftArrowKey.wasPressedThisFrame)
                    PageManual(-1);
                return;
            }
            // M remains text in the name/code fields; on the title menu it mutes sound.
            if (_splash.Step == SplashStep.Menu && keyboard.mKey.wasPressedThisFrame)
            {
                _audioMuted = !_audioMuted;
                ApplySettingsAndSave();
                ApplyMasterMute();
                return;
            }
            if (!keyboard.enterKey.wasPressedThisFrame && !keyboard.numpadEnterKey.wasPressedThisFrame)
                return;
            if (_splash.Step == SplashStep.NewAirline)
            {
                if (_splash.Setup.Step == SetupStep.Briefing)
                    TryStartFromSplash();
                else
                    RunSplashAction(AirlineSetupPainter.Next);
            }
            else if (_savedAirline != null)
                ContinueAirline();
            else
                RunSplashAction(SplashPainter.NewAirline);
        }

        /// <summary>Esc on the setup wizard steps back a card (or back to the title menu).</summary>
        private bool TrySplashBack()
        {
            if (!AirlineSetupOpen)
                return false;
            if (_splash.Step == SplashStep.NewAirline)
                RunSplashAction(AirlineSetupPainter.Back);
            // The title owns Escape; there is no running airline to resume yet.
            return true;
        }
    }
}

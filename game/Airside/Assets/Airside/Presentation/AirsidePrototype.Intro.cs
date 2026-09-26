using System;
using Airside.Domain;
using Airside.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Airside.Presentation
{
    /// <summary>
    /// The opening (ADR 0122). The game opens on the title screen: the approved dawn illustration
    /// with the wordmark, the live Adelaide clock and a glass card to Continue or start a New
    /// airline. Choosing one plays the hand-off: the illustration dissolves into the live airport
    /// while the camera glides down to the overview. Any key or click skips the glide. Soak runs
    /// skip both.
    /// </summary>
    public sealed partial class AirsidePrototype
    {
        public const float IntroSeconds = 4.2f;

        /// <summary>How long the title art takes to dissolve into the live airport.</summary>
        public const float IntroMarkRevealSeconds = 1.4f;

        private bool IntroActive => _cameraController != null && _cameraController.IsPlayingIntro;

        private readonly SplashModel _splash = new();
        private readonly HudDrawList _splashDrawList = new();
        private string _introGreeting = string.Empty;

        private void StartIntro(string greeting = null)
        {
            if (SoakMode || _cameraController == null)
                return;
            _introGreeting = greeting ?? string.Empty;
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

        private static float Smooth01(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }

        /// <summary>The hand-off: the title art dissolves while the camera glides, then a welcome pill fades.</summary>
        private void DrawIntro(HudLayout layout)
        {
            var elapsed = _cameraController.IntroElapsed;
            var width = layout.Viewport.x;
            var height = layout.Viewport.y;
            var artAlpha = 1f - Smooth01(elapsed / IntroMarkRevealSeconds);

            _splashDrawList.Clear();
            if (artAlpha > 0.01f)
            {
                _splashDrawList.Image(new HudBox(0f, 0f, width, height), SplashLayout.SplashArt, artAlpha);
                _splashDrawList.Gradient(new HudBox(0f, 0f, width * 0.6f, height), AirsidePalette.GlassHex, 0.88f * artAlpha);
            }

            var greetingIn = Smooth01((elapsed - 0.6f) / 0.6f);
            var greetingOut = Smooth01((elapsed - (IntroSeconds - 1.1f)) / 0.9f);
            var greetingAlpha = greetingIn * (1f - greetingOut);
            if (greetingAlpha > 0.01f && !string.IsNullOrEmpty(_introGreeting))
            {
                var box = HudShell.CentredPanel(new HudBox(0f, height * 0.18f, width, 60f), 460f, 52f);
                ToastPainter.Paint(_splashDrawList, box, _introGreeting, HudTone.Accent, greetingAlpha);
            }
            _hudPainter.Draw(_splashDrawList);

            var hintAlpha = Smooth01((elapsed - 1.2f) / 0.5f) * (1f - greetingOut);
            if (hintAlpha > 0.01f)
            {
                _splashDrawList.Clear();
                _splashDrawList.Text(new HudBox(0f, height - 58f, width, 18f), "PRESS ANY KEY TO SKIP", 10f,
                    HudTone.Default, HudTextStyle.Bold | HudTextStyle.Caption, HudAlign.Center, null, 0.7f * hintAlpha);
                _hudPainter.Draw(_splashDrawList);
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
            var splashLayout = SplashLayout.Create(layout.Viewport.x, layout.Viewport.y, _splash.Step, _splash.HasSave);
            SplashPainter.Paint(_splashDrawList, splashLayout, _splash, Mathf.Clamp01(Time.unscaledTime / 40f));

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
                var letters = new System.Text.StringBuilder(3);
                foreach (var c in typed)
                    if (char.IsLetter(c) && letters.Length < 3)
                        letters.Append(char.ToUpperInvariant(c));
                setup.Code = letters.ToString();
                setup.CodeEdited = setup.Code.Length > 0;
            }

            if (interactive && Event.current.type == EventType.Repaint && string.IsNullOrEmpty(GUI.GetNameOfFocusedControl()))
                GUI.FocusControl("airline-name");
        }

        private void FillSplashModel()
        {
            _splash.HasSave = _savedAirline != null;
            _splash.SaveError = _saveError ?? string.Empty;
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
            var difficulty = string.IsNullOrEmpty(_savedAirline.Difficulty) ? "Standard" : _savedAirline.Difficulty;
            _splash.SaveSummary = $"{fleet} aircraft  ·  ${_savedAirline.CareerFunds:N0}  ·  "
                                  + $"{_savedAirline.CareerReliability}% reliability  ·  {difficulty}";
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
                    _menuOpen = true;
                    _optionsOpen = true;
                    PlayUiClick();
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
            if (_controlsHelpOpen)
            {
                if (keyboard.rightArrowKey.wasPressedThisFrame)
                    PageManual(1);
                if (keyboard.leftArrowKey.wasPressedThisFrame)
                    PageManual(-1);
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
            if (!AirlineSetupOpen || _splash.Step != SplashStep.NewAirline)
                return false;
            RunSplashAction(AirlineSetupPainter.Back);
            return true;
        }
    }
}

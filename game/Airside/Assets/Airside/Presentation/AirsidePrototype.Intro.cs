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

        // ---- Title screen -----------------------------------------------------------------

        private GUIStyle _splashNameStyle;

        /// <summary>The title screen: Continue / New airline / Options / Quit, or the new-airline form.</summary>
        private void DrawSplash(HudLayout layout, bool interactive = true)
        {
            ProbeSavedAirline();
            FillSplashModel();
            var splashLayout = SplashLayout.Create(layout.Viewport.x, layout.Viewport.y, _splash.Step, _splash.HasSave);
            SplashPainter.Paint(_splashDrawList, splashLayout, _splash, Mathf.Clamp01(Time.unscaledTime / 40f));

            var enabled = GUI.enabled;
            GUI.enabled = enabled && interactive;
            var clicked = _hudPainter.Draw(_splashDrawList);

            if (_splash.Step == SplashStep.NewAirline)
            {
                var style = _splashNameStyle ??= new GUIStyle(GUI.skin.textField)
                {
                    fontSize = 17,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft,
                    padding = new RectOffset(14, 14, 4, 4),
                    normal = { background = null, textColor = AirsideTheme.InstrumentText },
                    focused = { background = null, textColor = AirsideTheme.InstrumentText },
                    hover = { background = null, textColor = AirsideTheme.InstrumentText }
                };
                GUI.SetNextControlName("airline-name");
                _airlineNameDraft = GUI.TextField(HudPainter.ToRect(splashLayout.NameField), _airlineNameDraft ?? string.Empty, 32, style);
                if (interactive && GUI.GetNameOfFocusedControl() != "airline-name" && Event.current.type == EventType.Repaint
                    && string.IsNullOrEmpty(GUI.GetNameOfFocusedControl()))
                    GUI.FocusControl("airline-name");
            }
            GUI.enabled = enabled;

            if (interactive && clicked != null)
                RunSplashAction(clicked);
        }

        private void FillSplashModel()
        {
            _splash.HasSave = _savedAirline != null;
            _splash.SaveError = _saveError ?? string.Empty;
            _splash.ClockText = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, AirlineClock.Adelaide).ToString("HH:mm");
            _splash.StartingFunds = AirlineCareerState.StartingFunds;
            _splash.NameValid = (_airlineNameDraft ?? string.Empty).Trim().Length > 0;
            _splash.SelectedLivery = _liveryChoice;
            if (_splash.Liveries.Count == 0)
                foreach (var livery in LiveryChoices)
                    _splash.Liveries.Add(livery);
            if (_savedAirline == null)
                return;
            _splash.SaveName = SavedAirlineName();
            _splash.SaveLiveryHex = null;
            foreach (var airline in _savedAirline.Airlines)
                if (airline.IsPlayer)
                    _splash.SaveLiveryHex = airline.LiveryHex;
            _splash.SaveTier = string.IsNullOrEmpty(_savedAirline.CareerTier) ? "Provisional" : _savedAirline.CareerTier;
            var fleet = SavedPlayerFleetCount();
            _splash.SaveSummary = $"{fleet} aircraft  ·  ${_savedAirline.CareerFunds:N0}  ·  {_savedAirline.CareerReliability}% reliability";
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
                    PlayUiClick();
                    return;
                case SplashPainter.Back:
                    _splash.Step = SplashStep.Menu;
                    GUI.FocusControl(null);
                    PlayUiClick();
                    return;
                case SplashPainter.Start:
                    TryStartFromSplash();
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

            var hex = HudAction.Payload(action, SplashPainter.LiveryPrefix);
            for (var i = 0; i < LiveryChoices.Length && hex.Length > 0; i++)
            {
                if (!string.Equals(LiveryChoices[i].hex, hex, StringComparison.OrdinalIgnoreCase))
                    continue;
                _liveryChoice = i;
                PlayUiClick();
                return;
            }
        }

        private void TryStartFromSplash()
        {
            var name = (_airlineNameDraft ?? string.Empty).Trim();
            if (name.Length == 0)
                return;
            GUI.FocusControl(null);
            _splash.Step = SplashStep.Menu;
            StartAirline(name);
        }

        /// <summary>Enter continues or starts; the title screen owns the keyboard while it is up.</summary>
        private void ReadSplashKeys(Keyboard keyboard)
        {
            if (!AirlineSetupOpen || _menuOpen)
                return;
            if (!keyboard.enterKey.wasPressedThisFrame && !keyboard.numpadEnterKey.wasPressedThisFrame)
                return;
            if (_splash.Step == SplashStep.NewAirline)
                TryStartFromSplash();
            else if (_savedAirline != null)
                ContinueAirline();
            else
            {
                _splash.Step = SplashStep.NewAirline;
                PlayUiClick();
            }
        }

        /// <summary>Esc on the new-airline form steps back to the title menu instead of opening the pause menu.</summary>
        private bool TrySplashBack()
        {
            if (!AirlineSetupOpen || _splash.Step != SplashStep.NewAirline)
                return false;
            _splash.Step = SplashStep.Menu;
            GUI.FocusControl(null);
            PlayUiClick();
            return true;
        }
    }
}

using UnityEngine;

namespace Airside.Presentation
{
    public sealed partial class AirsidePrototype
    {
        private readonly OptionsMenuModel _optionsModel = new();
        private HudView _optionsHudView = HudView.Overview;
        private readonly HudDrawList _optionsDrawList = new();
        private Vector2 _optionsScroll;
        private readonly HudDrawList _optionsRowsDrawList = new();

        private void OpenOptionsMenu()
        {
            _menuOpen = _optionsOpen = true;
            _optionsHudView = CurrentHudView;
            _optionsScroll = Vector2.zero;
            PlayUiClick();
        }

        private void CloseOptionsMenu()
        {
            _optionsOpen = false;
            if (AirlineSetupOpen)
                _menuOpen = false;
            PlayUiClick();
        }

        private void DrawOptionsMenu(HudLayout layout, GUIStyle panel, GUIStyle title, GUIStyle button)
        {
            FillOptionsModel();
            var box = OptionsMenuPainter.Panel(layout.Viewport.x, layout.Viewport.y);
            OptionsMenuPainter.Paint(_optionsDrawList, box, _optionsModel, paintRows: false);
            var action = _hudPainter.Draw(_optionsDrawList);
            var viewport = OptionsMenuPainter.RowsViewport(box);
            var needsScroll = _optionsModel.Rows.Count * OptionsMenuPainter.RowHeight > viewport.Height;
            if (!needsScroll)
            {
                // Preserve device-pixel text in ordinary windows; only use a clipped group when needed.
                OptionsMenuPainter.PaintRows(_optionsRowsDrawList, viewport, _optionsModel);
                action = _hudPainter.Draw(_optionsRowsDrawList) ?? action;
                if (action != null) RunOptionsAction(action);
                return;
            }
            var contentWidth = viewport.Width - (needsScroll ? 18f : 0f);
            _optionsScroll = GUI.BeginScrollView(HudPainter.ToRect(viewport), _optionsScroll,
                new Rect(0f, 0f, contentWidth, _optionsModel.Rows.Count * OptionsMenuPainter.RowHeight));
            var deviceSpace = _hudPainter.DeviceSpace;
            try
            {
                _hudPainter.DeviceSpace = false;
                OptionsMenuPainter.PaintRows(_optionsRowsDrawList, new HudBox(0f, 0f, contentWidth,
                    _optionsModel.Rows.Count * OptionsMenuPainter.RowHeight), _optionsModel);
                action = _hudPainter.Draw(_optionsRowsDrawList) ?? action;
            }
            finally
            {
                _hudPainter.DeviceSpace = deviceSpace;
                GUI.EndScrollView();
            }
            if (action != null)
                RunOptionsAction(action);
        }

        private void FillOptionsModel()
        {
            var s = AirsideSettings.Current;
            _optionsModel.FromTitle = AirlineSetupOpen;
            var rows = _optionsModel.Rows;
            rows.Clear();
            static string On(bool value) => value ? "ON" : "OFF";
            void Add(string label, string value, string detail, string id) =>
                rows.Add(new OptionsRow(label, value, detail, "options:" + id));
            switch (_optionsModel.Section)
            {
                case OptionsSection.General:
                    Add("Sound", On(s.SoundOn), "Airport ambience, aircraft and interface sounds.", "sound");
                    Add("Opening animation", On(s.OpeningAnimation), "Slow title-image movement and the camera glide on entry.", "opening");
                    break;
                case OptionsSection.Camera:
                    Add("Follow on selection", On(s.FollowOnSelect), "Follow an aircraft when you select it.", "follow");
                    Add("Invert orbit", On(s.InvertOrbit), "Reverse mouse movement when orbiting.", "invert");
                    Add("Camera speed", AirsideSettings.CameraSpeedLabels[s.CameraSpeedIndex], "Cycle how quickly manual camera movement responds.", "speed");
                    Add("Cockpit motion", On(s.CockpitMotion), "Camera movement inside the cockpit; turn off for a steadier view.", "cockpit");
                    break;
                case OptionsSection.Views:
                    Add("Customise", HudVisibility.Label(_optionsHudView).ToUpperInvariant(),
                        "Pick which view to set up. Each view remembers its own layout. Click to switch view.", "hudview");
                    foreach (HudElement element in System.Enum.GetValues(typeof(HudElement)))
                        Add(HudVisibility.Label(element), On(s.Hud.Shows(_optionsHudView, element)),
                            HudVisibility.Detail(element), "hud:" + (int)element);
                    break;
                case OptionsSection.Display:
                    Add("Night brightness", NightVisibility.Labels[NightVisibility.Clamp(s.NightBrightness)], "Cycle night visibility without changing the time of day.", "night");
                    Add("Frame rate", s.UncappedFrameRate ? "DISPLAY MAX" : "60 FPS", "60 fps reduces load; display max follows your screen's refresh rate.", "fps");
                    Add("Propeller blur", On(s.PropellerBlur), "Show blurred propellers and engine fans in motion.", "blur");
                    Add("Distant aircraft glow", On(s.DistantGlows), "Help distant aircraft remain visible against the sky.", "glows");
                    break;
                case OptionsSection.World:
                    Add("Live Adelaide weather", On(s.LiveWeather), LiveWeatherStatus, "weather");
                    Add("Live Adelaide sky traffic", On(s.LiveTraffic), LiveTrafficStatus, "traffic");
                    Add("Weather layers", On(s.WeatherLayers), "Show cloud, horizon and mist effects.", "layers");
                    Add("Aircraft lights", On(s.AircraftLights), "Show real-time light beams; lamp lenses remain visible.", "lights");
                    Add("Suburbs and trees", On(s.SuburbBuildings), "Changes take effect the next time you launch Airside.", "suburbs");
                    break;
                case OptionsSection.Notifications:
                    Add("Mac notifications", On(s.MacNotifications),
                        "Important airline events while Airside runs in the background.", "notifications");
                    Add("macOS permission", AirsideMacNotifications.PermissionLabel,
                        AirsideMacNotifications.PermissionHint, "notification-settings");
                    Add("Test notification", "SEND TEST",
                        "Preview an Airside banner after enabling and allowing notifications.", "notification-test");
                    break;
            }
        }

        private void RunOptionsAction(string action)
        {
            if (action == OptionsMenuPainter.Back) { CloseOptionsMenu(); return; }
            const string section = "options:section:";
            if (action.StartsWith(section) && int.TryParse(action.Substring(section.Length), out var index)
                && index >= 0 && index < OptionsMenuPainter.Sections.Length)
            { _optionsModel.Section = (OptionsSection)index; _optionsScroll = Vector2.zero; PlayUiClick(); return; }
            var s = AirsideSettings.Current;
            switch (action)
            {
                case "options:sound":
                    _audioMuted = !_audioMuted;
                    ApplySettingsAndSave(); ApplyMasterMute(); PlayUiClick(); return;
                case "options:hudview":
                    _optionsHudView = _optionsHudView == HudView.Overview ? HudView.Follow : HudView.Overview;
                    PlayUiClick(); return;
                case "options:opening": s.OpeningAnimation = !s.OpeningAnimation; break;
                case "options:notifications":
                    if (!AirsideMacNotifications.Supported && !s.MacNotifications)
                    { ShowToast(AirsideMacNotifications.PermissionHint, HudTone.Caution); return; }
                    s.MacNotifications = !s.MacNotifications;
                    if (s.MacNotifications) AirsideMacNotifications.RequestPermission();
                    else AirsideMacNotifications.Disable();
                    break;
                case "options:notification-settings":
                    if (!AirsideMacNotifications.OpenPermissionSettings())
                        ShowToast(AirsideMacNotifications.PermissionHint, HudTone.Caution);
                    PlayUiClick(); return;
                case "options:notification-test":
                    if (!AirsideMacNotifications.Test())
                        ShowToast("Enable Mac notifications and allow Airside in macOS Notifications, then try again.", HudTone.Caution);
                    PlayUiClick(); return;
                case "options:follow": s.FollowOnSelect = !s.FollowOnSelect; break;
                case "options:invert": s.InvertOrbit = !s.InvertOrbit; break;
                case "options:speed": s.CycleCameraSpeed(); break;
                case "options:cockpit": s.CockpitMotion = !s.CockpitMotion; break;
                case "options:night": s.CycleNightBrightness(); break;
                case "options:fps":
                    s.UncappedFrameRate = !s.UncappedFrameRate;
                    AirsideFramePacing.Apply(s.UncappedFrameRate, SoakMode); break;
                case "options:blur": s.PropellerBlur = !s.PropellerBlur; break;
                case "options:glows": s.DistantGlows = !s.DistantGlows; break;
                case "options:weather":
                    s.LiveWeather = !s.LiveWeather;
                    if (s.LiveWeather) _nextLiveWeatherPollAt = 0f; break;
                case "options:traffic": s.LiveTraffic = !s.LiveTraffic; break;
                case "options:layers": s.WeatherLayers = !s.WeatherLayers; break;
                case "options:lights": s.AircraftLights = !s.AircraftLights; break;
                case "options:suburbs": s.SuburbBuildings = !s.SuburbBuildings; break;
                default:
                    const string hud = "options:hud:";
                    if (action.StartsWith(hud) && int.TryParse(action.Substring(hud.Length), out var element)
                        && element >= 0 && element < HudVisibility.ElementCount)
                    {
                        s.Hud.Toggle(_optionsHudView, (HudElement)element);
                        ApplySettingsAndSave(); PlayUiClick(); return;
                    }
                    return;
            }
            s.Save();
            PlayUiClick();
        }
    }
}

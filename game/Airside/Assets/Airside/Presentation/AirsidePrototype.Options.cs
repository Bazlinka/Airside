using UnityEngine;

namespace Airside.Presentation
{
    public sealed partial class AirsidePrototype
    {
        private readonly OptionsMenuModel _optionsModel = new();
        private readonly HudDrawList _optionsDrawList = new();

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
            OptionsMenuPainter.Paint(_optionsDrawList, box, _optionsModel);
            var action = _hudPainter.Draw(_optionsDrawList);
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
                    Add("Aircraft labels", On(s.FieldTags), "Show registration labels over aircraft.", "tags");
                    Add("Airport map", On(s.MiniMap), "Keep the corner airport map visible.", "map");
                    Add("Opening animation", On(s.OpeningAnimation), "Slow title-image movement and the camera glide on entry.", "opening");
                    break;
                case OptionsSection.Camera:
                    Add("Follow on selection", On(s.FollowOnSelect), "Follow an aircraft when you select it.", "follow");
                    Add("Invert orbit", On(s.InvertOrbit), "Reverse mouse movement when orbiting.", "invert");
                    Add("Camera speed", AirsideSettings.CameraSpeedLabels[s.CameraSpeedIndex], "Cycle how quickly manual camera movement responds.", "speed");
                    Add("Cockpit motion", On(s.CockpitMotion), "Camera movement inside the cockpit; turn off for a steadier view.", "cockpit");
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
                        "Open Mac notification settings. macOS controls banners, sound and Focus.", "notification-settings");
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
            { _optionsModel.Section = (OptionsSection)index; PlayUiClick(); return; }
            var s = AirsideSettings.Current;
            switch (action)
            {
                case "options:sound":
                    _audioMuted = !_audioMuted;
                    ApplySettingsAndSave(); ApplyMasterMute(); PlayUiClick(); return;
                case "options:tags":
                    _fieldTagsVisible = !s.FieldTags;
                    ApplySettingsAndSave(); PlayUiClick(); return;
                case "options:map":
                    _miniMapVisible = !s.MiniMap;
                    ApplySettingsAndSave(); PlayUiClick(); return;
                case "options:opening": s.OpeningAnimation = !s.OpeningAnimation; break;
                case "options:notifications":
                    if (!AirsideMacNotifications.Supported && !s.MacNotifications)
                    { ShowToast("Mac notifications are available in the built Airside app.", HudTone.Caution); return; }
                    s.MacNotifications = !s.MacNotifications;
                    if (s.MacNotifications) AirsideMacNotifications.RequestPermission();
                    else AirsideMacNotifications.Disable();
                    break;
                case "options:notification-settings": AirsideMacNotifications.OpenSystemSettings(); PlayUiClick(); return;
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
                default: return;
            }
            s.Save();
            PlayUiClick();
        }
    }
}

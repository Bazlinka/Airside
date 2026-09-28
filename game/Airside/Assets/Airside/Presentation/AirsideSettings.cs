using UnityEngine;

namespace Airside.Presentation
{
    /// <summary>
    /// Player options that change the live session. Stored in PlayerPrefs so the
    /// next launch comes back the same way. Simulation timing is not here — live
    /// Adelaide time does not pause or skip (ADR 0045).
    /// </summary>
    public sealed class AirsideSettings
    {
        public const string PrefPrefix = "airside.settings.";

        public static readonly string[] CameraSpeedLabels = { "Slow", "Normal", "Fast" };
        public static readonly float[] CameraSpeedValues = { 0.4f, 0.65f, 1f };

        public static AirsideSettings Current { get; private set; } = new();

        public bool SoundOn = true;
        public bool FieldTags = true;
        public bool MiniMap = true;
        public bool FollowOnSelect = true;
        public bool InvertOrbit = false;

        /// <summary>
        /// Real aircraft from adsb.lol in the sky only (ADR 0081/0086). Off until the game
        /// can own the field; never drawn on the ground. Needs a connection.
        /// </summary>
        public bool LiveTraffic = false;
        /// <summary>
        /// Fixed-location Adelaide forecast used by sky/weather presentation only.
        /// On by default with a deterministic offline fallback.
        /// </summary>
        public bool LiveWeather = true;
        /// <summary>
        /// False caps a high-refresh (ProMotion/120 Hz+) display at 60 fps; true renders at
        /// the display's own rate. Background windows throttle either way (ADR 0101).
        /// </summary>
        public bool UncappedFrameRate = false;

        // Graphics tests (ADR 0155): each switches off one of the heavier effects added since the
        // 60 fps measurement of 2026-09-26, so a slowdown can be traced on the Mac that has it.
        // All on by default; turning one off changes only how the game looks.

        /// <summary>Overcast sheet, horizon band and low mist (ADR 0143): full-screen transparent layers.</summary>
        public bool WeatherLayers = true;
        /// <summary>Propeller and fan blur discs (ADR 0148/0151); off shows the blades turning instead.</summary>
        public bool PropellerBlur = true;
        /// <summary>Camera-facing glow on aircraft far out (ADR 0142).</summary>
        public bool DistantGlows = true;
        /// <summary>Real-time nav, strobe, beacon, landing and taxi lights on every aircraft; the lamps still glow.</summary>
        public bool AircraftLights = true;
        /// <summary>
        /// The extruded suburbs and satellite-placed trees around the airfield (ADR 0159/0160). Built with the world,
        /// so a change applies from the next launch.
        /// </summary>
        public bool SuburbBuildings = true;

        public int CameraSpeedIndex = 1;

        /// <summary>Night brightness level (ADR 0162), an index into <see cref="NightVisibility.Labels"/>.</summary>
        public int NightBrightness = NightVisibility.DefaultLevel;

        public AirsideSettings CycleNightBrightness()
        {
            NightBrightness = (NightVisibility.Clamp(NightBrightness) + 1) % NightVisibility.Labels.Length;
            return this;
        }

        public float CameraSpeed =>
            CameraSpeedIndex >= 0 && CameraSpeedIndex < CameraSpeedValues.Length
                ? CameraSpeedValues[CameraSpeedIndex]
                : 1f;

        /// <summary>
        /// <c>-airsideGraphicsOff weather,propblur,glows,lights</c> switches graphics tests off for
        /// this launch only, so a soak can A/B them without touching saved options (ADR 0155).
        /// </summary>
        public const string GraphicsOffFlag = "-airsideGraphicsOff";

        public static AirsideSettings Load()
        {
            Current = FromPrefs();
            ApplyGraphicsOff(Current, System.Environment.GetCommandLineArgs());
            return Current;
        }

        /// <summary>True when a launch flag set the graphics tests; they are then not saved over the player's choice.</summary>
        public bool GraphicsFromLaunchFlag { get; private set; }

        public static void ApplyGraphicsOff(AirsideSettings settings, string[] args)
        {
            if (settings == null || args == null)
                return;
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (args[i] != GraphicsOffFlag)
                    continue;
                settings.GraphicsFromLaunchFlag = true;
                foreach (var raw in args[i + 1].Split(','))
                {
                    switch (raw.Trim().ToLowerInvariant())
                    {
                        case "weather": settings.WeatherLayers = false; break;
                        case "propblur": settings.PropellerBlur = false; break;
                        case "glows": settings.DistantGlows = false; break;
                        case "lights": settings.AircraftLights = false; break;
                        case "suburbs": settings.SuburbBuildings = false; break;
                        case "all":
                            settings.WeatherLayers = settings.PropellerBlur = false;
                            settings.DistantGlows = settings.AircraftLights = false;
                            settings.SuburbBuildings = false;
                            break;
                    }
                }
            }
        }

        public static AirsideSettings FromPrefs()
        {
            var settings = new AirsideSettings();
            settings.SoundOn = Pref("sound", 1) != 0;
            settings.FieldTags = Pref("tags", 1) != 0;
            settings.MiniMap = Pref("minimap", 1) != 0;
            settings.FollowOnSelect = Pref("follow", 1) != 0;
            settings.InvertOrbit = Pref("invert", 0) != 0;
            settings.LiveTraffic = Pref("livetraffic.v2", 0) != 0;
            settings.LiveWeather = Pref("liveweather.v1", 1) != 0;
            settings.UncappedFrameRate = Pref("uncappedfps", 0) != 0;
            settings.WeatherLayers = Pref("gfx.weatherlayers", 1) != 0;
            settings.PropellerBlur = Pref("gfx.propblur", 1) != 0;
            settings.DistantGlows = Pref("gfx.distantglows", 1) != 0;
            settings.AircraftLights = Pref("gfx.aircraftlights", 1) != 0;
            settings.SuburbBuildings = Pref("gfx.suburbs", 1) != 0;
            settings.NightBrightness = NightVisibility.Clamp(Pref("nightbrightness.v1", NightVisibility.DefaultLevel));
            settings.CameraSpeedIndex = Mathf.Clamp(Pref("camera", 1), 0, CameraSpeedValues.Length - 1);
            return settings;
        }

        public void Save()
        {
            PlayerPrefs.SetInt(PrefPrefix + "sound", SoundOn ? 1 : 0);
            PlayerPrefs.SetInt(PrefPrefix + "tags", FieldTags ? 1 : 0);
            PlayerPrefs.SetInt(PrefPrefix + "minimap", MiniMap ? 1 : 0);
            PlayerPrefs.SetInt(PrefPrefix + "follow", FollowOnSelect ? 1 : 0);
            PlayerPrefs.SetInt(PrefPrefix + "invert", InvertOrbit ? 1 : 0);
            PlayerPrefs.SetInt(PrefPrefix + "livetraffic.v2", LiveTraffic ? 1 : 0);
            PlayerPrefs.SetInt(PrefPrefix + "liveweather.v1", LiveWeather ? 1 : 0);
            PlayerPrefs.SetInt(PrefPrefix + "uncappedfps", UncappedFrameRate ? 1 : 0);
            if (!GraphicsFromLaunchFlag)
            {
                PlayerPrefs.SetInt(PrefPrefix + "gfx.weatherlayers", WeatherLayers ? 1 : 0);
                PlayerPrefs.SetInt(PrefPrefix + "gfx.propblur", PropellerBlur ? 1 : 0);
                PlayerPrefs.SetInt(PrefPrefix + "gfx.distantglows", DistantGlows ? 1 : 0);
                PlayerPrefs.SetInt(PrefPrefix + "gfx.aircraftlights", AircraftLights ? 1 : 0);
                PlayerPrefs.SetInt(PrefPrefix + "gfx.suburbs", SuburbBuildings ? 1 : 0);
            }
            PlayerPrefs.SetInt(PrefPrefix + "camera", CameraSpeedIndex);
            PlayerPrefs.SetInt(PrefPrefix + "nightbrightness.v1", NightBrightness);
            PlayerPrefs.Save();
            Current = this;
        }

        public AirsideSettings CycleCameraSpeed()
        {
            CameraSpeedIndex = (CameraSpeedIndex + 1) % CameraSpeedValues.Length;
            return this;
        }

        private static int Pref(string key, int fallback) =>
            PlayerPrefs.GetInt(PrefPrefix + key, fallback);
    }
}

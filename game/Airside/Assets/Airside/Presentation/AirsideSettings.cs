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
        public int CameraSpeedIndex = 1;

        public float CameraSpeed =>
            CameraSpeedIndex >= 0 && CameraSpeedIndex < CameraSpeedValues.Length
                ? CameraSpeedValues[CameraSpeedIndex]
                : 1f;

        public static AirsideSettings Load()
        {
            Current = FromPrefs();
            return Current;
        }

        public static AirsideSettings FromPrefs()
        {
            var settings = new AirsideSettings();
            settings.SoundOn = Pref("sound", 1) != 0;
            settings.FieldTags = Pref("tags", 1) != 0;
            settings.MiniMap = Pref("minimap", 1) != 0;
            settings.FollowOnSelect = Pref("follow", 1) != 0;
            settings.InvertOrbit = Pref("invert", 0) != 0;
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
            PlayerPrefs.SetInt(PrefPrefix + "camera", CameraSpeedIndex);
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

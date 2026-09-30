using System;
using System.Globalization;
using System.IO;
using System.Linq;
using Airside.Domain;
using UnityEngine;

namespace Airside.Presentation
{
    /// <summary>
    /// Developer-only packaged mixer capture. No world, simulation or save is created.
    /// -airsideAudioReview all|ATR42|SF34|... -airsideAudioReviewOut /absolute/directory
    /// Captures the actual Unity listener output, rather than an offline approximation.
    /// </summary>
    public sealed class AircraftAudioReview : MonoBehaviour
    {
        private const float SecondsPerType = 28f;
        private AircraftSpec[] _types;
        private int _index;
        private float _started;
        private string _out;
        private AircraftSoundEmitter _voice;
        private AircraftAudioRecorder _recorder;
        private string _stage;

        public static bool TryStart(GameObject host)
        {
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, "-airsideAudioReview");
            if (index < 0) return false;
            var id = index + 1 < args.Length ? args[index + 1] : "all";
            var types = id == "all" ? AircraftCatalogue.All.ToArray()
                : AircraftCatalogue.All.Where(t => string.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase)).ToArray();
            var output = Array.IndexOf(args, "-airsideAudioReviewOut");
            if (types.Length == 0 || output < 0 || output + 1 >= args.Length || !Path.IsPathRooted(args[output + 1]))
            {
                Debug.LogError("[Aircraft audio] review requires a catalogue type (or all) and an absolute output directory");
                Application.Quit(2);
                return true;
            }
            var review = host.AddComponent<AircraftAudioReview>();
            review._types = types;
            review._out = args[output + 1];
            Directory.CreateDirectory(review._out);
            Application.runInBackground = true;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;
            AudioListener.pause = false;
            AudioListener.volume = 1f;
            foreach (var listener in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
                listener.enabled = false;
            foreach (var existingCamera in FindObjectsByType<Camera>(FindObjectsSortMode.None))
                existingCamera.enabled = false;
            var cameraHost = new GameObject("Audio review listener");
            cameraHost.transform.SetParent(host.transform, false);
            cameraHost.transform.localPosition = new Vector3(0f, 12f, -24f);
            var camera = cameraHost.AddComponent<Camera>();
            camera.backgroundColor = new Color(0.035f, 0.045f, 0.055f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            cameraHost.AddComponent<AudioListener>();
            review._recorder = cameraHost.AddComponent<AircraftAudioRecorder>();
            Debug.Log("[Aircraft audio] packaged review " + BuildIdentityReader.Current.FullLabel);
            // The first Update follows graphics/audio device initialisation. Starting
            // in Awake loses the first seconds from the listener capture.
            return true;
        }

        private void BeginType()
        {
            _started = Time.unscaledTime;
            _stage = null;
            var aircraft = new GameObject("Review " + _types[_index].Id);
            aircraft.transform.SetParent(transform, false);
            _voice = aircraft.AddComponent<AircraftSoundEmitter>();
            _voice.Configure(_types[_index].Type,
                Resources.Load<AudioClip>(AircraftEngineAudio.ResourceName(_types[_index].Type)), null);
            _recorder.Begin(SecondsPerType + 4f);
            Debug.Log("[Aircraft audio] begin " + _types[_index].Id);
        }

        private void Update()
        {
            if (_voice == null) BeginType();
            var t = Time.unscaledTime - _started;
            if (t >= SecondsPerType)
            {
                _voice.StopVoices();
                if (!_recorder.Save(Path.Combine(_out, _types[_index].Id + ".wav")))
                {
                    enabled = false;
                    Application.Quit(2);
                    return;
                }
                Destroy(_voice.gameObject);
                if (++_index >= _types.Length)
                {
                    File.WriteAllText(Path.Combine(_out, "complete.txt"), BuildIdentityReader.Current.FullLabel);
                    enabled = false;
                    Application.Quit();
                    return;
                }
                BeginType();
                t = 0f;
            }
            var type = _types[_index].Type;
            var prop = EngineVoice.ClassOf(type) == EngineClass.Turboprop;
            float left = 1f, right = 1f, power, rotation, speed = 0f, reverse = 0f;
            bool grounded = true, landing = false;
            string stage;
            if (t < 4f)
            {
                stage = "startup";
                right = Mathf.SmoothStep(0, 1, t / 3f);
                left = Mathf.SmoothStep(0, 1, (t - 1f) / 3f);
                power = prop ? 0.06f : 0.21f;
                rotation = (prop ? 0.68f : power) * Mathf.Max(left, right);
            }
            else if (t < 6f) { stage = "idle"; power = prop ? 0.06f : 0.21f; rotation = prop ? 0.68f : power; }
            else if (t < 8f) { stage = "taxi"; power = prop ? 0.14f : 0.26f; rotation = prop ? 0.78f : power; speed = 6f; }
            else if (t < 13f)
            {
                stage = "takeoff";
                var roll = Mathf.SmoothStep(0, 1, (t - 8f) / 5f);
                power = Mathf.Lerp(prop ? 0.14f : 0.26f, prop ? 1f : 0.95f, roll);
                rotation = prop ? Mathf.Lerp(0.78f, 1f, roll) : power;
                speed = 70f * roll;
                grounded = t < 12.5f;
            }
            else if (t < 15f) { stage = "climb"; power = 0.88f; rotation = prop ? 0.94f : power; grounded = false; }
            else if (t < 17f) { stage = "approach"; power = prop ? 0.30f : 0.45f; rotation = prop ? 0.92f : power; grounded = false; }
            else if (t < 17.5f) { stage = "flare"; power = prop ? 0.16f : 0.27f; rotation = prop ? 0.92f : power; grounded = false; landing = true; }
            else if (t < 21f)
            {
                stage = "touchdown-reverse";
                landing = true;
                var elapsed = t - 17.5f;
                reverse = Mathf.SmoothStep(0, 1, elapsed / 0.7f);
                power = Mathf.Lerp(prop ? 0.12f : 0.21f, prop ? 0.82f : 0.72f, reverse);
                rotation = prop ? 0.92f : power;
                speed = Mathf.Lerp(65f, 20f, elapsed / 3.5f);
            }
            else if (t < 23f)
            {
                stage = "rollout"; landing = true;
                reverse = 1f - Mathf.SmoothStep(0, 1, (t - 21f) / 1.1f);
                power = Mathf.Lerp(prop ? 0.14f : 0.26f, prop ? 0.82f : 0.72f, reverse);
                rotation = prop ? 0.78f : power;
                speed = Mathf.Lerp(20f, 5f, (t - 21f) / 2f);
            }
            else
            {
                stage = t < 27f ? "shutdown" : t < 27.5f ? "hidden" : "mute";
                left = 1f - Mathf.SmoothStep(0, 1, (t - 23f) / 2.6f);
                right = 1f - Mathf.SmoothStep(0, 1, (t - 24f) / 3f);
                power = prop ? 0.06f : 0.21f;
                rotation = (prop ? 0.68f : power) * Mathf.Max(left, right);
            }
            if (stage != _stage)
            {
                _stage = stage;
                Debug.Log("[Aircraft audio] " + type.Id + " " + stage + " at "
                          + t.ToString("F2", CultureInfo.InvariantCulture));
            }
            if (t >= 27f) _voice.gameObject.SetActive(false);
            _voice.Apply("REVIEW-" + type.Id, power, rotation, left, right, reverse, speed,
                grounded, landing, _recorder.transform.position, 1f, t >= 27.5f, Time.unscaledDeltaTime);
        }

        private void OnGUI()
        {
            if (_types == null || _index >= _types.Length) return;
            GUI.Label(new Rect(30, 30, 800, 40), "Aircraft audio review · " + _types[_index].Name + " · " + _stage);
        }
    }
}

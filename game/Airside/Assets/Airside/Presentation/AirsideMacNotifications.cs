using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Airside.Presentation
{
    /// <summary>Local macOS banners for important events while the player is elsewhere. No shell or network.</summary>
    public static class AirsideMacNotifications
    {
        private static readonly DesktopNotificationBuffer Buffer = new();
        private static bool _missing;
        private static bool _requested;
        private static float _nextStatusCheck;

        public static bool Supported
        {
            get
            {
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
                return !_missing;
#else
                return false;
#endif
            }
        }

        public static string PermissionLabel
        {
            get
            {
                if (!Supported) return "UNAVAILABLE";
                var state = State();
                return state switch { 0 => "NOT ASKED", 1 => "REQUESTING", 2 => "ALLOWED", 3 => "BLOCKED", _ => "UNAVAILABLE" };
            }
        }

        public static void RequestPermission()
        {
            if (!Supported) return;
            _requested = true;
            Call(() => AS_RequestPermission());
        }

        public static void Disable() { Buffer.ClearPending(); }

        public static void Publish(DesktopNotice? notice)
        {
            if (!notice.HasValue || !Supported || !AirsideSettings.Current.MacNotifications || Application.isFocused) return;
            if (State() == 2) Buffer.Enqueue(notice.Value, Time.unscaledTime);
        }

        public static void Tick()
        {
            if (!Supported || !AirsideSettings.Current.MacNotifications) { Buffer.ClearPending(); return; }
            if (!_requested) RequestPermission();
            if (Application.isFocused) { Buffer.ClearPending(); return; }
            if (State() != 2) { Buffer.ClearPending(); return; }
            var notice = Buffer.Take(Time.unscaledTime);
            if (notice.HasValue) Send(notice.Value, false);
        }

        public static bool Test()
        {
            if (!Supported || !AirsideSettings.Current.MacNotifications || State() != 2) return false;
            Send(new DesktopNotice("test", "Notifications are ready", "Airside will alert you to important airline events while it runs in the background."), true);
            return !_missing;
        }

        public static void OpenSystemSettings()
        {
            if (Supported) Call(() => AS_OpenSettings());
        }

        private static int State()
        {
            if (!Supported) return 4;
            var state = 4;
            Call(() =>
            {
                if (Time.unscaledTime >= _nextStatusCheck)
                {
                    _nextStatusCheck = Time.unscaledTime + 1f;
                    AS_RefreshPermission();
                }
                state = AS_PermissionState();
            });
            return state;
        }

        private static void Send(DesktopNotice notice, bool test) => Call(() =>
            AS_Send(notice.Title, notice.Body, AirsideSettings.Current.SoundOn ? 1 : 0, test ? 1 : 0));

        private static void Call(Action action)
        {
            if (!Supported) return;
            try { action(); }
            catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
            {
                _missing = true;
                Buffer.ClearPending();
                Debug.LogWarning("Airside macOS notifications unavailable in this build: " + e.GetType().Name);
            }
        }

        [DllImport("AirsideNotifications")] private static extern void AS_RequestPermission();
        [DllImport("AirsideNotifications")] private static extern void AS_RefreshPermission();
        [DllImport("AirsideNotifications")] private static extern int AS_PermissionState();
        [DllImport("AirsideNotifications")] private static extern void AS_OpenSettings();
        [DllImport("AirsideNotifications")] private static extern void AS_Send(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string title, [MarshalAs(UnmanagedType.LPUTF8Str)] string body, int sound, int test);
    }
}

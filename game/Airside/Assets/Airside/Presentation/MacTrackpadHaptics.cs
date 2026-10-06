using System;
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
using System.Runtime.InteropServices;
#endif

namespace Airside.Presentation
{
    /// <summary>
    /// Force Touch trackpad taps through the system's NSHapticFeedbackManager. It is called through the
    /// Objective-C runtime that every macOS process already links, so no plugin or third-party code is
    /// added. The trackpad only plays a tap while a finger is resting on it. Anywhere but macOS, or if
    /// the call ever fails, every method is a silent no-op. Presentation only.
    /// </summary>
    public static class MacTrackpadHaptics
    {
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
        private const string ObjC = "/usr/lib/libobjc.A.dylib";
        // NSHapticFeedbackPattern: Generic 0, Alignment 1, LevelChange 2. PerformanceTime: Now 1.
        private const long PerformNow = 1;
        private static IntPtr _performer, _performSelector;
        private static bool _ready, _failed;

        [DllImport(ObjC)] private static extern IntPtr objc_getClass(string name);
        [DllImport(ObjC)] private static extern IntPtr sel_registerName(string name);
        [DllImport(ObjC, EntryPoint = "objc_msgSend")]
        private static extern IntPtr SendForObject(IntPtr receiver, IntPtr selector);
        [DllImport(ObjC, EntryPoint = "objc_msgSend")]
        private static extern void SendPerform(IntPtr receiver, IntPtr selector, long pattern, long when);

        private static void Prepare()
        {
            if (_ready || _failed) return;
            try
            {
                var manager = objc_getClass("NSHapticFeedbackManager");
                if (manager == IntPtr.Zero) { _failed = true; return; }
                _performer = SendForObject(manager, sel_registerName("defaultPerformer"));
                _performSelector = sel_registerName("performFeedbackPattern:performanceTime:");
                _failed = _performer == IntPtr.Zero;
                _ready = !_failed;
            }
            catch (Exception)
            {
                _failed = true;
            }
        }

        public static bool Available
        {
            get { Prepare(); return _ready; }
        }

        public static void Perform(HapticKind kind)
        {
            if (kind == HapticKind.None) return;
            Prepare();
            if (!_ready) return;
            try
            {
                SendPerform(_performer, _performSelector, PatternFor(kind), PerformNow);
            }
            catch (Exception)
            {
                _failed = true;
                _ready = false;
            }
        }
#else
        public static bool Available => false;
        public static void Perform(HapticKind kind) { }
#endif

        /// <summary>The system pattern for a tap: light generic tick, snappy alignment, firm level change.</summary>
        public static long PatternFor(HapticKind kind) => kind switch
        {
            HapticKind.Heavy => 2,
            HapticKind.Medium => 1,
            _ => 0
        };
    }
}

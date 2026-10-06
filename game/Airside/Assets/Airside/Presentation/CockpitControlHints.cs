namespace Airside.Presentation
{
    /// <summary>One-line control reminders for the aircraft views, so the dock and the entry toast say the same thing.</summary>
    public static class CockpitControlHints
    {
        /// <param name="view">0 cockpit, 1 left window, 2 right window, 3 exterior (the flight view dock order).</param>
        public static string Dock(int view, bool narrow)
        {
            if (view == 3)
                return narrow ? "Drag to orbit · +/- zoom · Home" : "Drag to orbit · scroll or +/- to zoom · Home resets";
            return narrow
                ? "Drag or arrows to look · 1-5 glance · Home"
                : "Drag (either button) or arrow keys to look · 1-5 glance · scroll or +/- zoom · Home recentres";
        }

        /// <summary>Shown once on entering an aircraft view; null where the dock already says enough.</summary>
        public static string EntryToast(int view) => view switch
        {
            0 => "Look: drag with either button, or arrow keys. 1-5 glance at panels. Home recentres. Esc leaves.",
            1 or 2 => "Look: drag with either button, or arrow keys. Home recentres. Esc leaves.",
            3 => "Orbit: drag with either button. Scroll or +/- zooms. Home resets. Esc leaves.",
            _ => null
        };
    }
}

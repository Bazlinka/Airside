using System;
using System.Collections.Generic;

namespace Airside.Presentation
{
    /// <summary>
    /// Pure copy for the Controls help overlay (F1). Presentation only —
    /// documents existing hotkeys; never changes simulation.
    /// </summary>
    public static class ControlsHelp
    {
        public readonly struct Binding
        {
            public Binding(string key, string action)
            {
                Key = key ?? string.Empty;
                Action = action ?? string.Empty;
            }

            public string Key { get; }
            public string Action { get; }
        }

        public readonly struct Section
        {
            public Section(string title, IReadOnlyList<Binding> bindings)
            {
                Title = title ?? string.Empty;
                Bindings = bindings ?? Array.Empty<Binding>();
            }

            public string Title { get; }
            public IReadOnlyList<Binding> Bindings { get; }
        }

        public static IReadOnlyList<Section> Sections { get; } = new[]
        {
            new Section("Camera", new[]
            {
                new Binding("WASD", "Pan the camera"),
                new Binding("Q / E", "Orbit"),
                new Binding("Z / X", "Lower / raise the camera"),
                new Binding("F", "Follow selected aircraft"),
                new Binding("R", "Back to the overview"),
                new Binding("Mouse", "Right-drag orbits, drag pans, scroll zooms, click picks an aircraft"),
                new Binding("In an aircraft", "Drag or arrows look, 1-5 glance, Home recentres"),
            }),
            new Section("Airline", new[]
            {
                new Binding("Tab", "Map: plan flights"),
                new Binding("[ / ]", "Previous / next aircraft"),
                new Binding("L", "Aircraft tags (per view: overview or follow)"),
                new Binding("N", "Airport mini-map for this view (click or drag to move)"),
                new Binding("H", "Fleet: your aircraft and the market"),
                new Binding("T", "Ops: the departures and arrivals board"),
                new Binding("C", "Contracts: yours and the offers"),
                new Binding("F8", "Dev tools (playtest)"),
                new Binding("F1", "Flight Manual"),
            }),
            new Section("General", new[]
            {
                new Binding("Esc", "Close a panel, then open the menu"),
                new Binding("M", "Mute"),
            }),
        };

        public static int TotalBindings()
        {
            var total = 0;
            foreach (var section in Sections)
                total += section.Bindings.Count;
            return total;
        }

        public static bool IncludesKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return false;
            foreach (var section in Sections)
            {
                foreach (var binding in section.Bindings)
                {
                    if (string.Equals(binding.Key, key, StringComparison.OrdinalIgnoreCase))
                        return true;
                    // Multi-key rows like "Q / E" or "F8"
                    if (binding.Key.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0)
                        return true;
                }
            }

            return false;
        }
    }
}

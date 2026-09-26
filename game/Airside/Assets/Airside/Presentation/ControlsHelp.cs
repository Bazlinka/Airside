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
                new Binding("WASD", "Pan the overview"),
                new Binding("Q / E", "Orbit"),
                new Binding("Z / X", "Lower / raise"),
                new Binding("F", "Follow selected aircraft"),
                new Binding("R", "Overview"),
                new Binding("Mouse", "Orbit, pan, zoom, pick aircraft"),
            }),
            new Section("Airline", new[]
            {
                new Binding("Tab", "Map workspace — routes and flight planning"),
                new Binding("[ / ]", "Previous / next aircraft"),
                new Binding("L", "Aircraft tags at the airport"),
                new Binding("N", "Airport mini-map (click or drag to move)"),
                new Binding("H", "Fleet workspace — aircraft and market"),
                new Binding("T", "Operations workspace — movement board"),
                new Binding("C", "Contracts workspace — active and offers"),
                new Binding("F8", "Dev tools (playtest)"),
                new Binding("F1", "Flight Manual — how to play and controls"),
            }),
            new Section("General", new[]
            {
                new Binding("Esc", "Close panel, options, then menu"),
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

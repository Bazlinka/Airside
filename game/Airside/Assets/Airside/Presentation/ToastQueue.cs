using System.Collections.Generic;

namespace Airside.Presentation
{
    /// <summary>One HUD message and when it was shown (unscaled real seconds).</summary>
    public readonly struct ToastEntry
    {
        public ToastEntry(string message, float shownAt, int repeats = 1, HudTone tone = HudTone.Accent)
        {
            Message = message;
            ShownAt = shownAt;
            Repeats = repeats;
            Tone = tone;
        }

        /// <summary>The leading dot's colour: aqua news, amber career milestones, red refusals.</summary>
        public HudTone Tone { get; }
        public string Message { get; }
        public float ShownAt { get; }
        public int Repeats { get; }
    }

    /// <summary>
    /// Stacks HUD messages so a new one no longer wipes one still being read, and keeps a short
    /// history. Time is passed in, so EditMode tests drive it without a player loop.
    /// </summary>
    public sealed class ToastQueue
    {
        public const float LifetimeSeconds = 6f;
        public const float FadeSeconds = 0.6f;
        public const int MaxVisible = 3;
        public const int HistoryLength = 10;

        private readonly List<ToastEntry> _history = new();

        /// <summary>Newest first.</summary>
        public IReadOnlyList<ToastEntry> History => _history;

        public void Push(string message, float now, HudTone tone = HudTone.Accent)
        {
            if (string.IsNullOrEmpty(message))
                return;

            // The same message again while it is still up just restarts its timer.
            if (_history.Count > 0 && _history[0].Message == message && IsAlive(_history[0], now))
            {
                _history[0] = new ToastEntry(message, now, _history[0].Repeats + 1, tone);
                return;
            }

            _history.Insert(0, new ToastEntry(message, now, 1, tone));
            if (_history.Count > HistoryLength)
                _history.RemoveAt(_history.Count - 1);
        }

        /// <summary>Refill <paramref name="visible"/> with live messages, newest first, at most <see cref="MaxVisible"/>.</summary>
        public void Visible(float now, List<ToastEntry> visible)
        {
            visible.Clear();
            foreach (var entry in _history)
            {
                if (visible.Count >= MaxVisible)
                    break;
                if (IsAlive(entry, now))
                    visible.Add(entry);
            }
        }

        /// <summary>1 while fresh, easing to 0 over the last <see cref="FadeSeconds"/>.</summary>
        public static float Alpha(ToastEntry entry, float now)
        {
            var left = entry.ShownAt + LifetimeSeconds - now;
            if (left <= 0f)
                return 0f;
            return left >= FadeSeconds ? 1f : left / FadeSeconds;
        }

        private static bool IsAlive(ToastEntry entry, float now) =>
            now >= entry.ShownAt && now < entry.ShownAt + LifetimeSeconds;
    }
}

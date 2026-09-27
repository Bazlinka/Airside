using System;
using System.Collections.Generic;
using System.Text;
using Airside.Domain;
using Airside.Simulation;

namespace Airside.Presentation
{
    /// <summary>
    /// ADR 0132 — funds that count up (or down) to their new value instead of jumping. Pure: the
    /// runtime passes the real funds and the time; this answers what to print.
    /// </summary>
    public sealed class FundsTicker
    {
        public const float Seconds = 0.9f;

        private long _from;
        private long _to;
        private float _startedAt;
        private bool _started;

        /// <summary>+1 while counting up, −1 while counting down, 0 when settled.</summary>
        public int Direction { get; private set; }

        public long Show(long actual, float now)
        {
            if (!_started)
            {
                _started = true;
                _from = _to = actual;
                _startedAt = now;
            }
            else if (actual != _to)
            {
                _from = Current(now);
                _to = actual;
                _startedAt = now;
            }

            var shown = Current(now);
            Direction = shown == _to ? 0 : _to > shown ? 1 : -1;
            return shown;
        }

        /// <summary>Dev Showcase: count up to the current value again from <paramref name="from"/>.</summary>
        public void Replay(long from, float now)
        {
            if (!_started)
                return;
            _from = from;
            _startedAt = now;
        }

        private long Current(float now)
        {
            var t = Seconds <= 0f ? 1f : Math.Clamp((now - _startedAt) / Seconds, 0f, 1f);
            var eased = 1f - (1f - t) * (1f - t) * (1f - t);
            return _from + (long)Math.Round((_to - _from) * (double)eased);
        }
    }

    /// <summary>
    /// ADR 0132 — split-flap text changes: when a board field's text changes, its tiles spin through
    /// letters and settle left to right, the way a real departures board flips. Pure.
    /// </summary>
    public static class FlapAnimator
    {
        public const float Seconds = 0.7f;
        private const string Glyphs = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

        /// <summary>The text to show <paramref name="elapsed"/> seconds after <paramref name="from"/> became
        /// <paramref name="to"/>: settled characters from the left, spinning glyphs for the rest.</summary>
        public static string Frame(string from, string to, float elapsed)
        {
            to ??= string.Empty;
            from ??= string.Empty;
            if (elapsed >= Seconds || from == to)
                return to;
            var length = Math.Max(from.Length, to.Length);
            var settled = (int)(length * Math.Clamp(elapsed / Seconds, 0f, 1f));
            var tick = (int)(elapsed * 30f);
            var text = new StringBuilder(length);
            for (var i = 0; i < length; i++)
            {
                if (i < settled)
                    text.Append(i < to.Length ? to[i] : ' ');
                else if (i >= to.Length && i >= from.Length)
                    text.Append(' ');
                else
                    text.Append(Glyphs[(i * 7 + tick) % Glyphs.Length]);
            }

            return text.ToString().TrimEnd();
        }
    }

    /// <summary>Remembers each board field's text and when it last changed, so it can flip (ADR 0132).</summary>
    public sealed class FlapBoardState
    {
        private readonly Dictionary<string, (string From, string To, float ChangedAt)> _fields =
            new(StringComparer.Ordinal);

        public string Display(string key, string text, float now)
        {
            text ??= string.Empty;
            if (!_fields.TryGetValue(key, out var field))
            {
                // First sight: no flip, a field that was always there should not spin.
                _fields[key] = (text, text, now - FlapAnimator.Seconds);
                return text;
            }

            if (field.To != text)
            {
                var shownNow = FlapAnimator.Frame(field.From, field.To, now - field.ChangedAt);
                field = (shownNow, text, now);
                _fields[key] = field;
                LastChangeAt = now;
            }

            return FlapAnimator.Frame(field.From, field.To, now - field.ChangedAt);
        }

        /// <summary>When any field last changed (drives the board's flap rattle); −∞ before the first.</summary>
        public float LastChangeAt { get; private set; } = float.NegativeInfinity;

        /// <summary>Dev Showcase: every remembered field flips in from blank.</summary>
        public void ReplayAll(float now)
        {
            var keys = new List<string>(_fields.Keys);
            foreach (var key in keys)
                _fields[key] = (string.Empty, _fields[key].To, now);
            LastChangeAt = now;
        }

        /// <summary>Drops fields not shown for a while so the map does not grow forever.</summary>
        public void Forget(Func<string, bool> keep)
        {
            var drop = new List<string>();
            foreach (var key in _fields.Keys)
                if (!keep(key))
                    drop.Add(key);
            foreach (var key in drop)
                _fields.Remove(key);
        }
    }

    public enum CelebrationKind
    {
        TierReached,
        ContractDone,
        Finale
    }

    /// <summary>A big moment worth stopping for: a new tier, a finished contract, the finale (ADR 0132).</summary>
    public sealed class CelebrationCard
    {
        public CelebrationCard(CelebrationKind kind, string title, string subtitle, IReadOnlyList<string> lines,
            string imagePath = null)
        {
            Kind = kind;
            Title = title ?? string.Empty;
            Subtitle = subtitle ?? string.Empty;
            Lines = lines ?? Array.Empty<string>();
            ImagePath = imagePath ?? string.Empty;
        }

        public CelebrationKind Kind { get; }
        public string Title { get; }
        public string Subtitle { get; }
        public IReadOnlyList<string> Lines { get; }
        public string ImagePath { get; }

        /// <summary>"Regional tier!" with what it opens: the aircraft you can now buy, the routes, the base.</summary>
        public static CelebrationCard ForTier(OperatingTier tier, string airlineName)
        {
            var lines = new List<string>();
            string image = null;
            var names = new List<string>();
            long cheapest = long.MaxValue;
            foreach (var offer in AircraftAcquisition.All)
            {
                if (offer.RequiredTier != tier)
                    continue;
                names.Add(ShortName(offer.Type.Name));
                cheapest = Math.Min(cheapest, offer.Price);
                image ??= FleetWorkspacePainter.Thumbnail(offer.Type);
            }

            if (names.Count > 0)
            {
                var shown = string.Join(", ", names.GetRange(0, Math.Min(3, names.Count)));
                var more = names.Count > 3 ? $" and {names.Count - 3} more" : string.Empty;
                lines.Add($"New aircraft: {shown}{more}");
                lines.Add($"The cheapest is ${cheapest:N0}. See Fleet.");
            }

            if (tier == OperatingTier.International)
                lines.Add("New routes: Auckland, Bali, Singapore and more");
            var baseLevel = tier switch
            {
                OperatingTier.Regional => PlayerBaseLevel.ExpandedRegional,
                OperatingTier.Domestic => PlayerBaseLevel.JetGate,
                OperatingTier.International => PlayerBaseLevel.International,
                _ => PlayerBaseLevel.Starter
            };
            if (baseLevel != PlayerBaseLevel.Starter)
                lines.Add($"Base upgrade: the {PlayerBase.For(baseLevel).Title}");
            return new CelebrationCard(CelebrationKind.TierReached, $"{tier} tier!",
                $"{airlineName} moves up a tier.", lines, image);
        }

        /// <summary>"Airbus A220-300" → "A220-300": the maker adds length, not meaning, in a list.</summary>
        private static string ShortName(string name)
        {
            foreach (var maker in new[] { "Airbus ", "Boeing ", "Embraer " })
                if (name.StartsWith(maker, StringComparison.Ordinal))
                    return name.Substring(maker.Length);
            return name;
        }

        public static CelebrationCard ForContract(string title, long paid, int reliabilityGain, string thumbnail)
        {
            var lines = new List<string> { $"${paid:N0} for the last flight, bonus included" };
            if (reliabilityGain > 0)
                lines.Add($"Reliability +{reliabilityGain}");
            lines.Add("A new contract is waiting in Contracts.");
            return new CelebrationCard(CelebrationKind.ContractDone, "Contract done!", title, lines, thumbnail);
        }

        public static CelebrationCard ForFinale(string airlineName, int fleet, int destinations, long hours)
        {
            return new CelebrationCard(CelebrationKind.Finale, "Established airline",
                $"{airlineName} has made it.",
                new[]
                {
                    $"{fleet} aircraft · {destinations} destinations · {hours} h of flying",
                    "Every career goal is done.",
                    "Prestige challenges are open on the Airline page.",
                    "The airport is yours. Keep flying."
                });
        }
    }

    /// <summary>The celebration card: a centred glass panel over the airport (ADR 0132).</summary>
    public static class CelebrationPainter
    {
        public const string Close = "celebration:close";

        public static HudBox Panel(float width, float height) =>
            HudShell.CentredPanel(new HudBox(HudShell.Margin, HudShell.Margin, width - HudShell.Margin * 2f,
                height - HudShell.Margin * 2f), 560f, 420f);

        public static void Paint(HudDrawList into, HudBox panel, CelebrationCard card, float shownFor)
        {
            if (into == null || card == null || panel.IsEmpty)
                return;
            // Fade and rise in over a quarter second.
            var t = Math.Clamp(shownFor / 0.25f, 0f, 1f);
            var box = panel.Offset(0f, (1f - t) * 16f);
            into.Surface(box, 0.96f * t);
            var accent = card.Kind == CelebrationKind.ContractDone ? HudTone.Positive : HudTone.Caution;
            into.Fill(new HudBox(box.X, box.Y, box.Width, 4f), accent, t);

            var y = box.Y + 26f;
            into.Caption(new HudBox(box.X, y, box.Width, 12f),
                card.Kind == CelebrationKind.ContractDone ? "CONTRACT" : "CAREER", accent, HudAlign.Center, 10f);
            y += 20f;
            into.Text(new HudBox(box.X + 24f, y, box.Width - 48f, 36f), card.Title, 28f, HudTone.Default,
                HudTextStyle.Bold, HudAlign.Center, alpha: t);
            y += 40f;
            into.Text(new HudBox(box.X + 24f, y, box.Width - 48f, 20f), card.Subtitle, 14f, HudTone.Muted,
                HudTextStyle.Regular, HudAlign.Center, alpha: t);
            y += 30f;

            if (card.ImagePath.Length > 0)
            {
                var stage = new HudBox(box.X + (box.Width - 240f) * 0.5f, y, 240f, 110f);
                into.Image(stage, card.ImagePath, t);
                y += 118f;
            }

            foreach (var line in card.Lines)
            {
                if (y + 20f > box.Bottom - 64f)
                    break;
                into.Text(new HudBox(box.X + 32f, y, box.Width - 64f, 18f), line, 13f, HudTone.Default,
                    HudTextStyle.Regular, HudAlign.Center, alpha: t);
                y += 22f;
            }

            into.Button(new HudBox(box.X + (box.Width - 180f) * 0.5f, box.Bottom - 52f, 180f, 34f), "CONTINUE", Close,
                HudButtonStyle.Primary);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Airside.Simulation
{
    /// <summary>What held a player departure past its booked time (ADR 0128).</summary>
    public enum DelayCause
    {
        /// <summary>Unattributed: waiting for the ground-control grid, or time lost across a reload.</summary>
        Other,
        /// <summary>The turnaround ran past the booked time (booked too tight for the base's prep).</summary>
        Turnaround,
        /// <summary>Two aircraft already taxiing out from this apron.</summary>
        ApronBusy,
        /// <summary>The gate lead-in was in use.</summary>
        LeadIn,
        /// <summary>The route to the runway was not clear of other ground traffic.</summary>
        Taxiway,
        /// <summary>The route crosses a runway that was busy (ADR 0126).</summary>
        RunwayCrossing,
        /// <summary>A helicopter held by weather: a storm, fog for a civil flight, or hard wind (ADR 0207).</summary>
        Weather,
        /// <summary>A helicopter waiting for the pad while another lifted off or landed (ADR 0207).</summary>
        Pad
    }

    public static class DelayCauses
    {
        /// <summary>Lower-case words for a cause, to follow "3 min" or "mostly".</summary>
        public static string Label(DelayCause cause) => cause switch
        {
            DelayCause.Turnaround => "turnaround",
            DelayCause.ApronBusy => "apron traffic",
            DelayCause.LeadIn => "a blocked lead-in",
            DelayCause.Taxiway => "taxiway traffic",
            DelayCause.RunwayCrossing => "runway crossings",
            DelayCause.Weather => "weather",
            DelayCause.Pad => "pad traffic",
            _ => "ground control"
        };
    }

    public readonly struct DelayPart
    {
        public DelayPart(DelayCause cause, int seconds)
        {
            Cause = cause;
            Seconds = seconds;
        }

        public DelayCause Cause { get; }
        public int Seconds { get; }
    }

    /// <summary>
    /// Why a pushback was late, largest cause first. The parts always add up to
    /// <see cref="LatenessSeconds"/>. Empty when on time to the second.
    /// </summary>
    public readonly struct DelayBreakdown
    {
        private readonly DelayPart[] _parts;

        public DelayBreakdown(int latenessSeconds, IReadOnlyList<DelayPart> parts)
        {
            LatenessSeconds = Math.Max(0, latenessSeconds);
            var list = new List<DelayPart>();
            if (parts != null)
                foreach (var part in parts)
                    if (part.Seconds > 0)
                        list.Add(part);
            // Largest first; ties in enum order so the result never depends on input order.
            list.Sort((a, b) => a.Seconds != b.Seconds ? b.Seconds.CompareTo(a.Seconds) : a.Cause.CompareTo(b.Cause));
            _parts = list.ToArray();
        }

        public int LatenessSeconds { get; }
        public IReadOnlyList<DelayPart> Parts => _parts ?? Array.Empty<DelayPart>();
        public bool IsLate => LatenessSeconds > FlightEconomics.OnTimeGraceSeconds;

        /// <summary>The biggest named cause (not <see cref="DelayCause.Other"/>), when there is one.</summary>
        public DelayCause? TopCause
        {
            get
            {
                foreach (var part in Parts)
                    if (part.Cause != DelayCause.Other)
                        return part.Cause;
                return null;
            }
        }

        /// <summary>"RunwayCrossing:180;Turnaround:60" — the save form (ADR 0128).</summary>
        public string Serialize()
        {
            var text = new StringBuilder();
            foreach (var part in Parts)
            {
                if (text.Length > 0)
                    text.Append(';');
                text.Append(part.Cause).Append(':').Append(part.Seconds.ToString(CultureInfo.InvariantCulture));
            }

            return text.ToString();
        }

        /// <summary>Reads <see cref="Serialize"/>'s form. Unknown causes and bad numbers count as Other; the
        /// parts are then made to add up to <paramref name="latenessSeconds"/>.</summary>
        public static DelayBreakdown Parse(int latenessSeconds, string text)
        {
            var totals = new Dictionary<DelayCause, int>();
            if (!string.IsNullOrEmpty(text))
            {
                foreach (var item in text.Split(';'))
                {
                    var colon = item.IndexOf(':');
                    if (colon <= 0
                        || !int.TryParse(item.Substring(colon + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds)
                        || seconds <= 0)
                        continue;
                    if (!Enum.TryParse(item.Substring(0, colon), out DelayCause cause) || !Enum.IsDefined(typeof(DelayCause), cause))
                        cause = DelayCause.Other;
                    totals.TryGetValue(cause, out var sum);
                    totals[cause] = sum + seconds;
                }
            }

            return Balanced(latenessSeconds, totals);
        }

        internal static DelayBreakdown Balanced(int latenessSeconds, Dictionary<DelayCause, int> totals)
        {
            latenessSeconds = Math.Max(0, latenessSeconds);
            // Trim named causes that overshoot (largest cut last), then give any remainder to Other.
            var parts = new List<DelayPart>();
            var left = latenessSeconds;
            foreach (DelayCause cause in Enum.GetValues(typeof(DelayCause)))
            {
                if (cause == DelayCause.Other || !totals.TryGetValue(cause, out var seconds))
                    continue;
                var take = Math.Min(Math.Max(0, seconds), left);
                if (take <= 0)
                    continue;
                parts.Add(new DelayPart(cause, take));
                left -= take;
            }

            if (left > 0)
                parts.Add(new DelayPart(DelayCause.Other, left));
            return new DelayBreakdown(latenessSeconds, parts);
        }
    }

    /// <summary>
    /// One player departure's wait, sampled on the ground-control grid while it is ready at the stand
    /// (ADR 0128). The interval up to each sample goes to what was blocking at the sample before it, so
    /// the answer is the same however the clock is stepped. Not saved: after a reload the gap counts as
    /// <see cref="DelayCause.Other"/>.
    /// </summary>
    internal sealed class DelayLedger
    {
        private readonly Dictionary<DelayCause, int> _seconds = new();

        public DelayLedger(long departAtSeconds, long startSeconds, DelayCause cause)
        {
            DepartAtSeconds = departAtSeconds;
            StartSeconds = startSeconds;
            LastSampleSeconds = startSeconds;
            LastCause = cause;
        }

        /// <summary>The booked time this ledger belongs to; a rebooking starts a new one.</summary>
        public long DepartAtSeconds { get; }
        public long StartSeconds { get; }
        public long LastSampleSeconds { get; private set; }
        public DelayCause LastCause { get; private set; }

        public void Sample(long atSeconds, DelayCause blocking)
        {
            if (atSeconds < LastSampleSeconds)
                return;
            Add(LastCause, atSeconds - LastSampleSeconds);
            LastSampleSeconds = atSeconds;
            LastCause = blocking;
        }

        /// <summary>
        /// The whole story at pushback: turnaround overrun, the sampled wait, and anything before the
        /// ledger started (a reload) as Other.
        /// </summary>
        public static DelayBreakdown Close(DelayLedger ledger, long departAtSeconds, long readyAtSeconds, long pushbackSeconds)
        {
            var totals = new Dictionary<DelayCause, int>();
            var turnaround = Math.Min(readyAtSeconds, pushbackSeconds) - departAtSeconds;
            if (turnaround > 0)
                totals[DelayCause.Turnaround] = (int)turnaround;
            if (ledger != null && ledger.DepartAtSeconds == departAtSeconds && pushbackSeconds >= ledger.LastSampleSeconds)
            {
                ledger.Add(ledger.LastCause, pushbackSeconds - ledger.LastSampleSeconds);
                foreach (var pair in ledger._seconds)
                {
                    totals.TryGetValue(pair.Key, out var sum);
                    totals[pair.Key] = sum + pair.Value;
                }
            }

            return DelayBreakdown.Balanced((int)(pushbackSeconds - departAtSeconds), totals);
        }

        private void Add(DelayCause cause, long seconds)
        {
            if (seconds <= 0)
                return;
            _seconds.TryGetValue(cause, out var sum);
            _seconds[cause] = sum + (int)seconds;
        }
    }
}

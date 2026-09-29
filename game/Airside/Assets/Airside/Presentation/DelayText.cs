using System;
using System.Collections.Generic;
using System.Text;
using Airside.Simulation;

namespace Airside.Presentation
{
    /// <summary>
    /// ADR 0128 — plain words for why a pushback was late: "4 min late: 3 min runway crossings, 1 min
    /// turnaround", a tone for the toast, and one tip for the biggest cause. Presentation only.
    /// </summary>
    public static class DelayText
    {
        /// <summary>"on time", or "4 min late: 3 min runway crossings, 1 min turnaround".</summary>
        public static string Summary(DelayBreakdown delay)
        {
            if (!delay.IsLate)
                return "on time";
            var text = new StringBuilder($"{Minutes(delay.LatenessSeconds)} late");
            var named = new List<DelayPart>();
            foreach (var part in delay.Parts)
                if (part.Cause != DelayCause.Other && part.Seconds >= 30)
                    named.Add(part);
            if (named.Count == 0)
                return text.ToString();
            text.Append(": ");
            for (var i = 0; i < named.Count && i < 2; i++)
            {
                if (i > 0)
                    text.Append(", ");
                text.Append(Minutes(named[i].Seconds)).Append(' ').Append(DelayCauses.Label(named[i].Cause));
            }

            return text.ToString();
        }

        /// <summary>The settlement toast line (ADR 0053 + 0128).</summary>
        public static string SettlementToast(FlightSettlement settlement, int onTimeStreak)
        {
            // "VH-PAA earned $900 · contract flight 2 · on time, 7 in a row." with any tip, then
            // "Contract done!" last so the good news is not buried mid-line.
            var text = new StringBuilder($"{settlement.SettlementId.Registration} earned ${settlement.Payment:N0}");
            if (!settlement.ContractFulfilled && !string.IsNullOrEmpty(settlement.ContractDefinitionId))
                text.Append($" · contract flight {settlement.RotationsCompleted}");
            var tip = string.Empty;
            if (settlement.Delay is { } delay)
            {
                if (!delay.IsLate)
                    text.Append(onTimeStreak > 1 ? $" · on time, {onTimeStreak} in a row" : " · on time");
                else
                {
                    text.Append(" · ").Append(Summary(delay));
                    tip = Tip(delay);
                }
            }

            text.Append('.');
            if (tip.Length > 0)
                text.Append(' ').Append(tip);
            if (settlement.ContractFulfilled)
                text.Append(" Contract done!");
            return text.ToString();
        }

        public static HudTone Tone(FlightSettlement settlement)
        {
            if (settlement.Delay is not { IsLate: true } delay)
                return settlement.ContractFulfilled ? HudTone.Positive : HudTone.Accent;
            return delay.LatenessSeconds > FlightEconomics.HardLateSeconds ? HudTone.Negative : HudTone.Caution;
        }

        /// <summary>One short hint for the biggest named cause, or empty.</summary>
        public static string Tip(DelayBreakdown delay) => delay.TopCause switch
        {
            DelayCause.Turnaround => "Book further ahead so the turnaround finishes in time.",
            DelayCause.ApronBusy => "Stagger departures from the same apron.",
            DelayCause.LeadIn => "Another aircraft on the lead-in is blocking your push.",
            DelayCause.Taxiway => "Arrivals taxiing in are holding your push.",
            DelayCause.RunwayCrossing => "A busy runway holds aircraft at the crossing.",
            _ => string.Empty
        };

        /// <summary>"1 min", "12 min", "1 h 5 min" — rounded, never "0 min".</summary>
        public static string Minutes(int seconds)
        {
            var minutes = Math.Max(1, (int)Math.Round(seconds / 60.0));
            return minutes < 60 ? $"{minutes} min" : $"{minutes / 60} h {minutes % 60} min";
        }
    }
}

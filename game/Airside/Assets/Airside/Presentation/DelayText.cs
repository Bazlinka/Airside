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
            var reg = settlement.SettlementId.Registration;
            var earned = settlement.ContractFulfilled
                ? $"{reg} earned ${settlement.Payment:N0}. Contract done!"
                : !string.IsNullOrEmpty(settlement.ContractDefinitionId)
                    ? $"{reg} earned ${settlement.Payment:N0} · contract flight {settlement.RotationsCompleted}"
                    : $"{reg} earned ${settlement.Payment:N0}";
            if (!settlement.Delay.HasValue)
                return settlement.ContractFulfilled ? earned : earned + ".";
            var delay = settlement.Delay.Value;
            if (!delay.IsLate)
                return onTimeStreak > 1 ? $"{earned} · on time, {onTimeStreak} in a row." : $"{earned} · on time.";
            var tip = Tip(delay);
            return tip.Length > 0 ? $"{earned} · {Summary(delay)}. {tip}" : $"{earned} · {Summary(delay)}.";
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
            DelayCause.LeadIn => "A neighbour on the lead-in blocks your push.",
            DelayCause.Taxiway => "Arrivals taxiing past you hold your push.",
            DelayCause.RunwayCrossing => "Busy runways hold aircraft at the crossing.",
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

using System;
using Airside.Domain;

namespace Airside.Simulation
{
    /// <summary>
    /// How busy Adelaide is at a local hour: morning and evening banks, a smaller
    /// midday peak, and a quiet night. Used by the published day plan and sky traffic.
    /// </summary>
    public static class AdelaideHourProfile
    {
        public static float Density(int hour) => hour switch
        {
            6 or 7 or 8 => 1.00f,
            9 => 0.70f,
            10 => 0.40f,
            11 or 12 => 0.80f,
            13 => 0.50f,
            14 or 15 => 0.35f,
            16 or 17 or 18 => 1.00f,
            19 => 0.60f,
            20 => 0.30f,
            21 => 0.15f,
            _ => 0.06f
        };

        public static float DensityAt(AirlineClock clock, SimulationTime now)
        {
            clock ??= AirlineClock.Default;
            return Density(clock.LocalAt(now).Hour);
        }

        /// <summary>
        /// Minutes past midnight for <paramref name="count"/> movements, clustered on
        /// the busy banks instead of being spaced evenly through the day.
        /// </summary>
        public static int[] BankMinutes(int count, int firstHour, int lastHour)
        {
            if (count <= 0)
                return Array.Empty<int>();

            var weights = new float[(lastHour - firstHour + 1) * 60];
            var total = 0f;
            for (var i = 0; i < weights.Length; i++)
            {
                var hour = firstHour + i / 60;
                weights[i] = Density(hour);
                total += weights[i];
            }

            var result = new int[count];
            for (var slot = 0; slot < count; slot++)
            {
                var target = (slot + 0.5f) / count * total;
                var walk = 0f;
                var minute = 0;
                for (var i = 0; i < weights.Length; i++)
                {
                    walk += weights[i];
                    if (walk < target)
                        continue;
                    minute = i;
                    break;
                }

                result[slot] = firstHour * 60 + minute;
            }

            return result;
        }

        /// <summary>
        /// Next time an AI departure should go if the ready time falls in a quiet hole.
        /// Peak hours stay as-is so a scheduled 07:40 does not jump.
        /// </summary>
        public static DateTime NextUsefulLocal(DateTime local, int firstHour, int lastHour)
        {
            if (local.Hour < firstHour)
                return local.Date.AddHours(firstHour);
            if (local.Hour > lastHour)
                return local.Date.AddDays(1).AddHours(firstHour);
            if (Density(local.Hour) >= 0.45f)
                return local;

            for (var hour = local.Hour + 1; hour <= lastHour; hour++)
            {
                if (Density(hour) >= 0.45f)
                    return local.Date.AddHours(hour);
            }

            return local.Date.AddDays(1).AddHours(firstHour);
        }
    }
}

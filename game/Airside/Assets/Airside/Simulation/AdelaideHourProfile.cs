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
            // First wave (ADR 0110): night-stopped aircraft push from 05:00 onward.
            5 => 0.70f,
            6 or 7 or 8 => 1.00f,
            9 => 0.70f,
            10 => 0.40f,
            11 or 12 => 0.80f,
            13 => 0.50f,
            14 or 15 => 0.35f,
            16 or 17 or 18 => 1.00f,
            19 => 0.60f,
            20 => 0.30f,
            21 => 0.40f,
            22 => 0.28f,
            23 => 0.06f,
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
        /// Published departure marks (minutes past midnight) that pull nearby ready times
        /// together, so several operators share one scheduled time the way a real ADL bank
        /// does (06:00 Qantas, Virgin and Jetstar all on the board together). The tower
        /// still sequences the actual takeoffs a minute or two apart. ADR 0110.
        /// </summary>
        public static readonly int[] BankAnchorMinutes =
        {
            5 * 60 + 30, 5 * 60 + 45, 6 * 60, 6 * 60 + 15, 6 * 60 + 30, 7 * 60, 7 * 60 + 30, 8 * 60,
            8 * 60 + 30, 9 * 60, 11 * 60 + 30, 12 * 60, 12 * 60 + 30, 16 * 60 + 30, 17 * 60,
            17 * 60 + 30, 18 * 60, 18 * 60 + 30, 19 * 60, 20 * 60, 21 * 60, 21 * 60 + 30, 22 * 60,
            22 * 60 + 30, 23 * 60
        };

        /// <summary>A ready time this close before an anchor is published on the anchor.</summary>
        public const int AnchorPullMinutes = 15;

        /// <summary>
        /// First-wave marks for aircraft that night-stopped on the apron: 05:00–06:30,
        /// weighted toward 06:00 so the dawn board clusters rather than steps.
        /// </summary>
        public static readonly int[] FirstWaveMinutes =
        {
            5 * 60, 5 * 60 + 30, 5 * 60 + 45, 6 * 60, 6 * 60, 6 * 60 + 15, 6 * 60 + 30
        };

        /// <summary>The first-wave departure for a night-stopped aircraft, picked by a stable key.</summary>
        public static DateTime FirstWaveLocal(DateTime day, int key)
        {
            var index = (key & int.MaxValue) % FirstWaveMinutes.Length;
            return day.Date.AddMinutes(FirstWaveMinutes[index]);
        }

        /// <summary>
        /// Round a ready time up onto a published mark: the next <see cref="BankAnchorMinutes"/>
        /// within <see cref="AnchorPullMinutes"/>, otherwise the next 5-minute mark. The
        /// afternoon hole jumps to the next bank (<see cref="NextUsefulLocal"/>); the evening
        /// wind-down does not roll to tomorrow while the day is still open. A ready time
        /// after the last movement (23:00) goes to tomorrow's first wave
        /// (<paramref name="spreadKey"/> spreads those across 05:00–06:30). Never goes earlier.
        /// </summary>
        public static DateTime SnapToBankLocal(DateTime local, int firstHour, int lastHour, int spreadKey = 0)
        {
            var lastMinute = (lastHour + 1) * 60;
            if (local.Hour < firstHour)
                return Later(local, FirstWaveLocal(local.Date, spreadKey));
            if (local.TimeOfDay > TimeSpan.FromMinutes(lastMinute))
                return FirstWaveLocal(local.Date.AddDays(1), spreadKey);

            if (Density(local.Hour) < 0.45f)
            {
                var useful = NextUsefulLocal(local, firstHour, lastHour);
                if (useful.Date == local.Date && useful > local)
                    return useful;
            }

            var totalMin = local.Hour * 60 + local.Minute + (local.Second > 0 || local.Millisecond > 0 ? 1 : 0);
            if (local.Second == 0 && local.Millisecond == 0 && IsAnchor(totalMin))
                return local;
            foreach (var anchor in BankAnchorMinutes)
            {
                if (anchor < totalMin || anchor > lastMinute)
                    continue;
                if (anchor - totalMin <= AnchorPullMinutes)
                    return local.Date.AddMinutes(anchor);
                break;
            }

            var snapped = (totalMin + 4) / 5 * 5;
            if (snapped > lastMinute)
                return FirstWaveLocal(local.Date.AddDays(1), spreadKey);
            return local.Date.AddMinutes(snapped);
        }

        private static bool IsAnchor(int minute)
        {
            foreach (var anchor in BankAnchorMinutes)
                if (anchor == minute)
                    return true;
            return false;
        }

        private static DateTime Later(DateTime a, DateTime b) => a > b ? a : b;

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

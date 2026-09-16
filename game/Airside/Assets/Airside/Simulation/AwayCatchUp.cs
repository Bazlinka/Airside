using System;
using System.Collections.Generic;
using Airside.Domain;

namespace Airside.Simulation
{
    /// <summary>
    /// The airport keeps running while the game is closed (PROJECT_PLAN core rules;
    /// ADR 0045). On Continue the restored game is advanced by the real time away —
    /// through the same event-driven <see cref="AirlineOperations.Update"/> as live
    /// play, so the result is the one live play would have reached.
    /// </summary>
    public static class AwayCatchUp
    {
        /// <summary>A week. Longer absences resume a week on rather than simulating months.</summary>
        public const long MaxSeconds = 7 * 24 * 3600;

        /// <summary>Shorter gaps (a quick relaunch) resume without a summary.</summary>
        public const long MinimumSeconds = 60;

        /// <summary>
        /// Seconds to catch up: real time since the save, capped. Zero with no timestamp
        /// (version 1 saves) or when the device clock reads earlier than the save.
        /// </summary>
        public static long SecondsAway(AirlineSaveData data, DateTime utcNow)
        {
            if (data == null || data.SavedAtUtcTicks <= 0)
                return 0;

            var away = (utcNow.ToUniversalTime().Ticks - data.SavedAtUtcTicks) / TimeSpan.TicksPerSecond;
            if (away < MinimumSeconds)
                return 0;
            return Math.Min(away, MaxSeconds);
        }

        /// <summary>
        /// Where a restored game should be right now on its live clock. Past the one-week
        /// cap, or if the device clock reads earlier than the save, the clock is re-aligned
        /// so the game still reads real time from here on without simulating the gap.
        /// </summary>
        public static SimulationTime LiveTarget(AirlineOperations restored, DateTime utcNow)
        {
            if (restored == null) throw new ArgumentNullException(nameof(restored));

            var saved = restored.ProcessedTo;
            var live = restored.Clock.At(utcNow);
            var target = live;
            if (live.CompareTo(saved) < 0)
                target = saved;
            else if (live.ElapsedSeconds - saved.ElapsedSeconds > MaxSeconds)
                target = saved.Advance(MaxSeconds);

            if (!target.Equals(live))
                restored.Clock = AirlineClock.Aligned(target, utcNow);
            return target;
        }
    }

    /// <summary>What changed while the player was away, in plain sentences.</summary>
    public sealed class AwaySummary
    {
        private AwaySummary(long awaySeconds, IReadOnlyList<string> lines)
        {
            AwaySeconds = awaySeconds;
            Lines = lines;
        }

        public long AwaySeconds { get; }
        public IReadOnlyList<string> Lines { get; }

        public string Title => $"You were away {AirlineClock.DurationText(AwaySeconds)}";

        public static AwaySummary Build(AirlineSaveData before, AirlineOperations after, long awaySeconds)
        {
            if (before == null) throw new ArgumentNullException(nameof(before));
            if (after == null) throw new ArgumentNullException(nameof(after));

            var tripsBefore = new Dictionary<string, int>(StringComparer.Ordinal);
            // Restore already treats a missing fleet list as empty; a save written by an
            // older build (or hand-edited) has no Fleet array at all and threw here.
            foreach (var record in before.Fleet ?? new List<AircraftRecord>())
                tripsBefore[record.Registration] = record.CompletedTrips;

            var lines = new List<string>();
            foreach (var aircraft in after.FleetOf(after.PlayerAirline))
            {
                tripsBefore.TryGetValue(aircraft.Registration, out var was);
                var flown = aircraft.CompletedTrips - was;
                var status = Status(aircraft, after.Clock);
                lines.Add(flown > 0
                    ? $"{aircraft.Registration} completed {Plural(flown, "trip")} and {status}."
                    : $"{aircraft.Registration} {status}.");
            }

            foreach (var airline in after.Airlines)
            {
                if (airline.IsPlayer)
                    continue;
                var flown = 0;
                foreach (var aircraft in after.FleetOf(airline))
                {
                    tripsBefore.TryGetValue(aircraft.Registration, out var was);
                    flown += Math.Max(0, aircraft.CompletedTrips - was);
                }

                lines.Add(flown > 0 ? $"{airline.Name} flew {Plural(flown, "trip")}." : $"{airline.Name} flew no trips.");
            }

            if (awaySeconds >= AwayCatchUp.MaxSeconds)
                lines.Add("The airport caught up one week; longer absences are not simulated.");

            return new AwaySummary(awaySeconds, lines);
        }

        private static string Status(FleetAircraft aircraft, AirlineClock clock)
        {
            var dest = aircraft.CurrentDestination?.Name;
            return aircraft.State switch
            {
                FleetState.AtStand when aircraft.Scheduled.HasValue =>
                    $"is on {AdelaideGround.StandLabel(aircraft.Stand)}, departing {clock.TimeText(aircraft.Scheduled.Value.DepartAt)} for {aircraft.Scheduled.Value.Destination.Name}",
                FleetState.AtStand => $"is parked on {AdelaideGround.StandLabel(aircraft.Stand)} with no flight planned",
                FleetState.TaxiOut or FleetState.HoldingShort or FleetState.TakingOff => $"is departing for {dest}",
                FleetState.Outbound => $"is flying to {dest}",
                FleetState.AtDestination => $"is on the ground at {dest}",
                FleetState.Inbound => $"is flying home from {dest}",
                FleetState.HoldingForLanding or FleetState.Landing => "is landing at Adelaide",
                FleetState.AwaitingStand => "has landed and is waiting for you to choose a stand",
                FleetState.TaxiIn => $"is taxiing to {AdelaideGround.StandLabel(aircraft.Stand)}",
                _ => $"is {aircraft.State}"
            };
        }

        private static string Plural(int count, string noun) => count == 1 ? $"1 {noun}" : $"{count} {noun}s";
    }
}

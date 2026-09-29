using System;
using System.Collections.Generic;
using Airside.Domain;

namespace Airside.Simulation
{
    /// <summary>
    /// Hangar space for checks (ADR 0188). A hangar holds only so many aircraft at once (as many as fit side by side
    /// across its door, at most <see cref="HangarTow.MaxBerths"/>), so a check has to find a free berth in a hangar that fits
    /// its type; when every such hangar is full the check waits until one frees.
    ///
    /// Nothing is stored: berths are derived from the fleet's check intervals (start = <c>CheckUntil</c> minus the check's
    /// length). Aircraft are placed in order of check start, each taking the first free berth of its best hangar, so a
    /// later check never moves an earlier one and saves are unchanged. Aircraft whose type no hangar fits are not limited.
    /// </summary>
    public static class HangarBays
    {
        public readonly struct Berth
        {
            public Berth(bool hasHangar, int hangar, int slot, bool full, long freeAtSeconds)
            {
                HasHangar = hasHangar;
                Hangar = hangar;
                Slot = slot;
                Full = full;
                FreeAtSeconds = freeAtSeconds;
            }

            /// <summary>False when no hangar fits the type (no limit, no tow).</summary>
            public bool HasHangar { get; }
            public int Hangar { get; }
            public int Slot { get; }

            /// <summary>Every fitting hangar is full for the whole of the asked interval's start.</summary>
            public bool Full { get; }

            /// <summary>When the first berth frees, if <see cref="Full"/>.</summary>
            public long FreeAtSeconds { get; }
        }

        private struct Entry
        {
            public FleetAircraft Aircraft;
            public long Start, End;
            public bool IsSubject;
        }

        private struct Occupied
        {
            public string HangarId;
            public int Slot;
            public long End;
        }

        /// <summary>
        /// The berth for <paramref name="subject"/> in a check over [<paramref name="start"/>, <paramref name="end"/>) seconds,
        /// with the fleet's other checks already placed. <paramref name="subject"/> need not have <c>CheckUntil</c> set yet.
        /// </summary>
        public static Berth Assign(IEnumerable<FleetAircraft> fleet, FleetAircraft subject, long start, long end, PlayerBaseLevel level)
        {
            var entries = new List<Entry>();
            foreach (var aircraft in fleet)
            {
                if (aircraft == subject || aircraft == null || !aircraft.Airline.IsPlayer || !aircraft.CheckUntil.HasValue)
                    continue;
                var until = aircraft.CheckUntil.Value.ElapsedSeconds;
                entries.Add(new Entry { Aircraft = aircraft, End = until, Start = until - Maintenance.CheckSeconds(aircraft.Type, level) });
            }

            entries.Add(new Entry { Aircraft = subject, Start = start, End = end, IsSubject = true });
            entries.Sort((a, b) =>
            {
                var c = a.Start.CompareTo(b.Start);
                if (c != 0)
                    return c;
                // A check about to start goes after any that began at the same instant; one already running keeps its place.
                if (a.IsSubject != b.IsSubject && !subject.CheckUntil.HasValue)
                    return a.IsSubject ? 1 : -1;
                return string.CompareOrdinal(a.Aircraft.Registration, b.Aircraft.Registration);
            });

            var occupied = new List<Occupied>();
            foreach (var entry in entries)
            {
                occupied.RemoveAll(o => o.End <= entry.Start);
                var options = HangarTow.Options(entry.Aircraft.Type, entry.Aircraft.Stand);
                if (options.Count == 0)
                {
                    if (entry.IsSubject)
                        return new Berth(false, 0, 0, false, 0);
                    continue;
                }

                var placed = false;
                for (var h = 0; h < options.Count && !placed; h++)
                {
                    for (var slot = 0; slot < options[h].Capacity; slot++)
                    {
                        if (occupied.Exists(o => o.HangarId == options[h].HangarId && o.Slot == slot))
                            continue;
                        occupied.Add(new Occupied { HangarId = options[h].HangarId, Slot = slot, End = entry.End });
                        if (entry.IsSubject)
                            return new Berth(true, h, slot, false, 0);
                        placed = true;
                        break;
                    }
                }

                if (placed)
                    continue;
                if (entry.IsSubject)
                {
                    var freeAt = long.MaxValue;
                    foreach (var o in occupied)
                        foreach (var option in options)
                            if (o.HangarId == option.HangarId)
                                freeAt = Math.Min(freeAt, o.End);
                    return new Berth(true, 0, 0, true, freeAt == long.MaxValue ? entry.Start : freeAt);
                }

                // A check that predates the limit (or a smaller base level): keep it in its best hangar's first berth.
                occupied.Add(new Occupied { HangarId = options[0].HangarId, Slot = 0, End = entry.End });
            }

            return new Berth(false, 0, 0, false, 0);
        }

        /// <summary>The berth of an aircraft already in its check.</summary>
        public static Berth Of(IEnumerable<FleetAircraft> fleet, FleetAircraft aircraft, PlayerBaseLevel level)
        {
            if (aircraft == null || !aircraft.CheckUntil.HasValue)
                return new Berth(false, 0, 0, false, 0);
            var end = aircraft.CheckUntil.Value.ElapsedSeconds;
            return Assign(fleet, aircraft, end - Maintenance.CheckSeconds(aircraft.Type, level), end, level);
        }
    }
}

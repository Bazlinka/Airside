using System;
using System.Collections.Generic;
using Airside.Domain;

namespace Airside.Simulation
{
    /// <summary>A real aircraft remembered at the stand where it went quiet.</summary>
    public readonly struct LiveParkedAircraft
    {
        public LiveParkedAircraft(string hex, string label, string typeCode, StableId stand, double since)
        {
            Hex = hex;
            Label = label;
            TypeCode = typeCode;
            Stand = stand;
            Since = since;
        }

        public string Hex { get; }
        public string Label { get; }
        public string TypeCode { get; }
        public StableId Stand { get; }

        /// <summary>Real-time seconds when it was last heard, stopped at the stand.</summary>
        public double Since { get; }
    }

    /// <summary>
    /// Airliners switch their transponders off at the gate, so the live feed loses them the moment
    /// they park and the apron looked empty (ADR 0083). Remember a real aircraft that stopped at a
    /// stand and then went quiet, and keep drawing it there — until it transmits again (pushback),
    /// another real aircraft takes that stand, or <see cref="KeepSeconds"/> passes.
    /// Session memory only: never saved, never read by the simulation.
    /// </summary>
    public sealed class LiveParkedMemory
    {
        /// <summary>A long turnaround or an overnight stop; after this, assume it left unseen.</summary>
        public const double KeepSeconds = 4 * 3600;

        /// <summary>How close to a stand's stop point a stopped aircraft must be to count as parked there.</summary>
        public const double StandCaptureMetres = 35.0;

        private readonly Dictionary<string, (LiveAircraft Aircraft, double X, double Z, double HeardAt)> _lastStopped = new();
        private readonly Dictionary<string, LiveParkedAircraft> _parked = new();
        private readonly List<string> _drop = new();
        private readonly List<string> _standTaken = new();

        public IReadOnlyCollection<LiveParkedAircraft> Parked => _parked.Values;

        /// <summary>
        /// Feed the latest report at <paramref name="now"/> (real-time seconds). <paramref name="stands"/>
        /// are the stand stop points in world metres.
        /// </summary>
        public void Update(IReadOnlyList<LiveAircraft> feed, double now, IReadOnlyList<(StableId Id, double X, double Z)> stands)
        {
            var heard = new HashSet<string>();
            if (feed != null)
            {
                foreach (var aircraft in feed)
                {
                    // The feed keeps a silent aircraft listed for a while with an ever-older
                    // position; that is not it transmitting again.
                    if (aircraft.PositionAgeSeconds > LiveTraffic.StaleSeconds)
                        continue;
                    heard.Add(aircraft.Hex);
                    // Transmitting again: it is the feed's to draw, not ours.
                    _parked.Remove(aircraft.Hex);
                    if (aircraft.OnGround && aircraft.GroundSpeedKnots < 2.0
                                          && LiveTraffic.ModelFor(aircraft.TypeCode) != null)
                    {
                        YpadFrame.ToWorld(aircraft.Latitude, aircraft.Longitude, out var x, out var z);
                        _lastStopped[aircraft.Hex] = (aircraft, x, z, now - aircraft.PositionAgeSeconds);
                    }
                    else
                    {
                        _lastStopped.Remove(aircraft.Hex);
                    }
                }
            }

            // Stopped, then gone quiet: park it on the stand it stopped at.
            _drop.Clear();
            foreach (var pair in _lastStopped)
            {
                var last = pair.Value;
                if (heard.Contains(pair.Key))
                    continue;
                _drop.Add(pair.Key);
                if (!TryNearestStand(last.X, last.Z, stands, out var stand))
                    continue;
                ClearStand(stand);
                _parked[pair.Key] = new LiveParkedAircraft(pair.Key, last.Aircraft.Label, last.Aircraft.TypeCode, stand,
                    last.HeardAt);
            }

            foreach (var hex in _drop)
                _lastStopped.Remove(hex);

            _drop.Clear();
            foreach (var pair in _parked)
                if (now - pair.Value.Since > KeepSeconds)
                    _drop.Add(pair.Key);
            foreach (var hex in _drop)
                _parked.Remove(hex);
        }

        private void ClearStand(StableId stand)
        {
            _standTaken.Clear();
            foreach (var pair in _parked)
                if (pair.Value.Stand.Equals(stand))
                    _standTaken.Add(pair.Key);
            foreach (var hex in _standTaken)
                _parked.Remove(hex);
        }

        private static bool TryNearestStand(double x, double z, IReadOnlyList<(StableId Id, double X, double Z)> stands,
            out StableId nearest)
        {
            nearest = default;
            if (stands == null)
                return false;
            var best = StandCaptureMetres * StandCaptureMetres;
            var found = false;
            foreach (var stand in stands)
            {
                var dx = stand.X - x;
                var dz = stand.Z - z;
                var d = dx * dx + dz * dz;
                if (d > best)
                    continue;
                best = d;
                nearest = stand.Id;
                found = true;
            }

            return found;
        }
    }
}

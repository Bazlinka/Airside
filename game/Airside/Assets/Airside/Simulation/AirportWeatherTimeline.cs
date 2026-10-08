using System;
using System.Collections.Generic;
using Airside.Domain;

namespace Airside.Simulation
{
    [Serializable]
    public sealed class WeatherObservationRecord
    {
        public long FromSeconds, UntilSeconds;
        public int Kind, WindDegrees, WindKnots;
        public float Cover, Rain, Gloom, Visibility, Wetness;
        public WeatherLook Look => new WeatherLook(Cover, Rain, Gloom, Visibility, Wetness);
        public WeatherObservationRecord Copy() => (WeatherObservationRecord)MemberwiseClone();
    }

    /// <summary>Recorded live inputs replay exactly; unknown times use the deterministic forecast.</summary>
    public sealed class AirportWeatherTimeline
    {
        private readonly List<WeatherObservationRecord> _records = new();
        public IReadOnlyList<WeatherObservationRecord> Records => _records;

        public bool TryAt(SimulationTime at, out WeatherObservationRecord record)
        {
            for (var i = _records.Count - 1; i >= 0; i--)
                if (_records[i].FromSeconds <= at.ElapsedSeconds && at.ElapsedSeconds < _records[i].UntilSeconds)
                { record = _records[i]; return true; }
            record = null;
            return false;
        }
        public WeatherKind At(SimulationTime at) => TryAt(at, out var record) ? (WeatherKind)record.Kind : Weather.At(at);
        public SurfaceWind WindAt(AirlineClock clock, SimulationTime at) => TryAt(at, out var record)
            ? new SurfaceWind(record.WindDegrees, record.WindKnots) : RunwayWeather.At(clock, at);

        public void Observe(SimulationTime from, LiveWeatherSnapshot sample, long validSeconds = LiveWeather.StaleSeconds)
        {
            EndLiveAt(from);
            _records.Add(new WeatherObservationRecord { FromSeconds = from.ElapsedSeconds,
                UntilSeconds = from.ElapsedSeconds + Math.Max(1L, Math.Min(LiveWeather.StaleSeconds, validSeconds)), Kind = (int)sample.Kind,
                WindDegrees = sample.Wind.DirectionDegrees, WindKnots = sample.Wind.Knots,
                Cover = sample.Look.CloudCover, Rain = sample.Look.Precipitation, Gloom = sample.Look.Gloom,
                Visibility = sample.Look.Visibility, Wetness = sample.Look.Wetness });
        }
        public void EndLiveAt(SimulationTime at)
        {
            if (TryAt(at, out var record)) record.UntilSeconds = at.ElapsedSeconds;
        }
        public SimulationTime? NextBoundary(SimulationTime now)
        {
            long next = long.MaxValue;
            foreach (var record in _records)
            {
                if (record.FromSeconds > now.ElapsedSeconds) next = Math.Min(next, record.FromSeconds);
                if (record.UntilSeconds > now.ElapsedSeconds) next = Math.Min(next, record.UntilSeconds);
            }
            return next == long.MaxValue ? null : new SimulationTime(next);
        }
        public void Restore(IEnumerable<WeatherObservationRecord> records)
        {
            _records.Clear();
            if (records == null) return;
            long end = 0;
            foreach (var record in records)
            {
                // Flat optional records: blank pre-version/default JSON fields mean no sample.
                if (record == null || record.UntilSeconds <= record.FromSeconds) continue;
                if (record.FromSeconds < end || record.FromSeconds < 0
                    || !Enum.IsDefined(typeof(WeatherKind), record.Kind)
                    || record.WindKnots < 0 || record.WindDegrees < 0 || record.WindDegrees > 360
                    || !FiniteUnit(record.Cover) || !FiniteUnit(record.Rain) || !FiniteUnit(record.Gloom)
                    || !FiniteUnit(record.Visibility) || !FiniteUnit(record.Wetness))
                    throw new FormatException("Invalid recorded airport weather.");
                _records.Add(record.Copy()); end = record.UntilSeconds;
            }
        }
        private static bool FiniteUnit(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0 && value <= 1;
    }
}

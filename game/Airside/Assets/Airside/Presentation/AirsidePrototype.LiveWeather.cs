using System;
using System.Net.Http;
using System.Threading.Tasks;
using Airside.Simulation;
using UnityEngine;

namespace Airside.Presentation
{
    /// <summary>
    /// Presentation-only Adelaide forecast polling. The operational simulation never
    /// sees this feed, so a network failure cannot alter a runway, clearance, save or replay.
    /// </summary>
    public sealed partial class AirsidePrototype
    {
        private static readonly HttpClient WeatherHttp = CreateWeatherHttp();

        private Task<LiveWeatherSnapshot?> _liveWeatherFetch;
        private LiveWeatherSnapshot? _liveWeatherSnapshot;
        private float _liveWeatherFetchedAt = float.NegativeInfinity;
        private float _nextLiveWeatherPollAt;
        private int _liveWeatherFailures;

        private bool LiveWeatherHealthy =>
            AirsideSettings.Current.LiveWeather
            && _liveWeatherSnapshot.HasValue
            && Time.realtimeSinceStartup - _liveWeatherFetchedAt < LiveWeather.StaleSeconds;

        private string LiveWeatherStatus
        {
            get
            {
                if (!AirsideSettings.Current.LiveWeather)
                    return "Off";
                if (LiveWeatherHealthy)
                {
                    var sample = _liveWeatherSnapshot.Value;
                    return $"Live · {Weather.Describe(sample.Kind)} · {sample.TemperatureCelsius:0}°C";
                }
                return _liveWeatherFailures > 0 ? "On · offline fallback" : "On · connecting";
            }
        }

        private string PresentationWeatherSummary => LiveWeatherHealthy
            ? $"Live {Weather.Describe(_liveWeatherSnapshot.Value.Kind)} · {_liveWeatherSnapshot.Value.TemperatureCelsius:0}°C"
            : $"Forecast {Weather.Describe(CurrentWeather)}";

        private static HttpClient CreateWeatherHttp()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Airside/1.0 (+https://github.com/Bazlinka/Airside)");
            return client;
        }

        private void UpdateLiveWeather()
        {
            if (!AirsideSettings.Current.LiveWeather || ReviewWeather.HasValue)
                return;

            var now = Time.realtimeSinceStartup;
            if (_liveWeatherFetch != null)
            {
                if (!_liveWeatherFetch.IsCompleted)
                    return;

                if (_liveWeatherFetch.Status == TaskStatus.RanToCompletion
                    && _liveWeatherFetch.Result.HasValue)
                {
                    _liveWeatherSnapshot = _liveWeatherFetch.Result.Value;
                    _liveWeatherFetchedAt = now;
                    _liveWeatherFailures = 0;
                }
                else
                {
                    _liveWeatherFailures++;
                }

                _liveWeatherFetch = null;
                _nextLiveWeatherPollAt = now + (_liveWeatherFailures == 0
                    ? LiveWeather.PollSeconds
                    : Mathf.Min(LiveWeather.StaleSeconds,
                        30f * (1 << Mathf.Min(_liveWeatherFailures, 7))));
                return;
            }

            if (now < _nextLiveWeatherPollAt)
                return;

            _liveWeatherFetch = Task.Run(async () =>
            {
                try
                {
                    var json = await WeatherHttp.GetStringAsync(LiveWeather.RequestUrl());
                    return LiveWeather.TryParse(json, out var snapshot)
                        ? snapshot
                        : (LiveWeatherSnapshot?)null;
                }
                catch
                {
                    return null;
                }
            });
        }
    }
}

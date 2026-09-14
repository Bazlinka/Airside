using System;
using System.IO;
using System.Linq;
using Airside.Simulation;
using UnityEngine;

namespace Airside.Presentation
{
    /// <summary>
    /// Unattended soak mode for packaged builds (PROJECT_PLAN: a 30-minute soak with
    /// no crash, deadlock or unexplained stop). Launch with
    /// <c>-airsideSoak [-airsideSoakMinutes N]</c>: a fresh airline flies itself in live time,
    /// a heartbeat is logged to Player.log every real minute, and the app quits with
    /// a verdict when time is up. Soak saves go to their own file, never the player's.
    /// </summary>
    public sealed partial class AirsidePrototype
    {
        private const string SoakFlag = "-airsideSoak";
        private const string SoakMinutesFlag = "-airsideSoakMinutes";
        private const string SoakLogTag = "[Airside soak]";

        private static bool? _soakRequested;
        private float _soakMinutes = 30f;
        private float _soakStartedAt = -1f;
        private float _soakNextHeartbeat;
        private long _soakLastClock = -1;
        private int _soakStalledBeats;
        private int _soakFrames;
        private readonly SeededRandomSource _soakChoices = new(31337);

        private static bool SoakMode
        {
            get
            {
                _soakRequested ??= Array.IndexOf(Environment.GetCommandLineArgs(), SoakFlag) >= 0;
                return _soakRequested.Value;
            }
        }

        private static string SavePath => SoakMode
            ? Path.Combine(Application.persistentDataPath, "airline-save-soak.json")
            : AirlineSaveFile.DefaultPath;

        private void DriveSoak()
        {
            if (!SoakMode)
                return;

            _soakFrames++;
            if (_soakStartedAt < 0f)
            {
                var args = Environment.GetCommandLineArgs();
                var index = Array.IndexOf(args, SoakMinutesFlag);
                if (index >= 0 && index + 1 < args.Length && float.TryParse(args[index + 1], out var minutes) && minutes > 0f)
                    _soakMinutes = minutes;

                _soakStartedAt = Time.unscaledTime;
                _soakNextHeartbeat = _soakStartedAt + 60f;
                _saveProbed = true; // never offer or read the player's save
                StartAirline("Soak Air");
                Debug.Log($"{SoakLogTag} started for {_soakMinutes:0} min in live time");
            }

            if (_awaySummary != null)
                _awaySummary = null;
            _menuOpen = false;

            foreach (var aircraft in _operations.FleetOf(_operations.PlayerAirline))
            {
                if (aircraft.State == FleetState.AtStand && !aircraft.Scheduled.HasValue)
                {
                    var reachable = _operations.MapDestinations().Where(d => _operations.CanReach(aircraft, d)).ToList();
                    var destination = reachable[_soakChoices.NextInt(0, reachable.Count)];
                    // The first flight leaves four minutes in, so every soak covers a full
                    // engine start early; later ones are spread over half an hour.
                    var delay = aircraft.CompletedTrips == 0
                        ? 4 * 60
                        : _soakChoices.NextInt((int)EngineStartSequence.MinimumDepartureLeadSeconds, 1800);
                    _operations.ScheduleDeparture(aircraft, destination, _clock.Now.Advance(delay));
                }
                else if (aircraft.State == FleetState.AwaitingStand)
                {
                    foreach (var stand in _operations.FreeStands())
                    {
                        _operations.AssignStand(aircraft, stand);
                        break;
                    }
                }
            }

            var now = Time.unscaledTime;
            if (now >= _soakNextHeartbeat)
            {
                _soakNextHeartbeat += 60f;
                var clock = _clock.Now.ElapsedSeconds;
                _soakStalledBeats = clock == _soakLastClock ? _soakStalledBeats + 1 : 0;
                _soakLastClock = clock;
                var trips = _operations.Fleet.Sum(a => a.CompletedTrips);
                var states = string.Join(", ", _operations.Fleet.Select(a =>
                {
                    var e = EngineStartSequence.For(a, _preciseTime);
                    return $"{a.Registration} {a.State} eng L{e.Left:0.00}/R{e.Right:0.00}{(e.Beacon ? " beacon" : "")}{(e.DoorsOpen ? " doors" : "")}";
                }));
                Debug.Log($"{SoakLogTag} {(now - _soakStartedAt) / 60f:0} min · sim {AirlineClockText()} · trips {trips} · " +
                          $"fps {_soakFrames / 60f:0} · mem {GC.GetTotalMemory(false) / (1024 * 1024)} MB · {states}");
                _soakFrames = 0;
                if (_soakStalledBeats >= 2)
                    Debug.LogError($"{SoakLogTag} STALL: simulation clock has not moved for two minutes");
            }

            if (now - _soakStartedAt >= _soakMinutes * 60f)
            {
                var trips = _operations.Fleet.Sum(a => a.CompletedTrips);
                Debug.Log($"{SoakLogTag} COMPLETE after {_soakMinutes:0} min · sim {AirlineClockText()} · trips {trips} · " +
                          $"stalls {(_soakStalledBeats > 0 ? "yes" : "none")}");
                QuitGame();
            }
        }

        private string AirlineClockText() =>
            $"{_operations.Clock.DateText(_clock.Now)} {_operations.Clock.TimeText(_clock.Now)}";
    }
}

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using Airside.Domain;
using Airside.Simulation;
using UnityEngine;

namespace Airside.Presentation
{
    public sealed partial class AirsidePrototype
    {
        // Packaged QA only. Real schedules, reservations and aircraft transitions still run.
        // Accelerated runs verify the journey; use rate 1 separately for performance evidence.
        private string _reviewJourneyCode, _reviewJourneyAircraftId;
        private double _reviewJourneyRate = 1, _reviewJourneyEpoch;
        private float _reviewJourneyStart, _reviewJourneyClockAt, _reviewJourneyNextTrace;
        private double _reviewJourneySavedRate;
        private float _reviewJourneyPerfSeconds, _reviewJourneyPerfAt;
        private bool _reviewJourneyPerfStarted, _reviewJourneyPerfDone;
        private FleetState? _reviewJourneyLastState;
        private string _reviewJourneyOutput, _reviewJourneyShotPhase;
        private float _reviewJourneyNextShot;
        private bool _reviewJourneyCapturing, _reviewJourneyDone;
        private bool FlightJourneyReviewActive => SoakMode && _reviewJourneyCode != null;

        private void InitializeFlightJourneyReview(string[] args)
        {
            var index = Array.IndexOf(args, "-airsideReviewJourney");
            if (index < 0) return;
            if (!CockpitReview || index + 1 >= args.Length
                || !DestinationCatalogue.TryFind(args[index + 1], out var destination)
                || !FlightWorldGrid.Covered(destination.Latitude, destination.Longitude)
                || !RegionalRunways.TryGet(destination.Code, out _))
                throw new ArgumentException("Journey review requires cockpit review and a covered regional runway code.");
            _reviewJourneyCode = destination.Code;
            var outIndex = Array.IndexOf(args, "-airsideReviewJourneyOut");
            if (outIndex >= 0 && outIndex + 1 < args.Length)
            {
                _reviewJourneyOutput = Path.GetFullPath(args[outIndex + 1]);
                Directory.CreateDirectory(_reviewJourneyOutput);
            }
            var rateIndex = Array.IndexOf(args, "-airsideReviewJourneyRate");
            if (rateIndex >= 0 && (rateIndex + 1 >= args.Length
                || !double.TryParse(args[rateIndex + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out _reviewJourneyRate)
                || !double.IsFinite(_reviewJourneyRate) || _reviewJourneyRate < 1 || _reviewJourneyRate > 40))
                throw new ArgumentException("Journey review rate must be between 1 and 40.");
            var perfIndex = Array.IndexOf(args, "-airsideReviewJourneyPerfSeconds");
            if (perfIndex >= 0 && (perfIndex + 1 >= args.Length
                || !float.TryParse(args[perfIndex + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out _reviewJourneyPerfSeconds)
                || !float.IsFinite(_reviewJourneyPerfSeconds) || _reviewJourneyPerfSeconds < 60 || _reviewJourneyPerfSeconds > 1200))
                throw new ArgumentException("Journey performance window must be 60 to 1200 real seconds.");
        }

        private void BeginFlightJourneyReviewClock()
        {
            if (!FlightJourneyReviewActive) return;
            var subject = _operations.FleetOf(_operations.PlayerAirline)
                .FirstOrDefault(a => CockpitAvailability.Supported(a.Type));
            if (subject == null) throw new InvalidOperationException("Journey review needs a supported player aircraft.");
            _reviewJourneyAircraftId = subject.Registration;
            _reviewJourneyEpoch = _operations.Clock.SecondsAt(DateTime.UtcNow);
            _reviewJourneyStart = _reviewJourneyClockAt = Time.unscaledTime;
            Debug.Log($"[Airside journey] started ADL-{_reviewJourneyCode} rate {_reviewJourneyRate:0.##}; fresh soak save only");
        }

        private double FlightJourneyPresentationTime(double wallSeconds)
        {
            if (!FlightJourneyReviewActive) return LivePresentationTime(wallSeconds);
            var now = Time.unscaledTime;
            var frameSeconds = now - _reviewJourneyClockAt;
            // Accelerated QA must not omit flight phases after a blocked render frame.
            // The rate-1 measurement retains wall time and reports stalls honestly.
            if (_reviewJourneyRate > 1 && frameSeconds > 0.1f) frameSeconds = 0.1f;
            var seconds = _reviewJourneyEpoch + frameSeconds * _reviewJourneyRate;
            _reviewJourneyEpoch = seconds; _reviewJourneyClockAt = now;
            if (_reviewJourneyPerfSeconds > 0 && !_reviewJourneyPerfDone)
            {
                if (!_reviewJourneyPerfStarted && !AirportPresentationVisible)
                {
                    _reviewJourneyPerfStarted = true;
                    _reviewJourneyPerfAt = now;
                    _reviewJourneySavedRate = _reviewJourneyRate;
                    _reviewJourneyRate = 1;
                    _reviewJourneyEpoch = seconds; _reviewJourneyClockAt = now;
                    Debug.Log($"[Airside journey] PERFORMANCE START real {now-_reviewJourneyStart:0.0}s rate 1 duration {_reviewJourneyPerfSeconds:0}s");
                }
                else if (_reviewJourneyPerfStarted && now >= _reviewJourneyPerfAt + _reviewJourneyPerfSeconds)
                {
                    _reviewJourneyPerfDone = true;
                    _reviewJourneyRate = _reviewJourneySavedRate;
                    _reviewJourneyEpoch = seconds; _reviewJourneyClockAt = now;
                    Debug.Log($"[Airside journey] PERFORMANCE END real {now-_reviewJourneyStart:0.0}s; resume rate {_reviewJourneyRate:0.##}");
                }
            }
            return seconds;
        }

        private void TraceFlightJourneyReview()
        {
            if (!FlightJourneyReviewActive || !FleetMode) return;
            var aircraft = _operations.FleetOf(_operations.PlayerAirline)
                .FirstOrDefault(a => a.Registration == _reviewJourneyAircraftId);
            if (aircraft == null) return;
            CaptureFlightJourneyPhase(aircraft);
            if (_reviewJourneyLastState == aircraft.State && Time.unscaledTime < _reviewJourneyNextTrace) return;
            // The return climb is traced densely: its speeds (HUD ground speed and calibrated airspeed) are what the
            // departure hand-over must keep inside the speed envelope.
            var climbing = aircraft.State == FleetState.Inbound && TryEnroute(aircraft, out _, out var legElapsed) && legElapsed < 900;
            _reviewJourneyNextTrace = Time.unscaledTime + (climbing ? 0.4f : 5f);
            _reviewJourneyLastState = aircraft.State;
            var position = _cockpitView != null ? _cockpitView.position + FlightOrigin : Vector3.zero;
            Debug.Log($"[Airside journey] real {Time.unscaledTime-_reviewJourneyStart:0.0}s sim {_preciseTime:0.0} "
                + $"{aircraft.Registration} {aircraft.State} trips {aircraft.CompletedTrips} cockpit {InCockpit} "
                + $"world {position.x:0.0},{position.y:0.0},{position.z:0.0} "
                + $"gs {_cockpitGroundKnots:0} cas {FlightAtmosphere.CalibratedKnots(_cockpitGroundKnots, _cockpitGearHeight + FlightAtmosphere.FieldElevationMetres):0} "
                + $"attitude {(_cockpitView != null ? _cockpitView.eulerAngles : Vector3.zero)} "
                + $"origin {_flightOriginX:0},{_flightOriginZ:0} tiles {_flightTerrain?.ResidentTiles ?? 0} "
                + $"airport {AirportPresentationVisible} elevation {_flightTerrain?.HasElevation ?? false}");
        }

        private void CaptureFlightJourneyPhase(FleetAircraft aircraft)
        {
            if (_reviewJourneyOutput == null || _reviewJourneyCapturing || _reviewJourneyDone) return;
            var complete = aircraft.CompletedTrips > 0 && aircraft.State == FleetState.AtStand;
            if (!InCockpit && !complete) return;
            var phase = aircraft.State.ToString();
            if (CanWatchJourney(aircraft))
            {
                var remaining = (aircraft.StateEndsAt?.ElapsedSeconds ?? _preciseTime) - _preciseTime;
                if (aircraft.State == FleetState.Outbound && remaining < 180)
                    phase += remaining < 40 ? "-rollout" : "-approach";
                if (aircraft.State == FleetState.Inbound && remaining < 600) phase += "-final";
            }
            if (!complete && phase == _reviewJourneyShotPhase && Time.unscaledTime < _reviewJourneyNextShot) return;
            _reviewJourneyShotPhase = phase;
            _reviewJourneyNextShot = Time.unscaledTime + 15;
            if (complete)
            {
                _reviewJourneyDone = true;
                ExitCockpit(true);
                phase = "completed-overview";
            }
            var path = Path.Combine(_reviewJourneyOutput, $"{Time.unscaledTime-_reviewJourneyStart:0000.0}-{phase}.png");
            _reviewJourneyCapturing = true;
            StartCoroutine(CaptureFlightJourneyFrame(path, complete));
        }

        private System.Collections.IEnumerator CaptureFlightJourneyFrame(string path, bool complete)
        {
            var succeeded = false;
            yield return ReviewFrameCapture.Capture(path, ok => succeeded = ok);
            _reviewJourneyCapturing = false;
            if (!succeeded)
            {
                Application.Quit(2);
                yield break;
            }
            Debug.Log($"[Airside journey] frame {path}");
            if (complete)
            {
                Debug.Log($"[Airside journey] COMPLETE round trip; origin {_flightOriginX},{_flightOriginZ} airport {AirportPresentationVisible}");
                QuitGame();
            }
        }
    }
}

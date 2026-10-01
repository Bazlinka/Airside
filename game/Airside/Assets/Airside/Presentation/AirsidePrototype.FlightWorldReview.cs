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
        private float _reviewJourneyStart, _reviewJourneyNextTrace;
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
        }

        private void BeginFlightJourneyReviewClock()
        {
            if (!FlightJourneyReviewActive) return;
            var subject = _operations.FleetOf(_operations.PlayerAirline)
                .FirstOrDefault(a => CockpitAvailability.Supported(a.Type));
            if (subject == null) throw new InvalidOperationException("Journey review needs a supported player aircraft.");
            _reviewJourneyAircraftId = subject.Registration;
            _reviewJourneyEpoch = _operations.Clock.SecondsAt(DateTime.UtcNow);
            _reviewJourneyStart = Time.unscaledTime;
            Debug.Log($"[Airside journey] started ADL-{_reviewJourneyCode} rate {_reviewJourneyRate:0.##}; fresh soak save only");
        }

        private double FlightJourneyPresentationTime(double wallSeconds) => FlightJourneyReviewActive
            ? _reviewJourneyEpoch + (Time.unscaledTime - _reviewJourneyStart) * _reviewJourneyRate
            : LivePresentationTime(wallSeconds);

        private void TraceFlightJourneyReview()
        {
            if (!FlightJourneyReviewActive || !FleetMode) return;
            var aircraft = _operations.FleetOf(_operations.PlayerAirline)
                .FirstOrDefault(a => a.Registration == _reviewJourneyAircraftId);
            if (aircraft == null) return;
            CaptureFlightJourneyPhase(aircraft);
            if (_reviewJourneyLastState == aircraft.State && Time.unscaledTime < _reviewJourneyNextTrace) return;
            _reviewJourneyNextTrace = Time.unscaledTime + 5;
            _reviewJourneyLastState = aircraft.State;
            var position = _cockpitView != null ? _cockpitView.position + FlightOrigin : Vector3.zero;
            Debug.Log($"[Airside journey] real {Time.unscaledTime-_reviewJourneyStart:0.0}s sim {_preciseTime:0.0} "
                + $"{aircraft.Registration} {aircraft.State} trips {aircraft.CompletedTrips} cockpit {InCockpit} "
                + $"world {position.x:0.0},{position.y:0.0},{position.z:0.0} "
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
            yield return null;
            yield return new WaitForEndOfFrame();
            var texture = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Destroy(texture);
            _reviewJourneyCapturing = false;
            Debug.Log($"[Airside journey] frame {path}");
            if (complete)
            {
                Debug.Log($"[Airside journey] COMPLETE round trip; origin {_flightOriginX},{_flightOriginZ} airport {AirportPresentationVisible}");
                QuitGame();
            }
        }
    }
}

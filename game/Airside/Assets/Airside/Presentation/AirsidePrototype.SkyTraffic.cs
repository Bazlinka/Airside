using System.Collections.Generic;
using Airside.Domain;
using Airside.Simulation;
using UnityEngine;

namespace Airside.Presentation
{
    public sealed partial class AirsidePrototype
    {
        private Transform _skyTrafficRoot;
        private readonly Dictionary<string, Transform> _skyViews = new();

        private void UpdateSkyTraffic()
        {
            // Real traffic replaces the authored sky while the live feed is answering;
            // drawing both would double every Qantas and Virgin arrival.
            if (!FleetMode || _operations == null || LiveTrafficHealthy)
            {
                HideSkyTraffic();
                return;
            }

            if (_skyTrafficRoot == null)
            {
                _skyTrafficRoot = new GameObject("Sky traffic").transform;
                _skyTrafficRoot.SetParent(transform, false);
            }

            var live = new HashSet<string>();
            foreach (var flight in SkyTraffic.At(_preciseTime, _operations.Clock))
                ShowSkyFlight(flight, new Color(0.82f, 0.84f, 0.88f), live);
            foreach (var flight in AdelaideDayPlan.AirborneAt(_operations, new SimulationTime((long)_preciseTime)))
                ShowSkyFlight(flight, ColorForSkyAirline(flight.Callsign), live);

            var stale = new List<string>();
            foreach (var pair in _skyViews)
            {
                if (live.Contains(pair.Key))
                    continue;
                if (pair.Value != null)
                    pair.Value.gameObject.SetActive(false);
                stale.Add(pair.Key);
            }

            // Drop views that have been off-screen a while by destroying when the dictionary
            // grows past a quiet cap — keep a handful of hidden ones for reuse is overkill here.
            if (_skyViews.Count > 64)
            {
                foreach (var id in stale)
                {
                    if (!_skyViews.TryGetValue(id, out var view))
                        continue;
                    if (view != null)
                    {
                        AirsideNamedChildren.Forget(view);
                        ForgetAircraftViewParts(view);
                        Destroy(view.gameObject);
                    }

                    _skyViews.Remove(id);
                }
            }
        }

        private void ShowSkyFlight(SkyFlight flight, Color livery, HashSet<string> live)
        {
            if (SkyTraffic.OccupiesTheCircuit(flight)
                || !SkyTraffic.TryWorldPosition(flight, out var x, out var y, out var z))
                return;

            live.Add(flight.Callsign);
            if (!_skyViews.TryGetValue(flight.Callsign, out var view) || view == null)
            {
                view = BuildAircraftForType($"Sky {flight.Callsign}", flight.Type, livery, null);
                view.SetParent(_skyTrafficRoot, false);
                _skyViews[flight.Callsign] = view;
            }

            view.gameObject.SetActive(true);
            view.position = new Vector3((float)x, AirsideFlightPath.GroundY + (float)y, (float)z);
            // Negative X is nose up, the same as the fleet's ClimbPitchDegrees. These were
            // the wrong way round: departures climbed away nose-down, arrivals sank nose-up.
            var pitch = flight.To.Code == "ADL" ? -2.5f
                : flight.From.Code == "ADL" ? AirsideFlightPath.ClimbPitchDegrees
                : AirsideFlightPath.ClimbPitchDegrees * 0.35f;
            view.rotation = Quaternion.Euler(0f, SkyTraffic.DisplayUnityYaw(flight), 0f)
                            * Quaternion.Euler(pitch, 0f, 0f);

            var parts = PartsFor(view);
            UpdateAircraftLightsAndGear(parts.LightsAndGear, AircraftPhase.Circuit, PresentationDaylight, 0.5f,
                PresentationDeltaTime, PresentationClock, null);
            SpinJetFans(view, parts.FanLeft, parts.FanRight, AircraftPhase.Circuit, null);
            SpinPropellers(view, parts.Propellers, AircraftPhase.Circuit, null);
        }

        private static Color ColorForSkyAirline(string callsign)
        {
            if (callsign != null && callsign.StartsWith("REX"))
                return new Color(0.82f, 0.29f, 0.12f);
            if (callsign != null && callsign.StartsWith("QLK"))
                return new Color(0.85f, 0.08f, 0.12f);
            if (callsign != null && callsign.StartsWith("VOZ"))
                return new Color(0.84f, 0.10f, 0.39f);
            if (callsign != null && callsign.StartsWith("ANZ"))
                return new Color(0.12f, 0.12f, 0.12f);
            if (callsign != null && callsign.StartsWith("SIA"))
                return new Color(0.11f, 0.25f, 0.55f);
            if (callsign != null && callsign.StartsWith("CPA"))
                return new Color(0.00f, 0.40f, 0.39f);
            return new Color(0.82f, 0.84f, 0.88f);
        }

        private void HideSkyTraffic()
        {
            foreach (var pair in _skyViews)
                if (pair.Value != null)
                    pair.Value.gameObject.SetActive(false);
        }
    }
}

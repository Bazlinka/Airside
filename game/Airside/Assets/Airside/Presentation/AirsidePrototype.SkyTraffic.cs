using System.Collections.Generic;
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
            if (!FleetMode || _operations == null)
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
            foreach (var flight in SkyTraffic.At(_preciseTime))
            {
                if (!SkyTraffic.TryWorldPosition(flight, out var x, out var y, out var z))
                    continue;

                live.Add(flight.Callsign);
                if (!_skyViews.TryGetValue(flight.Callsign, out var view) || view == null)
                {
                    view = BuildAircraftForType($"Sky {flight.Callsign}", flight.Type,
                        new Color(0.82f, 0.84f, 0.88f), null);
                    view.SetParent(_skyTrafficRoot, false);
                    _skyViews[flight.Callsign] = view;
                }

                view.gameObject.SetActive(true);
                var position = new Vector3((float)x, AirsideFlightPath.GroundY + (float)y, (float)z);
                view.position = position;
                var yaw = Quaternion.Euler(0f, (float)flight.HeadingDegrees, 0f);
                var pitch = Quaternion.Euler(AirsideFlightPath.ClimbPitchDegrees * 0.35f, 0f, 0f);
                view.rotation = yaw * pitch;

                var parts = PartsFor(view);
                UpdateAircraftLightsAndGear(parts.LightsAndGear, AircraftPhase.Circuit, PresentationDaylight, 0.5f,
                    PresentationDeltaTime, PresentationClock, null);
                SpinJetFans(view, parts.FanLeft, parts.FanRight, AircraftPhase.Circuit, null);
                SpinPropellers(view, parts.Propellers, AircraftPhase.Circuit, null);
            }

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
            if (_skyViews.Count > 16)
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

        private void HideSkyTraffic()
        {
            foreach (var pair in _skyViews)
                if (pair.Value != null)
                    pair.Value.gameObject.SetActive(false);
        }
    }
}

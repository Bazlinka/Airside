using System;
using System.Collections.Generic;
using System.IO;
using Airside.Simulation;
using UnityEngine;

namespace Airside.Presentation
{
    public sealed partial class AirsidePrototype
    {
        private const string EmuAirDecal = "Textures/Decals/dc_livery_emu_air_v01.png";
        private const string PlayerDecalTemplate = "Textures/Decals/dc_livery_airside_traffic_v01.png";

        private readonly List<CommercialFlight> _fleetFlights = new();
        private readonly Dictionary<string, CommercialFlight> _fleetFlightById = new();
        private readonly Dictionary<string, FleetAircraft> _fleetAircraftById = new();
        private readonly Dictionary<string, Texture2D> _tintedDecals = new();
        private string _fleetFollowSignature;

        /// <summary>Fleet departures roll from the real 05 threshold; the demo circuit from where it stopped.</summary>
        private float TakeoffOffsetX => FleetMode ? 0f : AirsideFlightPath.CircuitTakeoffOffsetX;

        /// <summary>Once an airline is running, the fleets replace the demo circuit on the field.</summary>
        private bool FleetMode => _operations != null;

        /// <summary>What the 3D field draws: the fleets in airline mode, else the demo circuit.</summary>
        private IReadOnlyList<CommercialFlight> VisualFlights => FleetMode ? _fleetFlights : _simulation.Flights;

        /// <summary>
        /// Rebuild the drawn flights from the fleets. Player aircraft come first so
        /// Follow picks your aircraft before Emu Air's.
        /// </summary>
        private void RefreshFleetFlights()
        {
            if (!FleetMode)
                return;

            _fleetFlights.Clear();
            AddFleetFlights(player: true);
            AddFleetFlights(player: false);
        }

        private void AddFleetFlights(bool player)
        {
            var now = _clock.Now;
            foreach (var aircraft in _operations.Fleet)
            {
                if (aircraft.Airline.IsPlayer != player)
                    continue;

                var id = aircraft.Registration;
                _fleetAircraftById[id] = aircraft;
                var visual = FleetVisual.For(aircraft, now);

                if (!_fleetFlightById.TryGetValue(id, out var flight))
                {
                    flight = new CommercialFlight(id, now, AirportSimulation.StandOne,
                        _simulation.TaxiNetwork.RoutesTo(AirportSimulation.StandOne));
                    _fleetFlightById[id] = flight;
                }

                if (flight.Operation.Phase != visual.Phase || !flight.Operation.PhaseStartedAt.Equals(visual.PhaseStartedAt))
                    flight.Operation = AircraftOperation.InPhase(id, visual.Phase, visual.PhaseStartedAt);

                _fleetFlights.Add(flight);
            }
        }

        /// <summary>Engine start/shutdown state for a fleet aircraft; null for the demo circuit.</summary>
        private EngineState? FleetEngines(CommercialFlight flight) =>
            FleetMode && _fleetAircraftById.TryGetValue(flight.AircraftId, out var aircraft)
                ? EngineStartSequence.For(aircraft, _preciseTime)
                : null;

        private bool IsFleetFlightVisible(string aircraftId) =>
            _fleetAircraftById.TryGetValue(aircraftId, out var aircraft)
            && FleetVisual.For(aircraft, _clock.Now).Visible;

        private bool TryFleetGround(CommercialFlight flight, out FleetAircraft aircraft, out FleetVisual visual)
        {
            visual = default;
            aircraft = null;
            if (!FleetMode || !_fleetAircraftById.TryGetValue(flight.AircraftId, out aircraft))
                return false;
            visual = FleetVisual.For(aircraft, _clock.Now);
            return visual.Visible && visual.Leg != FleetGroundLeg.None;
        }

        /// <summary>
        /// The real Adelaide ground leg a fleet aircraft is on (ADR 0045), and the time into
        /// it at the fractional presentation clock. A leg the simulation timed differently
        /// (an older save) is stretched to fit, so the aircraft still arrives on time.
        /// </summary>
        private GroundPose FleetGroundPose(FleetAircraft aircraft, FleetVisual visual, float lookAheadSeconds)
        {
            switch (visual.Leg)
            {
                case FleetGroundLeg.Parked:
                {
                    var bay = AdelaideGround.Bay(aircraft.Stand);
                    var heading = bay.HeadingDegrees * Mathf.Deg2Rad;
                    return new GroundPose(bay.StopX, bay.StopZ, Mathf.Sin(heading), Mathf.Cos(heading), 0f, false);
                }
                case FleetGroundLeg.HoldingShort:
                {
                    var leg = AdelaideGround.TaxiOut(aircraft.DepartureStand);
                    var end = leg.PoseAt(leg.Seconds);
                    return new GroundPose(end.X, end.Z, end.NoseX, end.NoseZ, 0f, false);
                }
                case FleetGroundLeg.AwaitingStand:
                    return AdelaideGround.AwaitingPose(0);
                default:
                {
                    var leg = visual.Leg switch
                    {
                        FleetGroundLeg.TaxiOut => AdelaideGround.TaxiOut(aircraft.DepartureStand),
                        FleetGroundLeg.Lineup => AdelaideGround.Lineup,
                        FleetGroundLeg.Vacate => AdelaideGround.Vacate,
                        _ => AdelaideGround.TaxiIn(aircraft.Stand)
                    };
                    var elapsed = _preciseTime - visual.LegStartedAt.ElapsedSeconds + lookAheadSeconds;
                    var scale = visual.LegSeconds > 0 ? leg.Seconds / visual.LegSeconds : 1.0;
                    return leg.PoseAt(elapsed * scale);
                }
            }
        }

        /// <summary>0..1 through the current ground leg, in place of circuit phase progress; false when airborne.</summary>
        private bool TryFleetGroundProgress(CommercialFlight flight, float lookAheadSeconds, out float progress)
        {
            progress = 0f;
            if (!TryFleetGround(flight, out _, out var visual))
                return false;
            if (visual.LegSeconds > 0)
                progress = Mathf.Clamp01((float)((_preciseTime - visual.LegStartedAt.ElapsedSeconds + lookAheadSeconds) / visual.LegSeconds));
            else
                progress = visual.Leg == FleetGroundLeg.HoldingShort ? 1f : 0f;
            return true;
        }

        /// <summary>World position on the Adelaide ground routes, or null when the circuit path applies.</summary>
        private Vector3? FleetGroundPosition(CommercialFlight flight, float lookAheadSeconds)
        {
            if (!TryFleetGround(flight, out var aircraft, out var visual))
                return null;
            var pose = FleetGroundPose(aircraft, visual, lookAheadSeconds);
            return new Vector3(pose.X, AirsideFlightPath.GroundY, pose.Z);
        }

        /// <summary>Ground speed on the Adelaide routes in m/s, or null when the circuit path applies.</summary>
        private float? FleetGroundSpeed(CommercialFlight flight)
        {
            if (!TryFleetGround(flight, out var aircraft, out var visual))
                return null;
            return FleetGroundPose(aircraft, visual, 0f).Speed;
        }

        /// <summary>Where the nose points on a ground leg — tail-first on the pushback, parked heading at the bay.</summary>
        private Vector3 FleetGroundFacing(CommercialFlight flight, Vector3 travel)
        {
            if (!TryFleetGround(flight, out var aircraft, out var visual))
                return travel;
            var pose = FleetGroundPose(aircraft, visual, 0f);
            return new Vector3(pose.NoseX, 0f, pose.NoseZ);
        }

        // ---- Aircraft models --------------------------------------------------------

        private Transform BuildFleetAircraft(string aircraftId)
        {
            if (!_fleetAircraftById.TryGetValue(aircraftId, out var aircraft))
                return BuildAircraft($"Commercial {aircraftId}", AirsideTheme.CoastalBlue);

            var airline = aircraft.Airline;
            var accent = AirsideTheme.FromHex(airline.LiveryHex);
            var view = BuildAircraft($"Commercial {aircraftId}", accent,
                airline.IsPlayer ? null : EmuAirDecal);

            if (airline.IsPlayer)
            {
                var decal = TintedPlayerDecal(airline.LiveryHex, accent);
                if (decal != null)
                    ApplyLiveryTexture(view, decal);
            }

            foreach (var child in AirsideNamedChildren.Get(view))
            {
                if (!child.name.StartsWith("Livery", StringComparison.Ordinal))
                    continue;
                var renderer = child.GetComponent<Renderer>();
                if (renderer != null)
                    SetRendererColor(renderer, accent);
            }

            return view;
        }

        /// <summary>
        /// The neutral traffic livery with its blue bands repainted in the player's
        /// colour: darker blues take the colour, lighter ones a paler tint of it.
        /// </summary>
        private Texture2D TintedPlayerDecal(string hex, Color accent)
        {
            if (_tintedDecals.TryGetValue(hex, out var cached))
                return cached;

            Texture2D tinted = null;
            var path = ArtRuntimePaths.ResolveExisting(PlayerDecalTemplate);
            if (path != null)
            {
                tinted = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: true, linear: false);
                if (tinted.LoadImage(File.ReadAllBytes(path)))
                {
                    var pixels = tinted.GetPixels32();
                    for (var i = 0; i < pixels.Length; i++)
                    {
                        var p = pixels[i];
                        if (p.a < 8 || p.b - p.r < 30)
                            continue;
                        var lightness = Mathf.InverseLerp(100f, 180f, (p.r + p.g + p.b) / 3f);
                        var colour = Color.Lerp(accent, Color.white, lightness * 0.45f);
                        pixels[i] = new Color32(
                            (byte)(colour.r * 255f), (byte)(colour.g * 255f), (byte)(colour.b * 255f), p.a);
                    }

                    tinted.SetPixels32(pixels);
                    tinted.wrapMode = TextureWrapMode.Repeat;
                    tinted.name = $"dc_livery_player_{hex.TrimStart('#')}";
                    tinted.Apply(updateMipmaps: true, makeNoLongerReadable: true);
                }
                else
                {
                    Destroy(tinted);
                    tinted = null;
                }
            }

            _tintedDecals[hex] = tinted;
            return tinted;
        }

        /// <summary>Follow cycles through what is on the field, refreshed as aircraft come and go.</summary>
        private void RefreshFleetFollowTargets(Transform[] views)
        {
            if (_cameraController == null)
                return;

            var active = new List<Transform>();
            var signature = string.Empty;
            foreach (var view in views)
            {
                if (view == null || !view.gameObject.activeSelf)
                    continue;
                active.Add(view);
                signature += view.name + "|";
            }

            if (signature == _fleetFollowSignature)
                return;
            _fleetFollowSignature = signature;
            _cameraController.SetFollowTargets(active.ToArray());
        }
    }
}

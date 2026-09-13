using System;
using System.Collections.Generic;
using System.IO;
using Airside.Simulation;
using UnityEngine;

namespace Airside.Presentation
{
    /// <summary>
    /// Adelaide ground movement for the airline fleets (ADR 0045): terminal apron
    /// bays, and the taxi, lineup and vacate routes between them and runway 05/23 on
    /// the real pavement layout. Pure geometry — no scene objects.
    /// </summary>
    public static class FleetGroundRoutes
    {
        private static float Y => AirsideFlightPath.GroundY;

        /// <summary>Runway exit E2, used both to enter for departure and to vacate.</summary>
        public const float ExitX = AirsideAdelaidePavement.TaxiwayE2CenterX;

        /// <summary>The A↔F connector and terminal apron entry share this station.</summary>
        public const float LinkX = -300f;

        public const float HoldZ = AirsideAdelaidePavement.RunwayHoldingPositionFromCentrelineMetres;
        public const float TaxiwayFZ = AirsideAdelaidePavement.TaxiwayFCenterZ;
        public const float TaxiwayAZ = AirsideAdelaidePavement.TaxiwayACenterZ;

        /// <summary>Apron taxilane along the south of the bays, and the bay centreline row.</summary>
        public const float ApronLaneZ = 405f;
        public const float BayZ = 470f;

        /// <summary>Awaiting-stand spots queue west along Taxiway F from here.</summary>
        public const float AwaitingFirstX = -700f;
        public const float AwaitingSpacing = 60f;

        private static readonly float[] BayXs = { -950f, -800f, -650f, -500f };

        public static Vector3 Bay(string standId)
        {
            var index = 0;
            if (standId != null && standId.StartsWith("BAY-", StringComparison.Ordinal)
                && int.TryParse(standId.Substring(4), out var number))
                index = Mathf.Clamp(number - 1, 0, BayXs.Length - 1);
            return new Vector3(BayXs[index], Y, BayZ);
        }

        public static Vector3 HoldingPoint => new(ExitX, Y, HoldZ);

        public static Vector3 AwaitingSpot(int slot) =>
            new(AwaitingFirstX - AwaitingSpacing * Mathf.Max(0, slot), Y, TaxiwayFZ);

        /// <summary>Stand → holding point. The first leg is the pushback, drawn tail-first.</summary>
        public static Vector3[] TaxiOut(string standId)
        {
            var bay = Bay(standId);
            return new[]
            {
                bay,
                new Vector3(bay.x, Y, ApronLaneZ),
                new Vector3(LinkX, Y, ApronLaneZ),
                new Vector3(LinkX, Y, TaxiwayAZ),
                new Vector3(LinkX, Y, TaxiwayFZ),
                new Vector3(ExitX, Y, TaxiwayFZ),
                HoldingPoint
            };
        }

        /// <summary>Holding point → runway, then backtrack to where the takeoff roll starts.</summary>
        public static Vector3[] Lineup() => new[]
        {
            HoldingPoint,
            new Vector3(ExitX, Y, 0f),
            AirsideFlightPath.OnRunwayHold()
        };

        /// <summary>Rollout end → backtrack to E2 → clear onto F → queue for a stand.</summary>
        public static Vector3[] Vacate(int awaitingSlot) => new[]
        {
            AirsideFlightPath.OnRunwayHold(),
            new Vector3(ExitX, Y, 0f),
            HoldingPoint,
            new Vector3(ExitX, Y, TaxiwayFZ),
            AwaitingSpot(awaitingSlot)
        };

        public static Vector3[] TaxiIn(int awaitingSlot, string standId)
        {
            var bay = Bay(standId);
            return new[]
            {
                AwaitingSpot(awaitingSlot),
                new Vector3(LinkX, Y, TaxiwayFZ),
                new Vector3(LinkX, Y, TaxiwayAZ),
                new Vector3(LinkX, Y, ApronLaneZ),
                new Vector3(bay.x, Y, ApronLaneZ),
                bay
            };
        }

        /// <summary>Point at <paramref name="t"/> (0..1) of the route's length, and the segment it is on.</summary>
        public static Vector3 Sample(Vector3[] route, float t, out int segment)
        {
            segment = 0;
            if (route == null || route.Length == 0)
                return Vector3.zero;
            if (route.Length == 1)
                return route[0];

            var total = 0f;
            for (var i = 1; i < route.Length; i++)
                total += Vector3.Distance(route[i - 1], route[i]);
            var target = Mathf.Clamp01(t) * total;

            for (var i = 1; i < route.Length; i++)
            {
                var length = Vector3.Distance(route[i - 1], route[i]);
                if (target <= length || i == route.Length - 1)
                {
                    segment = i - 1;
                    return length <= 0f ? route[i] : Vector3.Lerp(route[i - 1], route[i], Mathf.Clamp01(target / length));
                }

                target -= length;
            }

            return route[route.Length - 1];
        }
    }

    public sealed partial class AirsidePrototype
    {
        private const string EmuAirDecal = "Textures/Decals/dc_livery_emu_air_v01.png";
        private const string PlayerDecalTemplate = "Textures/Decals/dc_livery_airside_traffic_v01.png";

        /// <summary>Minimum look-ahead along a taxi route, so the nose points down it at walking pace.</summary>
        private const float GroundLookAheadSeconds = 1.5f;

        private readonly List<CommercialFlight> _fleetFlights = new();
        private readonly Dictionary<string, CommercialFlight> _fleetFlightById = new();
        private readonly Dictionary<string, FleetAircraft> _fleetAircraftById = new();
        private readonly Dictionary<string, int> _awaitingSlot = new();
        private readonly Dictionary<string, Texture2D> _tintedDecals = new();
        private string _fleetFollowSignature;

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
                UpdateAwaitingSlot(aircraft, visual);

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

        /// <summary>
        /// Awaiting-stand spots are held from the moment an arrival starts to vacate
        /// until it parks, so a queue moving up never makes an aircraft jump.
        /// </summary>
        private void UpdateAwaitingSlot(FleetAircraft aircraft, FleetVisual visual)
        {
            var id = aircraft.Registration;
            var needsSlot = visual.Leg is FleetGroundLeg.Vacate or FleetGroundLeg.AwaitingStand or FleetGroundLeg.TaxiIn;
            if (!needsSlot)
            {
                _awaitingSlot.Remove(id);
                return;
            }

            if (_awaitingSlot.ContainsKey(id))
                return;

            var slot = 0;
            while (_awaitingSlot.ContainsValue(slot))
                slot++;
            _awaitingSlot[id] = slot;
        }

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

        /// <summary>Eased 0..1 through the current ground leg, read at the fractional presentation clock.</summary>
        private float FleetLegProgress(FleetVisual visual, float lookAheadSeconds)
        {
            if (visual.LegSeconds <= 0)
                return visual.Leg == FleetGroundLeg.HoldingShort ? 1f : 0f;

            var elapsed = _preciseTime - visual.LegStartedAt.ElapsedSeconds + lookAheadSeconds;
            var linear = Mathf.Clamp01((float)(elapsed / visual.LegSeconds));
            return Mathf.SmoothStep(0f, 1f, linear);
        }

        /// <summary>Ground-leg progress in place of circuit phase progress; false for airborne phases.</summary>
        private bool TryFleetGroundProgress(CommercialFlight flight, float lookAheadSeconds, out float progress)
        {
            progress = 0f;
            if (!TryFleetGround(flight, out _, out var visual))
                return false;
            progress = FleetLegProgress(visual, lookAheadSeconds);
            return true;
        }

        /// <summary>World position on the Adelaide ground routes, or null when the circuit path applies.</summary>
        private Vector3? FleetGroundPosition(CommercialFlight flight, float lookAheadSeconds)
        {
            if (!TryFleetGround(flight, out var aircraft, out var visual))
                return null;

            if (lookAheadSeconds > 0f)
                lookAheadSeconds = Mathf.Max(lookAheadSeconds, GroundLookAheadSeconds);
            var t = FleetLegProgress(visual, lookAheadSeconds);
            return FleetGroundRoutes.Sample(RouteFor(aircraft, visual), t, out _);
        }

        /// <summary>
        /// Heading for a ground leg: tail-first on the pushback, and a fixed facing
        /// while parked or waiting so the aircraft never spins on the spot.
        /// </summary>
        private Vector3 FleetGroundFacing(CommercialFlight flight, Vector3 travel)
        {
            if (!TryFleetGround(flight, out var aircraft, out var visual))
                return travel;

            switch (visual.Leg)
            {
                case FleetGroundLeg.Parked:
                    return Vector3.forward;
                case FleetGroundLeg.HoldingShort:
                    return Vector3.back;
                case FleetGroundLeg.AwaitingStand:
                    return Vector3.left;
                case FleetGroundLeg.TaxiOut:
                    FleetGroundRoutes.Sample(RouteFor(aircraft, visual), FleetLegProgress(visual, 0f), out var segment);
                    return segment == 0 ? -travel : travel;
                default:
                    return travel;
            }
        }

        private Vector3[] RouteFor(FleetAircraft aircraft, FleetVisual visual)
        {
            _awaitingSlot.TryGetValue(aircraft.Registration, out var slot);
            var stand = aircraft.Stand.Value;
            return visual.Leg switch
            {
                FleetGroundLeg.Parked => new[] { FleetGroundRoutes.Bay(stand) },
                FleetGroundLeg.TaxiOut => FleetGroundRoutes.TaxiOut(aircraft.DepartureStand.Value),
                FleetGroundLeg.HoldingShort => new[] { FleetGroundRoutes.HoldingPoint },
                FleetGroundLeg.Lineup => FleetGroundRoutes.Lineup(),
                FleetGroundLeg.Vacate => FleetGroundRoutes.Vacate(slot),
                FleetGroundLeg.AwaitingStand => new[] { FleetGroundRoutes.AwaitingSpot(slot) },
                FleetGroundLeg.TaxiIn => FleetGroundRoutes.TaxiIn(slot, stand),
                _ => new[] { AirsideFlightPath.OnRunwayHold() }
            };
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

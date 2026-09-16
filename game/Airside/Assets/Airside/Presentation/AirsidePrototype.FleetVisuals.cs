using System;
using System.Collections.Generic;
using System.IO;
using Airside.Simulation;
using UnityEngine;

namespace Airside.Presentation
{
    public sealed partial class AirsidePrototype
    {
        private const string PlayerDecalTemplate = "Textures/Decals/dc_livery_airside_traffic_v01.png";

        private readonly List<CommercialFlight> _fleetFlights = new();
        private readonly Dictionary<string, CommercialFlight> _fleetFlightById = new();
        private readonly Dictionary<string, FleetAircraft> _fleetAircraftById = new();
        private readonly Dictionary<string, Transform> _fleetViewById = new();
        private readonly Dictionary<string, Texture2D> _tintedDecals = new();
        // Reused every frame: the follow set only changes when an aircraft appears or leaves.
        private readonly List<Transform> _fleetActiveViews = new();
        private Transform[] _fleetFollowTargets = Array.Empty<Transform>();

        /// <summary>Fleet departures roll from the real 05 threshold; the demo circuit from where it stopped.</summary>
        private float TakeoffOffsetX => FleetMode ? 0f : AirsideFlightPath.CircuitTakeoffOffsetX;

        /// <summary>Once an airline is running, the fleets replace the demo circuit on the field.</summary>
        private bool FleetMode => _operations != null;

        /// <summary>What the 3D field draws: the fleets in airline mode, else the demo circuit.</summary>
        private IReadOnlyList<CommercialFlight> VisualFlights => FleetMode ? _fleetFlights : _simulation.Flights;

        /// <summary>
        /// Rebuild the drawn flights from the fleets. Player aircraft come first so
        /// Follow picks your aircraft before the AI traffic.
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
        // The current-instant pose is asked for several times per aircraft per frame (position,
        // facing, speed readout); remember it for the frame rather than re-walking the leg.
        private readonly Dictionary<string, (int Frame, double Time, FleetGroundLeg Leg, long LegStart, GroundPose Pose)> _fleetPoseNow = new();

        private GroundPose FleetGroundPose(FleetAircraft aircraft, FleetVisual visual, float lookAheadSeconds)
        {
            if (lookAheadSeconds != 0f)
                return ComputeFleetGroundPose(aircraft, visual, lookAheadSeconds);

            var frame = Time.frameCount;
            var legStart = visual.LegStartedAt.ElapsedSeconds;
            if (_fleetPoseNow.TryGetValue(aircraft.Registration, out var cached)
                && cached.Frame == frame && cached.Time == _preciseTime
                && cached.Leg == visual.Leg && cached.LegStart == legStart)
                return cached.Pose;

            var pose = ComputeFleetGroundPose(aircraft, visual, 0f);
            _fleetPoseNow[aircraft.Registration] = (frame, _preciseTime, visual.Leg, legStart, pose);
            return pose;
        }

        private GroundPose ComputeFleetGroundPose(FleetAircraft aircraft, FleetVisual visual, float lookAheadSeconds)
        {
            switch (visual.Leg)
            {
                case FleetGroundLeg.Parked:
                    // Bay stop or terminal-gate nose stop — never a gate resolved as a bay.
                    return AdelaideGround.StandPose(aircraft.Stand);
                // Queue slots: two aircraft holding short, or two arrivals waiting for a stand,
                // used to be drawn on the same spot, one inside the other.
                case FleetGroundLeg.HoldingShort:
                    return AdelaideGround.HoldingShortPose(aircraft.DepartureStand,
                        FleetVisual.QueueSlot(_operations.Fleet, aircraft));
                case FleetGroundLeg.AwaitingStand:
                    return AdelaideGround.AwaitingPose(FleetVisual.QueueSlot(_operations.Fleet, aircraft));
                default:
                {
                    var leg = visual.Leg switch
                    {
                        FleetGroundLeg.TaxiOut => AdelaideGround.TaxiOut(aircraft.DepartureStand, aircraft.Type),
                        FleetGroundLeg.Lineup => AdelaideGround.Lineup,
                        FleetGroundLeg.Vacate => AdelaideGround.VacateFor(aircraft.Type),
                        _ => AdelaideGround.TaxiIn(aircraft.Stand, aircraft.Type)
                    };
                    var elapsed = _preciseTime - visual.LegStartedAt.ElapsedSeconds + lookAheadSeconds;
                    var scale = visual.LegSeconds > 0 ? leg.Seconds / visual.LegSeconds : 1.0;
                    return HumanGroundPose(aircraft, visual.Leg, leg.PoseAt(elapsed * scale));
                }
            }
        }

        /// <summary>
        /// Small, smooth tracking corrections keep taxiing from looking rail-guided.
        /// They are deterministic per registration, stay well inside the pavement and
        /// disappear when stopped; there is no frame-to-frame random wobble.
        /// </summary>
        private GroundPose HumanGroundPose(FleetAircraft aircraft, FleetGroundLeg leg, GroundPose pose)
        {
            if (pose.Speed < 0.5f || leg is not (FleetGroundLeg.TaxiOut or FleetGroundLeg.TaxiIn or FleetGroundLeg.Lineup or FleetGroundLeg.Vacate))
                return pose;

            var seed = StableRegistrationHash(aircraft.Registration);
            var phase = (seed % 997) * 0.013f;
            var wave = Mathf.Sin((float)_preciseTime * 0.12f + phase)
                       + 0.35f * Mathf.Sin((float)_preciseTime * 0.037f + phase * 1.7f);
            var offset = wave * (leg is FleetGroundLeg.Lineup or FleetGroundLeg.Vacate ? 0.08f : 0.22f);
            var normalX = -pose.NoseZ;
            var normalZ = pose.NoseX;
            var headingBias = Mathf.Sin((float)_preciseTime * 0.09f + phase * 0.7f) * 0.7f * Mathf.Deg2Rad;
            var cos = Mathf.Cos(headingBias);
            var sin = Mathf.Sin(headingBias);
            var noseX = pose.NoseX * cos + pose.NoseZ * sin;
            var noseZ = -pose.NoseX * sin + pose.NoseZ * cos;
            return new GroundPose(pose.X + normalX * offset, pose.Z + normalZ * offset,
                noseX, noseZ, pose.Speed, pose.TailFirst);
        }

        private static int StableRegistrationHash(string value)
        {
            unchecked
            {
                var hash = 23;
                foreach (var ch in value ?? string.Empty)
                    hash = hash * 31 + ch;
                return hash & int.MaxValue;
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

        /// <summary>Visual steering angle from the current and near-future authored taxi poses.</summary>
        private float FleetNoseWheelSteering(CommercialFlight flight, float wheelbaseMetres)
        {
            if (!TryFleetGround(flight, out var aircraft, out var visual)
                || visual.Leg is not (FleetGroundLeg.TaxiOut or FleetGroundLeg.TaxiIn
                    or FleetGroundLeg.Lineup or FleetGroundLeg.Vacate))
                return 0f;

            const float lookAhead = 1.5f;
            var current = FleetGroundPose(aircraft, visual, 0f);
            var future = FleetGroundPose(aircraft, visual, lookAhead);
            return AirsideReusableMotion.NoseWheelSteerDegrees(
                current.NoseX, current.NoseZ, future.NoseX, future.NoseZ,
                current.Speed, lookAhead, wheelbaseMetres, current.TailFirst);
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
            var view = BuildAircraftForType($"Commercial {aircraftId}", aircraft.Type, accent, null);

            // The same neutral skin sheet works across every authored type. Repainting
            // it here gives AI traffic a coherent operator colour instead of leaving
            // Rex, QantasLink and Virgin in the old generic blue traffic texture.
            var decal = TintedLiveryDecal(airline.LiveryHex, accent);
            if (decal != null)
                ApplyLiveryTexture(view, decal);

            var namedChildren1 = AirsideNamedChildren.Get(view);
            var childNames1 = AirsideNamedChildren.Names(view);
            for (var childIndex1 = 0; childIndex1 < namedChildren1.Length; childIndex1++)
            {
                var child = namedChildren1[childIndex1];
                var childName = childNames1[childIndex1];
                if (!childName.StartsWith("Livery", StringComparison.Ordinal))
                    continue;
                var renderer = child.GetComponent<Renderer>();
                if (renderer != null)
                    SetRendererColor(renderer, accent);
            }

            EnsureAircraftIdentityMarkings(view, aircraft, accent);

            return view;
        }

        /// <summary>
        /// The neutral traffic livery with its blue bands repainted in the player's
        /// colour: darker blues take the colour, lighter ones a paler tint of it.
        /// </summary>
        private Texture2D TintedLiveryDecal(string hex, Color accent)
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

        /// <summary>
        /// Add readable, depth-tested operator and registration paint to both sides of
        /// the fuselage. TextMesh is used as geometry rather than a screen overlay so
        /// the marks correctly disappear behind wings, buildings and the aircraft body.
        /// </summary>
        private static void EnsureAircraftIdentityMarkings(
            Transform aircraftView,
            FleetAircraft aircraft,
            Color operatorColour)
        {
            if (aircraftView == null || aircraft == null || aircraft.Airline == null)
                return;

            var layout = AircraftIdentityMarkings.For(aircraft.Type);
            var operatorText = aircraft.Airline.Name.ToUpperInvariant();
            for (var side = -1; side <= 1; side += 2)
            {
                AddAircraftIdentityText(
                    aircraftView,
                    side < 0 ? "Operator title L" : "Operator title R",
                    operatorText,
                    new Vector3(side * layout.SideX, layout.OperatorY, layout.OperatorZ),
                    side,
                    layout.OperatorCharacterSize,
                    operatorColour,
                    FontStyle.Bold);
                AddAircraftIdentityText(
                    aircraftView,
                    side < 0 ? "Registration L" : "Registration R",
                    aircraft.Registration,
                    new Vector3(side * layout.SideX, layout.RegistrationY, layout.RegistrationZ),
                    side,
                    layout.RegistrationCharacterSize,
                    new Color(0.10f, 0.12f, 0.14f),
                    FontStyle.Normal);
            }

            AirsideNamedChildren.Forget(aircraftView);
        }

        private static void AddAircraftIdentityText(
            Transform parent,
            string name,
            string value,
            Vector3 localPosition,
            int side,
            float characterSize,
            Color colour,
            FontStyle style)
        {
            var label = new GameObject(name);
            label.transform.SetParent(parent, false);
            label.transform.localPosition = localPosition;
            // TextMesh's readable face points along local -Z (the same reason stand
            // identifiers use +90 degrees around X to face upward). Turn that face
            // outward from each side of the fuselage rather than into its skin.
            label.transform.localRotation = Quaternion.Euler(0f, side < 0 ? 90f : -90f, 0f);

            var text = label.AddComponent<TextMesh>();
            text.text = value;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = 64;
            text.characterSize = characterSize;
            text.fontStyle = style;
            text.color = colour;

            var renderer = label.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            DepthTestStandLabel(renderer);
        }

        /// <summary>
        /// Invisible pick volume + selection ring for fleet aircraft. Away (hidden)
        /// aircraft never receive a proxy, so they cannot be selected in 3D.
        /// </summary>
        private void EnsureFleetPickables(Transform[] views)
        {
            if (views == null)
                return;

            for (var i = 0; i < views.Length; i++)
            {
                var view = views[i];
                if (view == null || !view.gameObject.activeSelf)
                    continue;
                if (!AircraftPickRouting.TryRegistrationFromViewName(view.name, out var registration))
                    continue;
                if (!_fleetViewById.ContainsKey(registration))
                    continue;

                AircraftPickProxy.Ensure(view, registration);
                EnsureSelectionMarker(view);
                ForgetAircraftViewParts(view);
                // Both calls above parent new children under the view. AirsideNamedChildren
                // caches the child array for the life of the object and nothing was dropping
                // it, so every later name lookup ran against a hierarchy snapshot taken
                // before the pick proxy and selection marker existed.
                AirsideNamedChildren.Forget(view);
            }
        }

        private static void EnsureSelectionMarker(Transform aircraft)
        {
            if (aircraft == null || aircraft.Find(AircraftPickRouting.MarkerChildName) != null)
                return;

            var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = AircraftPickRouting.MarkerChildName;
            UnityEngine.Object.Destroy(marker.GetComponent<Collider>());
            marker.transform.SetParent(aircraft, false);
            marker.transform.localPosition = new Vector3(0f, -0.4f, 0f);
            marker.transform.localRotation = Quaternion.identity;
            var profile = aircraft.GetComponent<AircraftVisualProfileComponent>();
            var diameter = profile != null
                ? profile.SelectionMarkerDiameterMetres
                : 18f;
            marker.transform.localScale = new Vector3(diameter, 0.02f, diameter);

            var colour = new Color(AirsideTheme.CoastalBlue.r, AirsideTheme.CoastalBlue.g, AirsideTheme.CoastalBlue.b, 0.55f);
            var material = AirsideMaterialLibrary.CreateShared(colour, AirsideMaterialLibrary.SurfaceKind.Default);
            var renderer = marker.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            SetRendererColor(renderer, colour);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            marker.SetActive(false);
        }

        private void UpdateSelectionMarker(Transform aircraft, string aircraftId)
        {
            if (aircraft == null)
                return;
            var parts = PartsFor(aircraft);
            var marker = parts.Marker;
            if (marker == null)
                return;
            var profile = parts.Profile;

            var selected = !string.IsNullOrEmpty(_selectedAircraftId) && _selectedAircraftId == aircraftId;
            if (marker.gameObject.activeSelf != selected)
                marker.gameObject.SetActive(selected);
            if (!selected)
                return;

            var groundY = AirsideBareField.RunwayCenterY + AirsideBareField.RunwayHeightMetres * 0.5f + 0.04f;
            var visualCentre = profile != null
                ? aircraft.TransformPoint(profile.VisualCentreOffsetMetres)
                : aircraft.position;
            marker.position = new Vector3(visualCentre.x, groundY, visualCentre.z);
            marker.rotation = Quaternion.identity;
            // Soft pulse so the selected aircraft reads at overview distance.
            var pulse = 0.72f + 0.28f * Mathf.PingPong(Time.unscaledTime * 1.4f, 1f);
            var baseDiameter = profile != null
                ? profile.SelectionMarkerDiameterMetres
                : 18f;
            var scale = baseDiameter * (0.88f + 0.12f * pulse);
            var sx = aircraft.lossyScale.x > 0.001f ? scale / aircraft.lossyScale.x : scale;
            var sy = aircraft.lossyScale.y > 0.001f ? 0.04f / aircraft.lossyScale.y : 0.04f;
            var sz = aircraft.lossyScale.z > 0.001f ? scale / aircraft.lossyScale.z : scale;
            marker.localScale = new Vector3(sx, sy, sz);

            var renderer = parts.MarkerRenderer;
            if (renderer == null)
                return;
            var colour = Color.Lerp(AirsideTheme.CoastalBlue, AirsideTheme.SafetyYellow, 0.35f);
            colour.a = 0.4f + 0.25f * pulse;
            SetRendererColor(renderer, colour);
        }

        /// <summary>Follow cycles through what is on the field, refreshed as aircraft come and go.</summary>
        private void RefreshFleetFollowTargets(Transform[] views)
        {
            if (_cameraController == null)
                return;

            _fleetActiveViews.Clear();
            _fleetViewById.Clear();
            for (var i = 0; i < views.Length; i++)
            {
                var view = views[i];
                if (view == null || !view.gameObject.activeSelf)
                    continue;
                if (i < VisualFlights.Count
                    && VisualFlights[i].Operation.Phase == AircraftPhase.Approach
                    && !AircraftPickRouting.ApproachIsCloseEnough(
                        view.position.x, AirsideFlightPath.WestThresholdX))
                    continue;
                _fleetActiveViews.Add(view);
                if (i < VisualFlights.Count)
                    _fleetViewById[VisualFlights[i].AircraftId] = view;
            }

            if (SameTransforms(_fleetActiveViews, _fleetFollowTargets))
                return;

            // Pick proxies and markers live on the views, so they only need checking when
            // the set changes — not with a name lookup and child search every frame.
            EnsureFleetPickables(views);
            _fleetFollowTargets = _fleetActiveViews.ToArray();
            _cameraController.SetFollowTargets(_fleetFollowTargets);
        }

        private static bool SameTransforms(List<Transform> current, Transform[] previous)
        {
            if (current.Count != previous.Length)
                return false;
            for (var i = 0; i < previous.Length; i++)
                if (current[i] != previous[i])
                    return false;
            return true;
        }

        private bool TryFollowFleetAircraft(string aircraftId)
        {
            return _cameraController != null
                && _fleetViewById.TryGetValue(aircraftId, out var view)
                && _cameraController.StartFollow(view);
        }

        /// <summary>
        /// Direct 3D pick: a field click that was not a pan. Off-field aircraft have no
        /// proxy and are never returned by the raycast.
        /// </summary>
        private void TrySelectAircraftAtScreen(Vector2 inputSystemPosition)
        {
            if (!FleetMode || AirlineSetupOpen || _awaySummary != null || IntroActive || _menuOpen)
                return;
            if (_cameraController == null)
                return;

            var camera = _cameraController.GetComponent<Camera>();
            if (camera == null)
                return;

            // Auto-sync is off in DynamicsManager, so colliders still sit where the last physics
            // step left them; a departure moves a metre or more between steps. Pick against
            // where the aircraft are drawn this frame.
            Physics.SyncTransforms();
            var ray = camera.ScreenPointToRay(inputSystemPosition);
            var layer = LayerMask.NameToLayer(AircraftPickRouting.PickLayerName);
            var mask = layer >= 0 ? 1 << layer : ~0;
            var hits = Physics.RaycastAll(ray, AirsideBareField.MaxOrbitDistance * 2f, mask, QueryTriggerInteraction.Collide);
            if (hits == null || hits.Length == 0)
            {
                DeselectOnEmptyFieldClick();
                return;
            }

            var candidates = new List<AircraftPickHit>(hits.Length);
            for (var i = 0; i < hits.Length; i++)
            {
                var proxy = hits[i].collider != null
                    ? hits[i].collider.GetComponentInParent<AircraftPickProxy>()
                    : null;
                if (proxy == null || string.IsNullOrEmpty(proxy.AircraftId))
                    continue;
                var selectable = _fleetViewById.ContainsKey(proxy.AircraftId);
                candidates.Add(new AircraftPickHit(proxy.AircraftId, hits[i].distance, selectable));
            }

            var id = AircraftPickRouting.ResolveNearest(candidates);

            if (id == null || !_fleetAircraftById.TryGetValue(id, out var aircraft))
            {
                DeselectOnEmptyFieldClick();
                return;
            }

            SelectAircraft(aircraft);
        }

        /// <summary>
        /// A plain click on open ground lets go of the selection, like Esc, but leaves the
        /// camera and any open overlay where they are.
        /// </summary>
        private void DeselectOnEmptyFieldClick()
        {
            if (string.IsNullOrEmpty(_selectedAircraftId))
                return;
            _selectedAircraftId = null;
            PlayUiClick();
        }
    }
}

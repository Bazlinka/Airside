using System;
using System.Collections.Generic;
using System.IO;
using Airside.Domain;
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
        // Reused every click instead of a fresh List<AircraftPickHit> per raycast.
        private readonly List<AircraftPickHit> _pickCandidates = new();

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

                // An inbound already on the drawn extended final flies it as an approach.
                var phase = !visual.Visible && IsArrivingOnFinal(aircraft) ? AircraftPhase.Approach : visual.Phase;
                if (flight.Operation.Phase != phase || !flight.Operation.PhaseStartedAt.Equals(visual.PhaseStartedAt))
                    flight.Operation = AircraftOperation.InPhase(id, phase, visual.PhaseStartedAt);

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
            && (FleetVisual.For(aircraft, _clock.Now).Visible || IsArrivingOnFinal(aircraft));

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

            var pose = QueueShuffle(aircraft, visual.Leg, ComputeFleetGroundPose(aircraft, visual, 0f));
            _fleetPoseNow[aircraft.Registration] = (frame, _preciseTime, visual.Leg, legStart, pose);
            return pose;
        }

        private readonly Dictionary<string, (double Time, GroundPose Pose)> _queueShown = new();

        /// <summary>Queue shuffles move up at taxi pace instead of jumping a whole queue place.</summary>
        private const float QueueShuffleMetresPerSecond = 5f;

        /// <summary>
        /// A queue at the holding point or a runway exit moves up one place (60 m) the instant the
        /// aircraft in front goes. Ease the drawn aircraft there at taxi pace instead of teleporting.
        /// Only short hops in the queueing legs are eased; everything else passes straight through.
        /// </summary>
        private GroundPose QueueShuffle(FleetAircraft aircraft, FleetGroundLeg leg, GroundPose target)
        {
            var queueing = leg is FleetGroundLeg.HoldingShort or FleetGroundLeg.AwaitingStand or FleetGroundLeg.TaxiOut
                or FleetGroundLeg.Vacate;
            if (!queueing || !_queueShown.TryGetValue(aircraft.Registration, out var shown))
            {
                _queueShown[aircraft.Registration] = (_preciseTime, target);
                return target;
            }

            var dt = Math.Max(0.0, _preciseTime - shown.Time);
            var dx = target.X - shown.Pose.X;
            var dz = target.Z - shown.Pose.Z;
            var gap = (float)Math.Sqrt(dx * dx + dz * dz);
            var reach = (float)(Math.Max(QueueShuffleMetresPerSecond, target.Speed * 1.2f) * dt) + 0.05f;
            if (gap <= reach || gap > 2.5f * AdelaideGround.AwaitingSpacingMetres)
            {
                _queueShown[aircraft.Registration] = (_preciseTime, target);
                return target;
            }

            var step = reach / gap;
            var moving = new GroundPose(shown.Pose.X + dx * step, shown.Pose.Z + dz * step,
                dx / gap, dz / gap, QueueShuffleMetresPerSecond, false);
            _queueShown[aircraft.Registration] = (_preciseTime, moving);
            return moving;
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
                        FleetVisual.QueueSlot(_operations.Fleet, aircraft, _clock.Now), aircraft.AssignedRunway,
                        aircraft.Type);
                case FleetGroundLeg.AwaitingStand:
                    return AdelaideGround.AwaitingPose(FleetVisual.QueueSlot(_operations.Fleet, aircraft, _clock.Now),
                        aircraft.Type, aircraft.AssignedRunway);
                default:
                {
                    var leg = visual.Leg switch
                    {
                        FleetGroundLeg.TaxiOut => AdelaideGround.TaxiOut(aircraft.DepartureStand, aircraft.Type, aircraft.AssignedRunway),
                        FleetGroundLeg.Lineup => AdelaideGround.LineupFor(aircraft.AssignedRunway),
                        FleetGroundLeg.Vacate => AdelaideGround.VacateFor(aircraft.Type, aircraft.AssignedRunway),
                        _ => AdelaideGround.TaxiIn(aircraft.Stand, aircraft.Type, aircraft.AssignedRunway)
                    };
                    var elapsed = _preciseTime - visual.LegStartedAt.ElapsedSeconds + lookAheadSeconds;
                    var scale = visual.LegSeconds > 0 ? leg.Seconds / visual.LegSeconds : 1.0;
                    var t = elapsed * scale;
                    // Stop behind whoever is already holding short (GroundTraffic.TryPose does the same).
                    if (visual.Leg == FleetGroundLeg.TaxiOut)
                        t = Math.Min(t, GroundTraffic.QueuedSeconds(leg,
                            FleetVisual.QueueAhead(_operations.Fleet, aircraft, _clock.Now)));
                    else if (visual.Leg == FleetGroundLeg.Vacate)
                        t = Math.Min(t, GroundTraffic.QueuedSeconds(leg,
                            FleetVisual.ExitQueueAhead(_operations.Fleet, aircraft, _clock.Now)));
                    return HumanGroundPose(aircraft, visual.Leg, leg.PoseAt(t));
                }
            }
        }

        /// <summary>
        /// Tracking corrections keep taxiing from looking perfectly rail-guided, but stay
        /// subtle: a few centimetres of weave and about a degree of heading bias. A larger
        /// weave read as wandering off the centreline. Deterministic per registration; no
        /// frame-to-frame wobble. Pushback is excluded (tail-first legs never reach here).
        /// </summary>
        private GroundPose HumanGroundPose(FleetAircraft aircraft, FleetGroundLeg leg, GroundPose pose)
        {
            if (pose.Speed < 0.15f || leg is not (FleetGroundLeg.TaxiOut or FleetGroundLeg.TaxiIn or FleetGroundLeg.Lineup or FleetGroundLeg.Vacate))
                return pose;

            // Fade the weave in with speed. A hard on/off at 0.15 m/s popped the airframe up to
            // half a metre sideways (and a couple of degrees round) as it set off or stopped.
            var fade = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.15f, 2.5f, pose.Speed));
            var seed = StableRegistrationHash(aircraft.Registration);
            var phase = (seed % 997) * 0.013f;
            var wave = Mathf.Sin((float)_preciseTime * 0.09f + phase)
                       + 0.45f * Mathf.Sin((float)_preciseTime * 0.031f + phase * 1.7f)
                       + 0.18f * Mathf.Sin((float)_preciseTime * 0.19f + phase * 0.4f);
            // Was 0.55 m / 2.2° — felt like drifting off the taxiway. Keep a hint of life only.
            var offset = wave * (leg is FleetGroundLeg.Lineup or FleetGroundLeg.Vacate ? 0.06f : 0.18f) * fade;
            var normalX = -pose.NoseZ;
            var normalZ = pose.NoseX;
            var headingBias = Mathf.Sin((float)_preciseTime * 0.07f + phase * 0.7f) * 0.9f * Mathf.Deg2Rad * fade;
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

        /// <summary>
        /// Signed speed the tyres roll at: the real Adelaide ground-leg pose speed where one
        /// applies, negative on the tail-first pushback, else the circuit's phase speed.
        /// </summary>
        private float FleetTireRollSpeed(CommercialFlight flight, AircraftPhase phase, float progress,
            AircraftType type)
        {
            float? legSpeed = null;
            var tailFirst = false;
            if (TryFleetGround(flight, out var aircraft, out var visual))
            {
                var pose = FleetGroundPose(aircraft, visual, 0f);
                legSpeed = pose.Speed;
                tailFirst = pose.TailFirst;
            }

            return AirsideAircraftParts.TireRollMetresPerSecond(legSpeed, tailFirst,
                AirsideFlightPath.GroundSpeedMetresPerSecond(phase, progress, type));
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
                        if (p.a < 8 || p.b - p.r < 18)
                            continue;
                        var lightness = Mathf.InverseLerp(90f, 190f, (p.r + p.g + p.b) / 3f);
                        var colour = Color.Lerp(accent, Color.white, lightness * 0.28f);
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
            var operatorText = aircraft.Airline.FuselageTitle;
            // A long airline name is painted smaller rather than off the end of the fuselage.
            var operatorSize = AircraftTitlePaint.OperatorCharacterSize(
                aircraft.Type, operatorText, layout.OperatorCharacterSize);
            var (r, g, b) = aircraft.Airline.LiveryRgb();
            var titleColour = AircraftTitlePaint.AccentReadsOnWhiteMetal(r, g, b)
                ? operatorColour
                : RegistrationInk;
            // One tracker drives all four labels' side visibility per aircraft (was one
            // MonoBehaviour per label, each independently resolving Camera.main and
            // re-deriving the same aircraft-relative camera side every LateUpdate).
            var sideVisibility = aircraftView.gameObject.AddComponent<AircraftIdentitySideVisibility>();
            sideVisibility.Initialise(aircraftView);
            for (var side = -1; side <= 1; side += 2)
            {
                AddAircraftIdentityText(
                    aircraftView,
                    side < 0 ? "Operator title L" : "Operator title R",
                    operatorText,
                    new Vector3(side * layout.SideX, layout.OperatorY, layout.OperatorZ),
                    side,
                    operatorSize,
                    titleColour,
                    FontStyle.Bold,
                    sideVisibility);
                AddAircraftIdentityText(
                    aircraftView,
                    side < 0 ? "Registration L" : "Registration R",
                    aircraft.Registration,
                    new Vector3(side * layout.SideX, layout.RegistrationY, layout.RegistrationZ),
                    side,
                    layout.RegistrationCharacterSize,
                    RegistrationInk,
                    FontStyle.Normal,
                    sideVisibility);
            }

            AirsideNamedChildren.Forget(aircraftView);
        }

        /// <summary>
        /// Registration paint: a dark charcoal, the way a real registration is stencilled on
        /// a light fuselage. It used to be near-black (0.10, 0.12, 0.14) on a near-black
        /// backing plate, so the two cancelled out wherever they overlapped.
        /// </summary>
        private static readonly Color RegistrationInk = new(0.16f, 0.18f, 0.20f);

        private static void AddAircraftIdentityText(
            Transform parent,
            string name,
            string value,
            Vector3 localPosition,
            int side,
            float characterSize,
            Color colour,
            FontStyle style,
            AircraftIdentitySideVisibility sideVisibility)
        {
            var label = new GameObject(name);
            label.transform.SetParent(parent, false);
            label.transform.localPosition = localPosition;
            // TextMesh's readable face points along local -Z. Turn that face
            // outward from each side of the fuselage rather than into its skin.
            label.transform.localRotation = Quaternion.Euler(0f, side < 0 ? 90f : -90f, 0f);

            var text = label.AddComponent<TextMesh>();
            text.text = value;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = AircraftTitlePaint.FontPixelSize;
            text.characterSize = characterSize;
            text.fontStyle = style;
            text.color = colour;
            text.richText = false;

            var renderer = label.GetComponent<MeshRenderer>();
            renderer.sortingOrder = 2;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            DepthTestStandLabel(renderer);

            // The legacy font shader is double-sided. Keep only the camera-facing
            // fuselage title enabled so the opposite title cannot appear backwards
            // through the top of the aircraft in elevated follow views.
            sideVisibility.Register(text, renderer, side, colour);
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
            var followed = _cameraController.FollowTarget;
            for (var i = 0; i < views.Length; i++)
            {
                var view = views[i];
                if (view == null || !view.gameObject.activeSelf)
                    continue;
                // A far-approach aircraft is excluded as a new cycling/pick candidate (too
                // small and distant to be a sensible target) but never dropped out from
                // under a follow already in progress — that used to release the camera the
                // instant an inbound aircraft you were following entered its Approach phase,
                // well before it was anywhere near the runway, reading as a stutter/glitch.
                if (view != followed
                    && i < VisualFlights.Count
                    && VisualFlights[i].Operation.Phase == AircraftPhase.Approach
                    && !ApproachCloseEnough(VisualFlights[i], view.position))
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

        private bool ApproachCloseEnough(CommercialFlight flight, Vector3 worldPosition)
        {
            if (_fleetAircraftById.TryGetValue(flight.AircraftId, out var aircraft))
                return AircraftPickRouting.ApproachIsCloseEnough(
                    worldPosition.x, worldPosition.z, aircraft.AssignedRunway);
            return AircraftPickRouting.ApproachIsCloseEnough(
                worldPosition.x, AirsideFlightPath.WestThresholdX);
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

            _pickCandidates.Clear();
            for (var i = 0; i < hits.Length; i++)
            {
                var proxy = hits[i].collider != null
                    ? hits[i].collider.GetComponentInParent<AircraftPickProxy>()
                    : null;
                if (proxy == null || string.IsNullOrEmpty(proxy.AircraftId))
                    continue;
                var selectable = _fleetViewById.ContainsKey(proxy.AircraftId);
                _pickCandidates.Add(new AircraftPickHit(proxy.AircraftId, hits[i].distance, selectable));
            }

            var id = AircraftPickRouting.ResolveNearest(_pickCandidates);

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

    /// <summary>
    /// Drives camera-facing visibility for every identity label (operator title and
    /// registration, both sides) on one aircraft from a single LateUpdate. Previously
    /// each of the 4 labels carried its own tracker, so every aircraft paid for 4
    /// independent Camera.main resolutions and 4 identical InverseTransformPoint calls
    /// per frame; this does one of each per aircraft and shares one cached main-camera
    /// lookup across the whole fleet.
    /// </summary>
    internal sealed class AircraftIdentitySideVisibility : MonoBehaviour
    {
        private static Camera _cachedCamera;
        private static int _cachedCameraFrame = -1;

        private Transform _aircraft;
        private readonly List<TextMesh> _labels = new();
        private readonly List<Renderer> _renderers = new();
        private readonly List<int> _sides = new();
        private readonly List<Color> _baseColours = new();

        public void Initialise(Transform aircraft)
        {
            _aircraft = aircraft;
        }

        public void Register(TextMesh label, Renderer labelRenderer, int side, Color baseColour)
        {
            _labels.Add(label);
            _renderers.Add(labelRenderer);
            _sides.Add(side);
            _baseColours.Add(baseColour);
            RefreshOne(_renderers.Count - 1, ResolveCamera());
        }

        private void LateUpdate() => Refresh();

        private static Camera ResolveCamera()
        {
            var frame = Time.frameCount;
            if (_cachedCameraFrame != frame || _cachedCamera == null)
            {
                _cachedCamera = Camera.main;
                _cachedCameraFrame = frame;
            }
            return _cachedCamera;
        }

        private void Refresh()
        {
            var camera = ResolveCamera();
            if (_aircraft == null || camera == null)
                return;
            var cameraSide = _aircraft.InverseTransformPoint(camera.transform.position).x < 0f ? -1 : 1;
            var tint = DaylightTint();
            for (var i = 0; i < _renderers.Count; i++)
            {
                if (_renderers[i] == null)
                    continue;
                _renderers[i].enabled = cameraSide == _sides[i];
                ApplyTint(i, tint);
            }
        }

        private void RefreshOne(int index, Camera camera)
        {
            if (_aircraft == null || camera == null || _renderers[index] == null)
                return;
            var cameraSide = _aircraft.InverseTransformPoint(camera.transform.position).x < 0f ? -1 : 1;
            _renderers[index].enabled = cameraSide == _sides[index];
            ApplyTint(index, DaylightTint());
        }

        // Real fuselage titles are paint, not a decal light — they should dim/warm with the
        // same day/night grade as the rest of the airframe instead of sitting unlit-bright
        // at night. Floored well above zero so they stay legible under apron floodlight.
        private static float DaylightTint() => Mathf.Lerp(0.4f, 1f, AirsidePrototype.CurrentDaylight);

        private void ApplyTint(int index, float tint)
        {
            if (_labels[index] == null)
                return;
            var baseColour = _baseColours[index];
            _labels[index].color = new Color(baseColour.r * tint, baseColour.g * tint, baseColour.b * tint, baseColour.a);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Airside.Domain;
using Airside.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace Airside.Presentation
{
    public sealed partial class AirsidePrototype
    {
        private Transform BuildAmbientVehicle(string kind)
        {
            var vehicle = kind == "Fuel truck"
                ? BuildServiceVehicle("Fuel truck (AI)", new Color(0.95f, 0.76f, 0.12f), new Vector3(3.1f, 1.25f, 1.35f),
                    PreferArtKit(
                        "Models/Vehicles/mdl_fuel_truck_small_v07.gltf",
                        "Models/Vehicles/mdl_fuel_truck_small_v06.gltf",
                        "Models/Vehicles/mdl_fuel_truck_small_v05.gltf",
                        "Models/Vehicles/mdl_fuel_truck_small_authored_v01.gltf"))
                : BuildServiceVehicle("Baggage cart (AI)", new Color(0.91f, 0.38f, 0.12f), new Vector3(2.3f, 0.8f, 1.15f),
                    PreferArtKit(
                        "Models/Vehicles/mdl_baggage_tug_train_v07.gltf",
                        "Models/Vehicles/mdl_baggage_tug_train_v06.gltf",
                        "Models/Vehicles/mdl_baggage_tug_train_v05.gltf",
                        "Models/Vehicles/mdl_baggage_tug_train_authored_v01.gltf"));
            OrientPlusXKitToForward(vehicle);
            return vehicle;
        }

        private void UpdatePlayerTurnaroundServicing(FleetAircraft aircraft)
        {
            var prep = DeparturePrep.For(aircraft, _clock.Now, _operations.CareerState.BaseLevel);
            var pose = AdelaideGround.StandPose(aircraft.Stand);
            var nose = new Vector3(pose.NoseX, 0f, pose.NoseZ);
            var side = new Vector3(-nose.z, 0f, nose.x);
            var stop = new Vector3(pose.X, AirsideFlightPath.GroundY, pose.Z);
            // Where each vehicle works comes from this type's own layout (ADR 0175): the fuel
            // truck beside the right wing, the hi-loader at the service door, the bag train at
            // the hold. A turboprop is catered by hand, so its catering truck stays at the depot.
            var layout = AircraftLayout.For(aircraft.Type);
            var fuelService = LayoutToWorld(pose, layout.FuelTruck, stop.y);
            var cateringService = layout.CateringTruck is { } hiLoader ? LayoutToWorld(pose, hiLoader, stop.y) : stop;
            var baggageService = LayoutToWorld(pose, layout.BaggageTrain, stop.y);

            // Vehicles drive the real airside frontage road in and out (ADR 0115) rather than
            // appearing beside the aircraft when their stage starts. GroundServiceRun owns the
            // trip; this only draws it.
            DriveServiceVehicle(_fuelTruck, GroundServiceKind.Fuel, aircraft, prep, fuelService);
            if (layout.CateredByTruck)
            {
                DriveServiceVehicle(_cateringTruck, GroundServiceKind.Catering, aircraft, prep, cateringService);
                // Once alongside, the hi-loader squares up to the service door cab-first: its
                // platform is over the cab and bridges to the door (ADR 0176).
                if (_cateringTruck != null && _cateringTruck.gameObject.activeSelf
                    && Flat(_cateringTruck.position - cateringService).sqrMagnitude < 0.36f
                    && layout.CateringDoor is { } serviceDoor)
                {
                    var inward = new Vector3(nose.z, 0f, -nose.x) * -AircraftLayout.SideOf(serviceDoor);
                    _cateringTruck.rotation = Quaternion.Slerp(_cateringTruck.rotation,
                        Quaternion.LookRotation(inward, Vector3.up), Time.unscaledDeltaTime * 3f);
                }
            }
            else
                SetEquipmentVisible(_cateringTruck, false);
            DriveServiceVehicle(_baggageCart, GroundServiceKind.Baggage, aircraft, prep, baggageService);

            // Chocks at the nose gear and the ground power cart by the nose for the whole turn
            // (ADR 0126); built since the first turnaround pass and never placed until now.
            var facing = Quaternion.LookRotation(nose.sqrMagnitude > 0.001f ? nose : Vector3.forward);
            PlaceBoardingStairs(_chocks, true, stop, facing);
            PlaceBoardingStairs(_gpuCart, true, stop - nose * 4f + side * 3.5f, facing);

            // Fleet-specific stair trucks and remote buses are owned by Boarding.cs. The old
            // shared views would overlap those when two aircraft board at once.
            SetEquipmentVisible(_passengerBus, false);
            SetEquipmentVisible(_stairs, false);
        }

        /// <summary>
        /// Draw one ground vehicle where <see cref="GroundServiceRun"/> says it is: parked at
        /// its depot, driving the frontage road (including under the terminal), or working
        /// beside the aircraft. Presentation only — the trip never gates the simulation.
        /// </summary>
        private void DriveServiceVehicle(Transform vehicle, GroundServiceKind kind,
            FleetAircraft aircraft, DeparturePrepStatus prep, Vector3 servicePosition)
        {
            if (vehicle == null)
                return;

            var stage = kind switch
            {
                GroundServiceKind.Fuel => DeparturePrepStage.Fuel,
                GroundServiceKind.Catering => DeparturePrepStage.Catering,
                _ => DeparturePrepStage.Baggage
            };
            var untilStage = DeparturePrep.SecondsUntilStage(
                aircraft, _clock.Now, _operations.CareerState.BaseLevel, stage);
            var run = GroundServiceRun.For(kind, prep, servicePosition.x, servicePosition.z, untilStage);

            if (!run.Visible)
            {
                vehicle.gameObject.SetActive(false);
                return;
            }

            vehicle.gameObject.SetActive(true);
            if (!run.Driving)
            {
                UpdateVehicle(vehicle, true, servicePosition, servicePosition);
                return;
            }

            // Steer by sampling a little further along the same leg, so the vehicle faces the
            // way it is going through the frontage's bends instead of snapping at each point.
            var here = new Vector3(run.RoadX, AirsideFlightPath.GroundY, run.RoadZ);
            var target = run.Phase == GroundServicePhase.Outbound ? servicePosition : here;
            // On the road the road is the route (it runs under the terminal); only a vehicle that
            // has to get back to it from the aircraft is routed round what is in the way.
            var offRoad = Flat(vehicle.position - here).sqrMagnitude > 12f * 12f;
            UpdateVehicle(vehicle, true, here, target, routed: offRoad);
        }

        private static void PlaceBoardingStairs(Transform stairs, bool active, Vector3 position, Quaternion rotation)
        {
            if (stairs == null)
                return;
            stairs.gameObject.SetActive(active);
            if (!active)
                return;
            stairs.position = position;
            stairs.rotation = rotation;
        }

        private void HideTurnaroundEquipment()
        {
            SetEquipmentVisible(_fuelTruck, false);
            SetEquipmentVisible(_cateringTruck, false);
            SetEquipmentVisible(_baggageCart, false);
            SetEquipmentVisible(_passengerBus, false);
            SetEquipmentVisible(_stairs, false);
            SetEquipmentVisible(_chocks, false);
            SetEquipmentVisible(_gpuCart, false);
        }

        private void UpdateVehicle(Transform vehicle, bool active, Vector3 servicePosition, Vector3 parkPosition,
            bool routed = true)
        {
            if (vehicle == null)
                return;

            vehicle.gameObject.SetActive(true);
            var target = active ? servicePosition : parkPosition;
            // First show may still be at origin — start from the park bay.
            if (vehicle.position.sqrMagnitude < 0.01f)
                vehicle.position = parkPosition;

            var previous = vehicle.position;
            // Adelaide vehicle handbook: 25 km/h apron, 15 km/h terminal road,
            // and 10 km/h within 15 m of an aircraft (also when returning from service).
            var speed = Mathf.Min(active ? 7.5f : 5.5f, (routed ? 25f : 15f) / 3.6f);
            if (WithinAircraftVehicleSlowZone(previous)) speed = Mathf.Min(speed, 10f / 3.6f);
            // Round parked aircraft, buildings and the terminal, not through them; and never
            // into the path of an aircraft that is taxiing or being pushed back.
            var steer = routed ? RouteWaypoint(vehicle, target, VehicleClearanceMetres) : target;
            var next = Vector3.MoveTowards(previous, steer, Time.unscaledDeltaTime * speed);
            if (routed && !InMovingAircraftPath(previous) && InMovingAircraftPath(next + (next - previous).normalized * 4f))
                next = previous;
            vehicle.position = next;
            var travel = Vector3.Distance(previous, vehicle.position);
            if (travel > 0.001f)
            {
                var flat = steer - previous;
                flat.y = 0f;
                if (flat.sqrMagnitude > 0.0001f)
                {
                    var look = Quaternion.LookRotation(flat.normalized, Vector3.up);
                    vehicle.rotation = Quaternion.Slerp(vehicle.rotation, look, Time.unscaledDeltaTime * 4f);
                }
            }

            if (!active && Vector3.Distance(vehicle.position, parkPosition) < 0.05f)
                ResetServiceLoopParts(vehicle);

            // Presentation-only: wheels roll while the vehicle is moving (ANM-VEH rates).
            var spinRpm = active
                ? AirsideReusableMotion.VehicleWheelRpmTaxi
                : AirsideReusableMotion.VehicleWheelRpmService;
            var degrees = travel * 120f + (travel > 0.001f ? Time.unscaledDeltaTime * spinRpm : 0f);
            if (degrees <= 0f)
                return;
            var wheels = _serviceWheelPivots.GetValue(vehicle, CollectServiceWheelPivots);
            foreach (var wheel in wheels)
                if (wheel.Pivot != null)
                    wheel.Pivot.Rotate(wheel.Axis, degrees, Space.Self);
        }

        // Weak keys release cached axle frames when a vehicle leaves the pool permanently.
        private readonly System.Runtime.CompilerServices.ConditionalWeakTable<Transform,
            List<(Transform Pivot, Vector3 Axis)>> _serviceWheelPivots = new();

        private static List<(Transform Pivot, Vector3 Axis)> CollectServiceWheelPivots(Transform vehicle)
        {
            var result = new List<(Transform Pivot, Vector3 Axis)>();
            // Snapshot first: adding axle parents must not change this traversal.
            foreach (var meshPart in vehicle.GetComponentsInChildren<MeshFilter>(true))
            {
                var part = meshPart.transform;
                if (!ServiceVehicleWheelGeometry.IsRoadWheel(part.name) || meshPart.sharedMesh == null)
                    continue;
                var bounds = meshPart.sharedMesh.bounds;
                var centre = new Vector3(ServiceVehicleWheelGeometry.Centre(bounds.min.x, bounds.max.x),
                    ServiceVehicleWheelGeometry.Centre(bounds.min.y, bounds.max.y),
                    ServiceVehicleWheelGeometry.Centre(bounds.min.z, bounds.max.z));
                var scale = part.localScale;
                var size = Vector3.Scale(bounds.size, new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
                var axle = ServiceVehicleWheelGeometry.AxleAxis(size.x, size.y, size.z);
                var pivot = new GameObject("Service axle pivot").transform;
                pivot.SetParent(part.parent, false);
                pivot.localPosition = part.localPosition + part.localRotation * Vector3.Scale(scale, centre);
                pivot.localRotation = part.localRotation;
                pivot.localScale = scale;
                // Insert an axle frame without touching cached meshes. At zero roll the composed
                // transform is exactly the original transform, including prefab rotation/scale.
                part.SetParent(pivot, false);
                part.localPosition = -centre;
                part.localRotation = Quaternion.identity;
                part.localScale = Vector3.one;
                result.Add((pivot, axle == 2 ? Vector3.forward : axle == 0 ? Vector3.right : Vector3.up));
            }
            return result;
        }

        /// <summary>
        /// Decision 0025 item 5 — realtime apron ReflectionProbe so Lit wet asphalt /
        /// metal pick up local floods at night without a baked probe set.
        /// </summary>
        private void MaybeRefreshApronProbe(float daylight, float wetness)
        {
            if (_apronProbe == null)
                return;
            var band = AirsideRuntimeQuality.ProbeBand(daylight, wetness);
            if (band == _probeBand)
                return;
            _probeBand = band;
            _apronProbe.RenderProbe();
            if (_terminalProbe != null) _terminalProbe.RenderProbe();
        }

        private static ReflectionProbe BuildApronReflectionProbe()
        {
            var go = new GameObject("Apron reflection probe");
            go.transform.position = new Vector3(20f, 3.5f, 17f);
            var probe = go.AddComponent<ReflectionProbe>();
            probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime;
            probe.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.ViaScripting;
            probe.timeSlicingMode = UnityEngine.Rendering.ReflectionProbeTimeSlicingMode.IndividualFaces;
            probe.resolution = AirsideRuntimeQuality.Current == AirsideRuntimeQuality.Ladder.High ? 128 : 64;
            // Cover stand apron + hangar face so authored metal/glass get local floods.
            probe.size = new Vector3(56f, 22f, 42f);
            probe.center = Vector3.zero;
            probe.intensity = 1f;
            probe.boxProjection = true;
            probe.shadowDistance = 28f;
            probe.nearClipPlane = 0.3f;
            probe.farClipPlane = 90f;
            return probe;
        }

        /// <summary>
        /// Decision 0025 item 5, extended to the real Adelaide bare-field world — the
        /// original apron/terminal probes above only ever existed on the legacy 1:20
        /// miniature circuit, so the real terminal's wet asphalt and 28 glazing bays never
        /// picked up local floodlight reflections. Sized from
        /// <see cref="AdelaideTerminalArchitecture"/>'s real coordinates (stands at Z 388,
        /// roof floods/glazing at Z 434-435.55, X 1005-1575) — the whole frontage is under
        /// 50 m deep along Z, so one box-projected probe reaches both the stand apron and
        /// the terminal glass instead of needing two.
        /// </summary>
        private static ReflectionProbe BuildBareApronReflectionProbe()
        {
            var go = new GameObject("Bare apron/terminal reflection probe");
            go.transform.position = new Vector3(1290f, 7f, 411f);
            var probe = go.AddComponent<ReflectionProbe>();
            probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime;
            probe.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.ViaScripting;
            probe.timeSlicingMode = UnityEngine.Rendering.ReflectionProbeTimeSlicingMode.IndividualFaces;
            probe.resolution = AirsideRuntimeQuality.Current == AirsideRuntimeQuality.Ladder.High ? 128 : 64;
            probe.size = new Vector3(650f, 26f, 100f);
            probe.center = Vector3.zero;
            probe.intensity = 1f;
            probe.boxProjection = true;
            probe.shadowDistance = 180f;
            probe.nearClipPlane = 0.3f;
            probe.farClipPlane = 800f;
            AirsideSceneIndex.Remember(go);
            return probe;
        }

        private static void BuildAirfieldApronAndBuildings()
        {
            var fringe = Shade(AirsideTheme.DryGrass, 0.7f);
            CreateBlock("Apron fringe N", new Vector3(16f, -0.022f, 25.12f), new Vector3(28f, 0.055f, 1.2f), fringe,
                PreferSurfaceBasecolor("tx_grass_kingscote"), new Vector2(8f, 0.35f));
            CreateBlock("Apron fringe S", new Vector3(16f, -0.022f, 9.1f), new Vector3(28f, 0.055f, 1.2f), fringe,
                PreferSurfaceBasecolor("tx_grass_kingscote"), new Vector2(8f, 0.35f));
            // Planter strip between terminal glass and apron edge — prefer PRP-003 kit.
            if (!TryPlaceAirsidePlanterStrip())
            {
                CreateBlock("Terminal planter bed W", new Vector3(22.5f, 0.12f, 24.4f), new Vector3(7f, 0.28f, 1.1f),
                    new Color(0.28f, 0.22f, 0.16f));
                CreateBlock("Terminal planter bed E", new Vector3(29.5f, 0.12f, 24.45f), new Vector3(6.65f, 0.2688f, 1.067f),
                    new Color(0.3f, 0.24f, 0.17f));
                PlaceShrub(new Vector3(20f, 0f, 24.4f), 0.55f);
                PlaceShrub(new Vector3(23f, 0f, 24.5f), 0.62f);
                PlaceShrub(new Vector3(26f, 0f, 24.35f), 0.58f);
                PlaceShrub(new Vector3(29f, 0f, 24.45f), 0.65f);
                PlaceShrub(new Vector3(32f, 0f, 24.4f), 0.5f);
            }

            PlaceWorldMarkings();
            PlaceWorldLighting();
            PlaceWorldProps();
            BuildEnvironmentContext();

            BuildStandMarking(AirportLayout.StandX, 14f, "Stand 1");
            BuildStandMarking(AirportLayout.StandX, 24f, "Stand 2");
            // Stand bay digits come from PlaceWorldMarkings / PlaceRunwayDigit (kit-prefer).
        }

        /// <summary>
        /// Stylised staff / passenger silhouettes — readable life, not characters.
        /// </summary>
        private static void BuildApronLife()
        {
            var root = new GameObject("Apron life").transform;
            var crewKit = PreferArtKit(
                "Models/Characters/mdl_ramp_crew_kit_v02.gltf",
                "Models/Characters/mdl_ramp_crew_kit_v01.gltf");
            var paxKit = PreferArtKit(
                "Models/Characters/mdl_passenger_kit_v02.gltf",
                "Models/Characters/mdl_passenger_kit_v01.gltf");
            var hasChrKits = (!string.IsNullOrEmpty(crewKit) && ArtGltfLoader.HasKit(crewKit))
                             || (!string.IsNullOrEmpty(paxKit) && ArtGltfLoader.HasKit(paxKit));

            // Hero set always — marshallers, fuel, baggage, hangar, gate, a few passengers.
            PlacePerson(root, "Marshaller", new Vector3(14.5f, 0f, 16.5f), 200f, new Color(0.85f, 0.55f, 0.12f),
                hiVis: true, marshallerWand: true);
            PlacePerson(root, "Fueler", new Vector3(-3.2f, 0f, 13.2f), 90f, new Color(0.2f, 0.35f, 0.55f), hiVis: true);
            PlacePerson(root, "Hangar tech", new Vector3(-16f, 0f, 18.5f), 270f, new Color(0.35f, 0.4f, 0.45f));
            PlacePerson(root, "Landside passenger A", new Vector3(26.5f, 0f, 31.8f), 180f, new Color(0.45f, 0.22f, 0.2f));
            PlacePerson(root, "Landside passenger B", new Vector3(27.8f, 0f, 31.6f), 175f, new Color(0.2f, 0.35f, 0.4f));
            PlacePerson(root, "Gate attendant", new Vector3(24.2f, 0f, 30.8f), 200f, new Color(0.55f, 0.58f, 0.62f));
            PlacePerson(root, "Stand 2 marshaller", new Vector3(19.8f, 0f, 24f), 185f, new Color(0.9f, 0.5f, 0.1f),
                hiVis: true, marshallerWand: true);
            PlacePerson(root, "Baggage handler", new Vector3(20.5f, 0f, 19.5f), 250f, new Color(0.3f, 0.45f, 0.55f), hiVis: true);
            PlacePerson(root, "Bench sitter", new Vector3(29.5f, 0.15f, 31.5f), 0f, new Color(0.35f, 0.3f, 0.28f), seated: true);

            if (hasChrKits)
            {
                // Fidelity board CHR sheet — keep nine readable silhouettes, not a carpet:
                // hero set already covers three ramp roles + stand/sit passengers; add one
                // walker and one seated passenger so stand/walk/sit all read from overview.
                PlacePerson(root, "Ramp walker", new Vector3(18.5f, 0f, 14.2f), 95f, new Color(0.55f, 0.35f, 0.18f), hiVis: true);
                PlacePerson(root, "Bench sitter B", new Vector3(30.8f, 0.15f, 31.2f), 10f, new Color(0.25f, 0.35f, 0.4f), seated: true);
                return;
            }

            PlacePerson(root, "Ops walker", new Vector3(22f, 0f, 22.5f), 15f, new Color(0.25f, 0.28f, 0.32f));
            PlacePerson(root, "Ramp walker", new Vector3(18.5f, 0f, 14.2f), 95f, new Color(0.55f, 0.35f, 0.18f), hiVis: true);
            PlacePerson(root, "Hangar tech B", new Vector3(-18.5f, 0f, 17.2f), 200f, new Color(0.4f, 0.42f, 0.38f));
            PlacePerson(root, "Landside passenger C", new Vector3(25.6f, 0f, 31.4f), 190f, new Color(0.3f, 0.32f, 0.45f));
            PlacePerson(root, "Landside passenger D", new Vector3(28.5f, 0f, 32.2f), 160f, new Color(0.5f, 0.45f, 0.35f));
            PlacePerson(root, "Car park walker", new Vector3(34f, 0f, 34f), 220f, new Color(0.4f, 0.25f, 0.3f));
            PlacePerson(root, "Fuel pad walker", new Vector3(-32f, 0f, 20.5f), 110f, new Color(0.55f, 0.4f, 0.2f), hiVis: true);
            PlacePerson(root, "Stairs attendant", new Vector3(16.8f, 0f, 18.2f), 170f, new Color(0.6f, 0.35f, 0.25f), hiVis: true);
            PlacePerson(root, "Ops walker B", new Vector3(-6f, 0f, 24.5f), 40f, new Color(0.28f, 0.3f, 0.35f));
            PlacePerson(root, "Car park walker B", new Vector3(44f, 0f, 42f), 280f, new Color(0.35f, 0.4f, 0.45f));
            PlacePerson(root, "Landside passenger E", new Vector3(24.8f, 0f, 32.5f), 150f, new Color(0.55f, 0.3f, 0.35f));
            PlacePerson(root, "Bench sitter B", new Vector3(30.8f, 0.15f, 31.2f), 10f, new Color(0.25f, 0.35f, 0.4f), seated: true);
        }

        /// <summary>
        /// Batch F2 — place CHR kit meshes renamed for UpdateApronLife heuristics.
        /// Crew roles map to marshaller/fueler/ramp; passengers cycle stand/walk/sit variants.
        /// </summary>
        private static bool TryPlaceCharacterFromKit(
            Transform root,
            string name,
            Color clothes,
            bool seated,
            bool hiVis,
            bool marshallerWand)
        {
            string prefix;
            string kitPath;
            var lower = name.ToLowerInvariant();
            if (lower.Contains("marshaller"))
            {
                prefix = "marshaller";
                kitPath = PreferArtKit(
                "Models/Characters/mdl_ramp_crew_kit_v02.gltf",
                "Models/Characters/mdl_ramp_crew_kit_v01.gltf");
            }
            else if (lower.Contains("fueler") || lower.Contains("baggage") || lower.Contains("stairs")
                     || lower.Contains("ramp") || (hiVis && !seated))
            {
                prefix = lower.Contains("fueler") ? "fueler" : "ramp";
                kitPath = PreferArtKit(
                "Models/Characters/mdl_ramp_crew_kit_v02.gltf",
                "Models/Characters/mdl_ramp_crew_kit_v01.gltf");
            }
            else
            {
                kitPath = PreferArtKit(
                "Models/Characters/mdl_passenger_kit_v02.gltf",
                "Models/Characters/mdl_passenger_kit_v01.gltf");
                // string.GetHashCode is not stable across runtimes (and Math.Abs throws on
                // int.MinValue), so the same figure could change outfit between editor and
                // player. A fixed FNV-1a parity keeps each name on one variant.
                var even = StableNameHash(name) % 2 == 0;
                if (seated)
                    prefix = lower.Contains("sitter b") || even ? "sit_f" : "sit_e";
                else if (lower.Contains("walker"))
                    prefix = even ? "walk_c" : "walk_d";
                else
                    prefix = even ? "stand_a" : "stand_b";
            }

            if (string.IsNullOrEmpty(kitPath) || !ArtGltfLoader.HasKit(kitPath))
                return false;

            var torsoColor = hiVis ? new Color(0.95f, 0.72f, 0.12f) : clothes;
            var skin = new Color(0.78f, 0.62f, 0.5f);
            var placed = 0;

            void Place(string mesh, string displayName, Color color)
            {
                if (!ArtGltfLoader.TryPlaceNamedMesh(kitPath, mesh, Vector3.zero, Quaternion.identity, color, out var part))
                    return;
                part.SetParent(root, false);
                part.localPosition = Vector3.zero;
                part.name = displayName;
                placed++;
            }

            Place($"{prefix}_torso", $"{name} torso", torsoColor);
            Place($"{prefix}_head", $"{name} head", skin);
            if (hiVis || prefix is "marshaller" or "fueler" or "ramp")
            {
                Place($"{prefix}_vest", $"{name} vest stripe", new Color(0.95f, 0.95f, 0.9f));
                Place($"{prefix}_hat", $"{name} hard hat", new Color(0.95f, 0.78f, 0.15f));
            }

            if (seated || prefix.StartsWith("sit_", StringComparison.Ordinal))
            {
                Place($"{prefix}_legs", $"{name} legs", Shade(clothes, 0.7f));
                Place($"{prefix}_arm_l", $"{name} arm L", Shade(torsoColor, 0.85f));
                Place($"{prefix}_arm_r", $"{name} arm R", Shade(torsoColor, 0.85f));
            }
            else
            {
                Place($"{prefix}_leg_l", $"{name} leg L", Shade(clothes, 0.7f));
                Place($"{prefix}_leg_r", $"{name} leg R", Shade(clothes, 0.7f));
                Place($"{prefix}_arm_l", $"{name} arm L", Shade(torsoColor, 0.85f));
                Place($"{prefix}_arm_r", $"{name} arm R", Shade(torsoColor, 0.85f));
                Place($"{prefix}_shoe_l", $"{name} shoe L", new Color(0.15f, 0.15f, 0.16f));
                Place($"{prefix}_shoe_r", $"{name} shoe R", new Color(0.15f, 0.15f, 0.16f));
            }

            if (marshallerWand || prefix == "marshaller")
            {
                Place($"{prefix}_wand", $"{name} wand", new Color(0.95f, 0.2f, 0.15f));
                Place($"{prefix}_wand_tip", $"{name} wand tip", new Color(1f, 0.85f, 0.2f));
                // Silhouette board shows two wands — mirror a second into the other hand.
                if (ArtGltfLoader.TryPlaceNamedMesh(kitPath, $"{prefix}_wand", Vector3.zero, Quaternion.identity,
                        new Color(0.95f, 0.2f, 0.15f), out var wandL))
                {
                    wandL.SetParent(root, false);
                    wandL.localPosition = new Vector3(-0.42f, 0f, 0.05f);
                    wandL.localRotation = Quaternion.Euler(0f, 0f, 12f);
                    wandL.name = $"{name} wand L";
                    placed++;
                }

                if (ArtGltfLoader.TryPlaceNamedMesh(kitPath, $"{prefix}_wand_tip", Vector3.zero, Quaternion.identity,
                        new Color(1f, 0.85f, 0.2f), out var tipL))
                {
                    tipL.SetParent(root, false);
                    tipL.localPosition = new Vector3(-0.42f, 0.3f, 0.05f);
                    tipL.name = $"{name} wand tip L";
                    placed++;
                }
            }

            return placed >= 3;
        }

        private void UpdateApronLife()
        {
            if (!AirsideFocusMode.ShowPeople)
                return;

            if (_apronLifeRoot == null)
                _apronLifeRoot = AirsideSceneIndex.Find("Apron life");

            if (_apronLifeRoot == null)
                return;

            if (_apronPeople.Count == 0)
            {
                for (var i = 0; i < _apronLifeRoot.childCount; i++)
                {
                    var person = _apronLifeRoot.GetChild(i);
                    var name = person.name;
                    _apronPeople.Add((person, person.position,
                        name.IndexOf("walker", StringComparison.OrdinalIgnoreCase) >= 0,
                        name.IndexOf("sitter", StringComparison.OrdinalIgnoreCase) >= 0,
                        name.IndexOf("marshaller", StringComparison.OrdinalIgnoreCase) >= 0));
                }
            }

            // Marshallers wave while anything is inbound — one scan, not one per marshaller.
            var inbound = false;
            var flights = VisualFlights;
            for (var f = 0; f < flights.Count; f++)
            {
                if (flights[f].Operation.Phase is AircraftPhase.Approach or AircraftPhase.Landing or AircraftPhase.TaxiIn
                    && !IsRotorcraftFlight(flights[f]))
                {
                    inbound = true;
                    break;
                }
            }

            // Soft idle lean on torsos so figures don't read as frozen props.
            for (var i = 0; i < _apronPeople.Count; i++)
            {
                var (person, basePos, walker, sitter, marshaller) = _apronPeople[i];
                if (person == null || sitter)
                    continue;

                var wave = marshaller && inbound;

                // Shuffle walkers around their spawn; other standing figures get a tiny idle sway.
                if (walker)
                {
                    var ox = Mathf.Sin(Time.unscaledTime * AirsideReusableMotion.ApronWalkerHz * Mathf.PI * 2f + i) * 1.6f;
                    var oz = Mathf.Cos(Time.unscaledTime * AirsideReusableMotion.ApronWalkerHz * Mathf.PI * 2f * 0.8f + i * 0.7f) * 1.0f;
                    person.position = new Vector3(basePos.x + ox, basePos.y, basePos.z + oz);
                    var look = new Vector3(-oz, 0f, ox);
                    if (look.sqrMagnitude > 0.0001f)
                        person.rotation = Quaternion.Slerp(person.rotation, Quaternion.LookRotation(look.normalized),
                            Time.unscaledDeltaTime * 2f);
                }
                else
                {
                    var sway = Mathf.Sin(Time.unscaledTime * AirsideReusableMotion.ApronIdleSwayHz * Mathf.PI * 2f + i * 0.9f) * 0.08f;
                    person.position = new Vector3(basePos.x + sway, basePos.y, basePos.z);
                }

                var bodyParts = ApronBodyPartsFor(person);
                for (var partIndex = 0; partIndex < bodyParts.Length; partIndex++)
                {
                    var part = bodyParts[partIndex];
                    var child = part.Transform;
                    if (child == null)
                        continue;
                    switch (part.Kind)
                    {
                        case ApronBodyPartKind.Torso:
                        {
                            var lean = Mathf.Sin(Time.unscaledTime * AirsideReusableMotion.ApronIdleSwayHz * Mathf.PI * 2f * 1.6f + i * 1.3f) * 4f;
                            if (wave)
                                lean += Mathf.Sin(Time.unscaledTime * AirsideReusableMotion.ApronWaveHz * Mathf.PI * 2f) * 16f;
                            child.localEulerAngles = new Vector3(0f, 0f, lean);
                            break;
                        }
                        case ApronBodyPartKind.Wand when wave:
                        {
                            var tip = Mathf.Sin(Time.unscaledTime * AirsideReusableMotion.ApronWaveHz * Mathf.PI * 2f * 1.5f) * 28f;
                            child.localEulerAngles = new Vector3(tip, 0f, 12f);
                            break;
                        }
                        case ApronBodyPartKind.Arm when wave:
                        {
                            var swing = Mathf.Sin(Time.unscaledTime * AirsideReusableMotion.ApronWaveHz * Mathf.PI * 2f * 1.25f
                                + (part.IsLeft ? 0f : 1.2f)) * 35f;
                            child.localEulerAngles = new Vector3(swing, 0f, part.IsLeft ? -12f : 12f);
                            break;
                        }
                        case ApronBodyPartKind.Leg when walker:
                        {
                            var stride = Mathf.Sin(Time.unscaledTime * AirsideReusableMotion.ApronStrideHz
                                + (part.IsLeft ? 0f : 3.14f)) * 18f;
                            child.localEulerAngles = new Vector3(stride, 0f, 0f);
                            break;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Classified once per apron figure instead of every frame — <see cref="UpdateApronLife"/>
        /// used to re-test every child's name against torso/wand/arm/leg substrings each frame,
        /// for every figure on the apron.
        /// </summary>
        private ApronBodyPart[] ApronBodyPartsFor(Transform person)
        {
            var id = person.GetInstanceID();
            if (_apronBodyParts.TryGetValue(id, out var cached))
                return cached;

            var children = AirsideNamedChildren.Get(person);
            var names = AirsideNamedChildren.Names(person);
            var parts = new List<ApronBodyPart>();
            for (var i = 0; i < names.Length; i++)
            {
                var child = children[i];
                var childName = names[i];
                if (child == null || child == person)
                    continue;
                if (childName.IndexOf("torso", StringComparison.OrdinalIgnoreCase) >= 0)
                    parts.Add(new ApronBodyPart(child, ApronBodyPartKind.Torso, false));
                else if (childName.IndexOf("wand", StringComparison.OrdinalIgnoreCase) >= 0)
                    parts.Add(new ApronBodyPart(child, ApronBodyPartKind.Wand, false));
                else if (childName.IndexOf("arm", StringComparison.OrdinalIgnoreCase) >= 0)
                    parts.Add(new ApronBodyPart(child, ApronBodyPartKind.Arm, IsLeftSideLimb(childName)));
                else if (childName.IndexOf("leg", StringComparison.OrdinalIgnoreCase) >= 0)
                    parts.Add(new ApronBodyPart(child, ApronBodyPartKind.Leg, IsLeftSideLimb(childName)));
            }

            var result = parts.ToArray();
            _apronBodyParts[id] = result;
            return result;
        }

        /// <summary>
        /// Nest densified cargo bags under the first Cargo crate so bag unload bob carries them.
        /// </summary>
        private static void NestCargoBags(Transform vehicle)
        {
            Transform cargo = null;
            var bags = new List<Transform>();
            var namedChildren23 = AirsideNamedChildren.Get(vehicle);
            var childNames23 = AirsideNamedChildren.Names(vehicle);
            for (var childIndex23 = 0; childIndex23 < namedChildren23.Length; childIndex23++)
            {
                var child = namedChildren23[childIndex23];
                var childName = childNames23[childIndex23];
                if (child == vehicle)
                    continue;
                if (childName == "Cargo" && cargo == null)
                    cargo = child;
                else if (childName.StartsWith("Cargo bag", StringComparison.Ordinal))
                    bags.Add(child);
            }

            if (cargo == null)
                return;
            foreach (var bag in bags)
                NestUnderProp(cargo, bag, bag.name);
        }

        private static Transform BuildServiceVehicle(string name, Color color, Vector3 scale, string artRelativePath = null)
        {
            var root = new GameObject(name).transform;
            var usedArt = !string.IsNullOrEmpty(artRelativePath) && ArtPresentationLoader.TryInstantiate(
                artRelativePath,
                root,
                out _,
                kitName => kitName switch
                {
                    "cab" => $"{name} cab",
                    "tank" or "bus_body" or "tug" => $"{name} body",
                    "hose" => "Hose",
                    "hose_mount" => "Hose mount",
                    "hose_nozzle" => "Hose nozzle",
                    "hose_reel" => "Hose reel",
                    "hose_guard" => "Hose guard",
                    "hose_tray" => "Hose tray",
                    "door" or "cab_door" or "cab_door_r" => "Door",
                    "door_glass" or "door_glass_r" => "Door glass",
                    "door_handle" or "door_handle_l" or "door_handle_r" or "door_hinge_t" or "door_hinge_b"
                        => "Door handle",
                    "door_frame" => "Door frame",
                    "cargo_1" or "cargo_2" or "cargo_3" => "Cargo",
                    "cargo_tag_1" or "cargo_tag_2" => "Cargo tag",
                    "cargo_bag_1a" or "cargo_bag_1b" or "cargo_bag_1c"
                        or "cargo_bag_2a" or "cargo_bag_2b" or "cargo_bag_2c"
                        or "cargo_bag_3a" or "cargo_bag_3b" or "cargo_bag_3c" => "Cargo bag",
                    "headlight_l" => "Headlight L",
                    "headlight_r" => "Headlight R",
                    "taillight_l" => "Taillight L",
                    "taillight_r" => "Taillight R",
                    // Keep cart_* / cart_wheel_* names so wheels roll and carts do not bob as Cargo.
                    _ => $"{name} {kitName}"
                },
                kitName =>
                {
                    if (kitName.StartsWith("glass_pane", StringComparison.Ordinal)
                        || kitName is "cab_window" or "windows" or "tug_window" or "door_glass" or "door_glass_r"
                        or "windshield" or "rear_window" or "belt_loader_cab_glass")
                        return new Color(0.2f, 0.4f, 0.55f, 0.42f);
                    if (kitName.StartsWith("window_mullion", StringComparison.Ordinal)
                        || kitName is "window_sill" or "window_sill_b" or "window_header" or "window_header_b"
                        or "destination_board" or "destination_board_hood" or "destination_digit")
                        return color * 0.7f;
                    return kitName switch
                    {
                        "wheel_fl" or "wheel_fr" or "wheel_rl" or "wheel_rr"
                            or "wheel_ml" or "wheel_mr"
                            or "cart_wheel_1l" or "cart_wheel_1r" or "cart_wheel_2l" or "cart_wheel_2r"
                            or "cart_wheel_3l" or "cart_wheel_3r"
                            or "hub_fl" or "hub_fr" or "hub_rl" or "hub_rr"
                            or "hub_ml" or "hub_mr"
                            or "wheel_hub_fl" or "wheel_hub_fr" or "wheel_hub_rl" or "wheel_hub_rr"
                            or "mudflap_l" or "mudflap_r" => new Color(0.15f, 0.15f, 0.16f),
                        "hose_mount" or "hose" or "hose_reel" or "hose_nozzle" or "hose_guard" or "hose_tray"
                            or "hose_coil_a" or "hose_coil_b" or "hose_coil_c" or "hose_pivot"
                            or "pump_cabinet" or "pump_cabinet_door"
                            or "pump_gauge" or "pump_valve"
                            or "exhaust" or "pump_hose_out" => new Color(0.25f, 0.25f, 0.28f),
                        "door" or "cab_door" or "cab_door_r" or "door_frame" or "door_handle"
                            or "door_handle_l" or "door_handle_r" or "door_hinge_t" or "door_hinge_b"
                            => new Color(0.2f, 0.22f, 0.25f),
                        "cab" or "tug_cab" or "cab_roof" or "cab_visor" or "cab_fairing"
                            or "tug_seat" or "tug_seat_back" or "tug_rollbar"
                            or "tug_rollbar_top" or "tug_rollbar_l" or "tug_rollbar_r"
                            or "tug_floor" or "tug_steering" or "tug_steering_wheel"
                            or "counterweight" => color * 0.82f,
                        "beacon" or "beacon_guard" => new Color(0.95f, 0.35f, 0.12f),
                        "headlight_l" or "headlight_r" => new Color(0.95f, 0.95f, 0.85f),
                        "taillight_l" or "taillight_r" => new Color(0.85f, 0.15f, 0.12f),
                        "mirror_l" or "mirror_r" or "bumper" or "bumper_front" or "bumper_rear"
                            or "tug_bumper" or "tank_band" or "tank_band_2" or "tank_band_3" or "tank_band_4"
                            or "tank_cap" or "tank_cap_b" or "tank_ladder" or "tank_walkway"
                            or "tank_end_f" or "tank_end_r" or "tank_rail_l" or "tank_rail_r"
                            or "grill" or "light_bar" or "fender_fl" or "fender_fr" or "fender_rl" or "fender_rr"
                            or "fender_ml" or "fender_mr"
                            or "wheel_arch_fl" or "wheel_arch_fr" or "wheel_arch_rl" or "wheel_arch_rr"
                            or "chassis" or "step" or "step_r" or "roof_rack" or "roof_vent"
                            or "number_plate" or "fuel_hazard" or "hazard_chevron_1" or "hazard_chevron_2"
                            or "wiper" or "wiper_b" or "nose_round" or "tail_round"
                            or "body_panel_l" or "body_panel_r" or "skirt_l" or "skirt_r"
                            => color * 0.7f,
                        "cargo_1" or "cargo_2" or "cargo_3" or "cargo_tag_1" or "cargo_tag_2"
                            or "cargo_bag_1a" or "cargo_bag_1b" or "cargo_bag_1c"
                            or "cargo_bag_2a" or "cargo_bag_2b" or "cargo_bag_2c"
                            or "cargo_bag_3a" or "cargo_bag_3b" or "cargo_bag_3c"
                            or "cargo_lid_1" or "cargo_lid_2" or "cargo_lid_3"
                            or "cargo_latch_1" or "cargo_latch_2" or "cargo_latch_3"
                            => new Color(0.22f, 0.44f, 0.55f),
                        "stripe" or "stripe_b" or "stripe_upper" or "cab_stripe" or "tank_stripe"
                            or "tug_stripe" => new Color(0.95f, 0.85f, 0.2f),
                        "bus_body_upper" or "cabin_roof" => new Color(0.94f, 0.95f, 0.96f),
                        "cart_rail_1" or "cart_rail_2" or "cart_rail_3"
                            or "cart_rail_1b" or "cart_rail_2b" or "cart_rail_3b"
                            or "cart_gate_1" or "cart_gate_2" or "cart_gate_3"
                            or "cart_bed_1" or "cart_bed_2" or "cart_bed_3"
                            or "cart_canopy_1" or "cart_canopy_2" or "cart_canopy_3"
                            or "cart_post_1l" or "cart_post_1r" or "cart_post_2l" or "cart_post_2r"
                            or "cart_post_3l" or "cart_post_3r"
                            or "cart_post_1fl" or "cart_post_1fr" or "cart_post_2fl" or "cart_post_2fr"
                            or "cart_post_3fl" or "cart_post_3fr"
                            or "hitch_1" or "hitch_2" or "hitch_3"
                            or "hitch_pin_1" or "hitch_pin_2" or "hitch_pin_3"
                            or "tow_pivot_1" or "tow_pivot_2" or "tow_pivot_3" => color * 0.6f,
                        "seat_row_1" or "seat_row_2" or "seat_row_3" or "seat_row_4"
                            or "seat_back_1" or "seat_back_2" => new Color(0.35f, 0.38f, 0.42f),
                        _ => color
                    };
                },
                localPosition: new Vector3(0f, -0.55f, 0f));

            if (!usedArt)
            {
                ParentBlock(root, $"{name} body", Vector3.zero, scale, color);
                ParentBlock(root, $"{name} cab", new Vector3(scale.x * 0.28f, scale.y * 0.42f, 0f),
                    new Vector3(scale.x * 0.34f, scale.y * 0.62f, scale.z * 0.86f), color * 0.82f);
                ParentBlock(root, $"{name} wheel FL", new Vector3(scale.x * 0.32f, -scale.y * 0.35f, scale.z * 0.42f),
                    new Vector3(0.28f, 0.35f, 0.18f), new Color(0.15f, 0.15f, 0.16f));
                ParentBlock(root, $"{name} wheel FR", new Vector3(scale.x * 0.32f, -scale.y * 0.35f, -scale.z * 0.42f),
                    new Vector3(0.28f, 0.35f, 0.18f), new Color(0.15f, 0.15f, 0.16f));
                ParentBlock(root, $"{name} wheel RL", new Vector3(-scale.x * 0.28f, -scale.y * 0.35f, scale.z * 0.42f),
                    new Vector3(0.28f, 0.35f, 0.18f), new Color(0.15f, 0.15f, 0.16f));
                ParentBlock(root, $"{name} wheel RR", new Vector3(-scale.x * 0.28f, -scale.y * 0.35f, -scale.z * 0.42f),
                    new Vector3(0.28f, 0.35f, 0.18f), new Color(0.15f, 0.15f, 0.16f));
                ParentBlock(root, "Hose", new Vector3(scale.x * 0.45f, 0.15f, 0.2f),
                    new Vector3(0.12f, 0.12f, 0.4f), new Color(0.25f, 0.25f, 0.28f));
                ParentBlock(root, "Cargo", new Vector3(0f, 0.35f, 0f),
                    new Vector3(0.55f, 0.35f, 0.45f), new Color(0.75f, 0.55f, 0.2f));
                ParentBlock(root, "Door", new Vector3(scale.x * 0.2f, 0.25f, scale.z * 0.45f),
                    new Vector3(0.08f, 0.7f, 0.45f), new Color(0.2f, 0.22f, 0.25f));
            }
            else
            {
                NestServiceDoorParts(root);
                NestCargoBags(root);
            }

            root.gameObject.SetActive(false);
            return root;
        }

        private static Transform BuildStairs()
        {
            // Prefer denser authored service kit over thin Resources prefab (0025 item 2).
            var root = new GameObject("Passenger stairs").transform;
            var kit = PreferArtKit(
                "Models/Props/mdl_service_equipment_kit_v03.gltf",
                "Models/Props/mdl_service_equipment_kit_authored_v01.gltf",
                "Models/Props/mdl_service_equipment_kit_v02.gltf",
                "Models/Props/mdl_service_equipment_kit_v01.gltf");
            var yellow = AirsideTheme.SafetyYellow;
            var tread = new Color(0.62f, 0.63f, 0.65f);
            var placed = ArtGltfLoader.TryPlaceCombined(
                kit,
                new[]
                {
                    ("stairs_base", new Color(0.55f, 0.56f, 0.58f)),
                    ("stairs_rail_l", yellow),
                    ("stairs_rail_r", yellow),
                    ("stairs_rail_mid", yellow),
                    ("stairs_tread_1", tread),
                    ("stairs_tread_2", tread),
                    ("stairs_tread_3", tread),
                    ("stairs_tread_4", tread),
                    ("stairs_tread_5", tread),
                    ("stairs_tread_6", tread),
                    ("stairs_rail_cross", yellow),
                    ("stairs_post_1l", yellow),
                    ("stairs_post_1r", yellow),
                    ("stairs_post_2l", yellow),
                    ("stairs_post_2r", yellow),
                    ("stairs_post_3l", yellow),
                    ("stairs_post_3r", yellow),
                    ("stairs_nosing_1", yellow),
                    ("stairs_nosing_2", yellow),
                    ("stairs_nosing_3", yellow),
                    ("stairs_nosing_4", yellow),
                    ("stairs_nosing_5", yellow),
                    ("stairs_side_panel_l", AirsideTheme.CoastalBlue),
                    ("stairs_side_panel_r", AirsideTheme.CoastalBlue),
                    ("stairs_platform", new Color(0.7f, 0.72f, 0.74f)),
                    ("stairs_handle", Shade(AirsideTheme.CoastalBlue, 0.9f)),
                    ("stairs_brace", new Color(0.5f, 0.5f, 0.52f)),
                    ("stairs_wheel_l", new Color(0.15f, 0.15f, 0.16f)),
                    ("stairs_wheel_r", new Color(0.15f, 0.15f, 0.16f)),
                    ("stairs_wheel_rl", new Color(0.15f, 0.15f, 0.16f)),
                    ("stairs_wheel_rr", new Color(0.15f, 0.15f, 0.16f)),
                    ("stairs_hub_fl", new Color(0.25f, 0.26f, 0.28f)),
                    ("stairs_hub_fr", new Color(0.25f, 0.26f, 0.28f)),
                    ("stairs_hub_rl", new Color(0.25f, 0.26f, 0.28f)),
                    ("stairs_hub_rr", new Color(0.25f, 0.26f, 0.28f))
                },
                Vector3.zero, Quaternion.identity, "Passenger stairs kit", out var kitRoot);
            if (kitRoot != null)
            {
                kitRoot.SetParent(root, false);
                kitRoot.localPosition = Vector3.zero;
                kitRoot.localRotation = Quaternion.identity;
            }

            if (!placed && ArtGltfLoader.TryPlaceNamedMesh(kit, "stairs", Vector3.zero, Quaternion.identity,
                    new Color(0.7f, 0.72f, 0.74f), out var stairs))
            {
                stairs.SetParent(root, false);
                stairs.localPosition = Vector3.zero;
                placed = true;
            }

            if (placed)
            {
                root.gameObject.SetActive(false);
                return root;
            }

            DestroyPresentationObject(root.gameObject);
            if (ArtPresentationLoader.TryInstantiatePrefab("mdl_passenger_stairs_v02", out var prefabRoot)
                || ArtPresentationLoader.TryInstantiatePrefab("mdl_passenger_stairs_v01", out prefabRoot))
            {
                prefabRoot.name = "Passenger stairs";
                prefabRoot.gameObject.SetActive(false);
                return prefabRoot;
            }

            root = new GameObject("Passenger stairs").transform;
            ParentBlock(root, "Stairs base", Vector3.zero, new Vector3(1.1f, 0.2f, 2.4f), new Color(0.7f, 0.72f, 0.74f));
            ParentBlock(root, "Stairs rail L", new Vector3(-0.45f, 0.55f, 0f), new Vector3(0.08f, 1.0f, 2.2f), new Color(0.85f, 0.55f, 0.15f));
            ParentBlock(root, "Stairs rail R", new Vector3(0.45f, 0.55f, 0f), new Vector3(0.08f, 1.0f, 2.2f), new Color(0.85f, 0.55f, 0.15f));
            for (var i = 0; i < 5; i++)
                ParentBlock(root, $"Step {i}", new Vector3(0f, 0.15f + i * 0.18f, -0.9f + i * 0.35f),
                    new Vector3(0.95f, 0.08f, 0.32f), new Color(0.55f, 0.56f, 0.58f));

            root.gameObject.SetActive(false);
            return root;
        }

        private static Transform BuildChocks()
        {
            var root = new GameObject("Wheel chocks").transform;
            var kit = PreferArtKit(
                "Models/Props/mdl_service_equipment_kit_v03.gltf",
                "Models/Props/mdl_service_equipment_kit_authored_v01.gltf",
                "Models/Props/mdl_service_equipment_kit_v02.gltf",
                "Models/Props/mdl_service_equipment_kit_v01.gltf");
            var rubber = new Color(0.85f, 0.2f, 0.15f);
            var placed = ArtGltfLoader.TryPlaceCombined(
                kit,
                new[]
                {
                    ("chock_a", rubber),
                    ("chock_b", rubber),
                    ("chock_rope", new Color(0.2f, 0.2f, 0.22f)),
                    ("chock_handle", new Color(0.25f, 0.26f, 0.28f))
                },
                Vector3.zero, Quaternion.identity, "Wheel chocks kit", out var kitRoot,
                localOffsets: new[]
                {
                    new Vector3(-0.55f, 0f, 0f),
                    new Vector3(0.55f, 0f, 0f),
                    Vector3.zero,
                    Vector3.zero
                });
            if (kitRoot != null)
            {
                kitRoot.SetParent(root, false);
                kitRoot.localPosition = Vector3.zero;
                kitRoot.localRotation = Quaternion.identity;
            }

            // v02 kit may ship a single combined "chocks" mesh.
            if (!placed)
            {
                placed = ArtGltfLoader.TryPlaceNamedMesh(kit, "chocks", Vector3.zero, Quaternion.identity,
                    rubber, out var combined);
                if (combined != null)
                {
                    combined.SetParent(root, false);
                    combined.localPosition = Vector3.zero;
                }
            }

            if (placed)
            {
                root.gameObject.SetActive(false);
                return root;
            }

            DestroyPresentationObject(root.gameObject);
            if (ArtPresentationLoader.TryInstantiatePrefab("mdl_wheel_chocks_v01", out var prefabRoot))
            {
                prefabRoot.name = "Wheel chocks";
                prefabRoot.gameObject.SetActive(false);
                return prefabRoot;
            }

            root = new GameObject("Wheel chocks").transform;
            ParentBlock(root, "Chock L", new Vector3(-0.55f, 0f, 0f), new Vector3(0.35f, 0.22f, 0.45f), new Color(0.85f, 0.2f, 0.15f));
            ParentBlock(root, "Chock R", new Vector3(0.55f, 0f, 0f), new Vector3(0.35f, 0.22f, 0.45f), new Color(0.85f, 0.2f, 0.15f));
            root.gameObject.SetActive(false);
            return root;
        }

        private static Transform BuildGpuCart()
        {
            var root = new GameObject("GPU cart").transform;
            var kit = PreferArtKit(
                "Models/Props/mdl_service_equipment_kit_v03.gltf",
                "Models/Props/mdl_service_equipment_kit_authored_v01.gltf",
                "Models/Props/mdl_service_equipment_kit_v02.gltf",
                "Models/Props/mdl_service_equipment_kit_v01.gltf");
            var placed = ArtGltfLoader.TryPlaceCombined(
                kit,
                new[]
                {
                    ("gpu_body", AirsideTheme.CoastalBlue),
                    ("gpu_cab", Shade(AirsideTheme.CoastalBlue, 0.85f)),
                    ("gpu_vent", new Color(0.35f, 0.38f, 0.36f)),
                    ("gpu_panel", new Color(0.2f, 0.22f, 0.24f)),
                    ("gpu_panel_b", new Color(0.2f, 0.22f, 0.24f)),
                    ("gpu_grille", new Color(0.18f, 0.2f, 0.2f)),
                    ("gpu_grille_2", new Color(0.18f, 0.2f, 0.2f)),
                    ("gpu_slot_1", new Color(0.15f, 0.16f, 0.18f)),
                    ("gpu_slot_2", new Color(0.15f, 0.16f, 0.18f)),
                    ("gpu_cable", new Color(0.2f, 0.2f, 0.22f)),
                    ("gpu_cable_reel", new Color(0.22f, 0.22f, 0.24f)),
                    ("gpu_hitch", new Color(0.3f, 0.3f, 0.32f)),
                    ("gpu_beacon", new Color(0.95f, 0.35f, 0.12f)),
                    ("gpu_exhaust", new Color(0.3f, 0.32f, 0.3f)),
                    ("gpu_light", new Color(0.95f, 0.9f, 0.6f)),
                    ("gpu_handle", new Color(0.28f, 0.3f, 0.32f)),
                    ("gpu_stripe", new Color(0.15f, 0.16f, 0.18f)),
                    ("gpu_wheel_fl", new Color(0.15f, 0.15f, 0.16f)),
                    ("gpu_wheel_fr", new Color(0.15f, 0.15f, 0.16f)),
                    ("gpu_wheel_rl", new Color(0.15f, 0.15f, 0.16f)),
                    ("gpu_wheel_rr", new Color(0.15f, 0.15f, 0.16f)),
                    ("gpu_hub_fl", new Color(0.25f, 0.26f, 0.28f)),
                    ("gpu_hub_fr", new Color(0.25f, 0.26f, 0.28f)),
                    ("gpu_hub_rl", new Color(0.25f, 0.26f, 0.28f)),
                    ("gpu_hub_rr", new Color(0.25f, 0.26f, 0.28f))
                },
                Vector3.zero, Quaternion.identity, "GPU cart kit", out var kitRoot);
            if (kitRoot != null)
            {
                kitRoot.SetParent(root, false);
                kitRoot.localPosition = Vector3.zero;
                kitRoot.localRotation = Quaternion.identity;
            }

            if (!placed && ArtGltfLoader.TryPlaceNamedMesh(kit, "gpu", Vector3.zero, Quaternion.identity,
                    AirsideTheme.CoastalBlue, out var gpu))
            {
                gpu.SetParent(root, false);
                gpu.localPosition = Vector3.zero;
                placed = true;
            }

            if (placed)
            {
                root.gameObject.SetActive(false);
                return root;
            }

            DestroyPresentationObject(root.gameObject);
            if (ArtPresentationLoader.TryInstantiatePrefab("mdl_gpu_cart_v01", out var prefabRoot))
            {
                prefabRoot.name = "GPU cart";
                prefabRoot.gameObject.SetActive(false);
                return prefabRoot;
            }

            root = new GameObject("GPU cart").transform;
            ParentBlock(root, "GPU body", Vector3.zero, new Vector3(1.4f, 0.7f, 0.9f), AirsideTheme.CoastalBlue);
            ParentBlock(root, "GPU cable", new Vector3(0.85f, 0.1f, 0f), new Vector3(0.7f, 0.08f, 0.08f), new Color(0.2f, 0.2f, 0.22f));
            ParentBlock(root, "GPU wheel L", new Vector3(0.4f, -0.28f, 0.35f), new Vector3(0.22f, 0.22f, 0.14f), new Color(0.15f, 0.15f, 0.16f));
            ParentBlock(root, "GPU wheel R", new Vector3(0.4f, -0.28f, -0.35f), new Vector3(0.22f, 0.22f, 0.14f), new Color(0.15f, 0.15f, 0.16f));
            root.gameObject.SetActive(false);
            return root;
        }

        private static Transform BuildPushbackTug()
        {
            // VEH-004 — prefer pushback tug v03, then v02, then pipeline-proof v01.
            var pushbackKit = PreferArtKit(
                "Models/Vehicles/mdl_pushback_tug_v04.gltf",
                        "Models/Vehicles/mdl_pushback_tug_v03.gltf",
                "Models/Vehicles/mdl_pushback_tug_v02.gltf");
            if (!string.IsNullOrEmpty(pushbackKit)
                && ArtPresentationLoader.TryInstantiate(
                    pushbackKit,
                    null,
                    out var artRoot,
                    kitName => kitName switch
                    {
                        "towbar" => "Tug towbar",
                        "towbar_head" => "Tug towbar head",
                        "towbar_wheel" => "Tug towbar wheel",
                        "towbar_handle" => "Tug towbar handle",
                        "towbar_eye" => "Tug towbar eye",
                        "tow_pivot" or "towbar_pivot_mid" => "Tug tow pivot",
                        "tug_body" => "Tug body",
                        "tug_cab" => "Tug cab",
                        "beacon" => "Tug beacon",
                        "wheel_fl" => "Tug wheel FL",
                        "wheel_fr" => "Tug wheel FR",
                        "wheel_rl" => "Tug wheel RL",
                        "wheel_rr" => "Tug wheel RR",
                        _ => $"Tug {kitName}"
                    },
                    kitName =>
                    {
                        if (kitName.StartsWith("glass", StringComparison.Ordinal))
                            return new Color(0.2f, 0.4f, 0.55f, 0.42f);
                        return kitName switch
                        {
                            "wheel_fl" or "wheel_fr" or "wheel_rl" or "wheel_rr"
                                or "hub_fl" or "hub_fr" or "hub_rl" or "hub_rr"
                                or "towbar_wheel" => new Color(0.15f, 0.15f, 0.16f),
                            "towbar" or "towbar_head" or "towbar_handle" or "towbar_eye" or "tow_pivot"
                                or "towbar_pivot_mid"
                                => new Color(0.3f, 0.32f, 0.34f),
                            "beacon" => new Color(0.95f, 0.35f, 0.12f),
                            "headlight_l" or "headlight_r" => new Color(0.95f, 0.95f, 0.85f),
                            "taillight_l" or "taillight_r" => new Color(0.85f, 0.15f, 0.12f),
                            "stripe" => new Color(0.95f, 0.85f, 0.2f),
                            _ => new Color(0.82f, 0.62f, 0.18f)
                        };
                    },
                    localPosition: new Vector3(0f, -0.55f, 0f)))
            {
                artRoot.name = "Pushback tug";
                artRoot.gameObject.SetActive(false);
                return artRoot;
            }

            if (ArtPresentationLoader.TryInstantiatePrefab("mdl_pushback_tug_v03", out var prefabV03)
                || ArtPresentationLoader.TryInstantiatePrefab("mdl_pushback_tug_v02", out prefabV03)
                || ArtPresentationLoader.TryInstantiatePrefab("mdl_pushback_tug_v01", out prefabV03))
            {
                prefabV03.name = "Pushback tug";
                prefabV03.gameObject.SetActive(false);
                return prefabV03;
            }

            var root = new GameObject("Pushback tug").transform;
            var kit = PreferArtKit(
                "Models/Props/mdl_service_equipment_kit_v03.gltf",
                "Models/Props/mdl_service_equipment_kit_authored_v01.gltf",
                "Models/Props/mdl_service_equipment_kit_v02.gltf",
                "Models/Props/mdl_service_equipment_kit_v01.gltf");
            var placed = ArtGltfLoader.TryPlaceCombined(
                kit,
                new[]
                {
                    ("towbar", new Color(0.82f, 0.62f, 0.18f)),
                    ("towbar_head", new Color(0.3f, 0.32f, 0.34f)),
                    ("towbar_wheel", new Color(0.15f, 0.15f, 0.16f)),
                    ("towbar_handle", new Color(0.28f, 0.3f, 0.32f)),
                    ("towbar_eye", new Color(0.35f, 0.36f, 0.38f))
                },
                Vector3.zero, Quaternion.identity, "Tug towbar kit", out var kitRoot);
            if (kitRoot != null)
            {
                kitRoot.SetParent(root, false);
                kitRoot.localPosition = new Vector3(0f, -0.55f, 0f);
                kitRoot.localRotation = Quaternion.identity;
            }

            if (!placed)
            {
                ParentBlock(root, "Tug body", Vector3.zero, new Vector3(2.2f, 0.85f, 1.15f), new Color(0.82f, 0.62f, 0.18f));
                ParentBlock(root, "Tug cab", new Vector3(0.55f, 0.45f, 0f), new Vector3(0.9f, 0.7f, 1.0f), new Color(0.7f, 0.52f, 0.14f));
                ParentBlock(root, "Tug towbar", new Vector3(-1.4f, 0.05f, 0f), new Vector3(1.2f, 0.12f, 0.12f), new Color(0.3f, 0.3f, 0.32f));
                ParentBlock(root, "Tug wheel FL", new Vector3(0.6f, -0.35f, 0.45f), new Vector3(0.28f, 0.32f, 0.18f), new Color(0.15f, 0.15f, 0.16f));
                ParentBlock(root, "Tug wheel FR", new Vector3(0.6f, -0.35f, -0.45f), new Vector3(0.28f, 0.32f, 0.18f), new Color(0.15f, 0.15f, 0.16f));
                ParentBlock(root, "Tug wheel RL", new Vector3(-0.55f, -0.35f, 0.45f), new Vector3(0.28f, 0.32f, 0.18f), new Color(0.15f, 0.15f, 0.16f));
                ParentBlock(root, "Tug wheel RR", new Vector3(-0.55f, -0.35f, -0.45f), new Vector3(0.28f, 0.32f, 0.18f), new Color(0.15f, 0.15f, 0.16f));
                ParentBlock(root, "Tug beacon", new Vector3(0.55f, 0.9f, 0f), new Vector3(0.18f, 0.18f, 0.18f), new Color(0.95f, 0.35f, 0.12f));
            }

            root.gameObject.SetActive(false);
            return root;
        }

        private static void CreateCone(Vector3 position)
        {
            // Prefer denser authored props kit over thin Resources prefab (0025 item 2).
            var kit = PreferArtKit(
                "Models/Props/mdl_airfield_props_kit_authored_v01.gltf",
                "Models/Props/mdl_airfield_props_kit_v02.gltf",
                "Models/Props/mdl_airfield_props_kit_v01.gltf");
            var origin = position + new Vector3(0f, -0.25f, 0f);
            if (ArtGltfLoader.TryPlaceCombined(
                    kit,
                    new[]
                    {
                        ("cone_base", new Color(0.2f, 0.2f, 0.22f)),
                        ("cone_body", new Color(0.95f, 0.45f, 0.08f)),
                        ("cone_stripe", Color.white),
                        ("cone_tip", new Color(0.95f, 0.45f, 0.08f)),
                        ("cone_collar", Color.white),
                        ("cone_handle", new Color(0.25f, 0.25f, 0.28f))
                    },
                    origin, Quaternion.identity, "Safety cone", out _))
                return;

            if (ArtGltfLoader.TryPlaceNamedMesh(kit, "cone", origin, Quaternion.identity, new Color(0.95f, 0.45f, 0.08f), out _))
                return;

            if (ArtPresentationLoader.TryInstantiatePrefab("mdl_safety_cone_v01", out var prefabRoot))
            {
                prefabRoot.name = "Safety cone";
                prefabRoot.position = position;
                return;
            }

            var cone = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cone.name = "Safety cone";
            DestroyPresentationObject(cone.GetComponent<Collider>());
            cone.transform.position = position;
            cone.transform.localScale = new Vector3(0.28f, 0.35f, 0.28f);
            cone.GetComponent<Renderer>().sharedMaterial = CreateMaterial(new Color(0.95f, 0.45f, 0.08f));
            CreateBlock("Cone collar", position + new Vector3(0f, 0.12f, 0f), new Vector3(0.32f, 0.06f, 0.32f), Color.white);
        }

        private static void PlaceBaggageDolly(string kit, Vector3 position)
        {
            if (ArtGltfLoader.TryPlaceCombined(
                    kit,
                    new[]
                    {
                        ("dolly_bed", new Color(0.55f, 0.35f, 0.18f)),
                        ("dolly_rail_l", new Color(0.45f, 0.3f, 0.16f)),
                        ("dolly_rail_r", new Color(0.45f, 0.3f, 0.16f)),
                        ("dolly_rail_mid", new Color(0.45f, 0.3f, 0.16f)),
                        ("dolly_rail_end", new Color(0.45f, 0.3f, 0.16f)),
                        ("dolly_post_l", new Color(0.45f, 0.3f, 0.16f)),
                        ("dolly_post_r", new Color(0.45f, 0.3f, 0.16f)),
                        ("dolly_handle", new Color(0.4f, 0.4f, 0.42f)),
                        ("dolly_hitch", new Color(0.35f, 0.35f, 0.38f)),
                        ("dolly_hitch_pin", new Color(0.3f, 0.3f, 0.32f)),
                        ("dolly_cargo", new Color(0.7f, 0.55f, 0.25f)),
                        ("dolly_bag_a", new Color(0.75f, 0.55f, 0.2f)),
                        ("dolly_bag_b", new Color(0.65f, 0.45f, 0.18f)),
                        ("dolly_bag_c", new Color(0.8f, 0.6f, 0.25f)),
                        ("dolly_wheel_fl", new Color(0.15f, 0.15f, 0.16f)),
                        ("dolly_wheel_fr", new Color(0.15f, 0.15f, 0.16f)),
                        ("dolly_wheel_rl", new Color(0.15f, 0.15f, 0.16f)),
                        ("dolly_wheel_rr", new Color(0.15f, 0.15f, 0.16f)),
                        ("dolly_hub_fl", new Color(0.45f, 0.45f, 0.48f)),
                        ("dolly_hub_fr", new Color(0.45f, 0.45f, 0.48f)),
                        ("dolly_hub_rl", new Color(0.45f, 0.45f, 0.48f)),
                        ("dolly_hub_rr", new Color(0.45f, 0.45f, 0.48f))
                    },
                    position, Quaternion.identity, "Baggage dolly", out _))
                return;

            if (ArtGltfLoader.TryPlaceNamedMesh(kit, "baggage_dolly", position, Quaternion.identity,
                    new Color(0.55f, 0.35f, 0.18f), out _))
                return;

            if (ArtPresentationLoader.TryInstantiatePrefab("mdl_baggage_dolly_v01", out var prefabRoot))
            {
                prefabRoot.name = "Baggage dolly";
                prefabRoot.position = position;
                return;
            }

            CreateBlock("Dolly", position + new Vector3(0f, 0.35f, 0f), new Vector3(1.6f, 0.7f, 0.9f), new Color(0.55f, 0.35f, 0.18f));
        }

        /// <summary>
        /// Small fuel farm west of the hangar — readable silhouette, not a sim system.
        /// </summary>
        private static void BuildFuelFarm()
        {
            if (ArtPresentationLoader.TryInstantiatePrefab("mdl_fuel_farm_v01", out var farm))
            {
                farm.name = "Fuel farm";
                farm.position = new Vector3(-34f, 0f, 22f);
            }
            else
            {
                CreateBlock("Fuel pad", new Vector3(-34f, 0.02f, 21.5f), new Vector3(6.5f, 0.08f, 5.6f), new Color(0.29f, 0.31f, 0.33f),
                    PreferSurfaceBasecolor("tx_concrete_apron"), new Vector2(2.2f, 1.8f));
                CreateBlock("Fuel pad centre", new Vector3(-34f, 0.05f, 21.5f), new Vector3(0.12f, 0.02f, 3.6f), new Color(0.95f, 0.85f, 0.2f));
                CreateBlock("Fuel pad edge N", new Vector3(-34f, 0.05f, 24.2f), new Vector3(4.2f, 0.02f, 0.1f), Color.white);
                CreateBlock("Fuel pad edge S", new Vector3(-34f, 0.05f, 18.8f), new Vector3(4.2f, 0.02f, 0.1f), Color.white);
                CreateBlock("Fuel pad stop", new Vector3(-34f, 0.05f, 20.2f), new Vector3(2.4f, 0.02f, 0.12f), new Color(0.95f, 0.85f, 0.2f));
                CreateTaxiChordPad("Fuel pad link", new Vector3(-30f, 0.02f, 22f), new Vector3(-24f, 0.02f, 18f), 3.6f,
                    PreferSurfaceBasecolor("tx_asphalt_runway"), new Vector2(1.1f, 1f));
                PlaceFuelTank("Fuel tank A", new Vector3(-35.5f, 1.15f, 22.5f), new Color(0.72f, 0.55f, 0.18f));
                PlaceFuelTank("Fuel tank B", new Vector3(-32.2f, 1.15f, 22.5f), new Color(0.72f, 0.55f, 0.18f));
                var bund = new Color(0.4f, 0.42f, 0.4f);
                CreateBlock("Fuel bund N", new Vector3(-34f, 0.35f, 24.15f), new Vector3(6.4f, 0.55f, 0.35f), bund);
                CreateBlock("Fuel bund S", new Vector3(-34f, 0.35f, 19.85f), new Vector3(6.4f, 0.55f, 0.35f), bund);
                CreateBlock("Fuel bund W", new Vector3(-37.05f, 0.35f, 22f), new Vector3(0.35f, 0.55f, 4.0f), bund);
                CreateBlock("Fuel bund E", new Vector3(-30.95f, 0.35f, 22f), new Vector3(0.35f, 0.55f, 4.0f), bund);
                CreateBlock("Fuel pump", new Vector3(-34f, 0.7f, 19.6f), new Vector3(1.2f, 1.2f, 0.8f), new Color(0.25f, 0.28f, 0.3f));
                CreateBlock("Fuel hose reel", new Vector3(-33.1f, 0.45f, 19.8f), new Vector3(0.55f, 0.55f, 0.55f), new Color(0.35f, 0.2f, 0.12f));
                CreateCone(new Vector3(-30.5f, 0.25f, 19.2f));
                CreateCone(new Vector3(-37.5f, 0.25f, 19.2f));
                CreateBarrier(new Vector3(-34f, 0.45f, 18.6f), 0f);
            }

            // Amber safety flood over the fuel pad at night (presentation only).
            var lamp = new GameObject("Fuel farm light");
            lamp.transform.position = new Vector3(-34f, 4.2f, 22f);
            var light = lamp.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.72f, 0.28f);
            light.range = 16f;
            light.intensity = 0f;
            light.shadows = LightShadows.None;
        }

        private static void PlaceFuelTank(string name, Vector3 position, Color color)
        {
            var tank = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            tank.name = name;
            DestroyPresentationObject(tank.GetComponent<Collider>());
            tank.transform.position = position;
            tank.transform.localScale = new Vector3(2.0f, 1.15f, 2.0f);
            tank.GetComponent<Renderer>().sharedMaterial = AirsideMaterialLibrary.CreateShared(
                color, AirsideMaterialLibrary.SurfaceKind.PaintedMetal);
            // Cap + ladder stub for silhouette.
            CreateBlock($"{name} cap", position + new Vector3(0f, 1.25f, 0f), new Vector3(0.9f, 0.18f, 0.9f), Shade(color, 0.85f));
            CreateBlock($"{name} ladder", position + new Vector3(1.05f, 0.2f, 0f), new Vector3(0.12f, 1.8f, 0.35f),
                new Color(0.45f, 0.46f, 0.48f));
        }
    }
}

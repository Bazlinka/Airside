using System;
using System.Collections.Generic;
using Airside.Domain;
using Airside.Simulation;
using UnityEngine;

namespace Airside.Presentation
{
    /// <summary>Marks a turboprop's forward door as an airstair and how far it folds (ADR 0114).</summary>
    internal sealed class AirstairDoor : MonoBehaviour
    {
        /// <summary>Signed fold about the fuselage axis that puts the bottom step on the apron.</summary>
        public float OpenDegrees;
        /// <summary>Horizontal distance from the sill line to where the steps meet the ground.</summary>
        public float RunMetres;
        /// <summary>-1 left side, +1 right side.</summary>
        public int Side;
    }

    /// <summary>
    /// Boarding as it happens on an Adelaide apron (ADR 0114):
    ///
    /// * Saab 340, Dash 8-400 and ATR 42 fold their forward door down into its own airstair;
    /// * a jet on a stand without a bridge gets a stair truck at its L1 door;
    /// * passengers walk between the terminal and the stairs — CC0 Quaternius people
    ///   (Resources/Airside/Characters) sampled on the simulation clock — deplaning once the
    ///   door opens and boarding so the last one is aboard before it shuts.
    ///
    /// Timing is <see cref="BoardingFlow"/>; nothing here changes the simulation.
    /// </summary>
    public sealed partial class AirsidePrototype
    {
        // ---- Airstair doors ------------------------------------------------------------------

        private static readonly Color AirstairTread = new(0.36f, 0.38f, 0.41f);
        private static readonly Color AirstairRail = new(0.20f, 0.21f, 0.23f);

        /// <summary>
        /// Turn the forward cabin door into its airstair: hinge it on the sill, add treads and
        /// handrails on its inner face (inside the cabin while shut), and record the fold that
        /// sets the bottom step on the ground.
        /// </summary>
        private static void ConvertToAirstairDoor(Transform aircraft)
        {
            if (aircraft == null)
                return;
            Transform door = null;
            var children = AirsideNamedChildren.Get(aircraft);
            var names = AirsideNamedChildren.Names(aircraft);
            for (var i = 0; i < children.Length; i++)
            {
                if (names[i].StartsWith("CabinDoor", StringComparison.Ordinal))
                {
                    door = children[i];
                    break;
                }
            }

            if (door == null || door.GetComponent<AirstairDoor>() != null)
                return;

            // The frame stays on the fuselage; only the door itself folds.
            var frame = door.Find("Frame");
            if (frame != null)
                frame.SetParent(aircraft, true);

            if (!LocalBounds(aircraft, door, out var min, out var max))
                return;
            var centre = (min + max) * 0.5f;
            var side = centre.x < 0f ? -1 : 1;
            var pivot = new Vector3(side < 0 ? min.x : max.x, min.y, centre.z);
            RebakePartPivot(door, aircraft.TransformPoint(pivot));

            var height = Mathf.Max(0.4f, max.y - min.y);
            var width = Mathf.Max(0.4f, max.z - min.z);
            var sill = Mathf.Max(0f, min.y);
            var below = Mathf.Asin(Mathf.Clamp01(sill / height)) * Mathf.Rad2Deg;
            var marker = door.gameObject.AddComponent<AirstairDoor>();
            // A left door folds out and down with a positive turn about +Z (up → -X).
            marker.OpenDegrees = side * -(90f + below);
            marker.RunMetres = height * Mathf.Cos(below * Mathf.Deg2Rad);
            marker.Side = side;
            door.name = "CabinDoor airstair";

            var inward = -side;
            var steps = Mathf.Clamp(Mathf.RoundToInt(height / 0.23f), 3, 7);
            for (var k = 0; k < steps; k++)
            {
                var y = min.y + (k + 0.5f) / steps * height;
                AirstairPart(aircraft, door, $"Airstair tread {k + 1}",
                    new Vector3(pivot.x + inward * 0.08f, y, centre.z), new Vector3(0.14f, 0.05f, width * 0.82f), AirstairTread);
            }

            // Handrails stand up from both edges once the door is down; while shut they sit
            // folded inside the cabin.
            foreach (var edge in new[] { -1f, 1f })
            {
                var z = centre.z + edge * width * 0.47f;
                AirstairPart(aircraft, door, "Airstair rail", new Vector3(pivot.x + inward * 0.62f, min.y + height * 0.5f, z),
                    new Vector3(0.04f, height * 0.92f, 0.04f), AirstairRail);
                for (var post = 0; post < 3; post++)
                {
                    var y = min.y + (0.12f + post * 0.38f) * height;
                    AirstairPart(aircraft, door, "Airstair post", new Vector3(pivot.x + inward * 0.33f, y, z),
                        new Vector3(0.58f, 0.035f, 0.035f), AirstairRail);
                }
            }

            AirsideNamedChildren.Forget(aircraft);
        }

        private static readonly HashSet<string> AtrPassengerDoorParts = new(StringComparer.Ordinal)
        {
            "CabinDoor", "Cabin door frame", "Door frame", "Door handle", "door_fwd", "door_outline_fwd", "door_handle_fwd"
        };

        private static readonly HashSet<string> AtrCargoDoorParts = new(StringComparer.Ordinal)
        {
            "Cargo door", "Cargo door frame", "Cargo door latch", "cargo_door", "cargo_door_outline", "cargo_door_latch"
        };

        /// <summary>
        /// The ATR 42 mesh puts its passenger door forward left and its hold door aft right; the
        /// real aircraft boards through an aft-left airstair door and loads bags through a
        /// forward-left hold door. Move the parts to match (<see cref="AircraftLayout"/> holds
        /// the result), before the passenger door becomes the airstair.
        /// </summary>
        private static void RelocateAtrDoors(Transform aircraft)
        {
            var children = AirsideNamedChildren.Get(aircraft);
            var names = AirsideNamedChildren.Names(aircraft);
            var aft = aircraft.rotation * new Vector3(0f, 0f, AircraftLayout.AtrDoorShiftMetres);
            var forward = aircraft.rotation * new Vector3(0f, 0f, AircraftLayout.AtrCargoDoorShiftMetres);
            for (var i = 0; i < children.Length; i++)
            {
                var part = children[i];
                if (part == aircraft)
                    continue;
                // A part nested under another moved part goes with its parent.
                if (NestedIn(part, aircraft))
                    continue;
                if (AtrPassengerDoorParts.Contains(names[i]))
                    part.position += aft;
                else if (AtrCargoDoorParts.Contains(names[i]))
                {
                    // Half a turn about the aircraft's own vertical axis takes it to the left side.
                    part.RotateAround(aircraft.position, aircraft.up, 180f);
                    part.position += forward;
                }
            }
        }

        private static bool NestedIn(Transform part, Transform aircraft)
        {
            for (var p = part.parent; p != null && p != aircraft; p = p.parent)
                if (AtrPassengerDoorParts.Contains(p.name) || AtrCargoDoorParts.Contains(p.name))
                    return true;
            return false;
        }

        private static void AirstairPart(Transform aircraft, Transform door, string name, Vector3 aircraftLocal,
            Vector3 size, Color colour)
        {
            var block = CreateBlock(name, Vector3.zero, size, colour);
            block.transform.SetParent(aircraft, false);
            block.transform.localPosition = aircraftLocal;
            block.transform.localRotation = Quaternion.identity;
            block.transform.localScale = size;
            block.transform.SetParent(door, true);
        }

        private static bool LocalBounds(Transform frame, Transform part, out Vector3 min, out Vector3 max)
        {
            min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            var any = false;
            foreach (var filter in part.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null)
                    continue;
                foreach (var v in filter.sharedMesh.vertices)
                {
                    var local = frame.InverseTransformPoint(filter.transform.TransformPoint(v));
                    min = Vector3.Min(min, local);
                    max = Vector3.Max(max, local);
                    any = true;
                }
            }

            return any;
        }

        // ---- Stair trucks --------------------------------------------------------------------

        private const float StairTruckApproachMetres = 22f;

        private sealed class StairTruckView
        {
            public Transform Root;
            public Transform Flight;
            public float BuiltForSill = -1f;
            public string Registration;
        }

        private readonly Dictionary<string, StairTruckView> _stairTrucks = new(StringComparer.Ordinal);
        private readonly List<StairTruckView> _stairTruckPool = new();
        private readonly HashSet<string> _stairTruckWanted = new(StringComparer.Ordinal);

        private sealed class PassengerBusView
        {
            public Transform Root;
            public string Registration;
            /// <summary>Planned park→stand route (x, z pairs) and the ends it was planned for.</summary>
            public readonly List<float> Route = new();
            public Vector3 RoutedFrom;
            public Vector3 RoutedTo;
        }

        private const int MaxRemoteBuses = 6;
        private readonly Dictionary<string, PassengerBusView> _remoteBuses = new(StringComparer.Ordinal);
        private readonly List<PassengerBusView> _remoteBusPool = new();
        private readonly HashSet<string> _remoteBusWanted = new(StringComparer.Ordinal);
        private Transform _boardingRoot;

        private Transform BoardingRoot()
        {
            if (_boardingRoot == null)
                _boardingRoot = new GameObject("Boarding").transform;
            return _boardingRoot;
        }

        /// <summary>A self-propelled stair truck: chassis, cab, and a flight built to this door's sill.</summary>
        private StairTruckView TakeStairTruck(string registration)
        {
            if (_stairTrucks.TryGetValue(registration, out var truck))
                return truck;
            if (_stairTruckPool.Count > 0)
            {
                truck = _stairTruckPool[_stairTruckPool.Count - 1];
                _stairTruckPool.RemoveAt(_stairTruckPool.Count - 1);
            }
            else
            {
                truck = new StairTruckView { Root = new GameObject("Stair truck").transform };
                truck.Root.SetParent(BoardingRoot(), false);
                // Local +Z points at the aircraft; the platform meets the door at z = 0.
                BridgeBox(truck.Root, "Stair truck chassis", new Vector3(0f, 0.55f, -4.2f), new Vector3(2.2f, 0.9f, 6.4f),
                    new Color(0.22f, 0.24f, 0.27f));
                BridgeBox(truck.Root, "Stair truck cab", new Vector3(0f, 1.45f, -6.6f), new Vector3(2.1f, 1.5f, 1.6f),
                    AirsideTheme.SafetyYellow);
                BridgeBox(truck.Root, "Stair truck cab glass", new Vector3(0f, 1.75f, -5.78f), new Vector3(1.8f, 0.6f, 0.04f),
                    BridgeGlazing);
                // Wheels and a bumper so the truck reads as a vehicle, not a box (ADR 0124).
                foreach (var (wx, wz) in new[] { (-1.05f, -1.8f), (1.05f, -1.8f), (-1.05f, -6.3f), (1.05f, -6.3f) })
                {
                    var wheel = BridgeCylinder(truck.Root, "Stair truck wheel", new Vector3(wx, 0.42f, wz),
                        new Vector3(0.84f, 0.16f, 0.84f), new Color(0.09f, 0.09f, 0.1f));
                    wheel.localRotation = Quaternion.Euler(0f, 0f, 90f);
                }

                BridgeBox(truck.Root, "Stair truck bumper", new Vector3(0f, 0.5f, -7.5f), new Vector3(2.3f, 0.3f, 0.25f),
                    new Color(0.15f, 0.15f, 0.16f));
                truck.Flight = new GameObject("Stair flight").transform;
                truck.Flight.SetParent(truck.Root, false);
            }

            truck.Registration = registration;
            truck.Root.gameObject.SetActive(true);
            _stairTrucks[registration] = truck;
            return truck;
        }

        private static void BuildStairFlight(StairTruckView truck, float sill)
        {
            if (Mathf.Abs(truck.BuiltForSill - sill) < 0.05f)
                return;
            for (var i = truck.Flight.childCount - 1; i >= 0; i--)
                Destroy(truck.Flight.GetChild(i).gameObject);
            truck.BuiltForSill = sill;

            var run = StairRun(sill);
            var steps = Mathf.Max(4, Mathf.RoundToInt(sill / 0.2f));
            // Platform at the door, then the flight falling away from the aircraft.
            BridgeBox(truck.Flight, "Stair platform", new Vector3(0f, sill - 0.05f, -0.75f), new Vector3(1.9f, 0.1f, 1.5f),
                new Color(0.62f, 0.64f, 0.66f));
            BridgeBox(truck.Flight, "Stair canopy", new Vector3(0f, sill + 2.2f, -0.75f), new Vector3(2.0f, 0.08f, 1.6f),
                new Color(0.86f, 0.87f, 0.88f));
            for (var k = 0; k < steps; k++)
            {
                var t = (k + 0.5f) / steps;
                var y = sill * (1f - t);
                var z = -1.5f - run * t;
                BridgeBox(truck.Flight, "Stair tread", new Vector3(0f, y, z), new Vector3(1.6f, 0.06f, run / steps + 0.05f),
                    new Color(0.58f, 0.60f, 0.62f));
            }

            var slope = Mathf.Atan2(sill, run) * Mathf.Rad2Deg;
            var length = Mathf.Sqrt(sill * sill + run * run);
            foreach (var edge in new[] { -0.9f, 0.9f })
            {
                var rail = BridgeBox(truck.Flight, "Stair side", new Vector3(edge, sill * 0.5f + 0.45f, -1.5f - run * 0.5f),
                    new Vector3(0.06f, 0.9f, length), AirsideTheme.SafetyYellow);
                rail.localRotation = Quaternion.Euler(-slope, 0f, 0f);
                // Handrail on posts, a metre above the treads, and a guard round the platform.
                var handrail = BridgeBox(truck.Flight, "Stair handrail", new Vector3(edge, sill * 0.5f + 1.05f, -1.5f - run * 0.5f),
                    new Vector3(0.07f, 0.07f, length), new Color(0.78f, 0.79f, 0.8f));
                handrail.localRotation = Quaternion.Euler(-slope, 0f, 0f);
                var posts = Mathf.Max(2, Mathf.RoundToInt(length / 1.3f));
                for (var k = 0; k <= posts; k++)
                {
                    var t = k / (float)posts;
                    BridgeBox(truck.Flight, "Stair post", new Vector3(edge, sill * (1f - t) + 0.5f, -1.5f - run * t),
                        new Vector3(0.05f, 1.0f, 0.05f), new Color(0.78f, 0.79f, 0.8f));
                }

                BridgeBox(truck.Flight, "Stair platform rail", new Vector3(edge, sill + 1.0f, -0.75f),
                    new Vector3(0.06f, 0.06f, 1.5f), new Color(0.78f, 0.79f, 0.8f));
                BridgeBox(truck.Flight, "Stair platform post", new Vector3(edge, sill + 0.5f, -0.05f),
                    new Vector3(0.05f, 1.0f, 0.05f), new Color(0.78f, 0.79f, 0.8f));
            }
        }

        private static float StairRun(float sill) => Mathf.Max(1.2f, sill * 1.45f);

        private void UpdateStairTrucks()
        {
            _stairTruckWanted.Clear();
            if (FleetMode && _operations != null)
            {
                foreach (var aircraft in _operations.Fleet)
                {
                    var fraction = BoardingFlow.StairTruckFraction(aircraft, _preciseTime);
                    if (fraction <= 0.001f || !_fleetViewById.TryGetValue(aircraft.Registration, out var view) || view == null)
                        continue;
                    _stairTruckWanted.Add(aircraft.Registration);
                    var truck = TakeStairTruck(aircraft.Registration);
                    var (doorSill, into) = JetDoorSill(aircraft, view);
                    var sill = Mathf.Max(0.6f, doorSill.y - view.position.y);
                    BuildStairFlight(truck, sill);
                    var dock = new Vector3(doorSill.x, view.position.y, doorSill.z) - into * 0.45f;
                    var eased = Mathf.SmoothStep(0f, 1f, fraction);
                    truck.Root.position = dock - into * (1f - eased) * StairTruckApproachMetres;
                    truck.Root.rotation = Quaternion.LookRotation(into, Vector3.up);
                }
            }

            if (_stairTrucks.Count == _stairTruckWanted.Count)
                return;
            var gone = new List<string>();
            foreach (var pair in _stairTrucks)
                if (!_stairTruckWanted.Contains(pair.Key))
                    gone.Add(pair.Key);
            foreach (var registration in gone)
            {
                var truck = _stairTrucks[registration];
                truck.Root.gameObject.SetActive(false);
                _stairTrucks.Remove(registration);
                _stairTruckPool.Add(truck);
            }
        }

        /// <summary>World L1 door sill point of a jet and the horizontal direction into its fuselage.</summary>
        private static (Vector3 Sill, Vector3 Into) JetDoorSill(FleetAircraft aircraft, Transform view)
        {
            var door = AircraftDoors.L1(aircraft.Type);
            var sill = view.TransformPoint(new Vector3(door.LocalX, door.SillY, door.LocalZ));
            var into = Flat(view.right * (door.LocalX < 0f ? 1f : -1f));
            return (sill, into.sqrMagnitude > 0.001f ? into.normalized : Vector3.right);
        }

        // ---- Passengers ----------------------------------------------------------------------

        private static readonly string[] PassengerCharacters =
        {
            "chr_passenger_m_casual", "chr_passenger_f_casual", "chr_passenger_m_hoodie", "chr_passenger_f_formal",
            "chr_passenger_m_suit", "chr_passenger_f_suit", "chr_passenger_m_holiday"
        };

        /// <summary>
        /// The hi-vis ramp workers. ADR 0114 imported these two and never placed them, so the
        /// only people ever drawn were boarding passengers — and those appear for stairs
        /// boarding only, leaving an aerobridge gate with nobody on the apron at all.
        /// </summary>
        private static readonly string[] RampCharacters =
        {
            "chr_ramp_m_worker", "chr_ramp_f_worker"
        };

        private const int MaxVisiblePassengers = 60;
        private const float PassengerStairSpeed = 0.55f;

        private sealed class CharacterKind
        {
            public GameObject Prefab;
            public AnimationClip Walk;
            public AnimationClip Idle;
            public AnimationClip Interact;
            public AnimationClip Wave;
            public float ScaleToMetre = 1f;
        }

        private sealed class PassengerView
        {
            public GameObject Instance;
            public CharacterKind Kind;
            public float Height;
            public float Phase;
            public FigureRig Rig;
            public HandTools.Kit Bag;
            public int BagLook = -1;
        }

        private readonly struct WalkPath
        {
            public WalkPath(Vector3[] points, int stairStart)
            {
                Points = points;
                StairStart = stairStart;
            }

            /// <summary>Terminal → approach → stair foot → door. Stair segment from <see cref="StairStart"/>.</summary>
            public Vector3[] Points { get; }
            public int StairStart { get; }
        }

        private readonly List<CharacterKind> _characterKinds = new();
        private readonly List<CharacterKind> _rampKinds = new();
        private readonly List<RampCrewMember> _rampScratch = new();
        private sealed class RampCrewPerson
        {
            public GameObject Instance;
            public Transform Root;
            public CharacterKind Kind;
            public FigureRig Rig;
            public HandTools.Kit Kit;
            public RampTask? KitTask;
        }

        private sealed class RampCrewSet
        {
            public string Registration;
            public readonly List<RampCrewPerson> People = new();
            public ServiceProps Props;
            public readonly ServiceScene Scene = new();
            public readonly List<CrewAction> Actions = new();
        }

        private const int MaxRampCrewAircraft = 8;
        private readonly Dictionary<string, RampCrewSet> _rampCrewByAircraft = new(StringComparer.Ordinal);
        private readonly List<RampCrewSet> _rampCrewPool = new();
        private readonly HashSet<string> _rampCrewWanted = new(StringComparer.Ordinal);
        private bool _charactersLoaded;
        private readonly Dictionary<long, PassengerView> _passengers = new();
        private readonly Dictionary<CharacterKind, List<PassengerView>> _passengerPool = new();
        private readonly HashSet<long> _passengersWanted = new();
        private readonly List<PassengerMove> _moveScratch = new();
        private readonly List<long> _passengerScratch = new();
        private float[] _terminalFootprint;

        private void UpdateBoardingPresentation()
        {
            UpdateStairTrucks();
            UpdatePassengerBuses();
            UpdatePassengers();
            UpdateAllRampCrew();
        }

        private void UpdatePassengerBuses()
        {
            _remoteBusWanted.Clear();
            if (FleetMode && _operations != null && AirsideFocusMode.ShowTurnaroundVehicles)
            {
                foreach (var aircraft in _operations.Fleet)
                {
                    if (_remoteBusWanted.Count >= MaxRemoteBuses)
                        break;
                    var fraction = BoardingFlow.RemoteBusFraction(aircraft, _preciseTime);
                    if (fraction <= 0.001f || !_fleetViewById.TryGetValue(aircraft.Registration, out var aircraftView)
                        || aircraftView == null || !TryRemoteBusStop(aircraft, aircraftView, out var stop, out var park))
                        continue;
                    var bus = TakePassengerBus(aircraft.Registration);
                    if (bus == null)
                        continue;
                    _remoteBusWanted.Add(aircraft.Registration);
                    var eased = Mathf.SmoothStep(0f, 1f, fraction);
                    // The trip is a function of simulation time, so it follows one route planned
                    // round the parked aircraft and buildings between its park bay and the stand.
                    if (bus.Route.Count < 4 || Flat(bus.RoutedFrom - park).sqrMagnitude > 1f
                        || Flat(bus.RoutedTo - stop).sqrMagnitude > 1f)
                    {
                        PlanFixedRoute(park, stop, VehicleClearanceMetres, true, bus.Route);
                        bus.RoutedFrom = park;
                        bus.RoutedTo = stop;
                    }

                    var along = GroundRouter.Along(bus.Route, eased * GroundRouter.Length(bus.Route));
                    bus.Root.gameObject.SetActive(true);
                    bus.Root.position = new Vector3(along.X, stop.y, along.Z);
                    var direction = new Vector3(along.DirX, 0f, along.DirZ);
                    if (direction.sqrMagnitude > 0.001f)
                        bus.Root.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
                }
            }

            foreach (var bus in _remoteBusPool)
            {
                if (bus.Registration == null || _remoteBusWanted.Contains(bus.Registration))
                    continue;
                _remoteBuses.Remove(bus.Registration);
                bus.Registration = null;
                bus.Route.Clear();
                bus.Root.gameObject.SetActive(false);
            }
        }

        private PassengerBusView TakePassengerBus(string registration)
        {
            if (_remoteBuses.TryGetValue(registration, out var owned))
                return owned;
            PassengerBusView bus = null;
            foreach (var candidate in _remoteBusPool)
            {
                if (candidate.Registration == null)
                {
                    bus = candidate;
                    break;
                }
            }
            if (bus == null)
            {
                if (_remoteBusPool.Count >= MaxRemoteBuses)
                    return null;
                var root = BuildServiceVehicle("Passenger bus (remote)", new Color(0.22f, 0.44f, 0.55f),
                    new Vector3(3.8f, 1.5f, 1.45f), PreferArtKit(
                        "Models/Vehicles/mdl_passenger_bus_apron_v06.gltf",
                        "Models/Vehicles/mdl_passenger_bus_apron_v05.gltf",
                        "Models/Vehicles/mdl_passenger_bus_apron_authored_v01.gltf"));
                OrientPlusXKitToForward(root);
                root.SetParent(BoardingRoot(), true);
                bus = new PassengerBusView { Root = root };
                _remoteBusPool.Add(bus);
            }
            bus.Registration = registration;
            _remoteBuses[registration] = bus;
            return bus;
        }

        private void UpdatePassengers()
        {
            _passengersWanted.Clear();
            if (FleetMode && _operations != null && EnsureCharacters())
            {
                var level = _operations.CareerState?.BaseLevel ?? PlayerBaseLevel.Starter;
                foreach (var aircraft in _operations.Fleet)
                {
                    if (_passengersWanted.Count >= MaxVisiblePassengers)
                        break;
                    var mode = BoardingFlow.ModeFor(aircraft);
                    if (mode == BoardingMode.None)
                        continue;
                    if (!_fleetViewById.TryGetValue(aircraft.Registration, out var view) || view == null
                        || !view.gameObject.activeInHierarchy)
                        continue;
                    BoardingFlow.Moves(aircraft, _preciseTime, _moveScratch, 240, level);
                    if (_moveScratch.Count == 0 || !TryWalkPath(aircraft, view, mode, out var path))
                        continue;
                    var walking = _passengersWanted.Count;
                    foreach (var move in _moveScratch)
                    {
                        if (_passengersWanted.Count >= MaxVisiblePassengers)
                            break;
                        if (!PlacePassenger(aircraft, move, path))
                            continue;
                    }

                    if (_passengersWanted.Count > walking && mode != BoardingMode.Aerobridge)
                        KeepWalkwayTape(aircraft.Registration, path);
                }
            }

            UpdateWalkwayTape();

            _passengerScratch.Clear();
            foreach (var pair in _passengers)
                if (!_passengersWanted.Contains(pair.Key))
                    _passengerScratch.Add(pair.Key);
            foreach (var key in _passengerScratch)
            {
                var person = _passengers[key];
                person.Instance.SetActive(false);
                if (person.Bag != null)
                    person.Bag.Root.gameObject.SetActive(false);
                _passengers.Remove(key);
                if (!_passengerPool.TryGetValue(person.Kind, out var pool))
                    _passengerPool[person.Kind] = pool = new List<PassengerView>();
                pool.Add(person);
            }
        }

        private bool PlacePassenger(FleetAircraft aircraft, PassengerMove move, WalkPath path)
        {
            var elapsed = (float)(_preciseTime - move.StartSeconds);
            if (elapsed < 0f)
                return false;
            if (!Locate(path, elapsed, move.SpeedMetresPerSecond, move.Boarding,
                    out var position, out var heading, out var narrow))
                return false; // arrived: inside the cabin or the terminal

            // Three walking lanes stop a full load from occupying the exact same line. Stairs
            // remain single-file; their clear width is intentionally narrow.
            if (!narrow && heading.sqrMagnitude > 0.0001f)
            {
                var lane = move.Look % 3 - 1;
                var lateral = Vector3.Cross(Vector3.up, heading.normalized);
                position += lateral * (lane * 0.48f);
            }

            var key = ((long)aircraft.Registration.GetHashCode() << 20) ^ ((long)move.Index << 1) ^ (move.Boarding ? 1L : 0L);
            _passengersWanted.Add(key);
            if (!_passengers.TryGetValue(key, out var person))
            {
                person = TakePassenger(_characterKinds[move.Look % _characterKinds.Count], move.Look);
                _passengers[key] = person;
            }

            var t = person.Instance.transform;
            t.position = position;
            if (heading.sqrMagnitude > 0.0001f)
                t.rotation = Quaternion.LookRotation(heading, Vector3.up);
            var clip = person.Kind.Walk != null ? person.Kind.Walk : person.Kind.Idle;
            if (clip != null && clip.length > 0.01f)
            {
                // Stride roughly matched to pace; sampled on the simulation clock so it never
                // depends on frame rate.
                var cycle = elapsed * (move.SpeedMetresPerSecond / 1.3f) + person.Phase;
                clip.SampleAnimation(person.Instance, cycle % clip.length);
            }

            if (person.BagLook != move.Look)
                GiveHandLuggage(person, move.Look);
            if (person.Bag != null)
            {
                // On a turboprop the roller bag is left planeside at the stairs (ADR 0176).
                var keeps = !LeftBagPlaneside(aircraft, move, path, elapsed);
                person.Bag.Root.gameObject.SetActive(keeps);
                if (keeps)
                    HandTools.Pose(person.Bag, person.Rig, lifted: narrow);
            }

            return true;
        }

        /// <summary>Pooled passengers change look; their luggage follows the new one.</summary>
        private void GiveHandLuggage(PassengerView person, int look)
        {
            if (person.Bag != null)
                Destroy(person.Bag.Root.gameObject);
            person.BagLook = look;
            person.Bag = person.Rig != null ? HandTools.BuildPassengerBag(look, BoardingRoot()) : null;
        }

        private PassengerView TakePassenger(CharacterKind kind, int look)
        {
            if (_passengerPool.TryGetValue(kind, out var pool) && pool.Count > 0)
            {
                var reused = pool[pool.Count - 1];
                pool.RemoveAt(pool.Count - 1);
                reused.Instance.SetActive(true);
                return reused;
            }

            var instance = Instantiate(kind.Prefab, BoardingRoot());
            instance.name = "Passenger";
            // Clips are sampled on the simulation clock; an imported Animator with no
            // controller would only cost time.
            var animator = instance.GetComponent<Animator>();
            if (animator != null)
                animator.enabled = false;
            var height = 1.58f + look % 31 / 100f;
            instance.transform.localScale = Vector3.one * kind.ScaleToMetre * height;
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                // Keep FBX colours but make sure every material draws under URP.
                var materials = renderer.sharedMaterials;
                for (var i = 0; i < materials.Length; i++)
                {
                    // Every figure is drawn opaque in the project's own surface material, whatever
                    // shader or (source-pack) alpha the FBX importer produced.
                    var source = materials[i];
                    var colour = source != null ? source.color : Color.grey;
                    colour.a = 1f;
                    materials[i] = CreateSharedSurfaceMaterial(colour);
                }

                renderer.sharedMaterials = materials;
                if (renderer is SkinnedMeshRenderer skinned)
                    skinned.updateWhenOffscreen = false;
            }

            return new PassengerView
            {
                Instance = instance, Kind = kind, Height = height, Phase = look % 97 / 97f,
                Rig = FigureRig.Find(instance)
            };
        }

        /// <summary>
        /// Draws a small crew team at every active turnaround, not just the earliest player
        /// departure. The activity is derived from fleet state and simulation time; characters
        /// have no autonomous state to save and remain correct through pause, catch-up and reload.
        /// </summary>
        private void UpdateAllRampCrew()
        {
            _rampCrewWanted.Clear();
            if (FleetMode && _operations != null && EnsureCharacters() && _rampKinds.Count > 0)
            {
                foreach (var aircraft in _operations.Fleet)
                {
                    if (_rampCrewWanted.Count >= MaxRampCrewAircraft)
                        break;
                    if (!TryRampActivity(aircraft, out var activity, out var progress, out var seconds))
                        continue;
                    UpdateRampCrew(aircraft, activity, progress, seconds);
                }
            }

            RestUntouchedServiceVehicles();

            foreach (var set in _rampCrewPool)
            {
                if (set.Registration == null || _rampCrewWanted.Contains(set.Registration))
                    continue;
                _rampCrewByAircraft.Remove(set.Registration);
                set.Registration = null;
                HideServiceProps(set);
                foreach (var person in set.People)
                {
                    person.Instance.SetActive(false);
                    if (person.Kit != null)
                        person.Kit.Root.gameObject.SetActive(false);
                }
            }
        }

        private bool TryRampActivity(FleetAircraft aircraft, out RampActivity activity, out double progress,
            out double seconds)
        {
            activity = RampActivity.None;
            progress = 0;
            seconds = 1;
            if (aircraft == null || aircraft.State != FleetState.AtStand || string.IsNullOrEmpty(aircraft.Stand.Value)
                || BoardingFlow.InCheck(aircraft, _preciseTime))
                return false;
            var onStand = Math.Max(0.0, _preciseTime - aircraft.StateStartedAt.ElapsedSeconds);
            if (onStand < 90.0)
            {
                activity = RampActivity.Arrival;
                progress = onStand / 90.0;
                seconds = 90.0;
                return true;
            }

            double? toDeparture = aircraft.Scheduled is { Cancelled: false } booked
                ? booked.DepartAt.ElapsedSeconds - _preciseTime
                : null;
            if (aircraft.Airline.IsPlayer && aircraft.Scheduled.HasValue)
            {
                var prep = DeparturePrep.For(aircraft, _clock.Now, _operations.CareerState.BaseLevel);
                if (!prep.Ready && prep.Stage is not (DeparturePrepStage.Idle or DeparturePrepStage.Ready))
                {
                    activity = prep.Stage switch
                    {
                        DeparturePrepStage.Fuel => RampActivity.Fuel,
                        DeparturePrepStage.Catering => RampActivity.Catering,
                        DeparturePrepStage.Baggage => RampActivity.Baggage,
                        _ => RampActivity.Boarding
                    };
                    progress = prep.StageProgress;
                    seconds = DeparturePrep.StageSecondsFor(aircraft.Type, prep.Stage, _operations.CareerState.BaseLevel);
                    return true;
                }
            }

            if (toDeparture is <= PushbackTugTimeline.ApproachLeadSeconds and > -30.0)
            {
                activity = RampActivity.Pushback;
                progress = Math.Clamp((PushbackTugTimeline.ApproachLeadSeconds - toDeparture.Value)
                    / PushbackTugTimeline.ApproachLeadSeconds, 0.0, 1.0);
                seconds = PushbackTugTimeline.ApproachLeadSeconds;
                return true;
            }

            if (aircraft.Airline.IsPlayer)
                return false;
            if (onStand is >= 7 * 60.0 and < 11 * 60.0 && (!toDeparture.HasValue || toDeparture > 10 * 60.0))
            {
                activity = RampActivity.Catering;
                progress = (onStand - 7 * 60.0) / (4 * 60.0);
                seconds = 4 * 60.0;
                return true;
            }
            if (ApronServiceSchedule.BaggageAlongside(onStand, toDeparture) && onStand < 4 * 60.0)
            {
                activity = RampActivity.Baggage;
                progress = (onStand - ApronServiceSchedule.BaggageArrivesAfterSeconds) / (8 * 60.0);
                seconds = 8 * 60.0;
                return true;
            }
            if (ApronServiceSchedule.FuelAlongside(onStand, toDeparture))
            {
                activity = RampActivity.Fuel;
                progress = (onStand - ApronServiceSchedule.FuelArrivesAfterSeconds) / (8 * 60.0);
                seconds = 8 * 60.0;
                return true;
            }
            if (ApronServiceSchedule.BaggageAlongside(onStand, toDeparture))
            {
                activity = RampActivity.Baggage;
                progress = (onStand - ApronServiceSchedule.BaggageArrivesAfterSeconds) / (8 * 60.0);
                seconds = 8 * 60.0;
                return true;
            }
            if (toDeparture is <= 14 * 60.0 and > PushbackTugTimeline.ApproachLeadSeconds)
            {
                activity = RampActivity.Boarding;
                progress = 1.0 - (toDeparture.Value - PushbackTugTimeline.ApproachLeadSeconds)
                    / (14 * 60.0 - PushbackTugTimeline.ApproachLeadSeconds);
                seconds = 14 * 60.0 - PushbackTugTimeline.ApproachLeadSeconds;
                return true;
            }
            return false;
        }

        private void UpdateRampCrew(FleetAircraft aircraft, RampActivity activity, double progress, double seconds)
        {
            var layout = AircraftLayout.For(aircraft.Type);
            RampCrew.ForActivity(activity, progress, layout, _rampScratch);
            if (_rampScratch.Count == 0)
                return;
            var set = TakeRampCrewSet(aircraft.Registration);
            if (set == null)
                return;
            _rampCrewWanted.Add(aircraft.Registration);

            var pose = AdelaideGround.StandPose(aircraft.Stand);
            var nose = new Vector3(pose.NoseX, 0f, pose.NoseZ);
            if (nose.sqrMagnitude < 0.001f)
                nose = Vector3.forward;
            nose.Normalize();
            var ground = AirsideFlightPath.GroundY;

            // What each worker is doing right now, item by item (ADR 0176).
            var elapsed = Math.Clamp(progress, 0.0, 1.0) * seconds;
            IReadOnlyList<double> drops = null;
            (float X, float Z)? cart = null;
            if (activity == RampActivity.Boarding && layout.IsTurboprop
                && TryPlanesideDrops(aircraft, pose, _preciseTime - elapsed, out var at))
            {
                drops = _planesideDrops;
                cart = at;
            }

            var hiLoader = activity == RampActivity.Catering && layout.CateringTruck is { } dock
                && NearestServiceVehicle(LayoutToWorld(pose, dock, ground), 10f, _cateringTruck) != null;
            ServiceChoreography.Act(activity, aircraft.Type, elapsed, seconds, _rampScratch, set.Actions, set.Scene, drops, cart,
                hiLoader);
            DrawServiceScene(set, pose, ground, set.Scene);
            DriveServiceVehicleParts(activity, pose, layout, ground, set.Scene);

            for (var i = 0; i < _rampScratch.Count; i++)
            {
                var member = _rampScratch[i];
                var action = set.Actions[i];
                var person = EnsureRampWorker(set, i);
                if (person == null)
                    continue;
                person.Instance.SetActive(true);
                person.Root.position = LayoutToWorld(pose, (action.X, action.Z), ground + action.Height);
                person.Root.rotation = Quaternion.LookRotation(
                    Quaternion.AngleAxis(action.FacingDegrees, Vector3.up) * nose, Vector3.up);
                var clip = action.Walking && person.Kind.Walk != null ? person.Kind.Walk : RampClip(person.Kind, member.Task);
                if (clip != null && clip.length > 0.01f)
                {
                    var rate = action.Walking ? ServiceChoreography.CarryPace / 1.3 : 0.75;
                    clip.SampleAnimation(person.Instance, (float)((_preciseTime * rate + i * 0.37) % clip.length));
                }

                PoseCrewTool(person, member.Task, action.Item,
                    set.Scene.NozzleOut && member.Task == RampTask.FuelCoupling
                        ? LayoutToWorld(pose, set.Scene.HoseReel, ground + 1.0f)
                        : null);
            }
            for (var i = _rampScratch.Count; i < set.People.Count; i++)
            {
                set.People[i].Instance.SetActive(false);
                if (set.People[i].Kit != null)
                    set.People[i].Kit.Root.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Marshals wave their wands; carrying, radio and headset jobs stand with the arm down so
        /// a cone or radio hangs naturally; hands-on jobs (fuel, catering, bags) reach and work.
        /// </summary>
        private static AnimationClip RampClip(CharacterKind kind, RampTask task) => task switch
        {
            RampTask.MarshalArrival or RampTask.WingWalk => kind.Wave ?? kind.Interact,
            RampTask.PlaceSafetyEquipment or RampTask.BoardingSupervision or RampTask.PushbackHeadset
                => kind.Idle ?? kind.Interact,
            _ => kind.Interact ?? kind.Idle
        };

        private RampCrewSet TakeRampCrewSet(string registration)
        {
            if (_rampCrewByAircraft.TryGetValue(registration, out var owned))
                return owned;
            RampCrewSet set = null;
            foreach (var candidate in _rampCrewPool)
                if (candidate.Registration == null)
                {
                    set = candidate;
                    break;
                }
            if (set == null)
            {
                if (_rampCrewPool.Count >= MaxRampCrewAircraft)
                    return null;
                set = new RampCrewSet();
                _rampCrewPool.Add(set);
            }
            set.Registration = registration;
            _rampCrewByAircraft[registration] = set;
            return set;
        }

        private RampCrewPerson EnsureRampWorker(RampCrewSet set, int index)
        {
            while (set.People.Count <= index)
            {
                var slot = set.People.Count;
                var kind = _rampKinds[RampCrew.IsFemale(slot) && _rampKinds.Count > 1 ? 1 : 0];
                var instance = Instantiate(kind.Prefab, BoardingRoot());
                instance.name = $"Ramp worker {slot + 1}";
                var height = 1.72f + (slot % 2 == 0 ? 0.06f : -0.05f);
                instance.transform.localScale = Vector3.one * (kind.ScaleToMetre * height);
                var animator = instance.GetComponent<Animator>();
                if (animator != null)
                    animator.enabled = false;
                foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                set.People.Add(new RampCrewPerson
                {
                    Instance = instance, Root = instance.transform, Kind = kind, Rig = FigureRig.Find(instance)
                });
            }
            return set.People[index];
        }

        /// <summary>Equipment in the worker's hands (and ear defenders), posed from this frame's clip sample.</summary>
        private void PoseCrewTool(RampCrewPerson person, RampTask task, CarriedItem carrying, Vector3? hoseReel)
        {
            if (person.Rig == null)
                return;
            if (person.KitTask != task || person.Kit == null)
            {
                if (person.Kit != null)
                    Destroy(person.Kit.Root.gameObject);
                person.Kit = HandTools.BuildRampKit(task, BoardingRoot());
                person.KitTask = task;
            }

            person.Kit.Root.gameObject.SetActive(person.Instance.activeSelf);
            HandTools.SetCarried(person.Kit, carrying);
            person.Kit.HoseAnchor = hoseReel;
            HandTools.Pose(person.Kit, person.Rig);
        }

        private bool EnsureCharacters()
        {
            if (_charactersLoaded)
                return _characterKinds.Count > 0;
            _charactersLoaded = true;
            LoadCharacterKinds(PassengerCharacters, _characterKinds);
            LoadCharacterKinds(RampCharacters, _rampKinds);
            return _characterKinds.Count > 0;
        }

        /// <summary>
        /// Load a set of character prefabs and their Walk/Idle clips, normalised to a 1 m
        /// figure. Shared by the boarding passengers and the hi-vis ramp crew so the two sets
        /// cannot drift apart in how they are imported or scaled.
        /// </summary>
        private void LoadCharacterKinds(string[] ids, List<CharacterKind> into)
        {
            foreach (var id in ids)
            {
                var prefab = Resources.Load<GameObject>("Airside/Characters/" + id);
                if (prefab == null)
                    continue;
                var kind = new CharacterKind { Prefab = prefab };
                foreach (var clip in Resources.LoadAll<AnimationClip>("Airside/Characters/" + id))
                {
                    if (clip.name.EndsWith("Walk", StringComparison.Ordinal))
                        kind.Walk = clip;
                    else if (clip.name.EndsWith("Idle_Neutral", StringComparison.Ordinal))
                        kind.Idle = clip;
                    else if (clip.name.EndsWith("Interact", StringComparison.Ordinal))
                        kind.Interact = clip;
                    else if (clip.name.EndsWith("Wave", StringComparison.Ordinal))
                        kind.Wave = clip;
                }

                // Normalise to a 1 m tall figure; each passenger then gets their own height.
                var probe = Instantiate(prefab);
                var measured = MeasureFigureHeight(probe);
                kind.ScaleToMetre = measured > 0.1f ? 1f / measured : 1f / 1.8f;
                Destroy(probe);
                into.Add(kind);
            }
        }

        /// <summary>
        /// World height of a skinned figure, from its posed vertices. The Blender FBX puts the rig
        /// under a ×100 node, so an unscaled person is ~182 m tall — and a fresh skinned
        /// renderer's bounds read ~18.8 km, which shrank every person to under 2 cm.
        /// </summary>
        public static float MeasureFigureHeight(GameObject figure)
        {
            var low = float.MaxValue;
            var high = float.MinValue;
            var baked = new Mesh();
            var vertices = new List<Vector3>();
            foreach (var skinned in figure.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                // Baked with its renderer's scale, the mesh is in that renderer's local space.
                skinned.BakeMesh(baked, true);
                baked.GetVertices(vertices);
                var place = skinned.transform.localToWorldMatrix;
                foreach (var vertex in vertices)
                {
                    var y = place.MultiplyPoint3x4(vertex).y;
                    low = Mathf.Min(low, y);
                    high = Mathf.Max(high, y);
                }
            }

            if (Application.isPlaying)
                Destroy(baked);
            else
                DestroyImmediate(baked);
            return high > low ? high - low : 0f;
        }

        /// <summary>
        /// The walk between the terminal and this aircraft's stairs: out of the nearest terminal
        /// door, round to a point ahead of and outboard of the door (clear of the propellers),
        /// to the foot of the stairs, then up to the sill.
        /// </summary>
        private bool TryWalkPath(FleetAircraft aircraft, Transform view, BoardingMode mode, out WalkPath path)
        {
            path = default;
            var ground = view.position.y;

            if (mode == BoardingMode.Aerobridge)
            {
                foreach (var bridge in _aerobridges)
                {
                    if (!bridge.Site.Gate.Equals(aircraft.Stand) || !bridge.HasDocked || bridge.Shown < 0.92f)
                        continue;
                    var door = AircraftDoors.L1(aircraft.Type);
                    var bridgeTop = view.TransformPoint(new Vector3(door.LocalX, door.SillY + 0.05f, door.LocalZ));
                    var start = new Vector3(bridge.Site.RotundaX, bridge.Floor + 0.05f, bridge.Site.RotundaZ);
                    var cab = bridge.Cab.position + Vector3.up * 0.05f;
                    path = new WalkPath(new[] { start, Vector3.Lerp(start, cab, 0.5f), cab, bridgeTop }, int.MaxValue);
                    return true;
                }
                return false;
            }

            Vector3 top;
            Vector3 foot;
            Vector3 into;
            if (BoardingFlow.UsesStairTruck(mode))
            {
                var (sill, inward) = JetDoorSill(aircraft, view);
                into = inward;
                top = sill - into * 0.9f;
                var rise = Mathf.Max(0.6f, sill.y - ground);
                foot = new Vector3(top.x, ground, top.z) - into * (0.8f + StairRun(rise));
            }
            else
            {
                var airstair = view.GetComponentInChildren<AirstairDoor>(true);
                if (airstair == null)
                    return false;
                var hinge = airstair.transform.position;
                into = Flat(view.right * -airstair.Side).normalized;
                top = hinge + into * 0.3f;
                foot = new Vector3(hinge.x, ground, hinge.z) - into * (airstair.RunMetres + 0.4f);
            }

            var forward = Flat(view.forward).normalized;
            var approach = foot - into * 4f + forward * 3f;
            // ADR 0187: a fenced walkway leads from the terminal wall; people stay inside the tape until its end.
            Vector3? leadIn = null;
            var origin = mode == BoardingMode.RemoteBus && TryRemoteBusStop(aircraft, view, out var busStop, out _)
                ? busStop + into * 2.0f
                : NearestTerminalDoor(foot, ground);
            if (mode != BoardingMode.RemoteBus && AdelaideWalkwayGeometry.TryCorridor(aircraft.Stand, out var corridor))
            {
                leadIn = new Vector3(corridor[0], ground, corridor[1]);
                origin = new Vector3(corridor[2], ground, corridor[3]);
            }

            path = RoutedWalk(aircraft.Registration, origin, approach, foot, top, leadIn);
            return true;
        }

        private readonly Dictionary<string, (Vector3 Origin, Vector3 Approach, WalkPath Path)> _walkRoutes =
            new(System.StringComparer.Ordinal);

        /// <summary>
        /// Terminal (or bus) → round the aircraft → stair foot → door. The apron leg is planned
        /// round every parked airframe, its propeller arcs and low wings, and the buildings, so
        /// nobody walks through a wing or a turning propeller. Cached: this runs every frame.
        /// </summary>
        private WalkPath RoutedWalk(string registration, Vector3 origin, Vector3 approach, Vector3 foot, Vector3 top,
            Vector3? leadIn = null)
        {
            if (_walkRoutes.TryGetValue(registration, out var cached)
                && (cached.Origin - origin).sqrMagnitude < 0.25f && (cached.Approach - approach).sqrMagnitude < 0.25f)
                return cached.Path;
            var route = PlanFixedRoute(origin, approach, PersonClearanceMetres, false, new List<float>());
            var points = new List<Vector3>();
            if (leadIn.HasValue)
                points.Add(leadIn.Value);
            for (var i = 0; i + 1 < route.Count; i += 2)
                points.Add(new Vector3(route[i], origin.y, route[i + 1]));
            points.Add(foot);
            points.Add(top);
            var path = new WalkPath(points.ToArray(), points.Count - 2);
            _walkRoutes[registration] = (origin, approach, path);
            return path;
        }

        private static bool TryRemoteBusStop(FleetAircraft aircraft, Transform view,
            out Vector3 stop, out Vector3 park)
        {
            stop = default;
            park = default;
            if (aircraft == null || view == null || !BoardingFlow.UsesRemoteBus(BoardingFlow.ModeFor(aircraft)))
                return false;
            var (sill, inward) = JetDoorSill(aircraft, view);
            var into = inward.sqrMagnitude > 0.001f ? inward.normalized : Vector3.right;
            var forward = Flat(view.forward).normalized;
            var foot = new Vector3(sill.x, view.position.y, sill.z) - into *
                (0.8f + StairRun(Mathf.Max(0.6f, sill.y - view.position.y)));
            // The bus door faces the walking lane, outside the stair manoeuvring box.
            stop = foot - into * 9.5f + forward * 2.5f;
            park = stop - forward * 34f - into * 8f;
            return true;
        }

        private Vector3 NearestTerminalDoor(Vector3 from, float ground)
        {
            _terminalFootprint ??= TerminalFootprintXz();
            if (_terminalFootprint == null)
                return from - Vector3.forward * 30f;
            var best = Vector2.zero;
            var bestDistance = float.MaxValue;
            var p = new Vector2(from.x, from.z);
            var n = _terminalFootprint.Length / 2;
            for (var i = 0; i < n; i++)
            {
                var a = new Vector2(_terminalFootprint[i * 2], _terminalFootprint[i * 2 + 1]);
                var j = (i + 1) % n;
                var b = new Vector2(_terminalFootprint[j * 2], _terminalFootprint[j * 2 + 1]);
                var ab = b - a;
                var t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
                var q = a + ab * t;
                var d = (p - q).sqrMagnitude;
                if (d >= bestDistance)
                    continue;
                bestDistance = d;
                best = q;
            }

            var outward = (p - best).normalized;
            var door = best + outward * 1.2f;
            return new Vector3(door.x, ground, door.y);
        }

        private static float[] TerminalFootprintXz()
        {
            foreach (var outline in AdelaideLayout.Terminals)
                if (outline.Name == "Domestic & International Terminal")
                    return outline.Xz;
            return null;
        }

        /// <summary>Where a walker is after <paramref name="elapsed"/> seconds; false once they have arrived.</summary>
        private static bool Locate(WalkPath path, float elapsed, float speed, bool boarding, out Vector3 position,
            out Vector3 heading, out bool narrow)
        {
            position = default;
            heading = default;
            narrow = false;
            var points = path.Points;
            var remaining = elapsed;
            // Boarding walks the path forwards; deplaning walks it back from the door.
            var count = points.Length;
            for (var s = 0; s < count - 1; s++)
            {
                var i = boarding ? s : count - 1 - s;
                var j = boarding ? i + 1 : i - 1;
                var a = points[i];
                var b = points[j];
                var onStairs = Mathf.Min(i, j) >= path.StairStart;
                var pace = onStairs ? PassengerStairSpeed : speed;
                var length = Vector3.Distance(a, b);
                var duration = length / pace;
                if (remaining <= duration)
                {
                    var f = duration > 0f ? remaining / duration : 1f;
                    position = Vector3.Lerp(a, b, f);
                    heading = Flat(b - a);
                    narrow = onStairs;
                    return true;
                }

                remaining -= duration;
            }

            return false;
        }
    }
}

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

        private const int MaxVisiblePassengers = 60;
        private const float PassengerStairSpeed = 0.55f;

        private sealed class CharacterKind
        {
            public GameObject Prefab;
            public AnimationClip Walk;
            public AnimationClip Idle;
            public float ScaleToMetre = 1f;
        }

        private sealed class PassengerView
        {
            public GameObject Instance;
            public CharacterKind Kind;
            public float Height;
            public float Phase;
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
            UpdatePassengers();
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
                    if (mode is not (BoardingMode.IntegralAirstair or BoardingMode.StairTruck))
                        continue;
                    if (!_fleetViewById.TryGetValue(aircraft.Registration, out var view) || view == null
                        || !view.gameObject.activeInHierarchy)
                        continue;
                    BoardingFlow.Moves(aircraft, _preciseTime, _moveScratch, 240, level);
                    if (_moveScratch.Count == 0 || !TryWalkPath(aircraft, view, mode, out var path))
                        continue;
                    foreach (var move in _moveScratch)
                    {
                        if (_passengersWanted.Count >= MaxVisiblePassengers)
                            break;
                        if (!PlacePassenger(aircraft, move, path))
                            continue;
                    }
                }
            }

            _passengerScratch.Clear();
            foreach (var pair in _passengers)
                if (!_passengersWanted.Contains(pair.Key))
                    _passengerScratch.Add(pair.Key);
            foreach (var key in _passengerScratch)
            {
                var person = _passengers[key];
                person.Instance.SetActive(false);
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
            if (!Locate(path, elapsed, move.SpeedMetresPerSecond, move.Boarding, out var position, out var heading))
                return false; // arrived: inside the cabin or the terminal

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

            return true;
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

            return new PassengerView { Instance = instance, Kind = kind, Height = height, Phase = look % 97 / 97f };
        }

        private bool EnsureCharacters()
        {
            if (_charactersLoaded)
                return _characterKinds.Count > 0;
            _charactersLoaded = true;
            foreach (var id in PassengerCharacters)
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
                }

                // Normalise to a 1 m tall figure; each passenger then gets their own height.
                var probe = Instantiate(prefab);
                var bounds = new Bounds(probe.transform.position, Vector3.zero);
                var any = false;
                foreach (var renderer in probe.GetComponentsInChildren<Renderer>(true))
                {
                    if (!any)
                        bounds = renderer.bounds;
                    else
                        bounds.Encapsulate(renderer.bounds);
                    any = true;
                }

                kind.ScaleToMetre = any && bounds.size.y > 0.1f ? 1f / bounds.size.y : 1f / 1.8f;
                Destroy(probe);
                _characterKinds.Add(kind);
            }

            return _characterKinds.Count > 0;
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
            Vector3 top;
            Vector3 foot;
            Vector3 into;
            if (mode == BoardingMode.StairTruck)
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
            var terminal = NearestTerminalDoor(foot, ground);
            path = new WalkPath(new[] { terminal, approach, foot, top }, 2);
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
            out Vector3 heading)
        {
            position = default;
            heading = default;
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
                    return true;
                }

                remaining -= duration;
            }

            return false;
        }
    }
}

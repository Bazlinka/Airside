using System.Collections.Generic;
using Airside.Domain;
using Airside.Simulation;
using UnityEngine;

namespace Airside.Presentation
{
    /// <summary>
    /// The people and doors around the terminal: a sliding airside door where each walk to a stand begins and ends
    /// (<see cref="AdelaideTerminalDoors"/>), the next few boarders waiting in front of it, a gate agent at the door
    /// while a flight boards, and background passengers and staff on the ground (<see cref="AdelaideAmbientPeople"/>).
    /// Everything is a function of fleet state and the simulation clock; nothing here changes the simulation.
    /// </summary>
    public sealed partial class AirsidePrototype
    {
        /// <summary>The next boarders stand at the door this long before they start to walk.</summary>
        private const double BoardingQueueLeadSeconds = 28.0;

        private const int BoardingQueueMax = 6;

        /// <summary>Background people farther from the camera than this are not drawn.</summary>
        private const float AmbientCullMetres = 700f;

        private const long AmbientKeyBase = 1L << 61;
        private const long GateAgentKeyBase = 1L << 60;

        private sealed class TerminalDoorView
        {
            public TerminalDoor Door;
            public Transform Root;
            public Transform Left;
            public Transform Right;
            public float Open;
        }

        private readonly List<TerminalDoorView> _terminalDoors = new();
        private readonly List<PassengerMove> _queueScratch = new();
        private bool _terminalDoorsBuilt;

        private static float ApronY() =>
            (AirsideBareField.Enabled ? BoardingGroundContact.AdelaideApronY : BoardingGroundContact.MiniatureApronY)
            + (_airfieldRoot != null ? _airfieldRoot.position.y : 0f);

        /// <summary>The spot just outside the stand's terminal door where its walk begins.</summary>
        private static Vector3 TerminalDoorThreshold(FleetAircraft aircraft, Vector3 foot, float ground)
        {
            if (aircraft != null && AdelaideTerminalDoors.TryForBay(aircraft.Stand.Value, out var door)
                || AdelaideTerminalDoors.TryNearest(foot.x, foot.z, out door))
                return new Vector3(door.ThresholdX, ground, door.ThresholdZ);
            return Vector3.zero;
        }

        private static long PassengerKey(FleetAircraft aircraft, int index, bool boarding) =>
            ((long)aircraft.Registration.GetHashCode() << 20) ^ ((long)index << 1) ^ (boarding ? 1L : 0L);

        /// <summary>
        /// A person is drawn this often by distance: close figures every frame, distant ones pose less often (their
        /// position still moves every frame). Keeps a few hundred skinned meshes affordable.
        /// </summary>
        private bool PoseDue(Vector3 position, long key)
        {
            if (_mainCamera == null)
                return true;
            var d2 = (_mainCamera.transform.position - position).sqrMagnitude;
            var every = d2 < 90f * 90f ? 1 : d2 < 220f * 220f ? 2 : d2 < 500f * 500f ? 4 : 8;
            return every == 1 || (Time.frameCount + (int)(key & 7)) % every == 0;
        }

        // ---- Doors ---------------------------------------------------------------------------

        private void BuildTerminalDoors()
        {
            _terminalDoorsBuilt = true;
            var y = ApronY();
            var frame = new Color(0.22f, 0.24f, 0.27f);
            var glass = new Color(0.20f, 0.33f, 0.39f);
            var canopy = new Color(0.78f, 0.80f, 0.82f);
            var sign = new Color(0.30f, 0.60f, 0.64f);
            var w = AdelaideTerminalDoors.WidthMetres;
            foreach (var door in AdelaideTerminalDoors.Doors)
            {
                var root = new GameObject("Terminal door " + door.Id).transform;
                root.SetParent(BoardingRoot(), false);
                root.position = new Vector3(door.X, y, door.Z);
                // Local +Z points out of the building, onto the apron.
                root.rotation = Quaternion.LookRotation(new Vector3(door.NormalX, 0f, door.NormalZ), Vector3.up);
                BridgeBox(root, "Door post L", new Vector3(-w * 0.5f - 0.1f, 1.35f, 0.12f), new Vector3(0.2f, 2.7f, 0.32f), frame);
                BridgeBox(root, "Door post R", new Vector3(w * 0.5f + 0.1f, 1.35f, 0.12f), new Vector3(0.2f, 2.7f, 0.32f), frame);
                BridgeBox(root, "Door lintel", new Vector3(0f, 2.62f, 0.12f), new Vector3(w + 0.6f, 0.24f, 0.32f), frame);
                var left = BridgeBox(root, "Door panel L", new Vector3(-w * 0.25f, 1.2f, 0.1f), new Vector3(w * 0.5f - 0.04f, 2.4f, 0.06f), glass);
                var right = BridgeBox(root, "Door panel R", new Vector3(w * 0.25f, 1.2f, 0.1f), new Vector3(w * 0.5f - 0.04f, 2.4f, 0.06f), glass);
                BridgeBox(root, "Door canopy", new Vector3(0f, 3.05f, 1.4f), new Vector3(w + 1.8f, 0.14f, 2.8f), canopy);
                BridgeBox(root, "Canopy post L", new Vector3(-w * 0.5f - 0.7f, 1.5f, 2.6f), new Vector3(0.1f, 3.0f, 0.1f), frame);
                BridgeBox(root, "Canopy post R", new Vector3(w * 0.5f + 0.7f, 1.5f, 2.6f), new Vector3(0.1f, 3.0f, 0.1f), frame);
                BridgeBox(root, "Door sign", new Vector3(0f, 2.9f, 0.3f), new Vector3(1.8f, 0.3f, 0.06f), sign);
                _terminalDoors.Add(new TerminalDoorView { Door = door, Root = root, Left = left, Right = right });
            }
        }

        /// <summary>Doors slide open while anyone is at them and shut again once they have gone through.</summary>
        private void UpdateTerminalDoors()
        {
            var allowed = FleetMode && _operations != null && AirsideBareField.Enabled && _airfieldRoot != null;
            if (allowed && !_terminalDoorsBuilt)
                BuildTerminalDoors();
            if (!_terminalDoorsBuilt)
                return;
            var w = AdelaideTerminalDoors.WidthMetres;
            var step = Mathf.Min(0.1f, Time.deltaTime) / 0.7f;
            foreach (var view in _terminalDoors)
            {
                if (view.Root.gameObject.activeSelf != allowed)
                    view.Root.gameObject.SetActive(allowed);
                if (!allowed)
                    continue;
                var near = false;
                foreach (var person in _passengers.Values)
                {
                    var p = person.Instance.transform.position;
                    var dx = p.x - view.Door.X;
                    var dz = p.z - view.Door.Z;
                    if (dx * dx + dz * dz > 3.6f * 3.6f)
                        continue;
                    near = true;
                    break;
                }

                view.Open = Mathf.MoveTowards(view.Open, near ? 1f : 0f, step);
                var slide = Mathf.SmoothStep(0f, 1f, view.Open) * w * 0.45f;
                view.Left.localPosition = new Vector3(-w * 0.25f - slide, 1.2f, 0.1f);
                view.Right.localPosition = new Vector3(w * 0.25f + slide, 1.2f, 0.1f);
            }
        }

        // ---- People waiting at the door ------------------------------------------------------

        /// <summary>
        /// Boarders about to start their walk stand in front of the door, two abreast, and walk off when their turn comes
        /// (their key is the one the walk uses, so the same figure simply sets off).
        /// </summary>
        private void PlaceBoardingQueue(FleetAircraft aircraft, WalkPath path)
        {
            if (path.Points == null || path.Points.Length < 2)
                return;
            var first = Mathf.Min(path.OutsideStart, path.Points.Length - 2);
            var origin = path.Points[first];
            var direction = Flat(path.Points[first + 1] - origin);
            direction = direction.sqrMagnitude < 0.01f ? Vector3.forward : direction.normalized;
            var lateral = Vector3.Cross(Vector3.up, direction);
            var rank = 0;
            foreach (var move in _queueScratch)
            {
                if (!move.Boarding || move.StartSeconds <= _preciseTime)
                    continue;
                if (rank >= BoardingQueueMax)
                    break;
                var key = PassengerKey(aircraft, move.Index, true);
                _passengersWanted.Add(key);
                if (!_passengers.TryGetValue(key, out var person))
                {
                    person = TakePassenger(_characterKinds[move.Look % _characterKinds.Count], move.Look);
                    _passengers[key] = person;
                }

                var column = rank % 2 == 0 ? -1 : 1;
                var position = origin + direction * (0.9f + rank / 2 * 0.85f)
                               + lateral * (column * (0.75f + 0.12f * (move.Look % 3)));
                StandPerson(person, position, direction, key, move.Look, true);
                rank++;
            }
        }

        /// <summary>A gate agent in hi-vis stands by the door while a flight boards, facing the queue.</summary>
        private void PlaceGateAgent(FleetAircraft aircraft, WalkPath path)
        {
            if (path.Points == null || path.Points.Length < 2)
                return;
            var kinds = _rampKinds.Count > 0 ? _rampKinds : _characterKinds;
            var first = Mathf.Min(path.OutsideStart, path.Points.Length - 2);
            var origin = path.Points[first];
            var direction = Flat(path.Points[first + 1] - origin);
            direction = direction.sqrMagnitude < 0.01f ? Vector3.forward : direction.normalized;
            var lateral = Vector3.Cross(Vector3.up, direction);
            var hash = aircraft.Registration.GetHashCode() & 0xFFFFFF;
            var key = GateAgentKeyBase | (long)hash;
            _passengersWanted.Add(key);
            if (!_passengers.TryGetValue(key, out var person))
            {
                person = TakePassenger(kinds[hash % kinds.Count], hash);
                _passengers[key] = person;
            }

            var position = origin + direction * 0.5f + lateral * 1.55f;
            StandPerson(person, position, -lateral, key, hash, false);
        }

        /// <summary>Puts a person at a spot facing a direction, standing idle, holding their bag if they have one.</summary>
        private void StandPerson(PassengerView person, Vector3 position, Vector3 facing, long key, int look, bool bag)
        {
            var t = person.Instance.transform;
            t.position = position;
            if (facing.sqrMagnitude > 0.0001f)
                t.rotation = Quaternion.LookRotation(facing, Vector3.up);
            var due = PoseDue(position, key);
            var clip = person.Kind.Idle != null ? person.Kind.Idle : person.Kind.Walk;
            if (due && clip != null && clip.length > 0.01f)
            {
                var cycle = (float)_preciseTime * 0.8f + person.Phase * clip.length;
                clip.SampleAnimation(person.Instance, cycle % clip.length);
            }

            if (bag && person.BagLook != look)
                GiveHandLuggage(person, look);
            if (person.Bag == null)
                return;
            person.Bag.Root.gameObject.SetActive(bag);
            if (bag && due)
                HandTools.Pose(person.Bag, person.Rig, lifted: true);
        }

        // ---- Background people ---------------------------------------------------------------

        /// <summary>Passengers on the landside and staff along the airside wall, walking their lines on the simulation clock.</summary>
        private void PlaceAmbientPeople()
        {
            var walkers = AdelaideAmbientPeople.Walkers;
            var apronY = ApronY();
            var staffKinds = _rampKinds.Count > 0 ? _rampKinds : _characterKinds;
            for (var i = 0; i < walkers.Count; i++)
            {
                var walker = walkers[i];
                if (!AdelaideAmbientPeople.TryLocate(walker, _preciseTime, out var x, out var z, out var hx, out var hz,
                        out var moving))
                    continue;
                var y = walker.Staff ? apronY : AirsideAdelaideSurroundings.LandHeight(x, z);
                var position = new Vector3(x, y, z);
                if (_mainCamera != null && (position - _mainCamera.transform.position).sqrMagnitude > AmbientCullMetres * AmbientCullMetres)
                    continue;
                var kinds = walker.Staff ? staffKinds : _characterKinds;
                var key = AmbientKeyBase | (uint)walker.Id;
                _passengersWanted.Add(key);
                if (!_passengers.TryGetValue(key, out var person))
                {
                    person = TakePassenger(kinds[walker.Look % kinds.Count], walker.Look);
                    _passengers[key] = person;
                }

                var heading = new Vector3(hx, 0f, hz);
                var t = person.Instance.transform;
                t.position = position;
                if (heading.sqrMagnitude > 0.0001f)
                    t.rotation = Quaternion.LookRotation(heading, Vector3.up);
                var due = PoseDue(position, key);
                var clip = moving ? (person.Kind.Walk != null ? person.Kind.Walk : person.Kind.Idle)
                    : (person.Kind.Idle != null ? person.Kind.Idle : person.Kind.Walk);
                if (due && clip != null && clip.length > 0.01f)
                {
                    var cycle = moving
                        ? (float)(_preciseTime + walker.PhaseSeconds) * (walker.Speed / 1.3f) + person.Phase
                        : (float)_preciseTime * 0.8f + person.Phase * clip.length;
                    clip.SampleAnimation(person.Instance, cycle % clip.length);
                }

                // Looks 0-2 of every five carry a roller bag or holdall (HandTools.BuildPassengerBag).
                var carries = !walker.Staff && walker.Look % 5 < 3;
                if (carries && person.BagLook != walker.Look)
                    GiveHandLuggage(person, walker.Look);
                if (person.Bag == null)
                    continue;
                person.Bag.Root.gameObject.SetActive(carries);
                if (carries && due)
                    HandTools.Pose(person.Bag, person.Rig, lifted: false);
            }
        }
    }
}

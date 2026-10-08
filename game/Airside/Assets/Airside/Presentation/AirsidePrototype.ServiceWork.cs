using System;
using System.Collections.Generic;
using Airside.Domain;
using Airside.Simulation;
using UnityEngine;

namespace Airside.Presentation
{
    /// <summary>
    /// Draws <see cref="ServiceChoreography"/>: the loose bags, galley boxes and trolleys on
    /// their way into an aircraft, a jet's belt loader, a turboprop's planeside bag cart, the
    /// baggage train emptying, the catering hi-loader rising to the door and the fuel nozzle
    /// leaving its truck (ADR 0176). Presentation only; everything is a function of sim time.
    /// </summary>
    public sealed partial class AirsidePrototype
    {
        private static readonly Color BeltYellow = new(0.85f, 0.7f, 0.2f);
        private static readonly Color BeltDark = new(0.2f, 0.21f, 0.22f);

        private sealed class ServiceProps
        {
            public Transform Root;
            public readonly Dictionary<CarriedItem, List<Transform>> Loose = new();
            public Transform Belt;
            public Transform BeltSurface;
            public Transform BeltChassis;
            public Transform[] BeltTreads;
            public Transform Planeside;
            public readonly List<Transform> PlanesideBags = new();
        }

        private readonly HashSet<Transform> _serviceVehiclesTouched = new();
        private readonly Dictionary<Transform, (Vector3 Position, Vector3 Scale)> _liftRest = new();
        private readonly List<double> _planesideDrops = new();
        private readonly List<PassengerMove> _planesideMoves = new();

        private ServiceProps EnsureServiceProps(RampCrewSet set)
        {
            if (set.Props != null)
                return set.Props;
            var props = new ServiceProps { Root = new GameObject("Service work").transform };
            props.Root.SetParent(BoardingRoot(), false);
            set.Props = props;
            return props;
        }

        /// <summary>Place every piece of equipment the scene calls for; hide the rest.</summary>
        private void DrawServiceScene(RampCrewSet set, GroundPose pose, float ground, ServiceScene scene)
        {
            var props = EnsureServiceProps(set);
            props.Root.gameObject.SetActive(true);

            // Loose items in transit.
            var used = new Dictionary<CarriedItem, int>();
            foreach (var item in scene.Transit)
            {
                if (!props.Loose.TryGetValue(item.Kind, out var pool))
                    props.Loose[item.Kind] = pool = new List<Transform>();
                used.TryGetValue(item.Kind, out var n);
                if (pool.Count <= n)
                    pool.Add(HandTools.BuildItem(item.Kind, props.Root, pool.Count + 3));
                var t = pool[n];
                used[item.Kind] = n + 1;
                t.gameObject.SetActive(true);
                t.position = LayoutToWorld(pose, (item.X, item.Z), ground + item.Height - (item.Kind == CarriedItem.Bag ? 0.35f : 0f));
                t.rotation = Quaternion.LookRotation(new Vector3(pose.NoseX, 0f, pose.NoseZ), Vector3.up);
            }

            foreach (var pair in props.Loose)
            {
                used.TryGetValue(pair.Key, out var n);
                for (var i = n; i < pair.Value.Count; i++)
                    if (pair.Value[i].gameObject.activeSelf)
                        pair.Value[i].gameObject.SetActive(false);
            }

            // A jet's belt loader, foot on the apron and top at the hold sill.
            if (scene.BeltLoader)
            {
                if (props.Belt == null)
                {
                    props.Belt = BuildBeltLoader(props.Root, out props.BeltSurface, out props.BeltChassis);
                    props.BeltTreads = new Transform[18];
                    for (var i = 0; i < props.BeltTreads.Length; i++)
                        props.BeltTreads[i] = props.BeltSurface.Find("Conveyor tread " + i);
                }
                props.Belt.gameObject.SetActive(true);
                var foot = LayoutToWorld(pose, scene.BeltFoot, ground + 0.75f);
                var top = LayoutToWorld(pose, scene.BeltTop, ground + scene.BeltTopHeight);
                var span = top - foot;
                props.BeltSurface.SetPositionAndRotation((foot + top) * 0.5f, Quaternion.LookRotation(span.normalized, Vector3.up));
                props.BeltSurface.localScale = new Vector3(0.8f, 0.12f, span.magnitude + 0.4f);
                PoseBeltTreads(props.BeltTreads, _preciseTime, span.magnitude + .4f);
                var flat = new Vector3(span.x, 0f, span.z);
                var chassisCentre = Vector3.Lerp(foot, top, 0.35f);
                props.BeltChassis.SetPositionAndRotation(new Vector3(chassisCentre.x, ground + 0.35f, chassisCentre.z),
                    Quaternion.LookRotation(flat.normalized, Vector3.up));
            }
            else if (props.Belt != null)
                props.Belt.gameObject.SetActive(false);

            // A turboprop's planeside cart with the roller bags left on it.
            if (scene.PlanesideCart)
            {
                props.Planeside ??= BuildPlanesideCart(props.Root, props.PlanesideBags);
                props.Planeside.gameObject.SetActive(true);
                props.Planeside.SetPositionAndRotation(LayoutToWorld(pose, scene.PlanesideAt, ground),
                    Quaternion.LookRotation(new Vector3(pose.NoseX, 0f, pose.NoseZ), Vector3.up));
                for (var i = 0; i < props.PlanesideBags.Count; i++)
                    props.PlanesideBags[i].gameObject.SetActive(i < scene.PlanesideBags);
            }
            else if (props.Planeside != null)
                props.Planeside.gameObject.SetActive(false);
        }

        private void HideServiceProps(RampCrewSet set)
        {
            if (set.Props != null)
                set.Props.Root.gameObject.SetActive(false);
        }

        private static Transform BuildBeltLoader(Transform parent, out Transform surface, out Transform chassis)
        {
            var root = new GameObject("Belt loader").transform;
            root.SetParent(parent, false);
            chassis = new GameObject("Belt loader chassis").transform;
            chassis.SetParent(root, false);
            ParentBlock(chassis, "Chassis", Vector3.zero, new Vector3(1.8f, 0.5f, 4.2f), BeltYellow);
            ParentBlock(chassis, "Cab", new Vector3(0.55f, 0.55f, -1.5f), new Vector3(0.7f, 0.7f, 0.9f), BeltYellow * 0.85f);
            foreach (var x in new[] { -0.85f, 0.85f })
            foreach (var z in new[] { -1.5f, 1.5f })
                ParentBlock(chassis, "Wheel", new Vector3(x, -0.12f, z), new Vector3(0.22f, 0.5f, 0.5f), BeltDark);
            surface = new GameObject("Belt").transform;
            surface.SetParent(root, false);
            ParentBlock(surface, "Belt surface", Vector3.zero, Vector3.one, BeltDark);
            for (var i = 0; i < 18; i++)
                ParentBlock(surface, "Conveyor tread " + i, new Vector3(0f,.56f,(float)i/18-.5f),
                    new Vector3(.88f,.15f,.012f), BeltDark * .72f);
            foreach (var x in new[] { -0.5f, 0.5f })
                ParentBlock(surface, "Belt rail", new Vector3(x, 1.2f, 0f), new Vector3(0.08f, 1.4f, 1f), BeltYellow);
            return root;
        }

        private static void PoseBeltTreads(Transform[] treads, double seconds, float length)
        {
            if (treads == null) return;
            // Constant physical speed, independent of incline/length or rendered frame rate.
            var phase = (float)((seconds * .48 / Mathf.Max(length, .5f)) % 1.0);
            for (var i = 0; i < treads.Length; i++)
                if (treads[i] != null)
                    treads[i].localPosition = new Vector3(0f,.56f,Mathf.Repeat((float)i/treads.Length+phase,1f)-.5f);
        }

        private static Transform BuildPlanesideCart(Transform parent, List<Transform> bags)
        {
            var root = new GameObject("Planeside bag cart").transform;
            root.SetParent(parent, false);
            ParentBlock(root, "Cart bed", new Vector3(0f, 0.35f, 0f), new Vector3(1.2f, 0.08f, 2.2f), new Color(0.45f, 0.47f, 0.5f));
            foreach (var x in new[] { -0.5f, 0.5f })
            foreach (var z in new[] { -0.9f, 0.9f })
                ParentBlock(root, "Cart wheel", new Vector3(x, 0.13f, z), new Vector3(0.1f, 0.26f, 0.26f), BeltDark);
            ParentBlock(root, "Cart handle", new Vector3(0f, 0.55f, 1.15f), new Vector3(1.0f, 0.05f, 0.05f), BeltDark);
            // Up to eight cabin bags, two rows, laid flat on their sides on the bed.
            for (var i = 0; i < 8; i++)
            {
                var bag = HandTools.BuildItem(CarriedItem.Bag, root, i);
                bag.localPosition = new Vector3(i % 2 == 0 ? 0.02f : 0.58f, 0.39f + 0.085f, -0.8f + i / 2 * 0.52f);
                bag.localRotation = Quaternion.Euler(0f, 0f, 90f);
                bag.localScale = Vector3.one * 0.8f;
                bags.Add(bag);
            }

            return root;
        }

        // ---- The vehicles' own parts ------------------------------------------------------------

        /// <summary>The service vehicle of this name nearest <paramref name="at"/>, within reach.</summary>
        private Transform NearestServiceVehicle(Vector3 at, float reach, params Transform[] candidates)
        {
            Transform best = null;
            var bestDistance = reach * reach;
            foreach (var vehicle in candidates)
            {
                if (vehicle == null || !vehicle.gameObject.activeInHierarchy)
                    continue;
                var d = Flat(vehicle.position - at).sqrMagnitude;
                if (d < bestDistance)
                {
                    best = vehicle;
                    bestDistance = d;
                }
            }

            return best;
        }

        private Transform[] BaggageTrains()
        {
            var list = new List<Transform> { _baggageCart };
            foreach (var set in _ambientSets)
                list.Add(set.Bags);
            return list.ToArray();
        }

        private Transform[] FuelTrucks()
        {
            var list = new List<Transform> { _fuelTruck };
            foreach (var set in _ambientSets)
                list.Add(set.Fuel);
            return list.ToArray();
        }

        /// <summary>Apply this job's effect on the vehicle that is doing it.</summary>
        private void DriveServiceVehicleParts(RampActivity activity, GroundPose pose, AircraftLayout layout, float ground,
            ServiceScene scene)
        {
            switch (activity)
            {
                case RampActivity.Baggage:
                {
                    var train = NearestServiceVehicle(LayoutToWorld(pose, layout.BaggageTrain, ground), 14f, BaggageTrains());
                    if (train != null)
                        SetTrainLoad(train, scene.TrainLoad);
                    break;
                }
                case RampActivity.Fuel:
                {
                    var truck = NearestServiceVehicle(LayoutToWorld(pose, layout.FuelTruck, ground), 14f, FuelTrucks());
                    if (truck != null)
                        SetNamedPartsVisible(truck, "Hose nozzle", !scene.NozzleOut);
                    break;
                }
                case RampActivity.Catering when layout.CateringTruck is { } hiLoader:
                {
                    var truck = NearestServiceVehicle(LayoutToWorld(pose, hiLoader, ground), 10f, _cateringTruck);
                    if (truck != null)
                        SetLift(truck, scene.LiftHeight);
                    break;
                }
            }
        }

        /// <summary>Put every service vehicle no job touched this frame back to rest: full, stowed, lowered.</summary>
        private void RestUntouchedServiceVehicles()
        {
            foreach (var train in BaggageTrains())
                if (train != null && !_serviceVehiclesTouched.Contains(train))
                    SetTrainLoad(train, 1f, touch: false);
            foreach (var truck in FuelTrucks())
                if (truck != null && !_serviceVehiclesTouched.Contains(truck))
                    SetNamedPartsVisible(truck, "Hose nozzle", true, touch: false);
            if (_cateringTruck != null && !_serviceVehiclesTouched.Contains(_cateringTruck))
                SetLift(_cateringTruck, 0f, touch: false);
            _serviceVehiclesTouched.Clear();
        }

        private void SetTrainLoad(Transform train, float load, bool touch = true)
        {
            if (touch)
                _serviceVehiclesTouched.Add(train);
            var children = AirsideNamedChildren.Get(train);
            var names = AirsideNamedChildren.Names(train);
            var bags = 0;
            for (var i = 0; i < children.Length; i++)
                if (names[i] == "Cargo bag")
                    bags++;
            var keep = Mathf.CeilToInt(Mathf.Clamp01(load) * bags);
            var seen = 0;
            for (var i = 0; i < children.Length; i++)
            {
                if (names[i] != "Cargo bag")
                    continue;
                var show = seen++ < keep;
                if (children[i].gameObject.activeSelf != show)
                    children[i].gameObject.SetActive(show);
            }
        }

        private void SetNamedPartsVisible(Transform vehicle, string name, bool visible, bool touch = true)
        {
            if (touch)
                _serviceVehiclesTouched.Add(vehicle);
            var children = AirsideNamedChildren.Get(vehicle);
            var names = AirsideNamedChildren.Names(vehicle);
            for (var i = 0; i < children.Length; i++)
                if (names[i] == name && children[i].gameObject.activeSelf != visible)
                    children[i].gameObject.SetActive(visible);
        }

        /// <summary>Raise the hi-loader's box and platform by <paramref name="height"/>; stretch the scissor.</summary>
        private void SetLift(Transform truck, float height, bool touch = true)
        {
            if (touch)
                _serviceVehiclesTouched.Add(truck);
            var children = AirsideNamedChildren.Get(truck);
            var names = AirsideNamedChildren.Names(truck);
            for (var i = 0; i < children.Length; i++)
            {
                var name = names[i];
                var raised = name.Contains("box_") || name.Contains("platform");
                var scissor = name.Contains("scissor");
                if (!raised && !scissor)
                    continue;
                var part = children[i];
                if (!_liftRest.TryGetValue(part, out var rest))
                    _liftRest[part] = rest = (part.localPosition, part.localScale);
                var up = part.parent != null ? part.parent.InverseTransformVector(Vector3.up * height) : Vector3.up * height;
                if (raised)
                    part.localPosition = rest.Position + up;
                else
                {
                    // Scissor arms are baked from the truck floor: stretching them upward keeps
                    // their foot on the chassis and brings their top to the raised box.
                    part.localPosition = rest.Position;
                    part.localScale = new Vector3(rest.Scale.x, rest.Scale.y * (1f + height / 2.09f), rest.Scale.z);
                }
            }
        }

        // ---- Planeside bags ------------------------------------------------------------------

        /// <summary>
        /// For a turboprop boarding by its own airstair: when (seconds into the job) each boarding
        /// passenger with a roller bag leaves it at the planeside cart, and where that cart is.
        /// </summary>
        private bool TryPlanesideDrops(FleetAircraft aircraft, GroundPose pose, double jobStart, out (float X, float Z) cart)
        {
            cart = default;
            _planesideDrops.Clear();
            if (BoardingFlow.ModeFor(aircraft) != BoardingMode.IntegralAirstair
                || !_fleetViewById.TryGetValue(aircraft.Registration, out var view) || view == null
                || !TryWalkPath(aircraft, view, BoardingMode.IntegralAirstair, out var path))
                return false;
            var approach = path.Points[path.StairStart - 1];
            var foot = path.Points[path.StairStart];
            var along = Flat(foot - approach).normalized;
            var outward = Vector3.Cross(Vector3.up, along);
            var at = approach + outward * 1.4f;
            cart = AircraftLayout.ToLocal(pose, at.x, at.z);

            var toDrop = PathLength(path.Points, path.StairStart - 1);
            var level = _operations.CareerState?.BaseLevel ?? PlayerBaseLevel.Starter;
            BoardingFlow.Moves(aircraft, _preciseTime, _planesideMoves, 900, level);
            foreach (var move in _planesideMoves)
                if (move.Boarding && HandTools.TowsRollerBag(move.Look))
                    _planesideDrops.Add(move.StartSeconds + toDrop / Math.Max(0.3, move.SpeedMetresPerSecond) - jobStart);
            _planesideDrops.Sort();
            return true;
        }

        /// <summary>True once a boarding passenger has left their roller bag planeside.</summary>
        private static bool LeftBagPlaneside(FleetAircraft aircraft, PassengerMove move, WalkPath path, float elapsed) =>
            move.Boarding && HandTools.TowsRollerBag(move.Look)
            && AircraftLayout.For(aircraft.Type).IsTurboprop
            && BoardingFlow.ModeFor(aircraft) == BoardingMode.IntegralAirstair
            && path.StairStart >= 1
            && elapsed * move.SpeedMetresPerSecond >= PathLength(path.Points, path.StairStart - 1);

        private static float PathLength(Vector3[] points, int upTo)
        {
            var total = 0f;
            for (var i = 1; i <= upTo && i < points.Length; i++)
                total += Vector3.Distance(points[i - 1], points[i]);
            return total;
        }
    }
}

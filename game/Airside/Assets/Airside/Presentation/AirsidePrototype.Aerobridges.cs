using System;
using System.Collections.Generic;
using Airside.Domain;
using Airside.Simulation;
using UnityEngine;

namespace Airside.Presentation
{
    /// <summary>
    /// Terminal 1 aerobridges (ADR 0113): one per bridged gate, a fixed rotunda by the
    /// building and a three-section telescoping tunnel whose cab drives out to a parked
    /// jet's L1 door and back. Where they stand comes from <see cref="AdelaideAerobridges"/>;
    /// when they move comes from <see cref="AerobridgeTimeline"/>. Presentation only — the
    /// simulation never waits for a bridge.
    /// </summary>
    public sealed partial class AirsidePrototype
    {
        private const float BridgeCabDepth = 2.6f;
        private const float BridgeCabHeight = 3.0f;
        private const float BridgeCabWidth = 3.2f;
        private const float BridgeRotundaRadius = 2.4f;
        /// <summary>Gap between the cab's bellows and the fuselage skin when docked.</summary>
        private const float BridgeDockGap = 0.25f;
        /// <summary>Fastest the drawn bridge may catch up with its timeline: a full swing in ~40 s.</summary>
        private const float BridgeMaxFractionPerSecond = 1f / 40f;

        private static readonly Color BridgeSkin = new(0.83f, 0.85f, 0.87f);
        private static readonly Color BridgeGlazing = new(0.16f, 0.25f, 0.32f);
        private static readonly Color BridgeStructure = new(0.52f, 0.54f, 0.57f);
        private static readonly Color BridgeRubber = new(0.10f, 0.10f, 0.11f);
        private static readonly Color BridgeHazard = new(0.86f, 0.68f, 0.12f);

        private sealed class AerobridgeView
        {
            public AerobridgeSite Site;
            public Transform Root;
            public Transform Pivot;
            public readonly Transform[] Sections = new Transform[3];
            public readonly Transform[] SectionGlazing = new Transform[6];
            public Transform Cab;
            public Transform DriveColumn;
            public Transform DriveBogie;
            public float Floor;
            public float Shown;
            public Vector3 DockedCab;
            public float DockedYaw;
            public bool HasDocked;
        }

        private readonly List<AerobridgeView> _aerobridges = new();
        /// <summary>Ground height the terminal was sat on; null until Terminal 1 is built.</summary>
        private static float? _terminalGroundY;
        private double _aerobridgeClock = double.NaN;
        private readonly Dictionary<string, FleetAircraft> _gateHolders = new(StringComparer.Ordinal);
        private Transform _aerobridgeRoot;

        /// <summary>Builds every bridge, parked. Not under the static airfield root: they move.</summary>
        private void BuildAerobridges(float terminalGroundY)
        {
            if (_aerobridgeRoot != null)
                return;
            _aerobridgeRoot = new GameObject("Aerobridges").transform;
            var floor = AirsideFlightPath.GroundY + AdelaideAerobridges.RotundaFloorMetres;
            foreach (var site in AdelaideAerobridges.Sites)
            {
                var view = new AerobridgeView { Site = site, Floor = floor };
                view.Root = new GameObject($"Aerobridge {StandNames.Short(site.Gate)}").transform;
                view.Root.SetParent(_aerobridgeRoot, false);
                view.Root.position = new Vector3(site.RotundaX, 0f, site.RotundaZ);

                // Rotunda: the fixed column and the round room the tunnel swings from.
                var columnHeight = floor - terminalGroundY;
                BridgeCylinder(view.Root, "Rotunda column", new Vector3(0f, terminalGroundY + columnHeight / 2f, 0f),
                    new Vector3(1.6f, columnHeight / 2f, 1.6f), BridgeStructure);
                BridgeCylinder(view.Root, "Rotunda", new Vector3(0f, floor + BridgeCabHeight / 2f, 0f),
                    new Vector3(BridgeRotundaRadius * 2f, BridgeCabHeight / 2f + 0.1f, BridgeRotundaRadius * 2f), BridgeSkin);

                view.Pivot = new GameObject("Tunnel pivot").transform;
                view.Pivot.SetParent(view.Root, false);
                view.Pivot.localPosition = new Vector3(0f, floor, 0f);
                for (var i = 0; i < 3; i++)
                {
                    // Outer sections nest inside the inner, larger one, as a real telescope does.
                    var width = 2.9f - i * 0.18f;
                    var height = BridgeCabHeight - i * 0.16f;
                    view.Sections[i] = BridgeBox(view.Pivot, $"Tunnel {i + 1}", Vector3.zero,
                        new Vector3(width, height, 1f), BridgeSkin);
                    view.SectionGlazing[i * 2] = BridgeBox(view.Pivot, $"Tunnel {i + 1} glazing L", Vector3.zero,
                        new Vector3(0.04f, height * 0.34f, 1f), BridgeGlazing);
                    view.SectionGlazing[i * 2 + 1] = BridgeBox(view.Pivot, $"Tunnel {i + 1} glazing R", Vector3.zero,
                        new Vector3(0.04f, height * 0.34f, 1f), BridgeGlazing);
                }

                view.Cab = new GameObject("Cab").transform;
                view.Cab.SetParent(view.Root, false);
                BridgeBox(view.Cab, "Cab body", new Vector3(0f, BridgeCabHeight / 2f, 0f),
                    new Vector3(BridgeCabWidth, BridgeCabHeight, BridgeCabDepth), BridgeSkin);
                BridgeBox(view.Cab, "Cab window", new Vector3(0f, BridgeCabHeight * 0.66f, BridgeCabDepth / 2f + 0.01f),
                    new Vector3(BridgeCabWidth * 0.7f, BridgeCabHeight * 0.3f, 0.04f), BridgeGlazing);
                // The bellows canopy that closes round the door — the part that meets the aircraft.
                BridgeBox(view.Cab, "Cab bellows", new Vector3(0f, BridgeCabHeight / 2f, BridgeCabDepth / 2f + 0.25f),
                    new Vector3(BridgeCabWidth + 0.2f, BridgeCabHeight + 0.2f, 0.5f), BridgeRubber);

                view.DriveColumn = BridgeBox(view.Root, "Drive column", Vector3.zero, new Vector3(0.7f, 1f, 0.7f), BridgeHazard);
                view.DriveBogie = BridgeBox(view.Root, "Drive bogie", Vector3.zero, new Vector3(3.0f, 0.9f, 1.3f), BridgeRubber);
                PoseAerobridge(view, ParkedCab(view), ParkedYaw(view));
                _aerobridges.Add(view);
            }
        }

        /// <summary>Every frame: follow each gate's timeline, easing rather than jumping.</summary>
        private void UpdateAerobridges()
        {
            if (_aerobridges.Count == 0)
                return;

            _gateHolders.Clear();
            if (FleetMode && _operations != null)
            {
                foreach (var aircraft in _operations.Fleet)
                    if (aircraft.State is FleetState.AtStand or FleetState.TaxiIn && AdelaideAerobridges.Serves(aircraft.Stand))
                        _gateHolders[aircraft.Stand.Value] = aircraft;
            }

            var level = _operations?.CareerState?.BaseLevel ?? PlayerBaseLevel.Starter;
            // Ease in simulation seconds, so a sped-up soak or a catch-up after sleep follows
            // the timeline instead of dragging behind it at wall-clock pace.
            var dt = double.IsNaN(_aerobridgeClock) ? 0f : (float)Math.Max(0.0, _preciseTime - _aerobridgeClock);
            _aerobridgeClock = _preciseTime;
            foreach (var view in _aerobridges)
            {
                _gateHolders.TryGetValue(view.Site.Gate.Value, out var holder);
                var target = holder == null ? 0f : AerobridgeTimeline.DockedFraction(holder, _preciseTime, level);
                if (holder != null && holder.State == FleetState.AtStand && TryDockPose(view, holder, out var cab, out var yaw))
                {
                    view.DockedCab = cab;
                    view.DockedYaw = yaw;
                    view.HasDocked = true;
                }

                var previous = view.Shown;
                view.Shown = dt > 5f
                    ? target
                    : Mathf.MoveTowards(view.Shown, target, BridgeMaxFractionPerSecond * dt);
                if (!view.HasDocked)
                    view.Shown = 0f;
                if (Mathf.Approximately(previous, view.Shown) && view.Shown <= 0f)
                    continue;

                var eased = Mathf.SmoothStep(0f, 1f, view.Shown);
                var parked = ParkedCab(view);
                var parkedYaw = ParkedYaw(view);
                // Sweep like the real thing: swing about the rotunda and telescope, rather than
                // sliding the cab across the apron in a straight line.
                var rotunda = new Vector3(view.Site.RotundaX, 0f, view.Site.RotundaZ);
                var fromParked = Flat(parked - rotunda);
                var toDocked = Flat(view.DockedCab - rotunda);
                var angle = Mathf.LerpAngle(Mathf.Atan2(fromParked.x, fromParked.z) * Mathf.Rad2Deg,
                    Mathf.Atan2(toDocked.x, toDocked.z) * Mathf.Rad2Deg, eased);
                var length = Mathf.Lerp(fromParked.magnitude, toDocked.magnitude, eased);
                var height = Mathf.Lerp(parked.y, view.DockedCab.y, eased);
                var direction = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                var cabPosition = rotunda + direction * length + Vector3.up * height;
                PoseAerobridge(view, cabPosition, Mathf.LerpAngle(parkedYaw, view.DockedYaw, eased));
            }
        }

        /// <summary>Where the cab sits to meet this aircraft's L1 door, and which way it faces.</summary>
        private bool TryDockPose(AerobridgeView view, FleetAircraft aircraft, out Vector3 cab, out float yaw)
        {
            cab = default;
            yaw = 0f;
            var door = AircraftDoors.L1(aircraft.Type);
            Vector3 doorPoint;
            Vector3 intoFuselage;
            if (_fleetViewById.TryGetValue(aircraft.Registration, out var aircraftView) && aircraftView != null)
            {
                doorPoint = aircraftView.TransformPoint(new Vector3(door.LocalX, door.SillY, door.LocalZ));
                intoFuselage = Flat(aircraftView.right).normalized;
            }
            else if (AdelaideGround.TryTerminalGate(aircraft.Stand, out var gate))
            {
                var (x, z) = AdelaideAerobridges.DoorAt(gate, aircraft.Type);
                doorPoint = new Vector3(x, AirsideFlightPath.GroundY + door.SillY, z);
                var heading = gate.HeadingDegrees * Mathf.Deg2Rad;
                intoFuselage = new Vector3(Mathf.Cos(heading), 0f, -Mathf.Sin(heading));
            }
            else
            {
                return false;
            }

            if (intoFuselage.sqrMagnitude < 0.5f)
                return false;
            // The door sits on the left side; the cab stands off it, facing into the fuselage.
            cab = doorPoint - intoFuselage * (BridgeCabDepth / 2f + 0.5f + BridgeDockGap);
            yaw = Mathf.Atan2(intoFuselage.x, intoFuselage.z) * Mathf.Rad2Deg;
            return true;
        }

        private static Vector3 ParkedCab(AerobridgeView view) =>
            new(view.Site.ParkedCabX, view.Floor - 0.4f, view.Site.ParkedCabZ);

        private static float ParkedYaw(AerobridgeView view)
        {
            var dx = view.Site.ParkedCabX - view.Site.RotundaX;
            var dz = view.Site.ParkedCabZ - view.Site.RotundaZ;
            return Mathf.Atan2(dx, dz) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// Lay the tunnel from the rotunda to the cab: yaw and slope the pivot, share the
        /// length out over three nested sections, and stand the drive column under the
        /// outer section on the apron.
        /// </summary>
        private static void PoseAerobridge(AerobridgeView view, Vector3 cabFloor, float cabYaw)
        {
            var origin = new Vector3(view.Site.RotundaX, view.Floor, view.Site.RotundaZ);
            // The tunnel meets the back of the cab, not its centre.
            var cabYawRotation = Quaternion.Euler(0f, cabYaw, 0f);
            var cabBack = cabFloor - cabYawRotation * Vector3.forward * (BridgeCabDepth / 2f);
            var span = cabBack - origin;
            var flat = Flat(span);
            var yaw = Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;
            var pitch = -Mathf.Atan2(span.y, Mathf.Max(0.01f, flat.magnitude)) * Mathf.Rad2Deg;
            view.Pivot.position = origin;
            view.Pivot.rotation = Quaternion.Euler(pitch, yaw, 0f);

            var total = Mathf.Max(1f, span.magnitude - BridgeRotundaRadius);
            var step = total / 3f;
            const float overlap = 0.6f;
            for (var i = 0; i < 3; i++)
            {
                var start = BridgeRotundaRadius + i * step - (i > 0 ? overlap : 0f);
                var length = step + (i > 0 ? overlap : 0f);
                var centre = start + length / 2f;
                var section = view.Sections[i];
                var scale = section.localScale;
                section.localScale = new Vector3(scale.x, scale.y, length);
                section.localPosition = new Vector3(0f, scale.y / 2f, centre);
                for (var side = 0; side < 2; side++)
                {
                    var glazing = view.SectionGlazing[i * 2 + side];
                    var sign = side == 0 ? -1f : 1f;
                    glazing.localScale = new Vector3(glazing.localScale.x, glazing.localScale.y, length * 0.92f);
                    glazing.localPosition = new Vector3(sign * (scale.x / 2f + 0.02f), scale.y * 0.62f, centre);
                }
            }

            view.Cab.position = cabFloor;
            view.Cab.rotation = cabYawRotation;

            // Drive column two-thirds of the way out, under the outermost section.
            var drive = origin + span * 0.72f;
            var ground = AirsideFlightPath.GroundY;
            var columnHeight = Mathf.Max(0.5f, drive.y - ground);
            view.DriveColumn.position = new Vector3(drive.x, ground + columnHeight / 2f, drive.z);
            view.DriveColumn.localScale = new Vector3(0.7f, columnHeight, 0.7f);
            view.DriveColumn.rotation = Quaternion.Euler(0f, yaw, 0f);
            view.DriveBogie.position = new Vector3(drive.x, ground + 0.45f, drive.z);
            view.DriveBogie.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        private static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);

        private static Transform BridgeBox(Transform parent, string name, Vector3 localPosition, Vector3 scale, Color color)
        {
            var block = CreateBlock(name, Vector3.zero, scale, color);
            block.transform.SetParent(parent, false);
            block.transform.localPosition = localPosition;
            block.transform.localRotation = Quaternion.identity;
            block.transform.localScale = scale;
            var renderer = block.GetComponent<Renderer>();
            if (renderer != null)
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            return block.transform;
        }

        private static Transform BridgeCylinder(Transform parent, string name, Vector3 localPosition, Vector3 scale, Color color)
        {
            var cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cylinder.name = name;
            AirsideRuntimeQuality.StripVisualCollider(cylinder);
            cylinder.GetComponent<Renderer>().sharedMaterial = CreateSharedSurfaceMaterial(color, null, null);
            cylinder.transform.SetParent(parent, false);
            cylinder.transform.localPosition = localPosition;
            cylinder.transform.localScale = scale;
            AirsideSceneIndex.Remember(cylinder);
            return cylinder.transform;
        }
    }
}

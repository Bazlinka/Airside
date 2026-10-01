using System;
using System.Collections.Generic;
using Airside.Simulation;
using UnityEngine;

namespace Airside.Presentation
{
    /// <summary>
    /// The physical-animation pass for every fixed-wing aircraft: control surfaces hinged on their real
    /// (swept) hinge lines and driven by roll and pitch rate, flaps that run aft on their tracks, spoilers,
    /// and a landing gear that folds the way its airframe's gear does. The pure rules (signs, gear
    /// kinematics, hinge fit) live in <see cref="AircraftArticulation"/>; this file applies them to the
    /// transforms.
    /// </summary>
    public sealed partial class AirsidePrototype
    {
        /// <summary>One rolling wheel, with the radius class it spins by.</summary>
        private readonly struct WheelPart
        {
            public readonly Transform Transform;
            public readonly bool IsNose;

            public WheelPart(Transform transform, bool isNose)
            {
                Transform = transform;
                IsNose = isNose;
            }
        }

        /// <summary>
        /// Per-aircraft memory the control laws need: how fast the airframe is rolling and pitching (the
        /// attitude is a damped pose, so the rate is measured from it, not from the path) and how fast
        /// the tyres are spinning.
        /// </summary>
        private sealed class AircraftArticulationState
        {
            public float RollLeftRate;
            public float PitchUpRate;
            public float WheelSpinMetresPerSecond;
            private float _lastBank;
            private float _lastPitchUp;
            private bool _seeded;

            public void Observe(float bankLeftDegrees, float pitchUpDegrees, float deltaTime)
            {
                if (deltaTime <= 0f)
                    return;
                if (!_seeded)
                {
                    _lastBank = bankLeftDegrees;
                    _lastPitchUp = pitchUpDegrees;
                    _seeded = true;
                    return;
                }

                var rawRoll = (bankLeftDegrees - _lastBank) / deltaTime;
                var rawPitch = (pitchUpDegrees - _lastPitchUp) / deltaTime;
                // A jump bigger than any real roll in one frame is a respawn or a phase reset, not flying.
                if (Mathf.Abs(bankLeftDegrees - _lastBank) > 20f)
                    rawRoll = 0f;
                if (Mathf.Abs(pitchUpDegrees - _lastPitchUp) > 12f)
                    rawPitch = 0f;
                var blend = 1f - Mathf.Exp(-deltaTime / 0.3f);
                RollLeftRate = Mathf.Lerp(RollLeftRate, Mathf.Clamp(rawRoll, -30f, 30f), blend);
                PitchUpRate = Mathf.Lerp(PitchUpRate, Mathf.Clamp(rawPitch, -12f, 12f), blend);
                _lastBank = bankLeftDegrees;
                _lastPitchUp = pitchUpDegrees;
            }
        }

        // ---- Hinge geometry ----------------------------------------------------------------------

        private static bool UsesHingeFit(ControlSurfaceKind kind) =>
            kind is ControlSurfaceKind.Rudder or ControlSurfaceKind.Elevator or ControlSurfaceKind.Aileron
                or ControlSurfaceKind.Flap or ControlSurfaceKind.Spoiler;

        /// <summary>
        /// The hinge line of a control surface in its own frame, after its pivot has been rebaked to sit on that
        /// line. A swept wing or fin hinges its surfaces along a slanted line; rotating about the plain lateral
        /// axis instead tears the outboard end off the wing at any real deflection.
        /// </summary>
        private static Vector3 ControlSurfaceHinge(Transform part, ControlSurfaceKind kind, out float chordMetres)
        {
            var rudder = kind == ControlSurfaceKind.Rudder;
            var fallback = rudder ? Vector3.up : Vector3.right;
            chordMetres = 0f;
            if (part == null)
                return fallback;
            var filter = part.GetComponent<MeshFilter>();
            var mesh = filter != null ? filter.sharedMesh : null;
            if (mesh == null)
                return fallback;

            chordMetres = mesh.bounds.size.z;
            // A baked, non-readable mesh cannot be sampled; the plain axis is the best it can do.
            if (!UsesHingeFit(kind) || !mesh.isReadable)
                return fallback;

            var vertices = mesh.vertices;
            var span = new float[vertices.Length];
            var front = new float[vertices.Length];
            for (var i = 0; i < vertices.Length; i++)
            {
                span[i] = rudder ? vertices[i].y : vertices[i].x;
                front[i] = vertices[i].z;
            }

            if (!AircraftArticulation.FitHingeLine(span, front, vertices.Length, out var slope, out _, out _))
                return fallback;
            return (rudder ? new Vector3(0f, 1f, slope) : new Vector3(1f, 0f, slope)).normalized;
        }

        /// <summary>
        /// World position of a control surface's hinge: mid-span on the fitted front-edge line, so the rebaked
        /// pivot and the axis from <see cref="ControlSurfaceHinge"/> describe the same hinge.
        /// </summary>
        private static Vector3 ControlSurfaceHingePivot(Transform aircraft, Transform part, Bounds worldBounds,
            bool rudder)
        {
            var filter = part.GetComponent<MeshFilter>();
            var mesh = filter != null ? filter.sharedMesh : null;
            var centreLocal = aircraft.InverseTransformPoint(worldBounds.center);
            var maxZLocal = aircraft.InverseTransformPoint(
                new Vector3(worldBounds.center.x, worldBounds.center.y, worldBounds.max.z)).z;
            if (mesh == null || !mesh.isReadable)
                return aircraft.TransformPoint(new Vector3(centreLocal.x, centreLocal.y, maxZLocal));

            var vertices = mesh.vertices;
            var span = new float[vertices.Length];
            var front = new float[vertices.Length];
            for (var i = 0; i < vertices.Length; i++)
            {
                var v = aircraft.InverseTransformPoint(part.TransformPoint(vertices[i]));
                span[i] = rudder ? v.y : v.x;
                front[i] = v.z;
            }

            if (!AircraftArticulation.FitHingeLine(span, front, vertices.Length,
                    out _, out var spanCentre, out var frontAtCentre))
                return aircraft.TransformPoint(new Vector3(centreLocal.x, centreLocal.y, maxZLocal));

            var pivot = rudder
                ? new Vector3(centreLocal.x, spanCentre, frontAtCentre)
                : new Vector3(spanCentre, centreLocal.y, frontAtCentre);
            return aircraft.TransformPoint(pivot);
        }

        /// <summary>Hinge of a belly gear door: the longitudinal edge furthest from the centreline.</summary>
        private static Vector3 BellyDoorHingePivot(Bounds worldBounds, float centreX)
        {
            var hingeOnLeft = worldBounds.center.x <= centreX + 0.05f;
            return new Vector3(hingeOnLeft ? worldBounds.min.x : worldBounds.max.x,
                worldBounds.center.y, worldBounds.center.z);
        }

        /// <summary>-1 for a door hinged on its left (min X) edge, +1 on its right: the free edge swings down.</summary>
        private static float BellyDoorHingeSign(Transform door)
        {
            var renderer = door.GetComponent<Renderer>();
            if (renderer == null)
                return -1f;
            return door.position.x < renderer.bounds.center.x - 0.01f ? -1f : 1f;
        }

        // ---- Landing-gear rig --------------------------------------------------------------------

        /// <summary>
        /// Run once after the wheel pivots are rebaked. Leg-mounted door plates are carried by their leg so they
        /// fold away with it instead of spinning in their own plane, and a multi-axle main gear gets a truck
        /// beam the six wheels hang on so the beam can tip as the leg swings.
        /// </summary>
        private static void RigLandingGearArticulation(Transform aircraft)
        {
            var children = AirsideNamedChildren.Get(aircraft);
            var names = AirsideNamedChildren.Names(aircraft);
            Transform nose = null, left = null, right = null;
            for (var i = 0; i < children.Length; i++)
            {
                if (names[i] == "Gear nose") nose = children[i];
                else if (names[i] == "Gear L") left = children[i];
                else if (names[i] == "Gear R") right = children[i];
            }

            for (var i = 0; i < children.Length; i++)
            {
                var door = children[i];
                if (door == aircraft || !AirsideAircraftParts.IsGearDoor(names[i]))
                    continue;
                var renderer = door.GetComponent<Renderer>();
                if (renderer == null)
                    continue;
                var bounds = renderer.bounds;
                if (AircraftArticulation.ClassifyGearDoor(bounds.size.x, bounds.size.y, bounds.size.z)
                    != GearDoorKind.LegMounted)
                    continue;
                var strut = names[i].IndexOf("nose", StringComparison.OrdinalIgnoreCase) >= 0
                    ? nose
                    : aircraft.InverseTransformPoint(bounds.center).x < 0f ? left : right;
                NestUnderProp(strut, door, door.name);
            }

            RigMainGearTruck(aircraft, left, "L", names, children);
            RigMainGearTruck(aircraft, right, "R", names, children);
            AirsideNamedChildren.Forget(aircraft);
        }

        private static void RigMainGearTruck(Transform aircraft, Transform strut, string side,
            string[] names, Transform[] children)
        {
            if (strut == null)
                return;
            var tyrePrefix = "Tire " + side + " ";
            var axles = new List<Transform>();
            var wheelSet = new List<Transform>();
            var hasForward = false;
            var hasAft = false;
            for (var i = 0; i < children.Length; i++)
            {
                var name = names[i];
                if (children[i] == null || !AirsideAircraftParts.RollsInPlace(name)
                    || name.IndexOf(" " + side + " ", StringComparison.Ordinal) < 0)
                    continue;
                wheelSet.Add(children[i]);
                if (!name.StartsWith(tyrePrefix, StringComparison.Ordinal))
                    continue;
                axles.Add(children[i]);
                hasForward |= name.IndexOf("forward", StringComparison.OrdinalIgnoreCase) >= 0;
                hasAft |= name.IndexOf("aft", StringComparison.OrdinalIgnoreCase) >= 0;
            }

            // Two axles in line along the fuselage with a pair of wheels across each: a widebody truck.
            var inboard = false;
            foreach (var tyre in axles)
                inboard |= tyre.name.IndexOf("inboard", StringComparison.OrdinalIgnoreCase) >= 0;
            if (axles.Count < 4 || !hasForward || !hasAft || !inboard)
                return;

            var centre = Vector3.zero;
            foreach (var tyre in axles)
                centre += tyre.position;
            centre /= axles.Count;

            var truck = new GameObject("Truck " + side).transform;
            truck.SetParent(aircraft, false);
            truck.position = centre;
            truck.rotation = aircraft.rotation;
            truck.SetParent(strut, true);
            foreach (var wheel in wheelSet)
                wheel.SetParent(truck, true);
        }

        /// <summary>Slews a gear part's own retraction toward the phase's target; seeds it on first use.</summary>
        private static float SlewGearRetract(LightGearPart part, float target, float deltaTime)
        {
            if (!part.RetractSeeded)
            {
                part.Retract = target;
                part.RetractSeeded = true;
            }
            else
            {
                // Faster than the longest authored cycle, so it only ever softens a phase change.
                part.Retract = AircraftArticulation.MoveToward(part.Retract, target, deltaTime * 0.35f);
            }

            return part.Retract;
        }

        private static void PoseGearStrut(LightGearPart part, float retractTarget, float deltaTime)
        {
            var swing = AircraftArticulation.GearLegSwing01(SlewGearRetract(part, retractTarget, deltaTime));
            var rotation = AircraftArticulation.GearRetractRotation(part.Style, part.Role == GearRole.MainLeft, swing);
            var fold = Quaternion.AngleAxis(rotation.Degrees, new Vector3(rotation.AxisX, 0f, rotation.AxisZ));
            // The leg retracts in its parent's frame; the nose wheel steers about the leg's own axis.
            part.Transform.localRotation = fold * part.Rest * Quaternion.AngleAxis(part.SteerDegrees, Vector3.up);
        }

        private static void PoseGearTruck(LightGearPart part, float retractTarget, float deltaTime)
        {
            var swing = AircraftArticulation.GearLegSwing01(SlewGearRetract(part, retractTarget, deltaTime));
            part.Transform.localRotation = part.Rest
                * Quaternion.AngleAxis(AircraftArticulation.TruckTiltDegrees(swing), Vector3.right);
        }

        private static void PoseGearDoor(LightGearPart part, float retractTarget, float deltaTime)
        {
            var open = AircraftArticulation.GearDoorOpen01(SlewGearRetract(part, retractTarget, deltaTime));
            part.Transform.localRotation = part.Rest
                * Quaternion.AngleAxis(part.DoorSign * AircraftArticulation.BellyDoorDegrees(open), Vector3.forward);
        }

        private static void UpdateNoseWheelSteering(LightGearPart noseGear, float targetDegrees, float deltaTime)
        {
            if (noseGear == null || deltaTime <= 0f)
                return;
            noseGear.SteerDegrees = Mathf.MoveTowards(noseGear.SteerDegrees, targetDegrees, deltaTime * 80f);
        }

        // ---- Control surfaces --------------------------------------------------------------------

        private static void UpdateControlSurfaces(
            ControlSurfacePart[] parts, AircraftArticulationState state, AircraftPhase phase, float progress,
            float bankDegrees, float deltaTime, EngineState? engines, bool drawnOnGround = false)
        {
            // Presentation-only. deltaTime is the presentation clock, so surfaces hold still while paused
            // and sweep 4x faster at 4x speed instead of running on their own timeline.
            if (deltaTime <= 0f)
                return;
            var pitchUp = -PhasePitchDegrees(phase, progress);
            state.Observe(bankDegrees, pitchUp, deltaTime);
            var rollLeftRate = state.RollLeftRate;
            var hydraulicsOff = drawnOnGround && engines.HasValue && !engines.Value.AnyRunning
                                && phase is AircraftPhase.AtStand or AircraftPhase.Pushback;
            var droop = AircraftArticulation.ParkedDroopDegrees(hydraulicsOff);
            var elevatorUp = AircraftArticulation.ElevatorTrailingEdgeUpDegrees(pitchUp, state.PitchUpRate);
            var wingFlex = AirsideReusableMotion.WingFlexDegrees(phase, progress);
            var groundSpoiler = phase == AircraftPhase.Landing
                ? 40f * Mathf.Clamp01(Mathf.InverseLerp(
                      AirsideFlightPath.TouchdownProgress,
                      AirsideFlightPath.TouchdownProgress + 0.06f, progress)
                  - Mathf.InverseLerp(0.86f, 1f, progress))
                : 0f;

            for (var i = 0; i < parts.Length; i++)
            {
                var part = parts[i];
                var child = part.Transform;
                if (child == null)
                    continue;

                float target;
                float slew;
                switch (part.Kind)
                {
                    case ControlSurfaceKind.Rudder:
                        target = AircraftArticulation.RudderTrailingEdgeRightDegrees(rollLeftRate, bankDegrees);
                        slew = 60f;
                        break;
                    case ControlSurfaceKind.Elevator:
                        target = -elevatorUp * part.Factor + droop * 0.6f * part.Factor;
                        slew = 50f;
                        break;
                    case ControlSurfaceKind.Aileron:
                        target = AircraftArticulation.AileronTrailingEdgeDownDegrees(rollLeftRate, part.Factor < 0f)
                                 + droop;
                        slew = 60f;
                        break;
                    case ControlSurfaceKind.Spoiler:
                        // Spoilers pop on touchdown and stow as the rollout ends; in flight only the wing that is
                        // rolling down lifts one, to help the aileron.
                        target = -(groundSpoiler
                                   + AircraftArticulation.RollSpoilerDegrees(rollLeftRate, part.Factor < 0f));
                        slew = 55f;
                        break;
                    case ControlSurfaceKind.Flap:
                        // Takeoff flap is set for the roll and milked off after rotation. Flaps are slow
                        // hydraulic or electric actuators, a few degrees a second.
                        target = AirsideReusableMotion.FlapDegrees(phase, progress, drawnOnGround);
                        slew = 6f;
                        break;
                    case ControlSurfaceKind.Wing:
                    {
                        // Flex the authored wing roots in opposite directions so both tips rise under load.
                        // Keep the cue subtle and ease it between phases.
                        var euler = child.localEulerAngles;
                        var current = euler.z > 180f ? euler.z - 360f : euler.z;
                        euler.z = Mathf.MoveTowards(current, wingFlex * part.Factor, deltaTime * 3.5f);
                        child.localEulerAngles = euler;
                        continue;
                    }
                    default:
                        continue;
                }

                part.Deflection = AircraftArticulation.MoveToward(part.Deflection, target, slew * deltaTime);
                var hinge = part.Kind == ControlSurfaceKind.Rudder
                    ? AircraftArticulation.HingeAngleForRudderRight(part.Deflection)
                    : AircraftArticulation.HingeAngleForTrailingEdgeDown(part.Deflection);
                child.localRotation = part.RestRotation * Quaternion.AngleAxis(hinge, part.HingeAxis);
                if (part.Kind == ControlSurfaceKind.Flap)
                    child.localPosition = part.RestPosition + Vector3.back
                        * AircraftArticulation.FlapAftTravelMetres(part.ChordMetres, part.Deflection);
            }
        }
    }
}

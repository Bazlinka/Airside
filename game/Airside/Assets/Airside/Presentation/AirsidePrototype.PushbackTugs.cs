using System;
using System.Collections.Generic;
using Airside.Domain;
using Airside.Simulation;
using UnityEngine;

namespace Airside.Presentation
{
    /// <summary>
    /// ADR 0126 — a real tug for every tail-first pushback, AI and player alike, from a small pool of
    /// VEH-004 tug views. <see cref="PushbackTugTimeline"/> owns where each tug is; this only finds the
    /// aircraft's nose gear on its drawn model and places the view. Presentation only.
    /// </summary>
    public sealed partial class AirsidePrototype
    {
        private const int MaxPushbackTugs = 8;

        private sealed class TugView
        {
            public Transform Root;
            /// <summary>Metres from the tug's root to the towbar eye, along the side the towbar is on.</summary>
            public float Reach;
            /// <summary>+1 when the towbar trails behind the tug, −1 when it leads.</summary>
            public float TowbarBehind;
            public string Registration;
        }

        private readonly List<TugView> _tugPool = new();
        private readonly Dictionary<string, TugView> _tugByRegistration = new(StringComparer.Ordinal);
        private readonly HashSet<string> _tugsWanted = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Transform> _viewByRegistration = new(StringComparer.Ordinal);
        /// <summary>Nose gear ahead of the drawn model's root, measured once per type.</summary>
        private readonly Dictionary<string, float> _noseGearAhead = new(StringComparer.Ordinal);

        private void UpdatePushbackTugs()
        {
            if (!FleetMode || _operations == null || !AirsideFocusMode.ShowTurnaroundVehicles)
                return;

            _viewByRegistration.Clear();
            for (var i = 0; i < _commercialAircraft.Length && i < _commercialAircraftIds.Length; i++)
                if (_commercialAircraft[i] != null && _commercialAircraftIds[i] != null)
                    _viewByRegistration[_commercialAircraftIds[i]] = _commercialAircraft[i];

            _tugsWanted.Clear();
            foreach (var aircraft in _operations.Fleet)
            {
                if (!TryTugJob(aircraft, out var state, out var anchor, out var side, out var lateral, out var pull))
                    continue;
                var tug = TugFor(aircraft.Registration);
                if (tug == null)
                    continue;
                _tugsWanted.Add(aircraft.Registration);

                var ahead = NoseGearAhead(aircraft);
                var gearX = anchor.X + anchor.NoseX * ahead;
                var gearZ = anchor.Z + anchor.NoseZ * ahead;
                var pose = PushbackTugTimeline.Pose(state, gearX, gearZ, anchor.NoseX, anchor.NoseZ, tug.Reach,
                    side, lateral, pull);
                var facing = new Vector3(pose.FacingX, 0f, pose.FacingZ) * tug.TowbarBehind;
                tug.Root.gameObject.SetActive(true);
                tug.Root.position = new Vector3(pose.X, AirsideFlightPath.GroundY, pose.Z);
                if (facing.sqrMagnitude > 1e-4f)
                    tug.Root.rotation = Quaternion.LookRotation(facing);
                tug.Root.localScale = Vector3.one * (AircraftCatalogue.IsWidebody(aircraft.Type) ? 1.3f : 1f);
            }

            // Release tugs whose job ended.
            foreach (var tug in _tugPool)
            {
                if (tug.Registration == null || _tugsWanted.Contains(tug.Registration))
                    continue;
                _tugByRegistration.Remove(tug.Registration);
                tug.Registration = null;
                tug.Root.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Whether <paramref name="aircraft"/> has a tug now, and the pose the tug works from: the stand
        /// before the push, the live aircraft pose while pushing, the push-end pose afterwards.
        /// </summary>
        private bool TryTugJob(FleetAircraft aircraft, out TugState state, out GroundPose anchor, out float side,
            out float lateral, out float pull)
        {
            state = default;
            anchor = default;
            side = 1f;
            lateral = PushbackTugTimeline.StandLateralMetres;
            pull = PushbackTugTimeline.StandPullForwardMetres;
            GroundLeg leg;
            if (aircraft.State == FleetState.AtStand && aircraft.Scheduled is { Cancelled: false } booked)
            {
                leg = AdelaideGround.TaxiOut(aircraft.Stand, aircraft.Type, aircraft.AssignedRunway);
                if (leg.Parts.Count < 2 || !leg.Parts[0].TailFirst)
                    return false;
                state = PushbackTugTimeline.AtStand(booked.DepartAt.ElapsedSeconds - _preciseTime);
                anchor = AdelaideGround.StandPose(aircraft.Stand);
                return state.Visible;
            }

            if (aircraft.State != FleetState.TaxiOut)
                return false;
            var visual = FleetVisual.For(aircraft, _clock.Now);
            if (visual.Leg != FleetGroundLeg.TaxiOut)
                return false;
            leg = AdelaideGround.TaxiOut(aircraft.DepartureStand, aircraft.Type, aircraft.AssignedRunway);
            if (leg.Parts.Count < 2 || !leg.Parts[0].TailFirst)
                return false;

            var scale = visual.LegSeconds > 0 ? leg.Seconds / visual.LegSeconds : 1.0;
            var elapsed = (_preciseTime - visual.LegStartedAt.ElapsedSeconds) * scale;
            var push = leg.Parts[0].Seconds;
            var pause = leg.Parts[1].PauseBeforeSeconds;
            state = PushbackTugTimeline.OnTaxiOut(elapsed, push, pause);
            if (!state.Visible)
                return false;

            var pushEnd = leg.PoseAt(push + pause);
            anchor = state.Phase == TugPhase.Coupled ? leg.PoseAt(elapsed) : pushEnd;
            var ahead = leg.PositionAt(push + pause + 25.0);
            side = PushbackTugTimeline.ClearingSide(pushEnd.X, pushEnd.Z, pushEnd.NoseX, pushEnd.NoseZ, ahead.X, ahead.Z);
            lateral = PushbackTugTimeline.LateralFor(GroundTraffic.HalfSpan(aircraft.Type));
            pull = PushbackTugTimeline.PullForwardMetres;
            return true;
        }

        private TugView TugFor(string registration)
        {
            if (_tugByRegistration.TryGetValue(registration, out var owned))
                return owned;
            foreach (var tug in _tugPool)
            {
                if (tug.Registration != null)
                    continue;
                tug.Registration = registration;
                _tugByRegistration[registration] = tug;
                return tug;
            }

            if (_tugPool.Count >= MaxPushbackTugs)
                return null;
            var view = BuildTugView();
            if (view == null)
                return null;
            view.Registration = registration;
            _tugPool.Add(view);
            _tugByRegistration[registration] = view;
            return view;
        }

        private static TugView BuildTugView()
        {
            var root = BuildPushbackTug();
            if (root == null)
                return null;
            OrientPlusXKitToForward(root);
            root.gameObject.SetActive(true);

            // Measure where the towbar is on this kit, in the tug's own frame, so the eye lands on
            // the nose gear whichever way round the authored kit carries it.
            var towbarZ = 0f;
            var towbarCount = 0;
            var minZ = 0f;
            var maxZ = 0f;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var b = renderer.bounds;
                var min = root.InverseTransformPoint(b.min);
                var max = root.InverseTransformPoint(b.max);
                minZ = Mathf.Min(minZ, Mathf.Min(min.z, max.z));
                maxZ = Mathf.Max(maxZ, Mathf.Max(min.z, max.z));
                if (renderer.gameObject.name.IndexOf("towbar", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    towbarZ += root.InverseTransformPoint(b.center).z;
                    towbarCount++;
                }
            }

            var behind = towbarCount == 0 || towbarZ / towbarCount <= 0f ? 1f : -1f;
            root.gameObject.SetActive(false);
            root.name = "Pushback tug (fleet)";
            AirsideSceneIndex.Remember(root.gameObject);
            return new TugView
            {
                Root = root,
                TowbarBehind = behind,
                Reach = Mathf.Max(1.5f, behind > 0f ? -minZ : maxZ)
            };
        }

        /// <summary>
        /// How far the drawn nose gear sits ahead of the pose point, measured off the model the first time
        /// this type is seen (glTF lamp and gear nodes carry zero transforms, so the renderer bounds are the
        /// truth). Until then, and for models without a nose gear part, the pose point itself.
        /// </summary>
        private float NoseGearAhead(FleetAircraft aircraft)
        {
            var key = aircraft.Type.Id;
            if (_noseGearAhead.TryGetValue(key, out var ahead))
                return ahead;
            if (!_viewByRegistration.TryGetValue(aircraft.Registration, out var view) || view == null
                || !view.gameObject.activeInHierarchy)
                return 0f;
            Renderer gear = null;
            foreach (var renderer in view.GetComponentsInChildren<Renderer>(false))
            {
                var name = renderer.gameObject.name;
                if (name is "Gear nose" or "Tire nose" or "Wheel nose")
                {
                    gear = renderer;
                    if (name == "Gear nose")
                        break;
                }
            }

            if (gear == null)
                return 0f;
            var forward = view.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-4f)
                return 0f;
            forward.Normalize();
            var offset = gear.bounds.center - view.position;
            ahead = Vector3.Dot(new Vector3(offset.x, 0f, offset.z), forward);
            _noseGearAhead[key] = ahead;
            return ahead;
        }
    }
}

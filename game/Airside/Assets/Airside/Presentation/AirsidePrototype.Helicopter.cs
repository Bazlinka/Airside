using System;
using System.Collections.Generic;
using Airside.Domain;
using Airside.Simulation;
using UnityEngine;

namespace Airside.Presentation
{
    /// <summary>
    /// The Bell 412 in the airline game (ADR 0207): the authored AIR-017 kit with its main and tail rotors turned into
    /// spinning assemblies, a rotor blur, and the per-frame pose, rotor speed, lights and sound that follow
    /// <see cref="HelicopterTrack"/>. It lifts off a pad spot, flies to a hospital and lands back; it never touches a
    /// runway or taxiway.
    /// </summary>
    public sealed partial class AirsidePrototype
    {
        private const string MainRotorName = "Main rotor";
        private const string TailRotorName = "Tail rotor";
        private const float MainRotorDegreesPerSecond = 330f * 6f;
        private const float TailRotorDegreesPerSecond = 1600f * 6f;
        // At low frame rates a rotor blade must not step further than the blade spacing, or it strobes backwards.
        private const float MaxRotorStepDegrees = 40f;

        private sealed class HelicopterRig
        {
            public Transform Main;
            public Transform Tail;
            public Renderer MainDisc;
            public Renderer TailDisc;
            public float MainAngle;
            public float TailAngle;
            public float Seed;
        }

        private readonly Dictionary<int, HelicopterRig> _helicopterRigs = new();

        /// <summary>True when this drawn flight is a helicopter: no runway circuit, no tyre smoke, no taxi spray.</summary>
        private bool IsRotorcraftFlight(CommercialFlight flight) =>
            FleetMode && flight != null && _fleetAircraftById.TryGetValue(flight.AircraftId, out var aircraft)
            && aircraft.Type.IsRotorcraft;
        private bool _staticRescueHelicopterHidden;

        private static Transform BuildBell412(string name, Color accent)
        {
            var profile = AircraftVisualProfiles.Bell412;
            var root = new GameObject(name).transform;
            AircraftVisualProfileComponent.Ensure(root, profile);
            var usedArt = ArtPresentationLoader.TryInstantiate(
                profile.ArtRelativePath,
                root,
                out _,
                HelicopterPartName,
                kitName => AirsideAdelaideEmergencyAviation.PartColour(kitName),
                localPosition: new Vector3(0f, profile.ModelGroundOffsetMetres, 0f));
            if (!usedArt)
                AirsideAdelaideEmergencyAviation.BuildFallback(root);

            RigHelicopterRotors(root);
            EnsureGroundShadow(root);
            if (!HasNamedChild(root, "NavLight L"))
                ParentBlock(root, "NavLight L", new Vector3(-1.3f, 2.6f, -8.2f), new Vector3(0.12f, 0.12f, 0.12f), new Color(0.9f, 0.12f, 0.12f));
            if (!HasNamedChild(root, "NavLight R"))
                ParentBlock(root, "NavLight R", new Vector3(1.3f, 2.6f, -8.2f), new Vector3(0.12f, 0.12f, 0.12f), new Color(0.1f, 0.9f, 0.2f));
            if (!HasNamedChild(root, "Beacon"))
                ParentBlock(root, "Beacon", new Vector3(0f, 3.9f, -1.25f), new Vector3(0.14f, 0.14f, 0.14f), new Color(0.95f, 0.2f, 0.15f));
            if (!HasNamedChild(root, "LandingLight L"))
                ParentBlock(root, "LandingLight L", new Vector3(-0.55f, 1.1f, 3.2f), new Vector3(0.2f, 0.16f, 0.2f), new Color(0.95f, 0.95f, 0.85f));
            AttachEngineAudio(root, AircraftType.Bell412, 14f, 420f, 0.12f);
            _ = accent;
            return root;
        }

        /// <summary>
        /// The kit names the shared lights pass already knows (nav lamps, beacons and the searchlight as the
        /// landing light) keep their friendly names; everything else stays under its kit name.
        /// </summary>
        private static string HelicopterPartName(string kitName) => kitName switch
        {
            "nav_light_left" => "NavLight L",
            "nav_light_right" => "NavLight R",
            "beacon_top" => "Beacon",
            "beacon_belly" => "Beacon bottom",
            "nose_searchlight" => "LandingLight L",
            _ => kitName
        };

        /// <summary>
        /// The kit ships every blade at aircraft-space position with an identity node, like the fixed-wing
        /// propellers. Gather the main blades and hub under a node on the rotor axis, and the tail blades under
        /// one on the tail rotor shaft, so each spins about its own axis and not about the airframe origin.
        /// </summary>
        private static void RigHelicopterRotors(Transform root)
        {
            var children = AirsideNamedChildren.Get(root);
            var names = AirsideNamedChildren.Names(root);
            var mainBlades = new List<Transform>();
            var tailBlades = new List<Transform>();
            Transform hub = null, tailHub = null;
            for (var i = 0; i < children.Length; i++)
            {
                var name = names[i];
                if (name.StartsWith("main_rotor_blade", StringComparison.Ordinal) || name.StartsWith("Main rotor A", StringComparison.Ordinal)
                    || name.StartsWith("Main rotor B", StringComparison.Ordinal))
                    mainBlades.Add(children[i]);
                else if (name == "rotor_hub")
                    hub = children[i];
                else if (name.StartsWith("tail_rotor_blade", StringComparison.Ordinal))
                    tailBlades.Add(children[i]);
                else if (name == "tail_rotor_hub")
                    tailHub = children[i];
            }

            if (mainBlades.Count > 0)
            {
                // Axis: the hub's footprint centre at the height of the blade plane.
                var centre = BoundsCentre(hub != null ? hub : mainBlades[0]);
                var plane = BoundsCentre(mainBlades[0]).y;
                var main = new GameObject(MainRotorName).transform;
                main.SetParent(root, false);
                main.position = new Vector3(centre.x, plane, centre.z);
                foreach (var blade in mainBlades)
                    blade.SetParent(main, true);
                if (hub != null)
                    hub.SetParent(main, true);
                var radius = 0.5f;
                foreach (var blade in mainBlades)
                {
                    var b = blade.GetComponent<Renderer>().bounds;
                    radius = Mathf.Max(radius, new Vector2(b.max.x - main.position.x, b.max.z - main.position.z).magnitude,
                        new Vector2(b.min.x - main.position.x, b.min.z - main.position.z).magnitude);
                }

                BuildRotorDisc(main, "Rotor disc", radius * 2f, flat: true);
            }

            if (tailBlades.Count > 0)
            {
                var centre = tailHub != null ? BoundsCentre(tailHub) : BoundsCentre(tailBlades[0]);
                var tail = new GameObject(TailRotorName).transform;
                tail.SetParent(root, false);
                tail.position = centre;
                foreach (var blade in tailBlades)
                    blade.SetParent(tail, true);
                if (tailHub != null)
                    tailHub.SetParent(tail, true);
                var radius = 0.5f;
                foreach (var blade in tailBlades)
                {
                    var b = blade.GetComponent<Renderer>().bounds;
                    radius = Mathf.Max(radius, new Vector2(b.max.y - centre.y, b.max.z - centre.z).magnitude,
                        new Vector2(b.min.y - centre.y, b.min.z - centre.z).magnitude);
                }

                BuildRotorDisc(tail, "Tail rotor disc", Mathf.Clamp(radius * 2f, 1.2f, 2.6f), flat: false);
            }

            AirsideNamedChildren.Forget(root);
        }

        private static Vector3 BoundsCentre(Transform part)
        {
            var renderer = part.GetComponent<Renderer>();
            return renderer != null ? renderer.bounds.center : part.position;
        }

        /// <summary>The rotor blur: the same soft disc with tip ring the propellers use, in the rotor's own plane.</summary>
        private static void BuildRotorDisc(Transform rotor, string name, float diameter, bool flat)
        {
            var disc = GameObject.CreatePrimitive(PrimitiveType.Quad);
            disc.name = name;
            DestroyPresentationObject(disc.GetComponent<Collider>());
            disc.transform.SetParent(rotor, false);
            disc.transform.localPosition = Vector3.zero;
            // The main disc lies flat (spin axis up); the tail disc stands in the YZ plane (spin axis X).
            disc.transform.localRotation = flat ? Quaternion.Euler(90f, 0f, 0f) : Quaternion.Euler(0f, 90f, 0f);
            disc.transform.localScale = new Vector3(diameter, diameter, 1f);
            var renderer = disc.GetComponent<Renderer>();
            renderer.sharedMaterial = PropBlurMaterial(flat ? 4 : 2);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            SetRendererColor(renderer, new Color(0.72f, 0.74f, 0.78f, 0f));
            disc.SetActive(false);
        }

        private HelicopterRig RigFor(Transform view, string registration)
        {
            var id = view.GetInstanceID();
            if (_helicopterRigs.TryGetValue(id, out var rig) && rig.Main != null)
                return rig;
            rig = new HelicopterRig
            {
                Main = view.Find(MainRotorName),
                Tail = view.Find(TailRotorName),
                Seed = StableRegistrationHash(registration) % 997 * 0.013f
            };
            rig.MainDisc = rig.Main != null ? rig.Main.Find("Rotor disc")?.GetComponent<Renderer>() : null;
            rig.TailDisc = rig.Tail != null ? rig.Tail.Find("Tail rotor disc")?.GetComponent<Renderer>() : null;
            _helicopterRigs[id] = rig;
            return rig;
        }

        /// <summary>
        /// Draws one helicopter for the frame: its pose from <see cref="HelicopterTrack"/> (pad spot, hover, climb-out,
        /// the leg to the hospital, approach and landing), the rotor at the speed its engines give it, the lights and
        /// the rotor sound. Returns false when it is not drawn.
        /// </summary>
        private void UpdateHelicopterView(Transform view, CommercialFlight flight, FleetAircraft aircraft)
        {
            var pose = HelicopterTrack.For(aircraft, _preciseTime);
            if (!pose.Visible)
                return;

            var groundY = AirsideAdelaideEmergencyAviation.PadGroundY + 0.18f;
            var rig = RigFor(view, aircraft.Registration);
            var t = (float)_preciseTime;
            // A helicopter in the hover is never still: small, slow attitude wander, fading out as it gathers speed.
            var hover = pose.HeightMetres > 0.3f ? Mathf.Clamp01(1f - pose.SpeedMetresPerSecond / 14f) : 0f;
            var wobblePitch = hover * (Mathf.Sin(t * 1.7f + rig.Seed) * 0.6f + Mathf.Sin(t * 3.9f + rig.Seed * 2.3f) * 0.25f);
            var wobbleBank = hover * (Mathf.Sin(t * 1.3f + rig.Seed * 1.7f) * 0.7f + Mathf.Sin(t * 4.3f + rig.Seed) * 0.25f);
            view.position = new Vector3(pose.X, groundY + pose.HeightMetres, pose.Z);
            view.rotation = Quaternion.Euler(0f, pose.YawDegrees, 0f)
                            * Quaternion.Euler(pose.PitchDegrees + wobblePitch, 0f, pose.BankDegrees + wobbleBank);

            SpinHelicopterRotors(rig, pose);
            var visual = FleetVisual.For(aircraft, _clock.Now);
            var engines = FleetEngines(flight) ?? EngineState.Running;
            var progress = aircraft.StateEndsAt.HasValue
                ? Mathf.Clamp01((float)((_preciseTime - aircraft.StateStartedAt.ElapsedSeconds)
                                         / Math.Max(1, aircraft.StateEndsAt.Value.ElapsedSeconds - aircraft.StateStartedAt.ElapsedSeconds)))
                : 0.5f;
            var parts = PartsFor(view);
            UpdateAircraftLightsAndGear(parts.LightsAndGear, visual.Phase, PresentationDaylight, progress,
                PresentationDeltaTime, PresentationClock, engines, null, aircraft.Type);
            UpdateGroundShadow(view);
            UpdateSelectionMarker(view, flight.AircraftId);
            UpdateDistantLight(view, AirsideReusableMotion.LandingLightsOn(visual.Phase, progress, true));
            UpdateRotorcraftSound(view, aircraft, pose, engines);

            if (_cameraController != null && _cameraController.IsFollowing && _cameraController.FollowTarget == view)
                _cameraController.SetFollowPhase(visual.Phase, progress);
        }

        private void SpinHelicopterRotors(HelicopterRig rig, HelicopterWorldPose pose)
        {
            var dt = PresentationDeltaTime;
            var speed = pose.RotorSpeed01;
            if (rig.Main != null && dt > 0f)
            {
                rig.MainAngle = Mathf.Repeat(rig.MainAngle + Mathf.Min(MaxRotorStepDegrees,
                    MainRotorDegreesPerSecond * speed * dt), 360f);
                // The disc coning up under load: the blades lift a few degrees as the rotor takes weight.
                rig.Main.localRotation = Quaternion.Euler(0f, rig.MainAngle, 0f);
            }

            if (rig.Tail != null && dt > 0f)
            {
                rig.TailAngle = Mathf.Repeat(rig.TailAngle + Mathf.Min(MaxRotorStepDegrees * 2f,
                    TailRotorDegreesPerSecond * speed * dt), 360f);
                rig.Tail.localRotation = Quaternion.Euler(rig.TailAngle, 0f, 0f);
            }

            // The blur shows once the rotor is quick enough to smear, a soft ghost on top of the blades.
            SetRotorDisc(rig.MainDisc, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.3f, 0.9f, speed)) * 0.34f);
            SetRotorDisc(rig.TailDisc, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.3f, 0.9f, speed)) * 0.4f);
        }

        private static void SetRotorDisc(Renderer disc, float alpha)
        {
            if (disc == null)
                return;
            var on = alpha > 0.01f;
            if (disc.gameObject.activeSelf != on)
                disc.gameObject.SetActive(on);
            if (on)
                SetRendererColor(disc, new Color(0.72f, 0.74f, 0.78f, alpha));
        }

        private void UpdateRotorcraftSound(Transform view, FleetAircraft aircraft, HelicopterWorldPose pose, EngineState engines)
        {
            var id = view.GetInstanceID();
            if (!_engineAudio.TryGetValue(id, out var emitter) || emitter == null)
            {
                emitter = view.GetComponent<AircraftSoundEmitter>();
                if (emitter == null)
                    return;
                _engineAudio[id] = emitter;
            }

            emitter.Configure(aircraft.Type, LoadEngineClip(aircraft.Type), CreateTouchdownClip());
            emitter.InteriorListening = InCockpit;
            emitter.Apply(aircraft.Registration, pose.RotorLoad01, pose.RotorSpeed01, engines.Left, engines.Right,
                0f, 0f, pose.OnGround, false,
                _audioListener != null ? _audioListener.position : view.position,
                _aircraftZoomGain, _audioMuted, Time.unscaledDeltaTime);
        }

        /// <summary>
        /// In an airline game the fleet's own helicopter stands on the pad, so the parked demo helicopter that
        /// ADR 0186 placed there is hidden for as long as the fleet runs.
        /// </summary>
        private void UpdateStaticRescueHelicopter()
        {
            var hide = FleetMode;
            if (hide == _staticRescueHelicopterHidden)
                return;
            var stand = AirsideAdelaideEmergencyAviation.StaticHelicopter;
            if (stand == null)
                return;
            stand.gameObject.SetActive(!hide);
            _staticRescueHelicopterHidden = hide;
        }
    }
}

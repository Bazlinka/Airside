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
        private static void UpdateAircraftLightsAndGear(
            LightGearPart[] parts, AircraftPhase phase, float daylight, float progress01 = 1f, float deltaTime = -1f,
            float presentationTime = 0f, EngineState? engines = null, GroundPose? groundPose = null,
            AircraftType aircraftType = null)
        {
            if (deltaTime < 0f)
                deltaTime = Time.unscaledDeltaTime;
            // Pause freezes strut/door motion with the presentation clock.
            if (deltaTime <= 0f)
                deltaTime = 0f;
            var gearBias = aircraftType != null
                ? AirsideReusableMotion.GearBias(phase, progress01, aircraftType)
                : AirsideReusableMotion.GearBias(phase, progress01);
            var retractTarget = 1f - gearBias;
            var airborne = phase is AircraftPhase.Departed or AircraftPhase.Approach
                or AircraftPhase.Circuit or AircraftPhase.GoAround
                || (phase == AircraftPhase.Takeoff
                    && AirsideReusableMotion.SecondsSinceLiftoff(phase, progress01, aircraftType) > 0f);
            var enginesOn = engines?.AnyRunning ?? AirsideReusableMotion.PropellersSpinning(phase);
            var night = daylight < 0.35f;
            var profile = AircraftLightingProfile.For(aircraftType);
            var root = parts.Length > 0 ? parts[0].AircraftRoot : null;
            if (parts.Length > 0) presentationTime += parts[0].LightingClockOffset;
            var height = root != null ? Mathf.Max(0f, root.position.y - AirsideFlightPath.GroundY) : 0f;
            var landingLights = profile.LandingLampOn(phase, height);
            var camera = Camera.main;
            var bearing = root != null && camera != null
                ? Mathf.Atan2(root.InverseTransformPoint(camera.transform.position).x,
                    root.InverseTransformPoint(camera.transform.position).z) * Mathf.Rad2Deg : 0f;
            // Ground-movement phases only. This used to also gate on `night ||`, which made
            // the phase check meaningless after dark: EngineStartSequence spools engines up to
            // 120s before an at-stand departure and ramps them down over up to 35s after an
            // at-stand arrival, so `enginesOn` was already true while `phase == AtStand` for
            // those windows — every night departure/arrival beamed the nose taxi spotlight
            // from a motionless, gate-parked aircraft. A cold, parked aircraft never lit it;
            // this was the same bug in a narrower, still-visible form.
            // ADR 0126: and only while actually taxiing forward — dark on the tail-first push and
            // while stopped in a queue, as crews do, instead of lit from pushback to the hold.
            var taxiLights = profile.TaxiLampOn(phase, airborne, enginesOn,
                groundPose == null || (!groundPose.Value.TailFirst && groundPose.Value.Speed > 0.5f));

            for (var i = 0; i < parts.Length; i++)
            {
                var child = parts[i].Transform;
                if (child == null)
                    continue;
                switch (parts[i].Kind)
                {
                    case LightGearKind.GearDoor:
                        // Belly panels open before the leg moves and shut once it is locked up or down.
                        child.gameObject.SetActive(true);
                        PoseGearDoor(parts[i], retractTarget, deltaTime);
                        break;
                    case LightGearKind.GearStrut:
                        // Each leg folds the way its airframe's gear does (nose forward; mains inboard, aft or
                        // forward), steering composed inside the fold. Exact strut names only: the oleo, scissors
                        // and tyres are nested under the leg and ride it.
                        child.gameObject.SetActive(true);
                        PoseGearStrut(parts[i], retractTarget, deltaTime);
                        break;
                    case LightGearKind.GearTruck:
                        PoseGearTruck(parts[i], retractTarget, deltaTime);
                        break;
                    case LightGearKind.NavigationLight:
                    {
                        var navOn = AirsideReusableMotion.NavigationLightsOn(
                            engines?.AnyRunning ?? enginesOn,
                            engines?.Beacon ?? enginesOn);
                        // The lens is always there; only its glow and light switch (ADR 0124).
                        child.gameObject.SetActive(true);
                        EnsureNavPointLight(parts[i], navOn, profile);
                        var strobe = 0f;
                        if (parts[i].NavLight is AircraftNavigationLight.Left or AircraftNavigationLight.Right
                            || parts[i].NavLight == AircraftNavigationLight.Tail && profile.TailStrobe)
                        {
                            strobe = navOn && AirsideReusableMotion.StrobesOn(phase)
                                ? profile.StrobeLevel(presentationTime)
                                : 0f;
                            EnsureWingtipStrobe(parts[i], strobe, profile);
                        }
                        var visibility = camera != null
                            ? AircraftLightingProfile.NavigationVisibility(parts[i].NavLight, bearing) : 1f;
                        // The navigation lens remains coloured through the white strobe flash.
                        GlowLamp(parts[i], NavLensColor(parts[i].NavLight), navOn ? visibility : 0f, 0f);
                        break;
                    }
                    case LightGearKind.Beacon:
                    {
                        var beacon = profile.BeaconLevel(engines?.Beacon ?? enginesOn, presentationTime);
                        child.gameObject.SetActive(true);
                        EnsureBeaconPointLight(parts[i], beacon, profile);
                        GlowLamp(parts[i], new Color(1f, 0.16f, 0.08f), beacon, 0f);
                        break;
                    }
                    case LightGearKind.LandingLight:
                    {
                        child.gameObject.SetActive(true);
                        EnsureLandingSpotLight(parts[i], landingLights, night, profile);
                        if (!parts[i].LampResolved)
                        {
                            parts[i].Lamp = child.GetComponent<Renderer>();
                            parts[i].LampResolved = true;
                        }
                        var lamp = parts[i].Lamp;
                        if (lamp != null)
                        {
                            // Through SetRendererColor so the lamp material's _EMISSION keyword is
                            // on: an emission colour in a property block alone is ignored by URP
                            // Lit, so the lit landing lamps never glowed.
                            var color = landingLights
                                ? new Color(1f, 0.97f, 0.88f)
                                : new Color(0.55f, 0.55f, 0.5f);
                            SetRendererColor(lamp, color, landingLights
                                ? new Color(2.6f, 2.5f, 2.1f)
                                : Color.black);
                        }
                        UpdateLampFlare(parts[i], camera, landingLights, new Color(1f, 0.96f, 0.86f), 0.9f, 22f);
                        break;
                    }
                    case LightGearKind.TaxiLight:
                    {
                        child.gameObject.SetActive(true);
                        EnsureTaxiSpotLight(parts[i], taxiLights, profile);
                        GlowLamp(parts[i], new Color(1f, 0.94f, 0.78f), taxiLights ? 1f : 0f, 0f);
                        UpdateLampFlare(parts[i], camera, taxiLights, new Color(1f, 0.94f, 0.78f), 0.5f, 14f);
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Where a lamp's light sits (ADR 0124). The glTF aircraft kits export every lamp node with
        /// a zero transform and the lens baked into the mesh, so a Light on the node shone from
        /// the airframe origin — nav lights and strobes lit the belly. A child at the lens mesh's
        /// bounds centre puts the light in the lens; procedural lamps (centred cubes) get zero.
        /// </summary>
        private static MaterialPropertyBlock _lampFlareBlock;

        /// <summary>
        /// A lit landing/taxi lamp is a 20 cm lens on a 40 m airframe: invisible from any play camera, so
        /// the aircraft looked like it had no landing lights. Draw a soft camera-facing flare on the lamp
        /// while it is lit, sized to stay readable at distance (never below <paramref name="minPixels"/>
        /// across) and only seen from in front of the lamp, as a real beam is. Presentation only.
        /// </summary>
        private static void UpdateLampFlare(LightGearPart part, Camera camera, bool lit, Color colour, float minMetres, float minPixels)
        {
            var flare = part.Flare;
            if (flare == null && (!lit || camera == null || part.AircraftRoot == null || !AirsideSettings.Current.AircraftLights))
                return;
            var strength = 0f;
            var distance = 0f;
            Vector3 centre = default;
            if (lit && camera != null && part.AircraftRoot != null && AirsideSettings.Current.AircraftLights)
            {
                centre = LampPivot(part.Transform).position;
                var toCamera = camera.transform.position - centre;
                distance = toCamera.magnitude;
                if (distance > 0.1f)
                {
                    var facing = Vector3.Dot(part.AircraftRoot.forward, toCamera / distance);
                    strength = Mathf.Clamp01((facing - 0.1f) / 0.5f);
                }
            }

            if (strength <= 0.01f)
            {
                if (flare != null && flare.enabled)
                    flare.enabled = false;
                return;
            }

            if (flare == null)
            {
                var material = HaloMaterial();
                if (material == null)
                    return;
                var card = GameObject.CreatePrimitive(PrimitiveType.Quad);
                card.name = "Lamp flare";
                DestroyPresentationObject(card.GetComponent<Collider>());
                card.transform.SetParent(part.Transform, true);
                flare = part.Flare = card.GetComponent<Renderer>();
                flare.sharedMaterial = material;
                flare.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                flare.receiveShadows = false;
            }

            flare.enabled = true;
            var t = flare.transform;
            t.position = centre;
            var worldPerPixel = 2f * distance * Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad)
                                / Mathf.Max(1f, camera.pixelHeight);
            var size = Mathf.Max(minMetres, worldPerPixel * minPixels);
            var parentScale = part.Transform.lossyScale.x > 0.0001f ? part.Transform.lossyScale.x : 1f;
            t.localScale = Vector3.one * (size / parentScale);
            t.rotation = Quaternion.LookRotation(t.position - camera.transform.position, camera.transform.up);
            _lampFlareBlock ??= new MaterialPropertyBlock();
            _lampFlareBlock.SetColor("_BaseColor", colour * (1.8f * strength));
            flare.SetPropertyBlock(_lampFlareBlock);
        }

        private static Transform LampPivot(Transform lamp)
        {
            var pivot = lamp.Find(LampPivotName);
            if (pivot != null)
                return pivot;
            pivot = new GameObject(LampPivotName).transform;
            pivot.SetParent(lamp, false);
            var filter = lamp.GetComponent<MeshFilter>();
            if (filter != null && filter.sharedMesh != null)
                pivot.localPosition = filter.sharedMesh.bounds.center;
            return pivot;
        }

        private static Color NavLensColor(AircraftNavigationLight kind)
            => ToColor(AircraftNavigationPalette.For(kind));

        /// <summary>Lens glow: its own colour when on, a white flash for the wingtip strobe.</summary>
        private static void GlowLamp(LightGearPart part, Color lens, float on, float strobe)
        {
            if (!part.LampResolved)
            {
                part.Lamp = part.Transform.GetComponent<Renderer>();
                part.LampResolved = true;
            }

            if (part.Lamp == null)
                return;
            var emission = lens * (2.4f * on) + new Color(3.5f, 3.6f, 3.8f) * strobe;
            SetRendererColor(part.Lamp, Color.Lerp(lens * 0.55f, lens, on), emission);
        }

        /// <summary>
        /// Decision 0025 items 5+7 — wingtip nav lights cast real coloured PointLights.
        /// </summary>
        private static void EnsureNavPointLight(LightGearPart part, bool on, AircraftLightingProfile profile)
        {
            var lamp = part.Transform;
            var kind = part.NavLight;
            if (part.Light == null)
                part.Light = LampPivot(lamp).GetComponent<Light>() ?? lamp.GetComponent<Light>();
            var light = part.Light;
            if (light == null)
            {
                light = LampPivot(lamp).gameObject.AddComponent<Light>();
                light.type = LightType.Spot;
                light.color = NavLensColor(kind);
                light.range = 8f;
                light.shadows = LightShadows.None;
                part.Light = light;
            }

            light.enabled = on && AirsideSettings.Current.AircraftLights;
            if (on)
            {
                light.range = profile.NavRange;
                light.intensity = 0.6f * AirsideReusableMotion.NavSteady;
                light.type = LightType.Spot;
                light.spotAngle = kind == AircraftNavigationLight.Tail ? 140f : 110f;
                light.innerSpotAngle = light.spotAngle - 4f;
                var yaw = kind == AircraftNavigationLight.Left ? -55f
                    : kind == AircraftNavigationLight.Right ? 55f : 180f;
                if (part.AircraftRoot != null)
                    light.transform.rotation = part.AircraftRoot.rotation * Quaternion.Euler(0f, yaw, 0f);
            }
        }

        private static void EnsureWingtipStrobe(LightGearPart part, float intensity, AircraftLightingProfile profile)
        {
            var point = part.Strobe;
            if (point == null)
            {
                var wingtip = LampPivot(part.Transform);
                var strobe = wingtip.Find("White strobe");
                if (strobe == null)
                {
                    strobe = GameObject.CreatePrimitive(PrimitiveType.Sphere).transform;
                    strobe.name = "White strobe";
                    DestroyPresentationObject(strobe.GetComponent<Collider>());
                    var renderer = strobe.GetComponent<Renderer>();
                    renderer.sharedMaterial = CreateMaterial(new Color(0.65f, 0.68f, 0.7f));
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                    strobe.SetParent(wingtip, false);
                    // A separate clear lens next to the coloured position lamp, in metres.
                    var scale = Mathf.Max(0.001f, wingtip.lossyScale.x);
                    strobe.localScale = Vector3.one * (0.12f / scale);
                    strobe.position = wingtip.position + (part.AircraftRoot != null ? part.AircraftRoot.up : Vector3.up) * 0.10f;
                    var light = strobe.gameObject.AddComponent<Light>();
                    light.type = LightType.Point;
                    light.color = new Color(0.92f, 0.96f, 1f);
                    light.range = 18f;
                    light.shadows = LightShadows.None;
                }

                point = part.Strobe = strobe.GetComponent<Light>();
                part.StrobeLens = strobe.GetComponent<Renderer>();
            }

            point.enabled = intensity > 0.01f && AirsideSettings.Current.AircraftLights;
            point.range = profile.StrobeRange;
            point.intensity = profile.StrobeIntensity * intensity;
            if (part.StrobeLens != null)
                SetRendererColor(part.StrobeLens, new Color(0.65f, 0.68f, 0.7f),
                    new Color(3.5f, 3.6f, 3.8f) * intensity);
        }

        private static void EnsureBeaconPointLight(LightGearPart part, float intensity, AircraftLightingProfile profile)
        {
            var lamp = part.Transform;
            if (part.Light == null)
                part.Light = LampPivot(lamp).GetComponent<Light>() ?? lamp.GetComponent<Light>();
            var light = part.Light;
            if (light == null)
            {
                light = part.Light = LampPivot(lamp).gameObject.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(1f, 0.25f, 0.12f);
                light.range = 10f;
                light.shadows = LightShadows.None;
            }

            light.enabled = intensity > 0.01f && AirsideSettings.Current.AircraftLights;
            light.range = profile.BeaconRange;
            light.intensity = 2.6f * intensity;
        }

        /// <summary>
        /// Decision 0025 items 5+7 — real SpotLights on landing / taxi lamp meshes so
        /// approach and night taxi cast light on the runway and apron.
        /// </summary>
        private static void EnsureLandingSpotLight(LightGearPart part, bool on, bool night, AircraftLightingProfile profile)
        {
            var lamp = part.Transform;
            if (part.Light == null)
                part.Light = LampPivot(lamp).GetComponent<Light>() ?? lamp.GetComponent<Light>();
            var light = part.Light;
            if (light == null)
            {
                light = part.Light = LampPivot(lamp).gameObject.AddComponent<Light>();
                light.type = LightType.Spot;
                MoveBeamEmitterToLensFront(lamp, light);
                light.color = new Color(1f, 0.97f, 0.88f);
                light.range = 42f;
                light.spotAngle = 48f;
                light.innerSpotAngle = 22f;
            }

            light.enabled = on && AirsideSettings.Current.AircraftLights;
            if (!light.enabled)
                return;
            // ADR 0101: every shadowed spot re-renders the shadow casters into the additional-
            // light atlas each frame. In daylight the sun's key shadow swamps a landing lamp's,
            // so the extra pass bought nothing; keep it for night, where the beam is the key.
            var shadows = AirsideRuntimeQuality.LandingLampShadows(night);
            if (light.shadows != shadows)
                light.shadows = shadows;
            // Pinned daylight washes a night-tuned lamp. Keep the beam readable in follow.
            light.intensity = night ? profile.LandingIntensityNight : profile.LandingIntensityDay;
            light.range = profile.LandingRange;
            light.spotAngle = profile.LandingSpotAngle;
            light.innerSpotAngle = profile.LandingInnerAngle;
            // Lamp mesh faces +Z (aircraft forward); SpotLights aim along local +Z. Aim a little down
            // and toe the wing-root lamps outwards (right lamp +X = starboard, left lamp -X).
            var toe = lamp.name.EndsWith(" R", StringComparison.Ordinal) ? profile.LandingToeOutDegrees
                : lamp.name.EndsWith(" L", StringComparison.Ordinal) ? -profile.LandingToeOutDegrees
                : 0f;
            var height = part.AircraftRoot != null
                ? Mathf.Max(0f, part.AircraftRoot.InverseTransformPoint(light.transform.position).y + AirsideFlightPath.GroundY)
                : 0f;
            var pitch = profile.LandingPitchDownDegrees;
            // Actual fittings can sit higher than the previous guessed family height.
            // Keep the beam axis inside its range, without steering a flying beam at the ground.
            if (height > 0f)
                pitch = Mathf.Max(pitch, Mathf.Atan2(height, profile.LandingRange * 0.7f) * Mathf.Rad2Deg);
            light.transform.localRotation = Quaternion.Euler(pitch, toe, 0f);
        }

        private static void MoveBeamEmitterToLensFront(Transform lamp, Light light)
        {
            var filter = lamp.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null || light.transform == lamp) return;
            var bounds = filter.sharedMesh.bounds;
            // The exported lens has thickness. Emit in front of it, avoiding its own
            // shadow and an extremely bright spot inside the lamp housing.
            light.transform.localPosition = bounds.center + Vector3.forward * (bounds.extents.z + 0.03f);
        }

        private static void EnsureTaxiSpotLight(LightGearPart part, bool on, AircraftLightingProfile profile)
        {
            var lamp = part.Transform;
            if (part.Light == null)
                part.Light = LampPivot(lamp).GetComponent<Light>() ?? lamp.GetComponent<Light>();
            var light = part.Light;
            if (light == null)
            {
                light = part.Light = LampPivot(lamp).gameObject.AddComponent<Light>();
                light.type = LightType.Spot;
                MoveBeamEmitterToLensFront(lamp, light);
                light.color = new Color(1f, 0.94f, 0.78f);
                light.range = 18f;
                light.spotAngle = 55f;
                light.innerSpotAngle = 28f;
                light.shadows = LightShadows.None;
                light.intensity = 2.4f;
            }

            light.enabled = on && AirsideSettings.Current.AircraftLights;
            if (!light.enabled)
                return;
            light.range = profile.TaxiRange;
            light.spotAngle = profile.TaxiSpotAngle;
            light.innerSpotAngle = profile.TaxiInnerAngle;
            light.intensity = profile.TaxiIntensity;
            light.transform.localRotation = Quaternion.Euler(profile.TaxiPitchDownDegrees, 0f, 0f);
        }

        /// <summary>Glow has only six distinct outputs; do not rewrite every pane each frame.</summary>
        private static int CabinWindowGlowState(AircraftPhase phase, float daylight)
        {
            var phaseBand = phase == AircraftPhase.AtStand ? 1
                : phase == AircraftPhase.Departed ? 2 : 3;
            return phaseBand + (daylight < 0.4f ? 3 : 0);
        }

        /// <summary>
        /// Decision 0025 items 5+7 — cabin / cockpit glass picks up warm emissive glow
        /// at night and a softer stand dwell glow so the airframe reads alive.
        /// </summary>
        private static void UpdateCabinWindowGlow(
            (Transform Transform, Renderer Renderer, Color BaseColor)[] glass, AircraftPhase phase, float daylight)
        {
            var night = daylight < 0.4f;
            var atStand = phase == AircraftPhase.AtStand;
            var enginesOn = phase != AircraftPhase.AtStand && phase != AircraftPhase.Departed;
            var intensity = 0f;
            if (night)
                intensity = atStand ? 1.35f : enginesOn ? 1.05f : 0.55f;
            else if (atStand)
                intensity = 0.22f;

            var glow = new Color(1f, 0.82f, 0.55f) * intensity;
            for (var i = 0; i < glass.Length; i++)
            {
                if (glass[i].Renderer == null)
                    continue;
                SetRendererColor(glass[i].Renderer, glass[i].BaseColor, intensity > 0.01f ? glow : Color.black);
            }
        }

        private void CollectAirfieldLights(Renderer[] renderers = null)
        {
            _airfieldLightRenderers.Clear();
            _airfieldLightsAppliedDaylight = float.NaN;
            _airfieldLightIsTaxi.Clear();
            renderers ??= AirsideSceneIndex.Renderers;
            foreach (var renderer in renderers)
            {
                if (renderer == null)
                    continue;
                var n = renderer.gameObject.name;
                if (n.StartsWith("Runway edge", StringComparison.Ordinal) ||
                    n.StartsWith("Taxi light", StringComparison.Ordinal) ||
                    n.StartsWith("ALS", StringComparison.Ordinal) ||
                    n.StartsWith("REIL", StringComparison.Ordinal) ||
                    n.StartsWith("Apron flood", StringComparison.Ordinal) ||
                    n.StartsWith("T1 streetlight lamp", StringComparison.Ordinal) ||
                    n.StartsWith("edge_", StringComparison.Ordinal) ||
                    n.StartsWith("taxi_", StringComparison.Ordinal) ||
                    n.StartsWith("flood_", StringComparison.Ordinal) ||
                    n.StartsWith("obst_", StringComparison.Ordinal) ||
                    n == "runway_edge_light" ||
                    n == "taxiway_light" ||
                    n == "apron_floodlight" ||
                    n == "obstruction_light")
                {
                    _airfieldLightRenderers.Add(renderer);
                    _airfieldLightIsTaxi.Add(n.IndexOf("taxi", StringComparison.OrdinalIgnoreCase) >= 0);
                }
            }
        }

        private void BuildLightingAndCamera()
        {
            var camera = Camera.main;
            if (camera == null)
                camera = new GameObject("Main Camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            // Match overview framing (architectural miniature, decision 0022 / post-F polish).
            camera.fieldOfView = AirsideBareField.Enabled ? AirsideBareField.OverviewFov : 50f;
            // The bare field is 3.4 km across; the compact full-airport QA scene is not.
            // Tightening the latter's far plane keeps depth precision on apron paint and
            // avoids paying to submit a mostly empty 10 km view volume.
            camera.farClipPlane = AirsideBareField.Enabled ? AirsideBareField.CameraFarClip : 1200f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            _mainCamera = camera;

            _cameraController = camera.GetComponent<AirsideCameraController>();
            if (_cameraController == null)
                _cameraController = camera.gameObject.AddComponent<AirsideCameraController>();

            EnsureFocusAudioListener(camera);

            _sun = FindPreferredSunLight();
            if (_sun == null)
                _sun = new GameObject("Sun").AddComponent<Light>();
            _sun.type = LightType.Directional;
            _sun.shadows = LightShadows.Soft;
            _sun.shadowStrength = 0.78f;
            _sun.shadowBias = 0.035f;
            _sun.shadowNormalBias = 0.4f;

            // Cool fill opposite the key — softens night and dawn without a full probe bake.
            var fillGo = FindBuilt("Fill light");
            _fillLight = fillGo != null ? fillGo.GetComponent<Light>() : null;
            if (_fillLight == null)
                _fillLight = new GameObject("Fill light").AddComponent<Light>();
            _fillLight.type = LightType.Directional;
            _fillLight.shadows = LightShadows.None;
            _fillLight.intensity = 0.25f;
            _fillLight.color = new Color(0.45f, 0.55f, 0.75f);

            ApplyDayCycle();
        }

        /// <summary>Prefer a Light named "Sun", else an existing DirectionalLight — not a random Spot.</summary>
        private static Light FindPreferredSunLight()
        {
            var named = FindBuilt("Sun") ?? FindBuilt("Directional Light");
            return named != null ? named.GetComponent<Light>() : null;
        }

        /// <summary>
        /// A storm strike's flash: an instant spike, a quick partial fade, a smaller second
        /// pop, then dark within half a second — the double-flicker read of real lightning.
        /// 0 outside that half-second window either side of the strike.
        /// </summary>
        public static float LightningFlashEnvelope(float secondsSinceStrike)
        {
            if (secondsSinceStrike < 0f || secondsSinceStrike > 0.5f)
                return 0f;
            var primary = Mathf.Exp(-secondsSinceStrike * 14f);
            const float secondPulseAt = 0.09f;
            var secondary = secondsSinceStrike > secondPulseAt
                ? Mathf.Exp(-(secondsSinceStrike - secondPulseAt) * 22f) * 0.5f
                : 0f;
            return Mathf.Clamp01(Mathf.Max(primary, secondary));
        }

        private static byte[] ReilSides(Light[] lights)
        {
            var sides = new byte[lights.Length];
            for (var i = 0; i < lights.Length; i++)
            {
                if (lights[i] == null)
                    continue;
                var name = lights[i].name;
                if (name.StartsWith("REIL", StringComparison.Ordinal))
                    sides[i] = name.EndsWith("R", StringComparison.Ordinal) ? (byte)2 : (byte)1;
            }

            return sides;
        }

        private void UpdateAirfieldNavLights(float daylight)
        {
            // Edge / taxi lights punch up at dusk/night so the airfield stays readable.
            // No time term here: in steady day or night every lamp gets the same tint as last
            // frame, so hundreds of property-block writes are skipped until daylight moves.
            if (AirsideRuntimeQuality.DaylightSteady(_airfieldLightsAppliedDaylight, daylight))
                return;
            _airfieldLightsAppliedDaylight = daylight;
            UpdateLensGroups(daylight);
            var night = 1f - daylight;
            var intensity = Mathf.Lerp(0.35f, 1.35f, night);
            var warmWhite = Color.Lerp(new Color(0.85f, 0.88f, 0.7f), new Color(1f, 0.95f, 0.75f), night);
            for (var i = 0; i < _airfieldLightRenderers.Count; i++)
            {
                var renderer = _airfieldLightRenderers[i];
                if (renderer == null)
                    continue;
                var baseColor = _airfieldLightIsTaxi[i]
                    ? new Color(0.25f, 0.55f, 1f)
                    : warmWhite;
                var color = baseColor * intensity;
                color.a = 1f;
                SetRendererColor(renderer, color, baseColor * (0.2f + night * 1.4f));
            }
        }

        private void CollectNightGlowWindows()
        {
            _nightGlowRenderers.Clear();
            // Membership sets: List.Contains inside the scene-wide renderer walk below was
            // O(renderers x glow panes) during startup.
            var glowSet = new HashSet<Renderer>();
            var lightSet = new HashSet<Light>();
            var lights = new List<Light>();
            foreach (var name in new[]
                     {
                         "Terminal window glow L",
                         "Terminal window glow R",
                         "Terminal landside glow",
                         "Terminal canopy glow",
                         "Terminal canopy glow W",
                         "Terminal canopy glow E",
                         "Hangar window glow",
                         "Ops shed window glow",
                         "interior_glow_l",
                         "interior_glow_r",
                         "interior_glow_mid",
                         "interior_glow_desk",
                         "interior_glow",
                         "canopy_light_l",
                         "canopy_light_r",
                         "canopy_light_mid",
                         "side_window",
                         "side_window_b",
                         "office_window",
                         "window_l",
                         "window_r",
                         "window_side",
                         "window_side_b",
                         "glass_pane",
                         "glass_pane_l",
                         "glass_pane_r",
                         "glass_front",
                         "landside_glass"
                     })
            {
                var go = AirsideSceneIndex.FindGameObject(name);
                if (go == null)
                    continue;
                var renderer = go.GetComponent<Renderer>();
                if (renderer != null && glowSet.Add(renderer))
                    _nightGlowRenderers.Add(renderer);

                var wantsPoint = AirsideRuntimeQuality.WindowPointLights
                    && !(name.StartsWith("glass_pane", StringComparison.Ordinal)
                                   || name is "glass_front" or "landside_glass"
                                   || name.StartsWith("side_window", StringComparison.Ordinal)
                                   || name.StartsWith("window_", StringComparison.Ordinal)
                                   || name == "office_window");
                if (!wantsPoint)
                    continue;

                var light = go.GetComponent<Light>();
                if (light == null)
                {
                    light = go.AddComponent<Light>();
                    light.type = LightType.Point;
                    light.color = new Color(1f, 0.78f, 0.45f);
                    light.range = name.StartsWith("Hangar", StringComparison.Ordinal) ? 14f : 11f;
                    light.shadows = LightShadows.None;
                    light.intensity = 0f;
                }

                if (lightSet.Add(light))
                    lights.Add(light);
            }

            _windowLights = lights.ToArray();

            var paneLights = 0;
            var maxPaneLights = AirsideRuntimeQuality.PanePointLights;
            foreach (var renderer in AirsideSceneIndex.Renderers)
            {
                if (renderer == null || glowSet.Contains(renderer))
                    continue;
                var n = renderer.gameObject.name;
                // ADR 0124 merged facade glazing: one renderer each, no point lights (the
                // mesh pivot is the world origin, not a window).
                if (n is BuildingWindowsLitName or TowerCabGlassName)
                {
                    glowSet.Add(renderer);
                    _nightGlowRenderers.Add(renderer);
                    continue;
                }

                if (!(n.StartsWith("glass_pane", StringComparison.Ordinal)
                      || n.StartsWith("Terminal airside glazing", StringComparison.Ordinal)
                      || n.StartsWith("Terminal airside interior glow", StringComparison.Ordinal)
                      || n.StartsWith("skylight_l", StringComparison.Ordinal)
                      || n.StartsWith("skylight_r", StringComparison.Ordinal)
                      || n.StartsWith("skylight_mid", StringComparison.Ordinal)
                      || n is "skylight_l" or "skylight_r" or "skylight_mid"))
                    continue;
                if (n.StartsWith("skylight_frame", StringComparison.Ordinal))
                    continue;

                glowSet.Add(renderer);
                _nightGlowRenderers.Add(renderer);
                if (paneLights >= maxPaneLights || (_nightGlowRenderers.Count % 7) != 0)
                    continue;

                var light = renderer.GetComponent<Light>();
                if (light == null)
                {
                    light = renderer.gameObject.AddComponent<Light>();
                    light.type = LightType.Point;
                    light.color = new Color(1f, 0.78f, 0.45f);
                    light.range = n.StartsWith("Terminal airside glazing", StringComparison.Ordinal) ? 30f : 10f;
                    light.shadows = LightShadows.None;
                    light.intensity = 0f;
                }

                if (lightSet.Add(light))
                {
                    lights.Add(light);
                    paneLights++;
                }
            }

            _windowLights = lights.ToArray();
            _nightGlowKind.Clear();
            foreach (var renderer in _nightGlowRenderers)
            {
                var n = renderer.gameObject.name;
                _nightGlowKind.Add(n.StartsWith("Terminal airside glazing", StringComparison.Ordinal) ? (byte)1
                    : n.StartsWith("Terminal airside interior glow", StringComparison.Ordinal) ? (byte)2
                    : n is BuildingWindowsLitName or TowerCabGlassName ? (byte)3
                    : (byte)0);
            }

            _nightGlowAppliedDaylight = float.NaN;
            UpdateNightGlow(PresentationDaylight);
        }

        private void UpdateNightGlow(float daylight)
        {
            // Presentation-only: terminal/hangar windows warm up as daylight falls,
            // with a soft per-window flicker so night interiors feel occupied (0025 item 5).
            var glow = Mathf.Lerp(1.15f, 0.05f, daylight);
            var night = 1f - daylight;
            // Flicker (the only time term) runs at night; by day the panes and window lights
            // hold still, so skip rewriting them until daylight itself moves (ADR 0101).
            var flickering = night > 0.35f;
            var steady = !flickering && AirsideRuntimeQuality.DaylightSteady(_nightGlowAppliedDaylight, daylight);
            if (!steady)
                _nightGlowAppliedDaylight = flickering ? float.NaN : daylight;
            for (var i = 0; !steady && i < _nightGlowRenderers.Count; i++)
            {
                var renderer = _nightGlowRenderers[i];
                if (renderer == null)
                    continue;
                var flicker = night > 0.35f
                    ? 1f + 0.06f * Mathf.Sin(
                        Time.unscaledTime * (AirsideReusableMotion.WindowFlickerHz * Mathf.PI * 2f + i * 0.37f) + i)
                    : 1f;
                var kind = i < _nightGlowKind.Count ? _nightGlowKind[i] : (byte)0;
                if (kind == 1)
                {
                    var lit = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 0.85f, night));
                    var facade = Color.Lerp(
                        new Color(0.08f, 0.16f, 0.21f, 0.9f),
                        new Color(1.55f, 0.78f, 0.2f, 1f),
                        lit) * flicker;
                    facade.a = Mathf.Lerp(0.9f, 1f, lit);
                    SetRendererColor(renderer, facade, facade * lit);
                    continue;
                }
                if (kind == 2)
                {
                    // Behind the blue glass, use HDR unlit interior cards. At real-airport
                    // overview distance ordinary Lit emission is lost to night exposure.
                    var interior = Color.Lerp(
                        new Color(0.05f, 0.07f, 0.09f),
                        new Color(3.4f, 1.75f, 0.38f),
                        night * night) * flicker;
                    interior.a = 1f;
                    SetRendererColor(renderer, interior, interior);
                    continue;
                }
                if (kind == 3)
                {
                    // Facade glazing: dark tinted glass by day, warm offices at night. One
                    // renderer covers a whole airport of panes, so no flicker.
                    var lit = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.3f, 0.8f, night));
                    var pane = Color.Lerp(new Color(0.12f, 0.16f, 0.19f), new Color(1f, 0.74f, 0.38f), lit);
                    pane.a = 1f;
                    SetRendererColor(renderer, pane, new Color(1f, 0.68f, 0.3f) * (lit * 1.8f));
                    continue;
                }
                var color = new Color(1f, 0.82f, 0.45f, 1f) * (0.28f + glow * 0.85f) * flicker;
                color.a = 1f;
                var emission = new Color(1f, 0.72f, 0.32f) * (0.2f + glow * 2.4f) * flicker;
                SetRendererColor(renderer, color, emission);
            }

            if (_windowLights != null && !steady)
            {
                var intensity = Mathf.Lerp(2.4f, 0.02f, daylight);
                for (var i = 0; i < _windowLights.Length; i++)
                {
                    var light = _windowLights[i];
                    if (light == null)
                        continue;
                    var flicker = night > 0.35f
                        ? 1f + 0.05f * Mathf.Sin(
                            Time.unscaledTime * (AirsideReusableMotion.WindowFlickerHz * Mathf.PI * 2f + i * 0.41f) + i * 0.7f)
                        : 1f;
                    light.intensity = intensity * flicker;
                    light.enabled = intensity > 0.05f;
                }
            }

            if (_fuelFarmLight != null)
            {
                var farm = Mathf.Lerp(1.6f, 0.02f, daylight);
                _fuelFarmLight.intensity = farm;
                _fuelFarmLight.enabled = farm > 0.05f;
            }

            if (_arffBayLight != null)
            {
                var bay = Mathf.Lerp(1.8f, 0.02f, daylight);
                _arffBayLight.intensity = bay;
                _arffBayLight.enabled = bay > 0.05f;
            }
        }

        /// <summary>
        /// Decision 0025 items 5+7 — ARFF lightbar blinks amber/red at dusk so the
        /// rescue truck reads as active equipment, not a static prop.
        /// </summary>
        private void UpdateArffLightbar(float daylight)
        {
            if (_arffLightbarRenderer == null)
            {
                var truck = AirsideSceneIndex.Find("ARFF truck");
                if (truck != null)
                {
                    var bar = AirsideNamedChildren.FindContains(truck, "lightbar");
                    if (bar != null)
                        _arffLightbarRenderer = bar.GetComponent<Renderer>();
                }
            }

            if (_arffLightbarRenderer == null)
                return;

            var night = 1f - daylight;
            var blink = 0.55f + 0.45f * (0.5f + 0.5f * Mathf.Sin(
                Time.unscaledTime * AirsideReusableMotion.ArffLightbarHz * Mathf.PI * 2f));
            var amber = new Color(1f, 0.35f, 0.12f) * (0.15f + night * 1.8f * blink);
            SetRendererColor(_arffLightbarRenderer, Color.Lerp(new Color(0.95f, 0.85f, 0.2f), amber, night), amber);
        }

        private static Light[] CollectAlsLights()
        {
            var lights = new List<Light>();
            for (var i = 0; i < 8; i++)
            {
                var light = AirsideSceneIndex.FindLight($"ALS lamp {i}");
                if (light != null)
                    lights.Add(light);
            }

            foreach (var name in new[] { "REIL lamp L", "REIL lamp R" })
            {
                var light = AirsideSceneIndex.FindLight(name);
                if (light != null)
                    lights.Add(light);
            }

            return lights.ToArray();
        }

        private static Light[] BuildApronLights()
        {
            // Spot floods aimed at stand / hangar apron so authored metal picks up
            // directional wash at dusk (0025 item 5) — fewer omnidirectional spills.
            var legacySpecs = new[]
            {
                // Four corner masts — SpotLight height matches ~9 m authored flood heads.
                (new Vector3(8f, 9.2f, 12f), new Vector3(17f, 0.2f, 14f)),
                (new Vector3(32f, 9.2f, 12f), new Vector3(17f, 0.2f, 20f)),
                (new Vector3(8f, 9.2f, 22f), new Vector3(26f, 0.2f, 24f)),
                (new Vector3(32f, 9.2f, 22f), new Vector3(20f, 0.2f, 17f)),
                (new Vector3(-18f, 6.5f, 16f), new Vector3(-20f, 0.2f, 20f)),
                (new Vector3(17f, 6.8f, 26f), new Vector3(26f, 0.5f, 27f)),
                (new Vector3(-8f, 5.8f, 22f), new Vector3(-8f, 0.2f, 26f)),
                (new Vector3(20f, 6.5f, 10f), new Vector3(20f, 0.2f, 17f))
            };
            var specs = AirsideBareField.Enabled
                ? AdelaideTerminalArchitecture.ApronFloods()
                    .Select(f => (
                        new Vector3(f.X, AdelaideTerminalArchitecture.FloodHeightMetres, f.Z),
                        new Vector3(f.TargetX, 0.2f, f.TargetZ)))
                    .ToArray()
                : legacySpecs;
            var lights = new Light[AirsideRuntimeQuality.ApronFloodCount(specs.Length)];
            for (var i = 0; i < lights.Length; i++)
            {
                var (pos, lookAt) = specs[i];
                var go = new GameObject($"Apron flood {i + 1}");
                go.transform.position = pos;
                go.transform.LookAt(lookAt);
                var light = go.AddComponent<Light>();
                light.type = LightType.Spot;
                light.color = new Color(1f, 0.88f, 0.55f);
                light.range = AirsideBareField.Enabled ? AdelaideTerminalArchitecture.FloodRangeMetres : 36f;
                light.spotAngle = 78f;
                light.innerSpotAngle = 42f;
                light.intensity = 0.05f;
                // These floods are a warm fill. Soft shadows on the first four made extra
                // punctual shadow maps every frame and pushed the shadow atlas over its
                // budget (ADR 0162). The sun still shadows the apron; night landing lamps
                // still shadow on their own.
                light.shadows = LightShadows.None;
                lights[i] = light;
            }

            return lights;
        }

        /// <summary>
        /// Decision 0025 item 5 — PointLights along runway edges (every 8 m) plus taxi
        /// centreline hints and REIL pairs at both thresholds so the strip reads as a
        /// lit ribbon at dusk. Presentation only.
        /// </summary>
        private static Light[] BuildRunwayEdgePointLights()
        {
            if (AirsideBareField.Enabled)
                return BuildYpadRunwayEdgeLights();
            var lights = new System.Collections.Generic.List<Light>();
            var lightingKit = PreferArtKit(
                "Models/Props/mdl_airfield_lighting_kit_authored_v01.gltf",
                "Models/Props/mdl_airfield_lighting_kit_v02.gltf",
                "Models/Props/mdl_airfield_lighting_kit_v01.gltf");
            var hasLightingKit = !string.IsNullOrEmpty(lightingKit) && ArtGltfLoader.HasKit(lightingKit);
            var edgeStep = AirsideRuntimeQuality.EdgeLightStep;
            if (hasLightingKit)
                edgeStep = Mathf.Max(edgeStep, 10);
            var runwayEdge = (int)AirportLayout.RunwayHalfLength - 2;
            for (var x = -runwayEdge; x <= runwayEdge; x += edgeStep)
            {
                lights.Add(CreateEdgePointLight($"Runway edge point L {x}", new Vector3(x, 0.55f, -3.4f)));
                lights.Add(CreateEdgePointLight($"Runway edge point R {x}", new Vector3(x, 0.55f, 3.4f)));
            }

            // Blue taxi centreline hints — skip z=9 densify when PlaceWorldLighting owns taxi edges.
            if (!hasLightingKit)
            {
                var taxiStem = new Color(0.35f, 0.36f, 0.38f);
                var taxiLens = new Color(0.3f, 0.55f, 1f);
                for (var x = -12; x <= 28; x += 8)
                {
                    lights.Add(CreateEdgePointLight($"Taxi point {x}", new Vector3(x, 0.45f, 9f),
                        new Color(0.3f, 0.55f, 1f), range: 7.5f));
                    var origin = new Vector3(x, 0f, 9f);
                    if (!ArtGltfLoader.TryPlaceCombined(
                            lightingKit,
                            new[]
                            {
                                ("taxi_base", taxiStem),
                                ("taxi_stem", taxiStem),
                                ("taxi_lens", taxiLens)
                            },
                            origin, Quaternion.identity, $"Taxi fixture {x}", out _))
                    {
                        CreateBlock($"Taxi fixture {x}", new Vector3(x, 0.2f, 9f), new Vector3(0.18f, 0.35f, 0.18f), taxiStem);
                    }
                }
            }
            else if (AirsideRuntimeQuality.Current == AirsideRuntimeQuality.Ladder.High)
            {
                // Sparse taxi spill along Taxiway A so night taxi still reads without fixture glitter.
                for (var x = -8; x <= 24; x += 16)
                {
                    lights.Add(CreateEdgePointLight($"Taxi point {x}", new Vector3(x, 0.45f, 9f),
                        new Color(0.3f, 0.55f, 1f), range: 9f));
                }
            }

            // Always light the A1 runway exit fillet — kit thinning used to leave it dark.
            var fillet = AirsideRuntimeQuality.FilletLightCount;
            if (fillet >= 3)
            {
                lights.Add(CreateEdgePointLight("Taxi A1 point W", new Vector3(-22f, 0.45f, 2.2f),
                    new Color(0.3f, 0.55f, 1f), range: 8f));
            }

            lights.Add(CreateEdgePointLight("Taxi A1 point M", new Vector3(-18f, 0.45f, 4.5f),
                new Color(0.3f, 0.55f, 1f), range: 8f));
            if (fillet >= 3)
            {
                lights.Add(CreateEdgePointLight("Taxi A1 point E", new Vector3(-14f, 0.45f, 7f),
                    new Color(0.3f, 0.55f, 1f), range: 8f));
                lights.Add(CreateEdgePointLight("Taxi A2 point E", new Vector3(26f, 0.45f, 2.2f),
                    new Color(0.3f, 0.55f, 1f), range: 8f));
            }

            lights.Add(CreateEdgePointLight("Taxi A2 point M", new Vector3(22f, 0.45f, 4.5f),
                new Color(0.3f, 0.55f, 1f), range: 8f));
            if (fillet >= 3)
            {
                lights.Add(CreateEdgePointLight("Taxi A2 point W", new Vector3(18f, 0.45f, 7f),
                    new Color(0.3f, 0.55f, 1f), range: 8f));
            }

            // REIL-style white flashers just beyond each blast pad (blinked later).
            lights.Add(CreateEdgePointLight("REIL W L", new Vector3(-54f, 1.6f, -2.8f),
                new Color(1f, 1f, 0.95f), range: 16f));
            lights.Add(CreateEdgePointLight("REIL W R", new Vector3(-54f, 1.6f, 2.8f),
                new Color(1f, 1f, 0.95f), range: 16f));
            lights.Add(CreateEdgePointLight("REIL E L", new Vector3(54f, 1.6f, -2.8f),
                new Color(1f, 1f, 0.95f), range: 16f));
            lights.Add(CreateEdgePointLight("REIL E R", new Vector3(54f, 1.6f, 2.8f),
                new Color(1f, 1f, 0.95f), range: 16f));
            var taxiStemColor = new Color(0.35f, 0.36f, 0.38f);
            void PlaceReilPost(string name, Vector3 origin)
            {
                if (ArtGltfLoader.TryPlaceCombined(
                        lightingKit,
                        new[]
                        {
                            ("obst_base", taxiStemColor),
                            ("obst_stem", taxiStemColor),
                            ("obst_lens", new Color(1f, 1f, 0.9f))
                        },
                        origin, Quaternion.identity, name, out _))
                    return;
                CreateBlock(name, origin + new Vector3(0f, 0.8f, 0f), new Vector3(0.18f, 1.6f, 0.18f), new Color(0.4f, 0.42f, 0.44f));
            }

            PlaceReilPost("REIL post W L", new Vector3(-44f, 0f, -2.8f));
            PlaceReilPost("REIL post W R", new Vector3(-44f, 0f, 2.8f));
            PlaceReilPost("REIL post E L", new Vector3(44f, 0f, -2.8f));
            PlaceReilPost("REIL post E R", new Vector3(44f, 0f, 2.8f));
            return lights.ToArray();
        }

        private static Light CreateEdgePointLight(string name, Vector3 position, Color? color = null, float range = 11f)
        {
            var go = new GameObject(name);
            go.transform.position = position;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color ?? new Color(1f, 0.96f, 0.78f);
            light.range = range;
            light.intensity = 0.02f;
            AirsideSceneIndex.Remember(go);
            return light;
        }

        /// <summary>
        /// Decision 0025 item 5 — threshold / short approach point lights so runway
        /// ends read at dusk without a full nav-aid system. Presentation only.
        /// </summary>
        private static Light[] BuildThresholdApproachLights()
        {
            if (AirsideBareField.Enabled)
                return BuildYpadThresholdPapiAndApproachLights();
            var specs = new (Vector3 Pos, Color Color, float Range)[]
            {
                // West threshold (09) — warm white bars + green wing-bar hint.
                (new Vector3(-38f, 1.1f, -2.2f), new Color(1f, 0.96f, 0.82f), 14f),
                (new Vector3(-38f, 1.1f, 2.2f), new Color(1f, 0.96f, 0.82f), 14f),
                (new Vector3(-42f, 0.9f, -1.1f), new Color(0.35f, 0.95f, 0.55f), 10f),
                (new Vector3(-42f, 0.9f, 1.1f), new Color(0.35f, 0.95f, 0.55f), 10f),
                // East threshold (27).
                (new Vector3(38f, 1.1f, -2.2f), new Color(1f, 0.96f, 0.82f), 14f),
                (new Vector3(38f, 1.1f, 2.2f), new Color(1f, 0.96f, 0.82f), 14f),
                (new Vector3(42f, 0.9f, -1.1f), new Color(0.35f, 0.95f, 0.55f), 10f),
                (new Vector3(42f, 0.9f, 1.1f), new Color(0.35f, 0.95f, 0.55f), 10f),
                // Compact PAPI-style ladder south of west approach path.
                (new Vector3(-34f, 1.4f, -5.2f), new Color(1f, 0.35f, 0.28f), 9f),
                (new Vector3(-32.5f, 1.4f, -5.2f), new Color(1f, 0.35f, 0.28f), 9f),
                (new Vector3(-31f, 1.4f, -5.2f), new Color(1f, 0.95f, 0.75f), 9f),
                (new Vector3(-29.5f, 1.4f, -5.2f), new Color(1f, 0.95f, 0.75f), 9f)
            };

            var lights = new Light[AirsideRuntimeQuality.ThresholdLightCount(specs.Length)];
            var lightingKit = PreferArtKit(
                "Models/Props/mdl_airfield_lighting_kit_authored_v01.gltf",
                "Models/Props/mdl_airfield_lighting_kit_v02.gltf",
                "Models/Props/mdl_airfield_lighting_kit_v01.gltf");
            var stem = new Color(0.35f, 0.36f, 0.38f);
            for (var i = 0; i < lights.Length; i++)
            {
                var spec = specs[i];
                var origin = new Vector3(spec.Pos.x, 0f, spec.Pos.z);
                if (!ArtGltfLoader.TryPlaceCombined(
                        lightingKit,
                        new[]
                        {
                            ("edge_stem", stem),
                            ("edge_lens", spec.Color),
                            ("taxi_lens", spec.Color)
                        },
                        origin, Quaternion.identity, $"Threshold lamp {i}", out _))
                    CreateBlock($"Threshold lamp {i}", spec.Pos, new Vector3(0.22f, 0.18f, 0.22f), spec.Color);
                var go = new GameObject($"Threshold approach light {i + 1}");
                go.transform.position = spec.Pos + new Vector3(0f, 0.15f, 0f);
                var light = go.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = spec.Color;
                light.range = spec.Range;
                light.intensity = 0.04f;
                lights[i] = light;
            }

            return lights;
        }

        private static Light BuildAerodromeBeacon()
        {
            // Presentation-only aerodrome beacon — prefer lighting-kit obst mast.
            var mast = new GameObject("Aerodrome beacon").transform;
            mast.position = new Vector3(38f, 0f, 18f);
            var lightingKit = PreferArtKit(
                "Models/Props/mdl_airfield_lighting_kit_authored_v01.gltf",
                "Models/Props/mdl_airfield_lighting_kit_v02.gltf",
                "Models/Props/mdl_airfield_lighting_kit_v01.gltf");
            var steel = new Color(0.55f, 0.56f, 0.58f);
            var origin = mast.position;
            var kitMast = ArtGltfLoader.TryPlaceCombined(
                lightingKit,
                new[]
                {
                    ("obst_base", steel),
                    ("obst_stem", steel),
                    ("obst_lens", new Color(0.95f, 0.95f, 0.9f)),
                    ("obst_beacon_ring", new Color(1f, 0.9f, 0.5f))
                },
                origin, Quaternion.identity, "Aerodrome beacon mast", out var kitRoot);
            if (kitRoot != null)
            {
                kitRoot.SetParent(mast, true);
                kitMast = true;
            }

            if (!kitMast)
            {
                var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pole.name = "Beacon mast";
                DestroyPresentationObject(pole.GetComponent<Collider>());
                pole.transform.SetParent(mast, false);
                pole.transform.localPosition = new Vector3(0f, 4.5f, 0f);
                pole.transform.localScale = new Vector3(0.18f, 4.5f, 0.18f);
                if (pole.GetComponent<Renderer>() != null)
                    SetRendererColor(pole.GetComponent<Renderer>(), steel);

                var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                head.name = "Beacon head";
                DestroyPresentationObject(head.GetComponent<Collider>());
                head.transform.SetParent(mast, false);
                head.transform.localPosition = new Vector3(0f, 9.1f, 0f);
                head.transform.localScale = new Vector3(0.55f, 0.55f, 0.55f);
                SetRendererColor(head.GetComponent<Renderer>(), new Color(0.95f, 0.95f, 0.9f));
            }

            var lightGo = new GameObject("Beacon light");
            lightGo.transform.SetParent(mast, false);
            lightGo.transform.localPosition = new Vector3(0f, kitMast ? 6.5f : 9.1f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.85f, 1f, 0.9f);
            light.range = 42f;
            light.intensity = 0f;
            return light;
        }

        private void UpdateAerodromeBeacon(float daylight)
        {
            if (_aerodromeBeacon == null)
                return;

            // Night-only white/green pulse — presentation decoration, not navigational.
            if (daylight > 0.38f)
            {
                _aerodromeBeacon.intensity = 0f;
                _aerodromeBeacon.enabled = false;
                return;
            }

            _aerodromeBeacon.enabled = true;

            var pulse = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(PresentationClock * (AirsideReusableMotion.BeaconHz * Mathf.PI)));
            _aerodromeBeacon.intensity = pulse * Mathf.Lerp(2.4f, 0.2f, daylight / 0.38f);
            _aerodromeBeacon.color = Mathf.FloorToInt(PresentationClock * AirsideReusableMotion.BeaconHz) % 2 == 0
                ? new Color(0.95f, 0.98f, 1f)
                : new Color(0.35f, 0.95f, 0.55f);
        }

        /// <summary>
        /// Decision 0025 items 3+5 — short approach light bars west of the threshold
        /// so night approaches read as a lit path, not a bare runway end.
        /// </summary>
        private static void BuildApproachLightBars()
        {
            var bar = new Color(0.85f, 0.88f, 0.9f);
            var stem = new Color(0.35f, 0.36f, 0.38f);
            var lightingKit = PreferArtKit(
                "Models/Props/mdl_airfield_lighting_kit_authored_v01.gltf",
                "Models/Props/mdl_airfield_lighting_kit_v02.gltf",
                "Models/Props/mdl_airfield_lighting_kit_v01.gltf");
            // Simple ALS centreline + bar pairs west of runway 05 threshold (~x=-36).
            // Reuse edge/taxi/obst lighting kit parts so stations read authored, not toy cubes.
            // Kit path: fewer stations + silhouette fixtures so approach reads lit, not mesh soup.
            var hasLightingKit = !string.IsNullOrEmpty(lightingKit) && ArtGltfLoader.HasKit(lightingKit);
            var stationCount = hasLightingKit ? 5 : 8;
            var stationStep = hasLightingKit ? 7f : 5f;
            var anyKitStation = false;
            var alsCentre = new[]
            {
                ("edge_base", stem),
                ("edge_stem", stem),
                ("edge_lens", bar),
                ("edge_collar", Shade(stem, 1.1f))
            };
            var alsBar = new[]
            {
                ("edge_base", stem),
                ("edge_stem", stem),
                ("edge_lens", bar),
                ("edge_collar", Shade(stem, 1.1f)),
                ("taxi_base", stem),
                ("taxi_stem", stem),
                ("taxi_lens", bar),
                ("taxi_collar", Shade(stem, 1.05f))
            };
            for (var i = 0; i < stationCount; i++)
            {
                var x = -40f - i * stationStep;
                var origin = new Vector3(x, 0f, 0f);
                var kitStation = ArtGltfLoader.TryPlaceCombined(
                    lightingKit, i % 2 == 0 ? alsBar : alsCentre,
                    origin, Quaternion.identity, $"ALS station {i}", out _);

                if (!kitStation)
                {
                    CreateBlock($"ALS stem {i}", new Vector3(x, 0.35f, 0f), new Vector3(0.12f, 0.7f, 0.12f), stem);
                    CreateBlock($"ALS centre {i}", new Vector3(x, 0.75f, 0f), new Vector3(0.35f, 0.18f, 0.35f), bar);
                    CreateBlock($"ALS bar L {i}", new Vector3(x, 0.7f, -1.4f - i * 0.12f), new Vector3(0.25f, 0.14f, 2.2f + i * 0.18f), bar);
                    CreateBlock($"ALS bar R {i}", new Vector3(x, 0.7f, 1.4f + i * 0.12f), new Vector3(0.25f, 0.14f, 2.2f + i * 0.18f), bar);
                    if (i % 2 == 0)
                        CreateBlock($"ALS cross {i}", new Vector3(x, 0.68f, 0f), new Vector3(0.18f, 0.12f, 3.6f + i * 0.15f), bar);
                }

                if (kitStation)
                    anyKitStation = true;

                var lampGo = new GameObject($"ALS lamp {i}");
                lampGo.transform.position = new Vector3(x, 0.95f, 0f);
                // Aim SpotLights toward threshold (~x=-36) so approach washes asphalt (0025 item 5).
                lampGo.transform.rotation = Quaternion.LookRotation(new Vector3(-36f - x, -0.7f, 0f).normalized);
                var light = lampGo.AddComponent<Light>();
                light.type = LightType.Spot;
                light.color = new Color(1f, 0.95f, 0.85f);
                light.range = 14f + i * 0.6f;
                light.spotAngle = 42f;
                light.innerSpotAngle = 18f;
                light.intensity = 0f;
                light.shadows = LightShadows.None;

                // Emissive lens proxy only on greybox path — kit stations already ship edge_lens.
                if (!kitStation)
                {
                    var lens = CreateBlock($"ALS lens {i}", new Vector3(x, 0.78f, 0f), new Vector3(0.28f, 0.12f, 0.28f),
                        new Color(1f, 0.97f, 0.88f));
                    var lensRenderer = lens.GetComponent<Renderer>();
                    if (lensRenderer != null)
                        SetRendererColor(lensRenderer, new Color(1f, 0.97f, 0.88f), new Color(1f, 0.95f, 0.8f) * 1.4f);
                }
            }

            // Far REIL pair — pulsed SpotLights at night (collected with runway edge REIL names).
            var reilOriginL = new Vector3(-78f, 0f, -2.8f);
            var reilOriginR = new Vector3(-78f, 0f, 2.8f);
            var reilParts = new[]
            {
                ("obst_base", stem),
                ("obst_stem", stem),
                ("obst_lens", new Color(1f, 1f, 0.9f)),
                ("obst_guard", Shade(stem, 1.1f)),
                ("obst_ring", new Color(0.95f, 0.35f, 0.12f)),
                ("obst_cap", stem),
                ("obst_beacon_ring", new Color(1f, 0.9f, 0.5f))
            };
            var reilKit = ArtGltfLoader.TryPlaceCombined(
                    lightingKit, reilParts, reilOriginL, Quaternion.identity, "ALS REIL L", out _)
                | ArtGltfLoader.TryPlaceCombined(
                    lightingKit, reilParts, reilOriginR, Quaternion.identity, "ALS REIL R", out _);
            if (!reilKit)
            {
                CreateBlock("ALS REIL L", new Vector3(-78f, 0.8f, -2.8f), new Vector3(0.4f, 0.4f, 0.4f), new Color(1f, 1f, 0.9f));
                CreateBlock("ALS REIL R", new Vector3(-78f, 0.8f, 2.8f), new Vector3(0.4f, 0.4f, 0.4f), new Color(1f, 1f, 0.9f));
                CreateBlock("ALS REIL mast L", new Vector3(-78f, 0.4f, -2.8f), new Vector3(0.14f, 0.75f, 0.14f), stem);
                CreateBlock("ALS REIL mast R", new Vector3(-78f, 0.4f, 2.8f), new Vector3(0.14f, 0.75f, 0.14f), stem);
                CreateBlock("ALS REIL base L", new Vector3(-78f, 0.06f, -2.8f), new Vector3(0.45f, 0.1f, 0.45f), AirsideTheme.Concrete);
                CreateBlock("ALS REIL base R", new Vector3(-78f, 0.06f, 2.8f), new Vector3(0.45f, 0.1f, 0.45f), AirsideTheme.Concrete);
            }

            if (!anyKitStation)
            {
                CreateBlock("ALS lead-in bar", new Vector3(-58f, 0.72f, 0f), new Vector3(0.2f, 0.12f, 4.8f), bar);
                CreateBlock("ALS wing bar L", new Vector3(-52f, 0.7f, -3.2f), new Vector3(0.22f, 0.12f, 2.4f), bar);
                CreateBlock("ALS wing bar R", new Vector3(-52f, 0.7f, 3.2f), new Vector3(0.22f, 0.12f, 2.4f), bar);
            }
            else
            {
                // Kit path — reuse taxi stems for the approach wing / lead-in read.
                ArtGltfLoader.TryPlaceNamedMesh(
                    lightingKit, "taxi_stem", new Vector3(-58f, 0.5f, 0f),
                    Quaternion.Euler(0f, 90f, 0f), stem, out _, new Vector3(0.35f, 1.2f, 0.35f));
                ArtGltfLoader.TryPlaceNamedMesh(
                    lightingKit, "taxi_stem", new Vector3(-52f, 0.5f, -3.2f),
                    Quaternion.Euler(0f, 90f, 0f), stem, out _, new Vector3(0.3f, 0.7f, 0.3f));
                ArtGltfLoader.TryPlaceNamedMesh(
                    lightingKit, "taxi_stem", new Vector3(-52f, 0.5f, 3.2f),
                    Quaternion.Euler(0f, 90f, 0f), stem, out _, new Vector3(0.3f, 0.7f, 0.3f));
            }

            for (var side = 0; side < 2; side++)
            {
                var z = side == 0 ? -2.8f : 2.8f;
                var reilGo = new GameObject(side == 0 ? "REIL lamp L" : "REIL lamp R");
                reilGo.transform.position = new Vector3(-78f, 1.1f, z);
                reilGo.transform.rotation = Quaternion.LookRotation(new Vector3(1f, -0.15f, 0f));
                var reil = reilGo.AddComponent<Light>();
                reil.type = LightType.Spot;
                reil.color = new Color(1f, 1f, 0.92f);
                reil.range = 22f;
                reil.spotAngle = 28f;
                reil.innerSpotAngle = 12f;
                reil.intensity = 0f;
                reil.shadows = LightShadows.None;
            }
        }

        private static void PlaceWorldLighting()
        {
            var kit = PreferArtKit(
                "Models/Props/mdl_airfield_lighting_kit_authored_v01.gltf",
                "Models/Props/mdl_airfield_lighting_kit_v02.gltf",
                "Models/Props/mdl_airfield_lighting_kit_v01.gltf");
            var hasLightingKit = !string.IsNullOrEmpty(kit) && ArtGltfLoader.HasKit(kit);
            var edgeColor = new Color(1f, 1f, 0.85f);
            var taxiColor = new Color(0.25f, 0.55f, 1f);
            var obstruction = new Color(0.95f, 0.35f, 0.12f);

            var edgeStep = AirsideRuntimeQuality.LightingFixtureStep;
            if (!hasLightingKit)
                edgeStep = Mathf.Min(edgeStep, 10);
            for (var x = -44; x <= 44; x += edgeStep)
            {
                PlaceEdgeLamp(kit, new Vector3(x, 0f, -3.4f), edgeColor);
                PlaceEdgeLamp(kit, new Vector3(x, 0f, 3.4f), edgeColor);
            }

            var taxiStep = AirsideRuntimeQuality.LightingFixtureStep;
            for (var x = -8; x <= 28; x += taxiStep)
            {
                PlaceTaxiLamp(kit, new Vector3(x, 0f, 11.1f), taxiColor);
                PlaceTaxiLamp(kit, new Vector3(x, 0f, 6.9f), taxiColor);
            }

            // A1 exit fillet fixtures — path (-24,0)→(-12,9).
            PlaceTaxiLamp(kit, new Vector3(-18f, 0f, 4.5f), taxiColor);
            if (AirsideRuntimeQuality.FilletLightCount >= 3)
            {
                PlaceTaxiLamp(kit, new Vector3(-22f, 0f, 2.2f), taxiColor);
                PlaceTaxiLamp(kit, new Vector3(-14f, 0f, 7f), taxiColor);
            }

            PlaceObstructionLamp(kit, new Vector3(-20f, 5.0f, 20f), obstruction, "Hangar obstruction");
            PlaceObstructionLamp(kit, new Vector3(26f, 4.5f, 27f), obstruction, "Terminal roof light");
            PlaceObstructionLamp(kit, new Vector3(-8f, 3.2f, 26f), obstruction, "Ops obstruction");

            // Apron flood poles — four corners so night turnarounds read lit.
            var flood = new Color(0.75f, 0.78f, 0.8f);
            PlaceFloodMast(kit, new Vector3(8f, 0f, 12f), flood);
            PlaceFloodMast(kit, new Vector3(32f, 0f, 12f), flood);
            PlaceFloodMast(kit, new Vector3(8f, 0f, 22f), flood);
            PlaceFloodMast(kit, new Vector3(32f, 0f, 22f), flood);
        }

        private static void PlaceEdgeLamp(string kit, Vector3 position, Color color)
        {
            // Kit silhouette: base + stem + lens only (skip collar/gasket/glare/reflector soup).
            if (ArtGltfLoader.TryPlaceCombined(
                    kit,
                    new[]
                    {
                        ("edge_base", new Color(0.35f, 0.36f, 0.38f)),
                        ("edge_stem", new Color(0.45f, 0.46f, 0.48f)),
                        ("edge_lens", color)
                    },
                    position, Quaternion.identity, "Runway edge", out _))
                return;
            if (!ArtGltfLoader.TryPlaceNamedMesh(kit, "runway_edge_light", position, Quaternion.identity, color, out _))
                CreateBlock("Runway edge", position + new Vector3(0f, 0.05f, 0f), new Vector3(0.25f, 0.1f, 0.25f), color);
        }

        private static void PlaceTaxiLamp(string kit, Vector3 position, Color color)
        {
            if (ArtGltfLoader.TryPlaceCombined(
                    kit,
                    new[]
                    {
                        ("taxi_base", new Color(0.3f, 0.32f, 0.34f)),
                        ("taxi_stem", new Color(0.4f, 0.42f, 0.44f)),
                        ("taxi_lens", color)
                    },
                    position, Quaternion.identity, "Taxi light", out _))
                return;
            if (!ArtGltfLoader.TryPlaceNamedMesh(kit, "taxiway_light", position, Quaternion.identity, color, out _))
                CreateBlock("Taxi light", position + new Vector3(0f, 0.18f, 0f), new Vector3(0.18f, 0.35f, 0.18f), color);
        }

        private static void PlaceObstructionLamp(string kit, Vector3 position, Color color, string fallbackName)
        {
            if (ArtGltfLoader.TryPlaceCombined(
                    kit,
                    new[]
                    {
                        ("obst_base", new Color(0.35f, 0.36f, 0.38f)),
                        ("obst_stem", new Color(0.4f, 0.42f, 0.44f)),
                        ("obst_lens", color)
                    },
                    position, Quaternion.identity, fallbackName, out _))
                return;
            if (!ArtGltfLoader.TryPlaceNamedMesh(kit, "obstruction_light", position, Quaternion.identity, color, out _))
                CreateBlock(fallbackName, position + new Vector3(0f, 0.2f, 0f), new Vector3(0.22f, 0.22f, 0.22f), color);
        }

        private static void PlaceFloodMast(string kit, Vector3 position, Color color)
        {
            // Kit path: mast silhouette only — SpotLights in BuildApronLights still own night pools.
            var floodParts = AirsideRuntimeQuality.Current == AirsideRuntimeQuality.Ladder.High
                ? new[]
                {
                    ("flood_base", new Color(0.3f, 0.32f, 0.34f)),
                    ("flood_pole", color),
                    ("flood_head", new Color(0.25f, 0.26f, 0.28f)),
                    ("flood_crossarm", Shade(color, 0.9f)),
                    ("flood_arm", Shade(color, 0.85f)),
                    ("flood_lamp", new Color(1f, 0.95f, 0.8f)),
                    ("flood_visor", new Color(0.2f, 0.21f, 0.22f))
                }
                : new[]
                {
                    ("flood_base", new Color(0.3f, 0.32f, 0.34f)),
                    ("flood_pole", color),
                    ("flood_head", new Color(0.25f, 0.26f, 0.28f))
                };
            if (ArtGltfLoader.TryPlaceCombined(kit, floodParts, position, Quaternion.identity, "Apron flood", out _))
                return;

            if (!ArtGltfLoader.TryPlaceNamedMesh(kit, "apron_floodlight", position, Quaternion.identity, color, out _))
            {
                CreateBlock("Flood pole", position + new Vector3(0f, 4f, 0f), new Vector3(0.25f, 8f, 0.25f), color);
                CreateBlock("Flood head", position + new Vector3(0f, 8.1f, 0f), new Vector3(1.2f, 0.35f, 0.55f), new Color(0.25f, 0.26f, 0.28f));
            }
        }
    }
}

using System.Collections.Generic;
using Airside.Domain;
using Airside.Simulation;
using UnityEngine;
using UnityEngine.Rendering;

namespace Airside.Presentation
{
    /// <summary>
    /// ADR 0142 — a far-off aircraft is a few pixels of grey: an arrival 30 km out was drawn but
    /// could not be seen. Beyond 6 km each aircraft also carries a soft camera-facing glow (the
    /// ADR 0124 halo), sized to stay a few pixels across whatever the distance, bright white with
    /// landing lights on, dimmer without. Close in it fades out, where the model reads by itself.
    /// </summary>
    public sealed partial class AirsidePrototype
    {
        private const string DistantLightName = "Distant light";

        /// <summary>How many pixels across the glow stays at any distance.</summary>
        private const float DistantLightPixels = 7f;

        private readonly Dictionary<int, Renderer> _distantLights = new();
        private MaterialPropertyBlock _distantLightBlock;

        private void UpdateDistantLight(Transform view, bool landingLights, AircraftType type, AircraftPhase phase, bool powered)
        {
            if (view == null || _mainCamera == null)
                return;
            var distance = Vector3.Distance(_mainCamera.transform.position, view.position);
            var direction = view.InverseTransformPoint(_mainCamera.transform.position);
            var bearing = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            var forwardBeam = landingLights && Mathf.Abs(bearing) < 35f;
            var profile = AircraftLightingProfile.For(type);
            var flash = powered && AirsideReusableMotion.StrobesOn(phase) ? profile.StrobeLevel(PresentationClock + AircraftLightingProfile.ClockOffsetSeconds(view.name)) : 0f;
            // An arrival nose-on to the camera keeps its landing-light glow all the way in (it used to
            // vanish inside 6 km, leaving only a 20 cm lamp lens): fading out over the last 600 m.
            var beacon = ArrivalApproach.BeaconStrength(distance);
            if (forwardBeam)
                beacon = Mathf.Max(beacon, 0.7f * Mathf.Clamp01((distance - 600f) / 600f));
            var strength = AirsideSettings.Current.DistantGlows && powered
                ? beacon * (forwardBeam ? 1f : flash > 0f ? 0.85f : 0.28f)
                : 0f;
            // One entry per view ever drawn; drop those whose aircraft was destroyed, so the table stays small.
            if (_distantLights.Count > 96 && Time.frameCount % 600 == 0)
            {
                var stale = new List<int>();
                foreach (var pair in _distantLights)
                    if (pair.Value == null) stale.Add(pair.Key);
                foreach (var key in stale) _distantLights.Remove(key);
            }
            _distantLights.TryGetValue(view.GetInstanceID(), out var glow);
            if (strength <= 0.01f)
            {
                if (glow != null && glow.enabled)
                    glow.enabled = false;
                return;
            }

            if (glow == null)
            {
                var material = HaloMaterial();
                if (material == null)
                    return;
                var card = GameObject.CreatePrimitive(PrimitiveType.Quad);
                card.name = DistantLightName;
                Destroy(card.GetComponent<Collider>());
                card.transform.SetParent(view, false);
                glow = card.GetComponent<Renderer>();
                glow.sharedMaterial = material;
                glow.shadowCastingMode = ShadowCastingMode.Off;
                glow.receiveShadows = false;
                _distantLights[view.GetInstanceID()] = glow;
            }

            glow.enabled = true;
            var t = glow.transform;
            // Constant screen size: world size grows with distance and the camera's view height.
            var worldPerPixel = 2f * distance * Mathf.Tan(_mainCamera.fieldOfView * 0.5f * Mathf.Deg2Rad)
                                / Mathf.Max(1f, _mainCamera.pixelHeight);
            var size = worldPerPixel * DistantLightPixels;
            var parentScale = view.lossyScale.x > 0.0001f ? view.lossyScale.x : 1f;
            t.localScale = Vector3.one * (size / parentScale);
            t.rotation = Quaternion.LookRotation(t.position - _mainCamera.transform.position, _mainCamera.transform.up);
            _distantLightBlock ??= new MaterialPropertyBlock();
            var nav = Mathf.Abs(bearing) > 110f ? Color.white
                : NavLensColor(bearing < 0f ? AircraftNavigationLight.Left : AircraftNavigationLight.Right);
            var warm = forwardBeam ? new Color(1f, 0.96f, 0.86f)
                : flash > 0f ? new Color(0.92f, 0.96f, 1f) : nav;
            _distantLightBlock.SetColor("_BaseColor", warm * (1.6f * strength));
            glow.SetPropertyBlock(_distantLightBlock);
        }
    }
}

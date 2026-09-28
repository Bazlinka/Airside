using System.Collections.Generic;
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

        private void UpdateDistantLight(Transform view, bool landingLights)
        {
            if (view == null || _mainCamera == null)
                return;
            var distance = Vector3.Distance(_mainCamera.transform.position, view.position);
            var strength = AirsideSettings.Current.DistantGlows
                ? ArrivalApproach.BeaconStrength(distance) * (landingLights ? 1f : 0.45f)
                : 0f;
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
            var warm = landingLights ? new Color(1f, 0.96f, 0.86f) : new Color(1f, 0.55f, 0.45f);
            _distantLightBlock.SetColor("_BaseColor", warm * (1.6f * strength));
            glow.SetPropertyBlock(_distantLightBlock);
        }
    }
}

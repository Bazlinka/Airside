using System;
using Airside.Domain;
using UnityEngine;
using UnityEngine.Rendering;

namespace Airside.Presentation
{
    public sealed partial class AirsidePrototype
    {
        /// <summary>Fit the existing installation to the built airframe. No mesh asset is
        /// overwritten: fittings are moved on their runtime transforms, after model orientation.</summary>
        private static void FitAircraftLighting(Transform root, AircraftType type)
        {
            Bounds? hull = null;
            Bounds? all = null;
            Bounds? tailBoom = null;
            Transform top = null, nose = null, taxi = null;
            var hasBottom = false;
            var hasTail = false;
            var transforms = root.GetComponentsInChildren<Transform>(true);
            foreach (var t in transforms)
            {
                if (t.name == "Beacon") top = t;
                if (t.name == "Gear nose") nose = t;
                if (t.name == "TaxiLight") taxi = t;
                if (t.name == "Beacon bottom") hasBottom = true;
                if (AirsideAircraftParts.NavigationLightFor(t.name) == AircraftNavigationLight.Tail) hasTail = true;
                var filter = t.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;
                // Only body skins determine the roof, never the vertical tail or aerials.
                var body = t.name.Equals("fuselage", StringComparison.OrdinalIgnoreCase) || t.name == "Fuselage mid" || t.name == "FuselageMid";
                var bounds = filter.sharedMesh.bounds;
                for (var i = 0; i < 8; i++)
                {
                    var v = bounds.center + Vector3.Scale(bounds.extents,
                        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var local = root.InverseTransformPoint(t.TransformPoint(v));
                    if (!all.HasValue) all = new Bounds(local, Vector3.zero);
                    else { var b = all.Value; b.Encapsulate(local); all = b; }
                    if (t.name == "tail_boom")
                    {
                        if (!tailBoom.HasValue) tailBoom = new Bounds(local, Vector3.zero);
                        else { var b = tailBoom.Value; b.Encapsulate(local); tailBoom = b; }
                    }
                    if (!body) continue;
                    if (!hull.HasValue) hull = new Bounds(local, Vector3.zero);
                    else { var b = hull.Value; b.Encapsulate(local); hull = b; }
                }
            }
            // Also catches procedural fallbacks, whose lamps are added after the initial gear rig.
            if (nose != null && taxi != null && !taxi.IsChildOf(nose))
                NestUnderProp(nose, taxi, taxi.name);
            if (!hull.HasValue || !all.HasValue) return;
            var fuselage = hull.Value;
            var jet = AircraftLightingProfile.FamilyOf(type) is AircraftLightingFamily.RegionalJet
                or AircraftLightingFamily.Narrowbody or AircraftLightingFamily.Widebody;
            // Several authored jet beacons were at fin-top height. Move to the fuselage
            // crown near mid-body; retain the Bell and turboprop installations.
            if (jet && top != null)
            {
                var filter = top.GetComponent<MeshFilter>();
                var centre = filter != null && filter.sharedMesh != null
                    ? top.TransformPoint(filter.sharedMesh.bounds.center) : top.position;
                var target = root.TransformPoint(new Vector3(fuselage.center.x,
                    fuselage.max.y + 0.07f, fuselage.center.z));
                top.position += target - centre;
            }
            if (jet && !hasBottom)
                ParentBlock(root, "Beacon bottom", new Vector3(fuselage.center.x,
                    fuselage.min.y - 0.07f, fuselage.center.z), Vector3.one * 0.14f, new Color(1f, 0.16f, 0.08f));
            if (!hasTail)
                ParentBlock(root, "Tail nav light", new Vector3(fuselage.center.x,
                    (tailBoom ?? fuselage).center.y, (tailBoom ?? all.Value).min.z - 0.06f), Vector3.one * 0.12f, Color.white);

            // These lenses are fittings: when switched off they still exist and must not
            // cast miniature cube shadows on the hull. Taxi lights already ride the nose
            // strut through steering/retraction via NestLandingGearParts.
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (!(t.name.StartsWith("Beacon", StringComparison.Ordinal)
                    || t.name.StartsWith("LandingLight", StringComparison.Ordinal)
                    || t.name == "TaxiLight"
                    || AirsideAircraftParts.NavigationLightFor(t.name) != AircraftNavigationLight.None)) continue;
                var renderer = t.GetComponent<Renderer>();
                if (renderer != null) renderer.shadowCastingMode = ShadowCastingMode.Off;
            }
            AirsideNamedChildren.Forget(root);
        }
    }
}

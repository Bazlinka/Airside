using System;
using System.Collections.Generic;
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
            AddFuselageLiner(root);
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
        private static readonly Dictionary<Mesh, Mesh> FuselageLinerMeshes = new();
        private static Material _fuselageLinerMaterial;
        private const string FuselageLinerName = "Dark backing";
        /// <summary>How far in the liner sits: a fraction of the radius, about 8 cm on a narrowbody.</summary>
        private const float FuselageLinerInset = 0.965f;

        /// <summary>
        /// The fuselage kits are a single-sided skin with apertures cut for the windscreens and cabin
        /// windows, so through the glass you saw the far wall's culled back face and then the sky: a hollow
        /// airframe. A dark inner skin, the same shape drawn inside-out and a little smaller, closes it: from
        /// outside it is hidden behind the real skin, through a window it reads as a dark cabin or flight
        /// deck. Meshes are built once per kit and shared. Presentation only; the cockpit and cabin views
        /// hide the exterior shell, and this with it.
        /// </summary>
        private static void AddFuselageLiner(Transform root)
        {
            Transform fuselage = null;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.Equals("fuselage", StringComparison.OrdinalIgnoreCase)) { fuselage = t; break; }
            }

            if (fuselage == null || fuselage.Find(FuselageLinerName) != null) return;
            var filter = fuselage.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null || !filter.sharedMesh.isReadable) return;
            if (!FuselageLinerMeshes.TryGetValue(filter.sharedMesh, out var liner) || liner == null)
            {
                liner = BuildFuselageLinerMesh(filter.sharedMesh);
                FuselageLinerMeshes[filter.sharedMesh] = liner;
            }

            _fuselageLinerMaterial ??= CreateMaterial(new Color(0.03f, 0.032f, 0.036f));
            var go = new GameObject(FuselageLinerName);
            go.transform.SetParent(fuselage, false);
            go.AddComponent<MeshFilter>().sharedMesh = liner;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _fuselageLinerMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private static Mesh BuildFuselageLinerMesh(Mesh source)
        {
            var vertices = source.vertices;
            var normals = source.normals;
            var bounds = source.bounds;
            // The axis runs along z through the middle of the barrel, not the fin or wing roots.
            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            var zLow = bounds.center.z - bounds.extents.z * 0.1f;
            var zHigh = bounds.center.z + bounds.extents.z * 0.1f;
            foreach (var v in vertices)
            {
                if (v.z < zLow || v.z > zHigh) continue;
                if (v.x < minX) minX = v.x; if (v.x > maxX) maxX = v.x;
                if (v.y < minY) minY = v.y; if (v.y > maxY) maxY = v.y;
            }

            var cx = minX <= maxX ? (minX + maxX) * 0.5f : bounds.center.x;
            var cy = minY <= maxY ? (minY + maxY) * 0.5f : bounds.center.y;
            for (var i = 0; i < vertices.Length; i++)
            {
                vertices[i].x = cx + (vertices[i].x - cx) * FuselageLinerInset;
                vertices[i].y = cy + (vertices[i].y - cy) * FuselageLinerInset;
            }

            var mesh = new Mesh { name = source.name + " liner", indexFormat = source.indexFormat };
            mesh.vertices = vertices;
            if (normals != null && normals.Length == vertices.Length)
            {
                for (var i = 0; i < normals.Length; i++) normals[i] = -normals[i];
                mesh.normals = normals;
            }

            mesh.subMeshCount = source.subMeshCount;
            for (var s = 0; s < source.subMeshCount; s++)
            {
                var triangles = source.GetTriangles(s);
                for (var i = 0; i + 2 < triangles.Length; i += 3)
                    (triangles[i + 1], triangles[i + 2]) = (triangles[i + 2], triangles[i + 1]);
                mesh.SetTriangles(triangles, s);
            }

            mesh.RecalculateBounds();
            return mesh;
        }
    }
}

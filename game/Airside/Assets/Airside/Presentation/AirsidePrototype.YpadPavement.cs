using System;
using System.Collections.Generic;
using Airside.Simulation;
using UnityEngine;
using UnityEngine.Rendering;

namespace Airside.Presentation
{
    /// <summary>
    /// Builds the real Adelaide taxiways, aprons, taxi paint and terminal footprints from
    /// <see cref="AdelaideLayout"/> (OpenStreetMap, ODbL). Replaces the hand-placed
    /// rectangle-and-fillet skeleton, whose terminal sat at the wrong end of the field.
    /// </summary>
    public sealed partial class AirsidePrototype
    {
        private const float TaxiJointSegments = 14f;

        private static void BuildYpadTaxiwaysAndAprons(Color taxiAsphalt, Color paint)
        {
            var root = new GameObject("YPAD taxiways and aprons").transform;
            if (_airfieldRoot != null)
                root.SetParent(_airfieldRoot, false);

            // Heights, top surfaces in world metres: runway top 0.05, taxiways a touch
            // below so runways cover the crossings, then aprons, then sealed shoulders.
            var runwayTop = AirsideBareField.RunwayCenterY + AirsideBareField.RunwayHeightMetres * 0.5f;
            var taxiY = runwayTop - 0.006f;
            var apronY = runwayTop - 0.011f;
            var shoulderY = runwayTop - 0.016f;
            var paintY = runwayTop + 0.012f;

            var asphaltAlbedo = PreferSurfaceBasecolor("tx_asphalt_runway");
            var concreteAlbedo = PreferSurfaceBasecolor("tx_concrete_apron");
            var sealedShoulder = new Color(0.24f, 0.25f, 0.27f);
            var apronConcrete = new Color(0.46f, 0.47f, 0.48f);
            var taxiYellow = new Color(0.92f, 0.78f, 0.12f);

            var taxi = new SurfaceMesh();
            var shoulders = new SurfaceMesh();
            var edgeWear = new SurfaceMesh();
            var centrelines = new SurfaceMesh();
            foreach (var taxiway in AdelaideLayout.Taxiways)
            {
                var half = taxiway.Width * 0.5f;
                AddRibbon(taxi, taxiway.Xz, half, taxiY, roundJoints: true);
                AddRibbon(shoulders, taxiway.Xz, half + AirsideAdelaidePavement.TaxiSealedShoulderMetres, shoulderY, roundJoints: true);
                foreach (var strip in TaxiwayEdgeWear.Generate(taxiway.Xz, half))
                {
                    AddRibbon(
                        edgeWear,
                        new[] { strip.StartX, strip.StartZ, strip.EndX, strip.EndZ },
                        TaxiwayEdgeWear.WidthMetres * 0.5f,
                        taxiY + 0.003f,
                        roundJoints: false);
                }
                AddRibbon(centrelines, taxiway.Xz, 0.15f, paintY, roundJoints: false);
            }

            var aprons = new SurfaceMesh();
            var apronJoints = new SurfaceMesh();
            foreach (var apron in AdelaideLayout.Aprons)
            {
                AddPolygon(aprons, apron.Xz, apronY);
                foreach (var joint in ApronSlabJoints.Generate(apron.Xz))
                {
                    AddRibbon(
                        apronJoints,
                        new[] { joint.StartX, joint.StartZ, joint.EndX, joint.EndZ },
                        ApronSlabJoints.WidthMetres * 0.5f,
                        apronY + 0.004f,
                        roundJoints: false);
                }
            }

            var holdBars = new SurfaceMesh();
            for (var i = 0; i + 1 < AdelaideLayout.HoldingPositions.Length; i += 2)
                AddHoldBars(holdBars, AdelaideLayout.HoldingPositions[i], AdelaideLayout.HoldingPositions[i + 1], paintY);

            SpawnSurface(root, AirsideAdelaidePavement.TaxiwaysName + " shoulders", shoulders, sealedShoulder, asphaltAlbedo, castShadows: false);
            SpawnSurface(root, AirsideAdelaidePavement.ApronsName, aprons, apronConcrete, concreteAlbedo, castShadows: false);
            SpawnSurface(root, "Apron slab joints", apronJoints, new Color(0.27f, 0.28f, 0.28f), null,
                castShadows: false, useTextures: false);
            SpawnSurface(root, AirsideAdelaidePavement.TaxiwaysName, taxi, taxiAsphalt, asphaltAlbedo, castShadows: false);
            SpawnSurface(root, "Taxi edge wear", edgeWear, new Color(0.30f, 0.265f, 0.21f), null,
                castShadows: false, useTextures: false);
            SpawnSurface(root, "Taxiway centrelines", centrelines, taxiYellow, null, castShadows: false);
            SpawnSurface(root, "Runway holding positions", holdBars, taxiYellow, null, castShadows: false);
            BuildYpadStandMarkings(root, paintY, taxiYellow);

            var buildings = new SurfaceMesh();
            foreach (var terminal in AdelaideLayout.Terminals)
            {
                var height = terminal.Name.IndexOf("Flying Doctor", StringComparison.OrdinalIgnoreCase) >= 0 ? 8f : 14f;
                AddPrism(buildings, terminal.Xz, runwayTop, height);
            }

            SpawnSurface(root, AirsideAdelaidePavement.TerminalsName, buildings, new Color(0.80f, 0.80f, 0.78f), null, castShadows: true);
        }

        private static void BuildYpadStandMarkings(Transform root, float paintY, Color paint)
        {
            var geometry = new SurfaceMesh();
            foreach (var marking in AdelaideStandMarkings.All())
            {
                AddRibbon(geometry, marking.LeadIn, 0.18f, paintY, roundJoints: false);
                AddRibbon(geometry, marking.StopBar, 0.28f, paintY + 0.001f, roundJoints: false);

                var label = new GameObject($"Stand {marking.Reference} identifier");
                label.transform.SetParent(root, false);
                label.transform.position = new Vector3(marking.LabelX, paintY + 0.015f, marking.LabelZ);
                label.transform.rotation = Quaternion.Euler(90f, marking.LabelYawDegrees, 0f);
                var text = label.AddComponent<TextMesh>();
                text.text = marking.Reference;
                text.anchor = TextAnchor.MiddleCenter;
                text.alignment = TextAlignment.Center;
                text.fontSize = 64;
                text.characterSize = 0.22f;
                text.color = paint;
                var renderer = label.GetComponent<MeshRenderer>();
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                DepthTestStandLabel(renderer);
                AirsideSceneIndex.Remember(label);
            }

            SpawnSurface(root, "Regional stand lead-ins and stop bars", geometry, paint, null, castShadows: false);
        }

        /// <summary>
        /// TextMesh renders through the default font material on <c>GUI/Text Shader</c>, whose
        /// depth test is the global <c>unity_GUIZTestMode</c> — Always. The painted stand
        /// identifiers therefore drew on top of anything in front of them, including a parked
        /// aircraft standing on the very stand they name. The material's own copy of that
        /// property makes the labels depth-tested like the paint they sit on.
        /// </summary>
        private static void DepthTestStandLabel(MeshRenderer renderer)
        {
            if (renderer == null || renderer.sharedMaterial == null)
                return;
            var material = new Material(renderer.sharedMaterial) { name = "mat_stand_identifier_depth_tested" };
            material.SetInt(GuiZTestMode, (int)CompareFunction.LessEqual);
            renderer.sharedMaterial = material;
        }

        private static readonly int GuiZTestMode = Shader.PropertyToID("unity_GUIZTestMode");

        // ---- Mesh construction ----------------------------------------------------------

        private sealed class SurfaceMesh
        {
            public readonly List<Vector3> Vertices = new();
            public readonly List<Vector2> Uvs = new();
            public readonly List<int> Triangles = new();

            public int Add(Vector3 v)
            {
                Vertices.Add(v);
                // World-metre UVs so surfaces tile continuously across pieces.
                Uvs.Add(new Vector2(v.x / 9f, v.z / 9f));
                return Vertices.Count - 1;
            }

            /// <summary>Adds a triangle wound so its face points up (or outward for walls).</summary>
            public void Triangle(int a, int b, int c, Vector3 outward)
            {
                var normal = Vector3.Cross(Vertices[b] - Vertices[a], Vertices[c] - Vertices[a]);
                if (Vector3.Dot(normal, outward) < 0f)
                    (b, c) = (c, b);
                Triangles.Add(a);
                Triangles.Add(b);
                Triangles.Add(c);
            }
        }

        private static void AddRibbon(SurfaceMesh mesh, float[] xz, float halfWidth, float y, bool roundJoints)
        {
            var count = xz.Length / 2;
            for (var i = 0; i + 1 < count; i++)
            {
                var a = new Vector3(xz[i * 2], y, xz[i * 2 + 1]);
                var b = new Vector3(xz[(i + 1) * 2], y, xz[(i + 1) * 2 + 1]);
                var dir = b - a;
                if (dir.sqrMagnitude < 1e-4f)
                    continue;
                dir.Normalize();
                var side = new Vector3(-dir.z, 0f, dir.x) * halfWidth;
                var v0 = mesh.Add(a - side);
                var v1 = mesh.Add(a + side);
                var v2 = mesh.Add(b + side);
                var v3 = mesh.Add(b - side);
                mesh.Triangle(v0, v1, v2, Vector3.up);
                mesh.Triangle(v0, v2, v3, Vector3.up);
            }

            if (!roundJoints)
                return;

            // Discs at every vertex fill the wedge gaps at bends and round the ends,
            // so junctions read as paved fillets rather than cracked corners.
            for (var i = 0; i < count; i++)
                AddDisc(mesh, new Vector3(xz[i * 2], y, xz[i * 2 + 1]), halfWidth);
        }

        private static void AddDisc(SurfaceMesh mesh, Vector3 centre, float radius)
        {
            var c = mesh.Add(centre);
            var first = -1;
            var previous = -1;
            for (var s = 0; s <= TaxiJointSegments; s++)
            {
                var angle = s / TaxiJointSegments * Mathf.PI * 2f;
                var index = s == TaxiJointSegments
                    ? first
                    : mesh.Add(centre + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
                if (s == 0)
                    first = index;
                else
                    mesh.Triangle(c, previous, index, Vector3.up);
                previous = index;
            }
        }

        private static void AddHoldBars(SurfaceMesh mesh, float x, float z, float y)
        {
            // Bars run across the nearest taxiway, so find its direction at this point.
            var direction = Vector3.right;
            var best = float.MaxValue;
            var width = AirsideAdelaidePavement.TaxiwayWidthMetres;
            foreach (var taxiway in AdelaideLayout.Taxiways)
            {
                var xz = taxiway.Xz;
                for (var i = 0; i + 3 < xz.Length; i += 2)
                {
                    var a = new Vector2(xz[i], xz[i + 1]);
                    var b = new Vector2(xz[i + 2], xz[i + 3]);
                    var ab = b - a;
                    var t = Mathf.Clamp01(Vector2.Dot(new Vector2(x, z) - a, ab) / Mathf.Max(1e-4f, ab.sqrMagnitude));
                    var d = Vector2.Distance(a + ab * t, new Vector2(x, z));
                    if (d >= best || ab.sqrMagnitude < 1e-4f)
                        continue;
                    best = d;
                    direction = new Vector3(ab.x, 0f, ab.y).normalized;
                    width = taxiway.Width;
                }
            }

            // ICAO pattern A: two solid and two dashed bars; drawn here as four bars.
            var across = new Vector3(-direction.z, 0f, direction.x);
            for (var bar = 0; bar < 4; bar++)
            {
                var offset = direction * ((bar - 1.5f) * 0.9f);
                var centre = new Vector3(x, y, z) + offset;
                var halfLength = across * (width * 0.5f);
                var halfThick = direction * 0.15f;
                var v0 = mesh.Add(centre - halfLength - halfThick);
                var v1 = mesh.Add(centre + halfLength - halfThick);
                var v2 = mesh.Add(centre + halfLength + halfThick);
                var v3 = mesh.Add(centre - halfLength + halfThick);
                mesh.Triangle(v0, v1, v2, Vector3.up);
                mesh.Triangle(v0, v2, v3, Vector3.up);
            }
        }

        private static void AddPolygon(SurfaceMesh mesh, float[] xz, float y)
        {
            var count = xz.Length / 2;
            if (count < 3)
                return;
            var indices = new int[count];
            for (var i = 0; i < count; i++)
                indices[i] = mesh.Add(new Vector3(xz[i * 2], y, xz[i * 2 + 1]));
            foreach (var (a, b, c) in EarClip(xz))
                mesh.Triangle(indices[a], indices[b], indices[c], Vector3.up);
        }

        private static void AddPrism(SurfaceMesh mesh, float[] xz, float baseY, float height)
        {
            var count = xz.Length / 2;
            if (count < 3)
                return;

            var roof = new int[count];
            for (var i = 0; i < count; i++)
                roof[i] = mesh.Add(new Vector3(xz[i * 2], baseY + height, xz[i * 2 + 1]));
            foreach (var (a, b, c) in EarClip(xz))
                mesh.Triangle(roof[a], roof[b], roof[c], Vector3.up);

            // Outward from the footprint's winding, not from its centroid: the real terminal
            // and RFDS outlines are concave, and "away from the centroid" pointed 15 of their
            // walls inward, so those walls were back-face culled and left holes in the buildings.
            var signedArea = 0f;
            for (var i = 0; i < count; i++)
            {
                var j = (i + 1) % count;
                signedArea += xz[i * 2] * xz[j * 2 + 1] - xz[j * 2] * xz[i * 2 + 1];
            }

            var winding = signedArea >= 0f ? 1f : -1f;

            for (var i = 0; i < count; i++)
            {
                var j = (i + 1) % count;
                var a = new Vector3(xz[i * 2], baseY, xz[i * 2 + 1]);
                var b = new Vector3(xz[j * 2], baseY, xz[j * 2 + 1]);
                var up = Vector3.up * height;
                var edge = b - a;
                var outward = new Vector3(edge.z, 0f, -edge.x) * winding;
                var v0 = mesh.Add(a);
                var v1 = mesh.Add(b);
                var v2 = mesh.Add(b + up);
                var v3 = mesh.Add(a + up);
                mesh.Triangle(v0, v1, v2, outward);
                mesh.Triangle(v0, v2, v3, outward);
            }
        }

        /// <summary>Ear-clipping triangulation of a simple x,z polygon; returns index triples.</summary>
        private static List<(int, int, int)> EarClip(float[] xz)
        {
            var count = xz.Length / 2;
            var result = new List<(int, int, int)>();
            var remaining = new List<int>();
            for (var i = 0; i < count; i++)
                remaining.Add(i);

            float Cross(int o, int a, int b) =>
                (xz[a * 2] - xz[o * 2]) * (xz[b * 2 + 1] - xz[o * 2 + 1]) - (xz[a * 2 + 1] - xz[o * 2 + 1]) * (xz[b * 2] - xz[o * 2]);

            var area = 0f;
            for (var i = 0; i < count; i++)
                area += Cross(0, i, (i + 1) % count);
            var sign = area >= 0f ? 1f : -1f;

            bool Inside(int p, int a, int b, int c) =>
                Cross(a, b, p) * sign >= 0f && Cross(b, c, p) * sign >= 0f && Cross(c, a, p) * sign >= 0f;

            var guard = count * count;
            while (remaining.Count > 3 && guard-- > 0)
            {
                var clipped = false;
                for (var i = 0; i < remaining.Count; i++)
                {
                    var a = remaining[(i + remaining.Count - 1) % remaining.Count];
                    var b = remaining[i];
                    var c = remaining[(i + 1) % remaining.Count];
                    if (Cross(a, b, c) * sign <= 0f)
                        continue;
                    var ear = true;
                    foreach (var p in remaining)
                    {
                        if (p == a || p == b || p == c)
                            continue;
                        if (Inside(p, a, b, c))
                        {
                            ear = false;
                            break;
                        }
                    }

                    if (!ear)
                        continue;
                    result.Add((a, b, c));
                    remaining.RemoveAt(i);
                    clipped = true;
                    break;
                }

                if (!clipped)
                    break;
            }

            if (remaining.Count == 3)
                result.Add((remaining[0], remaining[1], remaining[2]));
            return result;
        }

        private static void SpawnSurface(
            Transform parent,
            string name,
            SurfaceMesh surface,
            Color color,
            string albedo,
            bool castShadows,
            bool useTextures = true)
        {
            if (surface.Triangles.Count == 0)
                return;

            var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(surface.Vertices);
            mesh.SetUVs(0, surface.Uvs);
            mesh.SetTriangles(surface.Triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            AirsideMeshUtil.UploadStatic(mesh);

            var go = new GameObject(name);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = useTextures
                ? CreateSharedSurfaceMaterial(color, albedo, Vector2.one)
                : AirsideMaterialLibrary.CreateShared(
                    color, AirsideMaterialLibrary.SurfaceKind.Default, null, Vector2.one, useTextures: false);
            renderer.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            go.transform.SetParent(parent, false);
            AirsideSceneIndex.Remember(go);
        }
    }
}

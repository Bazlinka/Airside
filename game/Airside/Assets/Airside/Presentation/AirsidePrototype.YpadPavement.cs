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

            // Sit the terminal on the landform, not runway Y — that was why the
            // OSM prisms floated over the dropped plateau on the default field.
            if (AirsideFocusMode.ShowTerminal)
            {
                var buildings = new SurfaceMesh();
                var groundY = runwayTop;
                foreach (var terminal in AdelaideLayout.Terminals)
                {
                    var height = terminal.Name.IndexOf("Flying Doctor", StringComparison.OrdinalIgnoreCase) >= 0
                        ? AdelaideTerminalArchitecture.RfdsHangarHeightMetres
                        : AdelaideTerminalArchitecture.ShellHeightMetres;
                    var sit = TerminalGroundY(terminal.Xz, runwayTop);
                    if (sit < groundY)
                        groundY = sit;
                    AddPrism(buildings, terminal.Xz, sit, height,
                        cutUndercroftPortal: terminal.Name == "Domestic & International Terminal");
                }

                SpawnSurface(root, AirsideAdelaidePavement.TerminalsName, buildings, new Color(0.43f, 0.45f, 0.46f), null, castShadows: true);
                BuildYpadOperationalBuildings(root, runwayTop);
                BuildAdelaideTerminalArchitecture(groundY);
                // Aerobridges hang off this terminal; built once the world exists (ADR 0113).
                _terminalGroundY = groundY;
            }
        }

        private static void BuildYpadOperationalBuildings(Transform root, float fallbackGroundY)
        {
            var support = new SurfaceMesh();
            var hangars = new SurfaceMesh();
            var freight = new SurfaceMesh();
            var fireStation = new SurfaceMesh();
            var tower = new SurfaceMesh();

            foreach (var building in AdelaideBuildings.All)
            {
                var sit = TerminalGroundY(building.Xz, fallbackGroundY);
                switch (building.Kind)
                {
                    case AdelaideBuildingKind.ControlTower:
                        // Preserve the surveyed footprint while giving the tower a recognisable
                        // narrow shaft and broader glazed cab instead of one 44 m concrete block.
                        var shaft = ScaleFootprint(building.Xz, 0.62f);
                        var shaftHeight = building.HeightMetres * 0.78f;
                        AddPrism(tower, shaft, sit, shaftHeight);
                        AddPrism(tower, building.Xz, sit + shaftHeight, building.HeightMetres - shaftHeight);
                        break;
                    case AdelaideBuildingKind.FireStation:
                        AddPrism(fireStation, building.Xz, sit, building.HeightMetres);
                        break;
                    case AdelaideBuildingKind.Hangar:
                        AddPrism(hangars, building.Xz, sit, building.HeightMetres);
                        break;
                    case AdelaideBuildingKind.Freight:
                        AddPrism(freight, building.Xz, sit, building.HeightMetres);
                        break;
                    default:
                        AddPrism(support, building.Xz, sit, building.HeightMetres);
                        break;
                }
            }

            var metalAlbedo = PreferSurfaceBasecolor("tx_corrugated_metal");
            SpawnSurface(root, "YPAD operational hangars", hangars, new Color(0.48f, 0.50f, 0.50f), metalAlbedo, castShadows: true);
            SpawnSurface(root, "YPAD freight and catering", freight, new Color(0.40f, 0.43f, 0.45f), metalAlbedo, castShadows: true);
            SpawnSurface(root, "YPAD support buildings", support, new Color(0.51f, 0.52f, 0.50f), null, castShadows: true);
            SpawnSurface(root, "YPAD fire station", fireStation, new Color(0.48f, 0.24f, 0.20f), null, castShadows: true);
            SpawnSurface(root, "YPAD control tower", tower, new Color(0.24f, 0.30f, 0.32f), null, castShadows: true);
        }

        private static float[] ScaleFootprint(float[] xz, float scale)
        {
            var scaled = new float[xz.Length];
            var centreX = 0f;
            var centreZ = 0f;
            var count = xz.Length / 2;
            for (var i = 0; i < count; i++)
            {
                centreX += xz[i * 2];
                centreZ += xz[i * 2 + 1];
            }

            centreX /= count;
            centreZ /= count;
            for (var i = 0; i < count; i++)
            {
                scaled[i * 2] = centreX + (xz[i * 2] - centreX) * scale;
                scaled[i * 2 + 1] = centreZ + (xz[i * 2 + 1] - centreZ) * scale;
            }

            return scaled;
        }

        private static void BuildAdelaideTerminalArchitecture(float groundY)
        {
            var glass = new Color(0.10f, 0.18f, 0.22f, 0.90f);
            var mullion = new Color(0.14f, 0.15f, 0.16f);
            var brow = new Color(0.34f, 0.36f, 0.37f);
            var skylight = new Color(0.16f, 0.24f, 0.27f);
            var plant = new Color(0.39f, 0.41f, 0.41f);
            var metalAlbedo = PreferSurfaceBasecolor("tx_corrugated_metal");

            foreach (var detail in AdelaideTerminalArchitecture.AirsideGlazing())
            {
                var pane = CreateBlock(detail.Name, new Vector3(detail.X, groundY + detail.Y, detail.Z),
                    new Vector3(detail.Width, detail.Height, detail.Depth), glass);
                // Use a runtime Lit glass instance here rather than the authored shared pane.
                // The night pass drives per-pane emission, which the authored material may not
                // expose in a packaged build.
                pane.GetComponent<Renderer>().sharedMaterial = AirsideMaterialLibrary.CreateShared(
                    glass, AirsideMaterialLibrary.SurfaceKind.Glass, null, Vector2.one, useTextures: false);

                var interior = CreateBlock(detail.Name.Replace("glazing", "interior glow"),
                    new Vector3(detail.X, groundY + detail.Y, detail.Z + 0.22f),
                    new Vector3(detail.Width - 1.2f, detail.Height - 1.0f, 0.08f),
                    new Color(0.16f, 0.11f, 0.045f));
                interior.GetComponent<Renderer>().sharedMaterial = AirsideMaterialLibrary.CreateShared(
                    new Color(0.16f, 0.11f, 0.045f), AirsideMaterialLibrary.SurfaceKind.UnlitSky,
                    null, Vector2.one, useTextures: false);
            }
            // Dark structural framing between the panes — without this the 28 bays read as
            // one continuous sheet of glass rather than a curtain wall.
            foreach (var detail in AdelaideTerminalArchitecture.GlazingMullions())
                CreateBlock(detail.Name, new Vector3(detail.X, groundY + detail.Y, detail.Z),
                    new Vector3(detail.Width, detail.Height, detail.Depth), mullion);
            foreach (var detail in AdelaideTerminalArchitecture.RoofBrow())
            {
                var canopy = CreateBlock(detail.Name, new Vector3(detail.X, groundY + detail.Y, detail.Z),
                    new Vector3(detail.Width, detail.Height, detail.Depth), brow);
                canopy.GetComponent<Renderer>().sharedMaterial =
                    CreateSharedSurfaceMaterial(brow, metalAlbedo, new Vector2(3f, 1f));
            }
            foreach (var detail in AdelaideTerminalArchitecture.RoofDetails())
            {
                var isSkylight = detail.Name.Contains("skylight");
                var block = CreateBlock(detail.Name, new Vector3(detail.X, groundY + detail.Y, detail.Z),
                    new Vector3(detail.Width, detail.Height, detail.Depth), isSkylight ? skylight : plant);
                // Plant/equipment screens are the same roof-furniture category as the brow
                // right beside them (arguably more plausibly corrugated-metal-clad than a
                // canopy) but used to render flat colour while the brow next to it was
                // textured metal. Skylights correctly stay flat glass-tint, no texture.
                if (!isSkylight)
                    block.GetComponent<Renderer>().sharedMaterial =
                        CreateSharedSurfaceMaterial(plant, metalAlbedo, new Vector2(2f, 2f));
            }
        }

        private static void BuildYpadStandMarkings(Transform root, float paintY, Color paint)
        {
            var geometry = new SurfaceMesh();
            foreach (var marking in AdelaideStandMarkings.All())
            {
                AddRibbon(geometry, marking.LeadIn, 0.22f, paintY, roundJoints: false);
                AddRibbon(geometry, marking.StopBar, 0.32f, paintY + 0.001f, roundJoints: false);
                AddClosedBox(geometry, marking.Envelope, 0.16f, paintY + 0.0015f);
                AddRibbon(geometry, marking.LeftShoulder, 0.16f, paintY + 0.001f, roundJoints: false);
                AddRibbon(geometry, marking.RightShoulder, 0.16f, paintY + 0.001f, roundJoints: false);

                // Match the runway's purpose-built stencil alphabet instead of using a
                // dynamic-font TextMesh. Real paint geometry now shares the same material,
                // depth and lighting as the lead-in and stop markings around it.
                var labelScale = AdelaideStandMarkings.StrokeLabelScale(marking.LabelCharacterSize);
                var labelMarks = AirsideStripMarkings.Label(
                    marking.Reference,
                    -AirsideStripMarkings.DesignationDigitHeight * 0.5f,
                    1f,
                    0f);
                AddStandLabel(geometry, labelMarks, marking.LabelX, marking.LabelZ,
                    marking.LabelYawDegrees, labelScale, paintY + 0.002f);
            }

            SpawnSurface(root, "Aircraft stand lead-ins and stop bars", geometry, paint, null, castShadows: false);
        }

        private static void AddStandLabel(SurfaceMesh mesh, AirsideStripMarkings.Mark[] marks,
            float originX, float originZ, float yawDegrees, float scale, float y)
        {
            if (marks == null || scale <= 0f)
                return;

            var yaw = yawDegrees * Mathf.Deg2Rad;
            var along = new Vector2(Mathf.Sin(yaw), Mathf.Cos(yaw));
            var across = new Vector2(Mathf.Cos(yaw), -Mathf.Sin(yaw));
            foreach (var mark in marks)
            {
                var halfAlong = mark.LengthX * scale * 0.5f;
                var halfAcross = mark.WidthZ * scale * 0.5f;
                var centre = new Vector2(originX, originZ)
                             + along * (mark.CenterX * scale)
                             + across * (mark.CenterZ * scale);
                var a = centre - along * halfAlong - across * halfAcross;
                var b = centre - along * halfAlong + across * halfAcross;
                var c = centre + along * halfAlong + across * halfAcross;
                var d = centre + along * halfAlong - across * halfAcross;
                var v0 = mesh.Add(new Vector3(a.x, y, a.y));
                var v1 = mesh.Add(new Vector3(b.x, y, b.y));
                var v2 = mesh.Add(new Vector3(c.x, y, c.y));
                var v3 = mesh.Add(new Vector3(d.x, y, d.y));
                mesh.Triangle(v0, v1, v2, Vector3.up);
                mesh.Triangle(v0, v2, v3, Vector3.up);
            }
        }

        /// <summary>
        /// World-space TextMesh defaults to the GUI/Text Shader (ZTest Always, ZWrite Off),
        /// so fuselage titles read through wings. Swap to a cutout Unlit that depth-tests
        /// and writes depth like real paint. Falls back to GUI ZTest if URP Unlit is missing.
        /// </summary>
        private static void DepthTestStandLabel(MeshRenderer renderer)
        {
            if (renderer == null || renderer.sharedMaterial == null)
                return;

            var source = renderer.sharedMaterial;
            var urpUnlit = Shader.Find("Universal Render Pipeline/Unlit");
            if (urpUnlit != null)
            {
                var material = new Material(urpUnlit) { name = "mat_world_label_depth_tested" };
                if (source.mainTexture != null)
                    material.SetTexture("_BaseMap", source.mainTexture);
                material.SetColor("_BaseColor", Color.white);
                material.SetFloat("_Surface", 0f);
                material.SetFloat("_AlphaClip", 1f);
                material.SetFloat("_Cutoff", 0.2f);
                material.EnableKeyword("_ALPHATEST_ON");
                material.SetOverrideTag("RenderType", "TransparentCutout");
                material.SetInt("_ZWrite", 1);
                material.SetInt("_Cull", (int)CullMode.Off);
                material.renderQueue = (int)RenderQueue.AlphaTest;
                renderer.sharedMaterial = material;
                return;
            }

            var fallback = new Material(source) { name = "mat_world_label_depth_tested" };
            fallback.SetInt(GuiZTestMode, (int)CompareFunction.LessEqual);
            renderer.sharedMaterial = fallback;
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

        private static void AddClosedBox(SurfaceMesh mesh, float[] xz, float halfWidth, float y)
        {
            if (xz == null || xz.Length < 8)
                return;
            for (var i = 0; i < 4; i++)
            {
                var a = i * 2;
                var b = ((i + 1) % 4) * 2;
                AddRibbon(mesh, new[] { xz[a], xz[a + 1], xz[b], xz[b + 1] }, halfWidth, y, roundJoints: false);
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

        private static float TerminalGroundY(float[] xz, float fallback)
        {
            if (xz == null || xz.Length < 2)
                return fallback;
            var sumX = 0f;
            var sumZ = 0f;
            var n = 0;
            for (var i = 0; i + 1 < xz.Length; i += 2)
            {
                sumX += xz[i];
                sumZ += xz[i + 1];
                n++;
            }

            if (n == 0)
                return fallback;
            return AirsideAdelaideGround.WorldHeight(sumX / n, sumZ / n);
        }

        private static void AddPrism(SurfaceMesh mesh, float[] xz, float baseY, float height, bool cutUndercroftPortal = false)
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
                var edge = b - a;
                var outward = new Vector3(edge.z, 0f, -edge.x) * winding;
                if (cutUndercroftPortal && height > AdelaideTerminalArchitecture.UndercroftPortalHeightMetres
                    && AdelaideTerminalArchitecture.TryUndercroftPortalOnWall(
                        a.x, a.z, b.x, b.z, out var first, out var last))
                {
                    var openingStart = Vector3.Lerp(a, b, first);
                    var openingEnd = Vector3.Lerp(a, b, last);
                    var lintel = Vector3.up * AdelaideTerminalArchitecture.UndercroftPortalHeightMetres;
                    AddPrismWall(mesh, a, openingStart, height, outward);
                    AddPrismWall(mesh, openingStart + lintel, openingEnd + lintel,
                        height - AdelaideTerminalArchitecture.UndercroftPortalHeightMetres, outward);
                    AddPrismWall(mesh, openingEnd, b, height, outward);
                }
                else
                    AddPrismWall(mesh, a, b, height, outward);
            }
        }

        private static void AddPrismWall(SurfaceMesh mesh, Vector3 a, Vector3 b, float height, Vector3 outward)
        {
            if ((b - a).sqrMagnitude < 0.0001f)
                return;
            var up = Vector3.up * height;
            var v0 = mesh.Add(a);
            var v1 = mesh.Add(b);
            var v2 = mesh.Add(b + up);
            var v3 = mesh.Add(a + up);
            mesh.Triangle(v0, v1, v2, outward);
            mesh.Triangle(v0, v2, v3, outward);
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

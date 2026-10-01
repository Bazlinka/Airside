using System;
using System.Collections.Generic;
using Airside.Simulation;

namespace Airside.Presentation
{
    /// <summary>Which merged mesh (and so which material) a building detail piece joins.</summary>
    public enum BuildingPart
    {
        /// <summary>Same material as the building shell: parapets, frames, trim, roof monitors.</summary>
        Shell,
        /// <summary>Glazing that warms up at night.</summary>
        WindowLit,
        /// <summary>Glazing that stays dark at night (empty offices), so facades do not read as one strip.</summary>
        WindowDark,
        /// <summary>Hangar, cargo and fire-bay doors.</summary>
        Door,
        /// <summary>Door seams, headers and the tower's structural steel.</summary>
        Trim,
        /// <summary>Rooftop air-conditioning and vents.</summary>
        Plant,
        /// <summary>Control-tower cab glass.</summary>
        CabGlass,
        /// <summary>The red obstruction light on the tower mast.</summary>
        ObstructionLight,
        /// <summary>Terminal kerb canopy and its columns.</summary>
        Canopy,
        /// <summary>Ground equipment kept inside hangars and docks: tugs, power carts, stairs (yellow-orange).</summary>
        Equipment,
        /// <summary>Tyres, bumpers, seat backs and cab glass on that equipment.</summary>
        EquipmentDark,
        /// <summary>Fire appliances and tool chests.</summary>
        EquipmentRed
    }

    /// <summary>
    /// A doorway cut right through a building's wall (ADR 0213): along edge <see cref="EdgeIndex"/> from
    /// <see cref="FromMetres"/> to <see cref="ToMetres"/> (measured from the edge's first point), from
    /// <see cref="BottomMetres"/> up to <see cref="TopMetres"/> above the base. The shell is hollow behind it.
    /// </summary>
    public readonly struct DetailOpening
    {
        public DetailOpening(int edgeIndex, float fromMetres, float toMetres, float bottomMetres, float topMetres)
        {
            EdgeIndex = edgeIndex;
            FromMetres = fromMetres;
            ToMetres = toMetres;
            BottomMetres = bottomMetres;
            TopMetres = topMetres;
        }

        public int EdgeIndex { get; }
        public float FromMetres { get; }
        public float ToMetres { get; }
        public float BottomMetres { get; }
        public float TopMetres { get; }
    }

    /// <summary>A box in world metres: centre, size along its own axis, height, depth, and the axis.</summary>
    public readonly struct DetailBox
    {
        public DetailBox(BuildingPart part, float x, float y, float z, float length, float height, float depth,
            float dirX, float dirZ)
        {
            Part = part;
            X = x;
            Y = y;
            Z = z;
            Length = length;
            Height = height;
            Depth = depth;
            DirX = dirX;
            DirZ = dirZ;
        }

        public BuildingPart Part { get; }
        public float X { get; }
        public float Y { get; }
        public float Z { get; }
        public float Length { get; }
        public float Height { get; }
        public float Depth { get; }
        /// <summary>Unit direction of <see cref="Length"/> on the ground plane.</summary>
        public float DirX { get; }
        public float DirZ { get; }
        public float Bottom => Y - Height * 0.5f;
        public float Top => Y + Height * 0.5f;
    }

    /// <summary>A vertical extrusion of an x,z footprint.</summary>
    public readonly struct DetailPrism
    {
        public DetailPrism(BuildingPart part, float[] xz, float baseY, float height)
        {
            Part = part;
            Xz = xz;
            BaseY = baseY;
            Height = height;
        }

        public BuildingPart Part { get; }
        public float[] Xz { get; }
        public float BaseY { get; }
        public float Height { get; }
        public float Top => BaseY + Height;
    }

    /// <summary>The visible roof silhouette added above an existing surveyed hangar shell.</summary>
    public enum HangarRoofProfile
    {
        Gable,
        Barrel,
        Sawtooth
    }

    /// <summary>
    /// An oriented roof in world metres. The existing footprint prism remains the watertight
    /// building; this is its light, presentation-only cap.
    /// </summary>
    public readonly struct DetailRoof
    {
        public DetailRoof(HangarRoofProfile profile, float x, float z, float length, float width,
            float eaveY, float rise, float dirX, float dirZ)
        {
            Profile = profile;
            X = x;
            Z = z;
            Length = length;
            Width = width;
            EaveY = eaveY;
            Rise = rise;
            DirX = dirX;
            DirZ = dirZ;
        }

        public HangarRoofProfile Profile { get; }
        public float X { get; }
        public float Z { get; }
        public float Length { get; }
        public float Width { get; }
        public float EaveY { get; }
        public float Rise { get; }
        public float DirX { get; }
        public float DirZ { get; }
        public float RidgeY => EaveY + Rise;
    }

    public sealed class BuildingDetailSet
    {
        public readonly List<DetailBox> Boxes = new();
        public readonly List<DetailPrism> Prisms = new();
        public readonly List<DetailRoof> Roofs = new();

        /// <summary>Doorways cut through the walls. When there are any, the building is drawn hollow behind them.</summary>
        public readonly List<DetailOpening> Openings = new();

        /// <summary>Height of the inside floor above the base (a loading dock is raised).</summary>
        public float InteriorFloorMetres;

        public bool IsHollow => Openings.Count > 0;
    }

    /// <summary>
    /// ADR 0124 — procedural facade and roof detail for the surveyed Adelaide footprints: roof
    /// parapets, storey window bands (some lit at night, some dark), hangar doors with seams and
    /// a roof monitor, fire-station appliance bays, a glazed control-tower cab with a mast and
    /// obstruction light, rooftop plant, and the terminal's landside canopy. Pure and
    /// deterministic (seeded from the OSM id) — no UnityEngine types — so it is tested headlessly
    /// and the runtime only merges the pieces into one mesh per material.
    /// </summary>
    public static partial class BuildingDetail
    {
        public const float StoreyMetres = 3.4f;
        public const float ParapetDepthMetres = 0.32f;
        public const float WindowBandHeightMetres = 1.35f;
        public const float WindowPaneMetres = 3.0f;
        public const float MullionWidthMetres = 0.14f;
        public const float TowerShaftFraction = 0.78f;
        public const float TowerShaftScale = 0.62f;
        public const float TowerMastMetres = 5.5f;

        public static float ParapetHeight(AdelaideBuildingKind kind) => kind switch
        {
            AdelaideBuildingKind.Hangar => 0.45f,
            AdelaideBuildingKind.Freight => 0.7f,
            _ => 0.65f
        };

        /// <summary>Everything for one operational building other than the tower's shaft/cab.</summary>
        public static BuildingDetailSet For(AdelaideBuilding building, float baseY)
        {
            var set = new BuildingDetailSet();
            if (building.Xz == null || building.Xz.Length < 6)
                return set;
            var xz = building.Xz;
            var height = building.HeightMetres;
            var random = new Hash(building.Id);
            switch (building.Kind)
            {
                case AdelaideBuildingKind.ControlTower:
                    AddTower(set, xz, baseY, height);
                    return set;
                case AdelaideBuildingKind.CarPark:
                    AddParapet(set, xz, baseY + height, 1.0f);
                    return set;
            }

            AddParapet(set, xz, baseY + height, ParapetHeight(building.Kind));
            var front = FrontEdge(xz);
            switch (building.Kind)
            {
                case AdelaideBuildingKind.Hangar:
                    // Open and hollow where the footprint allows (ADR 0213), else the flat door it always had.
                    if (!TryAddOpenHangar(set, xz, baseY, height, random))
                        AddHangarDoor(set, xz, front, baseY, height);
                    AddHangarRoof(set, xz, baseY + height, building.Id, 1f);
                    AddWindowBands(set, xz, baseY, 1, random, skipEdge: front);
                    AddPlinth(set, xz, baseY);
                    AddCladding(set, xz, baseY, height, front, random);
                    break;
                case AdelaideBuildingKind.FireStation:
                    AddApplianceBays(set, xz, front, baseY, height, random);
                    AddWindowBands(set, xz, baseY, Storeys(height), random, skipEdge: front);
                    AddPlinth(set, xz, baseY);
                    // A low-pitched roof cap; plant only when the footprint cannot carry one.
                    if (!AddHangarRoof(set, xz, baseY + height, building.Id, RoofRiseScale))
                        AddRoofPlant(set, xz, baseY + height, random);
                    break;
                case AdelaideBuildingKind.Freight:
                    AddLoadingDoors(set, xz, front, baseY, height, random);
                    AddWindowBands(set, xz, baseY, 1, random, skipEdge: front);
                    AddPlinth(set, xz, baseY);
                    AddCladding(set, xz, baseY, height, front, random);
                    if (!AddHangarRoof(set, xz, baseY + height, building.Id, RoofRiseScale))
                        AddRoofPlant(set, xz, baseY + height, random);
                    break;
                default:
                    AddWindowBands(set, xz, baseY, Storeys(height), random, skipEdge: -1);
                    AddPlinth(set, xz, baseY);
                    AddRoofPlant(set, xz, baseY + height, random);
                    break;
            }

            return set;
        }

        /// <summary>
        /// Terminal shell detail: parapet, rooftop plant, and on the landside (+z) facades a
        /// fascia band, office windows and a kerb canopy on columns. The airside curtain wall
        /// has its own 28-bay glazing (<see cref="AdelaideTerminalArchitecture"/>).
        /// </summary>
        public static BuildingDetailSet ForTerminal(string id, float[] xz, float baseY, float height, bool rfds)
        {
            var set = new BuildingDetailSet();
            if (xz == null || xz.Length < 6)
                return set;
            var random = new Hash(id);
            AddParapet(set, xz, baseY + height, rfds ? 0.45f : 0.85f);
            if (rfds)
            {
                AddHangarDoor(set, xz, FrontEdge(xz), baseY, height);
                return set;
            }

            var count = xz.Length / 2;
            var winding = Winding(xz);
            for (var i = 0; i < count; i++)
            {
                var edge = Edge(xz, i, winding);
                // Landside only: the airside wall faces -z and carries the curtain glazing.
                if (edge.Length < 20f || edge.OutZ < 0.7f)
                    continue;
                // Fascia band along the roof line.
                set.Boxes.Add(OnWall(BuildingPart.Trim, edge, 0.5f, edge.Length, baseY + height - 1.1f, 1.6f, 0.35f));
                // Office windows on the upper level.
                AddPanes(set, edge, baseY + 8.2f, random, margin: 3f);
                AddFacadeFins(set, edge, baseY, height);
                // Kerb canopy: a 6 m deep slab at 4.6 m on slim columns every 9 m.
                var canopyLength = edge.Length - 6f;
                if (canopyLength < 12f)
                    continue;
                AddEntranceBanks(set, edge, baseY, canopyLength);
                AddFacadeJoints(set, edge, baseY, height);
                set.Boxes.Add(OnWall(BuildingPart.Canopy, edge, 0.5f, canopyLength, baseY + 4.9f, 0.45f, 6.2f,
                    outward: 3.1f));
                set.Boxes.Add(OnWall(BuildingPart.Trim, edge, 0.5f, canopyLength, baseY + 5.25f, 0.3f, 0.3f,
                    outward: 6.1f));
                var columns = (int)(canopyLength / 9f);
                for (var c = 0; c <= columns; c++)
                {
                    var t = 0.5f + (c / (float)Math.Max(1, columns) - 0.5f) * (canopyLength - 1f) / edge.Length;
                    set.Boxes.Add(OnWall(BuildingPart.Canopy, edge, t, 0.35f, baseY + 2.35f, 4.7f, 0.35f, outward: 5.6f));
                }
            }

            AddRoofPlant(set, xz, baseY + height, random, maxUnits: 16,
                keepClear: AdelaideTerminalArchitecture.RoofDetails());
            AddAirsideDoors(set, baseY);
            return set;
        }

        // ---- Terminal doors ---------------------------------------------------------------

        /// <summary>Glass leaf width of one automatic sliding door, and its clear height.</summary>
        public const float SlidingLeafMetres = 1.2f;
        public const float SlidingDoorHeightMetres = 2.4f;
        /// <summary>Spacing of kerb-side entrance banks; each sits midway between two canopy columns.</summary>
        public const float EntrancePitchMetres = 45f;
        /// <summary>Apron-level staff/service door pitch along the airside wall.</summary>
        public const float ServiceDoorPitchMetres = 44f;
        /// <summary>Departures-level floor height where gate-lounge doors open onto the bridge rotundas.</summary>
        public const float GateDoorSillMetres = AdelaideAerobridges.RotundaFloorMetres;

        /// <summary>
        /// Landside entrance banks: a four-leaf automatic sliding door with glazed sidelights,
        /// a header sign fascia and kerb bollards, repeated along each landside facade under the
        /// canopy. Ground (arrivals) level; the departures level keeps its office glazing above.
        /// </summary>
        private static void AddEntranceBanks(BuildingDetailSet set, WallEdge edge, float baseY, float canopyLength)
        {
            var banks = Math.Max(1, (int)(canopyLength / EntrancePitchMetres));
            var pitch = canopyLength / banks;
            for (var b = 0; b < banks; b++)
            {
                var along = (edge.Length - canopyLength) * 0.5f + pitch * (b + 0.5f);
                var t = along / edge.Length;
                const float bankWidth = SlidingLeafMetres * 4f;
                // Frame: two jambs, a head, and a sill track proud of the wall.
                set.Boxes.Add(OnWall(BuildingPart.Trim, edge, t, bankWidth + 3.2f, baseY + SlidingDoorHeightMetres + 0.35f,
                    0.5f, 0.35f, outward: 0.2f));
                set.Boxes.Add(OnWall(BuildingPart.Trim, edge, t, bankWidth + 3.2f, baseY + 0.05f, 0.1f, 0.5f, outward: 0.25f));
                // Two centre-parting sliding pairs (Door) between fixed sidelights (WindowLit).
                for (var leaf = 0; leaf < 4; leaf++)
                {
                    var lt = t + (leaf - 1.5f) * SlidingLeafMetres / edge.Length;
                    set.Boxes.Add(OnWall(BuildingPart.Door, edge, lt, SlidingLeafMetres - 0.06f,
                        baseY + SlidingDoorHeightMetres * 0.5f, SlidingDoorHeightMetres, 0.06f, outward: 0.1f));
                }

                for (var side = -1; side <= 1; side += 2)
                {
                    var st = t + side * (bankWidth * 0.5f + 0.8f) / edge.Length;
                    set.Boxes.Add(OnWall(BuildingPart.WindowLit, edge, st, 1.4f, baseY + SlidingDoorHeightMetres * 0.5f,
                        SlidingDoorHeightMetres, 0.06f, outward: 0.1f));
                    set.Boxes.Add(OnWall(BuildingPart.Trim, edge, st + side * 0.8f / edge.Length, 0.16f,
                        baseY + SlidingDoorHeightMetres * 0.5f + 0.2f, SlidingDoorHeightMetres + 0.4f, 0.3f, outward: 0.16f));
                    // Kerb bollard either side of the bank.
                    set.Boxes.Add(OnWall(BuildingPart.Trim, edge, t + side * (bankWidth * 0.5f + 2.6f) / edge.Length, 0.24f,
                        baseY + 0.5f, 1.0f, 0.24f, outward: 1.4f));
                }

                // Lit sign fascia over the doors ("ARRIVALS" plate — no text baked into the mesh).
                set.Boxes.Add(OnWall(BuildingPart.WindowLit, edge, t, bankWidth * 0.8f, baseY + SlidingDoorHeightMetres + 0.85f,
                    0.4f, 0.1f, outward: 0.3f));
            }
        }

        /// <summary>Vertical expansion joints and downpipes every ~30 m so long landside walls do not read as one slab.</summary>
        private static void AddFacadeJoints(BuildingDetailSet set, WallEdge edge, float baseY, float height)
        {
            var joints = (int)(edge.Length / 30f);
            for (var j = 1; j < joints; j++)
            {
                var t = j / (float)joints;
                set.Boxes.Add(OnWall(BuildingPart.Trim, edge, t, 0.22f, baseY + height * 0.5f, height, 0.14f, outward: 0.07f));
                set.Boxes.Add(OnWall(BuildingPart.Trim, edge, t + 0.6f / edge.Length, 0.14f, baseY + height * 0.5f - 0.4f,
                    height - 0.8f, 0.14f, outward: 0.14f));
            }
        }

        /// <summary>
        /// Airside doors on the real curved wall. Every aerobridge rotunda gets its gate-lounge
        /// door at the departures-level floor (frame, two glass leaves, hold-room sign plate and a
        /// gate-number board, the number bar count matching the gate). Between them run apron-level
        /// staff doors, each with a frame, a step and a lit call plate. The baggage undercroft
        /// portal is kept clear.
        /// </summary>
        private static void AddAirsideDoors(BuildingDetailSet set, float baseY)
        {
            foreach (var site in AdelaideAerobridges.Sites)
            {
                var wallZ = AdelaideTerminalArchitecture.AirsideWallZAt(site.RotundaX);
                var z = wallZ - 0.32f;
                var cy = baseY + GateDoorSillMetres + SlidingDoorHeightMetres * 0.5f;
                set.Boxes.Add(new DetailBox(BuildingPart.Trim, site.RotundaX, cy + 0.3f, z, 3.4f, SlidingDoorHeightMetres + 0.6f,
                    0.3f, 1f, 0f));
                set.Boxes.Add(new DetailBox(BuildingPart.Door, site.RotundaX - 0.6f, cy, z - 0.1f, 1.1f,
                    SlidingDoorHeightMetres, 0.06f, 1f, 0f));
                set.Boxes.Add(new DetailBox(BuildingPart.Door, site.RotundaX + 0.6f, cy, z - 0.1f, 1.1f,
                    SlidingDoorHeightMetres, 0.06f, 1f, 0f));
                // Gate-number board above the door and a lounge sign plate.
                set.Boxes.Add(new DetailBox(BuildingPart.Trim, site.RotundaX, cy + SlidingDoorHeightMetres * 0.5f + 0.9f, z - 0.1f,
                    2.2f, 0.6f, 0.12f, 1f, 0f));
                set.Boxes.Add(new DetailBox(BuildingPart.Trim, site.RotundaX, baseY + GateDoorSillMetres - 0.15f, z - 0.2f,
                    3.4f, 0.3f, 0.6f, 1f, 0f));
            }

            var portalMin = AdelaideTerminalArchitecture.UndercroftPortalCentreX
                - AdelaideTerminalArchitecture.UndercroftPortalHalfWidthMetres - 3f;
            var portalMax = portalMin + AdelaideTerminalArchitecture.UndercroftPortalHalfWidthMetres * 2f + 6f;
            var start = AdelaideTerminalArchitecture.GlazingStartXMetres + ServiceDoorPitchMetres * 0.5f;
            var end = AdelaideTerminalArchitecture.GlazingStartXMetres
                + AdelaideTerminalArchitecture.GlazingBayCount * AdelaideTerminalArchitecture.GlazingBayPitchMetres;
            for (var x = start; x < end; x += ServiceDoorPitchMetres)
            {
                if (x > portalMin && x < portalMax)
                    continue;
                var z = AdelaideTerminalArchitecture.AirsideWallZAt(x) - 0.3f;
                set.Boxes.Add(new DetailBox(BuildingPart.Trim, x, baseY + 1.25f, z, 1.5f, 2.5f, 0.3f, 1f, 0f));
                set.Boxes.Add(new DetailBox(BuildingPart.Door, x, baseY + 1.1f, z - 0.12f, 1.1f, 2.2f, 0.08f, 1f, 0f));
                set.Boxes.Add(new DetailBox(BuildingPart.Trim, x, baseY + 0.08f, z - 0.4f, 1.8f, 0.16f, 0.8f, 1f, 0f));
                set.Boxes.Add(new DetailBox(BuildingPart.Trim, x + 1.1f, baseY + 1.9f, z - 0.14f, 0.3f, 0.2f, 0.1f, 1f, 0f));
            }
        }

        // ---- Pieces ---------------------------------------------------------------------

        private static void AddParapet(BuildingDetailSet set, float[] xz, float roofY, float parapet)
        {
            var count = xz.Length / 2;
            var winding = Winding(xz);
            for (var i = 0; i < count; i++)
            {
                var edge = Edge(xz, i, winding);
                if (edge.Length < 0.4f)
                    continue;
                // Overlap half a depth at each end so convex corners close.
                set.Boxes.Add(OnWall(BuildingPart.Shell, edge, 0.5f, edge.Length + ParapetDepthMetres,
                    roofY + parapet * 0.5f, parapet, ParapetDepthMetres, outward: -ParapetDepthMetres * 0.5f));
            }
        }

        private static void AddWindowBands(BuildingDetailSet set, float[] xz, float baseY, int storeys, Hash random,
            int skipEdge)
        {
            var count = xz.Length / 2;
            var winding = Winding(xz);
            for (var i = 0; i < count; i++)
            {
                if (i == skipEdge)
                    continue;
                var edge = Edge(xz, i, winding);
                if (edge.Length < 6f)
                    continue;
                for (var s = 0; s < storeys; s++)
                    AddPanes(set, edge, baseY + 1.0f + s * StoreyMetres + WindowBandHeightMetres * 0.5f + 0.35f, random,
                        margin: Math.Max(1.2f, edge.Length * 0.08f));
            }
        }

        /// <summary>A band of panes between mullions, each lit or dark at night.</summary>
        private static void AddPanes(BuildingDetailSet set, WallEdge edge, float centreY, Hash random, float margin)
        {
            var band = edge.Length - margin * 2f;
            if (band < 2f)
                return;
            var panes = Math.Max(1, (int)Math.Round(band / WindowPaneMetres));
            var pane = band / panes;
            for (var p = 0; p < panes; p++)
            {
                var t = (margin + pane * (p + 0.5f)) / edge.Length;
                var lit = random.Next() < 0.7f;
                set.Boxes.Add(OnWall(lit ? BuildingPart.WindowLit : BuildingPart.WindowDark, edge, t,
                    pane - MullionWidthMetres, centreY, WindowBandHeightMetres, 0.1f, outward: 0.04f));
            }

            // Frame: head and sill, plus a mullion at every pane joint.
            for (var p = 0; p <= panes; p++)
            {
                var t = (margin + pane * p) / edge.Length;
                set.Boxes.Add(OnWall(BuildingPart.Trim, edge, t, MullionWidthMetres, centreY,
                    WindowBandHeightMetres + 0.2f, 0.16f, outward: 0.07f));
            }

            set.Boxes.Add(OnWall(BuildingPart.Trim, edge, 0.5f, band + 0.2f, centreY + WindowBandHeightMetres * 0.5f + 0.06f,
                0.12f, 0.2f, outward: 0.08f));
            set.Boxes.Add(OnWall(BuildingPart.Trim, edge, 0.5f, band + 0.3f, centreY - WindowBandHeightMetres * 0.5f - 0.07f,
                0.14f, 0.3f, outward: 0.12f));
        }

        private static void AddHangarDoor(BuildingDetailSet set, float[] xz, int front, float baseY, float height)
        {
            if (front < 0)
                return;
            var edge = Edge(xz, front, Winding(xz));
            var width = Math.Min(edge.Length * 0.82f, 64f);
            var doorHeight = Math.Min(height * 0.8f, height - 1.4f);
            if (width < 6f || doorHeight < 2.5f)
                return;
            set.Boxes.Add(OnWall(BuildingPart.Door, edge, 0.5f, width, baseY + doorHeight * 0.5f, doorHeight, 0.16f,
                outward: 0.08f));
            // Leaf seams every ~6 m, a header beam and a floor track.
            var leaves = Math.Max(2, (int)Math.Round(width / 6f));
            for (var l = 1; l < leaves; l++)
            {
                var t = 0.5f + (l / (float)leaves - 0.5f) * width / edge.Length;
                set.Boxes.Add(OnWall(BuildingPart.Trim, edge, t, 0.22f, baseY + doorHeight * 0.5f, doorHeight, 0.3f,
                    outward: 0.15f));
            }

            set.Boxes.Add(OnWall(BuildingPart.Trim, edge, 0.5f, width + 1.2f, baseY + doorHeight + 0.4f, 0.8f, 0.5f,
                outward: 0.25f));
            set.Boxes.Add(OnWall(BuildingPart.Trim, edge, 0.5f, width + 1.2f, baseY + 0.06f, 0.12f, 0.8f, outward: 0.4f));
            for (var side = -1; side <= 1; side += 2)
                AddWallPack(set, edge, 0.5f + side * (width * 0.5f + 0.9f) / edge.Length, doorHeight, baseY);
        }

        /// <summary>
        /// Give every sufficiently regular hangar a recognisable roof rather than another flat
        /// OSM prism. The profile is stable per building, while the cap is progressively inset
        /// until all four corners are supported by the surveyed footprint.
        /// </summary>
        private static bool AddHangarRoof(BuildingDetailSet set, float[] xz, float roofY, string id, float riseScale)
        {
            var box = OrientedBounds(xz);
            var length = box.Length * 0.94f;
            var width = box.Width * 0.90f;
            for (var attempt = 0; attempt < 8 && !BoxInside(xz, box.X, box.Z, box.DirX, box.DirZ, length, width, 0.2f); attempt++)
            {
                length *= 0.92f;
                width *= 0.90f;
            }
            var roofX = box.X;
            var roofZ = box.Z;
            var roofDirX = box.DirX;
            var roofDirZ = box.DirZ;
            if (length < 8f || width < 5f
                || !BoxInside(xz, roofX, roofZ, roofDirX, roofDirZ, length, width, 0.2f))
            {
                // Concave/L-shaped sheds cannot support a rectangle centred on their overall
                // bounds. Fit the roof to the longest facade wing instead: its midpoint moved
                // inward by half the roof width is guaranteed to describe the occupied arm.
                var edge = Edge(xz, FrontEdge(xz), Winding(xz));
                length = edge.Length * 0.78f;
                width = Math.Min(8f, Math.Max(5f, box.Width * 0.22f));
                roofDirX = edge.DirX;
                roofDirZ = edge.DirZ;
                roofX = (edge.AX + edge.BX) * 0.5f - edge.OutX * (width * 0.58f);
                roofZ = (edge.AZ + edge.BZ) * 0.5f - edge.OutZ * (width * 0.58f);
                for (var attempt = 0; attempt < 8
                     && !BoxInside(xz, roofX, roofZ, roofDirX, roofDirZ, length, width, 0.1f); attempt++)
                {
                    length *= 0.90f;
                    width *= 0.88f;
                    roofX += -edge.OutX * 0.25f;
                    roofZ += -edge.OutZ * 0.25f;
                }
                if (length < 6f || width < 3.2f
                    || !BoxInside(xz, roofX, roofZ, roofDirX, roofDirZ, length, width, 0.1f))
                    return false;
            }

            var selector = StableSelector(id);
            var profile = (selector % 3) switch
            {
                0 => HangarRoofProfile.Gable,
                1 => HangarRoofProfile.Barrel,
                _ => HangarRoofProfile.Sawtooth
            };
            var rise = riseScale * (profile switch
            {
                HangarRoofProfile.Gable => Math.Min(4.2f, width * 0.18f),
                HangarRoofProfile.Barrel => Math.Min(3.4f, width * 0.15f),
                _ => Math.Min(2.8f, width * 0.12f)
            });
            set.Roofs.Add(new DetailRoof(profile, roofX, roofZ, length, width, roofY + 0.08f, rise,
                roofDirX, roofDirZ));

            // A dark ridge/clerestory keeps the roof readable at overview distance and breaks
            // up the otherwise long unlit sheets. Sawtooth roofs get repeated north-light bands.
            if (profile == HangarRoofProfile.Sawtooth)
            {
                for (var i = -2; i <= 2; i++)
                {
                    var across = i * width / 5f;
                    set.Boxes.Add(new DetailBox(BuildingPart.WindowDark,
                        roofX - roofDirZ * across, roofY + rise * 0.62f, roofZ + roofDirX * across,
                        length * 0.94f, rise * 0.55f, 0.18f, roofDirX, roofDirZ));
                }
            }
            else
            {
                set.Boxes.Add(new DetailBox(BuildingPart.WindowDark, roofX, roofY + rise * 0.72f, roofZ,
                    length * 0.82f, Math.Max(0.55f, rise * 0.32f), 0.24f, roofDirX, roofDirZ));
            }
            return true;
        }

        /// <summary>Freight sheds and the fire station take a shallower roof than a hangar.</summary>
        public const float RoofRiseScale = 0.55f;

        /// <summary>A low concrete plinth round the base of a wall so blocks do not meet the apron in a hard line.</summary>
        private static void AddPlinth(BuildingDetailSet set, float[] xz, float baseY)
        {
            var count = xz.Length / 2;
            var winding = Winding(xz);
            for (var i = 0; i < count; i++)
            {
                var edge = Edge(xz, i, winding);
                if (edge.Length < 3f)
                    continue;
                set.Boxes.Add(OnWall(BuildingPart.Trim, edge, 0.5f, edge.Length, baseY + 0.25f, 0.5f, 0.14f, outward: 0.05f));
            }
        }

        /// <summary>A lit wall pack over a door, so doors read at night as well as by day.</summary>
        private static void AddWallPack(BuildingDetailSet set, WallEdge edge, float t, float doorTop, float baseY)
        {
            set.Boxes.Add(OnWall(BuildingPart.WindowLit, edge, t, 0.55f, baseY + doorTop + 1.15f, 0.22f, 0.2f, outward: 0.3f));
        }

        private static int StableSelector(string value)
        {
            unchecked
            {
                var hash = 17;
                foreach (var c in value ?? string.Empty)
                    hash = hash * 31 + c;
                return hash & int.MaxValue;
            }
        }

        private static void AddRoofPlant(BuildingDetailSet set, float[] xz, float roofY, Hash random, int maxUnits = 14,
            AdelaideTerminalDetail[] keepClear = null)
        {
            var area = Math.Abs(SignedArea(xz));
            var wanted = Math.Min(maxUnits, (int)(area / 400f));
            // Big sheds carry big chillers; a 3 m unit vanishes on a 150 m roof.
            var size = area > 3000f ? 1.8f : 1f;
            if (wanted <= 0)
                return;
            var box = OrientedBounds(xz);
            var placed = 0;
            for (var attempt = 0; attempt < wanted * 8 && placed < wanted; attempt++)
            {
                var along = (random.Next() - 0.5f) * box.Length * 0.8f;
                var across = (random.Next() - 0.5f) * box.Width * 0.8f;
                var x = box.X + box.DirX * along - box.DirZ * across;
                var z = box.Z + box.DirZ * along + box.DirX * across;
                var length = (2.4f + random.Next() * 2.8f) * size;
                var depth = (1.6f + random.Next() * 1.6f) * size;
                var tall = 1.1f + random.Next() * 1.1f * size;
                if (!BoxInside(xz, x, z, box.DirX, box.DirZ, length, depth, 1.5f))
                    continue;
                var overlaps = false;
                foreach (var other in set.Boxes)
                {
                    if (other.Part != BuildingPart.Plant)
                        continue;
                    var dx = other.X - x;
                    var dz = other.Z - z;
                    if (dx * dx + dz * dz < 36f * size * size)
                        overlaps = true;
                }

                if (keepClear != null)
                    foreach (var other in keepClear)
                        if (Math.Abs(other.X - x) < other.Width * 0.5f + length && Math.Abs(other.Z - z) < other.Depth * 0.5f + length)
                            overlaps = true;
                if (overlaps)
                    continue;
                set.Boxes.Add(new DetailBox(BuildingPart.Plant, x, roofY + tall * 0.5f, z, length, tall, depth, box.DirX, box.DirZ));
                // A fan or vent cap on top.
                set.Boxes.Add(new DetailBox(BuildingPart.Trim, x, roofY + tall + 0.12f, z, Math.Min(1.4f, length * 0.4f), 0.24f,
                    Math.Min(1.4f, depth * 0.6f), box.DirX, box.DirZ));
                placed++;
            }
        }

        /// <summary>
        /// Tower: tapered shaft to 78 % of its height, a cab floor, an inset glass ring with a
        /// mullion at every corner, an overhanging roof, a plant cap and a mast with a red light.
        /// </summary>
        private static void AddTower(BuildingDetailSet set, float[] xz, float baseY, float height)
        {
            var shaftTop = baseY + height * TowerShaftFraction;
            set.Prisms.Add(new DetailPrism(BuildingPart.Shell, Scale(xz, TowerShaftScale), baseY, shaftTop - baseY));
            var floor = 1.2f;
            var cabTop = baseY + height - 3.5f;
            set.Prisms.Add(new DetailPrism(BuildingPart.Shell, Scale(xz, 1.0f), shaftTop, floor));
            var glass = Scale(xz, 0.95f);
            var glassBase = shaftTop + floor;
            var glassHeight = cabTop - glassBase;
            set.Prisms.Add(new DetailPrism(BuildingPart.CabGlass, glass, glassBase, glassHeight));
            var count = glass.Length / 2;
            for (var i = 0; i < count; i++)
                set.Boxes.Add(new DetailBox(BuildingPart.Trim, glass[i * 2], glassBase + glassHeight * 0.5f, glass[i * 2 + 1],
                    0.28f, glassHeight, 0.28f, 1f, 0f));
            set.Prisms.Add(new DetailPrism(BuildingPart.Shell, Scale(xz, 1.1f), cabTop, 0.8f));
            set.Prisms.Add(new DetailPrism(BuildingPart.Trim, Scale(xz, 0.5f), cabTop + 0.8f, 1.6f));
            var (cx, cz) = Centroid(xz);
            var mastBase = cabTop + 2.4f;
            set.Boxes.Add(new DetailBox(BuildingPart.Trim, cx, mastBase + TowerMastMetres * 0.5f, cz, 0.22f, TowerMastMetres, 0.22f, 1f, 0f));
            set.Boxes.Add(new DetailBox(BuildingPart.ObstructionLight, cx, mastBase + TowerMastMetres + 0.2f, cz, 0.45f, 0.4f, 0.45f, 1f, 0f));
            // A radar/antenna cross-arm.
            set.Boxes.Add(new DetailBox(BuildingPart.Trim, cx, mastBase + TowerMastMetres * 0.6f, cz, 2.4f, 0.12f, 0.12f, 1f, 0f));
        }

        private static int Storeys(float height) => Math.Max(1, (int)Math.Round(height / StoreyMetres));

        // ---- Geometry helpers -----------------------------------------------------------

        public readonly struct WallEdge
        {
            public WallEdge(float ax, float az, float bx, float bz, float winding)
            {
                AX = ax;
                AZ = az;
                BX = bx;
                BZ = bz;
                var dx = bx - ax;
                var dz = bz - az;
                Length = (float)Math.Sqrt(dx * dx + dz * dz);
                DirX = Length > 1e-5f ? dx / Length : 1f;
                DirZ = Length > 1e-5f ? dz / Length : 0f;
                OutX = DirZ * winding;
                OutZ = -DirX * winding;
            }

            public float AX { get; }
            public float AZ { get; }
            public float BX { get; }
            public float BZ { get; }
            public float Length { get; }
            public float DirX { get; }
            public float DirZ { get; }
            public float OutX { get; }
            public float OutZ { get; }
        }

        public static WallEdge Edge(float[] xz, int i, float winding)
        {
            var count = xz.Length / 2;
            var j = (i + 1) % count;
            return new WallEdge(xz[i * 2], xz[i * 2 + 1], xz[j * 2], xz[j * 2 + 1], winding);
        }

        /// <summary>+1 or -1 so that (dirZ, -dirX) * winding points out of the footprint.</summary>
        public static float Winding(float[] xz) => SignedArea(xz) >= 0f ? 1f : -1f;

        public static float SignedArea(float[] xz)
        {
            var count = xz.Length / 2;
            var area = 0f;
            for (var i = 0; i < count; i++)
            {
                var j = (i + 1) % count;
                area += xz[i * 2] * xz[j * 2 + 1] - xz[j * 2] * xz[i * 2 + 1];
            }

            return area * 0.5f;
        }

        /// <summary>A box on a wall at fraction <paramref name="t"/> along it, pushed out by <paramref name="outward"/>.</summary>
        private static DetailBox OnWall(BuildingPart part, WallEdge edge, float t, float length, float centreY, float height,
            float depth, float outward = 0f)
        {
            var x = edge.AX + (edge.BX - edge.AX) * t + edge.OutX * outward;
            var z = edge.AZ + (edge.BZ - edge.AZ) * t + edge.OutZ * outward;
            return new DetailBox(part, x, centreY, z, length, height, depth, edge.DirX, edge.DirZ);
        }

        /// <summary>
        /// The edge the doors go on: the longest wall that faces the field centre (the runways
        /// sit around the world origin), else the longest wall.
        /// </summary>
        public static int FrontEdge(float[] xz) => HangarFront.FrontEdge(xz);

        public static (float x, float z) Centroid(float[] xz)
        {
            var count = xz.Length / 2;
            var x = 0f;
            var z = 0f;
            for (var i = 0; i < count; i++)
            {
                x += xz[i * 2];
                z += xz[i * 2 + 1];
            }

            return (x / count, z / count);
        }

        public static float[] Scale(float[] xz, float scale)
        {
            var (cx, cz) = Centroid(xz);
            var result = new float[xz.Length];
            for (var i = 0; i < xz.Length / 2; i++)
            {
                result[i * 2] = cx + (xz[i * 2] - cx) * scale;
                result[i * 2 + 1] = cz + (xz[i * 2 + 1] - cz) * scale;
            }

            return result;
        }

        public static bool Contains(float[] xz, float x, float z)
        {
            var count = xz.Length / 2;
            var inside = false;
            for (int i = 0, j = count - 1; i < count; j = i++)
            {
                var xi = xz[i * 2];
                var zi = xz[i * 2 + 1];
                var xj = xz[j * 2];
                var zj = xz[j * 2 + 1];
                if ((zi > z) != (zj > z) && x < (xj - xi) * (z - zi) / (zj - zi) + xi)
                    inside = !inside;
            }

            return inside;
        }

        private static bool BoxInside(float[] xz, float x, float z, float dirX, float dirZ, float length, float depth,
            float margin)
        {
            var hl = length * 0.5f + margin;
            var hd = depth * 0.5f + margin;
            for (var sa = -1; sa <= 1; sa++)
            for (var sd = -1; sd <= 1; sd++)
            {
                var px = x + dirX * hl * sa - dirZ * hd * sd;
                var pz = z + dirZ * hl * sa + dirX * hd * sd;
                if (!Contains(xz, px, pz))
                    return false;
            }

            return true;
        }

        private readonly struct Bounds
        {
            public Bounds(float x, float z, float dirX, float dirZ, float length, float width)
            {
                X = x;
                Z = z;
                DirX = dirX;
                DirZ = dirZ;
                Length = length;
                Width = width;
            }

            public float X { get; }
            public float Z { get; }
            public float DirX { get; }
            public float DirZ { get; }
            public float Length { get; }
            public float Width { get; }
        }

        /// <summary>Bounds aligned to the footprint's longest wall.</summary>
        private static Bounds OrientedBounds(float[] xz)
        {
            var count = xz.Length / 2;
            var winding = Winding(xz);
            var dirX = 1f;
            var dirZ = 0f;
            var longest = 0f;
            for (var i = 0; i < count; i++)
            {
                var edge = Edge(xz, i, winding);
                if (edge.Length <= longest)
                    continue;
                longest = edge.Length;
                dirX = edge.DirX;
                dirZ = edge.DirZ;
            }

            float minA = float.MaxValue, maxA = float.MinValue, minP = float.MaxValue, maxP = float.MinValue;
            for (var i = 0; i < count; i++)
            {
                var a = xz[i * 2] * dirX + xz[i * 2 + 1] * dirZ;
                var p = -xz[i * 2] * dirZ + xz[i * 2 + 1] * dirX;
                minA = Math.Min(minA, a);
                maxA = Math.Max(maxA, a);
                minP = Math.Min(minP, p);
                maxP = Math.Max(maxP, p);
            }

            var ca = (minA + maxA) * 0.5f;
            var cp = (minP + maxP) * 0.5f;
            return new Bounds(ca * dirX - cp * dirZ, ca * dirZ + cp * dirX, dirX, dirZ, maxA - minA, maxP - minP);
        }

        /// <summary>FNV-seeded xorshift so each building's plant and lit panes never change between runs.</summary>
        private sealed class Hash
        {
            private uint _state;

            public Hash(string seed)
            {
                var h = 2166136261u;
                foreach (var c in seed ?? string.Empty)
                    h = (h ^ c) * 16777619u;
                _state = h == 0 ? 0x9E3779B9u : h;
            }

            public float Next()
            {
                _state ^= _state << 13;
                _state ^= _state >> 17;
                _state ^= _state << 5;
                return (_state & 0xFFFFFF) / 16777216f;
            }
        }
    }
}

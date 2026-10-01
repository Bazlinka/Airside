using System;
using System.Collections.Generic;
using Airside.Simulation;

namespace Airside.Presentation
{
    /// <summary>
    /// ADR 0213 — hollow hangars, appliance bays and loading docks. The shell has a real doorway cut through its front
    /// wall (<see cref="DetailOpening"/>), the runtime lines the inside (floor, walls, ceiling), and what is kept inside
    /// is placed here: tugs, power carts, stairs and tool chests in a hangar; a fire appliance in each open bay;
    /// pallets and a van at a dock. Everything is deterministic from the building id, pure maths, and kept clear of the
    /// strip down the middle that a towed aircraft uses (<see cref="HangarTow"/> leaves 2.5 m beside its wing tips).
    /// </summary>
    public static partial class BuildingDetail
    {
        /// <summary>Equipment sits within this distance of a hangar side wall, inside the aircraft's wing clearance.</summary>
        public const float EquipmentBandMetres = 2.3f;

        /// <summary>A raised loading dock's floor above the apron.</summary>
        public const float DockFloorMetres = 1.15f;

        // ---- Open hangar ------------------------------------------------------------------------

        private static bool TryAddOpenHangar(BuildingDetailSet set, float[] xz, float baseY, float height, Hash random)
        {
            if (!HangarFront.TryFor(xz, height, out var front))
                return false;
            var edge = Edge(xz, front.EdgeIndex, Winding(xz));
            var from = (edge.Length - front.DoorWidth) * 0.5f;
            var to = from + front.DoorWidth;
            set.Openings.Add(new DetailOpening(front.EdgeIndex, from, to, 0f, front.DoorHeight));
            AddOpeningFrame(set, edge, from, to, 0f, front.DoorHeight, baseY, track: true);
            AddStackedLeaves(set, edge, from, to, front.DoorHeight, baseY);
            AddHangarTrusses(set, xz, baseY, height);
            AddCeilingLights(set, xz, baseY, height, random);
            AddHangarEquipment(set, xz, front.EdgeIndex, baseY, random);
            for (var side = -1; side <= 1; side += 2)
                AddWallPack(set, edge, 0.5f + side * (front.DoorWidth * 0.5f + 0.9f) / edge.Length, front.DoorHeight - 2f, baseY);
            return true;
        }

        /// <summary>Jambs, a header beam and (for a hangar) a floor track round a doorway.</summary>
        private static void AddOpeningFrame(BuildingDetailSet set, WallEdge edge, float from, float to, float bottom,
            float top, float baseY, bool track)
        {
            var width = to - from;
            var mid = (from + to) * 0.5f / edge.Length;
            set.Boxes.Add(OnWall(BuildingPart.Trim, edge, mid, width + 1.2f, baseY + top + 0.4f, 0.8f, 0.5f, outward: 0.25f));
            for (var side = -1; side <= 1; side += 2)
            {
                var t = (side < 0 ? from - 0.3f : to + 0.3f) / edge.Length;
                set.Boxes.Add(OnWall(BuildingPart.Trim, edge, t, 0.6f, baseY + (bottom + top) * 0.5f, top - bottom, 0.5f,
                    outward: 0.25f));
            }

            if (track)
                set.Boxes.Add(OnWall(BuildingPart.Trim, edge, mid, width + 1.2f, baseY + bottom + 0.06f, 0.12f, 0.8f,
                    outward: 0.4f));
        }

        /// <summary>The sliding door leaves, bunched in two stacks on the wall either side of the opening.</summary>
        private static void AddStackedLeaves(BuildingDetailSet set, WallEdge edge, float from, float to, float doorHeight,
            float baseY)
        {
            for (var side = -1; side <= 1; side += 2)
            {
                var region = side < 0 ? from : edge.Length - to;
                if (region < 3f)
                    continue;
                var leafWidth = Math.Min(6f, (region - 0.5f) * 0.5f);
                for (var leaf = 0; leaf < 2; leaf++)
                {
                    var along = side < 0
                        ? 0.25f + leafWidth * (leaf + 0.5f)
                        : edge.Length - 0.25f - leafWidth * (leaf + 0.5f);
                    // The second leaf rides on an outer rail so the stack reads as two thicknesses.
                    set.Boxes.Add(OnWall(BuildingPart.Door, edge, along / edge.Length, leafWidth - 0.06f,
                        baseY + doorHeight * 0.5f, doorHeight - 0.2f, 0.16f, outward: 0.28f + leaf * 0.24f));
                    set.Boxes.Add(OnWall(BuildingPart.Trim, edge, along / edge.Length, leafWidth - 0.06f,
                        baseY + doorHeight * 0.5f, 0.12f, 0.2f, outward: 0.34f + leaf * 0.24f));
                }
            }
        }

        /// <summary>Steel trusses across the span, hung just under the ceiling, so the roof does not read as one plane.</summary>
        private static void AddHangarTrusses(BuildingDetailSet set, float[] xz, float baseY, float height)
        {
            var box = OrientedBounds(xz);
            var acrossX = -box.DirZ;
            var acrossZ = box.DirX;
            var y = baseY + height - HangarFront.CeilingDropMetres - 0.3f;
            for (var along = 4f; along < box.Length - 3f; along += 7.5f)
            {
                var x = box.X + box.DirX * (along - box.Length * 0.5f);
                var z = box.Z + box.DirZ * (along - box.Length * 0.5f);
                var length = box.Width - 1.2f;
                if (length < 6f || !BoxInside(xz, x, z, acrossX, acrossZ, length, 0.3f, 0.1f))
                    continue;
                set.Boxes.Add(new DetailBox(BuildingPart.Trim, x, y, z, length, 0.5f, 0.3f, acrossX, acrossZ));
                // A lower chord, so the truss has some depth.
                set.Boxes.Add(new DetailBox(BuildingPart.Trim, x, y - 0.55f, z, length, 0.14f, 0.2f, acrossX, acrossZ));
            }
        }

        /// <summary>Strip lights in rows down the hall; they glow at night like the lit windows.</summary>
        private static void AddCeilingLights(BuildingDetailSet set, float[] xz, float baseY, float height, Hash random)
        {
            var box = OrientedBounds(xz);
            var acrossX = -box.DirZ;
            var acrossZ = box.DirX;
            var rows = box.Width > 36f ? 3 : 2;
            var y = baseY + height - HangarFront.CeilingDropMetres - 0.12f;
            for (var row = 0; row < rows; row++)
            {
                var across = (row - (rows - 1) * 0.5f) * box.Width / (rows + 0.2f);
                for (var along = 6f; along < box.Length - 4f; along += 8f)
                {
                    var a = along - box.Length * 0.5f;
                    var x = box.X + box.DirX * a + acrossX * across;
                    var z = box.Z + box.DirZ * a + acrossZ * across;
                    if (!BoxInside(xz, x, z, box.DirX, box.DirZ, 4.5f, 0.4f, 1f))
                        continue;
                    // A few are off, as in any real hall.
                    var part = random.Next() < 0.88f ? BuildingPart.WindowLit : BuildingPart.WindowDark;
                    set.Boxes.Add(new DetailBox(part, x, y, z, 4.5f, 0.12f, 0.4f, box.DirX, box.DirZ));
                }
            }
        }

        // ---- Equipment kept inside --------------------------------------------------------------

        private enum Stored
        {
            Tug,
            PowerCart,
            Stairs,
            ToolChest,
            Dolly
        }

        private static (float Length, float Depth) Footprint(Stored kind) => kind switch
        {
            Stored.Tug => (3.4f, 1.8f),
            Stored.PowerCart => (2.4f, 1.3f),
            Stored.Stairs => (3.6f, 1.4f),
            Stored.ToolChest => (1.4f, 0.8f),
            _ => (3.2f, 1.6f)
        };

        private static Stored PickStored(Hash random)
        {
            var r = random.Next();
            return r < 0.22f ? Stored.Tug
                : r < 0.44f ? Stored.PowerCart
                : r < 0.60f ? Stored.Stairs
                : r < 0.82f ? Stored.ToolChest
                : Stored.Dolly;
        }

        /// <summary>Ground equipment parked along the side and back walls, inside the aircraft's clearance.</summary>
        private static void AddHangarEquipment(BuildingDetailSet set, float[] xz, int front, float baseY, Hash random)
        {
            var count = xz.Length / 2;
            var winding = Winding(xz);
            var placed = new List<(float X, float Z, float R)>();
            for (var i = 0; i < count; i++)
            {
                if (i == front)
                    continue;
                var edge = Edge(xz, i, winding);
                if (edge.Length < 8f)
                    continue;
                var along = 2.5f + random.Next() * 3f;
                while (along < edge.Length - 2.5f)
                {
                    var kind = PickStored(random);
                    var (length, depth) = Footprint(kind);
                    var inset = depth * 0.5f + 0.45f;
                    var x = edge.AX + edge.DirX * (along + length * 0.5f) - edge.OutX * inset;
                    var z = edge.AZ + edge.DirZ * (along + length * 0.5f) - edge.OutZ * inset;
                    var radius = Math.Max(length, depth) * 0.5f;
                    if (along + length > edge.Length - 2f || inset + depth * 0.5f > EquipmentBandMetres
                        || !BoxInside(xz, x, z, edge.DirX, edge.DirZ, length, depth, 0.1f) || Crowded(placed, x, z, radius))
                    {
                        along += 2f;
                        continue;
                    }

                    AddStoredItem(set, kind, x, z, edge.DirX, edge.DirZ, baseY);
                    placed.Add((x, z, radius));
                    along += length + 1.3f + random.Next() * 5f;
                }
            }
        }

        private static bool Crowded(List<(float X, float Z, float R)> placed, float x, float z, float radius)
        {
            foreach (var p in placed)
            {
                var dx = p.X - x;
                var dz = p.Z - z;
                var need = p.R + radius + 0.3f;
                if (dx * dx + dz * dz < need * need)
                    return true;
            }

            return false;
        }

        /// <summary>One piece of equipment: boxes in its own frame (a along dir, c across it), standing on the floor at baseY.</summary>
        private readonly struct Frame
        {
            private readonly BuildingDetailSet _set;
            private readonly float _x, _z, _dirX, _dirZ, _floor;

            public Frame(BuildingDetailSet set, float x, float z, float dirX, float dirZ, float floor)
            {
                _set = set;
                _x = x;
                _z = z;
                _dirX = dirX;
                _dirZ = dirZ;
                _floor = floor;
            }

            public void Box(BuildingPart part, float a, float c, float centreY, float length, float height, float depth)
            {
                var x = _x + _dirX * a - _dirZ * c;
                var z = _z + _dirZ * a + _dirX * c;
                _set.Boxes.Add(new DetailBox(part, x, _floor + centreY, z, length, height, depth, _dirX, _dirZ));
            }

            /// <summary>A wheel (dark) at a, c, with the given diameter and width.</summary>
            public void Wheel(float a, float c, float diameter, float width) =>
                Box(BuildingPart.EquipmentDark, a, c, diameter * 0.5f, diameter, diameter, width);
        }

        private static void AddStoredItem(BuildingDetailSet set, Stored kind, float x, float z, float dirX, float dirZ,
            float floor)
        {
            var f = new Frame(set, x, z, dirX, dirZ, floor);
            switch (kind)
            {
                case Stored.Tug:
                    f.Box(BuildingPart.Equipment, 0f, 0f, 0.65f, 3.4f, 0.8f, 1.7f);
                    f.Box(BuildingPart.Equipment, -0.55f, 0f, 1.5f, 1.3f, 1.0f, 1.5f);
                    f.Box(BuildingPart.EquipmentDark, -0.55f, 0f, 2.05f, 1.4f, 0.1f, 1.6f);
                    f.Box(BuildingPart.EquipmentDark, 0.1f, 0f, 1.5f, 0.1f, 0.8f, 1.35f);
                    f.Box(BuildingPart.EquipmentDark, 1.75f, 0f, 0.4f, 0.12f, 0.3f, 1.75f);
                    f.Box(BuildingPart.Trim, -1.62f, 0f, 0.85f, 0.14f, 0.2f, 1.0f);
                    for (var a = -1; a <= 1; a += 2)
                    for (var c = -1; c <= 1; c += 2)
                        f.Wheel(a * 1.15f, c * 0.78f, 0.7f, 0.32f);
                    break;
                case Stored.PowerCart:
                    f.Box(BuildingPart.Equipment, 0f, 0f, 0.95f, 2.0f, 1.0f, 1.15f);
                    f.Box(BuildingPart.EquipmentDark, 0f, 0f, 0.35f, 2.3f, 0.2f, 0.95f);
                    f.Box(BuildingPart.EquipmentDark, 0.55f, 0f, 1.5f, 0.7f, 0.1f, 0.9f);
                    f.Box(BuildingPart.Trim, 1.45f, 0f, 0.5f, 0.9f, 0.08f, 0.1f);
                    f.Wheel(-0.6f, -0.62f, 0.55f, 0.2f);
                    f.Wheel(-0.6f, 0.62f, 0.55f, 0.2f);
                    break;
                case Stored.Stairs:
                    // A boarding stair: deck on legs, four treads rising to it, and a rail along one side.
                    f.Box(BuildingPart.Equipment, 1.1f, 0f, 2.0f, 1.3f, 0.12f, 1.3f);
                    for (var tread = 0; tread < 4; tread++)
                        f.Box(BuildingPart.Equipment, 0.2f - tread * 0.5f, 0f, 0.25f + tread * 0.45f, 0.5f, 0.1f + tread * 0.0f, 1.2f);
                    f.Box(BuildingPart.EquipmentDark, 1.1f, -0.6f, 0.9f, 0.12f, 1.8f, 0.12f);
                    f.Box(BuildingPart.EquipmentDark, 1.1f, 0.6f, 0.9f, 0.12f, 1.8f, 0.12f);
                    f.Box(BuildingPart.Trim, 0.1f, -0.62f, 1.55f, 3.0f, 0.08f, 0.08f);
                    f.Wheel(1.3f, -0.6f, 0.5f, 0.2f);
                    f.Wheel(1.3f, 0.6f, 0.5f, 0.2f);
                    break;
                case Stored.ToolChest:
                    f.Box(BuildingPart.EquipmentRed, 0f, 0f, 0.65f, 1.3f, 1.2f, 0.75f);
                    f.Box(BuildingPart.EquipmentDark, 0f, 0f, 1.3f, 1.35f, 0.08f, 0.8f);
                    f.Box(BuildingPart.Trim, 0f, 0.39f, 0.9f, 1.0f, 0.5f, 0.04f);
                    f.Wheel(-0.5f, 0f, 0.2f, 0.7f);
                    break;
                default:
                    // A baggage-style dolly: low deck on four small wheels with a drawbar.
                    f.Box(BuildingPart.EquipmentDark, 0f, 0f, 0.5f, 3.0f, 0.14f, 1.5f);
                    f.Box(BuildingPart.Equipment, 0f, 0f, 0.62f, 2.9f, 0.1f, 1.4f);
                    f.Box(BuildingPart.Trim, 1.9f, 0f, 0.45f, 0.9f, 0.08f, 0.1f);
                    f.Wheel(-1.1f, -0.7f, 0.4f, 0.14f);
                    f.Wheel(-1.1f, 0.7f, 0.4f, 0.14f);
                    f.Wheel(1.1f, -0.7f, 0.4f, 0.14f);
                    f.Wheel(1.1f, 0.7f, 0.4f, 0.14f);
                    break;
            }
        }

        // ---- Fire station -----------------------------------------------------------------------

        private const float ApplianceLengthMetres = 8.2f;
        private const float ApplianceWidthMetres = 2.6f;

        private static void AddApplianceBays(BuildingDetailSet set, float[] xz, int front, float baseY, float height, Hash random)
        {
            if (front < 0)
                return;
            var edge = Edge(xz, front, Winding(xz));
            var bays = Math.Min(5, (int)(edge.Length * 0.85f / 6f));
            if (bays < 1)
                return;
            var doorHeight = Math.Min(5.2f, height - 1.5f);
            var pitch = edge.Length * 0.85f / bays;
            var doorWidth = Math.Min(4.6f, pitch - 1.2f);
            for (var b = 0; b < bays; b++)
            {
                var t = 0.5f + ((b + 0.5f) / bays - 0.5f) * edge.Length * 0.85f / edge.Length;
                var along = t * edge.Length;
                // An appliance stands nose-out, a metre inside the door. Open the bay only if there is room for it.
                var cx = edge.AX + edge.DirX * along - edge.OutX * (1.2f + ApplianceLengthMetres * 0.5f);
                var cz = edge.AZ + edge.DirZ * along - edge.OutZ * (1.2f + ApplianceLengthMetres * 0.5f);
                var open = BoxInside(xz, cx, cz, -edge.OutX, -edge.OutZ, ApplianceLengthMetres, ApplianceWidthMetres, 0.3f);
                if (open)
                {
                    set.Openings.Add(new DetailOpening(front, along - doorWidth * 0.5f, along + doorWidth * 0.5f, 0f, doorHeight));
                    // The shutter is rolled up into its box over the opening.
                    set.Boxes.Add(OnWall(BuildingPart.Door, edge, t, doorWidth + 0.4f, baseY + doorHeight + 0.3f, 0.6f, 0.35f,
                        outward: 0.2f));
                    AddAppliance(set, cx, cz, -edge.OutX, -edge.OutZ, baseY, random);
                }
                else
                {
                    set.Boxes.Add(OnWall(BuildingPart.Door, edge, t, doorWidth, baseY + doorHeight * 0.5f, doorHeight, 0.16f,
                        outward: 0.08f));
                    // Roller-door slats read as three horizontal lines.
                    for (var s = 1; s <= 3; s++)
                        set.Boxes.Add(OnWall(BuildingPart.Trim, edge, t, doorWidth, baseY + doorHeight * s / 4f, 0.08f, 0.22f,
                            outward: 0.12f));
                }

                // Frame the doorway either way.
                for (var side = -1; side <= 1; side += 2)
                    set.Boxes.Add(OnWall(BuildingPart.Trim, edge, t + side * (doorWidth * 0.5f + 0.2f) / edge.Length, 0.4f,
                        baseY + doorHeight * 0.5f, doorHeight, 0.3f, outward: 0.15f));
                AddWallPack(set, edge, t, doorHeight, baseY);
            }

            set.Boxes.Add(OnWall(BuildingPart.Trim, edge, 0.5f, edge.Length * 0.9f, baseY + doorHeight + 0.35f, 0.5f, 0.4f,
                outward: 0.2f));
        }

        /// <summary>A crash tender: red body and cab, a ladder rack, a roof monitor, wheels and a light bar.</summary>
        private static void AddAppliance(BuildingDetailSet set, float x, float z, float dirX, float dirZ, float baseY, Hash random)
        {
            var f = new Frame(set, x, z, dirX, dirZ, baseY);
            // dir points into the building, so the cab (front) is toward -dir, at the door.
            f.Box(BuildingPart.EquipmentRed, 1.1f, 0f, 1.55f, 5.2f, 1.9f, 2.5f);
            f.Box(BuildingPart.EquipmentRed, -2.6f, 0f, 1.75f, 2.4f, 2.3f, 2.5f);
            f.Box(BuildingPart.EquipmentDark, -3.82f, 0f, 2.1f, 0.1f, 1.0f, 2.2f);
            f.Box(BuildingPart.Trim, -3.9f, 0f, 0.55f, 0.25f, 0.3f, 2.5f);
            f.Box(BuildingPart.Trim, 1.1f, 0f, 2.75f, 4.6f, 0.2f, 0.7f);
            f.Box(BuildingPart.Trim, -2.6f, 0f, 3.0f, 0.3f, 0.2f, 1.9f);
            f.Box(BuildingPart.WindowLit, -2.6f, 0f, 3.18f, 0.2f, 0.12f, 1.7f);
            for (var a = 0; a < 3; a++)
            {
                var along = a == 0 ? -2.6f : a == 1 ? 0.2f : 2.2f;
                f.Wheel(along, -1.2f, 1.0f, 0.4f);
                f.Wheel(along, 1.2f, 1.0f, 0.4f);
            }

            f.Box(BuildingPart.Trim, 3.0f, 0f, 2.45f, 0.5f, 0.4f, 0.5f);
        }

        // ---- Freight docks ----------------------------------------------------------------------

        private static void AddLoadingDoors(BuildingDetailSet set, float[] xz, int front, float baseY, float height, Hash random)
        {
            if (front < 0)
                return;
            var edge = Edge(xz, front, Winding(xz));
            var doors = Math.Min(8, (int)(edge.Length * 0.7f / 8f));
            var doorHeight = Math.Min(4.2f, height - 1.8f);
            var dockOpen = false;
            for (var d = 0; d < doors; d++)
            {
                var t = 0.5f + ((d + 0.5f) / doors - 0.5f) * 0.7f;
                var along = t * edge.Length;
                // Every other dock stands open on a raised floor with a hall behind it, if the shed is deep enough.
                var cx = edge.AX + edge.DirX * along - edge.OutX * 4f;
                var cz = edge.AZ + edge.DirZ * along - edge.OutZ * 4f;
                var open = d % 2 == 0 && doorHeight > DockFloorMetres + 2.2f
                    && BoxInside(xz, cx, cz, edge.DirX, edge.DirZ, 4.2f, 6f, 0.3f);
                if (open)
                {
                    dockOpen = true;
                    set.Openings.Add(new DetailOpening(front, along - 1.8f, along + 1.8f, DockFloorMetres, doorHeight));
                    // Rolled-up shutter, and the load waiting just inside.
                    set.Boxes.Add(OnWall(BuildingPart.Door, edge, t, 4.0f, baseY + doorHeight + 0.2f, 0.4f, 0.3f, outward: 0.18f));
                    AddDockLoad(set, cx, cz, edge.DirX, edge.DirZ, baseY + DockFloorMetres, random);
                    if (random.Next() < 0.55f)
                        AddDockVan(set, edge, t, baseY);
                }
                else
                {
                    set.Boxes.Add(OnWall(BuildingPart.Door, edge, t, 3.6f, baseY + doorHeight * 0.5f, doorHeight, 0.14f,
                        outward: 0.07f));
                }

                set.Boxes.Add(OnWall(BuildingPart.Trim, edge, t, 4.2f, baseY + doorHeight + 0.25f, 0.35f, 0.9f,
                    outward: 0.45f));
                AddWallPack(set, edge, t, doorHeight - 0.3f, baseY);
                // Dock bumpers either side of the leaf and a kerb bollard in front.
                for (var side = -1; side <= 1; side += 2)
                    set.Boxes.Add(OnWall(BuildingPart.Trim, edge, t + side * 2.0f / edge.Length, 0.3f, baseY + 1.1f, 0.5f, 0.3f,
                        outward: 0.2f));
                set.Boxes.Add(OnWall(BuildingPart.Trim, edge, t, 0.24f, baseY + 0.55f, 1.1f, 0.24f, outward: 2.4f));
            }

            if (dockOpen)
                set.InteriorFloorMetres = DockFloorMetres;
        }

        /// <summary>Shrink-wrapped pallets and a roll cage just inside an open dock door.</summary>
        private static void AddDockLoad(BuildingDetailSet set, float x, float z, float dirX, float dirZ, float floor, Hash random)
        {
            var f = new Frame(set, x, z, dirX, dirZ, floor);
            for (var p = 0; p < 2; p++)
            {
                var c = -1.2f + p * 2.4f;
                var high = 0.9f + random.Next() * 0.9f;
                f.Box(BuildingPart.EquipmentDark, 0f, c, 0.08f, 1.2f, 0.16f, 1.0f);
                f.Box(BuildingPart.Door, 0f, c, 0.16f + high * 0.5f, 1.1f, high, 0.95f);
            }

            f.Box(BuildingPart.Equipment, 0f, 0f, 0.95f, 0.8f, 1.7f, 0.8f);
            f.Box(BuildingPart.EquipmentDark, 0f, 0f, 0.1f, 0.85f, 0.1f, 0.85f);
        }

        /// <summary>A box van backed up to a dock door.</summary>
        private static void AddDockVan(BuildingDetailSet set, WallEdge edge, float t, float baseY)
        {
            var x = edge.AX + (edge.BX - edge.AX) * t + edge.OutX * 4.2f;
            var z = edge.AZ + (edge.BZ - edge.AZ) * t + edge.OutZ * 4.2f;
            // The van's long axis is square to the wall.
            var f = new Frame(set, x, z, edge.OutX, edge.OutZ, baseY);
            f.Box(BuildingPart.Door, -1.0f, 0f, 2.0f, 6.0f, 2.3f, 2.4f);
            f.Box(BuildingPart.EquipmentDark, -1.0f, 0f, 0.7f, 6.0f, 0.3f, 2.2f);
            f.Box(BuildingPart.Equipment, 2.9f, 0f, 1.5f, 1.8f, 1.9f, 2.3f);
            f.Box(BuildingPart.EquipmentDark, 3.82f, 0f, 1.9f, 0.1f, 0.8f, 2.0f);
            for (var c = -1; c <= 1; c += 2)
            {
                f.Wheel(-2.4f, c * 1.1f, 0.9f, 0.3f);
                f.Wheel(2.9f, c * 1.1f, 0.9f, 0.3f);
            }
        }

        // ---- Wall detail ------------------------------------------------------------------------

        /// <summary>
        /// Terminal landside: slim vertical fins standing proud of the office glazing every 3 m (they catch the low sun
        /// and give the long wall a rhythm), a spandrel band at the first-floor line, and a deeper fascia return.
        /// </summary>
        private static void AddFacadeFins(BuildingDetailSet set, WallEdge edge, float baseY, float height)
        {
            var top = Math.Min(baseY + 8.2f + 0.9f, baseY + height - 1.2f);
            var bottom = baseY + 8.2f - 0.9f;
            var finHeight = top - bottom;
            if (finHeight < 0.6f)
                return;
            for (var along = 3f; along < edge.Length - 3f; along += 3f)
                set.Boxes.Add(OnWall(BuildingPart.Trim, edge, along / edge.Length, 0.1f, (top + bottom) * 0.5f, finHeight, 0.5f,
                    outward: 0.3f));
            // Spandrel between the ground-level doors and the offices.
            set.Boxes.Add(OnWall(BuildingPart.Trim, edge, 0.5f, edge.Length, baseY + 6.9f, 0.45f, 0.28f, outward: 0.14f));
            set.Boxes.Add(OnWall(BuildingPart.Trim, edge, 0.5f, edge.Length, baseY + 0.55f, 1.1f, 0.14f, outward: 0.07f));
        }

        /// <summary>
        /// Metal-clad walls: standing ribs every 3 m, a girt line, louvres, a personnel door with its lit pack on the
        /// long side walls, and downpipes at the corners. The front wall keeps just its girt and ribs clear of the doors.
        /// </summary>
        private static void AddCladding(BuildingDetailSet set, float[] xz, float baseY, float height, int front, Hash random)
        {
            var count = xz.Length / 2;
            var winding = Winding(xz);
            for (var i = 0; i < count; i++)
            {
                var edge = Edge(xz, i, winding);
                if (edge.Length < 8f)
                    continue;
                var ribHeight = height - 1.1f;
                var skipFrom = float.MaxValue;
                var skipTo = float.MinValue;
                foreach (var opening in set.Openings)
                    if (opening.EdgeIndex == i)
                    {
                        skipFrom = Math.Min(skipFrom, opening.FromMetres - 0.8f);
                        skipTo = Math.Max(skipTo, opening.ToMetres + 0.8f);
                    }

                // A door slab on this wall (closed doors) also keeps the ribs off it.
                if (i == front && set.Openings.Count == 0)
                {
                    skipFrom = edge.Length * 0.09f;
                    skipTo = edge.Length * 0.91f;
                }

                for (var along = 1.5f; along < edge.Length - 1.5f; along += 3f)
                {
                    if (along > skipFrom && along < skipTo)
                        continue;
                    set.Boxes.Add(OnWall(BuildingPart.Shell, edge, along / edge.Length, 0.12f, baseY + 0.5f + ribHeight * 0.5f,
                        ribHeight, 0.1f, outward: 0.05f));
                }

                // Girt line.
                set.Boxes.Add(OnWall(BuildingPart.Trim, edge, 0.5f, edge.Length - 0.4f, baseY + 3.6f, 0.16f, 0.12f, outward: 0.06f));
                if (i == front)
                    continue;
                // Eaves gutter, and a downpipe near each end.
                set.Boxes.Add(OnWall(BuildingPart.Trim, edge, 0.5f, edge.Length, baseY + height - 0.25f, 0.2f, 0.3f, outward: 0.15f));
                foreach (var end in new[] { 0.4f, edge.Length - 0.4f })
                    set.Boxes.Add(OnWall(BuildingPart.Trim, edge, end / edge.Length, 0.14f, baseY + (height - 0.4f) * 0.5f,
                        height - 0.4f, 0.14f, outward: 0.2f));
                if (edge.Length < 14f)
                    continue;
                // Louvres high on the wall, a pair for every ~30 m.
                for (var louvre = edge.Length * 0.2f; louvre < edge.Length - 4f; louvre += 14f)
                    set.Boxes.Add(OnWall(BuildingPart.Trim, edge, louvre / edge.Length, 2.4f, baseY + height - 1.6f, 0.9f, 0.14f,
                        outward: 0.08f));
                // One personnel door with a step and a lit pack, off to one side.
                var door = edge.Length * (0.3f + random.Next() * 0.4f);
                set.Boxes.Add(OnWall(BuildingPart.Trim, edge, door / edge.Length, 1.4f, baseY + 1.2f, 2.4f, 0.3f, outward: 0.15f));
                set.Boxes.Add(OnWall(BuildingPart.Door, edge, door / edge.Length, 1.0f, baseY + 1.1f, 2.2f, 0.08f, outward: 0.2f));
                set.Boxes.Add(OnWall(BuildingPart.Trim, edge, door / edge.Length, 1.8f, baseY + 0.08f, 0.16f, 0.8f, outward: 0.4f));
                AddWallPack(set, edge, door / edge.Length, 2.2f, baseY);
            }
        }
    }
}

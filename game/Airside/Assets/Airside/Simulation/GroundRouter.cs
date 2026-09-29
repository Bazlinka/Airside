using System;
using System.Collections.Generic;

namespace Airside.Simulation
{
    /// <summary>
    /// Something a ground vehicle or walker must go round: a building or aircraft outline
    /// (closed polygon) or a wall or fence line (open polyline), in world x/z metres.
    /// </summary>
    public sealed class RouteObstacle
    {
        public RouteObstacle(float[] xz, bool closed)
        {
            if (xz == null || xz.Length < 4)
                throw new ArgumentException("An obstacle needs at least two points.", nameof(xz));
            Xz = xz;
            Closed = closed;
            MinX = MinZ = float.MaxValue;
            MaxX = MaxZ = float.MinValue;
            for (var i = 0; i < xz.Length; i += 2)
            {
                MinX = Math.Min(MinX, xz[i]);
                MaxX = Math.Max(MaxX, xz[i]);
                MinZ = Math.Min(MinZ, xz[i + 1]);
                MaxZ = Math.Max(MaxZ, xz[i + 1]);
            }
        }

        public float[] Xz { get; }
        public bool Closed { get; }
        public float MinX { get; }
        public float MaxX { get; }
        public float MinZ { get; }
        public float MaxZ { get; }

        /// <summary>A rectangle from its four corners, in order.</summary>
        public static RouteObstacle Quad(float ax, float az, float bx, float bz, float cx, float cz, float dx, float dz) =>
            new(new[] { ax, az, bx, bz, cx, cz, dx, dz }, true);

        /// <summary>True when (x, z) is inside the outline or within <paramref name="clearance"/> of it.</summary>
        public bool Blocks(float x, float z, float clearance)
        {
            if (x < MinX - clearance || x > MaxX + clearance || z < MinZ - clearance || z > MaxZ + clearance)
                return false;
            if (Closed && Inside(x, z))
                return true;
            var n = Xz.Length / 2;
            var edges = Closed ? n : n - 1;
            var limit = clearance * clearance;
            for (var i = 0; i < edges; i++)
            {
                var j = (i + 1) % n;
                if (DistanceSquaredToSegment(x, z, Xz[i * 2], Xz[i * 2 + 1], Xz[j * 2], Xz[j * 2 + 1]) < limit)
                    return true;
            }

            return false;
        }

        private bool Inside(float x, float z)
        {
            var inside = false;
            var n = Xz.Length / 2;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                float xi = Xz[i * 2], zi = Xz[i * 2 + 1], xj = Xz[j * 2], zj = Xz[j * 2 + 1];
                if ((zi > z) != (zj > z) && x < (xj - xi) * (z - zi) / (zj - zi) + xi)
                    inside = !inside;
            }

            return inside;
        }

        private static float DistanceSquaredToSegment(float px, float pz, float ax, float az, float bx, float bz)
        {
            var abx = bx - ax;
            var abz = bz - az;
            var length = abx * abx + abz * abz;
            var t = length > 1e-6f ? Math.Max(0f, Math.Min(1f, ((px - ax) * abx + (pz - az) * abz) / length)) : 0f;
            var qx = ax + abx * t - px;
            var qz = az + abz * t - pz;
            return qx * qx + qz * qz;
        }
    }

    /// <summary>
    /// Plans a ground route round aircraft, buildings and walls (presentation only — nothing in
    /// the simulation waits for it). A local grid around the trip is marked where obstacles,
    /// grown by the traveller's clearance, stand; A* finds the cheapest way through; the result
    /// is pulled taut into a few straight legs.
    ///
    /// Blocked cells are expensive, not forbidden: a start or goal that sits against an obstacle
    /// (a stair truck docking at a door, a vehicle already hemmed in) still gets a route, and it
    /// is the shortest way out.
    ///
    /// Pure and deterministic, with no UnityEngine types, so the headless harness checks it.
    /// </summary>
    public static class GroundRouter
    {
        /// <summary>Extra room planned round the start–goal box, metres.</summary>
        public const float MarginMetres = 45f;

        private const int MaxCells = 90_000;
        private const float BlockedCost = 40f;

        /// <summary>
        /// Route from (sx, sz) to (gx, gz) keeping <paramref name="clearance"/> from every
        /// obstacle. Writes world waypoints, start first and goal last, into
        /// <paramref name="into"/> as x, z pairs. Returns false only for a degenerate request.
        /// </summary>
        public static bool Plan(float sx, float sz, float gx, float gz, IReadOnlyList<RouteObstacle> obstacles,
            float clearance, List<float> into, float cellMetres = 1f)
        {
            into.Clear();
            if (float.IsNaN(sx + sz + gx + gz))
                return false;

            var minX = Math.Min(sx, gx) - MarginMetres;
            var minZ = Math.Min(sz, gz) - MarginMetres;
            var spanX = Math.Abs(gx - sx) + MarginMetres * 2f;
            var spanZ = Math.Abs(gz - sz) + MarginMetres * 2f;
            var cell = Math.Max(cellMetres, (float)Math.Sqrt(spanX * spanZ / MaxCells));
            var w = Math.Max(2, (int)Math.Ceiling(spanX / cell));
            var h = Math.Max(2, (int)Math.Ceiling(spanZ / cell));
            var blocked = new bool[w * h];
            var any = false;
            // A straight leg is only checked at cell centres; grow each obstacle by a cell's
            // half-diagonal as well so no leg can shave a corner closer than the clearance.
            var grow = clearance + cell * 0.75f;

            if (obstacles != null)
                foreach (var o in obstacles)
                {
                    if (o.MaxX + grow < minX || o.MinX - grow > minX + w * cell
                        || o.MaxZ + grow < minZ || o.MinZ - grow > minZ + h * cell)
                        continue;
                    var x0 = Clamp((int)Math.Floor((o.MinX - grow - minX) / cell), 0, w - 1);
                    var x1 = Clamp((int)Math.Ceiling((o.MaxX + grow - minX) / cell), 0, w - 1);
                    var z0 = Clamp((int)Math.Floor((o.MinZ - grow - minZ) / cell), 0, h - 1);
                    var z1 = Clamp((int)Math.Ceiling((o.MaxZ + grow - minZ) / cell), 0, h - 1);
                    for (var cz = z0; cz <= z1; cz++)
                    for (var cx = x0; cx <= x1; cx++)
                    {
                        var index = cz * w + cx;
                        if (blocked[index])
                            continue;
                        if (o.Blocks(minX + (cx + 0.5f) * cell, minZ + (cz + 0.5f) * cell, grow))
                        {
                            blocked[index] = true;
                            any = true;
                        }
                    }
                }

            into.Add(sx);
            into.Add(sz);
            var start = Cell(sx, sz);
            var goal = Cell(gx, gz);
            if (any && start != goal)
            {
                var cells = AStar(start, goal);
                Smooth(cells);
            }

            into.Add(gx);
            into.Add(gz);
            return true;

            int Cell(float x, float z) =>
                Clamp((int)((z - minZ) / cell), 0, h - 1) * w + Clamp((int)((x - minX) / cell), 0, w - 1);

            List<int> AStar(int from, int to)
            {
                var cost = new float[w * h];
                var came = new int[w * h];
                for (var i = 0; i < cost.Length; i++)
                {
                    cost[i] = float.MaxValue;
                    came[i] = -1;
                }

                var tx = to % w;
                var tz = to / w;
                var open = new MinHeap(256);
                cost[from] = 0f;
                open.Push(from, Heuristic(from, to));
                while (open.Count > 0)
                {
                    var current = open.Pop();
                    if (current == to)
                        break;
                    var cx = current % w;
                    var cz = current / w;
                    for (var dz = -1; dz <= 1; dz++)
                    for (var dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dz == 0)
                            continue;
                        var nx = cx + dx;
                        var nz = cz + dz;
                        if (nx < 0 || nz < 0 || nx >= w || nz >= h)
                            continue;
                        var next = nz * w + nx;
                        var step = dx != 0 && dz != 0 ? 1.41421356f : 1f;
                        if (blocked[next])
                            step *= BlockedCost;
                        var tentative = cost[current] + step;
                        if (tentative >= cost[next])
                            continue;
                        cost[next] = tentative;
                        came[next] = current;
                        open.Push(next, tentative + Heuristic(next, to));
                    }
                }

                var path = new List<int>();
                for (var at = to; at >= 0; at = came[at])
                {
                    path.Add(at);
                    if (at == from)
                        break;
                }

                path.Reverse();
                return path;

                float Heuristic(int a, int b)
                {
                    var ax = Math.Abs(a % w - tx);
                    var az = Math.Abs(a / w - tz);
                    return Math.Max(ax, az) + 0.41421356f * Math.Min(ax, az);
                }
            }

            void Smooth(List<int> cells)
            {
                if (cells.Count < 3 || cells[0] != start)
                    return;
                // String-pull: from each kept point, jump to the furthest cell in plain sight.
                var anchor = 0;
                while (anchor < cells.Count - 1)
                {
                    var furthest = anchor + 1;
                    for (var k = cells.Count - 1; k > anchor + 1; k--)
                        if (Clear(cells[anchor], cells[k]))
                        {
                            furthest = k;
                            break;
                        }

                    if (furthest < cells.Count - 1)
                    {
                        var c = cells[furthest];
                        into.Add(minX + (c % w + 0.5f) * cell);
                        into.Add(minZ + (c / w + 0.5f) * cell);
                    }

                    anchor = furthest;
                }
            }

            // A straight leg between two cells crosses no blocked cell — except within a few
            // metres of the start or goal, which may themselves sit against an obstacle.
            bool Clear(int a, int b)
            {
                float ax = a % w, az = a / w, bx = b % w, bz = b / w;
                var steps = (int)Math.Ceiling(Math.Max(Math.Abs(bx - ax), Math.Abs(bz - az)) * 2f);
                var escape = (clearance + 2f) / cell;
                for (var s = 0; s <= steps; s++)
                {
                    var t = steps == 0 ? 0f : s / (float)steps;
                    var px = ax + (bx - ax) * t;
                    var pz = az + (bz - az) * t;
                    var index = Clamp((int)Math.Round(pz), 0, h - 1) * w + Clamp((int)Math.Round(px), 0, w - 1);
                    if (!blocked[index])
                        continue;
                    if (Near(px, pz, start) || Near(px, pz, goal))
                        continue;
                    return false;
                }

                return true;

                bool Near(float px, float pz, int c) =>
                    Math.Abs(px - c % w) <= escape && Math.Abs(pz - c / w) <= escape;
            }
        }

        /// <summary>Length of the polyline in <paramref name="xz"/> (x, z pairs).</summary>
        public static float Length(IReadOnlyList<float> xz)
        {
            var total = 0f;
            for (var i = 2; i + 1 < xz.Count; i += 2)
            {
                var dx = xz[i] - xz[i - 2];
                var dz = xz[i + 1] - xz[i - 1];
                total += (float)Math.Sqrt(dx * dx + dz * dz);
            }

            return total;
        }

        /// <summary>Point <paramref name="distance"/> metres along the polyline, and the leg's direction.</summary>
        public static (float X, float Z, float DirX, float DirZ) Along(IReadOnlyList<float> xz, float distance)
        {
            var remaining = Math.Max(0f, distance);
            for (var i = 2; i + 1 < xz.Count; i += 2)
            {
                var dx = xz[i] - xz[i - 2];
                var dz = xz[i + 1] - xz[i - 1];
                var leg = (float)Math.Sqrt(dx * dx + dz * dz);
                if (leg < 1e-4f)
                    continue;
                if (remaining <= leg || i + 2 >= xz.Count)
                {
                    var t = Math.Min(1f, remaining / leg);
                    return (xz[i - 2] + dx * t, xz[i - 1] + dz * t, dx / leg, dz / leg);
                }

                remaining -= leg;
            }

            return xz.Count >= 2 ? (xz[xz.Count - 2], xz[xz.Count - 1], 0f, 1f) : (0f, 0f, 0f, 1f);
        }

        private static int Clamp(int value, int min, int max) => value < min ? min : value > max ? max : value;

        private sealed class MinHeap
        {
            private int[] _items;
            private float[] _keys;

            public MinHeap(int capacity)
            {
                _items = new int[capacity];
                _keys = new float[capacity];
            }

            public int Count { get; private set; }

            public void Push(int item, float key)
            {
                if (Count == _items.Length)
                {
                    Array.Resize(ref _items, Count * 2);
                    Array.Resize(ref _keys, Count * 2);
                }

                var i = Count++;
                while (i > 0)
                {
                    var parent = (i - 1) / 2;
                    if (_keys[parent] <= key)
                        break;
                    _items[i] = _items[parent];
                    _keys[i] = _keys[parent];
                    i = parent;
                }

                _items[i] = item;
                _keys[i] = key;
            }

            public int Pop()
            {
                var top = _items[0];
                var lastItem = _items[--Count];
                var lastKey = _keys[Count];
                var i = 0;
                while (true)
                {
                    var child = i * 2 + 1;
                    if (child >= Count)
                        break;
                    if (child + 1 < Count && _keys[child + 1] < _keys[child])
                        child++;
                    if (_keys[child] >= lastKey)
                        break;
                    _items[i] = _items[child];
                    _keys[i] = _keys[child];
                    i = child;
                }

                if (Count > 0)
                {
                    _items[i] = lastItem;
                    _keys[i] = lastKey;
                }

                return top;
            }
        }
    }
}

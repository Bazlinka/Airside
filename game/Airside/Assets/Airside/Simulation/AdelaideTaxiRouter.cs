using System;
using System.Collections.Generic;

namespace Airside.Simulation
{
    /// <summary>
    /// Shortest path on Adelaide's OSM taxiway centrelines. 05/23 routes are
    /// already baked that way; 12/30 used a handful of waypoints that cut
    /// across grass. This is the same graph the layout generator uses: taxiway
    /// vertices snapped to 2 m, joins under 8 m, and the two runways as
    /// expensive crossings so D1–D2 can reach the 30 hold without a grass chord.
    /// </summary>
    public static class AdelaideTaxiRouter
    {
        public const float JoinMetres = 8f;
        public const float RunwayCost = 20f;

        private static Graph _graph;

        /// <summary>Taxiway-following polyline from <paramref name="start"/> to <paramref name="end"/>.</summary>
        public static float[] Route(float startX, float startZ, float endX, float endZ)
        {
            var graph = Shared();
            var start = graph.Nearest(startX, startZ);
            var goal = graph.Nearest(endX, endZ);
            var hops = graph.Dijkstra(start, goal);
            if (hops == null || hops.Count == 0)
                return GroundPathSmoothing.FilletAndDensify(new[] { startX, startZ, endX, endZ });

            var points = new List<float>(hops.Count * 2 + 4) { startX, startZ };
            foreach (var node in hops)
                Append(points, graph.X[node], graph.Z[node]);
            Append(points, endX, endZ);
            return GroundPathSmoothing.FilletAndDensify(points.ToArray(), 24f, 8f);
        }

        /// <summary>Metres from a point to the nearest taxiway centreline (not pavement edge).</summary>
        public static float DistanceToCentreline(float x, float z)
        {
            var best = float.MaxValue;
            foreach (var taxiway in AdelaideLayout.Taxiways)
            {
                var d = DistanceToPolyline(x, z, taxiway.Xz);
                if (d < best)
                    best = d;
            }

            return best;
        }

        private static Graph Shared() => _graph ??= Build();

        private static Graph Build()
        {
            var graph = new Graph();
            foreach (var taxiway in AdelaideLayout.Taxiways)
                graph.AddPolyline(taxiway.Xz, runway: false);
            graph.AddPolyline(MainRunway(), runway: true);
            graph.AddPolyline(CrossRunway(), runway: true);
            graph.JoinNearby(JoinMetres);
            return graph;
        }

        private static float[] MainRunway()
        {
            var half = AdelaideLayout.MainRunwayLengthMetres * 0.5f;
            var points = new List<float>(160);
            for (var x = -half; x <= half; x += 20f)
            {
                points.Add(x);
                points.Add(0f);
            }

            if (points[points.Count - 2] < half - 0.5f)
            {
                points.Add(half);
                points.Add(0f);
            }

            return points.ToArray();
        }

        private static float[] CrossRunway()
        {
            var half = AdelaideLayout.CrossRunwayLengthMetres * 0.5f;
            var points = new List<float>(160);
            for (var along = -half; along <= half; along += 20f)
            {
                LocalToWorld(along, 0f, out var x, out var z);
                points.Add(x);
                points.Add(z);
            }

            LocalToWorld(half, 0f, out var endX, out var endZ);
            if (points.Count < 2 || Hypot(points[points.Count - 2] - endX, points[points.Count - 1] - endZ) > 0.5f)
            {
                points.Add(endX);
                points.Add(endZ);
            }

            return points.ToArray();
        }

        private static void LocalToWorld(float localX, float localZ, out float x, out float z)
        {
            var yaw = AdelaideLayout.CrossRunwayYawDegrees * Math.PI / 180.0;
            var cos = Math.Cos(yaw);
            var sin = Math.Sin(yaw);
            x = (float)(AdelaideLayout.CrossRunwayCenterX + cos * localX + sin * localZ);
            z = (float)(AdelaideLayout.CrossRunwayCenterZ - sin * localX + cos * localZ);
        }

        private static void Append(List<float> points, float x, float z)
        {
            if (points.Count >= 2 && Hypot(points[points.Count - 2] - x, points[points.Count - 1] - z) < 0.6f)
                return;
            points.Add(x);
            points.Add(z);
        }

        private static float DistanceToPolyline(float x, float z, float[] xz)
        {
            var best = float.MaxValue;
            for (var i = 0; i + 3 < xz.Length; i += 2)
            {
                var d = DistanceToSegment(x, z, xz[i], xz[i + 1], xz[i + 2], xz[i + 3]);
                if (d < best)
                    best = d;
            }

            return best;
        }

        private static float DistanceToSegment(float x, float z, float ax, float az, float bx, float bz)
        {
            var dx = bx - ax;
            var dz = bz - az;
            var length2 = dx * dx + dz * dz;
            var t = length2 < 1e-6f ? 0f : Math.Max(0f, Math.Min(1f, ((x - ax) * dx + (z - az) * dz) / length2));
            return Hypot(x - (ax + dx * t), z - (az + dz * t));
        }

        private static float Hypot(float x, float z) => (float)Math.Sqrt(x * x + z * z);

        private sealed class Graph
        {
            public readonly List<float> X = new();
            public readonly List<float> Z = new();
            private readonly Dictionary<(int, int), int> _index = new();
            private readonly List<List<Edge>> _adj = new();

            public int Count => X.Count;

            public void AddPolyline(float[] xz, bool runway)
            {
                var previous = -1;
                for (var i = 0; i + 1 < xz.Length; i += 2)
                {
                    var node = Add(xz[i], xz[i + 1]);
                    if (previous >= 0 && previous != node)
                        Connect(previous, node, runway);
                    previous = node;
                }
            }

            public void JoinNearby(float metres)
            {
                var limit = metres * metres;
                for (var i = 0; i < Count; i++)
                {
                    for (var j = i + 1; j < Count; j++)
                    {
                        var dx = X[i] - X[j];
                        var dz = Z[i] - Z[j];
                        if (dx * dx + dz * dz >= limit || Linked(i, j))
                            continue;
                        Connect(i, j, runway: false);
                    }
                }
            }

            public int Nearest(float x, float z)
            {
                var best = 0;
                var bestD = float.MaxValue;
                for (var i = 0; i < Count; i++)
                {
                    var d = Hypot(X[i] - x, Z[i] - z);
                    if (d >= bestD)
                        continue;
                    bestD = d;
                    best = i;
                }

                return best;
            }

            public List<int> Dijkstra(int start, int goal)
            {
                var count = Count;
                var dist = new float[count];
                var prev = new int[count];
                var used = new bool[count];
                for (var i = 0; i < count; i++)
                {
                    dist[i] = float.PositiveInfinity;
                    prev[i] = -1;
                }

                dist[start] = 0f;
                for (var n = 0; n < count; n++)
                {
                    var u = -1;
                    var best = float.PositiveInfinity;
                    for (var i = 0; i < count; i++)
                    {
                        if (used[i] || dist[i] >= best)
                            continue;
                        best = dist[i];
                        u = i;
                    }

                    if (u < 0 || u == goal)
                        break;
                    used[u] = true;
                    foreach (var edge in _adj[u])
                    {
                        var next = dist[u] + edge.Cost;
                        if (next >= dist[edge.To])
                            continue;
                        dist[edge.To] = next;
                        prev[edge.To] = u;
                    }
                }

                if (float.IsInfinity(dist[goal]))
                    return null;

                var path = new List<int>();
                for (var at = goal; at >= 0; at = prev[at])
                    path.Add(at);
                path.Reverse();
                return path;
            }

            private int Add(float x, float z)
            {
                var key = (Snap(x), Snap(z));
                if (_index.TryGetValue(key, out var existing))
                    return existing;
                var id = Count;
                _index[key] = id;
                X.Add(x);
                Z.Add(z);
                _adj.Add(new List<Edge>(4));
                return id;
            }

            private void Connect(int a, int b, bool runway)
            {
                var cost = Hypot(X[a] - X[b], Z[a] - Z[b]);
                if (cost < 0.05f)
                    return;
                if (runway)
                    cost *= RunwayCost;
                _adj[a].Add(new Edge(b, cost));
                _adj[b].Add(new Edge(a, cost));
            }

            private bool Linked(int a, int b)
            {
                foreach (var edge in _adj[a])
                    if (edge.To == b)
                        return true;
                return false;
            }

            private static int Snap(float value) => (int)Math.Round(value / 2f) * 2;
        }

        private readonly struct Edge
        {
            public Edge(int to, float cost)
            {
                To = to;
                Cost = cost;
            }

            public int To { get; }
            public float Cost { get; }
        }
    }
}

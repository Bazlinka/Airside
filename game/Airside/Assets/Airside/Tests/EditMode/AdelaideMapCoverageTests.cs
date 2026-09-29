using System;
using System.Collections.Generic;
using System.IO;
using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>
    /// ADR 0184: no empty spots. Every building the game draws must have a street within reach: a house with no
    /// road anywhere near it means the road data has a hole (or the building is a stray).
    /// </summary>
    public sealed class AdelaideMapCoverageTests
    {
        public const float ReachMetres = 60f;
        private const float Cell = 60f;

        private static byte[] FindArt(string artPath)
        {
            var relative = Path.Combine("Assets", "Airside", "Art", artPath);
            foreach (var start in new[] { Directory.GetCurrentDirectory(), TestContext.CurrentContext.TestDirectory })
                for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
                    foreach (var candidate in new[] { Path.Combine(dir.FullName, relative), Path.Combine(dir.FullName, "game", "Airside", relative) })
                        if (File.Exists(candidate))
                            return File.ReadAllBytes(candidate);
            Assert.Fail(artPath + " not found");
            return null;
        }

        private static Dictionary<(int, int), List<int>> RoadGrid()
        {
            var grid = new Dictionary<(int, int), List<int>>();
            var p = AdelaideRoadNetwork.Points;
            foreach (var road in AdelaideRoadNetwork.Roads)
            {
                if (road.IsAirside)
                    continue;
                for (var i = road.PointStart; i + 1 < road.PointStart + road.PointCount; i++)
                {
                    float ax = p[i * 2], az = p[i * 2 + 1], bx = p[i * 2 + 2], bz = p[i * 2 + 3];
                    var steps = (int)(Math.Sqrt((bx - ax) * (bx - ax) + (bz - az) * (bz - az)) / Cell) + 1;
                    for (var s = 0; s <= steps; s++)
                    {
                        var t = (float)s / steps;
                        var key = ((int)Math.Floor((ax + (bx - ax) * t) / Cell), (int)Math.Floor((az + (bz - az) * t) / Cell));
                        if (!grid.TryGetValue(key, out var list))
                            grid[key] = list = new List<int>();
                        if (list.Count == 0 || list[list.Count - 1] != i)
                            list.Add(i);
                    }
                }
            }

            return grid;
        }

        private static double NearestRoad(Dictionary<(int, int), List<int>> grid, float x, float z)
        {
            var p = AdelaideRoadNetwork.Points;
            var best = double.MaxValue;
            var cx = (int)Math.Floor(x / Cell);
            var cz = (int)Math.Floor(z / Cell);
            for (var dx = -1; dx <= 1; dx++)
            for (var dz = -1; dz <= 1; dz++)
            {
                if (!grid.TryGetValue((cx + dx, cz + dz), out var list))
                    continue;
                foreach (var i in list)
                {
                    float ax = p[i * 2], az = p[i * 2 + 1], bx = p[i * 2 + 2], bz = p[i * 2 + 3];
                    var vx = bx - ax;
                    var vz = bz - az;
                    var l2 = vx * vx + vz * vz;
                    var t = l2 <= 0f ? 0f : Math.Max(0f, Math.Min(1f, ((x - ax) * vx + (z - az) * vz) / l2));
                    best = Math.Min(best, Math.Sqrt(Math.Pow(x - (ax + vx * t), 2) + Math.Pow(z - (az + vz * t), 2)));
                }
            }

            return best;
        }

        private static float FootprintRadius(AdelaideSuburbData.Building b)
        {
            var best = 0f;
            var f = b.Footprint;
            if (f == null)
                return 0f;
            for (var i = 0; i + 1 < f.Length; i += 2)
                best = Math.Max(best, (float)Math.Sqrt((f[i] - b.CentreX) * (f[i] - b.CentreX) + (f[i + 1] - b.CentreZ) * (f[i + 1] - b.CentreZ)));
            return best;
        }

        [Test]
        public void EveryBuildingHasARoadNearby()
        {
            var data = AdelaideSuburbData.Parse(FindArt(AdelaideSuburbData.ArtPath));
            Assert.That(data, Is.Not.Null);
            var grid = RoadGrid();
            var total = 0;
            var stranded = new List<string>();
            foreach (var b in data.Buildings)
            {
                total++;
                // a big building is reached from its edge, not its middle
                var radius = b.IsHipped ? Math.Max(b.HalfLength, b.HalfWidth) : FootprintRadius(b);
                if (NearestRoad(grid, b.CentreX, b.CentreZ) - radius > ReachMetres)
                    stranded.Add($"({b.CentreX:F0},{b.CentreZ:F0})");
            }

            Assert.That(total, Is.GreaterThan(5000));
            Assert.That(stranded.Count, Is.LessThanOrEqualTo(total / 500),
                $"{stranded.Count} of {total} buildings are more than {ReachMetres} m from a road, e.g. " +
                string.Join(" ", stranded.GetRange(0, Math.Min(12, stranded.Count))));
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using Airside.Simulation;
using UnityEngine;

namespace Airside.Presentation
{
    /// <summary>
    /// Where an aircraft turns round at a regional outstation: the mapped apron (and the terminal it faces) read
    /// from the same offline airport map the flight world draws (<see cref="AirsideFlightAirportEnvironment"/>).
    /// An airport with no map or no apron has no spot, and its aircraft stay at the end of the landing roll as before.
    /// </summary>
    public static class RegionalApron
    {
        /// <summary>An apron farther than this from the strip is not the strip's apron (a taxi would be unbelievable).</summary>
        public const double MaximumDistanceMetres = 2500.0;

        private static readonly Dictionary<string, TurnaroundSpot?> Cache = new();

        public static bool TryFor(RegionalRunway runway, out TurnaroundSpot spot)
        {
            if (Cache.TryGetValue(runway.Code, out var cached))
            {
                spot = cached ?? default;
                return cached.HasValue;
            }

            var found = Load(runway);
            Cache[runway.Code] = found;
            spot = found ?? default;
            return found.HasValue;
        }

        private static TurnaroundSpot? Load(RegionalRunway runway)
        {
            try
            {
                var path = ArtRuntimePaths.ResolveExisting("Terrain/airport_" + runway.Code.ToLowerInvariant() + "_v01.json");
                if (path == null)
                    return null;
                var map = JsonUtility.FromJson<AirsideFlightAirportEnvironment.Map>(File.ReadAllText(path));
                if (map?.features == null)
                    return null;
                var midX = (runway.Ax + runway.Bx) * 0.5;
                var midZ = (runway.Az + runway.Bz) * 0.5;
                double[] apronX = null, apronZ = null;
                var best = double.MaxValue;
                var terminalX = new List<double>();
                var terminalZ = new List<double>();
                foreach (var feature in map.features)
                {
                    if (feature?.points == null || feature.points.Length < 3)
                        continue;
                    if (feature.kind == "terminal")
                    {
                        foreach (var point in feature.points)
                        {
                            YpadFrame.ToWorld(point.lat, point.lon, out var tx, out var tz);
                            terminalX.Add(tx);
                            terminalZ.Add(tz);
                        }
                    }
                    else if (feature.kind == "apron")
                    {
                        var xs = new double[feature.points.Length];
                        var zs = new double[feature.points.Length];
                        double cx = 0, cz = 0;
                        for (var i = 0; i < xs.Length; i++)
                        {
                            YpadFrame.ToWorld(feature.points[i].lat, feature.points[i].lon, out xs[i], out zs[i]);
                            cx += xs[i];
                            cz += zs[i];
                        }

                        cx /= xs.Length;
                        cz /= xs.Length;
                        var d = (cx - midX) * (cx - midX) + (cz - midZ) * (cz - midZ);
                        if (d < best)
                        {
                            best = d;
                            apronX = xs;
                            apronZ = zs;
                        }
                    }
                }

                if (apronX == null || Math.Sqrt(best) > MaximumDistanceMetres)
                    return null;
                RegionalTurnaround.RunwayYaws(runway, out var landingYaw, out _);
                return RegionalTurnaround.Spot(apronX, apronZ, terminalX.ToArray(), terminalZ.ToArray(), landingYaw);
            }
            catch (Exception e) when (e is IOException || e is ArgumentException || e is UnauthorizedAccessException)
            {
                Debug.LogWarning("[Airside turnaround] Airport apron unavailable: " + runway.Code);
                return null;
            }
        }
    }
}

using System;
using System.Collections.Generic;
using Airside.Simulation;

namespace Airside.Presentation
{
    /// <summary>One background person who walks a fixed line back and forth, on the simulation clock.</summary>
    public readonly struct AmbientWalker
    {
        public AmbientWalker(int id, float x0, float z0, float x1, float z1, float speed, float dwellSeconds,
            double phaseSeconds, int look, bool staff)
        {
            Id = id; X0 = x0; Z0 = z0; X1 = x1; Z1 = z1; Speed = speed; DwellSeconds = dwellSeconds;
            PhaseSeconds = phaseSeconds; Look = look; Staff = staff;
            var dx = x1 - x0;
            var dz = z1 - z0;
            Length = (float)Math.Sqrt(dx * dx + dz * dz);
        }

        public int Id { get; }
        public float X0 { get; }
        public float Z0 { get; }
        public float X1 { get; }
        public float Z1 { get; }
        public float Length { get; }
        public float Speed { get; }

        /// <summary>Time spent at each end: out of sight (through the door, into the car) for a passenger, standing for staff.</summary>
        public float DwellSeconds { get; }
        public double PhaseSeconds { get; }
        public int Look { get; }

        /// <summary>Hi-vis airside staff (stand at the ends) rather than a passenger (disappear at the ends).</summary>
        public bool Staff { get; }

        public double WalkSeconds => Speed > 0.01f ? Length / Speed : 0.0;
        public double PeriodSeconds => 2.0 * (WalkSeconds + DwellSeconds);
    }

    /// <summary>
    /// Background people for the ground around the terminal: passengers and staff walking between the car parks
    /// and the terminal's landside entrances (real OpenStreetMap entrance nodes and car-park bays), and hi-vis
    /// staff pacing the airside wall between terminal doors. A pure function of the simulation clock, so pausing,
    /// time scale and loading a save all stay correct and nothing is stored. Pure (no UnityEngine).
    /// </summary>
    public static class AdelaideAmbientPeople
    {
        public const int PassengersPerEntrance = 3;

        /// <summary>Landside entrances of the terminal, latitude/longitude (OpenStreetMap entrance nodes, level 0).</summary>
        private static readonly double[] EntranceLatLon =
        {
            -34.9382831, 138.5372492, -34.9382002, 138.537334, -34.938625, 138.5367172,
            -34.9388841, 138.5367192, -34.9380143, 138.537604, -34.9372251, 138.5393351
        };

        private static AmbientWalker[] _walkers;

        public static IReadOnlyList<AmbientWalker> Walkers => _walkers ??= Build();

        /// <summary>Where a walker is at <paramref name="seconds"/>; false while they are out of sight at an end.</summary>
        public static bool TryLocate(in AmbientWalker walker, double seconds, out float x, out float z,
            out float headingX, out float headingZ, out bool moving)
        {
            x = walker.X0;
            z = walker.Z0;
            headingX = headingZ = 0f;
            moving = false;
            var period = walker.PeriodSeconds;
            if (period <= 0.001 || walker.Length < 0.5f)
                return false;
            var t = (seconds + walker.PhaseSeconds) % period;
            if (t < 0)
                t += period;
            var walk = walker.WalkSeconds;
            var dx = (walker.X1 - walker.X0) / walker.Length;
            var dz = (walker.Z1 - walker.Z0) / walker.Length;
            double along;
            float sign;
            if (t < walk)
            {
                along = t * walker.Speed;
                sign = 1f;
                moving = true;
            }
            else if (t < walk + walker.DwellSeconds)
            {
                if (!walker.Staff)
                    return false;
                along = walker.Length;
                sign = 1f;
            }
            else if (t < 2.0 * walk + walker.DwellSeconds)
            {
                along = walker.Length - (t - walk - walker.DwellSeconds) * walker.Speed;
                sign = -1f;
                moving = true;
            }
            else
            {
                if (!walker.Staff)
                    return false;
                along = 0.0;
                sign = -1f;
            }

            x = walker.X0 + dx * (float)along;
            z = walker.Z0 + dz * (float)along;
            headingX = dx * sign;
            headingZ = dz * sign;
            return true;
        }

        private static AmbientWalker[] Build()
        {
            var list = new List<AmbientWalker>();
            var id = 0;
            for (var e = 0; e + 1 < EntranceLatLon.Length; e += 2)
            {
                YpadFrame.ToWorld(EntranceLatLon[e], EntranceLatLon[e + 1], out var ex, out var ez);
                var bays = CarBaysNear((float)ex, (float)ez, 150f);
                if (bays.Count < 4)
                    bays = CarBaysNear((float)ex, (float)ez, 230f);
                for (var k = 0; k < PassengersPerEntrance && bays.Count > 0; k++)
                {
                    var h = Hash(e * 31 + k);
                    var bay = bays[h % bays.Count];
                    var speed = 1.1f + h / 7 % 40 / 100f;
                    var dwell = 12f + h / 11 % 50;
                    list.Add(new AmbientWalker(id++, bay.X, bay.Z, (float)ex, (float)ez, speed, dwell,
                        h % 997, h / 13, staff: false));
                }
            }

            // Hi-vis staff pace the airside wall between neighbouring doors, pausing at each.
            var doors = AdelaideTerminalDoors.Doors;
            for (var d = 0; d + 1 < doors.Count; d++)
            {
                var a = doors[d];
                var b = doors[d + 1];
                // Only doors on the same wall: a straight line between them stays outside the building.
                if (a.NormalX * b.NormalX + a.NormalZ * b.NormalZ < 0.99f || Math.Abs(a.Z - b.Z) > 3f || Math.Abs(a.X - b.X) > 3f && Math.Abs(a.NormalZ) < 0.5f)
                    continue;
                var offset = 2.4f;
                for (var k = 0; k < 2; k++)
                {
                    var h = Hash(1000 + d * 7 + k);
                    var lateral = offset + k * 1.4f;
                    list.Add(new AmbientWalker(id++, a.ThresholdX + a.NormalX * lateral, a.ThresholdZ + a.NormalZ * lateral,
                        b.ThresholdX + b.NormalX * lateral, b.ThresholdZ + b.NormalZ * lateral,
                        1.2f + h % 25 / 100f, 6f + h / 5 % 14, h % 211, h / 17, staff: true));
                }
            }

            return list.ToArray();
        }

        /// <summary>Car-park bays 45 m to <paramref name="maxMetres"/> from a landside entrance and on the landside (north) of it.</summary>
        private static List<(float X, float Z)> CarBaysNear(float entranceX, float entranceZ, float maxMetres)
        {
            var found = new List<(float X, float Z)>();
            var runs = AdelaideCarParks.Runs;
            var stride = AdelaideCarParks.RunStride;
            for (var i = 0; i + stride <= runs.Length; i += stride)
            {
                var count = (int)runs[i + 4];
                for (var bay = 0; bay < count; bay += 3)
                {
                    var x = runs[i] + runs[i + 2] * AdelaideCarParks.BayPitchMetres * bay;
                    var z = runs[i + 1] + runs[i + 3] * AdelaideCarParks.BayPitchMetres * bay;
                    var dx = x - entranceX;
                    var dz = z - entranceZ;
                    var d2 = dx * dx + dz * dz;
                    if (d2 < 45f * 45f || d2 > maxMetres * maxMetres || z < entranceZ - 6f)
                        continue;
                    found.Add((x, z));
                }
            }

            return found;
        }

        private static int Hash(int value)
        {
            unchecked
            {
                var h = value * (int)2654435761u + 0x2545F491;
                h ^= h >> 15;
                h *= 0x2c1b3c6d;
                h ^= h >> 12;
                return h & int.MaxValue;
            }
        }
    }
}

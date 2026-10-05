using System;
using Airside.Domain;

namespace Airside.Simulation
{
    /// <summary>One parking spot on the helipad: where a helicopter stands and which way it faces.</summary>
    public readonly struct HelipadSpot
    {
        public HelipadSpot(string id, float x, float z, float headingDegrees)
        {
            Id = new StableId(id);
            X = x;
            Z = z;
            HeadingDegrees = headingDegrees;
        }

        public StableId Id { get; }
        public float X { get; }
        public float Z { get; }

        /// <summary>Compass-style heading in the ground frame: 0 is +Z, 90 is +X.</summary>
        public float HeadingDegrees { get; }
    }

    /// <summary>
    /// The OSM Helipad West beside the SA Ambulance rescue base, as stands (ADR 0186, 0207). The 37.9 m pad
    /// takes three Bell 412-class helicopters on a ring just inside the perimeter, rotor discs clear of each
    /// other, each facing outward. Helicopters park here instead of on a bay or gate and never taxi.
    /// </summary>
    public static class AdelaideHelipad
    {
        public const long OsmWayId = 1229789628;
        public const float PadCentreX = -302.737f;
        public const float PadCentreZ = 697.242f;
        public const float PadRadiusMetres = 18.94f;

        /// <summary>Spot distance from the pad centre: a 14 m rotor disc stays inside the 18.9 m pad.</summary>
        public const float SpotRadiusMetres = 8.4f;

        public static readonly HelipadSpot[] Spots =
        {
            Spot("HELI-1", 90f),
            Spot("HELI-2", 210f),
            Spot("HELI-3", 330f)
        };

        public static bool TryGetSpot(StableId stand, out HelipadSpot spot)
        {
            foreach (var candidate in Spots)
            {
                if (!candidate.Id.Equals(stand))
                    continue;
                spot = candidate;
                return true;
            }

            spot = default;
            return false;
        }

        public static bool IsHelipadStand(StableId stand) => TryGetSpot(stand, out _);

        /// <summary>Where a helicopter parked on <paramref name="stand"/> stands, facing outward.</summary>
        public static GroundPose SpotPose(StableId stand)
        {
            if (!TryGetSpot(stand, out var spot))
                throw new ArgumentException($"{stand} is not a helipad spot.", nameof(stand));
            var heading = spot.HeadingDegrees * Math.PI / 180.0;
            return new GroundPose(spot.X, spot.Z, (float)Math.Sin(heading), (float)Math.Cos(heading), 0f, false);
        }

        public static string SpotLabel(StableId stand) =>
            TryGetSpot(stand, out _) ? "Helipad spot " + stand.Value.Substring("HELI-".Length) : stand.Value;

        private static HelipadSpot Spot(string id, float compassDegrees)
        {
            var rad = compassDegrees * Math.PI / 180.0;
            return new HelipadSpot(id,
                PadCentreX + (float)Math.Sin(rad) * SpotRadiusMetres,
                PadCentreZ + (float)Math.Cos(rad) * SpotRadiusMetres,
                compassDegrees);
        }
    }
}

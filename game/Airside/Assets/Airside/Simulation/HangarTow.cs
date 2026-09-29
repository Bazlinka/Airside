using System;
using System.Collections.Generic;
using Airside.Domain;

namespace Airside.Simulation
{
    /// <summary>
    /// A check is done in a hangar (ADR 0186). While an aircraft is in its check it is towed along the taxiways to a
    /// hangar that can hold it, waits inside, and is towed back to its stand in time for the check to end.
    ///
    /// Presentation only: the simulation still holds the stand and the check timer, exactly as before, so no other
    /// aircraft can be given that stand and saves are unchanged. This class only answers "where is the aircraft, and
    /// which way is it facing, this many seconds into its check". The choice of hangar, the route and the timings are
    /// pure and deterministic.
    /// </summary>
    public static class HangarTow
    {
        /// <summary>Tug speed: about 11 kt on the straight, gentler in turns.</summary>
        public static readonly GroundSpeedLimits TowLimits = new(5.5f, 0.5f, 0.7f, 1.0f);

        /// <summary>The tug slews the aircraft between the stand heading and the tow heading over this distance.</summary>
        public const float SlewMetres = 18f;

        /// <summary>Distance in front of a hangar door where the tow route meets the taxiways.</summary>
        public const float ApronMetres = 25f;

        /// <summary>Shortest wait inside; a check that cannot fit the two tows and this stays on its stand.</summary>
        public const double MinimumInsideSeconds = 900;

        /// <summary>Room left round the aircraft inside a hangar.</summary>
        public const float ClearanceMetres = 2.5f;

        public sealed class Plan
        {
            public string HangarName;
            public string HangarId;
            public GroundPath Out;
            public GroundPath Back;
            public float InsideX, InsideZ, InsideNoseX, InsideNoseZ;
            public float LeaveX, LeaveZ;
            public float StandNoseX, StandNoseZ;
            /// <summary>Sideways positions (metres across the door, slot 0 first) where an aircraft of this type fits.</summary>
            public float[] Offsets;
            public int Slot;
            public string TypeId;
            public float Length, Span;
            public StableId Stand;
            public float SideX, SideZ;
            public double TowSeconds => Math.Max(Out.Seconds, Back.Seconds);

            /// <summary>How many aircraft of this type the hangar holds at once.</summary>
            public int Capacity => Offsets.Length;
        }

        private static readonly Dictionary<string, Plan[]> Cache = new(StringComparer.Ordinal);
        private static readonly Dictionary<string, Plan> SlotCache = new(StringComparer.Ordinal);
        private static readonly object Gate = new();

        /// <summary>
        /// Every hangar that can hold this type, best first (slot 0 of each). Empty when none can. Jets prefer Cobham,
        /// turboprops Regional Express, then the nearest.
        /// </summary>
        public static IReadOnlyList<Plan> Options(AircraftType type, StableId stand)
        {
            if (type == null || string.IsNullOrEmpty(stand.Value) || !AircraftCatalogue.TryFor(type, out var spec))
                return Array.Empty<Plan>();
            var key = type.Id + "|" + stand.Value;
            lock (Gate)
            {
                if (!Cache.TryGetValue(key, out var options))
                    Cache[key] = options = BuildOptions(spec, stand);
                return options;
            }
        }

        /// <summary>The preferred tow for this aircraft type from this stand, or false when no hangar can hold it.</summary>
        public static bool TryPlan(AircraftType type, StableId stand, out Plan plan)
        {
            var options = Options(type, stand);
            plan = options.Count > 0 ? options[0] : null;
            return plan != null;
        }

        /// <summary>The same tow for another berth of the same hangar (slot 0 is the door centre).</summary>
        public static Plan ForSlot(Plan option, int slot)
        {
            slot = Math.Max(0, Math.Min(slot, option.Capacity - 1));
            if (slot == 0)
                return option;
            var key = option.HangarId + "|" + option.TypeId + "|" + option.Stand.Value + "|" + slot;
            lock (Gate)
            {
                if (SlotCache.TryGetValue(key, out var found))
                    return found;
                foreach (var building in AdelaideBuildings.All)
                {
                    if (building.Id != option.HangarId)
                        continue;
                    TryDoor(building.Xz, option.Length, option.Span, out var door);
                    found = MakePlan(building, door, AdelaideGround.StandPose(option.Stand), option.TypeId, option.Stand, slot);
                    if (found != null)
                    {
                        found.Length = option.Length;
                        found.Span = option.Span;
                    }

                    break;
                }

                SlotCache[key] = found ?? option;
                return found ?? option;
            }
        }

        /// <summary>
        /// The pose <paramref name="secondsIn"/> seconds into a check that lasts <paramref name="checkSeconds"/>.
        /// False when the aircraft simply stays on its stand (no hangar fits, or the check is too short to tow).
        /// </summary>
        public static bool TryPose(AircraftType type, StableId stand, double secondsIn, double checkSeconds, out GroundPose pose) =>
            TryPose(type, stand, 0, 0, secondsIn, checkSeconds, out pose);

        /// <summary>As above, into hangar option <paramref name="hangar"/> and its berth <paramref name="slot"/>.</summary>
        public static bool TryPose(AircraftType type, StableId stand, int hangar, int slot, double secondsIn, double checkSeconds,
            out GroundPose pose)
        {
            pose = default;
            var options = Options(type, stand);
            if (options.Count == 0)
                return false;
            var plan = ForSlot(options[Math.Max(0, Math.Min(hangar, options.Count - 1))], slot);
            var tow = plan.TowSeconds;
            if (checkSeconds < 2 * tow + MinimumInsideSeconds)
                return false;
            secondsIn = Math.Max(0.0, Math.Min(secondsIn, checkSeconds));
            if (secondsIn <= 0.0 || secondsIn >= checkSeconds)
                return false;
            if (secondsIn < tow)
            {
                pose = Along(plan.Out, secondsIn, plan, fromStand: true);
                return true;
            }

            if (secondsIn > checkSeconds - tow)
            {
                pose = Along(plan.Back, secondsIn - (checkSeconds - tow), plan, fromStand: false);
                return true;
            }

            var half = (checkSeconds) * 0.5;
            pose = secondsIn < half
                ? new GroundPose(plan.InsideX, plan.InsideZ, plan.InsideNoseX, plan.InsideNoseZ, 0f, false)
                : new GroundPose(plan.LeaveX, plan.LeaveZ, -plan.InsideNoseX, -plan.InsideNoseZ, 0f, false);
            return true;
        }

        private static GroundPose Along(GroundPath path, double seconds, Plan plan, bool fromStand)
        {
            var t = Math.Max(0.0, Math.Min(seconds, path.Seconds));
            var here = path.SampleAt(t);
            var metres = path.DistanceAt(t);
            var nx = here.DirectionX;
            var nz = here.DirectionZ;
            // Near the stand end the tug slews the aircraft between the tow heading and the stand heading.
            var fromStandEnd = fromStand ? metres : path.Length - metres;
            if (fromStandEnd < SlewMetres)
            {
                var k = 1f - fromStandEnd / SlewMetres;
                k = k * k * (3f - 2f * k);
                nx = nx * (1f - k) + plan.StandNoseX * k;
                nz = nz * (1f - k) + plan.StandNoseZ * k;
                var len = (float)Math.Sqrt(nx * nx + nz * nz);
                if (len > 1e-4f)
                {
                    nx /= len;
                    nz /= len;
                }
                else
                {
                    nx = here.DirectionX;
                    nz = here.DirectionZ;
                }
            }

            var speed = t <= 0.0 || t >= path.Seconds ? 0f : here.Speed;
            return new GroundPose(here.X, here.Z, nx, nz, speed, false);
        }

        private static Plan[] BuildOptions(AircraftSpec spec, StableId stand)
        {
            var pose = AdelaideGround.StandPose(stand);
            var length = (float)spec.LengthMetres;
            var span = (float)spec.WingspanMetres;
            var wants = spec.WingspanMetres > 30.0 ? "Cobham" : "Regional Express";

            var scored = new List<(float Score, AdelaideBuilding Building, Door Door)>();
            foreach (var building in AdelaideBuildings.All)
            {
                if (building.Kind != AdelaideBuildingKind.Hangar || building.Xz == null || building.Xz.Length < 6)
                    continue;
                if (!TryDoor(building.Xz, length, span, out var door))
                    continue;
                var dx = door.CentreX - pose.X;
                var dz = door.CentreZ - pose.Z;
                var score = (float)Math.Sqrt(dx * dx + dz * dz);
                if (building.Name.StartsWith(wants, StringComparison.Ordinal))
                    score -= 100000f;
                scored.Add((score, building, door));
            }

            scored.Sort((a, b) =>
            {
                var c = a.Score.CompareTo(b.Score);
                return c != 0 ? c : string.CompareOrdinal(a.Building.Id, b.Building.Id);
            });
            var plans = new List<Plan>();
            foreach (var (_, building, door) in scored)
            {
                var plan = MakePlan(building, door, pose, spec.Id, stand, 0);
                if (plan == null)
                    continue;
                plan.Length = length;
                plan.Span = span;
                plans.Add(plan);
            }

            return plans.ToArray();
        }

        private struct Door
        {
            public float CentreX, CentreZ;
            public float DirX, DirZ;
            public float HalfDepth;
            public float[] Offsets;
        }

        /// <summary>
        /// The best side of a hangar to bring an aircraft in by: the side nearest a taxiway from which the aircraft (nose in,
        /// tail toward the door) and its wing tips lie inside the outline with room to spare. Also lists the extra berths
        /// (side by side across the door) that fit.
        /// </summary>
        private static bool TryDoor(float[] xz, float length, float span, out Door door)
        {
            door = default;
            float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue;
            for (var i = 0; i < xz.Length; i += 2)
            {
                minX = Math.Min(minX, xz[i]);
                maxX = Math.Max(maxX, xz[i]);
                minZ = Math.Min(minZ, xz[i + 1]);
                maxZ = Math.Max(maxZ, xz[i + 1]);
            }

            var cx = (minX + maxX) * 0.5f;
            var cz = (minZ + maxZ) * 0.5f;
            var found = false;
            var bestDistance = float.MaxValue;
            for (var side = 0; side < 4; side++)
            {
                float dirX = side == 0 ? 1f : side == 1 ? -1f : 0f;
                float dirZ = side == 2 ? 1f : side == 3 ? -1f : 0f;
                var halfDepth = dirX != 0f ? (maxX - minX) * 0.5f : (maxZ - minZ) * 0.5f;
                if (halfDepth * 2f < length + 2f * ClearanceMetres)
                    continue;
                if (!Fits(xz, cx, cz, dirX, dirZ, halfDepth, length, span, 0f))
                    continue;
                var frontX = cx + dirX * (halfDepth + ApronMetres);
                var frontZ = cz + dirZ * (halfDepth + ApronMetres);
                var distance = AdelaideTaxiRouter.DistanceToCentreline(frontX, frontZ);
                if (distance >= bestDistance)
                    continue;
                bestDistance = distance;
                var offsets = new List<float> { 0f };
                var pitch = span + 3f;
                foreach (var candidate in new[] { pitch, -pitch, 2f * pitch, -2f * pitch })
                    if (offsets.Count < MaxBerths && Fits(xz, cx, cz, dirX, dirZ, halfDepth, length, span, candidate))
                        offsets.Add(candidate);
                door = new Door { CentreX = cx, CentreZ = cz, DirX = dirX, DirZ = dirZ, HalfDepth = halfDepth, Offsets = offsets.ToArray() };
                found = true;
            }

            return found;
        }

        /// <summary>The most aircraft a hangar holds side by side.</summary>
        public const int MaxBerths = 3;

        private static bool Fits(float[] xz, float cx, float cz, float dirX, float dirZ, float halfDepth, float length, float span,
            float offset)
        {
            var sideX = -dirZ;
            var sideZ = dirX;
            // Nose datum deep in the hangar, tail toward the door, offset across the door.
            var noseX = cx - dirX * (halfDepth - ClearanceMetres) + sideX * offset;
            var noseZ = cz - dirZ * (halfDepth - ClearanceMetres) + sideZ * offset;
            var tailX = noseX + dirX * length;
            var tailZ = noseZ + dirZ * length;
            var half = span * 0.5f + ClearanceMetres;
            var mx = (noseX + tailX) * 0.5f;
            var mz = (noseZ + tailZ) * 0.5f;
            return Inside(xz, noseX, noseZ) && Inside(xz, tailX, tailZ)
                && Inside(xz, mx + sideX * half, mz + sideZ * half) && Inside(xz, mx - sideX * half, mz - sideZ * half);
        }

        private static Plan MakePlan(AdelaideBuilding building, Door door, GroundPose stand, string typeId, StableId standId, int slot)
        {
            var offset = door.Offsets[Math.Min(slot, door.Offsets.Length - 1)];
            var sideX = -door.DirZ;
            var sideZ = door.DirX;
            var frontX = door.CentreX + door.DirX * (door.HalfDepth + ApronMetres) + sideX * offset;
            var frontZ = door.CentreZ + door.DirZ * (door.HalfDepth + ApronMetres) + sideZ * offset;
            var route = AdelaideTaxiRouter.Route(stand.X, stand.Z, frontX, frontZ);
            if (route == null || route.Length < 4)
                return null;

            var noseX = door.CentreX - door.DirX * (door.HalfDepth - ClearanceMetres) + sideX * offset;
            var noseZ = door.CentreZ - door.DirZ * (door.HalfDepth - ClearanceMetres) + sideZ * offset;
            var leaveX = door.CentreX + door.DirX * (door.HalfDepth - ClearanceMetres) + sideX * offset;
            var leaveZ = door.CentreZ + door.DirZ * (door.HalfDepth - ClearanceMetres) + sideZ * offset;

            var outbound = new List<float>(route) { noseX, noseZ };
            var back = new List<float> { leaveX, leaveZ };
            for (var i = route.Length - 2; i >= 0; i -= 2)
            {
                back.Add(route[i]);
                back.Add(route[i + 1]);
            }

            return new Plan
            {
                HangarName = building.Name,
                HangarId = building.Id,
                Out = new GroundPath(outbound.ToArray(), TowLimits),
                Back = new GroundPath(back.ToArray(), TowLimits),
                InsideX = noseX,
                InsideZ = noseZ,
                InsideNoseX = -door.DirX,
                InsideNoseZ = -door.DirZ,
                LeaveX = leaveX,
                LeaveZ = leaveZ,
                StandNoseX = stand.NoseX,
                StandNoseZ = stand.NoseZ,
                Offsets = door.Offsets,
                Slot = slot,
                TypeId = typeId,
                Stand = standId,
                SideX = sideX,
                SideZ = sideZ
            };
        }

        private static bool Inside(float[] xz, float x, float z)
        {
            var inside = false;
            var n = xz.Length / 2;
            for (int i = 0, j = n - 1; i < n; j = i++)
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
    }
}

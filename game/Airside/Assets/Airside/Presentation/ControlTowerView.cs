using System;
using Airside.Simulation;

namespace Airside.Presentation
{
    /// <summary>
    /// The Adelaide control-tower viewpoint: where the controller's eye sits in the cab and whether a
    /// screen ray touches the tower. Pure numbers (no UnityEngine) so it is tested headlessly; the
    /// camera and click wiring live in AirsidePrototype.Tower.cs.
    /// </summary>
    public static class ControlTowerView
    {
        /// <summary>Eye height above the cab floor slab, standing.</summary>
        public const float EyeAboveFloorMetres = 1.65f;
        /// <summary>Click target radius around the tower axis; the shaft is narrower, so this is forgiving.</summary>
        public const float PickRadiusMetres = 9f;
        public const float TopExtraMetres = 12f;

        public readonly struct Pose
        {
            public readonly float X, Y, Z, FacingDegrees, GroundY, TopY;
            public Pose(float x, float y, float z, float facingDegrees, float groundY, float topY)
            { X = x; Y = y; Z = z; FacingDegrees = facingDegrees; GroundY = groundY; TopY = topY; }
        }

        public static bool TryFind(out AdelaideBuilding tower)
        {
            foreach (var building in AdelaideBuildings.All)
                if (building.Kind == AdelaideBuildingKind.ControlTower) { tower = building; return true; }
            tower = default;
            return false;
        }

        /// <summary>Eye pose for a tower whose footprint mean sits at ground height <paramref name="groundY"/>.</summary>
        public static Pose PoseFor(AdelaideBuilding tower, float groundY, float lookAtX, float lookAtZ)
        {
            Centre(tower.Xz, out var cx, out var cz);
            var eyeY = groundY + tower.HeightMetres * BuildingDetail.TowerShaftFraction
                + BuildingDetail.TowerCabFloorMetres + EyeAboveFloorMetres;
            var yaw = (float)(Math.Atan2(lookAtX - cx, lookAtZ - cz) * 180.0 / Math.PI);
            return new Pose(cx, eyeY, cz, yaw, groundY, groundY + tower.HeightMetres + TopExtraMetres);
        }

        public static void Centre(float[] xz, out float cx, out float cz)
        {
            double sx = 0, sz = 0;
            var n = xz.Length / 2;
            for (var i = 0; i < n; i++) { sx += xz[i * 2]; sz += xz[i * 2 + 1]; }
            cx = n == 0 ? 0f : (float)(sx / n);
            cz = n == 0 ? 0f : (float)(sz / n);
        }

        /// <summary>
        /// Does a ray touch the upright cylinder around the tower? Returns the distance along the
        /// (unit-length) ray to the point where it first lies inside the cylinder's height band.
        /// </summary>
        public static bool RayHits(Pose pose, float ox, float oy, float oz, float dx, float dy, float dz, float maxDistance, out float distance)
        {
            distance = 0f;
            var a = dx * dx + dz * dz;
            if (a < 1e-9f) return false;
            var px = ox - pose.X;
            var pz = oz - pose.Z;
            var b = 2f * (px * dx + pz * dz);
            var c = px * px + pz * pz - PickRadiusMetres * PickRadiusMetres;
            var disc = b * b - 4f * a * c;
            if (disc < 0f) return false;
            var root = (float)Math.Sqrt(disc);
            var t0 = (-b - root) / (2f * a);
            var t1 = (-b + root) / (2f * a);
            if (t1 < 0f) return false;
            // Inside the infinite cylinder for t in [t0, t1]; accept when that span overlaps the height band.
            var tEnter = Math.Max(0f, t0);
            var yA = oy + dy * tEnter;
            var yB = oy + dy * t1;
            var lo = Math.Min(yA, yB);
            var hi = Math.Max(yA, yB);
            if (hi < pose.GroundY || lo > pose.TopY) return false;
            if (tEnter > maxDistance) return false;
            distance = tEnter;
            return true;
        }
    }
}

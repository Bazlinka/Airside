using System;

namespace Airside.Presentation
{
    /// <summary>Repeatable rainwater and roof-edge fittings on the existing surveyed shells.</summary>
    public static class BuildingFacadeModules
    {
        public const float DownpipePitchMetres = 18f;

        public static void Add(BuildingDetailSet set, float[] footprint, float baseY, float height)
        {
            if (set == null || footprint == null || footprint.Length < 6 || height < 3f) return;
            var area = 0f;
            var count = footprint.Length / 2;
            for (var i = 0; i < count; i++)
            {
                var j = (i + 1) % count;
                area += footprint[i * 2] * footprint[j * 2 + 1] - footprint[j * 2] * footprint[i * 2 + 1];
            }
            for (var i = 0; i < count; i++)
            {
                var j = (i + 1) % count;
                var ax = footprint[i * 2]; var az = footprint[i * 2 + 1];
                var dx = footprint[j * 2] - ax; var dz = footprint[j * 2 + 1] - az;
                var length = (float)Math.Sqrt(dx * dx + dz * dz);
                if (length < 5f) continue;
                dx /= length; dz /= length;
                var nx = area >= 0f ? dz : -dz;
                var nz = area >= 0f ? -dx : dx;
                // Gutter and underside create a slim, shaded edge rather than another solid roof slab.
                set.Boxes.Add(new DetailBox(BuildingPart.Trim, ax + dx * length * .5f + nx * .12f,
                    baseY + height - .22f, az + dz * length * .5f + nz * .12f,
                    length - .3f, .16f, .20f, dx, dz));
                var bays = Math.Max(1, (int)Math.Ceiling(length / DownpipePitchMetres));
                for (var b = 0; b <= bays; b++)
                {
                    // Omit bays occupied by a real doorway; fittings add no collision geometry.
                    var along = .45f + (length - .9f) * b / bays;
                    var obstructed = false;
                    foreach (var opening in set.Openings)
                        if (opening.EdgeIndex == i && along >= opening.FromMetres - .35f && along <= opening.ToMetres + .35f)
                            obstructed = true;
                    if (obstructed) continue;
                    var x = ax + dx * along + nx * .18f;
                    var z = az + dz * along + nz * .18f;
                    var h = height - .65f;
                    set.Boxes.Add(new DetailBox(BuildingPart.Trim, x, baseY + .2f + h * .5f, z,
                        .11f, h, .11f, dx, dz));
                    for (var y = 1.1f; y < h; y += 2.3f)
                        set.Boxes.Add(new DetailBox(BuildingPart.Trim, x, baseY + y, z,
                            .17f, .035f, .14f, dx, dz));
                }
            }
        }
    }
}

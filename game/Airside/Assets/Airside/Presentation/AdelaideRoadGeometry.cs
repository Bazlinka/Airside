using System;
using System.Collections.Generic;
using Airside.Simulation;

namespace Airside.Presentation
{
    /// <summary>What an airside road does at a point: draw asphalt, only paint lines (it lies on apron concrete), or nothing.</summary>
    public enum RoadSurfaceUse
    {
        Asphalt,
        PaintOnly,
        Skip
    }

    /// <summary>Linear RGBA, as the Surroundings shader reads vertex colour (alpha 0 blends toward the satellite photo).</summary>
    public readonly struct RoadColor
    {
        public readonly float R, G, B, A;

        public RoadColor(float r, float g, float b, float a)
        {
            R = r;
            G = g;
            B = b;
            A = a;
        }

        public static RoadColor Srgb(float r, float g, float b, float a) =>
            new RoadColor(ToLinear(r), ToLinear(g), ToLinear(b), a);

        private static float ToLinear(float c) =>
            c <= 0.04045f ? c / 12.92f : (float)Math.Pow((c + 0.055f) / 1.055f, 2.4f);
    }

    /// <summary>One mesh tile's worth of road geometry: flat x,y,z / r,g,b,a / index lists.</summary>
    public sealed class RoadMeshTile
    {
        public readonly List<float> Positions = new List<float>(4096);
        public readonly List<float> Colors = new List<float>(4096);
        public readonly List<float> Normals = new List<float>(4096);
        public readonly List<int> Triangles = new List<int>(8192);
        public int VertexCount => Positions.Count / 3;
    }

    /// <summary>Collects road quads and fans into 1.5 km tiles (by centroid) so the renderer can cull them.</summary>
    public sealed class RoadMeshSink
    {
        public const float TileMetres = 1500f;
        private readonly Dictionary<long, RoadMeshTile> _tiles = new Dictionary<long, RoadMeshTile>();

        public IEnumerable<KeyValuePair<long, RoadMeshTile>> Tiles => _tiles;
        public int TileCount => _tiles.Count;
        public int VertexCount { get; private set; }
        public int TriangleCount { get; private set; }

        private RoadMeshTile TileAt(float x, float z)
        {
            var ix = (int)Math.Floor(x / TileMetres);
            var iz = (int)Math.Floor(z / TileMetres);
            var key = ((long)ix << 32) | (uint)iz;
            if (!_tiles.TryGetValue(key, out var tile))
            {
                tile = new RoadMeshTile();
                _tiles[key] = tile;
            }

            return tile;
        }

        private static void AddVertex(RoadMeshTile t, float x, float y, float z, RoadColor c,
            float nx = 0f, float ny = 1f, float nz = 0f)
        {
            t.Normals.Add(nx);
            t.Normals.Add(ny);
            t.Normals.Add(nz);
            t.Positions.Add(x);
            t.Positions.Add(y);
            t.Positions.Add(z);
            t.Colors.Add(c.R);
            t.Colors.Add(c.G);
            t.Colors.Add(c.B);
            t.Colors.Add(c.A);
        }

        /// <summary>A quad a, b (one side) and c, d (the next), wound so its normal points up.</summary>
        public void Quad(float ax, float ay, float az, float bx, float by, float bz,
            float cx, float cy, float cz, float dx, float dy, float dz, RoadColor color)
        {
            var t = TileAt((ax + bx + cx + dx) * 0.25f, (az + bz + cz + dz) * 0.25f);
            var i = t.VertexCount;
            AddVertex(t, ax, ay, az, color);
            AddVertex(t, bx, by, bz, color);
            AddVertex(t, cx, cy, cz, color);
            AddVertex(t, dx, dy, dz, color);
            t.Triangles.Add(i);
            t.Triangles.Add(i + 2);
            t.Triangles.Add(i + 1);
            t.Triangles.Add(i + 1);
            t.Triangles.Add(i + 2);
            t.Triangles.Add(i + 3);
            VertexCount += 4;
            TriangleCount += 2;
        }

        /// <summary>One up-facing triangle (a, b, c in any winding; it is flipped to face up).</summary>
        public void Tri(float ax, float ay, float az, float bx, float by, float bz, float cx, float cy, float cz,
            RoadColor color)
        {
            var t = TileAt((ax + bx + cx) / 3f, (az + bz + cz) / 3f);
            var i = t.VertexCount;
            var up = (bz - az) * (cx - ax) - (bx - ax) * (cz - az) >= 0f;
            AddVertex(t, ax, ay, az, color);
            AddVertex(t, up ? bx : cx, up ? by : cy, up ? bz : cz, color);
            AddVertex(t, up ? cx : bx, up ? cy : by, up ? cz : bz, color);
            t.Triangles.Add(i);
            t.Triangles.Add(i + 1);
            t.Triangles.Add(i + 2);
            VertexCount += 3;
            TriangleCount += 1;
        }

        /// <summary>
        /// A closed-topped box without a bottom, standing on y0: centre (cx, cz), long axis (ux, uz) (unit), half sizes
        /// along and across it, and a height. Flat-shaded: each face has its own vertices and outward normal.
        /// </summary>
        public void Box(float cx, float y0, float cz, float ux, float uz, float halfLength, float halfWidth,
            float height, RoadColor color)
        {
            var vx = -uz;
            var vz = ux;
            float Px(float a, float b) => cx + ux * a + vx * b;
            float Pz(float a, float b) => cz + uz * a + vz * b;
            var y1 = y0 + height;
            var t = TileAt(cx, cz);
            // top
            Face(t, color, 0f, 1f, 0f,
                Px(-halfLength, -halfWidth), y1, Pz(-halfLength, -halfWidth), Px(halfLength, -halfWidth), y1, Pz(halfLength, -halfWidth),
                Px(halfLength, halfWidth), y1, Pz(halfLength, halfWidth), Px(-halfLength, halfWidth), y1, Pz(-halfLength, halfWidth));
            // four sides
            Face(t, color, ux, 0f, uz,
                Px(halfLength, -halfWidth), y0, Pz(halfLength, -halfWidth), Px(halfLength, halfWidth), y0, Pz(halfLength, halfWidth),
                Px(halfLength, halfWidth), y1, Pz(halfLength, halfWidth), Px(halfLength, -halfWidth), y1, Pz(halfLength, -halfWidth));
            Face(t, color, -ux, 0f, -uz,
                Px(-halfLength, halfWidth), y0, Pz(-halfLength, halfWidth), Px(-halfLength, -halfWidth), y0, Pz(-halfLength, -halfWidth),
                Px(-halfLength, -halfWidth), y1, Pz(-halfLength, -halfWidth), Px(-halfLength, halfWidth), y1, Pz(-halfLength, halfWidth));
            Face(t, color, vx, 0f, vz,
                Px(halfLength, halfWidth), y0, Pz(halfLength, halfWidth), Px(-halfLength, halfWidth), y0, Pz(-halfLength, halfWidth),
                Px(-halfLength, halfWidth), y1, Pz(-halfLength, halfWidth), Px(halfLength, halfWidth), y1, Pz(halfLength, halfWidth));
            Face(t, color, -vx, 0f, -vz,
                Px(-halfLength, -halfWidth), y0, Pz(-halfLength, -halfWidth), Px(halfLength, -halfWidth), y0, Pz(halfLength, -halfWidth),
                Px(halfLength, -halfWidth), y1, Pz(halfLength, -halfWidth), Px(-halfLength, -halfWidth), y1, Pz(-halfLength, -halfWidth));
        }

        /// <summary>A flat-shaded cylinder standing on y0 with a top cap (no bottom): <paramref name="sides"/> faces.</summary>
        public void Cylinder(float cx, float y0, float cz, float radius, float height, int sides, RoadColor color)
        {
            var t = TileAt(cx, cz);
            var y1 = y0 + height;
            var ring = new float[sides * 3];
            for (var k = 0; k < sides; k++)
            {
                var a0 = k * 2f * (float)Math.PI / sides;
                var a1 = (k + 1) * 2f * (float)Math.PI / sides;
                var mid = (a0 + a1) * 0.5f;
                var nx = (float)Math.Cos(mid);
                var nz = (float)Math.Sin(mid);
                float x0 = cx + (float)Math.Cos(a0) * radius, z0 = cz + (float)Math.Sin(a0) * radius;
                float x1 = cx + (float)Math.Cos(a1) * radius, z1 = cz + (float)Math.Sin(a1) * radius;
                Face(t, color, nx, 0f, nz, x0, y0, z0, x1, y0, z1, x1, y1, z1, x0, y1, z0);
                ring[k * 3] = x0;
                ring[k * 3 + 1] = y1;
                ring[k * 3 + 2] = z0;
            }

            Fan(cx, y1, cz, ring, color);
        }

        /// <summary>A quad q0..q3 in cyclic order, wound so its geometric normal agrees with (nx, ny, nz).</summary>
        private void Face(RoadMeshTile t, RoadColor color, float nx, float ny, float nz,
            float x0, float y0, float z0, float x1, float y1, float z1,
            float x2, float y2, float z2, float x3, float y3, float z3)
        {
            var e1x = x1 - x0;
            var e1y = y1 - y0;
            var e1z = z1 - z0;
            var e2x = x2 - x0;
            var e2y = y2 - y0;
            var e2z = z2 - z0;
            var gx = e1y * e2z - e1z * e2y;
            var gy = e1z * e2x - e1x * e2z;
            var gz = e1x * e2y - e1y * e2x;
            var agrees = gx * nx + gy * ny + gz * nz > 0f;
            var i = t.VertexCount;
            AddVertex(t, x0, y0, z0, color, nx, ny, nz);
            AddVertex(t, x1, y1, z1, color, nx, ny, nz);
            AddVertex(t, x2, y2, z2, color, nx, ny, nz);
            AddVertex(t, x3, y3, z3, color, nx, ny, nz);
            if (agrees)
            {
                t.Triangles.Add(i); t.Triangles.Add(i + 1); t.Triangles.Add(i + 2);
                t.Triangles.Add(i); t.Triangles.Add(i + 2); t.Triangles.Add(i + 3);
            }
            else
            {
                t.Triangles.Add(i); t.Triangles.Add(i + 2); t.Triangles.Add(i + 1);
                t.Triangles.Add(i); t.Triangles.Add(i + 3); t.Triangles.Add(i + 2);
            }

            VertexCount += 4;
            TriangleCount += 2;
        }

        /// <summary>A disc: centre plus a ring of x,y,z triples in increasing angle (x toward z).</summary>
        public void Fan(float cx, float cy, float cz, float[] ring, RoadColor color)
        {
            var n = ring.Length / 3;
            var t = TileAt(cx, cz);
            var i = t.VertexCount;
            AddVertex(t, cx, cy, cz, color);
            for (var k = 0; k < n; k++)
                AddVertex(t, ring[k * 3], ring[k * 3 + 1], ring[k * 3 + 2], color);
            for (var k = 0; k < n; k++)
            {
                t.Triangles.Add(i);
                t.Triangles.Add(i + 1 + (k + 1) % n);
                t.Triangles.Add(i + 1 + k);
            }

            VertexCount += n + 1;
            TriangleCount += n;
        }
    }

    public sealed class RoadBuildOptions
    {
        /// <summary>Surface height at x, z. Null puts every road on <see cref="BaseY"/>.</summary>
        public Func<float, float, float> GroundHeight;
        public float BaseY;
        public float YOffset = 0.08f;
        /// <summary>Applied to airside roads and junctions near the aprons; null means always asphalt.</summary>
        public Func<float, float, RoadSurfaceUse> AirsideRule;
        public float MaxSegmentMetres = 12f;

        internal float Height(float x, float z) => GroundHeight != null ? GroundHeight(x, z) : BaseY;

        internal RoadSurfaceUse Rule(float x, float z) =>
            AirsideRule != null ? AirsideRule(x, z) : RoadSurfaceUse.Asphalt;
    }

    /// <summary>
    /// ADR 0184 — the complete road network as flat geometry: mitred ribbons that follow the ground,
    /// junction discs, roundabout-safe loops, lane and edge paint that stops at side roads, and zebra
    /// crossings. Pure (no UnityEngine), so the headless harness checks it; the Unity side only
    /// copies the tiles into meshes.
    /// </summary>
    public static class AdelaideRoadGeometry
    {
        public const float MaxMiter = 1f / 0.6f;
        /// <summary>cos of the bend past which a ribbon is cut and joined with a disc (60 degrees).</summary>
        public const float SharpTurnDot = 0.5f;
        public const float JunctionDiscMinDegree = 3f;
        public const float TurningCircleRadius = 6f;
        public const float NearAlpha = 0.5f;
        public const float FarAlpha = 0.1f;
        public const float PaintLift = 0.03f;
        public const float DashMetres = 5f;
        public const float GapMetres = 5f;
        private const int DiscSides = 12;
        /// <summary>Points closer than this to the last kept one are dropped from a ribbon: mitring tiny segments folds it.</summary>
        public const float MinSegmentMetres = 0.75f;

        private static readonly RoadColor PaintWhite = RoadColor.Srgb(0.9f, 0.9f, 0.87f, 1f);

        // --- asphalt ---

        /// <summary>Ribbons for every drivable road, plus junction discs and turning circles. Returns roads drawn.</summary>
        public static int BuildAsphalt(RoadMeshSink sink, RoadBuildOptions o)
        {
            var drawn = 0;
            var roads = AdelaideRoadNetwork.Roads;
            for (var r = 0; r < roads.Length; r++)
            {
                var road = roads[r];
                if (road.Layer < 0 || (road.Flags & AdelaideRoadNetwork.RoadFlags.Tunnel) != 0)
                    continue;
                if (BuildRibbon(sink, o, road))
                    drawn++;
            }

            BuildJunctionDiscs(sink, o);
            BuildTurningCircles(sink, o);
            return drawn;
        }

        public static RoadColor AsphaltFor(AdelaideRoadNetwork.Road road, float alpha)
        {
            switch (road.Surface)
            {
                case AdelaideRoadNetwork.SurfaceKind.Concrete: return RoadColor.Srgb(0.56f, 0.56f, 0.54f, alpha);
                case AdelaideRoadNetwork.SurfaceKind.Unpaved: return RoadColor.Srgb(0.52f, 0.45f, 0.34f, alpha);
                case AdelaideRoadNetwork.SurfaceKind.Pavers: return RoadColor.Srgb(0.55f, 0.42f, 0.36f, alpha);
            }

            if (road.IsAirside)
                return RoadColor.Srgb(0.36f, 0.37f, 0.38f, alpha);
            return AsphaltForClass(road.Class, alpha);
        }

        private static RoadColor AsphaltForClass(AdelaideRoadNetwork.RoadClass cls, float alpha)
        {
            if (cls <= AdelaideRoadNetwork.RoadClass.Primary)
                return RoadColor.Srgb(0.24f, 0.26f, 0.27f, alpha);
            if (cls <= AdelaideRoadNetwork.RoadClass.Tertiary)
                return RoadColor.Srgb(0.27f, 0.29f, 0.30f, alpha);
            return RoadColor.Srgb(0.31f, 0.33f, 0.34f, alpha);
        }

        public static float AlphaAt(float x, float z) =>
            Math.Abs(x) < 3500f && Math.Abs(z) < 2500f ? NearAlpha : FarAlpha;

        private static bool BuildRibbon(RoadMeshSink sink, RoadBuildOptions o, AdelaideRoadNetwork.Road road)
        {
            // Airside roads are cut finely because the pavement rule decides per piece; public roads only where the
            // ground actually bends, so the flat plain costs a quad per OSM segment, not one per 12 m.
            var dense = road.IsAirside
                ? Densify(road.PointStart, road.PointCount, o.MaxSegmentMetres)
                : AdaptiveDensify(road.PointStart, road.PointCount, o);
            var pts = DropTiny(dense, MinSegmentMetres);
            var n = pts.Count / 2;
            if (n < 2)
                return false;

            var alpha = AlphaAt(pts[(n / 2) * 2], pts[(n / 2) * 2 + 1]);
            return RibbonFromPoints(sink, o, pts, road.Width, AsphaltFor(road, alpha), road.IsAirside);
        }

        /// <summary>
        /// A ribbon of the given width along x, z pairs (already densified): mitred bends, round joints at sharp ones,
        /// a disc for a run no bigger than two widths. Used for roads and for the precinct's footpaths.
        /// </summary>
        public static bool RibbonFromPoints(RoadMeshSink sink, RoadBuildOptions o, List<float> pts, float width,
            RoadColor color, bool airside)
        {
            var n = pts.Count / 2;
            if (n < 2)
                return false;
            var half = width * 0.5f;

            // A road no bigger than a couple of its own widths (a cul-de-sac loop, a turning bay) is a disc: a ribbon
            // around it would be narrower than it is wide and fold inside out.
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            for (var i = 0; i < n; i++)
            {
                minX = Math.Min(minX, pts[i * 2]);
                maxX = Math.Max(maxX, pts[i * 2]);
                minZ = Math.Min(minZ, pts[i * 2 + 1]);
                maxZ = Math.Max(maxZ, pts[i * 2 + 1]);
            }

            var extent = Math.Max(maxX - minX, maxZ - minZ);
            if (extent < width * 2f)
            {
                var cx = (minX + maxX) * 0.5f;
                var cz = (minZ + maxZ) * 0.5f;
                if (airside && o.Rule(cx, cz) != RoadSurfaceUse.Asphalt)
                    return false;
                Disc(sink, o, cx, cz, extent * 0.5f + half, color);
                return true;
            }

            // A mitred ribbon folds over itself on its inside edge at a sharp bend, so the road is cut into pieces at
            // any bend past SharpTurnDot and the joint is a round disc instead.
            var any = false;
            var first = 0;
            for (var i = 1; i < n; i++)
            {
                var last = i == n - 1;
                var sharp = false;
                if (!last)
                {
                    var da = Direction(pts[i * 2 - 2], pts[i * 2 - 1], pts[i * 2], pts[i * 2 + 1]);
                    var db = Direction(pts[i * 2], pts[i * 2 + 1], pts[i * 2 + 2], pts[i * 2 + 3]);
                    var dot = da.Item1 * db.Item1 + da.Item2 * db.Item2;
                    sharp = dot < SharpTurnDot;
                    if (!sharp && dot < 0.999f)
                    {
                        // The inside edge slides half * tan(bend / 2) along each segment; past the shorter one's
                        // length it folds over, however gentle the bend.
                        var shorter = Math.Min(Dist(pts[i * 2 - 2], pts[i * 2 - 1], pts[i * 2], pts[i * 2 + 1]),
                            Dist(pts[i * 2], pts[i * 2 + 1], pts[i * 2 + 2], pts[i * 2 + 3]));
                        var slide = half * (float)Math.Sqrt((1f - dot) / (1f + dot));
                        sharp = slide > 0.45f * shorter;
                    }
                }

                if (!last && !sharp)
                    continue;
                any |= RibbonPiece(sink, o, pts, first, i, half, color, airside);
                if (sharp)
                {
                    var x = pts[i * 2];
                    var z = pts[i * 2 + 1];
                    if (!airside || o.Rule(x, z) == RoadSurfaceUse.Asphalt)
                        Disc(sink, o, x, z, half, color);
                }

                first = i;
            }

            return any;
        }

        private static bool RibbonPiece(RoadMeshSink sink, RoadBuildOptions o, List<float> pts, int first, int last,
            float half, RoadColor color, bool airside)
        {
            var m = last - first + 1;
            var lx = new float[m];
            var lz = new float[m];
            var rx = new float[m];
            var rz = new float[m];
            var ly = new float[m];
            var ry = new float[m];
            for (var k = 0; k < m; k++)
            {
                var i = first + k;
                var x = pts[i * 2];
                var z = pts[i * 2 + 1];
                var db = k + 1 < m ? Direction(x, z, pts[i * 2 + 2], pts[i * 2 + 3]) : (0f, 0f);
                var da = k > 0 ? Direction(pts[i * 2 - 2], pts[i * 2 - 1], x, z) : db;
                if (k + 1 >= m)
                    db = da;
                var ax = da.Item1 + db.Item1;
                var az = da.Item2 + db.Item2;
                var al = (float)Math.Sqrt(ax * ax + az * az);
                if (al < 1e-4f)
                {
                    ax = db.Item1;
                    az = db.Item2;
                    al = 1f;
                }

                ax /= al;
                az /= al;
                var cos = Math.Max(1f / MaxMiter, db.Item1 * ax + db.Item2 * az);
                var off = half / cos;
                var nx = -az;
                var nz = ax;
                lx[k] = x + nx * off;
                lz[k] = z + nz * off;
                rx[k] = x - nx * off;
                rz[k] = z - nz * off;
                var yc = o.Height(x, z);
                ly[k] = Math.Max(o.Height(lx[k], lz[k]), yc) + o.YOffset;
                ry[k] = Math.Max(o.Height(rx[k], rz[k]), yc) + o.YOffset;
            }

            var any = false;
            for (var k = 0; k + 1 < m; k++)
            {
                var i = first + k;
                if (airside)
                {
                    var use = o.Rule((pts[i * 2] + pts[i * 2 + 2]) * 0.5f, (pts[i * 2 + 1] + pts[i * 2 + 3]) * 0.5f);
                    if (use != RoadSurfaceUse.Asphalt)
                        continue;
                }

                sink.Quad(lx[k], ly[k], lz[k], rx[k], ry[k], rz[k],
                    lx[k + 1], ly[k + 1], lz[k + 1], rx[k + 1], ry[k + 1], rz[k + 1], color);
                any = true;
            }

            return any;
        }

        private static void BuildJunctionDiscs(RoadMeshSink sink, RoadBuildOptions o)
        {
            var j = AdelaideRoadNetwork.Junctions;
            for (var k = 0; k + 3 < j.Length; k += 4)
            {
                if (j[k + 3] < JunctionDiscMinDegree)
                    continue;
                var x = j[k];
                var z = j[k + 1];
                var hw = j[k + 2];
                if (NearAprons(x, z) && o.Rule(x, z) != RoadSurfaceUse.Asphalt)
                    continue;
                var cls = hw >= 6.5f ? AdelaideRoadNetwork.RoadClass.Primary
                    : hw >= 4.5f ? AdelaideRoadNetwork.RoadClass.Tertiary
                    : AdelaideRoadNetwork.RoadClass.Residential;
                Disc(sink, o, x, z, hw * 1.02f, AsphaltForClass(cls, AlphaAt(x, z)));
            }
        }

        private static void BuildTurningCircles(RoadMeshSink sink, RoadBuildOptions o)
        {
            var f = AdelaideRoadNetwork.Furniture;
            var s = AdelaideRoadNetwork.FurnitureStride;
            for (var k = 0; k + s <= f.Length; k += s)
            {
                if ((int)f[k] != (int)AdelaideRoadNetwork.FurnitureKind.TurningCircle)
                    continue;
                var x = f[k + 1];
                var z = f[k + 2];
                Disc(sink, o, x, z, TurningCircleRadius,
                    AsphaltForClass(AdelaideRoadNetwork.RoadClass.Residential, AlphaAt(x, z)));
            }
        }

        private static void Disc(RoadMeshSink sink, RoadBuildOptions o, float x, float z, float radius, RoadColor color)
        {
            var yc = o.Height(x, z);
            var ring = new float[DiscSides * 3];
            for (var i = 0; i < DiscSides; i++)
            {
                var a = i * 2f * (float)Math.PI / DiscSides;
                var px = x + (float)Math.Cos(a) * radius;
                var pz = z + (float)Math.Sin(a) * radius;
                ring[i * 3] = px;
                ring[i * 3 + 1] = Math.Max(o.Height(px, pz), yc) + o.YOffset;
                ring[i * 3 + 2] = pz;
            }

            sink.Fan(x, yc + o.YOffset, z, ring, color);
        }

        /// <summary>Roughly where the pavement rule can matter, so it is not evaluated for every suburban junction.</summary>
        private static bool NearAprons(float x, float z) => x > -2000f && x < 2300f && z > -900f && z < 1100f;

        // --- paint ---

        /// <summary>
        /// Centre, lane and edge lines for the roads that carry them, cut back from side roads, plus zebra
        /// crossings. Returns the number of roads painted.
        /// </summary>
        public static int BuildMarkings(RoadMeshSink sink, RoadBuildOptions o)
        {
            var junctions = JunctionRadii();
            var painted = 0;
            var roads = AdelaideRoadNetwork.Roads;
            for (var r = 0; r < roads.Length; r++)
            {
                var road = roads[r];
                if (!IsMarked(road) || road.Layer < 0)
                    continue;
                var lines = RoadMarkingPlan.ForRoad(road.Width, road.Lanes, road.IsOneWay, road.IsAirside);
                if (lines.Length == 0)
                    continue;
                if (PaintRoad(sink, o, road, lines, junctions))
                    painted++;
            }

            BuildCrossings(sink, o);
            return painted;
        }

        public static bool IsMarked(AdelaideRoadNetwork.Road road)
        {
            if (road.IsAirside)
                return road.Class == AdelaideRoadNetwork.RoadClass.Service && road.Width >= 5f;
            return road.Class <= AdelaideRoadNetwork.RoadClass.Tertiary;
        }

        private static long Key(float x, float z) =>
            (long)Math.Round(x * 10.0) * 4000003L + (long)Math.Round(z * 10.0);

        private static Dictionary<long, float> JunctionRadii()
        {
            var map = new Dictionary<long, float>();
            var j = AdelaideRoadNetwork.Junctions;
            for (var k = 0; k + 3 < j.Length; k += 4)
                if (j[k + 3] >= JunctionDiscMinDegree)
                    map[Key(j[k], j[k + 1])] = j[k + 2];
            return map;
        }

        private static bool PaintRoad(RoadMeshSink sink, RoadBuildOptions o, AdelaideRoadNetwork.Road road,
            MarkingLine[] lines, Dictionary<long, float> junctions)
        {
            var pts = road.IsAirside ? Densify(road.PointStart, road.PointCount, 8f)
                : Densify(road.PointStart, road.PointCount, float.MaxValue);
            var n = pts.Count / 2;
            if (n < 2)
                return false;

            var any = false;
            var piece = new List<float>();
            var startTrim = 0f;

            void Flush(float endTrim)
            {
                if (piece.Count >= 4)
                {
                    var run = Trim(piece, startTrim, endTrim);
                    if (run.Count >= 4)
                    {
                        foreach (var line in lines)
                            WalkRun(sink, o, OffsetRun(run, line.Offset), line.Dashed, line.Width * 0.5f);
                        any = true;
                    }
                }

                piece = new List<float>();
            }

            for (var i = 0; i < n; i++)
            {
                var x = pts[i * 2];
                var z = pts[i * 2 + 1];
                var junction = junctions.TryGetValue(Key(x, z), out var radius);
                if (road.IsAirside && i + 1 < n &&
                    o.Rule((x + pts[i * 2 + 2]) * 0.5f, (z + pts[i * 2 + 3]) * 0.5f) == RoadSurfaceUse.Skip)
                {
                    piece.Add(x);
                    piece.Add(z);
                    Flush(0f);
                    startTrim = 0f;
                    continue;
                }

                piece.Add(x);
                piece.Add(z);
                if (junction && i > 0 && i + 1 < n)
                {
                    Flush(radius + 0.6f);
                    piece.Add(x);
                    piece.Add(z);
                    startTrim = radius + 0.6f;
                }
                else if (i == 0 && junction)
                {
                    startTrim = radius + 0.6f;
                }
                else if (i + 1 == n)
                {
                    Flush(junction ? radius + 0.6f : 0f);
                }
            }

            return any;
        }

        private static void WalkRun(RoadMeshSink sink, RoadBuildOptions o, List<float> run, bool dashed, float halfWidth)
        {
            if (run.Count < 4)
                return;
            var period = DashMetres + GapMetres;
            var traveled = 0f;
            var m = run.Count / 2;
            for (var p = 1; p < m; p++)
            {
                var fx = run[(p - 1) * 2];
                var fz = run[(p - 1) * 2 + 1];
                var tx = run[p * 2];
                var tz = run[p * 2 + 1];
                var segment = (float)Math.Sqrt((tx - fx) * (tx - fx) + (tz - fz) * (tz - fz));
                if (segment < 1e-3f)
                    continue;
                if (!dashed)
                {
                    Dash(sink, o, fx, fz, tx, tz, halfWidth);
                    continue;
                }

                var walked = 0f;
                while (walked < segment)
                {
                    var phase = traveled % period;
                    var onRemaining = DashMetres - phase;
                    var step = onRemaining > 0f ? Math.Min(onRemaining, segment - walked)
                        : Math.Min(period - phase, segment - walked);
                    if (onRemaining > 0f)
                    {
                        var t0 = walked / segment;
                        var t1 = (walked + step) / segment;
                        Dash(sink, o, fx + (tx - fx) * t0, fz + (tz - fz) * t0, fx + (tx - fx) * t1, fz + (tz - fz) * t1, halfWidth);
                    }

                    walked += step;
                    traveled += step;
                }
            }
        }

        private static void Dash(RoadMeshSink sink, RoadBuildOptions o, float fx, float fz, float tx, float tz, float halfWidth)
        {
            var dx = tx - fx;
            var dz = tz - fz;
            var len = (float)Math.Sqrt(dx * dx + dz * dz);
            if (len < 1e-3f)
                return;
            dx /= len;
            dz /= len;
            var px = -dz * halfWidth;
            var pz = dx * halfWidth;
            var lift = o.YOffset + PaintLift;
            float H(float x, float z) => o.Height(x, z) + lift;
            sink.Quad(fx + px, H(fx + px, fz + pz), fz + pz, fx - px, H(fx - px, fz - pz), fz - pz,
                tx + px, H(tx + px, tz + pz), tz + pz, tx - px, H(tx - px, tz - pz), tz - pz, PaintWhite);
        }

        /// <summary>Zebra crossings where OSM puts a crossing on a road wide enough to carry one.</summary>
        public static int BuildCrossings(RoadMeshSink sink, RoadBuildOptions o)
        {
            const int stripes = 6;
            const float pitch = 0.9f;
            const float halfThick = 0.22f;
            var f = AdelaideRoadNetwork.Furniture;
            var s = AdelaideRoadNetwork.FurnitureStride;
            var count = 0;
            for (var k = 0; k + s <= f.Length; k += s)
            {
                if ((int)f[k] != (int)AdelaideRoadNetwork.FurnitureKind.Crossing || f[k + 4] < 5.5f)
                    continue;
                var x = f[k + 1];
                var z = f[k + 2];
                if (NearAprons(x, z) && o.Rule(x, z) != RoadSurfaceUse.Asphalt)
                    continue;
                var yaw = f[k + 3] * (float)Math.PI / 180f;
                var dx = (float)Math.Cos(yaw);
                var dz = (float)Math.Sin(yaw);
                var nx = -dz;
                var nz = dx;
                var reach = Math.Max(1f, f[k + 4] * 0.5f - 0.7f);
                for (var i = 0; i < stripes; i++)
                {
                    var along = (i - (stripes - 1) * 0.5f) * pitch;
                    var cx = x + dx * along;
                    var cz = z + dz * along;
                    var lift = o.YOffset + PaintLift;
                    float H(float px, float pz) => o.Height(px, pz) + lift;
                    var a = (cx - dx * halfThick + nx * reach, cz - dz * halfThick + nz * reach);
                    var b = (cx - dx * halfThick - nx * reach, cz - dz * halfThick - nz * reach);
                    var c = (cx + dx * halfThick + nx * reach, cz + dz * halfThick + nz * reach);
                    var d = (cx + dx * halfThick - nx * reach, cz + dz * halfThick - nz * reach);
                    sink.Quad(a.Item1, H(a.Item1, a.Item2), a.Item2, b.Item1, H(b.Item1, b.Item2), b.Item2,
                        c.Item1, H(c.Item1, c.Item2), c.Item2, d.Item1, H(d.Item1, d.Item2), d.Item2, PaintWhite);
                }

                count++;
            }

            return count;
        }

        // --- helpers (public where the tests check them) ---

        /// <summary>The run moved <paramref name="offset"/> metres to its left, mitred so a bend does not pinch it.</summary>
        public static List<float> OffsetRun(List<float> xz, float offset)
        {
            if (Math.Abs(offset) < 1e-4f || xz.Count < 4)
                return new List<float>(xz);
            var n = xz.Count / 2;
            var result = new List<float>(xz.Count);
            for (var p = 0; p < n; p++)
            {
                var x = xz[p * 2];
                var z = xz[p * 2 + 1];
                var after = p + 1 < n ? Direction(x, z, xz[p * 2 + 2], xz[p * 2 + 3]) : (0f, 0f);
                var before = p > 0 ? Direction(xz[p * 2 - 2], xz[p * 2 - 1], x, z) : after;
                if (p + 1 >= n)
                    after = before;
                var dx = before.Item1 + after.Item1;
                var dz = before.Item2 + after.Item2;
                var l = (float)Math.Sqrt(dx * dx + dz * dz);
                if (l < 1e-3f)
                {
                    dx = after.Item1;
                    dz = after.Item2;
                }
                else
                {
                    dx /= l;
                    dz /= l;
                }

                var cos = Math.Max(0.5f, dx * after.Item1 + dz * after.Item2);
                result.Add(x + -dz * (offset / cos));
                result.Add(z + dx * (offset / cos));
            }

            return result;
        }

        /// <summary>The run with <paramref name="start"/> metres cut from its first end and <paramref name="end"/> from its last.</summary>
        public static List<float> Trim(List<float> xz, float start, float end)
        {
            var run = new List<float>(xz);
            if (start > 0f)
                run = CutFront(run, start);
            if (end > 0f && run.Count >= 4)
            {
                run = Reverse(run);
                run = CutFront(run, end);
                run = Reverse(run);
            }

            return run;
        }

        private static List<float> CutFront(List<float> xz, float distance)
        {
            var n = xz.Count / 2;
            var left = distance;
            for (var p = 1; p < n; p++)
            {
                var fx = xz[(p - 1) * 2];
                var fz = xz[(p - 1) * 2 + 1];
                var tx = xz[p * 2];
                var tz = xz[p * 2 + 1];
                var seg = (float)Math.Sqrt((tx - fx) * (tx - fx) + (tz - fz) * (tz - fz));
                if (seg > left)
                {
                    var t = left / seg;
                    var result = new List<float> { fx + (tx - fx) * t, fz + (tz - fz) * t };
                    for (var q = p; q < n; q++)
                    {
                        result.Add(xz[q * 2]);
                        result.Add(xz[q * 2 + 1]);
                    }

                    return result;
                }

                left -= seg;
            }

            return new List<float>();
        }

        private static List<float> Reverse(List<float> xz)
        {
            var n = xz.Count / 2;
            var result = new List<float>(xz.Count);
            for (var p = n - 1; p >= 0; p--)
            {
                result.Add(xz[p * 2]);
                result.Add(xz[p * 2 + 1]);
            }

            return result;
        }

        /// <summary>Ground bend, in metres, that a straight ribbon segment may hide (about the depth of a paint stripe).</summary>
        public const float FlatToleranceMetres = 0.015f;
        /// <summary>The longest a ribbon segment gets even on dead-flat ground: keeps mitres and tiles sensible.</summary>
        public const float MaxFlatSegmentMetres = 150f;
        private const float MinSubdivideMetres = 8f;

        /// <summary>
        /// A road's own vertices, with a segment split at its middle only when the ground under it is not a straight
        /// line (three interior samples off the chord by more than <see cref="FlatToleranceMetres"/>), or it is longer
        /// than <see cref="MaxFlatSegmentMetres"/>. The ribbon follows the same ground as the 12 m version, with far
        /// fewer quads on flat land.
        /// </summary>
        public static List<float> AdaptiveDensify(int pointStart, int pointCount, RoadBuildOptions o)
        {
            var src = Densify(pointStart, pointCount, float.MaxValue);
            if (o.GroundHeight == null)
                return src;
            var result = new List<float>(src.Count) { src[0], src[1] };
            for (var i = 1; i < src.Count / 2; i++)
            {
                SplitWhereBent(result, src[i * 2 - 2], src[i * 2 - 1], src[i * 2], src[i * 2 + 1], o, 0);
                result.Add(src[i * 2]);
                result.Add(src[i * 2 + 1]);
            }

            return result;
        }

        private static void SplitWhereBent(List<float> result, float ax, float az, float bx, float bz, RoadBuildOptions o, int depth)
        {
            var len = Dist(ax, az, bx, bz);
            if (len <= MinSubdivideMetres || depth >= 7)
                return;
            var ha = o.Height(ax, az);
            var hb = o.Height(bx, bz);
            var bent = len > MaxFlatSegmentMetres;
            for (var k = 1; k <= 3 && !bent; k++)
            {
                var t = k * 0.25f;
                bent = Math.Abs(o.Height(ax + (bx - ax) * t, az + (bz - az) * t) - (ha + (hb - ha) * t)) > FlatToleranceMetres;
            }

            if (!bent)
                return;
            var mx = (ax + bx) * 0.5f;
            var mz = (az + bz) * 0.5f;
            SplitWhereBent(result, ax, az, mx, mz, o, depth + 1);
            result.Add(mx);
            result.Add(mz);
            SplitWhereBent(result, mx, mz, bx, bz, o, depth + 1);
        }

        /// <summary>A road's vertices as x, z pairs with no segment longer than <paramref name="maxSegment"/>.</summary>
        public static List<float> Densify(int pointStart, int pointCount, float maxSegment)
        {
            var src = AdelaideRoadNetwork.Points;
            var result = new List<float>(pointCount * 2);
            for (var i = 0; i < pointCount; i++)
            {
                var x = src[(pointStart + i) * 2];
                var z = src[(pointStart + i) * 2 + 1];
                if (i > 0)
                {
                    var px = result[result.Count - 2];
                    var pz = result[result.Count - 1];
                    var len = (float)Math.Sqrt((x - px) * (x - px) + (z - pz) * (z - pz));
                    var pieces = maxSegment < len ? (int)Math.Ceiling(len / maxSegment) : 1;
                    for (var k = 1; k < pieces; k++)
                    {
                        var t = (float)k / pieces;
                        result.Add(px + (x - px) * t);
                        result.Add(pz + (z - pz) * t);
                    }
                }

                result.Add(x);
                result.Add(z);
            }

            return result;
        }

        /// <summary>The run without points nearer than <paramref name="min"/> to the previous kept point (the last point always stays).</summary>
        public static List<float> DropTiny(List<float> xz, float min)
        {
            var n = xz.Count / 2;
            var result = new List<float>(xz.Count) { xz[0], xz[1] };
            for (var i = 1; i < n; i++)
            {
                var x = xz[i * 2];
                var z = xz[i * 2 + 1];
                var dx = x - result[result.Count - 2];
                var dz = z - result[result.Count - 1];
                if (dx * dx + dz * dz >= min * min)
                {
                    result.Add(x);
                    result.Add(z);
                }
                else if (i == n - 1 && result.Count > 2)
                {
                    result[result.Count - 2] = x;
                    result[result.Count - 1] = z;
                }
            }

            return result;
        }

        private static float Dist(float ax, float az, float bx, float bz) =>
            (float)Math.Sqrt((bx - ax) * (bx - ax) + (bz - az) * (bz - az));

        private static (float, float) Direction(float ax, float az, float bx, float bz)
        {
            var dx = bx - ax;
            var dz = bz - az;
            var l = (float)Math.Sqrt(dx * dx + dz * dz);
            return l < 1e-4f ? (1f, 0f) : (dx / l, dz / l);
        }
    }
}

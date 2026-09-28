using System;
using System.Collections.Generic;

namespace Airside.Presentation
{
    /// <summary>Palette sizes shared by the generator, the data and <see cref="AirsideAdelaideSuburbs"/>.</summary>
    public static class AirsideSuburbPalette
    {
        public const int WallCount = 7;
        public const int RoofCount = 5;
    }

    /// <summary>
    /// ADR 0159 — the buildings around the airfield, read from
    /// <c>scripts/generate-adelaide-suburbs.py</c>'s "ASUB" file: OpenStreetMap houses, shops and
    /// sheds within 2 km of the airfield, plus street-front houses where OSM's footprints are
    /// missing. Pure data and geometry, no UnityEngine, so the format and the shapes are tested
    /// headlessly; <see cref="AirsideAdelaideSuburbs"/> turns it into meshes.
    /// </summary>
    public sealed class AdelaideSuburbData
    {
        public const string ArtPath = "Terrain/osm_adelaide_suburbs_v01.bin";

        public enum Kind : byte
        {
            FlatPrism = 0,
            OsmHouse = 1,
            FillerHouse = 2
        }

        public readonly struct Building
        {
            public Building(Kind kind, byte wall, byte roof, float wallHeight, float roofRise,
                float centreX, float centreZ, float halfLength, float halfWidth, float angle,
                float[] footprint, byte[] roofTriangles)
            {
                Kind = kind;
                Wall = wall;
                Roof = roof;
                WallHeight = wallHeight;
                RoofRise = roofRise;
                CentreX = centreX;
                CentreZ = centreZ;
                HalfLength = halfLength;
                HalfWidth = halfWidth;
                Angle = angle;
                Footprint = footprint;
                RoofTriangles = roofTriangles;
            }

            public Kind Kind { get; }
            public byte Wall { get; }
            public byte Roof { get; }
            public float WallHeight { get; }
            public float RoofRise { get; }
            public float CentreX { get; }
            public float CentreZ { get; }
            public float HalfLength { get; }
            public float HalfWidth { get; }
            public float Angle { get; }

            /// <summary>Flat prisms: x,z pairs, counter-clockwise seen from above.</summary>
            public float[] Footprint { get; }

            /// <summary>Flat prisms: the roof as index triples into <see cref="Footprint"/>.</summary>
            public byte[] RoofTriangles { get; }

            public bool IsHipped => Kind != Kind.FlatPrism;
        }

        private AdelaideSuburbData(Building[] buildings) => Buildings = buildings;

        public IReadOnlyList<Building> Buildings { get; }

        /// <summary>Reads an "ASUB" v1 file; null when it is anything else or truncated.</summary>
        public static AdelaideSuburbData Parse(byte[] bytes)
        {
            try
            {
                if (bytes == null || bytes.Length < 12 || bytes[0] != 'A' || bytes[1] != 'S' || bytes[2] != 'U' || bytes[3] != 'B')
                    return null;
                if (BitConverter.ToInt32(bytes, 4) != 1)
                    return null;
                var count = BitConverter.ToInt32(bytes, 8);
                if (count < 0 || count > 200_000)
                    return null;
                var list = new Building[count];
                var at = 12;
                for (var i = 0; i < count; i++)
                {
                    var kind = (Kind)bytes[at];
                    var wall = bytes[at + 1];
                    var roof = bytes[at + 2];
                    var wallHeight = BitConverter.ToSingle(bytes, at + 3);
                    var rise = BitConverter.ToSingle(bytes, at + 7);
                    at += 11;
                    if (kind == Kind.OsmHouse || kind == Kind.FillerHouse)
                    {
                        list[i] = new Building(kind, wall, roof, wallHeight, rise,
                            BitConverter.ToSingle(bytes, at), BitConverter.ToSingle(bytes, at + 4),
                            BitConverter.ToSingle(bytes, at + 8), BitConverter.ToSingle(bytes, at + 12),
                            BitConverter.ToSingle(bytes, at + 16), null, null);
                        at += 20;
                        continue;
                    }

                    if (kind != Kind.FlatPrism)
                        return null;
                    var n = bytes[at++];
                    var footprint = new float[n * 2];
                    float cx = 0f, cz = 0f;
                    for (var k = 0; k < n * 2; k++)
                    {
                        footprint[k] = BitConverter.ToSingle(bytes, at);
                        at += 4;
                    }

                    for (var k = 0; k < n; k++)
                    {
                        cx += footprint[k * 2];
                        cz += footprint[k * 2 + 1];
                    }

                    var t = bytes[at++];
                    var tris = new byte[t * 3];
                    Array.Copy(bytes, at, tris, 0, tris.Length);
                    at += tris.Length;
                    list[i] = new Building(kind, wall, roof, wallHeight, rise, cx / Math.Max(1, (int)n), cz / Math.Max(1, (int)n),
                        0f, 0f, 0f, footprint, tris);
                }

                return at == bytes.Length ? new AdelaideSuburbData(list) : null;
            }
            catch (Exception e) when (e is ArgumentException or IndexOutOfRangeException)
            {
                return null;
            }
        }

        /// <summary>The four footprint corners of a hipped house, counter-clockwise from above.</summary>
        public static void HouseCorners(Building b, float[] into)
        {
            var ux = (float)Math.Cos(b.Angle);
            var uz = (float)Math.Sin(b.Angle);
            var vx = -uz;
            var vz = ux;
            var i = 0;
            foreach (var (sl, sw) in new[] { (-1f, -1f), (1f, -1f), (1f, 1f), (-1f, 1f) })
            {
                into[i++] = b.CentreX + ux * sl * b.HalfLength + vx * sw * b.HalfWidth;
                into[i++] = b.CentreZ + uz * sl * b.HalfLength + vz * sw * b.HalfWidth;
            }
        }
    }
}

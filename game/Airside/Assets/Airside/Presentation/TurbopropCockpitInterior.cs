using Airside.Domain;
using UnityEngine;

namespace Airside.Presentation
{
    /// <summary>Shared turboprop layout tools; lifetime and geometry ownership live in CockpitInterior.</summary>
    public abstract class TurbopropCockpitInterior : CockpitInterior
    {
        public static TurbopropCockpitInterior Create(Transform aircraft, AircraftType type)
        {
            if (aircraft == null || type == null || (type.Id != AircraftType.Saab340.Id
                && type.Id != AircraftType.Atr42.Id && type.Id != AircraftType.Dash8Q400.Id)) return null;
            return type.Id == AircraftType.Saab340.Id
                ? SaabCockpitInterior.Build(aircraft)
                : GlassTurbopropCockpitInterior.Build(aircraft, type);
        }

        protected Material _black, _panel, _white, _green, _metal, _dialMarks, _compassMarks;

        protected Transform Disc(string name, Vector3 centre, float radius, Material material)
        {
            var vertices = new Vector3[32];
            for (var i = 0; i < vertices.Length; i++)
            {
                var angle = i * Mathf.PI * 2f / vertices.Length;
                vertices[i] = centre + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;
            }
            return Face(name, vertices, material);
        }

        protected void Stroke(string name, Vector3 a, Vector3 b, float width, Material material)
        {
            var delta = b - a;
            var cross = new Vector3(-delta.y, delta.x, 0f).normalized * (width * 0.5f);
            Face(name, new[] { a - cross, b - cross, b + cross, a + cross }, material);
        }

        protected Material DialArtwork(string name, int ticks, float startDegrees, float stepDegrees)
        {
            // Original code-drawn markings share the dial's depth surface. Separate
            // millimetre-thin tick geometry vanished in the large-coordinate player scene.
            const int size = 128;
            var pixels = new Color32[size * size];
            var dark = new Color32(6, 9, 9, 255);
            var light = new Color32(201, 211, 193, 255);
            for (var i = 0; i < pixels.Length; i++) pixels[i] = dark;
            var centre = new Vector2(63.5f, 63.5f);
            for (var tick = 0; tick < ticks; tick++)
            {
                var angle = (startDegrees + tick * stepDegrees) * Mathf.Deg2Rad;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                var a = centre + direction * 48f;
                var b = centre + direction * 57f;
                for (var y = 0; y < size; y++)
                    for (var x = 0; x < size; x++)
                    {
                        var point = new Vector2(x, y);
                        var t = Mathf.Clamp01(Vector2.Dot(point - a, b - a) / (b - a).sqrMagnitude);
                        if ((point - Vector2.Lerp(a, b, t)).sqrMagnitude <= 3.1f)
                            pixels[y * size + x] = light;
                    }
            }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
            { name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            texture.SetPixels32(pixels); texture.Apply(true, true); _textures.Add(texture);
            var material = Surface(name, Color.white, false);
            material.SetTexture("_BaseMap", texture);
            return material;
        }

    }
}

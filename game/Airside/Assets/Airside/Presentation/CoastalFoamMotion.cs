using System;

namespace Airside.Presentation
{
    /// <summary>Geographic ribbon vertices already contain their runway-frame coordinates.</summary>
    public static class CoastalFoamMotion
    {
        // These are the two merged geographic meshes built by BuildShoreFoam. Other
        // Coast foam objects are the legacy KI pads with local centred geometry.
        public static bool IsGeographicLayer(string name) => name is "Coast foam near" or "Coast foam outer";
        public static float Wave(float time, int layer) => 0.5f + 0.5f * (float)Math.Sin(time * 1.8f + layer * 1.7f);
        public static float Alpha(float wave) => 0.3f + 0.35f * wave;
        public static float PrimaryScaleZ(string name, float pulse) => IsGeographicLayer(name) ? 1f : 2.2f * pulse;
        public static float LayerScaleZ(string name, int layer, float wave) => IsGeographicLayer(name)
            ? 1f : (layer == 0 ? 1.1f : 1.4f) * (0.92f + 0.1f * wave);
    }
}

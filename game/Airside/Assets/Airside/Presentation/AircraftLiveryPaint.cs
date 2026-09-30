using System;
using System.Collections.Generic;
using UnityEngine;

namespace Airside.Presentation
{
    /// <summary>Original fleet paint. Aircraft enamel and machined metal never use building textures.</summary>
    public static class AircraftLiveryPaint
    {
        private static readonly Dictionary<(Color, AirsideMaterialLibrary.SurfaceKind), Material> Materials = new();
        public static readonly Color Ivory = new(0.96f, 0.95f, 0.90f);
        public static readonly Color AirframeWhite = new(0.94f, 0.955f, 0.965f);
        public static readonly Color WingGrey = new(0.78f, 0.81f, 0.83f);

        public static Color Emblem(Color primary) =>
            primary.r * 0.2126f + primary.g * 0.7152f + primary.b * 0.0722f > 0.62f
                ? new Color(0.16f, 0.25f, 0.31f) : Ivory;

        public static Color Secondary(Color primary)
        {
            // Cool liveries carry a warm sand pinstripe; warm liveries get slate ink.
            // The contrast survives a yellow, white or nearly black player colour.
            return primary.b + primary.g * 0.35f > primary.r
                ? new Color(0.79f, 0.65f, 0.39f)
                : new Color(0.16f, 0.25f, 0.31f);
        }

        /// <summary>
        /// The cargo variant of an operator colour (ADR 0194): the same hue, deep and desaturated, so a
        /// freighter reads as the same airline in its working paint. A near-grey colour keeps a slate
        /// hue rather than going black.
        /// </summary>
        public static Color FreightPrimary(Color primary)
        {
            Color.RGBToHSV(primary, out var hue, out var saturation, out _);
            if (saturation < 0.12f)
                return new Color(0.20f, 0.24f, 0.29f);
            return Color.HSVToRGB(hue, Mathf.Clamp(saturation, 0.40f, 0.80f), 0.34f);
        }

        public static Color Colour(string part, Color primary)
        {
            var key = part.Replace('_', ' ').ToLowerInvariant();
            if (key.Contains("emblem")) return Emblem(primary);
            if (key.Contains("secondary")) return Secondary(primary);
            return new Color(primary.r, primary.g, primary.b, 1f);
        }

        public static Material MaterialFor(string part, Color colour)
        {
            var name = part.Replace('_', ' ').ToLowerInvariant();
            var kind = AirsideMaterialLibrary.InferFromMeshName(part);
            var cowl = (name.StartsWith("nacelle", StringComparison.Ordinal)
                        || name is "engine l" or "engine r"
                        || name.StartsWith("engine left", StringComparison.Ordinal)
                        || name.StartsWith("engine right", StringComparison.Ordinal))
                       && !name.Contains("rim") && !name.Contains("intake") && !name.Contains("exhaust");
            var liftingSurface = name.StartsWith("wing ", StringComparison.Ordinal)
                                 || name.StartsWith("flap ", StringComparison.Ordinal)
                                 || name.StartsWith("spoiler ", StringComparison.Ordinal)
                                 || name.StartsWith("aileron ", StringComparison.Ordinal)
                                 || name.StartsWith("elevator ", StringComparison.Ordinal)
                                 || name.StartsWith("tailplane", StringComparison.Ordinal);
            if (cowl) { colour = AirframeWhite; kind = AirsideMaterialLibrary.SurfaceKind.AircraftSkin; }
            if (liftingSurface) { colour = WingGrey; kind = AirsideMaterialLibrary.SurfaceKind.AircraftSkin; }
            if (name.Contains("livery")) kind = AirsideMaterialLibrary.SurfaceKind.AircraftSkin;
            var key = (colour, kind);
            if (Materials.TryGetValue(key, out var cached) && cached != null) return cached;
            // These miniature aircraft already have modelled doors, apertures and controls.
            // A random tiled bump/AO map overwhelmed all of that at ordinary follow distance.
            var material = AirsideMaterialLibrary.Create(colour, kind, useTextures: false);
            material.name = "Aircraft finish " + kind;
            if (kind == AirsideMaterialLibrary.SurfaceKind.AircraftSkin)
            {
                material.SetFloat("_Metallic", 0.025f);
                material.SetFloat("_Smoothness", 0.52f);
            }
            else if (kind == AirsideMaterialLibrary.SurfaceKind.Metal)
            {
                material.SetFloat("_Metallic", 0.55f);
                material.SetFloat("_Smoothness", 0.42f);
            }
            material.enableInstancing = true;
            Materials[key] = material;
            return material;
        }
    }
}

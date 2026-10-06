namespace Airside.Presentation
{
    /// <summary>Existing authored star temperatures and brightness, now consumed by the shader.</summary>
    public static class CelestialStarColour
    {
        public static Rgb For(float brightness, float tint)
            => tint < 0.18f ? new Rgb(0.84f * brightness, 0.91f * brightness, brightness)
                : tint > 0.84f ? new Rgb(brightness, 0.91f * brightness, 0.78f * brightness)
                : new Rgb(brightness, brightness, 0.95f * brightness);
    }
}

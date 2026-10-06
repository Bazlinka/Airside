namespace Airside.Presentation
{
    /// <summary>Pilot-relative port red, starboard green, tail white.</summary>
    public static class AircraftNavigationPalette
    {
        public static Rgb For(AircraftNavigationLight kind) => kind switch
        {
            AircraftNavigationLight.Left => new Rgb(0.95f, 0.15f, 0.12f),
            AircraftNavigationLight.Right => new Rgb(0.12f, 0.95f, 0.28f),
            _ => new Rgb(0.95f, 0.95f, 0.90f)
        };
    }
}

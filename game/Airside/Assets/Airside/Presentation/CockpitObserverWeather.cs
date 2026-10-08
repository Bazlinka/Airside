namespace Airside.Presentation
{
    /// <summary>Shared observer precipitation for visible rain and windscreen wipers.</summary>
    public static class CockpitObserverWeather
    {
        public static float Rain(float airportPrecipitation, float height, bool inCockpit, float stormDepth = 0f)
            => airportPrecipitation * (inCockpit ? CockpitWeatherEnvelope.RainAtHeight(height, stormDepth) : 1f);

        public static bool WipersActive(float observerPrecipitation) => observerPrecipitation > 0.04f;
    }
}

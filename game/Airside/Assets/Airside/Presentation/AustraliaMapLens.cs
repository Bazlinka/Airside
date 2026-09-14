using System;

namespace Airside.Presentation
{
    /// <summary>
    /// Zoom/pan math for the Australia destinations map. No UnityEngine dependency so
    /// the headless harness can cover projection and label thresholds.
    /// </summary>
    public sealed class AustraliaMapLens
    {
        public const float MinLongitude = 112f;
        public const float MaxLongitude = 155f;
        public const float MinLatitude = -44.5f;
        public const float MaxLatitude = -9.5f;
        public const float MinZoom = 1f;
        public const float MaxZoom = 60f;
        public const float MidLatitudeDegrees = 27f;

        public float Zoom { get; private set; } = MinZoom;
        public float CenterLongitude { get; private set; } = 133.5f;
        public float CenterLatitude { get; private set; } = -27f;

        public void Reset()
        {
            Zoom = MinZoom;
            CenterLongitude = 133.5f;
            CenterLatitude = -27f;
        }

        public void SetZoom(float zoom) =>
            Zoom = Clamp(zoom, MinZoom, MaxZoom);

        /// <summary>Centre the view on a point (e.g. a tracked flight), kept inside the map bounds.</summary>
        public void CenterOn(float areaWidth, float areaHeight, double longitude, double latitude)
        {
            CenterLongitude = Clamp((float)longitude, MinLongitude, MaxLongitude);
            CenterLatitude = Clamp((float)latitude, MinLatitude, MaxLatitude);
            ClampCenterToView(areaWidth, areaHeight);
        }

        public bool ShowStateLabels => Zoom >= 1.55f;
        public bool ShowCountyDetail => Zoom >= 3.2f;

        public void Project(
            float areaX, float areaY, float areaWidth, float areaHeight,
            double longitude, double latitude,
            out float x, out float y)
        {
            var aspect = (float)Math.Cos(MidLatitudeDegrees * Math.PI / 180.0);
            var worldWidth = (MaxLongitude - MinLongitude) * aspect;
            var worldHeight = MaxLatitude - MinLatitude;
            var fit = Math.Min(areaWidth / worldWidth, areaHeight / worldHeight) * Zoom;
            var centerX = areaX + areaWidth * 0.5f;
            var centerY = areaY + areaHeight * 0.5f;
            x = centerX + ((float)longitude - CenterLongitude) * aspect * fit;
            y = centerY + (CenterLatitude - (float)latitude) * fit;
        }

        public void GuiToLonLat(
            float areaX, float areaY, float areaWidth, float areaHeight,
            float guiX, float guiY,
            out float longitude, out float latitude)
        {
            var aspect = (float)Math.Cos(MidLatitudeDegrees * Math.PI / 180.0);
            var worldWidth = (MaxLongitude - MinLongitude) * aspect;
            var worldHeight = MaxLatitude - MinLatitude;
            var fit = Math.Min(areaWidth / worldWidth, areaHeight / worldHeight) * Zoom;
            if (fit < 0.0001f)
            {
                longitude = CenterLongitude;
                latitude = CenterLatitude;
                return;
            }

            var centerX = areaX + areaWidth * 0.5f;
            var centerY = areaY + areaHeight * 0.5f;
            longitude = CenterLongitude + (guiX - centerX) / (aspect * fit);
            latitude = CenterLatitude - (guiY - centerY) / fit;
        }

        public void PanByGuiDelta(
            float areaWidth, float areaHeight,
            float fromGuiX, float fromGuiY,
            float toGuiX, float toGuiY)
        {
            GuiToLonLat(0f, 0f, areaWidth, areaHeight, fromGuiX, fromGuiY, out var fromLon, out var fromLat);
            GuiToLonLat(0f, 0f, areaWidth, areaHeight, toGuiX, toGuiY, out var toLon, out var toLat);
            CenterLongitude = Clamp(CenterLongitude + (fromLon - toLon), MinLongitude, MaxLongitude);
            CenterLatitude = Clamp(CenterLatitude + (fromLat - toLat), MinLatitude, MaxLatitude);
            ClampCenterToView(areaWidth, areaHeight);
        }

        public void ZoomAtGui(
            float areaWidth, float areaHeight,
            float guiX, float guiY,
            float factor)
        {
            GuiToLonLat(0f, 0f, areaWidth, areaHeight, guiX, guiY, out var beforeLon, out var beforeLat);
            SetZoom(Zoom * factor);
            GuiToLonLat(0f, 0f, areaWidth, areaHeight, guiX, guiY, out var afterLon, out var afterLat);
            CenterLongitude = Clamp(CenterLongitude + (beforeLon - afterLon), MinLongitude, MaxLongitude);
            CenterLatitude = Clamp(CenterLatitude + (beforeLat - afterLat), MinLatitude, MaxLatitude);
            ClampCenterToView(areaWidth, areaHeight);
        }

        private void ClampCenterToView(float areaWidth, float areaHeight)
        {
            var aspect = (float)Math.Cos(MidLatitudeDegrees * Math.PI / 180.0);
            var worldWidth = (MaxLongitude - MinLongitude) * aspect;
            var worldHeight = MaxLatitude - MinLatitude;
            var fit = Math.Min(areaWidth / worldWidth, areaHeight / worldHeight) * Zoom;
            if (fit < 0.0001f)
                return;
            var halfLon = (areaWidth * 0.5f) / (aspect * fit);
            var halfLat = (areaHeight * 0.5f) / fit;
            CenterLongitude = Clamp(CenterLongitude, MinLongitude + halfLon, MaxLongitude - halfLon);
            CenterLatitude = Clamp(CenterLatitude, MinLatitude + halfLat, MaxLatitude - halfLat);
        }

        private static float Clamp(float value, float min, float max) =>
            value < min ? min : value > max ? max : value;
    }

    /// <summary>
    /// Reading-aid coastline, state borders and labels. Approximate shapes for play —
    /// not survey data. Stored as lon/lat pairs (x = lon, y = lat).
    /// </summary>
    public static class AustraliaMapGeometry
    {
        public static readonly float[] MainlandCoastLonLat =
        {
            113.15f, -21.80f, 113.05f, -23.60f, 113.45f, -25.40f, 114.10f, -27.20f,
            114.60f, -28.80f, 115.20f, -30.40f, 115.55f, -31.80f, 115.35f, -32.80f,
            115.05f, -33.70f, 115.15f, -34.35f, 116.20f, -34.90f, 117.80f, -35.10f,
            119.40f, -34.40f, 121.40f, -33.85f, 123.60f, -33.10f, 126.00f, -32.25f,
            128.20f, -31.85f, 129.00f, -31.70f, 130.40f, -31.55f, 131.80f, -31.45f,
            133.20f, -31.90f, 134.60f, -32.80f, 135.90f, -34.55f, 136.80f, -33.40f,
            137.70f, -32.70f, 137.20f, -34.40f, 137.55f, -35.00f, 138.20f, -34.50f,
            138.55f, -34.85f, 138.25f, -35.55f, 139.20f, -35.95f, 140.20f, -37.20f,
            140.90f, -38.00f, 141.70f, -38.35f, 143.00f, -38.70f, 144.40f, -38.45f,
            145.20f, -38.55f, 146.20f, -39.00f, 147.40f, -38.40f, 148.40f, -37.80f,
            149.50f, -37.55f, 150.10f, -36.40f, 150.25f, -35.40f, 150.90f, -34.40f,
            151.30f, -33.85f, 151.70f, -33.10f, 152.40f, -31.80f, 153.00f, -30.40f,
            153.40f, -29.20f, 153.55f, -28.20f, 153.40f, -27.00f, 153.20f, -25.40f,
            152.40f, -24.40f, 150.90f, -23.40f, 149.40f, -22.20f, 147.80f, -20.40f,
            146.60f, -19.20f, 145.90f, -17.80f, 145.40f, -16.40f, 145.10f, -15.20f,
            144.20f, -14.20f, 143.20f, -12.90f, 142.60f, -11.60f, 142.50f, -10.80f,
            142.10f, -11.80f, 141.80f, -13.40f, 141.50f, -15.00f, 140.90f, -16.80f,
            140.20f, -17.50f, 139.20f, -17.35f, 137.80f, -16.60f, 136.90f, -15.70f,
            136.85f, -13.80f, 136.60f, -12.20f, 135.40f, -12.05f, 133.80f, -11.70f,
            132.20f, -11.40f, 130.90f, -12.20f, 129.80f, -14.20f, 128.40f, -15.20f,
            126.60f, -14.60f, 124.80f, -15.20f, 123.40f, -16.40f, 122.00f, -17.90f,
            120.40f, -19.20f, 118.40f, -20.20f, 116.60f, -20.55f, 114.80f, -21.40f,
            113.15f, -21.80f
        };

        public static readonly float[] TasmaniaCoastLonLat =
        {
            144.70f, -40.70f, 146.00f, -40.75f, 147.40f, -40.85f, 148.20f, -41.40f,
            148.30f, -42.20f, 147.90f, -43.20f, 147.00f, -43.55f, 146.00f, -43.40f,
            145.20f, -42.60f, 144.80f, -41.60f, 144.70f, -40.70f
        };

        public static readonly float[][] StateBorderLonLats =
        {
            new[] { 129.00f, -14.90f, 129.00f, -26.00f, 129.00f, -31.70f },
            new[] { 138.00f, -16.60f, 138.00f, -26.00f, 129.00f, -26.00f },
            new[] { 141.00f, -26.00f, 141.00f, -34.00f, 141.00f, -38.05f },
            new[] { 129.00f, -26.00f, 138.00f, -26.00f, 141.00f, -26.00f },
            new[] { 141.00f, -34.10f, 142.50f, -34.20f, 144.00f, -35.10f, 146.50f, -36.00f, 148.20f, -37.00f, 149.90f, -37.50f },
            new[] { 141.00f, -29.00f, 145.00f, -29.00f, 150.00f, -28.20f, 153.50f, -28.20f },
            new[] { 148.75f, -35.10f, 149.40f, -35.10f, 149.40f, -35.90f, 148.75f, -35.90f, 148.75f, -35.10f }
        };

        public static readonly (string code, float lon, float lat)[] StateLabels =
        {
            ("WA", 122.0f, -25.5f), ("NT", 133.5f, -19.5f), ("SA", 135.5f, -29.5f),
            ("QLD", 145.0f, -22.5f), ("NSW", 147.0f, -32.5f), ("VIC", 144.5f, -37.0f),
            ("TAS", 146.5f, -42.0f), ("ACT", 149.1f, -35.5f)
        };

        public static readonly (string name, float lon, float lat)[] RegionLabels =
        {
            ("Eyre Peninsula", 136.0f, -33.6f), ("Yorke Peninsula", 137.6f, -34.4f),
            ("Fleurieu", 138.5f, -35.4f), ("Limestone Coast", 140.6f, -37.2f),
            ("Riverland", 140.5f, -34.3f), ("Outback SA", 135.0f, -28.5f),
            ("Kangaroo Island", 137.2f, -35.8f), ("Sunraysia", 142.1f, -34.3f),
            ("Riverina", 146.0f, -35.0f), ("Illawarra", 150.9f, -34.4f),
            ("Hunter", 151.6f, -32.7f), ("Gold Coast", 153.4f, -28.1f),
            ("Sunshine Coast", 153.1f, -26.6f), ("Top End", 131.5f, -13.0f),
            ("Kimberley", 126.0f, -16.5f), ("Pilbara", 118.5f, -21.5f),
            ("South West WA", 116.5f, -33.5f)
        };

        public static int PointCount(float[] lonLat) => lonLat.Length / 2;
    }
}

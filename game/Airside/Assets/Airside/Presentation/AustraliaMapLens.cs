using System;

namespace Airside.Presentation
{
    /// <summary>
    /// Zoom/pan math for the route map. No UnityEngine dependency so the headless harness can cover
    /// projection and label thresholds. ADR 0140: the map now reaches from Doha to Los Angeles.
    /// Zoom 1 still frames Australia (the home view). Zooming out shows Asia and the Pacific, and
    /// zooming in goes as far as an airport's runways. Longitudes are unwrapped east (Honolulu is
    /// 202°, not −158°), so routes across the Pacific never jump across the map.
    /// </summary>
    public sealed class AustraliaMapLens
    {
        // The home frame: what zoom 1 fits to the panel.
        public const float HomeMinLongitude = 112f;
        public const float HomeMaxLongitude = 155f;
        public const float HomeMinLatitude = -44.5f;
        public const float HomeMaxLatitude = -9.5f;

        // How far the map can be panned: every destination and its coastline.
        public const float MinLongitude = 20f;
        public const float MaxLongitude = 260f;
        public const float MinLatitude = -56f;
        public const float MaxLatitude = 62f;

        public const float MinZoom = 0.2f;
        public const float HomeZoom = 1f;
        public const float MaxZoom = 400f;
        public const float MidLatitudeDegrees = 27f;

        /// <summary>From here the airport runway detail is drawn (ADR 0140).</summary>
        public const float AirportDetailZoom = 30f;

        private const float HomeCenterLongitude = 133.5f;
        private const float HomeCenterLatitude = -27f;
        private const float WorldCenterLongitude = 140f;
        private const float WorldCenterLatitude = 5f;

        public float Zoom { get; private set; } = HomeZoom;
        public float CenterLongitude { get; private set; } = HomeCenterLongitude;
        public float CenterLatitude { get; private set; } = HomeCenterLatitude;

        /// <summary>Back to the home view: Australia filling the panel.</summary>
        public void Reset()
        {
            Zoom = HomeZoom;
            CenterLongitude = HomeCenterLongitude;
            CenterLatitude = HomeCenterLatitude;
        }

        /// <summary>The whole map: Asia, the Pacific and the US west coast.</summary>
        public void ShowWorld(float areaWidth, float areaHeight)
        {
            Zoom = MinZoom;
            CenterLongitude = WorldCenterLongitude;
            CenterLatitude = WorldCenterLatitude;
            ClampCenterToView(areaWidth, areaHeight);
        }

        public void SetZoom(float zoom) =>
            Zoom = Clamp(zoom, MinZoom, MaxZoom);

        /// <summary>
        /// Longitude as the map uses it: the Americas and the far Pacific (west of 60°W) wrap to
        /// 180..300. Europe and Africa stay where they are, off the map's western edge. Wrapping them
        /// too drew coast segments right across the map.
        /// </summary>
        public static double Unwrap(double longitude) => longitude < -60.0 ? longitude + 360.0 : longitude;

        /// <summary>Centre the view on a point (e.g. a tracked flight), kept inside the map bounds.</summary>
        public void CenterOn(float areaWidth, float areaHeight, double longitude, double latitude)
        {
            CenterLongitude = Clamp((float)Unwrap(longitude), MinLongitude, MaxLongitude);
            CenterLatitude = Clamp((float)latitude, MinLatitude, MaxLatitude);
            ClampCenterToView(areaWidth, areaHeight);
        }

        public bool ShowStateLabels => Zoom >= 1.55f;
        public bool ShowCountyDetail => Zoom >= 3.2f;

        /// <summary>Which coastline to draw (ADR 0140): world, region or detail.</summary>
        public MapDetail Detail => Zoom < 0.55f ? MapDetail.World : Zoom < 1.8f ? MapDetail.Region : MapDetail.Detail;

        /// <summary>Towns of this rank or better are drawn (−1: none). Rank 0 is a capital city.</summary>
        public int TownRank => Zoom >= 14f ? 3 : Zoom >= 7f ? 2 : Zoom >= 3f ? 1 : -1;

        /// <summary>Screen pixels per degree of latitude at the current zoom, for a panel this size.</summary>
        public float PixelsPerDegree(float areaWidth, float areaHeight)
        {
            var aspect = Aspect;
            var worldWidth = (HomeMaxLongitude - HomeMinLongitude) * aspect;
            var worldHeight = HomeMaxLatitude - HomeMinLatitude;
            return Math.Min(areaWidth / worldWidth, areaHeight / worldHeight) * Zoom;
        }

        private static float Aspect => (float)Math.Cos(MidLatitudeDegrees * Math.PI / 180.0);

        public void Project(
            float areaX, float areaY, float areaWidth, float areaHeight,
            double longitude, double latitude,
            out float x, out float y)
        {
            var fit = PixelsPerDegree(areaWidth, areaHeight);
            var centerX = areaX + areaWidth * 0.5f;
            var centerY = areaY + areaHeight * 0.5f;
            x = centerX + ((float)Unwrap(longitude) - CenterLongitude) * Aspect * fit;
            y = centerY + (CenterLatitude - (float)latitude) * fit;
        }

        public void GuiToLonLat(
            float areaX, float areaY, float areaWidth, float areaHeight,
            float guiX, float guiY,
            out float longitude, out float latitude)
        {
            var fit = PixelsPerDegree(areaWidth, areaHeight);
            if (fit < 0.0001f)
            {
                longitude = CenterLongitude;
                latitude = CenterLatitude;
                return;
            }

            var centerX = areaX + areaWidth * 0.5f;
            var centerY = areaY + areaHeight * 0.5f;
            longitude = CenterLongitude + (guiX - centerX) / (Aspect * fit);
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
            var fit = PixelsPerDegree(areaWidth, areaHeight);
            if (fit < 0.0001f)
                return;
            var halfLon = (areaWidth * 0.5f) / (Aspect * fit);
            var halfLat = (areaHeight * 0.5f) / fit;
            CenterLongitude = ClampAxis(CenterLongitude, MinLongitude, MaxLongitude, halfLon);
            CenterLatitude = ClampAxis(CenterLatitude, MinLatitude, MaxLatitude, halfLat);
        }

        /// <summary>
        /// Keep the view inside the map on one axis. When the view is wider than the map
        /// (zoomed out), centre it: clamping to min+half..max-half with min above max
        /// flipped the centre between the two ends on every call, and once zoom eased
        /// every frame that drew two alternating copies of Australia.
        /// </summary>
        private static float ClampAxis(float value, float min, float max, float half) =>
            min + half >= max - half ? (min + max) * 0.5f : Clamp(value, min + half, max - half);

        private static float Clamp(float value, float min, float max) =>
            value < min ? min : value > max ? max : value;
    }

    public enum MapDetail
    {
        World,
        Region,
        Detail
    }

    /// <summary>
    /// Hand-placed labels for states and regions. The coastline and borders themselves are
    /// generated from Natural Earth (<see cref="MapGeographyData"/>, ADR 0140).
    /// </summary>
    public static class AustraliaMapGeometry
    {
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

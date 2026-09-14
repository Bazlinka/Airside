using Airside.Presentation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class AustraliaMapLensTests
    {
        [Test]
        public void Reset_ReturnsToContinentOverview()
        {
            var lens = new AustraliaMapLens();
            lens.SetZoom(4f);
            lens.Reset();

            Assert.That(lens.Zoom, Is.EqualTo(AustraliaMapLens.MinZoom));
            Assert.That(lens.ShowStateLabels, Is.False);
            Assert.That(lens.ShowCountyDetail, Is.False);
        }

        [Test]
        public void Project_KeepsAdelaideInsideTheFittedPanelAtOverview()
        {
            var lens = new AustraliaMapLens();
            lens.Project(0f, 0f, 640f, 400f, 138.531, -34.945, out var x, out var y);

            Assert.That(x, Is.GreaterThan(0f));
            Assert.That(x, Is.LessThan(640f));
            Assert.That(y, Is.GreaterThan(0f));
            Assert.That(y, Is.LessThan(400f));
        }

        [Test]
        public void SetZoom_RevealsStateThenRegionLabels()
        {
            var lens = new AustraliaMapLens();
            Assert.That(lens.ShowStateLabels, Is.False);

            lens.SetZoom(2f);
            Assert.That(lens.ShowStateLabels, Is.True);
            Assert.That(lens.ShowCountyDetail, Is.False);

            lens.SetZoom(4f);
            Assert.That(lens.ShowCountyDetail, Is.True);
        }

        [Test]
        public void Geometry_HasDenseCoastsAndStateBorders()
        {
            Assert.That(AustraliaMapGeometry.PointCount(AustraliaMapGeometry.MainlandCoastLonLat), Is.GreaterThan(60));
            Assert.That(AustraliaMapGeometry.PointCount(AustraliaMapGeometry.TasmaniaCoastLonLat), Is.GreaterThan(8));
            Assert.That(AustraliaMapGeometry.StateBorderLonLats.Length, Is.GreaterThanOrEqualTo(5));
            Assert.That(AustraliaMapGeometry.StateLabels.Length, Is.EqualTo(8));
            Assert.That(AustraliaMapGeometry.RegionLabels.Length, Is.GreaterThanOrEqualTo(10));
        }

        [Test]
        public void GuiToLonLat_RoundTripsNearTheMapCentre()
        {
            var lens = new AustraliaMapLens();
            lens.SetZoom(2.5f);
            const float width = 500f;
            const float height = 320f;
            var cx = width * 0.5f;
            var cy = height * 0.5f;
            lens.GuiToLonLat(0f, 0f, width, height, cx, cy, out var lon, out var lat);
            lens.Project(0f, 0f, width, height, lon, lat, out var backX, out var backY);

            Assert.That(backX, Is.EqualTo(cx).Within(0.5f));
            Assert.That(backY, Is.EqualTo(cy).Within(0.5f));
        }

        [Test]
        public void ZoomAtGui_IncreasesZoomWhileKeepingPivotStable()
        {
            var lens = new AustraliaMapLens();
            const float width = 500f;
            const float height = 320f;
            const float px = 180f;
            const float py = 140f;
            lens.GuiToLonLat(0f, 0f, width, height, px, py, out var beforeLon, out var beforeLat);
            lens.ZoomAtGui(width, height, px, py, 2f);
            lens.GuiToLonLat(0f, 0f, width, height, px, py, out var afterLon, out var afterLat);

            Assert.That(lens.Zoom, Is.EqualTo(2f).Within(0.01f));
            Assert.That(afterLon, Is.EqualTo(beforeLon).Within(0.05f));
            Assert.That(afterLat, Is.EqualTo(beforeLat).Within(0.05f));
        }
    }
}

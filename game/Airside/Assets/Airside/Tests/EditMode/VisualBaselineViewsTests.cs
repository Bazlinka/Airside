using System.Collections.Generic;
using Airside.Presentation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>Locks the Phase 0 visual-baseline camera bookmark table.</summary>
    public sealed class VisualBaselineViewsTests
    {
        [Test]
        public void All_HasTheSixPlanBookmarksWithStableIds()
        {
            Assert.That(AirsideVisualBaselineViews.All.Length, Is.EqualTo(6));
            var ids = new List<string>();
            foreach (var view in AirsideVisualBaselineViews.All)
                ids.Add(view.Id);
            Assert.That(ids, Is.EqualTo(new[]
            {
                "overview", "terminal-airside", "terminal-kerb",
                "hangar-row", "suburb-edge", "coast"
            }));
        }

        [Test]
        public void Overview_MatchesBareFieldFraming()
        {
            var view = AirsideVisualBaselineViews.Overview;
            Assert.That(view.Distance, Is.EqualTo(AirsideBareField.OverviewDistance));
            Assert.That(view.Pitch, Is.EqualTo(AirsideBareField.OverviewPitch));
            Assert.That(view.Yaw, Is.EqualTo(AirsideBareField.OverviewYaw));
            Assert.That(view.CenterX, Is.EqualTo(150f));
            Assert.That(view.CenterZ, Is.EqualTo(350f));
        }

        [Test]
        public void TryResolve_IsCaseInsensitiveAndRejectsUnknown()
        {
            Assert.That(AirsideVisualBaselineViews.TryResolve("Hangar-Row", out var hangar), Is.True);
            Assert.That(hangar.Id, Is.EqualTo("hangar-row"));
            Assert.That(AirsideVisualBaselineViews.TryResolve("nope", out _), Is.False);
            Assert.That(AirsideVisualBaselineViews.ResolveOrOverview("nope").Id, Is.EqualTo("overview"));
        }

        [Test]
        public void FromCommandLine_UsesReviewViewFlag()
        {
            var coast = AirsideVisualBaselineViews.FromCommandLine(new[]
            {
                "Airside", AirsideVisualBaselineViews.ReviewViewFlag, "coast"
            });
            Assert.That(coast.Id, Is.EqualTo("coast"));
            Assert.That(coast.Distance, Is.EqualTo(AirsideVisualBaselineViews.Coast.Distance));

            var fallback = AirsideVisualBaselineViews.FromCommandLine(new[] { "Airside" });
            Assert.That(fallback.Id, Is.EqualTo("overview"));
        }

        [Test]
        public void Bookmarks_StayInsideASensibleCaptureEnvelope()
        {
            foreach (var view in AirsideVisualBaselineViews.All)
            {
                Assert.That(view.Distance, Is.GreaterThan(100f).And.LessThan(5000f), view.Id);
                Assert.That(view.Pitch, Is.GreaterThan(5f).And.LessThan(80f), view.Id);
                Assert.That(view.Label, Is.Not.Empty);
                Assert.That(System.Math.Abs(view.CenterX), Is.LessThan(8000f), view.Id);
                Assert.That(System.Math.Abs(view.CenterZ), Is.LessThan(8000f), view.Id);
            }

            Assert.That(AirsideVisualBaselineViews.LightingTimes, Is.EqualTo(new[]
            {
                "12:00", "18:30", "23:30"
            }));
            Assert.That(AirsideVisualBaselineViews.OverviewBudgetFps, Is.EqualTo(60));
            Assert.That(AirsideVisualBaselineViews.CaptureWidth, Is.EqualTo(1600));
            Assert.That(AirsideVisualBaselineViews.CaptureHeight, Is.EqualTo(900));
        }

        [Test]
        public void TerminalAndHangarViews_AreCloserThanOverview()
        {
            Assert.That(AirsideVisualBaselineViews.TerminalAirside.Distance,
                Is.LessThan(AirsideVisualBaselineViews.Overview.Distance));
            Assert.That(AirsideVisualBaselineViews.HangarRow.Distance,
                Is.LessThan(AirsideVisualBaselineViews.Overview.Distance));
            Assert.That(AirsideVisualBaselineViews.TerminalAirside.CenterX,
                Is.GreaterThan(1000f), "Terminal 1 gates sit east of the field origin");
            Assert.That(AirsideVisualBaselineViews.HangarRow.CenterX,
                Is.GreaterThan(800f), "eastern hangar row");
            Assert.That(AirsideVisualBaselineViews.Coast.CenterX,
                Is.LessThan(0f), "coast looks west toward the gulf");
        }
    }
}

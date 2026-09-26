using System.Collections.Generic;
using System.Linq;
using Airside.Presentation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>
    /// The Glass Cockpit shell (ADR 0122): the navigation rail, status capsule, career ring card,
    /// flight tiles, radar, selected card, toast and workspace sheet. UnityEngine-free, so the
    /// fits-and-never-overlaps contract runs headlessly at every supported window.
    /// </summary>
    public sealed class HudShellTests
    {
        /// <summary>Virtual viewports: the HUD test set plus the physical sizes after HUD scaling.</summary>
        private static IEnumerable<(float Width, float Height)> Viewports()
        {
            foreach (var viewport in HudTestAirline.Viewports)
                yield return viewport;
            foreach (var (w, h) in new[] { (1280, 720), (1440, 900), (1920, 1080), (3456, 2168), (800, 500), (400, 780), (320, 240) })
            {
                var scale = System.Math.Clamp(System.Math.Min(w / 1440f, h / 900f), 0.55f, 2.25f);
                yield return (w / scale, h / scale);
            }
        }

        [Test]
        public void Shell_NamesTheCareerWorkspaceCareer()
        {
            Assert.That(HudShell.Tabs.Single(t => t.workspace == HudWorkspace.Stats).label, Is.EqualTo("CAREER"));
        }

        [Test]
        public void Shell_EveryPanelFitsAndNothingOverlaps()
        {
            foreach (var (width, height) in Viewports())
            foreach (var guide in new[] { false, true })
            foreach (var workspaceOpen in new[] { false, true })
            {
                var shell = HudShell.Layout(width, height, guide, workspaceOpen);
                var at = $"{width:0}x{height:0} guide={guide} sheet={workspaceOpen}";
                var shown = shell.Panels.Where(p => !p.Box.IsEmpty).ToList();
                foreach (var (name, box) in shown)
                {
                    Assert.That(box.X, Is.GreaterThanOrEqualTo(-0.01f), $"{name} off the left {at}");
                    Assert.That(box.Y, Is.GreaterThanOrEqualTo(-0.01f), $"{name} off the top {at}");
                    Assert.That(box.Right, Is.LessThanOrEqualTo(width + 0.01f), $"{name} off the right {at}");
                    Assert.That(box.Bottom, Is.LessThanOrEqualTo(height - HudShell.Margin + 0.01f),
                        $"{name} runs into the credit footer {at}");
                }
                for (var i = 0; i < shown.Count; i++)
                for (var j = i + 1; j < shown.Count; j++)
                    Assert.That(shown[i].Box.Overlaps(shown[j].Box), Is.False,
                        $"{shown[i].Name} overlaps {shown[j].Name} {at}");
                Assert.That(shell.Rail.IsEmpty, Is.False, "the rail is the navigation: always shown " + at);
                Assert.That(shell.Capsule.IsEmpty, Is.False, "the capsule is always shown " + at);
                if (workspaceOpen)
                    Assert.That(shell.Workspace.Width, Is.GreaterThan(300f), "sheet is usable " + at);
            }
        }

        [Test]
        public void Shell_DesktopShowsEveryOverviewPanel()
        {
            var shell = HudShell.Layout(1440f, 900f);
            Assert.That(shell.Career.IsEmpty, Is.False);
            Assert.That(shell.Operations.IsEmpty, Is.False);
            Assert.That(shell.MiniMap.IsEmpty, Is.False);
            Assert.That(shell.SelectedCard.IsEmpty, Is.False);
            Assert.That(shell.Toast.IsEmpty, Is.False);
            Assert.That(shell.Career.X, Is.LessThan(shell.SelectedCard.X), "career card bottom-left");
            Assert.That(shell.MiniMap.X, Is.GreaterThan(shell.SelectedCard.Right), "radar bottom-right");
            Assert.That(shell.Operations.Y, Is.LessThan(shell.MiniMap.Y), "flight tiles top-right");
            Assert.That(System.Math.Abs(shell.Capsule.X + shell.Capsule.Width * 0.5f - 720f), Is.LessThan(1f),
                "the capsule is centred on a desktop window");
        }

        [Test]
        public void Shell_OpenSheetReplacesTheOverviewPanels()
        {
            var shell = HudShell.Layout(1440f, 900f, workspaceOpen: true);
            Assert.That(shell.Career.IsEmpty && shell.Operations.IsEmpty && shell.MiniMap.IsEmpty
                        && shell.SelectedCard.IsEmpty, Is.True);
            Assert.That(shell.Workspace.X, Is.GreaterThan(shell.Rail.Right));
            Assert.That(shell.Workspace.Y, Is.GreaterThan(shell.Capsule.Bottom));
            Assert.That(shell.Toast.Overlaps(shell.Workspace), Is.False, "feedback never covers the sheet's heading");
        }

        [Test]
        public void Rail_HasOneSelectedItemPerPageAndItemsNeverOverlap()
        {
            foreach (var (width, height) in Viewports())
            {
                var rail = HudShell.Rail(width, height);
                foreach (var workspace in new[]
                         {
                             HudWorkspace.None, HudWorkspace.Operations, HudWorkspace.Map,
                             HudWorkspace.Fleet, HudWorkspace.Contracts, HudWorkspace.Stats
                         })
                {
                    var tabs = new List<HudNavTab>();
                    HudShell.FillTabs(rail, height, workspace, tabs);
                    Assert.That(tabs, Has.Count.EqualTo(HudShell.Tabs.Length), $"{workspace} {width}x{height}");
                    Assert.That(tabs.Count(t => t.Selected), Is.EqualTo(1));
                    for (var i = 1; i < tabs.Count; i++)
                        Assert.That(tabs[i].Box.Y, Is.GreaterThanOrEqualTo(tabs[i - 1].Box.Bottom - 0.01f));
                    Assert.That(tabs.Last().Box.Bottom, Is.LessThanOrEqualTo(rail.Bottom + 0.01f));
                }
            }
        }

        [Test]
        public void Capsule_DropsReadoutsRatherThanOverflowing()
        {
            var values = new[]
            {
                new HudCapsuleValue("AIRLINE", "A Very Long Airline Name Indeed"),
                new HudCapsuleValue("ADELAIDE", "12:36"),
                new HudCapsuleValue("FUNDS", "$1,234,567"),
                new HudCapsuleValue("RELIABILITY", "97%", HudTone.Default, HudCapsuleKind.Gauge, 0.97f),
                new HudCapsuleValue("TIER", "INTERNATIONAL", HudTone.Accent, HudCapsuleKind.Chip)
            };
            foreach (var (width, height) in Viewports())
            {
                var capsule = HudShell.Capsule(width, height);
                var segments = new List<HudCapsuleSegment>();
                HudShell.FillCapsule(capsule, values, segments);
                Assert.That(segments.Count, Is.GreaterThanOrEqualTo(2), $"{width}x{height}");
                foreach (var segment in segments)
                    Assert.That(segment.Box.Right, Is.LessThanOrEqualTo(capsule.Right + 0.01f), $"{width}x{height}");
            }
        }

        [Test]
        public void CareerCard_PaintsTheRingTheStageTargetAndTheNextAction()
        {
            var draw = new HudDrawList();
            HudShellPainter.PaintCareer(draw, HudShell.Layout(1440f, 900f).Career,
                new CareerObjective("Fly five services", "2/5", 0.4f, "Next: plan a flight for VH-PAX",
                    StatusSeverity.Attention, "TOWARD REGIONAL"), 1f / 3f, "1/3");

            Assert.That(draw.Commands.Count(c => c.Kind == HudDrawKind.Ring), Is.EqualTo(2), "track and progress");
            Assert.That(draw.Commands.Any(c => c.Text == "TOWARD REGIONAL"), Is.True);
            Assert.That(draw.Commands.Any(c => c.Text == "Fly five services" && c.FontSize >= 15f), Is.True);
            Assert.That(draw.Commands.Any(c => c.Text == "plan a flight for VH-PAX"), Is.True);
        }

        [Test]
        public void SheetHeader_UsesTitleCaseAndARoundClose()
        {
            var draw = new HudDrawList();
            var surface = HudShell.WorkspaceSurface(1440f, 900f);
            HudShellPainter.PaintSheetHeader(draw, surface, "CAREER ROADMAP", "subtitle",
                new HudBox(surface.X + 30f, surface.Y + 14f, 300f, 30f), new HudBox(surface.X + 24f, surface.Y + 44f, 300f, 16f));
            Assert.That(draw.Commands.Any(c => c.Text == "Career Roadmap"), Is.True);
            var close = draw.Commands.Single(c => c.ActionId == HudAction.Close);
            Assert.That(close.Box.Width, Is.EqualTo(close.Box.Height), "the close control is round");
        }
    }
}

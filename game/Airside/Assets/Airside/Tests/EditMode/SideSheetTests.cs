using System.Linq;
using Airside.Domain;
using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>ADR 0135 — side sheets over the airport, the slide-in, and text that fits them.</summary>
    public sealed class SideSheetTests
    {
        [Test]
        public void SideSheet_LeavesTheAirportInViewOnWideWindowsOnly()
        {
            var wide = HudShell.WorkspaceSurface(1920f, 1080f);
            var sheet = HudShell.SideSheet(wide);
            Assert.That(sheet.Right, Is.EqualTo(wide.Right).Within(0.01f), "anchored right");
            Assert.That(sheet.Width, Is.LessThan(wide.Width * 0.75f));
            Assert.That(sheet.Width, Is.GreaterThanOrEqualTo(HudShell.SideSheetMinWidth));

            var small = HudShell.WorkspaceSurface(1024f, 640f);
            Assert.That(HudShell.SideSheet(small), Is.EqualTo(small), "small windows keep the full width");
        }

        [Test]
        public void SheetEntrance_SlidesInAndSettles()
        {
            Assert.That(HudShell.SheetEntrance(0f).OffsetX, Is.GreaterThan(20f));
            Assert.That(HudShell.SheetEntrance(0.09f).OffsetX, Is.GreaterThan(0f).And.LessThan(20f));
            Assert.That(HudShell.SheetEntrance(1f), Is.EqualTo((0f, 1f)));
        }

        [Test]
        public void FitFontSize_ShrinksOnlyWhatDoesNotFit()
        {
            Assert.That(HudShell.FitFontSize("Short", 14f, 400f, 10f), Is.EqualTo(14f));
            var size = HudShell.FitFontSize("Regional starter base → Expanded regional base", 14f, 200f, 9f);
            Assert.That(size, Is.LessThan(14f).And.GreaterThanOrEqualTo(9f));
        }

        [Test]
        public void SheetWorkspaces_DrawInsideTheSheetAtEveryWindow()
        {
            var (clock, ops, _) = HudTestAirline.Create();
            var definition = RouteContractCatalogue.RegionalKingscoteIntro;
            Assert.That(ops.AcceptContract(definition).Accepted, Is.True);
            var fleet = new FleetWorkspaceModel();
            fleet.Rebuild(ops, clock.Now, null);
            var contracts = new ContractsWorkspaceModel();
            contracts.Rebuild(ops, clock.Now);
            var stats = new StatsWorkspaceModel();
            stats.Rebuild(ops, clock.Now);

            foreach (var (width, height) in HudTestAirline.Viewports.Concat(new[] { (1920f, 1080f), (2560f, 1440f) }))
            {
                var sheet = HudShell.SideSheet(HudShell.WorkspaceSurface(width, height));
                var draws = new[]
                {
                    Paint(d => FleetWorkspacePainter.Paint(d, fleet, FleetWorkspaceLayout.Create(sheet, fleet.Market.Count), null, 0)),
                    Paint(d => ContractsWorkspacePainter.Paint(d, contracts,
                        ContractsWorkspaceLayout.Create(sheet, contracts.ActiveTerms.Count), null)),
                    Paint(d => StatsWorkspacePainter.Paint(d, stats, StatsWorkspaceLayout.Create(sheet)))
                };
                foreach (var draw in draws)
                    foreach (var c in draw.Commands.Where(c => c.Kind != HudDrawKind.Surface))
                        Assert.That(c.Box.X >= sheet.X - 1f && c.Box.Right <= sheet.Right + 1f, Is.True,
                            $"{width}x{height} {c.Kind} '{c.Text}{c.ActionId}' outside the sheet");
            }
        }

        private static HudDrawList Paint(System.Action<HudDrawList> paint)
        {
            var draw = new HudDrawList();
            paint(draw);
            return draw;
        }
    }
}

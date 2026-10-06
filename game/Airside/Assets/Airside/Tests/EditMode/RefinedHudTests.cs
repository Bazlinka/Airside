using System.Linq;
using Airside.Presentation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class RefinedHudTests
    {
        [TestCase(1280f, 720f)] [TestCase(1440f, 900f)] [TestCase(1920f, 1080f)]
        public void Overview_LeavesCentreClearAndRadarIsOptIn(float width, float height)
        {
            var shell = HudShell.Layout(width, height);
            Assert.That(shell.Operations.IsEmpty && shell.MiniMap.IsEmpty, Is.True);
            Assert.That(shell.SelectedCard.X, Is.GreaterThan(width * .6f));
            Assert.That(shell.Career.Height, Is.LessThanOrEqualTo(68));
            var centre = new HudBox(width * .25f, height * .25f, width * .35f, height * .4f);
            Assert.That(shell.Panels.Where(p => !p.Box.IsEmpty).Any(p => p.Box.Overlaps(centre)), Is.False);
            var radar = HudShell.Layout(width, height, showMiniMap: true);
            Assert.That(radar.MiniMap.IsEmpty, Is.False);
            Assert.That(radar.MiniMap.Overlaps(radar.SelectedCard), Is.False);
        }

        [TestCase(1280f, 720f)] [TestCase(1440f, 900f)]
        public void Inspector_KeepsActionsOutsideScrollingDetails(float width, float height)
        {
            var panel = HudShell.Layout(width, height).SelectedCard;
            var layout = new AircraftInspectorLayout(panel);
            var data = new SelectionCardData { Registration = "VH-PAX", IsPlayer = true, IsMaintenance = true,
                CanFollow = true, ShowCameraActions = true, CanCockpit = true, CanExterior = true };
            for (var i = 0; i < 6; i++) data.Prep.Add(new SelectionPrepStage("Maintenance step", 0, i == 2));
            var list = new HudDrawList(); AircraftInspectorPainter.Footer(list, layout, data);
            Assert.That(list.Commands.Where(c => c.Kind == HudDrawKind.Button).All(c =>
                c.Box.Y >= layout.Footer.Y && c.Box.Bottom <= panel.Bottom && !c.Box.Overlaps(layout.Body)), Is.True);
            Assert.That(list.Commands.Single(c => c.ActionId == HudAction.CardFollow).Text, Is.EqualTo("Follow aircraft"));
        }

        [Test]
        public void CompactCareer_BaseActionDoesNotOverlapLiveryChoices()
        {
            var (clock, ops, _) = HudTestAirline.Create();
            var model = new StatsWorkspaceModel(); model.Rebuild(ops, clock.Now);
            var layout = StatsWorkspaceLayout.Create(HudShell.WorkspaceSurface(800, 600));
            var list = new HudDrawList(); StatsWorkspacePainter.Paint(list, model, layout);
            var upgrade = list.Commands.Single(c => c.ActionId == HudAction.UpgradeBase).Box;
            var swatches = list.Commands.Where(c => c.Kind == HudDrawKind.Hotspot && c.ActionId.StartsWith(HudAction.LiveryPrefix));
            Assert.That(swatches.All(c => !c.Box.Overlaps(upgrade)), Is.True);
            Assert.That(list.Commands.Where(c => c.Kind == HudDrawKind.Button || c.Kind == HudDrawKind.Hotspot)
                .All(c => c.Box.Bottom <= layout.Surface.Bottom), Is.True);
        }

        [Test]
        public void Inspector_PreservesBlockerNavigationAndWorkspaceFeedback()
        {
            var shell = HudShell.Layout(1440, 900, workspaceOpen: true);
            Assert.That(shell.Toast.IsEmpty, Is.False);
            Assert.That(shell.Toast.Overlaps(shell.Workspace), Is.False);
            var data = new SelectionCardData { Registration = "VH-BLOCKER", BackRegistration = "VH-PAX" };
            var list = new HudDrawList();
            AircraftInspectorPainter.Header(list, new AircraftInspectorLayout(HudShell.Layout(1440, 900).SelectedCard), data);
            Assert.That(list.Commands.Any(c => c.Kind == HudDrawKind.Button && c.ActionId == HudAction.SelectPrefix + "VH-PAX"), Is.True);
        }

        [Test]
        public void Shell_ProvidesOverviewRadarAndMenuActionsWithoutSpeedControls()
        {
            var list = new HudDrawList(); HudShellPainter.PaintControls(list, HudShell.Layout(1440, 900).Capsule, false);
            var actions = list.Commands.Where(c => c.Kind == HudDrawKind.Button).Select(c => c.ActionId).ToArray();
            Assert.That(actions, Is.EquivalentTo(new[] { HudShellPainter.OverviewAction, HudShellPainter.MiniMapAction, HudShellPainter.MenuAction }));
        }
    }
}

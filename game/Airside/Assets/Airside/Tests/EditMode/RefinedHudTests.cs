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

        [Test]
        public void Inspector_FitsAParkedAircraftAndKeepsActionsInsideTheShorterPanel()
        {
            var full = HudShell.Layout(1440f, 900f).SelectedCard;
            var data = new SelectionCardData { Registration = "VH-PAA", IsPlayer = true, CanFollow = true,
                ShowCameraActions = true, CanExterior = true };
            var fitted = AircraftInspectorLayout.FittedHeight(full.Height, AircraftInspectorPainter.ContentHeight(data));
            Assert.That(fitted, Is.LessThan(full.Height * .7f), "a parked aircraft gets a short panel");
            var panel = new HudBox(full.X, full.Y, full.Width, fitted);
            var layout = new AircraftInspectorLayout(panel);
            Assert.That(layout.Body.Height, Is.GreaterThanOrEqualTo(AircraftInspectorPainter.ContentHeight(data) - 1f));
            var list = new HudDrawList(); AircraftInspectorPainter.Footer(list, layout, data);
            Assert.That(list.Commands.Where(c => c.Kind == HudDrawKind.Button).All(c =>
                c.Box.Y >= layout.Footer.Y && c.Box.Bottom <= panel.Bottom), Is.True);

            for (var i = 0; i < 12; i++) data.Prep.Add(new SelectionPrepStage("Step", 0, false));
            Assert.That(AircraftInspectorLayout.FittedHeight(full.Height, AircraftInspectorPainter.ContentHeight(data)),
                Is.EqualTo(full.Height), "long details still use the full height and scroll");
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

        [TestCase(340f, false)] [TestCase(340f, true)]
        [TestCase(400f, false)] [TestCase(400f, true)]
        [TestCase(500f, false)] [TestCase(500f, true)]
        public void Inspector_CompactPanelsKeepEveryActionDistinct(float height, bool canCancel)
        {
            var panel = new HudBox(100f, 100f, HudShell.SelectedCardWidth, height);
            var layout = new AircraftInspectorLayout(panel);
            var data = new SelectionCardData { Registration = "VH-PAX", IsPlayer = true, PhaseLabel = "Available",
                PrimaryLabel = "Plan flight", CanCancel = canCancel, CanFollow = true,
                ShowCameraActions = true, CanCockpit = true, CanPassenger = false, CanExterior = true };
            var header = new HudDrawList();
            AircraftInspectorPainter.Header(header, layout, data);
            Assert.That(header.Commands.Where(c => c.Kind != HudDrawKind.Surface)
                .All(c => c.Box.Bottom <= layout.Header.Bottom), Is.True,
                "identity and close must remain above the scrolling details");
            var footer = new HudDrawList();
            AircraftInspectorPainter.Footer(footer, layout, data);
            var buttons = footer.Commands.Where(c => c.Kind == HudDrawKind.Button).ToArray();
            Assert.That(buttons, Has.Length.EqualTo(5));
            foreach (var button in buttons)
            {
                Assert.That(button.Box.Y, Is.GreaterThanOrEqualTo(layout.Footer.Y), button.ActionId);
                Assert.That(button.Box.Bottom, Is.LessThanOrEqualTo(layout.Footer.Bottom), button.ActionId);
                Assert.That(button.Box.Overlaps(layout.Body), Is.False, button.ActionId);
            }
            for (var i = 0; i < buttons.Length; i++)
            for (var j = i + 1; j < buttons.Length; j++)
                Assert.That(buttons[i].Box.Overlaps(buttons[j].Box), Is.False,
                    buttons[i].ActionId + " overlaps " + buttons[j].ActionId);
            Assert.That(buttons.Single(c => c.ActionId == "camera-passenger").Enabled, Is.False,
                "an unavailable camera target must remain disabled");
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

using System.Linq;
using Airside.Domain;
using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class InterfaceRefreshTests
    {
        [TestCase(1280f, 720f)]
        [TestCase(1440f, 900f)]
        [TestCase(1920f, 1080f)]
        [TestCase(800f, 600f)]
        public void ViewingDock_PreservesTheSightlineAndContainsAllControls(float width, float height)
        {
            var layout = new FlightViewHudLayout(width, height);
            Assert.That(layout.Identity.Bottom, Is.LessThan(layout.Controls.Y));
            var data = new FlightViewHudData
            {
                Registration = "VH-PAX", Aircraft = "Saab 340B", Phase = "Cruise", Route = "ADL → KGC",
                Speed = "224 kt", VerticalSpeed = "0 ft/min", Distance = "85 km", SelectedView = 0
            };
            data.Available[0] = data.Available[3] = true;
            var draw = new HudDrawList();
            FlightViewHudPainter.Paint(draw, layout, data);
            var buttons = draw.Commands.Where(c => c.Kind == HudDrawKind.Button).ToArray();
            foreach (var control in buttons)
            {
                Assert.That(control.Box.X, Is.GreaterThanOrEqualTo(0));
                Assert.That(control.Box.Right, Is.LessThanOrEqualTo(width));
                Assert.That(control.Box.Y, Is.GreaterThanOrEqualTo(0));
                Assert.That(control.Box.Bottom, Is.LessThanOrEqualTo(height));
            }
            for (var i = 0; i < buttons.Length; i++)
            for (var j = i + 1; j < buttons.Length; j++)
                Assert.That(buttons[i].Box.Overlaps(buttons[j].Box), Is.False, buttons[i].ActionId + " / " + buttons[j].ActionId);
            Assert.That(buttons.Single(c => c.ActionId == FlightViewHudPainter.ViewPrefix + "1").Enabled, Is.False);
            Assert.That(buttons.Single(c => c.ActionId == FlightViewHudPainter.ViewPrefix + "3").Enabled, Is.True);
        }

        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void AircraftCard_ViewActionsStayBelowOperationalActions(bool player, bool awaitingStand)
        {
            var data = new SelectionCardData
            {
                Registration = "VH-PAX", TypeName = "Saab 340B", IsPlayer = player,
                AwaitingStand = awaitingStand, PrimaryLabel = "Plan flight", CanCancel = true,
                ShowCameraActions = true, CanCockpit = true, CanPassenger = true, CanExterior = true
            };
            data.Stands.Add(new SelectionStandChoice("50D", "Bay 50D", true));
            var box = new HudBox(20, 20, 530, SelectionCardPainter.HeightFor(data));
            var draw = new HudDrawList();
            SelectionCardPainter.Paint(draw, box, data);
            var buttons = draw.Commands.Where(c => c.Kind == HudDrawKind.Button).ToArray();
            foreach (var control in buttons)
                Assert.That(control.Box.Bottom, Is.LessThanOrEqualTo(box.Bottom));
            for (var i = 0; i < buttons.Length; i++)
            for (var j = i + 1; j < buttons.Length; j++)
                Assert.That(buttons[i].Box.Overlaps(buttons[j].Box), Is.False,
                    buttons[i].ActionId + " / " + buttons[j].ActionId);
            Assert.That(buttons.Count(c => c.ActionId.StartsWith("camera-")), Is.EqualTo(3));
        }

        [TestCase(1280f, 720f)]
        [TestCase(800f, 600f)]
        public void ReturnReport_KeepsActionsBelowItsScrollableFleet(float width, float height)
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = AirlineOperations.StartAtAdelaide(clock, new SeededRandomSource(7), Airline.Player("Bight Air", "#39708A"));
            var before = AirlineSave.Capture(ops);
            var summary = AwaySummary.Build(before, ops, 3600);
            var layout = new ReturnBriefingLayout(new HudBox(20, 20, width - 40, height - 40));
            var draw = new HudDrawList();
            ReturnBriefingPainter.Paint(draw, layout, summary, ops.PlayerAirline.Name);
            Assert.That(layout.Fleet.Bottom, Is.LessThan(layout.Continue.Y));
            Assert.That(layout.Surface.Bottom, Is.LessThanOrEqualTo(height));
            Assert.That(layout.Continue.Right, Is.LessThanOrEqualTo(layout.Surface.Right));
            Assert.That(draw.Commands.Any(c => c.ActionId == ReturnBriefingPainter.ContinueAction), Is.True);
            Assert.That(draw.Commands.Any(c => c.Text == "NET FUNDS"), Is.True);
            Assert.That(summary.NetFunds, Is.Zero);
        }
    }
}

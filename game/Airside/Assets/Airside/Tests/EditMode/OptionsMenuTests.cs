using System.Linq;
using Airside.Presentation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class OptionsMenuTests
    {
        [TestCase(800f, 600f)]
        [TestCase(1024f, 640f)]
        [TestCase(1440f, 900f)]
        public void FiveRowsAndEveryNavigationActionFitThePanel(float width, float height)
        {
            var model = new OptionsMenuModel { Section = OptionsSection.World, FromTitle = true };
            for (var i = 0; i < 5; i++)
                model.Rows.Add(new OptionsRow("Live Adelaide weather", "ON", "Changes take effect the next time you launch Airside.", "option:" + i));
            var panel = OptionsMenuPainter.Panel(width, height);
            var draw = new HudDrawList();
            OptionsMenuPainter.Paint(draw, panel, model);
            foreach (var c in draw.Commands.Where(c => c.Kind != HudDrawKind.Line))
            {
                Assert.That(c.Box.X, Is.GreaterThanOrEqualTo(panel.X));
                Assert.That(c.Box.Y, Is.GreaterThanOrEqualTo(panel.Y));
                Assert.That(c.Box.Right, Is.LessThanOrEqualTo(panel.Right));
                Assert.That(c.Box.Bottom, Is.LessThanOrEqualTo(panel.Bottom));
            }
            Assert.That(draw.Commands.Count(c => c.Kind == HudDrawKind.Button), Is.EqualTo(11));
            Assert.That(draw.Commands.Single(c => c.ActionId == OptionsMenuPainter.Back).Text, Is.EqualTo("BACK TO TITLE"));
        }

        [Test]
        public void InGameOptionsReturnLabelDoesNotClaimToLeaveTheAirline()
        {
            var model = new OptionsMenuModel();
            var draw = new HudDrawList();
            OptionsMenuPainter.Paint(draw, OptionsMenuPainter.Panel(1440f, 900f), model);
            Assert.That(draw.Commands.Single(c => c.ActionId == OptionsMenuPainter.Back).Text, Is.EqualTo("BACK"));
        }
    }
}

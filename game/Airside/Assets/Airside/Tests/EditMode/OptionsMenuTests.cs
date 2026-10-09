using System.Linq;
using Airside.Presentation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class OptionsMenuTests
    {
        [Test]
        public void CompactOptionsKeepDescriptionsClearOfTheNextRowAndFooter()
        {
            var model = new OptionsMenuModel();
            for (var i = 0; i < 6; i++) model.Rows.Add(new OptionsRow("Labels", "ON", "A wrapped description.", "row:" + i));
            var viewport = OptionsMenuPainter.RowsViewport(OptionsMenuPainter.Panel(800f, 600f));
            Assert.That(model.Rows.Count * OptionsMenuPainter.RowHeight, Is.GreaterThan(viewport.Height), "runtime must scroll rather than compress");
            var draw = new HudDrawList();
            OptionsMenuPainter.PaintRows(draw, new HudBox(0f, 0f, viewport.Width - 18f, model.Rows.Count * OptionsMenuPainter.RowHeight), model);
            var details = draw.Commands.Where(c => c.Kind == HudDrawKind.Text && c.Text == "A wrapped description.").ToArray();
            for (var i = 1; i < details.Length; i++)
                Assert.That(details[i - 1].Box.Bottom, Is.LessThan(i * OptionsMenuPainter.RowHeight));
            Assert.That(draw.Commands.Count(c => c.Kind == HudDrawKind.Button), Is.EqualTo(6));
        }
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
            Assert.That(draw.Commands.Count(c => c.Kind == HudDrawKind.Button), Is.EqualTo(OptionsMenuPainter.Sections.Length + 6), "section tabs, five rows and Back");
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

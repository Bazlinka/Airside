using Airside.Presentation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>Controls help sheet content (presentation copy only).</summary>
    public sealed class ControlsHelpTests
    {
        [Test]
        public void Sections_CoverCameraAirlineAndGeneral()
        {
            Assert.That(ControlsHelp.Sections.Count, Is.EqualTo(3));
            Assert.That(ControlsHelp.Sections[0].Title, Is.EqualTo("Camera"));
            Assert.That(ControlsHelp.Sections[1].Title, Is.EqualTo("Airline"));
            Assert.That(ControlsHelp.Sections[2].Title, Is.EqualTo("General"));
            Assert.That(ControlsHelp.TotalBindings(), Is.GreaterThanOrEqualTo(12));
        }

        [Test]
        public void IncludesKey_DocumentsPlaytestHotkeys()
        {
            Assert.That(ControlsHelp.IncludesKey("Tab"), Is.True);
            Assert.That(ControlsHelp.IncludesKey("H"), Is.True);
            Assert.That(ControlsHelp.IncludesKey("T"), Is.True);
            Assert.That(ControlsHelp.IncludesKey("C"), Is.True);
            Assert.That(ControlsHelp.IncludesKey("F8"), Is.True);
            Assert.That(ControlsHelp.IncludesKey("F1"), Is.True);
            Assert.That(ControlsHelp.IncludesKey("Esc"), Is.True);
            Assert.That(ControlsHelp.IncludesKey("M"), Is.True);
            Assert.That(ControlsHelp.IncludesKey("F"), Is.True);
            Assert.That(ControlsHelp.IncludesKey("WASD"), Is.True);
            Assert.That(ControlsHelp.IncludesKey("XYZ"), Is.False);
        }
    }
}

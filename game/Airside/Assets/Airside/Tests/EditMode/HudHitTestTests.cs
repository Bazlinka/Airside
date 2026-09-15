using System.Collections.Generic;
using Airside.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Airside.Tests
{
    public sealed class HudHitTestTests
    {
        [Test]
        public void ToGui_FlipsTheOriginAndRemovesHudScale()
        {
            var gui = HudHitTest.ToGui(new Vector2(200f, 100f), 900f, 2f);
            Assert.That(gui.x, Is.EqualTo(100f).Within(0.001f));
            Assert.That(gui.y, Is.EqualTo(400f).Within(0.001f));
        }

        [Test]
        public void ToGui_TreatsAZeroScaleAsOne()
        {
            Assert.That(HudHitTest.ToGui(new Vector2(10f, 10f), 100f, 0f), Is.EqualTo(new Vector2(10f, 90f)));
        }

        [Test]
        public void IsOverHud_CountsFieldTagsAndToastAsHud()
        {
            var panels = new List<Rect> { new(0f, 0f, 100f, 50f) };
            var overlays = new List<Rect> { new(400f, 300f, 80f, 20f) };

            // A press on a field tag (origin bottom-left, screen 900 tall, scale 1).
            Assert.That(HudHitTest.IsOverHud(new Vector2(420f, 590f), 900f, 1f, panels, overlays), Is.True);
            // The same press with no overlays registered falls through to the field.
            Assert.That(HudHitTest.IsOverHud(new Vector2(420f, 590f), 900f, 1f, panels, null), Is.False);
            // Panels still count.
            Assert.That(HudHitTest.IsOverHud(new Vector2(50f, 880f), 900f, 1f, panels, overlays), Is.True);
            // Open field.
            Assert.That(HudHitTest.IsOverHud(new Vector2(700f, 200f), 900f, 1f, panels, overlays), Is.False);
        }

        [Test]
        public void Contains_HandlesMissingLists()
        {
            Assert.That(HudHitTest.Contains(Vector2.zero, null, null), Is.False);
        }
    }
}

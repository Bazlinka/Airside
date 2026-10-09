using Airside.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Airside.Tests
{
    public sealed class AirsideDisplayTests
    {
        [Test]
        public void StaleSmallMaximisedWindow_IsBelowNative()
        {
            Assert.That(AirsideDisplay.IsBelowNative(FullScreenMode.MaximizedWindow, 1600, 900, 3456, 2234), Is.True);
            Assert.That(AirsideDisplay.IsBelowNative(FullScreenMode.FullScreenWindow, 1920, 1080, 3456, 2234), Is.True);
        }

        [Test]
        public void NativeOrWindowed_IsLeftAlone()
        {
            Assert.That(AirsideDisplay.IsBelowNative(FullScreenMode.FullScreenWindow, 3456, 2168, 3456, 2234), Is.False);
            Assert.That(AirsideDisplay.IsBelowNative(FullScreenMode.Windowed, 1600, 900, 3456, 2234), Is.False);
            Assert.That(AirsideDisplay.IsBelowNative(FullScreenMode.MaximizedWindow, 1600, 900, 0, 0), Is.False);
        }
    }
}

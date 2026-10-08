using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Airside.Domain;
using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace Airside.Tests
{
    public sealed class OpeningOptionsNativeTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void AllExistingAndNewOptionsAreAvailableExactlyOnce()
        {
            System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(AirsidePrototype).TypeHandle);
            var host = new GameObject("Options coverage");
            host.SetActive(false);
            try
            {
                var prototype = host.AddComponent<AirsidePrototype>();
                var model = (OptionsMenuModel)typeof(AirsidePrototype).GetField("_optionsModel", Private).GetValue(prototype);
                var actions = new List<string>();
                foreach (var section in new[] { OptionsSection.General, OptionsSection.Camera, OptionsSection.Display, OptionsSection.World })
                {
                    model.Section = section;
                    typeof(AirsidePrototype).GetMethod("FillOptionsModel", Private).Invoke(prototype, null);
                    Assert.That(model.Rows.Count, Is.LessThanOrEqualTo(5));
                    actions.AddRange(model.Rows.Select(row => row.Action));
                }
                Assert.That(actions.Distinct().Count(), Is.EqualTo(17));
                Assert.That(actions.Count, Is.EqualTo(17));
                Assert.That(actions, Does.Contain("options:cockpit"));
                Assert.That(actions, Does.Contain("options:opening"));
            }
            finally { Object.DestroyImmediate(host); }
        }

        [TestCase(true)]
        [TestCase(false)]
        public void OptionsBackClosesTitleOverlayButKeepsInGameMenu(bool fromTitle)
        {
            System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(AirsidePrototype).TypeHandle);
            var host = new GameObject("Options return");
            host.SetActive(false);
            try
            {
                var prototype = host.AddComponent<AirsidePrototype>();
                typeof(AirsidePrototype).GetField("_menuOpen", Private).SetValue(prototype, true);
                typeof(AirsidePrototype).GetField("_optionsOpen", Private).SetValue(prototype, true);
                if (!fromTitle)
                {
                    var clock = new ManualSimulationClock(new SimulationTime(0));
                    var ops = AirlineOperations.StartAtAdelaide(clock, new SeededRandomSource(1), new Airline("TEST", "Options test", "#39708A", true));
                    typeof(AirsidePrototype).GetField("_operations", Private).SetValue(prototype, ops);
                }
                typeof(AirsidePrototype).GetMethod("CloseOptionsMenu", Private).Invoke(prototype, null);
                Assert.That(typeof(AirsidePrototype).GetField("_optionsOpen", Private).GetValue(prototype), Is.False);
                Assert.That(typeof(AirsidePrototype).GetField("_menuOpen", Private).GetValue(prototype), Is.EqualTo(!fromTitle));
            }
            finally { Object.DestroyImmediate(host); }
        }

        [Test]
        public void TitleOwnsEscapeWithoutOpeningAnInGameResumeMenu()
        {
            System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(AirsidePrototype).TypeHandle);
            var host = new GameObject("Title Escape");
            host.SetActive(false);
            try
            {
                var prototype = host.AddComponent<AirsidePrototype>();
                var handled = typeof(AirsidePrototype).GetMethod("TrySplashBack", Private).Invoke(prototype, null);
                Assert.That(handled, Is.True);
                Assert.That(typeof(AirsidePrototype).GetField("_menuOpen", Private).GetValue(prototype), Is.False);
            }
            finally { Object.DestroyImmediate(host); }
        }

        [Test]
        public void OpeningAnimationPreferenceCanDisableMotionAndKeepsItsDefault()
        {
            const string key = AirsideSettings.PrefPrefix + "openinganimation.v1";
            var existed = PlayerPrefs.HasKey(key);
            var previous = PlayerPrefs.GetInt(key);
            try
            {
                PlayerPrefs.DeleteKey(key);
                Assert.That(AirsideSettings.FromPrefs().OpeningAnimation, Is.True);
                PlayerPrefs.SetInt(key, 0);
                Assert.That(AirsideSettings.FromPrefs().OpeningAnimation, Is.False);
                PlayerPrefs.SetInt(key, 1);
                Assert.That(AirsideSettings.FromPrefs().OpeningAnimation, Is.True);
            }
            finally
            {
                if (existed) PlayerPrefs.SetInt(key, previous); else PlayerPrefs.DeleteKey(key);
            }
        }
    }
}

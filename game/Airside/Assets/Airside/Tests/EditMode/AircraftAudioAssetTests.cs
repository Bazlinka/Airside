using Airside.Domain;
using Airside.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Airside.Tests
{
    public sealed class AircraftAudioAssetTests
    {
        [Test]
        public void EveryProfileLoadsItsOwnThreeFiniteSeamlessEngineLayers()
        {
            foreach (var spec in AircraftCatalogue.All)
                foreach (var layer in new[] { "idle", "power", "reverse" })
                {
                    var name = AircraftAudioProfiles.For(spec.Type).Resource(layer);
                    var clip = Resources.Load<AudioClip>(name);
                    Assert.That(clip, Is.Not.Null, name);
                    Assert.That(clip.channels, Is.EqualTo(1), name);
                    Assert.That(clip.length, Is.EqualTo(8f).Within(0.01f), name);
                    var data = new float[clip.samples];
                    Assert.That(clip.GetData(data, 0), Is.True, name);
                    double square = 0, step = 0;
                    float peak = 0;
                    var finite = true;
                    for (var i = 0; i < data.Length; i++)
                    {
                        var value = data[i];
                        if (i > 0) step += Mathf.Abs(value - data[i - 1]);
                        finite &= !float.IsNaN(value) && !float.IsInfinity(value);
                        square += value * value;
                        peak = Mathf.Max(peak, Mathf.Abs(value));
                    }
                    Assert.That(finite, Is.True, name);
                    Assert.That(peak, Is.LessThan(0.84f), name);
                    Assert.That(square / data.Length, Is.GreaterThan(0.001), name);
                    Assert.That(Mathf.Abs(data[0] - data[data.Length - 1]), Is.LessThan(System.Math.Max(0.006, step / (data.Length - 1) * 3)), name);
                }
            Assert.That(Resources.Load<AudioClip>(AircraftSoundEmitter.TouchdownResource), Is.Not.Null);
            Assert.That(Resources.Load<AudioClip>(AircraftSoundEmitter.RollResource), Is.Not.Null);
        }

        [Test]
        public void InteriorVoicesDisableDopplerAndRestoreItOnExit()
        {
            var go = new GameObject("Cockpit audio aircraft");
            try
            {
                var emitter = go.AddComponent<AircraftSoundEmitter>();
                emitter.Configure(AircraftType.Saab340, null, null);
                var sources = go.GetComponentsInChildren<AudioSource>();
                emitter.InteriorListening = true;
                // Zero gain avoids playback; the actual mix still configures every voice.
                emitter.Apply("cockpit", 1f, 1f, 1f, 1f, 0f, 30f, false, false,
                    Vector3.zero, 0f, false, 0.1f);
                foreach (var source in sources) Assert.That(source.dopplerLevel, Is.Zero);
                emitter.InteriorListening = false;
                emitter.Apply("cockpit", 1f, 1f, 1f, 1f, 0f, 30f, false, false,
                    Vector3.zero, 0f, false, 0.1f);
                foreach (var source in sources) Assert.That(source.dopplerLevel, Is.EqualTo(0.2f).Within(0.001f));
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void MutingCullingAndDisablingImmediatelySilenceEverySpatialLayer()
        {
            var go = new GameObject("Audio test aircraft");
            try
            {
                var emitter = go.AddComponent<AircraftSoundEmitter>();
                emitter.Configure(AircraftType.AirbusA350900, null, null);
                var sources = go.GetComponentsInChildren<AudioSource>();
                Assert.That(sources.Length, Is.EqualTo(5));
                foreach (var source in sources)
                {
                    Assert.That(source.spatialBlend, Is.EqualTo(1f));
                    Assert.That(source.playOnAwake, Is.False);
                    Assert.That(source.volume, Is.Zero, "spawn is quiet");
                    source.volume = 0.4f;
                }
                emitter.Apply("test", 1f, 1f, 1f, 1f, 1f, 65f, true, true, Vector3.zero, 1f, true, 0.1f);
                foreach (var source in sources) Assert.That(source.volume, Is.Zero, "muted");
                foreach (var source in sources) source.volume = 0.4f;
                emitter.Apply("test", 1f, 1f, 1f, 1f, 1f, 65f, true, true, new Vector3(10000f, 0f, 0f), 1f, false, 0.1f);
                foreach (var source in sources) Assert.That(source.volume, Is.Zero, "beyond hearing range");
                foreach (var source in sources) source.volume = 0.4f;
                go.SetActive(false);
                // EditMode does not run ordinary MonoBehaviour lifecycle callbacks;
                // exercise the inactive update guard here, and player capture covers OnDisable.
                emitter.Apply("test", 1f, 1f, 1f, 1f, 1f, 65f, true, true, Vector3.zero, 1f, false, 0.1f);
                foreach (var source in sources) Assert.That(source.volume, Is.Zero, "hidden aircraft");
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}

using System.Linq;
using Airside.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Airside.Tests
{
    /// <summary>
    /// The Quaternius people must draw at human size and in their own colours. Both were broken
    /// on main without any test noticing: people were under 2 cm tall and the ramp crew had no
    /// materials, so only their wands and crates were visible.
    /// </summary>
    public sealed class CharacterFigureTests
    {
        private static readonly string[] Characters =
        {
            "chr_passenger_m_casual", "chr_passenger_f_casual", "chr_passenger_m_hoodie", "chr_passenger_f_formal",
            "chr_passenger_m_suit", "chr_passenger_f_suit", "chr_passenger_m_holiday",
            "chr_ramp_m_worker", "chr_ramp_f_worker"
        };

        [TestCaseSource(nameof(Characters))]
        public void Figure_MeasuresAsAPerson_AndWalksAtTheGivenHeight(string id)
        {
            var prefab = Resources.Load<GameObject>("Airside/Characters/" + id);
            Assert.That(prefab, Is.Not.Null);
            var figure = Object.Instantiate(prefab);
            try
            {
                var measured = AirsidePrototype.MeasureFigureHeight(figure);
                Assert.That(measured, Is.GreaterThan(0.1f));

                const float height = 1.75f;
                figure.transform.localScale = Vector3.one * (height / measured);
                var walk = Resources.LoadAll<AnimationClip>("Airside/Characters/" + id)
                    .First(c => c.name.EndsWith("Walk"));
                walk.SampleAnimation(figure, walk.length * 0.3f);
                var head = figure.GetComponentsInChildren<Transform>(true).First(t => t.name == "Head");
                Assert.That(head.position.y, Is.InRange(height * 0.7f, height), "head at head height while walking");
                Assert.That(AirsidePrototype.MeasureFigureHeight(figure), Is.InRange(height * 0.9f, height * 1.08f));
            }
            finally
            {
                Object.DestroyImmediate(figure);
            }
        }

        [TestCaseSource(nameof(Characters))]
        public void Figure_HasItsOwnMaterials(string id)
        {
            var prefab = Resources.Load<GameObject>("Airside/Characters/" + id);
            var materials = prefab.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).ToArray();
            Assert.That(materials, Is.Not.Empty);
            Assert.That(materials, Has.None.Null, "every slot is remapped to Characters/Materials");
            Assert.That(materials.All(m => m.name.StartsWith(id + "-")), Is.True);
        }
    }
}

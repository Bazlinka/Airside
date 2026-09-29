using System.Linq;
using Airside.Presentation;
using Airside.Simulation;
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
        [TestCase(RampTask.MarshalArrival, "Wave")]
        [TestCase(RampTask.WingWalk, "Wave")]
        [TestCase(RampTask.FuelCoupling, "Interact")]
        [TestCase(RampTask.BoardingSupervision, "Idle_Neutral")]
        public void RampEquipment_IsHeldInTheHands(RampTask task, string clipSuffix)
        {
            // The old equipment was placed at fixed offsets from the feet, so the wands floated
            // beside the marshaller instead of moving with the waving arm.
            var figure = WorkerAt(1.75f);
            var parent = new GameObject("Equipment").transform;
            try
            {
                var clip = Resources.LoadAll<AnimationClip>("Airside/Characters/chr_ramp_m_worker")
                    .First(c => c.name.EndsWith(clipSuffix));
                var bones = figure.GetComponentsInChildren<Transform>(true);
                var rightWrist = bones.First(b => b.name == "Wrist.R");
                var kit = HandTools.BuildRampKit(task, parent);
                var rig = FigureRig.Find(figure);
                Assert.That(rig, Is.Not.Null);
                foreach (var t in new[] { 0.1f, 0.45f, 0.8f })
                {
                    clip.SampleAnimation(figure, t * clip.length);
                    HandTools.Pose(kit, rig);
                    Assert.That(Vector3.Distance(kit.Right.position, rightWrist.position), Is.LessThan(0.14f),
                        "right fist follows the right wrist through the clip");
                    Assert.That(Vector3.Distance(kit.RightUpright.position, rightWrist.position), Is.LessThan(0.14f));
                }

                var held = kit.Right.GetComponentsInChildren<Renderer>().Length
                           + kit.RightUpright.GetComponentsInChildren<Renderer>().Length;
                Assert.That(held, Is.GreaterThan(0), $"{task} puts something in the right hand");
                if (kit.Hose != null)
                    Assert.That(kit.Hose[^1].position.y, Is.LessThan(0.2f), "fuel hose runs down to the apron");
            }
            finally
            {
                Object.DestroyImmediate(parent.gameObject);
                Object.DestroyImmediate(figure);
            }
        }

        [Test]
        public void EveryRampJob_HasEquipmentAndEarDefenders()
        {
            var parent = new GameObject("Equipment").transform;
            try
            {
                foreach (RampTask task in System.Enum.GetValues(typeof(RampTask)))
                {
                    var kit = HandTools.BuildRampKit(task, parent);
                    Assert.That(kit.Headset.GetComponentsInChildren<Renderer>().Length, Is.EqualTo(2), task.ToString());
                    Assert.That(kit.Root.GetComponentsInChildren<Renderer>().Length, Is.GreaterThan(2), task.ToString());
                }
            }
            finally
            {
                Object.DestroyImmediate(parent.gameObject);
            }
        }

        [Test]
        public void RollerBag_RollsOnTheApronAndIsLiftedOnStairs()
        {
            var figure = Object.Instantiate(Resources.Load<GameObject>("Airside/Characters/chr_passenger_m_suit"));
            figure.transform.localScale = Vector3.one * (1.75f / AirsidePrototype.MeasureFigureHeight(figure));
            var parent = new GameObject("Luggage").transform;
            try
            {
                var walk = Resources.LoadAll<AnimationClip>("Airside/Characters/chr_passenger_m_suit").First(c => c.name.EndsWith("Walk"));
                walk.SampleAnimation(figure, 0.3f * walk.length);
                var bag = HandTools.BuildPassengerBag(0, parent);
                var rig = FigureRig.Find(figure);
                HandTools.Pose(bag, rig);
                var wheels = bag.Roller.GetComponentsInChildren<Transform>().Where(t => t.name == "Wheel").ToArray();
                Assert.That(wheels.Min(w => w.position.y), Is.InRange(0f, 0.06f), "wheels on the ground");
                Assert.That(Vector3.Dot(bag.Roller.position - figure.transform.position, figure.transform.forward), Is.LessThan(0f),
                    "towed behind");
                HandTools.Pose(bag, rig, lifted: true);
                Assert.That(bag.Roller.gameObject.activeSelf, Is.False);
                Assert.That(bag.RightHanging.gameObject.activeSelf, Is.True);
                Assert.That(HandTools.BuildPassengerBag(3, parent), Is.Null, "some passengers travel empty-handed");
            }
            finally
            {
                Object.DestroyImmediate(parent.gameObject);
                Object.DestroyImmediate(figure);
            }
        }

        private static GameObject WorkerAt(float height)
        {
            var figure = Object.Instantiate(Resources.Load<GameObject>("Airside/Characters/chr_ramp_m_worker"));
            figure.transform.localScale = Vector3.one * (height / AirsidePrototype.MeasureFigureHeight(figure));
            return figure;
        }
    }
}

using Airside.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Airside.Tests
{
    public sealed class AirsideNamedChildrenTests
    {
        [Test]
        public void Names_MatchTransformsIndexForIndex_AndForgetRefreshesThem()
        {
            var root = new GameObject("Aircraft root").transform;
            try
            {
                new GameObject("Gear L").transform.SetParent(root, false);
                var prop = new GameObject("Propeller L").transform;
                prop.SetParent(root, false);
                new GameObject("Blade").transform.SetParent(prop, false);

                var children = AirsideNamedChildren.Get(root);
                var names = AirsideNamedChildren.Names(root);
                Assert.That(names.Length, Is.EqualTo(children.Length));
                for (var i = 0; i < children.Length; i++)
                    Assert.That(names[i], Is.EqualTo(children[i].name));
                Assert.That(AirsideNamedChildren.HasName(root, "Blade"), Is.True);
                Assert.That(AirsideNamedChildren.FindContains(root, "propeller"), Is.SameAs(prop));

                // Names are read once; a rebuilt hierarchy must Forget to be re-read.
                prop.name = "Propeller R";
                Assert.That(AirsideNamedChildren.FindContains(root, "Propeller R"), Is.Null);
                AirsideNamedChildren.Forget(root);
                Assert.That(AirsideNamedChildren.FindContains(root, "Propeller R"), Is.SameAs(prop));
            }
            finally
            {
                AirsideNamedChildren.Forget(root);
                Object.DestroyImmediate(root.gameObject);
            }
        }
    }
}

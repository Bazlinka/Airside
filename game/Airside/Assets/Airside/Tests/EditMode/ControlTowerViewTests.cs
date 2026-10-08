using Airside.Presentation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class ControlTowerViewTests
    {
        [Test]
        public void Tower_ExistsAndEyeIsInTheCabAboveTheShaft()
        {
            Assert.IsTrue(ControlTowerView.TryFind(out var tower));
            var pose = ControlTowerView.PoseFor(tower, 10f, 0f, 0f);
            Assert.Greater(pose.Y, 10f + tower.HeightMetres * 0.7f);
            Assert.Less(pose.Y, 10f + tower.HeightMetres);
        }

        [Test]
        public void FacingPointsAtTheLookTarget()
        {
            ControlTowerView.TryFind(out var tower);
            var pose = ControlTowerView.PoseFor(tower, 0f, 0f, 0f);
            // The tower sits about (617, 512) from the origin, so facing the origin looks south-west.
            Assert.AreEqual(-129.7f, pose.FacingDegrees, 1.5f);
        }

        [Test]
        public void RayThroughTheShaft_Hits_AndAMissBesideItDoesNot()
        {
            ControlTowerView.TryFind(out var tower);
            var pose = ControlTowerView.PoseFor(tower, 0f, 0f, 0f);
            Assert.IsTrue(ControlTowerView.RayHits(pose, pose.X - 200f, 20f, pose.Z, 1f, 0f, 0f, 1000f, out var d));
            Assert.AreEqual(200f - ControlTowerView.PickRadiusMetres, d, 0.01f);
            Assert.IsFalse(ControlTowerView.RayHits(pose, pose.X - 200f, 20f, pose.Z + 40f, 1f, 0f, 0f, 1000f, out _));
        }

        [Test]
        public void RayOverTheTop_OrBehind_DoesNotHit()
        {
            ControlTowerView.TryFind(out var tower);
            var pose = ControlTowerView.PoseFor(tower, 0f, 0f, 0f);
            Assert.IsFalse(ControlTowerView.RayHits(pose, pose.X - 200f, pose.TopY + 30f, pose.Z, 1f, 0f, 0f, 1000f, out _));
            Assert.IsFalse(ControlTowerView.RayHits(pose, pose.X + 200f, 20f, pose.Z, 1f, 0f, 0f, 1000f, out _));
        }
    }
}

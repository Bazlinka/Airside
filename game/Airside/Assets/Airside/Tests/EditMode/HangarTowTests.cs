using System;
using Airside.Domain;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>ADR 0186: a check tows the aircraft to a hangar and back; the stand and timer are untouched.</summary>
    public sealed class HangarTowTests
    {
        private const double Check = 4 * 3600.0;

        [Test]
        public void EveryRegionalBay_HasAHangarForTheTurboprops()
        {
            foreach (var bay in AdelaideLayout.Bays)
                foreach (var spec in new[] { AircraftCatalogue.Saab340, AircraftCatalogue.Atr42, AircraftCatalogue.Dash8Q400 })
                    Assert.That(HangarTow.TryPlan(spec.Type, new StableId(bay.Id), out var plan), Is.True, $"{bay.Id} {spec.Id}");
        }

        [Test]
        public void TurbopropsGoToRegionalExpress_AndJetsToCobham()
        {
            var bay = new StableId(AdelaideLayout.Bays[0].Id);
            Assert.That(HangarTow.TryPlan(AircraftCatalogue.Saab340.Type, bay, out var saab), Is.True);
            Assert.That(saab.HangarName, Is.EqualTo("Regional Express"));
            var gate = new StableId(AdelaideGround.TerminalGates[0].Id);
            if (HangarTow.TryPlan(AircraftCatalogue.Boeing737800.Type, gate, out var jet))
                Assert.That(jet.HangarName, Does.StartWith("Cobham"));
        }

        [Test]
        public void TheAircraftStartsOnItsStand_SitsInsideTheHangar_AndReturnsToItsStand()
        {
            var type = AircraftCatalogue.Saab340.Type;
            var bay = new StableId(AdelaideLayout.Bays[0].Id);
            var stand = AdelaideGround.StandPose(bay);
            Assert.That(HangarTow.TryPlan(type, bay, out var plan), Is.True);

            Assert.That(HangarTow.TryPose(type, bay, 0.0, Check, out _), Is.False, "before the check the stand pose applies");
            Assert.That(HangarTow.TryPose(type, bay, Check, Check, out _), Is.False, "after it too");

            Assert.That(HangarTow.TryPose(type, bay, 1.0, Check, out var early), Is.True);
            Assert.That(Distance(early.X, early.Z, stand.X, stand.Z), Is.LessThan(2f));

            Assert.That(HangarTow.TryPose(type, bay, Check * 0.5 - 60, Check, out var inside), Is.True);
            var hangar = Array.Find(AdelaideBuildings.All, b => b.Id == plan.HangarId);
            Assert.That(Inside(hangar.Xz, inside.X, inside.Z), Is.True, "parked inside the hangar outline");

            Assert.That(HangarTow.TryPose(type, bay, Check - 1.0, Check, out var late), Is.True);
            Assert.That(Distance(late.X, late.Z, stand.X, stand.Z), Is.LessThan(2f));
            Assert.That(late.NoseX * stand.NoseX + late.NoseZ * stand.NoseZ, Is.GreaterThan(0.99f), "back on the stand heading");
        }

        [Test]
        public void TheTow_MovesSmoothlyAtTugSpeed()
        {
            var type = AircraftCatalogue.Saab340.Type;
            var bay = new StableId(AdelaideLayout.Bays[0].Id);
            HangarTow.TryPlan(type, bay, out var plan);
            var last = default(GroundPose);
            var have = false;
            for (var t = 1.0; t < plan.TowSeconds; t += 2.0)
            {
                Assert.That(HangarTow.TryPose(type, bay, t, Check, out var pose), Is.True);
                if (have)
                    Assert.That(Distance(pose.X, pose.Z, last.X, last.Z), Is.LessThan(2f * HangarTow.TowLimits.MaxSpeed + 0.5f), $"t={t}");
                last = pose;
                have = true;
            }

            Assert.That(plan.TowSeconds, Is.InRange(30, 1800), "a tow across the airfield takes minutes, not hours");
        }

        [Test]
        public void ACheckTooShortToTow_StaysOnItsStand()
        {
            var bay = new StableId(AdelaideLayout.Bays[0].Id);
            Assert.That(HangarTow.TryPose(AircraftCatalogue.Saab340.Type, bay, 100, 600, out _), Is.False);
        }

        [Test]
        public void PackagedHangarStill_AtNinetySeconds_IsMidOutboundTowNotStillOnStand()
        {
            // remaining.sh follow-hangar-tow uses CAPTURE_DELAY=90. The outbound tow must
            // still be under way (or at least clearly off the stand) at that instant.
            const double captureDelay = 90;
            var type = AircraftCatalogue.Saab340.Type;
            var offStand = 0;
            var midTow = 0;
            foreach (var bayLayout in AdelaideLayout.Bays)
            {
                var bay = new StableId(bayLayout.Id);
                if (!HangarTow.TryPlan(type, bay, out var plan))
                    continue;
                Assert.That(plan.TowSeconds, Is.GreaterThan(captureDelay),
                    $"{bay.Value}: outbound tow {plan.TowSeconds:0}s must outlast the 90s still");
                Assert.That(HangarTow.TryPose(type, bay, captureDelay, Check, out var pose), Is.True,
                    $"{bay.Value}: tow pose at {captureDelay}s");
                var stand = AdelaideGround.StandPose(bay);
                var metres = Distance(pose.X, pose.Z, stand.X, stand.Z);
                if (metres > 15f)
                    offStand++;
                if (captureDelay < plan.TowSeconds && metres > 15f)
                    midTow++;
                TestContext.WriteLine($"{bay.Value}: TowSeconds={plan.TowSeconds:0} at90m={metres:0.0}");
            }

            Assert.That(offStand, Is.GreaterThan(0), "at least one bay is clearly off-stand at 90s");
            Assert.That(midTow, Is.GreaterThan(0), "at least one bay is mid-outbound at 90s");
        }

        private static float Distance(float ax, float az, float bx, float bz) =>
            (float)Math.Sqrt((ax - bx) * (ax - bx) + (az - bz) * (az - bz));

        private static bool Inside(float[] xz, float x, float z)
        {
            var inside = false;
            var n = xz.Length / 2;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                if ((xz[i * 2 + 1] > z) != (xz[j * 2 + 1] > z)
                    && x < (xz[j * 2] - xz[i * 2]) * (z - xz[i * 2 + 1]) / (xz[j * 2 + 1] - xz[i * 2 + 1]) + xz[i * 2])
                    inside = !inside;
            }

            return inside;
        }
    }
}

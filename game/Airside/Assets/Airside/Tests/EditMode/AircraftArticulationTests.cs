using Airside.Presentation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>
    /// The physical-animation pass: control-surface signs and laws, hinge fitting, gear kinematics and
    /// wheel spin. The rules are pure, so the same checks run headless and in the editor; the rotation
    /// senses they imply are checked against real Quaternion maths in <see cref="AircraftArticulationRigTests"/>.
    /// </summary>
    public sealed class AircraftArticulationTests
    {
        [Test]
        public void Elevator_IsTrailingEdgeUp_OnRotationAndFlare_AndDownOnDerotation()
        {
            Assert.That(AircraftArticulation.ElevatorTrailingEdgeUpDegrees(4f, 5f), Is.GreaterThan(5f),
                "rotating the nose up needs up elevator");
            Assert.That(AircraftArticulation.ElevatorTrailingEdgeUpDegrees(4f, -3f), Is.LessThan(
                AircraftArticulation.ElevatorTrailingEdgeUpDegrees(4f, 0f)),
                "the nose coming down eases the elevator");
            Assert.That(AircraftArticulation.ElevatorTrailingEdgeUpDegrees(0f, 0f), Is.EqualTo(0f).Within(1e-4f));
            Assert.That(AircraftArticulation.ElevatorTrailingEdgeUpDegrees(90f, 90f),
                Is.EqualTo(AircraftArticulation.MaxElevatorUpDegrees));
            Assert.That(AircraftArticulation.ElevatorTrailingEdgeUpDegrees(-90f, -90f),
                Is.EqualTo(-AircraftArticulation.MaxElevatorDownDegrees));
        }

        [Test]
        public void Ailerons_RollLeft_LiftsLeftTrailingEdge_AndDropsTheRight()
        {
            var left = AircraftArticulation.AileronTrailingEdgeDownDegrees(8f, rightWing: false);
            var right = AircraftArticulation.AileronTrailingEdgeDownDegrees(8f, rightWing: true);
            Assert.That(left, Is.LessThan(0f), "rolling left raises the left aileron");
            Assert.That(right, Is.GreaterThan(0f), "rolling left lowers the right aileron");
            Assert.That(AircraftArticulation.AileronTrailingEdgeDownDegrees(-8f, true), Is.LessThan(0f),
                "rolling right raises the right aileron");
        }

        [Test]
        public void Ailerons_ReturnToNeutral_OnASteadyBank()
        {
            Assert.That(AircraftArticulation.AileronTrailingEdgeDownDegrees(0f, false), Is.EqualTo(0f).Within(1e-4f));
            Assert.That(AircraftArticulation.AileronTrailingEdgeDownDegrees(0f, true), Is.EqualTo(0f).Within(1e-4f));
        }

        [Test]
        public void Ailerons_StayWithinTravel()
        {
            Assert.That(AircraftArticulation.AileronTrailingEdgeDownDegrees(500f, true),
                Is.EqualTo(AircraftArticulation.MaxAileronDownDegrees));
            Assert.That(AircraftArticulation.AileronTrailingEdgeDownDegrees(500f, false),
                Is.EqualTo(-AircraftArticulation.MaxAileronUpDegrees));
        }

        [Test]
        public void Rudder_FollowsTheRoll_RightRollGivesRightRudder()
        {
            Assert.That(AircraftArticulation.RudderTrailingEdgeRightDegrees(-10f, 0f), Is.GreaterThan(0f));
            Assert.That(AircraftArticulation.RudderTrailingEdgeRightDegrees(10f, 0f), Is.LessThan(0f));
            // A steady bank (no roll rate) holds only a token amount.
            Assert.That(System.Math.Abs(AircraftArticulation.RudderTrailingEdgeRightDegrees(0f, 25f)), Is.LessThan(3f));
            Assert.That(AircraftArticulation.RudderTrailingEdgeRightDegrees(-999f, -999f),
                Is.EqualTo(AircraftArticulation.MaxRudderDegrees));
        }

        [Test]
        public void RollSpoilers_RaiseOnlyOnTheWingGoingDown()
        {
            Assert.That(AircraftArticulation.RollSpoilerDegrees(10f, rightWing: false), Is.GreaterThan(0f));
            Assert.That(AircraftArticulation.RollSpoilerDegrees(10f, rightWing: true), Is.EqualTo(0f));
            Assert.That(AircraftArticulation.RollSpoilerDegrees(-10f, rightWing: true), Is.GreaterThan(0f));
            Assert.That(AircraftArticulation.RollSpoilerDegrees(1f, rightWing: false), Is.EqualTo(0f), "deadband");
            Assert.That(AircraftArticulation.RollSpoilerDegrees(999f, false),
                Is.EqualTo(AircraftArticulation.MaxRollSpoilerDegrees));
        }

        [Test]
        public void HingeAngles_TrailingEdgeDownAndRudderRightAreNegative()
        {
            Assert.That(AircraftArticulation.HingeAngleForTrailingEdgeDown(20f), Is.EqualTo(-20f));
            Assert.That(AircraftArticulation.HingeAngleForRudderRight(12f), Is.EqualTo(-12f));
        }

        [Test]
        public void Droop_OnlyWhenHydraulicsAreOff()
        {
            Assert.That(AircraftArticulation.ParkedDroopDegrees(true), Is.GreaterThan(0f));
            Assert.That(AircraftArticulation.ParkedDroopDegrees(false), Is.EqualTo(0f));
        }

        [Test]
        public void FlapRunsAftOnItsTracks_InProportionToDeflection()
        {
            Assert.That(AircraftArticulation.FlapAftTravelMetres(2.1f, 0f), Is.EqualTo(0f));
            var half = AircraftArticulation.FlapAftTravelMetres(2.1f, 11f);
            var full = AircraftArticulation.FlapAftTravelMetres(2.1f, 22f);
            Assert.That(full, Is.EqualTo(half * 2f).Within(1e-4f));
            Assert.That(full, Is.InRange(0.1f, 0.4f), "a believable fraction of the chord, not a slide off the wing");
            Assert.That(AircraftArticulation.FlapAftTravelMetres(2.1f, -5f), Is.EqualTo(0f));
        }

        // ---- Hinge fit ----------------------------------------------------------------------

        [Test]
        public void FitHingeLine_RecoversTheSweepOfAnAileron()
        {
            // A left aileron's front edge sweeps back with span: z = -19.4 + 0.45 * (x + 14.5), x from -16.6 to -12.5.
            const int steps = 21;
            var x = new float[steps * 2];
            var z = new float[steps * 2];
            for (var i = 0; i < steps; i++)
            {
                var span = -16.6f + 4.1f * i / (steps - 1);
                var front = -19.4f + 0.45f * (span + 14.5f);
                x[i * 2] = span;
                z[i * 2] = front;
                x[i * 2 + 1] = span;
                z[i * 2 + 1] = front - 1.1f; // trailing edge, behind the hinge
            }

            Assert.That(AircraftArticulation.FitHingeLine(x, z, x.Length, out var slope, out var centre,
                out var frontAtCentre), Is.True);
            Assert.That(slope, Is.EqualTo(0.45f).Within(0.03f));
            Assert.That(centre, Is.EqualTo(-14.55f).Within(0.01f));
            Assert.That(frontAtCentre, Is.EqualTo(-19.4f + 0.45f * (-14.55f + 14.5f)).Within(0.06f));
        }

        [Test]
        public void FitHingeLine_AFlatPlate_HasNoSweep()
        {
            var x = new float[] { 0f, 1f, 2f, 3f, 4f, 0f, 1f, 2f, 3f, 4f };
            var z = new float[] { 5f, 5f, 5f, 5f, 5f, 4f, 4f, 4f, 4f, 4f };
            Assert.That(AircraftArticulation.FitHingeLine(x, z, x.Length, out var slope, out var centre,
                out var frontAtCentre), Is.True);
            Assert.That(slope, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(centre, Is.EqualTo(2f).Within(1e-4f));
            Assert.That(frontAtCentre, Is.EqualTo(5f).Within(1e-3f));
        }

        [Test]
        public void FitHingeLine_Degenerate_FallsBackToTheForwardMostVertex()
        {
            var x = new float[] { 1f, 1.05f };
            var z = new float[] { 3f, 4f };
            Assert.That(AircraftArticulation.FitHingeLine(x, z, 2, out var slope, out _, out var front), Is.False);
            Assert.That(slope, Is.EqualTo(0f));
            Assert.That(front, Is.EqualTo(4f));
            Assert.That(AircraftArticulation.FitHingeLine(null, null, 0, out _, out _, out _), Is.False);
        }

        [Test]
        public void FitHingeLine_RejectsAnAbsurdSlant()
        {
            // A chamfered corner would read as a ~70 degree hinge; the sweep of a real surface never is.
            var x = new float[] { 0f, 1f, 2f, 3f, 4f, 5f, 6f, 7f };
            var z = new float[] { 0f, 3f, 6f, 9f, 12f, 15f, 18f, 21f };
            Assert.That(AircraftArticulation.FitHingeLine(x, z, x.Length, out var slope, out _, out var front), Is.False);
            Assert.That(slope, Is.EqualTo(0f));
            Assert.That(front, Is.EqualTo(21f));
        }

        // ---- Gear ---------------------------------------------------------------------------

        [Test]
        public void MainGearStyle_JetsAndSponsonLegsFoldInboard_Dash8GoesAft_SaabForward()
        {
            Assert.That(AircraftArticulation.MainGearStyle(turboprop: true, hasInnerNacelleDoors: false,
                legsOnFuselageSponson: true), Is.EqualTo(GearRetractStyle.Inboard), "ATR 42");
            Assert.That(AircraftArticulation.MainGearStyle(turboprop: false, hasInnerNacelleDoors: false),
                Is.EqualTo(GearRetractStyle.Inboard));
            Assert.That(AircraftArticulation.MainGearStyle(turboprop: true, hasInnerNacelleDoors: true),
                Is.EqualTo(GearRetractStyle.Aft));
            Assert.That(AircraftArticulation.MainGearStyle(turboprop: true, hasInnerNacelleDoors: false),
                Is.EqualTo(GearRetractStyle.Forward), "Saab 340");
        }

        [Test]
        public void GearLeg_IsLockedAtBothEnds_AndMovesMonotonically()
        {
            Assert.That(AircraftArticulation.GearLegSwing01(0f), Is.EqualTo(0f));
            Assert.That(AircraftArticulation.GearLegSwing01(0.08f), Is.EqualTo(0f), "doors open before the leg moves");
            Assert.That(AircraftArticulation.GearLegSwing01(1f), Is.EqualTo(1f));
            Assert.That(AircraftArticulation.GearLegSwing01(0.95f), Is.EqualTo(1f), "locked up before the doors close");
            var last = -1f;
            for (var i = 0; i <= 20; i++)
            {
                var swing = AircraftArticulation.GearLegSwing01(i / 20f);
                Assert.That(swing, Is.GreaterThanOrEqualTo(last));
                last = swing;
            }
        }

        [Test]
        public void GearDoors_AreShutWhenDownOrUp_AndOpenWhileTheLegIsMoving()
        {
            Assert.That(AircraftArticulation.GearDoorOpen01(0f), Is.EqualTo(0f));
            Assert.That(AircraftArticulation.GearDoorOpen01(1f), Is.EqualTo(0f));
            Assert.That(AircraftArticulation.GearDoorOpen01(0.5f), Is.EqualTo(1f).Within(1e-4f));
            // Wide open by the time the leg has begun to move, still open as it arrives.
            Assert.That(AircraftArticulation.GearDoorOpen01(0.12f), Is.GreaterThan(0.99f));
            Assert.That(AircraftArticulation.GearDoorOpen01(0.88f), Is.GreaterThan(0.99f));
        }

        [Test]
        public void GearRetract_RotationAxesAndDirections()
        {
            var nose = AircraftArticulation.GearRetractRotation(GearRetractStyle.Forward, true, 1f);
            Assert.That((nose.AxisX, nose.AxisZ), Is.EqualTo((1f, 0f)));
            Assert.That(nose.Degrees, Is.LessThan(-85f), "forward is a negative pitch about the lateral axis");

            var aft = AircraftArticulation.GearRetractRotation(GearRetractStyle.Aft, true, 1f);
            Assert.That(aft.Degrees, Is.GreaterThan(85f));

            var inboardLeft = AircraftArticulation.GearRetractRotation(GearRetractStyle.Inboard, true, 1f);
            var inboardRight = AircraftArticulation.GearRetractRotation(GearRetractStyle.Inboard, false, 1f);
            Assert.That((inboardLeft.AxisX, inboardLeft.AxisZ), Is.EqualTo((0f, 1f)));
            Assert.That(inboardLeft.Degrees, Is.GreaterThan(80f));
            Assert.That(inboardRight.Degrees, Is.EqualTo(-inboardLeft.Degrees), "mirror images");

            Assert.That(AircraftArticulation.GearRetractRotation(GearRetractStyle.Inboard, true, 0f).Degrees,
                Is.EqualTo(0f), "down and locked is the authored pose");
        }

        [Test]
        public void TruckTilt_StartsAtZero_AndTipsTheForwardAxleUp()
        {
            Assert.That(AircraftArticulation.TruckTiltDegrees(0f), Is.EqualTo(0f));
            Assert.That(AircraftArticulation.TruckTiltDegrees(1f), Is.LessThan(-20f));
            Assert.That(AircraftArticulation.TruckTiltDegrees(1f), Is.GreaterThan(-45f));
        }

        // Real geometry: A320/737 plates beside the legs, the belly panel ahead of the nose leg.
        [TestCase(0.05f, 1.42f, 1.14f, GearDoorKind.LegMounted)]
        [TestCase(0.06f, 0.62f, 1.4f, GearDoorKind.LegMounted)]
        [TestCase(0.10f, 0.84f, 1.20f, GearDoorKind.LegMounted)] // Dash 8 nose door beside the leg
        [TestCase(0.88f, 0.06f, 1.10f, GearDoorKind.Belly)]
        [TestCase(0.78f, 0.08f, 1.45f, GearDoorKind.Belly)]
        public void ClassifyGearDoor_FromRealExtents(float sx, float sy, float sz, GearDoorKind expected)
        {
            Assert.That(AircraftArticulation.ClassifyGearDoor(sx, sy, sz), Is.EqualTo(expected));
        }

        [Test]
        public void BellyDoor_OpensFully_AndShutMeansZero()
        {
            Assert.That(AircraftArticulation.BellyDoorDegrees(0f), Is.EqualTo(0f));
            Assert.That(AircraftArticulation.BellyDoorDegrees(1f), Is.InRange(60f, 85f));
            Assert.That(AircraftArticulation.BellyDoorDegrees(7f), Is.EqualTo(AircraftArticulation.BellyDoorDegrees(1f)));
        }

        [TestCase("Gear nose", true)]
        [TestCase("Gear L", true)]
        [TestCase("Gear R", true)]
        [TestCase("Gear door L", false)]
        [TestCase("Gear oleo L", false)]
        [TestCase("Gear fairing L", false)]
        [TestCase("Tire L", false)]
        [TestCase("", false)]
        [TestCase(null, false)]
        public void IsGearStrut_OnlyTheThreeLegs(string name, bool expected)
        {
            Assert.That(AirsideAircraftParts.IsGearStrut(name), Is.EqualTo(expected));
        }

        [TestCase("Gear door L", true)]
        [TestCase("Gear door nose", true)]
        [TestCase("gear_door_inner_l", true)]
        [TestCase("gear_door_nose_r", true)]
        [TestCase("Gear L", false)]
        [TestCase("Gear fairing L", false)]
        [TestCase("Truck L", false)]
        [TestCase("", false)]
        [TestCase(null, false)]
        public void IsGearDoor_BothPresentationAndKitNames(string name, bool expected)
        {
            Assert.That(AirsideAircraftParts.IsGearDoor(name), Is.EqualTo(expected));
        }

        [Test]
        public void GearDoors_ClearBeforeAnyLegMovement_InBothDirections()
        {
            for (var i = 1; i < 1000; i++)
            {
                var t = i / 1000f;
                var swing = AircraftArticulation.GearLegSwing01(t);
                if (swing > 0f && swing < 1f)
                    Assert.That(AircraftArticulation.GearDoorOpen01(t), Is.EqualTo(1f).Within(0.0001f));
            }
        }

        [Test]
        public void GearCycle_ReversalAndPauseRetainTheSameContinuousStroke()
        {
            var cycle = new AircraftArticulation.GearCycle();
            cycle.Step(0f, 0f, 22f);
            cycle.Step(1f, 3.5f, 22f);
            Assert.That(cycle.Retract, Is.EqualTo(0.5f).Within(0.0001f));
            cycle.Step(0f, 0f, 22f);
            Assert.That(cycle.Retract, Is.EqualTo(0.5f).Within(0.0001f), "pause holds a reversing cycle");
            cycle.Step(0f, 2.2f, 22f);
            Assert.That(cycle.Retract, Is.EqualTo(0.4f).Within(0.0001f), "extension continues from the current lock travel");
        }

        [Test]
        public void GearCycle_FramePartitionDoesNotChangeThePose()
        {
            var fine = new AircraftArticulation.GearCycle();
            var coarse = new AircraftArticulation.GearCycle();
            fine.Step(1f, 0f, 22f);
            coarse.Step(1f, 0f, 22f);
            for (var i = 0; i < 220; i++) fine.Step(0f, 0.1f, 22f);
            for (var i = 0; i < 22; i++) coarse.Step(0f, 1f, 22f);
            Assert.That(fine.Retract, Is.EqualTo(coarse.Retract).Within(0.00001f));
            Assert.That(fine.Retract, Is.EqualTo(0f).Within(0.00001f));
        }

        // ---- Wheels -------------------------------------------------------------------------

        [Test]
        public void Wheels_SpinUpHardAtTouchdown_ButSpinDownGently()
        {
            var spunUp = AircraftArticulation.WheelSpinStep(0f, 70f, 0.1f);
            Assert.That(spunUp, Is.GreaterThan(10f), "tyres spin up fast on touchdown");
            Assert.That(AircraftArticulation.WheelSpinStep(0f, 70f, 1f), Is.EqualTo(70f), "and are up to speed in under a second");

            var spunDown = AircraftArticulation.WheelSpinStep(70f, 0f, 0.1f);
            Assert.That(spunDown, Is.GreaterThan(60f), "airborne, they coast down rather than stopping dead");
            Assert.That(AircraftArticulation.WheelSpinStep(70f, 0f, 10f), Is.EqualTo(0f));
        }

        [Test]
        public void Wheels_FollowGroundSpeedExactly_OnceAtSpeed_AndHandleReversal()
        {
            Assert.That(AircraftArticulation.WheelSpinStep(8f, 8f, 0.016f), Is.EqualTo(8f));
            Assert.That(AircraftArticulation.WheelSpinStep(5f, 5f, 0f), Is.EqualTo(5f), "paused");
            var pushback = AircraftArticulation.WheelSpinStep(0f, -1.5f, 0.1f);
            Assert.That(pushback, Is.EqualTo(-1.5f), "a pushback reverses straight away");
        }
    }
}

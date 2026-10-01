using Airside.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Airside.Tests
{
    /// <summary>
    /// Locks the pure articulation rules to real Unity rotation maths. The pure tests state signs in aircraft
    /// terms; these confirm that the hinge angle and axis the rig applies really move the metal that way:
    /// +Z forward, +X right, +Y up, nose-up a negative Euler X, a left bank a positive Euler Z.
    /// </summary>
    public sealed class AircraftArticulationRigTests
    {
        private static readonly Vector3 TrailingEdge = Vector3.back;

        [Test]
        public void ProjectConvention_NoseUpIsNegativePitch_LeftBankIsPositiveRoll()
        {
            Assert.That((Quaternion.Euler(AirsideFlightPath.RotatePitchDegrees, 0f, 0f) * Vector3.forward).y,
                Is.GreaterThan(0f), "a rotating aircraft's nose rises");
            Assert.That((Quaternion.Euler(0f, 0f, 10f) * Vector3.right).y, Is.GreaterThan(0f),
                "positive roll lifts the right wing: a left bank");
        }

        [Test]
        public void TrailingEdgeDown_LowersTheTrailingEdge()
        {
            var hinge = AircraftArticulation.HingeAngleForTrailingEdgeDown(20f);
            Assert.That((Quaternion.AngleAxis(hinge, Vector3.right) * TrailingEdge).y, Is.LessThan(-0.2f));
            // On a swept hinge too: the same sense about a slanted axis.
            var swept = new Vector3(1f, 0f, 0.45f).normalized;
            Assert.That((Quaternion.AngleAxis(hinge, swept) * TrailingEdge).y, Is.LessThan(-0.2f));
            var sweptBack = new Vector3(1f, 0f, -0.45f).normalized;
            Assert.That((Quaternion.AngleAxis(hinge, sweptBack) * TrailingEdge).y, Is.LessThan(-0.2f));
        }

        [Test]
        public void ElevatorUp_RaisesTheTrailingEdge()
        {
            var up = AircraftArticulation.ElevatorTrailingEdgeUpDegrees(8f, 5f);
            var hinge = AircraftArticulation.HingeAngleForTrailingEdgeDown(-up);
            Assert.That((Quaternion.AngleAxis(hinge, Vector3.right) * TrailingEdge).y, Is.GreaterThan(0.2f));
        }

        [Test]
        public void RightRudder_SwingsTheTrailingEdgeToTheRight()
        {
            var hinge = AircraftArticulation.HingeAngleForRudderRight(15f);
            Assert.That((Quaternion.AngleAxis(hinge, Vector3.up) * TrailingEdge).x, Is.GreaterThan(0.2f));
            // A swept fin hinge slants up and aft.
            var swept = new Vector3(0f, 1f, -0.5f).normalized;
            Assert.That((Quaternion.AngleAxis(hinge, swept) * TrailingEdge).x, Is.GreaterThan(0.2f));
        }

        [Test]
        public void RightRoll_RightAileronUp_LeftAileronDown_RightRudder()
        {
            // Rolling right is a negative left-roll rate.
            var rightAileron = AircraftArticulation.AileronTrailingEdgeDownDegrees(-8f, rightWing: true);
            var leftAileron = AircraftArticulation.AileronTrailingEdgeDownDegrees(-8f, rightWing: false);
            Assert.That((Quaternion.AngleAxis(
                AircraftArticulation.HingeAngleForTrailingEdgeDown(rightAileron), Vector3.right) * TrailingEdge).y,
                Is.GreaterThan(0f), "right aileron trailing edge rises");
            Assert.That((Quaternion.AngleAxis(
                AircraftArticulation.HingeAngleForTrailingEdgeDown(leftAileron), Vector3.right) * TrailingEdge).y,
                Is.LessThan(0f), "left aileron trailing edge drops");
            var rudder = AircraftArticulation.RudderTrailingEdgeRightDegrees(-8f, 0f);
            Assert.That((Quaternion.AngleAxis(
                AircraftArticulation.HingeAngleForRudderRight(rudder), Vector3.up) * TrailingEdge).x, Is.GreaterThan(0f),
                "right roll, right rudder");
        }

        [Test]
        public void RaisedSpoiler_LiftsItsTrailingEdge()
        {
            var raise = 35f;
            var hinge = AircraftArticulation.HingeAngleForTrailingEdgeDown(-raise);
            Assert.That((Quaternion.AngleAxis(hinge, Vector3.right) * TrailingEdge).y, Is.GreaterThan(0.4f));
        }

        [Test]
        public void GearLegs_FoldTheirTipTowardTheirStowage()
        {
            var tip = Vector3.down;

            var nose = AircraftArticulation.GearRetractRotation(GearRetractStyle.Forward, false, 1f);
            Assert.That((Quaternion.AngleAxis(nose.Degrees, new Vector3(nose.AxisX, 0f, nose.AxisZ)) * tip).z,
                Is.GreaterThan(0.9f), "the nose leg folds forward");

            var aft = AircraftArticulation.GearRetractRotation(GearRetractStyle.Aft, true, 1f);
            Assert.That((Quaternion.AngleAxis(aft.Degrees, new Vector3(aft.AxisX, 0f, aft.AxisZ)) * tip).z,
                Is.LessThan(-0.9f), "a Dash 8 main folds aft");

            var left = AircraftArticulation.GearRetractRotation(GearRetractStyle.Inboard, true, 1f);
            Assert.That((Quaternion.AngleAxis(left.Degrees, new Vector3(left.AxisX, 0f, left.AxisZ)) * tip).x,
                Is.GreaterThan(0.9f), "the left main (at -X) swings toward the centreline");

            var right = AircraftArticulation.GearRetractRotation(GearRetractStyle.Inboard, false, 1f);
            Assert.That((Quaternion.AngleAxis(right.Degrees, new Vector3(right.AxisX, 0f, right.AxisZ)) * tip).x,
                Is.LessThan(-0.9f), "the right main (at +X) swings toward the centreline");
        }

        [Test]
        public void TruckTilt_RaisesTheForwardAxle()
        {
            var tilt = AircraftArticulation.TruckTiltDegrees(1f);
            Assert.That((Quaternion.AngleAxis(tilt, Vector3.right) * Vector3.forward).y, Is.GreaterThan(0.3f));
        }

        [Test]
        public void BellyDoor_FreeEdgeSwingsDown_OnEitherHinge()
        {
            var open = AircraftArticulation.BellyDoorDegrees(1f);
            // Hinged on the left edge: the free edge is to the right (+X) and the sign is -1.
            Assert.That((Quaternion.AngleAxis(-open, Vector3.forward) * Vector3.right).y, Is.LessThan(-0.5f));
            // Hinged on the right edge: the free edge is to the left (-X) and the sign is +1.
            Assert.That((Quaternion.AngleAxis(open, Vector3.forward) * Vector3.left).y, Is.LessThan(-0.5f));
        }
    }
}

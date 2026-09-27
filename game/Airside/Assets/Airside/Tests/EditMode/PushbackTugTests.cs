using System;
using System.Linq;
using Airside.Domain;
using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>ADR 0126 — pushback tugs and AI turnaround vehicles.</summary>
    public sealed class PushbackTugTests
    {
        private static float Distance(TugPose a, float x, float z) =>
            (float)Math.Sqrt((a.X - x) * (a.X - x) + (a.Z - z) * (a.Z - z));

        [Test]
        public void Timeline_RunsApproachCoupledDisconnectReturnThenLeaves()
        {
            Assert.That(PushbackTugTimeline.AtStand(600).Phase, Is.EqualTo(TugPhase.None));
            Assert.That(PushbackTugTimeline.AtStand(120).Phase, Is.EqualTo(TugPhase.Approach));
            Assert.That(PushbackTugTimeline.AtStand(30).Phase, Is.EqualTo(TugPhase.Coupled));
            Assert.That(PushbackTugTimeline.AtStand(-300).Phase, Is.EqualTo(TugPhase.Coupled), "waits coupled through a hold");
            Assert.That(PushbackTugTimeline.OnTaxiOut(10, 60, 25).Phase, Is.EqualTo(TugPhase.Coupled));
            Assert.That(PushbackTugTimeline.OnTaxiOut(70, 60, 25).Phase, Is.EqualTo(TugPhase.Disconnect));
            Assert.That(PushbackTugTimeline.OnTaxiOut(62, 60, 25).Progress, Is.EqualTo(0f), "still unhooking");
            Assert.That(PushbackTugTimeline.OnTaxiOut(90, 60, 25).Phase, Is.EqualTo(TugPhase.Return));
            Assert.That(PushbackTugTimeline.OnTaxiOut(200, 60, 25).Phase, Is.EqualTo(TugPhase.None));
        }

        [Test]
        public void Coupled_TheTowbarEyeIsOnTheNoseGear()
        {
            var pose = PushbackTugTimeline.Pose(new TugState(TugPhase.Coupled, 0f), 100f, 50f, 0f, 1f, 3.2f, 1f, 20f);
            Assert.That(pose.X, Is.EqualTo(100f).Within(1e-4f));
            Assert.That(pose.Z, Is.EqualTo(53.2f).Within(1e-4f), "tug sits its reach ahead of the gear");
            Assert.That(pose.FacingZ, Is.EqualTo(1f).Within(1e-4f), "facing along the nose, towbar trailing back");
        }

        [Test]
        public void Disconnect_ApproachAndDisconnectAreContinuousAndEndClearOfTheWing()
        {
            const float gearX = 0f, gearZ = 0f, reach = 3f, lateral = 26f;
            TugPose? previous = null;
            for (var i = 0; i <= 200; i++)
            {
                var pose = PushbackTugTimeline.Pose(new TugState(TugPhase.Disconnect, i / 200f), gearX, gearZ, 0f, 1f, reach, 1f, lateral);
                if (previous.HasValue)
                    Assert.That(Distance(pose, previous.Value.X, previous.Value.Z), Is.LessThan(0.5f), $"jump at {i}");
                previous = pose;
            }

            // The end of the disconnect is the start of the return, and it is clear of the wing.
            var end = PushbackTugTimeline.Pose(new TugState(TugPhase.Disconnect, 1f), gearX, gearZ, 0f, 1f, reach, 1f, lateral);
            var start = PushbackTugTimeline.Pose(new TugState(TugPhase.Return, 0f), gearX, gearZ, 0f, 1f, reach, 1f, lateral);
            Assert.That(Distance(end, start.X, start.Z), Is.LessThan(0.01f));
            Assert.That(Math.Abs(end.X), Is.GreaterThanOrEqualTo(lateral - 0.01f), "parked a half-span plus clearance to the side");
            // The approach runs the same curve backwards and finishes on the towbar.
            var coupled = PushbackTugTimeline.Pose(new TugState(TugPhase.Coupled, 0f), gearX, gearZ, 0f, 1f, reach, 1f, lateral);
            var arrived = PushbackTugTimeline.Pose(new TugState(TugPhase.Approach, 1f), gearX, gearZ, 0f, 1f, reach, 1f, lateral);
            Assert.That(Distance(arrived, coupled.X, coupled.Z), Is.LessThan(0.01f));
        }

        [Test]
        public void Tug_ClearsToTheSideTheAircraftDoesNotTurn()
        {
            // Nose north (+z); the taxi heads off to the east (+x), i.e. to the nose's right.
            var side = PushbackTugTimeline.ClearingSide(0f, 0f, 0f, 1f, 40f, 30f);
            var parked = PushbackTugTimeline.Pose(new TugState(TugPhase.Disconnect, 1f), 0f, 0f, 0f, 1f, 3f, side, 20f);
            Assert.That(parked.X, Is.LessThan(0f), "west, away from the eastbound taxi");
            Assert.That(PushbackTugTimeline.LateralFor(GroundTraffic.HalfSpan(AircraftType.AirbusA350900)),
                Is.GreaterThan(GroundTraffic.HalfSpan(AircraftType.AirbusA350900)));
        }

        [Test]
        public void EveryAdelaidePushback_HasATowbarPartAndADisconnectPause()
        {
            var stand = AirlineOperations.AdelaideStands.First(s => AdelaideGround.IsTerminalGate(s));
            var leg = AdelaideGround.TaxiOut(stand, AircraftType.Boeing7378, RunwayDirection.Runway23);
            Assert.That(leg.Parts[0].TailFirst, Is.True);
            Assert.That(leg.Parts[1].PauseBeforeSeconds, Is.EqualTo(AdelaideGround.TugDisconnectSeconds));
            var state = PushbackTugTimeline.OnTaxiOut(leg.Parts[0].Seconds + 1, leg.Parts[0].Seconds, leg.Parts[1].PauseBeforeSeconds);
            Assert.That(state.Phase, Is.EqualTo(TugPhase.Disconnect));
        }

        [Test]
        public void AiTurnaround_VehiclesArriveAfterParkingAndLeaveBeforePushback()
        {
            Assert.That(ApronServiceSchedule.FuelAlongside(60, 3600), Is.False, "not the moment it parks");
            Assert.That(ApronServiceSchedule.FuelAlongside(600, 3600), Is.True);
            Assert.That(ApronServiceSchedule.FuelAlongside(600, 5 * 60), Is.False, "gone before the tug comes");
            Assert.That(ApronServiceSchedule.BaggageAlongside(300, 20 * 60), Is.True);
            Assert.That(ApronServiceSchedule.BaggageAlongside(300, 5 * 60), Is.False);
            Assert.That(ApronServiceSchedule.BaggageAlongside(3 * 3600, null), Is.False, "an idle aircraft is not serviced forever");
            // The last service vehicle leaves before the tug starts its approach.
            Assert.That(ApronServiceSchedule.BaggageLeavesBeforeSeconds, Is.GreaterThan(PushbackTugTimeline.ApproachLeadSeconds));
        }

        [Test]
        public void TurnaroundVehicles_ShowOnTheDefaultField()
        {
            Assert.That(AirsideFocusMode.ShowTurnaroundVehicles, Is.True);
        }
    }
}

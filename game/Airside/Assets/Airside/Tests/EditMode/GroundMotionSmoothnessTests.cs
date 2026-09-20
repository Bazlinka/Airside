using System;
using System.Collections.Generic;
using Airside.Domain;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>
    /// Drives every taxi-in, pushback + taxi-out and vacate leg at 0.25 s and checks it looks
    /// like an aircraft: no crawl spots mid-route, no snapping heading, no spinning nose. These
    /// caught a 12/30 hairpin that dropped an aircraft to 0.4 m/s and swung it 150° in a second,
    /// a pushback that snapped its nose back by ~95° when the tug turn began, and a gate
    /// taxi-out that drove 28 m the wrong way and reversed.
    /// </summary>
    public sealed class GroundMotionSmoothnessTests
    {
        private const double Dt = 0.25;

        // Measured worst case after the fixes: 37 deg/s and 43 deg/s^2. Before: 396 and 2880.
        private const double MaxYawDegreesPerSecond = 60.0;
        private const double MaxYawAccelerationDegreesPerSecondSquared = 120.0;

        private static readonly AircraftType[] Regionals =
            { AircraftType.Atr42, AircraftType.Saab340, AircraftType.Dash8Q400 };

        private static readonly AircraftType[] Jets =
            { AircraftType.Boeing7378, AircraftType.AirbusA321Neo, AircraftType.AirbusA350900, AircraftType.Boeing78710 };

        private static double Heading(GroundPose p) => Math.Atan2(p.NoseX, p.NoseZ) * 180.0 / Math.PI;

        private static double Delta(double a, double b)
        {
            var d = b - a;
            while (d > 180) d -= 360;
            while (d < -180) d += 360;
            return d;
        }

        private static void Check(string label, GroundLeg leg, List<string> failures)
        {
            // A taxi part must not stop or crawl in its interior (the ends legitimately do).
            var offset = 0.0;
            foreach (var part in leg.Parts)
            {
                offset += part.PauseBeforeSeconds;
                for (var t = 8.0; t < part.Path.Seconds - 8.0; t += Dt)
                {
                    var v = part.Path.SampleAt(t).Speed;
                    if (v < 0.6f)
                    {
                        failures.Add($"{label}: crawls to {v:0.0} m/s at {offset + t:0}s");
                        break;
                    }
                }

                offset += part.Path.Seconds;
            }

            var before = leg.PoseAt(0);
            var prev = leg.PoseAt(Dt);
            var prevRate = Delta(Heading(before), Heading(prev)) / Dt;
            double worstRate = 0, worstRateAt = 0, worstAccel = 0, worstAccelAt = 0, worstJump = 0, worstJumpAt = 0;
            for (var t = 2 * Dt; t <= leg.Seconds; t += Dt)
            {
                var pose = leg.PoseAt(t);
                var rate = Delta(Heading(prev), Heading(pose)) / Dt;
                if (Math.Abs(rate) > worstRate) { worstRate = Math.Abs(rate); worstRateAt = t; }
                var accel = Math.Abs(rate - prevRate) / Dt;
                if (accel > worstAccel) { worstAccel = accel; worstAccelAt = t; }
                prevRate = rate;

                var dx = pose.X - prev.X;
                var dz = pose.Z - prev.Z;
                var over = Math.Sqrt(dx * dx + dz * dz) - (Math.Max(pose.Speed, prev.Speed) * Dt + 0.2);
                if (over > worstJump) { worstJump = over; worstJumpAt = t; }
                prev = pose;
            }

            if (worstRate > MaxYawDegreesPerSecond)
                failures.Add($"{label}: nose swings {worstRate:0} deg/s at {worstRateAt:0}s");
            if (worstAccel > MaxYawAccelerationDegreesPerSecondSquared)
                failures.Add($"{label}: yaw accelerates {worstAccel:0} deg/s^2 at {worstAccelAt:0}s");
            if (worstJump > 0.25)
                failures.Add($"{label}: jumps {worstJump:0.0} m at {worstJumpAt:0}s");
        }

        [Test]
        public void EveryGroundLeg_IsSmooth()
        {
            var failures = new List<string>();
            var allRunways = new[]
            {
                RunwayDirection.Runway05, RunwayDirection.Runway23,
                RunwayDirection.Runway12, RunwayDirection.Runway30
            };
            var mainRunways = new[] { RunwayDirection.Runway05, RunwayDirection.Runway23 };

            foreach (var bay in AdelaideLayout.Bays)
            {
                var stand = new StableId(bay.Id);
                foreach (var type in Regionals)
                {
                    Check($"IN  bay {bay.Reference} {type.Id}", AdelaideGround.TaxiIn(stand, type), failures);
                    foreach (var runway in allRunways)
                        Check($"OUT bay {bay.Reference} {type.Id} {runway}", AdelaideGround.TaxiOut(stand, type, runway), failures);
                }
            }

            // Jets stay on 05/23; the cross strip belongs to the regionals.
            foreach (var gate in AdelaideLayout.TerminalGates)
            {
                var stand = new StableId(gate.Id);
                foreach (var type in Jets)
                {
                    Check($"IN  gate {gate.Reference} {type.Id}", AdelaideGround.TaxiIn(stand, type), failures);
                    foreach (var runway in mainRunways)
                        Check($"OUT gate {gate.Reference} {type.Id} {runway}", AdelaideGround.TaxiOut(stand, type, runway), failures);
                }
            }

            foreach (var type in Regionals)
                foreach (var runway in allRunways)
                    Check($"VACATE {type.Id} {runway}", AdelaideGround.VacateFor(type, runway), failures);
            foreach (var type in Jets)
                foreach (var runway in mainRunways)
                    Check($"VACATE {type.Id} {runway}", AdelaideGround.VacateFor(type, runway), failures);

            Assert.That(failures, Is.Empty, string.Join("\n", failures.GetRange(0, Math.Min(25, failures.Count))));
        }

        [Test]
        public void Pushback_TurnsFromTheHeadingItAlreadyHas()
        {
            // A push that curves before the tug turn starts must not snap the nose back to the
            // stand heading when it does (bay 10D swung ~95° in one sample).
            foreach (var bay in AdelaideLayout.Bays)
            {
                var leg = AdelaideGround.TaxiOut(new StableId(bay.Id), AircraftType.Atr42, RunwayDirection.Runway05);
                var push = leg.Parts[0];
                var previous = Heading(leg.PoseAt(0));
                for (var t = Dt; t <= push.Path.Seconds; t += Dt)
                {
                    var heading = Heading(leg.PoseAt(t));
                    Assert.That(Math.Abs(Delta(previous, heading)), Is.LessThan(MaxYawDegreesPerSecond * Dt),
                        $"bay {bay.Reference}: heading steps at {t:0.0}s of the push");
                    previous = heading;
                }
            }
        }

        [Test]
        public void RelaxTightTurns_CollapsesAnOutAndBackSpur()
        {
            // 12 m out along +x and straight back, then on along +z: the shape a graph router leaves
            // when it snaps a route's start to a node behind it.
            var spur = new float[] { 0, 0, 12, 0, 0.5f, 0.5f, 0.5f, 60 };
            var relaxed = GroundPathSmoothing.RelaxTightTurns(spur, 24f, 4f);

            Assert.That(relaxed[0], Is.EqualTo(0f));
            Assert.That(relaxed[1], Is.EqualTo(0f));
            Assert.That(relaxed[relaxed.Length - 2], Is.EqualTo(0.5f));
            Assert.That(relaxed[relaxed.Length - 1], Is.EqualTo(60f));

            var worstTurn = 0.0;
            for (var i = 4; i + 1 < relaxed.Length; i += 2)
            {
                var a = Math.Atan2(relaxed[i - 2] - relaxed[i - 4], relaxed[i - 1] - relaxed[i - 3]);
                var b = Math.Atan2(relaxed[i] - relaxed[i - 2], relaxed[i + 1] - relaxed[i - 1]);
                var d = Math.Abs(b - a);
                if (d > Math.PI) d = 2 * Math.PI - d;
                worstTurn = Math.Max(worstTurn, d * 180.0 / Math.PI);
            }

            // 4 m spacing at a 24 m minimum radius allows about 9.5 degrees per vertex.
            Assert.That(worstTurn, Is.LessThan(12.0), "every vertex is a gentle bend, no hairpin left");
        }

        [Test]
        public void RelaxTightTurns_LeavesAStraightRouteAlone()
        {
            var straight = new float[] { 0, 0, 50, 0, 100, 0, 200, 0 };
            var relaxed = GroundPathSmoothing.RelaxTightTurns(straight);
            for (var i = 0; i + 1 < relaxed.Length; i += 2)
                Assert.That(relaxed[i + 1], Is.EqualTo(0f).Within(1e-3f), "stays on the line");
            Assert.That(relaxed[relaxed.Length - 2], Is.EqualTo(200f).Within(1e-3f));
        }
    }
}

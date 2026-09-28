using System;
using Airside.Domain;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>
    /// The lineup used to crab: both axles sat on the nose path, so the fuselage was a chord
    /// of the turn and the tail travelled sideways. The rear gear now trails without sideslip,
    /// on one circular arc, and is straight before the roll.
    /// </summary>
    public sealed class LineupSteeringTests
    {
        [Test]
        public void Lineup_RearGearRollsAlongTheFuselage()
        {
            foreach (var runway in new[]
                     {
                         RunwayDirection.Runway05, RunwayDirection.Runway23,
                         RunwayDirection.Runway12, RunwayDirection.Runway30
                     })
            foreach (var spec in AircraftCatalogue.All)
            {
                if (!RunwayWeather.IsMainRunway(runway) && AirlineOperations.NeedsTerminalGate(spec.Type))
                    continue;
                var leg = AdelaideGround.LineupFor(runway, spec.Type);
                Assert.That(leg.Parts[0].SlipFree, Is.True, spec.Id);
                var wheelbase = AircraftPerformance.For(spec.Type).NoseToMainGearMetres;
                var worst = 0.0;
                var prev = leg.PoseAt(0);
                for (var t = 0.25; t <= leg.Seconds; t += 0.25)
                {
                    var pose = leg.PoseAt(t);
                    var mx = pose.X - pose.NoseX * wheelbase;
                    var mz = pose.Z - pose.NoseZ * wheelbase;
                    var vx = mx - (prev.X - prev.NoseX * wheelbase);
                    var vz = mz - (prev.Z - prev.NoseZ * wheelbase);
                    var travel = Math.Sqrt(vx * vx + vz * vz);
                    if (travel > 0.2)
                    {
                        var hx = prev.NoseX + pose.NoseX;
                        var hz = prev.NoseZ + pose.NoseZ;
                        var h = Math.Sqrt(hx * hx + hz * hz);
                        var along = h > 1e-4 ? (vx * hx + vz * hz) / (travel * h) : 1;
                        worst = Math.Max(worst, Math.Acos(Math.Max(-1, Math.Min(1, along))) * 180 / Math.PI);
                    }

                    prev = pose;
                }

                Assert.That(worst, Is.LessThan(6.0),
                    $"{spec.Id} on {runway} rear gear sideslips {worst:0.0}°");
            }
        }

        [Test]
        public void Lineup_FinishesStraightOnTheRunway_WithoutLooping()
        {
            foreach (var runway in new[]
                     {
                         RunwayDirection.Runway05, RunwayDirection.Runway23,
                         RunwayDirection.Runway12, RunwayDirection.Runway30
                     })
            foreach (var spec in AircraftCatalogue.All)
            {
                if (!RunwayWeather.IsMainRunway(runway) && AirlineOperations.NeedsTerminalGate(spec.Type))
                    continue;
                var leg = AdelaideGround.LineupFor(runway, spec.Type);
                var end = leg.PoseAt(leg.Seconds);
                RunwayFrame.Forward(runway, out var fx, out var fz);
                var aligned = end.NoseX * fx + end.NoseZ * fz;
                Assert.That(aligned, Is.GreaterThan(Math.Cos(8 * Math.PI / 180)),
                    $"{spec.Id} on {runway} finishes {Math.Acos(Math.Max(-1, aligned)) * 180 / Math.PI:0}° off the runway");

                var start = leg.PoseAt(0);
                var straight = Math.Sqrt((end.X - start.X) * (end.X - start.X) + (end.Z - start.Z) * (end.Z - start.Z));
                Assert.That(leg.Parts[0].Path.Length, Is.LessThan(straight * 1.8 + 40f),
                    $"{spec.Id} on {runway} lineup loops");
            }
        }

        [Test]
        public void TakeoffRoll_EasesOffTheStopAndDoesNotMoveTheRotatePoint()
        {
            Assert.That(AircraftPerformanceProfile.TakeoffRollSpeed01(0f), Is.EqualTo(0f).Within(1e-4f));
            Assert.That(AircraftPerformanceProfile.TakeoffRollSpeed01(1f), Is.EqualTo(1f).Within(1e-4f));
            Assert.That(AircraftPerformanceProfile.TakeoffRollDistance01(0f), Is.EqualTo(0f).Within(1e-4f));
            Assert.That(AircraftPerformanceProfile.TakeoffRollDistance01(1f), Is.EqualTo(1f).Within(1e-4f));
            var previous = 0f;
            for (var u = 0.05f; u <= 1f; u += 0.05f)
            {
                var speed = AircraftPerformanceProfile.TakeoffRollSpeed01(u);
                Assert.That(speed, Is.GreaterThan(previous));
                previous = speed;
            }

            // Gentler than a straight ramp at the start, same distance by the end.
            Assert.That(AircraftPerformanceProfile.TakeoffRollSpeed01(0.08f), Is.LessThan(0.08f));
            Assert.That(AircraftPerformanceProfile.TakeoffRollDistance01(0.5f), Is.LessThan(0.5f));
        }
    }
}

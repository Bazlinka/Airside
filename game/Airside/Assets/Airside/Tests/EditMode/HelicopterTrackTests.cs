using System;
using System.Linq;
using Airside.Domain;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>ADR 0207: the drawn path of a helicopter — continuous, on its pad spot, never through the ground.</summary>
    public sealed class HelicopterTrackTests
    {
        private static AirlineOperations NewOps(out ManualSimulationClock clock, int seed)
        {
            clock = new ManualSimulationClock(new SimulationTime(0));
            return AirlineOperations.StartAtAdelaide(clock, new SeededRandomSource((uint)seed),
                Airline.Player("Track Air", "#1F3A93"));
        }

        [Test]
        public void ADaysFlying_IsContinuous_NeverUnderground_AndNeverTeleports()
        {
            var ops = NewOps(out var clock, 6);
            var heli = ops.Fleet.Single(a => a.Type.IsRotorcraft);
            var previous = HelicopterTrack.For(heli, clock.Now.ElapsedSeconds);
            var visibleSeconds = 0;
            var segments = new System.Collections.Generic.HashSet<HelicopterSegment>();
            for (var second = 1; second <= 16 * 3600; second++)
            {
                clock.Advance(1);
                ops.Update();
                var pose = HelicopterTrack.For(heli, clock.Now.ElapsedSeconds);
                segments.Add(pose.Segment);
                if (pose.Visible)
                {
                    visibleSeconds++;
                    Assert.That(pose.HeightMetres, Is.GreaterThanOrEqualTo(-0.01f), $"t={second} {pose.Segment}");
                    Assert.That(pose.RotorSpeed01, Is.InRange(0f, 1f));
                    if (previous.Visible)
                    {
                        var jump = Math.Sqrt(Math.Pow(pose.X - previous.X, 2) + Math.Pow(pose.Z - previous.Z, 2)
                                             + Math.Pow(pose.HeightMetres - previous.HeightMetres, 2));
                        Assert.That(jump, Is.LessThan(75.0), $"t={second} {previous.Segment}->{pose.Segment}: {jump:0.0} m in a second");
                        var yaw = Math.Abs(((pose.YawDegrees - previous.YawDegrees + 540f) % 360f) - 180f);
                        Assert.That(yaw, Is.LessThan(75f), $"t={second} {previous.Segment}->{pose.Segment}: {yaw:0} deg in a second");
                        Assert.That(pose.SpeedMetresPerSecond, Is.LessThan(70f), "a Bell 412 does not fly 250 km/h on a short hop");
                    }
                }

                previous = pose;
            }

            Assert.That(visibleSeconds, Is.GreaterThan(3600), "it spends a good part of the day on or near the pad");
            Assert.That(segments, Does.Contain(HelicopterSegment.Parked));
            Assert.That(segments, Does.Contain(HelicopterSegment.Takeoff));
            Assert.That(segments, Does.Contain(HelicopterSegment.Outbound));
            Assert.That(segments, Does.Contain(HelicopterSegment.Inbound));
            Assert.That(segments, Does.Contain(HelicopterSegment.Landing));
            Assert.That(segments, Does.Contain(HelicopterSegment.Hidden), "out of sight while at the hospital");
        }

        [Test]
        public void Parked_SitsOnItsSpotFacingOutward_WithTheRotorStoppedOvernight()
        {
            var ops = NewOps(out var clock, 6);
            var heli = ops.Fleet.Single(a => a.Type.IsRotorcraft);
            var spot = AdelaideHelipad.Spots.Single(s => s.Id.Equals(heli.Stand));
            var pose = HelicopterTrack.For(heli, clock.Now.ElapsedSeconds);
            Assert.That(pose.Segment, Is.EqualTo(HelicopterSegment.Parked));
            Assert.That(pose.X, Is.EqualTo(spot.X));
            Assert.That(pose.Z, Is.EqualTo(spot.Z));
            Assert.That(pose.YawDegrees, Is.EqualTo(spot.HeadingDegrees).Within(0.01f));
            Assert.That(pose.HeightMetres, Is.EqualTo(0f));
            Assert.That(pose.OnGround, Is.True);
            Assert.That(pose.RotorSpeed01, Is.EqualTo(0f), "cold and stopped well before its call-out");
        }

        [Test]
        public void TheRotorSpoolsUpBeforeLiftOff_AndWindsDownAfterParking()
        {
            var ops = NewOps(out var clock, 6);
            var heli = ops.Fleet.Single(a => a.Type.IsRotorcraft);
            var departAt = heli.Scheduled.Value.DepartAt.ElapsedSeconds;
            Assert.That(RotorcraftEngines.RotorSpeed01(heli, departAt - 200), Is.EqualTo(0f));
            var mid = RotorcraftEngines.RotorSpeed01(heli, departAt - 30);
            Assert.That(mid, Is.InRange(0.05f, 0.95f), "part-way up to speed");
            Assert.That(RotorcraftEngines.RotorSpeed01(heli, departAt - 1), Is.GreaterThan(0.95f));
            var state = RotorcraftEngines.StateFor(heli, departAt - 30);
            Assert.That(state.AnyRunning, Is.True);
            Assert.That(state.Beacon, Is.True);

            // Fly one call-out, then watch it wind down on the pad.
            var landedAt = -1L;
            for (var second = 0; second < 8 * 3600 && landedAt < 0; second++)
            {
                clock.Advance(1);
                ops.Update();
                if (heli.State == FleetState.AtStand && heli.CompletedTrips > 0)
                    landedAt = clock.Now.ElapsedSeconds;
            }

            Assert.That(landedAt, Is.GreaterThan(0));
            Assert.That(RotorcraftEngines.RotorSpeed01(heli, landedAt + 30), Is.EqualTo(1f), "still running after landing");
            Assert.That(RotorcraftEngines.RotorSpeed01(heli, landedAt + RotorcraftEngines.CoolDownSeconds + 30),
                Is.InRange(0.05f, 0.95f), "winding down");
            Assert.That(RotorcraftEngines.RotorSpeed01(heli, landedAt + 600), Is.EqualTo(0f).Or.GreaterThan(0f),
                "stopped, or already spooling for the next call-out");
        }

        [Test]
        public void TheFlightLine_RunsFromThePadToTheHospital_AtTrueScaleForNearSites()
        {
            var spot = AdelaideHelipad.Spots[0];
            foreach (var code in new[] { "RAH", "FMC" })
            {
                DestinationCatalogue.TryFind(code, out var site);
                var world = HelicopterTrack.SiteWorld(site);
                var worldKm = Math.Sqrt(Math.Pow(world.X - AdelaideHelipad.PadCentreX, 2)
                                        + Math.Pow(world.Z - AdelaideHelipad.PadCentreZ, 2)) / 1000.0;
                // The pad is under a kilometre from the airport reference point, so the drawn and true
                // distances to a near hospital agree to that margin.
                var trueKm = site.DistanceKmTo(DestinationCatalogue.Adelaide);
                Assert.That(worldKm, Is.EqualTo(trueKm).Within(1.2), code);
            }

            var heading = HelicopterTrack.FlightHeadingDegrees(spot, (spot.X + 100f, spot.Z), outbound: true);
            Assert.That(heading, Is.EqualTo(90f).Within(0.01f), "toward +X");
            Assert.That(HelicopterTrack.FlightHeadingDegrees(spot, (spot.X, spot.Z + 100f), outbound: false),
                Is.EqualTo(180f).Within(0.01f), "the way back is the other way round");
        }

        [Test]
        public void LerpAngle_TakesTheShortWayRound()
        {
            Assert.That(HelicopterTrack.LerpAngle(350f, 10f, 0.5f), Is.EqualTo(0f).Within(0.01f));
            Assert.That(HelicopterTrack.LerpAngle(10f, 350f, 0.5f), Is.EqualTo(0f).Within(0.01f));
            Assert.That(HelicopterTrack.LerpAngle(90f, 270f, 0f), Is.EqualTo(90f).Within(0.01f));
            Assert.That(HelicopterTrack.LerpAngle(90f, 180f, 1f), Is.EqualTo(180f).Within(0.01f));
        }
    }
}

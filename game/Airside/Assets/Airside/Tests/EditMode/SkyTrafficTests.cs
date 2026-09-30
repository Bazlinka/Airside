using System;
using System.Collections.Generic;
using Airside.Domain;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class SkyTrafficTests
    {
        [Test]
        public void Snapshot_IsDeterministicAndNeverUsesAdelaideAsAnEndpoint()
        {
            var a = SkyTraffic.At(new SimulationTime(3 * 3600));
            var b = SkyTraffic.At(new SimulationTime(3 * 3600));
            Assert.That(a.Count, Is.EqualTo(b.Count));
            Assert.That(a.Count, Is.GreaterThan(0), "the sky is not empty at 03:00");
            for (var i = 0; i < a.Count; i++)
            {
                Assert.That(a[i].Callsign, Is.EqualTo(b[i].Callsign));
                Assert.That(a[i].Latitude, Is.EqualTo(b[i].Latitude).Within(1e-9));
                Assert.That(a[i].From.Code, Is.Not.EqualTo("ADL"));
                Assert.That(a[i].To.Code, Is.Not.EqualTo("ADL"));
                Assert.That(a[i].Progress, Is.InRange(0.0, 1.0));
            }
        }

        [TestCase(0.0)]
        [TestCase(10800.25)]
        [TestCase(86400.0)]
        public void ReusableSnapshot_MatchesTheIndependentSnapshot(double seconds)
        {
            var buffer = new List<SkyFlight> { default };
            SkyTraffic.FillAt(seconds, null, buffer);
            var snapshot = SkyTraffic.At(seconds);
            Assert.That(buffer.Count, Is.EqualTo(snapshot.Count));
            for (var i = 0; i < buffer.Count; i++)
            {
                Assert.That(buffer[i].Callsign, Is.EqualTo(snapshot[i].Callsign));
                Assert.That(buffer[i].Latitude, Is.EqualTo(snapshot[i].Latitude).Within(1e-9));
                Assert.That(buffer[i].Longitude, Is.EqualTo(snapshot[i].Longitude).Within(1e-9));
                Assert.That(buffer[i].Progress, Is.EqualTo(snapshot[i].Progress).Within(1e-9));
            }

            SkyTraffic.FillAt(seconds + 60, null, buffer);
            Assert.That(buffer.Count, Is.EqualTo(SkyTraffic.At(seconds + 60).Count),
                "a reused buffer must not retain flights from the previous frame");
        }

        [Test]
        public void RouteCallsign_IsCachedAcrossFrames()
        {
            var route = SkyTraffic.Routes[0];
            var first = route.CallsignForStart(0);
            Assert.That(first, Is.EqualTo(route.Airline + route.FlightNumber));
            Assert.That(ReferenceEquals(first, route.CallsignForStart(0)), Is.True);
            Assert.That(route.CallsignForStart(route.IntervalSeconds),
                Is.EqualTo(route.Airline + (route.FlightNumber + 2)));
            Assert.That(ReferenceEquals(first, route.CallsignForStart(route.IntervalSeconds * 40)), Is.True,
                "the numbered service repeats without allocating another callsign");
        }

        [Test]
        public void CorridorCallsigns_DoNotCollideAcrossOppositeDirections()
        {
            var used = new HashSet<string>(StringComparer.Ordinal);
            foreach (var route in SkyTraffic.Routes)
            {
                for (var slot = 0; slot < 40; slot++)
                {
                    var callsign = route.CallsignForStart(slot * route.IntervalSeconds);
                    Assert.That(used.Add(callsign), Is.True,
                        $"{callsign} must stay unique across corridor pairs");
                }
            }
        }

        [Test]
        public void EveryCorridorCrossesTheRegion()
        {
            foreach (var route in SkyTraffic.Routes)
            {
                Assert.That(DestinationCatalogue.TryFind(route.FromCode, out var from), Is.True);
                Assert.That(DestinationCatalogue.TryFind(route.ToCode, out var to), Is.True);
                var closest = SkyTraffic.ClosestApproachKm(from, to);
                Assert.That(closest, Is.LessThanOrEqualTo(SkyTraffic.VisibleRadiusKm),
                    $"{route.FromCode}–{route.ToCode} must cross the visible Adelaide sky");
            }
        }

        [Test]
        public void PerthSydney_PassesCloseEnoughToDrawOverAdelaide()
        {
            Assert.That(DestinationCatalogue.TryFind("PER", out var perth), Is.True);
            Assert.That(DestinationCatalogue.TryFind("SYD", out var sydney), Is.True);
            var closest = SkyTraffic.ClosestApproachKm(perth, sydney);
            Assert.That(closest, Is.LessThanOrEqualTo(SkyTraffic.VisibleRadiusKm),
                "PER–SYD is a corridor that should overfly the Adelaide region");

            var seen = false;
            for (var t = 0L; t < 12 * 3600 && !seen; t += 30)
            {
                foreach (var flight in SkyTraffic.At(new SimulationTime(t)))
                {
                    if (flight.From.Code != "PER" || flight.To.Code != "SYD")
                        continue;
                    if (SkyTraffic.TryWorldPosition(flight, out var x, out var y, out var z))
                    {
                        seen = true;
                        Assert.That(y, Is.GreaterThan(400));
                        Assert.That(Math.Sqrt(x * x + z * z), Is.LessThanOrEqualTo(SkyTraffic.DrawRadiusMetres + 1.0));
                        break;
                    }
                }
            }

            Assert.That(seen, Is.True, "a PER–SYD flight must enter the 3D draw radius within twelve hours");
        }

        [Test]
        public void Progress_MovesAlongTheCorridor()
        {
            SkyFlight? first = null;
            SkyFlight? later = null;
            const long t0 = 1800;
            foreach (var flight in SkyTraffic.At(new SimulationTime(t0)))
            {
                first = flight;
                break;
            }

            Assert.That(first.HasValue);
            foreach (var flight in SkyTraffic.At(new SimulationTime(t0 + 120)))
            {
                if (flight.Callsign != first.Value.Callsign)
                    continue;
                later = flight;
                break;
            }

            if (later.HasValue)
                Assert.That(later.Value.Progress, Is.GreaterThan(first.Value.Progress));
        }

        [Test]
        public void DisplayUnityYaw_FacesTheCompressedOnScreenPath()
        {
            SkyFlight? found = null;
            for (var t = 0L; t < 6 * 3600 && !found.HasValue; t += 60)
            {
                foreach (var candidate in SkyTraffic.At(new SimulationTime(t)))
                {
                    if (!SkyTraffic.TryWorldPosition(candidate, out _, out _, out _))
                        continue;
                    found = candidate;
                    break;
                }
            }

            Assert.That(found.HasValue, "a corridor flight must be in draw range within six hours");
            var flight = found.Value;
            Assert.That(SkyTraffic.TryWorldPosition(flight, out var x, out _, out var z), Is.True);
            var step = Math.Min(1.0, flight.Progress + 0.003);
            FlightRoute.Point(flight.From.Latitude, flight.From.Longitude, flight.To.Latitude, flight.To.Longitude,
                step, flight.Callsign, out var lat, out var lon);
            var ahead = new SkyFlight(flight.Callsign, flight.Type, flight.From, flight.To, step,
                lat, lon, flight.AltitudeFeet, flight.HeadingDegrees);
            Assert.That(SkyTraffic.TryWorldPosition(ahead, out var ax, out _, out var az), Is.True);
            var expected = Math.Atan2(ax - x, az - z) * 180.0 / Math.PI;
            Assert.That(SkyTraffic.DisplayUnityYaw(flight), Is.EqualTo(expected).Within(0.5));
        }

        [Test]
        public void NearField_IsOneToOneSoArrivalsMoveAtReadableSpeed()
        {
            SkyTraffic.ToLocalMetres(-34.95, 138.53, out var east, out var north);
            var trueRange = Math.Sqrt(east * east + north * north);
            SkyTraffic.ProjectLocal(east, north, out var x, out var z);
            var display = Math.Sqrt(x * x + z * z);
            Assert.That(trueRange / 1000.0, Is.LessThan(SkyTraffic.NearFieldKm));
            Assert.That(display / trueRange, Is.EqualTo(1.0).Within(0.01));
        }

        [Test]
        public void DrawnClosingSpeed_AtCruiseIsNotACrawl()
        {
            // Bailey: overflights moved in tiny steps. A drawn closing speed under ~30 m/s
            // at cruise means the projection is still compressing motion into a crawl.
            const double stepSeconds = 10.0;
            var best = 0.0;
            for (var t = 0.0; t < 12 * 3600; t += 60)
            {
                foreach (var flight in SkyTraffic.At(t))
                {
                    if (!SkyTraffic.TryWorldPosition(flight, out var x0, out var y0, out var z0))
                        continue;
                    SkyFlight? later = null;
                    foreach (var candidate in SkyTraffic.At(t + stepSeconds))
                    {
                        if (candidate.Callsign != flight.Callsign)
                            continue;
                        later = candidate;
                        break;
                    }

                    if (!later.HasValue
                        || !SkyTraffic.TryWorldPosition(later.Value, out var x1, out var y1, out var z1))
                        continue;
                    var speed = Math.Sqrt(
                        (x1 - x0) * (x1 - x0) + (y1 - y0) * (y1 - y0) + (z1 - z0) * (z1 - z0)) / stepSeconds;
                    if (speed > best)
                        best = speed;
                }
            }

            Assert.That(best, Is.GreaterThanOrEqualTo(30.0),
                $"drawn corridor cruise must not crawl; best seen was {best:0.0} m/s");
        }

        [Test]
        public void DisplayAltitude_SeparatesTurbopropsJetsAndWidebodies()
        {
            Assert.That(DestinationCatalogue.TryFind("ADL", out var adl), Is.True);
            Assert.That(DestinationCatalogue.TryFind("MEL", out var mel), Is.True);
            Assert.That(DestinationCatalogue.TryFind("SIN", out var sin), Is.True);
            var now = 1800.0;
            Assert.That(SkyTraffic.TryEnroute("REX1", AircraftType.Saab340, adl, mel, now, 0, out var rex), Is.True);
            Assert.That(SkyTraffic.TryEnroute("VA1", AircraftType.Boeing7378, adl, mel, now, 0, out var jet), Is.True);
            Assert.That(SkyTraffic.TryEnroute("SQ1", AircraftType.Boeing78710, adl, sin, now, 0, out var heavy), Is.True);
            var rexY = SkyTraffic.DisplayAltitudeMetres(rex);
            var jetY = SkyTraffic.DisplayAltitudeMetres(jet);
            var heavyY = SkyTraffic.DisplayAltitudeMetres(heavy);
            Assert.That(jetY, Is.GreaterThan(rexY + 40));
            Assert.That(heavyY, Is.GreaterThan(jetY + 40));
        }

        [Test]
        public void NightSkyReviewWindow_HasDrawableCruiseTrafficEarlyInSoak()
        {
            // Packaged night-sky stills use -airsideReviewTime for lighting only; SkyTraffic
            // follows the soak sim clock from T+0. Capture delay is ~45s live — there must
            // already be drawable cruise traffic, not an empty sky waiting for a bank hour.
            foreach (var t in new[] { 30.0, 120.0, 300.0 })
            {
                var drawn = 0;
                foreach (var flight in SkyTraffic.At(t))
                {
                    if (SkyTraffic.TryWorldPosition(flight, out _, out var y, out _) && y > 80.0)
                        drawn++;
                }

                Assert.That(drawn, Is.GreaterThan(0),
                    $"t={t:0}s should already show drawable cruise overflights for the night-sky still");
            }
        }

        [Test]
        public void NightSkyReviewYaw_FacesADrawableOverflightSector()
        {
            // scripts/review-post-audit-p0*.sh use yaw 270 / pitch 8 / 11 km. Early soak
            // has QF1531 west of the field (~bearing 270 at T+45s).
            const double reviewYaw = 270.0;
            var t = 45.0;
            var bestDelta = 180.0;
            foreach (var flight in SkyTraffic.At(t))
            {
                if (!SkyTraffic.TryWorldPosition(flight, out var x, out var y, out var z) || y <= 80.0)
                    continue;
                var bearing = (Math.Atan2(x, z) * 180.0 / Math.PI + 360.0) % 360.0;
                var delta = Math.Abs(((bearing - reviewYaw + 540.0) % 360.0) - 180.0);
                if (delta < bestDelta)
                    bestDelta = delta;
            }

            Assert.That(bestDelta, Is.LessThanOrEqualTo(20.0),
                "review yaw 270 should face the early-soak drawable overflight");
        }
    }
}

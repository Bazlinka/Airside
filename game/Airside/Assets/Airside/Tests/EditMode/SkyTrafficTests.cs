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
                Is.EqualTo(route.Airline + (route.FlightNumber + 1)));
            Assert.That(ReferenceEquals(first, route.CallsignForStart(route.IntervalSeconds * 40)), Is.True,
                "the numbered service repeats without allocating another callsign");
        }

        [Test]
        public void PerthMelbourne_PassesCloseEnoughToDrawOverAdelaide()
        {
            Assert.That(DestinationCatalogue.TryFind("PER", out var perth), Is.True);
            Assert.That(DestinationCatalogue.TryFind("MEL", out var melbourne), Is.True);
            var closest = SkyTraffic.ClosestApproachKm(perth, melbourne);
            Assert.That(closest, Is.LessThanOrEqualTo(SkyTraffic.VisibleRadiusKm),
                "PER–MEL is the corridor that should actually overfly the field");

            var seen = false;
            for (var t = 0L; t < 6 * 3600 && !seen; t += 30)
            {
                foreach (var flight in SkyTraffic.At(new SimulationTime(t)))
                {
                    if (flight.From.Code != "PER" || flight.To.Code != "MEL")
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

            Assert.That(seen, Is.True, "a PER–MEL flight must enter the 3D draw radius within six hours");
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
        public void NearField_IsAlmostOneToOneSoArrivalsMoveAtReadableSpeed()
        {
            SkyTraffic.ToLocalMetres(-34.95, 138.53, out var east, out var north);
            var trueRange = Math.Sqrt(east * east + north * north);
            SkyTraffic.ProjectLocal(east, north, out var x, out var z);
            var display = Math.Sqrt(x * x + z * z);
            Assert.That(trueRange / 1000.0, Is.LessThan(SkyTraffic.NearFieldKm));
            Assert.That(display / trueRange, Is.EqualTo(SkyTraffic.NearFieldMetres / (SkyTraffic.NearFieldKm * 1000.0))
                .Within(0.01));
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
    }
}

using System;
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

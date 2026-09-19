using Airside.Domain;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class FlightDisruptionTests
    {
        [Test]
        public void For_IsDeterministicAndCanDelayOrCancel()
        {
            var clock = AirlineClock.Default;
            var when = new SimulationTime(8 * 3600);
            var a = FlightDisruption.For("VOZ210", when, clock);
            var b = FlightDisruption.For("VOZ210", when, clock);
            Assert.That(a.Cancelled, Is.EqualTo(b.Cancelled));
            Assert.That(a.DelayMinutes, Is.EqualTo(b.DelayMinutes));
            Assert.That(a.Reason, Is.EqualTo(b.Reason));

            var sawDelay = false;
            var sawCancel = false;
            for (var i = 0; i < 4000 && !(sawDelay && sawCancel); i++)
            {
                var hit = FlightDisruption.For($"TEST{i}", when, clock);
                if (hit.Cancelled)
                    sawCancel = true;
                else if (hit.Delayed)
                    sawDelay = true;
            }

            Assert.That(sawDelay, Is.True, "some published slots delay");
            Assert.That(sawCancel, Is.True, "some published slots cancel");
        }

        [Test]
        public void BoardLabel_NamesTheException()
        {
            Assert.That(new FlightDisruption(true, 0, "weather").BoardLabel, Is.EqualTo("Cancelled"));
            Assert.That(new FlightDisruption(false, 22, "crew").BoardLabel, Is.EqualTo("Delayed +22"));
            Assert.That(FlightDisruption.None.BoardLabel, Is.EqualTo(string.Empty));
        }
    }
}

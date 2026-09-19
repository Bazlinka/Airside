using Airside.Domain;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class DepartureTurnTests
    {
        [Test]
        public void Melbourne_TurnsRightOffRunway05()
        {
            Assert.That(DestinationCatalogue.TryFind("MEL", out var melbourne), Is.True);
            var home = DestinationCatalogue.Adelaide;
            Assert.That(DepartureTurn.Blend(AircraftPhase.Takeoff, 0.2f), Is.EqualTo(0f));
            Assert.That(DepartureTurn.Blend(AircraftPhase.Takeoff, CircuitProfile.RotateProgress),
                Is.EqualTo(0f), "still on the roll at rotate");
            Assert.That(DepartureTurn.LateralMetres(RunwayDirection.Runway05, home, melbourne,
                AircraftPhase.Takeoff, 0.2f), Is.EqualTo(0f));

            var yaw = DepartureTurn.YawDegrees(RunwayDirection.Runway05, home, melbourne,
                AircraftPhase.Departed, 1f);
            var lateral = DepartureTurn.LateralMetres(RunwayDirection.Runway05, home, melbourne,
                AircraftPhase.Departed, 1f);
            Assert.That(yaw, Is.GreaterThan(20f), "Melbourne sits well right of 05");
            Assert.That(lateral, Is.LessThan(-80f), "right of 05 is −Z in the runway frame");
            Assert.That(DepartureTurn.Blend(AircraftPhase.Takeoff,
                (CircuitProfile.RotateProgress + 1f) * 0.5f), Is.GreaterThan(0.1f),
                "the turn starts after rotate, still on the takeoff");
        }

        [Test]
        public void Perth_TurnsLeftOffRunway05()
        {
            Assert.That(DestinationCatalogue.TryFind("PER", out var perth), Is.True);
            var lateral = DepartureTurn.LateralMetres(RunwayDirection.Runway05, DestinationCatalogue.Adelaide,
                perth, AircraftPhase.Departed, 1f);
            Assert.That(lateral, Is.GreaterThan(80f), "Perth sits left of 05");
        }
    }
}

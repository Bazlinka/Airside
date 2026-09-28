using Airside.Domain;
using Airside.Presentation;
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
            Assert.That(DepartureTurn.Blend(AircraftPhase.Takeoff, 0.99f), Is.EqualTo(0f),
                "the initial climb stays on the runway heading");
            Assert.That(DepartureTurn.LateralMetres(RunwayDirection.Runway05, home, melbourne,
                AircraftPhase.Takeoff, 0.2f), Is.EqualTo(0f));
            Assert.That(DepartureTurn.Blend(AircraftPhase.Departed, DepartureTurn.TurnStartProgress),
                Is.EqualTo(0f), "still flying the remaining strip");

            var yaw = DepartureTurn.YawDegrees(RunwayDirection.Runway05, home, melbourne,
                AircraftPhase.Departed, 1f);
            var lateral = DepartureTurn.LateralMetres(RunwayDirection.Runway05, home, melbourne,
                AircraftPhase.Departed, 1f);
            Assert.That(yaw, Is.GreaterThan(20f), "Melbourne sits well right of 05");
            Assert.That(lateral, Is.LessThan(-80f), "right of 05 is −Z in the runway frame");
            Assert.That(DepartureTurn.Blend(AircraftPhase.Departed,
                (DepartureTurn.TurnStartProgress + DepartureTurn.TurnEstablishedProgress) * 0.5f),
                Is.GreaterThan(0.1f), "the turn starts after the far threshold");

            var mid = (DepartureTurn.TurnStartProgress + DepartureTurn.TurnEstablishedProgress) * 0.5f;
            var now = DepartureTurn.YawDegrees(RunwayDirection.Runway05, home, melbourne,
                AircraftPhase.Departed, mid);
            var ahead = DepartureTurn.YawDegrees(RunwayDirection.Runway05, home, melbourne,
                AircraftPhase.Departed, mid + 0.1f);
            Assert.That(ahead - now, Is.GreaterThan(4f),
                "yaw keeps changing through the SID so the wings can bank with it");
        }

        [Test]
        public void TurnStartsAfterTheFarThreshold()
        {
            var x = CircuitProfile.TakeoffEndX
                + (CircuitProfile.DepartedEndX - CircuitProfile.TakeoffEndX)
                * DepartureTurn.TurnStartProgress;
            Assert.That(x, Is.GreaterThan(AirsideAdelaidePavement.MainLengthMetres * 0.5f - 50f),
                "the SID must not start while the aircraft is still over 05/23");
        }

        [Test]
        public void Perth_TurnsLeftOffRunway05()
        {
            Assert.That(DestinationCatalogue.TryFind("PER", out var perth), Is.True);
            var lateral = DepartureTurn.LateralMetres(RunwayDirection.Runway05, DestinationCatalogue.Adelaide,
                perth, AircraftPhase.Departed, 1f);
            Assert.That(lateral, Is.GreaterThan(80f), "Perth sits left of 05");
        }

        // ADR ??? / crab-fix: once the SID turn locks in, DepartureTurn.Blend (and so
        // LateralMetres/YawDegrees) stops changing — Presentation used to then keep
        // translating the aircraft along the ORIGINAL runway heading forever, frozen sideways
        // offset and all, while the nose stayed turned: the fuselage pointed at the
        // destination but the ground track kept going straight, the classic crabbing/drifting
        // look. EstablishedTrackMetres is what continues the ground track onto the
        // established heading for whatever along-track distance is covered after that point.

        [Test]
        public void EstablishedTrackMetres_IsZeroWithNoFurtherDistance()
        {
            Assert.That(DestinationCatalogue.TryFind("MEL", out var melbourne), Is.True);
            var (forward, sideways) = DepartureTurn.EstablishedTrackMetres(
                RunwayDirection.Runway05, DestinationCatalogue.Adelaide, melbourne, 0f);
            Assert.That(forward, Is.EqualTo(0f));
            Assert.That(sideways, Is.EqualTo(0f));
        }

        [Test]
        public void EstablishedTrackMetres_ContinuesTurningTheSameWayLateralMetresAlreadyDid()
        {
            Assert.That(DestinationCatalogue.TryFind("MEL", out var melbourne), Is.True);
            Assert.That(DestinationCatalogue.TryFind("PER", out var perth), Is.True);
            var home = DestinationCatalogue.Adelaide;

            // Melbourne's established lateral is negative (right of 05, -Z) - see
            // Melbourne_TurnsRightOffRunway05. Further travel on that same heading must keep
            // moving the same way, not double back or sit still.
            var (_, melSideways) = DepartureTurn.EstablishedTrackMetres(
                RunwayDirection.Runway05, home, melbourne, 1000f);
            Assert.That(melSideways, Is.LessThan(0f),
                "further travel toward Melbourne should keep moving -Z, matching LateralMetres");

            // Perth's established lateral is positive (left of 05, +Z) - see
            // Perth_TurnsLeftOffRunway05.
            var (_, perSideways) = DepartureTurn.EstablishedTrackMetres(
                RunwayDirection.Runway05, home, perth, 1000f);
            Assert.That(perSideways, Is.GreaterThan(0f),
                "further travel toward Perth should keep moving +Z, matching LateralMetres");
        }

        [Test]
        public void EstablishedTrackMetres_StaysBoundedForANearReversalTurn()
        {
            // A regional taking whichever of 12/30 the wind favours, not the end that favours
            // its destination, can end up with a destination almost behind it - a much larger
            // turn than any jet on 05/23 ever takes. Sydney sits close to due east of
            // Adelaide; departing 30 (heading 303 true) toward it is close to a 120-140
            // degree turn, well past the 90 degrees where a tan-based shortcut would blow up.
            Assert.That(DestinationCatalogue.TryFind("SYD", out var sydney), Is.True);
            var (forward, sideways) = DepartureTurn.EstablishedTrackMetres(
                RunwayDirection.Runway30, DestinationCatalogue.Adelaide, sydney, 5000f);
            Assert.That(float.IsFinite(forward), Is.True);
            Assert.That(float.IsFinite(sideways), Is.True);
            // A pure rotation of a 5000 m step never travels more than 5000 m in total,
            // whichever way it points.
            var magnitude = System.Math.Sqrt(forward * forward + sideways * sideways);
            Assert.That(magnitude, Is.LessThanOrEqualTo(5000.001));
        }

        [Test]
        public void EstablishedTrackMetres_PreservesTheStepDistance()
        {
            // cos^2 + sin^2 = 1 always: a decomposition of a straight-line step onto forward
            // and sideways components never changes how far that step actually was.
            Assert.That(DestinationCatalogue.TryFind("MEL", out var melbourne), Is.True);
            var (forward, sideways) = DepartureTurn.EstablishedTrackMetres(
                RunwayDirection.Runway05, DestinationCatalogue.Adelaide, melbourne, 2500f);
            var magnitude = System.Math.Sqrt(forward * forward + sideways * sideways);
            Assert.That(magnitude, Is.EqualTo(2500).Within(0.01));
        }

        [Test]
        public void Forward_MatchesLateralMetresSignNotANaiveSin()
        {
            Assert.That(DestinationCatalogue.TryFind("MEL", out var melbourne), Is.True);
            var home = DestinationCatalogue.Adelaide;
            var (_, across) = DepartureTurn.Forward(RunwayDirection.Runway05, home, melbourne,
                AircraftPhase.Departed, 1f);
            var lateral = DepartureTurn.LateralMetres(RunwayDirection.Runway05, home, melbourne,
                AircraftPhase.Departed, 1f);
            Assert.That(across, Is.LessThan(0f));
            Assert.That(System.Math.Sign(across), Is.EqualTo(System.Math.Sign(lateral)),
                "Forward's sideways component must turn the same way LateralMetres does");
        }

        // ADR 0161: the departure flies a real turn.

        [Test]
        public void Arc_NoseAlwaysPointsAlongThePath()
        {
            var relative = 70.0 * System.Math.PI / 180.0;
            var radius = DepartureTurn.TurnRadiusMetres(190f);
            for (var along = 10f; along < 4000f; along += 97f)
            {
                var a = DepartureTurn.Arc(relative, radius, along);
                var b = DepartureTurn.Arc(relative, radius, along + 1f);
                var trackYaw = System.Math.Atan2(-(b.sideways - a.sideways), b.forward - a.forward) * 180.0 / System.Math.PI;
                Assert.That(trackYaw, Is.EqualTo(a.yawDegrees).Within(0.6), $"at {along:0} m the nose and the track agree");
            }
        }

        [Test]
        public void Arc_TurnsTheWholeWayOntoTheDestinationBearing()
        {
            Assert.That(DestinationCatalogue.TryFind("MEL", out var melbourne), Is.True);
            var home = DestinationCatalogue.Adelaide;
            var relative = DepartureTurn.RelativeRadiansFor(RunwayDirection.Runway05, home, melbourne);
            var radius = DepartureTurn.TurnRadiusMetres(185f);
            var far = DepartureTurn.Arc(relative, radius, 20_000f);
            Assert.That(far.yawDegrees, Is.EqualTo(relative * 180.0 / System.Math.PI).Within(0.01),
                "the full bearing, not 72 % of it");
            Assert.That(far.sideways, Is.LessThan(-1000f), "Melbourne is right of 05, −Z");
            Assert.That(DepartureTurn.Arc(relative, radius, 0f).yawDegrees, Is.EqualTo(0f), "straight until the turn starts");
            Assert.That(DepartureTurn.Arc(relative, radius, -300f).sideways, Is.EqualTo(0f));
        }

        [Test]
        public void TurnRadius_FollowsSpeedLikeARealTurn()
        {
            Assert.That(DepartureTurn.TurnRadiusMetres(140f), Is.InRange(900f, 1300f), "a turboprop at 140 kt");
            Assert.That(DepartureTurn.TurnRadiusMetres(190f), Is.InRange(1800f, 2400f), "a jet at 190 kt");
        }

        [Test]
        public void ArcBank_RollsInHoldsAndRollsOut()
        {
            var relative = 60.0 * System.Math.PI / 180.0;
            var radius = 1000f;
            var arc = (float)(relative * radius);
            Assert.That(DepartureTurn.ArcBank(relative, radius, 0f), Is.EqualTo(0f));
            Assert.That(DepartureTurn.ArcBank(relative, radius, 20f), Is.GreaterThan(-DepartureTurn.ArcBankDegrees * 0.5f)
                .And.LessThan(0f), "rolling in");
            Assert.That(DepartureTurn.ArcBank(relative, radius, arc * 0.5f), Is.EqualTo(-DepartureTurn.ArcBankDegrees).Within(0.01f),
                "a right turn banks right at 25°");
            Assert.That(DepartureTurn.ArcBank(relative, radius, arc + 50f), Is.EqualTo(0f), "wings level on the new heading");
            Assert.That(DepartureTurn.ArcBank(-relative, radius, arc * 0.5f), Is.EqualTo(DepartureTurn.ArcBankDegrees).Within(0.01f));
        }
    }
}

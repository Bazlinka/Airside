using System;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>
    /// Ground vehicles used to appear beside the aircraft when their stage began and vanish
    /// when it ended. They now drive the real Adelaide airside frontage road, so what matters
    /// is that the road is sane, that a trip actually follows it, and that a vehicle whose
    /// depot is on the far side of Terminal 1 passes underneath it on the way.
    /// </summary>
    public sealed class GroundServiceRunTests
    {
        private static DeparturePrepStatus Prep(DeparturePrepStage stage,
            double fuel = 0, double catering = 0, double baggage = 0) =>
            new(stage, 0.5, false, stage.ToString(), fuel, catering, baggage, 0, 600);

        // ---- the baked road ------------------------------------------------------------

        [Test]
        public void TheFrontageRoadIsLoadedAndRunsTheLengthOfTheTerminal()
        {
            Assert.That(AdelaideServiceRoadPath.PointCount, Is.GreaterThan(50),
                "the frontage should be resampled finely enough to drive");
            Assert.That(AdelaideServiceRoads.ApronFrontageMetres, Is.GreaterThan(800f)
                .And.LessThan(1600f), "Terminal 1's frontage is about 1.1 km");
        }

        [Test]
        public void TheRoadIsOrderedAndHasNoTeleports()
        {
            for (var i = 0; i < AdelaideServiceRoadPath.PointCount - 1; i++)
            {
                AdelaideServiceRoadPath.PointAt(i, out var ax, out var az);
                AdelaideServiceRoadPath.PointAt(i + 1, out var bx, out var bz);
                var step = Math.Sqrt((bx - ax) * (bx - ax) + (bz - az) * (bz - az));
                Assert.That(step, Is.LessThan(40.0),
                    $"points {i}->{i + 1} jump {step:F0} m; a vehicle would cut the corner");
            }
        }

        [Test]
        public void TheFrontageItselfPassesUnderNothing()
        {
            // The real road runs about 91 m off the airside wall, which is 44 m clear even of
            // a fully extended aerobridge. Marking an undercroft on it would be a fiction.
            for (var i = 0; i < AdelaideServiceRoadPath.PointCount; i++)
                Assert.That(AdelaideServiceRoadPath.IsUndercroft(i), Is.False);
        }

        [Test]
        public void TheUndercroftSpurIsLongEnoughToDriveThrough()
        {
            Assert.That(AdelaideServiceRoadPath.SpurPointCount, Is.GreaterThan(4));
            Assert.That(AdelaideServiceRoads.UndercroftFirstIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(AdelaideServiceRoads.UndercroftLastIndex,
                Is.GreaterThan(AdelaideServiceRoads.UndercroftFirstIndex));
            Assert.That(AdelaideServiceRoads.UndercroftLastIndex,
                Is.LessThan(AdelaideServiceRoadPath.SpurPointCount));

            var length = AdelaideServiceRoadPath.SpurDistanceBetween(
                AdelaideServiceRoads.UndercroftFirstIndex, AdelaideServiceRoads.UndercroftLastIndex);
            Assert.That(length, Is.GreaterThan(15f),
                "an undercroft a vehicle drives through must be longer than the vehicle");
        }

        [Test]
        public void TheSpurStartsOnTheFrontageAndEndsUnderTheTerminal()
        {
            AdelaideServiceRoadPath.SpurPointAt(0, out var ax, out var az);
            AdelaideServiceRoadPath.PointAt(AdelaideServiceRoads.UndercroftJoinIndex,
                out var jx, out var jz);
            Assert.That(ax, Is.EqualTo(jx).Within(1.0f), "the spur must branch off the real road");
            Assert.That(az, Is.EqualTo(jz).Within(1.0f));

            AdelaideServiceRoadPath.SpurPointAt(AdelaideServiceRoadPath.SpurPointCount - 1,
                out _, out var endZ);
            Assert.That(endZ, Is.GreaterThan(az), "the spur runs inward, toward the building");
            Assert.That(AdelaideServiceRoadPath.IsSpurUndercroft(
                AdelaideServiceRoadPath.SpurPointCount - 1), Is.True,
                "the far end of the spur is under the terminal");
        }

        // ---- driving the road ----------------------------------------------------------

        [Test]
        public void ATripStartsAtTheDepotAndEndsAtTheStand()
        {
            const int from = 10;
            const int to = 120;
            AdelaideServiceRoadPath.Travel(from, to, 0f, out var x0, out var z0, out _);
            AdelaideServiceRoadPath.PointAt(from, out var fx, out var fz);
            Assert.That(x0, Is.EqualTo(fx).Within(0.01f));
            Assert.That(z0, Is.EqualTo(fz).Within(0.01f));

            AdelaideServiceRoadPath.Travel(from, to, 1f, out var x1, out var z1, out _);
            AdelaideServiceRoadPath.PointAt(to, out var tx, out var tz);
            Assert.That(x1, Is.EqualTo(tx).Within(0.5f));
            Assert.That(z1, Is.EqualTo(tz).Within(0.5f));
        }

        [Test]
        public void ATripFollowsTheRoadRatherThanTheStraightLine()
        {
            const int from = 10;
            const int to = 150;
            var along = AdelaideServiceRoadPath.DistanceBetween(from, to);

            AdelaideServiceRoadPath.PointAt(from, out var ax, out var az);
            AdelaideServiceRoadPath.PointAt(to, out var bx, out var bz);
            var direct = Math.Sqrt((bx - ax) * (bx - ax) + (bz - az) * (bz - az));

            Assert.That(along, Is.GreaterThanOrEqualTo(direct - 0.5),
                "a road can never be shorter than the straight line between its ends");

            // Every sampled point must stay near some point of the polyline.
            for (var k = 0; k <= 10; k++)
            {
                AdelaideServiceRoadPath.Travel(from, to, k / 10f, out var px, out var pz, out _);
                var nearest = AdelaideServiceRoadPath.NearestIndex(px, pz);
                AdelaideServiceRoadPath.PointAt(nearest, out var nx, out var nz);
                var off = Math.Sqrt((px - nx) * (px - nx) + (pz - nz) * (pz - nz));
                Assert.That(off, Is.LessThan(20.0), "the trip left the road");
            }
        }

        [Test]
        public void DriveTimeIsPlausibleForAnApronVehicle()
        {
            var seconds = GroundServiceRun.DriveSeconds(0, AdelaideServiceRoadPath.PointCount - 1);
            Assert.That(seconds, Is.GreaterThan(60.0).And.LessThan(600.0),
                "end to end along a 1.1 km apron road at 25 km/h is a couple of minutes");
        }

        // ---- the trip, stage by stage --------------------------------------------------

        [Test]
        public void AVehicleWaitsAtItsDepotWhenThereIsNothingToDo()
        {
            var status = GroundServiceRun.For(GroundServiceKind.Fuel,
                Prep(DeparturePrepStage.Idle), 1200f, 380f, secondsUntilStage: 9999);

            Assert.That(status.Phase, Is.EqualTo(GroundServicePhase.Staged));
            Assert.That(status.Visible, Is.False, "a staged vehicle is not drawn at the stand");
        }

        [Test]
        public void AVehicleSetsOffBeforeItsStageBegins()
        {
            var approaching = GroundServiceRun.For(GroundServiceKind.Fuel,
                Prep(DeparturePrepStage.Catering), 1200f, 380f,
                secondsUntilStage: GroundServiceRun.ApproachLeadSeconds - 1);

            Assert.That(approaching.Phase, Is.EqualTo(GroundServicePhase.Outbound));
            Assert.That(approaching.Driving, Is.True);
            Assert.That(approaching.Visible, Is.True);
        }

        [Test]
        public void AVehicleIsAtTheAircraftWhileItsStageRuns()
        {
            var fuelling = GroundServiceRun.For(GroundServiceKind.Fuel,
                Prep(DeparturePrepStage.Fuel, fuel: 0.4), 1200f, 380f, secondsUntilStage: 0);

            Assert.That(fuelling.Phase, Is.EqualTo(GroundServicePhase.Servicing));
            Assert.That(fuelling.Driving, Is.False);
            Assert.That(fuelling.LegProgress, Is.EqualTo(0.4f).Within(0.01f),
                "servicing progress tracks the stage, not a road position");
        }

        [Test]
        public void AVehicleDrivesHomeOnceItsStageIsDone()
        {
            var leaving = GroundServiceRun.For(GroundServiceKind.Fuel,
                Prep(DeparturePrepStage.Catering, fuel: 1.0), 1200f, 380f,
                secondsUntilStage: -10);

            Assert.That(leaving.Phase, Is.EqualTo(GroundServicePhase.Inbound));
            Assert.That(leaving.Driving, Is.True);
        }

        [Test]
        public void EachVehicleHasItsOwnDepotSoTheyDoNotAllArriveFromOneSpot()
        {
            var fuel = GroundServiceRun.DepotIndex(GroundServiceKind.Fuel);
            var catering = GroundServiceRun.DepotIndex(GroundServiceKind.Catering);
            var baggage = GroundServiceRun.DepotIndex(GroundServiceKind.Baggage);

            Assert.That(fuel, Is.Not.EqualTo(catering));
            Assert.That(catering, Is.Not.EqualTo(baggage));
            Assert.That(fuel, Is.Not.EqualTo(baggage));
        }

        // ---- the point of the exercise -------------------------------------------------

        [Test]
        public void TheBaggageVehicleDrivesOutFromUnderTheTerminal()
        {
            // Baggage works out of the hall beneath the terminal, so its trip begins under the
            // building and emerges onto the apron. This is the whole point of the spur.
            Assert.That(GroundServiceRun.UsesUndercroft(GroundServiceKind.Baggage), Is.True);

            var depot = GroundServiceRun.DepotIndex(GroundServiceKind.Baggage);
            var stand = AdelaideServiceRoadPath.NearestIndex(1500f, 350f);

            GroundServiceRun.TravelOut(GroundServiceKind.Baggage, depot, stand, 0f,
                out _, out _, out var atStart);
            Assert.That(atStart, Is.True, "the trip starts under the terminal");

            GroundServiceRun.TravelOut(GroundServiceKind.Baggage, depot, stand, 1f,
                out _, out _, out var atEnd);
            Assert.That(atEnd, Is.False, "and finishes out on the apron at the stand");
        }

        [Test]
        public void TheBaggageTripLeavesTheUndercroftExactlyOnce()
        {
            var depot = GroundServiceRun.DepotIndex(GroundServiceKind.Baggage);
            var stand = AdelaideServiceRoadPath.NearestIndex(1500f, 350f);

            var transitions = 0;
            bool? previous = null;
            for (var k = 0; k <= 400; k++)
            {
                GroundServiceRun.TravelOut(GroundServiceKind.Baggage, depot, stand, k / 400f,
                    out _, out _, out var under);
                if (previous.HasValue && under != previous.Value)
                    transitions++;
                previous = under;
            }

            Assert.That(transitions, Is.EqualTo(1),
                "a vehicle should emerge from under the terminal once, not flicker in and out");
        }

        [Test]
        public void FuelAndCateringNeverGoUnderTheTerminal()
        {
            foreach (var kind in new[] { GroundServiceKind.Fuel, GroundServiceKind.Catering })
            {
                Assert.That(GroundServiceRun.UsesUndercroft(kind), Is.False);
                var depot = GroundServiceRun.DepotIndex(kind);
                var stand = AdelaideServiceRoadPath.NearestIndex(1200f, 350f);
                for (var k = 0; k <= 100; k++)
                {
                    GroundServiceRun.TravelOut(kind, depot, stand, k / 100f, out _, out _, out var under);
                    Assert.That(under, Is.False, $"{kind} stays on the apron frontage");
                }
            }
        }
    }
}

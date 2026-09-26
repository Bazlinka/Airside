using System;
using System.Collections.Generic;
using Airside.Domain;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>
    /// Every place a fleet aircraft hands from one drawn leg to the next — rollout to vacate,
    /// vacate to taxi-in, taxi-in to the stand, stand to pushback, taxi-out to lineup and
    /// lineup to the takeoff roll — must meet in position and heading, for every runway end,
    /// every type and every stand that type can use. A gap here is a visible teleport.
    /// </summary>
    public sealed class GroundLegSeamTests
    {
        private const float MaxGapMetres = 3f;
        private const float MaxTurnDegrees = 20f;

        private static readonly RunwayDirection[] Runways =
        {
            RunwayDirection.Runway05, RunwayDirection.Runway23, RunwayDirection.Runway12, RunwayDirection.Runway30
        };

        private static IEnumerable<AircraftType> Types()
        {
            foreach (var spec in AircraftCatalogue.All)
                yield return spec.Type;
        }

        private static IEnumerable<StableId> StandsFor(AircraftType type)
        {
            foreach (var stand in AirlineOperations.AdelaideRegionalBays)
                if (AirlineOperations.StandFits(type, stand))
                    yield return stand;
            foreach (var stand in AirlineOperations.AdelaideTerminalGates)
                if (AirlineOperations.StandFits(type, stand))
                    yield return stand;
        }

        private static bool UsesRunway(AircraftType type, RunwayDirection runway) =>
            RunwayWeather.IsMainRunway(runway) || !AirlineOperations.NeedsTerminalGate(type);

        [Test]
        public void Arrival_RolloutVacateAwaitAndTaxiInMeet()
        {
            var failures = new List<string>();
            foreach (var runway in Runways)
            {
                RunwayFrame.ToWorld(runway, CircuitProfile.RolloutEndX, 0f, 0f, out var rollX, out _, out var rollZ);
                RunwayFrame.Forward(runway, out var fx, out var fz);
                foreach (var type in Types())
                {
                    if (!UsesRunway(type, runway))
                        continue;
                    var vacate = AdelaideGround.VacateFor(type, runway);
                    var start = vacate.PoseAt(0);
                    Check(failures, $"{runway} {type.Id} rollout→vacate", rollX, rollZ, fx, fz, start);

                    var end = vacate.PoseAt(vacate.Seconds);
                    var await0 = AdelaideGround.AwaitingPose(0, type, runway);
                    Check(failures, $"{runway} {type.Id} vacate→await", end.X, end.Z, end.NoseX, end.NoseZ, await0);

                    foreach (var stand in StandsFor(type))
                    {
                        var taxiIn = AdelaideGround.TaxiIn(stand, type, runway);
                        Check(failures, $"{runway} {type.Id} await→taxi-in {stand.Value}",
                            await0.X, await0.Z, await0.NoseX, await0.NoseZ, taxiIn.PoseAt(0));
                    }
                }
            }

            Assert.That(failures, Is.Empty, Report(failures));
        }

        [Test]
        public void Stand_TaxiInEndsAndTaxiOutStartsOnTheStand()
        {
            var failures = new List<string>();
            foreach (var type in Types())
            foreach (var stand in StandsFor(type))
            {
                var parked = AdelaideGround.StandPose(stand);
                var taxiIn = AdelaideGround.TaxiIn(stand, type);
                var arrived = taxiIn.PoseAt(taxiIn.Seconds);
                Check(failures, $"{type.Id} taxi-in→stand {stand.Value}",
                    arrived.X, arrived.Z, arrived.NoseX, arrived.NoseZ, parked);

                foreach (var runway in Runways)
                {
                    if (!UsesRunway(type, runway))
                        continue;
                    var taxiOut = AdelaideGround.TaxiOut(stand, type, runway);
                    Check(failures, $"{runway} {type.Id} stand→pushback {stand.Value}",
                        parked.X, parked.Z, parked.NoseX, parked.NoseZ, taxiOut.PoseAt(0));
                }
            }

            Assert.That(failures, Is.Empty, Report(failures));
        }

        [Test]
        public void Departure_TaxiOutHoldLineupAndTakeoffRollMeet()
        {
            var failures = new List<string>();
            foreach (var runway in Runways)
            {
                RunwayFrame.ToWorld(runway, CircuitProfile.TakeoffStartX, 0f, 0f, out var rollX, out _, out var rollZ);
                RunwayFrame.Forward(runway, out var fx, out var fz);

                foreach (var type in Types())
                {
                    if (!UsesRunway(type, runway))
                        continue;
                    var lineup = AdelaideGround.LineupFor(runway, type);
                    var lineupStart = lineup.PoseAt(0);
                    var lineupEnd = lineup.PoseAt(lineup.Seconds);
                    Check(failures, $"{runway} {type.Id} lineup→takeoff roll", lineupEnd.X, lineupEnd.Z,
                        lineupEnd.NoseX, lineupEnd.NoseZ, new GroundPose(rollX, rollZ, fx, fz, 0f, false));

                foreach (var stand in StandsFor(type))
                {
                    var taxiOut = AdelaideGround.TaxiOut(stand, type, runway);
                    var end = taxiOut.PoseAt(taxiOut.Seconds);
                    var hold = AdelaideGround.HoldingShortPose(stand, 0, runway, type);
                    Check(failures, $"{runway} {type.Id} taxi-out→hold {stand.Value}",
                        end.X, end.Z, end.NoseX, end.NoseZ, hold);
                    // Lineup turns onto the runway, so only position must meet here.
                    Check(failures, $"{runway} {type.Id} hold→lineup {stand.Value}",
                        hold.X, hold.Z, hold.NoseX, hold.NoseZ, lineupStart, checkHeading: false);
                }
                }
            }

            Assert.That(failures, Is.Empty, Report(failures));
        }

        private static void Check(List<string> failures, string seam, float x, float z, float noseX, float noseZ,
            GroundPose next, bool checkHeading = true)
        {
            var gap = Math.Sqrt((next.X - x) * (next.X - x) + (next.Z - z) * (next.Z - z));
            if (gap > MaxGapMetres)
                failures.Add($"{seam}: {gap:0.0} m gap");
            if (!checkHeading)
                return;
            var dot = Math.Max(-1.0, Math.Min(1.0, noseX * next.NoseX + noseZ * next.NoseZ));
            var turn = Math.Acos(dot) * 180.0 / Math.PI;
            if (turn > MaxTurnDegrees)
                failures.Add($"{seam}: nose snaps {turn:0}°");
        }

        private static string Report(List<string> failures)
        {
            var shown = failures.Count > 60 ? failures.GetRange(0, 60) : failures;
            return $"{failures.Count} seam(s):\n" + string.Join("\n", shown);
        }
    }
}

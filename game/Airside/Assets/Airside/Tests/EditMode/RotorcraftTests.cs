using System;
using System.Linq;
using Airside.Domain;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>ADR 0207: helicopters in the airline simulation — type, pad, flight shape, weather and rescue flying.</summary>
    public sealed class RotorcraftTests
    {
        private static AirlineOperations NewOps(out ManualSimulationClock clock, int seed = 2026)
        {
            clock = new ManualSimulationClock(new SimulationTime(0));
            return AirlineOperations.StartAtAdelaide(clock, new SeededRandomSource((uint)seed),
                Airline.Player("Rotor Air", "#1F3A93"));
        }

        private static FleetAircraft Rescue(AirlineOperations ops) =>
            ops.Fleet.Single(a => a.Airline.Id.Value == "SAAS");

        // ---- Type and catalogue --------------------------------------------------------------

        [Test]
        public void Bell412_IsARotorcraft_OnTheHelipad_AndNotInTheAirlineList()
        {
            Assert.That(AircraftType.Bell412.IsRotorcraft, Is.True);
            Assert.That(AircraftType.Atr42.IsRotorcraft, Is.False);
            Assert.That(AircraftCatalogue.Bell412.StandClass, Is.EqualTo(StandClass.Helipad));
            Assert.That(AircraftCatalogue.Bell412.StandClassLabel, Is.EqualTo("Helipad"));
            Assert.That(AircraftCatalogue.All.Any(s => s.Id == "B412"), Is.False,
                "every runway and taxi rule iterates All; a helicopter must not appear there");
            Assert.That(AircraftCatalogue.Rotorcraft, Has.Count.EqualTo(1));
            Assert.That(AircraftCatalogue.TryFor(AircraftType.Bell412, out var spec), Is.True);
            Assert.That(spec.Id, Is.EqualTo("B412"));
            Assert.That(AircraftType.TryFromId("B412", out var found), Is.True);
            Assert.That(found.IsRotorcraft, Is.True);
        }

        [Test]
        public void Bell412_FactsMatchBellsPublishedEnvelope()
        {
            var spec = AircraftCatalogue.Bell412;
            Assert.That(spec.LengthMetres, Is.EqualTo(17.1).Within(0.05), "rotors turning");
            Assert.That(spec.WingspanMetres, Is.EqualTo(14.02).Within(0.05), "the rotor disc");
            Assert.That(spec.HeightMetres, Is.EqualTo(4.6).Within(0.05));
            Assert.That(spec.PlanningCruiseKmh, Is.LessThanOrEqualTo(spec.ManufacturerMaxCruiseKmh));
            Assert.That(spec.PracticalRangeKm, Is.LessThanOrEqualTo(spec.ManufacturerRangeKm));
            Assert.That(spec.ModelStatus, Is.EqualTo(ModelStatus.Genuine));
        }

        // ---- Stands ---------------------------------------------------------------------------

        [Test]
        public void Helicopters_ParkOnTheHelipadOnly_AndNothingElseParksThere()
        {
            foreach (var stand in AirlineOperations.AdelaideHelipadStands)
            {
                Assert.That(AirlineOperations.StandFits(AircraftType.Bell412, stand), Is.True);
                Assert.That(AirlineOperations.StandFits(AircraftType.Atr42, stand), Is.False);
                Assert.That(AirlineOperations.StandFits(AircraftType.Boeing737800, stand), Is.False);
            }

            foreach (var stand in AirlineOperations.AdelaideRegionalBays.Concat(AirlineOperations.AdelaideTerminalGates))
                Assert.That(AirlineOperations.StandFits(AircraftType.Bell412, stand), Is.False);
            Assert.That(AirlineOperations.AdelaideStands.Count,
                Is.EqualTo(AirlineOperations.AdelaideRegionalBays.Count + AirlineOperations.AdelaideTerminalGates.Count
                           + AirlineOperations.AdelaideHelipadStands.Count));
        }

        [Test]
        public void HelipadSpots_SitOnThePad_WithRotorDiscsClearOfEachOther()
        {
            var spots = AdelaideHelipad.Spots;
            var rotor = AircraftCatalogue.Bell412.WingspanMetres;
            foreach (var spot in spots)
            {
                var fromCentre = Math.Sqrt(Math.Pow(spot.X - AdelaideHelipad.PadCentreX, 2)
                                           + Math.Pow(spot.Z - AdelaideHelipad.PadCentreZ, 2));
                Assert.That(fromCentre + rotor / 2.0, Is.LessThan(AdelaideHelipad.PadRadiusMetres),
                    $"{spot.Id} rotor stays over the pad");
            }

            for (var i = 0; i < spots.Length; i++)
            for (var j = i + 1; j < spots.Length; j++)
            {
                var apart = Math.Sqrt(Math.Pow(spots[i].X - spots[j].X, 2) + Math.Pow(spots[i].Z - spots[j].Z, 2));
                Assert.That(apart, Is.GreaterThan(rotor), $"{spots[i].Id} / {spots[j].Id} discs do not overlap");
            }
        }

        [Test]
        public void SpotPose_FacesOutward_AndTheGroundPoseSeesTheHelipad()
        {
            foreach (var spot in AdelaideHelipad.Spots)
            {
                var pose = AdelaideGround.StandPose(spot.Id);
                Assert.That(pose.X, Is.EqualTo(spot.X));
                Assert.That(pose.Z, Is.EqualTo(spot.Z));
                var outwardX = spot.X - AdelaideHelipad.PadCentreX;
                var outwardZ = spot.Z - AdelaideHelipad.PadCentreZ;
                Assert.That(pose.NoseX * outwardX + pose.NoseZ * outwardZ, Is.GreaterThan(0f));
                Assert.That(AdelaideGround.StandLabel(spot.Id), Does.StartWith("Helipad spot"));
            }
        }

        // ---- Flight shape ---------------------------------------------------------------------

        [Test]
        public void Takeoff_StartsOnThePad_LiftsToAHover_TurnsThenAcceleratesAway()
        {
            var p = RotorcraftPerformance.Bell412;
            var start = HelicopterFlight.TakeoffPose(p, 0f);
            Assert.That(start.HeightMetres, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(start.AlongMetres, Is.EqualTo(0f));
            Assert.That(start.Turn01, Is.EqualTo(0f));

            var hover = HelicopterFlight.TakeoffPose(p, p.LiftSeconds + 0.1f);
            Assert.That(hover.HeightMetres, Is.EqualTo(p.HoverHeightMetres).Within(0.05f));
            Assert.That(hover.SpeedMetresPerSecond, Is.EqualTo(0f), "a hover is stationary");

            var midTurn = HelicopterFlight.TakeoffPose(p, p.LiftSeconds + p.HoverHoldSeconds + p.PedalTurnSeconds * 0.5f);
            Assert.That(midTurn.Turn01, Is.InRange(0.3f, 0.7f));
            Assert.That(midTurn.AlongMetres, Is.EqualTo(0f), "the pedal turn is on the spot");

            var end = HelicopterFlight.TakeoffPose(p, p.TakeoffSeconds);
            Assert.That(end.Turn01, Is.EqualTo(1f));
            Assert.That(end.HeightMetres, Is.EqualTo(p.ClimbOutHeightMetres).Within(0.5f));
            Assert.That(end.SpeedMetresPerSecond, Is.EqualTo(p.ClimbOutMetresPerSecond).Within(0.01f));
            Assert.That(end.AlongMetres, Is.EqualTo(p.ClimbOutDistanceMetres).Within(0.5f));
        }

        [Test]
        public void Takeoff_NeverGoesBackwardsOrDown_AndMatchesTheCruiseHandOver()
        {
            var p = RotorcraftPerformance.Bell412;
            var lastAlong = -1f;
            var lastHeight = -1f;
            for (var t = 0f; t <= p.TakeoffSeconds; t += 0.25f)
            {
                var pose = HelicopterFlight.TakeoffPose(p, t);
                Assert.That(pose.AlongMetres, Is.GreaterThanOrEqualTo(lastAlong - 1e-3f), $"t={t}");
                Assert.That(pose.HeightMetres, Is.GreaterThanOrEqualTo(lastHeight - 1e-3f), $"t={t}");
                lastAlong = pose.AlongMetres;
                lastHeight = pose.HeightMetres;
            }

            var end = HelicopterFlight.TakeoffPose(p, p.TakeoffSeconds);
            var handOver = HelicopterFlight.CruiseStart(p);
            Assert.That(handOver.AlongMetres, Is.EqualTo(end.AlongMetres).Within(0.5f));
            Assert.That(handOver.HeightMetres, Is.EqualTo(end.HeightMetres).Within(0.5f));
            Assert.That(handOver.SpeedMetresPerSecond, Is.EqualTo(end.SpeedMetresPerSecond).Within(0.01f));
        }

        [Test]
        public void Landing_ArrivesAtSpeedOnTheApproach_DecelerateToAHover_TurnsAndSettles()
        {
            var p = RotorcraftPerformance.Bell412;
            var start = HelicopterFlight.LandingPose(p, 0f);
            Assert.That(start.AlongMetres, Is.EqualTo(-p.ApproachDistanceMetres).Within(0.5f));
            Assert.That(start.HeightMetres, Is.EqualTo(p.ApproachHeightMetres).Within(0.5f));
            Assert.That(start.SpeedMetresPerSecond, Is.EqualTo(p.ApproachMetresPerSecond).Within(0.01f));

            var hover = HelicopterFlight.LandingPose(p, p.DecelSeconds + 0.1f);
            Assert.That(hover.AlongMetres, Is.EqualTo(0f).Within(0.1f));
            Assert.That(hover.HeightMetres, Is.EqualTo(p.HoverHeightMetres).Within(0.05f));
            Assert.That(hover.SpeedMetresPerSecond, Is.EqualTo(0f));

            var end = HelicopterFlight.LandingPose(p, p.LandingSeconds);
            Assert.That(end.HeightMetres, Is.EqualTo(0f).Within(1e-3f));
            Assert.That(end.Turn01, Is.EqualTo(1f), "back to the parked heading");
            Assert.That(end.SpeedMetresPerSecond, Is.EqualTo(0f));
        }

        [Test]
        public void Landing_NeverClimbsOrOvershootsThePad()
        {
            var p = RotorcraftPerformance.Bell412;
            var lastAlong = float.MinValue;
            var lastHeight = float.MaxValue;
            for (var t = 0f; t <= p.LandingSeconds; t += 0.25f)
            {
                var pose = HelicopterFlight.LandingPose(p, t);
                Assert.That(pose.AlongMetres, Is.LessThanOrEqualTo(1e-3f), $"t={t}: never past the pad");
                Assert.That(pose.AlongMetres, Is.GreaterThanOrEqualTo(lastAlong - 1e-3f), $"t={t}");
                Assert.That(pose.HeightMetres, Is.LessThanOrEqualTo(lastHeight + 1e-3f), $"t={t}: only ever descending");
                lastAlong = pose.AlongMetres;
                lastHeight = pose.HeightMetres;
            }
        }

        [Test]
        public void ClimbOutAndApproach_AreHelicopterSpeedsNotJetSpeeds()
        {
            var p = RotorcraftPerformance.Bell412;
            Assert.That(p.ClimbOutKnots, Is.InRange(60f, 100f));
            Assert.That(p.ApproachKnots, Is.InRange(50f, 90f));
            Assert.That(p.CruiseKnots, Is.LessThanOrEqualTo(122f), "Bell's maximum cruise");
            Assert.That(CircuitProfile.Knots(p.CruiseKnots) * 3.6, Is.LessThanOrEqualTo(AircraftCatalogue.Bell412.ManufacturerMaxCruiseKmh + 1));
            Assert.That(p.TakeoffSeconds, Is.InRange(20f, 90f));
            Assert.That(p.LandingSeconds, Is.InRange(20f, 90f));
        }

        // ---- Leg timing -----------------------------------------------------------------------

        [Test]
        public void ShortLegs_CarryNoJetClimbAllowance()
        {
            var km = 12.0;
            var heli = LegTiming.AirborneSeconds(km, AircraftType.Bell412);
            var atr = LegTiming.AirborneSeconds(km, AircraftType.Atr42);
            Assert.That(heli, Is.LessThan(atr), "the ten-minute climb and descent allowance is a fixed-wing figure");
            Assert.That(heli, Is.InRange(LegTiming.RotorcraftLegAllowanceSeconds,
                LegTiming.RotorcraftLegAllowanceSeconds + 600));
        }

        // ---- Weather rules --------------------------------------------------------------------

        [TestCase(WeatherKind.Clear, 10, false, true)]
        [TestCase(WeatherKind.Rain, 25, false, true)]
        [TestCase(WeatherKind.Storm, 5, false, false)]
        [TestCase(WeatherKind.Storm, 5, true, false)]
        [TestCase(WeatherKind.Fog, 5, false, false)]
        [TestCase(WeatherKind.Fog, 5, true, true)]
        [TestCase(WeatherKind.Clear, 45, false, false)]
        [TestCase(WeatherKind.Clear, 45, true, true)]
        [TestCase(WeatherKind.Clear, 60, true, false)]
        public void MayMove_FollowsWeatherAndWind(WeatherKind weather, int wind, bool rescue, bool expected)
        {
            Assert.That(RotorcraftRules.MayMove(weather, wind, rescue), Is.EqualTo(expected));
        }

        // ---- Rescue operator ------------------------------------------------------------------

        [Test]
        public void NewGame_HasTheRescueHelicopterOnTheHelipad()
        {
            var ops = NewOps(out _);
            var heli = Rescue(ops);
            Assert.That(heli.Type.IsRotorcraft, Is.True);
            Assert.That(heli.Airline.IsEmergency, Is.True);
            Assert.That(AirlineOperations.ExemptFromCurfew(heli), Is.True);
            Assert.That(AirlineOperations.AdelaideHelipadStands, Does.Contain(heli.Stand));
            Assert.That(heli.State, Is.EqualTo(FleetState.AtStand));
            Assert.That(heli.Scheduled.HasValue, Is.True, "the crew has a call-out booked");
            Assert.That(DestinationCatalogue.IsRescueSite(heli.Scheduled.Value.Destination), Is.True);
        }

        [Test]
        public void RescueSites_AreFlyableButNotAirportsOnTheRouteMap()
        {
            foreach (var site in DestinationCatalogue.RescueSites)
            {
                Assert.That(DestinationCatalogue.TryFind(site.Code, out var found), Is.True, site.Code);
                Assert.That(found.Equals(site), Is.True);
                Assert.That(DestinationCatalogue.All.Any(d => d.Code == site.Code), Is.False,
                    $"{site.Code} is not an airport a route can be planned to");
                Assert.That(site.DistanceKmTo(DestinationCatalogue.Adelaide),
                    Is.InRange(2.0, AircraftType.Bell412.PracticalRangeKm), site.Code);
            }
        }

        [Test]
        public void TheRescueCrew_FliesADayOfCallouts_AndAlwaysComesHome()
        {
            var ops = NewOps(out var clock);
            var heli = Rescue(ops);
            var seen = new System.Collections.Generic.HashSet<FleetState>();
            var seenSites = new System.Collections.Generic.HashSet<string>();
            for (var minute = 0; minute < 24 * 60; minute++)
            {
                clock.Advance(60);
                ops.Update();
                seen.Add(heli.State);
                if (heli.CurrentDestination.HasValue)
                    seenSites.Add(heli.CurrentDestination.Value.Code);
            }

            Assert.That(seen, Does.Contain(FleetState.TakingOff));
            Assert.That(seen, Does.Contain(FleetState.Outbound));
            Assert.That(seen, Does.Contain(FleetState.AtDestination));
            Assert.That(seen, Does.Contain(FleetState.Inbound));
            Assert.That(seen, Does.Contain(FleetState.Landing));
            Assert.That(seen, Does.Not.Contain(FleetState.TaxiOut), "a helicopter never taxis");
            Assert.That(seen, Does.Not.Contain(FleetState.HoldingShort));
            Assert.That(seen, Does.Not.Contain(FleetState.HoldingForLanding));
            Assert.That(seen, Does.Not.Contain(FleetState.TaxiIn));
            Assert.That(heli.CompletedTrips, Is.GreaterThanOrEqualTo(3), "a rescue helicopter flies several call-outs a day");
            Assert.That(seenSites.Count, Is.GreaterThanOrEqualTo(2), "to more than one hospital");
            Assert.That(seenSites.All(DestinationCatalogue.RescueSites.Select(s => s.Code).Contains), Is.True);
        }

        [Test]
        public void ARescueMovement_NeverHoldsARunway()
        {
            var ops = NewOps(out var clock);
            var heli = Rescue(ops);
            for (var minute = 0; minute < 24 * 60; minute++)
            {
                clock.Advance(60);
                ops.Update();
                Assert.That(ops.IsOccupyingRunway(heli), Is.False, $"minute {minute}: {heli.State}");
            }
        }

        [Test]
        public void TheHelicopterFlies_WhileTheJetsKeepFlying()
        {
            // The helicopter shares no runway, taxiway or stand with the jets, so both streams run side by side.
            var with = NewOps(out var clockWith, 77);
            for (var minute = 0; minute < 12 * 60; minute++)
            {
                clockWith.Advance(60);
                with.Update();
            }

            var jets = with.Fleet.Where(a => !a.Type.IsRotorcraft).ToList();
            Assert.That(jets.Sum(a => a.CompletedTrips), Is.GreaterThan(0), "traffic flowed");
            Assert.That(with.Fleet.Any(a => a.Type.IsRotorcraft && a.CompletedTrips > 0), Is.True);
        }

        [Test]
        public void ThePad_IsHeldWhileAHelicopterLiftsOffOrLands_AndFreeOtherwise()
        {
            var ops = NewOps(out var clock);
            var heli = Rescue(ops);
            Assert.That(ops.IsStandFree(heli.Stand), Is.False);
            var sawBusy = false;
            for (var second = 0; second < 6 * 3600; second++)
            {
                clock.Advance(1);
                ops.Update();
                if (heli.State is FleetState.TakingOff or FleetState.Landing)
                {
                    sawBusy = true;
                    Assert.That(ops.NextEventAt().HasValue, Is.True);
                }
            }

            Assert.That(sawBusy, Is.True);
        }

        [Test]
        public void TheDay_IsDeterministic_SameSeedSameCallouts()
        {
            string Run(int seed)
            {
                var ops = NewOps(out var clock, seed);
                var heli = Rescue(ops);
                var log = new System.Text.StringBuilder();
                for (var minute = 0; minute < 8 * 60; minute++)
                {
                    clock.Advance(60);
                    ops.Update();
                    log.Append(heli.State).Append(heli.CurrentDestination?.Code).Append(heli.CompletedTrips).Append(';');
                }

                return log.ToString();
            }

            Assert.That(Run(5), Is.EqualTo(Run(5)));
        }

        [Test]
        public void SkipToNextEvent_MatchesSecondBySecond_ForTheHelicopter()
        {
            var a = NewOps(out var clockA, 3);
            var b = NewOps(out var clockB, 3);
            var heliA = Rescue(a);
            var heliB = Rescue(b);
            const long horizon = 10 * 3600;
            while (clockA.Now.ElapsedSeconds < horizon)
            {
                clockA.Advance(1);
                a.Update();
            }

            while (clockB.Now.ElapsedSeconds < horizon)
            {
                var next = b.NextEventAt();
                var target = next.HasValue ? Math.Min(next.Value.ElapsedSeconds, horizon) : horizon;
                if (target > clockB.Now.ElapsedSeconds)
                    clockB.Set(new SimulationTime(target));
                b.Update();
            }

            Assert.That(heliB.State, Is.EqualTo(heliA.State));
            Assert.That(heliB.CompletedTrips, Is.EqualTo(heliA.CompletedTrips));
            Assert.That(heliB.CurrentDestination?.Code, Is.EqualTo(heliA.CurrentDestination?.Code));
        }

        // ---- Saves ----------------------------------------------------------------------------

        [Test]
        public void ASave_TakenInEveryHelicopterState_RestoresAndFliesOnIdentically()
        {
            var ops = NewOps(out var clock, 9);
            var heli = Rescue(ops);
            var checkedStates = new System.Collections.Generic.HashSet<FleetState>();
            for (var second = 0; second < 14 * 3600 && checkedStates.Count < 5; second++)
            {
                clock.Advance(1);
                ops.Update();
                if (!(heli.State is FleetState.TakingOff or FleetState.Outbound or FleetState.AtDestination
                      or FleetState.Inbound or FleetState.Landing) || !checkedStates.Add(heli.State))
                    continue;

                var restoredClock = new ManualSimulationClock(clock.Now);
                var restored = AirlineSave.Restore(AirlineSave.Capture(ops), restoredClock);
                var twin = restored.Fleet.Single(a => a.Registration == heli.Registration);
                Assert.That(twin.State, Is.EqualTo(heli.State));
                Assert.That(twin.CurrentDestination?.Code, Is.EqualTo(heli.CurrentDestination?.Code));
                Assert.That(twin.StateEndsAt?.ElapsedSeconds, Is.EqualTo(heli.StateEndsAt?.ElapsedSeconds));
                Assert.That(twin.Type.IsRotorcraft, Is.True);

                // Both worlds then run an hour and must agree on what the helicopter did.
                var liveClock = new ManualSimulationClock(clock.Now);
                for (var t = 0; t < 3600; t++)
                {
                    restoredClock.Advance(1);
                    restored.Update();
                }

                var reference = AirlineSave.Restore(AirlineSave.Capture(ops), liveClock);
                for (var t = 0; t < 3600; t++)
                {
                    liveClock.Advance(1);
                    reference.Update();
                }

                var a = reference.Fleet.Single(x => x.Registration == heli.Registration);
                var b = restored.Fleet.Single(x => x.Registration == heli.Registration);
                Assert.That(b.State, Is.EqualTo(a.State), $"after {heli.State}");
                Assert.That(b.CompletedTrips, Is.EqualTo(a.CompletedTrips));
            }

            Assert.That(checkedStates.Count, Is.GreaterThanOrEqualTo(4), "saved in most of the helicopter states");
        }

        [Test]
        public void AnOlderSave_WithoutTheRescueHelicopter_GainsItOnLoad()
        {
            var ops = NewOps(out var clock, 4);
            var data = AirlineSave.Capture(ops);
            data.Fleet.RemoveAll(r => r.Registration == "VH-SAR");
            var restored = AirlineSave.Restore(data, new ManualSimulationClock(clock.Now));
            Assert.That(restored.Fleet.Any(a => a.Registration == "VH-SAR" && a.Type.IsRotorcraft), Is.True);
        }
    }
}

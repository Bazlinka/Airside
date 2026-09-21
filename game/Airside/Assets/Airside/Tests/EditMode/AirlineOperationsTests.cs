using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Airside.Domain;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>Player airline, AI airline and the tower at Adelaide (ADR 0045).</summary>
    public sealed class AirlineOperationsTests
    {
        private static Destination Code(string code)
        {
            Assert.That(DestinationCatalogue.TryFind(code, out var destination), Is.True, code);
            return destination;
        }

        private static Airline Player() => Airline.Player("Southern Cross Regional", "#C8102E");

        /// <summary>Home with only the player's airline, so nothing moves unless asked.</summary>
        private static (ManualSimulationClock clock, AirlineOperations ops, FleetAircraft plane) PlayerOnly(int aircraft = 1)
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = new AirlineOperations(clock, new SeededRandomSource(7), DestinationCatalogue.Adelaide,
                AirlineOperations.AdelaideRegionalBays);
            var player = Player();
            ops.AddAirline(player);
            FleetAircraft first = null;
            for (var i = 0; i < aircraft; i++)
            {
                var added = ops.AddAircraft(player, $"VH-PA{(char)('A' + i)}", AircraftType.Saab340,
                    AirlineOperations.AdelaideRegionalBays[i]);
                first ??= added;
            }

            return (clock, ops, first);
        }

        private static void RunTo(ManualSimulationClock clock, AirlineOperations ops, long seconds)
        {
            clock.Set(new SimulationTime(seconds));
            ops.Update();
        }

        [Test]
        public void Adelaide_IsTheDefaultLocation()
        {
            Assert.That(AirportLocation.Default, Is.EqualTo(AirportLocation.Adelaide));
            Assert.That(AirportLocation.FromId("adl").Name, Is.EqualTo("Adelaide"));
        }

        [TestCase("KGC", 110, 130)]
        [TestCase("PLO", 240, 270)]
        [TestCase("MGB", 350, 400)]
        [TestCase("MEL", 630, 670)]
        [TestCase("SYD", 1140, 1190)]
        [TestCase("PER", 2100, 2150)]
        [TestCase("DRW", 2590, 2660)]
        public void LegDistances_MatchRealGreatCircleFigures(string code, double minKm, double maxKm)
        {
            var km = DestinationCatalogue.Adelaide.DistanceKmTo(Code(code));
            Assert.That(km, Is.InRange(minKm, maxKm));
        }

        [Test]
        public void Atr42_ReachesRegionalAndSouthEastButNotTheFarCities()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = new AirlineOperations(clock, new SeededRandomSource(7), DestinationCatalogue.Adelaide,
                AirlineOperations.AdelaideRegionalBays);
            var player = Player();
            ops.AddAirline(player);
            var plane = ops.AddAircraft(player, "VH-ATR", AircraftType.Atr42,
                AirlineOperations.AdelaideRegionalBays[0]);
            foreach (var code in new[] { "KGC", "PLO", "WYA", "MGB", "CED", "CPD", "MQL", "BHQ", "MEL", "CBR" })
                Assert.That(ops.CanReach(plane, Code(code)), Is.True, code);
            foreach (var code in new[] { "SYD", "HBA", "ASP", "BNE", "OOL", "CNS", "DRW", "PER" })
                Assert.That(ops.CanReach(plane, Code(code)), Is.False, code);
            Assert.That(ops.MapDestinations().Any(d => d.Equals(DestinationCatalogue.Adelaide)), Is.False);
        }

        [Test]
        public void Saab340_ReachesRegionalHopsButNotSydney()
        {
            var (_, ops, plane) = PlayerOnly();
            foreach (var code in new[] { "KGC", "PLO", "WYA", "MGB", "CED", "MEL" })
                Assert.That(ops.CanReach(plane, Code(code)), Is.True, code);
            foreach (var code in new[] { "SYD", "BNE", "PER", "DRW" })
                Assert.That(ops.CanReach(plane, Code(code)), Is.False, code);
        }

        [Test]
        public void LegTiming_IsRealLength()
        {
            var (_, ops, plane) = PlayerOnly();
            Assert.That(ops.AirborneSeconds(plane, Code("KGC")) / 60.0, Is.InRange(22, 30));
            Assert.That(ops.AirborneSeconds(plane, Code("MEL")) / 60.0, Is.InRange(80, 95));
        }

        [Test]
        public void PlayerRoundTrip_GoesOutAndBackThenWaitsForAStand()
        {
            var (clock, ops, plane) = PlayerOnly();
            var kingscote = Code("KGC");
            Assert.That(ops.ScheduleDeparture(plane, kingscote, new SimulationTime(600)).Accepted, Is.True);

            RunTo(clock, ops, 599);
            Assert.That(plane.State, Is.EqualTo(FleetState.AtStand));
            Assert.That(ops.IsStandFree(AirlineOperations.AdelaideRegionalBays[0]), Is.False);

            RunTo(clock, ops, 600);
            Assert.That(plane.State, Is.EqualTo(FleetState.TaxiOut));
            Assert.That(ops.IsStandFree(AirlineOperations.AdelaideRegionalBays[0]), Is.False,
                "bay stays held through taxi-out so a landing cannot take it mid-push");

            var takeoffAt = plane.StateEndsAt.Value.ElapsedSeconds;
            RunTo(clock, ops, takeoffAt);
            Assert.That(plane.State, Is.EqualTo(FleetState.TakingOff), "empty runway: no hold");

            // The runway it was given, not 05's lineup: a regional can be sent to 12/30.
            var outboundAt = takeoffAt + AirlineOperations.TakeoffRunwaySecondsFor(plane.Type, plane.AssignedRunway);
            RunTo(clock, ops, outboundAt + 1);
            Assert.That(plane.State, Is.EqualTo(FleetState.Outbound));
            Assert.That(plane.IsOffMap, Is.True);
            Assert.That(plane.CurrentDestination, Is.EqualTo(kingscote));

            var airborne = ops.AirborneSeconds(plane, kingscote);
            var landingAt = outboundAt + airborne + AirlineOperations.DestinationTurnaroundSeconds + airborne;
            RunTo(clock, ops, landingAt);
            Assert.That(plane.State, Is.EqualTo(FleetState.Landing));

            RunTo(clock, ops, landingAt + AirlineOperations.LandingRunwaySeconds + 3600);
            Assert.That(plane.State, Is.EqualTo(FleetState.AtStand), "player aircraft auto-park when a stand is free");
            Assert.That(plane.CompletedTrips, Is.EqualTo(1));
            Assert.That(plane.CurrentDestination, Is.Null);
        }

        [Test]
        public void Commands_RefuseWhatTheAirportWouldNotAllow()
        {
            var (clock, ops, plane) = PlayerOnly(aircraft: 2);
            var other = ops.Fleet[1];

            Assert.That(ops.ScheduleDeparture(plane, Code("PER"), new SimulationTime(60)).Accepted, Is.False, "out of range");
            Assert.That(ops.ScheduleDeparture(plane, Code("MEL"), new SimulationTime(600)).Accepted, Is.False, "band");
            Assert.That(ops.ScheduleDeparture(plane, DestinationCatalogue.Adelaide, new SimulationTime(60)).Accepted, Is.False, "home");
            Assert.That(ops.AssignStand(plane, AirlineOperations.AdelaideRegionalBays[2]).Accepted, Is.False, "not awaiting a stand");

            RunTo(clock, ops, 100);
            Assert.That(ops.ScheduleDeparture(plane, Code("KGC"), new SimulationTime(50)).Accepted, Is.False, "in the past");

            ops.ScheduleDeparture(plane, Code("KGC"), new SimulationTime(400));
            RunTo(clock, ops, 401);
            Assert.That(ops.ScheduleDeparture(plane, Code("PLO"), new SimulationTime(500)).Accepted, Is.False, "already taxiing");
            Assert.That(ops.CancelDeparture(plane).Accepted, Is.False);

            Assert.That(ops.ScheduleDeparture(other, Code("KGC"), new SimulationTime(5000)).Accepted, Is.True);
            Assert.That(ops.CancelDeparture(other).Accepted, Is.True);
            Assert.That(other.Scheduled, Is.Null);

            RunTo(clock, ops, 20000);
            Assert.That(plane.State, Is.EqualTo(FleetState.AtStand));
            Assert.That(ops.AssignStand(plane, other.Stand).Accepted, Is.False, "not awaiting a stand");
            Assert.That(ops.AssignStand(plane, new StableId("BAY-99")).Accepted, Is.False, "not a stand");
        }

        [Test]
        public void Tower_SequencesQueuedDeparturesWithRunwaySeparation()
        {
            var (clock, ops, first) = PlayerOnly(aircraft: 2);
            var second = ops.Fleet[1];
            ops.ScheduleDeparture(first, Code("KGC"), new SimulationTime(1000));
            ops.ScheduleDeparture(second, Code("KGC"), new SimulationTime(1000));

            long firstTakeoffAt = -1;
            long secondTakeoffAt = -1;
            var sawHold = false;
            for (var steps = 0; steps < 64 && clock.Now.ElapsedSeconds < 1000 + 4 * 3600; steps++)
            {
                var next = ops.NextEventAt();
                if (next == null)
                    break;
                RunTo(clock, ops, next.Value.ElapsedSeconds);
                if (first.State == FleetState.TakingOff && firstTakeoffAt < 0)
                    firstTakeoffAt = clock.Now.ElapsedSeconds;
                if (second.State == FleetState.HoldingShort)
                    sawHold = true;
                if (second.State == FleetState.TakingOff && secondTakeoffAt < 0)
                {
                    secondTakeoffAt = clock.Now.ElapsedSeconds;
                    break;
                }
            }

            Assert.That(firstTakeoffAt, Is.GreaterThan(0), "first departed");
            Assert.That(secondTakeoffAt, Is.GreaterThan(firstTakeoffAt), "second waits its turn");
            Assert.That(secondTakeoffAt, Is.GreaterThanOrEqualTo(firstTakeoffAt + AirlineOperations.RunwaySeparationSeconds),
                "wake separation");
            Assert.That(sawHold, Is.True, "second holds short while the runway is busy");
        }

        [Test]
        public void NewGame_HasOverlappingArrivalsAndDeparturesNotASingleFileQueue()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = AirlineOperations.StartAtAdelaide(clock, new SeededRandomSource(2026), Player());
            Assert.That(ops.Fleet.Count(a => a.State == FleetState.Inbound), Is.GreaterThanOrEqualTo(11),
                "a peak bank of arrivals is already inbound");
            Assert.That(ops.Airlines.Select(a => a.Name),
                Does.Contain("Qantas").And.Contain("Jetstar").And.Contain("Virgin Australia")
                    .And.Contain("Malaysia Airlines").And.Contain("Emirates")
                    .And.Contain("Qatar Airways").And.Contain("Fiji Airways"));

            RunTo(clock, ops, 12 * 60);
            var live = ops.Fleet.Count(a => a.State is
                FleetState.Inbound or FleetState.HoldingForLanding or FleetState.Landing
                or FleetState.TaxiOut or FleetState.HoldingShort or FleetState.TakingOff
                or FleetState.TaxiIn);
            var kinds = ops.Fleet.Select(a => a.State).Distinct().Count();
            Assert.That(live, Is.GreaterThanOrEqualTo(6), "several aircraft are moving at once");
            Assert.That(kinds, Is.GreaterThanOrEqualTo(3), "they are not all in the same phase");
        }

        [Test]
        public void NewGame_InternationalOperatorsReachTheirAdelaideCities()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = AirlineOperations.StartAtAdelaide(clock, new SeededRandomSource(2026), Player());
            foreach (var (id, city) in new[]
                     {
                         ("MAS", "KUL"), ("UAE", "DXB"), ("QTR", "DOH"), ("FJI", "NAN"),
                         ("SIA", "SIN"), ("ANZ", "AKL")
                     })
            {
                var aircraft = ops.Fleet.Single(a => a.Airline.Id.Value == id);
                Assert.That(DestinationCatalogue.TryFind(city, out var destination), Is.True, city);
                Assert.That(ops.CanReach(aircraft, destination), Is.True, $"{id} must reach {city}");
                if (aircraft.State == FleetState.Inbound)
                    Assert.That(aircraft.CurrentDestination?.Code, Is.EqualTo(city));
            }

            Assert.That(AirlineOperations.AiFirstDepartureHour, Is.EqualTo(6));
            Assert.That(AirlineOperations.AiLastDepartureHour, Is.EqualTo(22));
        }

        [Test]
        public void NewGame_OpeningArrivalsAreAPeakBankNotAPileUp()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = AirlineOperations.StartAtAdelaide(clock, new SeededRandomSource(2026), Player());
            var arrivals = ops.Fleet
                .Where(a => a.State == FleetState.Inbound && a.StateEndsAt.HasValue)
                .Select(a => a.StateEndsAt.Value.ElapsedSeconds)
                .OrderBy(t => t)
                .ToArray();
            Assert.That(arrivals.Length, Is.GreaterThanOrEqualTo(11));
            Assert.That(arrivals[^1] - arrivals[0], Is.GreaterThanOrEqualTo(25 * 60),
                "the bank lasts a normal peak, not a ten-minute dump");
            for (var i = 1; i < arrivals.Length; i++)
            {
                Assert.That(arrivals[i] - arrivals[i - 1], Is.GreaterThanOrEqualTo(90),
                    "arrivals are spaced, not stacked on one minute");
            }
        }

        [Test]
        public void Ground_AllowsTwoPushbacksOnOneApronAndHoldsTheThird()
        {
            var (clock, ops, first) = PlayerOnly(aircraft: 3);
            var second = ops.Fleet[1];
            var third = ops.Fleet[2];
            var firstDestination = Code("KGC");
            var secondDestination = Code("PLO");
            var thirdDestination = Code("MGB");
            ops.ScheduleDeparture(first, firstDestination, new SimulationTime(600));
            ops.ScheduleDeparture(second, secondDestination, new SimulationTime(600));
            ops.ScheduleDeparture(third, thirdDestination, new SimulationTime(600));

            RunTo(clock, ops, 600);
            Assert.That(first.State, Is.EqualTo(FleetState.TaxiOut));
            Assert.That(second.State, Is.EqualTo(FleetState.TaxiOut),
                "two aircraft may taxi on the same apron at once");
            Assert.That(third.State, Is.EqualTo(FleetState.AtStand),
                "a third waits until one of the first two has cleared the stands");
            Assert.That(third.Scheduled.Value.Destination, Is.EqualTo(thirdDestination));
            var firstClear = first.StateStartedAt.Advance(
                AirlineOperations.TaxiClearSecondsFrom(first.DepartureStand, first.Type, first.AssignedRunway));
            var secondClear = second.StateStartedAt.Advance(
                AirlineOperations.TaxiClearSecondsFrom(second.DepartureStand, second.Type, second.AssignedRunway));
            var releaseAt = firstClear.CompareTo(secondClear) < 0 ? firstClear : secondClear;
            // A departure that had to wait moves on the ground-control grid (GroundTraffic).
            var pushAt = GroundTraffic.OnGrid(releaseAt) ? releaseAt : GroundTraffic.NextGrid(releaseAt);

            RunTo(clock, ops, releaseAt.ElapsedSeconds - 1);
            Assert.That(third.State, Is.EqualTo(FleetState.AtStand));
            RunTo(clock, ops, pushAt.ElapsedSeconds);
            Assert.That(third.State, Is.EqualTo(FleetState.TaxiOut));
            Assert.That(third.CurrentDestination, Is.EqualTo(thirdDestination));
            Assert.That(third.Scheduled, Is.Null);
        }

        [Test]
        public void Ground_PushbacksOnSeparateApronsDoNotDelayEachOther()
        {
            // Regional bays and terminal gates sit on separate aprons with their own taxi
            // routes and never share pavement (AirportTaxiNetwork), so a bay pushback has
            // no reason to hold up an unrelated gate pushback. This used to be gated by one
            // fleet-wide 60 s release regardless of which apron either aircraft was on.
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = new AirlineOperations(clock, new SeededRandomSource(7), DestinationCatalogue.Adelaide,
                AirlineOperations.AdelaideStands);
            var player = Player();
            ops.AddAirline(player);
            var bayPlane = ops.AddAircraft(player, "VH-PAA", AircraftType.Atr42, AirlineOperations.AdelaideRegionalBays[0]);
            var gatePlane = ops.AddAircraft(player, "VH-PAJ", AircraftType.Boeing78710, AirlineOperations.AdelaideTerminalGates[0]);
            ops.ScheduleDeparture(bayPlane, Code("KGC"), new SimulationTime(600));
            ops.ScheduleDeparture(gatePlane, Code("MEL"), new SimulationTime(600));

            RunTo(clock, ops, 600);
            Assert.That(bayPlane.State, Is.EqualTo(FleetState.TaxiOut));
            Assert.That(gatePlane.State, Is.EqualTo(FleetState.TaxiOut),
                "a different apron's pushback should not be held up by the bay's release gate");
        }

        [Test]
        public void Tower_LandsArrivalsBeforeReleasingDepartures()
        {
            var (clock, ops, arriving) = PlayerOnly(aircraft: 2);
            var departing = ops.Fleet[1];
            ops.ScheduleDeparture(arriving, Code("KGC"), new SimulationTime(600));
            RunTo(clock, ops, 600);
            var airborne = ops.AirborneSeconds(arriving, Code("KGC"));
            var backInCircuit = arriving.StateEndsAt.Value.ElapsedSeconds
                                + AirlineOperations.TakeoffRunwaySecondsFor(arriving.Type, arriving.AssignedRunway)
                                + airborne + AirlineOperations.DestinationTurnaroundSeconds + airborne;

            // Arrive at the hold before the inbound does, whichever 12/30 end they draw.
            var departTaxi = Math.Max(
                AirlineOperations.TaxiOutSecondsFrom(departing.Stand, departing.Type, RunwayDirection.Runway12),
                AirlineOperations.TaxiOutSecondsFrom(departing.Stand, departing.Type, RunwayDirection.Runway30));
            ops.ScheduleDeparture(departing, Code("PLO"),
                new SimulationTime(backInCircuit - departTaxi));
            RunTo(clock, ops, backInCircuit);

            Assert.That(arriving.State, Is.EqualTo(FleetState.Landing));
            Assert.That(departing.State, Is.EqualTo(FleetState.HoldingShort));
        }

        [Test]
        public void Timeline_IsIdenticalForAnyStepSizeOrSkipping()
        {
            var horizon = 36 * 3600L;

            string Run(Func<ManualSimulationClock, AirlineOperations, bool> step)
            {
                var clock = new ManualSimulationClock(new SimulationTime(0));
                var ops = AirlineOperations.StartAtAdelaide(clock, new SeededRandomSource(2026), Player());
                var mine = ops.FleetOf(ops.PlayerAirline).Single();
                ops.ScheduleDeparture(mine, Code("KGC"), new SimulationTime(1800));
                // The player's plane is left waiting for a stand once home: a stand choice
                // made "when noticed" would differ by step size, which is the player's
                // doing rather than the simulation's.
                while (clock.Now.ElapsedSeconds < horizon && step(clock, ops))
                {
                }

                clock.Set(new SimulationTime(horizon));
                ops.Update();
                return Snapshot(ops);
            }

            var bySecond = Run((c, o) => { c.Advance(1); o.Update(); return true; });
            var byOddSteps = Run((c, o) => { c.Advance(Math.Min(horizon - c.Now.ElapsedSeconds, c.Now.ElapsedSeconds % 7 == 0 ? 613 : 37)); o.Update(); return true; });
            var bySkipping = Run((c, o) =>
            {
                var next = o.NextEventAt();
                if (next == null || next.Value.ElapsedSeconds > horizon) return false;
                c.Set(next.Value);
                o.Update();
                return true;
            });

            Assert.That(byOddSteps, Is.EqualTo(bySecond));
            Assert.That(bySkipping, Is.EqualTo(bySecond));
        }

        [Test]
        public void AiAirline_FliesAllDayWithoutDoubleBookingStandsOrRunway()
        {
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = AirlineOperations.StartAtAdelaide(clock, new SeededRandomSource(11), Player());
            var emu = ops.Airlines.Single(a => a.Name == "Rex");

            Assert.That(ops.FleetOf(emu).Count(), Is.EqualTo(3));
            Assert.That(ops.FleetOf(emu).Where(a => a.State == FleetState.AtStand).All(a => a.Scheduled.HasValue),
                Is.True, "parked AI schedules itself while the opening arrival is already flying");

            for (var steps = 0; steps < 10000; steps++)
            {
                var next = ops.NextEventAt();
                if (next == null || next.Value.ElapsedSeconds > 48 * 3600)
                    break;
                clock.Set(next.Value);
                ops.Update();

                var mainOnRunway = ops.Fleet.Count(a =>
                    ops.IsOccupyingRunway(a) && RunwayWeather.IsMainRunway(a.AssignedRunway));
                var crossOnRunway = ops.Fleet.Count(a =>
                    ops.IsOccupyingRunway(a) && !RunwayWeather.IsMainRunway(a.AssignedRunway));
                Assert.That(mainOnRunway, Is.LessThanOrEqualTo(1));
                Assert.That(crossOnRunway, Is.LessThanOrEqualTo(1));
                var held = ops.Fleet.Where(a => a.State is FleetState.AtStand or FleetState.TaxiIn).Select(a => a.Stand).ToList();
                Assert.That(held.Distinct().Count(), Is.EqualTo(held.Count));
            }

            Assert.That(ops.FleetOf(emu).Sum(a => a.CompletedTrips), Is.GreaterThanOrEqualTo(10));
            Assert.That(ops.FleetOf(ops.PlayerAirline).Single().State, Is.EqualTo(FleetState.AtStand), "player plane untouched");
        }

        [Test]
        public void Airline_ValidatesLiveryAndSinglePlayer()
        {
            Assert.Throws<ArgumentException>(() => Airline.Player("X", "red"));
            Assert.That(Airline.Player("X", "#00ff80").LiveryRgb(), Is.EqualTo(((byte)0, (byte)255, (byte)128)));

            var (_, ops, _) = PlayerOnly();
            Assert.Throws<InvalidOperationException>(() => ops.AddAirline(Airline.Player("Another", "#000000")));
        }

        [Test]
        public void GoAroundSeed_DiffersForSameLengthRegistrationsAtTheSameTripCountAndHour()
        {
            // Every registration in the fleet is the same "VH-XXX" format/length, so a seed
            // built from Length instead of the full string gave identical sister ships
            // (e.g. Rex's three Saab 340s) the exact same go-around draw whenever their trip
            // counts and the wall-clock hour happened to line up - they all went around, or
            // none did, in lockstep forever. Hashing the whole registration fixes that.
            var rex1 = AirlineOperations.GoAroundSeed(3, "VH-ZRC", 3600);
            var rex2 = AirlineOperations.GoAroundSeed(3, "VH-ZRD", 3600);
            var rex3 = AirlineOperations.GoAroundSeed(3, "VH-ZRE", 3600);
            Assert.That(rex1, Is.Not.EqualTo(rex2));
            Assert.That(rex2, Is.Not.EqualTo(rex3));
            Assert.That(rex1, Is.Not.EqualTo(rex3));
        }

        [Test]
        public void AddMissingTerminalOperators_ChecksTheGivenTimeNotWhateverProcessedToStillIs()
        {
            // Update() calls this with the time it is advancing TO, before _processedTo
            // itself is advanced - the method must honour that explicit time rather than
            // silently falling back to the stale _processedTo, or Cathay Pacific's seasonal
            // arrival lags by a full Update() call every time a save crosses the boundary.
            var clock = new ManualSimulationClock(new SimulationTime(0));
            var ops = new AirlineOperations(clock, new SeededRandomSource(1),
                DestinationCatalogue.Adelaide, AirlineOperations.AdelaideStands);
            // AirlineClock.Default's epoch (14 Sep 2026) makes _processedTo (SimulationTime(0),
            // unchanged here) pre-season - the exact "stale time" this bug read from.
            Assert.That(ops.IsCathaySeason(ops.ProcessedTo), Is.False, "fixture must start pre-season");

            var inSeason = ops.Clock.AtLocal(new DateTime(2026, 12, 1, 12, 0, 0));
            Assert.That(ops.IsCathaySeason(inSeason), Is.True, "fixture's 'at' time must be in-season");

            var added = ops.AddMissingTerminalOperators(at: inSeason);

            Assert.That(added, Is.GreaterThan(0), "Cathay's aircraft should join when checked against the in-season time");
            Assert.That(ops.Fleet.Any(a => a.Airline.Id.Value == "CPA"), Is.True);
        }

        [Test]
        public void RenameAirline_ChangesTheNameEveryFleetReferenceAlreadyHolds()
        {
            var (_, ops, plane) = PlayerOnly();
            Assert.That(ops.RenameAirline("Kangaroo Island Air").Accepted, Is.True);
            Assert.That(ops.PlayerAirline.Name, Is.EqualTo("Kangaroo Island Air"));
            // Same Airline instance everywhere — no separate re-sync needed.
            Assert.That(plane.Airline.Name, Is.EqualTo("Kangaroo Island Air"));

            Assert.That(ops.RenameAirline("  ").Accepted, Is.False, "blank name refused");
            Assert.That(ops.RenameAirline(new string('X', 25)).Accepted, Is.False, "over the 24-character budget");
            Assert.That(ops.PlayerAirline.Name, Is.EqualTo("Kangaroo Island Air"), "refused renames leave the name alone");
        }

        [Test]
        public void SetLivery_ChangesTheColourAndRefusesAnInvalidHex()
        {
            var (_, ops, plane) = PlayerOnly();
            Assert.That(ops.SetLivery("#00AEEF").Accepted, Is.True);
            Assert.That(ops.PlayerAirline.LiveryHex, Is.EqualTo("#00AEEF"));
            Assert.That(plane.Airline.LiveryHex, Is.EqualTo("#00AEEF"));

            Assert.That(ops.SetLivery("blue").Accepted, Is.False);
            Assert.That(ops.PlayerAirline.LiveryHex, Is.EqualTo("#00AEEF"), "refused repaint leaves the livery alone");
        }

        [Test]
        public void SellAircraft_RefundsAFractionAndRemovesItFromTheFleet()
        {
            var (_, ops, _) = PlayerOnly();
            ops.RestoreCareerState(200_000, 100, nameof(OperatingTier.International), null, 0, 0,
                Array.Empty<string>(), Array.Empty<string>(), 40);
            Assert.That(ops.BuyAircraft(AircraftType.Atr42).Accepted, Is.True);
            var bought = ops.Fleet.Single(a => a.Type.Id == AircraftType.Atr42.Id);
            var fundsBefore = ops.CareerState.Funds;

            var result = ops.SellAircraft(bought);

            Assert.That(result.Accepted, Is.True);
            Assert.That(ops.Fleet.Contains(bought), Is.False);
            var expectedRefund = (long)Math.Round(AircraftAcquisition.Atr42.Price * AirlineOperations.ResaleFraction);
            Assert.That(ops.CareerState.Funds, Is.EqualTo(fundsBefore + expectedRefund));
            Assert.That(expectedRefund, Is.LessThan(AircraftAcquisition.Atr42.Price),
                "reselling is always a net loss versus buying");
        }

        [Test]
        public void SellAircraft_RefusesTheStarterAircraftWithNoPurchasePrice()
        {
            var (_, ops, plane) = PlayerOnly();
            // The starter Saab was never bought (AircraftAcquisition's own doc comment), so it
            // has no listed price to base a resale fraction on — refused rather than inventing one.
            Assert.That(AircraftAcquisition.TryFor(plane.Type, out _), Is.False);
            Assert.That(ops.SellAircraft(plane).Accepted, Is.False);
            Assert.That(ops.Fleet.Contains(plane), Is.True);
        }

        [Test]
        public void SellAircraft_RefusesAnAircraftThatIsNotParkedAtItsStand()
        {
            var (clock, ops, plane) = PlayerOnly();
            Assert.That(ops.ScheduleDeparture(plane, Code("KGC"), new SimulationTime(600)).Accepted, Is.True);
            RunTo(clock, ops, 600);
            Assert.That(plane.State, Is.Not.EqualTo(FleetState.AtStand));

            Assert.That(ops.SellAircraft(plane).Accepted, Is.False);
            Assert.That(ops.Fleet.Contains(plane), Is.True, "refused sale leaves the fleet untouched");
        }

        [Test]
        public void SellAircraft_RefusesAnotherAirlinesAircraft()
        {
            var (_, ops, _) = PlayerOnly();
            var rival = Airline.Rex();
            ops.AddAirline(rival);
            var rivalPlane = ops.AddAircraft(rival, "VH-ZRC", AircraftType.Saab340,
                AirlineOperations.AdelaideRegionalBays[1]);

            Assert.That(ops.SellAircraft(rivalPlane).Accepted, Is.False);
        }

        private static string Snapshot(AirlineOperations ops)
        {
            var text = new StringBuilder();
            foreach (var a in ops.Fleet)
            {
                text.Append(a.Registration).Append(' ').Append(a.State)
                    .Append(" since ").Append(a.StateStartedAt.ElapsedSeconds)
                    .Append(" stand ").Append(a.Stand.Value)
                    .Append(" to ").Append(a.CurrentDestination?.Code)
                    .Append(" next ").Append(a.Scheduled?.Destination.Code).Append('@').Append(a.Scheduled?.DepartAt.ElapsedSeconds)
                    .Append(" trips ").Append(a.CompletedTrips).AppendLine();
            }

            return text.ToString();
        }
    }
}

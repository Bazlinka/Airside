using System;
using Airside.Domain;
using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class LiveTrafficTests
    {
        // Shape of a real adsb.lol v2 response (21 Sep 2026), trimmed: an airborne Dash 8,
        // a taxiing 777 ("ground"), a trainer, one with no position, and a bad value.
        private const string Sample = @"{""ac"":[
            {""hex"":""7c6b2d"",""type"":""adsb_icao"",""flight"":""QLK586D "",""r"":""VH-QOB"",""t"":""DH8D"",
             ""alt_baro"":3200,""alt_geom"":3350,""gs"":180.5,""track"":48.2,""baro_rate"":-704,
             ""lat"":-35.05,""lon"":138.40,""seen_pos"":1.2,""seen"":0.4},
            {""hex"":""06a0af"",""flight"":""QTR55Y  "",""r"":""A7-BAC"",""t"":""B77W"",""alt_baro"":""ground"",
             ""gs"":11.8,""track"":230.0,""lat"":-34.943576,""lon"":138.535595},
            {""hex"":""7c7a1f"",""flight"":""YTN"",""r"":""VH-YTN"",""t"":""DA40"",""alt_baro"":1550,""gs"":99.5,
             ""track"":10.0,""lat"":-34.664845,""lon"":138.469334},
            {""hex"":""7c0000"",""flight"":""NOPOS"",""t"":""A320"",""alt_baro"":9000},
            {""hex"":""7c0001"",""flight"":""ODDA"",""t"":""A320"",""alt_baro"":9000,""gs"":""fast"",
             ""lat"":-34.5,""lon"":138.9,""track"":200}
        ],""msg"":""No error"",""now"":1789966000000,""total"":5,""ctime"":1789966000000,""ptime"":0}";

        [Test]
        public void Parse_ReadsTheFeedAndSkipsWhatItCannotPlace()
        {
            var aircraft = LiveTraffic.Parse(Sample);
            Assert.That(aircraft.Count, Is.EqualTo(4), "the aircraft with no position is skipped");

            var dash8 = aircraft[0];
            Assert.That(dash8.Callsign, Is.EqualTo("QLK586D"));
            Assert.That(dash8.Registration, Is.EqualTo("VH-QOB"));
            Assert.That(dash8.AltitudeFeet, Is.EqualTo(3200));
            Assert.That(dash8.VerticalRateFpm, Is.EqualTo(-704));
            Assert.That(dash8.PositionAgeSeconds, Is.EqualTo(1.2).Within(1e-9));

            Assert.That(aircraft[1].OnGround, Is.True);
            Assert.That(aircraft[3].Callsign, Is.EqualTo("ODDA"), "\\u escapes decode");
            Assert.That(aircraft[3].GroundSpeedKnots, Is.EqualTo(0), "a bad field is ignored, not fatal");
        }

        [Test]
        public void Parse_NeverThrowsOnJunk()
        {
            Assert.That(LiveTraffic.Parse(null), Is.Empty);
            Assert.That(LiveTraffic.Parse(""), Is.Empty);
            Assert.That(LiveTraffic.Parse("<html>502 Bad Gateway</html>"), Is.Empty);
            Assert.That(LiveTraffic.Parse(@"{""ac"":[{""hex"":""x"",""lat"":"), Is.Empty);
            Assert.That(LiveTraffic.Parse(@"{""msg"":""rate limited""}"), Is.Empty);
        }

        [Test]
        public void OnlyAirborneAirlinersAwayFromTheFieldAreDrawn()
        {
            var aircraft = LiveTraffic.Parse(Sample);
            Assert.That(LiveTraffic.TryPose(aircraft[0], 0, out _), Is.True, "airborne Dash 8");
            Assert.That(LiveTraffic.TryPose(aircraft[1], 0, out _), Is.False, "taxiing 777 stays off the field");
            Assert.That(LiveTraffic.TryPose(aircraft[2], 0, out _), Is.False, "no honest model for a DA40");
            Assert.That(LiveTraffic.TryPose(aircraft[0], LiveTraffic.StaleSeconds + 1, out _), Is.False, "stale");

            var lowFinal = new LiveAircraft("a", "QFA1", "VH-X", "B738", -34.955, 138.515, 300, false, 140, 50, -700, 0);
            Assert.That(LiveTraffic.TryPose(lowFinal, 0, out _), Is.False, "short final over the field is the sim's runway");
        }

        [Test]
        public void ModelFor_MapsRealTypesToTheCatalogue()
        {
            Assert.That(LiveTraffic.ModelFor("DH8D"), Is.EqualTo(AircraftType.Dash8Q400));
            Assert.That(LiveTraffic.ModelFor("sf34"), Is.EqualTo(AircraftType.Saab340));
            Assert.That(LiveTraffic.ModelFor("B38M"), Is.EqualTo(AircraftType.Boeing7378));
            Assert.That(LiveTraffic.ModelFor("A320"), Is.EqualTo(AircraftType.AirbusA321Neo));
            Assert.That(LiveTraffic.ModelFor("B789"), Is.EqualTo(AircraftType.Boeing78710));
            Assert.That(LiveTraffic.ModelFor("C172"), Is.Null);
            Assert.That(LiveTraffic.ModelFor(null), Is.Null);
        }

        [Test]
        public void YpadFrame_MatchesTheLayoutGenerator()
        {
            // Reference values from scripts/generate-ypad-layout.py local().
            AssertWorld(-34.9585244, 138.5172171, -1539.52, 0.0, "05 threshold");
            AssertWorld(-34.9406962, 138.5431392, 1539.52, 0.0, "23 threshold");
            AssertWorld(-34.942337, 138.524033, 84.19, 976.78, "12/30 strip");
            AssertWorld(-34.943576, 138.535595, 806.87, 196.11, "T1 apron");

            Assert.That(YpadFrame.UnityYawFromTrue(50.2), Is.EqualTo(90f).Within(0.5f), "runway 05 heading is +x");
        }

        [Test]
        public void NearFieldIsOneToOne_FarFieldStaysInsideTheDrawRadius()
        {
            LiveTraffic.Display(3000, -1500, 2000, out var x, out var y, out var z);
            Assert.That(x, Is.EqualTo(3000).Within(1e-6));
            Assert.That(z, Is.EqualTo(-1500).Within(1e-6));
            Assert.That(y, Is.EqualTo(2000 / 3.28084).Within(0.01));

            LiveTraffic.Display(100_000, 0, 35_000, out var fx, out var fy, out _);
            Assert.That(fx, Is.LessThanOrEqualTo(LiveTraffic.DrawRadiusMetres + 0.01));
            Assert.That(fx, Is.GreaterThan(LiveTraffic.NearFieldMetres));
            Assert.That(fy, Is.LessThan(35_000 / 3.28084 * 0.5), "cruise height is squeezed with distance");
        }

        [Test]
        public void DeadReckoning_MovesAlongTheTrack()
        {
            // 12 km south-west, tracking 050 at 180 kt: after 10 s it is ~926 m further along +x.
            var inbound = new LiveAircraft("b", "RXA1", "VH-R", "SF34", -35.02, 138.43, 3000, false, 180, 50.2, 0, 0);
            Assert.That(LiveTraffic.TryPose(inbound, 0, out var now), Is.True);
            Assert.That(LiveTraffic.TryPose(inbound, 10, out var later), Is.True);
            Assert.That(later.X, Is.GreaterThan(now.X));
            Assert.That(Math.Abs(later.YawDegrees - 90f), Is.LessThan(15f), "faces up the 05 axis");
            Assert.That(now.PitchDegrees, Is.EqualTo(3f).Within(0.5f), "level flight shows a slight nose-up attitude");
        }

        [Test]
        public void Credit_NamesAdsbLolOnlyWhileLiveAircraftAreShown()
        {
            Assert.That(MapAttribution.FieldCredit(true, true), Does.Not.Contain("adsb.lol"));
            var live = MapAttribution.FieldCredit(true, true, usesLiveTraffic: true);
            Assert.That(live, Does.Contain("OpenStreetMap"));
            Assert.That(live, Does.Contain("adsb.lol (ODbL)"));
        }

        [Test]
        public void SkyTraffic_UsesTheSameSideOfTheRunwayAsTheField()
        {
            // Melbourne is south-east: right of runway 05, so -z in the field's frame.
            SkyTraffic.ToRunwayFrame(1000, -800, out _, out var across);
            YpadFrame.FromEastNorth(1000, -800, out _, out var fieldZ);
            Assert.That(Math.Sign(across), Is.EqualTo(Math.Sign(fieldZ)), "authored overflights were mirrored");
            Assert.That(fieldZ, Is.LessThan(0));
        }

        private static void AssertWorld(double lat, double lon, double x, double z, string name)
        {
            YpadFrame.ToWorld(lat, lon, out var wx, out var wz);
            Assert.That(wx, Is.EqualTo(x).Within(1.0), name + " x");
            Assert.That(wz, Is.EqualTo(z).Within(1.0), name + " z");
        }
    
        [Test]
        public void FieldPose_DrawsTaxiingAirlinersOnThePavementOneToOne()
        {
            var aircraft = LiveTraffic.Parse(Sample);
            Assert.That(LiveTraffic.TryFieldPose(aircraft[1], 0, out var taxi, out var onGround), Is.True, "taxiing 777");
            Assert.That(onGround, Is.True);
            Assert.That(taxi.Y, Is.EqualTo(0).Within(1e-6));
            Assert.That(taxi.X, Is.EqualTo(806.87).Within(15.0), "on the T1 apron, not squeezed");
            Assert.That(taxi.Z, Is.EqualTo(196.11).Within(15.0));

            Assert.That(LiveTraffic.TryFieldPose(aircraft[2], 0, out _, out _), Is.False, "no model for a DA40");
            Assert.That(LiveTraffic.TryFieldPose(aircraft[0], 0, out _, out _), Is.False, "the Dash 8 is up at 3,200 ft");

            var flare = new LiveAircraft("c", "QFA7", "VH-Y", "B738", -34.9575, 138.519, 120, false, 135, 50.2, -650, 0);
            Assert.That(LiveTraffic.TryFieldPose(flare, 0, out var low, out var grounded), Is.True, "landing over the threshold");
            Assert.That(grounded, Is.False);
            Assert.That(low.Y, Is.EqualTo((120 - LiveTraffic.FieldElevationFeet) / 3.28084).Within(0.5));
        }

        [Test]
        public void GroundClearance_StandsAsideForTheGameAndItsRunways()
        {
            var ship = new[] { new GroundObstacle(1000, 400, 12) };
            Assert.That(LiveGroundClearance.Clashes(1015, 400, 18, ship, false, false), Is.True, "wingtips would touch");
            Assert.That(LiveGroundClearance.Clashes(1100, 400, 18, ship, false, false), Is.False);

            Assert.That(LiveGroundClearance.Clashes(-600, 10, 18, null, mainStripInUse: true, crossStripInUse: false), Is.True,
                "on 05/23 while the game uses it");
            Assert.That(LiveGroundClearance.Clashes(-600, 10, 18, null, mainStripInUse: false, crossStripInUse: false), Is.False);
            Assert.That(LiveGroundClearance.Clashes(-600, 300, 18, null, mainStripInUse: true, crossStripInUse: false), Is.False,
                "an apron well off the runway is fine");

            AdelaideCrossRoutes.LocalToWorld(200f, 5f, out var cx, out var cz);
            Assert.That(LiveGroundClearance.Clashes(cx, cz, 18, null, false, crossStripInUse: true), Is.True, "on 12/30");
            AdelaideCrossRoutes.WorldToLocal(cx, cz, out var along, out var across);
            Assert.That(along, Is.EqualTo(200f).Within(0.01f));
            Assert.That(across, Is.EqualTo(5f).Within(0.01f));
        }

        [Test]
        public void Feed_CoversTheRegionForTheMap_ButTheSkyStaysLocal()
        {
            Assert.That(LiveTraffic.RequestUrl(), Does.EndWith("/250"));
            var far = new LiveAircraft("d", "VOZ9", "VH-Z", "B738", -33.0, 136.0, 37000, false, 450, 90, 0, 0);
            Assert.That(LiveTraffic.TryPose(far, 0, out _), Is.False, "well past 60 NM: map only");

            LiveTraffic.PositionAfter(far, 60, out var lat, out var lon);
            Assert.That(lon, Is.GreaterThan(-0.0 + 136.0), "flies east along its track");
            Assert.That(lat, Is.EqualTo(-33.0).Within(1e-3));
        }
}
}

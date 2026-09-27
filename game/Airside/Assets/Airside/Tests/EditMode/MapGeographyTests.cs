using System;
using System.Linq;
using Airside.Domain;
using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    /// <summary>ADR 0140 — countries, the wider map, real coastlines, towns and runways.</summary>
    public sealed class MapGeographyTests
    {
        private const float Width = 1200f, Height = 700f;

        [Test]
        public void EveryDestination_KnowsItsCountry()
        {
            foreach (var d in DestinationCatalogue.All)
            {
                Assert.That(d.Country, Has.Length.EqualTo(2), d.Code);
                Assert.That(d.CountryName, Is.Not.EqualTo(d.Country), $"{d.Code} needs a country name");
                if (d.IsAustralian)
                    Assert.That(d.State, Is.Not.Empty, d.Code);
                else
                    Assert.That(d.State, Is.Empty, $"{d.Code}: overseas airports keep the country out of State");
                Assert.That(d.Region, Is.EqualTo(d.IsAustralian ? d.State : d.CountryName));
            }

            Assert.That(DestinationCatalogue.All.Select(d => d.Code).Distinct().Count(), Is.EqualTo(DestinationCatalogue.All.Count()));
        }

        [Test]
        public void EveryAirport_SitsOnLand()
        {
            // Kingscote, Whyalla and Cairns used to fall in the sea on the hand-drawn coast.
            foreach (var code in new[] { "KGC", "WYA", "CNS", "PLO", "HBA", "ADL" })
            {
                Assert.That(DestinationCatalogue.TryFind(code, out var d), Is.True);
                Assert.That(MapGeography.OnLand(d.Longitude, d.Latitude), Is.True, code);
            }

            // Every Australian and near-Pacific airport is inside the detailed land outline. Overseas,
            // the land test uses the coarser region outline, so an airport on reclaimed land (Osaka
            // Kansai, Hong Kong) or just inland (Shanghai Pudong) must at least be close to the fine coast
            // drawn around it.
            foreach (var d in DestinationCatalogue.All)
            {
                var km = MapGeography.KmToCoast(d.Longitude, d.Latitude);
                var inDetail = d.Longitude >= MapGeography.DetailWest && d.Longitude <= MapGeography.DetailEast
                               && d.Latitude >= MapGeography.DetailSouth && d.Latitude <= MapGeography.DetailNorth;
                if (inDetail)
                    Assert.That(MapGeography.OnLand(d.Longitude, d.Latitude) || km < 3.0, Is.True, $"{d.Code}: {km:F1} km");
                else
                    Assert.That(MapGeography.OnLand(d.Longitude, d.Latitude) || km < 15.0, Is.True, $"{d.Code}: {km:F1} km");
            }
        }

        [Test]
        public void TheNewAirports_HaveTheirBands()
        {
            foreach (var code in new[] { "NAN", "NOU", "POM", "DPS" })
                Assert.That(RouteAccess.BandOf(code), Is.EqualTo(RouteBand.Pacific), code);
            foreach (var code in new[] { "NRT", "KIX", "ICN", "PVG", "BKK", "SGN", "MNL", "CGK", "HNL", "LAX" })
                Assert.That(RouteAccess.BandOf(code), Is.EqualTo(RouteBand.LongHaul), code);
            Assert.That(RouteAccess.IsInternational("NOU"), Is.True);

            Assert.That(DestinationCatalogue.TryFind("LAX", out var lax), Is.True);
            var km = DestinationCatalogue.Adelaide.DistanceKmTo(lax);
            Assert.That(km, Is.InRange(13_000, 14_000));
            Assert.That(AircraftType.AirbusA350900.CanReach(km), Is.True);
            Assert.That(AircraftType.Boeing78710.CanReach(km), Is.False);

            Assert.That(DestinationCatalogue.TryFind("DPS", out var bali), Is.True);
            Assert.That(RouteAccess.Allows(AircraftType.AirbusA321Neo, bali), Is.True, "the A321neo can fly to Bali");
            Assert.That(RouteAccess.Allows(AircraftType.AirbusA321Neo, lax), Is.False);
        }

        [Test]
        public void TheWorldView_ShowsEveryDestination()
        {
            var lens = new AustraliaMapLens();
            lens.ShowWorld(Width, Height);
            foreach (var d in DestinationCatalogue.All)
            {
                lens.Project(0f, 0f, Width, Height, d.Longitude, d.Latitude, out var x, out var y);
                Assert.That(x, Is.InRange(0f, Width), d.Code);
                Assert.That(y, Is.InRange(0f, Height), d.Code);
            }

            // Honolulu and Los Angeles are east of Australia, not wrapped round to the far west.
            lens.Project(0f, 0f, Width, Height, 138.5, -34.9, out var adlX, out _);
            Assert.That(DestinationCatalogue.TryFind("LAX", out var lax), Is.True);
            lens.Project(0f, 0f, Width, Height, lax.Longitude, lax.Latitude, out var laxX, out _);
            Assert.That(laxX, Is.GreaterThan(adlX));
        }

        [Test]
        public void ARouteAcrossThePacific_NeverJumpsAcrossTheMap()
        {
            var lens = new AustraliaMapLens();
            lens.ShowWorld(Width, Height);
            Assert.That(DestinationCatalogue.TryFind("LAX", out var lax), Is.True);
            var adl = DestinationCatalogue.Adelaide;
            lens.Project(0f, 0f, Width, Height, adl.Longitude, adl.Latitude, out var px, out var py);
            for (var i = 1; i <= 48; i++)
            {
                RouteMap.GreatCirclePoint(adl.Latitude, adl.Longitude, lax.Latitude, lax.Longitude, i / 48.0,
                    out var lat, out var lon);
                lens.Project(0f, 0f, Width, Height, lon, lat, out var x, out var y);
                Assert.That(Math.Abs(x - px), Is.LessThan(Width * 0.1f), $"step {i}");
                px = x;
            }
        }

        [Test]
        public void DetailFollowsZoom()
        {
            var lens = new AustraliaMapLens();
            lens.SetZoom(AustraliaMapLens.MinZoom);
            Assert.That(lens.Detail, Is.EqualTo(MapDetail.World));
            Assert.That(lens.TownRank, Is.EqualTo(-1));
            lens.SetZoom(1f);
            Assert.That(lens.Detail, Is.EqualTo(MapDetail.Region));
            lens.SetZoom(4f);
            Assert.That(lens.Detail, Is.EqualTo(MapDetail.Detail));
            Assert.That(lens.TownRank, Is.EqualTo(1));
            lens.SetZoom(20f);
            Assert.That(lens.TownRank, Is.EqualTo(3));
        }

        [Test]
        public void TheCoastStaysCheapToDraw_AtEveryZoom()
        {
            foreach (var zoom in new[] { AustraliaMapLens.MinZoom, 1f, 2f, 6f, 30f })
            {
                var lens = new AustraliaMapLens();
                lens.SetZoom(zoom);
                if (zoom > 1.5f)
                    lens.CenterOn(Width, Height, 138.5, -34.9);
                var into = new HudDrawList();
                RouteMapWorkspacePainter.PaintGeography(into, new HudBox(0f, 0f, Width, Height), lens);
                var lines = into.Commands.Count(c => c.Kind == HudDrawKind.Line);
                Assert.That(lines, Is.InRange(40, 5_000), $"zoom {zoom}");
            }
        }

        [Test]
        public void ZoomedIn_TownsAppear()
        {
            var lens = new AustraliaMapLens();
            lens.SetZoom(8f);
            lens.CenterOn(Width, Height, 138.6, -34.9);
            var into = new HudDrawList();
            RouteMapWorkspacePainter.PaintGeography(into, new HudBox(0f, 0f, Width, Height), lens);
            var labels = into.Commands.Where(c => c.Kind == HudDrawKind.Text).Select(c => c.Text).ToList();
            Assert.That(labels, Does.Contain("Gawler").And.Contain("Murray Bridge"));
            Assert.That(labels, Does.Not.Contain("Adelaide"), "the airport already names Adelaide");
        }

        [Test]
        public void EveryServedAirport_HasRunwaysWhereItIs()
        {
            foreach (var d in DestinationCatalogue.All)
            {
                var runways = MapGeography.RunwaysAt(d.Code).ToList();
                Assert.That(runways, Is.Not.Empty, d.Code);
                foreach (var r in runways)
                {
                    Assert.That(r.LengthM, Is.InRange(900, 5_500), $"{d.Code} {r.End1}");
                    var mid = new Destination("MID", string.Empty, string.Empty, (r.Lat1 + r.Lat2) / 2, (r.Lon1 + r.Lon2) / 2);
                    Assert.That(mid.DistanceKmTo(d), Is.LessThan(6.0), $"{d.Code} {r.End1}/{r.End2}");
                }
            }

            Assert.That(MapGeography.ElevationFt("CBR"), Is.EqualTo(1886));
        }

        [Test]
        public void ZoomedRightIn_TheAirportShowsItsRunways()
        {
            var lens = new AustraliaMapLens();
            lens.SetZoom(80f);
            var adl = DestinationCatalogue.Adelaide;
            lens.CenterOn(Width, Height, adl.Longitude, adl.Latitude);
            var into = new HudDrawList();
            RouteMapWorkspacePainter.PaintAirports(into, new HudBox(0f, 0f, Width, Height), lens,
                Array.Empty<PlannerDestination>(), adl);
            var text = into.Commands.Where(c => c.Kind == HudDrawKind.Text).Select(c => c.Text).ToList();
            Assert.That(text, Does.Contain("05").And.Contain("23").And.Contain("12").And.Contain("30"));
            Assert.That(text.Any(t => t.Contains("ADL") && t.Contains("ft")), Is.True);

            lens.SetZoom(10f);
            var far = new HudDrawList();
            RouteMapWorkspacePainter.PaintAirports(far, new HudBox(0f, 0f, Width, Height), lens,
                Array.Empty<PlannerDestination>(), adl);
            Assert.That(far.Commands, Is.Empty, "no runway detail until zoomed right in");
        }
    }
}

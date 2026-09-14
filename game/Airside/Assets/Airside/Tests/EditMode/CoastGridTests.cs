using System.Collections.Generic;
using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class CoastGridTests
    {
        private static CoastGrid Grid() =>
            new(AdelaideCoast.SeaPolygon, AdelaideCoast.Coastline, AirsideBareField.GroundLengthMetres * 0.5f,
                AirsideBareField.GroundWidthMetres * 0.5f);

        [Test]
        public void Coast_IsWestOfTheAirfieldAndClearOfIt()
        {
            Assert.That(AdelaideCoast.NearestCoastMetres, Is.InRange(1800f, 4000f));
            for (var i = 0; i + 1 < AdelaideCoast.Coastline.Length; i += 2)
            {
                var x = AdelaideCoast.Coastline[i];
                var z = AdelaideCoast.Coastline[i + 1];
                Assert.That(System.Math.Abs(x) >= 1950f || System.Math.Abs(z) >= 1400f, $"coast point {x},{z} inside the airfield");
            }
        }

        [Test]
        public void Grid_KnowsSeaFromLand()
        {
            var grid = Grid();
            var sea = 0;
            var land = 0;
            for (var zi = 0; zi < grid.CountZ; zi++)
            for (var xi = 0; xi < grid.CountX; xi++)
            {
                if (grid.IsSea(xi, zi)) sea++; else land++;
                if (System.Math.Abs(grid.X(xi)) < 1950f && System.Math.Abs(grid.Z(zi)) < 1400f)
                    Assert.That(grid.IsSea(xi, zi), Is.False, "the airfield is not in the sea");
            }

            Assert.That(sea, Is.GreaterThan(0));
            Assert.That(land, Is.GreaterThan(sea / 4));
        }

        [Test]
        public void Grid_HasLinesExactlyOnTheAirfieldEdge()
        {
            var axis = CoastGrid.Axis(1950f);
            Assert.That(System.Array.IndexOf(axis, 1950f), Is.GreaterThanOrEqualTo(0));
            Assert.That(System.Array.IndexOf(axis, -1950f), Is.GreaterThanOrEqualTo(0));
            Assert.That(System.Array.IndexOf(axis, 1950f - CoastGrid.OverlapMetres), Is.GreaterThanOrEqualTo(0));
            for (var i = 1; i < axis.Length; i++)
                Assert.That(axis[i] - axis[i - 1], Is.GreaterThan(0.4f));
            Assert.That(axis[0], Is.EqualTo(-CoastGrid.ExtentMetres));
        }

        [Test]
        public void Distance_MatchesBruteForce()
        {
            var grid = Grid();
            for (var zi = 0; zi < grid.CountZ; zi += 17)
            for (var xi = 0; xi < grid.CountX; xi += 13)
            {
                var brute = System.Math.Min(CoastGrid.MaxDistanceMetres,
                    CoastGrid.DistanceToPolyline(AdelaideCoast.Coastline, grid.X(xi), grid.Z(zi)));
                Assert.That(grid.CoastDistance(xi, zi), Is.EqualTo(brute).Within(0.5f));
            }
        }

        [Test]
        public void RowCrossings_CountsASquareTwice()
        {
            var square = new[] { 0f, 0f, 10f, 0f, 10f, 10f, 0f, 10f };
            var crossings = new List<float>();
            CoastGrid.RowCrossings(square, 5f, crossings);
            Assert.That(crossings, Is.EqualTo(new[] { 0f, 10f }));
        }

        [Test]
        public void Credit_NamesOpenStreetMap()
        {
            Assert.That(MapAttribution.FieldCredit(true, true), Does.Contain("OpenStreetMap"));
        }
    }
}

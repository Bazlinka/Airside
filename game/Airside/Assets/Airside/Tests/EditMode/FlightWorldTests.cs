using System;
using System.IO;
using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class FlightWorldTests
    {
        [TestCase(-1,-1)] [TestCase(0,0)] [TestCase(15999,0)] [TestCase(16000,1)]
        public void TileUsesFloorAcrossBothSides(double metres,int expected) => Assert.That(FlightWorldGrid.Tile(metres),Is.EqualTo(expected));
        [Test] public void RunwayTerrainCellsStayFlatAcrossTheCoarseMesh()
        {
            var stride=FlightWorldGrid.TileMetres/FlightWorldGrid.Cells;
            foreach(var runway in RegionalRunways.All)
            for(var i=0;i<=20;i++)
            {
                var x=runway.Ax+(runway.Bx-runway.Ax)*i/20;
                var z=runway.Az+(runway.Bz-runway.Az)*i/20;
                var cx=Math.Floor(x/stride)*stride;var cz=Math.Floor(z/stride)*stride;
                for(var dz=0;dz<=1;dz++) for(var dx=0;dx<=1;dx++)
                    Assert.That(RegionalRunways.Ground(cx+dx*stride,cz+dz*stride,900),
                        Is.EqualTo(runway.Elevation).Within(.001),runway.Code);
            }
        }

        [Test] public void ResidentWindowHasFixedUpperBoundAfterLongFlights()
        {
            foreach(var centre in new[]{-100,0,100})
            {
                var count=0;
                for(var x=centre-10;x<=centre+10;x++) for(var z=centre-10;z<=centre+10;z++)
                    if(FlightWorldGrid.Resident(x,z,centre,centre))count++;
                Assert.That(count,Is.EqualTo(FlightWorldGrid.MaxTiles));
            }
        }
        [Test] public void OriginKeepsAircraftWithinFourKilometresWithoutLosingGeographicPosition()
        {
            foreach(var global in new[]{-1300000.123,0,1450321.987})
            {
                var origin=FlightWorldGrid.Origin(global);
                var rendered=(float)(global-origin);
                Assert.That(Math.Abs(rendered),Is.LessThanOrEqualTo(4000));
                Assert.That(origin+rendered,Is.EqualTo(global).Within(.001));
            }
        }
        [Test] public void AllSouthAustralianDestinationsAreCovered()
        {
            foreach(var d in Airside.Domain.DestinationCatalogue.Australia)
                if(d.Region=="SA")Assert.That(FlightWorldGrid.Covered(d.Latitude,d.Longitude),Is.True,d.Name);
        }
        [Test] public void MappedRegionalLandingsEndOnTheRunwayAtAirportElevation()
        {
            Assert.That(RegionalRunways.All.Length,Is.EqualTo(7));
            foreach(var runway in RegionalRunways.All)
            {
                RegionalFlightPath.Landing(runway,0,0,0,out var x,out var y,out var z);
                var dx=runway.Bx-runway.Ax;var dz=runway.Bz-runway.Az;
                var t=((x-runway.Ax)*dx+(z-runway.Az)*dz)/(dx*dx+dz*dz);
                Assert.That(t,Is.InRange(0d,1d));
                Assert.That(y,Is.EqualTo(runway.Elevation+RegionalFlightPath.AirsideFlightPathDatum).Within(.001));
                Assert.That(RegionalRunways.Ground(x,z,900),Is.EqualTo(runway.Elevation).Within(.001));
                var lastHeight=double.MaxValue;
                for(var seconds=180;seconds>=0;seconds--)
                {
                    RegionalFlightPath.Landing(runway,0,0,seconds,out _,out y,out _);
                    Assert.That(y,Is.LessThanOrEqualTo(lastHeight));lastHeight=y;
                }
            }
        }
        [Test] public void ShippedElevationCoversNorthernAndSouthernSouthAustralia()
        {
            var relative=Path.Combine("game","Airside","Assets","Airside","Art",FlightWorldHeights.ArtPath);
            FlightWorldHeights grid=null;
            for(var dir=new DirectoryInfo(TestContext.CurrentContext.TestDirectory);dir!=null;dir=dir.Parent)
            {
                var path=Path.Combine(dir.FullName,relative);
                if(File.Exists(path)){grid=FlightWorldHeights.Parse(File.ReadAllBytes(path));break;}
            }
            Assert.That(grid,Is.Not.Null);
            foreach(var point in new[]{(-26d,129d),(-38d,140d),(-30d,135d),(-35d,138d)})
            {
                Assert.That(grid.TryHeight(point.Item1,point.Item2,out var h),Is.True);
                Assert.That(h,Is.InRange(0d,2000d));
            }
        }
        [Test] public void RegionalDepartureStartsAtThePreviousRolloutEndAndClimbs()
        {
            foreach(var runway in RegionalRunways.All)
            {
                RegionalFlightPath.Landing(runway,0,0,0,out var px,out var py,out var pz);
                RegionalFlightPath.Departure(runway,0,1500,out var x,out var y,out var z);
                Assert.That(x,Is.EqualTo(px));Assert.That(z,Is.EqualTo(pz));Assert.That(y,Is.EqualTo(py));
                RegionalFlightPath.Departure(runway,40,1500,out _,out y,out _);Assert.That(y,Is.EqualTo(py));
                RegionalFlightPath.Departure(runway,120,1500,out _,out y,out _);Assert.That(y,Is.EqualTo(1500));
            }
        }
        private static byte[] Grid()
        {
            using var stream=new MemoryStream();using var writer=new BinaryWriter(stream);
            writer.Write(0x47544153);writer.Write(1);writer.Write(2);writer.Write(2);
            writer.Write(130d);writer.Write(-35d);writer.Write(1d);
            foreach(short h in new short[]{0,100,200,300})writer.Write(h);
            return stream.ToArray();
        }
        [Test] public void HeightsInterpolateAndRefuseOutOfBounds()
        {
            var grid=FlightWorldHeights.Parse(Grid());Assert.That(grid,Is.Not.Null);
            Assert.That(grid.TryHeight(-34.5,130.5,out var h),Is.True);Assert.That(h,Is.EqualTo(150));
            Assert.That(grid.TryHeight(-30,140,out _),Is.False);
            Assert.That(grid.TryHeight(double.NaN,130,out _),Is.False);
            Assert.That(grid.TryHeight(-34,131,out h),Is.True);Assert.That(h,Is.EqualTo(300));
        }
        [Test] public void CorruptOrTruncatedHeightsFailSoft()
        {
            Assert.That(FlightWorldHeights.Parse(new byte[40]),Is.Null);
            var bytes=Grid();Array.Resize(ref bytes,bytes.Length-1);
            Assert.That(FlightWorldHeights.Parse(bytes),Is.Null);
            bytes=Grid();Array.Copy(BitConverter.GetBytes(double.NaN),0,bytes,32,8);
            Assert.That(FlightWorldHeights.Parse(bytes),Is.Null);
        }
    }
}

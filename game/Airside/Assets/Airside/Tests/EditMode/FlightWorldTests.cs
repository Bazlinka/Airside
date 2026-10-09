using System;
using System.IO;
using Airside.Domain;
using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class FlightWorldTests
    {
        [TestCase(FleetState.Outbound, 600, AircraftPhase.Departed)]
        [TestCase(FleetState.Inbound, 600, AircraftPhase.Departed)]
        [TestCase(FleetState.Inbound, 0, AircraftPhase.Takeoff)]
        [TestCase(FleetState.Inbound, 39.9, AircraftPhase.Takeoff)]
        [TestCase(FleetState.Inbound, 40, AircraftPhase.Departed)]
        [TestCase(FleetState.Inbound, 120, AircraftPhase.Departed)]
        [TestCase(FleetState.Outbound, 819.9, AircraftPhase.Departed)]
        [TestCase(FleetState.Outbound, 820, AircraftPhase.Approach)]
        [TestCase(FleetState.Outbound, 959.9, AircraftPhase.Approach)]
        [TestCase(FleetState.Outbound, 960, AircraftPhase.Landing)]
        [TestCase(FleetState.Outbound, 1000, AircraftPhase.Landing)]
        [TestCase(FleetState.AtDestination, 0, AircraftPhase.AtStand)]
        public void JourneyVisualPhase_UsesActualJourneyStage(FleetState state, double elapsed, AircraftPhase expected)
        {
            Assert.That(RegionalFlightPath.JourneyPhase(state, elapsed, 1000), Is.EqualTo(expected));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void JourneyVisualPhase_PreservesAircraftSpecificReturnRotation(bool turboprop)
        {
            var type = turboprop ? AircraftType.Dash8Q400 : AircraftType.Boeing7378;
            var rotate = RegionalFlightPath.RotateSeconds(type);
            Assert.That(RegionalFlightPath.JourneyPhase(FleetState.Inbound, rotate - 0.001, 1000, rotate),
                Is.EqualTo(AircraftPhase.Takeoff));
            Assert.That(RegionalFlightPath.JourneyPhase(FleetState.Inbound, rotate, 1000, rotate),
                Is.EqualTo(AircraftPhase.Departed));
        }

        [TestCase(-1,-1)] [TestCase(0,0)] [TestCase(15999,0)] [TestCase(16000,1)]
        public void TileUsesFloorAcrossBothSides(double metres,int expected) => Assert.That(FlightWorldGrid.Tile(metres),Is.EqualTo(expected));
        [TestCase(-1,-1)] [TestCase(0,0)] [TestCase(63999,0)] [TestCase(64000,1)] [TestCase(-64001,-2)]
        public void CoarseTileUsesFloorAcrossBothSides(double metres,int expected) => Assert.That(FlightWorldGrid.CoarseTile(metres),Is.EqualTo(expected));
        [Test] public void CoarseRingIsBoundedAndCoversTheWholeStateFromAdelaide()
        {
            Assert.That(FlightWorldGrid.CoarseMaxTiles,Is.EqualTo(289));
            var resident=0;
            for(var x=-20;x<=20;x++) for(var z=-20;z<=20;z++) if(FlightWorldGrid.CoarseResident(x,z,0,0)) resident++;
            Assert.That(resident,Is.EqualTo(121),"the default ring is 5 tiles");
            resident=0;
            for(var x=-20;x<=20;x++) for(var z=-20;z<=20;z++)
                if(FlightWorldGrid.CoarseResident(x,z,0,0,FlightWorldGrid.CoarseMaxRadiusTiles)) resident++;
            Assert.That(resident,Is.EqualTo(FlightWorldGrid.CoarseMaxTiles));
            Assert.That(FlightWorldGrid.CoarseResident(5,-5,0,0),Is.True);
            Assert.That(FlightWorldGrid.CoarseResident(6,0,0,0),Is.False);
            Assert.That(FlightWorldGrid.CoarseResident(int.MaxValue,0,int.MinValue,0),Is.False,"no overflow");
            // The ring must reach well past the 96 km Adelaide rings and the fine ring's 56 km.
            Assert.That((FlightWorldGrid.CoarseRadiusTiles+0.0)*FlightWorldGrid.CoarseTileMetres,Is.GreaterThan(300000));
            Assert.That(FlightWorldGrid.CoarseTileMetres/FlightWorldGrid.CoarseCells,Is.LessThanOrEqualTo(2000),"~2 km cells match the DEM");
        }
        [Test] public void CoarseRingGrowsToKeepTheGroundUnderTheCameraCovered()
        {
            Assert.That(FlightWorldGrid.CoarseRadiusFor(60001),Is.EqualTo(FlightWorldGrid.CoarseRadiusTiles));
            Assert.That(FlightWorldGrid.CoarseRadiusFor(AirsideBareField.MaxOrbitDistance),Is.EqualTo(FlightWorldGrid.CoarseMaxRadiusTiles));
            foreach(var distance in new[]{60001.0,150000,300000,AirsideBareField.MaxOrbitDistance})
            {
                // The camera stands at most its orbit distance from the focus; the focus can sit anywhere in its tile.
                var reach=(FlightWorldGrid.CoarseRadiusFor(distance)+0.0)*FlightWorldGrid.CoarseTileMetres;
                Assert.That(reach,Is.GreaterThan(distance),$"ground under the camera at {distance} m");
            }
        }
        [Test] public void WideHorizonFadeKeepsTheFocusClearAndHidesTheRingEdge()
        {
            foreach(var distance in new[]{60001.0,150000,AirsideBareField.MaxOrbitDistance})
            {
                var scale=AirsideCameraFeel.HorizonScale((float)distance,AirsideBareField.CameraFarClip);
                FlightWorldGrid.WideHorizonFade(distance,scale,out var start,out var end);
                Assert.That(start*scale,Is.GreaterThan(distance),"the focus is never hazed");
                Assert.That(end,Is.GreaterThan(start));
                var reach=(FlightWorldGrid.CoarseRadiusFor(distance)+0.0)*FlightWorldGrid.CoarseTileMetres;
                Assert.That(end*scale,Is.LessThan(Math.Sqrt(reach*reach+distance*distance)),"gone before the nearest ring edge in view");
            }
        }
        [Test] public void WideMapStartsOnlyBeyondTheClassicZoom()
        {
            Assert.That(FlightWorldGrid.WideMap(AirsideBareField.ClassicMaxOrbitDistance),Is.False);
            Assert.That(FlightWorldGrid.WideMap(45000),Is.False,"the old zoom limit never streams");
            Assert.That(FlightWorldGrid.WideMap(60000),Is.False);
            Assert.That(FlightWorldGrid.WideMap(60001),Is.True);
            Assert.That(FlightWorldGrid.WideMap(AirsideBareField.MaxOrbitDistance),Is.True);
        }
        [Test] public void RunwayApproachTerrainCellsStayFlatAcrossTheDetailedMesh()
        {
            var stride=FlightWorldGrid.TileMetres/FlightWorldDetail.Cells(false,true);
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
        [Test] public void AllAustralianDestinationsAreCovered()
        {
            foreach(var d in Airside.Domain.DestinationCatalogue.Australia)
                Assert.That(FlightWorldGrid.Covered(d.Latitude,d.Longitude),Is.True,d.Name);
        }
        [Test] public void MappedRegionalLandingsEndOnTheRunwayAtAirportElevation()
        {
            Assert.That(RegionalRunways.All.Length,Is.GreaterThanOrEqualTo(18));
            Assert.That(RegionalRunways.TryGet("HBA",out _),Is.True);
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
        [Test] public void CruiseDensityHasHysteresisAndAirportDetailStaysBounded()
        {
            Assert.That(FlightWorldDetail.Cruise(4400,false),Is.False);
            Assert.That(FlightWorldDetail.Cruise(4500,false),Is.True);
            Assert.That(FlightWorldDetail.Cruise(3600,true),Is.True);
            Assert.That(FlightWorldDetail.Cruise(3499,true),Is.False);
            Assert.That(FlightWorldDetail.Cruise(double.NaN,true),Is.False);
            Assert.That(FlightWorldDetail.Cells(true,false),Is.EqualTo(8));
            Assert.That(FlightWorldDetail.Cells(false,true),Is.EqualTo(64));
            var count=0;
            for(var z=-8;z<=8;z++) for(var x=-8;x<=8;x++)
                if(FlightWorldDetail.ApproachTile(x,z,0,0)) count++;
            Assert.That(count,Is.EqualTo(9));
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

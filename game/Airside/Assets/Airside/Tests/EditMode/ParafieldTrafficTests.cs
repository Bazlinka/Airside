using System;
using Airside.Domain;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class ParafieldTrafficTests
    {
        [Test]
        public void LayoutHasFourRealRunwaysAndConnectedMappedTaxiRoutes()
        {
            Assert.That(ParafieldLayout.Runways.Length,Is.EqualTo(4));
            Assert.That(ParafieldLayout.MainRunway.Name,Is.EqualTo("03L/21R"));
            Assert.That(ParafieldLayout.MainRunway.Length,Is.InRange(1300,1400));
            Assert.That(ParafieldLayout.Taxiways.Length,Is.GreaterThan(20));
            Assert.That(ParafieldLayout.Parking.Length,Is.EqualTo(ParafieldTraffic.AircraftCount));
            Assert.That(ParafieldLayout.RunwayToApron[0].Distance(ParafieldLayout.LandingExit),Is.LessThan(.01));
        }

        [Test]
        public void FullRosterVisitsEveryPhaseAndKeepsGroundAircraftSeparated()
        {
            var clock=new ManualSimulationClock(new SimulationTime(0));
            var traffic=new ParafieldTraffic(clock,new SeededRandomSource(593));
            var phases=new bool[Enum.GetValues(typeof(ParafieldPhase)).Length];
            var flown=new bool[ParafieldTraffic.AircraftCount];
            for(var t=0L;t<traffic.SlotSeconds*ParafieldTraffic.AircraftCount;t++)
            {
                clock.Set(new SimulationTime(t));traffic.Update();
                for(var i=0;i<ParafieldTraffic.AircraftCount;i++)
                {
                    var pose=traffic.Pose(i,t);
                    phases[(int)pose.Phase]=true;
                    if(pose.Height>20)flown[i]=true;
                    Assert.That(double.IsNaN(pose.Position.X) || double.IsNaN(pose.Height),Is.False);
                    if(pose.Phase!=ParafieldPhase.Parked)
                    {
                        foreach(var runway in ParafieldLayout.Runways)
                        {
                            Assert.That(traffic.Reservations.TryGetOwner(new StableId("YPPF-runway-"+runway.Name),out var owner),Is.True);
                            Assert.That(owner,Is.EqualTo(new StableId("parafield-trainer-"+i)));
                        }
                    }
                    if(pose.Height>2)continue;
                    for(var j=i+1;j<ParafieldTraffic.AircraftCount;j++)
                    {
                        var other=traffic.Pose(j,t);
                        if(other.Height<2)Assert.That(pose.Position.Distance(other.Position),Is.GreaterThan(12),"ground clearance at "+t);
                    }
                }
            }
            Assert.That(phases,Is.All.True);
            Assert.That(flown,Is.All.True);
        }

        [Test]
        public void ClockJumpMatchesFrequentUpdatesAndPreservesContinuousPhaseJoins()
        {
            var ca=new ManualSimulationClock(new SimulationTime(0));var cb=new ManualSimulationClock(new SimulationTime(0));
            var a=new ParafieldTraffic(ca,new SeededRandomSource(593));var b=new ParafieldTraffic(cb,new SeededRandomSource(593));
            for(var t=1;t<=3000;t++) { ca.Set(new SimulationTime(t));a.Update(); }
            cb.Set(new SimulationTime(3000));b.Update();
            Assert.That(a.ActiveAircraft,Is.EqualTo(b.ActiveAircraft));
            for(var i=0;i<ParafieldTraffic.AircraftCount;i++)
            {
                Assert.That(a.Pose(i,3000.5).Position.X,Is.EqualTo(b.Pose(i,3000.5).Position.X));
                Assert.That(a.Pose(i,3000.5).Height,Is.EqualTo(b.Pose(i,3000.5).Height));
            }
            ca=new ManualSimulationClock(new SimulationTime(0));a=new ParafieldTraffic(ca,new SeededRandomSource(593));
            var prior=a.Pose(a.ActiveAircraft,0);
            for(var t=1L;t<a.CycleSeconds(a.ActiveAircraft)-1;t++)
            {
                ca.Set(new SimulationTime(t));a.Update();
                var pose=a.Pose(a.ActiveAircraft,t);
                Assert.That(pose.Position.Distance(prior.Position),Is.LessThan(90),"continuity at "+t);
                Assert.That(Math.Abs(pose.Height-prior.Height),Is.LessThan(4),"climb/descent rate at "+t);
                prior=pose;
            }
        }

        [Test]
        public void ConflictingMovementReservationPreventsTheTrainerLeavingParking()
        {
            var table=new ReservationTable();
            table.TryReplace(new StableId("other-aircraft"),new[] {new StableId("YPPF-taxi-corridor")},out _);
            var traffic=new ParafieldTraffic(new ManualSimulationClock(new SimulationTime(0)),new SeededRandomSource(593),table);
            Assert.That(traffic.ActiveAircraft,Is.EqualTo(-1));
            for(var i=0;i<ParafieldTraffic.AircraftCount;i++) Assert.That(traffic.Pose(i,0).Phase,Is.EqualTo(ParafieldPhase.Parked));
        }

        [Test]
        public void GroundPlatformBlendsBackToTerrainOutsideAirport()
        {
            Assert.That(ParafieldLayout.GroundHeight(ParafieldLayout.CentreX,ParafieldLayout.CentreZ,100),
                Is.EqualTo(ParafieldLayout.ElevationMetres).Within(.001));
            Assert.That(ParafieldLayout.GroundHeight(ParafieldLayout.CentreX+2200,ParafieldLayout.CentreZ,100),Is.EqualTo(100));
        }

        [Test]
        public void ParkingAndApronLaneStayOnPavementAndClearOfHangars()
        {
            foreach(var points in new[] {ParafieldLayout.Parking,ParafieldLayout.ParkingLane})
            foreach(var point in points)
            {
                var paved=false;
                foreach(var apron in ParafieldLayout.Aprons)paved|=Inside(point,apron);
                Assert.That(paved,Is.True,"parking/lane must be on the mapped apron");
                foreach(var building in ParafieldLayout.Buildings)
                {
                    Assert.That(Inside(point,building),Is.False);
                    for(var i=0;i<building.Length;i++)
                        Assert.That(EdgeDistance(point,building[i],building[(i+1)%building.Length]),
                            Is.GreaterThan(8),"11 m trainer span plus building margin");
                }
            }
            for(var i=1;i<ParafieldLayout.ParkingLane.Length;i++)
            for(var sample=0;sample<=20;sample++)
            {
                var point=ParafieldPoint.Lerp(ParafieldLayout.ParkingLane[0],ParafieldLayout.ParkingLane[i],sample/20.0);
                var paved=false;
                foreach(var apron in ParafieldLayout.Aprons)paved|=Inside(point,apron);
                Assert.That(paved,Is.True,"apron lane stays on asphalt between stands");
            }
        }

        private static bool Inside(ParafieldPoint p,ParafieldPoint[] polygon)
        {
            var result=false;
            for(var i=0;i<polygon.Length;i++)
            {
                var a=polygon[i];var b=polygon[(i+1)%polygon.Length];
                if((a.Z>p.Z)!=(b.Z>p.Z) && p.X<(b.X-a.X)*(p.Z-a.Z)/(b.Z-a.Z)+a.X)result=!result;
            }
            return result;
        }

        private static double EdgeDistance(ParafieldPoint p,ParafieldPoint a,ParafieldPoint b)
        {
            var dx=b.X-a.X;var dz=b.Z-a.Z;var squared=dx*dx+dz*dz;
            var t=squared<.000001 ? 0 : Math.Clamp(((p.X-a.X)*dx+(p.Z-a.Z)*dz)/squared,0,1);
            return p.Distance(ParafieldPoint.Lerp(a,b,t));
        }
    }
}

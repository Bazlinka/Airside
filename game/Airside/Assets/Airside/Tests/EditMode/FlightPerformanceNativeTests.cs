using System;
using System.Collections.Generic;
using Airside.Domain;
using Airside.Presentation;
using Airside.Simulation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class FlightPerformanceNativeTests
    {
        private static IEnumerable<AircraftType> Types()
        {foreach(var spec in AircraftCatalogue.All) if(!spec.Type.IsRotorcraft) yield return spec.Type;}
        [TestCaseSource(nameof(Types))]
        public void ScheduledArrivalCoversItsDistanceWithoutAJump(AircraftType type)
        {
            foreach(var km in new[]{130.0,650,1500,type.PracticalRangeKm})
            {
                var seconds=LegTiming.AirborneSeconds(km,type);
                var speed=CircuitProfile.Knots(AircraftPerformance.For(type).ApproachKnots);
                var final=ArrivalApproach.ShowMetres/speed;
                var before=ArrivalMapTrack.DistanceOutMetres(km,seconds,final+.01,type);
                Assert.That(before-ArrivalApproach.ShowMetres,Is.InRange(0.0,5.0),type.Id+" "+km+" km arrival jump");
            }
        }
        [TestCaseSource(nameof(Types))]
        public void ArrivalHeightIsContinuousIntoTheSameFinalInEveryView(AircraftType type)
        {
            const double km=1500;
            var seconds=LegTiming.AirborneSeconds(km,type);
            var speed=CircuitProfile.Knots(AircraftPerformance.For(type).ApproachKnots);
            var finalSeconds=ArrivalApproach.ShowMetres/speed;
            var at=ArrivalMapTrack.HeightMetres(km,seconds,finalSeconds,type);
            var before=ArrivalMapTrack.HeightMetres(km,seconds,finalSeconds+.001,type);
            var after=ArrivalMapTrack.HeightMetres(km,seconds,finalSeconds-.001,type);
            Assert.That(at,Is.EqualTo(ArrivalMapTrack.FinalEntryHeightMetres(type)).Within(.001));
            Assert.That(before,Is.EqualTo(at).Within(.01));Assert.That(after,Is.EqualTo(at).Within(.01));
            var last=ArrivalMapTrack.HeightMetres(km,seconds,seconds,type);
            var descending=false;
            for(var remaining=(double)seconds-1;remaining>=0;remaining-=1)
            {
                var height=ArrivalMapTrack.HeightMetres(km,seconds,remaining,type);
                if(height<last-.01) descending=true;
                if(descending) Assert.That(height,Is.LessThanOrEqualTo(last+.001),type.Id+" climbed again on arrival");
                Assert.That(last-height,Is.LessThan(4000/(60*EnrouteProfile.FeetPerMetre)),type.Id+" excess sink");
                last=height;
            }
        }
        [TestCaseSource(nameof(Types))]
        public void RegionalTakeoffWaitsForThatTypesVrAndStaysWithin250Cas(AircraftType type)
        {
            var runway=RegionalRunways.All[0];var p=AircraftPerformance.For(type);
            var rotate=RegionalFlightPath.RotateSeconds(type);
            RegionalFlightPath.Departure(runway,0,1800,type,out var x0,out var y0,out var z0);
            RegionalFlightPath.Departure(runway,rotate,1800,type,out var xr,out var yr,out var zr);
            Assert.That(yr,Is.EqualTo(y0).Within(.0001));
            Assert.That(Math.Sqrt((xr-x0)*(xr-x0)+(zr-z0)*(zr-z0)),Is.EqualTo(p.TakeoffRollMetres).Within(.01));
            const double dt=.01;
            RegionalFlightPath.Departure(runway,rotate-dt,1800,type,out var xb,out _,out var zb);
            var vr=Math.Sqrt((xr-xb)*(xr-xb)+(zr-zb)*(zr-zb))/dt/CircuitProfile.KnotsToMetresPerSecond;
            Assert.That(vr,Is.EqualTo(p.RotateKnots).Within(.1));
            for(var t=rotate+dt;t<120-dt;t+=.5)
            {
                RegionalFlightPath.Departure(runway,t,1800,type,out var x,out var y,out var z);
                RegionalFlightPath.Departure(runway,t+dt,1800,type,out var nx,out var ny,out var nz);
                var gs=Math.Sqrt((nx-x)*(nx-x)+(nz-z)*(nz-z))/dt/CircuitProfile.KnotsToMetresPerSecond;
                Assert.That(FlightAtmosphere.CalibratedKnots(gs,y),Is.LessThanOrEqualTo(250));
                Assert.That(ny,Is.GreaterThanOrEqualTo(y));
            }
        }
    }
}

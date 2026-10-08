using System;
using System.Collections.Generic;
using Airside.Domain;
using Airside.Simulation;
using Airside.Presentation;
using NUnit.Framework;

namespace Airside.Tests
{
    public sealed class FlightPerformanceRealismTests
    {
        private static IEnumerable<AircraftType> FixedWing()
        {foreach(var spec in AircraftCatalogue.All) if(!spec.Type.IsRotorcraft) yield return spec.Type;}
        [TestCaseSource(nameof(FixedWing))]
        public void WholeLegMatchesItsDerivativeAndHasLevelCruise(AircraftType type)
        {
            var p=EnrouteProfile.For(1500,LegTiming.AirborneSeconds(1500,type),type);
            const double dt=.001;
            for(var t=1.0;t<p.LegSeconds-1;t+=3)
            {
                var rate=(p.AltitudeFeetAt(t+dt)-p.AltitudeFeetAt(t-dt))/(2*dt)*60;
                Assert.That(rate,Is.EqualTo(p.VerticalSpeedFeetPerMinuteAt(t)).Within(.05),type.Id+" at "+t);
                Assert.That(p.AltitudeFeetAt(t),Is.InRange(Math.Min(p.StartFeet,p.EndFeet),p.CruiseFeet+.001));
                var speed=(p.DistanceFractionAt(t+dt)-p.DistanceFractionAt(t-dt))*p.LegMetres/(2*dt)/CircuitProfile.KnotsToMetresPerSecond;
                if(p.DistanceFractionAt(t)<.99999) Assert.That(speed,Is.EqualTo(p.GroundSpeedKnotsAt(t)).Within(.1));
                if(FlightAtmosphere.AltitudeMetres(p.AltitudeFeetAt(t))*EnrouteProfile.FeetPerMetre<=10000)
                    Assert.That(p.CalibratedSpeedKnotsAt(t),Is.LessThanOrEqualTo(250.01),type.Id+" low-altitude speed");
            }
            Assert.That(p.CruiseSeconds,Is.GreaterThan(0));
            var cruise=p.ClimbSeconds+p.CruiseSeconds*.5;
            Assert.That(p.VerticalSpeedFeetPerMinuteAt(cruise),Is.Zero);
            Assert.That(p.AltitudeFeetAt(cruise),Is.EqualTo(p.CruiseFeet));
            Assert.That(p.CruiseFeet,Is.LessThanOrEqualTo(AircraftPerformance.For(type).MaxCruiseFeet));
            Assert.That(p.VerticalSpeedFeetPerMinuteAt(p.ClimbSeconds-.001),Is.InRange(0,.1));
            Assert.That(p.VerticalSpeedFeetPerMinuteAt(p.ClimbSeconds+p.CruiseSeconds+.001),Is.InRange(-.1,0));
        }
        [Test]
        public void E190RateIsNotPinnedTo2000AndNormalCruiseIsBelowCertificationCeiling()
        {
            var type=AircraftType.EmbraerE190;
            var p=EnrouteProfile.For(3000,LegTiming.AirborneSeconds(3000,type),type);
            Assert.That(p.VerticalSpeedFeetPerMinuteAt(30),Is.GreaterThan(2000));
            Assert.That(p.CruiseFeet,Is.LessThanOrEqualTo(37000));
            Assert.That(AircraftPerformance.For(type).MaxCruiseFeet,Is.EqualTo(41000));
            var highTime=p.ClimbSeconds-120;
            Assert.That(p.VerticalSpeedFeetPerMinuteAt(highTime),Is.LessThan(1800));
        }
        [Test]
        public void BellRetainsItsLowVtolCruiseAndDefaultProfileIsSafe()
        {
            var type=AircraftType.Bell412;
            var p=EnrouteProfile.For(5,LegTiming.AirborneSeconds(5,type),type);
            Assert.That(p.CruiseFeet,Is.LessThanOrEqualTo(1500));
            Assert.That(RotorcraftPerformance.For(type).CruiseKnots,Is.LessThan(122));
            Assert.That(default(EnrouteProfile).AltitudeFeetAt(0),Is.Zero);
        }
        [TestCase(0)] [TestCase(3048)] [TestCase(10668)] [TestCase(13106)]
        public void CasTasRoundTripAndMachUseLocalSoundSpeed(double altitude)
        {
            foreach(var cas in new[]{110.0,160,250,300})
                Assert.That(FlightAtmosphere.CalibratedKnots(FlightAtmosphere.TrueKnots(cas,altitude),altitude),Is.EqualTo(cas).Within(.00001));
            Assert.That(FlightAtmosphere.Mach(250,altitude),Is.EqualTo(250/FlightAtmosphere.SoundKnots(altitude)).Within(.00001));
            if(altitude>0) Assert.That(FlightAtmosphere.TrueKnots(250,altitude),Is.GreaterThan(250));
        }
        [TestCase(.016)] [TestCase(.5)] [TestCase(1.5)] [TestCase(5)]
        public void CameraTelemetryKeepsUpdatingAtAcceleratedRatesAndAcrossOriginMoves(double dt)
        {
            var sampler=new FlightTelemetrySampler();
            sampler.Reset(10,20,0,0,100,0);
            for(var i=1;i<=300;i++)
            {
                var t=i*dt;var origin=i<150 ? 0 : 16000;
                sampler.Sample(10+100*t-origin,20,origin,0,100+15*t,t);
            }
            Assert.That(sampler.GroundKnots,Is.EqualTo(100/CircuitProfile.KnotsToMetresPerSecond).Within(.01));
            Assert.That(sampler.VerticalMetresPerSecond,Is.EqualTo(15).Within(.001));
            sampler.Sample(10+100*300*dt-16000,20,16000,0,100+15*300*dt,300*dt);
            Assert.That(sampler.VerticalMetresPerSecond,Is.EqualTo(15).Within(.001),"Paused pose retains last motion reading");
        }
        [TestCaseSource(nameof(FixedWing))]
        public void LowAltitudeTakeoffSpeedAndRotationRemainTypeSpecific(AircraftType type)
        {
            var p=AircraftPerformance.For(type);
            for(var i=0;i<=1000;i++)
                Assert.That(p.AirspeedKnots(AircraftPhase.Takeoff,i/1000f),Is.InRange(0,250));
            Assert.That(p.AirspeedKnots(AircraftPhase.Takeoff,p.RotateProgress),Is.EqualTo(p.RotateKnots).Within(.01));
        }
    }
}

using System;
using System.Collections.Generic;
using Airside.Domain;

namespace Airside.Simulation
{
    public enum EnroutePhase
    {
        Climb,
        Cruise,
        Descent
    }

    /// <summary>Deterministic researched flight display profile. Altitude rates taper with height and capture
    /// cruise smoothly. Speed and integrated map distance share one schedule, constrained in CAS and Mach.
    /// No camera mode owns a separate flight model, and existing leg/save timers remain authoritative.</summary>
    public readonly struct EnrouteProfile
    {
        public const double FeetPerMetre = 3.28084;
        public const double ClimbFeetPerMinute = 1200;
        public const double DescentFeetPerMinute = 1500;
        public const double ClimbSpeedFraction = 0.65;
        public const double DescentSpeedFraction = 0.8;
        public const double MinCruiseFeet = 8000;
        public const double MaxCruiseFeet = 25000;

        /// <summary>Australian transition altitude: feet at or below it, flight levels above.</summary>
        public const double TransitionAltitudeFeet = 10000;

        /// <summary>Climb plus descent may take at most this share of the leg; short hops level off lower.</summary>
        private const double MaxClimbDescentShare = 0.85;

        public EnrouteProfile(double legKm, double legSeconds)
            : this(legKm, legSeconds, AircraftType.Atr42)
        {
        }

        public EnrouteProfile(double legKm, double legSeconds, AircraftType type, double? endFeet = null)
        {
            this = default;
            var performance = AircraftPerformance.For(type);
            LegMetres = Math.Max(0.0, legKm * 1000.0);
            LegSeconds = Math.Max(1.0, legSeconds);
            StartFeet = (type?.IsRotorcraft == true ? RotorcraftPerformance.For(type).ClimbOutHeightMetres : performance.DepartedEndHeight) * FeetPerMetre;
            EndFeet = endFeet ?? (type?.IsRotorcraft == true ? RotorcraftPerformance.For(type).ApproachHeightMetres : CircuitProfile.ApproachStartHeight) * FeetPerMetre;

            _type=type ?? AircraftType.Atr42;
            _operating=FlightOperatingProfile.For(_type);
            var wanted=PlannedCruiseFeet(legKm,_type);
            var low=Math.Max(StartFeet,EndFeet);
            var high=Math.Max(low,wanted);
            // Choose a lower level when this trip cannot fit the type's climb, capture and descent.
            for(var i=0;i<24;i++)
            {
                var level=(low+high)*.5;
                var up=new FlightVerticalProfile(StartFeet,level,_operating,false);
                var down=new FlightVerticalProfile(level,EndFeet,_operating,true,endFeet.HasValue);
                if(up.Seconds+down.Seconds<=MaxClimbDescentShare*LegSeconds) low=level; else high=level;
            }
            CruiseFeet=low;
            _climb=new FlightVerticalProfile(StartFeet,CruiseFeet,_operating,false);
            _descent=new FlightVerticalProfile(CruiseFeet,EndFeet,_operating,true,endFeet.HasValue);
            ClimbSeconds=_climb.Seconds; DescentSeconds=_descent.Seconds;
            CruiseSeconds=Math.Max(0,LegSeconds-ClimbSeconds-DescentSeconds);
            ClimbRateFeetPerMinute=_operating.LowClimb;
            DescentRateFeetPerMinute=_operating.LowDescent;
            // One immutable speed table per cached leg. Phase joins are explicit nodes, so cruise changes
            // neither speed nor integrated position. Unreachable schedules lag rather than invent overspeed.
            var nodes=new List<double>{0};
            for(var phase=0;phase<3;phase++)
            {
                var from=phase==0 ? 0 : phase==1 ? ClimbSeconds : ClimbSeconds+CruiseSeconds;
                var duration=phase==0 ? ClimbSeconds : phase==1 ? CruiseSeconds : DescentSeconds;
                if(duration<=0) continue;
                for(var i=1;i<=256;i++) nodes.Add(from+duration*i/256);
            }
            // Put speed-rule corners at exact pressure heights so interpolation cannot smear
            // the above-10,000ft acceleration back across the restricted side of the boundary.
            foreach(var amsl in new[]{10000.0,12000.0})
            {
                var field=amsl-FlightAtmosphere.FieldElevationMetres*FeetPerMetre;
                if(field>StartFeet && field<CruiseFeet) nodes.Add(HeightTime(_climb,field,false));
                if(field>EndFeet && field<CruiseFeet) nodes.Add(ClimbSeconds+CruiseSeconds+HeightTime(_descent,field,true));
            }
            nodes.Sort();
            _speedTimes=nodes.ToArray();_speedKnots=new double[_speedTimes.Length];_distance=new double[_speedTimes.Length];
            var fastest=MaxCruiseMetresPerSecond(_type)/CircuitProfile.KnotsToMetresPerSecond;
            var ceilingSpeeds=new double[_speedTimes.Length];
            for(var n=0;n<_speedTimes.Length;n++) ceilingSpeeds[n]=ScheduledSpeed(_speedTimes[n],fastest);
            var lower=0.0;var upper=fastest;
            for(var i=0;i<28;i++)
            {
                var candidate=(lower+upper)*.5;
                var distance=0.0;var previous=Math.Min(candidate,ceilingSpeeds[0]);
                for(var n=1;n<_speedTimes.Length;n++)
                {
                    var speed=Math.Min(candidate,ceilingSpeeds[n]);
                    distance+=(speed+previous)*.5*(_speedTimes[n]-_speedTimes[n-1])*CircuitProfile.KnotsToMetresPerSecond;
                    previous=speed;
                }
                if(distance<LegMetres) lower=candidate;else upper=candidate;
            }
            CruiseMetresPerSecond=upper*CircuitProfile.KnotsToMetresPerSecond;
            for(var n=0;n<_speedTimes.Length;n++)
            {
                _speedKnots[n]=Math.Min(upper,ceilingSpeeds[n]);
                if(n>0) _distance[n]=_distance[n-1]+(_speedKnots[n]+_speedKnots[n-1])*.5
                    *(_speedTimes[n]-_speedTimes[n-1])*CircuitProfile.KnotsToMetresPerSecond;
            }
        }

        private static double HeightTime(FlightVerticalProfile profile,double height,bool descending)
        {
            var low=0.0;var high=profile.Seconds;
            for(var i=0;i<32;i++)
            {
                var mid=(low+high)*.5;
                if((profile.Height(mid)<height)!=descending) low=mid; else high=mid;
            }
            return (low+high)*.5;
        }

        private readonly AircraftType _type;
        private readonly FlightOperatingProfile _operating;
        private readonly FlightVerticalProfile _climb, _descent;
        private readonly double[] _speedTimes, _speedKnots, _distance;
        private static readonly Dictionary<(string,double,double,double?),EnrouteProfile> Cache=new();
        public static EnrouteProfile For(double km,double seconds,AircraftType type = null,double? endFeet = null)
        {
            var key=(type?.Id ?? AircraftType.Atr42.Id,km,seconds,endFeet);
            if(Cache.TryGetValue(key,out var found)) return found;
            var result=new EnrouteProfile(km,seconds,type,endFeet);
            // Bounded, derived cache; no simulation/persistence state. A fresh save reuses the same immutable data.
            if(Cache.Count>=512) Cache.Clear();
            Cache[key]=result;return result;
        }
        private double ScheduledSpeed(double seconds,double cruiseKnots)
        {
            var height=AltitudeFeetAt(seconds);
            var performance=AircraftPerformance.For(_type);
            var climbLimit=_operating.SpeedLimitTrueKnots(_type,height,true);
            var openLimit=_operating.SpeedLimitTrueKnots(_type,height);
            var release=Smooth((seconds-(ClimbSeconds-120))/120);
            var limit=climbLimit+(openLimit-climbLimit)*release;
            var altitude=FlightAtmosphere.AltitudeMetres(height);
            var speed=cruiseKnots;
            if(seconds<ClimbSeconds)
            {
                var accelerate=Smooth((height-StartFeet)/Math.Max(1,5000-StartFeet));
                var cas=performance.ClimbOutKnots+(_operating.ClimbCas-performance.ClimbOutKnots)*accelerate;
                var climbSpeed=FlightAtmosphere.TrueKnots(cas,altitude);
                speed=Math.Min(cruiseKnots,climbSpeed+(cruiseKnots-climbSpeed)*release);
            }
            else if(seconds>ClimbSeconds+CruiseSeconds)
            {
                var decelerate=Smooth((height-EndFeet)/Math.Max(1,10000-EndFeet));
                var cas=performance.ApproachEntryKnots+(Math.Max(250,_operating.ClimbCas)-performance.ApproachEntryKnots)*decelerate;
                speed=Math.Min(cruiseKnots,FlightAtmosphere.TrueKnots(cas,altitude));
            }
            // Tiny margin keeps interpolation between ISA samples below the continuous CAS envelope.
            return Math.Min(Math.Max(0,speed),Math.Max(0,limit-1));
        }
        private static double Smooth(double value) {var u=Math.Clamp(value,0,1);return u*u*(3-2*u);}

        public double LegMetres { get; }
        public double LegSeconds { get; }
        public double StartFeet { get; }
        public double EndFeet { get; }
        public double CruiseFeet { get; }
        public double ClimbSeconds { get; }
        public double CruiseSeconds { get; }
        public double DescentSeconds { get; }
        public double CruiseMetresPerSecond { get; }
        public double ClimbRateFeetPerMinute { get; }
        public double DescentRateFeetPerMinute { get; }

        /// <summary>
        /// The fastest this type can cruise, in m/s: the published maximum where there is one,
        /// otherwise the planning cruise with a small allowance for a tailwind.
        /// </summary>
        public static double MaxCruiseMetresPerSecond(AircraftType type)
        {
            if (type == null)
                return 0.0;
            var planningKmh = type.CruiseKmh;
            var maxKmh = AircraftCatalogue.TryFor(type, out var spec) && spec.ManufacturerMaxCruiseKmh > 0
                ? spec.ManufacturerMaxCruiseKmh
                : planningKmh;
            return Math.Max(maxKmh, planningKmh * 1.1) / 3.6;
        }

        /// <summary>Cruise level an ATR 42 would plan for a leg: ~6 000 ft + 25 ft/km, to the nearest 1 000 ft.</summary>
        public static double PlannedCruiseFeet(double legKm)
            => PlannedCruiseFeet(legKm, AircraftType.Atr42);

        public static double PlannedCruiseFeet(double legKm, AircraftType type)
        {
            if(type?.IsRotorcraft == true) return RotorcraftPerformance.For(type).CruiseFeet;
            var performance = AircraftPerformance.For(type);
            // Every authored jet climbs like a jet, not just the 737 — A321neo, A350 and
            // 787 fell through to the turboprop formula and planned a widebody
            // international service at ~22,000 ft on a medium leg instead of the
            // ~33,000 ft a jet would actually plan, which showed up directly in the HUD's
            // "FLxxx" readout.
            var jet = AircraftCatalogue.TryFor(type, out var spec)
                      && spec.StandClass == StandClass.TerminalGate;
            var raw = (jet ? 8000 : 6000) + (jet ? 38 : 25) * Math.Max(0.0, legKm);
            var rounded = Math.Round(raw / 1000.0) * 1000.0;
            return Math.Max(MinCruiseFeet, Math.Min(Math.Min(performance.MaxCruiseFeet,FlightOperatingProfile.For(type).NormalCruiseFeet), rounded));
        }

        public EnroutePhase PhaseAt(double seconds)
        {
            if (seconds < ClimbSeconds)
                return EnroutePhase.Climb;
            return seconds < ClimbSeconds + CruiseSeconds ? EnroutePhase.Cruise : EnroutePhase.Descent;
        }

        public double AltitudeFeetAt(double seconds)
        {
            if(_climb==null || _descent==null) return 0;
            var t=Clamp(seconds,0,LegSeconds);
            if(t<ClimbSeconds) return _climb.Height(t);
            if(t<ClimbSeconds+CruiseSeconds) return CruiseFeet;
            return _descent.Height(t-ClimbSeconds-CruiseSeconds);
        }
        public double VerticalSpeedFeetPerMinuteAt(double seconds)
        {
            if(seconds<0 || seconds>=LegSeconds) return 0;
            if(seconds<ClimbSeconds) return _climb.Rate(seconds);
            if(seconds<ClimbSeconds+CruiseSeconds) return 0;
            return _descent.Rate(seconds-ClimbSeconds-CruiseSeconds);
        }
        public double GroundSpeedKnotsAt(double seconds)
        {
            if(_speedTimes==null || _speedTimes.Length<2) return 0;
            var i=SpeedSegment(seconds);
            var u=Clamp((seconds-_speedTimes[i])/(_speedTimes[i+1]-_speedTimes[i]),0,1);
            return _speedKnots[i]+(_speedKnots[i+1]-_speedKnots[i])*u;
        }
        public double CalibratedSpeedKnotsAt(double seconds) => FlightAtmosphere.CalibratedKnots(
            GroundSpeedKnotsAt(seconds),FlightAtmosphere.AltitudeMetres(AltitudeFeetAt(seconds)));
        public double MachAt(double seconds) => FlightAtmosphere.Mach(GroundSpeedKnotsAt(seconds),
            FlightAtmosphere.AltitudeMetres(AltitudeFeetAt(seconds)));
        private int SpeedSegment(double seconds)
        {
            var i=Array.BinarySearch(_speedTimes,Clamp(seconds,0,LegSeconds));
            return Math.Clamp(i>=0 ? i : ~i-1,0,_speedTimes.Length-2);
        }
        /// <summary>Exact integral of the same piecewise-linear speed the HUD reports.</summary>
        public double DistanceFractionAt(double seconds)
        {
            if(LegMetres<=0) return 1;
            if(_speedTimes==null || _speedTimes.Length<2) return 0;
            var t=Clamp(seconds,0,LegSeconds);var i=SpeedSegment(t);var dt=t-_speedTimes[i];
            var metres=_distance[i]+(_speedKnots[i]+GroundSpeedKnotsAt(t))*.5*dt*CircuitProfile.KnotsToMetresPerSecond;
            return Clamp(metres/LegMetres,0,1);
        }

        /// <summary>"9 000 ft" at or below the transition altitude, "FL220" above it.</summary>
        public static string AltitudeText(double feet)
        {
            if (feet > TransitionAltitudeFeet)
                return $"FL{Math.Round(feet / 100.0):000}";
            var rounded = Math.Round(feet / 100.0) * 100.0;
            return $"{rounded:#,0} ft";
        }

        private static double Clamp(double v, double min, double max) => v < min ? min : v > max ? max : v;
    }
}

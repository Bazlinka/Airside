using System;
using System.Collections.Generic;
using Airside.Domain;

namespace Airside.Simulation
{
    public enum ParafieldPhase { Parked, TaxiOut, RunUp, Takeoff, Climb, Circuit, Approach, Landing, TaxiIn }

    public readonly struct ParafieldPose
    {
        public readonly ParafieldPoint Position;
        public readonly double Height, Yaw, Pitch, Bank, Speed;
        public readonly ParafieldPhase Phase;
        public ParafieldPose(ParafieldPoint p, double height, double yaw, double pitch, double bank,
            double speed, ParafieldPhase phase)
        { Position=p; Height=height; Yaw=yaw; Pitch=pitch; Bank=bank; Speed=speed; Phase=phase; }
        public bool EngineRunning => Phase != ParafieldPhase.Parked;
    }

    /// <summary>
    /// Independent, seeded trainer traffic. A reserved movement corridor serialises
    /// ground/runway crossings. Poses are clock functions, never integrated per frame.
    /// Saves, the player fleet and Adelaide's reservations are deliberately separate.
    /// </summary>
    public sealed class ParafieldTraffic
    {
        public const int AircraftCount = 4;
        public const double TaxiSpeed = 6, RotateSpeed = 27, Acceleration = 1.25;
        public const double RunUpSeconds = 25, CircuitHeight = 304.8;
        private readonly ISimulationClock _clock;
        private readonly ReservationTable _reservations;
        private readonly int _offset;
        private readonly StableId[] _owners = new StableId[AircraftCount];
        private readonly StableId[] _stands = new StableId[AircraftCount];
        private readonly StableId[][] _clearances = new StableId[AircraftCount][];
        private readonly ParafieldPoint[][] _out = new ParafieldPoint[AircraftCount][];
        private readonly ParafieldPoint[][] _in = new ParafieldPoint[AircraftCount][];
        private readonly double[] _outTimes = new double[AircraftCount], _inTimes = new double[AircraftCount];
        private readonly ParafieldPoint[] _air;
        private readonly double[] _heights, _airTimes;
        private readonly ParafieldPoint _takeoffStart, _lift, _touchdown, _exit;
        private readonly double _takeoffTime, _landingTime, _airTime;
        public double SlotSeconds { get; }
        public int ActiveAircraft { get; private set; } = -1;
        public ReservationTable Reservations => _reservations;

        public ParafieldTraffic(ISimulationClock clock, IRandomSource random, ReservationTable reservations = null)
        {
            _clock=clock ?? throw new ArgumentNullException(nameof(clock));
            _offset=(random ?? throw new ArgumentNullException(nameof(random))).NextInt(0,AircraftCount);
            _reservations=reservations ?? new ReservationTable();
            var runway=ParafieldLayout.MainRunway;
            var dx=(runway.A.X-runway.B.X)/runway.Length; var dz=(runway.A.Z-runway.B.Z)/runway.Length;
            ParafieldPoint Along(double forward, double left=0) => runway.B.Offset(dx*forward-dz*left,dz*forward+dx*left);
            _takeoffStart=Along(20);
            _takeoffTime=RotateSpeed/Acceleration;
            _lift=Along(20+RotateSpeed*RotateSpeed/(2*Acceleration));
            _touchdown=Along(120);
            _exit=ParafieldLayout.LandingExit;
            _landingTime=2*_touchdown.Distance(_exit)/(32+TaxiSpeed);
            _air=new[] { _lift, Along(1300), Along(4100), Along(4100,1400), Along(-2200,1400), Along(-2200), _touchdown };
            _heights=new[] { 0.0, 85.0, CircuitHeight, CircuitHeight, CircuitHeight, 120.0, 0.0 };
            _airTimes=new double[_air.Length-1];
            for(var i=0;i<_airTimes.Length;i++)
            {
                _airTimes[i]=Math.Max(_air[i].Distance(_air[i+1])/(i>=4 ? 32 : 38),
                    Math.Abs(_heights[i+1]-_heights[i])/3.7);
                _airTime+=_airTimes[i];
            }
            var maximum=0.0;
            for(var i=0;i<AircraftCount;i++)
            {
                _owners[i]=new StableId("parafield-trainer-"+i);
                _stands[i]=new StableId("YPPF-parking-"+i);
                _reservations.TryReplace(_owners[i],new[] {_stands[i]},out _);
                var resources=new List<StableId> { _stands[i], new("YPPF-taxi-corridor") };
                foreach(var r in ParafieldLayout.Runways) resources.Add(new StableId("YPPF-runway-"+r.Name));
                _clearances[i]=resources.ToArray();
                var outward=new List<ParafieldPoint> { ParafieldLayout.Parking[i],ParafieldLayout.ParkingLane[i],ParafieldLayout.ParkingLane[0] };
                outward.AddRange(ParafieldLayout.ApronToRunway);
                outward.Add(_takeoffStart);
                _out[i]=outward.ToArray();
                var inward=new List<ParafieldPoint>(ParafieldLayout.RunwayToApron);
                inward.Add(ParafieldLayout.ParkingLane[0]);
                inward.Add(ParafieldLayout.ParkingLane[i]);
                inward.Add(ParafieldLayout.Parking[i]);
                _in[i]=inward.ToArray();
                _outTimes[i]=PathLength(_out[i])/TaxiSpeed;
                _inTimes[i]=PathLength(_in[i])/TaxiSpeed;
                maximum=Math.Max(maximum,CycleSeconds(i));
            }
            SlotSeconds=Math.Ceiling(maximum+45);
            Update();
        }

        public double CycleSeconds(int index) => _outTimes[index]+RunUpSeconds+_takeoffTime+_airTime+_landingTime+_inTimes[index];

        public void Update()
        {
            var now=Math.Max(0,_clock.Now.ElapsedSeconds);
            var slot=(long)Math.Floor(now/SlotSeconds);
            var selected=(int)((slot+_offset)%AircraftCount);
            var active=now-slot*SlotSeconds<CycleSeconds(selected) ? selected : -1;
            if(active==ActiveAircraft) return;
            if(ActiveAircraft>=0) _reservations.TryReplace(_owners[ActiveAircraft],new[] {_stands[ActiveAircraft]},out _);
            ActiveAircraft=-1;
            if(active>=0 && _reservations.TryReplace(_owners[active],_clearances[active],out _)) ActiveAircraft=active;
        }

        public ParafieldPose Pose(int index, double preciseSeconds)
        {
            if(index<0 || index>=AircraftCount) throw new ArgumentOutOfRangeException(nameof(index));
            // Presentation may interpolate within this clock tick; it cannot cross an
            // unreserved slot boundary or make the simulation advance.
            var time=Math.Clamp(preciseSeconds,_clock.Now.ElapsedSeconds,_clock.Now.ElapsedSeconds+0.999999);
            if(index!=ActiveAircraft) return PoseOnPath(_out[index],0,0,ParafieldPhase.Parked);
            var t=time-Math.Floor(time/SlotSeconds)*SlotSeconds;
            if(t<_outTimes[index]) return PoseOnPath(_out[index],t*TaxiSpeed,TaxiSpeed,ParafieldPhase.TaxiOut);
            t-=_outTimes[index];
            var yaw=Yaw(_takeoffStart,_lift);
            if(t<RunUpSeconds) return new ParafieldPose(_takeoffStart,0,yaw,0,0,0,ParafieldPhase.RunUp);
            t-=RunUpSeconds;
            if(t<_takeoffTime) return new ParafieldPose(ParafieldPoint.Lerp(_takeoffStart,_lift,t*t/(_takeoffTime*_takeoffTime)),
                0,yaw,Math.Max(0,(t/_takeoffTime-.8)*25),0,Acceleration*t,ParafieldPhase.Takeoff);
            t-=_takeoffTime;
            if(t<_airTime) return AirPose(t);
            t-=_airTime;
            if(t<_landingTime)
            {
                var u=t/_landingTime;
                var total=(32+TaxiSpeed)*.5;
                var progress=(32*u-(32-TaxiSpeed)*u*u*.5)/total;
                return new ParafieldPose(ParafieldPoint.Lerp(_touchdown,_exit,progress),0,Yaw(_touchdown,_exit),0,0,
                    32-(32-TaxiSpeed)*u,ParafieldPhase.Landing);
            }
            t-=_landingTime;
            return PoseOnPath(_in[index],Math.Min(PathLength(_in[index]),t*TaxiSpeed),TaxiSpeed,ParafieldPhase.TaxiIn);
        }

        private ParafieldPose AirPose(double t)
        {
            var i=0;
            while(i<_airTimes.Length-1 && t>=_airTimes[i]) t-=_airTimes[i++];
            var u=Math.Clamp(t/_airTimes[i],0,1);
            var p=Curve(i,u); var lo=Curve(i,Math.Max(0,u-.002)); var hi=Curve(i,Math.Min(1,u+.002));
            var heading=Yaw(lo,hi);
            var prior=Yaw(Curve(i,Math.Max(0,u-.004)),lo);
            var delta=(heading-prior+540)%360-180;
            // Linear climb/descent heights are bounded by the speed-derived leg times.
            var height=_heights[i]+(_heights[i+1]-_heights[i])*u;
            var pitch=Math.Atan2(_heights[i+1]-_heights[i],_air[i].Distance(_air[i+1]))*180/Math.PI;
            return new ParafieldPose(p,height,heading,pitch,Math.Clamp(-delta*14,-22,22),i>=4 ? 32 : 38,
                i<2 ? ParafieldPhase.Climb : i>=4 ? ParafieldPhase.Approach : ParafieldPhase.Circuit);
        }

        private ParafieldPoint Curve(int i,double t)
        {
            var a=_air[Math.Max(0,i-1)]; var b=_air[i]; var c=_air[i+1]; var d=_air[Math.Min(_air.Length-1,i+2)];
            double C(double v0,double v1,double v2,double v3) =>
                .5*((2*v1)+(-v0+v2)*t+(2*v0-5*v1+4*v2-v3)*t*t+(-v0+3*v1-3*v2+v3)*t*t*t);
            return new ParafieldPoint(C(a.X,b.X,c.X,d.X),C(a.Z,b.Z,c.Z,d.Z));
        }

        private static double PathLength(ParafieldPoint[] path)
        { var sum=0.0; for(var i=1;i<path.Length;i++) sum+=path[i-1].Distance(path[i]); return sum; }

        private static double Yaw(ParafieldPoint a,ParafieldPoint b) => Math.Atan2(b.X-a.X,b.Z-a.Z)*180/Math.PI;

        private static ParafieldPose PoseOnPath(ParafieldPoint[] path,double distance,double speed,ParafieldPhase phase)
        {
            for(var i=1;i<path.Length;i++)
            {
                var length=path[i-1].Distance(path[i]);
                if(distance<=length || i==path.Length-1)
                    return new ParafieldPose(ParafieldPoint.Lerp(path[i-1],path[i],Math.Clamp(distance/Math.Max(length,.001),0,1)),
                        0,Yaw(path[i-1],path[i]),0,0,speed,phase);
                distance-=length;
            }
            return default;
        }
    }
}

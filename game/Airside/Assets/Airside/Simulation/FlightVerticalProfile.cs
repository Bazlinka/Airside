using System;
using System.Collections.Generic;

namespace Airside.Simulation
{
    /// <summary>Immutable altitude schedule with continuous height and vertical speed. Each Hermite band
    /// integrates its two endpoint rates; a final 600-ft capture eases to/from zero rate at cruise.</summary>
    public sealed class FlightVerticalProfile
    {
        private readonly Segment[] _segments;
        public double Seconds { get; }
        private readonly double _start, _end;
        public FlightVerticalProfile(double start, double end, FlightOperatingProfile profile, bool descent, bool endLevel = false)
        {
            _start=start; _end=end;
            var points=new List<double>{start};
            var capture=descent ? Math.Max(end,start-600) : Math.Max(start,end-600);
            if(descent) points.Add(capture);
            foreach(var band in descent ? new[]{24000.0,15000,10000,5000} : new[]{5000.0,10000,15000,24000})
                if(band>Math.Min(start,end) && band<Math.Max(start,end) && (descent ? band<capture : band<capture)) points.Add(band);
            if(!descent) points.Add(capture);
            else if(endLevel)
            {
                var endCapture=Math.Min(capture,end+600);
                points.RemoveAll(h=>h> end && h<endCapture);
                points.Add(endCapture);
            }
            points.Add(end);
            var segments=new List<Segment>();
            var time=0.0;
            for(var i=1;i<points.Count;i++)
            {
                var a=points[i-1];var b=points[i];if(Math.Abs(b-a)<.0001) continue;
                var r0=(descent ? profile.DescentRate(a) : profile.ClimbRate(a))/60;
                var r1=(descent ? profile.DescentRate(b) : profile.ClimbRate(b))/60;
                if(descent && i==1) r0=0;
                if((!descent || endLevel) && i==points.Count-1) r1=0;
                var seconds=2*Math.Abs(b-a)/Math.Max(.001,r0+r1);
                segments.Add(new Segment(a,b,descent ? -r0:r0,descent ? -r1:r1,time,seconds));time+=seconds;
            }
            _segments=segments.ToArray();Seconds=time;
        }
        public double Height(double seconds)
        {
            if(seconds<=0) return _start;
            foreach(var s in _segments) if(seconds<s.Start+s.Seconds) return s.Height(seconds);
            return _end;
        }
        public double Rate(double seconds)
        {
            if(seconds<0 || seconds>=Seconds) return 0;
            foreach(var s in _segments) if(seconds<s.Start+s.Seconds) return s.Rate(seconds)*60;
            return 0;
        }
        private readonly struct Segment
        {
            public Segment(double a,double b,double r0,double r1,double start,double seconds)
            { A=a;B=b;R0=r0;R1=r1;Start=start;Seconds=seconds; }
            private double A {get;} private double B {get;} private double R0 {get;} private double R1 {get;}
            public double Start {get;} public double Seconds {get;}
            public double Height(double time)
            {
                var u=Math.Clamp((time-Start)/Seconds,0,1);
                return (2*u*u*u-3*u*u+1)*A+(-2*u*u*u+3*u*u)*B
                    +(u*u*u-2*u*u+u)*Seconds*R0+(u*u*u-u*u)*Seconds*R1;
            }
            public double Rate(double time)
            {
                var u=Math.Clamp((time-Start)/Seconds,0,1);
                return ((6*u*u-6*u)*A+(-6*u*u+6*u)*B)/Seconds
                    +(3*u*u-4*u+1)*R0+(3*u*u-2*u)*R1;
            }
        }
    }
}

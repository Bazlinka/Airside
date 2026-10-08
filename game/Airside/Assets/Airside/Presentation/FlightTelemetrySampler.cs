using System;
using Airside.Simulation;

namespace Airside.Presentation
{
    /// <summary>Read actual aircraft motion in simulation seconds, cancelling floating-origin changes.
    /// Sampling works at accelerated clocks too; entering/switching the camera never advances the flight.</summary>
    public sealed class FlightTelemetrySampler
    {
        private double _x,_z,_height,_time;
        private bool _ready;
        public double GroundKnots {get;private set;}
        public double VerticalMetresPerSecond {get;private set;}
        public void Reset(double renderX,double renderZ,double originX,double originZ,double height,double time)
        {
            _x=renderX+originX;_z=renderZ+originZ;_height=height;_time=time;_ready=true;
            GroundKnots=VerticalMetresPerSecond=0;
        }
        public void Sample(double renderX,double renderZ,double originX,double originZ,double height,double time)
        {
            var x=renderX+originX;var z=renderZ+originZ;var dt=time-_time;
            if(!_ready || dt<0 || dt>30)
            {Reset(renderX,renderZ,originX,originZ,height,time);return;}
            if(dt<=0) return;
            var dx=x-_x;var dz=z-_z;
            var speed=Math.Sqrt(dx*dx+dz*dz)/dt/CircuitProfile.KnotsToMetresPerSecond;
            GroundKnots+=(speed-GroundKnots)*(1-Math.Exp(-6*dt));
            VerticalMetresPerSecond+=((height-_height)/dt-VerticalMetresPerSecond)*(1-Math.Exp(-10*dt));
            _x=x;_z=z;_height=height;_time=time;
        }
    }
}

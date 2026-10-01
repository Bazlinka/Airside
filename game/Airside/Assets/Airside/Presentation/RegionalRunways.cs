using System;
using Airside.Simulation;
namespace Airside.Presentation
{
    public readonly struct RegionalRunway
    {
        public readonly string Code;
        public readonly double Ax,Az,Bx,Bz,Elevation,Width;
        public RegionalRunway(string code,double alat,double alon,double blat,double blon,double elevation,double width)
        { Code=code; YpadFrame.ToWorld(alat,alon,out Ax,out Az);YpadFrame.ToWorld(blat,blon,out Bx,out Bz);Elevation=elevation;Width=width; }
        public double Length => Math.Sqrt((Bx-Ax)*(Bx-Ax)+(Bz-Az)*(Bz-Az));
    }
    public static class RegionalRunways
    {
        // OurAirports public-domain snapshot docs/data/sa-flight-runways-v01.json.
        public static readonly RegionalRunway[] All = new RegionalRunway[]
        {
            new RegionalRunway("BHQ",-32.008998871,141.460998535,-31.996299744,141.483001709,291.998,29.870),
            new RegionalRunway("CPD",-29.044300079,134.714004517,-29.035900116,134.725006104,225.552,29.870),
            new RegionalRunway("CED",-32.127799988,133.697998047,-32.133399963,133.714996338,23.470,29.870),
            new RegionalRunway("KGC",-35.723622000,137.521743000,-35.708409000,137.529049000,7.315,29.870),
            // MGB endpoints estimated from airport centre, sourced heading and length.
            new RegionalRunway("MGB",-37.736947652,140.780561000,-37.751816348,140.780561000,0.000,24.384),
            new RegionalRunway("PLO",-34.612598419,135.876998901,-34.599700928,135.880996704,10.973,29.870),
            new RegionalRunway("WYA",-33.051101685,137.518005371,-33.066299438,137.520004272,12.497,45.110),
        };
        public static bool TryGet(string code,out RegionalRunway runway)
        { foreach(var r in All) if(r.Code==code){runway=r;return true;} runway=default;return false; }
        public static double Ground(double x,double z,double height)
        {
            foreach(var r in All)
            {
                var dx=r.Bx-r.Ax;var dz=r.Bz-r.Az;
                var t=Math.Clamp(((x-r.Ax)*dx+(z-r.Az)*dz)/(dx*dx+dz*dz),0,1);
                var distance=Math.Sqrt(Math.Pow(x-r.Ax-t*dx,2)+Math.Pow(z-r.Az-t*dz,2));
                if(distance<3000){var u=Math.Clamp((distance-1500)/1500,0,1);return r.Elevation+(height-r.Elevation)*u*u*(3-2*u);}
            }
            return height;
        }
    }
}

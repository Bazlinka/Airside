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
        // Existing MAP-002 OurAirports catalogue; the longest Australian runway owns flight paths.
        public static readonly RegionalRunway[] All = Build(false);
        public static readonly RegionalRunway[] Strips = Build(true);
        private static RegionalRunway[] Build(bool everyStrip)
        {
            var result = new System.Collections.Generic.List<RegionalRunway>();
            foreach (var r in MapGeographyData.Runways)
            {
                if (r.Code == "ADL" || !FlightWorldGrid.Covered(r.Lat1, r.Lon1)) continue;
                var runway = new RegionalRunway(r.Code, r.Lat1, r.Lon1, r.Lat2, r.Lon2,
                    (MapGeography.ElevationFt(r.Code) ?? 0) * .3048, r.WidthM);
                var index = result.FindIndex(item => item.Code == r.Code);
                if (everyStrip || index < 0) result.Add(runway);
                else if (runway.Length > result[index].Length) result[index] = runway;
            }
            return result.ToArray();
        }
        public static bool TryGet(string code,out RegionalRunway runway)
        { foreach(var r in All) if(r.Code==code){runway=r;return true;} runway=default;return false; }
        public static double Ground(double x,double z,double height)
        {
            var distance=800.0;var elevation=height;
            foreach(var runway in Strips)
            {
                if(x<Math.Min(runway.Ax,runway.Bx)-800 || x>Math.Max(runway.Ax,runway.Bx)+800
                    || z<Math.Min(runway.Az,runway.Bz)-800 || z>Math.Max(runway.Az,runway.Bz)+800) continue;
                var d=FlightWorldDetail.RunwayDistance(x,z,runway);
                if(d<distance){distance=d;elevation=runway.Elevation;}
            }
            if(distance>=800) return height;
            var u=Math.Clamp((distance-400)/400,0,1);
            return elevation+(height-elevation)*u*u*(3-2*u);
        }
    }
}

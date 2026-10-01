#!/usr/bin/env python3
"""Generate the regional strip catalogue from OurAirports public-domain CSV snapshots.
Usage: python3 scripts/generate-sa-flight-runways.py airports.csv runways.csv
Missing MGB thresholds are estimated from airport centre, sourced length/heading.
"""
import csv,json,math,sys
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
airports=[r for r in csv.DictReader(open(sys.argv[1])) if r['iata_code'] in {'KGC','PLO','WYA','MGB','CED','CPD','BHQ'}]
runways=list(csv.DictReader(open(sys.argv[2])))
selected=[];constructors=[]
for a in airports:
 r=max((r for r in runways if r['airport_ident']==a['ident'] and r['closed']=='0'),key=lambda r:(r['surface'] in ('ASP','PER'),float(r['length_ft'])))
 selected.append({'airport':{k:a[k] for k in ['iata_code','ident','latitude_deg','longitude_deg','elevation_ft']},'runway':r})
 elev=float(a['elevation_ft'] or 0)*.3048;width=float(r['width_ft'] or 80)*.3048
 if r['le_latitude_deg'] and r['he_latitude_deg']:
  alat,alon,blat,blon=map(float,[r['le_latitude_deg'],r['le_longitude_deg'],r['he_latitude_deg'],r['he_longitude_deg']])
 else:
  lat,lon=map(float,[a['latitude_deg'],a['longitude_deg']]);half=float(r['length_ft'])*.3048/2
  heading=math.radians(float(r['le_heading_degT'] or int(r['le_ident'])*10))
  north=math.cos(heading)*half;east=math.sin(heading)*half
  alat,blat=lat-north/110574,lat+north/110574;alon,blon=lon-east/(111320*math.cos(math.radians(lat))),lon+east/(111320*math.cos(math.radians(lat)))
  constructors.append('            // MGB endpoints estimated from airport centre, sourced heading and length.')
 constructors.append(f'            new RegionalRunway("{a["iata_code"]}",{alat:.9f},{alon:.9f},{blat:.9f},{blon:.9f},{elev:.3f},{width:.3f}),')
(ROOT/'docs/data/sa-flight-runways-v01.json').write_text(json.dumps({'source':'https://davidmegginson.github.io/ourairports-data/','licence':'Public domain','accessed':'2026-10-01','selected':selected},indent=2)+'\n')
code='''using System;
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
CONSTRUCTORS
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
'''.replace('CONSTRUCTORS','\n'.join(constructors))
(ROOT/'game/Airside/Assets/Airside/Presentation/RegionalRunways.cs').write_text(code)

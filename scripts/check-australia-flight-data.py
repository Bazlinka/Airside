#!/usr/bin/env python3
"""Offline geometry/grid sanity and byte-identical packaged mirrors for DAT-AU-FLIGHT.
No network, Unity, screenshots or performance claims. Run after the bake or artifact import.
"""
import hashlib,json,math,struct,zlib
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
ART=ROOT/'game/Airside/Assets/Airside/Art/Terrain'
MIRROR=ROOT/'game/Airside/Assets/StreamingAssets/Airside/Art/Terrain'
CODES='asp bhq bne cbr ced cns cpd drw hba kgc mel mgb mql ool per plo syd wya'.split()
def grid(path):
 data=path.read_bytes();magic,version,w,h,west,south,step=struct.unpack('<4siii3d',data[:40])
 assert version==1 and 0<w<=4096 and 0<h<=4096 and all(math.isfinite(x) for x in (west,south,step)) and step>0,path
 if magic==b'SATG':
  assert len(data)==40+w*h*2,path
  values=struct.unpack('<'+'h'*(w*h),data[40:])
 else:
  assert magic==b'SALC',path
  values=zlib.decompress(data[40:],-15);assert len(values)==w*h and max(values)<8,path
 return w,h,west,south,step,values

def sample(path,lat,lon):
 w,h,west,south,step,values=grid(path)
 x,z=int((lon-west)/step),int((lat-south)/step)
 assert 0<=x<w and 0<=z<h,(path,lat,lon)
 return values[z*w+x]

def main():
 files=[ART/'dem_australia_v01.bin',ART/'landcover_australia_v01.bin']
 for code in CODES: files.extend([ART/f'dem_approach_{code}_v01.bin',ART/f'landcover_approach_{code}_v01.bin',ART/f'airport_{code}_v01.json'])
 for path in files:
  assert path.exists(),f'Missing {path.name}'
  assert path.read_bytes()==(MIRROR/path.name).read_bytes(),path.name+' mirror mismatch'
  if path.suffix=='.bin': grid(path)
  else:
   data=json.loads(path.read_text());assert data['code']==path.name[8:11].upper() and data['features'],path
   for f in data['features']:
    assert math.isfinite(f['width']) and math.isfinite(f['height']) and len(f['points'])>=2,(path,f)
    for p in f['points']: assert -45<=p['lat']<=-9 and 112<=p['lon']<=155,(path,p)
 field=sample(ART/'dem_approach_hba_v01.bin',-42.8369,147.5128)
 mountain=sample(ART/'dem_approach_hba_v01.bin',-42.895,147.237)
 bay=sample(ART/'landcover_approach_hba_v01.bin',-42.87,147.55)
 assert -5<=field<=70,('Hobart field metres',field)
 assert mountain>900,('kunanyi relief metres',mountain)
 assert bay==0,('Frederick Henry Bay class',bay)
 print(f'PASS: {len(files)} geodata files, grids and OSM coordinates valid; packaged mirrors exact; HBA field {field} m, kunanyi {mountain} m, bay water class {bay}.')
 manifest=[]
 for p in files:manifest.append({'file':p.name,'bytes':p.stat().st_size,'sha256':hashlib.sha256(p.read_bytes()).hexdigest()})
 (ROOT/'docs/data/australia-flight-world-shipped-v01.json').write_text(json.dumps({'files':manifest,'fallback':'Offline national/SA grids and mapped runway strips; no runtime HTTP'},indent=2)+'\n')
if __name__=='__main__':main()

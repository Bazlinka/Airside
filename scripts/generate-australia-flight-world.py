#!/usr/bin/env python3
"""Bake offline Australian cruise and airport-approach terrain (DAT-AU-FLIGHT).
Requires numpy, pillow, rasterio. Cached HTTPS reads only; transient failures abort.
Cruise: AWS Terrarium zoom 6 (~2 km), WorldCover class at .025 degrees.
Approach: Terrarium zoom 11 (~60 m), WorldCover at .001 degrees over +/- .35 deg.
Airport OSM snapshots: aeroways/buildings/major roads in +/- .055 degrees.
Licences and exact provenance: docs/data/australia-flight-world-v01.json.
"""
import concurrent.futures as futures
import hashlib, io, json, math, re, struct, time, urllib.request, urllib.error, zlib
from pathlib import Path
import numpy as np
from PIL import Image
import rasterio
from rasterio.enums import Resampling
from rasterio.windows import from_bounds
ROOT=Path(__file__).resolve().parents[1]
CACHE=ROOT/'work/cache/australia-flight'; CACHE.mkdir(parents=True,exist_ok=True)
ART=ROOT/'game/Airside/Assets/Airside/Art/Terrain'
MIRROR=ROOT/'game/Airside/Assets/StreamingAssets/Airside/Art/Terrain'
TERRAIN='https://s3.amazonaws.com/elevation-tiles-prod/terrarium/{}/{}/{}.png'
COVER='https://esa-worldcover.s3.eu-central-1.amazonaws.com/v200/2021/map/ESA_WorldCover_10m_2021_v200_{}_Map.tif'
LUT=np.zeros(256,dtype='u1')
for source,target in {10:1,20:2,30:3,40:4,50:5,60:6,70:6,80:0,90:7,95:7,100:6}.items(): LUT[source]=target
MANIFEST={'date':'2026-10-08','elevation_source':TERRAIN,'elevation_terms':'Mapzen terrain tiles CC BY 4.0; underlying Australia elevation USGS GMTED2010/SRTM, NASA and NOAA ETOPO1 (public domain). Attribution: Mapzen, USGS, NASA, NOAA. https://github.com/tilezen/joerd/blob/master/docs/attribution.md','cover_source':COVER,'cover_terms':'CC BY 4.0; © ESA WorldCover project 2021 / Contains modified Copernicus Sentinel data (2021) processed by ESA WorldCover consortium','osm_terms':'ODbL 1.0, © OpenStreetMap contributors','cost':'$0, public HTTPS','fallback':'SA DEM/cover, Natural Earth coastline and authored height palette; no runtime network','files':[]}

def write(name,blob):
 for root in (ART,MIRROR): (root/name).write_bytes(blob)
 MANIFEST['files'].append({'file':name,'bytes':len(blob),'sha256':hashlib.sha256(blob).hexdigest()})
 print('Wrote',name,len(blob),flush=True)

def fetch(url,cache):
 if cache.exists(): return cache.read_bytes()
 for attempt in range(6):
  try:
   with urllib.request.urlopen(urllib.request.Request(url,headers={'User-Agent':'AirsideTerrainBake/1.0'}),timeout=45) as response: data=response.read()
   cache.write_bytes(data);return data
  except (urllib.error.URLError,TimeoutError):
   if attempt==5: raise RuntimeError("HTTPS resource unavailable: "+url)
   time.sleep(min(10,(attempt+1)*2))

def merc(lat,lon,zoom):
 scale=2**zoom
 return (lon+180)/360*scale,(1-np.arcsinh(np.tan(np.radians(lat)))/np.pi)/2*scale

def elevation(bounds,step,zoom):
 west,south,east,north=bounds
 w=round((east-west)/step)+1;h=round((north-south)/step)+1
 xs,_=merc(0,west+np.arange(w)*step,zoom);_,ys=merc(south+np.arange(h)*step,0,zoom)
 result=np.zeros((h,w),dtype='<i2')
 pairs=[(int(x),int(y)) for y in np.unique(ys.astype(int)) for x in np.unique(xs.astype(int))]
 def read(pair):
  x,y=pair; data=fetch(TERRAIN.format(zoom,x,y)+'?airside=20261008',CACHE/f'dem-{zoom}-{x}-{y}.png')
  rgba=np.array(Image.open(io.BytesIO(data))).astype('f4')
  return x,y,rgba[:,:,0]*256+rgba[:,:,1]+rgba[:,:,2]/256-32768
 with futures.ThreadPoolExecutor(max_workers=4) as pool:
  for x,y,data in pool.map(read,pairs):
   cols=np.flatnonzero(xs.astype(int)==x);rows=np.flatnonzero(ys.astype(int)==y)
   xx=(xs[cols]-x)*256;yy=(ys[rows]-y)*256
   # Bilinear resample quantized tiles. Negative seabed is kept until land/water masking in runtime.
   ix=np.minimum(xx.astype(int),254);iy=np.minimum(yy.astype(int),254)
   fx=xx-ix;fy=yy-iy
   a=data[np.ix_(iy,ix)]*(1-fx)+data[np.ix_(iy,ix+1)]*fx
   b=data[np.ix_(iy+1,ix)]*(1-fx)+data[np.ix_(iy+1,ix+1)]*fx
   result[np.ix_(rows,cols)]=np.clip(np.rint(a*(1-fy[:,None])+b*fy[:,None]),-32000,32000).astype('<i2')
 return struct.pack('<4siii3d',b'SATG',1,w,h,west,south,step)+result.tobytes()

class RangeFile(io.RawIOBase):
 """Verified urllib HTTPS ranges, preserving cloud proxy/CA trust for GDAL's Python opener."""
 def __init__(self,url):
  self.url=url; self.pos=0;self.blocks={}
  with urllib.request.urlopen(urllib.request.Request(url,method='HEAD'),timeout=35) as response: self.length=int(response.headers['Content-Length'])
 def readable(self): return True
 def seekable(self): return True
 def tell(self): return self.pos
 def seek(self,offset,whence=0):
  self.pos=offset if whence==0 else self.pos+offset if whence==1 else self.length+offset
  return self.pos
 def read(self,size=-1):
  end=self.length if size<0 else min(self.length,self.pos+size);pieces=[]
  while self.pos<end:
   block=self.pos//65536
   if block not in self.blocks:
    start=block*65536;stop=min(self.length-1,start+65535)
    request=urllib.request.Request(self.url,headers={'Range':f'bytes={start}-{stop}'})
    for attempt in range(4):
     try:
      with urllib.request.urlopen(request,timeout=45) as response:
       data=response.read()
       if response.status!=206: raise RuntimeError('Range request ignored: '+self.url)
      break
     except (urllib.error.URLError,TimeoutError):
      if attempt==3: raise
      time.sleep(min(10,(attempt+1)*2))
    self.blocks[block]=data
   data=self.blocks[block];offset=self.pos%65536;n=min(end-self.pos,len(data)-offset)
   if n<=0: break
   pieces.append(data[offset:offset+n]);self.pos+=n
  return b''.join(pieces)
 def readinto(self,buffer):
  data=self.read(len(buffer));buffer[:len(data)]=data;return len(data)

def open_range(path,mode='rb'):
 if not str(path).startswith('https://') or not str(path).endswith('.tif'): raise FileNotFoundError(path)
 return RangeFile(path)

def cover(bounds,step):
 west,south,east,north=bounds;w=round((east-west)/step);h=round((north-south)/step)
 result=np.zeros((h,w),dtype='u1')
 tasks=[(la,lo) for la in range(math.floor(south/3)*3,math.ceil(north/3)*3,3) for lo in range(math.floor(west/3)*3,math.ceil(east/3)*3,3)]
 def read(pair):
  la,lo=pair;name=f'S{abs(la):02d}E{lo:03d}'
  l,r=max(west,lo),min(east,lo+3);b,t=max(south,la),min(north,la+3)
  col=round((l-west)/step);row=round((north-t)/step);nw=round((r-l)/step);nh=round((t-b)/step)
  cache=CACHE/f'cover-{name}-{l:.4f}-{b:.4f}-{r:.4f}-{t:.4f}-{step}.npy'
  if cache.exists(): return row,col,np.load(cache)
  url=COVER.format(name)
  try:
   with urllib.request.urlopen(urllib.request.Request(url,method='HEAD'),timeout=35): pass
  except urllib.error.HTTPError as e:
   if e.code!=404: raise
   data=np.zeros((nh,nw),dtype='u1')
  else:
   with rasterio.Env(GDAL_HTTP_CAINFO='/etc/ssl/certs/ca-certificates.crt',GDAL_DISABLE_READDIR_ON_OPEN='EMPTY_DIR',GDAL_HTTP_TIMEOUT='40',GDAL_HTTP_MAX_RETRY='2',CPL_VSIL_CURL_ALLOWED_EXTENSIONS='.tif'):
    with rasterio.open(url,opener=open_range) as src:
     data=LUT[src.read(1,window=from_bounds(l,b,r,t,src.transform),out_shape=(nh,nw),resampling=Resampling.mode)]
  np.save(cache,data);return row,col,data
 with futures.ThreadPoolExecutor(max_workers=6) as pool:
  for row,col,data in pool.map(read,tasks):
   result[row:row+data.shape[0],col:col+data.shape[1]]=data
 compressor=zlib.compressobj(9,zlib.DEFLATED,-15);raw=np.flipud(result).tobytes()
 return struct.pack('<4siii3d',b'SALC',1,w,h,west,south,step)+compressor.compress(raw)+compressor.flush()

def airports():
 code=(ROOT/'game/Airside/Assets/Airside/Presentation/MapGeographyData.cs').read_text()
 rows=re.findall(r'\("([A-Z]{3})", "[^"]+", "[^"]+", ([\d.-]+), ([\d.-]+), ([\d.-]+), ([\d.-]+), (\d+), (\d+)\)',code)
 selected={}
 for c,la,lo,lb,lob,length,width in rows:
  la,lo,lb,lob=map(float,(la,lo,lb,lob))
  if c!='ADL' and -44<=la<=-10 and 112<=lo<=154 and (c not in selected or int(length)>selected[c]['length']):
   selected[c]={'code':c,'lat':(la+lb)/2,'lon':(lo+lob)/2,'length':int(length)}
 return list(selected.values())

def osm(airport):
 c=airport['code'];la,lo=airport['lat'],airport['lon'];r=.055
 query=f'[out:json][timeout:35];(way["aeroway"]({la-r},{lo-r},{la+r},{lo+r});way["building"]({la-.035},{lo-.035},{la+.035},{lo+.035});way["highway"~"motorway|trunk|primary|secondary"]({la-r},{lo-r},{la+r},{lo+r}););out tags geom;'
 import urllib.parse
 cache=CACHE/f'osm-{c}.json'
 data=json.loads(fetch('https://overpass-api.de/api/interpreter?'+urllib.parse.urlencode({'data':query}),cache))
 features=[]
 for element in data['elements']:
  tags=element.get('tags',{});points=element.get('geometry',[])
  if len(points)<2: continue
  kind=tags.get('aeroway') or ('building' if 'building' in tags else 'road')
  if kind not in ('runway','taxiway','apron','terminal','hangar','building','road'): continue
  width=float((re.findall(r'\d+(?:\.\d+)?',tags.get('width','')) or [23 if kind=='taxiway' else 10])[0])
  height=float((re.findall(r'\d+(?:\.\d+)?',tags.get('height','')) or [float(tags.get('building:levels','1'))*3.5 if tags.get('building:levels','1').isdigit() else 7])[0])
  features.append({'kind':kind,'width':min(width,100),'height':min(height,80),'points':[{'lat':p['lat'],'lon':p['lon']} for p in points]})
 features.sort(key=lambda f: (f['kind']=='building', f['kind']=='road'))
 write(f'airport_{c.lower()}_v01.json',json.dumps({'code':c,'features':features},separators=(',',':')).encode())

def main():
 import sys
 group=sys.argv[1] if len(sys.argv)>1 else 'all'
 if group in ('all','cruise'):
  bounds=(112.,-45.,155.,-9.)
  write('dem_australia_v01.bin',elevation(bounds,.025,6))
  write('landcover_australia_v01.bin',cover(bounds,.025))
 if group in ('all','airports'):
  for a in airports():
   la,lo=a['lat'],a['lon'];bounds=(round(lo-.35,3),round(la-.35,3),round(lo+.35,3),round(la+.35,3))
   write(f'dem_approach_{a["code"].lower()}_v01.bin',elevation(bounds,.001,11))
   write(f'landcover_approach_{a["code"].lower()}_v01.bin',cover(bounds,.001))
 if group in ('all','osm'):
  for a in airports(): osm(a)
 path=ROOT/f'docs/data/australia-flight-world-{group}-v01.json';path.write_text(json.dumps(MANIFEST,indent=2)+'\n')
if __name__=='__main__': main()

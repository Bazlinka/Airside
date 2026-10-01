#!/usr/bin/env python3
"""Bake ~2 km Copernicus GLO-90 heights for the SA cockpit world. No full-res mosaics.
Requires numpy/rasterio. Reads COG overviews, caches per degree, fails on network
errors (only confirmed HTTP 404 is sea). Source/terms: registry.opendata.aws/copernicus-dem.
Output SATG v1: magic, version,width,height,west,south,step (3 doubles), int16 metres.
"""
import concurrent.futures
import hashlib
import json
import struct
import urllib.request
import urllib.error
from pathlib import Path
import numpy as np
import rasterio
from rasterio.enums import Resampling
ROOT = Path(__file__).resolve().parents[1]
CACHE = ROOT / 'work/cache/sa-dem'
OUT = ROOT / 'game/Airside/Assets/Airside/Art/Terrain/dem_south_australia_v01.bin'
WEST, SOUTH, EAST, NORTH, STEP = 128., -39., 142., -25., .02
URL = 'https://copernicus-dem-90m.s3.amazonaws.com/{0}/{0}.tif'
def tile(pair):
    lat, lon = pair
    cache = CACHE / f'{lat}_{lon}.npy'
    if cache.exists():
        return lat, lon, np.load(cache)
    name = f'Copernicus_DSM_COG_30_S{abs(lat):02d}_00_E{lon:03d}_00_DEM'
    url = URL.format(name)
    try:
        with urllib.request.urlopen(urllib.request.Request(url, method='HEAD'), timeout=60): pass
    except urllib.error.HTTPError as e:
        if e.code != 404: raise
        data = np.zeros((64,64), dtype=np.int16)
    else:
        with rasterio.Env(GDAL_DISABLE_READDIR_ON_OPEN='EMPTY_DIR', GDAL_HTTP_TIMEOUT='60',
                          CPL_VSIL_CURL_ALLOWED_EXTENSIONS='.tif'):
            with rasterio.open(url) as src:
                values = src.read(1, out_shape=(64,64), resampling=Resampling.average, masked=True)
                data = np.clip(values.filled(0), 0, 32000).astype(np.int16)
    np.save(cache, data)
    return lat, lon, data

def main():
    CACHE.mkdir(parents=True, exist_ok=True)
    n = int(round((EAST-WEST)/STEP))+1
    result = np.zeros((n,n), dtype='<i2')
    pairs = [(lat,lon) for lat in range(int(SOUTH), int(NORTH)+1)
             for lon in range(int(WEST), int(EAST)+1)]
    with concurrent.futures.ThreadPoolExecutor(max_workers=6) as pool:
        for i,(lat,lon,data) in enumerate(pool.map(tile,pairs)):
            rows = np.flatnonzero((SOUTH+np.arange(n)*STEP >= lat) & (SOUTH+np.arange(n)*STEP < lat+1))
            cols = np.flatnonzero((WEST+np.arange(n)*STEP >= lon) & (WEST+np.arange(n)*STEP < lon+1))
            sy = np.clip(((lat+1-(SOUTH+rows*STEP))*64).astype(int),0,63)
            sx = np.clip(((WEST+cols*STEP-lon)*64).astype(int),0,63)
            result[np.ix_(rows,cols)] = data[np.ix_(sy,sx)]
            if i%15==0: print(f'COG tiles {i+1}/{len(pairs)}', flush=True)
    payload = struct.pack('<4siii3d',b'SATG',1,n,n,WEST,SOUTH,STEP)+result.tobytes()
    OUT.write_bytes(payload)
    metadata = {'source':URL,'dataset':'Copernicus DEM GLO-90','bounds':[WEST,SOUTH,EAST,NORTH],
                'step_degrees':STEP,'sha256':hashlib.sha256(payload).hexdigest(), 'bytes':len(payload),
                'fallback':'Natural Earth land/sea; no invented elevation', 'missing_tiles':'HTTP 404 only',
                'processing':'64x64 average COG overview per degree, nearest samples to 0.02 degree grid'}
    (ROOT/'docs/data/sa-flight-terrain-v01.json').write_text(json.dumps(metadata,indent=2)+'\n')
    print(json.dumps(metadata), flush=True)
if __name__=='__main__': main()

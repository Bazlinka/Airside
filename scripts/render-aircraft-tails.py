#!/usr/bin/env python3
"""Render all fifteen active runtime tails in side, rear and top contact sheets."""
import argparse
import importlib.util
from pathlib import Path
import numpy as np
from PIL import Image,ImageDraw

HERE=Path(__file__).resolve().parent

def module(file):
    spec=importlib.util.spec_from_file_location(file.replace('-','_'),HERE/file)
    m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m);return m


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('output',type=Path);args=parser.parse_args();args.output.mkdir(parents=True,exist_ok=True)
    thumbs=module('render-aircraft-thumbnails.py');models=module('refine-aircraft-tails.py').models()
    models.pop('A320_SOURCE')
    for view,az,el in [('side',90,0),('rear',150,18),('top',0,89)]:
        sheet=Image.new('RGB',(1800,1080),(245,245,245));draw=ImageDraw.Draw(sheet)
        for k,(cid,path) in enumerate(models.items()):
            parts=thumbs.load_parts(str(path));hull=dict(parts)['fuselage']
            # Keep the Bell's whole boom; all other views crop to the aft 30%.
            lo=hull[:,:,2].min();hi=hull[:,:,2].max();cut=lo+(hi-lo)*.30
            pieces=[];colours=[]
            for name,tris in parts:
                selected=tris[tris[:,:,2].max(axis=1)<cut]
                if len(selected):
                    pieces.append(selected);colours.append(np.tile(thumbs.colour(name),(len(selected),1)))
            im,_=thumbs.rasterise(np.concatenate(pieces),np.concatenate(colours),
                thumbs.view_matrix(az,el),360,330,1,margin=.07)
            bg=Image.new('RGB',im.size,(239,120,230));bg.paste(im,(0,0),im)
            x=k%5*360;y=k//5*360;sheet.paste(bg,(x,y+25));draw.text((x+10,y+5),cid,fill='black')
        path=args.output/(view+'.png');sheet.save(path);print(path,flush=True)

if __name__=='__main__':main()

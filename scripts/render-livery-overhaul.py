#!/usr/bin/env python3
"""Offline geometry proof sheet, not a Unity gameplay capture (ADR 0204)."""
import argparse
import importlib.util
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont, ImageOps

HERE = Path(__file__).resolve().parent

def module(name):
    spec = importlib.util.spec_from_file_location(name, HERE / name)
    value = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(value)
    return value


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('output', type=Path)
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    paint = module('render-aircraft-paint.py')
    finish = module('finish-aircraft-liveries.py')
    thumbs = paint.thumbs
    font = ImageFont.truetype(paint.FONT, 17)
    presets = [('Coastline','#0F8B8D'),('Southern Cross','#1F3A93'),('Outback','#B8742A'),
               ('Gulf','#3A8DDE'),('Redgum','#70415C')]
    sheet = Image.new('RGB', (1200, 7*260), '#EEF1EC')
    for i,(cid,model,_) in enumerate(thumbs.MODELS):
        parts = thumbs.load_parts(str(Path(thumbs.ART)/model))
        path = args.output / (cid+'.png')
        thumbs.render_view(parts,str(path),90,8,width=600,height=220,supersample=1,
                           colour_fn=paint.paint_colour(paint.hex_rgb(presets[i%5][1])))
        tile = ImageOps.mirror(Image.open(path)).convert('RGBA')
        x,y=(i%2)*600,(i//2)*260
        sheet.paste(tile,(x,y),tile)
        ImageDraw.Draw(sheet).text((x+15,y+228),cid+' · '+finish.PROFILES[cid][0],font=font,fill='#17242A')
        print(cid,flush=True)
    sheet.save(args.output/'all-aircraft.jpg',quality=90)
    sheet = Image.new('RGB',(1000,5*250),'#EEF1EC')
    parts = thumbs.load_parts(str(Path(thumbs.ART)/thumbs.MODELS[5][1]))
    for i,(label,hex_colour) in enumerate(presets):
        path=args.output/(label.lower().replace(' ','-')+'.png')
        thumbs.render_view(parts,str(path),90,8,width=1000,height=210,supersample=1,
                           colour_fn=paint.paint_colour(paint.hex_rgb(hex_colour)))
        tile=ImageOps.mirror(Image.open(path)).convert('RGBA')
        sheet.paste(tile,(0,i*250),tile)
        ImageDraw.Draw(sheet).text((20,i*250+218),label,font=font,fill='#17242A')
        print(label,flush=True)
    sheet.save(args.output/'five-presets.jpg',quality=90)

if __name__ == '__main__':
    main()

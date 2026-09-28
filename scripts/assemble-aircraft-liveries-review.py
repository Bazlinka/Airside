#!/usr/bin/env python3
"""Assemble labelled review sheets from unmodified Unity captures; never substitute geometry."""
from pathlib import Path
import argparse
import importlib.util
from PIL import Image, ImageDraw, ImageFont

ROOT=Path(__file__).resolve().parents[1]
spec=importlib.util.spec_from_file_location('finish',ROOT/'scripts/finish-aircraft-liveries.py')
finish=importlib.util.module_from_spec(spec);spec.loader.exec_module(finish)
PALETTES=[('coastline','Coastline Regional'),('emu','Emu Air'),('southern-cross','Southern Cross Link')]
FONT=next(p for p in ['/System/Library/Fonts/Supplemental/Arial.ttf','/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf'] if Path(p).exists())

def sheet(rows,source,destination,width=720):
    height=int(width*900/1440)+45
    result=Image.new('RGB',(width*3,height*len(rows)),(30,40,49))
    draw=ImageDraw.Draw(result);font=ImageFont.truetype(FONT,21)
    for r,cid in enumerate(rows):
        for c,(key,label) in enumerate(PALETTES):
            image=Image.open(source/key/(cid+'_front.png')).convert('RGB')
            image.thumbnail((width,height-45),Image.Resampling.LANCZOS)
            x,y=c*width,r*height
            result.paste(image,(x,y))
            draw.text((x+20,y+height-34),f'{cid}  /  {label}',font=font,fill=(233,240,242))
    result.save(destination,quality=93,subsampling=0)


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--input',type=Path,default=ROOT/'work/aircraft-liveries-review')
    parser.add_argument('--output',type=Path,default=ROOT/'docs/testing/aircraft-liveries-2026-09-28')
    args=parser.parse_args();args.output.mkdir(parents=True,exist_ok=True)
    for cid in finish.PROFILES: sheet([cid],args.input,args.output/(cid+'.jpg'))
    sheet(['ATR42','B38M','A359'],args.input,args.output/'livery-options.jpg',width=600)
    cards='\n'.join(f'<section id="{cid}"><h2>{cid} <span>{p[0]}</span></h2><a href="{cid}.jpg"><img src="{cid}.jpg" alt="{cid}: Coastline, Emu and Southern Cross liveries" loading="lazy"></a></section>' for cid,p in finish.PROFILES.items())
    options=''.join(f'<option value="{cid}">{cid} — {p[0]}</option>' for cid,p in finish.PROFILES.items())
    html='''<!doctype html><html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Airside / Aircraft liveries</title>
<style>body{margin:0;background:#1e2831;color:#edf1f3;font:16px system-ui}main{max-width:1500px;margin:auto;padding:40px 24px}h1{font-size:38px;margin:6px 0 14px}p{max-width:850px;line-height:1.6;color:#bbc9d3}nav{position:sticky;top:0;background:#1e2831ed;padding:16px 0;border-bottom:1px solid #496072}select{padding:10px;color:#edf1f3;background:#2e3d49;border:1px solid #617989;font:inherit}h2{font-size:23px;margin-top:40px}h2 span{font-weight:400;color:#a8bdcb;margin-left:16px}img{width:100%;height:auto;border-radius:6px}section{scroll-margin-top:85px}small{color:#b8c7d0}</style>
<main><small>AIRSIDE · ORIGINAL AIRCRAFT PAINT · 28 SEPTEMBER 2026</small><h1>Thirteen aircraft. Three colourways each.</h1><p>Coastline Regional in teal and sand. Emu Air in ochre and slate. Southern Cross Link in navy and sand. Every layout follows the aircraft's own skin and retains your airline's name and chosen colour.</p><p>These are actual Unity renders using the game's aircraft builder and materials. Studio lighting; see README.md for tests and separate packaged playtest results. Click a sheet for the full-size image.</p>
<nav><label for="aircraft">Jump to aircraft </label><select id="aircraft" onchange="document.getElementById(this.value).scrollIntoView({behavior:'smooth'})">'''+options+'</select></nav>'+cards+'</main></html>'
    (args.output/'index.html').write_text(html)
    print('Saved 13 aircraft sheets, three-type overview, and local gallery:',args.output)

if __name__=='__main__': main()

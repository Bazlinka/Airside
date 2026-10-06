#!/usr/bin/env python3
"""Assemble four-view native aircraft renders; never synthesises a missing capture."""
import argparse
from pathlib import Path
from PIL import Image,ImageOps,ImageDraw,ImageFont
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('input', type=Path)
parser.add_argument('output', type=Path)
args = parser.parse_args()
src, out = args.input, args.output
out.mkdir(parents=True, exist_ok=True)
font_path = next(p for p in ('/System/Library/Fonts/Supplemental/Arial Bold.ttf',
    '/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf') if Path(p).exists())
font = ImageFont.truetype(font_path, 18)
ids=['ATR42','SF34','DH8D','E190','A223','A320','B738','B38M','A21N','A359','A339','B789','B78X','B412']
sheets={view:Image.new('RGB',(1200,7*400),'#EEF1EC') for view in ['side','opposite','front','overview']}
for i,cid in enumerate(ids):
 sheet=Image.new('RGB',(1200,800),'#EEF1EC')
 for j,view in enumerate(['side','opposite','front','overview']):
  im=Image.open(src/f'{cid}_{view}.png');tile=ImageOps.contain(im,(600,375));x=(j%2)*600;y=(j//2)*400;sheet.paste(tile,(x,y));ImageDraw.Draw(sheet).text((x+12,y+377),cid+' · '+view,font=font,fill='#17242A')
 sheet.save(out/(cid+'.jpg'),quality=90)
 for view in sheets:
  im=ImageOps.contain(Image.open(src/f'{cid}_{view}.png'),(600,375));x=(i%2)*600;y=(i//2)*400;sheets[view].paste(im,(x,y));ImageDraw.Draw(sheets[view]).text((x+12,y+377),cid,font=font,fill='#17242A')
for view,sheet in sheets.items():sheet.save(out/('all-'+view+'.jpg'),quality=90)

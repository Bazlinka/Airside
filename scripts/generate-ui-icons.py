#!/usr/bin/env python3
"""Project-owned 32-unit line icons. Keep editable SVG and deterministic PNG mirrors."""
from pathlib import Path
import uuid
from PIL import Image, ImageDraw
ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'game/Airside/Assets/Airside/Art/UI/Icons'
SRC=ROOT/'docs/art/source/ui-icons'
# Lines use the same 1.8-unit round stroke and a consistent 4-unit safe area.
P={}
def icon(key,*paths,circles=()): P[key]=(paths,circles)
plane=[(16,4),(18,12),(27,19),(27,22),(18,19),(18,25),(22,28),(16,26),(10,28),(14,25),(14,19),(5,22),(5,19),(14,12),(16,4)]
icon('system_aircraft',plane)
icon('operation_departure',plane,[(5,28),(27,28)])
icon('operation_arrival',[(x,32-y) for x,y in plane],[(5,28),(27,28)])
icon('operation_stand',[(5,27),(5,11),(16,5),(27,11),(27,27)],[(10,27),(10,16),(22,16),(22,27)],[(7,11),(25,11)])
icon('operation_taxi',[(5,25),(12,25),(12,16),(24,16),(24,7)],[(20,11),(24,7),(28,11)])
icon('operation_turnaround',[(6,13),(8,8),(14,5),(23,7),(27,13)],[(22,13),(27,13),(27,8)],[(26,19),(24,24),(18,27),(9,25),(5,19)],[(10,19),(5,19),(5,24)])
icon('operation_hold',[(11,8),(11,24)],[(21,8),(21,24)],circles=[(16,16,12)])
icon('operation_completed',[(8,16),(14,22),(24,10)],circles=[(16,16,12)])
icon('economy_route',[(6,25),(10,25),(10,12),(22,12),(22,7)],circles=[(6,25,3),(24,6,3)])
icon('economy_reputation',[(16,4),(20,12),(29,13),(22,19),(24,28),(16,24),(8,28),(10,19),(3,13),(12,12),(16,4)])
icon('economy_income',[(5,25),(27,25)],[(7,20),(14,13),(19,17),(26,7)],[(19,7),(26,7),(26,14)])
icon('economy_cost',[(5,27),(27,27)],[(8,20),(8,14)],[(16,20),(16,9)],[(24,20),(24,5)])
icon('economy_payroll',[(4,10),(28,10),(28,25),(4,25),(4,10)],[(10,10),(10,5),(22,5),(22,10)],[(13,17),(19,17)])
icon('economy_cash',[(4,8),(28,8),(28,25),(4,25),(4,8)],[(4,13),(9,13)],[(23,20),(28,20)],circles=[(16,16,4)])
icon('economy_research',[(23,23),(28,28)],circles=[(14,14,9)])
icon('service_fuel',[(6,27),(6,6),(19,6),(19,27)],[(4,27),(21,27)],[(9,10),(16,10),(16,16),(9,16),(9,10)],[(19,17),(24,17),(24,24),(28,24),(28,9),(24,5)])
icon('service_passengers',[(5,27),(5,22),(8,19),(13,19),(16,22),(16,27)],[(19,27),(19,22),(23,19),(27,21),(27,27)],circles=[(10,10,4),(24,11,3)])
icon('service_baggage',[(7,10),(25,10),(25,26),(7,26),(7,10)],[(12,10),(12,5),(20,5),(20,10)],[(12,14),(12,22)],[(20,14),(20,22)])
icon('service_cleaning',[(25,5),(13,20)],[(10,15),(19,24),(13,29),(4,20),(10,15)],[(8,22),(13,27)])
icon('service_catering',[(6,8),(6,24),(26,24)],[(5,12),(27,12)],[(9,8),(9,4)],[(16,8),(16,4)],[(23,8),(23,4)])
icon('service_inspection',[(11,7),(7,7),(7,28),(25,28),(25,7),(21,7)],[(11,4),(21,4),(21,10),(11,10),(11,4)],[(11,19),(15,23),(22,15)])
icon('service_priority',[(18,3),(7,19),(15,19),(13,29),(25,13),(17,13),(18,3)])
icon('system_overview',[(5,14),(14,14),(14,5),(5,5),(5,14)],[(18,5),(27,5),(27,14),(18,14),(18,5)],[(5,18),(14,18),(14,27),(5,27),(5,18)],[(18,18),(27,18),(27,27),(18,27),(18,18)])
icon('system_follow',[(16,3),(16,8)],[(16,24),(16,29)],[(3,16),(8,16)],[(24,16),(29,16)],circles=[(16,16,9),(16,16,3)])
icon('system_play',[(10,5),(26,16),(10,27),(10,5)])
icon('system_pause',[(10,5),(10,27)],[(22,5),(22,27)])
icon('system_speed',[(5,8),(13,16),(5,24)],[(17,8),(25,16),(17,24)])
icon('system_save',[(5,5),(23,5),(27,9),(27,27),(5,27),(5,5)],[(10,5),(10,13),(21,13),(21,5)],[(10,27),(10,19),(22,19),(22,27)])
speaker=[(5,12),(10,12),(17,6),(17,26),(10,20),(5,20),(5,12)]
icon('system_audio_on',speaker,[(22,9),(26,13),(26,19),(22,23)])
icon('system_audio_off',speaker,[(22,12),(28,20)],[(28,12),(22,20)])
cloud=[(5,21),(3,17),(5,12),(10,11),(12,7),(18,6),(23,10),(25,10),(29,14),(29,19),(26,22),(5,22)]
icon('weather_overcast',cloud)
icon('weather_rain',cloud,[(8,25),(6,29)],[(16,25),(14,29)],[(24,25),(22,29)])
icon('weather_storm',cloud,[(18,22),(12,27),(18,27),(15,31)])
icon('weather_fog',[(4,10),(28,10)],[(7,16),(25,16)],[(4,22),(28,22)],[(9,28),(23,28)])
icon('weather_clear',[(16,3),(16,6)],[(16,26),(16,29)],[(3,16),(6,16)],[(26,16),(29,16)],[(7,7),(9,9)],[(23,23),(25,25)],[(7,25),(9,23)],[(23,9),(25,7)],circles=[(16,16,7)])
icon('weather_wind',[(4,10),(22,10),(25,7),(23,4),(20,4)],[(4,16),(28,16)],[(4,22),(19,22),(22,25),(20,28),(17,28)])
icon('weather_heat',[(12,5),(12,21),(8,24),(8,27),(11,30),(17,30),(20,27),(20,24),(16,21),(16,5),(12,5)],[(24,9),(29,9)],[(24,15),(28,15)],[(14,12),(14,25)])
SRC.mkdir(parents=True,exist_ok=True)
for key,(paths,circles) in P.items():
    svg=['<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 32 32">','<g fill="none" stroke="#F3F1E9" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round">']
    im=Image.new('RGBA',(512,512));d=ImageDraw.Draw(im);scale=16;line=29;colour=(243,241,233,255)
    for points in paths:
        svg.append('<polyline points="'+' '.join(f'{x},{y}' for x,y in points)+'"/>')
        pts=[(int(x*scale),int(y*scale)) for x,y in points];d.line(pts,fill=colour,width=line,joint='curve')
        for x,y in pts:d.ellipse((x-line/2,y-line/2,x+line/2,y+line/2),fill=colour)
    for x,y,r in circles:
        svg.append(f'<circle cx="{x}" cy="{y}" r="{r}"/>');d.ellipse(((x-r)*scale,(y-r)*scale,(x+r)*scale,(y+r)*scale),outline=colour,width=line)
    svg.extend(['</g>','</svg>']);(SRC/f'ui_{key}_v02.svg').write_text('\n'.join(svg)+'\n')
    target=OUT/f'ui_{key}_v02.png';im.resize((128,128),Image.Resampling.LANCZOS).save(target)
    meta=Path(str(target)+'.meta')
    if not meta.exists():
        old=OUT/f'ui_{key}_v01.png.meta';text=(old if old.exists() else OUT/'ui_system_follow_v01.png.meta').read_text();import re
        meta.write_text('\n'.join(line.rstrip() for line in re.sub(r'^guid: .+$','guid: '+uuid.uuid4().hex,text,flags=re.M).splitlines())+'\n')
print(f'Generated {len(P)} editable SVG / runtime icon pairs')

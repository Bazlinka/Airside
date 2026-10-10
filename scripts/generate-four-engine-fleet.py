#!/usr/bin/env python3
"""Generate original AIR-018 747-8 and AIR-019 A380-800 metre-authored runtime kits."""
from pathlib import Path
import argparse
import importlib.util
import numpy as np
from aircraft_body import BodyProfile
from aircraft_intakes import refine as refine_intakes

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('widebody', ROOT/'scripts/generate-air-009-a350-900.py')
w = importlib.util.module_from_spec(spec); spec.loader.exec_module(w)
ART = ROOT/'game/Airside/Assets/Airside/Art/Models/Aircraft'
TYPES = {'B748': (76.25,68.40,19.40,3.05,3.05,6.55,'mdl_747_8_v01'),
         'A388': (72.73,79.75,24.09,3.57,4.20,6.90,'mdl_a380_800_v01')}


def generate(kind):
    length,span,height,rx,ry,cy,_ = TYPES[kind]
    a380 = kind == 'A388'
    # One continuous hull includes the 747's stretched upper deck; no second intersecting cylinder.
    rows = [(-length+.12,.08,.08,cy),(-length+2,.5,.45,cy+.3),
            (-length+6,1.6,1.4,cy+.1),(-length+12,rx,ry,cy),(-40,rx,ry,cy),
            (-28,rx,ry,cy),(-22,rx,ry,cy),(-18,rx,ry if a380 else 3.6,cy if a380 else 7.1),
            (-12,rx,ry if a380 else 4.55,cy if a380 else 8.05),
            (-8,rx*.99,ry if a380 else 4.55,cy if a380 else 8.05),
            (-5,rx*.85,ry*.92 if a380 else 3.95,cy if a380 else 8.15),
            (-3,rx*.58,ry*.70 if a380 else 2.8,cy if a380 else 8.2),
            (-1,rx*.25,ry*.32 if a380 else 1.1,cy if a380 else 8.0),(-.12,.08,.08,cy)]
    profile = BodyProfile(w.oval_lathe_fuselage(rows,segments=72))
    meshes = {'fuselage': profile.loft()}
    def surface(z,angle,offset=0):
        rx,ry,cy=profile.sample(z);a=np.radians(angle)
        return np.array([(rx+offset)*np.cos(a),cy+(ry+offset)*np.sin(a),z],np.float32)
    def angle_at(z,y,side):
        # Radius and centre from the actual smooth hull, not a nominal constant cabin section.
        top=surface(z,90);bottom=surface(z,270)
        c=(top[1]+bottom[1])*.5;r=(top[1]-bottom[1])*.5
        a=np.degrees(np.arcsin(np.clip((y-c)/r,-.98,.98)))
        return 180-a if side < 0 else a
    for side,suffix in ((-1,'left'),(1,'right')):
        # Main deck and full/partial upper deck are separately recognisable.
        for deck,y,start,end in [('main',cy,-10,-length+13),
                                  ('upper',cy+2.8 if a380 else 10.3,-9,-length+13 if a380 else -19)]:
            for i,z in enumerate(np.arange(start,end,-.6),1):
                if any(abs(z-d)<.85 for d in (-8,-25,-45,-length+12)):continue
                meshes[f'cabin_window_{suffix}_{deck}_{i}']=w.skin.window(surface,float(z),angle_at(z,y,side),width=.26,height=.36)
        for i,z in enumerate((-8,-25,-45,-length+12),1):
            y=5.6 if a380 else 5.2
            meshes[f'door_{suffix}_{i}']=w.skin.skin_patch(surface,z,angle_at(z,y,side),.55,1.0,front=.006,radius=.12,rings=3,max_edge=.18)
        # Flight deck is on the upper hump of the 747, between decks in the A380.
        for i,z in enumerate((-4.6,-5.5,-6.3),1):
            meshes[f'windscreen_{suffix}_{i}']=w.skin.skin_patch(surface,z,angle_at(z,8.95 if a380 else 10.4,side),.42,.32,front=.012,radius=.08,rings=2)
        rootz=-length*.35
        wing = [(side*rx*.75,4.9,rootz,15.2 if a380 else 13.8,1.05),
                (side*12,5.3,rootz-3,11,.75),
                (side*24,6.2,rootz-10,6,.38),
                (side*(span/2-1.5),7.6,rootz-18,2.2,.16),
                (side*span/2,8.4,rootz-20,1,.07)]
        meshes[f'wing_{suffix}']=w.lofted_aerofoil(wing,chord_points=24)
        for name,stations,fraction in [('flap',wing[:3],.28),('aileron',wing[2:],.3),('spoiler',wing[1:3],.45)]:
            meshes[f'{name}_{suffix}']=w.lofted_aerofoil([(x,y+.05,z-c+c*fraction,c*fraction,t*.22) for x,y,z,c,t in stations],chord_points=12)
        meshes[f'nav_light_{suffix}']=w.box(side*(span/2-.06),8.4,rootz-20.5,.12,.12,.16)
        # Two nacelles per wing, own openings, recessed fans and blade sets.
        for outer,x,front,radius in [(False,13 if a380 else 12,-28 if a380 else -30,1.65 if a380 else 1.45),
                                     (True,25 if a380 else 22,-34 if a380 else -36,1.65 if a380 else 1.45)]:
            key=('outer_' if outer else '')+suffix
            cx=side*x;ey=3.0 if a380 else 2.7;rear=front-8
            nac=w.oval_lathe_fuselage([(rear,.5,.5,ey),(rear+1,radius*.65,radius*.65,ey),
               (front-3,radius,radius,ey),(front-.12,radius*.97,radius*.97,ey)],segments=48)
            nac[0][:,0]+=cx;meshes['engine_'+key]=nac
            meshes['intake_'+key]=w._annulus(cx,ey,front,front-.5,radius*.97,radius*.82,segments=48)
            meshes['fan_'+key]=w.cylinder(cx,ey,front-.6,radius*.78,.06,axis='z',segments=48)
            for i,a in enumerate(np.arange(0,360,20),1):
                v,idx=w._fan_blade(0,0,0,float(a));v*=radius*.78/1.42;v+=np.array([cx,ey,front-.55],np.float32)
                meshes[f'fan_blade_{"outer_" if outer else ""}{suffix[0]}{i}']=(v,idx)
            meshes['pylon_'+key]=w.box(cx,4.9,front-3,.65,3.8,3)
            meshes['exhaust_'+key]=w.cylinder(cx,ey,rear,.55,.5,axis='z',segments=32)
        # Authored gear: four main trucks, plus twin nose wheels (18 / 22 wheels).
        mainz=-39.9 if a380 else -37.5
        for body,x,z,axles in [(False,6.0,mainz+1.5,2),(True,2.0,mainz-1.5,3 if a380 else 2)]:
            key=suffix+('_body' if body else '')
            radius=.7 if a380 else .65;cx=side*x
            offsets=np.linspace(.85,-.85,axles)
            pieces=[w.box(cx,2.1,z,.28,3.0,.45),w.box(cx,radius,z,.18,.16,2.1)]
            for axle,dz in enumerate(offsets):
                pieces.append(w.box(cx,radius,z+dz,1.3,.15,.15))
                for lateral,dx in [('inboard',-.4),('outboard',.4)]:
                    pos='forward' if axle==0 else 'aft' if axle==axles-1 else 'centre'
                    w._wheel(meshes,f'{key}_{pos}_{lateral}',cx+side*dx,z+dz,radius,.3)
            meshes['gear_'+('body_' if body else '')+suffix]=w.skin.merge_meshes(pieces)
            # Fixed fairing reaches each leg attachment, doors sit on the bay underside.
            meshes['gear_fairing_'+('body_' if body else '')+suffix]=w.skin.x_tube(side*1.5,cx+side*.5,3.9,z,.50,2.3)
            meshes[f'gear_door_{key}']=w.box(cx,3.64,z,1.2,.08,2.6)
        # Connected conventional tail, separately hinged elevator/rudder.
        tp=[(side*.2,cy+ry*.58,-length+13,10,.5),
            (side*8,cy+ry*.68,-length+9,6,.25),(side*15,cy+ry*.8,-length+7,3,.1)]
        meshes['tailplane_'+suffix]=w.lofted_aerofoil(tp,chord_points=20)
        meshes['elevator_'+suffix]=w.lofted_aerofoil([(x,y,z-c+c*.28,c*.28,t*.25) for x,y,z,c,t in tp],chord_points=12)
        meshes['livery_stripe'+('_lower' if side>0 else '')]=w.skin.livery_ribbon(surface,-11,-length+14,side,half_width=.32,rise_degrees=-14,samples=80)
    fin=[(cy+ry*.60,0,-length+17,14,.55),(height*.70,0,-length+10,9,.32),(height,0,-length+5,3,.12)]
    meshes['tail_fin']=w.lofted_aerofoil(fin,chord_points=22,vertical=True)
    meshes['rudder']=w.lofted_aerofoil([(y,x,z-c+c*.30,c*.30,t*.22) for y,x,z,c,t in fin],chord_points=12,vertical=True)
    nose=-8
    meshes['gear_nose']=w.skin.merge_meshes([w.box(0,2.1,nose,.26,3.1,.4),w.box(0,.55,nose,1,.14,.14)])
    w._wheel(meshes,'nose_left',-.35,nose,.55,.25);w._wheel(meshes,'nose_right',.35,nose,.55,.25)
    meshes['gear_door_nose_l']=w.box(-.38,3.7,nose,.7,.08,3.6)
    meshes['gear_door_nose_r']=w.box(.38,3.7,nose,.7,.08,3.6)
    meshes['taxi_light']=w.box(0,1.2,nose+.3,.2,.15,.15)
    meshes['landing_light_l']=w.box(-rx*.8,4.9,-length*.35-1,.3,.2,.16)
    meshes['landing_light_r']=w.box(rx*.8,4.9,-length*.35-1,.3,.2,.16)
    meshes['beacon_top']=w.box(0,cy+ry+.08,-32,.14,.14,.14)
    meshes['beacon_bottom']=w.box(0,cy-ry-.06,-32,.14,.12,.14)
    # Shared writer opens inner engines; run its same refinement on the outer pair.
    outer={k.replace('outer_',''):value for k,value in meshes.items() if 'outer_' in k}
    refine_intakes(outer)
    for k,value in outer.items():
        name=k.replace('_left','_outer_left').replace('_right','_outer_right')
        if name in meshes or k.startswith('intake_liner_'):meshes[name]=value
    return {name:w.orient_outward(v,i) for name,(v,i) in meshes.items()}


def validate(kind,meshes):
    length,span,height,*_=TYPES[kind]
    vertices=np.concatenate([v for v,_ in meshes.values()]);lo=vertices.min(0);hi=vertices.max(0)
    assert np.allclose(hi-lo,[span,height,length],atol=.03),(kind,hi-lo)
    assert abs(lo[1])<.01 and abs(hi[2])<.01,(lo,hi)
    for name,(v,i) in meshes.items():
        assert np.isfinite(v).all() and len(i)>0 and len(i)%3==0 and i.max()<len(v),name
    assert len([k for k in meshes if k.startswith('tire_')])==(22 if kind=='A388' else 18)
    assert len([k for k in meshes if k.startswith('engine_')])==4
    return hi-lo


def main():
    parser=argparse.ArgumentParser();parser.add_argument('--check',action='store_true');args=parser.parse_args()
    for kind,(*_,basename) in TYPES.items():
        meshes=generate(kind);dimensions=validate(kind,meshes)
        glazing_spec=importlib.util.spec_from_file_location('glazing',ROOT/'scripts/polish-aircraft-glazing.py')
        glazing=importlib.util.module_from_spec(glazing_spec);glazing_spec.loader.exec_module(glazing)
        glazing.polish(meshes)
        if args.check:
            # Regenerate in scratch and compare the actual binary/model payload, excluding fresh GUIDs.
            import tempfile
            with tempfile.TemporaryDirectory() as tmp:
                folder=Path(tmp)/'Aircraft';folder.mkdir();w.write_kit(folder,basename,meshes)
                for ext in ('.gltf','.bin','.fbx'):
                    assert (folder/(basename+ext)).read_bytes()==(ART/(basename+ext)).read_bytes(),basename+ext
        else:w.write_kit(ART,basename,meshes)
        print(kind,len(meshes),'parts',dimensions)

if __name__=='__main__':main()

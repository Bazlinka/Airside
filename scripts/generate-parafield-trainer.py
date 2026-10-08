#!/usr/bin/env python3
"""Original, unbranded four-seat high-wing trainer. Project-owned geometry, zero cost.

Metre envelope: 11.0 m span, 8.3 m length, fixed tricycle gear. Uses Airside's
existing lathe/airfoil kit writer; no external mesh or aircraft logos.
"""
import importlib.util
from pathlib import Path
import numpy as np

ROOT=Path(__file__).resolve().parents[1]
spec=importlib.util.spec_from_file_location('airside_aircraft',ROOT/'scripts/generate-air-001-v05.py')
base=importlib.util.module_from_spec(spec); spec.loader.exec_module(base)
meshes={}
meshes['fuselage']=base.oval_lathe_fuselage([(-4.0,.10,.13,1.40),(-2.3,.26,.29,1.40),
    (-.8,.56,.59,1.45),(1.2,.58,.62,1.47),(2.4,.44,.36,1.20),(3.55,.34,.32,1.16)],segments=24)
meshes['cowl']=base.oval_lathe_fuselage([(2.45,.46,.37,1.19),(3.72,.31,.29,1.19)],segments=24)
meshes['spinner']=base.oval_lathe_fuselage([(3.75,.18,.18,1.19),(4.15,.01,.01,1.19)],segments=16)
meshes['windscreen_glass']=base.windscreen_pane(0,1.90,1.48,1.02,.48,.035,pitch_deg=32)
for side,x in [('left',-.565),('right',.565)]:
    meshes['window_glass_'+side]=base.box(x,1.68,.35,.035,.49,1.38)
    meshes['door_seam_'+side]=base.box(x*1.02,1.28,.43,.018,.025,1.6)
    meshes['door_handle_'+side]=base.box(x*1.06,1.49,.1,.04,.035,.16)
    meshes['wing_'+side]=base.airfoil_wing(0,2.12,.30,5.5,1.65,1.16,.20,.11,side=-1 if x<0 else 1,dihedral=.015)
    meshes['tailplane_'+side]=base.airfoil_wing(0,1.58,-3.15,1.8,.95,.62,.12,.07,side=-1 if x<0 else 1,dihedral=0)
    # Strut aligned from the lower cabin to the wing (no floating supports).
    a=np.array([x,1.0,.4]); b=np.array([x/abs(x)*3.1,2.15,.3]); d=b-a
    v,ind=base.cylinder(0,0,0,.035,float(np.linalg.norm(d)),axis='y',segments=8)
    up=d/np.linalg.norm(d); right=np.cross(up,[0,0,1]); right/=np.linalg.norm(right); fwd=np.cross(right,up)
    meshes['wing_strut_'+side]=(v@np.array([right,up,fwd])+(a+b)/2,ind)
    meshes['main_gear_'+side]=base.box(x*1.6,.54,-.15,.11,.70,.12)
    meshes['main_wheel_'+side]=base.cylinder(x*1.85,.24,-.15,.24,.16,axis='x',segments=20)
    meshes['nav_light_'+side]=base.box(-5.45 if x<0 else 5.45,2.23,.14,.10,.07,.14)
meshes['tail_fin']=base.box(0,2.1,-3.44,.10,1.42,1.06)
meshes['rudder_accent']=base.box(0,2.15,-3.90,.13,1.2,.16)
meshes['nose_gear']=base.box(0,.57,2.40,.10,.65,.10)
meshes['nose_wheel']=base.cylinder(0,.19,2.40,.19,.13,axis='x',segments=20)
meshes['prop_blade_1']=base.box(0,1.70,3.78,.13,1.02,.06)
meshes['prop_blade_2']=base.box(0,.68,3.78,.13,1.02,.06)
meshes['beacon']=base.box(0,2.84,-3.45,.09,.08,.10)
meshes['livery_accent_left']=base.box(-.568,1.25,.12,.016,.065,1.95)
meshes['livery_accent_right']=base.box(.568,1.25,.12,.016,.065,1.95)
# Refine the body and replace the old flat cockpit/cabin slabs with skin shells.
spec=importlib.util.spec_from_file_location('trainer_body',ROOT/'scripts/aircraft_body.py')
body=importlib.util.module_from_spec(spec);spec.loader.exec_module(body)
profile=body.refine(meshes)
spec=importlib.util.spec_from_file_location('trainer_skin',ROOT/'scripts/aircraft_skin.py')
skin=importlib.util.module_from_spec(spec);spec.loader.exec_module(skin)
def surface(z,angle,offset=0):
    rx,ry,cy=profile.sample(z);theta=np.deg2rad(angle)
    return np.array([(rx+offset)*np.cos(theta),cy+(ry+offset)*np.sin(theta),z],np.float32)
meshes['windscreen_glass']=skin.skin_patch(surface,1.55,90,.39,.47,front=.012,radius=.08,rings=4,max_edge=.08)
for side,angle in [('left',158),('right',22)]:
    meshes['window_glass_'+side]=skin.skin_patch(surface,.35,angle,.65,.23,front=.012,radius=.08,rings=4,max_edge=.08)
    lower=202 if side=='left' else -22
    meshes['livery_accent_'+side]=skin.skin_patch(surface,.12,lower,.975,.0325,front=.010,radius=.025,rings=3,max_edge=.10)
    meshes['door_seam_'+side]=skin.skin_patch(surface,.43,lower,.80,.0125,front=.008,radius=.010,rings=2,max_edge=.10)
    handle=180 if side=='left' else 0
    meshes['door_handle_'+side]=skin.skin_patch(surface,.10,handle,.08,.0175,front=.025,radius=.01,rings=2,max_edge=.05)
meshes={k:(v.astype(np.float32),i) for k,(v,i) in meshes.items()}
folder=ROOT/'game/Airside/Assets/Airside/Art/Models/Aircraft'
path=folder/'mdl_parafield_trainer_v01.gltf'
preserved={Path(str(q)+'.meta'):Path(str(q)+'.meta').read_bytes() for q in (path,path.with_suffix('.bin')) if Path(str(q)+'.meta').exists()}
base._auth.pack_gltf(path,meshes)
for meta,data in preserved.items():meta.write_bytes(data)
print('Generated',len(meshes),'trainer parts')

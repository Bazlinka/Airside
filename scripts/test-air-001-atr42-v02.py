#!/usr/bin/env python3
"""Geometry regressions for the v02 authored model; no Unity dependency."""
import importlib.util
from collections import Counter
from pathlib import Path
import numpy as np
p=Path(__file__).with_name('generate-air-001-atr42-v02.py')
spec=importlib.util.spec_from_file_location('atr_v02',p);m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)
meshes=m.final_meshes();original=m.v01.final_meshes()
verts=np.concatenate([v for v,_ in meshes.values()])
np.testing.assert_allclose(np.ptp(verts,axis=0),[24.57,7.59,22.67],atol=.003)
assert abs(verts[:,1].min())<.001
moving=('gear_', 'tire_', 'wheel_', 'rim_', 'propeller_', 'spinner_', 'prop_hub_', 'hub_cap_', 'flap_', 'aileron_', 'elevator_', 'rudder', 'spoiler_', 'door_fwd', 'cargo_door')
assert {n for n in original if n.startswith(moving)} <= set(meshes)
for name,(v,indices) in meshes.items():
    assert np.isfinite(v).all() and len(indices)%3==0 and indices.max()<len(v), name
    t=v[indices.reshape(-1,3)]
    assert np.all(np.linalg.norm(np.cross(t[:,1]-t[:,0],t[:,2]-t[:,0]),axis=1)>1e-9),name
    if name=='fuselage' or name.startswith(('cabin_window','windscreen')) or name in ('door_fwd','cargo_door'):
        edges=Counter()
        for a,b,c in indices.reshape(-1,3):
            for x,y in ((a,b),(b,c),(c,a)):edges[(int(x),int(y))]+=1
        assert all(edges[(b,a)]==count for (a,b),count in edges.items()),name+' winding/seam'
# Windows sit on the local curved skin, not a fixed-width slab detached at the tail.
for name,(v,_) in meshes.items():
    if not name.startswith('cabin_window'):continue
    rx,ry,cy=[np.interp(v[:,2],m.STATIONS[:,0],m.STATIONS[:,i]) for i in (1,2,3)]
    radius=np.sqrt((v[:,0]/rx)**2+((v[:,1]-cy)/ry)**2)
    assert np.all((radius>1.003)&(radius<1.025)),name
assert abs(meshes['tailplane'][0][:,1].max()-meshes['tail_fin'][0][:,1].max())<.04
for name in ('elevator_left','elevator_right'):
    assert abs(meshes[name][0][:,1].mean()-meshes['tailplane'][0][:,1].mean())<.3
assert sum(len(i)//3 for _,i in meshes.values())<20000
print('PASS: dimensions, ground contact, articulation names, finite nondegenerate triangles, closed fitted skins, glazing clearance, T-tail alignment and triangle budget.')

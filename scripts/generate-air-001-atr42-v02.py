#!/usr/bin/env python3
"""AIR-001 v02: fitted glazing, a rounded commuter nose and a true T-tail.

Project-owned geometry; retains v01 moving-part names and dimensions. glTF is
loaded directly until a Unity prefab is baked. Does not overwrite v01 assets.
"""
import importlib.util
from pathlib import Path
import numpy as np

SCRIPTS = Path(__file__).resolve().parent
spec = importlib.util.spec_from_file_location('atr_v01', SCRIPTS / 'generate-air-001-atr42.py')
v01 = importlib.util.module_from_spec(spec)
spec.loader.exec_module(v01)
BASENAME = 'mdl_atr42_starter_v02'
# z, lateral radius, vertical radius, centre height. Fuller cabin shoulders,
# shorter rounded nose, and a continuous tapered rear pressure body.
STATIONS = np.array([
    (-10.4239635, .015, .015, 1.53), (-9.8,.20,.24,1.56),
    (-8.7,.52,.55,1.64), (-7.4,.91,.92,1.76),
    (-6.0,1.20,1.15,1.86), (-4.8,1.335,1.25,1.90),
    (4.9,1.335,1.25,1.90), (6.6,1.31,1.23,1.92),
    (8.0,1.24,1.16,1.94), (8.9,1.10,1.01,1.92),
    (9.7,.92,.80,1.83), (10.4,.86,.72,1.80),
    (11.0,.70,.55,1.70), (11.6,.48,.35,1.60),
    (12.0,.25,.19,1.53), (12.2460365,.035,.035,1.50)
], dtype=np.float32)


def surface(z, theta, offset=0):
    rx, ry, cy = [np.interp(z, STATIONS[:,0], STATIONS[:,i]) for i in (1,2,3)]
    return np.array([(rx+offset)*np.cos(theta), cy+(ry+offset)*np.sin(theta), z])


def body():
    # Duplicate each station between its authored neighbours for smooth fitted
    # glazing tessellation, while retaining a deterministic low triangle count.
    zs=np.concatenate([np.linspace(a,b,2,endpoint=False) for a,b in zip(STATIONS[:-1,0],STATIONS[1:,0])] + [[STATIONS[-1,0]]])
    verts=np.array([surface(z,a) for z in zs for a in np.linspace(0,2*np.pi,64,endpoint=False)],np.float32)
    faces=[]
    for j in range(len(zs)-1):
        for i in range(64):
            a=j*64+i;b=j*64+(i+1)%64;c=b+64;d=a+64
            faces.extend([a,b,c,a,c,d])
    # Ends capped exactly at the dimension envelope (no extra lathe tip length).
    for ring in (0,len(zs)-1):
        centre=len(verts);verts=np.vstack([verts,[0,STATIONS[0 if ring==0 else -1,3],zs[ring]]])
        for i in range(64):
            edge=[ring*64+i,ring*64+(i+1)%64]
            if ring==0:edge.reverse()
            faces.extend([centre,*edge])
    return verts.astype(np.float32),np.array(faces,np.uint16)


def patch(z, theta, half_z, half_theta, offset=.015):
    # Rounded, conformal closed panel. Concentric rings follow the fuselage;
    # thin solid backing prevents one-sided glazing from vanishing from inside.
    count=32;verts=[]
    for depth in (offset,offset-.008):
        verts.append(surface(z,theta,depth))
        for radius in (.5,1):
            for a in np.linspace(0,2*np.pi,count,endpoint=False):
                u=np.sign(np.cos(a))*abs(np.cos(a))**.45
                v=np.sign(np.sin(a))*abs(np.sin(a))**.45
                verts.append(surface(z+half_z*u*radius,theta+half_theta*v*radius,depth))
    faces=[];layer=1+2*count
    for base in (0,layer):
        for i in range(count):
            j=(i+1)%count
            faces.extend([base,base+1+i,base+1+j])
            a=base+1+i;b=base+1+j;c=b+count;d=a+count
            faces.extend([a,d,c,a,c,b])
    # Opposite winding on the backing surface makes a consistently closed shell.
    backing_start=len(faces)//2
    for i in range(backing_start,len(faces),3):
        faces[i+1],faces[i+2]=faces[i+2],faces[i+1]
    for i in range(count):
        a=1+count+i;b=1+count+(i+1)%count
        faces.extend([a,b+layer,b,a,a+layer,b+layer])
    return np.array(verts,np.float32),np.array(faces,np.uint16)


def final_meshes():
    meshes=v01.final_meshes()
    meshes['fuselage']=body()
    # Remove the old raised cockpit boxes, replacing them with fitted panes and
    # subtly larger backing panels. The centre pillar is body paint between panes.
    for name in list(meshes):
        if name.startswith(('cockpit_', 'windscreen_', 'cabin_window_')):
            del meshes[name]
    for name,theta in [('l',np.deg2rad(135)),('r',np.deg2rad(45))]:
        meshes['windscreen_'+name]=patch(9.28,theta,.78,np.deg2rad(21))
        meshes['windscreen_pillar_'+name]=patch(9.28,theta,.82,np.deg2rad(23),.005)
    for i,z in enumerate((5.85,4.9,3.95,3,2.05,1.1,.15,-.8,-1.75,-2.7,-3.65,-4.6,-5.55),1):
        for prefix,theta in [('cabin_window_',np.deg2rad(156)),('cabin_window_r',np.deg2rad(24))]:
            meshes[prefix+str(i)]=patch(z,theta,.235,.17)
    # Seat door skins on the new fuselage without changing door names/pivots.
    for name in ('door_fwd','cargo_door'):
        old=meshes[name][0];z=float(old[:,2].mean());theta=np.deg2rad(180 if old[:,0].mean()<0 else 0)
        meshes[name]=patch(z,theta,float(np.ptp(old[:,2]))*.5,.55,.012)
    # Narrow the stretched fin chord; carry rudder with it. Move the horizontal
    # stabiliser AND elevators to the fin crown rather than leaving a cruciform.
    for name in ('tail_fin','rudder'):
        verts,idx=meshes[name];verts=verts.copy();verts[:,2]=-9.1+(verts[:,2]+9.1)*.79;meshes[name]=(verts,idx)
    shift=7.57-float(meshes['tailplane'][0][:,1].max())
    for name in ('tailplane','elevator_left','elevator_right'):
        meshes[name]=v01.move_y(meshes[name],shift)
    # Re-seat the antenna on the trailing fin, not floating forward of the tail.
    meshes['hf_antenna']=v01.panel(0,7.12,-8.9,.025,.025,.72)
    verts,idx=meshes['tail_nav_light'];verts=verts.copy()
    verts += np.array([0,7.35,-9.05])-verts.mean(axis=0)
    meshes['tail_nav_light']=(verts,idx)
    # Smooth saddle fillets where the high wing meets the cabin roof. These are
    # static skin, separate from the flaps and other articulated surfaces.
    for side,label in ((-1,'left'),(1,'right')):
        fairing=v01._v05.oval_lathe_fuselage([(-1.6,.12,.08,2.92),(-.7,.37,.20,3.00),(1.8,.42,.23,3.04),(3.6,.13,.09,2.98)],segments=24)
        meshes['wing_root_'+label]=v01.translated(fairing,side*1.12,0,0)
    return {name:v01._v06.outward_winding(mesh) for name,mesh in meshes.items()}


def main():
    meshes=final_meshes()
    # Regeneration must not change Unity GUIDs for this revision.
    metas=[v01.AIRCRAFT/(BASENAME+ext+'.meta') for ext in ('.gltf','.bin')]
    preserved={p:p.read_text() for p in metas if p.exists()}
    v01._v05._auth.pack_gltf(v01.AIRCRAFT/(BASENAME+'.gltf'),meshes)
    for p,text in preserved.items():p.write_text(text)
    verts=np.concatenate([v for v,_ in meshes.values()])
    print(f'{BASENAME}: {len(meshes)} parts; bounds {np.ptp(verts,axis=0)}; {sum(len(i)//3 for _,i in meshes.values())} triangles')

if __name__=='__main__':main()

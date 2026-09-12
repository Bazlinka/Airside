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



def profile_prism(name_points, half_width):
    """Closed, thin fairing extruded across X from a Y/Z side profile."""
    points=np.asarray(name_points,np.float32)
    verts=np.array([[x,y,z] for x in (-half_width,half_width) for z,y in points],np.float32)
    n=len(points);faces=[]
    for i in range(1,n-1):
        faces.extend([0,i+1,i,n,n+i,n+i+1])
    for i in range(n):
        j=(i+1)%n
        faces.extend([i,j,n+j,i,n+j,n+i])
    return verts,np.asarray(faces,np.uint16)

def final_meshes():
    meshes=v01.final_meshes()
    meshes['fuselage']=body()
    # Replace the old raised cockpit boxes with four fitted panes. Their spacing
    # leaves real body-colour pillars instead of coplanar backing panels, removing
    # the z-fighting that made the nose look broken in the first v02 review.
    for name in list(meshes):
        if name.startswith(('cockpit_', 'windscreen_', 'cabin_window_')):
            del meshes[name]
    cockpit_panes=(
        ('windscreen_l',np.deg2rad(109),9.42,.56,np.deg2rad(15)),
        ('windscreen_r',np.deg2rad(71),9.42,.56,np.deg2rad(15)),
        ('cockpit_side_l',np.deg2rad(145),9.16,.52,np.deg2rad(13)),
        ('cockpit_side_r',np.deg2rad(35),9.16,.52,np.deg2rad(13)),
    )
    for name,theta,z,half_z,half_theta in cockpit_panes:
        meshes[name]=patch(z,theta,half_z,half_theta,.020)

    # Thirteen evenly pitched, fitted cabin panes per side. Insets between each
    # pane stay body-coloured so the window row reads cleanly from overview.
    for i,z in enumerate((5.55,4.68,3.81,2.94,2.07,1.20,.33,-.54,-1.41,-2.28,-3.15,-4.02,-4.89),1):
        for prefix,theta in [('cabin_window_',np.deg2rad(158)),('cabin_window_r',np.deg2rad(22))]:
            meshes[prefix+str(i)]=patch(z,theta,.205,.145,.020)

    # Proper front passenger and aft cargo door positions. A slightly larger
    # grey backing patch creates a consistent recessed frame without floating
    # boxes. Door skins remain separately named for the existing animation.
    for name in ('door_fwd','cargo_door','door_frame_fwd','door_handle_fwd','cargo_sill','cargo_door_latch'):
        meshes.pop(name,None)
    meshes['door_outline_fwd']=patch(6.43,np.pi,.46,.54,.010)
    meshes['door_fwd']=patch(6.43,np.pi,.40,.49,.020)
    meshes['door_handle_fwd']=patch(6.66,np.pi,.055,.045,.027)
    meshes['cargo_door_outline']=patch(-3.34,0,.86,.58,.010)
    meshes['cargo_door']=patch(-3.34,0,.79,.52,.020)
    meshes['cargo_door_latch']=patch(-2.83,0,.065,.045,.027)

    # Narrow the stretched fin chord and rebuild the tail as one connected read.
    # A dorsal root fairing joins the pressure body to the fin; a crown saddle
    # overlaps both fin and stabiliser so no daylight gap can appear from any view.
    for name in ('tail_fin','rudder'):
        verts,idx=meshes[name];verts=verts.copy();verts[:,2]=-9.1+(verts[:,2]+9.1)*.79;meshes[name]=(verts,idx)
    meshes['tail_root_fairing']=profile_prism([
        (-9.82,2.20),(-9.45,3.02),(-8.55,3.58),(-6.35,2.66),(-5.55,2.48)
    ],.22)
    shift=7.57-float(meshes['tailplane'][0][:,1].max())
    for name in ('tailplane','elevator_left','elevator_right'):
        meshes[name]=v01.move_y(meshes[name],shift)
    meshes['tailplane_saddle']=v01._v05.oval_lathe_fuselage([
        (-9.55,.10,.04,7.47),(-9.12,.35,.08,7.48),(-8.52,.58,.11,7.46),
        (-7.42,.54,.10,7.46),(-6.58,.18,.05,7.47)
    ],segments=28)
    meshes['hf_antenna']=profile_prism([(-9.18,7.22),(-8.58,7.22),(-8.74,7.47)],.025)
    verts,idx=meshes['tail_nav_light'];verts=verts.copy()
    verts += np.array([0,7.50,-9.28])-verts.mean(axis=0)
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

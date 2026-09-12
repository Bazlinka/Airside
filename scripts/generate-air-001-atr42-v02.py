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
    # Rising, compact rear pressure cone (tail is not a mirrored nose).
    (-10.4239635,.05,.04,2.22),(-9.90,.24,.18,2.22),
    (-9.30,.48,.36,2.18),(-8.60,.75,.60,2.10),
    (-7.80,1.03,.88,2.00),(-7.00,1.23,1.15,1.90),
    (-6.20,1.35,1.30,1.85),
    # Stout constant-section cabin.
    (-4.80,1.40,1.36,1.84),(4.90,1.40,1.36,1.84),
    (6.60,1.39,1.34,1.84),(7.40,1.38,1.31,1.84),
    # Blunt, drooping flight-deck and radome profile from the references.
    (8.40,1.32,1.24,1.80),(9.20,1.20,1.10,1.70),
    (10.00,1.07,.93,1.55),(10.80,.88,.74,1.38),
    (11.45,.62,.50,1.25),(11.90,.36,.28,1.17),
    (12.15,.18,.14,1.12),(12.2460365,.06,.05,1.10)
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




def surface_quad(corners, offset=.022):
    """Thin planar pane fitted against the local fuselage surface.

    A four-point sample of a curved fuselage is not coplanar. Sending that raw
    quad to the renderer creates a visible diagonal fold, which made the glass
    read as chevrons or cracked tiles. Real pressure-cabin glazing is flat, so
    project the sampled corners onto their best-fit plane and give the pane a
    small, consistently inward thickness.
    """
    sampled=np.asarray([surface(z,np.deg2rad(theta),offset) for z,theta in corners],np.float64)
    centre=sampled.mean(axis=0)
    _,_,basis=np.linalg.svd(sampled-centre,full_matrices=False)
    normal=basis[-1]
    mean_theta=np.deg2rad(np.mean([theta for _,theta in corners]))
    outward=np.array([np.cos(mean_theta),np.sin(mean_theta),0.0])
    if np.dot(normal,outward)<0:
        normal=-normal
    front=sampled-((sampled-centre)@normal)[:,None]*normal
    front=front+normal*.008
    back=front-normal*.010
    front=front.astype(np.float32);back=back.astype(np.float32)
    verts=np.vstack([front,back]);count=len(corners);faces=[]
    for i in range(1,count-1):
        faces.extend([0,i,i+1,count,count+i+1,count+i])
    for i in range(count):
        j=(i+1)%count;faces.extend([i,count+j,j,i,count+i,count+j])
    return verts,np.asarray(faces,np.uint16)

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


def aero_panel(side,x0,x1,zf0,zb0,zf1,zb1,y0,y1,t0,t1):
    """Closed tapered aero surface from two spanwise stations."""
    x0*=side;x1*=side
    corners=np.asarray([
        [x0,y0-t0/2,zb0],[x0,y0-t0*.2,zf0],[x1,y1-t1*.2,zf1],[x1,y1-t1/2,zb1],
        [x0,y0+t0/2,zb0],[x0,y0+t0*.35,zf0],[x1,y1+t1*.35,zf1],[x1,y1+t1/2,zb1]
    ],np.float32)
    faces=[]
    for a,b,c,d in ((0,1,2,3),(4,7,6,5),(0,4,5,1),(3,2,6,7),(1,5,6,2),(0,3,7,4)):
        faces.extend([a,b,c,a,c,d])
    return corners,np.asarray(faces,np.uint16)

def engine_pod(x):
    pod=v01._v05.oval_lathe_fuselage([
        (-1.78,.14,.12,2.77),(-1.38,.38,.34,2.79),(-.45,.58,.53,2.81),
        (1.55,.68,.62,2.83),(3.55,.65,.60,2.84),(4.55,.53,.49,2.84),
        (5.10,.22,.20,2.82)
    ],segments=36)
    return v01.translated(pod,x,0,0)


def final_meshes():
    meshes=v01.final_meshes()
    meshes['fuselage']=body()
    # Reference-sized high wing: about 51 m² planform, tapered outer panels
    # and gentle dihedral. Rebuild its separate controls on the same stations so
    # no flap or aileron floats outside the new, narrower chord.
    for name in list(meshes):
        if name.startswith(('wing_left','wing_right','flap_','aileron_','spoiler_')):
            meshes.pop(name,None)
    for side,label in ((-1,'left'),(1,'right')):
        meshes['wing_'+label]=aero_panel(side,1.18,12.285,1.66,-1.84,.96,-.16,3.02,3.24,.30,.10)
        meshes['flap_'+label]=aero_panel(side,1.55,5.85,-.48,-1.82,-.34,-1.30,3.00,3.10,.09,.06)
        meshes['aileron_'+label]=aero_panel(side,6.15,11.82,-.27,-1.25,.19,-.23,3.11,3.23,.065,.04)
        meshes['spoiler_'+label]=aero_panel(side,2.35,5.45,.45,-.34,.32,-.36,3.19,3.24,.025,.02)

        for index,x_abs in enumerate((2.65,4.75),1):
            fair=v01._v05.oval_lathe_fuselage([(-1.35,.08,.06,2.89),(-.55,.15,.10,2.90),(.22,.11,.07,2.93),(.48,.03,.025,2.95)],segments=18)
            meshes[f'flap_track_{label[0]}{index}']=v01.translated(fair,side*x_abs,0,0)
        fair=v01._v05.oval_lathe_fuselage([(-1.42,.10,.07,2.88),(-.48,.19,.12,2.91),(.35,.12,.08,2.94)],segments=20)
        meshes['flap_fairing_'+label[0]]=v01.translated(fair,side*5.55,0,0)

    # One compact nacelle per side. v01 contained a long engine shell plus a
    # second pod, which made each engine look swollen and unfinished.
    for name in list(meshes):
        if name.startswith(('engine_','nacelle_','pylon_','intake_','exhaust_','oil_cooler_','cowl_flap_')):
            meshes.pop(name,None)
    for side,label in ((-1,'left'),(1,'right')):
        x=side*3.99
        meshes['engine_'+label]=engine_pod(x)
        meshes['intake_'+label]=v01._v05.cylinder(x,2.82,4.96,.38,.10,axis='z',segments=28)
        meshes['exhaust_'+label]=v01._v05.cylinder(x,2.79,-1.52,.14,.48,axis='z',segments=20)
        meshes['pylon_'+label]=v01.panel(x,3.12,1.15,.50,.34,1.75)
    # Replace the old raised cockpit boxes with four fitted panes. Their spacing
    # leaves real body-colour pillars instead of coplanar backing panels, removing
    # the z-fighting that made the nose look broken in the first v02 review.
    for name in list(meshes):
        if name.startswith(('cockpit_', 'windscreen_', 'cabin_window_')):
            del meshes[name]
    cockpit_panes={
        # Broad ATR-style forward panes, ordered around each perimeter. The
        # near-vertical inner edges create a slim centre post; the outer edges
        # meet the side panes without the earlier arrowhead-shaped gaps.
        'windscreen_l':[(8.72,91.5),(8.72,118),(9.78,114),(10.02,92)],
        'windscreen_r':[(8.72,62),(8.72,88.5),(10.02,88),(9.78,66)],
        'cockpit_side_l':[(8.48,121),(8.48,149),(9.48,143),(9.78,117)],
        'cockpit_side_r':[(8.48,31),(8.48,59),(9.78,63),(9.48,37)],
    }
    for name,corners in cockpit_panes.items():
        meshes[name]=surface_quad(corners)

    # Thirteen evenly pitched, fitted cabin panes per side. Insets between each
    # pane stay body-coloured so the window row reads cleanly from overview.
    for i,z in enumerate((5.55,4.68,3.81,2.94,2.07,1.20,.33,-.54,-1.41,-2.28,-3.15,-4.02,-4.89),1):
        # Restrained corner cuts keep the silhouette readable without the old
        # concentric-ring tessellation that made each pane look shattered.
        meshes[f'cabin_window_{i}']=surface_quad([
            (z-.19,148),(z-.15,145.5),(z+.15,145.5),(z+.19,148),
            (z+.19,156),(z+.15,158.5),(z-.15,158.5),(z-.19,156)
        ],.021)
        meshes[f'cabin_window_r{i}']=surface_quad([
            (z-.19,24),(z-.15,21.5),(z+.15,21.5),(z+.19,24),
            (z+.19,32),(z+.15,34.5),(z-.15,34.5),(z-.19,32)
        ],.021)

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
    meshes['tail_fin']=profile_prism([
        (-9.82,2.15),(-5.72,2.72),(-6.18,3.42),(-6.62,4.34),(-7.02,5.30),(-7.40,6.30),(-7.78,7.38),(-7.96,7.59),(-8.92,7.59)
    ],.16)
    meshes['rudder']=profile_prism([
        (-9.78,2.62),(-8.86,7.50),(-9.42,7.50),(-9.98,2.48)
    ],.045)
    meshes['tail_root_fairing']=profile_prism([
        (-10.00,2.12),(-9.22,3.12),(-7.88,3.68),(-6.28,2.73),(-5.52,2.52)
    ],.25)
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

    # Nose gear sits below the flight deck rather than under the cabin centre.
    for name in list(meshes):
        if name.startswith(('gear_nose','gear_oleo_nose','gear_scissors_nose','gear_door_nose',
                            'tire_nose_','wheel_nose_','rim_nose_')):
            verts,idx=meshes[name];verts=verts.copy();verts[:,2]+=1.0;meshes[name]=(verts,idx)

    # Smooth saddle fillets where the high wing meets the cabin roof. These are
    # static skin, separate from the flaps and other articulated surfaces.
    for side,label in ((-1,'left'),(1,'right')):
        fairing=v01._v05.oval_lathe_fuselage([(-1.72,.10,.07,2.94),(-1.35,.34,.18,3.01),(1.20,.40,.21,3.08),(1.82,.12,.08,3.02)],segments=24)
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

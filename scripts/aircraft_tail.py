"""Project-owned connected empennages: curved roots and matched movable surfaces.

Profiles are metre-authored per type in aircraft_tail_profiles.json. The root of
any vertical surface is fitted into the actual pressure body/boom, not a bounding
box. Stabiliser and control surface share the same aerofoil and hinge section.
"""
from pathlib import Path
import json
import numpy as np

PROFILES = json.loads(Path(__file__).with_name('aircraft_tail_profiles.json').read_text())
HINGE = .70


def merge(pieces):
    vertices, indices, offset = [], [], 0
    for v, i in pieces:
        vertices.append(v); indices.append(i.astype(np.uint32)+offset); offset += len(v)
    if offset >= 65536:
        raise ValueError('tail exceeds UInt16 vertex budget')
    return np.concatenate(vertices).astype(np.float32), np.concatenate(indices).astype(np.uint16)


def shell(stations, *, vertical=False, chord=(0., 1.), root=None, root_span=None,
          root_height=None, span_samples=None):
    """Closed shared-vertex aerofoil, with outward winding and finite trailing edge."""
    rows = np.asarray(stations, float)
    # A design station may differ from the uniform sample by one float ulp.
    # Coalesce those rows before float32 export to avoid zero-width faces.
    samples=np.linspace(rows[0,0],rows[-1,0],25) if span_samples is None else np.asarray(span_samples)
    samples=samples[(samples>=rows[0,0]-1e-6)&(samples<=rows[-1,0]+1e-6)]
    spans = np.unique(np.round(np.r_[rows[:, 0],samples],6))
    params = np.stack([np.interp(spans, rows[:, 0], rows[:, i]) for i in range(1, 5)], axis=1)
    # Include the hinge explicitly so fixed and moving shells meet exactly.
    us = np.unique(np.r_[np.linspace(chord[0], chord[1], 25), chord[0], chord[1]])
    loop = [(u, 1) for u in us]+[(u, -1) for u in us[::-1]]
    vertices = []
    for span, (offset, le, length, thickness) in zip(spans, params):
        for u, side in loop:
            z = le-u*length
            # Normalised symmetric NACA-like thickness; round LE, thin finite TE.
            f = 5*(.2969*np.sqrt(u)-.1260*u-.3516*u*u+.2843*u**3-.1015*u**4)
            half = max(thickness*f, .0015)
            if vertical:
                y = span
                x = offset+side*half
                if root is not None:
                    rx, ry, cy = root(z)
                    crown = cy+ry*np.sqrt(max(0., 1-(x/max(rx, 1e-6))**2))
                    # Spread the curved attachment over the lower section. Every
                    # chord station embeds in the hull instead of spanning over it.
                    base, tip = root_span if root_span is not None else (rows[0, 0], rows[-1, 0])
                    weight = max(0., 1-(span-base)/max(.25, (tip-base)*.18))
                    y += weight*(crown-.045-base)
                vertices.append((x,y,z))
            else:
                y=offset+side*half
                if root is not None:
                    rx,ry,cy=root(z)
                    weight=max(0.,1-abs(span)/max(.20,rx*1.25))
                    # Thin aft fuselage sections can be shallower than the
                    # stabiliser aerofoil. Embed both faces of its centre ring,
                    # then smoothly restore the authored section outboard.
                    target=cy+np.clip(offset-root_height+side*half+ry*.15,-ry*.85,ry*.85)
                    y+=weight*(target-y)
                vertices.append((span,y,z))
    vertices = np.asarray(vertices,np.float32)
    n = len(loop); indices=[]
    for k in range(len(spans)-1):
        for j in range(n):
            a=k*n+j;b=k*n+(j+1)%n
            indices.extend((a,b,b+n,a,b+n,a+n))
    for k in (0,len(spans)-1):
        centre=len(vertices);vertices=np.vstack((vertices,vertices[k*n:(k+1)*n].mean(0)))
        for j in range(n):
            a=k*n+j;b=k*n+(j+1)%n
            indices.extend((centre,b,a) if k==0 else (centre,a,b))
    indices=np.asarray(indices,np.uint16)
    # X-span and Y-span parameterisations have opposite winding.
    tris=vertices[indices.reshape(-1,3)]
    volume=np.einsum('ij,ij->i',tris[:,0],np.cross(tris[:,1],tris[:,2])).sum()/6
    if volume<0:indices=indices.reshape(-1,3)[:,[0,2,1]].reshape(-1)
    return vertices.astype(np.float32),indices


def subset(rows, low, high):
    rows=np.asarray(rows,float)
    spans=np.unique(np.r_[low,rows[(rows[:,0]>low)&(rows[:,0]<high),0],high])
    return np.column_stack((spans,*[np.interp(spans,rows[:,0],rows[:,i]) for i in range(1,5)]))


def fin(meshes, rows, root, *, movable=True):
    rows=np.asarray(rows,float)
    if not movable:
        meshes['tail_fin']=shell(rows,vertical=True,root=root)
        return
    a,b=rows[0,0],rows[-1,0]
    lower=a+(b-a)*.10;upper=b-(b-a)*.08
    # Share tessellation as well as the mathematical aerofoil. Independently
    # sampled curved root/hinge edges can otherwise differ by visible millimetres.
    samples=np.r_[rows[:,0],np.linspace(a,b,25),lower,upper,a+(b-a)*.18]
    fit = dict(root=root, root_span=(a,b),span_samples=samples)
    # The aft root/tip remain static. The rudder fits the swept trailing 30%
    # between them; no separate oversized rectangular board behind the fin.
    meshes['tail_fin']=merge([
        shell(rows,vertical=True,chord=(0,HINGE),**fit),
        shell(subset(rows,a,lower),vertical=True,chord=(HINGE,1),**fit),
        shell(subset(rows,upper,b),vertical=True,chord=(HINGE,1),span_samples=samples)])
    meshes['rudder']=shell(subset(rows,lower,upper),vertical=True,chord=(HINGE,1),**fit)


def refine(meshes, type_id, body):
    """Replace tails only; hull, cockpit, wings, engines and landing gear survive."""
    if type_id in ('B412','TRAINER'):
        return utility(meshes,type_id,body)
    profile=PROFILES[type_id]
    # Obsolete independent tips/root boxes would overlap the revised aerofoils.
    for key in ('tail_fin_tip','tailplane_tip_l','tailplane_tip_r','tail_root_fairing',
                'tailplane_saddle','livery_tail_sweep','dorsal_fin'):
        meshes.pop(key,None)
    fin(meshes,profile['tail_fin'],body)
    horizontal=np.asarray(profile['tailplane'],float).copy()
    if type_id not in ('ATR42','DH8D'):
        _,ry,cy=body(horizontal[0,2]-horizontal[0,3]*.5)
        horizontal[:,1] += cy+ry*.15-horizontal[0,1]
    # Centre carry-through + two separate articulated elevators. Both surfaces
    # use the same thickness distribution, including the actual hinge section.
    half=horizontal[-1,0]
    left=horizontal[::-1].copy();left[:,0]*=-1
    all_rows=np.vstack((left[:-1],horizontal))
    inner=min(.15,half*.025)
    samples=np.r_[all_rows[:,0],np.linspace(-half,half,49),-inner,inner]
    fit=dict(span_samples=samples)
    if type_id not in ('ATR42','DH8D'):fit.update(root=body,root_height=horizontal[0,1])
    meshes['tailplane']=merge([shell(all_rows,chord=(0,HINGE),**fit),
        shell(subset(all_rows,-inner,inner),chord=(HINGE,1),**fit)])
    meshes['elevator_left']=shell(subset(all_rows,-half,-inner),chord=(HINGE,1),**fit)
    meshes['elevator_right']=shell(subset(all_rows,inner,half),chord=(HINGE,1),**fit)
    # A low dorsal extension follows the hull at its entire lower edge. It
    # enters the main fin rather than leaving a pointed floating triangle.
    first=np.asarray(profile['tail_fin'][0],float)
    le=first[2];reach=first[3]*.40
    crown=body(le+reach)[1:].sum()
    dorsal=[[crown-.05,0,le+reach,reach+.25,.15],
            [max(crown+.08,first[0]+.35),0,le+.12,.55,.10]]
    meshes['dorsal_fin']=shell(dorsal,vertical=True,root=body)
    if type_id in ('ATR42','DH8D'):
        # The T-tail centre bullet straddles both the fin crown and stabiliser.
        from aircraft_body import BodyProfile
        y=horizontal[0,1];z=horizontal[0,2];ch=horizontal[0,3]
        aft=z-ch-.10 if type_id=='ATR42' else -16.415
        stations=[(aft,.015,.015,y),(z-ch*.72,.18,.12,y),
                  (z-.12,.20,.11,y),(z+.25,.015,.015,y)]
        theta=np.arange(32)*2*np.pi/32
        v=np.array([(rx*np.cos(t),cy+ry*np.sin(t),zz) for zz,rx,ry,cy in stations for t in theta],np.float32)
        meshes['tailplane_saddle']=BodyProfile((v,None)).loft(segments=32)
    # Lamps authored on the old fin must follow the revised surface, otherwise
    # a small floating block remains beside an otherwise connected tail.
    rows=np.asarray(profile['tail_fin'],float)
    for name in ('tail_nav_light','beacon_top'):
        if name not in meshes:continue
        v,i=meshes[name];centre=(v.min(0)+v.max(0))*.5
        _,ry,cy=body(centre[2])
        if centre[1] < cy+ry+.15:continue  # existing fuselage lamp
        le=np.interp(centre[1],rows[:,0],rows[:,2])
        chord=np.interp(centre[1],rows[:,0],rows[:,3])
        z=le-chord+.025 if name=='tail_nav_light' else le-.08
        width=round(float(np.ptp(v[:,2])),5)
        moved=v.copy();moved[:,2]=z+np.where(v[:,2]<centre[2],-width*.5,width*.5)
        meshes[name]=(moved,i)
    if type_id=='DH8D' and 'hf_antenna' in meshes:
        v,i=meshes['hf_antenna'];base=float(v[:,1].min())+.03
        z=np.interp(base,rows[:,0],rows[:,2])-.12
        width=round(float(np.ptp(v[:,2])),5);centre=(v[:,2].min()+v[:,2].max())*.5
        moved=v.copy();moved[:,2]=z+np.where(v[:,2]<centre,-width*.5,width*.5)
        meshes['hf_antenna']=(moved,i)


def utility(meshes,type_id,body):
    """Rounded trainer tail and tapered Bell fin/stabilisers; retain rotor datums."""
    if type_id=='TRAINER':
        rows=[[1.40,0,-2.75,1.22,.12],[2.25,0,-3.10,.87,.10],[2.81,0,-3.38,.48,.07]]
        fin(meshes,rows,body,movable=False)
        meshes.pop('rudder_accent',None)
        # Fixed trainer rudder accent follows the real fin, no slab behind it.
        meshes['rudder_accent']=shell(rows,vertical=True,chord=(.76,1),root=body)
        h=[[0,1.44,-2.84,.94,.12],[1.8,1.44,-3.18,.58,.065]]
    else:
        # Retain the Bell's rotor hub/shaft exactly. The swept vertical fin has a
        # rounded section; its lower edge beds into the tail boom.
        from aircraft_body import BodyProfile
        boom=BodyProfile(meshes['tail_boom']).linear
        rows=[[2.45,0,-8.62,1.22,.16],[3.55,0,-9.00,1.06,.13],[4.56,0,-9.30,.68,.08]]
        fin(meshes,rows,boom,movable=False)
        h=[[0,2.62,-7.77,.72,.12],[2.05,2.62,-8.12,.45,.07]]
    h=np.asarray(h,float)
    for side,name in ((-1,'left'),(1,'right')):
        rows=h.copy()
        if side<0:rows=rows[::-1];rows[:,0]*=-1
        meshes['tailplane_'+name]=shell(rows,root=body if type_id=='TRAINER' else boom,
                                      root_height=h[0,1])

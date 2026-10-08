#!/usr/bin/env python3
"""Check shipped tail attachment, shared hinge sections, profiles and packaged mirrors."""
from pathlib import Path
import numpy as np
from aircraft_body import BodyProfile
from aircraft_tail import PROFILES
import importlib.util

# Independent dimensions transcribed from the referenced manufacturer drawings.
PUBLISHED_SPANS={'DH8D':9.27,'E190':12.08,'A320':12.45,'A21N':12.45,
                 'A359':18.79,'A339':19.40,'B789':19.81,'B78X':19.81}

HERE=Path(__file__).resolve().parent
spec=importlib.util.spec_from_file_location('tails',HERE/'refine-aircraft-tails.py')
tails=importlib.util.module_from_spec(spec);spec.loader.exec_module(tails)


def inside(v,body):
    radii=body(v[:,2]);rx,ry,cy=radii.T
    return (v[:,0]/np.maximum(rx,1e-6))**2+((v[:,1]-cy)/np.maximum(ry,1e-6))**2


def hinge_distance(a,b,axis):
    # Every front-edge vertex of a movable surface must be on the matching
    # fixed aerofoil. Use point-to-triangle distance, not bounding-box overlap.
    v,i=a;t=v[i.reshape(-1,3)].astype(float)
    # Shell rings have 25 upper and 25 lower points plus two end caps.
    # Their first/last points are the real hinge, including the curved root.
    front=b[:-2].reshape(-1,50,3)[:,[0,-1]].reshape(-1,3)
    worst=0.
    for p in np.asarray(front):
        # Broad-phase boxes retain faces that can genuinely touch the hinge.
        near=np.all(p>=t.min(1)-.01,axis=1)&np.all(p<=t.max(1)+.01,axis=1)
        ts=t[near]
        if not len(ts):raise AssertionError('hinge has no fixed surface within 1 cm')
        a,b,c=ts[:,0],ts[:,1],ts[:,2];ab=b-a;ac=c-a;n=np.cross(ab,ac)
        norm=np.einsum('ij,ij->i',n,n);distance=np.einsum('ij,ij->i',p-a,n)/np.maximum(norm,1e-20)
        q=p-distance[:,None]*n
        dot00=np.einsum('ij,ij->i',ab,ab);dot01=np.einsum('ij,ij->i',ab,ac);dot11=np.einsum('ij,ij->i',ac,ac)
        dot20=np.einsum('ij,ij->i',q-a,ab);dot21=np.einsum('ij,ij->i',q-a,ac);den=dot00*dot11-dot01**2
        u=(dot11*dot20-dot01*dot21)/np.maximum(den,1e-20);w=(dot00*dot21-dot01*dot20)/np.maximum(den,1e-20)
        valid=(u>=-1e-4)&(w>=-1e-4)&(u+w<=1.0001)&(norm>1e-16)
        ds=np.where(valid,np.abs(distance)*np.sqrt(norm),np.inf)
        for start,end in ((a,b),(b,c),(c,a)):
            e=end-start;s=np.clip(np.einsum('ij,ij->i',p-start,e)/np.maximum(np.einsum('ij,ij->i',e,e),1e-20),0,1)
            ds=np.minimum(ds,np.linalg.norm(start+s[:,None]*e-p,axis=1))
        worst=max(worst,float(ds.min()))
    return worst


def main():
    for cid,path in tails.models().items():
        m=tails.readkit(path);type_id='A320' if cid=='A320_SOURCE' else cid
        hull=np.concatenate([v for n,(v,_) in m.items() if n in ('fuselage','fuselage_port')])
        body=BodyProfile((hull,None)).linear
        boom=BodyProfile(m['tail_boom']).linear if cid=='B412' else body
        for n,(v,i) in m.items():
            if n.startswith(('tail','rudder','elevator','dorsal')):
                assert np.isfinite(v).all() and i.max()<len(v),(cid,n,'invalid mesh')
                assert len(v)<65536,(cid,n,'UInt16 overflow')
                t=v[i.reshape(-1,3)];norm=np.linalg.norm(np.cross(t[:,1]-t[:,0],t[:,2]-t[:,0]),axis=1)
                assert np.count_nonzero(norm<1e-9)<len(t)*.05,(cid,n,'degenerate mesh')
        # First shell's first ring is the full chordwise curved fin root.
        root=m['tail_fin'][0][:50]
        assert np.max(inside(root,boom))<1.002,(cid,'fin root outside hull',np.max(inside(root,boom)))
        if type_id in PROFILES:
            for moving,axis in (('rudder',1),('elevator_left',0),('elevator_right',0)):
                fixed='tail_fin' if axis==1 else 'tailplane'
                gap=hinge_distance(m[fixed],m[moving][0],axis)
                assert gap<.006,(cid,moving,'hinge gap',gap)
            if type_id not in ('ATR42','DH8D'):
                v=m['tailplane'][0];root=v[np.abs(v[:,0])<1e-6]
                assert len(root)>20 and inside(root,body).max()<1.002,(cid,'stabiliser root outside hull')
            else:
                # Verify actual intersection of the horizontal tail with the fin
                # crown: centre-section material must overlap, not just a saddle.
                v=m['tailplane'][0];centre=v[np.abs(v[:,0])<1e-6];finv=m['tail_fin'][0]
                y=float(centre[:,1].mean());rows=np.asarray(PROFILES[type_id]['tail_fin'])
                le=np.interp(y,rows[:,0],rows[:,2]);ch=np.interp(y,rows[:,0],rows[:,3])
                assert (centre[:,2].min()<le and centre[:,2].max()>le-ch),(cid,'T-tail disconnected')
            span=np.ptp(m['tailplane'][0][:,0]);expected=2*PROFILES[type_id]['tailplane'][-1][0]
            assert abs(span-expected)<1e-4,(cid,'span differs from profile')
            if type_id in PUBLISHED_SPANS:
                assert abs(span-PUBLISHED_SPANS[type_id])<.001,(cid,'published stabiliser span')
        else:
            for side in ('left','right'):
                v=m['tailplane_'+side][0];root=v[np.abs(v[:,0])<1e-6]
                assert inside(root,boom).max()<1.002,(cid,'utility stabiliser outside hull')
        for ext in ('.gltf','.bin'):
            mirror=ROOT/'game/Airside/Assets/StreamingAssets/Airside/Art'/path.relative_to(tails.ART).with_suffix(ext)
            assert path.with_suffix(ext).read_bytes()==mirror.read_bytes(),(cid,'packaged mirror differs',ext)
        print(cid+': embedded roots, matched hinges, valid tail meshes and packaged mirror')

ROOT=HERE.parent
if __name__=='__main__':main()

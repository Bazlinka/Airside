#!/usr/bin/env python3
"""Original fitted aircraft paint and flush spoiler panels (ADR 0150).

Clips paint polygons against the actual airframe triangles. No ellipse guess,
flat decal boards, texture stretching or extra material slots per window.
Standalone: finish the existing polished runtime kits; glazing regeneration calls
finish() itself. Each type has an authored composition in PROFILES. Colour is
supplied by AircraftLiveryPaint in Unity, so the player's airline colour survives.
"""
from pathlib import Path
import argparse
import importlib.util
import json
import numpy as np

HERE = Path(__file__).resolve().parent
ROOT = HERE.parent
ART = ROOT / 'game/Airside/Assets/Airside/Art/Models/Aircraft'

# name, ribbon family, sweep begins (fraction nose -> tail), lower belt, belt width
PROFILES = {
    'ATR42': ('Saltwater', 'feather', .63, -.38, .17),
    'SF34': ('Ochre Country', 'sunrise', .69, -.31, .14),
    'DH8D': ('Coastal Current', 'current', .70, -.40, .17),
    'E190': ('Southern Star', 'compass', .67, -.34, .18),
    'A223': ('Morning Light', 'rays', .66, -.39, .20),
    'A320': ('Tidal Arc', 'tide', .66, -.37, .22),
    'B738': ('Outback Horizon', 'horizon', .71, -.34, .18),
    'B38M': ('Crosswind', 'crosswind', .68, -.40, .22),
    'A21N': ('Long Coast', 'coast', .73, -.36, .19),
    'A359': ('Southern Aurora', 'aurora', .68, -.40, .23),
    'A339': ('Desert Dawn', 'dawn', .71, -.38, .24),
    'B789': ('Ocean Reach', 'ocean', .69, -.42, .23),
    'B78X': ('Southern Meridian', 'meridian', .73, -.38, .23),
}


# Each convex polygon is clipped independently to both sides of the real fin.
# Thirteen silhouettes: no logo is reused across catalogue types.
MOTIFS = {
    'feather': [[(.17,.43),(.73,.64),(.58,.68),(.15,.50)],
               [(.18,.60),(.62,.77),(.49,.81),(.16,.66)]],
    'current': [[(.15,.39),(.55,.51),(.69,.65),(.50,.58),(.15,.46)],
                [(.19,.61),(.58,.72),(.63,.82),(.43,.75),(.18,.67)]],
    'rays': [[(.17,.46),(.65,.46),(.61,.51),(.17,.51)],
             [(.31,.55),(.26,.74),(.32,.77),(.37,.55)],
             [(.41,.55),(.44,.83),(.50,.81),(.47,.55)],
             [(.51,.55),(.64,.72),(.68,.68),(.57,.54)]],
    'tide': [[(.15,.43),(.55,.43),(.70,.58),(.35,.55)],
             [(.19,.65),(.52,.58),(.66,.67),(.36,.75)]],
    'horizon': [[(.14,.43),(.68,.43),(.64,.49),(.14,.49)],
                [(.18,.54),(.42,.73),(.50,.62),(.33,.54)],
                [(.43,.54),(.52,.66),(.65,.54)]],
    'crosswind': [[(.17,.41),(.28,.42),(.56,.80),(.47,.82)],
                  [(.17,.68),(.23,.76),(.65,.50),(.63,.43)]],
    'coast': [[(.15,.42),(.29,.45),(.58,.77),(.53,.83)],
              [(.35,.46),(.43,.48),(.66,.69),(.65,.77)]],
    'aurora': [[(.17,.40),(.29,.43),(.47,.79),(.40,.82)],
               [(.32,.43),(.43,.46),(.62,.73),(.57,.80)],
               [(.49,.46),(.58,.47),(.72,.62),(.70,.70)]],
    'dawn': [[(.15,.43),(.67,.43),(.64,.48),(.15,.48)],
             [(.23,.57),(.40,.79),(.59,.57),(.41,.62)]],
    'ocean': [[(.15,.46),(.48,.57),(.68,.55),(.53,.66),(.28,.58)],
              [(.20,.67),(.50,.74),(.60,.70),(.47,.81),(.25,.75)]],
    'meridian': [[(.35,.41),(.42,.41),(.48,.82),(.41,.84)],
                 [(.17,.59),(.34,.72),(.32,.63)],
                 [(.50,.64),(.66,.54),(.53,.54)]]
}


def emblem_polygons(family):
    if family == 'sunrise':
        angles = np.linspace(0, np.pi, 20)
        return [[(.38,.52), (.38+.19*np.cos(a),.52+.19*np.sin(a)),
                 (.38+.19*np.cos(b),.52+.19*np.sin(b))]
                for a,b in zip(angles[:-1],angles[1:])] + [
                    [(.14,.46),(.65,.46),(.60,.50),(.14,.50)]]
    if family == 'compass':
        tips = [(.38,.86),(.43,.66),(.65,.61),(.43,.56),
                (.38,.39),(.33,.56),(.15,.61),(.33,.66)]
        return [[(.38,.61),a,b] for a,b in zip(tips,tips[1:]+tips[:1])]
    return MOTIFS[family]


def triangulate_polygon(polygon):
    """Ear-clip concave marks so the convex surface clip never bridges a notch."""
    points = [np.array(p, float) for p in polygon]
    def cross(a, b, c):
        u, v = b-a, c-a
        return u[0]*v[1]-u[1]*v[0]
    if sum(cross(np.zeros(2),a,b) for a,b in zip(points,points[1:]+points[:1])) < 0:
        points.reverse()
    triangles = []
    while len(points) > 3:
        for i in range(len(points)):
            a,b,c = points[i-1],points[i],points[(i+1)%len(points)]
            if cross(a,b,c) <= 1e-9:
                continue
            others = [p for j,p in enumerate(points) if j not in ((i-1)%len(points),i,(i+1)%len(points))]
            if any(min(cross(a,b,p),cross(b,c,p),cross(c,a,p)) >= -1e-9 for p in others):
                continue
            triangles.append([a,b,c])
            del points[i]
            break
        else:
            raise ValueError('invalid emblem polygon')
    triangles.append(points)
    return triangles


def module(filename):
    spec = importlib.util.spec_from_file_location(filename.replace('-', '_'), HERE / filename)
    obj = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(obj)
    return obj


def load_meshes(path):
    data = json.loads(path.read_text())
    blob = (path.parent / data['buffers'][0]['uri']).read_bytes()
    def acc(index):
        a = data['accessors'][index]; v = data['bufferViews'][a['bufferView']]
        width = {'SCALAR': 1, 'VEC3': 3}[a['type']]
        dtype = {5126: '<f4', 5123: '<u2', 5125: '<u4'}[a['componentType']]
        return np.frombuffer(blob, dtype=dtype, count=a['count']*width,
            offset=v.get('byteOffset', 0)+a.get('byteOffset', 0)).reshape(-1, width).copy()
    result = {}
    for node in data['nodes']:
        primitive = data['meshes'][node['mesh']]['primitives'][0]
        result[node['name']] = (acc(primitive['attributes']['POSITION']), acc(primitive['indices']).reshape(-1))
    return result


def merge(pieces, allow32=False):
    pieces = [p for p in pieces if len(p[1])]
    if not pieces: return np.empty((0,3), np.float32), np.empty(0, np.uint16)
    vertices = []; indices = []; count = 0
    for v, i in pieces:
        vertices.append(v); indices.append(i.astype(np.uint32)+count); count += len(v)
    if count > 65535 and not allow32: raise ValueError('paint mesh exceeds 16-bit loader contract')
    return np.concatenate(vertices).astype(np.float32), np.concatenate(indices).astype(np.uint32 if allow32 else np.uint16)


def clip(mesh, polygon, axes=(2,1), side=None, offset=.012):
    """Clip triangles to a convex projected polygon, keeping their exact 3D surface."""
    vertices, indices = mesh
    poly = np.asarray(polygon, float)
    area = sum(a[0]*b[1]-a[1]*b[0] for a,b in zip(poly,np.roll(poly,-1,axis=0)))
    if area < 0: poly = poly[::-1]
    triangles = vertices[indices.reshape(-1,3)]
    uv = triangles[:,:,axes]
    low, high = poly.min(0), poly.max(0)
    keep = np.all(uv.max(1) >= low-1e-7,1) & np.all(uv.min(1) <= high+1e-7,1)
    if side is not None: keep &= triangles[:,:,0].mean(1)*side > .001
    out = []; faces = []
    for tri in triangles[keep]:
        points = list(tri.astype(float))
        for a,b in zip(poly, np.roll(poly,-1,axis=0)):
            if not points: break
            edge = b-a
            def distance(p):
                q = p[list(axes)]-a
                return edge[0]*q[1]-edge[1]*q[0]
            next_points = []
            for p,q in zip(points, points[1:]+points[:1]):
                dp,dq = distance(p),distance(q)
                pin,qin = dp >= -1e-8,dq >= -1e-8
                if pin: next_points.append(p)
                if pin != qin: next_points.append(p+(q-p)*dp/(dp-dq))
            points = next_points
        if len(points)<3: continue
        start = len(out)
        for p in points:
            p = p.copy()
            if axes == (2,1): p[0] += (side if side is not None else np.sign(p[0]))*offset
            else: p[1] += offset
            out.append(p)
        for i in range(1,len(points)-1):
            if np.linalg.norm(np.cross(points[i]-points[0],points[i+1]-points[0]))>1e-9:
                faces.extend((start,start+i,start+i+1))
    return np.asarray(out,np.float32).reshape(-1,3),np.asarray(faces,np.uint16)


def flush_spoilers(meshes):
    """Replace raised boxes with a thin patch clipped to the actual upper wing."""
    for name in list(meshes):
        if not name.startswith('spoiler_'): continue
        side = 'left' if ('left' in name or name.endswith('_l')) else 'right'
        wing = meshes.get('wing_'+side)
        if wing is None: continue
        v,_ = meshes[name]
        # The footprint is an authored quadrilateral; a convex hull handles swept wings.
        points = sorted(set(map(tuple,v[:,(0,2)])))
        def cross(o,a,b): return (a[0]-o[0])*(b[1]-o[1])-(a[1]-o[1])*(b[0]-o[0])
        lower=[];upper=[]
        for p in points:
            while len(lower)>=2 and cross(lower[-2],lower[-1],p)<=0: lower.pop()
            lower.append(p)
        for p in reversed(points):
            while len(upper)>=2 and cross(upper[-2],upper[-1],p)<=0: upper.pop()
            upper.append(p)
        hull=lower[:-1]+upper[:-1]
        wv,wi=wing;tri=wi.reshape(-1,3);p=wv[tri]
        # Loft winding differs by wing; upper triangles lie above the wing's local
        # camber line. Select by the authored outward normal and its sign at the top.
        normals=np.cross(p[:,1]-p[:,0],p[:,2]-p[:,0])
        top=normals[:,1]>0
        if np.median(p[top,:,1]) < np.median(p[~top,:,1]): top=~top
        patch=clip((wv,tri[top].reshape(-1)),hull,axes=(0,2),offset=.008)
        if len(patch[1]): meshes[name]=patch


def finish(meshes, type_id):
    """Final paint pass; fuselage/glazing, gear and type dimensions survive."""
    _, family, sweep, belt, width = PROFILES[type_id]
    for name in list(meshes):
        if name.startswith('livery_'): del meshes[name]
    flush_spoilers(meshes)
    # The shared narrowbody pylon top extended above the wing and appeared as a
    # rectangular block. Fit the upper edge into the wing's lower skin instead.
    for side in ('left','right'):
        pylon=meshes.get('pylon_'+side); wing=meshes.get('wing_'+side)
        if pylon is None or wing is None: continue
        pv,pi=pylon; wv,_=wing
        stations=np.unique(wv[:,0])
        underside=np.array([wv[wv[:,0]==x,1].min() for x in stations])
        limit=np.interp(pv[:,0],stations,underside)+.025
        pv=pv.copy(); pv[:,1]=np.minimum(pv[:,1],limit)
        meshes['pylon_'+side]=(pv,pi)
    skin=merge([mesh for name,mesh in meshes.items() if name in ('fuselage','fuselage_port')], allow32=True)
    v,_=skin; zmin,zmax=v[:,2].min(),v[:,2].max(); length=zmax-zmin
    wide=v[np.abs(v[:,0])>np.abs(v[:,0]).max()*.97]
    cy=(wide[:,1].max()+wide[:,1].min())*.5
    # Cross-section height from all mid-body vertices, not only the horizontal rim.
    middle=v[(v[:,2]>zmin+length*.35)&(v[:,2]<zmin+length*.65)]
    if not len(middle): middle=v
    cy=(middle[:,1].max()+middle[:,1].min())*.5; ry=np.ptp(middle[:,1])*.5
    def point(t,h): return (zmax-t*length,cy+h*ry)
    # Fine, continuous belt below the windows. The last aft panel climbs into the fin.
    ts=np.linspace(.15,.935,30)
    def height(t):
        q=max(0.,(t-sweep)/(.935-sweep))
        if family in ('horizon', 'meridian', 'crosswind'):
            rise = q*q  # a crisp, late ascending ribbon
        elif family in ('current', 'tide', 'ocean'):
            rise = np.sin(q*np.pi*.5)**2  # a broad wave sweep
        else:
            rise = q*q*(3-2*q)
        return belt+1.34*rise
    secondary=[]
    for side,key in ((-1,'livery_stripe'),(1,'livery_stripe_lower')):
        primary=[]
        for a,b in zip(ts[:-1],ts[1:]):
            taper=min(1.,(a-.14)/.075)
            ha,hb=height(a),height(b)
            wa=width*taper; wb=width*min(1.,(b-.14)/.075)
            primary.append(clip(skin,[point(a,ha-wa),point(b,hb-wb),point(b,hb),point(a,ha)],side=side))
            # A separated pinstripe gives the belt an intentional two-tone edge.
            secondary.append(clip(skin,[point(a,ha+.035),point(b,hb+.035),point(b,hb+.095),point(a,ha+.095)],side=side,offset=.014))
        meshes[key]=merge(primary)
    tail=merge([mesh for name,mesh in meshes.items() if name in ('tail_fin','tail_fin_tip')])
    tv,_=tail; ymin,ymax=tv[:,1].min(),tv[:,1].max(); tzmin,tzmax=tv[:,2].min(),tv[:,2].max()
    # Work in the fin's projected silhouette; clipping fits even swept and T-tail fins.
    tail_vertices, tail_indices = tail
    tail_edges = tail_vertices[tail_indices.reshape(-1,3)][:,((0,1),(1,2),(2,0)),:].reshape(-1,2,3)
    def tailpoint(u,h):
        # A swept fin's bounding rectangle contains empty space. Fit each symbol row
        # into the actual projected silhouette so rays/pointers cannot be cut away.
        y = ymin+(ymax-ymin)*h
        a,b = tail_edges[:,0,:],tail_edges[:,1,:]
        dy = b[:,1]-a[:,1]
        crosses = (np.minimum(a[:,1],b[:,1]) <= y) & (np.maximum(a[:,1],b[:,1]) >= y) & (np.abs(dy)>1e-8)
        hits = a[crosses,2]+(b[crosses,2]-a[crosses,2])*(y-a[crosses,1])/dy[crosses]
        if not len(hits): raise ValueError('fin silhouette has no span')
        return (hits.min()+(hits.max()-hits.min())*u,y)
    emblems=[]
    for side in (-1,1):
        # Contrasting lower diagonal echo under the white symbol.
        secondary.append(clip(tail,[tailpoint(.05,.16),tailpoint(.92,.39),tailpoint(.88,.48),tailpoint(.03,.25)],side=side,offset=.018))
        for polygon in emblem_polygons(family):
            for triangle in triangulate_polygon(polygon):
                emblems.append(clip(tail,[tailpoint(*p) for p in triangle],side=side,offset=.022))
    meshes['livery_secondary']=merge(secondary)
    meshes['livery_emblem']=merge(emblems)
    # Cowl accents are painted shells. Leave intake lips, exhausts and pylons metal/grey.
    for name,mesh in list(meshes.items()):
        if name not in ('engine_left','engine_right','nacelle_left','nacelle_right'): continue
        side='left' if name.endswith('left') else 'right'
        if name.startswith('nacelle_') and 'engine_'+side in meshes:
            continue  # On jets this separate nacelle node is only the metal intake lip.
        p,_=mesh; lo=p.min(0); hi=p.max(0)
        za=lo[2]+(hi[2]-lo[2])*.20; zb=lo[2]+(hi[2]-lo[2])*.38
        # Full cowl rings share the cylinder's original triangles, pushed radially 1 cm.
        patch=clip(mesh,[(za,lo[1]-1),(zb,lo[1]-1),(zb,hi[1]+1),(za,hi[1]+1)],offset=0)
        pv,pi=patch
        radial=pv[:,:2]-(lo[:2]+hi[:2])*.5
        radial/=np.maximum(np.linalg.norm(radial,axis=1,keepdims=True),1e-6)
        pv[:,:2]+=radial*.012
        meshes['livery_cowl_'+side]=(pv,pi)
    return meshes


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('types',nargs='*')
    parser.add_argument('--output-dir',type=Path,default=ART)
    args=parser.parse_args()
    glazing=module('polish-aircraft-glazing.py')
    writer=module('generate-authored-fbx-turboprop-terminal.py').write_kit
    for cid in args.types or PROFILES:
        basename=glazing.SOURCES[cid][2]
        meshes=load_meshes(ART/(basename+'.gltf'))
        filename,function,_=glazing.SOURCES[cid]
        source=getattr(glazing.load_module(filename),function)()
        for name,mesh in source.items():
            if name.startswith(('spoiler_','pylon_')): meshes[name]=mesh
        finish(meshes,cid)
        args.output_dir.mkdir(parents=True,exist_ok=True)
        writer(args.output_dir,basename,meshes)
        print(cid,PROFILES[cid][0],sum(len(i)//3 for _,i in meshes.values()),'triangles',flush=True)

if __name__=='__main__': main()

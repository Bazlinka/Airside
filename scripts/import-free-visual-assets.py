#!/usr/bin/env python3
"""Offline free-asset derivatives: fitted A320 parts, service wheels/cab and foliage.

Source geometry and licence evidence are kept in sources/free-visuals. No network,
Blender, airline textures or paid components needed. Existing skin apertures, doors,
articulation names and vehicle dimensions remain the presentation contract.
"""
from pathlib import Path
import json,struct,gzip,importlib.util
import numpy as np
from PIL import Image,ImageFilter
from vrml97_geometry import Parser,meshes
ROOT=Path(__file__).resolve().parents[1]
ART=ROOT/'game/Airside/Assets/Airside/Art'
SRC=ROOT/'scripts/sources/free-visuals'
def module(file):
 s=importlib.util.spec_from_file_location(file,ROOT/'scripts'/file);m=importlib.util.module_from_spec(s);s.loader.exec_module(m);return m
batch=module('generate-batch-c-models.py')
thumb=module('render-aircraft-thumbnails.py')
def readkit(path):
 j=json.loads(path.read_text());blob=path.with_name(j['buffers'][0]['uri']).read_bytes()
 def acc(i):
  a=j['accessors'][i];v=j['bufferViews'][a['bufferView']];n={'SCALAR':1,'VEC3':3}[a['type']];dt={5126:'<f4',5123:'<u2',5125:'<u4'}[a['componentType']]
  return np.frombuffer(blob,dtype=dt,count=a['count']*n,offset=v.get('byteOffset',0)+a.get('byteOffset',0)).reshape(-1,n).copy()
 result={}
 for n in j['nodes']:
  p=j['meshes'][n['mesh']]['primitives'][0];v=acc(p['attributes']['POSITION']);i=acc(p['indices']).reshape(-1);assert len(v)<65536 and i.max()<len(v);result[n['name']]=(v,i.astype(np.uint16))
 return result
def fit(v,bounds):
 lo,hi=bounds; a=v.min(0);b=v.max(0);return (v-a)/np.maximum(b-a,1e-6)*(hi-lo)+lo
def bounds(items):
 v=np.concatenate([m[0] for m in items]);return v.min(0),v.max(0)
def save(relative,kit):
 path=ART/relative
 existing_meta={Path(str(q)+'.meta'):Path(str(q)+'.meta').read_bytes() for q in [path,path.with_suffix('.bin')] if Path(str(q)+'.meta').exists()}
 batch.pack_gltf(path,kit)
 for meta,data in existing_meta.items():meta.write_bytes(data)
 # Generators must not rotate Unity identities on each run.
 for q in [path,path.with_suffix('.bin')]:
  if not Path(str(q)+'.meta').exists():batch.write_default_meta(q)
 j=json.loads(path.read_text());j['asset']['generator']='Airside free source adaptation, 2026-10-08';path.write_text(json.dumps(j,indent=2)+'\n')
 print(relative,len(kit),'parts',sum(len(x[1])//3 for x in kit.values()),'triangles')

def glb(file):
 b=(SRC/file).read_bytes();size,kind=struct.unpack_from('<II',b,12);j=json.loads(b[20:20+size]);blob=b[28+size:]
 def acc(i):
  a=j['accessors'][i];v=j['bufferViews'][a['bufferView']];n={'SCALAR':1,'VEC2':2,'VEC3':3,'VEC4':4}[a['type']];dt={5126:'<f4',5123:'<u2',5125:'<u4',5121:'u1'}[a['componentType']];off=v.get('byteOffset',0)+a.get('byteOffset',0)
  return np.ndarray((a['count'],n),dtype=dt,buffer=blob,offset=off,strides=(v.get('byteStride',np.dtype(dt).itemsize*n),np.dtype(dt).itemsize)).copy()
 out=[]
 def walk(index,parent=np.eye(4)):
  n=j['nodes'][index];m=np.eye(4);m[:3,3]=n.get('translation',[0,0,0]);q=n.get('rotation',[0,0,0,1]);x,y,z,w=q;m[:3,:3]=np.array([[1-2*(y*y+z*z),2*(x*y-z*w),2*(x*z+y*w)],[2*(x*y+z*w),1-2*(x*x+z*z),2*(y*z-x*w)],[2*(x*z-y*w),2*(y*z+x*w),1-2*(x*x+y*y)]])@np.diag(n.get('scale',[1,1,1]));m=parent@m
  if 'mesh' in n:
   for p in j['meshes'][n['mesh']]['primitives']:
    v=acc(p['attributes']['POSITION']);v=(np.c_[v,np.ones(len(v))]@m.T)[:,:3];idx=acc(p['indices']).reshape(-1).astype(np.uint16);out.append((n.get('name','part'),v.astype(np.float32),idx))
  for child in n.get('children',[]):walk(child,m)
 for i in j['scenes'][j.get('scene',0)]['nodes']:walk(i)
 return out

def aircraft():
 original=readkit(ART/'Models/Aircraft/mdl_a320_200_v01.gltf');kit=dict(original)
 imported=meshes(Parser(gzip.decompress((SRC/'A320.wrl.gz').read_bytes()).decode('utf8')).all())
 by={}
 for name,tex,v,idx in imported:by.setdefault(name,[]).append((v.astype(np.float32),idx.reshape(-1).astype(np.uint16)))
 def merged(names):
  chunks=[x for n in names for x in by[n]];vs=[];ids=[];offset=0
  for v,i in chunks:vs.append(v);ids.append(i.astype(np.uint32)+offset);offset+=len(v)
  assert offset<65536;return np.concatenate(vs),np.concatenate(ids).astype(np.uint16)
 # Fitted engine casings and actual curved pylon surfaces from the supplied A320.
 # Flight deck/cabin skin stay authored: their genuine openings are needed for cabin views.
 for side,cas,tail,pylon in [('right','Cylinder01','Cylinder02','ChamferBox01'),('left','Cylinder207','Cylinder208','ChamferBox29')]:
  for target,source in [('engine_'+side,cas),('exhaust_'+side,tail),('pylon_'+side,pylon)]:
   v,i=merged([source])
   if target.startswith('engine_'):
    # The downloaded texture-painted inlet had a filled fan face. Remove its
    # central fore-cap so our genuinely rotating blades remain visible.
    tri=i.reshape(-1,3);t=v[tri];centre=(v.min(0)+v.max(0))/2
    radial=np.linalg.norm((t[:,:,:2]-centre[:2]),axis=2).mean(1)
    cut=(t[:,:,2].mean(1)>centre[2]) & (radial<.99)
    i=tri[~cut].reshape(-1)
   kit[target]=(fit(v,bounds([original[target]])).astype(np.float32),i)
  # Fan blade shapes retain the original fan pivots and individual spinning nodes.
  blades=[(n,v,i) for n,tex,v,i in imported if n.startswith('Box') and len(i)==20 and (v[:,0].mean()>4 if side=='right' else v[:,0].mean()<-4) and 6.23<v[:,2].mean()<6.35]
  if blades:
   group=np.concatenate([v for _,v,_ in blades]);a,b=group.min(0),group.max(0)
   targets=[n for n in original if n.startswith('fan_blade_'+('r' if side=='right' else 'l'))];lo,hi=bounds([original[n] for n in targets]);
   for n in targets:kit.pop(n)
   for k,(_,v,i) in enumerate(blades):kit['fan_blade_'+('r' if side=='right' else 'l')+str(k+1)]=((v-a)/np.maximum(b-a,1e-6)*(hi-lo)+lo,i.reshape(-1).astype(np.uint16))
 # Source tyre tread and rounded sidewalls improve all wheels without moving a gear datum.
 for n in [n for n in kit if n.startswith('tire_')]:
  v,i=merged(['Torus02' if 'nose' in n else 'Torus33']);kit[n]=(fit(v,bounds([original[n]])).astype(np.float32),i)
 # Source horizontal tail: resample into the existing envelope so elevators stay aligned.
 v,i=merged(['Cylinder05','Cylinder197']);kit['tailplane']=(fit(v,bounds([original['tailplane']])).astype(np.float32),i)
 # Finish after source adaptation so imported stabilisers cannot restore old
 # gaps or bypass the A320-specific tail profile.
 module('refine-aircraft-tails.py').apply(kit,'A320')
 save('Models/Aircraft/mdl_a320_200_v02.gltf',kit)

def vehicles():
 wheel=glb('wheel-truck.glb');v=np.concatenate([x[1] for x in wheel]);ids=[];o=0
 for _,p,i in wheel:ids.append(i+o);o+=len(p)
 idx=np.concatenate(ids); # source wheel axle X; Airside service vehicles have Z axles.
 v=v[:,[2,1,0]];idx=idx.reshape(-1,3)[:,[0,2,1]].reshape(-1)
 for base in ['mdl_fuel_truck_small_v06','mdl_baggage_tug_train_v06','mdl_passenger_bus_apron_v06','mdl_catering_truck_v01','mdl_pushback_tug_v03']:
  kit=readkit(ART/f'Models/Vehicles/{base}.gltf')
  for name,old in list(kit.items()):
   if name.startswith('wheel_'):kit[name]=(fit(v,bounds([old])).astype(np.float32),idx.astype(np.uint16))
  # Compact service cab uses the front of Kenney's truck; preserve our doors,
  # tanks, ladders, scissor lift and equipment bodies rather than importing a road truck.
  if base in ['mdl_fuel_truck_small_v06','mdl_catering_truck_v01'] and 'cab' in kit:
   body=next(x for x in glb('truck.glb') if x[0]=='body');pv,pi=body[1],body[2].reshape(-1,3);keep=(pv[pi][:,:,2].min(1)>.35);pi=pi[keep];used,ii=np.unique(pi.reshape(-1),return_inverse=True);pv=pv[used][:,[2,1,0]];kit['cab']=(fit(pv,bounds([kit['cab']])).astype(np.float32),ii.reshape(-1,3)[:,[0,2,1]].reshape(-1).astype(np.uint16))
  version={'v06':'v07','v01':'v02','v03':'v04'}[base[-3:]];save('Models/Vehicles/'+base[:-3]+version+'.gltf',kit)

def rounded_canopy(v,idx):
 # Weld only to find connected leaf lobes; one subdivision and a spherical blend
 # round the source's block forms while retaining its branching/cluster layout.
 points,inverse=np.unique(v,axis=0,return_inverse=True);tri=inverse[idx].reshape(-1,3);parent=list(range(len(points)))
 def root(i):
  while parent[i]!=i:parent[i]=parent[parent[i]];i=parent[i]
  return i
 for t in tri:
  a=root(t[0])
  for k in t[1:]:parent[root(k)]=a
 groups={}
 for t in tri:groups.setdefault(root(t[0]),[]).append(t)
 vertices=[];indices=[]
 for triangles in groups.values():
  tris=points[np.array(triangles)];lo=tris.reshape(-1,3).min(0);hi=tris.reshape(-1,3).max(0);centre=(lo+hi)/2;radius=np.maximum((hi-lo)/2,1e-4)
  out=[]
  for a,b,c in tris:
   ab=(a+b)/2;bc=(b+c)/2;ca=(c+a)/2
   out.extend([a,ab,ca,ab,b,bc,ca,bc,c,ab,bc,ca])
  out=np.asarray(out);d=(out-centre)/radius;length=np.linalg.norm(d,axis=1,keepdims=True);spherical=centre+d/np.maximum(length,1e-4)*radius
  out=out*.2+spherical*.8;offset=len(vertices);vertices.extend(out);indices.extend(range(offset,offset+len(out)))
 return np.array(vertices,np.float32),np.array(indices,np.uint16)

def nature():
 kit={}
 tree=glb('tree_thin.glb')
 kit['tree_foliage_0']=rounded_canopy(tree[0][1],tree[0][2]);kit['tree_trunk_0']=(tree[1][1],tree[1][2])
 bush=glb('plant_bushDetailed.glb')
 for k,(_,v,i) in enumerate(bush):kit[f'bush_foliage_{k}']=rounded_canopy(v,i)
 save('Models/Environment/mdl_local_foliage_v01.gltf',kit)

def cloth():
 a=np.asarray(Image.open(SRC/'fabric-colour.jpg').convert('RGB')).astype(float)/255
 l=a.mean(2);broad=np.asarray(Image.fromarray((l*255).astype("uint8")).filter(ImageFilter.GaussianBlur(18))).astype(float)/255
 grey=np.clip(.86+(l-broad)*.28,.79,.93)
 colour=(np.repeat(grey[:,:,None],3,2)*255).astype('uint8')
 surface=ART/'Textures/Surfaces'
 Image.fromarray(colour).save(surface/'tx_cabin_fabric_basecolor_v01.png')
 Image.open(SRC/'fabric-normal.jpg').save(surface/'tx_cabin_fabric_normal_v01.png')
 rough=np.asarray(Image.open(SRC/'fabric-rough.jpg').convert('L'));mask=np.zeros((*rough.shape,4),dtype='uint8');mask[:,:,3]=255-rough
 Image.fromarray(mask).save(surface/'tx_cabin_fabric_mask_v01.png')
if __name__=='__main__':aircraft();vehicles();nature();cloth()

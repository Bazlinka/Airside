import json,numpy as np,matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
from mpl_toolkits.mplot3d.art3d import Poly3DCollection
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
p=ROOT/'game/Airside/Assets/Airside/Art/Models/Aircraft/mdl_atr42_starter_v02.gltf';g=json.loads(p.read_text());b=(p.parent/g['buffers'][0]['uri']).read_bytes()
def arr(i):
 a=g['accessors'][i];v=g['bufferViews'][a['bufferView']];n={'VEC3':3,'SCALAR':1}[a['type']];return np.frombuffer(b,dtype={5126:'<f4',5123:'<u2',5125:'<u4'}[a['componentType']],count=a['count']*n,offset=v.get('byteOffset',0)+a.get('byteOffset',0)).reshape(-1,n)
def colour(n):
 if n.startswith(('cabin_window','windscreen','cockpit_side')):return np.array([.055,.15,.20])
 if n.startswith(('tire','propeller')) or n.startswith('intake'):return np.array([.075,.08,.085])
 if n.startswith(('wheel','rim','gear_','flap_track')):return np.array([.38,.40,.42])
 if n.startswith(('nav_light_left',)):return np.array([.1,.8,.25])
 if n.startswith(('nav_light_right',)):return np.array([.85,.1,.1])
 if 'tip' in n and 'propeller' in n:return np.array([.95,.72,.08])
 if n.startswith(('engine','nacelle','pylon','livery')):return np.array([.08,.34,.50])
 if n.startswith(('wing','flap','aileron','spoiler','tail','rudder','elevator')):return np.array([.28,.43,.36])
 if n.startswith(('door_outline','cargo_door_outline')):return np.array([.36,.4,.42])
 if n.startswith(('exhaust','antenna','pitot','static_wick')):return np.array([.22,.23,.24])
 return np.array([.88,.90,.91])
meshes=[]
for node in g['nodes']:
 if 'mesh' not in node:continue
 for prim in g['meshes'][node['mesh']]['primitives']:
  v=arr(prim['attributes']['POSITION']);tri=v[arr(prim['indices']).reshape(-1,3)]
  # World display axes: longitudinal, lateral, vertical.
  meshes.append((node['name'],tri[:,:,[2,0,1]],colour(node['name'])))
def draw(ax,elev,az,title,limits=None):
 light=np.array([-.35,-.25,.9]);light/=np.linalg.norm(light)
 triangles=[];colours=[]
 for n,t,c in meshes:
  normals=np.cross(t[:,1]-t[:,0],t[:,2]-t[:,0]);normals/=np.maximum(np.linalg.norm(normals,axis=1)[:,None],1e-8)
  shade=.58+.42*np.maximum(.15,np.abs(normals@light));triangles.extend(t);colours.extend(np.clip(c*shade[:,None],0,1))
 ax.add_collection3d(Poly3DCollection(triangles,facecolors=colours,edgecolors='none'))
 if limits:ax.set(xlim=limits[0],ylim=limits[1],zlim=limits[2]);aspect=tuple(b-a for a,b in limits)
 else:ax.set(xlim=(-11,13),ylim=(-13,13),zlim=(0,8));aspect=(24,26,8)
 ax.set_box_aspect(aspect);ax.view_init(elev,az);ax.set_proj_type('ortho');ax.set_axis_off();ax.set_title(title,fontsize=11,pad=3);ax.set_facecolor('#eef0ec')
fig=plt.figure(figsize=(14,9),facecolor='#eef0ec')
views=[
    (16,-48,'Front three-quarter',None),
    (3,-90,'Left profile',((-11,12.5),(-3,3),(0,7.8))),
    (8,2,'Nose and flight deck',((7.4,12.35),(-2.2,2.2),(0,3.7))),
    (14,138,'Rear three-quarter / joined tail',None),
]
for i,v in enumerate(views):draw(fig.add_subplot(2,2,i+1,projection='3d'),*v)
plt.tight_layout(pad=.7);plt.savefig(str(Path(__file__).resolve().parents[1] / 'docs/art/candidates/air_001_atr42_v02_mesh_review.png'),dpi=180)

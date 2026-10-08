import re,numpy as np
class Parser:
 def __init__(self,text): self.t=re.findall(r'"[^"\n]*"|[{}\[\]]|[^\s,{}\[\]]+',re.sub(r'#.*','',text));self.i=0;self.defs={}
 def pop(self): r=self.t[self.i];self.i+=1;return r
 def value(self):
  p=self.pop()
  if p=='[':
   a=[]
   while self.t[self.i]!=']': a.append(self.value())
   self.pop();return a
  if p=='USE':return self.defs[self.pop()]
  name=None
  if p=='DEF':name=self.pop();p=self.pop()
  if self.i<len(self.t) and self.t[self.i]=='{':
   self.pop(); d={'type':p}
   if name:d['name']=name;self.defs[name]=d
   while self.t[self.i]!='}':
    key=self.pop();n={'translation':3,'rotation':4,'scale':3,'center':3,'scaleOrientation':4,'diffuseColor':3,'specularColor':3,'emissiveColor':3,'bboxCenter':3,'bboxSize':3}.get(key,1)
    v=[self.value() for _ in range(n)];d[key]=v if n>1 else v[0]
   self.pop();return d
  if p.startswith('"'):return p[1:-1]
  try:return float(p)
  except ValueError:return p
 def all(self):
  a=[]
  while self.i<len(self.t): a.append(self.value())
  return a

def transform(n):
 m=np.eye(4);m[:3,3]=n.get('translation',[0,0,0]);r=n.get('rotation',[0,0,1,0]);axis=np.asarray(r[:3]);a=r[3];L=np.linalg.norm(axis)
 if L>0:
  x,y,z=axis/L;K=np.array([[0,-z,y],[z,0,-x],[-y,x,0]]);m[:3,:3]=np.eye(3)+np.sin(a)*K+(1-np.cos(a))*(K@K)
 m[:3,:3]=m[:3,:3]@np.diag(n.get('scale',[1,1,1]));return m

def meshes(nodes):
 out=[]
 def walk(n,m=np.eye(4),name=''):
  if not isinstance(n,dict):return
  name=n.get('name',name)
  if n['type']=='Transform': m=m@transform(n)
  if n['type']=='Shape':
   g=n['geometry'];v=np.asarray(g['coord']['point']).reshape(-1,3);v=(np.c_[v,np.ones(len(v))]@m.T)[:,:3];f=[];poly=[]
   for idx in g['coordIndex']:
    if idx<0:
     for j in range(1,len(poly)-1):f.append([poly[0],poly[j],poly[j+1]])
     poly=[]
    else:poly.append(int(idx))
   app=n.get('appearance',{}); tex=app.get('texture',{}).get('url','');tex=tex[0] if isinstance(tex,list) else tex
   out.append((name,tex,v,np.array(f)))
  for child in n.get('children',[]):walk(child,m,name)
 for n in nodes:walk(n)
 return out
if __name__=='__main__':
 import sys
 a=meshes(Parser(open(sys.argv[1]).read()).all());print('MESHS',len(a))
 for name,tex,v,f in a: print(name,tex,len(v),len(f),np.round(v.min(0),2),np.round(v.max(0),2))

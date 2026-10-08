#!/usr/bin/env python3
"""Refit every active tail in place, preserving unrelated runtime meshes and GUIDs."""
from pathlib import Path
import argparse
import importlib.util
import json
import numpy as np
from aircraft_body import BodyProfile
from aircraft_tail import refine

HERE=Path(__file__).resolve().parent
ROOT=HERE.parent
ART=ROOT/'game/Airside/Assets/Airside/Art'

def module(file):
    spec=importlib.util.spec_from_file_location(file.replace('-','_'),HERE/file)
    m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m);return m


def models():
    result={cid:ART/model for cid,model,_ in module('render-aircraft-thumbnails.py').MODELS}
    # Keep the A320 authored fallback consistent with the preferred adapted kit.
    result['A320_SOURCE']=ART/'Models/Aircraft/mdl_a320_200_v01.gltf'
    result['B412']=ART/'Models/Aircraft/mdl_bell_412_rescue_v01.gltf'
    result['TRAINER']=ART/'Models/Aircraft/mdl_parafield_trainer_v01.gltf'
    return result


def readkit(path):
    doc=json.loads(path.read_text());blob=path.with_name(doc['buffers'][0]['uri']).read_bytes()
    def acc(k):
        a=doc['accessors'][k];v=doc['bufferViews'][a['bufferView']]
        n={'SCALAR':1,'VEC3':3}[a['type']];dtype={5126:'<f4',5123:'<u2',5125:'<u4'}[a['componentType']]
        return np.frombuffer(blob,dtype=dtype,count=a['count']*n,
            offset=v.get('byteOffset',0)+a.get('byteOffset',0)).reshape(-1,n).copy()
    result={}
    for node in doc['nodes']:
        p=doc['meshes'][node['mesh']]['primitives'][0]
        result[node['name']]=(acc(p['attributes']['POSITION']),acc(p['indices']).reshape(-1))
    return result


def apply(meshes,cid):
    old=dict(meshes)
    hull=[mesh for name,mesh in meshes.items() if name in ('fuselage','fuselage_port')]
    v=np.concatenate([m[0] for m in hull]);body=BodyProfile((v,None)).linear
    refine(meshes,cid,body)
    paint=module('finish-aircraft-liveries.py')
    if cid in paint.PROFILES:
        paint.finish(meshes,cid,paint_only=True)
        # Paint clipping only needs new fin symbols/root signatures. Keep every
        # unrelated paint node byte-identical, including titles and engines.
        for name,mesh in old.items():
            if name.startswith('livery_') and name not in ('livery_secondary','livery_emblem','livery_tail_sweep'):
                meshes[name]=mesh
    elif cid=='B412':
        def bilateral(mesh,polygon,offset=.018):
            return paint.merge([paint.clip(mesh,t,side=s,offset=offset)
                for s in (-1,1) for t in paint.triangulate_polygon(polygon)])
        meshes['rescue_red_tail']=paint.merge([
            bilateral(meshes['tail_boom'],[(-9.8,1.4),(-2,1.4),(-2,2.08),(-9.8,2.85)]),
            bilateral(meshes['tail_fin'],[(-11,2.2),(-8,2.2),(-8,4.6),(-11,4.6)])])
        meshes['livery_emblem']=bilateral(meshes['tail_fin'],
            [(-9.90,3.03),(-9.45,3.84),(-8.94,3.03),(-9.23,3.03),(-9.45,3.42),(-9.66,3.03)],offset=.022)
    return old


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('types',nargs='*');parser.add_argument('--check',action='store_true')
    args=parser.parse_args();writer=module('generate-authored-fbx-turboprop-terminal.py')
    for cid,path in models().items():
        if args.types and cid not in args.types:continue
        meshes=readkit(path);old=apply(meshes,'A320' if cid=='A320_SOURCE' else cid)
        equal=meshes.keys()==old.keys() and all(np.array_equal(v,old[n][0]) and np.array_equal(i,old[n][1]) for n,(v,i) in meshes.items())
        if args.check:
            if not equal:raise AssertionError(cid+' tail regeneration differs')
            print(cid+': deterministic tail output')
        else:
            writer.write_kit(path.parent,path.stem,meshes)
            print(cid+': fitted tail, unrelated airframe retained',flush=True)

if __name__=='__main__':main()

#!/usr/bin/env python3
"""Agent-led issue reproduction on this Mac or a private Mac Actions runner; return real evidence."""
from __future__ import annotations
import argparse
import importlib.util
import json
import os
from pathlib import Path
import platform
import re
import subprocess
import sys
import time
import uuid

ROOT=Path(__file__).resolve().parents[1]
REPO='Bazlinka/Airside'
WORKFLOW='agent-diagnostics.yml'


def run(*args, **kwargs):
    return subprocess.run(list(args),cwd=ROOT,text=True,check=True,**kwargs)


def output(*args):
    return run(*args,stdout=subprocess.PIPE).stdout.strip()


def load_gameplay():
    spec=importlib.util.spec_from_file_location('agent_gameplay',ROOT/'scripts/agent-gameplay.py')
    module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module);return module


def request_validate(request):
    if not re.fullmatch(r'[a-f0-9]{40}',request.get('revision','')): raise ValueError('Use an exact 40-character commit SHA')
    if not re.fullmatch(r'[A-Za-z0-9-]{1,80}',request.get('request_id','')): raise ValueError('Invalid request ID')
    issue=request.get('issue','')
    if not isinstance(issue,str) or not 1<=len(issue)<=2000: raise ValueError('Issue text must be 1 to 2000 characters')
    scenario=request.get('scenario','')
    if not isinstance(scenario,str) or len(scenario)>20000:raise ValueError('Scenario JSON is too large')
    gameplay=load_gameplay()
    if scenario:
        plan=gameplay.validate_plan(json.loads(scenario),request['revision'])
        plan['issue']=issue
    else:plan=gameplay.issue_plan(issue,request['revision'],request.get('aircraft_type',''))
    return plan


def execute(request):
    """Only called locally or by authenticated manual workflow dispatch, never PR events."""
    plan=request_validate(request)
    directory=ROOT/'work/issue-diagnostics'/request['request_id'];directory.mkdir(parents=True,exist_ok=False)
    result=dict(request=request,status='blocked',evidence='requires agent inspection',commit=request['revision'])
    (directory/'request.json').write_text(json.dumps(request,indent=2))
    try:
        if platform.system()!='Darwin':raise ValueError('No local Mac; dispatch with --remote instead')
        current=output('git','rev-parse','HEAD')
        if current!=request['revision']:
            # Worker owns an isolated checkout. Local invocation must never replace someone's work.
            if not os.environ.get('GITHUB_ACTIONS'):raise ValueError('Checkout differs; use a clean scoped checkout or --remote')
            run('git','fetch','--no-tags','--depth=1','origin',request['revision'])
            run('git','checkout','--detach','--force',request['revision'])
            if output('git','rev-parse','HEAD')!=request['revision']:raise ValueError('Requested commit was not checked out')
        if output('git','status','--porcelain','--untracked-files=normal','--','game','scripts'):
            raise ValueError('Diagnostic source checkout is dirty')
        gameplay=load_gameplay()
        app=ROOT/'work/builds/Airside.app'
        try:gameplay.build_preflight(app,request['revision'],'')
        except ValueError:
            print('Building requested revision once for native reproduction',flush=True)
            try:run('bash','scripts/build-mac.sh')
            finally:
                # Clean preflight established these files were untouched. Unity rewrites
                # their generated metadata, including on a failed build; preserve source.
                run('git','restore','--','game/Airside/ProjectSettings/ProjectSettings.asset',
                    'game/Airside/Packages/packages-lock.json')
            gameplay.build_preflight(app,request['revision'],output('git','status','--porcelain','--','game','scripts'))
        plan_path=directory/'scenario.json';plan_path.write_text(json.dumps(plan,indent=2))
        # Issue probes settle weather/camera and require real frames; no substitute smoke pass.
        code=subprocess.run([sys.executable,str(ROOT/'scripts/agent-gameplay.py'),'--scenario',str(plan_path),
                             '--output',str(directory/'runs'),'--timeout','240'],cwd=ROOT).returncode
        result['status']='captured' if code==0 else 'failed'
        result['exitCode']=code
        result['next']='Inspect all relevant PNGs, per-step state and player.log; record reproduction/cause, fix, then repeat this scenario on the fix commit.'
        return 0 if code==0 else 1
    except (ValueError,OSError,subprocess.CalledProcessError) as error:
        result['error']=str(error);print(result['error'],file=sys.stderr);return 2
    finally:
        (directory/'diagnostic.json').write_text(json.dumps(result,indent=2))
        print('Diagnostic evidence: '+str(directory),flush=True)


def remote(request,destination,timeout):
    request_validate(request)
    # A private repo write-authorized dispatch, no arbitrary shell or network control port.
    fields=json.dumps({'ref':'main','inputs':request})
    run('gh','api','--method','POST',f'repos/{REPO}/actions/workflows/{WORKFLOW}/dispatches',
        '--input','-',input=fields,stdout=subprocess.PIPE)
    print('Mac diagnostic requested: '+request['request_id'],flush=True)
    deadline=time.monotonic()+timeout;run_id=None;next_progress=0
    while time.monotonic()<deadline:
        if run_id is None:
            items=json.loads(output('gh','run','list','--repo',REPO,'--workflow',WORKFLOW,'--event','workflow_dispatch',
                                    '--limit','50','--json','databaseId,displayTitle,status,conclusion'))
            match=next((item for item in items if item['displayTitle'].startswith('Diagnose '+request['request_id']+' at ')),None)
            if match:run_id=str(match['databaseId'])
        if run_id:
            state=json.loads(output('gh','run','view',run_id,'--repo',REPO,'--json','status,conclusion,url'))
            if state['status']=='completed':
                destination.mkdir(parents=True,exist_ok=False)
                run('gh','run','download',run_id,'--repo',REPO,'--name','issue-diagnostics-'+request['request_id'],'--dir',str(destination))
                print('Actual Mac evidence: '+str(destination),flush=True)
                print('Inspect PNGs/logs/state before diagnosing or claiming a fix. '+state['url'],flush=True)
                return 0 if state['conclusion']=='success' else 1
        if time.monotonic()>=next_progress:
            print('Waiting for Mac runner'+(' run '+run_id if run_id else '')+'; no evidence/pass yet',flush=True)
            next_progress=time.monotonic()+20
        time.sleep(3)
    # Leave explicit request identity for resuming; never silently call the investigation passed.
    print('Mac diagnostic timed out or runner is offline. Request '+request['request_id']+
          ('; run '+run_id if run_id else '')+'. Inspect/cancel the job before retrying.',file=sys.stderr)
    return 2


def main(argv=None):
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--issue')
    parser.add_argument('--scenario',type=Path)
    parser.add_argument('--aircraft-type',default='')
    parser.add_argument('--revision',default='HEAD')
    parser.add_argument('--remote',action='store_true')
    parser.add_argument('--plan',action='store_true')
    parser.add_argument('--timeout',type=int,default=1800)
    parser.add_argument('--output',type=Path,default=ROOT/'work/remote-diagnostics')
    parser.add_argument('--execute-request',action='store_true',help=argparse.SUPPRESS)
    args=parser.parse_args(argv)
    try:
        if args.execute_request:
            if not os.environ.get('GITHUB_ACTIONS'):raise ValueError('Workflow request execution requires GitHub Actions')
            request={key:os.environ.get('DIAGNOSTIC_'+env,'') for key,env in
                     [('request_id','ID'),('revision','REVISION'),('issue','ISSUE'),('scenario','SCENARIO'),('aircraft_type','AIRCRAFT')]}
            return execute(request)
        if not args.issue:raise ValueError('Describe the reported issue with --issue')
        if args.timeout<1:raise ValueError('Timeout must be positive')
        request=dict(request_id='diag-'+uuid.uuid4().hex[:16],revision=output('git','rev-parse',args.revision),issue=args.issue,
                     scenario=args.scenario.read_text() if args.scenario else '',aircraft_type=args.aircraft_type)
        plan=request_validate(request)
        if args.plan:print(json.dumps(dict(request=request,scenario=plan),indent=2));return 0
        if args.remote:return remote(request,args.output.resolve()/request['request_id'],args.timeout)
        if platform.system()!='Darwin':
            print('No local Mac; automatically requesting real Mac evidence',flush=True)
            return remote(request,args.output.resolve()/request['request_id'],args.timeout)
        return execute(request)
    except (ValueError,OSError,subprocess.CalledProcessError) as error:
        print(str(error),file=sys.stderr);return 2


if __name__=='__main__':sys.exit(main())

#!/usr/bin/env python3
"""Provision the private Airside Mac diagnostic runner outside protected Documents; secrets never printed."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import platform
import subprocess
import sys
import urllib.request

ROOT=Path(__file__).resolve().parents[1]
REPO='Bazlinka/Airside'


def api(path,method='GET'):
    return json.loads(subprocess.check_output(['gh','api','--method',method,path],text=True))


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--directory',type=Path,default=Path.home()/'Developer/Airside-DiagnosticsRunner')
    parser.add_argument('--install-service',action='store_true',help='Install/start user login service for automatic cloud requests')
    args=parser.parse_args()
    if platform.system()!='Darwin' or platform.machine()!='arm64':raise SystemExit('This runner requires the Apple Silicon Mac')
    directory=args.directory.resolve();directory.mkdir(parents=True,exist_ok=True)
    os.chmod(directory,0o700)
    if not (directory/'.runner').exists():
        releases=api(f'repos/{REPO}/actions/runners/downloads')
        release=next(item for item in releases if item['os']=='osx' and item['architecture']=='arm64')
        archive=directory/release['filename']
        if not archive.exists():
            print('Downloading official Actions Mac runner',flush=True)
            urllib.request.urlretrieve(release['download_url'],archive)
        checksum=hashlib.sha256(archive.read_bytes()).hexdigest()
        if checksum!=release['sha256_checksum']:raise SystemExit('Runner checksum mismatch')
        subprocess.run(['tar','xzf',str(archive),'-C',str(directory)],check=True)
        token=api(f'repos/{REPO}/actions/runners/registration-token','POST')['token']
        result=subprocess.run([str(directory/'config.sh'),'--url','https://github.com/'+REPO,'--token',token,
                              '--name','airside-mac-diagnostics','--labels','airside-diagnostics',
                              '--work','jobs','--unattended'],cwd=directory,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,text=True)
        if result.returncode:
            print(result.stdout.replace(token,'[redacted]'),file=sys.stderr)
            return result.returncode
        print('Registered private airside-mac-diagnostics runner',flush=True)
    for name in ('.credentials','.credentials_rsaparams','.runner'):
        path=directory/name
        if path.exists():os.chmod(path,0o600)
    if args.install_service:
        if not (directory/'.service').exists():subprocess.run(['./svc.sh','install'],cwd=directory,check=True)
        subprocess.run(['./svc.sh','start'],cwd=directory,check=True)
        subprocess.run(['./svc.sh','status'],cwd=directory,check=True)
    else:print('Run ./run.sh in '+str(directory)+'; or install its user service with --install-service')
    return 0


if __name__=='__main__':sys.exit(main())

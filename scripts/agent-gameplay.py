#!/usr/bin/env python3
"""Run opt-in agent gameplay scenarios against one matching Mac build; never a routine merge gate."""
from __future__ import annotations
import argparse
import json
import os
from pathlib import Path
import re
import signal
import struct
import subprocess
import sys
import time
import uuid
import zlib

ROOT = Path(__file__).resolve().parents[1]
FEATURES = ('panels', 'booking', 'save', 'views', 'menu', 'weather')
CRASH = re.compile(r'(?:NullReferenceException|MissingReferenceException|InvalidOperationException|ArgumentException|ArgumentOutOfRangeException|IndexOutOfRangeException|AssertionException|Shader error|\[Airside agent\].*failed|\[Airside capture\].*(?:failed|unsupported|exceeded))')


def git(*args):
    return subprocess.check_output(['git', *args], cwd=ROOT, text=True).strip()


def make_plan(features, commit):
    steps = []
    def add(id, action, value='', capture=False, settle=0.25):
        steps.append(dict(id=id, action=action, value=value, capture=capture, settleSeconds=settle))
    if 'panels' in features:
        for name in ('Fleet', 'Operations', 'Contracts', 'Stats'):
            add('panel-'+name.lower(), 'workspace', name, True)
        add('planner', 'planner', capture=True)
        add('workspace-close', 'workspace', 'None')
    if 'booking' in features or 'save' in features:
        add('book', 'book')
        if 'save' in features: add('save-booked', 'save')
        add('cancel', 'cancel')
        if 'save' in features: add('save-cancelled', 'save')
    if 'views' in features:
        add('follow', 'follow', capture=True, settle=1)
        for mode in ('LeftWindow', 'RightWindow', 'Exterior'):
            add('view-'+mode.lower(), 'view', mode, True, 1)
        add('overview', 'overview', capture=True, settle=1)
    if 'menu' in features:
        add('menu-open', 'menu', 'open', True)
        add('menu-close', 'menu', 'close')
    if 'weather' in features:
        add('weather-rain', 'weather', 'Rain', True, 15)
        add('weather-clear', 'weather', 'Clear', True, 15)
    if not steps: raise ValueError('Select at least one feature')
    return dict(protocol=1, expectedCommit=commit, steps=steps)


def build_preflight(app, expected, dirty):
    if dirty: raise ValueError('Commit game/scripts changes first; gameplay evidence must match a clean build')
    executable = app / 'Contents/MacOS/Airside'
    stamp = app / 'Contents/Resources/Data/StreamingAssets/build-identity.txt'
    if not executable.is_file() or not os.access(executable, os.X_OK):
        raise ValueError('No playable build; build once with scripts/build-mac.sh, then reuse it')
    values = dict(line.split('=', 1) for line in stamp.read_text().splitlines() if '=' in line) if stamp.is_file() else {}
    if values.get('commitFull') != expected or values.get('dirty', 'true').lower() not in ('false', '0'):
        raise ValueError('Stale, dirty or unstamped build; build the current committed source once')
    return executable


def png_valid(path):
    if not path.is_file(): return False
    data = path.read_bytes()
    if len(data) < 45 or data[:8] != b'\x89PNG\r\n\x1a\n' or data[12:16] != b'IHDR': return False
    width, height = struct.unpack('>II', data[16:24])
    if width == 0 or height == 0: return False
    offset, pixels = 8, False
    while offset + 12 <= len(data):
        size = struct.unpack('>I', data[offset:offset+4])[0]
        end = offset + 12 + size
        if end > len(data): return False
        chunk = data[offset+4:offset+8]
        crc = struct.unpack('>I', data[end-4:end])[0]
        if zlib.crc32(data[offset+4:end-4]) & 0xffffffff != crc: return False
        if chunk == b'IDAT' and size > 0: pixels = True
        if chunk == b'IEND': return size == 0 and end == len(data) and pixels
        offset = end
    return False


def validate_report(plan, report, directory):
    if report.get('protocol') != 1 or report.get('status') != 'passed': raise ValueError('Runtime scenario did not pass')
    if report.get('commit') != plan['expectedCommit'] or report.get('dirty'): raise ValueError('Runtime build identity mismatch')
    if Path(report.get('savePath', '')).resolve() != (directory/'test-save.json').resolve(): raise ValueError('Runtime save was not isolated')
    actual = report.get('steps', [])
    if len(actual) != len(plan['steps']): raise ValueError('Missing scenario steps')
    for expected, result in zip(plan['steps'], actual):
        if result.get('id') != expected['id'] or result.get('action') != expected['action'] or result.get('status') != 'passed':
            raise ValueError('Failed/mismatched step: '+expected['id'])
        if expected['capture'] and (result.get('screenshot') != expected['id']+'.png' or not png_valid(directory/(expected['id']+'.png'))):
            raise ValueError('Missing/invalid real-frame evidence: '+expected['id'])
    return len(actual)


def launch(command, directory, timeout):
    """Own only this child process group. No killall, existing-player reuse or shell."""
    log = directory/'player.log'
    started = time.monotonic()
    process = None
    awake = None
    with (directory/'launcher.log').open('w') as output:
        try:
            process = subprocess.Popen(command, stdout=output, stderr=subprocess.STDOUT, start_new_session=True)
            if sys.platform == 'darwin':
                awake = subprocess.Popen(['caffeinate', '-d', '-i', '-u', '-w', str(process.pid)], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
            deadline = started + timeout
            next_progress = started + 20
            while process.poll() is None:
                now = time.monotonic()
                if now >= deadline: raise TimeoutError(f'Gameplay exceeded {timeout}s; no pass recorded')
                if now >= next_progress:
                    print(f'{directory.name}: running {now-started:.0f}s; log {log}', flush=True)
                    next_progress = now + 20
                time.sleep(0.25)
            if process.returncode != 0: raise ValueError(f'Player exited {process.returncode}; see {log}')
            text = log.read_text(errors='replace') if log.is_file() else ''
            if CRASH.search(text): raise ValueError(f'Runtime error found; see {log}')
            return text, round(time.monotonic()-started, 2)
        finally:
            if process is not None and process.poll() is None:
                os.killpg(process.pid, signal.SIGTERM)
                try: process.wait(timeout=3)
                except subprocess.TimeoutExpired:
                    os.killpg(process.pid, signal.SIGKILL)
                    process.wait(timeout=3)
            if awake is not None:
                awake.terminate()
                awake.wait(timeout=3)


def journey_verdict(text, directory):
    required = ('TakingOff', 'Outbound', 'Inbound', 'Landing')
    missing = [state for state in required if not re.search(r'\[Airside journey\].*\b'+state+r'\b', text)]
    frames = [p for p in directory.glob('*.png') if png_valid(p)]
    if missing or '[Airside journey] COMPLETE round trip;' not in text or not any('completed-overview' in p.name for p in frames):
        raise ValueError('Incomplete round trip/evidence; missing states: '+', '.join(missing))
    return [p.name for p in frames]


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--profile', choices=('smoke', 'full'), default='smoke')
    parser.add_argument('--features', help='Comma-separated: '+', '.join(FEATURES))
    parser.add_argument('--app', type=Path, default=ROOT/'work/builds/Airside.app')
    parser.add_argument('--output', type=Path, default=ROOT/'work/agent-gameplay')
    parser.add_argument('--timeout', type=int, default=90, help='Feature session wall-clock bound')
    parser.add_argument('--journey-timeout', type=int, default=600)
    parser.add_argument('--destination', default='KGC', help='Covered regional round-trip destination')
    parser.add_argument('--plan', action='store_true', help='Print exact scenario and launch intent; do not launch/write')
    args = parser.parse_args(argv)
    try:
        if args.timeout < 1 or args.journey_timeout < 1: raise ValueError('Timeouts must be positive')
        if not re.fullmatch(r'[A-Z]{3,4}', args.destination): raise ValueError('Destination must be an airport code')
        features = args.features.split(',') if args.features else list(FEATURES if args.profile == 'full' else FEATURES[:5])
        unknown = set(features)-set(FEATURES)
        if unknown: raise ValueError('Unknown features: '+', '.join(sorted(unknown)))
        commit = git('rev-parse', 'HEAD')
        plan = make_plan(features, commit)
        if args.plan:
            print(json.dumps(dict(profile=args.profile, plan=plan, journey=args.destination if args.profile == 'full' else None,
                                  rate=40 if args.profile == 'full' else 1, featureTimeout=args.timeout, journeyTimeout=args.journey_timeout), indent=2))
            return 0
        dirty = git('status', '--porcelain', '--untracked-files=normal', '--', 'game', 'scripts')
        executable = build_preflight(args.app.resolve(), commit, dirty)
    except (ValueError, OSError, subprocess.CalledProcessError) as error:
        print(str(error), file=sys.stderr); return 2
    run = args.output.resolve()/(time.strftime('%Y%m%d-%H%M%S')+'-'+uuid.uuid4().hex[:8])
    run.mkdir(parents=True)
    scenario = run/'features'; scenario.mkdir()
    plan_path = scenario/'plan.json'; plan_path.write_text(json.dumps(plan, indent=2))
    summary = dict(protocol=1, commit=commit, profile=args.profile, status='failed', features=features,
                   visualQuality='requires agent image inspection', performance='unverified', scenarios=[])
    def command(directory):
        return [str(executable), '-airsideSoak', '-airsideSoakMinutes', str((args.journey_timeout+120)/60),
                '-airsideReviewWeather', 'Clear', '-airsideReviewTime', '13:30',
                '-screen-fullscreen', '0', '-screen-width', '1280', '-screen-height', '800',
                '-logFile', str(directory/'player.log')]
    try:
        print(f'Agent gameplay: {run}', flush=True)
        text, seconds = launch(command(scenario)+['-airsideAgentGameplay', str(plan_path)], scenario, args.timeout)
        report = json.loads((scenario/'gameplay-report.json').read_text())
        checked = validate_report(plan, report, scenario)
        summary['scenarios'].append(dict(name='features', status='passed', steps=checked, realSeconds=seconds))
        if args.profile == 'full':
            directory=run/'journey'; directory.mkdir()
            flags=['-airsideAgentSaveDirectory', str(directory), '-airsideReviewCockpit', '-airsideReviewJourney', args.destination,
                   '-airsideReviewJourneyOut', str(directory), '-airsideReviewJourneyRate', '40']
            text, seconds=launch(command(directory)+flags, directory, args.journey_timeout)
            frames=journey_verdict(text, directory)
            summary['scenarios'].append(dict(name='round-trip', status='passed', realSeconds=seconds, rate=40, frames=frames))
        summary['status']='passed'
    except (ValueError, OSError, TimeoutError, json.JSONDecodeError) as error:
        summary['error']=str(error)
        print(str(error), file=sys.stderr)
    except KeyboardInterrupt:
        summary['error']='Interrupted; owned player stopped; incomplete evidence'
    finally:
        (run/'summary.json').write_text(json.dumps(summary, indent=2))
        print(f'{summary["status"].upper()}: {run/"summary.json"}', flush=True)
    return 0 if summary['status']=='passed' else 1


if __name__ == '__main__':
    sys.exit(main())

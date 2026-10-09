#!/usr/bin/env python3
"""Focused regression checks for agent gameplay evidence and process isolation."""
import importlib.util
from pathlib import Path
import sys
import struct
import zlib
import tempfile
import unittest

spec = importlib.util.spec_from_file_location('agent_gameplay', Path(__file__).with_name('agent-gameplay.py'))
runner = importlib.util.module_from_spec(spec); spec.loader.exec_module(runner)


class GameplayRunnerTests(unittest.TestCase):
    def test_stale_and_dirty_builds_rejected(self):
        with tempfile.TemporaryDirectory() as temp:
            app=Path(temp)/'Airside.app'; executable=app/'Contents/MacOS/Airside'
            executable.parent.mkdir(parents=True); executable.write_text('test'); executable.chmod(0o755)
            stamp=app/'Contents/Resources/Data/StreamingAssets/build-identity.txt'
            stamp.parent.mkdir(parents=True); stamp.write_text('commitFull=old\ndirty=false\n')
            with self.assertRaisesRegex(ValueError, 'Stale'): runner.build_preflight(app,'new','')
            stamp.write_text('commitFull=new\ndirty=false\n')
            self.assertEqual(runner.build_preflight(app,'new',''),executable)
            with self.assertRaisesRegex(ValueError, 'Commit'): runner.build_preflight(app,'new',' M scripts/a.py')

    def test_incomplete_or_mismatched_steps_never_pass(self):
        with tempfile.TemporaryDirectory() as temp:
            directory=Path(temp); plan=runner.make_plan(['booking'],'abc')
            report=dict(protocol=1,status='passed',commit='abc',dirty=False,savePath=str(directory/'test-save.json'),steps=[])
            with self.assertRaisesRegex(ValueError,'Missing'):runner.validate_report(plan,report,directory)
            report['steps']=[dict(id=s['id'],action=s['action'],status='passed') for s in plan['steps']]
            self.assertEqual(runner.validate_report(plan,report,directory),2)
            report['steps'][1]['action']='book'
            with self.assertRaisesRegex(ValueError,'mismatched'):runner.validate_report(plan,report,directory)

    def test_missing_frame_and_personal_save_rejected(self):
        with tempfile.TemporaryDirectory() as temp:
            directory=Path(temp); plan=runner.make_plan(['panels'],'abc')
            report=dict(protocol=1,status='passed',commit='abc',dirty=False,savePath='/tmp/personal-save.json',steps=[])
            with self.assertRaisesRegex(ValueError,'isolated'):runner.validate_report(plan,report,directory)
            report['savePath']=str(directory/'test-save.json')
            report['steps']=[dict(id=s['id'],action=s['action'],status='passed',screenshot=s['id']+'.png') for s in plan['steps']]
            with self.assertRaisesRegex(ValueError,'evidence'):runner.validate_report(plan,report,directory)

    def test_png_header_alone_is_not_capture(self):
        with tempfile.TemporaryDirectory() as temp:
            path=Path(temp)/'frame.png';path.write_bytes(b'\x89PNG\r\n\x1a\n')
            self.assertFalse(runner.png_valid(path))

    def test_valid_png_with_complete_report_passes(self):
        def chunk(kind, payload):
            return struct.pack('>I',len(payload))+kind+payload+struct.pack('>I',zlib.crc32(kind+payload)&0xffffffff)
        png=(b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('>IIBBBBB',1,1,8,2,0,0,0))
             +chunk(b'IDAT',zlib.compress(b'\0\xff\xff\xff'))+chunk(b'IEND',b''))
        with tempfile.TemporaryDirectory() as temp:
            directory=Path(temp);plan=runner.make_plan(['menu'],'abc')
            (directory/'menu-open.png').write_bytes(png)
            report=dict(protocol=1,status='passed',commit='abc',dirty=False,savePath=str(directory/'test-save.json'),
                        steps=[dict(id=s['id'],action=s['action'],status='passed',screenshot=s['id']+'.png') for s in plan['steps']])
            self.assertEqual(runner.validate_report(plan,report,directory),2)
            (directory/'menu-open.png').write_bytes(png[:-1]+b'X')
            self.assertFalse(runner.png_valid(directory/'menu-open.png'))

    def test_journey_marker_alone_never_passes(self):
        with tempfile.TemporaryDirectory() as temp:
            with self.assertRaisesRegex(ValueError,'Incomplete'):
                runner.journey_verdict('[Airside journey] COMPLETE round trip;',Path(temp))

    def test_timeout_stops_owned_process(self):
        with tempfile.TemporaryDirectory() as temp:
            with self.assertRaises(TimeoutError):
                runner.launch([sys.executable,'-c','import time; time.sleep(30)'],Path(temp),0.1)

    def test_nonzero_exit_and_runtime_error_fail(self):
        with tempfile.TemporaryDirectory() as temp:
            directory=Path(temp)
            with self.assertRaisesRegex(ValueError,'exited 3'):
                runner.launch([sys.executable,'-c','raise SystemExit(3)'],directory,5)
            (directory/'player.log').write_text('NullReferenceException: broken view')
            with self.assertRaisesRegex(ValueError,'Runtime error'):
                runner.launch([sys.executable,'-c','pass'],directory,5)

    def test_save_feature_checks_booked_and_cancelled_state(self):
        plan=runner.make_plan(['save'],'abc')
        self.assertEqual([s['id'] for s in plan['steps']],['book','save-booked','cancel','save-cancelled'])
        full=runner.make_plan(runner.FEATURES,'abc')['steps']
        self.assertEqual(len({s['id'] for s in full}),len(full))

    def test_custom_scenario_rejects_commands_and_unsafe_ids(self):
        plan=runner.make_plan(['menu'],'abc')
        plan['steps'][0]['id']='../../save'
        with self.assertRaises(ValueError):runner.validate_plan(plan,'abc')
        plan['steps'][0]['id']='safe';plan['steps'][0]['action']='shell'
        with self.assertRaises(ValueError):runner.validate_plan(plan,'abc')

    def test_issue_probes_weather_and_unknown_issues_need_reproduction(self):
        plan=runner.issue_plan('cloud weather looks flat','abc')
        self.assertEqual([s['value'] for s in plan['steps'] if s['action']=='weather'],
                         ['Clear','Cloudy','Rain','Storm','Fog','Rain'])
        self.assertIn('camera',[s['action'] for s in plan['steps']])
        with self.assertRaisesRegex(ValueError,'reproduction'):runner.issue_plan('something is wrong','abc')

    def test_runtime_errors_override_step_passes(self):
        report=dict(protocol=1,status='passed',runtimeErrors=['shader broken'])
        with self.assertRaisesRegex(ValueError,'Runtime errors'):
            runner.validate_report(runner.make_plan(['menu'],'abc'),report,Path('/tmp'))


if __name__ == '__main__': unittest.main()

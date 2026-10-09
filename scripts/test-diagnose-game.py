#!/usr/bin/env python3
"""Check diagnostic request safety, custom probes and host selection without launching the game."""
import importlib.util
import json
from pathlib import Path
import unittest
from unittest.mock import patch

spec=importlib.util.spec_from_file_location('diagnose',Path(__file__).with_name('diagnose-game.py'))
diagnose=importlib.util.module_from_spec(spec);spec.loader.exec_module(diagnose)


class DiagnosticRequestTests(unittest.TestCase):
    def request(self):
        return dict(request_id='diag-test',revision='a'*40,issue='weather looks wrong',scenario='',aircraft_type='')

    def test_exact_revision_and_safe_id_required(self):
        request=self.request();request['revision']='main; rm -rf x'
        with self.assertRaises(ValueError):diagnose.request_validate(request)
        request=self.request();request['request_id']='../../personal-save'
        with self.assertRaises(ValueError):diagnose.request_validate(request)

    def test_custom_scenario_is_not_replaced_by_smoke(self):
        request=self.request();request['scenario']=json.dumps(dict(protocol=1,steps=[dict(id='cloud-angle',action='camera',
                                                  value='25,45,500',capture=True,settleSeconds=2)]))
        plan=diagnose.request_validate(request)
        self.assertEqual([s['id'] for s in plan['steps']],['cloud-angle'])
        self.assertEqual(plan['expectedCommit'],'a'*40)
        self.assertEqual(plan['issue'],request['issue'])

    def test_unsupported_commands_cannot_be_dispatched(self):
        request=self.request();request['scenario']=json.dumps(dict(protocol=1,steps=[dict(id='bad',action='shell',value='touch x',capture=False,settleSeconds=1)]))
        with self.assertRaises(ValueError):diagnose.request_validate(request)

    def test_unrecognised_issue_requires_specific_reproduction(self):
        request=self.request();request['issue']='something broke'
        with self.assertRaisesRegex(ValueError,'reproduction'):diagnose.request_validate(request)

    def test_linux_automatically_uses_mac_transport(self):
        with patch.object(diagnose,'output',return_value='a'*40),patch.object(diagnose.platform,'system',return_value='Linux'), \
             patch.object(diagnose,'remote',return_value=0) as remote,patch.object(diagnose,'execute') as execute:
            self.assertEqual(diagnose.main(['--issue','weather looks wrong']),0)
            remote.assert_called_once();execute.assert_not_called()


if __name__=='__main__':unittest.main()

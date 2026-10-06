"""Deterministic cleanup controls; all process/Docker operations are simulated."""
import contextlib
import importlib.util
import io
import json
from pathlib import Path
import tempfile
from types import SimpleNamespace
import unittest
from unittest import mock


class OwnedCleanupTests(unittest.TestCase):
    def setUp(self):
        path = Path(__file__).with_name('run-source-live-lanes.py')
        spec = importlib.util.spec_from_file_location('source_live_runner', path)
        self.runner = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(self.runner)
        temporary = tempfile.TemporaryDirectory(prefix='p1r-simulated-cleanup-')
        self.addCleanup(temporary.cleanup)
        self.runner.OUTPUT = Path(temporary.name)
        self.runner.INVOCATION = 'simulated-invocation'
        self.runner.LABEL = 'hexalith.p1r.remediation=simulated-invocation'
        self.runner.OWNED[:] = ['owned-third', 'owned-second', 'owned-first']
        self.ids = {name: name + '-id' for name in self.runner.OWNED}
        self.labels = {'hexalith.p1r.remediation': self.runner.INVOCATION}
        self.shared = {'shared-id': {'name': '/shared', 'image': 'immutable-image',
                                    'running': True, 'started_at': 'unchanged'}}
        self.calls = []
        self.removed = []
        self.discovery_failure = None
        self.inspect_failure = None
        self.remove_failure = None
        self.wrong_owner = None
        self.final_container_failure = False
        self.inspect_launch_failure = None

    def popen(self, argv, **kwargs):
        self.calls.append(argv)
        output = ''
        code = 0
        if argv[:3] == ['docker', 'ps', '-aq']:
            selector = argv[-1]
            if selector.startswith('name=^/'):
                name = selector[len('name=^/'):-1]
                if name == self.discovery_failure:
                    output, code = 'simulated exact-name discovery failure', 1
                else:
                    output = self.ids[name]
            else:
                self.assertEqual(selector, 'label=' + self.runner.LABEL)
                if self.final_container_failure:
                    output, code = 'simulated final owned inventory failure', 1
                else:
                    output = '\n'.join(identity for identity in self.ids.values()
                                       if identity not in self.removed)
        elif argv[:2] == ['docker', 'inspect']:
            self.assertEqual(argv[2:4], ['--format', '{{json .Config.Labels}}'])
            self.assertIn(argv[-1], self.ids.values())
            if argv[-1] == self.inspect_launch_failure:
                raise OSError('simulated ownership inspect launch failure')
            if argv[-1] == self.inspect_failure:
                output, code = 'simulated ownership inspect failure', 1
            else:
                output = json.dumps({'hexalith.p1r.remediation': 'unrelated-invocation'}
                                    if argv[-1] == self.wrong_owner else self.labels)
        elif argv[:3] == ['docker', 'rm', '-f']:
            self.assertIn(argv[-1], self.ids.values())
            self.assertNotEqual(argv[-1], self.wrong_owner)
            if argv[-1] == self.remove_failure:
                output, code = 'simulated owned remove failure', 1
            else:
                self.removed.append(argv[-1])
                output = argv[-1]
        else:
            self.fail('Unexpected simulated command: ' + repr(argv))
        return SimpleNamespace(pid=12345, returncode=code,
                               communicate=lambda timeout=None: (output, None))

    def run_cleanup(self, final_inventory=None, processes=None):
        inventory = mock.Mock(return_value=self.shared) if final_inventory is None else final_inventory
        process_inventory = mock.Mock(return_value=[]) if processes is None else processes
        with mock.patch.object(self.runner.subprocess, 'Popen', self.popen), \
                mock.patch.object(self.runner, 'resources', inventory), \
                mock.patch.object(self.runner, 'live_processes', process_inventory), \
                contextlib.redirect_stdout(io.StringIO()):
            summary = self.runner.cleanup(self.shared, None)
        receipt = self.runner.OUTPUT / 'live-runtime-and-cleanup.json'
        self.assertTrue(receipt.is_file())
        self.assertEqual(json.loads(receipt.read_text()), summary)
        self.assertEqual(summary['ownership_label'], self.runner.LABEL)
        self.assertEqual(summary['owned_resource_names'], self.runner.OWNED)
        return summary

    def test_inspect_and_remove_failures_keep_attempting_and_retain_raw_receipts(self):
        self.inspect_failure = self.ids['owned-first']
        self.remove_failure = self.ids['owned-second']
        summary = self.run_cleanup()
        self.assertEqual(summary['exit_code'], 1)
        self.assertTrue(summary['shared_preserved'])
        self.assertEqual([error['stage'] for error in summary['cleanup_errors']],
                         ['container-ownership', 'container-remove'])
        self.assertIn(self.ids['owned-third'], self.removed)
        self.assertEqual((self.runner.OUTPUT / 'cleanup-0-inspect.log').read_text(),
                         'simulated ownership inspect failure')
        self.assertEqual((self.runner.OUTPUT / 'cleanup-1-remove.log').read_text(),
                         'simulated owned remove failure')
        failed = [command for command in summary['commands'] if command['exit_code']]
        self.assertEqual(len(failed), 2)

    def test_inspect_launch_failure_keeps_its_receipt_and_continues_cleanup(self):
        self.inspect_launch_failure = self.ids['owned-first']
        summary = self.run_cleanup()
        self.assertEqual(summary['exit_code'], 1)
        self.assertEqual(len(self.removed), 2)
        receipt = json.loads((self.runner.OUTPUT / 'cleanup-0-inspect.json').read_text())
        self.assertIsNone(receipt['exit_code'])
        self.assertEqual(receipt['launch_error'], 'OSError: simulated ownership inspect launch failure')
        self.assertEqual(receipt['argv'][-1], self.inspect_launch_failure)
        self.assertEqual(summary['cleanup_errors'][0]['stage'], 'container-ownership')

    def test_exact_name_discovery_failure_does_not_abort_later_owned_resources(self):
        self.discovery_failure = 'owned-first'
        summary = self.run_cleanup()
        self.assertEqual(summary['exit_code'], 1)
        self.assertEqual(summary['cleanup_errors'][0]['stage'], 'container-discovery')
        self.assertEqual(self.removed, [self.ids['owned-second'], self.ids['owned-third']])

    def test_changed_ownership_label_is_preserved_and_never_removed(self):
        self.wrong_owner = self.ids['owned-first']
        summary = self.run_cleanup()
        self.assertEqual(summary['exit_code'], 1)
        self.assertEqual(summary['owned_resource_labels']['owned-first'],
                         {'hexalith.p1r.remediation': 'unrelated-invocation'})
        self.assertNotIn(self.wrong_owner, self.removed)
        self.assertEqual(len(self.removed), 2)

    def test_each_final_inventory_error_preserves_a_nonpassing_summary(self):
        self.final_container_failure = True
        inventory = mock.Mock(side_effect=RuntimeError('simulated final resource inventory failure'))
        processes = mock.Mock(side_effect=[[], RuntimeError('simulated final process inventory failure')])
        summary = self.run_cleanup(inventory, processes)
        self.assertEqual(summary['exit_code'], 1)
        self.assertFalse(summary['shared_preserved'])
        self.assertIsNone(summary['owned_containers_remaining'])
        self.assertIsNone(summary['owned_processes_remaining'])
        self.assertEqual([error['stage'] for error in summary['cleanup_errors']],
                         ['final-resource-inventory', 'final-owned-container-inventory',
                          'final-owned-process-inventory'])
        self.assertEqual(len(self.removed), 3)

    def test_process_discovery_error_still_attempts_every_owned_container(self):
        processes = mock.Mock(side_effect=[PermissionError('simulated process inventory failure'), []])
        summary = self.run_cleanup(processes=processes)
        self.assertEqual(summary['exit_code'], 1)
        self.assertEqual(summary['cleanup_errors'][0]['stage'], 'process-discovery')
        self.assertEqual(len(self.removed), 3)

    def test_process_stop_failure_does_not_abort_other_groups_or_containers(self):
        self.runner.GROUPS[:] = [100, 101]
        processes = mock.Mock(side_effect=[[100, 101], []])
        with mock.patch.object(self.runner.os, 'getpgid', lambda pid: pid), \
                mock.patch.object(self.runner.os, 'killpg',
                                  side_effect=[PermissionError('simulated owned process stop failure'), None]) as stop:
            summary = self.run_cleanup(processes=processes)
        self.assertEqual(summary['exit_code'], 1)
        self.assertEqual(summary['cleanup_errors'][0]['stage'], 'process-stop')
        self.assertEqual({call.args[0] for call in stop.call_args_list}, {100, 101})
        self.assertEqual(len(self.removed), 3)

    def test_success_requires_complete_inventory_and_preserves_exact_labels(self):
        summary = self.run_cleanup()
        self.assertEqual(summary['exit_code'], 0)
        self.assertEqual(summary['cleanup_errors'], [])
        self.assertEqual(summary['owned_containers_remaining'], [])
        self.assertEqual(summary['owned_processes_remaining'], [])
        self.assertTrue(summary['shared_preserved'])
        self.assertTrue(all(labels == self.labels
                            for labels in summary['owned_resource_labels'].values()))


if __name__ == '__main__':
    unittest.main()

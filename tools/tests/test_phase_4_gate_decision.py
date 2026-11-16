"""Real gate CLI/workflow entry paths, real OQ8, and HTTP-only platform fixtures."""

import copy
import contextlib
import io
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import textwrap
import unittest
from unittest import mock

import phase_4_transport_fixture as HTTP
import test_phase_4_transition as T
import phase_4_gate_decision as D

OQ8_WORKFLOW = '.github/workflows/oq8-governing-design.yml'
GATE_WORKFLOW = '.github/workflows/phase-4-high-risk-gate-pass.yml'
CONSUMER = 'tools/validate-phase-4-gate-decision.py'
CONTROLS = (*T.SCRIPTS, GATE_WORKFLOW)


class GateDecisionTests(unittest.TestCase):
    def setUp(self):
        # Reuse the existing genuine predecessor and authenticated HTTP fixtures.
        self.f = T.TransitionTests(methodName='runTest')
        self.f.setUp()
        self.addCleanup(self.f.doCleanups)
        self.expected_level = T.SINGLE

    def environment(self, **extra):
        f = self.f
        f.state_path.write_text(json.dumps(f.state))
        environment = {k: v for k, v in os.environ.items() if not k.startswith(('GH_', 'GITHUB_', 'PYTHON'))}
        environment.update(PYTHONPATH=str(f.site) + os.pathsep + str(T.ROOT / 'tools/tests'),
                           PHASE4_FIXTURE_STATE=str(f.state_path), GH_TOKEN=f.fixture_token,
                           PHASE4_REQUEST_JSON=T.canonical(f.request).decode(), SEALED_RUN_ID='7', SEALED_ATTEMPT='2')
        environment.update(extra)
        return environment

    def cli(self, raw=None, coordinates=('7', '2'), extra=(), **environment):
        f = self.f
        f.request_path.write_bytes(T.canonical(f.request) if raw is None else raw)
        argv = [sys.executable, '-B', str(f.root / CONSUMER), '--request', str(f.request_path),
                '--result', str(f.result_path)]
        for flag, value in zip(('--run-id', '--attempt'), coordinates):
            if value is not None:
                argv += [flag, value]
        process = subprocess.run([*argv, *extra], env=self.environment(**environment), text=True,
                                 capture_output=True, timeout=45)
        f.state = json.loads(f.state_path.read_text())
        self.assertNotIn('Traceback', process.stderr)
        document = json.loads(f.result_path.read_text())
        self.assert_contract(document, process.returncode)
        return process, document

    def assert_contract(self, document, exit_code):
        schema = json.loads((T.ROOT / 'tools/phase-4-transition.schema.json').read_text())
        definition = schema['$defs']['gate_decision']
        self.assertEqual(set(definition['required']), set(document))
        self.assertEqual('hexalith.eventstore.phase-4-gate-decision.v1', document['schema'])
        self.assertEqual('G-OQ8', document['gate'])
        self.assertEqual('governing-design-availability', document['scope'])
        self.assertEqual('FAIL' if exit_code else 'PASS', document['result'])
        if exit_code:
            self.assertTrue(document['codes'])
            self.assertIsNone(document['seal'])
            self.assertEqual('none', document['achieved_level'])
            self.assertEqual('none', document['downstream_minimum'])
        else:
            self.assertEqual([], document['codes'])
            self.assertEqual(self.expected_level, document['required_level'])
            self.assertEqual(self.expected_level, document['achieved_level'])
            self.assertEqual(self.expected_level, document['downstream_minimum'])
            self.assertEqual({'run_id': 7, 'attempt': 2, 'artifact_id': 16, 'ci_app_id': 15368}, document['seal'])
            inputs = {item['path'] for item in document['binding']['inputs']}
            self.assertTrue(set((*CONTROLS, OQ8_WORKFLOW)) <= inputs)
            self.assertTrue(document['approvals'])
            self.assertTrue(document['predecessors'])

    def refused(self, code, status=1, **kwargs):
        process, document = self.cli(**kwargs)
        self.assertEqual(status, process.returncode, process.stderr)
        self.assertEqual([code], document['codes'], process.stderr)
        return document

    def workflow(self, path, mode='consume'):
        f = self.f
        content = (f.root / path).read_text()
        name = 'Consume authenticated G-OQ8 evaluation' if path == OQ8_WORKFLOW else 'Verify this guarded attempt'
        step = content.split('      - name: ' + name + '\n', 1)[1].split('      - name:', 1)[0]
        script = textwrap.dedent(step.split('        run: |\n', 1)[1])
        self.assertNotIn('${{', script)
        directory = f.base / 'runner'
        directory.mkdir(exist_ok=True)
        environment = self.environment(RUNNER_TEMP=str(directory))
        for key in ('GH_TOKEN', 'PHASE4_REQUEST_JSON', 'SEALED_RUN_ID', 'SEALED_ATTEMPT', 'MODE'):
            environment.pop(key, None)
        context = {'github.token': f.fixture_token, 'inputs.mode': mode,
                   'inputs.request': T.canonical(f.request).decode(),
                   'inputs.decision_request': T.canonical(f.request).decode(),
                   'inputs.sealed_run_id': '7', 'inputs.sealed_attempt': '2'}
        environment.update({key: context[expression[4:-3]]
                            for key, expression in self.workflow_bindings(path, step).items()})
        process = subprocess.run(['bash', '-e', '-o', 'pipefail', '-c', script], cwd=f.root,
                                 env=environment, text=True, capture_output=True, timeout=45)
        f.state = json.loads(f.state_path.read_text())
        output = directory / ('phase-4-gate-decision' if path == OQ8_WORKFLOW else 'phase-4-transition')
        self.assertTrue((output / 'report.txt').exists(), process.stderr)
        self.assertNotIn('Traceback', (output / 'report.txt').read_text())
        return process, json.loads((output / 'result.json').read_text())

    def workflow_bindings(self, path, step):
        bindings = dict(re.findall(r'^          ([A-Z0-9_]+): (.+)$', step, re.MULTILINE))
        expected = {'GH_TOKEN': '${{ github.token }}', 'SEALED_RUN_ID': '${{ inputs.sealed_run_id }}',
                    'SEALED_ATTEMPT': '${{ inputs.sealed_attempt }}'}
        if path == OQ8_WORKFLOW:
            expected['PHASE4_REQUEST_JSON'] = '${{ inputs.decision_request }}'
        else:
            expected.update(MODE='${{ inputs.mode }}', PHASE4_REQUEST_JSON='${{ inputs.request }}')
        self.assertEqual(expected, bindings)
        return bindings

    def test_exact_cli_options_and_failure_destination_recovery(self):
        self.f.request['gate'] = 'G-TENANT'  # Valid closed input; refuse before platform access.
        self.refused('ADAPTER_UNAVAILABLE')
        for option, value in (('--roo', str(self.f.root)), ('--request-e', None), ('--run-i', '7'),
                              ('--attemp', '2'), ('--resul', str(self.f.result_path))):
            self.f.result_path.write_text('{"result":"PASS","prior_report":true}')
            extra = (option,) if value is None else (option, value)
            with self.subTest(option=option):
                self.refused('INPUT_INVALID', 2, extra=extra)
        self.f.request_path.write_bytes(T.canonical(self.f.request))
        arguments = [sys.executable, '-B', str(self.f.root / CONSUMER), '--root=' + str(self.f.root),
                     '--request=' + str(self.f.request_path), '--run-id=7', '--attempt=2',
                     '--result=' + str(self.f.result_path)]
        for extra, status, code in (([], 1, 'ADAPTER_UNAVAILABLE'), (['--attempt'], 2, 'INPUT_INVALID')):
            self.f.result_path.write_text('{"result":"PASS","prior_report":true}')
            process = subprocess.run([*arguments, *extra], env=self.environment(), text=True,
                                     capture_output=True, timeout=5)
            self.assertEqual(status, process.returncode, process.stderr)
            document = json.loads(self.f.result_path.read_text())
            self.assert_contract(document, status)
            self.assertEqual([code], document['codes'])
            self.assertNotIn('prior_report', document)
            self.assertEqual([], json.loads(self.f.state_path.read_text())['observed'])

    def test_file_request_size_boundaries_replace_prior_report_without_platform_reads(self):
        self.f.request['gate'] = 'G-TENANT'
        raw = T.canonical(self.f.request)
        for size in (D.C.MAX_BYTES - 1, D.C.MAX_BYTES, D.C.MAX_BYTES + 1):
            self.f.result_path.write_text('{"result":"PASS","prior_report":true}')
            with self.subTest(size=size):
                oversized = size > D.C.MAX_BYTES
                document = self.refused('INPUT_INVALID' if oversized else 'ADAPTER_UNAVAILABLE',
                                        2 if oversized else 1, raw=raw + b' ' * (size - len(raw)))
                self.assertNotIn('prior_report', document)
                self.assertEqual([], self.f.state['observed'])

    def test_environment_request_size_boundaries_without_os_exec_limit(self):
        self.f.request['gate'] = 'G-TENANT'
        raw = T.canonical(self.f.request).decode()
        arguments = ['--root', str(self.f.root), '--request-env', '--run-id', '7', '--attempt', '2',
                     '--result', str(self.f.result_path)]
        for size in (D.C.MAX_BYTES - 1, D.C.MAX_BYTES, D.C.MAX_BYTES + 1):
            self.f.result_path.write_text('{"result":"PASS","prior_report":true}')
            environment = self.environment(PHASE4_REQUEST_JSON=raw + ' ' * (size - len(raw)))
            # Invoke the real main in-process so the OS's per-variable exec limit
            # cannot reject the oversized environment before the guard sees it.
            with self.subTest(size=size), mock.patch.dict(os.environ, environment, clear=True), \
                    mock.patch('urllib.request.build_opener', return_value=HTTP.Transport()), \
                    contextlib.redirect_stdout(io.StringIO()), contextlib.redirect_stderr(io.StringIO()):
                status = D.main(arguments)
            oversized = size > D.C.MAX_BYTES
            self.assertEqual(2 if oversized else 1, status)
            document = json.loads(self.f.result_path.read_text())
            self.assert_contract(document, status)
            self.assertEqual(['INPUT_INVALID' if oversized else 'ADAPTER_UNAVAILABLE'], document['codes'])
            self.assertNotIn('prior_report', document)
            self.assertEqual([], json.loads(self.f.state_path.read_text())['observed'])

    def test_both_workflow_input_to_environment_mappings_are_exact(self):
        for path, name in ((OQ8_WORKFLOW, 'Consume authenticated G-OQ8 evaluation'),
                           (GATE_WORKFLOW, 'Verify this guarded attempt')):
            step = (self.f.root / path).read_text().split('      - name: ' + name + '\n', 1)[1].split('      - name:', 1)[0]
            with self.subTest(workflow=path):
                self.workflow_bindings(path, step)
                with self.assertRaises(AssertionError):
                    self.workflow_bindings(path, step.replace('${{ inputs.sealed_attempt }}', '${{ inputs.sealed_run_id }}'))

    def test_both_actual_workflow_entry_paths_require_authenticated_consumption(self):
        process, candidate = self.workflow(GATE_WORKFLOW, 'evaluate')
        self.assertEqual(0, process.returncode)
        self.assertEqual(T.C.RESULT_SCHEMA, candidate['schema'])
        self.assertEqual('evaluate', candidate['phase'])
        self.assertIsNone(candidate['seal'])
        self.f.artifact(candidate)
        for path in (OQ8_WORKFLOW, GATE_WORKFLOW):
            with self.subTest(workflow=path):
                process, document = self.workflow(path)
                self.assertEqual(0, process.returncode, document)
                self.assert_contract(document, process.returncode)
                self.assertTrue(all(item['method'] == 'GET' for item in self.f.state['observed']))
        # An earlier successful output is replaced when either entry point fails.
        self.f.api_document('/issues/comments/100')['updated_at'] = '2026-01-03T00:00:00Z'
        for path in (OQ8_WORKFLOW, GATE_WORKFLOW):
            process, document = self.workflow(path)
            self.assertEqual(1, process.returncode)
            self.assert_contract(document, process.returncode)
            self.assertEqual(['APPROVAL_EDITED'], document['codes'])

    def test_availability_success_cannot_issue_a_gate_decision(self):
        f = self.f
        diagnostic = subprocess.run([sys.executable, '-B', str(f.root / T.OQ8)],
                                    text=True, capture_output=True, timeout=5)
        self.assertEqual(0, diagnostic.returncode, diagnostic.stderr)
        self.assertIn('availability verified', diagnostic.stdout)
        self.refused('INPUT_INVALID', 2, raw=diagnostic.stdout.encode())
        f.artifact()
        f.request['approvals'] = []
        self.refused('APPROVAL_MISSING')

    def test_local_candidate_saved_decision_and_unsupported_inputs_are_not_authority(self):
        self.f.artifact()
        process, decision = self.cli()
        self.assertEqual(0, process.returncode, process.stderr)
        self.refused('INPUT_INVALID', 2, raw=T.canonical(decision))
        self.refused('INPUT_INVALID', 2, raw=T.canonical(self.f.assert_pass()))
        self.refused('INPUT_INVALID', 2, extra=('--sealed-result', str(self.f.result_path)))
        self.refused('INPUT_INVALID', 2, extra=('evaluate',))
        # Saved success does not survive a failed platform recheck.
        self.f.state['responses'][T.API + '/actions/runs/7/attempts/2'] = {'error': 404}
        self.refused('API_UNAVAILABLE')

    def test_malformed_closed_requests_paths_and_coordinates_exit_two(self):
        for content in (b'{', b'[]', b'{"schema":1,"schema":2}', b'\xff'):
            with self.subTest(content=content):
                self.refused('INPUT_INVALID', 2, raw=content)
        for coordinates in ((None, None), ('7', None), ('0', '2'), ('-1', '2'), ('x', '2'), ('7', '1.0')):
            with self.subTest(coordinates=coordinates):
                self.refused('INPUT_INVALID', 2, coordinates=coordinates)
        self.f.request['subjects'][0]['path'] = '../unsafe'
        self.refused('INPUT_INVALID', 2)
        self.assertEqual([], self.f.state['observed'])

    def test_wrong_gate_transition_subject_mode_or_nfr_scope_is_refused(self):
        original = copy.deepcopy(self.f.request)
        for key, value in (('gate', 'G-RUNTIME-PARITY'), ('gate', 'G-TENANT'), ('gate', None),
                           ('transition', 'readiness'), ('subject_mode', 'ad26'), ('nfr_ids', ['NFR7'])):
            self.f.request = copy.deepcopy(original)
            self.f.request[key] = value
            with self.subTest(key=key, value=value):
                self.refused('ADAPTER_UNAVAILABLE')
        self.assertEqual([], self.f.state['observed'])

    def test_missing_failed_wrong_stale_runs_and_ordinary_oq8_run_are_refused(self):
        self.f.artifact()
        original = copy.deepcopy(self.f.state)
        mutations = [('conclusion', 'failure', 'RUN_NOT_SUCCESSFUL'), ('status', 'in_progress', 'RUN_NOT_SUCCESSFUL'),
                     ('head_sha', T.BEFORE, 'RUN_INVALID'), ('run_attempt', 1, 'RUN_INVALID'),
                     ('event', 'push', 'RUN_INVALID'), ('path', OQ8_WORKFLOW, 'WORKFLOW_MISMATCH')]
        for field, value, code in mutations:
            self.f.state = copy.deepcopy(original)
            self.f.api_document('/actions/runs/7/attempts/2')[field] = value
            with self.subTest(field=field):
                self.refused(code)
        self.f.state = copy.deepcopy(original)
        self.f.api_document('/check-suites/13')['app']['id'] = 1
        self.refused('CI_UNAUTHENTICATED')
        self.f.state = original
        self.refused('SELF_SEAL_FORBIDDEN', GITHUB_RUN_ID='7')

    def test_changed_controls_workflows_subjects_evidence_and_current_main_invalidate_pass(self):
        self.f.artifact()
        original = copy.deepcopy(self.f.state)
        paths = {*CONTROLS, OQ8_WORKFLOW, self.f.request['subjects'][0]['path'], self.f.request['evidence'][0]['path']}
        for path in paths:
            self.f.state = copy.deepcopy(original)
            self.f.blob(T.HEAD, path, (self.f.root / path).read_bytes() + b'\n# changed\n')
            with self.subTest(path=path):
                self.refused('SOURCE_MISMATCH')
        self.f.state = copy.deepcopy(original)
        self.f.put('/git/ref/heads/main', {'object': {'sha': T.BEFORE}})
        self.refused('SOURCE_MISMATCH')
        self.f.state = copy.deepcopy(original)
        self.f.state['responses'][T.API + '/git/ref/heads/main'] = {'sequence': [
            {'json': {'object': {'sha': T.HEAD}}}, {'json': {'object': {'sha': T.HEAD}}},
            {'json': {'object': {'sha': T.BEFORE}}}]}
        self.refused('SOURCE_MISMATCH')

    def test_current_approvals_window_level_predecessor_and_artifact_are_rechecked(self):
        self.f.artifact()
        original = copy.deepcopy(self.f.state)
        self.f.api_document('/issues/comments/100')['body'] = '{}'
        self.refused('APPROVAL_INVALID')
        self.f.state = copy.deepcopy(original)
        del self.f.state['responses'][T.API + '/issues/comments/100']
        self.refused('API_UNAVAILABLE')
        self.f.state = copy.deepcopy(original)
        self.f.api_document('/activity?ref=refs%2Fheads%2Fmain&per_page=100')[0]['timestamp'] = '2026-01-01T00:00:01Z'
        self.refused('APPROVAL_WINDOW')
        self.f.state = copy.deepcopy(original)
        self.f.request['required_level'] = T.INDEPENDENT
        self.refused('LEVEL_MISMATCH')
        self.f.request['required_level'] = T.SINGLE
        self.f.request['predecessors'][0]['output_sha256'] = '0' * 64
        self.f.approve()
        self.refused('PREDECESSOR_FAILED')
        self.f.state = copy.deepcopy(original)
        self.f.api_document('/actions/runs/7/artifacts?per_page=100')['artifacts'][0]['expired'] = True
        self.refused('ARTIFACT_INVALID')

    def test_genuine_oq8_validator_and_rejection_suite_must_both_succeed(self):
        self.f.artifact()
        self.f.replace_bound_file('docs/architecture/oq8/oq8-idempotency-design.md', b'corrupt design\n')
        self.f.approve()
        self.refused('PREDECESSOR_FAILED')

        # A valid design and successful raw validator cannot replace failing fixtures.
        self.f.replace_bound_file('docs/architecture/oq8/oq8-idempotency-design.md',
                                 (T.ROOT / 'docs/architecture/oq8/oq8-idempotency-design.md').read_bytes())
        self.f.replace_bound_file('tools/tests/test_oq8_governing_design.py', b'raise SystemExit(1)\n')
        self.f.approve()
        self.refused('PREDECESSOR_FAILED')

    def test_independent_roster_requires_distinct_approval_and_preserves_computed_labels(self):
        registry = json.loads((self.f.root / T.REGISTRY).read_text())
        registry['identities'].append({'id': 'github:reviewer', 'kind': 'human', 'github_id': 202})
        for role in registry['roles']:
            registry['roles'][role].append('github:reviewer')
        self.f.replace_bound_file(T.REGISTRY, T.canonical(registry))
        self.f.request['required_level'] = T.INDEPENDENT
        matrix = json.loads((self.f.root / T.MATRIX).read_text())
        for row in matrix['gates']:
            if row['classification'] == 'high-risk':
                row['required_level'] = T.INDEPENDENT
        self.f.replace_bound_file(T.MATRIX, T.canonical(matrix))
        self.f.request['predecessors'][0]['assurance_level'] = T.INDEPENDENT
        self.f.approve(user_id=202)
        process, candidate = self.f.run_cli()
        self.assertEqual(0, process.returncode, process.stderr)
        self.f.artifact(candidate)
        self.expected_level = T.INDEPENDENT
        process, document = self.cli()
        self.assertEqual(0, process.returncode, process.stderr)
        self.assertEqual(T.INDEPENDENT, document['achieved_level'])
        self.f.approve()
        self.refused('INDEPENDENT_REQUIRED')

    def test_symbolic_request_and_diagnostic_paths_fail_with_structured_output(self):
        self.f.request_path.write_bytes(T.canonical(self.f.request))
        symbolic = self.f.base / 'symbolic-request.json'
        symbolic.symlink_to(self.f.request_path)
        process = subprocess.run([sys.executable, '-B', str(self.f.root / CONSUMER),
                                  '--request', str(symbolic), '--run-id', '7', '--attempt', '2',
                                  '--result', str(self.f.result_path)],
                                 env=self.environment(), text=True, capture_output=True, timeout=5)
        self.assertEqual(2, process.returncode)
        self.assert_contract(json.loads(self.f.result_path.read_text()), process.returncode)
        self.assertEqual([], json.loads(self.f.state_path.read_text())['observed'])
        protected = self.f.base / 'protected'
        protected.write_text('unchanged')
        self.f.result_path.unlink()
        self.f.result_path.symlink_to(protected)
        process = subprocess.run([sys.executable, '-B', str(self.f.root / CONSUMER),
                                  '--request', str(symbolic), '--result', str(self.f.result_path)],
                                 env=self.environment(), text=True, capture_output=True, timeout=5)
        self.assertEqual(2, process.returncode)
        self.assert_contract(json.loads(process.stdout), process.returncode)
        self.assertEqual('unchanged', protected.read_text())

    def test_workflow_permissions_manual_guard_diagnostics_and_fixture_adoption(self):
        oq8 = (T.ROOT / OQ8_WORKFLOW).read_text()
        job = oq8.split('  assured-decision:\n', 1)[1]
        self.assertIn("if: github.event_name == 'workflow_dispatch' && (inputs.decision_request != '' || inputs.sealed_run_id != '' || inputs.sealed_attempt != '')", job)
        self.assertNotIn('needs:', job)
        self.assertNotIn(': write', oq8)
        self.assertIn('if: always()', job)
        self.assertIn('name: phase-4-gate-decision-', job)
        self.assertIn('  push:\n    branches: [main, release/parties-sdk-3.130.2]', oq8)
        self.assertIn('  pull_request:\n    branches: [main, release/parties-sdk-3.130.2]', oq8)
        for path in (OQ8_WORKFLOW, GATE_WORKFLOW):
            text = (T.ROOT / path).read_text()
            self.assertIn(CONSUMER + ' --request-env --run-id', text)
            self.assertNotIn(b'\r', (T.ROOT / path).read_bytes())
            for action in re.findall(r'uses: ([^\n]+)', text):
                self.assertRegex(action, r'@[0-9a-f]{40} # v[0-9]')
        fixtures = (T.ROOT / '.github/workflows/phase-4-transition-fixtures.yml').read_text()
        self.assertIn("-p 'test_phase_4_gate_decision.py'", fixtures)


if __name__ == '__main__':
    unittest.main()

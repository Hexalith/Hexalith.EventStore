"""Real CLI and real OQ8 integration with only the authenticated HTTP boundary substituted."""

import base64
import copy
import hashlib
import importlib.util
import io
import http.server
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import struct
import sys
import tempfile
import threading
import unittest
from unittest import mock
import urllib.error
import urllib.request
import zipfile

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'tools'))
import phase_4_transition as C

# Independently pinned contract and platform fixture identities.
REPO = 'Hexalith/Hexalith.EventStore'
API = 'https://api.github.com/repos/' + REPO
HEAD, BEFORE = 'a' * 40, 'b' * 40
TRANSITIONS = ('high-risk-gate-pass', 'high-risk-nfr-story-done', 'readiness', 'release-available',
               'production-promoted', 'consumer-removal')
SINGLE, INDEPENDENT = 'single-maintainer-attested', 'independent'
SCRIPTS = ('tools/phase_4_transition.py', 'tools/validate-phase-4-transition.py',
           'tools/validate-phase-4-gate-risk-matrix.py', 'tools/phase-4-transition.schema.json',
           'tools/phase_4_gate_decision.py', 'tools/validate-phase-4-gate-decision.py')
MATRIX = '_bmad-output/implementation-artifacts/phase-4-gate-risk-matrix.json'
REGISTRY = '_bmad-output/implementation-artifacts/owner-role-registry.json'
OQ8 = 'tools/validate-oq8-governing-design.py'
INPUTS = ('tools/tests/test_oq8_governing_design.py', '.editorconfig', '.gitattributes', 'docs/architecture/oq8/oq8-idempotency-design.md', 'docs/architecture/oq8/LICENSE',
          'docs/architecture/oq8/README.md', '_bmad-output/planning-artifacts/prd.md',
          '_bmad-output/planning-artifacts/architecture.md', '_bmad-output/planning-artifacts/epics.md',
          '_bmad-output/implementation-artifacts/spec-4-17-oq8-governing-design-authority-import.md',
          '.github/workflows/oq8-governing-design.yml')
SCHEMA = 'hexalith.eventstore.phase-4-transition-request.v1'


def hash_bytes(value):
    return hashlib.sha256(value).hexdigest()


def canonical(value):
    return json.dumps(value, sort_keys=True, separators=(',', ':'), ensure_ascii=True).encode()


def subject(files):
    return hash_bytes(''.join(sorted(f'{hash_bytes(content)}  {path}\n' for path, content in files)).encode())


class TransitionTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.base = Path(self.temp.name)
        self.root = self.base / 'checkout'
        matrix = json.loads((ROOT / MATRIX).read_text())
        self.row = next(row for row in matrix['gates'] if row['gate'] == 'G-OQ8')
        files = {*SCRIPTS, MATRIX, REGISTRY, OQ8, *INPUTS, *self.row['subject_paths'],
                 *(f'.github/workflows/phase-4-{t}.yml' for t in TRANSITIONS)}
        for path in files:
            destination = self.root / path
            destination.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(ROOT / path, destination)
        self.state = {'observed': [], 'responses': {}}
        self.state_path = self.base / 'http.json'
        self.request_path = self.base / 'request.json'
        self.result_path = self.base / 'result.json'
        self.site = self.base / 'python-site'
        self.site.mkdir()
        (self.site / 'sitecustomize.py').write_text('import phase_4_transport_fixture; phase_4_transport_fixture.install()\n')
        self.blobs = {(HEAD, p): (self.root / p).read_bytes() for p in files}
        self.request = {'schema': SCHEMA, 'repository': REPO, 'transition': 'high-risk-gate-pass', 'head_sha': HEAD,
                        'subject_mode': 'files', 'subjects': [self.file(p) for p in self.row['subject_paths']],
                        'evidence': [self.file(p) for p in (OQ8, *INPUTS) if p not in self.row['subject_paths']],
                        'matrix': self.file(MATRIX), 'registry': self.file(REGISTRY), 'required_level': SINGLE,
                        'author': 'github:jpiquot', 'gate': 'G-OQ8', 'nfr_ids': [], 'approvals': [], 'predecessors': []}
        output = (b'OQ8 governing-design availability verified: 1a55b0302e91233e12db91e6e245f0a22d6bf13fcf6cdf5ee0cbe5759f08dcd8\n'
                  b'Authority availability only; runtime conformance, assurance, readiness, release, deployment, consumer migration and Folders closure remain separate gates.\n')
        self.request['predecessors'] = [{'adapter': 'oq8-governing-design-v1', 'commit': HEAD,
                                         'validator': self.file(OQ8), 'inputs': [self.file(p) for p in INPUTS],
                                         'output_sha256': hash_bytes(output), 'assurance_level': SINGLE}]
        self.put('/git/ref/heads/main', {'object': {'sha': HEAD}})
        self.put('/activity?ref=refs%2Fheads%2Fmain&per_page=100', [self.activity(HEAD, BEFORE, '2026-01-01T00:00:00Z')])
        for path in self.row['subject_paths']:
            self.blobs[(BEFORE, path)] = self.blobs[(HEAD, path)]
        self.blobs[(BEFORE, self.row['subject_paths'][0])] += b'\n# old subject\n'
        self.populate_blobs()
        self.approve()
        self.add_run()

    def file(self, path):
        return {'path': path, 'sha256': hash_bytes((self.root / path).read_bytes())}

    def put(self, path, value, headers=None):
        self.state['responses'][API + path] = {'json': value, 'headers': headers or {}}

    def blob(self, commit, path, content):
        self.blobs[(commit, path)] = content
        self.put('/contents/' + path + '?ref=' + commit,
                 {'type': 'file', 'path': path, 'encoding': 'base64', 'size': len(content),
                  'content': base64.b64encode(content).decode(),
                  'sha': hashlib.sha1(b'blob ' + str(len(content)).encode() + b'\0' + content).hexdigest()})

    def populate_blobs(self):
        for (commit, path), content in list(self.blobs.items()):
            self.blob(commit, path, content)

    def activity(self, after, before, timestamp):
        return {'ref': 'refs/heads/main', 'activity_type': 'push', 'after': after, 'before': before, 'timestamp': timestamp}

    def approve(self, user_id=6775094, created='2026-01-02T00:00:00Z', roles=None):
        self.request['approvals'] = []
        roles = roles or self.row['evaluator_roles']
        bound = hash_bytes(canonical({k: v for k, v in self.request.items() if k != 'approvals'}))
        value = subject([(b['path'], (self.root / b['path']).read_bytes()) for b in self.request['subjects']])
        for index, role in enumerate(roles):
            payload = {'schema': 'hexalith.eventstore.phase-4-transition-approval.v1', 'repository': REPO,
                       'transition': self.request['transition'], 'head_sha': HEAD, 'role': role,
                       'subject_sha256': value, 'request_sha256': bound, 'required_level': self.request['required_level'],
                       'traceability_sha256': None, 'decision': 'approved'}
            body = canonical(payload).decode()
            self.request['approvals'].append({'comment_id': 100 + index, 'body_sha256': hash_bytes(body.encode()), 'role': role})
            self.put('/issues/comments/' + str(100 + index),
                     {'id': 100 + index, 'body': body, 'user': {'id': user_id, 'login': 'arbitrary-login', 'type': 'User'},
                      'created_at': created, 'updated_at': created, 'issue_url': API + '/issues/42',
                      'performed_via_github_app': None})

    def add_run(self):
        self.put('/actions/runs/7/attempts/2', {'id': 7, 'run_attempt': 2, 'event': 'workflow_dispatch', 'head_sha': HEAD,
                 'head_branch': 'main', 'repository': {'full_name': REPO}, 'head_repository': {'full_name': REPO},
                 'status': 'completed', 'conclusion': 'success', 'path': '.github/workflows/phase-4-high-risk-gate-pass.yml',
                 'workflow_id': 12, 'check_suite_id': 13})
        self.put('/actions/workflows/12', {'id': 12, 'path': '.github/workflows/phase-4-high-risk-gate-pass.yml', 'state': 'active'})
        self.put('/check-suites/13', {'id': 13, 'head_sha': HEAD, 'status': 'completed', 'conclusion': 'success',
                                   'app': {'id': 15368, 'slug': 'github-actions'}})
        self.put('/actions/runs/7/attempts/2/jobs?per_page=100', {'total_count': 1, 'jobs': [
            {'id': 14, 'name': 'Phase 4 transition attempt', 'run_id': 7, 'head_sha': HEAD,
             'status': 'completed', 'conclusion': 'success', 'check_run_url': API + '/check-runs/15'}]})
        self.put('/check-suites/13/check-runs?per_page=100', {'total_count': 1, 'check_runs': [
            {'id': 15, 'url': API + '/check-runs/15', 'head_sha': HEAD, 'app': {'id': 15368},
             'check_suite': {'id': 13}, 'status': 'completed', 'conclusion': 'success'}]})

    def api_document(self, endpoint):
        return self.state['responses'][API + endpoint]['json']

    def run_cli(self, phase='evaluate', env_extra=None, raw=None, expected=None):
        self.state_path.write_text(json.dumps(self.state))
        self.request_path.write_bytes(raw if raw is not None else canonical(self.request))
        env = {k: v for k, v in os.environ.items() if not k.startswith(('GH_', 'GITHUB_', 'PYTHON'))}
        env.update({'PYTHONPATH': str(self.site) + os.pathsep + str(ROOT / 'tools/tests'),
                    'PHASE4_FIXTURE_STATE': str(self.state_path), 'GH_TOKEN': 'fixture-secret-never-in-child'})
        env.update(env_extra or {})
        argv = [sys.executable, '-B', str(self.root / 'tools/validate-phase-4-transition.py'), phase,
                '--root', str(self.root), '--request', str(self.request_path), '--result', str(self.result_path)]
        if phase == 'consume':
            argv += ['--run-id', '7', '--attempt', '2']
        if expected:
            argv += ['--expected-transition', expected]
        result = subprocess.run(argv, text=True, capture_output=True, env=env, timeout=45)
        self.state = json.loads(self.state_path.read_text())
        self.assertNotIn('Traceback', result.stderr)
        self.assertTrue(self.result_path.exists(), result.stderr)
        return result, json.loads(self.result_path.read_text())

    def assert_pass(self, phase='evaluate'):
        result, document = self.run_cli(phase)
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertEqual('PASS', document['result'])
        self.assertEqual(SINGLE, document['achieved_level'])
        self.assertEqual(SINGLE, document['downstream_minimum'])
        return document

    def assert_refused(self, code, phase='evaluate', exit_code=1, **kwargs):
        result, document = self.run_cli(phase, **kwargs)
        self.assertEqual(exit_code, result.returncode, result.stderr)
        self.assertEqual('FAIL', document['result'])
        self.assertEqual([code], document['codes'], result.stderr)
        return document

    def artifact(self, candidate=None, extra=None):
        if candidate is None:
            candidate = self.assert_pass()
        buffer = io.BytesIO()
        with zipfile.ZipFile(buffer, 'w', compression=zipfile.ZIP_DEFLATED) as archive:
            archive.writestr('result.json', canonical(candidate))
            for path, content in (extra or {}).items():
                archive.writestr(path, content)
        content = buffer.getvalue()
        self.put('/actions/runs/7/artifacts?per_page=100', {'total_count': 1, 'artifacts': [
            {'id': 16, 'name': 'phase-4-transition-result-7-2', 'expired': False, 'size_in_bytes': len(content),
             'workflow_run': {'id': 7, 'head_sha': HEAD}, 'digest': 'sha256:' + hash_bytes(content)}]})
        self.state['responses'][API + '/actions/artifacts/16/zip'] = {'binary': base64.b64encode(content).decode()}

    def test_real_oq8_evaluation_then_authenticated_consumption(self):
        self.artifact()
        result = self.assert_pass('consume')
        self.assertEqual({'run_id': 7, 'attempt': 2, 'artifact_id': 16, 'ci_app_id': 15368}, result['seal'])
        self.assertEqual(5, len(result['approvals']))
        self.assertTrue(result['predecessors'])
        self.assertTrue(all(item['method'] == 'GET' for item in self.state['observed']))

    def test_every_run_identity_rejection_has_positive_control(self):
        self.artifact()
        original = copy.deepcopy(self.state)
        mutations = [
            ('/actions/runs/7/attempts/2', 'head_sha', BEFORE, 'RUN_INVALID'),
            ('/actions/runs/7/attempts/2', 'event', 'push', 'RUN_INVALID'),
            ('/actions/runs/7/attempts/2', 'run_attempt', 1, 'RUN_INVALID'),
            ('/actions/runs/7/attempts/2', 'repository', {'full_name': 'attacker/repo'}, 'RUN_INVALID'),
            ('/actions/runs/7/attempts/2', 'head_repository', {'full_name': 'attacker/repo'}, 'RUN_INVALID'),
            ('/actions/runs/7/attempts/2', 'head_branch', 'feature', 'RUN_INVALID'),
            ('/actions/runs/7/attempts/2', 'path', '.github/workflows/phase-4-gate-risk-matrix.yml', 'WORKFLOW_MISMATCH'),
            ('/actions/runs/7/attempts/2', 'status', 'in_progress', 'RUN_NOT_SUCCESSFUL'),
            ('/actions/runs/7/attempts/2', 'conclusion', 'failure', 'RUN_NOT_SUCCESSFUL'),
            ('/check-suites/13', 'app', {'id': 1, 'slug': 'github-actions'}, 'CI_UNAUTHENTICATED'),
            ('/check-suites/13', 'head_sha', BEFORE, 'CI_UNAUTHENTICATED'),
            ('/actions/workflows/12', 'path', '.github/workflows/ci.yml', 'WORKFLOW_MISMATCH'),
        ]
        for endpoint, field, value, code in mutations:
            with self.subTest(field=field, value=value):
                self.state = copy.deepcopy(original)
                self.api_document(endpoint)[field] = value
                self.assert_refused(code, 'consume')
        self.state = original
        self.assert_pass('consume')
        self.assert_refused('SELF_SEAL_FORBIDDEN', 'consume', env_extra={'GITHUB_RUN_ID': '7'})

    def test_missing_run_and_api_failures(self):
        self.assert_pass()
        for status in (401, 403, 404, 429, 500):
            self.state['responses'][API + '/actions/runs/7/attempts/2'] = {'error': status}
            self.assert_refused('API_UNAVAILABLE', 'consume')

    def test_approvals_are_refetched_and_every_field_bound(self):
        self.artifact()
        original = copy.deepcopy(self.state)
        mutations = [('id', 999, 'APPROVAL_INVALID'), ('body', '{}', 'APPROVAL_INVALID'),
                     ('user', {'id': 99, 'type': 'User'}, 'APPROVAL_IDENTITY'),
                     ('user', {'id': 6775094, 'type': 'Bot'}, 'APPROVAL_IDENTITY'),
                     ('performed_via_github_app', {'id': 1}, 'APPROVAL_IDENTITY'),
                     ('updated_at', '2026-01-03T00:00:00Z', 'APPROVAL_EDITED'),
                     ('issue_url', 'https://api.github.com/repos/attacker/repo/issues/1', 'APPROVAL_INVALID')]
        for field, value, code in mutations:
            with self.subTest(field=field):
                self.state = copy.deepcopy(original)
                self.api_document('/issues/comments/100')[field] = value
                self.assert_refused(code, 'consume')
        self.state = copy.deepcopy(original)
        self.state['responses'][API + '/issues/comments/100'] = {'error': 404}
        self.assert_refused('API_UNAVAILABLE', 'consume')
        self.state = original
        self.assert_pass('consume')

    def test_approval_payload_rejects_wrong_role_level_transition_subject_and_request(self):
        self.assert_pass()
        for field, value in [('role', 'release-owner'), ('required_level', INDEPENDENT),
                             ('transition', 'readiness'), ('subject_sha256', '0' * 64),
                             ('request_sha256', '0' * 64), ('decision', 'rejected')]:
            self.approve()
            record = self.api_document('/issues/comments/100')
            body = json.loads(record['body'])
            body[field] = value
            record['body'] = canonical(body).decode()
            self.request['approvals'][0]['body_sha256'] = hash_bytes(record['body'].encode())
            with self.subTest(field=field):
                self.assert_refused('APPROVAL_INVALID')

    def test_missing_approval_returns_copyable_payload_without_minting_record(self):
        self.assert_pass()
        self.request['approvals'] = []
        result = self.assert_refused('APPROVAL_MISSING')
        self.assertEqual(5, len(result['approval_payloads']))
        self.assertEqual('approved', result['approval_payloads'][0]['decision'])
        self.assertIsNone(result['seal'])
        self.approve()
        self.request['approvals'].pop()
        self.assert_refused('APPROVAL_MISSING')

    def test_24_hours_uses_push_time_including_non_subject_push_history(self):
        self.assert_pass()
        self.approve(created='2026-01-01T23:59:59Z')
        self.assert_refused('APPROVAL_WINDOW')
        self.approve(created='2099-01-02T00:00:00Z')
        self.assert_refused('APPROVAL_FUTURE')
        self.approve()
        middle = 'c' * 40
        for path in self.row['subject_paths']:
            self.blob(middle, path, self.blobs[(HEAD, path)])
        self.put('/activity?ref=refs%2Fheads%2Fmain&per_page=100', [self.activity(HEAD, middle, '2026-01-01T12:00:00Z')],
                 {'Link': '<' + API + '/activity?cursor=2>; rel="next"'})
        self.put('/activity?cursor=2', [self.activity(middle, BEFORE, '2026-01-01T00:00:00Z')])
        self.assert_pass()
        self.api_document('/activity?cursor=2')[0]['timestamp'] = '2026-01-01T00:00:01Z'
        self.assert_refused('APPROVAL_WINDOW')

    def test_missing_discontinuous_or_cyclic_push_history(self):
        self.assert_pass()
        endpoint = '/activity?ref=refs%2Fheads%2Fmain&per_page=100'
        self.put(endpoint, [])
        self.assert_refused('HISTORY_UNAVAILABLE')
        self.put(endpoint, [self.activity(BEFORE, 'c' * 40, '2026-01-01T00:00:00Z')])
        self.assert_refused('HISTORY_UNAVAILABLE')
        self.put(endpoint, [], {'Link': '<' + API + endpoint + '>; rel="next"'})
        self.assert_refused('PAGINATION_INCOMPLETE')
        self.put(endpoint, [], {'Link': '<https://evil.invalid/>; rel="next"'})
        self.assert_refused('API_INVALID')

    def test_source_matrix_registry_subject_evidence_and_workflow_are_exact(self):
        self.artifact()
        for path in (MATRIX, REGISTRY, INPUTS[0], OQ8, SCRIPTS[0], '.github/workflows/phase-4-high-risk-gate-pass.yml'):
            with self.subTest(path=path):
                previous = (self.root / path).read_bytes()
                (self.root / path).write_bytes(previous + b'\n')
                self.assert_refused('SOURCE_MISMATCH', 'consume')
                (self.root / path).write_bytes(previous)
        self.assert_pass('consume')
        self.api_document('/git/ref/heads/main')['object']['sha'] = BEFORE
        self.assert_refused('SOURCE_MISMATCH', 'consume')

    def test_prior_result_is_not_accepted_after_bound_request_changes(self):
        self.artifact()
        self.request['nfr_ids'] = ['NFR1']
        self.approve()
        self.assert_refused('RESULT_MISMATCH', 'consume')

    def test_predecessor_identity_rerun_failure_missing_and_downstream_minimum(self):
        self.assert_pass()
        original = copy.deepcopy(self.request)
        for field, value, code in [('commit', BEFORE, 'API_UNAVAILABLE'),
                                   ('adapter', 'caller-shell', 'ADAPTER_UNAVAILABLE'),
                                   ('output_sha256', '0' * 64, 'PREDECESSOR_FAILED'),
                                   ('assurance_level', 'none', 'LEVEL_MISMATCH')]:
            with self.subTest(field=field):
                self.request = copy.deepcopy(original)
                self.request['predecessors'][0][field] = value
                self.approve()
                self.assert_refused(code)
        self.request = copy.deepcopy(original)
        self.request['predecessors'][0]['validator']['sha256'] = '0' * 64
        self.approve()
        self.assert_refused('PREDECESSOR_IDENTITY')
        self.request = copy.deepcopy(original)
        self.request['predecessors'] = []
        self.approve()
        self.assert_refused('BUSINESS_PREREQUISITE_MISSING')
        # Corrupt the actual Git bytes while retaining independently approved validator pins.
        self.request = copy.deepcopy(original)
        self.approve()
        self.blob(HEAD, INPUTS[1], self.blobs[(HEAD, INPUTS[1])] + b'broken')
        self.assert_refused('SOURCE_MISMATCH')

    def test_all_six_transitions_and_absent_lifecycle_consumers_fail_closed(self):
        self.assert_pass()
        for transition in TRANSITIONS[1:]:
            self.request['transition'] = transition
            self.request['gate'] = None
            self.request['nfr_ids'] = ['NFR1'] if transition == 'high-risk-nfr-story-done' else []
            roles = {'high-risk-nfr-story-done': ['product-owner', 'test-owner'], 'readiness': ['product-owner'],
                     'release-available': ['release-owner'], 'production-promoted': ['deployment-owner'],
                     'consumer-removal': ['consumer-owner']}[transition]
            self.approve(roles=roles)
            with self.subTest(transition=transition):
                self.assert_refused('ADAPTER_UNAVAILABLE' if transition == 'high-risk-nfr-story-done' else 'BUSINESS_PREREQUISITE_MISSING')
        self.assertEqual({'NFR1','NFR2','NFR3','NFR4','NFR7','NFR10','NFR11','NFR12','NFR14','NFR15','NFR16','NFR17'}, C.HIGH_RISK_NFRS)

    def test_unbound_gate_and_runtime_parity_never_execute_caller_command(self):
        self.assert_pass()
        self.request['gate'] = 'G-RUNTIME-PARITY'
        self.approve()
        self.assert_refused('GATE_UNBOUND')
        self.request['gate'] = 'G-TENANT'
        self.approve()
        self.assert_refused('GATE_UNBOUND')

    def test_independent_numeric_roster_self_approval_and_aliasing(self):
        self.assert_pass()
        registry = json.loads((self.root / REGISTRY).read_text())
        registry['identities'].append({'id': 'github:reviewer', 'kind': 'human', 'github_id': 202})
        registry['identities'].append({'id': 'github:author-alias', 'kind': 'human', 'github_id': 6775094})
        for role in registry['roles']:
            registry['roles'][role] += ['github:reviewer', 'github:author-alias']
        content = canonical(registry)
        (self.root / REGISTRY).write_bytes(content)
        self.blob(HEAD, REGISTRY, content)
        self.request['registry'] = self.file(REGISTRY)
        self.assert_refused('LEVEL_MISMATCH')
        self.request['required_level'] = INDEPENDENT
        matrix = json.loads((self.root / MATRIX).read_text())
        for row in matrix['gates']:
            if row['classification'] == 'high-risk':
                row['required_level'] = INDEPENDENT
        self.replace_bound_file(MATRIX, canonical(matrix))
        self.request['predecessors'][0]['assurance_level'] = INDEPENDENT
        self.approve()
        self.assert_refused('INDEPENDENT_REQUIRED')
        self.approve(user_id=202)
        result, document = self.run_cli()
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertEqual(INDEPENDENT, document['achieved_level'])
        self.assertEqual(INDEPENDENT, document['downstream_minimum'])

    def test_artifact_identity_archive_and_result_rejections(self):
        candidate = self.assert_pass()
        self.artifact(candidate)
        self.assert_pass('consume')
        for field, value in [('expired', True), ('digest', 'sha256:' + '0'*64), ('name', 'local-file'),
                             ('workflow_run', {'id': 8, 'head_sha': HEAD})]:
            self.artifact(candidate)
            self.api_document('/actions/runs/7/artifacts?per_page=100')['artifacts'][0][field] = value
            with self.subTest(field=field):
                self.assert_refused('ARTIFACT_INVALID', 'consume')
        for path in ('../escaped', '/absolute', 'a/../../escaped', 'a\\escape'):
            self.artifact(candidate, {path: 'bad'})
            self.assert_refused('INPUT_INVALID', 'consume', exit_code=2)
        self.assertFalse((self.base / 'escaped').exists())
        candidate['seal'] = {'run_id': 7}
        self.artifact(candidate)
        self.assert_refused('RESULT_MISMATCH', 'consume')

    def test_jobs_checks_and_artifacts_paginate_without_omission(self):
        self.artifact()
        endpoint = '/actions/runs/7/artifacts?per_page=100'
        artifacts = self.api_document(endpoint)['artifacts']
        self.put(endpoint, {'total_count': 1, 'artifacts': []}, {'Link': '<' + API + '/artifacts?cursor=2>; rel="next"'})
        self.put('/artifacts?cursor=2', {'total_count': 1, 'artifacts': artifacts})
        self.assert_pass('consume')
        self.api_document('/artifacts?cursor=2')['total_count'] = 2
        self.assert_refused('PAGINATION_INCOMPLETE', 'consume')
        self.put(endpoint, {'total_count': 1, 'artifacts': artifacts})
        self.api_document('/actions/runs/7/attempts/2/jobs?per_page=100')['jobs'][0]['check_run_url'] += 'bad'
        self.assert_refused('CI_UNAUTHENTICATED', 'consume')

    def test_bad_input_closed_contract_paths_duplicates_and_cli_binding(self):
        self.assert_pass()
        original = copy.deepcopy(self.request)
        for update in ({'command': 'touch /tmp/forbidden'}, {'required_level': 'root'}, {'head_sha': 'x'},
                       {'subjects': [{'path': '../escape', 'sha256': '0'*64}]}, {'subjects': 'string'},
                       {'approvals': [{'comment_id': True, 'body_sha256': '0'*64, 'role': 'test-owner'}]}):
            self.request = copy.deepcopy(original)
            self.request.update(update)
            self.assert_refused('INPUT_INVALID', exit_code=2)
        self.request = original
        self.assert_refused('INPUT_INVALID', exit_code=2, raw=b'{"schema":1,"schema":2}')
        self.assert_refused('INPUT_INVALID', exit_code=2, raw=b'{"bad":NaN}')
        self.assert_refused('INPUT_INVALID', exit_code=2, expected='readiness')

    def test_ad26_exact_subject_preserves_header_rule_append_and_traceability_distinction(self):
        architecture = (ROOT / '_bmad-output/planning-artifacts/architecture.md').read_bytes()
        lines = architecture.decode().splitlines()
        first = next(n for n, line in enumerate(lines) if line.startswith('**Append race (NFR7 class (c)).**'))
        last = first
        while lines[last]:
            last += 1
        start = next(n for n, line in enumerate(lines) if line.startswith('### AD-26 '))
        end = start + 1
        while end < len(lines) and not lines[end].startswith(('### ', '## ')):
            end += 1
        while not lines[end-1]:
            end -= 1
        selected = lines[first:last] + [re.sub(r' \[(ASSUMPTION|ADOPTED)\]$', '', lines[start])] + lines[start+1:end]
        expected = hash_bytes(('\n'.join(selected) + '\n').encode())
        self.assertEqual(expected, C.ad26_subject(architecture))
        self.assertEqual(expected, C.ad26_subject(b'outside subject\n' + architecture))
        self.assertEqual(expected, C.ad26_subject(architecture.replace(b'### AD-26 - Production Runs Only On A Proven Fail-Closed Profile [ASSUMPTION]', b'### AD-26 - Production Runs Only On A Proven Fail-Closed Profile [ADOPTED]')))
        with self.assertRaises(C.Refusal):
            C.ad26_subject(architecture.replace(b'\n', b'\r\n'))

    def replace_bound_file(self, path, content):
        (self.root / path).write_bytes(content)
        self.blob(HEAD, path, content)
        for item in [*self.request['subjects'], *self.request['evidence'], self.request['matrix'], self.request['registry'],
                     self.request['predecessors'][0]['validator'], *self.request['predecessors'][0]['inputs']]:
            if item['path'] == path:
                item['sha256'] = hash_bytes(content)

    def test_fixed_predecessor_process_has_no_inherited_token_or_python_path(self):
        self.assert_pass()
        # Execute a pinned fixture validator that observes the real child process environment.
        script = b"import os,sys\nassert not any(k in os.environ for k in ('GH_TOKEN','GITHUB_TOKEN','PYTHONPATH','PHASE4_FIXTURE_STATE'))\nassert sys.flags.isolated and sys.flags.dont_write_bytecode\nprint('isolated')\n"
        self.replace_bound_file(OQ8, script)
        self.replace_bound_file('tools/tests/test_oq8_governing_design.py', b'pass\n')
        self.request['predecessors'][0]['output_sha256'] = hash_bytes(b'isolated\n')
        self.approve()
        self.assert_pass()

    def test_predecessor_failure_is_observed_from_real_pinned_code_and_output_is_bounded(self):
        self.assert_pass()
        self.replace_bound_file('docs/architecture/oq8/oq8-idempotency-design.md', b'corrupt imported design\n')
        self.approve()
        self.assert_refused('PREDECESSOR_FAILED')
        self.replace_bound_file(OQ8, b"import sys\nsys.stdout.write('x' * 2097152)\n")
        self.approve()
        self.assert_refused('PREDECESSOR_LIMIT')

    def test_historical_predecessor_is_retrieved_from_its_bound_commit(self):
        self.assert_pass()
        older = 'c' * 40
        predecessor = self.request['predecessors'][0]
        predecessor['commit'] = older
        for item in [predecessor['validator'], *predecessor['inputs']]:
            self.blob(older, item['path'], (self.root / item['path']).read_bytes())
        self.approve()
        self.assert_pass()
        self.assertTrue(any('?ref=' + older in item['url'] for item in self.state['observed']))

    def test_invalid_registry_author_subject_matrix_and_approval_role_are_observed(self):
        self.assert_pass()
        original = copy.deepcopy(self.request)
        self.request['author'] = 'bmad:murat'
        self.assert_refused('AUTHOR_INVALID')
        self.request = copy.deepcopy(original)
        self.request['subjects'].pop()
        self.assert_refused('SUBJECT_MISMATCH')
        self.request = copy.deepcopy(original)
        self.request['transition'] = 'production-promoted'
        self.assert_refused('MATRIX_INVALID')
        self.request = copy.deepcopy(original)
        self.request['approvals'][0]['role'] = 'release-owner'
        self.assert_refused('APPROVAL_ROLE')
        self.request = copy.deepcopy(original)
        registry = json.loads((self.root / REGISTRY).read_text())
        registry['identities'][0]['github_id'] = 0
        self.replace_bound_file(REGISTRY, canonical(registry))
        self.assert_refused('REGISTRY_INVALID')

    def test_ad26_approval_and_push_history_use_exact_subject_in_cli(self):
        self.assert_pass()
        path = '_bmad-output/planning-artifacts/architecture.md'
        self.request['subject_mode'] = 'ad26'
        self.request['subjects'] = [self.file(path)]
        # Keep every OQ8 dependency bound even though the AD-26 subject is narrower.
        existing = {item['path'] for item in self.request['evidence']}
        for required in [OQ8, *INPUTS]:
            if required != path and required not in existing:
                self.request['evidence'].append(self.file(required))
        self.request['transition'] = 'production-promoted'
        self.request['gate'] = None
        roles = ['deployment-owner', 'architecture-owner']
        self.approve(roles=roles)
        current = (self.root / path).read_bytes()
        self.blob(BEFORE, path, current.replace(b'oq8-postgresql-v1', b'oq8-postgresql-v0'))
        for reference in self.request['approvals']:
            record = self.api_document('/issues/comments/' + str(reference['comment_id']))
            body = json.loads(record['body'])
            body['subject_sha256'] = C.ad26_subject(current)
            body['request_sha256'] = hash_bytes(canonical({'repository': REPO, 'transition': self.request['transition'], 'subject_sha256': body['subject_sha256'], 'required_level': SINGLE}))
            body['traceability_sha256'] = hash_bytes(current)
            record['body'] = canonical(body).decode()
            reference['body_sha256'] = hash_bytes(record['body'].encode())
        result = self.assert_refused('BUSINESS_PREREQUISITE_MISSING')
        self.assertEqual(C.ad26_subject(current), result['binding']['subject_sha256'])
        self.assertEqual(2, len(result['approvals']))
        self.assertTrue(all(p['traceability_sha256'] == hash_bytes(current) for p in result['approval_payloads']))

    def test_ad26_historical_issuance_traceability_survives_outside_subject_edit(self):
        self.test_ad26_approval_and_push_history_use_exact_subject_in_cli()
        path = '_bmad-output/planning-artifacts/architecture.md'
        original = (self.root / path).read_bytes()
        issuance = 'd' * 40
        self.blob(issuance, path, original)
        for reference in self.request['approvals']:
            record = self.api_document('/issues/comments/' + str(reference['comment_id']))
            body = json.loads(record['body'])
            body['head_sha'] = issuance
            record['body'] = canonical(body).decode()
            reference['body_sha256'] = hash_bytes(record['body'].encode())
        self.replace_bound_file(path, b'outside the ratification subject\n' + original)
        result = self.assert_refused('BUSINESS_PREREQUISITE_MISSING')
        self.assertEqual(2, len(result['approvals']))
        self.assertTrue(all(p['traceability_sha256'] != hash_bytes(original) for p in result['approval_payloads']))
        self.blob(issuance, path, original + b'wrong traceability\n')
        self.assert_refused('APPROVAL_INVALID')

    def test_review_matrix_static_invariants_and_oq8_business_bindings(self):
        self.assert_pass()
        original = json.loads((self.root / MATRIX).read_text())
        mutations = [('classification', 'standard-control'), ('unexpected', True),
                     ('required_level', INDEPENDENT), ('achieved_level', SINGLE),
                     ('command', 'python3 unrelated.py'), ('trigger', 'arbitrary'),
                     ('evidence_identity', 'arbitrary'), ('pass_condition', 'arbitrary')]
        for key, value in mutations:
            matrix = copy.deepcopy(original)
            row = next(item for item in matrix['gates'] if item['gate'] == 'G-OQ8')
            row[key] = value
            self.replace_bound_file(MATRIX, canonical(matrix))
            self.approve()
            with self.subTest(key=key):
                self.assert_refused('MATRIX_INVALID')
        matrix = copy.deepcopy(original)
        next(item for item in matrix['gates'] if item['gate'] == 'G-NFR18')['reason'] = 'wrong-reason'
        self.replace_bound_file(MATRIX, canonical(matrix))
        self.approve()
        self.assert_refused('MATRIX_INVALID')
        self.replace_bound_file(MATRIX, canonical(original))
        self.approve()
        self.assert_pass()  # Other gates remain honestly unbound and unachieved.

    def test_review_corrupt_deflate_is_bad_input_without_traceback(self):
        self.artifact()
        endpoint = API + '/actions/artifacts/16/zip'
        content = bytearray(base64.b64decode(self.state['responses'][endpoint]['binary']))
        filename_length, extra_length = struct.unpack_from('<HH', content, 26)
        offset = 30 + filename_length + extra_length
        content[offset] = 255  # Invalid DEFLATE block type, with ZIP directory intact.
        self.state['responses'][endpoint]['binary'] = base64.b64encode(content).decode()
        self.api_document('/actions/runs/7/artifacts?per_page=100')['artifacts'][0]['digest'] = 'sha256:' + hash_bytes(content)
        self.assert_refused('INPUT_INVALID', 'consume', exit_code=2)

    def test_review_process_group_descendants_are_terminated_and_reaped(self):
        pid_file = self.base / 'pids.json'
        script = ("import json,os,subprocess,sys,time\n"
                  "child=subprocess.Popen([sys.executable,'-c','import time; time.sleep(300)'])\n"
                  f"open({str(pid_file)!r},'w').write(json.dumps([os.getpid(),child.pid]))\n"
                  "sys.stdout.write('x'*2097152); sys.stdout.flush()\n"
                  "time.sleep(300)\n").encode()
        self.replace_bound_file(OQ8, script)
        self.approve()
        self.assert_refused('PREDECESSOR_LIMIT')
        pids = json.loads(pid_file.read_text())
        self.assertEqual(2, len(pids))
        for pid in pids:
            self.assertFalse(Path('/proc', str(pid)).exists(), f'predecessor process {pid} remains')

    def test_review_quiet_hung_validator_times_out_exits_one_and_is_reaped(self):
        pid_file = self.base / 'quiet-pid'
        script = (f"import os,time\nopen({str(pid_file)!r},'w').write(str(os.getpid()))\ntime.sleep(300)\n").encode()
        self.replace_bound_file(OQ8, script)
        self.approve()
        self.assert_refused('PREDECESSOR_LIMIT')
        self.assertFalse(Path('/proc', pid_file.read_text()).exists())

    def test_review_push_history_stops_after_first_page_establishes_change(self):
        self.assert_pass()
        endpoint = API + '/activity?ref=refs%2Fheads%2Fmain&per_page=100'
        self.state['responses'][endpoint]['headers'] = {'Link': '<' + API + '/activity?cursor=unneeded>; rel="next"'}
        self.assert_pass()
        self.assertFalse(any(item['url'].endswith('cursor=unneeded') for item in self.state['observed']))

    def test_review_combined_binding_inventory_reaches_contract_boundary(self):
        for key in ('subjects', 'evidence'):
            while len(self.request[key]) < 100:
                path = f'boundary/{key}-{len(self.request[key])}.txt'
                destination = self.root / path
                destination.parent.mkdir(parents=True, exist_ok=True)
                destination.write_bytes(b'bound input\n')
                self.blob(HEAD, path, destination.read_bytes())
                self.blob(BEFORE, path, destination.read_bytes())
                self.request[key].append(self.file(path))
        self.request['approvals'] = []
        result = self.assert_refused('APPROVAL_MISSING')
        self.assertEqual(209, len(result['binding']['inputs']))
        schema = json.loads((ROOT / 'tools/phase-4-transition.schema.json').read_text())
        self.assertEqual(209, schema['$defs']['binding']['properties']['inputs']['maxItems'])
        self.request['subjects'].append({'path': 'boundary/too-many', 'sha256': '0' * 64})
        self.assert_refused('INPUT_INVALID', exit_code=2)

    def test_review_ad26_ignores_crlf_outside_selected_subject_only(self):
        content = (ROOT / '_bmad-output/planning-artifacts/architecture.md').read_bytes()
        expected = C.ad26_subject(content)
        self.assertEqual(expected, C.ad26_subject(b'outside\r\n' + content + b'outside\r\n'))
        changed = content.replace(b'### AD-26 - Production Runs Only On A Proven Fail-Closed Profile [ASSUMPTION]\n',
                                  b'### AD-26 - Production Runs Only On A Proven Fail-Closed Profile [ASSUMPTION]\r\n')
        with self.assertRaises(C.Refusal):
            C.ad26_subject(changed)

    def test_review_local_read_is_bounded_and_symlinks_still_fail(self):
        control = C.Control(self.root, self.request, None)
        path = self.root / 'large-input'
        with path.open('wb') as stream:
            stream.truncate(20 * 1024 * 1024)
        original = Path.open
        reads = []
        class Reader:
            def __init__(self, stream):
                self.stream = stream
            def __enter__(self):
                return self
            def __exit__(self, *args):
                self.stream.close()
            def fileno(self):
                return self.stream.fileno()
            def read(self, size=-1):
                reads.append(size)
                return self.stream.read(size)
        with mock.patch.object(Path, 'open', lambda target, *args, **kwargs: Reader(original(target, *args, **kwargs))):
            with self.assertRaises(C.Refusal) as caught:
                control.local('large-input')
        self.assertEqual(2, caught.exception.exit_code)
        self.assertEqual([1024 * 1024 + 1], reads)
        (self.root / 'symbolic-input').symlink_to(path)
        with self.assertRaises(C.Refusal):
            control.local('symbolic-input')

    def test_review_hashed_configuration_lf_rules_are_explicit(self):
        attributes = (ROOT / '.gitattributes').read_text().splitlines()
        config = (ROOT / '.editorconfig').read_text()
        for name in ('.editorconfig', '.gitattributes'):
            self.assertIn(name + ' text eol=lf', attributes)
            section = re.search(r'(?ms)^\[/' + re.escape(name) + r'\]\n(.*?)(?=^\[|\Z)', config)
            self.assertIsNotNone(section)
            self.assertIn('end_of_line = lf', section[1])
            self.assertNotIn(b'\r', (ROOT / name).read_bytes())

    def test_review_consumption_rechecks_head_after_predecessors(self):
        self.artifact()
        self.state['responses'][API + '/git/ref/heads/main'] = {'sequence': [
            {'json': {'object': {'sha': HEAD}}}, {'json': {'object': {'sha': BEFORE}}}]}
        self.assert_refused('SOURCE_MISMATCH', 'consume')

    def test_review_new_subject_file_is_confirmed_by_before_commit_tree(self):
        path = self.request['subjects'][0]['path']
        endpoint = API + '/contents/' + path + '?ref=' + BEFORE
        self.state['responses'][endpoint] = {'error': 404}
        tree = 'e' * 40
        self.put('/git/commits/' + BEFORE, {'sha': BEFORE, 'tree': {'sha': tree}})
        self.put('/git/trees/' + tree, {'sha': tree, 'truncated': False, 'tree': []})
        self.assert_pass()
        for status in (403, 500):
            self.state['responses'][endpoint] = {'error': status}
            self.assert_refused('API_UNAVAILABLE')
        self.state['responses'][endpoint] = {'error': 404}
        self.state['responses'][API + '/git/commits/' + BEFORE] = {'error': 404}
        self.assert_refused('API_UNAVAILABLE')

    def test_review_artifact_redirects_drop_credentials_and_refuse_unsafe_or_further_redirects(self):
        self.artifact()
        endpoint = API + '/actions/artifacts/16/zip'
        archive = self.state['responses'][endpoint]
        storage = 'https://fixture.blob.core.windows.net/artifact?signature=fixture'
        self.state['responses'][storage] = copy.deepcopy(archive)
        self.state['responses'][endpoint] = {'error': 302, 'headers': {'Location': storage}}
        self.assert_pass('consume')
        downloads = [item for item in self.state['observed'] if item['url'] == storage]
        self.assertTrue(downloads)
        self.assertTrue(all('authorization' not in item['headers'] for item in downloads))
        self.assertTrue(any('authorization' in item['headers'] for item in self.state['observed'] if item['url'] == endpoint))
        for location in ('http://fixture.blob.core.windows.net/archive', 'https://evil.invalid/archive',
                         'https://owner:secret@fixture.blob.core.windows.net/archive'):
            self.state['responses'][endpoint]['headers']['Location'] = location
            self.assert_refused('ARTIFACT_INVALID', 'consume')
        self.state['responses'][endpoint]['headers']['Location'] = storage
        self.state['responses'][storage] = {'error': 302, 'headers': {'Location': 'https://second.blob.core.windows.net/archive'}}
        self.assert_refused('API_UNAVAILABLE', 'consume')
        self.assertFalse(any(item['url'].startswith('https://second.') for item in self.state['observed']))

    def test_review_actual_redirect_handler_prevents_following_redirects(self):
        observed = []
        class Handler(http.server.BaseHTTPRequestHandler):
            def do_GET(self):
                observed.append(self.path)
                self.send_response(302)
                self.send_header('Location', '/second')
                self.end_headers()
            def log_message(self, *args):
                pass
        server = http.server.ThreadingHTTPServer(('127.0.0.1', 0), Handler)
        thread = threading.Thread(target=lambda: server.serve_forever(poll_interval=0.01))
        thread.start()
        try:
            opener = urllib.request.build_opener(C.NoRedirect())
            with self.assertRaises(urllib.error.HTTPError) as caught:
                opener.open(f'http://127.0.0.1:{server.server_port}/first', timeout=2)
            self.assertEqual(302, caught.exception.code)
            caught.exception.close()
            self.assertEqual(['/first'], observed)
        finally:
            server.shutdown()
            thread.join()
            server.server_close()

    def test_workflows_permissions_pins_triggers_lf_and_required_check_name(self):
        for transition in TRANSITIONS:
            path = ROOT / f'.github/workflows/phase-4-{transition}.yml'
            content = path.read_text()
            self.assertNotIn(b'\r', path.read_bytes())
            self.assertIn('  workflow_dispatch:', content)
            self.assertNotRegex(content, r'(?m)^  (push|pull_request):')
            self.assertNotIn(': write', content)
            self.assertIn('if: always()', content)
            self.assertIn('--expected-transition ' + transition, content)
            self.assertNotIn('${{ inputs.', content.split('run: |')[1])
            actions = re.findall(r'uses: ([^\n]+)', content)
            self.assertTrue(all(re.search(r'@[0-9a-f]{40} # v[0-9]', action) for action in actions))
        fixtures = (ROOT / '.github/workflows/phase-4-transition-fixtures.yml').read_text()
        self.assertIn('name: Phase 4 transition fixtures', fixtures)
        self.assertIn('  push:', fixtures)
        self.assertIn('  pull_request:', fixtures)
        schema = json.loads((ROOT / 'tools/phase-4-transition.schema.json').read_text())
        self.assertEqual(list(TRANSITIONS), schema['$defs']['request']['properties']['transition']['enum'])
        for definition in schema['$defs'].values():
            if definition.get('type') == 'object':
                self.assertIs(False, definition['additionalProperties'])


if __name__ == '__main__':
    unittest.main()

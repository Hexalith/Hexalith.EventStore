"""Real CLI and real OQ8 integration with only the authenticated HTTP boundary substituted."""

import base64
import copy
import contextlib
import hashlib
import importlib.util
import io
import http.server
import json
import os
from pathlib import Path
import re
import secrets
import shutil
import subprocess
import struct
import sys
import tempfile
import threading
import types
import unittest
from unittest import mock
import urllib.error
import urllib.request
import zipfile

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'tools'))
import phase_4_transition as C
import phase_4_readiness as R
import publication_authority as P

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


def workflow_run_bodies(content):
    """Read each literal or single-line run value without adjoining YAML fields."""
    lines = content.splitlines(keepends=True)
    bodies = []
    index = 0
    while index < len(lines):
        match = re.match(r'^(?P<indent> +)(?P<step>-[ \t]+)?run:[ \t]*(?P<value>[^\r\n]*)', lines[index])
        index += 1
        if not match:
            continue
        value = match['value'].strip()
        if not re.fullmatch(r'[|>][+\-1-9]*(?:[ \t]+#[^\r\n]*)?', value):
            bodies.append(value)
            continue
        body = []
        header_indent = len(match['indent']) + len(match['step'] or '')
        while index < len(lines):
            line = lines[index]
            indent = len(line) - len(line.lstrip(' '))
            if line.strip() and indent <= header_indent:
                break
            body.append(line)
            index += 1
        bodies.append(''.join(body))
    return bodies


class TransitionTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.base = Path(self.temp.name)
        self.root = self.base / 'checkout'
        matrix = json.loads((ROOT / MATRIX).read_text())
        self.row = next(row for row in matrix['gates'] if row['gate'] == 'G-OQ8')
        files = {*SCRIPTS, R.VERIFIER, *P.CONTROL_FILES, MATRIX, REGISTRY, OQ8, *INPUTS, *self.row['subject_paths'],
                 *(f'.github/workflows/phase-4-{t}.yml' for t in TRANSITIONS)}
        for path in files:
            destination = self.root / path
            destination.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(ROOT / path, destination)
        self.fixture_token = secrets.token_hex(32)
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

    def approve(self, user_id=6775094, created='2026-01-02T00:00:00Z', roles=None, first_id=100):
        self.request['approvals'] = []
        roles = roles or self.row['evaluator_roles']
        bound = hash_bytes(canonical({k: v for k, v in self.request.items() if k != 'approvals'}))
        value = subject([(b['path'], (self.root / b['path']).read_bytes()) for b in self.request['subjects']])
        if self.request['transition'] == 'readiness' and 'readiness' in self.request:
            value = hash_bytes(canonical({'baseline_tree': self.request['readiness']['baseline_tree']}))
        if self.request['transition'] in ('release-available', 'production-promoted') and self.request['subject_mode']=='files':
            projection=types.SimpleNamespace(request=self.request,api=types.SimpleNamespace(
                file=lambda commit,path,allow_absent=False:self.blobs.get((commit,path))))
            value=P.lifecycle_subject_at(projection,HEAD)
        for index, role in enumerate(roles):
            payload = {'schema': 'hexalith.eventstore.phase-4-transition-approval.v1', 'repository': REPO,
                       'transition': self.request['transition'], 'head_sha': self.request['head_sha'], 'role': role,
                       'subject_sha256': value, 'request_sha256': bound, 'required_level': self.request['required_level'],
                       'traceability_sha256': None, 'decision': 'approved'}
            body = canonical(payload).decode()
            self.request['approvals'].append({'comment_id': first_id + index, 'body_sha256': hash_bytes(body.encode()), 'role': role})
            self.put('/issues/comments/' + str(first_id + index),
                     {'id': first_id + index, 'body': body, 'user': {'id': user_id, 'login': 'arbitrary-login', 'type': 'User'},
                      'created_at': created, 'updated_at': created, 'issue_url': API + '/issues/42',
                      'performed_via_github_app': None})

    def add_run(self, run_id=7):
        head = self.request['head_sha']
        workflow = '.github/workflows/phase-4-high-risk-gate-pass.yml'
        base = f'/actions/runs/{run_id}'
        workflow_id, suite, job, check = range(run_id + 5, run_id + 9)
        success = {'status': 'completed', 'conclusion': 'success'}
        run = dict(success, id=run_id, run_attempt=2, event='workflow_dispatch', head_sha=head, head_branch='main',
                   repository={'full_name': REPO}, head_repository={'full_name': REPO}, path=workflow,
                   workflow_id=workflow_id, check_suite_id=suite)
        self.put(base + '/attempts/2', run)
        self.put(base, dict(run))
        self.put(f'/actions/workflows/{workflow_id}', {'id': workflow_id, 'path': workflow, 'state': 'active'})
        self.put(f'/check-suites/{suite}', dict(success, id=suite, head_sha=head, app={'id': 15368, 'slug': 'github-actions'}))
        url = API + f'/check-runs/{check}'
        self.put(base + '/attempts/2/jobs?per_page=100', {'total_count': 1, 'jobs': [
            dict(success, id=job, name='Phase 4 transition attempt', run_id=run_id, head_sha=head, check_run_url=url)]})
        self.put(f'/check-suites/{suite}/check-runs?per_page=100', {'total_count': 1, 'check_runs': [
            dict(success, id=check, url=url, head_sha=head, app={'id': 15368}, check_suite={'id': suite})]})

    def api_document(self, endpoint):
        return self.state['responses'][API + endpoint]['json']

    def run_cli(self, phase='evaluate', env_extra=None, raw=None, expected=None):
        # Publication needs the complete owner graph; unrelated OQ8 controls
        # should not repeatedly serialize those source blobs into HTTP state.
        if isinstance(self.request, dict) and self.request.get('transition') in ('release-available', 'production-promoted'):
            for path in P.B.SOURCES:
                if (HEAD, path) not in self.blobs:
                    content=(ROOT/path).read_bytes()
                    destination=self.root/path
                    destination.parent.mkdir(parents=True,exist_ok=True)
                    if not destination.exists(): destination.write_bytes(content)
                    self.blob(HEAD,path,content)
                    self.blob(BEFORE,path,content)
        self.state_path.write_text(json.dumps(self.state))
        self.request_path.write_bytes(raw if raw is not None else canonical(self.request))
        env = {k: v for k, v in os.environ.items() if k != 'PHASE4_TENANTS_READ_TOKEN' and not k.startswith(('GH_', 'GITHUB_', 'PYTHON'))}
        env.update({'PYTHONPATH': str(self.site) + os.pathsep + str(ROOT / 'tools/tests'),
                    'PHASE4_FIXTURE_STATE': str(self.state_path), 'GH_TOKEN': self.fixture_token})
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

    def observe_commands(self):
        log = self.base / 'commands.jsonl'
        with (self.site / 'sitecustomize.py').open('a') as startup:
            startup.write(f'phase_4_transport_fixture.observe_commands({str(log)!r})\n')
        return log

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

    def artifact(self, candidate=None, extra=None, run_id=7):
        if candidate is None:
            candidate = self.assert_pass()
        buffer = io.BytesIO()
        with zipfile.ZipFile(buffer, 'w', compression=zipfile.ZIP_DEFLATED) as archive:
            archive.writestr('result.json', canonical(candidate))
            for path, content in (extra or {}).items():
                archive.writestr(path, content)
        content = buffer.getvalue()
        self.put(f'/actions/runs/{run_id}/artifacts?per_page=100', {'total_count': 1, 'artifacts': [
            {'id': run_id + 9, 'name': f'phase-4-transition-result-{run_id}-2', 'expired': False, 'size_in_bytes': len(content),
             'workflow_run': {'id': run_id, 'head_sha': self.request['head_sha']}, 'digest': 'sha256:' + hash_bytes(content)}]})
        self.state['responses'][API + f'/actions/artifacts/{run_id + 9}/zip'] = {'binary': base64.b64encode(content).decode()}

    def test_real_oq8_evaluation_then_authenticated_consumption(self):
        self.artifact()
        command_log = self.observe_commands()
        result = self.assert_pass('consume')
        commands = [json.loads(line) for line in command_log.read_text().splitlines()]
        scripts = [Path(argv[3]).name for argv in commands
                   if len(argv) >= 4 and argv[:3] == [sys.executable, '-I', '-B']]
        self.assertCountEqual([Path(OQ8).name, 'test_oq8_governing_design.py'], scripts)
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

    def test_all_six_transitions_and_missing_lifecycle_prerequisites_fail_closed(self):
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
                self.assert_refused('STORY_IDENTITY_MISSING' if transition == 'high-risk-nfr-story-done' else 'BUSINESS_PREREQUISITE_MISSING')
        self.assertEqual({'NFR1','NFR2','NFR3','NFR4','NFR7','NFR10','NFR11','NFR12','NFR14','NFR15','NFR16','NFR17'}, C.HIGH_RISK_NFRS)

    def prepare_readiness(self):
        """A private clean commit and independently supplied synthetic owner outcomes."""
        import phase_4_tenant_fixture as F
        oq8 = copy.deepcopy(self.request)
        F.Qualification(self)
        tenant = copy.deepcopy(self.request)
        import phase_4_status_id_fixture as SF
        del self.request['tenant_qualification']
        SF.Qualification(self)
        status = copy.deepcopy(self.request)
        matrix = json.loads((self.root / MATRIX).read_text())
        rows = {row['gate']: row for row in matrix['gates']}
        for gate in R.GATES:
            row = rows[gate]
            if row['classification'] == 'high-risk' and row['command'] is None:
                row.update(command=f'owner validator for {gate}', trigger='owner CI', subject_paths=[C.M.PRD],
                           evidence_identity=f'{gate} CI evidence', pass_condition='owner validator passes',
                           binding_owner=None)
        (self.root / MATRIX).write_bytes(canonical(matrix))
        def git(*args):
            return subprocess.check_output(['git', '-C', str(self.root), *args], text=True).strip()
        git('init', '-q')
        git('add', '.')
        git('-c', 'user.name=Fixture', '-c', 'user.email=fixture@example.test',
            'commit', '-qm', 'test: prepare isolated readiness fixture')
        head, tree = git('rev-parse', 'HEAD'), git('rev-parse', 'HEAD^{tree}')
        for path in (*SCRIPTS, *R.CONTROL_FILES, MATRIX, REGISTRY, C.M.PRD, C.workflow_path('readiness')):
            self.blob(head, path, (self.root / path).read_bytes())
        self.put('/git/commits/' + head, {'sha': head, 'tree': {'sha': tree}})
        self.put('/git/ref/heads/main', {'object': {'sha': head}})
        self.put('/activity?ref=refs%2Fheads%2Fmain&per_page=100', [self.activity(head, '0' * 40, '2026-01-01T00:00:00Z')])
        workflow = C.workflow_path('readiness')
        run = self.api_document('/actions/runs/7/attempts/2')
        run.update(head_sha=head, path=workflow)
        self.put('/actions/runs/7', dict(run))
        self.api_document('/actions/workflows/12')['path'] = workflow
        self.api_document('/check-suites/13')['head_sha'] = head
        self.api_document('/actions/runs/7/attempts/2/jobs?per_page=100')['jobs'][0]['head_sha'] = head
        self.api_document('/check-suites/13/check-runs?per_page=100')['check_runs'][0]['head_sha'] = head
        self.owner_facts = {}
        claims = {}
        for i, gate in enumerate(R.GATES, 20):
            identities = {f'{name}_sha256': hash_bytes(f'{gate}/{name}'.encode())
                          for name in ('command', 'subject', 'evidence', 'result', 'approvals')}
            command = rows[gate].get('command')
            if command:
                identities['command_sha256'] = hash_bytes(command.encode())
            claims[gate] = dict(identities, run_id=i, attempt=1)
            if rows[gate]['classification'] == 'standard-control':
                claims[gate].update(run_id=None, attempt=None)
            self.owner_facts[gate] = dict(claims[gate], gate=gate, head_sha=head, result='PASS',
                                          approvers=[{'role': role, 'github_id': 6775094} for role in
                                                     rows[gate].get('evaluator_roles', ['product-owner'])],
                                          assurance_level=SINGLE if rows[gate]['classification'] == 'high-risk' else 'none')
        for gate, owner in [('G-OQ8', oq8), ('G-TENANT', tenant), ('G-STATUS-ID', status)]:
            owner.update(head_sha=head, matrix=self.file(MATRIX))
            claims[gate]['owner_request'] = owner
        for run in tenant['tenant_qualification']['runs'].values():
            if run['repository'] == REPO:
                run['head_sha'] = head
        status['status_qualification']['run']['head_sha'] = head
        claims['G-OQ8']['availability_run'] = {'run_id': 120, 'attempt': 1}
        self.request.update(transition='readiness', head_sha=head, gate=None, subjects=[self.file(C.M.PRD)],
                            evidence=[], matrix=self.file(MATRIX), predecessors=[], nfr_ids=[])
        del self.request['status_qualification']
        self.request['readiness'] = {'baseline_tree': tree, 'workflow_sha256': self.file(workflow)['sha256'],
                                     'verifier_sha256': hash_bytes(canonical({p: self.file(p)['sha256']
                                                       for p in (*SCRIPTS, *R.CONTROL_FILES)})), 'gates': claims}
        self.approve(roles=['product-owner'])
        self.start_patch(mock.patch('urllib.request.build_opener', return_value=F.Transport(self.state)))

    def start_patch(self, patcher):
        self.addCleanup(patcher.stop)
        return patcher.start()

    def install_synthetic_owners(self):
        return self.start_patch(mock.patch.object(R, 'owner_evidence',
                                side_effect=lambda control, gate, claim: self.owner_facts[gate]))

    def readiness_cli(self, phase='evaluate', *, raw=None, success=True, coordinates=('7', '2')):
        self.request_path.write_bytes(canonical(self.request) if raw is None else raw)
        argv = [phase, '--root', str(self.root), '--request', str(self.request_path),
                '--expected-transition', 'readiness', '--result', str(self.result_path)]
        if phase == 'consume':
            argv += ['--run-id', coordinates[0], '--attempt', coordinates[1]]
        with contextlib.redirect_stdout(io.StringIO()), contextlib.redirect_stderr(io.StringIO()) as err:
            code = C.main(argv)
        doc = json.loads(self.result_path.read_text())
        self.assertEqual(success, code == 0, err.getvalue())
        if not success:
            self.assertEqual('Reject', doc['readiness_decision']['disposition'])
            self.assertEqual([], doc['readiness_decision']['records'])
            self.assertEqual('none', doc['downstream_minimum'])
            self.assertIsNone(doc['seal'])
        return doc

    def test_readiness_sealed_order_source_hashes_and_minimum(self):
        self.prepare_readiness()
        owners = self.install_synthetic_owners()
        candidate = self.readiness_cli()
        self.assertNotIn('readiness_decision', candidate)
        self.assertIsNone(candidate['seal'])
        self.assertEqual(self.owner_facts, candidate['binding']['readiness']['gates'])
        self.artifact(candidate)
        result = self.readiness_cli('consume')
        self.assertEqual(30, owners.call_count)
        self.assertEqual('READY', result['readiness_decision']['disposition'])
        records = result['readiness_decision']['records']
        self.assertEqual(['G-HIGH-RISK', 'G-READINESS', 'READY'], [r['record'] for r in records])
        inputs = {g: hash_bytes(canonical(v)) for g, v in self.owner_facts.items()}
        seal_digest = hash_bytes(canonical({'candidate_sha256': hash_bytes(canonical(candidate)), 'seal': result['seal']}))
        self.assertEqual({**inputs, 'readiness-attempt': seal_digest}, records[0]['inputs'])
        for previous, current in zip(records, records[1:]):
            inputs[previous['record']] = hash_bytes(canonical(previous))
            self.assertEqual(inputs, current['inputs'])
        self.assertEqual(17, len(records[-1]['inputs']))
        self.assertEqual({SINGLE}, {r['assurance_level'] for r in records})
        self.assertEqual({hash_bytes(canonical(candidate['binding']))}, {r['source_sha256'] for r in records})
        self.artifact(result)
        refused = self.readiness_cli('consume', success=False)
        self.assertIn('RESULT_MISMATCH', refused['codes'])

    def test_readiness_inventory_rejects_omission_duplicate_and_downstream_input(self):
        self.prepare_readiness()
        gates = self.request['readiness']['gates']
        with mock.patch.dict(gates['G-NFR8'], attempt=1):
            self.assertEqual(['INPUT_INVALID'], self.readiness_cli(success=False)['codes'])
        with mock.patch.dict(gates['G-APPEND'], run_id=None, attempt=None):
            self.assertEqual(['INPUT_INVALID'], self.readiness_cli(success=False)['codes'])
        missing = gates.pop('G-MVP-COVERAGE')
        refused = self.readiness_cli(success=False)
        self.assertEqual('G-MVP-COVERAGE', refused['readiness_decision']['refusals'][0]['gate'])
        gates['G-MVP-COVERAGE'] = missing
        raw = canonical(self.request).replace(b'"G-BASELINE":', b'"G-BASELINE":{},"G-BASELINE":', 1)
        refused = self.readiness_cli(raw=raw, success=False)
        self.assertIn('G-BASELINE', refused['readiness_decision']['refusals'][0]['message'])
        gates['READY'] = missing
        self.assertIn('READY', self.readiness_cli(success=False)['readiness_decision']['refusals'][0]['message'])

    def test_readiness_rechecks_changed_subject_stale_evidence_failed_result_and_gate_approval(self):
        self.prepare_readiness()
        self.install_synthetic_owners()
        self.artifact(self.readiness_cli())
        for field, gate, value, code in [('subject_sha256', 'G-BASELINE', '0' * 64, 'PREDECESSOR_IDENTITY'),
                                        ('head_sha', 'G-MVP-COVERAGE', BEFORE, 'PREDECESSOR_IDENTITY'),
                                        ('attempt', 'G-OQ8', 2, 'PREDECESSOR_IDENTITY'),
                                        ('approvals_sha256', 'G-NFR18', '0' * 64, 'PREDECESSOR_IDENTITY'),
                                        ('result', 'G-CLAUSE', 'FAIL', 'PREDECESSOR_FAILED'),
                                        ('assurance_level', 'G-APPEND', 'none', 'LEVEL_MISMATCH')]:
            with self.subTest(field=field, gate=gate), mock.patch.dict(self.owner_facts[gate], {field: value}):
                refused = self.readiness_cli('consume', success=False)
                self.assertEqual([(gate, code)], [(r['gate'], r['code']) for r in refused['readiness_decision']['refusals']])
        (self.root / 'untracked').touch()
        self.assertEqual('G-BASELINE', self.readiness_cli('consume', success=False)['readiness_decision']['refusals'][0]['gate'])

    def test_readiness_lost_product_approval_and_unsuccessful_or_wrong_seal(self):
        self.prepare_readiness()
        self.install_synthetic_owners()
        self.artifact(self.readiness_cli())
        for endpoint, field, value, code in [
                ('/issues/comments/100', 'updated_at', '2026-02-02T00:00:00Z', 'APPROVAL_EDITED'),
                ('/actions/runs/7', 'run_attempt', 3, 'RUN_NOT_SUCCESSFUL'),
                ('/actions/runs/7/attempts/2', 'conclusion', 'failure', 'RUN_NOT_SUCCESSFUL'),
                ('/actions/runs/7/attempts/2', 'run_attempt', 1, 'RUN_INVALID')]:
            with self.subTest(field=field), mock.patch.dict(self.api_document(endpoint), {field: value}):
                self.assertEqual([code], self.readiness_cli('consume', success=False)['codes'])
        self.approve(created='2026-01-01T23:59:59Z', roles=['product-owner'])
        self.assertEqual(['APPROVAL_WINDOW'], self.readiness_cli(success=False)['codes'])
        self.request['approvals'] = []
        self.assertEqual(['APPROVAL_MISSING'], self.readiness_cli(success=False)['codes'])

    def test_live_readiness_resolvers_refuse_unavailable_owners_and_evidence(self):
        self.prepare_readiness()
        refusals = self.readiness_cli(success=False)['readiness_decision']['refusals']
        self.assertEqual(set(R.GATES), {r['gate'] for r in refusals})
        for row in refusals:
            self.assertEqual('API_UNAVAILABLE' if row['gate'] in R.GATE_DECISIONS else 'ADAPTER_UNAVAILABLE', row['code'])

    def verified_owner_gate(self, gate, claim, *, issue=False):
        """Use the production owner consumer against the fixture's authenticated endpoints."""
        import phase_4_tenant_fixture as F
        with mock.patch('urllib.request.build_opener', return_value=F.Transport(self.state)):
            current = C.Control(self.root, self.request, C.GitHub())
            current.rows = {row['gate']: row for row in json.loads((self.root / MATRIX).read_text())['gates']}
            if issue:
                self.artifact(current.evaluate())
                self.put('/actions/runs/7', dict(self.api_document('/actions/runs/7/attempts/2')))
            return R.owner_evidence(current, gate, dict(claim, owner_request=self.request, run_id=7, attempt=2))

    def add_oq8_availability_run(self):
        import phase_4_tenant_fixture as F
        workflow = '.github/workflows/oq8-governing-design.yml'
        run = dict(self.api_document('/actions/runs/7/attempts/2'), id=77, run_attempt=1, workflow_id=72,
                   check_suite_id=73, path=workflow, event='push')
        self.put('/actions/runs/77', dict(run))
        self.put('/actions/runs/77/attempts/1', run)
        self.put('/actions/workflows/72', {'id': 72, 'state': 'active', 'path': workflow})
        self.put('/check-suites/73', dict(self.api_document('/check-suites/13'), id=73))
        jobs, checks = [], []
        checkout = 'actions/checkout@' + '1' * 40
        success = {'status': 'completed', 'conclusion': 'success'}
        for index, (name, step) in enumerate([
                ('OQ8 governing-design fixtures', 'Test exact identity and rejection paths'),
                ('OQ8 governing-design live evaluation', 'Evaluate and report current authority availability')], 80):
            url = API + '/check-runs/' + str(index)
            jobs.append(dict(success, id=index, name=name, run_attempt=1, run_id=77, head_sha=HEAD, check_run_url=url,
                             steps=[dict(success, name='Run ' + checkout, started_at=F.START, completed_at=F.START),
                                    dict(success, name=step)]))
            checks.append(dict(success, id=index, name=name, head_sha=HEAD, check_suite={'id': 73}, app={'id': 15368}, url=url))
            log = F.Qualification.log_lines(F.START, ['##[group]Run ' + checkout, 'with:', '  repository: ' + REPO,
                      '##[endgroup]', 'Syncing repository: ' + REPO, '[command]/usr/bin/git log -1 --format=%H', HEAD])
            self.state['responses'][API + f'/actions/jobs/{index}/logs'] = {'binary': base64.b64encode(log.encode()).decode()}
        self.put('/actions/runs/77/attempts/1/jobs?per_page=100', {'total_count': 2, 'jobs': jobs})
        self.put('/check-suites/73/check-runs?filter=all&per_page=100', {'total_count': 2, 'check_runs': checks})

    def test_readiness_oq8_needs_both_hosted_jobs_and_its_separate_owner_seal(self):
        self.add_oq8_availability_run()
        claim = {'availability_run': {'run_id': 77, 'attempt': 1}}
        gate = self.verified_owner_gate('G-OQ8', claim, issue=True)
        self.assertEqual(('G-OQ8', HEAD, SINGLE), (gate['gate'], gate['head_sha'], gate['assurance_level']))
        job = self.api_document('/actions/runs/77/attempts/1/jobs?per_page=100')['jobs'][0]
        with mock.patch.dict(job, conclusion='skipped'), self.assertRaisesRegex(C.Refusal, 'missing successful job'):
            self.verified_owner_gate('G-OQ8', claim)

    def test_readiness_tenant_reconsumes_the_owner_qualification(self):
        import phase_4_tenant_fixture as F
        qualification = F.Qualification(self)
        record = self.verified_owner_gate('G-TENANT', {}, issue=True)
        self.assertEqual(('G-TENANT', HEAD, SINGLE), (record['gate'], record['head_sha'], record['assurance_level']))
        run = qualification.claim['runs']['tenants_source']['run_id']
        with mock.patch.dict(qualification.document('tenants_source', f'/actions/runs/{run}'), run_attempt=9):
            with self.assertRaisesRegex(C.Refusal, 'superseded'):
                self.verified_owner_gate('G-TENANT', {})

    def bind_real_readiness_owners(self):
        import phase_4_tenant_fixture as F
        parent = self.request
        head = parent['head_sha']
        claims = parent['readiness']['gates']
        rows = {row['gate']: row for row in json.loads((self.root / MATRIX).read_text())['gates']}
        self.add_oq8_availability_run()
        for gate, run_id in [('G-OQ8', 51), ('G-TENANT', 61), ('G-STATUS-ID', 81)]:
            claim = claims[gate]
            self.request = claim['owner_request']
            if gate == 'G-OQ8':
                self.request['predecessors'][0]['commit'] = head
                claim['availability_run'] = {'run_id': 77, 'attempt': 1}
            elif gate == 'G-STATUS-ID':
                import phase_4_status_id_fixture as SF
                SF.Qualification(self)
            else:
                # Generate the four real qualification lanes on the private Git head.
                F.Qualification(self)
            self.approve(roles=rows[gate]['evaluator_roles'], first_id=run_id * 10)
            self.add_run(run_id)
            for entry in [*self.request['subjects'], *self.request['evidence']]:
                self.blob(head, entry['path'], (self.root / entry['path']).read_bytes())
            self.blob(head, C.workflow_path('high-risk-gate-pass'), (self.root / C.workflow_path('high-risk-gate-pass')).read_bytes())
            verifier = C.Control(self.root, self.request, C.GitHub())
            self.artifact(verifier.evaluate(), run_id=run_id)
            claim.update(run_id=run_id, attempt=2)
            record = R.owner_evidence(verifier, gate, claim)
            claim.update({key: record[key] for key in R.IDENTITIES})
        self.request = parent
        self.approve(roles=['product-owner'])

    def test_readiness_enrolled_owners_revalidated_together_through_consumption(self):
        self.prepare_readiness()
        # The tenant qualification fixture uses its module's HEAD for checkout logs,
        # package/receipt source and gitlinks. Point all of them at this clean commit.
        with mock.patch('test_phase_4_transition.HEAD', self.request['head_sha']):
            self.bind_real_readiness_owners()
        refusals = self.readiness_cli(success=False)['readiness_decision']['refusals']
        self.assertIn('G-APPEND', {r['gate'] for r in refusals})
        self.assertFalse(any(r['gate'] in R.GATE_DECISIONS for r in refusals), refusals)
        enrolled = R.owner_evidence
        late_change = None
        def resolve(control, gate, claim):
            if gate in R.GATE_DECISIONS:
                return enrolled(control, gate, claim)
            if gate == 'G-MVP-COVERAGE' and late_change:
                late_change()
            return self.owner_facts[gate]
        with mock.patch.object(R, 'owner_evidence', side_effect=resolve):
            candidate = self.readiness_cli()
            self.artifact(candidate)
            self.assertEqual('READY', self.readiness_cli('consume')['readiness_decision']['disposition'])
            tenant_run = self.request['readiness']['gates']['G-TENANT']['owner_request']['tenant_qualification']['runs']['tenants_source']
            tenants_url = f"https://api.github.com/repos/{tenant_run['repository']}/actions/runs/{tenant_run['run_id']}"
            for gate, endpoint, field, value, code in [
                    ('G-OQ8', '/actions/runs/51', 'run_attempt', 3, 'RUN_NOT_SUCCESSFUL'),
                    ('G-OQ8', '/actions/runs/77', 'run_attempt', 2, 'RUN_NOT_SUCCESSFUL'),
                    ('G-TENANT', tenants_url, 'run_attempt', 2, 'RUN_NOT_SUCCESSFUL'),
                    ('G-TENANT', '/issues/comments/610', 'updated_at', '2026-02-02T00:00:00Z', 'APPROVAL_EDITED'),
                    ('G-STATUS-ID', '/actions/runs/81', 'run_attempt', 3, 'RUN_NOT_SUCCESSFUL'),
                    ('G-STATUS-ID', '/actions/runs/1009', 'run_attempt', 2, 'RUN_NOT_SUCCESSFUL'),
                    ('G-STATUS-ID', '/issues/comments/810', 'updated_at', '2026-02-02T00:00:00Z', 'APPROVAL_EDITED'),
                    ('G-READINESS', '/issues/comments/100', 'updated_at', '2026-02-02T00:00:00Z', 'APPROVAL_EDITED'),
                    ('G-READINESS', '/actions/runs/7', 'run_attempt', 3, 'RUN_NOT_SUCCESSFUL')]:
                with self.subTest(endpoint=endpoint):
                    url = API + endpoint if endpoint.startswith('/') else endpoint
                    doc = self.state['responses'][url]['json']
                    original = doc[field]
                    late_change = lambda: doc.update({field: value})
                    refused = self.readiness_cli('consume', success=False)
                    self.assertEqual([(gate, code)], [(r['gate'], r['code']) for r in refused['readiness_decision']['refusals']])
                    doc[field] = original
            listing = self.api_document('/actions/runs/1009/artifacts?per_page=100')
            artifacts = listing['artifacts']
            for name, mutation in [('contracts-test-results', 'disappear'), ('blocking-test-results', 'expire')]:
                changed = copy.deepcopy(artifacts)
                target = next(row for row in changed if row['name'] == name)
                if mutation == 'disappear':
                    changed.remove(target)
                else:
                    target['expires_at'] = '2020-01-01T00:00:00Z'
                late_change = lambda: listing.update(artifacts=changed, total_count=len(changed))
                refused = self.readiness_cli('consume', success=False)
                self.assertEqual([('G-STATUS-ID', 'ARTIFACT_INVALID')],
                                 [(r['gate'], r['code']) for r in refused['readiness_decision']['refusals']])
                listing.update(artifacts=artifacts, total_count=len(artifacts))
            # The other gates have already consumed the status decision when its archive changes.
            status_listing = API + '/actions/runs/81/artifacts?per_page=100'
            status_archive = API + '/actions/artifacts/90/zip'
            originals = {url: self.state['responses'][url] for url in (status_listing, status_archive)}
            late_change = lambda: self.artifact({'result': 'FAIL'}, run_id=81)
            refused = self.readiness_cli('consume', success=False)
            self.assertEqual([('G-STATUS-ID', 'RESULT_MISMATCH')],
                             [(r['gate'], r['code']) for r in refused['readiness_decision']['refusals']])
            self.state['responses'].update(originals)
            late_change = None
            jobs = self.api_document('/actions/runs/77/attempts/1/jobs?per_page=100')['jobs']
            old_id = jobs[0]['id']
            jobs[0]['id'] = 800
            self.state['responses'][API + '/actions/jobs/800/logs'] = self.state['responses'][API + f'/actions/jobs/{old_id}/logs']
            refused = self.readiness_cli('consume', success=False)
            finding, = refused['readiness_decision']['refusals']
            self.assertEqual(('G-OQ8', 'PREDECESSOR_IDENTITY'), (finding['gate'], finding['code']))
            self.assertIn('evidence_sha256', finding['message'])

    def test_readiness_structural_downstream_checks_and_numeric_aggregate_author(self):
        self.prepare_readiness()
        self.install_synthetic_owners()
        control = C.Control(self.root, self.request, C.GitHub())
        candidate = control.evaluate()
        matrix = json.loads((self.root / MATRIX).read_text())
        paths = {entry['path']: entry['sha256'] for entry in candidate['binding']['inputs']}
        for record in [{'record': 'release-available'}, dict(matrix['downstream'][0], record='unrecognized')]:
            with self.subTest(record=record), self.assertRaisesRegex(C.Refusal, 'downstream'):
                validator = C.M.Evaluation(self.root, MATRIX, C.dt.datetime.now(C.dt.timezone.utc))
                R.bind(control, paths, dict(matrix, downstream=[*matrix['downstream'], record]), validator)
        for label in ('invented', SINGLE):
            with self.subTest(label=label), self.assertRaisesRegex(C.Refusal, 'downstream'):
                validator = C.M.Evaluation(self.root, MATRIX, C.dt.datetime.now(C.dt.timezone.utc))
                R.bind(control, paths, dict(matrix, downstream=[dict(matrix['downstream'][0], assurance_level=label)]), validator)
        control.required = control.achieved = INDEPENDENT
        # All evaluator roles are covered by one numeric identity, the aggregate
        # author. Another registered person must actually approve each required role.
        control.registry['identities']['github:other'] = {'kind': 'human', 'github_id': 17}
        for role in control.registry['roles'].values():
            role.append('github:other')
        with self.assertRaises(C.Refusal) as refused:
            R.decide(control, candidate, {})
        self.assertEqual(('G-HIGH-RISK', 'INDEPENDENT_REQUIRED'), (refused.exception.gate, refused.exception.code))
        for record in control.binding['readiness']['gates'].values():
            if record['gate'] in C.M.HIGH_RISK_GATES:
                record['assurance_level'] = INDEPENDENT
            for approver in record['approvers']:
                approver['github_id'] = 17
        self.assertEqual('READY', R.decide(control, candidate, {})['disposition'])
        with mock.patch.dict(control.rows['G-HIGH-RISK'], author='github:other'), self.assertRaises(C.Refusal) as refused:
            R.decide(control, candidate, {})
        self.assertEqual('INDEPENDENT_REQUIRED', refused.exception.code)

    def test_readiness_sealed_gate_drift_tree_failure_duplicate_fields_and_invalid_coordinates(self):
        self.prepare_readiness()
        self.install_synthetic_owners()
        candidate = self.readiness_cli()
        candidate['binding']['readiness']['gates']['G-NFR18']['result_sha256'] = '0' * 64
        self.artifact(candidate)
        failed = self.readiness_cli('consume', success=False)
        self.assertEqual(('G-NFR18', 'RESULT_MISMATCH'), tuple(failed['readiness_decision']['refusals'][0][k] for k in ('gate', 'code')))
        for coordinates in [('', '2'), ('7', ''), ('abc', '2'), ('7', 'one')]:
            with self.subTest(coordinates=coordinates):
                err = self.readiness_cli('consume', success=False, coordinates=coordinates)
                self.assertEqual(['INPUT_INVALID'], err['codes'])
        with mock.patch.dict(os.environ, PHASE4_REQUEST_JSON=canonical(self.request).decode()), \
                contextlib.redirect_stdout(io.StringIO()), contextlib.redirect_stderr(io.StringIO()):
            code = C.main(['consume', '--root', str(self.root), '--request-env', '--expected-transition', 'readiness',
                           '--run-id=--invalid', '--attempt=', '--result', str(self.result_path)])
        self.assertEqual(2, code)
        self.assertEqual('Reject', json.loads(self.result_path.read_text())['readiness_decision']['disposition'])
        raw = canonical(self.request).replace(b'"G-CONSUMER":{"approvals_sha256":',
                                              b'"G-CONSUMER":{"approvals_sha256":"","approvals_sha256":', 1)
        self.assertEqual('G-CONSUMER', self.readiness_cli(raw=raw, success=False)['readiness_decision']['refusals'][0]['gate'])
        del self.state['responses'][API + '/git/commits/' + self.request['head_sha']]
        err = self.readiness_cli(success=False)
        self.assertEqual('G-BASELINE', err['readiness_decision']['refusals'][0]['gate'])

    def prepare_publication_transport(self):
        from test_publication_authority import chain
        records=chain()
        profile_bytes=(ROOT/'tests/Hexalith.EventStore.Contracts.Tests/Deployment/Fixtures/SyntheticProfile.v1.json').read_bytes()
        profile=json.loads(profile_bytes)
        rendered=b'synthetic rendered deployment'
        rendered_path='deploy/dapr/synthetic-rendered.yaml'
        source_path='deploy/dapr/synthetic-composition.json'
        inventory={'schema':'hexalith.eventstore.production-composing-inventory.v1',
                   'source':{'path':source_path,'sha256':hash_bytes(b'independent composition')},
                   'expectedApplicationIds':[a['id'] for a in profile['applications']],
                   'expectedComponentIds':[c['id'] for c in profile['components']]}
        profile_bytes=profile_bytes.replace(('\"renderedDeploymentDigest\":\"'+profile['environment']['renderedDeploymentDigest']+'\"').encode(),
            ('\"renderedDeploymentDigest\":\"'+hash_bytes(rendered)+'\"').encode())
        release=profile['release']
        packages=[{'id':a['id'],'version':a['version'],'sha256':a['digest']} for a in release['artifacts'] if a['kind']=='module-package']
        for index,record in enumerate(records['records']):
            record['subject'].update(packages=packages,server_package_sha256=packages[0]['sha256'],
                oci_index_sha256=release['composedHostDigest'],extensions_sha256=release['extensionDigests'],
                catalog_sha256=release['catalogContentDigest'],runtime_sha256=release['runtimeImageDigest'])
            record['registry']=self.file(REGISTRY)
            record['issuer'].update(id='github:jpiquot',github_id=6775094)
            if record['promotion']:
                promotion=record['promotion']
                promotion.update(environment=profile['environment']['id'],profile={'path':P.PROFILE,
                    'resolved_sha256':hash_bytes(profile_bytes),'infrastructure_sha256':profile['infrastructureDigest']},
                    inventory={'path':P.INVENTORY,'sha256':hash_bytes(canonical(inventory))},
                    rendered_deployment={'path':rendered_path,'sha256':hash_bytes(rendered)})
                promotion['deployment_sha256']=P.digest(P.canonical({'purpose':'publication-rendered-deployment','schemaVersion':1,
                    'oci_index_sha256':record['subject']['oci_index_sha256'],'profile_sha256':promotion['profile']['resolved_sha256'],
                    'rendered_sha256':promotion['rendered_deployment']['sha256']}))
            record['predecessor_sha256']=P.record_digest(records['records'][index-1]) if index else None
            body=canonical({'schema':'hexalith.eventstore.publication-authority-approval.v1','record_claim_sha256':P.claim_digest(record),
                'role':record['issuer']['role'],'subject_sha256':record['subject']['subject_sha256'],'decision':'approved'}).decode()
            record['approval']['body_sha256']=record['issuer']['authentication_evidence_sha256']=hash_bytes(body.encode())
            self.put('/issues/comments/'+str(record['approval']['comment_id']),{'id':record['approval']['comment_id'],'body':body,
                'user':{'id':6775094,'login':'owner','type':'User'},'performed_via_github_app':None,
                'created_at':'2026-01-02T00:00:00Z','updated_at':'2026-01-02T00:00:00Z','issue_url':API+'/issues/42'})
        P.validate_chain(records)
        contents={P.RECORDS:canonical(records),P.PROFILE:profile_bytes,P.INVENTORY:canonical(inventory),
                  source_path:b'independent composition',rendered_path:rendered}
        for source in (ROOT/'src/Hexalith.EventStore.Contracts/Deployment').glob('ProductionProfile*.cs'):
            contents[source.relative_to(ROOT).as_posix()]=source.read_bytes()
        for path,content in contents.items():
            destination=self.root/path; destination.parent.mkdir(parents=True,exist_ok=True); destination.write_bytes(content)
        paths=set(P.CONTROL_FILES)|set(contents)
        self.request.update(transition='production-promoted',gate=None,predecessors=[],evidence=[],subjects=[self.file(path) for path in sorted(paths)])
        for path in paths:
            content=(self.root/path).read_bytes()
            self.blob(HEAD,path,content); self.blob(BEFORE,path,content)
        older=copy.deepcopy(records)
        for record in older['records']: record['candidate']['sha256']='0'*64
        self.blob(BEFORE,P.RECORDS,canonical(older))
        self.approve(roles=['deployment-owner'])
        return records,rendered_path

    def test_fully_authenticated_publication_reaches_fixed_build_proof_and_checks_each_binding(self):
        _,rendered_path=self.prepare_publication_transport()
        result,document=self.run_cli(env_extra={'PATH':'/nonexistent'})
        self.assertEqual(1,result.returncode,result.stderr)
        self.assertIn(P.BUILD_INPUT_PROOF,result.stderr)
        self.assertEqual(['BUSINESS_PREREQUISITE_MISSING'],document['codes'])
        self.assertEqual(1,len(document['approvals']))
        comments={item['url'] for item in self.state['observed'] if '/issues/comments/' in item['url']}
        self.assertTrue({API+'/issues/comments/'+str(1000+i) for i in range(5)}<=comments)
        self.assertIsNone(document['seal'])
        for path in (P.PROFILE,P.INVENTORY,P.RECORDS,rendered_path,*P.B.CONTROLS,*P.B.SOURCES):
            original=(self.root/path).read_bytes()
            (self.root/path).write_bytes(original+b'\n')
            with self.subTest(path=path): self.assert_refused('SOURCE_MISMATCH')
            (self.root/path).write_bytes(original)

    def test_real_control_lifecycle_subject_tracks_claim_history_and_keeps_raw_binding(self):
        records,_=self.prepare_publication_transport()
        pending=copy.deepcopy(records)
        for record in pending['records']:
            record['approval']={'comment_id':None,'body_sha256':None}
            record['issuer']['authentication_evidence_sha256']=None
        self.blobs[(BEFORE,P.RECORDS)]=canonical(pending)
        older='c'*40
        for item in self.request['subjects']:
            self.blobs[(older,item['path'])]=self.blobs[(HEAD,item['path'])]
        authored=copy.deepcopy(pending); authored['records'][-1]['candidate']['sha256']='0'*64
        self.blobs[(older,P.RECORDS)]=canonical(authored)
        api=types.SimpleNamespace(file=lambda commit,path,allow_absent=False:self.blobs.get((commit,path)),iter_pages=lambda _: [[
            self.activity(HEAD,BEFORE,'2026-01-10T00:00:00Z'),self.activity(BEFORE,older,'2026-01-01T00:00:00Z')]])
        control=C.Control(self.root,self.request,api)
        control.subject=control.subject_at(HEAD)
        self.assertEqual(control.subject,control.subject_at(BEFORE))
        self.assertNotEqual(hash_bytes(self.blobs[(HEAD,P.RECORDS)]),hash_bytes(self.blobs[(BEFORE,P.RECORDS)]))
        self.assertEqual('2026-01-01T00:00:00+00:00',control.changed_push().isoformat())
        self.blobs[(BEFORE,P.RECORDS)]=canonical(authored)
        self.assertEqual('2026-01-10T00:00:00+00:00',control.changed_push().isoformat())
        bound=next(item for item in self.request['subjects'] if item['path']==P.RECORDS)
        self.assertEqual(hash_bytes(self.blobs[(HEAD,P.RECORDS)]),bound['sha256'])

    def test_fixed_publication_adapter_requires_bound_profile_inventory_records_and_owner_code(self):
        self.assert_pass()
        self.request['transition'] = 'release-available'
        self.request['gate'] = None
        self.approve(roles=['release-owner'])
        document = self.assert_refused('BUSINESS_PREREQUISITE_MISSING')
        bound = {item['path'] for item in document['binding']['inputs']}
        self.assertTrue(set(P.CONTROL_FILES) <= bound)
        self.assertEqual(1, len(document['approvals']))
        self.assertEqual('none', document['downstream_minimum'])
        self.assertIsNone(document['seal'])
        schema=json.loads((ROOT/'tools/phase-4-transition.schema.json').read_text())
        maximum=C.MAX_RESULT_INPUTS+len(P.CONTROL_FILES)+len(P.B.SOURCES)
        self.assertEqual(maximum,schema['$defs']['binding']['properties']['inputs']['maxItems'])
        self.assertEqual(maximum,schema['$defs']['binding']['allOf'][0]['else']['then']['properties']['inputs']['maxItems'])

    def assert_gate_refused_before_command(self, gate, code):
        self.request['gate'] = gate
        matrix = json.loads((self.root / MATRIX).read_text())
        row = next(row for row in matrix['gates'] if row['gate'] == gate)
        self.approve(roles=row['evaluator_roles'])
        command_log = self.observe_commands()
        self.assert_refused(code)
        self.assertEqual('', command_log.read_text())

    def test_unbound_append_gate_refuses_before_command(self):
        self.assert_gate_refused_before_command('G-APPEND', 'GATE_UNBOUND')

    def test_runtime_parity_follow_on_refuses_before_command(self):
        self.assert_gate_refused_before_command('G-RUNTIME-PARITY', 'GATE_UNBOUND')

    def test_bound_tenant_gate_missing_required_subjects_refuses_before_command(self):
        path = '.gitmodules'
        content = (ROOT / path).read_bytes()
        (self.root / path).write_bytes(content)
        self.blob(HEAD, path, content)
        self.request['subjects'].append(self.file(path))
        self.assert_gate_refused_before_command('G-TENANT', 'SUBJECT_MISMATCH')

    def test_status_id_checks_each_compiled_release_case_even_with_new_artifact_pins(self):
        import xml.etree.ElementTree as ET
        import phase_4_status_id_fixture as F
        S = F.S
        q = F.Qualification(self)
        for assembly, fragment, mutation in [
            (S.CONTRACTS, 'CanonicalUppercaseUlidIsValidUnderVersionTwo', 'duplicate'),
            (S.GENERATORS, 'Run_ReferencedCommandContract_GeneratesRouteTenantPostAction', 'omit'),
            (S.GENERATORS, '[v1 length 128]', 'skip'),
            (S.GENERATORS, '[v1 length 129]', 'omit'),
            (S.GENERATORS, '[published Double 1]', 'rename'),
            (S.GENERATORS, '[published Int64 2]', 'wrong-definition'),
            (S.SAMPLE, 'IncrementCounter_WhenMessageIdDiffersFromCorrelationId', 'fail'),
            (S.SERVER, 'EveryCatalogCommandDeclaresExactlyOneMessageIdVersion', 'debug-assembly'),
        ]:
            with self.subTest(fragment=fragment, mutation=mutation):
                artifact = 'contracts-test-results' if assembly == S.CONTRACTS else 'blocking-test-results'
                path = assembly + '/' + assembly + '.trx'
                original = q.files[artifact][path]
                tree = ET.fromstring(original)
                results = tree.find('{*}Results')
                row = next(r for r in results if fragment in r.get('testName'))
                unit = tree.find(f"./{{*}}TestDefinitions/{{*}}UnitTest[@id='{row.get('testId')}']")
                if mutation == 'duplicate':
                    other = next(r for r in results if fragment in r.get('testName') and r is not row)
                    row.attrib.update(other.attrib)
                elif mutation == 'omit':
                    results.remove(row)
                elif mutation == 'rename':
                    label = row.get('testName').replace('[published Double 1]', '[published Int16 1]')
                    row.set('testName', label)
                    unit.set('name', label)
                elif mutation == 'wrong-definition':
                    row.set('executionId', 'another-execution')
                elif mutation == 'debug-assembly':
                    path_to_dll = unit.get('storage').replace('/Release/', '/Debug/')
                    unit.set('storage', path_to_dll)
                    unit.find('{*}TestMethod').set('codeBase', path_to_dll)
                else:
                    row.set('outcome', 'NotExecuted' if mutation == 'skip' else 'Failed')
                summary = ET.SubElement(tree, '{' + S.TRX_NAMESPACE + '}ResultSummary', outcome='Completed')
                ET.SubElement(summary, '{' + S.TRX_NAMESPACE + '}Counters', passed=str(len(tree.find('{*}TestDefinitions'))))
                q.files[artifact][path] = ET.tostring(tree)
                q.publish('eventstore_ci', artifact, q.pin['artifacts'][artifact]['id'])
                self.approve()
                self.assert_refused('BUSINESS_PREREQUISITE_MISSING')
                q.files[artifact][path] = original
                q.publish('eventstore_ci', artifact, q.pin['artifacts'][artifact]['id'])
        self.approve()
        self.assert_pass()

    def test_status_id_reporter_requires_its_namespace_encoding_assembly_and_unique_executions(self):
        import xml.etree.ElementTree as ET
        import phase_4_status_id_fixture as F
        S = F.S
        data = F.trx(S.GENERATORS)
        self.assertEqual(73, len(S.check_cases(S.GENERATORS, data)['passing_cases']))
        foreign = data.replace(S.TRX_NAMESPACE.encode(), b'urn:foreign')
        mixed = data.replace(b'<TestMethod ', b'<TestMethod xmlns="urn:foreign" ', 1)
        dtd = '<!DOCTYPE TestRun [<!ENTITY case "text">]>'
        for raw in [foreign, mixed, (dtd + data.decode()).encode('utf-16'),
                    (dtd + data.decode()).encode('utf-16le'), (dtd + data.decode()).encode()]:
            with self.subTest(prefix=raw[:20]), self.assertRaises(C.Refusal):
                S.check_cases(S.GENERATORS, raw)
        for mutation in ('framework', 'other checkout', 'unrelated execution'):
            tree = ET.fromstring(data)
            defs = tree.find('{*}TestDefinitions')
            unit = defs[0]
            path = unit.get('storage')
            if mutation == 'unrelated execution':
                spare = copy.deepcopy(unit)
                spare.set('id', 'other')
                spare.find('{*}TestMethod').set('className', 'UnrelatedTest')
                spare.find('{*}Execution').set('id', 'other')
                defs.append(spare)
                results = tree.find('{*}Results')
                row = copy.deepcopy(results[0])
                row.set('testId', 'other')
                row.set('testName', 'UnrelatedTest.Example')
                results.insert(0, row)
            else:
                target = path.replace('net11.0', 'net8.0') if mutation == 'framework' else '/different-checkout' + path
                unit.set('storage', target)
                unit.find('{*}TestMethod').set('codeBase', target)
            with self.subTest(mutation=mutation), self.assertRaisesRegex(C.Refusal, 'assembly paths|execution ID reused'):
                S.check_cases(S.GENERATORS, ET.tostring(tree))

    def test_status_id_rejects_repinned_report_from_wrong_project_directory(self):
        import phase_4_status_id_fixture as F
        q = F.Qualification(self)
        name = 'contracts-test-results'
        files = q.files[name]
        path, = files
        files['wrong-project/' + path.split('/')[-1]] = files.pop(path)
        q.publish('eventstore_ci', name, q.pin['artifacts'][name]['id'])
        self.approve()
        self.assert_refused('ARTIFACT_INVALID')

    def test_status_id_authenticates_the_declared_workflow_both_jobs_and_artifact_producers(self):
        import phase_4_status_id_fixture as F
        q = F.Qualification(self)
        self.approve(created='2026-01-01T23:59:59Z')
        self.assert_refused('APPROVAL_WINDOW')
        self.approve()
        for endpoint, field, value, code in [
            ('/actions/runs/1009', 'status', 'in_progress', 'RUN_NOT_SUCCESSFUL'),
            ('/actions/runs/1009/attempts/1', 'head_sha', BEFORE, 'RUN_INVALID'),
            ('/actions/runs/1009/attempts/1', 'path', '.github/workflows/integration.yml', 'WORKFLOW_MISMATCH'),
            ('/check-suites/9', 'app', {'id': 12, 'slug': 'other'}, 'CI_UNAUTHENTICATED'),
        ]:
            old = self.api_document(endpoint)[field]
            self.api_document(endpoint)[field] = value
            self.assert_refused(code)
            self.api_document(endpoint)[field] = old
        for index in (0, 1):
            jobs = '/actions/runs/1009/attempts/1/jobs?per_page=100'
            self.api_document(jobs)['jobs'][index]['conclusion'] = 'failure'
            self.assert_refused('CI_UNAUTHENTICATED')
            self.api_document(jobs)['jobs'][index]['conclusion'] = 'success'
            endpoint = API + '/actions/jobs/' + str(900 + index) + '/logs'
            original = self.state['responses'][endpoint]['binary']
            for before, after, code in [(HEAD, BEFORE, 'SOURCE_MISMATCH'), ('Artifact ID is 9', 'Artifact ID is 1', 'ARTIFACT_INVALID')]:
                log = base64.b64decode(original).decode().replace(before, after)
                self.state['responses'][endpoint]['binary'] = base64.b64encode(log.encode()).decode()
                self.assert_refused(code)
            self.state['responses'][endpoint]['binary'] = original
        artifact = q.pin['artifacts']['blocking-test-results']['id']
        self.state['responses'][API + f'/actions/artifacts/{artifact}/zip']['binary'] = base64.b64encode(b'other bytes').decode()
        self.assert_refused('ARTIFACT_INVALID')

    def test_status_id_refuses_declaration_and_verifier_substitutions(self):
        import phase_4_status_id_fixture as F
        q = F.Qualification(self)
        for obj, field, value in [(q.claim, 'adapter_sha256', '1' * 64), (q.pin, 'workflow_sha256', '2' * 64)]:
            original = obj[field]
            obj[field] = value
            self.approve()
            self.assert_refused('SOURCE_MISMATCH')
            obj[field] = original
        matrix = json.loads((self.root / MATRIX).read_bytes())
        row = next(row for row in matrix['gates'] if row['gate'] == 'G-STATUS-ID')
        row['guarded_transitions'].remove('readiness')
        (self.root / MATRIX).write_bytes(canonical(matrix))
        self.blob(HEAD, MATRIX, canonical(matrix))
        self.request['matrix'] = self.file(MATRIX)
        self.approve()
        self.assert_refused('MATRIX_INVALID')

    def test_tenant_current_four_lane_evidence_and_exact_approval(self):
        import phase_4_tenant_fixture as F
        q = F.Qualification(self)
        transport = F.Transport(self.state)
        with mock.patch("urllib.request.build_opener", return_value=transport):
            def evaluate():
                return C.Control(self.root, self.request, C.GitHub()).evaluate()

            candidate = evaluate()
            self.assertEqual(q.claim, candidate["binding"]["tenant_qualification"])
            self.assertEqual(self.row["evaluator_roles"], [a["role"] for a in candidate["approvals"]])
            self.assertEqual(SINGLE, candidate["downstream_minimum"])
            original = copy.deepcopy(self.state["responses"])
            for lane in ("eventstore_ci", "eventstore_integration", "tenants_source", "tenants_package"):
                run = q.claim["runs"][lane]
                url = "/actions/runs/" + str(run["run_id"])
                for path, key, value, code in (
                    (url, "run_attempt", 2, "RUN_NOT_SUCCESSFUL"),
                    (url + "/attempts/1", "head_sha", BEFORE, "RUN_INVALID"),
                    (url + "/attempts/1", "repository", {"full_name": "other/repository"}, "RUN_INVALID"),
                    (url + "/attempts/1", "path", ".github/workflows/other.yml", "WORKFLOW_MISMATCH"),
                    (url + "/attempts/1", "conclusion", "failure", "RUN_NOT_SUCCESSFUL"),
                ):
                    with self.subTest(lane=lane, key=key):
                        self.state["responses"] = copy.deepcopy(original)
                        q.document(lane, path)[key] = value
                        with self.assertRaises(C.Refusal) as refused:
                            evaluate()
                        self.assertEqual(code, refused.exception.code)
            self.state["responses"] = copy.deepcopy(original)
            q.document("tenants_source", "/git/trees/" + F.TREE + "?recursive=1")["tree"][0]["sha"] = BEFORE
            with self.assertRaisesRegex(C.Refusal, "gitlink"):
                evaluate()
            self.state["responses"] = copy.deepcopy(original)
            del self.request["approvals"][-1]
            with self.assertRaisesRegex(C.Refusal, "every evaluator"):
                evaluate()

    def test_tenant_pull_request_requires_original_target_and_actual_checkout(self):
        import phase_4_tenant_fixture as F
        q = F.Qualification(self)
        lane = "tenants_source"
        run = q.claim["runs"][lane]
        attempt = q.document(lane, f"/actions/runs/{run['run_id']}/attempts/1")
        attempt.update(event="pull_request", head_branch="qualify", pull_requests=[])
        pr = {"head": {"ref": "qualify", "sha": F.TENANTS_HEAD, "repo": {"full_name": F.TENANTS}},
              "base": {"ref": "main", "repo": {"full_name": F.TENANTS}}}
        wrong_base = {**pr, "base": {**pr["base"], "ref": "elsewhere"}}
        q.put(F.TENANTS, "/commits/" + F.TENANTS_HEAD + "/pulls?per_page=100", [pr])
        transport = F.Transport(self.state)
        with mock.patch("urllib.request.build_opener", return_value=transport):
            def evaluate():
                return C.Control(self.root, self.request, C.GitHub()).evaluate()
            attempt["pull_requests"] = [pr]
            self.assertEqual("PASS", evaluate()["result"])
            for invalid in ([], [wrong_base], [pr, wrong_base],
                            [{**pr, "head": {**pr["head"], "sha": BEFORE}}],
                            [{**pr, "head": {**pr["head"], "ref": "another-branch"}}],
                            [{**pr, "head": {**pr["head"], "repo": {"full_name": "another/repo"}}}]):
                with self.subTest(invalid=invalid):
                    attempt["pull_requests"] = invalid
                    with self.assertRaises(C.Refusal) as refusal:
                        evaluate()
                    self.assertEqual("RUN_INVALID", refusal.exception.code)
            self.assertFalse(any("/pulls?" in url for url in transport.auth))
            log_url = F.prefix(F.TENANTS) + "/actions/jobs/300/logs"
            record = self.state["responses"][log_url]
            original = base64.b64decode(record["binary"]).decode()
            attempt["pull_requests"] = [pr]
            q.set_log(F.TENANTS, 300, original.replace(F.TENANTS_HEAD, "7" * 40))
            with self.assertRaisesRegex(C.Refusal, "actual checkout"):
                evaluate()
            attempt["pull_requests"] = []
            steps = q.document(lane, f"/actions/runs/{run['run_id']}/attempts/1/jobs?per_page=100")["jobs"][0]["steps"]
            steps.append({"name": "Run printenv GITHUB_BASE_REF", "started_at": F.END, "completed_at": F.END,
                          "status": "completed", "conclusion": "success"})
            lines = ["##[group]Run printenv GITHUB_BASE_REF", "printenv GITHUB_BASE_REF",
                     "shell: /usr/bin/bash -e {0}", "##[endgroup]", "main"]
            q.set_log(F.TENANTS, 300, original + q.log_lines(F.END, lines))
            self.assertEqual("PASS", evaluate()["result"])
            q.set_log(F.TENANTS, 300, original + q.log_lines(F.END, [*lines[:-1], "other"]))
            with self.assertRaisesRegex(C.Refusal, "ineligible original PR base"):
                evaluate()

    def test_tenant_modified_business_evidence_refuses_even_with_new_approval(self):
        import phase_4_tenant_fixture as F
        import phase_4_tenant_gate as A
        q = F.Qualification(self)
        transport = F.Transport(self.state)
        with mock.patch("urllib.request.build_opener", return_value=transport):
            def evaluate():
                return C.Control(self.root, self.request, C.GitHub()).evaluate()

            evaluate()
            for lane, name, file, path, value in (
                ("tenants_source", A.SOURCE_PROFILE, A.RUNTIME, ("persistedTenant",), False),
                ("tenants_source", A.SOURCE_PROFILE, A.RUNTIME, ("outstandingLeases",), False),
                ("tenants_source", A.SOURCE_PROFILE, A.RUNTIME, ("distinctAuditOwnerReadback",), False),
                ("tenants_source", A.SOURCE_PROFILE, A.RUNTIME, ("sdkSource",), BEFORE),
                ("tenants_source", A.SOURCE_PROFILE, A.BACKEND, ("backend", "initialDatabaseSize"), 1),
                ("tenants_source", A.SOURCE_PROFILE, A.BACKEND, ("identity", "runAttempt"), "2"),
                ("tenants_source", A.SOURCE_PROFILE, A.BACKEND, ("backend", "created"), "2019-01-01T00:00:00Z"),
                ("tenants_package", A.REST_RESULTS, A.PACKAGE,
                 ("runtimeManifests", "Hexalith.Tenants.Api", "packages", "Hexalith.McpCli.Abstractions"), "6.0.0"),
            ):
                with self.subTest(file=file, path=path):
                    original = q.files[name][file]
                    data = json.loads(original)
                    target = data
                    for key in path[:-1]:
                        target = target[key]
                    target[path[-1]] = value
                    q.files[name][file] = canonical(data)
                    artifact_id = q.claim["runs"][lane]["artifacts"][name]["id"]
                    q.publish(lane, name, artifact_id)
                    with self.assertRaises(C.Refusal) as stale_approval:
                        evaluate()
                    self.assertEqual("APPROVAL_INVALID", stale_approval.exception.code)
                    self.approve()
                    with self.assertRaises(C.Refusal) as refusal:
                        evaluate()
                    self.assertEqual("BUSINESS_PREREQUISITE_MISSING", refusal.exception.code)
                    q.files[name][file] = original
                    q.publish(lane, name, artifact_id)
                    self.approve()
            # Keep the method, count and status, but replace the one mixed-case
            # request/grant with another passing theory row in the same class.
            name = "aspire-test-results"
            file = "aspire-results.trx"
            original = q.files[name][file]
            self.assertIn(b"TENANT-ALPHA", original)
            q.files[name][file] = original.replace(b"TENANT-ALPHA", b"other-grant")
            artifact_id = q.claim["runs"]["tenants_package"]["artifacts"][name]["id"]
            q.publish("tenants_package", name, artifact_id)
            self.approve()
            with self.assertRaisesRegex(C.Refusal, "missing required case"):
                evaluate()
            q.files[name][file] = original
            q.publish("tenants_package", name, artifact_id)
            self.approve()
            trx = "authenticated-rest-rest.trx"
            artifact_id = q.claim["runs"]["tenants_package"]["artifacts"][A.REST_RESULTS]["id"]
            original = q.files[A.REST_RESULTS][trx]
            for modified in (original.replace(b"SixAuthenticatedDaprRestReadsMatchPersistedState", b"UnrelatedRead"),
                             original.replace(b'outcome="Passed"', b'outcome="NotExecuted"', 1)):
                q.files[A.REST_RESULTS][trx] = modified
                q.publish("tenants_package", A.REST_RESULTS, artifact_id)
                self.approve()
                with self.assertRaisesRegex(C.Refusal, "AuthenticatedTenantsRouteTests"):
                    evaluate()

    def test_tenant_jobs_and_artifact_are_bound_to_their_attempt_and_digest(self):
        import phase_4_tenant_fixture as F
        import phase_4_tenant_gate as A
        q = F.Qualification(self)
        transport = F.Transport(self.state)
        lane = "tenants_source"
        run = q.claim["runs"][lane]
        base = "/actions/runs/" + str(run["run_id"])
        artifacts = q.document(lane, base + "/artifacts?per_page=100")["artifacts"]
        job = q.document(lane, base + "/attempts/1/jobs?per_page=100")["jobs"][0]
        check = q.document(lane, "/check-suites/3/check-runs?filter=all&per_page=100")["check_runs"][0]
        with mock.patch("urllib.request.build_opener", return_value=transport):
            for obj, key, wrong, code in (
                (artifacts[0], "expired", True, "ARTIFACT_INVALID"),
                (artifacts[0], "expires_at", F.END, "ARTIFACT_INVALID"),
                (artifacts[0], "created_at", "2019-01-01T00:00:00Z", "ARTIFACT_INVALID"),
                (artifacts[0], "id", 999, "ARTIFACT_INVALID"),
                (artifacts[0], "digest", "sha256:" + "0" * 64, "ARTIFACT_INVALID"),
                (job, "run_attempt", 2, "CI_UNAUTHENTICATED"),
                (next(s for s in job["steps"] if s["name"] == "Require actual cohort binding, owned bootstrap, persisted effects and refusal"),
                 "conclusion", "skipped", "BUSINESS_PREREQUISITE_MISSING"),
                (check, "app", {"id": 999}, "CI_UNAUTHENTICATED"),
            ):
                with self.subTest(key=key):
                    original, obj[key] = obj[key], wrong
                    with self.assertRaises(C.Refusal) as refusal:
                        C.Control(self.root, self.request, C.GitHub()).evaluate()
                    self.assertEqual(code, refusal.exception.code)
                    obj[key] = original
            q.files[A.SOURCE_PROFILE].pop(A.RUNTIME)
            q.publish(lane, A.SOURCE_PROFILE, run["artifacts"][A.SOURCE_PROFILE]["id"])
            self.approve()
            with self.assertRaisesRegex(C.Refusal, "missing required report"):
                C.Control(self.root, self.request, C.GitHub()).evaluate()

    def test_tenant_authentication_tracks_each_repository_and_rechecks_live_attempts(self):
        import phase_4_tenant_fixture as F
        q = F.Qualification(self)
        transport = F.Transport(self.state)
        with mock.patch("urllib.request.build_opener", return_value=transport), mock.patch.dict(os.environ, {
                "GH_TOKEN": "test-only-sdk", "PHASE4_TENANTS_READ_TOKEN": "test-only-owner"}):
            def evaluate():
                return C.Control(self.root, self.request, C.GitHub()).evaluate()

            self.assertEqual("PASS", evaluate()["result"])
            for repo, credential in ((REPO, "test-only-sdk"), (F.TENANTS, "test-only-owner")):
                observed = [token for url, token in transport.auth.items() if url.startswith(F.prefix(repo) + "/")]
                self.assertGreater(len(observed), 5)
                self.assertEqual({"Bearer " + credential}, set(observed))
            url = API + "/actions/runs/" + str(q.claim["runs"]["eventstore_ci"]["run_id"])
            original = self.state["responses"][url]
            for mutation in ({"run_attempt": 2}, {"head_sha": BEFORE}, {"conclusion": "failure"}):
                self.state["responses"][url] = {"sequence": [original, {"json": {**original["json"], **mutation}}]}
                with self.assertRaises(C.Refusal) as refused:
                    evaluate()
                self.assertEqual("RUN_NOT_SUCCESSFUL", refused.exception.code)

    def test_tenant_artifact_pins_job_upload_and_download_bytes(self):
        import phase_4_tenant_fixture as F
        q = F.Qualification(self)
        lane, name = "tenants_source", "tenants-source-catalog-owner-profile"
        run = q.claim["runs"][lane]
        inventory = q.document(lane, f"/actions/runs/{run['run_id']}/artifacts?per_page=100")
        inventory["artifacts"].append({**inventory["artifacts"][0], "id": 808, "expired": True})
        inventory["total_count"] = 2
        with mock.patch("urllib.request.build_opener", return_value=F.Transport(self.state)):
            def evaluate():
                return C.Control(self.root, self.request, C.GitHub()).evaluate()

            self.assertEqual("PASS", evaluate()["result"])
            pin = run["artifacts"][name]
            archive = self.state["responses"][F.prefix(F.TENANTS) + f"/actions/artifacts/{pin['id']}/zip"]
            b64 = archive["binary"]
            data = base64.b64decode(b64)
            archive["binary"] = base64.b64encode(data[:-1] + bytes([data[-1] ^ 1])).decode()
            with self.assertRaisesRegex(C.Refusal, "downloaded artifact differs"):
                evaluate()
            archive["binary"] = b64
            record = self.state["responses"][F.prefix(F.TENANTS) + "/actions/jobs/300/logs"]
            original = base64.b64decode(record["binary"]).decode()
            # The other job ran concurrently and has the expected upload text,
            # but cannot establish that job 300 produced the archive.
            q.set_log(F.TENANTS, 301, original)
            for changed in (original.replace("Artifact ID is 300", "Artifact ID is 808"),
                            original.replace("/work/tenants-source-catalog-result.json", "/tmp/other.json")):
                q.set_log(F.TENANTS, 300, changed)
                with self.assertRaises(C.Refusal) as refused:
                    evaluate()
                self.assertEqual("ARTIFACT_INVALID", refused.exception.code)
            del self.state["responses"][F.prefix(F.TENANTS) + "/actions/jobs/300/logs"]
            with self.assertRaises(C.Refusal) as unavailable:
                evaluate()
            self.assertEqual("API_UNAVAILABLE", unavailable.exception.code)

    def test_tenant_release_assembly_required_methods_arguments_and_malformed_receipts(self):
        import phase_4_tenant_fixture as F
        import phase_4_tenant_gate as A
        import xml.etree.ElementTree as ET
        server = "Hexalith.EventStore.Server.Tests.trx"
        original = F.trx(server)
        signed = b"Bound_command_requires_exact_signed_requester_and_domain"
        self.assertIn(signed, original)
        with self.assertRaisesRegex(C.Refusal, signed.decode()):
            A.check_cases(server, original.replace(signed, b"AnotherWorkloadTest"))
        for attribute in ("storage", "codeBase"):
            trx = ET.fromstring(original)
            for elem in trx.iter():
                if attribute in elem.attrib:
                    elem.set(attribute, elem.get(attribute).replace("/Release/", "/Debug/"))
            with self.assertRaisesRegex(C.Refusal, "wrong Release assembly"):
                A.check_cases(server, ET.tostring(trx))
        admin = "Hexalith.EventStore.Admin.Server.Tests.trx"
        original = F.trx(admin)
        wrong_arguments = original.replace(b"permitted: False", b"permitted: Other")
        self.assertNotEqual(original, wrong_arguments)
        with self.assertRaisesRegex(C.Refusal, "missing required case"):
            A.check_cases(admin, wrong_arguments)
        rest = "authenticated-rest-rest.trx"
        original = F.trx(rest)
        for required in (b"AnonymousRestRequestCannotReadCommandSeededTenant",
                         b"InvalidProtectedCursorHasRealSentinelAndRecoversPageOneThroughProductionGateway"):
            self.assertIn(required, original)
            with self.assertRaisesRegex(C.Refusal, required.decode()):
                A.check_cases(rest, original.replace(required, b"AnotherPassingRestTest"))
        for content in (b'<?xml version="1.0" encoding="unsupported-encoding"?>' + original, b"<NotAReport"):
            with self.assertRaisesRegex(C.Refusal, "malformed TRX"):
                A.check_cases(rest, content)
        with self.assertRaisesRegex(C.Refusal, "invalid evidence timestamp"):
            A.timestamp("2025-99-99T25:99:99Z")

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
        fallback = schema['$defs']['binding']['allOf'][0]['else']['else']
        self.assertEqual(209, fallback['else']['properties']['inputs']['maxItems'])
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
                         'https://' + secrets.token_hex(8) + ':' + secrets.token_hex(32) + '@fixture.blob.core.windows.net/archive'):
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

    def assert_workflow_inputs_are_environment_bound(self, content):
        bodies = workflow_run_bodies(content)
        self.assertTrue(bodies)
        for body in bodies:
            self.assertNotIn('${{ inputs.', body)
            self.assertNotRegex(body, r'\$\{\{\s*inputs\s*\.')

    def test_workflow_run_body_guard_rejects_inline_inputs_in_later_body(self):
        content = (ROOT / '.github/workflows/phase-4-high-risk-nfr-story-done.yml').read_text()
        self.assert_workflow_inputs_are_environment_bound(content)
        blocks = list(re.finditer(r'(?m)^        run: \|\n', content))
        self.assertGreaterEqual(len(blocks), 2)
        insert = blocks[-1].end()
        changed = content[:insert] + '          echo "${{ inputs.mode }}"\n' + content[insert:]
        with self.assertRaises(AssertionError):
            self.assert_workflow_inputs_are_environment_bound(changed)
        with self.assertRaises(AssertionError):
            self.assert_workflow_inputs_are_environment_bound('steps:\n  - name: unsafe\n    run: echo "${{ inputs.mode }}"\n')
        safe_steps = 'steps:\n  - run: echo safe\n  - run: |\n      echo safe\n    env:\n      MODE: ${{ inputs.mode }}\n'
        self.assert_workflow_inputs_are_environment_bound(safe_steps)
        for changed in (safe_steps.replace('echo safe', 'echo "${{ inputs.mode }}"', 1),
                        safe_steps.replace('      echo safe', '      echo "${{ inputs.mode }}"')):
            with self.assertRaises(AssertionError):
                self.assert_workflow_inputs_are_environment_bound(changed)

    def test_workflow_run_body_guard_retains_commented_headers_and_compact_expressions(self):
        for step in ('    run:', '  - run:'):
            for header in ('|  ', '| # note', '|- # note'):
                for expression in ('${{inputs.mode}}', '${{ inputs.mode}}', '${{inputs.mode }}', '${{\tinputs.mode\t}}'):
                    safe = 'steps:\n' + step + ' ' + header + '\n      echo safe\n    env:\n      MODE: ' + expression + '\n'
                    self.assert_workflow_inputs_are_environment_bound(safe)
                    with self.assertRaises(AssertionError):
                        self.assert_workflow_inputs_are_environment_bound(safe.replace('echo safe', 'echo "' + expression + '"'))
                    single = 'steps:\n' + step + ' echo "' + expression + '"\n'
                    with self.assertRaises(AssertionError):
                        self.assert_workflow_inputs_are_environment_bound(single)

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
            self.assert_workflow_inputs_are_environment_bound(content)
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

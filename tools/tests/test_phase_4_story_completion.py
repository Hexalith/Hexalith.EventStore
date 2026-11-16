"""Actual completion writers and dedicated workflow with HTTP-only substitution."""

import copy
import contextlib
import io
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
import textwrap
import tomllib
import unittest
from unittest import mock

import phase_4_transport_fixture as HTTP
import test_phase_4_transition as T
import phase_4_story_completion as S
import phase_4_transition as C

CONSUMER = 'tools/validate-phase-4-story-completion.py'
WORKFLOW = '.github/workflows/phase-4-high-risk-nfr-story-done.yml'
# The original boundary fixtures deliberately use an unsupported spec identity.
# Supported spec-5-3-* identities and genuine business execution are covered by
# test_phase_4_story_5_3.py; HTTP fixtures never manufacture business success.
SPEC = '_bmad-output/implementation-artifacts/5-3-completion-fixture.md'


class StoryCompletionTests(unittest.TestCase):
    def setUp(self):
        self.f = T.TransitionTests(methodName='runTest')
        self.f.setUp()
        self.addCleanup(self.f.doCleanups)
        f = self.f
        for path in (*C.STORY_CONTROL_FILES, S.TRACKER):
            destination = f.root / path
            destination.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(T.ROOT / path, destination)
            f.blob(T.HEAD, path, destination.read_bytes())
        # A private checkout with read-only access to the real committed planning
        # authority. No fixture commits, branches or writes to the shared Git dir.
        directory = subprocess.check_output(['git', '-C', str(T.ROOT), 'rev-parse', '--absolute-git-dir'], text=True).strip()
        (f.root / '.git').write_text('gitdir: ' + directory + '\n')
        self.key = next(key for key in S.tracker_rows((f.root / S.TRACKER).read_bytes()) if key.startswith('5-3-'))
        self.select('5.3')

    def select(self, story_id, target='spec', path=None):
        f = self.f
        self.key = next(key for key in S.tracker_rows((f.root / S.TRACKER).read_bytes())
                        if key.startswith(story_id.replace('.', '-') + '-'))
        self.spec = path or SPEC.replace('5-3', story_id.replace('.', '-'))
        (f.root / self.spec).write_text("---\nstatus: 'in-progress'\nstory_key: '" + self.key + "'\n---\n\nFixture slice.\n")
        tracker = (f.root / S.TRACKER).read_text()
        tracker = re.sub(r'(?m)^(  ' + re.escape(self.key) + r': )[a-z-]+', r'\g<1>in-progress', tracker)
        (f.root / S.TRACKER).write_text(tracker)
        f.request.update(transition='high-risk-nfr-story-done', gate=None, subject_mode='files', evidence=[], predecessors=[])
        f.request['subjects'] = []
        for path in (S.PRD, S.EPICS, S.TRACKER, self.spec):
            content = (f.root / path).read_bytes()
            f.blob(T.HEAD, path, content)
            f.blob(T.BEFORE, path, content if path != self.spec else content + b'\nOld subject.\n')
            f.request['subjects'].append(f.file(path))
        reader = C.Control.__new__(C.Control)
        reader.root = f.root
        f.request['story'] = S.derive(reader.local, self.spec, target)
        f.request['nfr_ids'] = f.request['story']['nfr_ids']
        f.approve(roles=['product-owner', 'test-owner'])
        f.api_document('/actions/runs/7/attempts/2')['path'] = WORKFLOW
        f.api_document('/actions/workflows/12')['path'] = WORKFLOW

    def environment(self, **extra):
        f = self.f
        f.state_path.write_text(json.dumps(f.state))
        environment = {key: value for key, value in os.environ.items() if not key.startswith(('GH_', 'GITHUB_', 'PYTHON'))}
        environment.update(PYTHONPATH=str(f.site) + os.pathsep + str(T.ROOT / 'tools/tests'),
                           PHASE4_FIXTURE_STATE=str(f.state_path), GH_TOKEN=f.fixture_token,
                           PHASE4_REQUEST_JSON=T.canonical(f.request).decode())
        environment.update(extra)
        return environment

    def cli(self, target='spec', authenticated=False, write=True, extra=(), **environment):
        f = self.f
        argv = [sys.executable, '-B', str(f.root / CONSUMER), '--spec', self.spec, '--target', target,
                '--result', str(f.result_path)]
        if authenticated:
            argv += ['--request-env', '--run-id', '7', '--attempt', '2']
        if write:
            argv += ['--write']
        process = subprocess.run([*argv, *extra], cwd=f.root, env=self.environment(**environment),
                                 text=True, capture_output=True, timeout=45)
        f.state = json.loads(f.state_path.read_text())
        self.assertNotIn('Traceback', process.stderr)
        document = json.loads(process.stdout)
        schema = json.loads((T.ROOT / 'tools/phase-4-transition.schema.json').read_text())
        # Select the emitted ordinary/refused report through the advertised root
        # union, rather than accepting an unreachable $defs entry. Discriminators
        # are distinct constants and each selected report has closed fields.
        variants = [schema['$defs'][entry['$ref'].split('/')[-1]] for entry in schema['oneOf']]
        matching = [variant for variant in variants
                    if variant['properties']['schema']['const'] == document['schema']]
        self.assertEqual(1, len(matching), 'completion report must match exactly one root contract')
        self.assertFalse(matching[0]['additionalProperties'])
        self.assertEqual(set(matching[0]['required']), set(document))
        self.assertTrue(set(document['codes']) <= set(C.CODES))
        return process, document

    def refused(self, code, **kwargs):
        original = {(path): (self.f.root / path).read_bytes() for path in (self.spec, S.TRACKER)}
        process, document = self.cli(**kwargs)
        self.assertNotEqual(0, process.returncode, process.stderr)
        self.assertEqual('FAIL', document['result'])
        self.assertEqual([code], document['codes'], process.stderr)
        self.assertIsNone(document['verification'])
        for path, content in original.items():
            self.assertEqual(content, (self.f.root / path).read_bytes())
        return document

    def evaluation(self, expected='ADAPTER_UNAVAILABLE'):
        process, document = self.f.run_cli(expected='high-risk-nfr-story-done')
        self.assertEqual(1, process.returncode, process.stderr)
        self.assertEqual([expected], document['codes'], process.stderr)
        return document

    def sealed_fixture(self):
        # A synthetic candidate cannot manufacture the missing NFR proof: the
        # consumer must rerun evaluation and reject it. Never claim positive live
        # completion; the genuine positive controls remain the OQ8 tests.
        candidate = self.evaluation()
        candidate.update(result='PASS', codes=[])
        self.f.artifact(candidate=candidate)
        return candidate

    def test_primary_supporting_ranges_and_supplemental_coverage_are_derived(self):
        for story_id, guarded in [('5.3', ['NFR3', 'NFR4']), ('1.2', ['NFR16']),
                                  ('3.17', ['NFR12', 'NFR17']), ('8.1', sorted(C.HIGH_RISK_NFRS - {'NFR14', 'NFR15'}, key=lambda n: int(n[3:]))),
                                  ('9.2', [])]:
            with self.subTest(story_id=story_id):
                self.select(story_id)
                self.assertEqual(guarded, self.f.request['story']['high_risk_nfr_ids'])
        self.assertIn('NFR19', S.coverage((self.f.root / S.PRD).read_bytes(), (self.f.root / S.EPICS).read_bytes(), '8.1')['nfr_ids'])

    def test_both_writers_refuse_governed_completion_without_adapter(self):
        for target in ('spec', 'tracker'):
            self.select('5.3', target)
            document = self.refused('ADAPTER_UNAVAILABLE', target=target)
            self.assertIn('NFR3', document['message'])
            self.assertIn('NFR4', document['message'])
            self.assertIn('adapter unavailable', document['message'])
            self.assertEqual([], self.f.state['observed'])

    def test_valid_authenticated_approvals_still_require_story_owned_adapter(self):
        document = self.evaluation()
        self.assertEqual(T.SINGLE, document['achieved_level'])
        self.assertEqual(2, len(document['approvals']))
        self.assertEqual(self.f.request['story'], document['binding']['story'])
        self.assertTrue(set(C.STORY_CONTROL_FILES) <= {item['path'] for item in document['binding']['inputs']})
        self.sealed_fixture()
        self.refused('ADAPTER_UNAVAILABLE', authenticated=True)
        urls = [row['url'] for row in self.f.state['observed']]
        self.assertIn(T.API + '/actions/runs/7/attempts/2', urls)
        self.assertIn(T.API + '/issues/comments/100', urls)

    def test_omitted_nfr_ids_cannot_downgrade_coverage(self):
        del self.f.request['nfr_ids']
        self.f.approve(roles=['product-owner', 'test-owner'])
        document = self.evaluation()
        self.assertEqual(['NFR3', 'NFR4'], document['binding']['story']['high_risk_nfr_ids'])
        self.f.request['nfr_ids'] = []
        self.f.approve(roles=['product-owner', 'test-owner'])
        self.evaluation('COVERAGE_MISMATCH')

    def test_genuine_oq8_predecessor_cannot_substitute_for_story_nfr_evidence(self):
        original = T.TransitionTests(methodName='runTest')
        original.setUp()
        self.addCleanup(original.doCleanups)
        self.f.request['predecessors'] = original.request['predecessors']
        bound = {item['path'] for item in self.f.request['subjects']}
        self.f.request['evidence'] = [item for item in (*original.request['subjects'], *original.request['evidence'])
                                      if item['path'] not in bound]
        self.f.approve(roles=['product-owner', 'test-owner'])
        result = self.evaluation()
        self.assertEqual(1, len(result['predecessors']))
        self.assertEqual('none', result['downstream_minimum'])

    def test_missing_or_forged_claim_identity_and_coverage_refuse(self):
        original = copy.deepcopy(self.f.request)
        del self.f.request['story']
        self.evaluation('STORY_IDENTITY_MISSING')
        self.f.request = copy.deepcopy(original)
        for field, value in [('nfr_ids', []), ('high_risk_nfr_ids', []), ('coverage_sha256', '0' * 64),
                             ('story_id', '9.2'), ('story_key', '9-2-phase-4-high-risk-gate-matrix-and-assurance-control')]:
            self.f.request = copy.deepcopy(original)
            self.f.request['story'][field] = value
            with self.subTest(field=field):
                self.evaluation('COVERAGE_MISMATCH')

    def test_missing_or_conflicting_mapping_is_never_ordinary(self):
        path = self.f.root / S.EPICS
        original = path.read_bytes()
        path.write_bytes(original.replace(b'### Story 5.3:', b'### Removed 5.3:', 1))
        self.refused('STORY_IDENTITY_MISSING')
        path.write_bytes(original)
        content = path.read_text()
        start = content.index('### Story 5.3:')
        index = content.index('**Requirements coverage:**', start)
        end = content.index('\n', index)
        path.write_text(content[:index] + '**Requirements coverage:** Primary FR26; no NFR ownership.' + content[end:])
        self.refused('COVERAGE_MISMATCH')
        path.write_bytes(original)
        tracker = self.f.root / S.TRACKER
        tracker.write_text(tracker.read_text().replace('development_status:\n', 'development_status:\n  5-3-forged-alias: in-progress\n', 1))
        self.refused('STORY_IDENTITY_MISSING')

    def test_numeric_filename_cannot_relabel_high_risk_claim_to_ordinary_story(self):
        unguarded = next(key for key in S.tracker_rows((self.f.root / S.TRACKER).read_bytes()) if key.startswith('9-2-'))
        path = self.f.root / self.spec
        path.write_text(path.read_text().replace(self.key, unguarded))
        self.refused('STORY_IDENTITY_MISSING')

    def test_persisted_identity_cannot_be_relabelled_even_for_generic_filename(self):
        self.select('5.3', path='_bmad-output/implementation-artifacts/spec-generic-fixture.md')
        original = (self.f.root / self.spec).read_bytes()
        unguarded = next(key for key in S.tracker_rows((self.f.root / S.TRACKER).read_bytes()) if key.startswith('9-2-'))
        (self.f.root / self.spec).write_bytes(original.replace(self.key.encode(), unguarded.encode()))
        def history(root, *args, absent=False):
            if args == ('show', 'HEAD:' + self.spec):
                return original
            return (root / args[1][5:]).read_bytes()
        with mock.patch.object(S, 'git', side_effect=history):
            with self.assertRaises(C.Refusal) as caught:
                S.complete(self.f.root, self.spec, 'spec', write=True)
        self.assertEqual('STORY_IDENTITY_MISSING', caught.exception.code)

    def test_new_generic_copy_cannot_relabel_a_governed_spec_as_ordinary(self):
        governed = (self.f.root / self.spec).read_bytes()
        ordinary = next(key for key in S.tracker_rows((self.f.root / S.TRACKER).read_bytes()) if key.startswith('9-2-'))
        self.spec = '_bmad-output/implementation-artifacts/spec-copied-generic-fixture.md'
        (self.f.root / self.spec).write_bytes(governed.replace(self.key.encode(), ordinary.encode()))
        self.refused('STORY_IDENTITY_MISSING')

    def test_established_committed_generic_identity_still_supports_ordinary_completion(self):
        self.select('9.2', path='_bmad-output/implementation-artifacts/spec-established-generic-fixture.md')
        original = (self.f.root / self.spec).read_bytes()
        def history(root, *args, absent=False):
            return original if args == ('show', 'HEAD:' + self.spec) else (root / args[1][5:]).read_bytes()
        with mock.patch.object(S, 'git', side_effect=history):
            result = S.complete(self.f.root, self.spec, 'spec', write=True)
        self.assertEqual('ordinary-slice', result['disposition'])
        self.assertEqual('done', S.frontmatter((self.f.root / self.spec).read_bytes())['status'])

    def test_alternate_duplicate_keys_and_alias_structures_refuse(self):
        self.select('9.2')
        path = self.f.root / self.spec
        original = path.read_text()
        governed = '5-3-production-authentication-guards-and-secret-stripping'
        for field in (f'"story_key": "{governed}"', f"'story_key': '{governed}'",
                      f'"story\\u005fkey": "{governed}"', f'story_key : {governed}',
                      '"status": done', "'status': done", 'status : done',
                      '? story_key\n: ' + governed, '<<: *identity',
                      '  story_key: ' + governed):
            with self.subTest(field=field):
                path.write_text(original.replace('\n---\n', '\n' + field + '\n---\n'))
                self.refused('STORY_IDENTITY_MISSING')
        for scalar in ('*identity', '&identity in-progress', '!!str in-progress', '|\n  in-progress'):
            with self.subTest(scalar=scalar):
                path.write_text(original.replace("'in-progress'", scalar))
                self.refused('STORY_IDENTITY_MISSING')

    def test_template_scalar_comments_are_supported_and_preserved_on_write(self):
        for scalar in ("'in-progress'", '"in-progress"', 'in-progress'):
            with self.subTest(scalar=scalar):
                self.select('1.7')
                path = self.f.root / self.spec
                path.write_text("---\ntype: 'feature' # feature | bugfix\n"
                                + 'story_key: "' + self.key + '" # canonical story\n'
                                + 'status : ' + scalar + ' # draft | in-progress | done\n---\n\nSlice.\n')
                process, document = self.cli()
                self.assertEqual(0, process.returncode, process.stderr)
                self.assertEqual('ordinary-slice', document['disposition'])
                self.assertIn("status : 'done' # draft | in-progress | done\n", path.read_text())
                self.assertEqual(self.key, S.frontmatter(path.read_bytes())['story_key'])

    def test_real_2_8_and_3_16_controls_use_owner_cells_and_both_declaration_formats(self):
        for story_id, expected in [('2.8', ['NFR1', 'NFR6', 'NFR12', 'NFR15', 'NFR16']),
                                   ('2.13', ['NFR5', 'NFR12', 'NFR15', 'NFR16']),
                                   ('3.16', ['NFR9', 'NFR11', 'NFR12'])]:
            with self.subTest(story_id=story_id):
                self.select(story_id)
                self.assertEqual(expected, self.f.request['story']['nfr_ids'])
                result = self.evaluation()
                self.assertEqual(expected, result['binding']['story']['nfr_ids'])

    def test_missing_approval_seal_and_time_separation_refuse(self):
        original = copy.deepcopy(self.f.request)
        self.f.request['approvals'] = []
        self.evaluation('APPROVAL_MISSING')
        self.f.request = original
        self.f.approve(created='2026-01-01T23:59:59Z', roles=['product-owner', 'test-owner'])
        self.evaluation('APPROVAL_WINDOW')
        self.f.approve(roles=['product-owner', 'test-owner'])
        self.refused('API_UNAVAILABLE', authenticated=True)
        self.f.api_document('/actions/runs/7/attempts/2')['conclusion'] = 'failure'
        self.refused('RUN_NOT_SUCCESSFUL', authenticated=True)

    def test_current_source_control_workflow_and_claim_drift_are_rechecked(self):
        self.sealed_fixture()
        paths = [*C.STORY_CONTROL_FILES, WORKFLOW, S.PRD, S.EPICS, S.TRACKER, self.spec]
        for path in paths:
            old = (self.f.root / path).read_bytes()
            self.f.blob(T.HEAD, path, old + b'\n')
            with self.subTest(path=path):
                self.refused('SOURCE_MISMATCH', authenticated=True)
            self.f.blob(T.HEAD, path, old)
        self.f.request['story']['target'] = 'tracker'
        self.refused('COVERAGE_MISMATCH', authenticated=True)

    def test_saved_passing_report_is_not_reusable_authority(self):
        self.sealed_fixture()
        self.f.result_path.write_text('{"result":"PASS","seal":{"run_id":7}}')
        self.refused('ADAPTER_UNAVAILABLE', authenticated=True)
        self.f.state['responses'][T.API + '/actions/runs/7/attempts/2'] = {'error': 404}
        self.refused('API_UNAVAILABLE', authenticated=True)
        self.assertEqual('FAIL', json.loads(self.f.result_path.read_text())['result'])

    def test_ordinary_slice_writes_do_not_close_parent_or_whole_nfr(self):
        self.select('9.2')
        tracker = (self.f.root / S.TRACKER).read_bytes()
        process, document = self.cli()
        self.assertEqual(0, process.returncode, process.stderr)
        self.assertEqual('ordinary-slice', document['disposition'])
        self.assertEqual('done', S.frontmatter((self.f.root / self.spec).read_bytes())['status'])
        self.assertEqual(tracker, (self.f.root / S.TRACKER).read_bytes())
        self.assertIn('no parent-story or whole-NFR closure', document['message'])
        self.refused('BUSINESS_PREREQUISITE_MISSING', target='tracker')
        self.select('1.7', 'tracker')
        process, document = self.cli(target='tracker')
        self.assertEqual(0, process.returncode, process.stderr)
        self.assertEqual('done', S.tracker_rows((self.f.root / S.TRACKER).read_bytes())[self.key])

    def test_actual_cli_ordinary_validation_without_write_preserves_both_targets(self):
        for target in ('spec', 'tracker'):
            with self.subTest(target=target):
                self.select('1.7', target)
                originals = {path: (self.f.root / path).read_bytes() for path in (self.spec, S.TRACKER)}
                process, document = self.cli(target=target, write=False)
                self.assertEqual(0, process.returncode, process.stderr)
                self.assertEqual('ordinary-slice', document['disposition'])
                for path, content in originals.items():
                    self.assertEqual(content, (self.f.root / path).read_bytes())

    def test_successful_crlf_write_preserves_the_last_status_field_terminator(self):
        self.select('1.7')
        path = self.f.root / self.spec
        original = ("---\r\ntitle: 'CRLF slice'\r\nstory_key: '" + self.key
                    + "'\r\nstatus: 'in-progress'\r\n---\r\n\r\nBody.\r\n").encode()
        path.write_bytes(original)
        process, document = self.cli()
        self.assertEqual(0, process.returncode, process.stderr)
        self.assertEqual('ordinary-slice', document['disposition'])
        self.assertEqual(original.replace(b"'in-progress'", b"'done'"), path.read_bytes())

    def test_concurrent_input_edit_during_temporary_preparation_is_preserved(self):
        real_temporary = S.tempfile.NamedTemporaryFile
        for target in ('spec', 'tracker'):
            for changed in ('target', S.PRD):
                with self.subTest(target=target, changed=changed):
                    self.select('1.7', target)
                    destination = self.spec if target == 'spec' else S.TRACKER
                    source = destination if changed == 'target' else changed
                    original = (self.f.root / source).read_bytes()
                    concurrent = original + b'\n# Concurrent edit must survive.\n'
                    target_original = (self.f.root / destination).read_bytes()
                    def prepare(*args, **kwargs):
                        stream = real_temporary(*args, **kwargs)
                        (self.f.root / source).write_bytes(concurrent)
                        return stream
                    try:
                        with mock.patch.object(S.tempfile, 'NamedTemporaryFile', side_effect=prepare):
                            with self.assertRaises(C.Refusal) as caught:
                                S.complete(self.f.root, self.spec, target, write=True)
                        self.assertEqual('SOURCE_MISMATCH', caught.exception.code)
                        self.assertEqual(concurrent, (self.f.root / source).read_bytes())
                        if source != destination:
                            self.assertEqual(target_original, (self.f.root / destination).read_bytes())
                        self.assertEqual([], list((self.f.root / destination).parent.glob('.story-completion-*')))
                    finally:
                        (self.f.root / source).write_bytes(original)

    def test_changed_planning_authority_cannot_downgrade_to_ordinary(self):
        self.select('1.7')
        path = self.f.root / S.PRD
        path.write_bytes(path.read_bytes() + b'\nChanged authority.\n')
        self.refused('SOURCE_MISMATCH')

    def test_historical_noop_preserves_label_without_authority_but_request_revalidates(self):
        self.sealed_fixture()
        path = self.f.root / self.spec
        path.write_text(path.read_text().replace("status: 'in-progress'", "status: 'done'"))
        with mock.patch.object(S, 'git', side_effect=lambda root, *args, **kw: (root / args[1][5:]).read_bytes()):
            result = S.complete(self.f.root, self.spec, 'spec', write=True)
            self.assertEqual('historical-unchanged', result['disposition'])
            self.assertIsNone(result['verification'])
            with mock.patch.dict(os.environ, self.environment()), mock.patch('urllib.request.build_opener', return_value=HTTP.Transport()):
                with self.assertRaises(C.Refusal):
                    S.complete(self.f.root, self.spec, 'spec', self.f.request, 7, 2)

    def test_unchanged_historical_spec_without_story_identity_remains_historical(self):
        path = self.f.root / self.spec
        path.write_text("---\nstatus: 'done'\n---\n\nHistorical unbound spec.\n")
        original = path.read_bytes()
        with mock.patch.object(S, 'git', return_value=original):
            result = S.complete(self.f.root, self.spec, 'spec', write=True)
        self.assertEqual('historical-unchanged', result['disposition'])
        self.assertIsNone(result['claim'])
        self.assertEqual(original, path.read_bytes())

    def test_unchanged_committed_governed_tracker_noop_is_historical_only(self):
        self.select('5.3', 'tracker')
        path = self.f.root / self.spec
        path.write_text(path.read_text().replace("status: 'in-progress'", "status: 'done'"))
        tracker = self.f.root / S.TRACKER
        tracker.write_text(tracker.read_text().replace('  ' + self.key + ': in-progress', '  ' + self.key + ': done'))
        snapshots = {path: (self.f.root / path).read_bytes() for path in (self.spec, S.TRACKER)}
        def history(root, *args, absent=False):
            return snapshots[args[1][5:]]
        output = io.StringIO()
        with mock.patch.object(S, 'git', side_effect=history), contextlib.redirect_stdout(output):
            code = S.main(['--root', str(self.f.root), '--spec', self.spec, '--target', 'tracker', '--write'])
        document = json.loads(output.getvalue())
        self.assertEqual(0, code)
        self.assertEqual('historical-unchanged', document['disposition'])
        self.assertIsNone(document['claim'])
        self.assertIsNone(document['verification'])
        for path, content in snapshots.items():
            self.assertEqual(content, (self.f.root / path).read_bytes())

    def test_diagnostic_path_cannot_overwrite_completion_sources(self):
        for path in (self.spec, S.TRACKER, S.PRD, 'tools/phase_4_transition.py'):
            original = (self.f.root / path).read_bytes()
            self.refused('INPUT_INVALID', extra=['--result', str(self.f.root / path)])
            self.assertEqual(original, (self.f.root / path).read_bytes())
        self.refused('INPUT_INVALID', extra=['--unknown', '--result', str(self.f.root / self.spec)])

    def test_invalid_diagnostic_destinations_refuse_before_writing_ordinary_labels(self):
        directory = self.f.base / 'result-directory'
        directory.mkdir()
        external = self.f.base / 'external-input.json'
        external.write_bytes(b'External input must survive.\n')
        symbolic = self.f.base / 'symbolic-result.json'
        symbolic.symlink_to(external)
        hardlink = self.f.base / 'hardlinked-result.json'
        os.link(external, hardlink)
        for target in ('spec', 'tracker'):
            self.select('1.7', target)
            for destination in (directory, symbolic):
                with self.subTest(target=target, destination=destination):
                    self.refused('INPUT_INVALID', target=target, extra=['--result', str(destination)])
            for destination in (external, hardlink):
                for malformed in (False, True):
                    with self.subTest(target=target, destination=destination, malformed=malformed):
                        arguments = ['--request', str(external), '--result', str(destination)]
                        if malformed:
                            arguments.append('--unknown')
                        self.refused('INPUT_INVALID', target=target, extra=arguments)
                        self.assertEqual(b'External input must survive.\n', external.read_bytes())
                        self.assertEqual(b'External input must survive.\n', hardlink.read_bytes())
        self.assertTrue(symbolic.is_symlink())

    def test_diagnostic_temporary_preparation_failure_preserves_lifecycle_bytes(self):
        self.select('1.7')
        snapshots = {path: (self.f.root / path).read_bytes() for path in (self.spec, S.TRACKER)}
        output, errors = io.StringIO(), io.StringIO()
        with mock.patch.object(S.tempfile, 'NamedTemporaryFile', side_effect=OSError('fixture unavailable output')):
            with contextlib.redirect_stdout(output), contextlib.redirect_stderr(errors):
                code = S.main(['--root', str(self.f.root), '--spec', self.spec, '--target', 'spec', '--write',
                               '--result', str(self.f.result_path)])
        self.assertEqual(2, code)
        self.assertEqual(['INPUT_INVALID'], json.loads(output.getvalue())['codes'])
        for path, content in snapshots.items():
            self.assertEqual(content, (self.f.root / path).read_bytes())

    def test_missing_coordinates_and_symbolic_inputs_return_structured_refusal(self):
        self.refused('SEAL_MISSING', extra=['--request-env'])
        self.refused('INPUT_INVALID', extra=['--run-id', '7'])
        path = self.f.root / self.spec
        destination = self.f.base / 'linked-spec.md'
        path.rename(destination)
        path.symlink_to(destination)
        process, document = self.cli()
        self.assertNotEqual(0, process.returncode)
        self.assertEqual(['INPUT_INVALID'], document['codes'])
        self.assertIn("status: 'in-progress'", destination.read_text())

    def test_persistent_customization_covers_both_actual_bmad_writers(self):
        content = tomllib.loads((T.ROOT / S.CUSTOMIZATION).read_text())
        facts = '\n'.join(content['workflow']['persistent_facts'])
        self.assertNotIn('on_complete', content['workflow'])
        for writer in ('step-05-present', 'step-oneshot'):
            self.assertIn(writer, facts)
            source = (T.ROOT / '.agents/skills/bmad-build' / (writer + '.md')).read_text()
            self.assertIn('done', source)
            self.assertIn('review', source)
        self.assertIn('--target spec --write', facts)
        self.assertIn('--target tracker --write', facts)
        self.assertIn('review', facts)
        process = subprocess.run([sys.executable, '-B', str(T.ROOT / '_bmad/scripts/resolve_customization.py'),
                                  '--project-root', str(T.ROOT), '--skill', str(T.ROOT / '.agents/skills/bmad-build'),
                                  '--key', 'workflow.persistent_facts'], text=True, capture_output=True, timeout=30)
        self.assertEqual(0, process.returncode, process.stderr)
        self.assertIn(facts, json.loads(process.stdout)['workflow.persistent_facts'])

    def test_dedicated_workflow_evaluate_and_consume_use_the_real_boundary(self):
        content = (self.f.root / WORKFLOW).read_text()
        step = content.split('      - name: Verify this guarded attempt\n', 1)[1].split('      - name:', 1)[0]
        script = textwrap.dedent(step.split('        run: |\n', 1)[1])
        self.assertIn(CONSUMER, script)
        self.assertNotIn('--write', script)
        bindings = dict(re.findall(r'^          ([A-Z0-9_]+): (.+)$', step, re.M))
        self.assertEqual({'GH_TOKEN': '${{ github.token }}', 'MODE': '${{ inputs.mode }}',
                          'PHASE4_REQUEST_JSON': '${{ inputs.request }}',
                          'SEALED_RUN_ID': '${{ inputs.sealed_run_id }}',
                          'SEALED_ATTEMPT': '${{ inputs.sealed_attempt }}'}, bindings)
        runner = self.f.base / 'runner'
        runner.mkdir()
        self.sealed_fixture()
        for mode in ('evaluate', 'consume'):
            process = subprocess.run(['bash', '-e', '-o', 'pipefail', '-c', script], cwd=self.f.root,
                                     env=self.environment(MODE=mode, RUNNER_TEMP=str(runner),
                                                          SEALED_RUN_ID='7', SEALED_ATTEMPT='2'),
                                     text=True, capture_output=True, timeout=45)
            self.f.state = json.loads(self.f.state_path.read_text())
            document = json.loads((runner / 'phase-4-transition/result.json').read_text())
            self.assertEqual(1, process.returncode, (runner / 'phase-4-transition/report.txt').read_text())
            self.assertEqual(['ADAPTER_UNAVAILABLE'], document['codes'])
        fixtures = (T.ROOT / '.github/workflows/phase-4-transition-fixtures.yml').read_text()
        self.assertIn("-p 'test_phase_4_story_completion.py'", fixtures)
        self.assertNotRegex(content, r'(?m)^  (push|pull_request):')

    def test_story_binding_inventory_supports_215_inputs_without_weakening_ordinary_limit(self):
        for key in ('subjects', 'evidence'):
            while len(self.f.request[key]) < 100:
                path = f'boundary/{key}-{len(self.f.request[key])}.txt'
                destination = self.f.root / path
                destination.parent.mkdir(parents=True, exist_ok=True)
                destination.write_bytes(b'bound input\n')
                self.f.blob(T.HEAD, path, destination.read_bytes())
                self.f.blob(T.BEFORE, path, destination.read_bytes())
                self.f.request[key].append(self.f.file(path))
        self.f.request['approvals'] = []
        result = self.evaluation('APPROVAL_MISSING')
        self.assertEqual(215, len(result['binding']['inputs']))
        schema = json.loads((T.ROOT / 'tools/phase-4-transition.schema.json').read_text())
        binding = schema['$defs']['binding']
        self.assertEqual(260, binding['properties']['inputs']['maxItems'])
        self.assertEqual(215, binding['allOf'][0]['then']['properties']['inputs']['maxItems'])
        self.assertEqual(209, binding['allOf'][0]['else']['else']['properties']['inputs']['maxItems'])
        self.assertEqual(260, binding['allOf'][0]['else']['then']['properties']['inputs']['maxItems'])


if __name__ == '__main__':
    unittest.main()

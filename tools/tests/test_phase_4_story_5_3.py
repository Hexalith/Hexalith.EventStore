"""Fixed adapter contracts and real CLI refusals; no synthetic business PASS.

Platform HTTP facts are fixtures. Business positive controls are actual .NET
execution recorded by the standalone validator; the current complete claim
cannot pass while its executable scanner capabilities remain incomplete.
"""

import copy
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest
from unittest import mock
import zlib
import zipfile
import base64

import test_phase_4_transition as T
import test_phase_4_story_completion as W
import phase_4_story_5_3 as A
import phase_4_transition as C


class AdapterContracts(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory()
        self.addCleanup(self.directory.cleanup)
        self.root = Path(self.directory.name)

    def claim(self):
        return {'story_key': A.STORY_IDENTITY, 'story_id': '5.3', 'spec_path': '_bmad-output/implementation-artifacts/spec-5-3-new-slice.md',
                'target': 'spec', 'scope': 'slice', 'nfr_ids': ['NFR3', 'NFR4'], 'high_risk_nfr_ids': ['NFR3', 'NFR4'],
                'coverage_sha256': '0' * 64}

    def test_exact_supported_coverage_and_spec_identity_only(self):
        claim = self.claim()
        self.assertTrue(A.supported(claim))
        claim['target'] = 'tracker'
        self.assertTrue(A.supported(claim))
        for key, value in [('story_key', '5-3-an-alias'), ('story_id', '5.11'), ('nfr_ids', ['NFR3']),
                           ('nfr_ids', ['NFR3', 'NFR4', 'NFR5']), ('high_risk_nfr_ids', ['NFR4']),
                           ('spec_path', '_bmad-output/implementation-artifacts/spec-unbound.md')]:
            changed = {**claim, key: value}
            with self.subTest(key=key, value=value):
                self.assertFalse(A.supported(changed))

    def file(self, path, value=b'bound source\n', mode='100644'):
        destination = self.root / path
        destination.parent.mkdir(parents=True, exist_ok=True)
        destination.write_bytes(value)
        return {'path': path, 'mode': mode, 'git_sha': A.blob_sha(value)}

    def git_tree(self, root, entries):
        nested = {}
        for row in entries:
            node = nested
            parts = row['path'].split('/')
            for name in parts[:-1]:
                node = node.setdefault(name, {})
            node[parts[-1]] = row
            if row['mode'] != '160000':
                A.store_object(root, 'blob', (root / row['path']).read_bytes())
        def write(node):
            value = b''
            for name, item in sorted(node.items(), key=lambda pair: pair[0] + ('/' if 'git_sha' not in pair[1] else '')):
                tree = 'git_sha' not in item
                oid = write(item) if tree else item['git_sha']
                value += ('40000' if tree else item['mode']).encode() + b' ' + name.encode() + b'\0' + bytes.fromhex(oid)
            return A.store_object(root, 'tree', value)
        return write(nested)

    def git_commit(self, root, tree):
        return A.store_object(root, 'commit', ('tree ' + tree + '\nauthor Fixture <fixture@example.test> 0 +0000\ncommitter Fixture <fixture@example.test> 0 +0000\n\nFixture object only.\n').encode())

    def test_authentic_global_json_crlf_and_private_bound_attributes(self):
        canonical = A.git(T.ROOT, 'show', 'HEAD:global.json')
        oid = A.blob_sha(canonical)
        attributes = (T.ROOT / '.gitattributes').read_bytes() + b'global.json filter=must-not-run\n'
        attr = self.file('.gitattributes', attributes)
        global_file = self.file('global.json', canonical.replace(b'\n', b'\r\n'))
        global_file['git_sha'] = oid
        A.write_index(self.root, [attr, global_file])
        A.store_object(self.root, 'blob', attributes)
        (self.root / '.git/info').mkdir()
        (self.root / '.git/info/attributes').write_text('global.json -text\n')
        marker = self.root / 'filter-ran'
        with (self.root / '.git/config').open('a') as stream:
            stream.write('[filter "must-not-run"]\nclean = touch ' + str(marker) + '\n')
        result = A.bind_entries(self.root, [attr, global_file])
        self.assertEqual(oid, result[1]['git_sha'])
        self.assertEqual(C.digest((self.root / 'global.json').read_bytes()), result[1]['sha256'])
        self.assertFalse(marker.exists())
        # A historical CRLF blob with text=auto already authenticates as-is.
        old = self.file('old.txt', b'legacy\r\n')
        attr = self.file('.gitattributes', b'* text=auto\n')
        A.store_object(self.root, 'blob', (self.root / '.gitattributes').read_bytes())
        self.assertEqual(old['git_sha'], A.bind_entries(self.root, [attr, old])[1]['git_sha'])

    def test_replacement_objects_cannot_rewrite_required_commit_or_tree(self):
        first = self.file('first.txt')
        A.write_index(self.root, [first])
        tree = self.git_tree(self.root, [first])
        original = self.git_commit(self.root, tree)
        second = self.file('second.txt')
        replaced = self.git_commit(self.root, self.git_tree(self.root, [second]))
        directory = self.root / '.git/refs/replace'
        directory.mkdir()
        (directory / original).write_text(replaced + '\n')
        ordinary = subprocess.check_output(['git', '-C', str(self.root), 'ls-tree', '--name-only', original]).decode()
        self.assertIn('second.txt', ordinary)
        self.assertIn(b'first.txt', A.git(self.root, 'ls-tree', '--name-only', original))
        self.assertEqual(original, A.object_sha('commit', A.verified_object(self.root, 'commit', original)))
        self.assertEqual(b'bound source\n', W.S.git(self.root, 'show', original + ':first.txt'))

    def test_bounded_historical_commit_ancestry_materializes_without_unused_trees(self):
        entry = self.file('current.txt')
        A.write_index(self.root, [entry])
        current_tree = self.git_tree(self.root, [entry])
        old_tree = A.store_object(self.root, 'tree', b'100644 old.txt\0' + bytes.fromhex(entry['git_sha']))
        parent = self.git_commit(self.root, old_tree)
        head = A.store_object(self.root, 'commit', ('tree ' + current_tree + '\nparent ' + parent
            + '\nauthor Fixture <fixture@example.test> 0 +0000\ncommitter Fixture <fixture@example.test> 0 +0000\n\nBounded history.\n').encode())
        objects = A.read_objects(self.root, [head, parent, current_tree, entry['git_sha']])
        manifest = {'entries': A.bind_entries(self.root, [entry]), 'submodules': [], 'metadata': [
            {'path': '', 'head': head, 'origin': 'https://github.com/Hexalith/Hexalith.EventStore.git', 'objects': objects}]}
        private = self.root / 'isolated'
        private.mkdir()
        A.materialize(self.root, private, manifest)
        self.assertEqual(head, A.git(private, 'rev-parse', 'HEAD').decode().strip())
        A.git(private, 'merge-base', '--is-ancestor', parent, head)
        with self.assertRaises(C.Refusal):
            A.git(private, 'cat-file', '-e', old_tree)

    def test_installed_runtime_alias_binds_target_bytes_and_detects_retargeting(self):
        target = self.root / 'runtime.dll'
        target.write_bytes(b'installed runtime bytes')
        alias = self.root / 'runtime-alias.dll'
        alias.symlink_to(target.name)
        expected = C.digest(target.read_bytes())
        self.assertEqual(expected, A.runtime_digest(alias))
        # Package/source integrity still refuses links; only installed runtime
        # aliases use resolution, and each runtime check resolves them afresh.
        with self.assertRaises(C.Refusal):
            A.file_digest(alias)
        other = self.root / 'other.dll'
        other.write_bytes(b'changed runtime bytes')
        alias.unlink()
        alias.symlink_to(other.name)
        self.assertNotEqual(expected, A.runtime_digest(alias))

    def test_private_dependency_archive_and_extracted_bytes_are_verified(self):
        cache = self.root / 'packages'
        package = cache / 'fixture/1.0.0'
        package.mkdir(parents=True)
        archive = package / 'fixture.1.0.0.nupkg'
        with zipfile.ZipFile(archive, 'w') as document:
            document.writestr('lib/runtime.dll', b'verified dependency bytes')
        (package / 'lib').mkdir()
        dll = package / 'lib/runtime.dll'
        dll.write_bytes(b'verified dependency bytes')
        archive.with_suffix('.nupkg.sha512').write_bytes(base64.b64encode(hashlib.sha512(archive.read_bytes()).digest()))
        bound = A.dependency_inventory(cache)
        self.assertEqual(3, len(bound))
        dll.write_bytes(b'altered extracted cache')
        with self.assertRaises(C.Refusal):
            A.dependency_inventory(cache)

    def test_nuget_redundant_separators_preserve_byte_binding_and_reject_collisions(self):
        cache = self.root / 'packages'
        package = cache / 'fixture/1.0.0'
        package.mkdir(parents=True)
        archive = package / 'fixture.1.0.0.nupkg'
        with zipfile.ZipFile(archive, 'w') as document:
            document.writestr('analyzers/cs//Plugin.dll', b'verified analyzer bytes')
        target = package / 'analyzers/cs/Plugin.dll'
        target.parent.mkdir(parents=True)
        target.write_bytes(b'verified analyzer bytes')
        checksum = archive.with_suffix('.nupkg.sha512')
        checksum.write_bytes(base64.b64encode(hashlib.sha512(archive.read_bytes()).digest()))
        self.assertEqual(3, len(A.dependency_inventory(cache)))
        with zipfile.ZipFile(archive, 'a') as document:
            document.writestr('analyzers/cs/Plugin.dll', b'verified analyzer bytes')
        checksum.write_bytes(base64.b64encode(hashlib.sha512(archive.read_bytes()).digest()))
        with self.assertRaises(C.Refusal):
            A.dependency_inventory(cache)

    def test_support_safe_diagnostics_forward_method_and_bound_path_only(self):
        marker = os.urandom(32).hex()
        work = self.root / 'work'
        work.mkdir()
        (work / 'lane.xml').write_text('<assemblies><assembly><test type="Business.Tests" method="RejectsCredential" name="case(' + marker + ')" result="Fail"><failure><message>'
            + marker + ' business/auth.cs:42 outside/unknown.cs:9</message></failure></test></assembly></assemblies>')
        result = A.failure_diagnostic('lane', C.Refusal('STORY_EVIDENCE_FAILED', 'fixed evidence command failed'), work,
                                      {'entries': [{'path': 'business/auth.cs'}]})
        self.assertEqual(['Business.Tests.RejectsCredential'], result['failed_tests'])
        self.assertEqual(['business/auth.cs:42'], result['source_findings'])
        self.assertNotIn(marker, json.dumps(result))

    def test_all_file_bytes_and_membership_are_content_bound(self):
        entries = [self.file('source.cs'), self.file('Directory.Build.props')]
        bound = A.bind_entries(self.root, entries)
        self.assertEqual(C.digest(b'bound source\n'), bound[0]['sha256'])
        (self.root / 'source.cs').write_bytes(b'drift\n')
        with self.assertRaises(C.Refusal) as caught:
            A.bind_entries(self.root, entries)
        self.assertEqual('SOURCE_MISMATCH', caught.exception.code)
        for entries in ([entries[0], entries[0]], [{'path': '../escape', 'mode': '100644', 'git_sha': 'a'*40}]):
            with self.assertRaises(C.Refusal):
                A.validate_entries(entries)

    def test_scratch_index_preserves_exact_root_membership_and_gitlinks(self):
        entries = [self.file('a.cs'), {'path': 'references/Dependency', 'mode': '160000', 'git_sha': 'a' * 40}]
        A.write_index(self.root, entries)
        self.assertEqual(entries, A.index_entries(self.root))
        self.assertFalse((self.root / 'references/Dependency/.git').exists())
        self.assertFalse((self.root / '.git/config').read_text().find('credential') >= 0)

    def test_absolute_escaping_and_unbound_symlinks_refuse(self):
        target = self.file('target.txt')
        link = self.root / 'link.txt'
        entry = {'path': 'link.txt', 'mode': '120000', 'git_sha': '0' * 40}
        for destination in (str(self.root / 'target.txt'), '../outside.txt'):
            with self.subTest(destination=destination):
                link.symlink_to(destination)
                with self.assertRaises(C.Refusal):
                    A.read_source(self.root, entry)
                link.unlink()
        link.symlink_to('target.txt')
        entry['git_sha'] = A.blob_sha(b'target.txt')
        with self.assertRaises(C.Refusal):
            A.bind_entries(self.root, [entry])
        self.assertEqual(2, len(A.bind_entries(self.root, [entry, target])))

    def xml(self, total=1, passed=1, failed=0, skipped=0, kind='Business.Tests', result='Pass'):
        path = self.root / 'result.xml'
        tests = ''.join('<test type="' + kind + '" result="' + result + '" />' for _ in range(total))
        path.write_text(f'<assemblies><assembly total="{total}" passed="{passed}" failed="{failed}" skipped="{skipped}" errors="0"><collection>{tests}</collection></assembly></assemblies>')
        return path

    def test_xml_positive_discovery_failed_skipped_empty_and_missing_selectors(self):
        result = A.counts(self.xml(), ['Business.Tests'])
        self.assertEqual({'Business.Tests': 1}, result['selectors'])
        for options, selectors in [({'total': 0, 'passed': 0}, []), ({'passed': 0, 'failed': 1, 'result': 'Fail'}, []),
                                   ({'passed': 0, 'skipped': 1, 'result': 'Skip'}, []), ({}, ['Missing.Tests'])]:
            with self.subTest(options=options, selectors=selectors):
                with self.assertRaises(C.Refusal):
                    A.counts(self.xml(**options), selectors)
        self.assertEqual(1, A.counts(self.xml(passed=0, failed=1, result='Fail'), [], passing=False)['failed'])
        path = self.xml()
        path.write_text(path.read_text().replace('errors="0"', 'errors="0" not-run="1"'))
        with self.assertRaises(C.Refusal):
            A.counts(path, [])

    def test_authenticated_inventory_binds_current_bytes_and_add_delete_membership(self):
        entries = []
        required = {*A.CONTROL_FILES, A.ORIGINAL_SPEC, *C.CONTROL_FILES, *C.STORY_CONTROL_FILES,
                    '.gitmodules', 'Hexalith.EventStore.slnx', 'global.json', 'Directory.Build.props',
                    'Directory.Build.targets', 'Directory.Packages.props'}
        for path in sorted(required):
            entries.append(self.file(path, (T.ROOT / path).read_bytes()))
        business = self.file('business/auth.cs')
        entries.append(business)
        def object_file(root, kind, value):
            body = kind.encode() + b' ' + str(len(value)).encode() + b'\0' + value
            identity = hashlib.sha1(body).hexdigest()
            destination = root / '.git/objects' / identity[:2] / identity[2:]
            destination.parent.mkdir(parents=True, exist_ok=True)
            destination.write_bytes(zlib.compress(body))
            return identity
        for path in A.SUBMODULES:
            root = self.root / path
            root.mkdir(parents=True)
            value = b'fixed fixture build input\n'
            name = A.PACT if path.endswith('Hexalith.FrontComposer') else 'input.props'
            destination = root / name
            destination.parent.mkdir(parents=True, exist_ok=True)
            destination.write_bytes(value)
            member = {'path': name, 'mode': '100644', 'git_sha': A.blob_sha(value)}
            A.write_index(root, [member])
            tree = self.git_tree(root, [member])
            commit = self.git_commit(root, tree)
            entries.append({'path': path, 'mode': '160000', 'git_sha': commit})
        A.write_index(self.root, entries)
        tree = self.git_tree(self.root, entries)
        head = self.git_commit(self.root, tree)
        responses = {'/git/commits/' + head: {'sha': head, 'tree': {'sha': tree}},
                     '/git/trees/' + tree + '?recursive=1': {'sha': tree, 'truncated': False, 'tree': [
                         {'path': row['path'], 'mode': row['mode'], 'sha': row['git_sha'],
                          'type': 'commit' if row['mode'] == '160000' else 'blob'} for row in entries]}}
        class Platform:
            def get(self, endpoint):
                return responses[endpoint]
        with mock.patch.object(A, 'BUILDS_HISTORY', ()):
            manifest = A.inventory(self.root, Platform(), head)
        self.assertGreater(A.summary(manifest)['source_count'], len(required))
        A.assert_stable(self.root, manifest)
        (self.root / business['path']).write_bytes(b'edited input\n')
        with self.assertRaises(C.Refusal):
            A.assert_stable(self.root, manifest)
        (self.root / business['path']).write_bytes(b'bound source\n')
        new = self.file('business/new.cs')
        for changed in ([*entries, new], [row for row in entries if row != business]):
            A.write_index(self.root, changed)
            with self.assertRaises(C.Refusal):
                A.assert_stable(self.root, manifest)
        A.write_index(self.root, entries)
        A.assert_stable(self.root, manifest)
        private = self.root / 'private'
        private.mkdir()
        A.materialize(self.root, private, manifest)
        self.assertEqual(head, A.git(private, 'rev-parse', 'HEAD').decode().strip())
        self.assertEqual('https://github.com/Hexalith/Hexalith.EventStore.git', A.git(private, 'config', '--get', 'remote.origin.url').decode().strip())
        self.assertEqual((self.root / 'references/Hexalith.FrontComposer' / A.PACT).read_bytes(),
                         (private / 'references/Hexalith.FrontComposer' / A.PACT).read_bytes())

    def test_full_tree_subject_changes_for_source_or_membership_changes(self):
        class Platform:
            def get(self, endpoint):
                commit = endpoint.rsplit('/', 1)[1]
                return {'sha': commit, 'tree': {'sha': 'c' * 40 if commit == T.HEAD else 'd' * 40}}
        platform = Platform()
        self.assertNotEqual(A.subject_at(platform, T.HEAD), A.subject_at(platform, T.BEFORE))

    def test_private_dapr_home_has_empty_components_without_ambient_copy(self):
        entry = self.file('source.txt')
        runtime = self.root / 'daprd'
        runtime.write_bytes(b'bound executable fixture')
        manifest = {'entries': A.bind_entries(self.root, [entry]), 'submodules': [], 'runtime': [
            {'name': 'dapr-runtime', 'source': str(runtime), 'destination': '.dapr/bin/daprd',
             'sha256': A.runtime_digest(runtime), 'size': runtime.stat().st_size}]}
        private = self.root / 'private'
        private.mkdir()
        A.materialize(self.root, private, manifest)
        self.assertTrue((private / '.dapr/components').is_dir())
        self.assertEqual([], list((private / '.dapr/components').iterdir()))
        self.assertIn('samplingRate: "0"', (private / '.dapr/config.yaml').read_text())

    def test_diagnostic_rechecks_real_index_and_untracked_path_modes(self):
        tracked = self.file('business/tracked.cs')
        A.write_index(self.root, [tracked])
        for path in {*A.CONTROL_FILES, *C.STORY_CONTROL_FILES}:
            self.file(path)
        helper = self.file('business/helper.cs')
        manifest = {'entries': A.bind_entries(self.root, A.diagnostic_entries(self.root), exact=False), 'submodules': []}
        index = (self.root / '.git/index').read_bytes()
        A.assert_stable(self.root, manifest, diagnostic=True)
        late = self.file('business/late.cs')
        with self.assertRaises(C.Refusal) as caught:
            A.assert_stable(self.root, manifest, diagnostic=True)
        self.assertEqual('SOURCE_MISMATCH', caught.exception.code)
        (self.root / late['path']).unlink()
        (self.root / helper['path']).chmod(0o755)
        with self.assertRaises(C.Refusal):
            A.assert_stable(self.root, manifest, diagnostic=True)
        (self.root / helper['path']).chmod(0o644)
        A.assert_stable(self.root, manifest, diagnostic=True)
        self.assertEqual(index, (self.root / '.git/index').read_bytes())

    def test_cleanup_continues_after_inspect_and_remove_disappearance(self):
        owner = 'a' * 32
        identifiers = ['b' * 64, 'c' * 64, 'd' * 64]
        calls = []
        def run(argv, label, timeout):
            calls.append(argv)
            if argv[1] == 'ps':
                return b'' if any(value.startswith('id=') for value in argv) else ('\n'.join(identifiers) + '\n').encode()
            if argv[1] == 'inspect':
                if argv[-1] == identifiers[0]:
                    raise C.Refusal('STORY_EVIDENCE_FAILED', 'fixed evidence command failed')
                return json.dumps(owner).encode()
            if argv[1] == 'rm' and argv[-1] == identifiers[1]:
                raise C.Refusal('STORY_EVIDENCE_FAILED', 'fixed evidence command failed')
            return b'removed'
        A.cleanup_authentication_containers(owner, run)
        self.assertEqual(identifiers, [argv[-1] for argv in calls if argv[1] == 'inspect'])
        self.assertEqual(identifiers[1:], [argv[-1] for argv in calls if argv[1] == 'rm'])

    def test_cleanup_aggregates_real_failures_after_remaining_owned_ids(self):
        owner = 'a' * 32
        identifiers = ['b' * 64, 'c' * 64, 'd' * 64]
        calls = []
        def run(argv, label, timeout):
            calls.append(argv)
            if argv[1] == 'ps':
                selected = next((value[3:] for value in argv if value.startswith('id=')), None)
                return ((selected or '\n'.join(identifiers)) + '\n').encode()
            if argv[1] == 'inspect':
                return json.dumps(owner + ' ' if argv[-1] == identifiers[0] else owner).encode()
            if argv[1] == 'rm' and argv[-1] == identifiers[1]:
                raise C.Refusal('STORY_EVIDENCE_FAILED', 'daemon refused removal')
            return b'removed'
        with self.assertRaises(C.Refusal) as caught:
            A.cleanup_authentication_containers(owner, run)
        self.assertIn('ownership changed; refusing cleanup', str(caught.exception))
        self.assertIn(identifiers[1] + ':cleanup failed', str(caught.exception))
        self.assertEqual(identifiers[1:], [argv[-1] for argv in calls if argv[1] == 'rm'])

    def test_timeout_cleanup_only_removes_exact_invocation_owned_containers(self):
        owner = 'a' * 32
        identifier = 'b' * 64
        calls = []
        def run(argv, label, timeout):
            calls.append(argv)
            if argv[1] == 'ps':
                self.assertIn('label=hexalith.eventstore.authentication-test-owner=' + owner, argv)
                return (identifier + '\n').encode()
            if argv[1] == 'inspect':
                return (json.dumps(owner) + '\n').encode()
            return b'removed'
        A.cleanup_authentication_containers(owner, run)
        self.assertEqual(['docker', 'rm', '--force', identifier], calls[-1])
        calls.clear()
        def foreign(argv, label, timeout):
            calls.append(argv)
            return (identifier + '\n').encode() if argv[1] == 'ps' else json.dumps('foreign-owner').encode()
        with self.assertRaises(C.Refusal):
            A.cleanup_authentication_containers(owner, foreign)
        self.assertFalse(any(argv[1] == 'rm' for argv in calls))

    def test_outer_budget_failure_reaps_started_authentication_with_the_same_owner(self):
        for started in (False, True):
            with self.subTest(started=started):
                selected_owner = []
                logs = self.root / ('retained-' + str(started))
                logs.mkdir()
                def interrupted(argv, scratch, filename, **options):
                    owner = options['environment_extra'][A.AUTH_OWNER_ENV]
                    temporary = Path(options['environment_extra'][A.AUTH_TEMP_ENV])
                    self.assertTrue(temporary.is_dir())
                    self.assertFalse(temporary.is_relative_to(scratch))
                    if argv[0] == 'docker':
                        self.assertEqual(selected_owner[0], owner)
                        (scratch / filename).write_text('owned cleanup inventory empty\n')
                        return b''
                    selected_owner.append(owner)
                    if started:
                        (scratch / (A.AUTH_MARKER_PREFIX + owner)).touch()
                    raise C.Refusal('STORY_EVIDENCE_LIMIT', 'forced outer budget exhaustion')
                def cleanup_owned(owner, run):
                    return run(['docker', 'ps'], 'integration-auth-cleanup-list', timeout=30)
                manifest = {'runtime': [{'name': 'dotnet', 'source': sys.executable}]}
                with mock.patch.object(A, 'materialize'), mock.patch.object(C.Control, 'bounded_process', side_effect=interrupted), \
                     mock.patch.object(A, 'cleanup_authentication_containers', side_effect=cleanup_owned) as cleanup:
                    with self.assertRaises(C.Refusal) as caught:
                        A.run(self.root, manifest, diagnostic=True, logs=logs)
                self.assertEqual('STORY_EVIDENCE_LIMIT', caught.exception.code)
                self.assertEqual(1, len(selected_owner))
                self.assertEqual(1 if started else 0, cleanup.call_count)
                if started:
                    self.assertEqual(selected_owner[0], cleanup.call_args.args[0])
                    self.assertEqual('owned cleanup inventory empty\n',
                        (logs / 'outer-integration-auth-cleanup-list.log').read_text())

    def test_bounded_process_removes_tokens_and_enforces_output(self):
        marker = os.urandom(32).hex()
        with mock.patch.dict(os.environ, GH_TOKEN=marker, GITHUB_TOKEN=marker):
            content = C.Control.bounded_process([sys.executable, '-I', '-B', '-c',
                'import os; assert not any(k in os.environ for k in ("GH_TOKEN", "GITHUB_TOKEN", "PYTHONPATH")); print("isolated")'], self.root, 'output.txt')
        self.assertEqual(b'isolated\n', content)
        with self.assertRaises(C.Refusal) as caught:
            C.Control.bounded_process([sys.executable, '-I', '-B', '-c', 'print("x" * 1100000)'], self.root,
                                      'large.txt', limit_code='STORY_EVIDENCE_LIMIT')
        self.assertEqual('STORY_EVIDENCE_LIMIT', caught.exception.code)

    def test_validator_accepts_no_caller_executable_or_saved_report(self):
        for arguments in (['--dotnet', '/bin/true'], ['--command', '/bin/true'], ['--result', 'saved-pass.json']):
            process = subprocess.run([sys.executable, '-B', str(T.ROOT / A.VALIDATOR), *arguments],
                                     capture_output=True, text=True, timeout=15)
            self.assertNotEqual(0, process.returncode)
            self.assertNotIn('Traceback', process.stderr)

    def test_genuine_current_scanner_positive_and_historical_gap_controls(self):
        dotnet = shutil.which('dotnet')
        self.assertIsNotNone(dotnet, 'The applicable business-evidence fixture lane requires the pinned .NET SDK.')
        shutil.copyfile(T.ROOT / 'Hexalith.EventStore.slnx', self.root / 'Hexalith.EventStore.slnx')
        probes = A.capability_probe(self.root, T.ROOT, Path(dotnet).resolve(), Path.home() / '.nuget/packages')
        self.assertEqual(list(A.CAPABILITIES), [item['id'] for item in probes])
        self.assertEqual(7, len(probes))
        self.assertTrue(all(type(item['detected']) is bool for item in probes))
        for item in probes:
            with self.subTest(capability=item['id']):
                self.assertTrue(item['detected'], 'The actual business recognizer must reject every generated credential control.')

    def test_real_bound_validator_refuses_and_forwards_genuine_business_results(self):
        # A deliberately incomplete private source fixture cannot complete the
        # declared plan. Its scanner partials and entry points are real, and the
        # isolated explicit validator must execute them, preserving the failure.
        paths = {*C.CONTROL_FILES, *A.CONTROL_FILES, 'global.json', 'Directory.Packages.props',
                 'Hexalith.EventStore.slnx', 'nuget.config'}
        paths.update(path.relative_to(T.ROOT).as_posix() for path in
                     (T.ROOT / 'tests/Hexalith.EventStore.Server.Tests/Security').glob('SecretsProtectionTests*.cs'))
        paths.update(path.relative_to(T.ROOT).as_posix() for path in (T.ROOT / 'references/Hexalith.Builds/Props').glob('*') if path.is_file())
        entries = [self.file(path, (T.ROOT / path).read_bytes()) for path in sorted(paths)]
        bound = A.bind_entries(self.root, entries, exact=False)
        A.write_index(self.root, bound)
        dotnet = Path(shutil.which('dotnet')).resolve()
        manifest = {'adapter': A.ADAPTER, 'root_tree': None, 'entries': bound, 'submodules': [], 'metadata': [],
                    'runtime': [{'name': 'dotnet', 'source': str(dotnet), 'destination': None,
                                 'sha256': A.file_digest(dotnet), 'size': dotnet.stat().st_size}]}
        real_process = C.Control.bounded_process
        source = self.root / 'nuget.config'
        original = source.read_bytes()
        def concurrent_edit(*arguments, **options):
            output = real_process(*arguments, **options)
            source.write_bytes(original + b'\n')
            return output
        try:
            with mock.patch.object(C.Control, 'bounded_process', side_effect=concurrent_edit):
                with self.assertRaises(C.Refusal) as caught:
                    A.run(self.root, manifest, diagnostic=True)
            self.assertEqual('SOURCE_MISMATCH', caught.exception.code)
            report = caught.exception.story_evidence
        finally:
            source.write_bytes(original)
        self.assertEqual('FAIL', report['result'])
        self.assertEqual(list(A.CAPABILITIES), [item['id'] for item in report['capabilities']])
        self.assertTrue(report['capabilities'][0]['detected'])
        missing = [item for item in report['capabilities'] if not item['detected']]
        capability = next(row for row in report['lanes'] if row['id'] == 'scanner-capabilities')
        self.assertEqual('FAIL' if missing else 'PASS', capability['result'])
        self.assertTrue(report['diagnostics'])
        control = C.Control.__new__(C.Control)
        control.request = {'transition': 'high-risk-nfr-story-done'}
        control.binding, control.required, control.achieved, control.minimum = {}, 'none', 'none', 'none'
        control.approvals, control.payloads, control.predecessors = [], [], []
        control.story_evidence = report
        forwarded = control.result('evaluate', ['STORY_EVIDENCE_FAILED'])
        self.assertEqual(report, forwarded['story_evidence'])
        self.assertEqual(report['diagnostics'], forwarded['story_evidence']['diagnostics'])

    def test_detached_descendant_is_killed_and_reaped(self):
        child = self.root / 'detached.py'
        child.write_text('import os,time\nfrom pathlib import Path\npid=os.fork()\nif pid==0:\n os.setsid()\n Path("detached.pid").write_text(str(os.getpid()))\n time.sleep(60)\nelse:\n time.sleep(0.2)\n')
        C.Control.bounded_process([sys.executable, '-I', '-B', str(child)], self.root, 'detached.log')
        pid = int((self.root / 'detached.pid').read_text())
        self.assertFalse(Path('/proc', str(pid)).exists(), 'Detached evidence child must be terminated and reaped.')


class SupportedClaimRefusals(unittest.TestCase):
    """Real CLI/platform checks; missing complete source cannot be faked green."""

    def setUp(self):
        self.writer = W.StoryCompletionTests(methodName='runTest')
        self.writer.setUp()
        self.addCleanup(self.writer.doCleanups)
        self.writer.select('5.3', path='_bmad-output/implementation-artifacts/spec-5-3-supported-fixture.md')
        self.f = self.writer.f
        for commit, tree in ((T.HEAD, 'c' * 40), (T.BEFORE, 'd' * 40)):
            self.f.put('/git/commits/' + commit, {'sha': commit, 'tree': {'sha': tree}})
        self.approve()

    def approve(self, created='2026-01-02T00:00:00Z'):
        self.f.approve(created=created, roles=['product-owner', 'test-owner'])
        subject = C.digest(C.canonical({'adapter': A.ADAPTER, 'root_tree': 'c' * 40}))
        for reference in self.f.request['approvals']:
            comment = self.f.api_document('/issues/comments/' + str(reference['comment_id']))
            body = json.loads(comment['body'])
            body['subject_sha256'] = subject
            comment['body'] = C.canonical(body).decode()
            reference['body_sha256'] = C.digest(comment['body'].encode())

    def test_business_source_only_push_restarts_twenty_four_hour_window(self):
        # The four caller-selected coverage files are unchanged in history.
        # Only the authenticated root tree changes; the earlier narrow subject
        # would have missed this real business-source/membership change.
        for item in self.f.request['subjects']:
            self.f.blob(T.BEFORE, item['path'], (self.f.root / item['path']).read_bytes())
        self.approve(created='2026-01-01T23:59:59Z')
        self.writer.evaluation('APPROVAL_WINDOW')

    def test_missing_complete_inventory_refuses_both_targets_and_saved_candidate(self):
        self.f.put('/git/trees/' + 'c' * 40 + '?recursive=1', {'sha': 'c' * 40, 'truncated': False, 'tree': [
            {'path': 'business/auth.cs', 'type': 'blob', 'mode': '100644', 'sha': 'e' * 40}]})
        for target in ('spec', 'tracker'):
            with self.subTest(target=target):
                self.f.request['story']['target'] = target
                self.approve()
                candidate = self.writer.evaluation('SOURCE_MISMATCH')
                candidate.update(result='PASS', codes=[])
                self.f.artifact(candidate=candidate)
                document = self.writer.refused('SOURCE_MISMATCH', target=target, authenticated=True)
                self.assertIn('membership', document['message'])

    def test_truncated_inventory_is_never_complete_evidence(self):
        self.f.put('/git/trees/' + 'c' * 40 + '?recursive=1', {'sha': 'c' * 40, 'truncated': True, 'tree': []})
        self.writer.evaluation('PAGINATION_INCOMPLETE')

    def test_missing_previous_commit_is_not_subject_creation_evidence(self):
        self.f.state['responses'][T.API + '/git/commits/' + T.BEFORE] = {'error': 404}
        self.writer.evaluation('HISTORY_UNAVAILABLE')


if __name__ == '__main__':
    unittest.main()

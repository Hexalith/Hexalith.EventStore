"""Snapshot/application-graph controls; synthetic local hashes grant no authority."""
import hashlib
import os
from pathlib import Path
import shutil
import sys
import tempfile
import unittest
from unittest import mock

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'tools'))
import profile_bridge_build as B
import phase_4_transition as T


class ProfileBridgeBuildTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.base = Path(self.temp.name)
        self.root = self.base / 'checkout'
        for path in B.BUILD_INPUTS:
            destination = self.root / path
            destination.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(ROOT / path, destination)
        self.data = {'deploy/dapr/production-profile.yaml': b'synthetic profile',
                     'deploy/dapr/production-composing-inventory.json': b'synthetic inventory',
                     'composition.json': b'independent composition'}
        for path, content in self.data.items():
            destination = self.root / path
            destination.parent.mkdir(parents=True, exist_ok=True)
            destination.write_bytes(content)

    def assert_refused_before_child(self, action):
        with mock.patch.object(T.Control, 'bounded_process') as child:
            with self.assertRaises(B.Refusal):
                action()
            child.assert_not_called()

    def test_complete_membership_exact_owner_bytes_and_hashes_survive_checkout_edits(self):
        captured = B.capture(self.root, self.data)
        self.assertEqual(38, len(B.SOURCES))
        self.assertEqual(set(B.BUILD_INPUTS) | set(self.data), set(dict(captured.files)))
        for path in B.SOURCES:
            self.assertEqual((ROOT / path).read_bytes(), dict(captured.files)[path])
            self.assertEqual(hashlib.sha256((ROOT / path).read_bytes()).hexdigest(), dict(captured.hashes)[path])
        for path in (*B.SOURCES, *self.data):
            (self.root / path).write_bytes(b'checkout changed after capture')
        snapshot = self.base / 'snapshot'
        B.materialize(captured, snapshot)
        B.verify(captured, snapshot)
        for path, content in captured.files:
            self.assertEqual(content, (snapshot / path).read_bytes())

    def test_missing_sources_controls_and_changed_data_refuse_before_launch(self):
        for path in B.BUILD_INPUTS:
            original = (self.root / path).read_bytes()
            (self.root / path).unlink()
            with self.subTest(path=path):
                self.assert_refused_before_child(lambda: B.capture(self.root, self.data))
            (self.root / path).write_bytes(original)
        for path, original in self.data.items():
            (self.root / path).write_bytes(b'changed before capture')
            with self.subTest(path=path):
                self.assert_refused_before_child(lambda: B.capture(self.root, self.data))
            (self.root / path).write_bytes(original)

    def test_extra_profile_or_bridge_members_refuse_but_unrelated_contracts_are_not_captured(self):
        for relative in (B.SOURCE_DIRECTORY + '/ProductionProfileUnexpected.cs', B.BRIDGE + '/Unexpected.cs',
                         B.BRIDGE + '/Directory.Build.targets'):
            path = self.root / relative
            path.write_bytes(b'#error must never compile')
            with self.subTest(path=relative):
                self.assert_refused_before_child(lambda: B.capture(self.root, self.data))
            path.unlink()
        unrelated = self.root / 'src/Hexalith.EventStore.Contracts/Unexpected.cs'
        unrelated.write_bytes(b'#error unrelated Contracts source')
        self.assertNotIn(unrelated.relative_to(self.root).as_posix(), dict(B.capture(self.root, self.data).files))

    def test_closed_project_and_config_refuse_imports_references_defaults_and_fallbacks(self):
        project = (self.root / B.PROJECT).read_bytes()
        config = (self.root / B.CONFIG).read_bytes()
        candidates = [project.replace(b'</Project>', extra + b'</Project>') for extra in (
            b'<Import Project="poison.targets" />', b'<ItemGroup><ProjectReference Include="other.csproj" /></ItemGroup>',
            b'<ItemGroup><PackageReference Include="other" Version="1" /></ItemGroup>')]
        candidates += [project.replace(b'EnableDefaultItems>false', b'EnableDefaultItems>true'),
                       project.replace(b'Include="Program.cs"', b'Include="*.cs"'),
                       project.replace(b'net11.0', b'net99.0')]
        for content in candidates:
            (self.root / B.PROJECT).write_bytes(content)
            self.assert_refused_before_child(lambda: B.capture(self.root, self.data))
        (self.root / B.PROJECT).write_bytes(project)
        for content in (config.replace(b'<clear />', b'<add key="fallback" value="https://invalid/" />', 1),
                        config.replace(b'</configuration>', b'<config><add key="globalPackagesFolder" value="/cached" /></config></configuration>')):
            (self.root / B.CONFIG).write_bytes(content)
            self.assert_refused_before_child(lambda: B.capture(self.root, self.data))

    def test_paths_bounded_regular_files_and_symbolic_ancestors_refuse(self):
        for relative in ('../outside', '/absolute', 'a//b', 'a/./b', 'a\\b'):
            self.assert_refused_before_child(lambda: B.capture(self.root, {**self.data, relative: b'x'}))
        path = self.root / B.PROGRAM
        original = path.read_bytes()
        path.unlink()
        path.symlink_to(ROOT / B.PROGRAM)
        self.assert_refused_before_child(lambda: B.capture(self.root, self.data))
        path.unlink()
        os.mkfifo(path)
        self.assert_refused_before_child(lambda: B.capture(self.root, self.data))
        path.unlink()
        with path.open('wb') as stream:
            stream.truncate(B.MAX_FILE + 1)
        self.assert_refused_before_child(lambda: B.capture(self.root, self.data))
        path.write_bytes(original)
        symbolic = self.base / 'symbolic'
        symbolic.symlink_to(self.root, target_is_directory=True)
        self.assert_refused_before_child(lambda: B.capture(symbolic, self.data))

    def test_global_sdk_pin_cannot_redirect_to_checkout_sdks_or_custom_resolvers(self):
        for content in (b'{"sdk":{"version":"11.0.100","paths":["../untrusted"]}}',
                        b'{"sdk":{"version":"11.0.100"},"msbuild-sdks":{"Microsoft.NET.Sdk":"1.0.0"}}',
                        b'{"sdk":{"version":"11.0.100","version":"99.0.0"}}',
                        b'{"sdk":{"version":"11.0.100","allowPrerelease":"true"}}'):
            (self.root / 'global.json').write_bytes(content)
            self.assert_refused_before_child(lambda: B.capture(self.root, self.data))

    def test_individually_bounded_sources_cannot_exceed_total_capture_budget(self):
        content = b'x' * B.MAX_FILE
        for path in B.SOURCES[:B.MAX_TOTAL // B.MAX_FILE + 1]:
            (self.root / path).write_bytes(content)
            self.assertEqual(content, B.read(self.root, path))
        self.assert_refused_before_child(lambda: B.capture(self.root, self.data))

    def test_larger_data_file_limits_accept_boundary_and_refuse_one_byte_more(self):
        for path in ('deploy/dapr/production-profile.yaml', 'composition.json', 'deploy/dapr/rendered.yaml'):
            with self.subTest(path=path):
                destination = self.root / path
                original = destination.read_bytes() if destination.exists() else None
                data = {**self.data, path: b'x' * (4 * B.MAX_FILE)}
                try:
                    destination.write_bytes(data[path])
                    self.assertEqual(len(dict(B.capture(self.root, data).files)[path]), 4 * B.MAX_FILE)
                    data[path] += b'x'
                    destination.write_bytes(data[path])
                    self.assert_refused_before_child(lambda: B.capture(self.root, data))
                finally:
                    if original is None: destination.unlink()
                    else: destination.write_bytes(original)

    def test_substituted_intermediate_directory_is_not_followed_by_descriptor_open(self):
        directory = self.root / B.SOURCE_DIRECTORY
        saved = directory.with_name('saved-deployment')
        original_open = os.open
        substituted, followed = [], []
        def raced(path, flags, *args, **kwargs):
            target = path == 'Deployment' and kwargs.get('dir_fd') is not None and not substituted
            if target:
                directory.rename(saved)
                directory.symlink_to(saved, target_is_directory=True)
                substituted.append(True)
            descriptor = original_open(path, flags, *args, **kwargs)
            if target: followed.append(True)
            return descriptor
        with mock.patch.object(B.os, 'open', side_effect=raced):
            self.assert_refused_before_child(lambda: B.capture(self.root, self.data))
        self.assertEqual([True], substituted)
        self.assertEqual([], followed)

    def test_substituted_final_source_is_not_followed_by_descriptor_open(self):
        source = self.root / B.SOURCES[0]
        saved = self.base / 'saved-source'
        original_open = os.open
        substituted, followed = [], []
        def raced(path, flags, *args, **kwargs):
            target = path == source.name and kwargs.get('dir_fd') is not None and not substituted
            if target:
                source.rename(saved)
                source.symlink_to(saved)
                substituted.append(True)
            descriptor = original_open(path, flags, *args, **kwargs)
            if target: followed.append(True)
            return descriptor
        with mock.patch.object(B.os, 'open', side_effect=raced):
            self.assert_refused_before_child(lambda: B.capture(self.root, self.data))
        self.assertEqual([True], substituted)
        self.assertEqual([], followed)

    def test_snapshot_missing_changed_extra_and_symbolic_members_refuse(self):
        captured = B.capture(self.root, self.data)
        for mutation in ('missing', 'changed', 'extra', 'directory', 'symbolic'):
            snapshot = self.base / mutation
            B.materialize(captured, snapshot)
            target = snapshot / B.SOURCES[0]
            if mutation == 'missing': target.unlink()
            if mutation == 'changed': target.write_bytes(b'changed')
            if mutation == 'extra': (snapshot / 'extra.cs').write_bytes(b'added')
            if mutation == 'directory': (snapshot / 'extra').mkdir()
            if mutation == 'symbolic':
                target.unlink()
                target.symlink_to(self.root / B.SOURCES[0])
            with self.subTest(mutation=mutation):
                self.assert_refused_before_child(lambda: B.verify(captured, snapshot))

    def test_snapshot_mutation_before_first_or_later_child_refuses(self):
        captured = B.capture(self.root, self.data)
        original = B.materialize
        def mutated(capture, snapshot):
            original(capture, snapshot)
            (snapshot / B.PROGRAM).write_bytes(b'changed after copy')
        with mock.patch.object(B, 'materialize', side_effect=mutated), mock.patch.object(B.shutil, 'which', return_value='/usr/bin/dotnet'):
            self.assert_refused_before_child(lambda: B.execute(captured, 'a'*64, 'b'*64, '-'))
        def child(command, scratch, filename, **kwargs):
            (scratch / 'snapshot' / B.PROGRAM).write_bytes(b'changed after restore')
            return b''
        with mock.patch.object(T.Control, 'bounded_process', side_effect=child) as process, mock.patch.object(B.shutil, 'which', return_value='/usr/bin/dotnet'):
            with self.assertRaises(B.Refusal): B.execute(captured, 'a'*64, 'b'*64, '-')
            self.assertEqual(1, process.call_count)

    def test_separate_scratch_sdk_pin_mutation_after_restore_blocks_build(self):
        captured = B.capture(self.root, self.data)
        stages = []
        def child(command, scratch, filename, **kwargs):
            stages.append(command[1])
            self.assertEqual(dict(captured.files)['global.json'], (scratch / 'snapshot/global.json').read_bytes())
            (scratch / 'global.json').write_bytes(b'changed separate SDK pin after restore')
            return b''
        with mock.patch.object(T.Control, 'bounded_process', side_effect=child), \
             mock.patch.object(B.shutil, 'which', return_value='/usr/bin/dotnet'):
            with self.assertRaises(B.Refusal): B.execute(captured, 'a'*64, 'b'*64, '-')
        self.assertEqual(['restore'], stages)

    def test_fixed_commands_private_outputs_filtered_environment_and_configuration(self):
        captured = B.capture(self.root, self.data)
        for ci, configuration in (('', 'Debug'), ('true', 'Release')):
            calls = []
            def child(command, scratch, filename, **kwargs):
                calls.append((command, kwargs))
                self.assertNotIn(str(self.root), ' '.join(command))
                environment = kwargs['environment_extra']
                self.assertNotIn('GITHUB_TOKEN', environment)
                self.assertNotIn('DirectoryBuildPropsPath', environment)
                self.assertNotIn('MSBuildSDKsPath', environment)
                self.assertEqual(str(scratch / 'packages'), environment['NUGET_PACKAGES'])
                self.assertEqual('1', environment['MSBUILDDISABLENODEREUSE'])
                self.assertEqual('0', environment['DOTNET_CLI_USE_MSBUILD_SERVER'])
                if filename == 'build.log':
                    (scratch / 'bin').mkdir()
                    (scratch / 'bin/ProductionProfileBridge.dll').write_bytes(b'fresh synthetic assembly')
                return b'{"synthetic":"result"}' if filename == 'profile-result.txt' else b''
            with mock.patch.dict(os.environ, {'CI': ci, 'GITHUB_TOKEN': 'secret', 'NUGET_PACKAGES': '/poison',
                    'DirectoryBuildPropsPath': '/poison', 'MSBuildSDKsPath': '/poison'}), \
                 mock.patch.object(T.Control, 'bounded_process', side_effect=child), \
                 mock.patch.object(B.shutil, 'which', return_value='/usr/bin/dotnet'):
                self.assertEqual(b'{"synthetic":"result"}', B.execute(captured, 'a'*64, 'b'*64, '-'))
            self.assertEqual(3, len(calls))
            for command, _ in calls[:2]:
                for flag in ('--disable-build-servers', '-noAutoResponse', '-p:ImportDirectoryBuildProps=false',
                             '-p:ImportDirectoryBuildTargets=false', '-p:ImportDirectoryPackagesProps=false',
                             '-p:UseSharedCompilation=false', '-p:EnableTargetingPackDownload=false',
                             '-p:RestoreFallbackFolders=', '-p:DisableImplicitLibraryPacksFolder=true',
                             '-p:Configuration=' + configuration):
                    self.assertIn(flag, command)

    def test_sdk_unavailable_child_limits_and_noop_build_never_supply_success(self):
        captured = B.capture(self.root, self.data)
        with mock.patch.object(B.shutil, 'which', return_value=None):
            self.assert_refused_before_child(lambda: B.execute(captured, 'a'*64, 'b'*64, '-'))
        with mock.patch.object(B.shutil, 'which', return_value='/usr/bin/dotnet'):
            for result in (b'', T.Refusal('PROFILE_BRIDGE_UNAVAILABLE', 'bounded')):
                with mock.patch.object(T.Control, 'bounded_process', side_effect=result if isinstance(result, Exception) else None,
                                       return_value=result):
                    with self.assertRaises(B.Refusal) as caught:
                        B.execute(captured, 'a'*64, 'b'*64, '-')
                    self.assertIn(caught.exception.code, ('PROFILE_OWNER_RESULT_INVALID', 'PROFILE_BRIDGE_UNAVAILABLE'))


if __name__ == '__main__':
    unittest.main()

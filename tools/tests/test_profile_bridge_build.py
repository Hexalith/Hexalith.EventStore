"""Snapshot/application-graph controls; synthetic local hashes grant no authority."""
import hashlib
from contextlib import nullcontext
import json
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

    def fake_installation(self):
        dotnet = self.base / 'installation/dotnet'
        sdk = dotnet.parent / 'sdk/11.0.100'
        runtime = dotnet.parent / 'shared/Microsoft.NETCore.App/11.0.0'
        for path in (dotnet, dotnet.parent/'host/hostfxr', sdk/'Microsoft.Build.Tasks.Core.dll', sdk/'Roslyn/bincore/csc',
                     sdk/'analyzer.dll', dotnet.parent/'packs/Ref/reference.dll',
                     runtime/'libcoreclr.so', runtime/'System.Private.CoreLib.dll'):
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(b'synthetic installed identity')
        for stem in (sdk/'dotnet',sdk/'MSBuild',sdk/'Roslyn/bincore/csc'):
            for suffix in ('.deps.json','.runtimeconfig.json'):Path(str(stem)+suffix).write_bytes(b'{}')
        (sdk/'Current/Imports/Microsoft.Common.targets/ImportBefore').mkdir(parents=True)
        return dotnet, sdk, runtime

    def fixture_child(self, dotnet, sdk, runtime, calls, change=None):
        def child(command, scratch, filename, **kwargs):
            stage = command[-3]
            calls.append(stage)
            settings = json.loads((scratch/'invocation.json').read_bytes())
            output = b''
            if stage == 'sdk': output = b'11.0.100\n'
            if stage == 'preprocess':
                (scratch/'proof/preprocessed.xml').write_text(
                    '<!--\n'+str(scratch/'snapshot'/B.PROJECT)+'\n-->\n<Project><UsingTask TaskName="Message" AssemblyFile="'+
                    str(sdk/'Microsoft.Build.Tasks.Core.dll')+'" /></Project>')
            if stage == 'evaluate': output = b'{"Properties":{}}'
            if stage in ('restore','plan','build'):
                (scratch/'proof'/ (stage+'.tasks')).write_text(
                    'Using "Message" task from assembly "'+str(sdk/'Microsoft.Build.Tasks.Core.dll')+'".')
            if stage == 'restore':
                (scratch/'obj').mkdir()
                (scratch/'obj/project.assets.json').write_bytes(b'{}')
            if stage in ('plan','build'):
                (scratch/'obj/generated.cs').write_bytes(b'// generated source')
                (scratch/'obj/generated.editorconfig').write_bytes(b'is_global=true')
                items = ['source|'+str(scratch/'snapshot'/path) for path in (*B.SOURCES,B.PROGRAM)]
                items += ['source|'+str(scratch/'obj/generated.cs'),
                    'reference|'+str(dotnet.parent/'packs/Ref/reference.dll'),
                    'analyzer|'+str(sdk/'analyzer.dll'),'config|'+str(scratch/'obj/generated.editorconfig'),
                    'import|'+str(scratch/'snapshot'/B.PROJECT)]
                items += [name+'|' for name in ('response','arguments','toolpath','toolexe','cscenvironment','package','project')]
                items += ['complete|profile-bridge-v1']
                (scratch/'proof'/(stage+'.items')).write_text('\n'.join(items)+'\n')
            if stage == 'build':
                (scratch/'bin').mkdir()
                (scratch/'bin/ProductionProfileBridge.dll').write_bytes(b'fresh synthetic assembly')
                (scratch/'proof/compiler.response').write_bytes(b'synthetic response')
            if stage == 'run': output = b'{"synthetic":"result"}'
            receipt = {'invocation':command[-2], 'stage':stage, 'command':B.fixed_command(stage,scratch,settings),
                       'contract':command[-1], 'exit_code':0, 'executions':[B.fixed_command(stage,scratch,settings)],
                       'execution_environments':[json.loads((scratch/'proof'/(stage+'.contract')).read_bytes())['environment']],
                       'runtime_roots':[] if stage=='sdk' else [str(runtime)], 'toolchain_files':[str(dotnet)],
                       'compiler_response':hashlib.sha256(b'synthetic response').hexdigest() if stage=='build' else None,
                       'outputs':B.output_hashes(scratch), 'syscall_stops':1, 'network_attempts':0, 'complete':True}
            if stage=='build':
                receipt['executions'].append([str(sdk/'Roslyn/bincore/csc'),'/noconfig','@'+str(scratch/'compiler.rsp')])
                compiler_environment=receipt['execution_environments'][0].copy()
                compiler_environment.update({'MSBUILDFAILONDRIVEENUMERATINGWILDCARD':'1','MSBUILDUSESERVER':'0',
                    'MSBuildExtensionsPath':str(sdk),'MSBuildLoadMicrosoftTargetsReadOnly':'true',
                    'MSBuildSDKsPath':str(sdk/'Sdks'),'_MSBUILDTLENABLED':'0'})
                receipt['execution_environments'].append(compiler_environment)
            (scratch/'proof'/(stage+'.trace')).write_text(json.dumps(receipt))
            if change: change(stage,scratch,receipt,kwargs)
            return output
        return child

    def test_fixed_stage_order_environment_and_configuration_produce_invocation_scoped_proof(self):
        captured=B.capture(self.root,self.data)
        dotnet,sdk,runtime=self.fake_installation()
        invocations=[]
        for ci,configuration in (('', 'Debug'),('true','Release')):
            calls=[]
            def check(stage,scratch,receipt,kwargs):
                environment=kwargs['environment_extra']
                self.assertNotIn('GITHUB_TOKEN',environment)
                self.assertNotIn('MSBuildSDKsPath',environment)
                self.assertEqual('true',environment['DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE'])
                self.assertEqual('1',environment['DOTNET_CLI_TELEMETRY_OPTOUT'])
                self.assertEqual('false',environment['MSBuildEnableWorkloadResolver'])
                settings=json.loads((scratch/'invocation.json').read_bytes())
                self.assertEqual(configuration,settings['configuration'])
                if stage=='restore':
                    for flag in ('-noAutoResponse','-p:EnableTargetingPackDownload=false',
                                 '-p:DisableImplicitLibraryPacksFolder=true','-p:RestoreSources=',
                                 '-p:ImportDirectoryBuildProps=false','-p:UseSharedCompilation=false'):
                        self.assertIn(flag,receipt['command'])
            with mock.patch.dict(os.environ,{'CI':ci,'GITHUB_TOKEN':'secret','MSBuildSDKsPath':'/poison'}), \
                 mock.patch.object(B.shutil,'which',return_value=str(dotnet)), \
                 mock.patch.object(T.Control,'bounded_process',side_effect=self.fixture_child(dotnet,sdk,runtime,calls,check)):
                result=B.execute(captured,'a'*64,'b'*64,'-')
            self.assertEqual(['sdk','preprocess','evaluate','restore','plan','build','run'],calls)
            self.assertEqual(b'{"synthetic":"result"}',result)
            self.assertTrue(B.proved(result))
            self.assertEqual('11.0.100',result.proof.sdk)
            invocations.append(result.proof.invocation)
        self.assertNotEqual(*invocations)
        self.assertFalse(B.proved(json.loads('{"complete":true}')))
        forged=B.OwnerOutput(b'{}');forged.proof=B.InvocationProof('a','b','11.0.100',(),())
        self.assertFalse(B.proved(forged))

    def test_snapshot_pin_and_installed_task_mutation_stop_before_successor(self):
        captured=B.capture(self.root,self.data)
        dotnet,sdk,runtime=self.fake_installation()
        for target in ('snapshot/'+B.PROGRAM,'global.json','installation','selection'):
            calls=[]
            original=(sdk/'Microsoft.Build.Tasks.Core.dll').read_bytes()
            def change(stage,scratch,*_):
                if stage=='restore':
                    if target=='selection':
                        (sdk.parent/'11.0.101').mkdir()
                        return
                    path=sdk/'Microsoft.Build.Tasks.Core.dll' if target=='installation' else scratch/target
                    path.write_bytes(b'mutated execution input')
            with self.subTest(target=target), mock.patch.object(B.shutil,'which',return_value=str(dotnet)), \
                 mock.patch.object(T.Control,'bounded_process',side_effect=self.fixture_child(dotnet,sdk,runtime,calls,change)):
                with self.assertRaises(B.Refusal):B.execute(captured,'a'*64,'b'*64,'-')
            self.assertEqual(['sdk','preprocess','evaluate','restore'],calls)
            (sdk/'Microsoft.Build.Tasks.Core.dll').write_bytes(original)
            if target=='selection':(sdk.parent/'11.0.101').rmdir()

    def test_equal_length_restored_mtime_task_and_metadata_changes_refuse(self):
        captured=B.capture(self.root,self.data);dotnet,sdk,runtime=self.fake_installation()
        for path in (sdk/'Microsoft.Build.Tasks.Core.dll',sdk/'MSBuild.runtimeconfig.json'):
            calls=[];original=path.read_bytes();before=path.stat()
            def mutate(stage,*_):
                if stage=='restore':
                    path.write_bytes(b'X'+original[1:])
                    os.utime(path,ns=(before.st_atime_ns,before.st_mtime_ns))
                    after=path.stat()
                    self.assertEqual((before.st_ino,before.st_size,before.st_mtime_ns),
                                     (after.st_ino,after.st_size,after.st_mtime_ns))
                    self.assertNotEqual(before.st_ctime_ns,after.st_ctime_ns)
            with self.subTest(path=path), mock.patch.object(B.shutil,'which',return_value=str(dotnet)), \
                 mock.patch.object(T.Control,'bounded_process',side_effect=self.fixture_child(dotnet,sdk,runtime,calls,mutate)):
                with self.assertRaises(B.Refusal):B.execute(captured,'a'*64,'b'*64,'-')
            self.assertNotIn('plan',calls)
            path.write_bytes(original)

    def test_late_nested_sdk_wildcard_import_refuses_before_restore(self):
        captured=B.capture(self.root,self.data);dotnet,sdk,runtime=self.fake_installation();calls=[]
        nested=sdk/'Current/Imports/Microsoft.Common.targets/ImportBefore'
        def mutate(stage,*_):
            if stage=='preprocess':
                before=nested.stat()
                (nested/'Late.targets').write_text('<Project />')
                os.utime(nested,ns=(before.st_atime_ns,before.st_mtime_ns))
        with mock.patch.object(B.shutil,'which',return_value=str(dotnet)), \
             mock.patch.object(T.Control,'bounded_process',side_effect=self.fixture_child(dotnet,sdk,runtime,calls,mutate)):
            with self.assertRaises(B.Refusal):B.execute(captured,'a'*64,'b'*64,'-')
        self.assertEqual(['sdk','preprocess'],calls)

    def test_capability_cannot_be_transferred_or_reused_for_another_invocation(self):
        captured=B.capture(self.root,self.data);dotnet,sdk,runtime=self.fake_installation();calls=[]
        with mock.patch.object(B.shutil,'which',return_value=str(dotnet)), \
             mock.patch.object(T.Control,'bounded_process',side_effect=self.fixture_child(dotnet,sdk,runtime,calls)):
            first=B.execute(captured,'a'*64,'b'*64,'-')
            self.assertTrue(B.proved(first,captured=captured))
            self.assertFalse(B.proved(first,captured=B.Capture(captured.files,captured.hashes)))
            for content in (bytes(first),b'unrelated owner bytes'):
                transferred=B.OwnerOutput(content);transferred.proof=first.proof;transferred._context=first._context
                self.assertFalse(B.proved(transferred))
            second=B.execute(captured,'a'*64,'c'*64,'-')
            self.assertTrue(B.proved(second));self.assertFalse(B.proved(first))
            second.proof=first.proof
            self.assertFalse(B.proved(second))

    def test_missing_forged_stale_and_incomplete_stage_observations_stop_successors(self):
        captured=B.capture(self.root,self.data)
        dotnet,sdk,runtime=self.fake_installation()
        (sdk/'Unobserved.props').write_text('<Project />')
        cases=('missing-trace','stale-invocation','incomplete','network','unknown-task','unknown-source',
               'response','missing-field','missing-assembly','no-compiler','post-producer-tamper','late-import')
        for case in cases:
            calls=[]
            def change(stage,scratch,receipt,*_):
                if stage=='restore' and case=='unknown-task':
                    (scratch/'proof/restore.tasks').write_text('Using "Unknown" task from assembly "/outside/task.dll".')
                if stage=='plan' and case in ('unknown-source','response','missing-field'):
                    path=scratch/'proof/plan.items';text=path.read_text()
                    if case=='unknown-source':text+='source|/outside/unbound.cs\n'
                    if case=='response':text=text.replace('response|','response|/outside/poison.rsp')
                    if case=='missing-field':text=text.replace('response|\n','')
                    path.write_text(text)
                if stage=='plan' and case=='late-import':
                    with (scratch/'proof/plan.items').open('a') as output:output.write('import|'+str(sdk/'Unobserved.props')+'\n')
                if stage=='plan' and case=='post-producer-tamper':(scratch/'obj/generated.cs').write_bytes(b'changed after completed child')
                if stage=='build' and case=='missing-assembly':(scratch/'bin/ProductionProfileBridge.dll').unlink()
                if stage=='build' and case=='no-compiler':
                    receipt['executions']=receipt['executions'][:1]
                    (scratch/'proof/build.trace').write_text(json.dumps(receipt))
                if stage=='sdk' and case in ('missing-trace','stale-invocation','incomplete','network'):
                    path=scratch/'proof/sdk.trace'
                    if case=='missing-trace':path.unlink();return
                    if case=='stale-invocation':receipt['invocation']='0'*64
                    if case=='incomplete':receipt['complete']=False
                    if case=='network':receipt['network_attempts']=1
                    path.write_text(json.dumps(receipt))
            with self.subTest(case=case), mock.patch.object(B.shutil,'which',return_value=str(dotnet)), \
                 mock.patch.object(T.Control,'bounded_process',side_effect=self.fixture_child(dotnet,sdk,runtime,calls,change)):
                with self.assertRaises(B.Refusal):B.execute(captured,'a'*64,'b'*64,'-')
            self.assertNotIn('run',calls)
            if case in ('missing-trace','stale-invocation','incomplete','network'):self.assertEqual(['sdk'],calls)
            if case=='unknown-task':self.assertNotIn('plan',calls)
            if case in ('unknown-source','response','missing-field','post-producer-tamper','late-import'):self.assertNotIn('build',calls)

    def test_private_or_sdk_paths_cannot_inject_msbuild_properties(self):
        captured=B.capture(self.root,self.data);dotnet,_,_=self.fake_installation()
        unsafe=self.base/'private;CustomBeforeMicrosoftCommonProps=poison'
        unsafe.mkdir()
        with mock.patch.object(B.shutil,'which',return_value=str(dotnet)), \
             mock.patch.object(B.tempfile,'TemporaryDirectory',return_value=nullcontext(str(unsafe))):
            self.assert_refused_before_child(lambda:B.execute(captured,'a'*64,'b'*64,'-'))
        executable=unsafe/'dotnet';executable.write_bytes(b'installed identity')
        with mock.patch.object(B.shutil,'which',return_value=str(executable)):
            self.assert_refused_before_child(lambda:B.execute(captured,'a'*64,'b'*64,'-'))

    def test_missing_advertising_policy_guard_refuses_before_preflight(self):
        captured=B.capture(self.root,self.data)
        dotnet,_,_=self.fake_installation();environment=B.fixed_environment
        def incomplete(scratch,dotnet):
            result=environment(scratch,dotnet)
            del result['DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE']
            return result
        with mock.patch.object(B.shutil,'which',return_value=str(dotnet)), \
             mock.patch.object(B,'fixed_environment',side_effect=incomplete), \
             mock.patch.object(T.Control,'bounded_process') as child:
            with self.assertRaises(B.Refusal) as caught:B.execute(captured,'a'*64,'b'*64,'-')
            self.assertEqual(B.PROOF_UNAVAILABLE,caught.exception.code)
            child.assert_not_called()

    def test_unavailable_tracing_bounded_failure_and_noop_cannot_pass(self):
        captured=B.capture(self.root,self.data)
        dotnet,_,_=self.fake_installation()
        with mock.patch.object(B.shutil,'which',return_value=None):
            self.assert_refused_before_child(lambda:B.execute(captured,'a'*64,'b'*64,'-'))
        for effect in (None,T.Refusal('PROFILE_BRIDGE_UNAVAILABLE','bounded')):
            with mock.patch.object(B.shutil,'which',return_value=str(dotnet)), \
                 mock.patch.object(T.Control,'bounded_process',side_effect=effect,return_value=b'') as child:
                with self.assertRaises(B.Refusal):B.execute(captured,'a'*64,'b'*64,'-')
                self.assertEqual(1,child.call_count)

    def test_generated_input_mutation_is_caught_before_build_launch(self):
        captured=B.capture(self.root,self.data)
        dotnet,sdk,runtime=self.fake_installation()
        calls=[]; checks=[];original=B.verify
        def verify(captured,snapshot):
            original(captured,snapshot)
            if (snapshot.parent/'proof/plan.items').exists():
                checks.append(True)
                if len(checks)==2:
                    (snapshot.parent/'obj/generated.cs').write_bytes(b'tampered after producer observation')
        with mock.patch.object(B.shutil,'which',return_value=str(dotnet)), \
             mock.patch.object(B,'verify',side_effect=verify), \
             mock.patch.object(T.Control,'bounded_process',side_effect=self.fixture_child(dotnet,sdk,runtime,calls)):
            with self.assertRaises(B.Refusal):B.execute(captured,'a'*64,'b'*64,'-')
        self.assertEqual(['sdk','preprocess','evaluate','restore','plan'],calls)

    def test_added_private_runtime_input_is_rejected_before_owner_launch(self):
        captured=B.capture(self.root,self.data)
        dotnet,sdk,runtime=self.fake_installation();calls=[];original=B.verify;checks=[]
        def verify(captured,snapshot):
            original(captured,snapshot)
            if (snapshot.parent/'proof/build.items').exists():
                checks.append(True)
                if len(checks)==2:
                    (snapshot.parent/'bin/ProductionProfileBridge.runtimeconfig.dev.json').write_bytes(b'{"unbound":true}')
        with mock.patch.object(B.shutil,'which',return_value=str(dotnet)), \
             mock.patch.object(B,'verify',side_effect=verify), \
             mock.patch.object(T.Control,'bounded_process',side_effect=self.fixture_child(dotnet,sdk,runtime,calls)):
            with self.assertRaises(B.Refusal):B.execute(captured,'a'*64,'b'*64,'-')
        self.assertEqual(['sdk','preprocess','evaluate','restore','plan','build'],calls)

    def test_unknown_preflight_task_never_reaches_restore(self):
        captured=B.capture(self.root,self.data)
        dotnet,sdk,runtime=self.fake_installation();calls=[]
        def poison(stage,scratch,*_):
            if stage=='preprocess':
                path=scratch/'proof/preprocessed.xml'
                path.write_text(path.read_text().replace(str(sdk/'Microsoft.Build.Tasks.Core.dll'),'/outside/task.dll'))
        with mock.patch.object(B.shutil,'which',return_value=str(dotnet)), \
             mock.patch.object(T.Control,'bounded_process',side_effect=self.fixture_child(dotnet,sdk,runtime,calls,poison)):
            with self.assertRaises(B.Refusal):B.execute(captured,'a'*64,'b'*64,'-')
        self.assertNotIn('restore',calls)

    def test_compiler_response_rejects_unobserved_paths_response_poison_and_omissions(self):
        scratch=self.base/'private'
        source=str(scratch/'snapshot'/B.PROGRAM)
        reference=str(self.base/'reference.dll')
        analyzer=str(self.base/'analyzer.dll')
        config=str(self.base/'generated.editorconfig')
        plan=('source|'+source+'\nreference|'+reference+'\nanalyzer|'+analyzer+'\nconfig|'+config+'\n').encode()
        response=('/reference:'+reference+' /analyzer:'+analyzer+' /analyzerconfig:'+config+' '+source).encode()
        B.verify_compiler_response(response,scratch,plan)
        for changed in (response+b' @/outside/poison.rsp',response+b' /resource:/outside/payload',
                        response.replace(reference.encode(),b'/outside/ref.dll'),response.replace(source.encode(),b'')):
            with self.subTest(response=changed), self.assertRaises(B.Refusal):
                B.verify_compiler_response(changed,scratch,plan)


if __name__ == '__main__':
    unittest.main()

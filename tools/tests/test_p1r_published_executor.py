"""Executor tooling controls; no fixture in this suite grants operational qualification."""
import base64
import copy
import contextlib
import importlib.util
import io
import json
import os
import signal
import threading
import time
from pathlib import Path
import subprocess
import sys
import shutil
import tempfile
import unittest
import uuid
from unittest import mock

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import p1r_evidence_storage as storage
import p1r_published_executor as executor
import p1r_published_qualification as qualification
import p1r_qualification as preparation
import p1r_qualification_runtime as runtime
from p1r_isolation_fixture import separate_inputs, separate_source_fixture


def verifier_fixture(head=12, floor=1, snapshot=True, proof=False, suffix=""):
    raw = {}
    for tenant, end in (("tenant-a", head), ("tenant-b", 3)):
        first = floor if tenant == "tenant-a" else 1
        for sequence in range(first, end + 1):
            event = {"messageId": f"message-{tenant}-{sequence}{suffix}", "sequenceNumber": sequence,
                     "tenantId": tenant, "domain": "counter", "aggregateId": "fixture", "aggregateType": "Counter",
                     "timestamp": "2026-01-01T00:00:00.1234567+00:00", "globalPosition": sequence,
                     "eventTypeName": "CounterIncremented", "metadataVersion": 1, "serializationFormat": "json", "payload": "e30="}
            raw[f"{tenant}:counter:fixture:events:{sequence}"] = executor.canonical(event)
        metadata = {"currentSequence": end, "retainedFloor": first, "lastModified": "2026-01-01T00:00:00Z"}
        if proof and tenant == "tenant-a":
            source = {"codecVersion": 1, "tenantId": tenant, "domain": "counter", "aggregateId": "fixture",
                "sourceIncarnation": "01ARZ3NDEKTSV4RRFFQ69G5FAV", "headSequence": head,
                "headMessageId": f"message-{tenant}-{head}{suffix}", "retainedFloor": first, "erased": False}
            all_events = verifier_fixture(head=head, floor=1, snapshot=False, suffix=suffix)[0]
            source["cumulativeDigest"] = executor.source_proof_digest({int(key.rsplit(':', 1)[1]): data
                for key, data in all_events.items() if tenant in key and ':events:' in key}, source)
            metadata["sourceProof"] = source
        raw[f"{tenant}:counter:fixture:metadata"] = executor.canonical(metadata)
    if snapshot:
        raw["tenant-a:counter:fixture:snapshot"] = executor.canonical({"sequenceNumber": 9,
            "snapshotEnvelopeVersion": 1, "state": {"count": 9}, "createdAt": "2026-01-01T00:00:00Z"})
    return raw, verifier_rows(raw)


def verifier_rows(raw):
    rows = []
    for key, data in sorted(raw.items()):
        value = json.loads(data)
        kind = "metadata" if key.endswith(':metadata') else "snapshot" if key.endswith(':snapshot') else "event"
        rows.append({"key": key, "tenant": "tenant-a" if "tenant-a" in key else "tenant-b", "kind": kind,
                     "sequence": value.get('currentSequence') if kind == 'metadata' else value.get('sequenceNumber'),
                     "floor": value.get('retainedFloor', 1) if kind == 'metadata' else None, "sha256": executor.digest(data)})
    return rows


class SeparateRootExecutorTests(unittest.TestCase):
    def testSuccessfulObservationEmitsToolingPinsAndAdmitsTheSelectedReleaseRoots(self):
        with tempfile.TemporaryDirectory() as temporary, separate_source_fixture(temporary) as f:
            selected = separate_inputs(f)
            for version in qualification.COMPARISON_VERSIONS:
                f.git(f.tooling,'tag','v'+version)
            f.git(f.builds,'tag','v4.30.1')
            def archive_bytes(identifier,version): return (identifier+':'+version).encode()
            for package in selected['candidate']['packages']:
                package['archive_sha256'] = preparation.digest(archive_bytes(package['id'],'3.125.1'))
            worker = executor.Executor.__new__(executor.Executor)
            worker.output,worker.commands,worker.result = f.root/'observation',[],{}
            worker.output.mkdir()
            worker.source_root,worker.source_builds_root = f.source,f.source_builds
            worker.source_binding = preparation.source_binding(f.tooling,f.pins,f.source,f.source_builds)
            worker.save = mock.Mock()
            def build(version):
                root = worker.output/version
                for identifier in executor.PACKAGE_IDS:
                    base = root/'packages'/identifier.lower()/version
                    base.mkdir(parents=True)
                    (base/(identifier.lower()+'.'+version+'.nupkg')).write_bytes(archive_bytes(identifier,version))
                    preparation.write_json(base/'.nupkg.metadata',{'contentHash':base64.b64encode(b'x'*64).decode()})
                return root
            def inspect(path,data):
                identifier = next(name for name in executor.PACKAGE_IDS if name.lower() == path.parent.parent.name)
                version = path.parent.name
                return ({'valid':True,'id':identifier,'version':version,'repository_commit':
                         f.release if version == '3.125.1' else f.pins['commit']},
                        {'signature':True,'synthetic_marker':False},{})
            worker.build_consumers = mock.Mock(side_effect=build)
            worker.run = lambda *args,**kwargs:worker.commands.append({'output':qualification.REPOSITORY_SIGNATURE})
            with mock.patch.object(executor,'ROOT',f.tooling), mock.patch.object(qualification,'inspect_archive',side_effect=inspect):
                worker.observe_inputs({'candidate_proposed':selected['candidate'],'tooling':f.pins},'temporary fixture only')
                emitted = preparation.read_json(worker.output/'owner-inputs.json')
                self.assertEqual(emitted['tooling'],f.pins)
                self.assertEqual(emitted['candidate'],selected['candidate'])
                self.assertIsNone(emitted['rollback'])
                executor.validate_execution_inputs(emitted,f.source,f.source_builds)
                self.assertEqual(worker.result['inputs_sha256'],preparation.digest((worker.output/'owner-inputs.json').read_bytes()))

    def cli(self):
        spec = importlib.util.spec_from_file_location('binding_executor_cli', preparation.ROOT/'tools/p1r-published-executor.py')
        module = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(module)
        return module

    def testBothCliEntryPathsValidateAndForwardSeparateRoots(self):
        cli = self.cli()
        for entry in ('run','observe'):
            for private in (False,True):
                with self.subTest(entry=entry,private=private), tempfile.TemporaryDirectory() as temporary, separate_source_fixture(temporary) as f:
                    inputs = separate_inputs(f)
                    planning = {'candidate_proposed':inputs['candidate'],'tooling':f.pins}
                    selected = f.root/'selected.json'
                    preparation.write_json(selected,inputs if entry == 'run' else planning)
                    args = [entry,'--out',str(f.root/'output')]
                    args += ['--inputs',str(selected)] if entry == 'run' else ['--planning-observations',str(selected),'--selection-reference','fixture']
                    suffix = ['--source-root',str(f.source),'--source-builds-root',str(f.source_builds)]
                    import p1r_isolation as isolation
                    with mock.patch.object(executor,'ROOT',f.tooling), \
                            mock.patch.object(cli,'Executor') as worker, \
                            mock.patch.object(isolation,'IsolatedRunner') as outer, \
                            mock.patch.object(isolation,'private_proof'), \
                            mock.patch.dict(os.environ,{'HEXALITH_P1R_ISOLATION':'fixture'} if private else {},clear=True), \
                            contextlib.redirect_stdout(io.StringIO()):
                        worker.return_value.errors = []
                        outer.return_value.run.return_value = 0
                        outer.return_value.result = {'command_stdout':'','errors':[],'passed':True}
                        self.assertEqual(cli.main(args+suffix),0)
                        if private:
                            worker.assert_called_once()
                            (worker.return_value.execute if entry == 'run' else worker.return_value.observe_inputs).assert_called_once()
                        else:
                            outer.assert_called_once()
                            self.assertEqual(outer.return_value.run.call_args.args[0][-4:],suffix)
                            worker.assert_not_called()

    def testBothCliEntryPathsRefuseBeforeAnyOperationalBoundary(self):
        cli = self.cli()
        import p1r_isolation as isolation
        for entry in ('run','observe'):
            for defect in ('tooling-pin','tooling-dirty','source-dirty','tooling-builds','source-builds','tag','missing-source','unknown-successor'):
                with self.subTest(entry=entry,defect=defect), tempfile.TemporaryDirectory() as temporary, separate_source_fixture(temporary) as f:
                    inputs = separate_inputs(f)
                    source, builds = f.source, f.source_builds
                    if defect == 'tooling-pin': inputs['tooling']['commit'] = 'a'*40
                    elif defect == 'tooling-dirty': (f.tooling/'src/Runtime.cs').write_text('// changed\n')
                    elif defect == 'source-dirty': (f.source/'src/Runtime.cs').write_text('// changed\n')
                    elif defect == 'tooling-builds': inputs['tooling']['builds_commit'] = f.release_builds
                    elif defect == 'source-builds': builds = f.builds
                    elif defect == 'tag': f.git(f.source,'tag','-d','v3.125.1')
                    elif defect == 'unknown-successor': inputs['candidate'].update(version='3.125.2',tag='v3.125.2')
                    selected = f.root/'selected.json'
                    preparation.write_json(selected,inputs if entry == 'run' else {'candidate_proposed':inputs['candidate'],'tooling':inputs['tooling']})
                    args = [entry,'--out',str(f.root/'output')]
                    args += ['--inputs',str(selected)] if entry == 'run' else ['--planning-observations',str(selected),'--selection-reference','fixture']
                    if defect != 'missing-source': args += ['--source-root',str(source),'--source-builds-root',str(builds)]
                    with mock.patch.object(executor,'ROOT',f.tooling), mock.patch.object(cli,'Executor') as worker, \
                            mock.patch.object(isolation,'IsolatedRunner') as outer, contextlib.redirect_stderr(io.StringIO()):
                        self.assertEqual(cli.main(args),2)
                        worker.assert_not_called()
                        outer.assert_not_called()
                    self.assertFalse((f.root/'output').exists())

    def testObservationAndExecutionKeepBothIdentitiesAtConsumerBoundary(self):
        for entry in ('run','observe'):
            with self.subTest(entry=entry), tempfile.TemporaryDirectory() as temporary, separate_source_fixture(temporary) as f:
                inputs = separate_inputs(f)
                binding = preparation.source_binding(f.tooling,f.pins,f.source,f.source_builds)
                worker = executor.Executor.__new__(executor.Executor)
                worker.source_binding, worker.source_root, worker.source_builds_root = binding,f.source,f.source_builds
                worker.inputs, worker.candidate = inputs,'3.125.1'
                worker.build_consumers = mock.Mock(side_effect=RuntimeError('consumer boundary reached'))
                worker.cleanup = mock.Mock()
                worker.save = mock.Mock()
                worker.pending_restore = None
                worker.versions = ('3.110.0','3.70.1','3.125.1')
                with mock.patch.object(executor,'ROOT',f.tooling), mock.patch.object(executor,'provisional_successor_execution_policy',return_value={}):
                    if entry == 'observe':
                        with self.assertRaisesRegex(RuntimeError,'consumer boundary reached'):
                            worker.observe_inputs({'candidate_proposed':inputs['candidate'],'tooling':f.pins},'fixture')
                    else:
                        with self.assertRaisesRegex(RuntimeError,'consumer boundary reached'): worker.execute()
                        worker.cleanup.assert_called_once()
                self.assertEqual(worker.source_binding,binding)

    def testBothSourceRoutesSelectTheTaggedSdkBeforeEvaluationOrRestore(self):
        class ConsumerBoundaryReached(Exception):
            pass
        for route in ('candidate_source', 'runtime_source'):
            for source in (False, True):
                with self.subTest(route=route, source=source), tempfile.TemporaryDirectory() as temporary:
                    root = Path(temporary)
                    tagged = root/'tagged'
                    tagged.mkdir()
                    selected_sdk = b'{"sdk":{"version":"10.0.401","rollForward":"disable"}}\n'
                    (tagged/'global.json').write_bytes(selected_sdk)
                    consumers = root/'consumer-template'
                    consumers.mkdir()
                    worker = executor.Executor.__new__(executor.Executor)
                    worker.output, worker.source_root, worker.candidate = root/'execution', tagged, '3.125.1'
                    worker.explicit_source_root = route == 'candidate_source'
                    worker.source_binding = {} if route == 'candidate_source' else {
                        'runtime_source': {'repository':str(tagged), 'builds':{'path':str(root/'builds')}}}
                    worker.check_sources = mock.Mock()
                    role = 'source' if source else 'candidate'
                    def boundary(*args, **kwargs):
                        self.assertEqual((worker.output/'consumers'/role/'global.json').read_bytes(), selected_sdk)
                        raise ConsumerBoundaryReached()
                    worker.run = mock.Mock(side_effect=boundary)
                    worker.evaluate_source_consumers = mock.Mock(side_effect=boundary)
                    with mock.patch.object(executor,'CONSUMERS',consumers), \
                            self.assertRaises(ConsumerBoundaryReached):
                        worker.build_consumers('3.125.1',source=source)
                    self.assertEqual(worker.run.call_count + worker.evaluate_source_consumers.call_count, 1)

    def testSourceConsumersUseVerifiedReferencesCatalogAndVersionBeforeRestore(self):
        with tempfile.TemporaryDirectory() as temporary, separate_source_fixture(temporary) as f:
            worker = executor.Executor.__new__(executor.Executor)
            worker.source_binding = preparation.source_binding(f.tooling,f.pins,f.source,f.source_builds)
            worker.source_root, worker.source_builds_root = f.source,f.source_builds
            worker.output, worker.candidate, worker.commands = f.root/'execution','3.125.1',[]
            references = [f.source/'src'/identifier/(identifier+'.csproj') for identifier in executor.PACKAGE_IDS]
            def run(argv, **kwargs):
                if argv[1] == 'restore': raise RuntimeError('restore boundary reached')
                project = Path(argv[2])
                if '-preprocess' in argv:
                    data = '\n'+str(f.source_builds/'Props/Directory.Packages.props')+'\n'
                    data += '/usr/share/dotnet/sdk/10.0.401/Current/Microsoft.Common.props\n'
                    data += str(kwargs['cwd']/'packages/restored/1.0/build/restored.targets')+'\n'
                else:
                    data = json.dumps({'Properties':{'MSBuildProjectFullPath':str(project),
                        'EventStoreVersion':'3.125.1','HexalithEventStoreVersion':'3.125.1','Version':'3.125.1'},
                        'Items':{'ProjectReference':[{'FullPath':str(path)} for path in references]
                                 if not project.is_relative_to(f.source) else []}})
                worker.commands.append({'argv':list(map(str,argv)),'exit_code':0,'output':data,
                                        'output_sha256':preparation.digest(data.encode())})
                return data.encode()
            worker.run = mock.Mock(side_effect=run)
            with self.assertRaisesRegex(RuntimeError,'restore boundary reached'):
                worker.build_consumers('3.125.1',source=True)
            self.assertEqual(len(worker.source_evaluations),8)
            consumer_root = worker.output/'consumers/source'
            qualification.validate_source_evaluations({'source_evaluations':worker.source_evaluations},worker.source_binding,consumer_root)
            smaller_domain = copy.deepcopy(worker.source_evaluations)
            value = json.loads(smaller_domain[1]['evaluation']['output'])
            value['Items']['ProjectReference'] = [row for row in value['Items']['ProjectReference']
                if Path(row['FullPath']).stem in ('Hexalith.EventStore.Contracts','Hexalith.EventStore.DomainService')]
            smaller_domain[1]['evaluation']['output'] = json.dumps(value)
            smaller_domain[1]['evaluation']['output_sha256'] = preparation.digest(smaller_domain[1]['evaluation']['output'].encode())
            qualification.validate_source_evaluations({'source_evaluations':smaller_domain},worker.source_binding,consumer_root)
            properties = worker.run.call_args.args[0]
            self.assertIn('-p:EventStoreSourceRoot='+str(f.source),properties)
            self.assertIn('-p:Hexalith1BuildPackageProps='+str(f.source_builds/'Props/Directory.Packages.props'),properties)
            self.assertNotIn('-p:EventStoreSourceRoot='+str(f.tooling),properties)
            for mutation in ('reference','catalog','source-catalog','version','arguments','omit','all-references',
                             'consumer-root','slash-property','response-file','extra-target','external-target'):
                changed = copy.deepcopy(worker.source_evaluations)
                if mutation == 'omit': changed.pop()
                elif mutation == 'all-references':
                    changed = changed[:3]
                    for item in changed:
                        value = json.loads(item['evaluation']['output'])
                        value['Items']['ProjectReference'] = []
                        item['evaluation']['output'] = json.dumps(value)
                        item['evaluation']['output_sha256'] = preparation.digest(item['evaluation']['output'].encode())
                elif mutation == 'consumer-root':
                    for item in changed[:3]:
                        old = item['project']
                        item['project'] = old.replace(str(consumer_root),str(f.root/'other/consumers/source'))
                        for command in ('imports','evaluation'):
                            item[command]['argv'][2] = item['project']
                        value = json.loads(item['evaluation']['output'])
                        value['Properties']['MSBuildProjectFullPath'] = item['project']
                        item['evaluation']['output'] = json.dumps(value)
                        item['evaluation']['output_sha256'] = preparation.digest(item['evaluation']['output'].encode())
                elif mutation in ('slash-property','response-file','extra-target'):
                    changed[0]['evaluation']['argv'].append({'slash-property':'/p:UseCurrentSource=false',
                        'response-file':'@overrides.rsp','extra-target':'-target:Build'}[mutation])
                elif mutation == 'external-target':
                    changed[0]['imports']['output'] += '/tmp/unrelated.targets\n'
                    changed[0]['imports']['output_sha256'] = preparation.digest(changed[0]['imports']['output'].encode())
                elif mutation in ('catalog','source-catalog'):
                    changed[0]['imports']['output'] += str((f.builds/'Props' if mutation == 'catalog' else f.source/'obj')/'Directory.Packages.props')+'\n'
                    changed[0]['imports']['output_sha256'] = preparation.digest(changed[0]['imports']['output'].encode())
                elif mutation == 'arguments':
                    command = changed[0]['evaluation']['argv']
                    index = next(i for i,arg in enumerate(command) if arg.startswith('-p:EventStoreSourceRoot='))
                    command[index] = '-p:EventStoreSourceRoot='+str(f.tooling)
                else:
                    value = json.loads(changed[0]['evaluation']['output'])
                    if mutation == 'reference': value['Items']['ProjectReference'][0]['FullPath'] = str(f.tooling/'src/wrong.csproj')
                    else: value['Properties']['HexalithEventStoreVersion'] = '3.124.0'
                    changed[0]['evaluation']['output'] = json.dumps(value)
                    changed[0]['evaluation']['output_sha256'] = preparation.digest(changed[0]['evaluation']['output'].encode())
                with self.subTest(mutation=mutation), self.assertRaises(preparation.InvalidPacket):
                    qualification.validate_source_evaluations({'source_evaluations':changed},worker.source_binding,consumer_root)
            # The published restore receives neither source references nor catalog overrides.
            worker.run.reset_mock()
            with self.assertRaisesRegex(RuntimeError,'restore boundary reached'): worker.build_consumers('3.125.1')
            props = worker.run.call_args.args[0]
            self.assertFalse(any('SourceRoot=' in str(value) or 'BuildPackage' in str(value) or 'UseCurrentSource=' in str(value) for value in props))

    def testPostRestoreImportsAreReevaluatedBeforeAnyDebugBuild(self):
        for substituted in (False,True):
            with self.subTest(substituted=substituted), tempfile.TemporaryDirectory() as temporary, separate_source_fixture(temporary) as f:
                worker = executor.Executor.__new__(executor.Executor)
                worker.source_binding = preparation.source_binding(f.tooling,f.pins,f.source,f.source_builds)
                worker.source_root,worker.source_builds_root = f.source,f.source_builds
                worker.output,worker.candidate,worker.commands = f.root/'execution','3.125.1',[]
                restored, built = [],[]
                source_projects = [f.source/'src'/name/(name+'.csproj') for name in executor.PACKAGE_IDS]
                def run(argv, **kwargs):
                    if argv[1] == 'build':
                        built.append(argv)
                        raise RuntimeError('Debug build boundary reached')
                    if argv[1] == 'restore':
                        restored.append(argv)
                        data = 'restored'
                    elif '-preprocess' in argv:
                        imported = [f.source_builds/'Props/Directory.Packages.props']
                        if restored:
                            imported.append(Path('/tmp/restore-injected.targets') if substituted else kwargs['cwd']/'host/obj/generated.targets')
                        data = '\n'+'\n'.join(map(str,imported))+'\n'
                    else:
                        project = Path(argv[2])
                        data = json.dumps({'Properties':{'MSBuildProjectFullPath':str(project),'EventStoreVersion':'3.125.1',
                            'HexalithEventStoreVersion':'3.125.1','Version':'3.125.1'},'Items':{'ProjectReference':
                            [{'FullPath':str(path)} for path in source_projects] if not project.is_relative_to(f.source) else []}})
                    worker.commands.append({'argv':list(map(str,argv)),'exit_code':0,'output':data,'output_sha256':preparation.digest(data.encode())})
                    return data.encode()
                worker.run = run
                with self.assertRaisesRegex(preparation.InvalidPacket if substituted else RuntimeError,
                                            'external source import' if substituted else 'Debug build boundary reached'):
                    worker.build_consumers('3.125.1',source=True)
                self.assertEqual(len(restored),3)
                self.assertEqual(len(built),0 if substituted else 1)
                if not substituted:
                    self.assertTrue(all('generated.targets' in item['imports']['output'] for item in worker.source_evaluations))


class QualifiedSnapshotWitnessTests(unittest.TestCase):
    candidate_version = '3.121.0'

    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)

    def testRequirementUsesSelectedCaseAndWriterRatherThanOptionalProof(self):
        for lane in ('snapshot-tail','retained-covered','retained-uncovered','metadata-write'):
            for writer in (self.candidate_version,'source'):
                with self.subTest(lane=lane,writer=writer):
                    self.assertTrue(executor.witnesses.requires_qualified_snapshot(self.candidate_version,lane,writer+'-to-3.110.0'))
            for writer in ('3.110.0','3.70.1','3.122.2',
                           *(version for version in ('3.121.0','3.122.1','3.125.0','3.125.1','3.126.0') if version != self.candidate_version)):
                with self.subTest(lane=lane,writer=writer):
                    self.assertFalse(executor.witnesses.requires_qualified_snapshot(self.candidate_version,lane,writer+'-to-'+self.candidate_version))
        self.assertTrue(executor.witnesses.requires_qualified_snapshot(self.candidate_version,'post-upgrade-restore','fresh-backup-restore-append-restart'))
        for version in ('3.119.0','3.122.0','3.122.2','3.125.2','3.126.1','3.127.0'):
            self.assertFalse(executor.witnesses.requires_qualified_snapshot(version,'post-upgrade-restore','fresh-backup-restore-append-restart'))
            self.assertFalse(executor.witnesses.requires_qualified_snapshot(version,'snapshot-tail','source-to-3.110.0'))

    def validate(self,fixture,lane='post-upgrade-restore'):
        row,evidence,binding,sources = fixture
        executor.witnesses.validate_case(row,evidence,binding,lane,sources,candidate_version=self.candidate_version)

    def testEveryRequiredCaseExecutesActualStrictValidatorWithSelectedPhysicalWriter(self):
        for lane in ('post-upgrade-restore','snapshot-tail','retained-covered','retained-uncovered','metadata-write'):
            for writer in ((self.candidate_version,) if lane == 'post-upgrade-restore' else (self.candidate_version,'source')):
                with self.subTest(lane=lane,writer=writer):
                    fixture = self.fixture(writer_version=writer,lane=lane)
                    actual_validator = executor.witnesses.validate_qualified_snapshot
                    with mock.patch.object(executor.witnesses,'validate_qualified_snapshot',wraps=actual_validator) as validator:
                        self.validate(fixture,lane)
                    validator.assert_called_once()
                    self.assertEqual(validator.call_args.args[-1],writer)
                    changed = copy.deepcopy(fixture)
                    del changed[1]['observations']['seed_snapshot']
                    with self.assertRaisesRegex(preparation.InvalidPacket,'genuine seed'):
                        self.validate(changed,lane)

    def testSourceSeedWitnessSubstitutionsRemainRejectedAfterRehashing(self):
        for mutation in ('source','envelope','probe','launch','provider'):
            fixture = self.fixture(writer_version='source',lane='snapshot-tail')
            with self.subTest(mutation=mutation):
                if mutation == 'source':
                    del fixture[1]['snapshot_fixture_source']
                elif mutation == 'envelope':
                    seed = fixture[1]['observations']['seed_snapshot']
                    seed['persisted_snapshot']['snapshotEnvelopeVersion'] = 0
                    self.change_output(fixture[1]['commands'][11],lambda value:value[0].__setitem__(1,seed['persisted_snapshot']))
                    for name,index in (('before_tail',12),('after_tail',16)):
                        next(item for item in seed[name] if item['kind']=='snapshot')['sha256'] = executor.digest(executor.canonical(seed['persisted_snapshot']))
                        fixture[1]['commands'][index]['output'] = executor.canonical(seed[name]).decode()
                        fixture[1]['commands'][index]['output_sha256'] = executor.digest(fixture[1]['commands'][index]['output'].encode())
                elif mutation == 'probe':
                    fixture[1]['commands'][5]['probe_binary']['configuration'] = 'Release'
                elif mutation == 'launch':
                    configuration = fixture[1]['configurations'][0]
                    configuration['version'] = self.candidate_version
                    for command in fixture[1]['commands']:
                        if command.get('runtime_configuration') == configuration['id']:
                            command['runtime_configuration_sha256'] = executor.digest(executor.canonical(configuration))
                else:
                    fixture[1]['commands'][11]['input_sha256'] = executor.digest(b'SELECT substituted')
                self.rehash_inventory(fixture)
                with self.assertRaises(preparation.InvalidPacket):
                    self.validate(fixture,'snapshot-tail')

    def testFullSuccessorAdmissionPassesIntactAndRejectsDeletedProof(self):
        fixture = self.fixture()
        self.validate(fixture)
        removed = copy.deepcopy(fixture)
        del removed[1]['observations']['seed_snapshot']
        with self.assertRaisesRegex(preparation.InvalidPacket,'genuine seed'):
            self.validate(removed)
        # Historical admission remains independent of the successor-only proof.
        executor.witnesses.validate_case(removed[0],removed[1],removed[2],'post-upgrade-restore',removed[3])
        for name in ('registration','manual','persisted_snapshot','before_tail','after_tail','tail'):
            changed = copy.deepcopy(fixture)
            del changed[1]['observations']['seed_snapshot'][name]
            with self.subTest(name=name), self.assertRaises(preparation.InvalidPacket):
                self.validate(changed)
        for identity in ('qualified-snapshot-created-by-published-actor','qualified-snapshot-prefix-coordinate-exact',
                         'qualified-snapshot-tail-coordinate-exact'):
            changed = copy.deepcopy(fixture)
            index = next(i for i,item in enumerate(changed[0]['checks']) if item['id'] == identity)
            del changed[0]['checks'][index]
            del changed[1]['check_witnesses'][index]
            changed[0]['assertions'] = preparation.checks_counter(changed[0]['checks'])
            with self.subTest(identity=identity), self.assertRaises(preparation.InvalidPacket):
                self.validate(changed)

    def testProviderBindsExactBase64SnapshotBytesIncludingCSharpPropertyOrder(self):
        fixture = self.fixture()
        seed = fixture[1]['observations']['seed_snapshot']
        raw = json.dumps(seed['persisted_snapshot'],separators=(',',':')).encode()
        self.assertNotEqual(raw,executor.canonical(seed['persisted_snapshot']))
        self.change_output(fixture[1]['commands'][11],lambda value:value[0].__setitem__(1,base64.b64encode(raw).decode()))
        for name,index in (('before_tail',12),('after_tail',16)):
            next(item for item in seed[name] if item['kind']=='snapshot')['sha256'] = executor.digest(raw)
            fixture[1]['commands'][index]['output'] = executor.canonical(seed[name]).decode()
            fixture[1]['commands'][index]['output_sha256'] = executor.digest(fixture[1]['commands'][index]['output'].encode())
        self.rehash_inventory(fixture)
        self.validate(fixture)
        self.change_output(fixture[1]['commands'][11],lambda value:value[0].__setitem__(1,
            base64.b64encode(executor.canonical(seed['persisted_snapshot'])).decode()))
        with self.assertRaisesRegex(preparation.InvalidPacket,'persisted inventory hash'):
            self.validate(fixture)

    def testChronologicalSeedCompletionCannotBeSubstituted(self):
        fixture = self.fixture()
        fixture[1]['commands'][6]['started_utc'] = '2026-10-14T00:00:07.500000+00:00'
        self.rehash_inventory(fixture)
        with self.assertRaisesRegex(preparation.InvalidPacket,'not chronological'):
            self.validate(fixture)

    def testServingBinariesMustPhysicallyExistAndMatchRetainedDigest(self):
        for project in ('Host','Domain'):
            for replacement in (None,b'replaced serving binary'):
                fixture = self.fixture()
                command = next(item for item in fixture[1]['commands'] if item.get('consumer_binary',{}).get('project')==project)
                path = Path(command['argv'][1])
                if replacement is None:
                    path.unlink()
                else:
                    path.write_bytes(replacement)
                with self.subTest(project=project,replacement=replacement), self.assertRaises((preparation.InvalidPacket,FileNotFoundError)):
                    self.validate(fixture)

    def testProviderMutationInsideGenuineSeedWindowIsRejectedButLaterDiagnosticsRemainAllowed(self):
        fixture = self.fixture()
        command = copy.deepcopy(fixture[1]['commands'][11])
        command.update(id=100,provider_sql='TRUNCATE TABLE state;',input_sha256=executor.digest(b'TRUNCATE TABLE state;'),
                       input_bytes=len(b'TRUNCATE TABLE state;'),output='',output_sha256=executor.digest(b''))
        fixture[1]['commands'].append(command)
        self.rehash_inventory(fixture)
        with self.assertRaisesRegex(preparation.InvalidPacket,'provider mutation|unapproved'):
            self.validate(fixture)
        command.update(started_utc='2026-10-14T00:00:22+00:00',finished_utc='2026-10-14T00:00:22.500000+00:00')
        self.validate(fixture)

    def testInventoryExecutableSubstitutionIsRejectedAfterRehashing(self):
        for executable in ('/bin/echo', '/substituted/python3', 'sh'):
            fixture = self.fixture()
            inventory = fixture[1]['commands'][12]
            self.assertEqual(inventory['argv'][0], fixture[2]['python']['path'])
            inventory['argv'][0] = executable
            self.rehash_inventory(fixture)
            with self.subTest(executable=executable), self.assertRaises(preparation.InvalidPacket):
                self.validate(fixture)

    def testCanonicalEmptyBodyReadinessGETAndFailedRetryAreAccepted(self):
        fixture = self.fixture()
        for command in fixture[1]['commands']:
            if command['argv'][:2] == ['HTTP','GET']:
                command['argv'].append('request-sha256='+executor.digest(b''))
        retry = copy.deepcopy(fixture[1]['commands'][13])
        retry.update(id=100,exit_code=124,started_utc='2026-10-14T00:00:15.250000+00:00',
                     finished_utc='2026-10-14T00:00:15.300000+00:00',output='URLError',
                     output_sha256=executor.digest(b'URLError'))
        fixture[1]['commands'].append(retry)
        self.rehash_inventory(fixture)
        self.validate(fixture)

    def testFourArgumentSeedGETRejectsAlteredHashExtraArgumentsAndForeignURLs(self):
        for mutation in ('hash','extra','foreign-ready','foreign-health','query','host'):
            fixture = self.fixture()
            command = copy.deepcopy(fixture[1]['commands'][13])
            command['id'] = 100
            command['argv'].append('request-sha256='+executor.digest(b''))
            if mutation == 'hash':
                command['argv'][3] = 'request-sha256='+executor.digest(b'changed')
            elif mutation == 'extra':
                command['argv'].append('unapproved-extra-argument')
            elif mutation == 'foreign-ready':
                command['argv'][2] = 'http://127.0.0.1:19999/ready'
            elif mutation == 'foreign-health':
                command['argv'][2] = 'http://127.0.0.1:19999/v1.0/healthz/outbound'
            elif mutation == 'query':
                command['argv'][2] += '?substituted=true'
            else:
                command['argv'][2] = command['argv'][2].replace('127.0.0.1','localhost')
            fixture[1]['commands'].append(command)
            self.rehash_inventory(fixture)
            with self.subTest(mutation=mutation), self.assertRaisesRegex(preparation.InvalidPacket,'unapproved|readiness'):
                self.validate(fixture)

    def testRestartedDomainReadinessGETRequiresItsBoundPhysicalLaunch(self):
        fixture = self.fixture()
        commands,configurations = fixture[1]['commands'],fixture[1]['configurations']
        host,sidecar = copy.deepcopy(commands[8]),copy.deepcopy(commands[9])
        host.update(id=100,started_utc='2026-10-14T00:00:14+00:00',finished_utc='2026-10-14T00:00:20+00:00',
                    output='Now listening on: http://127.0.0.1:10008')
        host['output_sha256'] = executor.digest(host['output'].encode())
        sidecar.update(id=101,started_utc='2026-10-14T00:00:14.200000+00:00',finished_utc='2026-10-14T00:00:20+00:00',
                       runtime_configuration=configurations[1]['id'],
                       runtime_configuration_sha256=executor.digest(executor.canonical(configurations[1])))
        for name,value in (('--app-port','10008'),('--dapr-http-port','10007')):
            sidecar['argv'][sidecar['argv'].index(name)+1] = value
        for command in (host,sidecar):
            command['process'] = {key:command['id'] for key in ('pid','pgid','session','start_ticks')}
            command['owned_processes'] = [command['process']]
            command['cleanup'][0]['before'] = [command['process']]
        commands.extend((host,sidecar))
        for identity,url in ((102,'http://127.0.0.1:10008/ready'),(103,'http://127.0.0.1:10007/v1.0/healthz/outbound')):
            command = copy.deepcopy(commands[13])
            command.update(id=identity,argv=['HTTP','GET',url,'request-sha256='+executor.digest(b'')],
                           started_utc='2026-10-14T00:00:14.500000+00:00',finished_utc='2026-10-14T00:00:14.600000+00:00')
            commands.append(command)
        self.rehash_inventory(fixture)
        self.validate(fixture)
        sidecar['runtime_configuration'] = configurations[0]['id']
        sidecar['runtime_configuration_sha256'] = executor.digest(executor.canonical(configurations[0]))
        with self.assertRaises(preparation.InvalidPacket):
            self.validate(fixture)

    def testShellMediatedProviderMutationInsideSeedIsRejected(self):
        for shell in ('sh', '/bin/bash', 'env', 'busybox', '/bin/toybox', 'timeout'):
            for outer_shell in (False, True):
                fixture = self.fixture()
                command = copy.deepcopy(fixture[1]['commands'][11])
                argv = [shell, '-c', 'psql -U postgres -d eventstore -c "TRUNCATE TABLE state"']
                if Path(shell).name in ('busybox', 'toybox'):
                    argv.insert(1, 'sh')
                elif shell == 'timeout':
                    argv[1:1] = ['5', 'sh']
                command.update(id=100, argv=argv if outer_shell else command['argv'][:4] + argv,
                               output='', output_sha256=executor.digest(b''))
                fixture[1]['commands'].append(command)
                self.rehash_inventory(fixture)
                with self.subTest(shell=shell, outer_shell=outer_shell), self.assertRaisesRegex(
                        preparation.InvalidPacket, 'provider mutation|shell-mediated'):
                    self.validate(fixture)

    def import_fixture(self,scope='published-package'):
        row,evidence,binding,sources = self.fixture()
        evidence['id'] = row['id']
        profile = {'runtime':'dapr','runtime_version':'1.18.2','backend':'state.postgresql',
                   'backend_image':executor.POSTGRES,'selected_by':'test-owner','reference':'synthetic control only'}
        for command in evidence['commands']:
            command['cwd'] = str(preparation.ROOT)
        template = (preparation.ROOT/'deploy/dapr/statestore-postgresql.yaml').read_text()
        for config in evidence['configurations']:
            config['runtime_version'] = profile['runtime_version']
            for file in config['files']:
                file['content'] = template if file.get('name') == 'state.yaml' else 'synthetic configuration'
                file['sha256'] = executor.digest(file['content'].encode())
                if file.get('name') == 'state.yaml':
                    file['credential_redacted'] = True
                    config['state_component_sha256'] = file['sha256']
            for command in evidence['commands']:
                if command.get('runtime_configuration') == config['id']:
                    command['runtime_configuration_sha256'] = executor.digest(executor.canonical(config))
        selection = {'candidate':{'version':self.candidate_version},'rollback':None,
            'comparisons':[{'version':version} for version in qualification.COMPARISON_VERSIONS],
            'selected_additions':[],'operational_profile':profile,'assertion_instrumentation':{'mechanism':executor.MECHANISM}}
        verified = {role:{'archives':{'Hexalith.EventStore.Server':str(index)*64},'dlls':{str(index+4)*64}}
                    for index,role in enumerate(('candidate',*('comparison-'+version for version in qualification.COMPARISON_VERSIONS)),1)}
        context = {'inputs':{'value':selection,'scope':'owner-selected','sha256':'1'*64},
                   'source_binding':binding,'verified':verified}
        executed = {'mechanism':executor.MECHANISM,'cases':[evidence],'executor_source':binding,'predicate_sources':sources,
                    'executor_source_sha256':executor.digest(json.dumps(binding,indent=2,sort_keys=True).encode()+b'\n')}
        receipt = {'schema':qualification.LANE_RECEIPT_SCHEMA,'id':'2'*32,'lane':'post-upgrade-restore','scope':scope,
            'fixture':{'id':'p1r-'+'b'*32,'synthetic':False},'inputs_sha256':'1'*64,'instrumentation':executor.MECHANISM,
            'profile':profile if scope=='operational' else None,'argv':['python3','synthetic control'],
            'cwd':str(preparation.ROOT),'started_utc':'2026-10-14T00:00:00+00:00','finished_utc':'2026-10-14T00:00:30+00:00',
            'exit_code':0,'identities':{'configuration':'Release',
                'packages':[{'id':key,'sha256':value} for entry in verified.values() for key,value in entry['archives'].items()],
                'loaded_assemblies':[{'name':'Hexalith.EventStore.Server','version':self.candidate_version,'path':'/synthetic/Server.dll',
                    'sha256':value} for entry in verified.values() for value in entry['dlls']]},
            'cases':[row],'execution_evidence':executed,'output_sha256':executor.digest(executor.canonical(executed)),
            'assertions':row['assertions'],'execution':'passed','compatibility':'compatible'}
        if self.candidate_version in ('3.122.1','3.125.0','3.125.1','3.126.0'):
            receipt = self.attach_isolation(receipt,context)
        return receipt,context

    def attach_isolation(self,receipt,context):
        from p1r_isolation_fixture import run_controller
        # Each inherited fixture call gets a fresh real controller lifecycle.
        wrapper = Path(self.temporary.name)/'wrapper'
        if wrapper.exists():
            shutil.rmtree(wrapper)
        code,worker,retained = run_controller(Path(self.temporary.name),receipt,context)
        self.assertEqual(code,0,worker.result['errors'])
        return retained

    def testActualReceiptImportForwardsSelectedVersionAndRefusesMissingOrSubstitutedProof(self):
        receipt,context = self.import_fixture()
        self.assertEqual(qualification.receipt_outcome(receipt,context)[2]['execution'],'passed')
        for mutation in ('missing','provider'):
            changed = copy.deepcopy(receipt)
            evidence = changed['execution_evidence']['cases'][0]
            if mutation == 'missing':
                del evidence['observations']['seed_snapshot']
            else:
                self.change_output(evidence['commands'][11],lambda value:value[0][1].update(state={'count':8}))
            changed['output_sha256'] = executor.digest(executor.canonical(changed['execution_evidence']))
            with self.subTest(mutation=mutation),self.assertRaises(preparation.InvalidPacket):
                qualification.receipt_outcome(changed,context)
        changed = copy.deepcopy(receipt)
        del changed['execution_evidence']['cases'][0]['observations']['seed_snapshot']
        changed['output_sha256'] = executor.digest(executor.canonical(changed['execution_evidence']))
        actual = executor.witnesses.validate_case
        def drop_forwarding(row,evidence,binding,lane,sources,**kwargs):
            return actual(row,evidence,binding,lane,sources)
        # The missing-proof refusal control itself fails when candidate forwarding is removed.
        with mock.patch.object(executor.witnesses,'validate_case',side_effect=drop_forwarding):
            with self.assertRaises(AssertionError):
                with self.assertRaises(preparation.InvalidPacket):
                    qualification.receipt_outcome(changed,context)
    def testUnverifiedProbePathsDigestsFailedExitsAndTailEndpointsAreRejected(self):
        fixture = self.fixture()
        for identity in (6,7,16):
            for field,value in ((1,'/unverified/Probe.dll'),('exit_code',1)):
                changed = copy.deepcopy(fixture)
                command = changed[1]['commands'][identity-1]
                if field == 1:
                    command['argv'][1] = value
                else:
                    command[field] = value
                with self.subTest(identity=identity,field=field), self.assertRaises((preparation.InvalidPacket,FileNotFoundError)):
                    self.validate(changed)
        for mutate in (lambda command:command['argv'].__setitem__(3,'http://127.0.0.1:29999'),
                       lambda command:command['probe_binary'].update(role='comparison-3.110.0'),
                       lambda command:command['probe_binary'].update(configuration='Debug'),
                       lambda command:command['probe_binary'].update(sha256='f'*64)):
            changed = copy.deepcopy(fixture)
            mutate(changed[1]['commands'][15])
            with self.assertRaises(preparation.InvalidPacket):
                self.validate(changed)
        binary = Path(fixture[1]['commands'][5]['argv'][1])
        binary.write_bytes(b'replaced at the same physical path')
        with self.assertRaisesRegex(preparation.InvalidPacket,'physical digest'):
            self.validate(fixture)

    def testLaunchReadinessOwnershipAndRestartedStateStoreAreMandatory(self):
        fixture = self.fixture()
        for version in ('source','3.122.2',
                        *[candidate for candidate in ('3.121.0','3.122.1','3.125.0','3.125.1','3.126.0') if candidate != self.candidate_version]):
            changed = copy.deepcopy(fixture)
            config = changed[1]['configurations'][0]
            config['version'] = version
            for command in changed[1]['commands']:
                if command.get('runtime_configuration') == config['id']:
                    command['runtime_configuration_sha256'] = executor.digest(executor.canonical(config))
            with self.subTest(version=version), self.assertRaisesRegex(preparation.InvalidPacket,'launch configuration'):
                self.validate(changed)
        for identity,field,value in ((8,'exit_code',1),(11,'process',None),(19,'process',fixture[1]['commands'][10]['process']),
                                     (3,'exit_code',1),(4,'exit_code',1),(15,'exit_code',1)):
            changed = copy.deepcopy(fixture)
            changed[1]['commands'][identity-1][field] = value
            with self.subTest(identity=identity,field=field), self.assertRaises(preparation.InvalidPacket):
                self.validate(changed)
        changed = copy.deepcopy(fixture)
        config = changed[1]['configurations'][1]
        config['state_container'] = 'f'*64
        changed[1]['commands'][18]['runtime_configuration_sha256'] = executor.digest(executor.canonical(config))
        with self.assertRaisesRegex(preparation.InvalidPacket,'persisted stream'):
            self.validate(changed)
        # Graceful cleanup can intentionally terminate a launch, after readiness and the calls.
        changed = copy.deepcopy(fixture)
        for index in (7,8,9,10,17,18):
            changed[1]['commands'][index]['exit_code'] = -15
        self.validate(changed)

    def testProviderMustReadExactStateQueryFromOwnedDatabaseSuccessfully(self):
        fixture = self.fixture()
        for mutate in (lambda command:command['argv'].__setitem__(3,'f'*64),
                       lambda command:command.update(exit_code=1),
                       lambda command:command.update(input_sha256=executor.digest(b'SELECT fake')),
                       lambda command:command['argv'].__setitem__(8,'other-database')):
            changed = copy.deepcopy(fixture)
            mutate(changed[1]['commands'][11])
            with self.assertRaisesRegex(preparation.InvalidPacket,'owned provider'):
                self.validate(changed)
        execution_path = Path(self.temporary.name)/'execution.json'
        execution_value = json.loads(execution_path.read_text())
        execution_value['owned_containers'] = {}
        executor.write_json(execution_path,execution_value)
        with self.assertRaisesRegex(preparation.InvalidPacket,'invocation-owned database'):
            self.validate(fixture)

    def testTailCountAndDistinctOrderedInventoriesCannotBeSubstituted(self):
        fixture = self.fixture()
        changed = copy.deepcopy(fixture)
        self.change_output(changed[1]['commands'][15],lambda value:value['observation'].update(committed=2,sequence=11))
        changed[1]['observations']['seed_snapshot']['tail'].update(committed=2,sequence=11)
        with self.assertRaisesRegex(preparation.InvalidPacket,'actual append'):
            self.validate(changed)
        for identity,field,value in ((17,'started_utc','2026-10-14T00:00:13+00:00'),(13,'exit_code',1),(17,'exit_code',1)):
            changed = copy.deepcopy(fixture)
            changed[1]['commands'][identity-1][field] = value
            with self.subTest(identity=identity,field=field), self.assertRaises(preparation.InvalidPacket):
                self.validate(changed)
        changed = copy.deepcopy(fixture)
        before = changed[1]['observations']['seed_snapshot']['before_tail']
        changed[1]['observations']['seed_snapshot']['after_tail'] = before
        del changed[1]['commands'][16]
        with self.assertRaises(preparation.InvalidPacket):
            self.validate(changed)
        for name,command_index in (('before_tail',12),('after_tail',16)):
            changed = copy.deepcopy(fixture)
            rows = changed[1]['observations']['seed_snapshot'][name]
            next(item for item in rows if item['tenant']=='tenant-a' and item['kind']=='metadata')['sequence'] -= 1
            rows[:] = [item for item in rows if not(item['tenant']=='tenant-a' and item['kind']=='event' and item['sequence']==5)]
            changed[1]['commands'][command_index]['output'] = executor.canonical(rows).decode()
            self.rehash_inventory(changed)
            with self.subTest(name=name), self.assertRaisesRegex(preparation.InvalidPacket,'contradicts retained execution'):
                self.validate(changed)

    def testFailedActualTailAndCoordinatesRemainRecordedFailures(self):
        fixture = self.fixture()
        row,evidence,_,_ = fixture
        tail = evidence['observations']['seed_snapshot']['tail']
        tail.clear()
        tail.update(committed=2,accepted=False,event_count=0,error='retained failed append')
        command = evidence['commands'][15]
        value = json.loads(command['output'])
        value['observation'] = tail
        for check in value['measurement']['checks'][-2:]:
            check['passed'] = False
        value['measurement']['assertions'] = preparation.checks_counter(value['measurement']['checks'])
        command['output'] = executor.canonical(value).decode()
        rows = evidence['observations']['seed_snapshot']['after_tail']
        rows[:] = [item for item in rows if not(item['tenant']=='tenant-a' and item['kind']=='event' and item['sequence']==12)]
        next(item for item in rows if item['tenant']=='tenant-a' and item['kind']=='metadata')['sequence'] = 11
        evidence['commands'][16]['output'] = executor.canonical(rows).decode()
        failed = {'tenant-a:tail:append-3-accepted','tenant-a:tail:append-3-one-event',
                  'tenant-a:committed-count','seed-persisted-fifteen-events','qualified-snapshot-tail-coordinate-exact'}
        for check,witness in zip(row['checks'],evidence['check_witnesses']):
            if check['id'] in failed:
                check['passed'] = False
                if witness['kind'] == 'predicate':
                    witness['predicate']['value'] = False
        row['assertions'] = preparation.checks_counter(row['checks'])
        row['disposition'] = 'incompatible'
        self.rehash_inventory(fixture)
        self.validate(fixture)
        self.assertEqual(row['assertions']['failed'],5)

    def testEveryLimitAndDerivedReservationIsBoundToSharedFixtureSource(self):
        fixture = self.fixture()
        for name in fixture[1]['observations']['seed_snapshot']['registration']['limits']:
            changed = copy.deepcopy(fixture)
            registration = changed[1]['observations']['seed_snapshot']['registration']
            registration['limits'][name] += 1
            for command in changed[1]['commands']:
                if command['id'] in (1,5):
                    self.change_output(command,lambda value:value.update(snapshot_qualification=registration))
            with self.subTest(name=name), self.assertRaisesRegex(preparation.InvalidPacket,'finite profile'):
                self.validate(changed)
        changed = copy.deepcopy(fixture)
        del changed[1]['snapshot_fixture_source']
        with self.assertRaisesRegex(preparation.InvalidPacket,'retained shared fixture source'):
            self.validate(changed)

    def testSnapshotIdentityProtectionAndCreationTimeAreBoundToProviderEnvelope(self):
        fixture = self.fixture()
        for name,value in (('tenantId','tenant-b'),('domain','other'),('aggregateId','other'),
                           ('snapshotEnvelopeVersion',0),('state',{'count':8}),
                           ('protectionMetadata',{'state':0,'metadataVersion':2}),
                           ('createdAt','2026-10-14T00:00:01+00:00')):
            changed = copy.deepcopy(fixture)
            snapshot = changed[1]['observations']['seed_snapshot']['persisted_snapshot']
            snapshot[name] = value
            self.change_output(changed[1]['commands'][11],lambda raw:raw[0].__setitem__(1,snapshot))
            for inventory in (changed[1]['observations']['seed_snapshot']['before_tail'],changed[1]['observations']['seed_snapshot']['after_tail']):
                next(item for item in inventory if item['kind']=='snapshot')['sha256'] = executor.digest(executor.canonical(snapshot))
            changed[1]['commands'][12]['output'] = executor.canonical(changed[1]['observations']['seed_snapshot']['before_tail']).decode()
            changed[1]['commands'][16]['output'] = executor.canonical(changed[1]['observations']['seed_snapshot']['after_tail']).decode()
            self.rehash_inventory(changed)
            with self.subTest(name=name), self.assertRaises(preparation.InvalidPacket):
                self.validate(changed)

    @staticmethod
    def change_output(command,mutate):
        value = json.loads(command['output'])
        mutate(value)
        command['output'] = executor.canonical(value).decode()
        command['output_sha256'] = executor.digest(command['output'].encode())

    @staticmethod
    def rehash_inventory(fixture):
        row,evidence,_,_ = fixture
        row['inventory'] = {name+'_sha256':executor.digest(executor.canonical([item for item in evidence['observations'][name]
                          if item['kind']!='bookkeeping'])) for name in ('before','after')}

    def fixture(self,writer_version=None,lane='post-upgrade-restore'):
        writer_version = self.candidate_version if writer_version is None else writer_version
        role,configuration = ('source','Debug') if writer_version == 'source' else ('candidate','Release')
        root = Path(self.temporary.name)
        consumer = root/'consumers'/role
        binary = consumer/'probe/bin'/configuration/'net10.0/Probe.dll'
        binary.parent.mkdir(parents=True,exist_ok=True)
        binary.write_bytes(b'private synthetic probe binary')
        serving_binaries = {}
        for project in ('Host','Domain'):
            path = consumer/project.lower()/'bin'/configuration/'net10.0'/(project+'.dll')
            path.parent.mkdir(parents=True,exist_ok=True)
            path.write_bytes(('physical synthetic '+project).encode())
            serving_binaries[project] = {'role':role,'configuration':configuration,'project':project,
                'path':str(path),'sha256':executor.digest(path.read_bytes())}
        executor.write_json(consumer/'serving-binaries.json',serving_binaries)
        binary_witness = {'role':role,'configuration':configuration,'path':str(binary),'sha256':executor.digest(binary.read_bytes())}
        executor.write_json(consumer/'probe-binary.json',binary_witness)
        executor.write_json(consumer/'evidence.json',{'role':role,'configuration':configuration,
            'projects':[{'name':'Probe','output':'probe/bin/'+configuration+'/net10.0'}]})
        fixture_path = preparation.ROOT/'tools/p1r-published-consumers/FixtureSnapshotQualification.cs'
        fixture_source = {'path':str(fixture_path),'sha256':executor.digest(fixture_path.read_bytes()),'content':fixture_path.read_text()}
        test_path = Path(__file__).resolve()
        test_source = {'path':str(test_path),'sha256':executor.digest(test_path.read_bytes()),'content':test_path.read_text()}
        binding = {'repository':str(preparation.ROOT),'main':{'files':[
            {'path':str(path.relative_to(preparation.ROOT)),'sha256':executor.digest(path.read_bytes())}
            for path in (fixture_path,test_path)]}}
        python_path = Path(sys.executable).resolve()
        binding['python'] = {'path':str(python_path),'sha256':executor.digest(python_path.read_bytes()),'version':list(sys.version_info[:3])}
        executor.write_json(root/'executor-source.json',binding)
        database,invocation = 'a'*64,'b'*32
        owned = {'name':'p1r-'+invocation+'-source-control','image':executor.POSTGRES,'role':'source','port':15432}
        executor.write_json(root/'execution.json',{'invocation':invocation,'owned_containers':{database:owned},
            'executor_source_sha256':executor.digest((root/'executor-source.json').read_bytes())})
        executor.write_json(root/'commands.json',[{'argv':['docker','inspect',database],'exit_code':0,
            'output':json.dumps({'Id':database,'Name':'/'+owned['name'],'Config':{'Labels':{'hexalith.p1r.invocation':invocation}}})}])
        state = {'name':'state.yaml','path':str(root/'resources/state.yaml'),'sha256':'c'*64,'rendered_sha256':'d'*64,'content':'state.postgresql'}
        configurations = [{'id':identity,'version':writer_version,'state_container':database,'snapshot_interval':10,
            'state_component_sha256':'c'*64,'backend_image':executor.POSTGRES,'files':[state,
            {'path':str(root/'resources/pubsub.yaml')},{'path':str(root/'discovery.yaml')}]} for identity in ('first','restarted')]
        snapshot = {'tenantId':'tenant-a','domain':'counter','aggregateId':'fixture','sequenceNumber':9,
            'snapshotEnvelopeVersion':1,'state':{'count':9},'createdAt':'2026-10-14T00:00:09.500000+00:00',
            'protectionMetadata':{'state':0,'metadataVersion':1,'scheme':None,'keyAlias':None,'contentHint':None,'compatibilityFlags':None}}
        key = 'eventstore||AggregateActor||tenant-a:counter:fixture||tenant-a:counter:fixture:'
        def inventory(head,second=False):
            rows = [{'key':key+'metadata','tenant':'tenant-a','kind':'metadata','sequence':head,'floor':1,'sha256':str(head).zfill(64)},
                    {'key':key+'snapshot','tenant':'tenant-a','kind':'snapshot','sequence':9,'floor':None,'sha256':executor.digest(executor.canonical(snapshot))}]
            rows.extend({'key':key+'events:'+str(index),'tenant':'tenant-a','kind':'event','sequence':index,'floor':None,'sha256':str(index).zfill(64)} for index in range(1,head+1))
            if second:
                rows.extend({'key':'tenant-b:event:'+str(index),'tenant':'tenant-b','kind':'event','sequence':index,'floor':None,'sha256':'e'*64} for index in range(1,4))
            return sorted(rows,key=lambda row:row['key'])
        before,after = inventory(9),inventory(12,True)
        limits = {'maximumCaptureWorkspaceBytes':512000,'maximumPreCommandStateGraphBytes':1024,'maximumWorkingStateGraphBytes':1024,
            'maximumDetachedEventGraphBytes':8192,'maximumEventCount':16,'maximumEventBytes':1024,'maximumStateJsonBytes':1024,
            'maximumDomainParsedStateGraphBytes':8192,'maximumEscapedWireStateBytes':6146,'maximumActorParsedStateBytes':1024,
            'maximumActorParsedGraphBytes':8192,'maximumCanonicalStateMeasurementBytes':1024,'maximumCanonicalEnvelopeBytes':8192,
            'maximumNodeCount':100,'maximumDepth':16,'maximumSerializerScratchBytes':262144,'maximumSimultaneousLiveBytes':512000,
            'maximumActorWorkspaceBytes':512000,'domainReservationBytes':294914,'actorReservationBytes':292864}
        registration = {'registered':True,'test_only':True,'profile_id':'p1r-counter-folded-v1',
            'parity_evidence_id':'p1r-counter-roundtrip-event-parity-v1','state_json':{'count':9},
            'state_sha256':executor.digest(executor.canonical({'count':9})),'state_roundtrip':True,
            'original_count':9,'roundtrip_count':9,'canonical_event_parity':True,'canonical_event_sha256':executor.digest(b'{}'),
            'detached_apply':True,'applied_count':10,'limits':limits}
        manual = {'outcome':'Created','sequence':9,'snapshot_key':'tenant-a:counter:fixture:snapshot','reason_code':None,'message':None}
        tail = {'committed':3,'sequence':12}
        measurements = executor.Measurements()
        commands = []
        import datetime
        def timestamp(second):
            return (datetime.datetime(2026,10,14,tzinfo=datetime.timezone.utc)+datetime.timedelta(seconds=second)).isoformat()
        def record(argv,value,start,end,launch=False,config=None):
            output = value if isinstance(value,str) else executor.canonical(value).decode()
            command = {'id':len(commands)+1,'argv':argv,'exit_code':0,'started_utc':timestamp(start),'finished_utc':timestamp(end),
                       'output':output,'output_sha256':executor.digest(output.encode())}
            if launch:
                command.update(process={'pid':command['id'],'pgid':command['id'],'session':command['id'],'start_ticks':command['id']},
                               cleanup=[{'remaining':[],'errors':[]}])
                command['owned_processes'] = [command['process']]
                command['cleanup'][0]['before'] = [command['process']]
            if config is not None:
                command.update(runtime_configuration=config['id'],runtime_configuration_sha256=executor.digest(executor.canonical(config)))
            if argv[:1] == ['dotnet'] and argv[1] == str(binary):
                command['probe_binary'] = dict(binary_witness)
            commands.append(command)
            return command
        def probe(value,identities):
            checks = [{'id':identity,'passed':True} for identity in identities]
            return {'instrumentation':executor.MECHANISM,'observation':value,
                    'measurement':{'checks':checks,'assertions':preparation.checks_counter(checks)}}
        def append_checks(count):
            return [identity for index in range(1,count+1) for identity in ('append-'+str(index)+'-accepted','append-'+str(index)+'-one-event')]
        def sidecar(appid,app,port,config,start,end):
            return record(['/owned/daprd','--app-id',appid,'--app-channel-address','127.0.0.1','--app-port',str(app),
                '--dapr-http-port',str(port),'--config',str(root/'discovery.yaml'),'--resources-path',str(root/'resources')],
                'sidecar started',start,end,True,config)
        def host(project,app,start,end):
            command = record(['dotnet',str(consumer/project.lower()/'bin'/configuration/'net10.0'/(project+'.dll'))],
                'Now listening on: http://127.0.0.1:'+str(app),start,end,True)
            command['consumer_binary'] = serving_binaries[project]
            return command
        record(['HTTP','GET','http://127.0.0.1:10002/ready'],{'snapshot_qualification':registration},2,2.1)
        record(['HTTP','GET','http://127.0.0.1:10003/v1.0/healthz/outbound'],{},2.6,2.7)
        record(['HTTP','GET','http://127.0.0.1:10004/ready'],{},4,4.1)
        record(['HTTP','GET','http://127.0.0.1:10001/v1.0/healthz/outbound'],{},5,5.1)
        record(['HTTP','GET','http://127.0.0.1:10002/ready'],{'snapshot_qualification':registration},6,6.1)
        command = record(['dotnet',str(binary),'seed','http://127.0.0.1:10001','tenant-a','fixture','9'],probe({'committed':9,'sequence':9},append_checks(9)),7,8)
        measurements.import_probe(json.loads(command['output']),'tenant-a:prefix:',command['id'])
        command = record(['dotnet',str(binary),'manual-snapshot','http://127.0.0.1:10001','tenant-a','fixture'],probe(manual,
            ('manual-snapshot-created','manual-snapshot-at-nine','manual-snapshot-no-failure')),9,10)
        measurements.import_probe(json.loads(command['output']),'snapshot:',command['id'])
        host('Host',10004,3,11)
        host('Domain',10002,1,11)
        sidecar('counter',10002,10003,configurations[0],2.5,11)
        sidecar('eventstore',10004,10001,configurations[0],4.5,11)
        command = record(['docker','exec','-i',database,'psql','-U','postgres','-d','eventstore','-At','-v','ON_ERROR_STOP=1','-f','-'],[[key+'snapshot',snapshot]],12,12.5)
        query = "SELECT coalesce(json_agg(json_build_array(key,value) ORDER BY key)::text,'[]') FROM state"
        command.update(input_sha256=executor.digest(query.encode()),input_bytes=len(query.encode()),provider_sql=query)
        argv = [str(Path(sys.executable).resolve()),'-I',str(preparation.ROOT/'tools/p1r-published-executor.py'),'inventory','--container',database,
                '--invocation',invocation,'--backend','state.postgresql']
        record(argv,before,13,13.5)
        record(['HTTP','GET','http://127.0.0.1:10006/ready'],{},15.5,15.6)
        record(['HTTP','GET','http://127.0.0.1:10005/v1.0/healthz/outbound'],{},16.5,16.6)
        command = record(['dotnet',str(binary),'seed','http://127.0.0.1:10005','tenant-a','fixture','3'],probe(tail,append_checks(3)),17,18)
        measurements.import_probe(json.loads(command['output']),'tenant-a:tail:',command['id'])
        record(argv,after,21,21.5)
        host('Host',10006,15,20)
        sidecar('eventstore',10006,10005,configurations[1],16,20)
        for identity in ('snapshot-fixture-enrolled','snapshot-state-roundtrip-parity','snapshot-canonical-event-parity',
                'snapshot-detached-apply-parity','snapshot-nine-events-committed','tenant-a:committed-count','tenant-b:committed-count',
                'seed-persisted-fifteen-events','qualified-snapshot-created-by-published-actor','qualified-snapshot-envelope-version-one',
                'qualified-snapshot-persisted-nine','qualified-snapshot-prefix-coordinate-exact','qualified-snapshot-tail-coordinate-exact',
                'qualified-snapshot-and-prefix-bytes-preserved'):
            passed = True
            measurements.check(identity,passed)
        observation = {'before':after,'after':after,'seed_snapshot':{'registration':registration,'manual':manual,'persisted_snapshot':snapshot,
            'before_tail':before,'after_tail':after,'tail':tail}}
        if lane != 'post-upgrade-restore':
            operational_before,operational_after = copy.deepcopy(after),copy.deepcopy(after)
            if lane in ('retained-covered','retained-uncovered','metadata-write'):
                for rows in (operational_before,operational_after):
                    rows[:] = [item for item in rows if not(item['tenant']=='tenant-a' and item['kind']=='event' and item['sequence']<5)]
                    next(item for item in rows if item['kind']=='metadata')['floor'] = 5
                    if lane == 'retained-uncovered':
                        next(item for item in rows if item['kind']=='snapshot')['sequence'] = 2
            if lane == 'metadata-write':
                next(item for item in operational_after if item['kind']=='metadata')['sequence'] = 13
                operational_after.append({'key':key+'events:13','tenant':'tenant-a','kind':'event','sequence':13,'floor':None,'sha256':'f'*64})
                operational_after.sort(key=lambda item:item['key'])
            observation.update(before=operational_before,after=operational_after,
                primary={'accepted':lane!='retained-uncovered','sequence':12,'event_count':0})
            record(argv,operational_before,22,22.1)
            record(argv,operational_after,23,23.1)
            record(['dotnet',str(binary),'actor'],probe(observation['primary'],()),24,24.1)
            for identity in ('primary-expected-hydration','hydration-no-events','second-tenant-hydrates','primary-sequence-twelve',
                             'persisted-current-sequence-exact','persisted-event-sequence-set-exact','persisted-snapshot-coordinate-exact',
                             'prior-event-and-snapshot-bytes-preserved','second-tenant-state-preserved'):
                passed = True
                measurements.check(identity,passed)
            if lane == 'metadata-write':
                observation.update(append={'accepted':True,'event_count':1,'sequence':13},
                    replay={'accepted':True,'event_count':0,'sequence':13})
                for name in ('append','replay'):
                    record(['dotnet',str(binary),'actor'],probe(observation[name],()),25,25.1)
                for identity in ('append-effect-accepted','append-thirteen','retained-floor-five-preserved','supported-append-restart-rehydrates-thirteen'):
                    passed = True
                    measurements.check(identity,passed)
        evidence = {'commands':commands,'configurations':configurations,'observations':observation,
            'snapshot_fixture_source':fixture_source,'check_witnesses':measurements.witnesses}
        identifier = 'fresh-backup-restore-append-restart' if lane == 'post-upgrade-restore' else writer_version+'-to-3.110.0'
        outcome = 'refusal' if lane == 'retained-uncovered' else 'effect'
        row = {'id':identifier,'operation':'supported','expected':outcome,'outcome':outcome,
            'disposition':'compatible','checks':measurements.checks,'assertions':measurements.counter}
        fixture = row,evidence,binding,[test_source]
        self.rehash_inventory(fixture)
        return fixture


class CurrentSourceReadinessSnapshotWitnessTests(QualifiedSnapshotWitnessTests):
    candidate_version = '3.125.0'

    def testOperationalImportRejectsMissingFinalIsolation(self):
        receipt,context = self.import_fixture(scope='operational')
        self.assertEqual(qualification.receipt_outcome(receipt,context)[2]['execution'],'passed')
        changed = copy.deepcopy(receipt)
        del changed['execution_evidence']['isolation']
        changed['output_sha256'] = executor.digest(executor.canonical(changed['execution_evidence']))
        with self.assertRaises(preparation.InvalidPacket):
            qualification.receipt_outcome(changed,context)

    def testRecoveryRetainsExactContributingCleanup(self):
        ProposedQualifiedSnapshotWitnessTests.testFinalRecoveryReferencePreservesExactEmbeddedContributingCleanupContract(self)


class ProposedQualifiedSnapshotWitnessTests(QualifiedSnapshotWitnessTests):
    candidate_version = '3.122.1'

    @staticmethod
    def change_isolation(receipt,name,mutate):
        reference = receipt['execution_evidence']['isolation']
        record = reference[name]
        value = json.loads(storage.preserved_bytes(record, storage.receipt_context(receipt, receipt["execution_evidence"]["executor_source"],
            json.loads((Path(reference["execution"]["path"]).parent / "inputs.json").read_bytes()))))
        if name == 'wrapper':
            value = storage.materialize(value, value.get('evidence_context', {}))
        mutate(value)
        path = Path(record['path'])
        executor.write_json(path,value)
        data = path.read_bytes()
        record.pop('content_ref', None)
        record.update(content=data.decode(),sha256=executor.digest(data))
        if name == 'private':
            wrapper = reference['wrapper']
            value = json.loads(storage.preserved_bytes(wrapper, storage.receipt_context(receipt, receipt["execution_evidence"]["executor_source"],
                json.loads((Path(reference["execution"]["path"]).parent / "inputs.json").read_bytes()))))
            value['private_execution']['sha256'] = record['sha256']
            path = Path(wrapper['path'])
            executor.write_json(path,value)
            data = path.read_bytes()
            wrapper.pop('content_ref', None)
            wrapper.update(content=data.decode(),sha256=executor.digest(data))
        receipt['output_sha256'] = executor.digest(executor.canonical(receipt['execution_evidence']))

    def testOperationalImportRequiresMatchingSuccessfulFinalIsolationAndActualAbsenceCommands(self):
        receipt,context = self.import_fixture(scope='operational')
        self.assertEqual(qualification.receipt_outcome(receipt,context)[2]['execution'],'passed')
        mutations = (
            ('wrapper',lambda value:value.update(passed=False)),
            ('private',lambda value:value.update(passed=False)),
            ('private',lambda value:value.update(invocation='e'*32)),
            ('private',lambda value:value.update(source_checked_after_cleanup=False)),
            ('private',lambda value:value['proof']['namespaces'].update(pid='pid:[foreign]')),
            ('private',lambda value:value['proof']['executables']['python'].update(path='/substituted/python')),
            ('private',lambda value:value['proof']['executables']['python'].update(sha256='9'*64)),
            ('private',lambda value:value['proof'].update(python_version=[3,0,0])),
            ('wrapper',lambda value:value['owned_containers'][value['sdk']].update(image=executor.POSTGRES)),
            ('wrapper',lambda value:value['owned_containers'][value['daemon']].update(image=executor.POSTGRES)),
            ('wrapper',lambda value:value['runner_proof'].update(Ports={'2375/tcp':[{'HostPort':'2375'}]})),
            ('wrapper',lambda value:value['runner_proof']['Mounts'][1].update(Source='/var/run/docker.sock')),
            ('private',lambda value:value['command'].__setitem__(4,'/substituted-output')),
            ('execution',lambda value:value.update(inputs_sha256='9'*64)),
            ('wrapper',lambda value:value['cleanup'][0].update(remaining=['sdk-still-live'])),
            ('wrapper',lambda value:value['cleanup'][0].update(absent_volumes=[])),
            ('wrapper',lambda value:value['commands'].pop(1)),
            ('wrapper',lambda value:value['commands'][0].update(output='foreign-container',output_sha256=executor.digest(b'foreign-container'))),
        )
        for index,(name,mutate) in enumerate(mutations):
            receipt,context = self.import_fixture(scope='operational')
            self.change_isolation(receipt,name,mutate)
            with self.subTest(index=index),self.assertRaises(preparation.InvalidPacket):
                qualification.receipt_outcome(receipt,context)
        receipt,context = self.import_fixture(scope='operational')
        del receipt['execution_evidence']['isolation']
        receipt['output_sha256'] = executor.digest(executor.canonical(receipt['execution_evidence']))
        with self.assertRaisesRegex(preparation.InvalidPacket,'final isolation evidence'):
            qualification.receipt_outcome(receipt,context)
        receipt,context = self.import_fixture(scope='operational')
        Path(receipt['execution_evidence']['isolation']['wrapper']['path']).write_text('substituted physical receipt')
        with self.assertRaisesRegex(preparation.InvalidPacket,'physical reference substituted'):
            qualification.receipt_outcome(receipt,context)

    def testRuntimeConfiguredImportCannotBypassIsolationByClaimingPackageOnlyScope(self):
        receipt,context = self.import_fixture()
        del receipt['execution_evidence']['isolation']
        receipt['output_sha256'] = executor.digest(executor.canonical(receipt['execution_evidence']))
        with self.assertRaisesRegex(preparation.InvalidPacket,'final isolation evidence'):
            qualification.receipt_outcome(receipt,context)

    def testFinalIsolationRejectsRehashedBackwardsCleanupAndEscapedReceiptOrCommandTimes(self):
        receipt,context = self.import_fixture(scope='operational')
        self.assertEqual(qualification.receipt_outcome(receipt,context)[2]['execution'],'passed')
        def backwards(value):
            from datetime import datetime,timedelta
            for item in [*value['cleanup'],*value['commands']]:
                for field in ('started_utc','finished_utc'):
                    item[field] = (datetime.fromisoformat(item[field])-timedelta(seconds=24)).isoformat()
        for mutate in (backwards,lambda value:value['cleanup'].reverse(),
                       lambda value:value['cleanup'][1].update(started_utc=value['cleanup'][0]['started_utc'])):
            receipt,context = self.import_fixture(scope='operational')
            self.change_isolation(receipt,'wrapper',mutate)
            with self.assertRaisesRegex(preparation.InvalidPacket,'outer cleanup'):
                qualification.receipt_outcome(receipt,context)
        for name,mutate in (
                ('private',lambda value:value.update(started_utc='2026-10-14T00:00:01+00:00')),
                ('execution',lambda value:value.update(finished_utc='2026-10-14T00:00:33+00:00'))):
            receipt,context = self.import_fixture(scope='operational')
            self.change_isolation(receipt,name,mutate)
            with self.assertRaisesRegex(preparation.InvalidPacket,'teardown timestamps'):
                qualification.receipt_outcome(receipt,context)
        for field,value in (('started_utc','2026-10-13T23:59:59+00:00'),('finished_utc','2026-10-14T00:00:50+00:00')):
            receipt,context = self.import_fixture(scope='operational')
            receipt[field] = value
            receipt['output_sha256'] = executor.digest(executor.canonical(receipt['execution_evidence']))
            with self.subTest(field=field),self.assertRaisesRegex(preparation.InvalidPacket,'lane receipt escapes'):
                qualification.receipt_outcome(receipt,context)
        for start,end in (('2026-10-13T23:59:59+00:00','2026-10-14T00:00:02+00:00'),
                          ('2026-10-14T00:00:45+00:00','2026-10-14T00:00:45.100000+00:00')):
            receipt,context = self.import_fixture(scope='operational')
            command = receipt['execution_evidence']['cases'][0]['commands'][0]
            command.update(started_utc=start,finished_utc=end)
            receipt['output_sha256'] = executor.digest(executor.canonical(receipt['execution_evidence']))
            with self.subTest(start=start),self.assertRaisesRegex(preparation.InvalidPacket,'receipt command escapes'):
                qualification.receipt_outcome(receipt,context)

    def testFinalRecoveryReferencePreservesExactEmbeddedContributingCleanupContract(self):
        try:
            from test_p1r_published_qualification import restore_receipt
        except ImportError:
            from tools.tests.test_p1r_published_qualification import restore_receipt
        receipt,context = self.import_fixture(scope='operational')
        proof = receipt['execution_evidence']['isolation']
        root = Path(proof['execution']['path']).parent
        wrapper = Path(proof['wrapper']['path']).parent
        restore = restore_receipt(scope='operational')
        cleanup = restore['cleanup']
        for value in (restore,cleanup):
            value.update(inputs_sha256=receipt['inputs_sha256'],profile=receipt['profile'],fixture=receipt['fixture'],compatibility='compatible')
        cleanup['invocation'] = 'b'*32
        previous_container = next(item['id'] for item in cleanup['owned'] if item['kind']=='container')
        for owned in cleanup['owned']:
            owned['label'] = 'hexalith.p1r.invocation='+cleanup['invocation']
            if owned['kind'] == 'container':
                owned['id'] = 'a'*64
        for attempt in cleanup['attempts']:
            for key in ('targeted','removed','remaining'):
                attempt[key] = ['a'*64 if identity == previous_container else identity for identity in attempt[key]]
        from datetime import datetime,timedelta,timezone
        for index,command in enumerate(restore['commands']):
            command['started_utc'] = command['finished_utc'] = (datetime(2026,10,14,tzinfo=timezone.utc)+timedelta(seconds=index+1)).isoformat()
        for index,attempt in enumerate(cleanup['attempts']):
            attempt['started_utc'] = f'2026-10-14T00:00:{25+2*index}+00:00'
            attempt['finished_utc'] = f'2026-10-14T00:00:{26+2*index}+00:00'
        restore['packages'] = context['verified']['candidate']['archives']
        paths = []
        for value in (cleanup,restore):
            path = root/'receipts'/(value['id']+'.json')
            executor.write_json(path,value)
            paths.append(str(path))
        execution = json.loads((root/'execution.json').read_bytes())
        execution['receipts'].extend(paths)
        executor.write_json(root/'execution.json',execution)
        qualification.finalize_isolation_receipts(root,wrapper,context['inputs']['value'])
        cleanup,restore = (json.loads(Path(path).read_bytes()) for path in paths)
        self.assertEqual(restore['cleanup'],cleanup)
        self.assertEqual(qualification.receipt_outcome(restore,context)[2]['execution'],'passed')
        self.assertEqual(qualification.receipt_outcome(cleanup,context)[2]['execution'],'passed')
        for original in (cleanup,restore):
            changed = copy.deepcopy(original)
            observed = changed if changed['schema'] == runtime.CLEANUP_SCHEMA else changed['cleanup']
            observed['invocation'] = 'a'*32
            for owned in observed['owned']:
                owned['label'] = 'hexalith.p1r.invocation='+observed['invocation']
            with self.subTest(schema=original['schema']),self.assertRaisesRegex(preparation.InvalidPacket,'cleanup invocation differs'):
                qualification.receipt_outcome(changed,context)
        for original in (cleanup,restore):
            changed = copy.deepcopy(original)
            observed = changed if changed['schema'] == runtime.CLEANUP_SCHEMA else changed['cleanup']
            old_id = next(item['id'] for item in observed['owned'] if item['kind']=='container')
            next(item for item in observed['owned'] if item['kind']=='container')['id'] = 'e'*64
            for attempt in observed['attempts']:
                for key in ('targeted','removed','remaining'):
                    attempt[key] = ['e'*64 if identity == old_id else identity for identity in attempt[key]]
            with self.subTest(schema=original['schema']),self.assertRaisesRegex(preparation.InvalidPacket,'cleanup containers differ'):
                qualification.receipt_outcome(changed,context)


class Published31251SnapshotWitnessTests(ProposedQualifiedSnapshotWitnessTests):
    candidate_version = '3.125.1'
    source_commit = qualification.SELECTED_SOURCE_COMMIT
    tag_builds_commit = qualification.SELECTED_TAG_BUILDS

    def setUp(self):
        super().setUp()
        self.tagged_root = Path(self.temporary.name) / 'tagged-source'
        self.tagged_root.mkdir()
        component = self.tagged_root/'deploy/dapr/statestore-postgresql.yaml'
        component.parent.mkdir(parents=True)
        component.write_bytes((preparation.ROOT/'deploy/dapr/statestore-postgresql.yaml').read_bytes())
        (self.tagged_root/'.gitmodules').write_text('[submodule \"Builds\"]\n path = references/Hexalith.Builds\n url = fixture-builds\n')
        self.test_binding = None
        original_binding,original_git = preparation.source_binding,preparation.git
        def bind(root=preparation.ROOT,source_root=None):
            if self.test_binding is not None and Path(root) == preparation.ROOT:
                return copy.deepcopy(self.test_binding)
            return original_binding(root,source_root) if source_root is not None else original_binding(root)
        def git(root,*arguments):
            if Path(root) == self.tagged_root:
                if arguments[0] == 'ls-tree':
                    return ('160000 commit '+self.tag_builds_commit+'\treferences/Hexalith.Builds\n').encode()
                return self.source_commit.encode()
            return original_git(root,*arguments)
        for patcher in (mock.patch.object(preparation,'source_binding',side_effect=bind),
                        mock.patch.object(preparation,'self_contained_checkout'),
                        mock.patch.object(preparation,'require_tagged_checkout'),
                        mock.patch.object(preparation,'git',side_effect=git)):
            patcher.start()
            self.addCleanup(patcher.stop)

    def testActualImportReadsPostgresqlTemplateFromTheBoundTaggedRoot(self):
        receipt,context = self.import_fixture(scope='operational')
        validate = qualification.validate_postgresql_component
        with mock.patch.object(qualification,'validate_postgresql_component',wraps=validate) as observed:
            self.assertEqual(qualification.receipt_outcome(receipt,context)[2]['execution'],'passed')
        self.assertTrue(observed.call_args_list)
        self.assertTrue(all(Path(call.args[1]) == self.tagged_root for call in observed.call_args_list))
        component = self.tagged_root/'deploy/dapr/statestore-postgresql.yaml'
        component.write_bytes(component.read_bytes()+b'\n# substituted bound template\n')
        with self.assertRaisesRegex(preparation.InvalidPacket,'PostgreSQL component template'):
            qualification.receipt_outcome(receipt,context)

    def testActualImportAcceptsSiblingTaggedSourceMountAndRejectsRehashedSubstitution(self):
        with tempfile.TemporaryDirectory(prefix='p1r-sibling-source-') as temporary:
            target = Path(temporary) / 'tagged-source'
            shutil.move(str(self.tagged_root), target)
            self.tagged_root = target
            receipt, context = self.import_fixture(scope='operational')
            self.assertEqual(qualification.receipt_outcome(receipt, context)[2]['execution'], 'passed')
            for mutation in ('mount', 'command'):
                receipt, context = self.import_fixture(scope='operational')
                if mutation == 'mount':
                    def change(value):
                        mount = next(row for row in value['runner_proof']['Mounts']
                                     if row['Destination'] == str(self.tagged_root))
                        mount['Destination'] = str(self.tagged_root.parent / 'substituted')
                    self.change_isolation(receipt, 'wrapper', change)
                else:
                    self.change_isolation(receipt, 'private',
                                          lambda value: value['command'].__setitem__(-1, str(preparation.ROOT)))
                    self.change_isolation(receipt, 'wrapper',
                                          lambda value: value['inner_command'].__setitem__(-1, str(preparation.ROOT)))
                with self.subTest(mutation=mutation), self.assertRaises(preparation.InvalidPacket):
                    qualification.receipt_outcome(receipt, context)

    def attach_isolation(self,receipt,context):
        binding = context['source_binding']
        production = {'head':self.source_commit,'diff_sha256':preparation.digest(b''),
            'files':[{'path':'src/Production.cs','sha256':'a'*64,'git_blob':'b'*40,'index_blob':'b'*40}],
            'gitlinks':{'references/Hexalith.Builds':self.tag_builds_commit}}
        binding['candidate_source'] = {'repository':str(self.tagged_root),'main':production,'retained_inputs':[],
            'python':binding['python'],'dependencies':[{'path':'references/Hexalith.Builds','initialized':True,
                'binding':dict(copy.deepcopy(production),head=self.tag_builds_commit)}]}
        self.test_binding = binding
        context['inputs']['value']['candidate'].update(tag='v'+self.candidate_version,tag_commit=self.source_commit,
            tag_builds_commit=self.tag_builds_commit,feed='https://api.nuget.org/v3/index.json')
        source_path = Path(self.temporary.name)/'executor-source.json'
        executor.write_json(source_path,binding)
        execution_path = Path(self.temporary.name)/'execution.json'
        execution = json.loads(execution_path.read_bytes())
        execution['executor_source_sha256'] = executor.digest(source_path.read_bytes())
        executor.write_json(execution_path,execution)
        receipt['execution_evidence'].update(executor_source=binding,executor_source_sha256=executor.digest(source_path.read_bytes()))
        receipt['output_sha256'] = executor.digest(executor.canonical(receipt['execution_evidence']))
        return super().attach_isolation(receipt,context)


class Published31260SnapshotWitnessTests(Published31251SnapshotWitnessTests):
    candidate_version = '3.126.0'
    source_commit = 'fcf78d6cafb6c0c9f52dc350a9a09aff54ded288'
    tag_builds_commit = '1dfc39e3a3e6ab1c8ce95138490b39d76e7feb2c'


class IndependentRecoverySelectionTests(unittest.TestCase):
    def setUp(self):
        import importlib.util
        path = preparation.ROOT/'_bmad-output/implementation-artifacts/evidence/6-1-p1r-31210-published-run/review-round-1/independent-audit-next.py'
        spec = importlib.util.spec_from_file_location('p1r_next_independent_audit',path)
        module = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(module)
        self.validate = module.require_recovery_selection
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.binding = {'private':'same frozen source closure'}
        self.inputs_sha256 = 'a'*64
        self.root = Path(self.temporary.name)/'original'
        self.supplement = Path(self.temporary.name)/'supplement'
        self.selected = []
        for directory,lane,restore in ((self.root,'post-upgrade-restore',True),(self.supplement,'checkout',False)):
            invocation = directory.name
            rows = [('lane',{'lane':lane})]
            if restore:
                rows.append(('restore',{'schema':runtime.RESTORE_SCHEMA,'role':'candidate'}))
            rows.append(('cleanup',{'schema':runtime.CLEANUP_SCHEMA,'invocation':invocation,'execution':'failed'}))
            paths = []
            for name,row in rows:
                row['inputs_sha256'] = self.inputs_sha256
                path = directory/'receipts'/(name+'.json')
                executor.write_json(path,row)
                paths.append(str(path))
            executor.write_json(directory/'executor-source.json',self.binding)
            executor.write_json(directory/'execution.json',{'invocation':invocation,'finished_utc':'2026-10-14T00:00:01+00:00',
                'inputs_sha256':self.inputs_sha256,'executor_source_sha256':executor.digest((directory/'executor-source.json').read_bytes()),
                'receipts':paths})
            self.selected.extend(paths)

    def testEveryContributingInvocationRetainsRecoveryEvenWhenCleanupFailed(self):
        coverage = self.validate(self.selected,self.root,self.inputs_sha256,self.binding)
        self.assertEqual(len(coverage),2)
        self.assertEqual(sum(len(row['required_recovery_receipts']) for row in coverage),3)

    def testOriginalRestoreCleanupAndSupplementCleanupCannotBeOmitted(self):
        for path in self.selected:
            if Path(path).name == 'lane.json':
                continue
            reduced = [name for name in self.selected if name != path]
            with self.subTest(path=path), self.assertRaisesRegex(preparation.InvalidPacket,'omits contributing invocation'):
                self.validate(reduced,self.root,self.inputs_sha256,self.binding)

    def testRemovingRestoreRegistrationOrChangingSupplementBindingIsRefused(self):
        path = self.root/'execution.json'
        execution = json.loads(path.read_text())
        original_execution = copy.deepcopy(execution)
        execution['receipts'] = [name for name in execution['receipts'] if not name.endswith('restore.json')]
        executor.write_json(path,execution)
        selected = [name for name in self.selected if not name.endswith('restore.json')]
        with self.assertRaisesRegex(preparation.InvalidPacket,'candidate restore'):
            self.validate(selected,self.root,self.inputs_sha256,self.binding)
        executor.write_json(path,original_execution)
        executor.write_json(self.supplement/'executor-source.json',{'private':'substituted source'})
        with self.assertRaisesRegex(preparation.InvalidPacket,'different source closure'):
            self.validate(self.selected,self.root,self.inputs_sha256,self.binding)


class MeasurementTests(unittest.TestCase):
    def testHistoricalSnapshotRepairReplacesEntireDtoAndPreservesOtherPersistedState(self):
        worker = executor.Executor.__new__(executor.Executor)
        worker.active, worker.redis = [], "owned"
        key = "eventstore||tenant-a:counter:fixture:snapshot"
        snapshot = {"sequenceNumber": 10, "state": {"currentSequence": 10, "events": [{"payload": "e30="}],
                    "snapshotState": None, "Count": 10}, "createdAt": "2026-01-01T00:00:00Z",
                    "tenantId": "tenant-a", "domain": "counter", "aggregateId": "fixture",
                    "protectionMetadata": {"state": 0}, "snapshotEnvelopeVersion": 0}
        values = {key: executor.canonical(snapshot),
                  "eventstore||tenant-a:counter:fixture:events:7": executor.canonical({"payload": "original"}),
                  "eventstore||tenant-a:counter:fixture:metadata": executor.canonical({"currentSequence": 12, "retainedFloor": 5}),
                  "eventstore||tenant-b:counter:fixture:snapshot": executor.canonical(snapshot | {"tenantId": "tenant-b"})}
        before = copy.deepcopy(values)
        worker.raw_state = mock.Mock(return_value=values)
        worker.postgres_write = mock.Mock()
        worker.postgres_delete = mock.Mock()

        worker.mutate("snapshot-nine")

        worker.postgres_write.assert_called_once_with("owned", key, snapshot | {"sequenceNumber": 9, "state": {"count": 9}})
        worker.postgres_delete.assert_not_called()
        self.assertEqual(values, before)

    def testHistoricalSnapshotRepairRefusesAnActiveWriterBeforeAnyProviderRead(self):
        worker = executor.Executor.__new__(executor.Executor)
        worker.active = ["live-writer"]
        worker.raw_state = mock.Mock()
        worker.postgres_write = mock.Mock()
        with self.assertRaisesRegex(executor.InvalidPacket, "writer is active"):
            worker.mutate("snapshot-nine")
        worker.raw_state.assert_not_called()
        worker.postgres_write.assert_not_called()

    def testSuccessorSnapshotSeedUsesNineManualCreationAndThreeActualAppends(self):
        writers = [(version, version) for version in ("3.121.0", "3.122.1", "3.125.0", "3.125.1", "3.126.0")]
        for candidate, version in writers + [("3.125.0", "source"), ("3.125.1", "source")]:
            with self.subTest(candidate=candidate, version=version):
                worker, checks = self.snapshotSeedWorker({"sequenceNumber": 9, "snapshotEnvelopeVersion": 1, "state": {"count": 9}},
                    candidate=candidate)
                worker.seed(version, 10, checks)
                self.assertEqual([call.args[0] for call in worker.probe.call_args_list], [version] * 4)
                self.assertEqual([call.args[1][0] for call in worker.probe.call_args_list], ["seed", "manual-snapshot", "seed", "seed"])
                self.assertEqual([call.args[1][-1] for call in worker.probe.call_args_list if call.args[1][0] == "seed"], ["9", "3", "3"])
                worker.mutate.assert_not_called()
                self.assertEqual(checks.counter["failed"], 0)
                self.assertEqual(worker.last_snapshot_creation["persisted_snapshot"]["snapshotEnvelopeVersion"], 1)

    def testUnqualifiedOrMissingSuccessorSnapshotRemainsFailedWithoutSqlSynthesis(self):
        writers = [(version, version) for version in ("3.121.0", "3.122.1", "3.125.0", "3.125.1", "3.126.0")]
        for candidate, version in writers + [("3.125.0", "source"), ("3.125.1", "source")]:
            for snapshot in (None, {"sequenceNumber": 9, "state": {"count": 9}},
                             {"sequenceNumber": 9, "snapshotEnvelopeVersion": 1, "state": {"count": 8}}):
                with self.subTest(candidate=candidate,version=version,snapshot=snapshot):
                    worker, checks = self.snapshotSeedWorker(snapshot,candidate=candidate)
                    with self.assertRaises(executor.SeedFailure):
                        worker.seed(version, 10, checks)
                    self.assertGreater(checks.counter["failed"], 0)
                    worker.mutate.assert_not_called()

    def testUnenrolledSuccessorSeedRefusesBeforeManualSnapshotOrDiagnosticSynthesis(self):
        for candidate,version in (("3.125.0","3.122.0"),("3.125.0","3.122.2"),
                                  ("3.125.0","3.125.2"),("3.125.2","3.125.2"),
                                  ("3.125.2","source"),("3.127.0","3.127.0"),("3.127.0","source"),
                                  ("3.125.1","3.127.0"),("3.125.1","3.125.0"),
                                  ("3.125.0","3.121.0")):
            worker, checks = self.snapshotSeedWorker(None,candidate=candidate)
            with self.subTest(candidate=candidate,version=version), self.assertRaisesRegex(executor.InvalidPacket,"not explicitly enabled"):
                worker.seed(version,10,checks)
            worker.probe.assert_not_called()
            worker.mutate.assert_not_called()
            worker.raw_state.assert_not_called()
            worker.stop_nodes.assert_not_called()
            worker.postgres_query.assert_not_called()
            worker.start_nodes.assert_not_called()

    def testEverySeedIntervalRefusesUnenrolledCandidatesAndWrongWritersBeforeMutation(self):
        for interval in (10, 50, 1000):
            for candidate, version in (("3.127.0", "source"), ("3.127.0", "3.127.0"),
                                       ("3.127.0", "3.110.0"), ("3.125.2", "source"),
                                       ("3.125.1", "3.125.0"), ("3.125.1", "3.127.0"),
                                       ("3.125.1", "unknown")):
                worker, checks = self.snapshotSeedWorker(None, candidate=candidate)
                with self.subTest(interval=interval, candidate=candidate, version=version):
                    with self.assertRaisesRegex(executor.InvalidPacket, "not explicitly enabled"):
                        worker.seed(version, interval, checks)
                    worker.stop_nodes.assert_not_called()
                    worker.postgres_query.assert_not_called()
                    worker.start_nodes.assert_not_called()
                    worker.probe.assert_not_called()
                    worker.mutate.assert_not_called()

    def testCaseRetainsSharedFixtureSourceForEnrolledPackageAndSourceSeedsOnly(self):
        for candidate,writer in (("3.121.0","3.121.0"),("3.122.1","3.122.1"),("3.125.0","3.125.0"),("3.125.1","3.125.1"),
                                 ("3.125.1","source"),("3.125.0","source"),
                                 ("3.122.2","3.122.2"),("3.125.2","3.125.2"),
                                 ("3.127.0","source"),("3.125.0",None)):
            with self.subTest(candidate=candidate,writer=writer):
                worker = executor.Executor.__new__(executor.Executor)
                worker.commands, worker.configurations, worker.errors = [], [], []
                worker.candidate = candidate
                worker.last_snapshot_creation = {"writer":writer} if writer is not None else None
                def action(checks):
                    worker.commands.append({"id":1})
                    return {"seed_snapshot":worker.last_snapshot_creation},"effect","compatible"
                _,evidence = worker.case("snapshot-tail",(writer or candidate)+"-to-3.110.0",action,False)
                if candidate in ("3.122.2","3.125.2","3.127.0") or writer is None:
                    self.assertNotIn("snapshot_fixture_source",evidence)
                else:
                    path = preparation.ROOT/"tools/p1r-published-consumers/FixtureSnapshotQualification.cs"
                    self.assertEqual(evidence["snapshot_fixture_source"],
                        {"path":str(path),"sha256":executor.digest(path.read_bytes()),"content":path.read_text()})

    def testHistoricalSnapshotSeedPreservesSeparateDiagnosticFixture(self):
        worker, checks = self.snapshotSeedWorker(None)
        worker.mutate = mock.Mock()
        worker.probe = mock.Mock(side_effect=[{"committed": 12}, {"committed": 3}])
        raw, rows = verifier_fixture()
        worker.inventory = mock.Mock(return_value=rows)
        worker.raw_state = mock.Mock(return_value=raw)
        worker.seed("3.110.0", 10, checks)
        worker.mutate.assert_called_once_with("snapshot-nine")
        worker.raw_state.assert_called_once()
        self.assertIsNone(worker.last_snapshot_creation)

    @staticmethod
    def snapshotSeedRows(count):
        return verifier_fixture(head=9 if count == 9 else 12)[1]

    def snapshotSeedWorker(self, snapshot, candidate="3.121.0"):
        worker = executor.Executor.__new__(executor.Executor)
        worker.candidate, worker.redis, worker.sidecar_port, worker.domain_port = candidate, "owned", 10001, 10002
        worker.stop_nodes = worker.postgres_query = worker.start_nodes = mock.Mock()
        worker.mutate = mock.Mock(side_effect=AssertionError("successor snapshot cannot be synthesized"))
        worker.http = mock.Mock(return_value={"snapshot_qualification": {
            "registered": True, "test_only": True, "state_roundtrip": True, "original_count": 9,
            "roundtrip_count": 9, "canonical_event_parity": True, "canonical_event_sha256": executor.digest(b"{}"),
            "detached_apply": True, "applied_count": 10}})
        worker.probe = mock.Mock(side_effect=[{"committed": 9, "sequence": 9}, {"outcome": "Created", "sequence": 9},
            {"committed": 3, "sequence": 12}, {"committed": 3, "sequence": 3}])
        raw, _ = verifier_fixture(snapshot=False)
        if snapshot is not None:
            raw["tenant-a:counter:fixture:snapshot"] = executor.canonical(snapshot)
        worker.raw_state = mock.Mock(return_value=raw)
        prefix_raw, _ = verifier_fixture(head=9, snapshot=False)
        if snapshot is not None:
            prefix_raw["tenant-a:counter:fixture:snapshot"] = executor.canonical(snapshot)
        worker.inventory = mock.Mock(side_effect=[verifier_rows(prefix_raw), verifier_rows(raw)])
        return worker, executor.Measurements()

    def testRestoreReceiptAttributesBackupAndDatabaseCreationToTheirCommands(self):
        for candidate in ("3.125.0", "3.125.1", "3.126.0"):
            with self.subTest(candidate=candidate):
                with tempfile.TemporaryDirectory() as scratch, mock.patch.object(executor, "reserve_port", return_value=15432):
                    worker = CleanupTests.worker(self, scratch)
                    worker.candidate = candidate
                    worker.redis = "source-id"
                    worker.postgres_password = "<TEST_PASSWORD>"
                    worker.inputs = {"operational_profile": {}, "candidate": {"packages": []}}
                    worker.raw_state = mock.Mock(return_value=verifier_fixture(floor=5)[0])

                    def run(argv, **kwargs):
                        output = b""
                        if argv[:2] == ["docker", "inspect"]:
                            name = argv[-1]
                            output = (json.dumps({"Id": "restored-id", "Name": "/" + name,
                                "Config": {"Labels": {"hexalith.p1r.invocation": worker.invocation}}}) + "\n").encode()
                        elif argv[:2] == ["docker", "cp"] and ":/tmp/" in str(argv[2]):
                            Path(argv[3]).write_bytes(b"synthetic-backup")
                        worker.record(argv, worker.scratch, executor.stamp(), 0, output)
                        return output

                    def inventory(*args):
                        run(["synthetic-inventory"])
                        return []

                    def actor(*args):
                        run(["synthetic-actor"])
                        return {"accepted": True, "event_count": 1}

                    worker.run = run
                    worker.inventory = inventory
                    worker.actor = mock.Mock(side_effect=actor)
                    worker.seed, worker.mutate, worker.stop_nodes = mock.Mock(), mock.Mock(), mock.Mock()
                    worker.start_nodes = mock.Mock(side_effect=lambda *args, **kwargs: [
                        {"pid": 1, "start_ticks": 1, "pgid": 1, "session": 1}])

                    worker.restore_case("synthetic-restore", executor.Measurements())
                    worker.seed.assert_called_once_with(candidate, 10, mock.ANY)
                    self.assertEqual([call.args[0] for call in worker.start_nodes.call_args_list], [candidate, candidate])
                    self.assertTrue(all(call.args[0] == candidate for call in worker.actor.call_args_list))
                    commands = {row["step"]: row for row in worker.pending_restore["commands"]}
                    self.assertEqual(commands["backup"]["argv"][:4], ["docker", "exec", "source-id", "pg_dump"])
                    self.assertEqual(commands["create-database"]["argv"], ["docker", "start", "restored-id"])
                    self.assertEqual(commands["backup"]["output_sha256"], executor.digest(b"synthetic-backup"))
                    for step in ("backup", "create-database"):
                        recorded = next(row for row in worker.commands if row["argv"] == commands[step]["argv"])
                        for field in ("cwd", "started_utc", "finished_utc", "exit_code"):
                            self.assertEqual(commands[step][field], recorded[field])

    def testPrivateProbeBinaryAndExactInputDigestAreCapturedByCommands(self):
        with tempfile.TemporaryDirectory() as scratch:
            worker = CleanupTests.worker(self,scratch)
            binary = worker.scratch/'Probe.dll'
            binary.write_bytes(b'private synthetic binary')
            witness = {'path':str(binary),'sha256':executor.digest(binary.read_bytes()),'role':'candidate','configuration':'Release'}
            worker.probe_binaries = {str(binary):witness}
            command = worker.record(['dotnet',binary,'identity'],worker.scratch,executor.stamp(),0,b'{}')
            self.assertEqual(command['probe_binary'],witness)
            serving = dict(witness,project='Host')
            worker.consumer_binaries = {str(binary):serving}
            command = worker.record(['dotnet',binary],worker.scratch,executor.stamp(),0,b'host ready')
            self.assertEqual(command['consumer_binary'],serving)
            query = 'SELECT fixture'
            command = worker.record(['docker','exec','-i','owned','psql'],worker.scratch,executor.stamp(),0,b'[]',input_bytes=query.encode())
            self.assertEqual(command['provider_sql'],query)
            self.assertEqual(command['input_sha256'],executor.digest(query.encode()))
            payload = b'private exact query input'
            result = worker.run([sys.executable,'-I','-c','import sys; sys.stdout.buffer.write(sys.stdin.buffer.read())'],input_bytes=payload)
            self.assertEqual(result,payload)
            self.assertEqual(worker.commands[-1]['input_sha256'],executor.digest(payload))
            self.assertEqual(worker.commands[-1]['input_bytes'],len(payload))

    def testHttpCommandRecordClampsEarlierFinish(self):
        worker = executor.Executor.__new__(executor.Executor)
        worker.commands, worker.configurations = [], []
        worker.save = mock.Mock()
        started = "2026-10-10T12:00:00.001300+00:00"
        with mock.patch.object(executor, "stamp", return_value="2026-10-10T12:00:00+00:00"):
            row = worker.record(["HTTP", "GET", "http://127.0.0.1:1/ready"], Path("/tmp"), started, 0, b"ready")
        self.assertEqual(row["finished_utc"], started)
        preparation.validate_times(row)

    def testPostgresqlV1InventoryKeepsActorJsonAndDecodesBookkeeping(self):
        identity = "a" * 64
        observed = {"Id": identity, "Config": {"Labels": {"hexalith.p1r.invocation": "owned"}}}
        rows = [
            ["eventstore||AggregateActor||tenant-a:counter:fixture||tenant-a:counter:fixture:events:12",
             {"sequenceNumber": 12}],
            ["eventstore||AggregateActor||tenant-a:counter:fixture||tenant-a:counter:fixture:metadata",
             {"currentSequence": 12, "retainedFloor": 5}],
            ["eventstore||tenant-a:message:status", "eyJzdGF0dXMiOjV9"],
        ]
        with mock.patch.object(executor.subprocess, "check_output", side_effect=[
                (json.dumps(observed) + "\n").encode(), (json.dumps(rows) + "\n").encode()]):
            inventory = executor.postgres_inventory(identity, "owned")
        self.assertEqual([row["kind"] for row in inventory], ["event", "metadata", "bookkeeping"])
        self.assertEqual(inventory[1]["floor"], 5)
        self.assertEqual(inventory[0]["sha256"], executor.digest(executor.canonical({"sequenceNumber": 12})))
        self.assertEqual(executor.postgres_value("eyJzdGF0dXMiOjV9"), b'{"status":5}')

    def testPostgresqlMutationRetainsJsonObjectShape(self):
        worker = executor.Executor.__new__(executor.Executor)
        worker.containers = {"owned": {"image": executor.POSTGRES}}
        worker.run = mock.Mock(return_value=b"UPDATE 1\n")
        worker.postgres_write("owned", "tenant-a:counter:fixture:metadata", {"currentSequence": 12, "retainedFloor": 5})
        sql = worker.run.call_args.kwargs["input_bytes"].decode()
        self.assertIn(executor.canonical({"currentSequence": 12, "retainedFloor": 5}).hex(), sql)
        self.assertIn("::jsonb", sql)

    def testPostgresqlInventoryNormalizesDatabaseCollationOrder(self):
        identity = "a" * 64
        observed = {"Id": identity, "Config": {"Labels": {"hexalith.p1r.invocation": "owned"}}}
        rows = [["eventstore||eventstore:reminders:v1:control", {}],
                ["eventstore||GlobalPositionActor||global||current-global-position", {}]]
        with mock.patch.object(executor.subprocess, "check_output", side_effect=[
                (json.dumps(observed) + "\n").encode(), (json.dumps(rows) + "\n").encode()]):
            inventory = executor.postgres_inventory(identity, "owned")
        self.assertEqual([row["key"] for row in inventory], sorted(row[0] for row in rows))

    def testPostgresqlInventoryRejectsMalformedStreamState(self):
        identity = "a" * 64
        observed = {"Id": identity, "Config": {"Labels": {"hexalith.p1r.invocation": "owned"}}}
        for suffix in (":metadata", ":snapshot"):
            with self.subTest(suffix), mock.patch.object(executor.subprocess, "check_output", side_effect=[
                    (json.dumps(observed) + "\n").encode(),
                    (json.dumps([["eventstore||AggregateActor||tenant-a:counter:fixture||tenant-a:counter:fixture" + suffix,
                                  ["malformed"]]]) + "\n").encode()]):
                with self.assertRaisesRegex(preparation.InvalidPacket, "invalid persisted .* content"):
                    executor.postgres_inventory(identity, "owned")

    def testCandidateTrustedEffectsRequirePersistedAudit(self):
        for operation, observed, check_id in (
                ("trusted-effect", {"accepted": True, "sequence": 13, "replayed": True},
                 "candidate-trusted-effect-audit-persisted"),
                ("unauthorized-effect", {"accepted": False, "sequence": 12, "unexpected": False,
                                         "unauthorized_refused": True, "denial": "invalid-gateway-proof"},
                 "candidate-unauthorized-effect-audit-persisted")):
            with self.subTest(operation):
                worker = executor.Executor.__new__(executor.Executor)
                worker.redis, worker.host_port = "owned", 12345
                with mock.patch.object(worker, "probe", return_value={"actor_methods": ["ProcessTrustedEffectAsync"]}), \
                     mock.patch.object(worker, "seed"), mock.patch.object(worker, "inventory", side_effect=[[], []]), \
                     mock.patch.object(worker, "start_nodes"), mock.patch.object(worker, "stop_nodes"), \
                     mock.patch.object(worker, "http", return_value=observed), \
                     mock.patch.object(worker, "postgres_audits", return_value=[]):
                    checks = executor.Measurements()
                    worker.mixed_case("3.119.0-" + operation, checks)
                self.assertIn({"id": check_id, "passed": False}, checks.checks)

    def testCandidateAuditMatchRequiresExactlyOneMatchingRecord(self):
        trusted = {"action": "submission", "tenant": "tenant-a", "effectid": "effect-1", "workload": "p1r-fixture",
                   "purpose": "published-qualification", "disposition": "authorized"}
        denied = dict(trusted, action="gateway-proof", effectid=None, disposition="denied")
        observed = {"trusted-effect": {"accepted": True, "sequence": 13, "replayed": True},
                    "unauthorized-effect": {"accepted": False, "sequence": 12, "unexpected": False,
                                            "unauthorized_refused": True, "denial": "invalid-gateway-proof"}}
        for operation, records, passed in (
                ("trusted-effect", [trusted], True),
                ("trusted-effect", [trusted, dict(trusted)], False),
                ("trusted-effect", [dict(trusted, action="gateway-proof")], False),
                ("trusted-effect", [dict(trusted, disposition="denied")], False),
                ("trusted-effect", [dict(trusted, tenant="tenant-b")], False),
                ("trusted-effect", [dict(trusted, workload="other-workload")], False),
                ("trusted-effect", [dict(trusted, purpose="other-purpose")], False),
                ("trusted-effect", [dict(trusted, effectid=None)], False),
                ("unauthorized-effect", [denied], True),
                ("unauthorized-effect", [dict(denied, effectid="effect-1")], False),
                ("unauthorized-effect", [trusted], False)):
            with self.subTest(operation=operation, records=records):
                worker = executor.Executor.__new__(executor.Executor)
                worker.redis, worker.host_port = "owned", 12345
                with mock.patch.object(worker, "probe", return_value={"actor_methods": ["ProcessTrustedEffectAsync"]}), \
                     mock.patch.object(worker, "seed"), mock.patch.object(worker, "inventory", side_effect=[[], []]), \
                     mock.patch.object(worker, "start_nodes"), mock.patch.object(worker, "stop_nodes"), \
                     mock.patch.object(worker, "http", return_value=observed[operation]), \
                     mock.patch.object(worker, "postgres_audits",
                                       return_value=[{"key": "audit", "record": record} for record in records]):
                    checks = executor.Measurements()
                    worker.mixed_case("3.119.0-" + operation, checks)
                self.assertIn({"id": "candidate-" + operation + "-audit-persisted", "passed": passed}, checks.checks)

    def testPostgresqlAuditsDecodeDaprBinaryRowsAndFoldFieldNames(self):
        worker = executor.Executor.__new__(executor.Executor)
        worker.containers = {"owned": {"image": executor.POSTGRES}}
        rows = [["eventstore||p1r-qualification-audit-1",
                 base64.b64encode(json.dumps({"action": "submission", "effectId": "effect-1"}).encode()).decode()],
                ["eventstore||p1r-qualification-audit-2", base64.b64encode(b"not-json").decode()]]
        worker.run = mock.Mock(return_value=json.dumps(rows).encode())
        audits = worker.postgres_audits("owned")
        self.assertEqual([row["record"] for row in audits], [{"action": "submission", "effectid": "effect-1"}, {}])
        self.assertIn("p1r-qualification-audit-%", worker.run.call_args.kwargs["input_bytes"].decode())

    def testPostgresqlDiagnosticsRefuseUnownedContainers(self):
        identity = "a" * 64
        for observed in ({"Id": identity, "Config": {"Labels": {"hexalith.p1r.invocation": "other"}}},
                         {"Id": "b" * 64, "Config": {"Labels": {"hexalith.p1r.invocation": "owned"}}}):
            with self.subTest(observed=observed), mock.patch.object(
                    executor.subprocess, "check_output", return_value=(json.dumps(observed) + "\n").encode()) as check_output:
                with self.assertRaisesRegex(preparation.InvalidPacket, "unowned provider inventory"):
                    executor.postgres_inventory(identity, "owned")
                self.assertEqual(check_output.call_count, 1)
        worker = executor.Executor.__new__(executor.Executor)
        worker.run = mock.Mock()
        for containers in ({}, {"owned": {"image": executor.REDIS}}):
            worker.containers = containers
            with self.subTest(containers=containers), \
                 self.assertRaisesRegex(preparation.InvalidPacket, "unowned PostgreSQL diagnostic"):
                worker.postgres_query("owned", "TRUNCATE TABLE state;")
        worker.run.assert_not_called()

    def testRenderedPostgresqlCredentialIsAbsentFromAppEnvironments(self):
        with tempfile.TemporaryDirectory() as temporary, \
             mock.patch.dict(os.environ, {"POSTGRES_CONNECTION_STRING": "inherited-secret"}), \
             mock.patch.object(executor, "reserve_port", side_effect=range(12000, 12012)), \
             mock.patch.object(executor.time, "sleep"):
            root = Path(temporary)
            (root / "configurations").mkdir()
            tooling,release = root/'tooling',root/'release'
            template = (executor.ROOT/'deploy/dapr/statestore-postgresql.yaml').read_text()
            for path,marker in ((tooling,'tooling'),(release,'release')):
                (path/'deploy/dapr').mkdir(parents=True)
                (path/'deploy/dapr/statestore-postgresql.yaml').write_text(template+'\n# '+marker+' template\n')
            worker = executor.Executor.__new__(executor.Executor)
            worker.output = worker.scratch = root
            worker.source_root = release
            worker.inputs = {"operational_profile": {"runtime_version": "1.18.2"}}
            worker.redis, worker.redis_port, worker.pubsub_port = "owned", 15432, 16379
            worker.postgres_password = "<TEST_PASSWORD>"
            worker.invocation = "a" * 32
            worker.digest_key = worker.delegation = worker.app_token = worker.workload_key = "fixture"
            worker.evidence = {"candidate": root}
            worker.daprd, worker.placement_port, worker.scheduler_port = root / "daprd", 50005, 50006
            worker.configurations, worker.active = [], []
            environments = []
            worker.stop_nodes = mock.Mock()
            worker.wait = mock.Mock()
            worker.launch = lambda argv, environment: environments.append(environment) or {"ownership": mock.Mock(root={})}
            with mock.patch.object(executor,'ROOT',tooling):
                worker.start_nodes(executor.CANDIDATE)
            self.assertEqual(len(environments), 4)
            self.assertTrue(all("POSTGRES_CONNECTION_STRING" not in value for value in environments))
            self.assertIn("password=<TEST_PASSWORD>", (root / "resources/state.yaml").read_text())
            self.assertIn('# release template',(root/'resources/state.yaml').read_text())
            self.assertNotIn('# tooling template',(root/'resources/state.yaml').read_text())
            self.assertNotIn("<TEST_PASSWORD>", json.dumps(worker.configurations))
            self.assertIsNotNone(worker.configurations[0]["source_workload_authority"])
            qualification.validate_postgresql_component(
                next(file for file in worker.configurations[0]["files"] if file["name"] == "state.yaml"),release)
            wrong = dict(next(file for file in worker.configurations[0]['files'] if file['name'] == 'state.yaml'),
                         content=(tooling/'deploy/dapr/statestore-postgresql.yaml').read_text())
            wrong['sha256'] = preparation.digest(wrong['content'].encode())
            with self.assertRaisesRegex(preparation.InvalidPacket,'component template'):
                qualification.validate_postgresql_component(wrong,release)

    def testDockerDiscoverySelectsOnlyPreservationAndOwnershipFields(self):
        argv = executor.safe_inspect_argv("synthetic-id")
        self.assertEqual(argv[:3], ["docker", "inspect", "--format"])
        self.assertEqual(argv[-1], "synthetic-id")
        self.assertNotIn(".Config.Env", argv[3])
        self.assertNotIn("{{json .Config}}", argv[3])
        observed = {"Id": "synthetic-id", "Image": "sha256:synthetic", "Name": "/synthetic",
            "State": {"Running": True, "StartedAt": "synthetic-time"},
            "Config": {"Labels": {"hexalith.p1r.invocation": "synthetic-control"}}}
        self.assertEqual(executor.docker_observations((json.dumps(observed) + "\n").encode()), [observed])
    def testNaturalReminderRequiresCompletedReadOnlyObservationBeforeInjection(self):
        worker = executor.Executor.__new__(executor.Executor)
        worker.sidecar_port, worker.commands = 12345, []
        observed_arguments = []
        def probe(version, arguments, checks, prefix, timeout=180):
            observed_arguments.append(arguments)
            worker.commands.append({"id": len(worker.commands) + 1})
            checks.check(prefix + "actor-sequence-observed", True)
            return {"sequence": 12 if len(observed_arguments) == 1 else 13}
        with mock.patch.object(worker, "probe", side_effect=probe), mock.patch.object(executor.time, "sleep"):
            checks = executor.Measurements()
            observed = worker.natural_reminder(checks)
        self.assertTrue(observed["completed"])
        self.assertFalse(observed["manual_callback_sent_before_observation"])
        self.assertEqual([row["sequence"] for row in observed["observations"]], [12, 13])
        self.assertTrue(all(arguments[0] == "sequence" for arguments in observed_arguments))
        self.assertEqual(checks.counter, {"attempted": 3, "passed": 3, "failed": 0})

    def testExpiredNaturalReminderObservationIsNonpassing(self):
        worker = executor.Executor.__new__(executor.Executor)
        checks = executor.Measurements()
        with mock.patch.object(executor.time, "monotonic", side_effect=[0, 31]):
            observed = worker.natural_reminder(checks)
        self.assertFalse(observed["completed"])
        self.assertEqual(observed["observations"], [])
        self.assertEqual(checks.counter, {"attempted": 1, "passed": 0, "failed": 1})

    def reminderWorker(self):
        worker = executor.Executor.__new__(executor.Executor)
        worker.candidate, worker.host_port, worker.sidecar_port = '3.125.1', 10001, 10002
        worker.seed, worker.stop_nodes, worker.start_nodes = mock.Mock(), mock.Mock(), mock.Mock()
        worker.inventory = mock.Mock(return_value=[])
        worker.probe = mock.Mock(return_value={'sequence':12})
        worker.natural_reminder = mock.Mock(return_value={'completed':True})
        def http(method,url,body):
            parsed = executor.urllib.parse.urlsplit(url)
            revision = int(parsed.path.rsplit('/',1)[-1])
            query = executor.urllib.parse.parse_qs(parsed.query)
            due = query.get('due',[worker.reminder_due])[0]
            identity = {'intent_due':due,'intent_revision':revision,
                        'reminder_name':executor.witnesses.reminder_name(due,revision)}
            return dict(identity, **({'armed':1,'unresolved':0,'quarantined':0,'submitted':0}
                                    if '/register/' in url else {'sequence':13,'callback_status':204}))
        worker.http = mock.Mock(side_effect=http)
        return worker

    def testReminderFutureLeadSurvivesSlowReadinessAndPreservesArmedDue(self):
        worker = self.reminderWorker()
        moments = ['2026-10-18T00:00:00+00:00','2026-10-18T00:01:10+00:00',
                   '2026-10-18T00:01:10.100000+00:00','2026-10-18T00:01:50+00:00',
                   '2026-10-18T00:01:50.100000+00:00']
        checks = executor.Measurements()
        with mock.patch.object(executor,'stamp',side_effect=moments), mock.patch.object(executor.time,'sleep'):
            observed,_,_ = worker.reminder_case('scheduled-restart-effect',checks)
        self.assertEqual({window['due_utc'] for window in observed['deadline_windows']}, {'2026-10-18T00:02:00+00:00'})
        self.assertEqual(checks.counter['failed'],0)
        self.assertEqual(worker.http.call_count,3)

    def testReminderMissedInitialOrRestartWindowRefusesWithoutRescheduling(self):
        for phase,moments,posts in (
            ('initial',['2026-10-18T00:00:00+00:00','2026-10-18T00:02:01+00:00'],0),
            ('initial-response',['2026-10-18T00:00:00+00:00','2026-10-18T00:01:10+00:00',
                                 '2026-10-18T00:02:01+00:00'],1),
            ('restart',['2026-10-18T00:00:00+00:00','2026-10-18T00:01:10+00:00',
                        '2026-10-18T00:01:10.100000+00:00','2026-10-18T00:02:01+00:00'],1)):
            worker,checks = self.reminderWorker(),executor.Measurements()
            with self.subTest(phase=phase), mock.patch.object(executor,'stamp',side_effect=moments), \
                    mock.patch.object(executor.time,'sleep'):
                with self.assertRaisesRegex(preparation.InvalidPacket,'future deadline missed'):
                    worker.reminder_case('scheduled-restart-effect',checks)
                self.assertEqual(worker.http.call_count,posts)
                self.assertEqual(worker.reminder_due,'2026-10-18T00:02:00+00:00')
                self.assertEqual(checks.counter['failed'],1)

    def testReminderRevisionTwoHasItsOwnFutureStartupLead(self):
        worker,checks = self.reminderWorker(),executor.Measurements()
        moments = ['2026-10-18T00:00:00+00:00','2026-10-18T00:01:10+00:00',
                   '2026-10-18T00:01:10.100000+00:00','2026-10-18T00:02:40+00:00',
                   '2026-10-18T00:03:50+00:00','2026-10-18T00:03:50.100000+00:00']
        with mock.patch.object(executor,'stamp',side_effect=moments), mock.patch.object(executor.time,'sleep'):
            observed,_,_ = worker.reminder_case('stale-generation-refusal',checks)
        self.assertEqual(observed['deadline_windows'][0]['due_utc'],'2026-10-18T00:02:00+00:00')
        self.assertEqual(observed['deadline_windows'][2]['due_utc'],'2026-10-18T00:04:40+00:00')
        callback = executor.urllib.parse.urlsplit(worker.http.call_args.args[1])
        self.assertEqual(executor.urllib.parse.parse_qs(callback.query), {
            'due':[observed['registration']['intent_due']], 'reminderName':[observed['registration']['reminder_name']]})
        self.assertNotEqual(observed['registration']['reminder_name'],observed['rearmed']['reminder_name'])
        self.assertEqual(observed['repeated_or_stale']['reminder_name'],observed['registration']['reminder_name'])
        self.assertEqual(checks.counter['failed'],0)

    def testReminderIndependentCodecMatchesPublishedGoldenVectors(self):
        for item,revision,expected in (
            ('item-1',0,'DH9MMM0QHN2MWP2XDVTVGDQ8PMVMN8KDNFAPWMAJQNA7T0E6PB20'),
            ('item-1',1,'AED0KV4P0SF4RAQGXRJ8AZJEJ1NE6TVF5GWFHD0Z3HRMVRSQA8D0'),
            ('Item-1',1,'6P3J38VYKVQCKFH8WGX4ZP2KCD7QJVT8MN0D74K352JFEMV75NYG')):
            with self.subTest(item=item,revision=revision):
                self.assertEqual(executor.witnesses.reminder_name('2026-10-01T09:30:00+00:00',revision,item=item),'date-wrs-'+expected)

    def testReminderProducerRetainsRejectedActorRequestAsFailedMeasurement(self):
        for status in (403,404,500,None,True):
            worker,checks = self.reminderWorker(),executor.Measurements()
            invoke = worker.http.side_effect
            def http(method,url,body):
                response = invoke(method,url,body)
                if '/callback/' in url:response['callback_status'] = status
                return response
            worker.http.side_effect = http
            moments = ['2026-10-18T00:00:00+00:00','2026-10-18T00:01:10+00:00',
                       '2026-10-18T00:01:10.100000+00:00','2026-10-18T00:01:50+00:00',
                       '2026-10-18T00:01:50.100000+00:00']
            with self.subTest(status=status),mock.patch.object(executor,'stamp',side_effect=moments),mock.patch.object(executor.time,'sleep'):
                observed,_,_ = worker.reminder_case('scheduled-restart-effect',checks)
                self.assertEqual(checks.counter['failed'],1)
                self.assertIn({'id':'callback-actor-request-accepted','passed':False},checks.checks)
                self.assertEqual(observed['repeated_or_stale']['callback_status'],status)

    def testReminderIdentityImportRejectsUnknownTokenAndRehashedSubstitutions(self):
        worker,checks = self.reminderWorker(),executor.Measurements()
        moments = ['2026-10-18T00:00:00+00:00','2026-10-18T00:01:10+00:00',
                   '2026-10-18T00:01:10.100000+00:00','2026-10-18T00:02:40+00:00',
                   '2026-10-18T00:03:50+00:00','2026-10-18T00:03:50.100000+00:00']
        with mock.patch.object(executor,'stamp',side_effect=moments),mock.patch.object(executor.time,'sleep'):
            observed,_,_ = worker.reminder_case('stale-generation-refusal',checks)
        rows = [({'argv':['HTTP','POST',call.args[1]]},observed[key])
                for call,key in zip(worker.http.call_args_list,('registration','rearmed','repeated_or_stale'))]
        values = {row['id']:row['passed'] for row in checks.checks}
        executor.witnesses.validate_reminder_identity(observed,rows,values,'stale-generation-refusal')
        for mutation in ('due','name','revision','request','unknown-token','missing-query','rearmed','check','status'):
            changed,commands,verdicts = copy.deepcopy(observed),copy.deepcopy(rows),dict(values)
            if mutation in ('due','name','revision'):
                key = {'due':'intent_due','name':'reminder_name','revision':'intent_revision'}[mutation]
                changed['registration'][key] = {'due':changed['due'],'name':changed['rearmed']['reminder_name'],'revision':2}[mutation]
            elif mutation == 'request':commands[-1][0]['argv'][2] += '&due=2026-10-18T00%3A04%3A40%2B00%3A00'
            elif mutation == 'unknown-token':
                # The previous fixture recomputed revision one with revision two's current due.
                due = changed['rearmed']['intent_due']
                name = executor.witnesses.reminder_name(due,1)
                changed['repeated_or_stale'].update(intent_due=due,reminder_name=name)
                commands[-1][0]['argv'][2] = 'http://127.0.0.1/reminder/callback/1?'+executor.urllib.parse.urlencode({'due':due,'reminderName':name})
            elif mutation == 'missing-query':commands[-1][0]['argv'][2] = 'http://127.0.0.1/reminder/callback/1'
            elif mutation == 'rearmed':changed['rearmed']['reminder_name'] = changed['registration']['reminder_name']
            elif mutation == 'status':changed['repeated_or_stale']['callback_status'] = 403
            else:verdicts['callback-replays-original-armed-identity'] = False
            commands = [(command,copy.deepcopy(changed[key])) for (command,_),key in
                        zip(commands,('registration','rearmed','repeated_or_stale'))]
            with self.subTest(mutation=mutation),self.assertRaises(preparation.InvalidPacket):
                executor.witnesses.validate_reminder_identity(changed,commands,verdicts,'stale-generation-refusal')

        for status in (403,404,500,None,True):
            changed,commands,verdicts = copy.deepcopy(observed),copy.deepcopy(rows),dict(values)
            changed['repeated_or_stale']['callback_status'] = status
            verdicts['callback-actor-request-accepted'] = False
            commands[-1] = (commands[-1][0],copy.deepcopy(changed['repeated_or_stale']))
            with self.subTest(retained_nonpassing_status=status):
                executor.witnesses.validate_reminder_identity(changed,commands,verdicts,'stale-generation-refusal')

    def testReminderTimingImportBindsActualRegistrationAndLaunchConfiguration(self):
        worker,checks = self.reminderWorker(),executor.Measurements()
        moments = ['2026-10-18T00:00:00+00:00','2026-10-18T00:01:10+00:00',
                   '2026-10-18T00:01:10.100000+00:00','2026-10-18T00:01:50+00:00',
                   '2026-10-18T00:01:50.100000+00:00']
        with mock.patch.object(executor,'stamp',side_effect=moments), mock.patch.object(executor.time,'sleep'):
            observed,_,_ = worker.reminder_case('scheduled-restart-effect',checks)
        commands = [({'argv':['HTTP','POST','http://127.0.0.1/reminder/register/1'],
                     'started_utc':'2026-10-18T00:01:'+second+'.010000+00:00',
                     'finished_utc':'2026-10-18T00:01:'+second+'.090000+00:00'}, {}) for second in ('10','50')]
        configurations = [{'observed_utc':'2026-10-18T00:01:'+second+'+00:00','reminder_due':observed['due']}
                          for second in ('00','40')]
        values = {row['id']:row['passed'] for row in checks.checks}
        executor.witnesses.validate_reminder_windows(observed,commands,configurations,values,'scheduled-restart-effect')
        for mutation in ('missing','remaining','expired','due','phase','command','configuration'):
            changed,rows,configs = copy.deepcopy(observed),copy.deepcopy(commands),copy.deepcopy(configurations)
            if mutation == 'missing':del changed['deadline_windows']
            elif mutation == 'remaining':changed['deadline_windows'][0]['remaining_seconds'] += 1
            elif mutation == 'expired':changed['deadline_windows'][0]['observed_utc'] = '2026-10-18T00:03:00+00:00'
            elif mutation == 'due':changed['deadline_windows'][2]['due_utc'] = '2026-10-18T00:03:00+00:00'
            elif mutation == 'phase':changed['deadline_windows'].reverse()
            elif mutation == 'command':rows[0][0]['finished_utc'] = '2026-10-18T00:03:00+00:00'
            else:configs[1]['reminder_due'] = '2026-10-18T00:03:00+00:00'
            with self.subTest(mutation=mutation),self.assertRaises(preparation.InvalidPacket):
                executor.witnesses.validate_reminder_windows(changed,rows,configs,values,'scheduled-restart-effect')

    def testExecutionRefusesSubstitutedApprovedTupleBeforeOperations(self):
        inputs = {"candidate": {"version": "3.119.0", "tag": "v3.119.0",
            "tag_commit": "f463442cca19e4199982a23a08bae4a490767d4a",
            "builds": {"version": "4.30.1-20-g2cf0002", "commit": "2cf00028bbe563d80d4d12b5fb2054914f14fcb6"}},
            "rollback": None, "assertion_instrumentation": {"mechanism": executor.MECHANISM},
            "comparisons": [{"version": version} for version in qualification.COMPARISON_VERSIONS],
            "operational_profile": {"runtime": "dapr", "runtime_version": "1.18.2",
                "backend": "state.postgresql", "backend_image": executor.POSTGRES},
            "selected_additions": list(qualification.ADDITIONS)}
        # Isolate the executor's exact approved-selection guard from the input schema.
        # This tooling fixture cannot create a package or operational qualification.
        with mock.patch.object(qualification, "validate_inputs", return_value="owner-selected"):
            executor.validate_execution_inputs(inputs)
            for mutation in (lambda value: value["candidate"].update(tag_commit="0" * 40),
                             lambda value: value["candidate"]["builds"].update(version="4.30.0"),
                             lambda value: value["operational_profile"].update(runtime_version="1.18.4"),
                             lambda value: value["operational_profile"].update(backend_image="redis:latest"),
                             lambda value: value["operational_profile"].update(backend="state.redis"),
                             lambda value: value.update(rollback={"version": "3.110.0"}),
                             lambda value: value["selected_additions"].pop(),
                             lambda value: value["comparisons"].reverse()):
                changed = copy.deepcopy(inputs)
                mutation(changed)
                with self.assertRaisesRegex(preparation.InvalidPacket, "selections substituted"):
                    executor.validate_execution_inputs(changed)

    def testSuccessorExecutionRequiresExactTagSourceAndBuildsBeforeResources(self):
        candidate = {"version": "3.121.0", "tag": "v3.121.0", "tag_commit": "a" * 40,
                     "tag_builds_commit": "b" * 40, "builds": {"version": "4.31.0", "commit": "c" * 40}}
        inputs = {"candidate": candidate, "rollback": None,
                  "assertion_instrumentation": {"mechanism": executor.MECHANISM},
                  "comparisons": [{"version": version} for version in qualification.COMPARISON_VERSIONS],
                  "operational_profile": {"runtime": "dapr", "runtime_version": "1.18.2",
                                          "backend": "state.postgresql", "backend_image": executor.POSTGRES},
                  "selected_additions": list(qualification.ADDITIONS)}
        observations = [b"a" * 40, b"a" * 40, b"160000 commit " + b"b" * 40 + b"\treferences/Hexalith.Builds\n"]
        with mock.patch.object(qualification, "validate_inputs", return_value="owner-selected"), \
             mock.patch.object(preparation, "git", side_effect=observations) as git:
            executor.validate_execution_inputs(inputs)
            self.assertEqual(git.call_count, 3)
        for mutation in (lambda value: value["candidate"].update(version="3.120.0"),
                         lambda value: value["candidate"].update(tag_commit="d" * 40),
                         lambda value: value["candidate"].update(tag_builds_commit="d" * 40)):
            changed = copy.deepcopy(inputs)
            mutation(changed)
            with mock.patch.object(qualification, "validate_inputs", return_value="owner-selected"), \
                 mock.patch.object(preparation, "git", side_effect=observations):
                with self.assertRaises(preparation.InvalidPacket):
                    executor.validate_execution_inputs(changed)

    def testSuccessorObservationUsesSelectedTagAndBuildsBeforeConsumers(self):
        candidate = {"version": "3.121.0", "tag": "v3.121.0", "tag_commit": "a" * 40,
                     "tag_builds_commit": "b" * 40, "builds": {"version": "4.31.0", "commit": "c" * 40},
                     "feed": "https://fixture.invalid/v3/index.json",
                     "packages": [{"id": identifier, "archive_sha256": "d" * 64,
                                   "content_hash": base64.b64encode(b"fixture-hash" * 5 + b"xxxx").decode(),
                                   "repository_commit": "a" * 40}
                                  for identifier in executor.PACKAGE_IDS]}
        worker = executor.Executor.__new__(executor.Executor)
        worker.build_consumers = mock.Mock(side_effect=RuntimeError("consumer boundary reached"))
        observations = [b"a" * 40, b"a" * 40,
                        b"160000 commit " + b"b" * 40 + b"\treferences/Hexalith.Builds\n"]
        with mock.patch.object(preparation, "git", side_effect=observations) as git:
            with self.assertRaisesRegex(RuntimeError, "consumer boundary reached"):
                worker.observe_inputs({"candidate_proposed": candidate}, "selected-successor")
        self.assertEqual(worker.candidate, "3.121.0")
        self.assertEqual(worker.versions, ("3.110.0", "3.70.1", "3.121.0"))
        worker.build_consumers.assert_called_once_with("3.110.0")
        self.assertEqual(git.call_count, 3)

    def test31251EditedToolingHeadCannotInheritReleasedSourceBinding(self):
        candidate = {"version": "3.125.1", "tag": "v3.125.1", "tag_commit": "a" * 40,
                     "tag_builds_commit": "b" * 40, "builds": {"version": "4.31.0", "commit": "c" * 40},
                     "feed": "https://fixture.invalid/v3/index.json",
                     "packages": [{"id": identifier, "archive_sha256": "e" * 64,
                                   "content_hash": base64.b64encode(b"fixture-hash" * 5 + b"xxxx").decode(),
                                   "repository_commit": "a" * 40}
                                  for identifier in executor.PACKAGE_IDS]}
        inputs = {"candidate": candidate, "rollback": None,
                  "assertion_instrumentation": {"mechanism": executor.MECHANISM},
                  "comparisons": [{"version": version} for version in qualification.COMPARISON_VERSIONS],
                  "operational_profile": {"runtime": "dapr", "runtime_version": "1.18.2",
                                          "backend": "state.postgresql", "backend_image": executor.POSTGRES},
                  "selected_additions": list(qualification.ADDITIONS)}
        observations = [b"a" * 40, b"d" * 40,
                        b"160000 commit " + b"b" * 40 + b"\treferences/Hexalith.Builds\n"]
        with mock.patch.object(qualification, "validate_inputs", return_value="owner-selected"), \
             mock.patch.object(preparation, "source_binding", return_value={}), \
             mock.patch.object(qualification, "validate_candidate_source"), \
             mock.patch.object(preparation, "git", side_effect=observations):
            with self.assertRaisesRegex(preparation.InvalidPacket, "exact source"):
                executor.validate_execution_inputs(inputs)
        worker = executor.Executor.__new__(executor.Executor)
        worker.build_consumers = mock.Mock()
        with mock.patch.object(preparation, "git", side_effect=observations):
            with self.assertRaisesRegex(preparation.InvalidPacket, "source or tag substituted"):
                worker.observe_inputs({"candidate_proposed": candidate}, "selected-successor")
        worker.build_consumers.assert_not_called()

    def testSuccessorExecutionPlanRerunsCandidateAndFencesOldRoutes(self):
        selection = {"candidate": {"version": "3.121.0"},
                     "comparisons": [{"version": version} for version in qualification.COMPARISON_VERSIONS]}
        rules = executor.provisional_successor_execution_policy(selection)
        self.assertEqual(rules[("mixed-api", "3.121.0-stale-fence")], "supported")
        self.assertEqual(rules[("logical-event-evolution", "logical-alias-replay")], "supported")
        self.assertEqual(rules[("metadata-write", "3.121.0-to-3.70.1")], "refused")
        self.assertEqual(len(rules), sum(map(len, qualification.executed_case_inventory(selection).values())))

    def testStatusRetryabilityKeepsUnknownFalseAndTrueDistinct(self):
        self.assertEqual(executor.STATUS_RETRYABILITY, (None, False, True))
        self.assertEqual([type(value) for value in executor.STATUS_RETRYABILITY], [type(None), bool, bool])

    def testMissingRequiredDirectionIsRefusedEvenWithConsistentCounts(self):
        from test_p1r_published_qualification import lane_case, lane_receipt, synthetic_inputs, synthetic_evidence
        with tempfile.TemporaryDirectory() as scratch:
            _, candidate = synthetic_evidence(Path(scratch) / "candidate", version="3.119.0")
            inputs = synthetic_inputs(candidate, fixture=None)
            inputs["assertion_instrumentation"]["mechanism"] = executor.MECHANISM
            cases = [lane_case(identity) for identity in qualification.executed_case_inventory(inputs)["full-replay"][:-1]]
            receipt = lane_receipt("full-replay", cases=cases, scope="published-package", compatibility="compatible")
            receipt["inputs_sha256"], receipt["instrumentation"] = "a" * 64, executor.MECHANISM
            command = {"argv": ["synthetic-fixture-command"], "cwd": "/synthetic", "exit_code": 0,
                       "started_utc": receipt["started_utc"], "finished_utc": receipt["finished_utc"],
                       "output": "synthetic fixture", "output_sha256": executor.digest(b"synthetic fixture")}
            receipt["execution_evidence"] = {"mechanism": executor.MECHANISM, "executor_source": {"fixture": qualification.SYNTHETIC},
                "cases": [{"id": case["id"], "commands": [command]} for case in cases]}
            receipt["execution_evidence"]["executor_source_sha256"] = executor.digest(
                json.dumps(receipt["execution_evidence"]["executor_source"], indent=2, sort_keys=True).encode() + b"\n")
            context = {"inputs": {"scope": "owner-selected", "value": inputs, "sha256": "a" * 64},
                       "source_binding": {"fixture": qualification.SYNTHETIC}, "verified": {}}
            # The direction guard precedes the archive trust boundary; no synthetic archive can qualify.
            inputs["comparisons"] = [{"version": version} for version in qualification.COMPARISON_VERSIONS]
            with self.assertRaisesRegex(preparation.InvalidPacket, "required direction"):
                qualification.bind_lane(receipt, {"scope": "published-package"}, context)

    def testExecutedPostgresqlConfigurationMustRetainRedactedTrackedTemplate(self):
        from test_p1r_published_qualification import lane_case, lane_receipt, synthetic_inputs, synthetic_evidence
        with tempfile.TemporaryDirectory() as scratch:
            _, candidate = synthetic_evidence(Path(scratch) / "candidate", version="3.119.0")
            inputs = synthetic_inputs(candidate, fixture=None)
            inputs["assertion_instrumentation"]["mechanism"] = executor.MECHANISM
            inputs["comparisons"] = [{"version": version} for version in qualification.COMPARISON_VERSIONS]
            inputs["operational_profile"]["backend"] = "state.postgresql"
            cases = [lane_case(identity) for identity in qualification.executed_case_inventory(inputs)["full-replay"]]
            receipt = lane_receipt("full-replay", cases=cases, scope="published-package", compatibility="compatible")
            receipt["inputs_sha256"], receipt["instrumentation"] = "a" * 64, executor.MECHANISM
            fixture_value = uuid.uuid4().hex
            private_rendering = f"host=127.0.0.1 password={fixture_value}"
            rendered = (executor.ROOT / "deploy/dapr/statestore-postgresql.yaml").read_text().replace(
                "{env:POSTGRES_CONNECTION_STRING}", private_rendering)
            configuration = {"backend_image": inputs["operational_profile"]["backend_image"],
                             "runtime_version": inputs["operational_profile"]["runtime_version"],
                             "files": [{"name": "state.yaml", "content": rendered, "sha256": executor.digest(rendered.encode()),
                                        "rendered_sha256": "a" * 64, "credential_redacted": True}]}
            receipt["execution_evidence"] = {"mechanism": executor.MECHANISM, "executor_source": {"fixture": qualification.SYNTHETIC},
                "cases": [{"id": case["id"], "configurations": [configuration]} for case in cases]}
            receipt["execution_evidence"]["executor_source_sha256"] = executor.digest(
                json.dumps(receipt["execution_evidence"]["executor_source"], indent=2, sort_keys=True).encode() + b"\n")
            context = {"inputs": {"scope": "owner-selected", "value": inputs, "sha256": "a" * 64},
                       "source_binding": {"fixture": qualification.SYNTHETIC}, "verified": {}}
            with self.assertRaisesRegex(preparation.InvalidPacket, "PostgreSQL component template"):
                qualification.bind_lane(receipt, {"scope": "published-package"}, context)

    def testComparisonArchivesHaveIndependentPacketLanes(self):
        from test_p1r_published_qualification import synthetic_inputs, synthetic_evidence, synthetic_verifier, SYNTHETIC_BINDING
        with tempfile.TemporaryDirectory() as scratch:
            root = Path(scratch)
            evidence, candidate = synthetic_evidence(root / "candidate")
            selections, directories = [], []
            for version in qualification.COMPARISON_VERSIONS:
                directory, selection = synthetic_evidence(root / version, "comparison-" + version, version)
                selections.append(selection)
                directories.append(directory)
            inputs = synthetic_inputs(candidate)
            inputs["comparisons"] = selections
            inputs_path = root / "inputs.json"
            executor.write_json(inputs_path, inputs)
            with mock.patch.object(preparation, "source_binding", return_value=copy.deepcopy(SYNTHETIC_BINDING)):
                packet = qualification.create_packet(root / "packet", inputs=inputs_path, candidate_evidence=evidence,
                    comparison_evidence=directories, verifier=synthetic_verifier)
                self.assertEqual(packet["errors"], [])
                result = qualification.validate_packet(root / "packet")
            self.assertTrue(result["valid"])
            self.assertFalse(result["technically_qualified"])
            self.assertFalse(result["capable_rollback_qualified"])
            self.assertEqual([row["id"] for row in packet["lanes"]["packages"]][-2:],
                ["comparison-3.70.1-packages", "comparison-3.110.0-packages"])

    def testFailuresAreIncludedInDerivedCounters(self):
        measured = executor.Measurements()
        measured.check("effect", True)
        measured.check("preservation", False)
        self.assertEqual(measured.counter, {"attempted": 2, "passed": 1, "failed": 1})
        self.assertEqual(measured.checks[-1], {"id": "preservation", "passed": False})

    def testDuplicateAndNonBooleanChecksRefused(self):
        measured = executor.Measurements()
        measured.check("effect", True)
        for identity, value in (("effect", False), ("numeric", 1), ("missing", None)):
            with self.assertRaises(preparation.InvalidPacket):
                measured.check(identity, value)

    def testInventedProbeCountersAndOmittedFailuresRefused(self):
        value = {"instrumentation": executor.MECHANISM, "measurement": {
            "checks": [{"id": "effect", "passed": False}], "assertions": {"attempted": 1, "passed": 1, "failed": 0}}}
        with self.assertRaises(preparation.InvalidPacket):
            executor.Measurements().import_probe(value, "probe:")
        value["measurement"]["checks"] = []
        with self.assertRaises(preparation.InvalidPacket):
            executor.Measurements().import_probe(value, "probe:")

    def testFiniteInventoryIncludesActualHistoricalAndCandidateDirections(self):
        selection = {"candidate": {"version": "3.119.0"}}
        inventory = qualification.executed_case_inventory(selection)
        for lane in ("metadata-write", "full-replay", "snapshot-tail", "retained-covered"):
            self.assertIn("3.110.0-to-3.70.1", inventory[lane])
            self.assertIn("3.70.1-to-3.110.0", inventory[lane])
            self.assertIn("3.119.0-to-3.70.1", inventory[lane])
            self.assertIn("3.70.1-to-3.119.0", inventory[lane])
        self.assertEqual(set(inventory), set(qualification.SCENARIOS + qualification.ADDITIONS))
        self.assertIn("3.119.0-fenced-effect", inventory["mixed-api"])
        self.assertIn("3.119.0-trusted-effect", inventory["mixed-api"])

    def testComparisonPolicyDoesNotGrantRollback(self):
        from test_p1r_published_qualification import synthetic_inputs, synthetic_evidence
        with tempfile.TemporaryDirectory() as scratch:
            root = Path(scratch)
            _, candidate = synthetic_evidence(root / "candidate")
            comparisons = []
            for version in qualification.COMPARISON_VERSIONS:
                _, selection = synthetic_evidence(root / version, role="comparison-" + version, version=version)
                comparisons.append(selection)
            inputs = synthetic_inputs(candidate, rollback=None)
            inputs["comparisons"] = comparisons
            qualification.validate_inputs(inputs)
            lanes = qualification.initial_lanes(inputs)
            self.assertEqual(len(lanes["packages"]), 4)
            result = qualification.evaluate(lanes, {"value": inputs, "scope": "tooling-synthetic"}, None)
            self.assertFalse(result["capable_rollback_qualified"])
            self.assertFalse(result["p1r_usable"])
            for mutation in (lambda value: value["comparisons"].pop(),
                             lambda value: value["comparisons"].reverse(),
                             lambda value: value["comparisons"][0].update(version="3.114.0")):
                changed = copy.deepcopy(inputs)
                mutation(changed)
                with self.assertRaises(preparation.InvalidPacket):
                    qualification.validate_inputs(changed)


class CleanupTests(unittest.TestCase):
    def worker(self, scratch):
        worker = executor.Executor.__new__(executor.Executor)
        worker.output = Path(scratch) / "receipts-output"
        worker.output.mkdir()
        worker.scratch = Path(scratch) / "owned-scratch"
        worker.scratch.mkdir()
        worker.invocation = "1" * 32
        worker.fixture = {"id": "synthetic-control", "synthetic": True}
        worker.inputs = None
        worker.inputs_sha256 = None
        worker.commands, worker.receipts, worker.errors = [], [], []
        worker.processes, worker.active, worker.containers = [], [], {}
        worker.pending_containers = {}
        worker.configurations = []
        preparation.subreaper()
        worker.shared_before = {}
        worker.result = {"errors": [], "receipts": []}
        return worker

    def testCleanupContinuesAfterOneFailureAndRepeatsEveryTarget(self):
        with tempfile.TemporaryDirectory() as scratch:
            worker = self.worker(scratch)
            worker.containers = {"bad": {}, "good": {}}
            visited = []
            def run(argv, **kwargs):
                visited.append(tuple(argv))
                if argv[:2] == ["docker", "inspect"] and len(argv) == 5:
                    identity = argv[-1]
                    if identity == "bad":
                        raise RuntimeError("synthetic injected cleanup failure")
                    worker.commands.append({"exit_code": 0})
                    return json.dumps({"Id": identity, "Config": {"Labels": {"hexalith.p1r.invocation": worker.invocation}}}).encode()
                return b""
            with mock.patch.object(worker, "run", side_effect=run):
                receipt = worker.cleanup()
            self.assertEqual(sum(row == ("docker", "rm", "-f", "good") for row in visited), 2)
            self.assertEqual(receipt["attempts"][-1]["remaining"], ["bad"])
            self.assertEqual(receipt["execution"], "failed")
            self.assertEqual(receipt["scope"], "tooling-synthetic")
            self.assertEqual(receipt["compatibility"], "unverified")
            self.assertFalse(worker.scratch.exists())

    def testInterruptedActualChildIsRemovedAndRepeatedCleanupIsIdempotent(self):
        with tempfile.TemporaryDirectory() as scratch:
            worker = self.worker(scratch)
            child = worker.launch([sys.executable, "-I", "-c", "import time; time.sleep(60)"], dict())
            identity = child["ownership"].root
            preparation.signal_owned(identity, __import__("signal").SIGINT)
            def discovery(argv, **kwargs):
                return b""
            with mock.patch.object(worker, "run", side_effect=discovery):
                receipt = worker.cleanup()
            self.assertFalse(preparation.same_process(identity, preparation.process_state(identity["pid"])))
            self.assertEqual(receipt["attempts"][-1]["remaining"], [])
            self.assertEqual(len(receipt["attempts"]), 2)
            self.assertEqual(receipt["scope"], "tooling-synthetic")

    def testInterruptionRetainsPartialCaseAndPropagatesAfterOwnedCleanup(self):
        with tempfile.TemporaryDirectory() as scratch:
            worker = self.worker(scratch)
            calls = []
            def interrupt(checks):
                checks.check("started", True)
                raise KeyboardInterrupt()
            with mock.patch.object(worker, "stop_nodes", side_effect=lambda: calls.append("cleanup")):
                with self.assertRaises(KeyboardInterrupt):
                    worker.case("full-replay", "synthetic-interruption", interrupt, True)
            self.assertEqual(calls, ["cleanup"])
            retained = list((worker.output / "interrupted").glob("*.json"))
            self.assertEqual(len(retained), 1)
            value = json.loads(retained[0].read_bytes())
            self.assertEqual(value["case"]["assertions"], {"attempted": 2, "passed": 1, "failed": 1})


class CorrectionTests(unittest.TestCase):
    worker = CleanupTests.worker
    def testCleanupDiagnosticsRetainErrnoAndCleanRetryCannotEraseRefusal(self):
        ownership=preparation.OwnedProcesses()
        with mock.patch.object(ownership,'discover',side_effect=OSError(5,'bounded synthetic read failure')):
            failed=ownership.cleanup()
        self.assertEqual(failed['errors'],['OSError'])
        self.assertEqual(ownership.cleanup_diagnostics,[{'operation':'discover-owned','type':'OSError','errno':5}])
        self.assertEqual(failed['remaining'],[])
        clean=ownership.cleanup()
        self.assertEqual(clean['errors'],[])
        with tempfile.TemporaryDirectory() as scratch:
            worker=self.worker(scratch)
            actual_cleanup=preparation.OwnedProcesses.cleanup
            attempts=[]
            def cleanup(owner):
                if not attempts:
                    with mock.patch.object(owner,'discover',side_effect=OSError(5,'bounded synthetic read failure')):
                        result=actual_cleanup(owner)
                else:
                    result=actual_cleanup(owner)
                attempts.append(result)
                return result
            with mock.patch.object(preparation.OwnedProcesses,'cleanup',cleanup):
                with self.assertRaisesRegex(preparation.InvalidPacket,'owned command cleanup failed'):
                    worker.run([sys.executable,'-I','-c','pass'])
            self.assertEqual(worker.commands[-1]['exit_code'],0)
            self.assertEqual(worker.commands[-1]['cleanup'],[failed,clean])
            self.assertEqual(worker.commands[-1]['cleanup_error_diagnostics'],ownership.cleanup_diagnostics)

    def metadata_fixture(self, scratch):
        worker = self.worker(scratch)
        def run(argv, **kwargs):
            observation = {"sequence":12,"floor":1,"etag":"fixture-etag","last_modified":"2026-01-01T00:00:00+00:00"}
            value = {"instrumentation":executor.MECHANISM, "measurement": {
                "checks":[{"id":identity,"passed":True} for identity in ("sequence-twelve","etag-preserved","last-modified-preserved")],
                "assertions":{"attempted":3,"passed":3,"failed":0}},"observation":observation}
            Path(argv[-2]).write_text('{}')
            data=executor.canonical(value)
            worker.record(argv,worker.scratch,preparation.stamp(),0,data)
            return data
        worker.evidence = {"comparison-3.70.1":worker.scratch}
        with mock.patch.object(worker,"run",side_effect=run):
            row,evidence = worker.case("metadata-read","3.70.1-web-floor-5",
                lambda checks:worker.metadata_case("metadata-read","3.70.1-web-floor-5",checks),False)
        binding={"repository":str(preparation.ROOT),"main":{"files":[
            {"path":"tools/p1r_published_executor.py","sha256":executor.digest((preparation.ROOT/'tools/p1r_published_executor.py').read_bytes())}]}}
        return worker,row,evidence,binding

    def validate(self, worker, row, evidence, binding):
        executor.witnesses.validate_case(row,evidence,binding,"metadata-read",list(worker.witness_sources.values()))

    def testFailedMetadataCheckCannotBeFlippedWithRecomputedCountersAndHashes(self):
        with tempfile.TemporaryDirectory() as scratch:
            worker,row,evidence,binding=self.metadata_fixture(scratch)
            self.validate(worker,row,evidence,binding)
            self.assertFalse(row["checks"][-1]["passed"])
            changed=copy.deepcopy(row)
            changed["checks"][-1]["passed"]=True
            changed["assertions"]=preparation.checks_counter(changed["checks"])
            with self.assertRaisesRegex(preparation.InvalidPacket,"predicate operands"):
                self.validate(worker,changed,evidence,binding)
            forged=copy.deepcopy(evidence)
            forged["check_witnesses"][-1]["predicate"]["operands"]=[5,5]
            with self.assertRaisesRegex(preparation.InvalidPacket,"retained floor"):
                self.validate(worker,changed,forged,binding)

    def testProbeFailureCannotBeOmittedEvenWhenOuterTotalsAgree(self):
        with tempfile.TemporaryDirectory() as scratch:
            worker,row,evidence,binding=self.metadata_fixture(scratch)
            row["checks"].pop(0)
            evidence["check_witnesses"].pop(0)
            row["assertions"]=preparation.checks_counter(row["checks"])
            with self.assertRaisesRegex(preparation.InvalidPacket,"silently drops"):
                self.validate(worker,row,evidence,binding)

    def testInventedInventoryDigestIsIndependentlyRefused(self):
        with tempfile.TemporaryDirectory() as scratch:
            worker,row,evidence,binding=self.metadata_fixture(scratch)
            row["inventory"]={"before_sha256":"a"*64,"after_sha256":"a"*64}
            with self.assertRaisesRegex(preparation.InvalidPacket,"inventory hashes"):
                self.validate(worker,row,evidence,binding)

    def testRuntimeOperationCannotDropRenderedConfigurations(self):
        with tempfile.TemporaryDirectory() as scratch:
            worker,row,evidence,binding=self.metadata_fixture(scratch)
            evidence["commands"][0]["argv"].append("actor")
            with self.assertRaisesRegex(preparation.InvalidPacket,"sidecar command configuration"):
                self.validate(worker,row,evidence,binding)

    def testExecutorSourceSubstitutionRefusedAfterOuterHashRecomputation(self):
        from test_p1r_published_qualification import lane_case,lane_receipt
        selected={"candidate":{"version":"3.119.0"},"comparisons":[{"version":v} for v in qualification.COMPARISON_VERSIONS],
            "assertion_instrumentation":{"mechanism":executor.MECHANISM},"selected_additions":[]}
        receipt=lane_receipt("provenance",scope="published-package",compatibility="compatible",
            cases=[lane_case(case) for case in qualification.executed_case_inventory(selected)["provenance"]])
        receipt["inputs_sha256"]= "a"*64
        receipt["instrumentation"]=executor.MECHANISM
        evidence={"mechanism":executor.MECHANISM,"executor_source":{"substituted":True},
            "cases":[{"id":case["id"],"commands":[]} for case in receipt["cases"]]}
        evidence["executor_source_sha256"]=executor.digest(json.dumps(evidence["executor_source"],indent=2,sort_keys=True).encode()+b"\n")
        receipt["execution_evidence"],receipt["output_sha256"]=evidence,executor.digest(executor.canonical(evidence))
        with self.assertRaisesRegex(preparation.InvalidPacket,"independently bound source closure"):
            qualification.bind_lane(receipt,{"scope":"published-package"},{"inputs":{"value":selected,"scope":"owner-selected","sha256":"a"*64},
                "source_binding":{"original":True},"verified":{}})

    def testTimeoutOwnsAndRemovesDetachedSynchronousDescendant(self):
        with tempfile.TemporaryDirectory() as scratch:
            worker=self.worker(scratch)
            sentinel=subprocess.Popen([sys.executable,'-I','-c','import time;time.sleep(20)'],start_new_session=True)
            shared=preparation.process_state(sentinel.pid)
            try:
                script="import subprocess,sys,time;p=subprocess.Popen([sys.executable,'-I','-c','import time;time.sleep(20)'],start_new_session=True);print(p.pid,flush=True);time.sleep(20)"
                worker.run([sys.executable,'-I','-c',script],timeout=.2,check=False)
                command=worker.commands[-1]
                self.assertEqual(command['exit_code'],124)
                self.assertGreaterEqual(len(command['owned_processes']),2)
                self.assertTrue(all(not attempt['remaining'] for attempt in command['cleanup']))
                self.assertTrue(preparation.same_process(preparation.identity(shared),preparation.process_state(sentinel.pid)))
            finally:
                sentinel.terminate();sentinel.wait()

    def testCancellationRetainsCommandTreeCleanup(self):
        with tempfile.TemporaryDirectory() as scratch:
            worker=self.worker(scratch)
            timer=threading.Timer(.2,lambda:os.kill(os.getpid(),signal.SIGINT))
            timer.start()
            try:
                with self.assertRaises(KeyboardInterrupt):
                    worker.run([sys.executable,'-I','-c','import time;time.sleep(20)'])
            finally:
                timer.cancel();timer.join()
            self.assertEqual(worker.commands[-1]['exit_code'],130)
            self.assertEqual(worker.commands[-1]['cleanup'][-1]['remaining'],[])

    def testInitializationFailureDestroysScratchAndRetainsRefusal(self):
        with tempfile.TemporaryDirectory() as scratch:
            owned=Path(scratch)/'owned';owned.mkdir()
            with mock.patch.object(executor.tempfile,'mkdtemp',return_value=str(owned)), \
                 mock.patch.object(preparation,'source_binding',side_effect=preparation.InvalidPacket('source refusal')), \
                 mock.patch.object(executor.Executor,'run',return_value=b''):
                with self.assertRaisesRegex(preparation.InvalidPacket,'source refusal'):
                    executor.Executor(Path(scratch)/'output')
            self.assertFalse(owned.exists())
            result=json.loads((Path(scratch)/'output/execution.json').read_bytes())
            self.assertIn('initialization: source refusal',result['errors'])
            self.assertTrue(result['consumer_cleanup'])

    def testInterruptedContainerCreationRecoversExactNameAndLabelWithoutCidfile(self):
        with tempfile.TemporaryDirectory() as scratch:
            worker=self.worker(scratch)
            observed={}
            def run(argv,**kwargs):
                if argv[:2]==['docker','create']:
                    observed['name']=argv[argv.index('--name')+1]
                    raise KeyboardInterrupt()
                worker.commands.append({'exit_code':0})
                return json.dumps({'Id':'owned-id','Name':'/'+observed['name'],
                    'Config':{'Labels':{'hexalith.p1r.invocation':worker.invocation}}}).encode()
            with mock.patch.object(worker,'run',side_effect=run):
                with self.assertRaises(KeyboardInterrupt):
                    worker.container('interrupted',executor.REDIS)
            self.assertIn('owned-id',worker.containers)
            self.assertEqual(worker.pending_containers,{})

    def testLateReminderThirteenRemainsNonpassingAndUsesRemainingTimeout(self):
        with tempfile.TemporaryDirectory() as scratch:
            worker=self.worker(scratch);worker.sidecar_port=12345
            timeouts=[]
            def probe(version,arguments,checks,prefix,timeout):
                timeouts.append(timeout);worker.commands.append({'id':1});return {'sequence':13}
            with mock.patch.object(worker,'probe',side_effect=probe),mock.patch.object(executor.time,'monotonic',side_effect=[0,1,2,31,32]):
                measured=executor.Measurements();observed=worker.natural_reminder(measured)
            self.assertEqual(timeouts,[28])
            self.assertFalse(observed['completed'])
            self.assertFalse(measured.checks[-1]['passed'])

    def testRetainedWitnessSourceSurvivesUnavailableOriginalPath(self):
        with tempfile.TemporaryDirectory() as scratch:
            worker,row,evidence,binding=self.metadata_fixture(scratch)
            with mock.patch.object(Path,'read_bytes',side_effect=OSError('original source unavailable')):
                self.validate(worker,row,evidence,binding)

    def testMissingStableMetadataFailureCannotBeDroppedTogetherWithWitness(self):
        with tempfile.TemporaryDirectory() as scratch:
            worker,row,evidence,binding=self.metadata_fixture(scratch)
            row['checks'].pop();evidence['check_witnesses'].pop()
            row['assertions']=preparation.checks_counter(row['checks'])
            with self.assertRaisesRegex(preparation.InvalidPacket,'retained floor'):
                self.validate(worker,row,evidence,binding)

    def testSharedSourceCaseMeasuresActualPackageDelta(self):
        with tempfile.TemporaryDirectory() as scratch:
            worker=self.worker(scratch);worker.inputs={'candidate':{'version':'3.119.0'}}
            def metadata(lane,identity,measurements):
                floor=5 if identity.startswith('source') else 1
                measurements.check('floor-one',floor == 1)
                return {'metadata':{'sequence':12,'floor':floor}},'effect','compatible'
            with mock.patch.object(worker,'metadata_case',side_effect=metadata):
                measured=executor.Measurements();observed,outcome,disposition=worker.checkout_case('legacy-metadata',measured)
            self.assertEqual(observed['comparison']['selected_case'],'3.119.0-pascal-floor-None')
            self.assertEqual(observed['comparison']['source_case'],'source-pascal-floor-None')
            self.assertFalse(measured.checks[-1]['passed'])
            self.assertEqual(disposition,'incompatible')
            self.assertTrue(observed['comparison']['delta'])

    def testDroppingNonNullQueryIdentityProofFailsWirePreservation(self):
        with tempfile.TemporaryDirectory() as scratch:
            worker=self.worker(scratch)
            def probe(version,arguments,measurements,prefix):
                original=json.loads(Path(arguments[3]).read_bytes())
                if prefix == 'reader:':
                    original.pop('identityAdmissionProof',None)
                Path(arguments[4]).write_bytes(executor.canonical(original))
                return {'handling':'executed','fields':original}
            with mock.patch.object(worker,'probe',side_effect=probe):
                measured=executor.Measurements();_,_,disposition=worker.wire_case('query-wire','3.119.0-3.110.0-json-dual',measured)
            self.assertIn({'id':'preserved:identityAdmissionProof','passed':False},measured.checks)
            self.assertEqual(disposition,'incompatible')

    def testUnexpectedProofFailureRetainsAfterInventoryAndRemainsError(self):
        with tempfile.TemporaryDirectory() as scratch:
            worker=self.worker(scratch);worker.host_port=12345;worker.redis='owned'
            observed={'accepted':False,'unexpected':True,'unauthorized_refused':False,'denial':None,'sequence':12,
                'diagnostic':[{'denial_message':'Trusted effect denial audit is unavailable.'}]}
            with mock.patch.object(worker,'seed'),mock.patch.object(worker,'inventory',return_value=[]), \
                 mock.patch.object(worker,'start_nodes'),mock.patch.object(worker,'stop_nodes'), \
                 mock.patch.object(worker,'probe',return_value={'actor_methods':['ProcessTrustedEffectAsync']}), \
                 mock.patch.object(worker,'http',return_value=observed), \
                 mock.patch.object(worker,'postgres_audits',return_value=[]):
                measured=executor.Measurements();result,outcome,_=worker.mixed_case('3.119.0-unauthorized-effect',measured)
            self.assertEqual(outcome,'error')
            self.assertEqual(result['after'],[])
            self.assertIn({'id':'execution-completed','passed':False},measured.checks)
            self.assertIn({'id':'refusal-domain-preserved','passed':True},measured.checks)

    def testSupportedMetadataAppendRestartsActualSelectedWriterAndReplaysThirteen(self):
        with tempfile.TemporaryDirectory() as scratch:
            worker=self.worker(scratch)
            def rows(head):
                values=[{'key':'tenant-a:metadata','tenant':'tenant-a','kind':'metadata','sha256':'a'*64,'sequence':head,'floor':5},
                        {'key':'tenant-a:snapshot','tenant':'tenant-a','kind':'snapshot','sha256':'b'*64,'sequence':9,'floor':None}]
                values += [{'key':'tenant-a:events:'+str(index),'tenant':'tenant-a','kind':'event','sha256':'c'*64,'sequence':index,'floor':None} for index in range(5,head+1)]
                return sorted(values,key=lambda value:value['key'])
            def actor(version,tenant,expected,kind,measurements,prefix):
                return {'accepted':True,'event_count':1 if kind=='IncrementCounter' else 0,'sequence':expected}
            starts=[]
            with mock.patch.object(worker,'seed'),mock.patch.object(worker,'mutate'), \
                 mock.patch.object(worker,'inventory',side_effect=[rows(13),rows(13)]), \
                 mock.patch.object(worker,'start_nodes',side_effect=lambda version,**kwargs:starts.append(version)), \
                 mock.patch.object(worker,'stop_nodes'),mock.patch.object(worker,'actor',side_effect=actor):
                measured=executor.Measurements();observation,_,_=worker.live_case('metadata-write','3.110.0-to-3.119.0',measured,
                    prepared={'before':rows(12),'seed_snapshot':None})
            self.assertEqual(starts,['3.119.0','3.119.0'])
            self.assertEqual(observation['replay']['sequence'],13)
            self.assertIn({'id':'supported-append-restart-rehydrates-thirteen','passed':True},measured.checks)


    def wire_fixture(self,scratch,lane):
        worker=self.worker(scratch)
        worker.evidence={'candidate':worker.scratch,'comparison-3.110.0':worker.scratch}
        def run(argv,**kwargs):
            fields=json.loads(Path(argv[-2]).read_bytes())
            if lane=='projection-wire':
                fields['globalPosition']=None
            else:
                fields.pop('identityAdmissionProof',None)
            Path(argv[-1]).write_bytes(executor.canonical(fields))
            value={'instrumentation':executor.MECHANISM,'measurement':{
                'checks':[{'id':'contract-type-present','passed':True},{'id':'wire-output-nonempty','passed':True}],
                'assertions':{'attempted':2,'passed':2,'failed':0}},'observation':{'handling':'executed','fields':fields}}
            data=executor.canonical(value);worker.record(argv,worker.scratch,preparation.stamp(),0,data);return data
        identifier='3.119.0-3.110.0-json-'+('dual' if lane=='query-wire' else 'positive')
        with mock.patch.object(worker,'run',side_effect=run):
            row,evidence=worker.case(lane,identifier,lambda checks:worker.wire_case(lane,identifier,checks),False)
        binding={'repository':str(preparation.ROOT),'main':{'files':[{'path':'tools/p1r_published_executor.py',
                 'sha256':executor.digest((preparation.ROOT/'tools/p1r_published_executor.py').read_bytes())}]}}
        return worker,row,evidence,binding

    def testProjectionAndQueryFailuresCannotBeFlippedWithContradictoryProbeFields(self):
        for lane,field in (('projection-wire','globalPosition'),('query-wire','identityAdmissionProof')):
            with self.subTest(lane=lane),tempfile.TemporaryDirectory() as scratch:
                worker,row,evidence,binding=self.wire_fixture(scratch,lane)
                retained=list(worker.witness_sources.values())
                executor.witnesses.validate_case(row,evidence,binding,lane,retained)
                check=next(item for item in row['checks'] if item['id']=='preserved:'+field)
                witness=next(item for item in evidence['check_witnesses'] if item['id']==check['id'])
                self.assertFalse(check['passed'])
                check['passed']=True
                witness['predicate']['operands'][0]=witness['predicate']['operands'][1]
                row['assertions']=preparation.checks_counter(row['checks'])
                with self.assertRaisesRegex(preparation.InvalidPacket,'reader field'):
                    executor.witnesses.validate_case(row,evidence,binding,lane,retained)

    def testLogicalRegistrationCannotContradictRetainedDomainReadiness(self):
        with tempfile.TemporaryDirectory() as scratch:
            worker=self.worker(scratch)
            def action(checks):
                registration={'manifest_registered':False,'bounded_v1_serializer_registered':True}
                worker.record(['HTTP','GET','http://127.0.0.1:12345/ready'],worker.scratch,preparation.stamp(),124,b'URLError')
                worker.record(['HTTP','GET','http://127.0.0.1:12345/ready'],worker.scratch,preparation.stamp(),0,
                              executor.canonical({'evolution_registration':registration}))
                checks.check('registered-logical-alias-evolution-executed',registration.get('manifest_registered') is True)
                return {'domain_registration_observation':registration,'registered_logical_alias_evolution':{'executed':False}},'effect','incompatible'
            row,evidence=worker.case('logical-event-evolution','legacy-clr-readback',action,False)
            binding={'repository':str(preparation.ROOT),'main':{'files':[{'path':'tools/tests/test_p1r_published_executor.py',
                     'sha256':executor.digest(Path(__file__).read_bytes())}]}}
            retained=list(worker.witness_sources.values())
            with self.assertRaisesRegex(preparation.InvalidPacket,'independent test-only pin'):
                executor.witnesses.validate_case(row,evidence,binding,'logical-event-evolution',retained)
            self.assertEqual(evidence['commands'][0]['output'],'URLError')
            for output,status in (('URLError',0),('[]',0),(evidence['commands'][1]['output'],124)):
                with self.subTest(output=output,status=status):
                    failed=json.loads(json.dumps(evidence))
                    failed['commands'][1]['output']=output
                    failed['commands'][1]['exit_code']=status
                    with self.assertRaisesRegex(preparation.InvalidPacket,'Domain readiness'):
                        executor.witnesses.validate_case(row,failed,binding,'logical-event-evolution',retained)

    def testCompatibleLogicalWitnessBindsExactActorUrlAndInventory(self):
        with tempfile.TemporaryDirectory() as scratch:
            worker=self.worker(scratch)
            alias='P1R.Legacy.CounterIncremented'
            registration={'manifest_registered':True,'fixture_manifest_fingerprint':
                '5a5748913258b5832b333fe507bc689f1ac9e53a9201b4bb5cb983b535da933b',
                'fixture_manifest_test_only':True,'gateway_authority':False}
            actor={'accepted':True,'event_count':0,'sequence':12,'error':None}
            events=[]
            for sequence in range(1,13):
                value={'sequenceNumber':sequence,'eventTypeName':alias,'payload':'e30='}
                key=f'eventstore||AggregateActor||tenant-a:counter:fixture||tenant-a:counter:fixture:events:{sequence}'
                events.append({'key':key,'tenant':'tenant-a','kind':'event','sha256':executor.digest(executor.canonical(value)),
                               'sequence':sequence,'floor':None})
            for sequence in range(1,4):
                value={'sequenceNumber':sequence,'eventTypeName':alias,'payload':'e30='}
                key=f'eventstore||AggregateActor||tenant-b:counter:fixture||tenant-b:counter:fixture:events:{sequence}'
                events.append({'key':key,'tenant':'tenant-b','kind':'event','sha256':executor.digest(executor.canonical(value)),
                               'sequence':sequence,'floor':None})
            events.sort(key=lambda item:item['key'])
            def record(argv,value):
                worker.record(argv,worker.scratch,preparation.stamp(),0,executor.canonical(value))
            def action(checks):
                config={'id':'synthetic-runtime','files':[{'path':'/owned/config.yaml'},
                    {'path':'/owned/resources/state.yaml'},{'path':'/owned/resources/pubsub.yaml'}]}
                worker.configurations.append(config)
                record(['daprd','--config','/owned/config.yaml','--resources-path','/owned/resources'],{})
                record(['synthetic','inventory'],events)
                record(['HTTP','GET','http://127.0.0.1:12345/ready'],{'evolution_registration':registration})
                record(['dotnet','Probe.dll','actor','http://127.0.0.1:12345','tenant-a','fixture','12','AssertCounter'],
                    {'instrumentation':executor.MECHANISM,
                    'measurement':{'checks':[],'assertions':{'attempted':0,'passed':0,'failed':0}},'observation':actor})
                readback=[]
                for sequence in range(1,13):
                    value={'sequenceNumber':sequence,'eventTypeName':alias,'payload':'e30='}
                    url=(f'http://127.0.0.1:12345/v1.0/actors/AggregateActor/'
                         f'tenant-a%3Acounter%3Afixture/state/tenant-a%3Acounter%3Afixture%3Aevents%3A{sequence}')
                    record(['HTTP','GET',url],value)
                    readback.append({'sequence':sequence,'url':url,'sha256':executor.digest(executor.canonical(value)),
                                     'event_type_name':alias,'payload_sha256':executor.digest(b'{}')})
                record(['synthetic','inventory'],events)
                checks.check('tenant-a:committed-count',True)
                checks.check('tenant-b:committed-count',True)
                checks.check('seed-persisted-fifteen-events',len(events)==15)
                checks.check('unknown-version-refused-before-application',True)
                checks.check('registered-logical-alias-evolution-executed',True)
                return {'before':events,'after':events,'actor':actor,'legacy_alias':alias,
                    'domain_registration_observation':registration,'dapr_application_readback':readback,
                    'registered_logical_alias_evolution':{'executed':True,'gateway_authority':False}},'effect','compatible'
            row,evidence=worker.case('logical-event-evolution','legacy-alias-replay',action,False)
            binding={'repository':str(preparation.ROOT),'main':{'files':[{'path':'tools/tests/test_p1r_published_executor.py',
                     'sha256':executor.digest(Path(__file__).read_bytes())}]}}
            retained=list(worker.witness_sources.values())
            executor.witnesses.validate_case(row,evidence,binding,'logical-event-evolution',retained)
            for name,mutation in (
                ('url',lambda value:value['observations']['dapr_application_readback'][0].update(url='http://127.0.0.1:12345/state/wrong')),
                ('sidecar',lambda value:value['observations']['dapr_application_readback'][0].update(
                    url=value['observations']['dapr_application_readback'][0]['url'].replace(':12345/',':12346/'))),
                ('payload-sha',lambda value:value['observations']['dapr_application_readback'][0].update(payload_sha256='f'*64)),
                ('missing-probe-argument',lambda value:next(command for command in value['commands']
                    if 'actor' in command['argv'])['argv'].pop()),
                ('wrong-tenant',lambda value:next(command for command in value['commands']
                    if 'actor' in command['argv'])['argv'].__setitem__(4,'tenant-b')),
                ('wrong-aggregate',lambda value:next(command for command in value['commands']
                    if 'actor' in command['argv'])['argv'].__setitem__(5,'other')),
                ('wrong-count',lambda value:next(command for command in value['commands']
                    if 'actor' in command['argv'])['argv'].__setitem__(6,'11')),
                ('wrong-command',lambda value:next(command for command in value['commands']
                    if 'actor' in command['argv'])['argv'].__setitem__(7,'OtherCommand')),
                ('wrong-executable',lambda value:next(command for command in value['commands']
                    if 'actor' in command['argv'])['argv'].__setitem__(0,'python')),
                ('failed-probe',lambda value:next(command for command in value['commands']
                    if 'actor' in command['argv']).update(exit_code=1)),
                ('inventory',lambda value:value['observations']['after'][0].update(sha256='f'*64)),
                ('actor',lambda value:value['observations']['actor'].update(accepted=False))):
                with self.subTest(name=name):
                    altered=copy.deepcopy(evidence)
                    mutation(altered)
                    with self.assertRaises(preparation.InvalidPacket):
                        executor.witnesses.validate_case(row,altered,binding,'logical-event-evolution',retained)
            altered=copy.deepcopy(evidence)
            changed=copy.deepcopy(row)
            key=events[0]['key']
            for name in ('before','after'):
                next(item for item in altered['observations'][name] if item['key']==key)['sha256']='f'*64
            for command in altered['commands']:
                if 'inventory' in command['argv']:
                    inventory=json.loads(command['output'])
                    next(item for item in inventory if item['key']==key)['sha256']='f'*64
                    command['output']=executor.canonical(inventory).decode()
                    command['output_sha256']=executor.digest(command['output'].encode())
            changed['inventory']={name+'_sha256':executor.digest(executor.canonical(
                [item for item in altered['observations'][name] if item['kind']!='bookkeeping']))
                for name in ('before','after')}
            with self.assertRaisesRegex(preparation.InvalidPacket,'exact provider event inventory'):
                executor.witnesses.validate_case(changed,altered,binding,'logical-event-evolution',retained)

    def testUnknownVersionWitnessRejectsWrongActorProviderKey(self):
        artifact=Path(__file__).resolve().parents[2]/'_bmad-output/implementation-artifacts/evidence/6-1-p1r-source-repair-2026-10-10'
        original=json.loads((artifact/'source-cases/unknown-version-refusal.json').read_text())
        binding=json.loads((artifact/'executor-source.json').read_text())
        sources=json.loads((artifact/'predicate-sources.json').read_text())
        executor.witnesses.validate_case(original['case'],original['evidence'],binding,original['lane'],sources)
        altered=copy.deepcopy(original['evidence'])
        exact='eventstore||AggregateActor||tenant-a:counter:fixture||tenant-a:counter:fixture:events:7'
        changed=0
        for command in altered['commands']:
            try:
                pairs=json.loads(command.get('output') or '')
            except (ValueError,TypeError):
                continue
            if not isinstance(pairs,list):
                continue
            for pair in pairs:
                if isinstance(pair,list) and len(pair)==2 and pair[0]==exact:
                    pair[0]=exact.replace('||AggregateActor||','||OtherActor||')
                    changed+=1
            if any(isinstance(pair,list) and len(pair)==2 and pair[0].startswith('eventstore||OtherActor||')
                   for pair in pairs):
                command['output']=executor.canonical(pairs).decode()
                command['output_sha256']=executor.digest(command['output'].encode())
        self.assertGreater(changed,0)
        with self.assertRaisesRegex(preparation.InvalidPacket,'retained provider diagnostic'):
            executor.witnesses.validate_case(original['case'],altered,binding,original['lane'],sources)

    def testCompatibleStaleFenceWitnessRejectsAlteredDenialAndInventory(self):
        with tempfile.TemporaryDirectory() as scratch:
            worker=self.worker(scratch)
            rows=[{'key':'tenant-a:counter:fixture:metadata','tenant':'tenant-a','kind':'metadata',
                   'sha256':'a'*64,'sequence':12,'floor':1}]
            operation={'accepted':False,'unexpected':False,'sequence':12,'stale_refused':True,
                'stale_denial':'stale-fencing-token','stale_actor_method':'ProcessFencedCommandAsync',
                'stale_diagnostic':[{'actual_exception_type':'System.InvalidOperationException',
                                     'denial_message':'The idempotency execution authority is no longer current.'}],
                'forged_refused':True,'forged_denial':'stale-or-invalid-fence',
                'forged_actor_method':'ProcessFencedCommandAsync',
                'forged_diagnostic':[{'actual_exception_type':'System.InvalidOperationException',
                                      'denial_message':'The idempotency execution fence is missing, stale, or invalid.'}]}
            url='http://127.0.0.1:12345/qualification/stale-fence'
            def action(checks):
                worker.record(['synthetic','inventory'],worker.scratch,preparation.stamp(),0,executor.canonical(rows))
                worker.record(['HTTP','POST',url],worker.scratch,preparation.stamp(),0,executor.canonical(operation))
                worker.record(['synthetic','inventory'],worker.scratch,preparation.stamp(),0,executor.canonical(rows))
                for identity in ('tenant-a:committed-count','tenant-b:committed-count','seed-persisted-fifteen-events',
                                 'stale-context-refused','forged-proof-refused','refusal-domain-preserved'):
                    checks.check(identity,True)
                return {'before':rows,'after':rows,'operation':operation},'refusal','compatible'
            row,evidence=worker.case('mixed-api','source-stale-fence',action,False)
            binding={'repository':str(preparation.ROOT),'main':{'files':[{'path':'tools/tests/test_p1r_published_executor.py',
                     'sha256':executor.digest(Path(__file__).read_bytes())}]}}
            retained=list(worker.witness_sources.values())
            executor.witnesses.validate_case(row,evidence,binding,'mixed-api',retained)
            for name,mutation in (
                ('denial',lambda value:value['observations']['operation']['stale_diagnostic'][0].update(denial_message='opaque')),
                ('stale-type',lambda value:value['observations']['operation']['stale_diagnostic'][0].update(
                    actual_exception_type='System.Exception')),
                ('forged-type',lambda value:value['observations']['operation']['forged_diagnostic'][0].update(
                    actual_exception_type='System.Exception')),
                ('inventory',lambda value:value['observations']['after'][0].update(sha256='b'*64))):
                with self.subTest(name=name):
                    altered=copy.deepcopy(evidence)
                    mutation(altered)
                    with self.assertRaises(preparation.InvalidPacket):
                        executor.witnesses.validate_case(row,altered,binding,'mixed-api',retained)

    def testStaleFenceExecutorRejectsWrongDiagnosticExceptionTypes(self):
        with tempfile.TemporaryDirectory() as scratch:
            worker=self.worker(scratch)
            worker.host_port=12345
            baseline={'accepted':False,'unexpected':False,'sequence':12,'stale_refused':True,
                'stale_denial':'stale-fencing-token','stale_actor_method':'ProcessFencedCommandAsync',
                'stale_diagnostic':[{'actual_exception_type':'System.InvalidOperationException',
                                     'denial_message':'The idempotency execution authority is no longer current.'}],
                'forged_refused':True,'forged_denial':'stale-or-invalid-fence',
                'forged_actor_method':'ProcessFencedCommandAsync',
                'forged_diagnostic':[{'actual_exception_type':'System.InvalidOperationException',
                                      'denial_message':'The idempotency execution fence is missing, stale, or invalid.'}]}
            def evaluate(observation):
                checks=executor.Measurements()
                with mock.patch.object(worker,'probe',return_value={'actor_methods':['ProcessFencedCommandAsync']}), \
                     mock.patch.object(worker,'seed'), mock.patch.object(worker,'inventory',return_value=[]), \
                     mock.patch.object(worker,'start_nodes'), mock.patch.object(worker,'stop_nodes'), \
                     mock.patch.object(worker,'http',return_value=observation):
                    _,_,disposition=worker.mixed_case('source-stale-fence',checks)
                return disposition,{item['id']:item['passed'] for item in checks.checks}
            self.assertEqual(evaluate(baseline)[0],'compatible')
            for diagnostic,check in (('stale_diagnostic','stale-context-refused'),
                                     ('forged_diagnostic','forged-proof-refused')):
                with self.subTest(diagnostic=diagnostic):
                    altered=copy.deepcopy(baseline)
                    altered[diagnostic][0]['actual_exception_type']='System.Exception'
                    disposition,checks=evaluate(altered)
                    self.assertEqual(disposition,'incompatible')
                    self.assertFalse(checks[check])

    def testStatusBindingRetainsStartupFailuresAndRequiresSuccessfulJsonReadback(self):
        with tempfile.TemporaryDirectory() as scratch:
            worker=self.worker(scratch)
            def action(checks):
                observed=[]
                worker.record(['HTTP','GET','http://127.0.0.1:12345/ready'],worker.scratch,preparation.stamp(),124,b'URLError')
                for retryable in (None,False,True):
                    label='null' if retryable is None else str(retryable).lower()
                    value={'messageId':'fixture-'+label,'correlationId':'fixture-correlation','retryable':retryable,
                           'recoveryReasonCode':'fixture-code','drainAttemptCount':3,'domain':'counter','committedEventSequence':12}
                    worker.record(['HTTP','GET','http://127.0.0.1:12345/status/'+label],worker.scratch,preparation.stamp(),0,executor.canonical(value))
                    for name in ('messageId','correlationId','retryable','recoveryReasonCode','drainAttemptCount','domain','committedEventSequence'):
                        checks.check('status:'+label+':'+name,name in value and value[name] == value[name])
                    observed.append({'retryability_case':label,'input':value,'readback':value})
                return {'operation':observed},'effect','compatible'
            row,evidence=worker.case('mixed-api','3.119.0-status',action,False)
            binding={'repository':str(preparation.ROOT),'main':{'files':[{'path':'tools/tests/test_p1r_published_executor.py',
                     'sha256':executor.digest(Path(__file__).read_bytes())}]}}
            retained=list(worker.witness_sources.values())
            executor.witnesses.validate_case(row,evidence,binding,'mixed-api',retained)
            self.assertEqual(evidence['commands'][0]['output'],'URLError')
            for output,status in (('URLError',0),('[]',0),(evidence['commands'][1]['output'],124)):
                with self.subTest(output=output,status=status):
                    failed=json.loads(json.dumps(evidence))
                    failed['commands'][1]['output']=output
                    failed['commands'][1]['exit_code']=status
                    with self.assertRaisesRegex(preparation.InvalidPacket,'status readback'):
                        executor.witnesses.validate_case(row,failed,binding,'mixed-api',retained)



class BoundaryAdmissionTests(unittest.TestCase):
    def testAdmissionReceiptBindsProbeAndSourceWitness(self):
        lane, identifier = "metadata-write", "3.119.0-to-3.70.1"
        with tempfile.TemporaryDirectory() as scratch:
            worker = executor.Executor.__new__(executor.Executor)
            worker.commands, worker.configurations, worker.errors = [], [], []
            worker.scratch = Path(scratch)
            worker.save = worker.stop_nodes = mock.Mock()
            worker.witness_sources = {}
            worker.support_rules = {(lane, identifier): "refused"}
            admission = {"handling": "refused", "lane": lane, "case_id": identifier,
                         "boundary": "before-protected-route", "reason_code": "incapable-published-endpoint",
                         "protected_access": False, "effect": False, "write": False, "loaded_version": "3.70.1+fixture"}
            value = {"instrumentation": executor.MECHANISM, "observation": admission,
                     "measurement": {"checks": [{"id": "admission-refused", "passed": True}],
                                     "assertions": {"attempted": 1, "passed": 1, "failed": 0}}}

            def probe(version, arguments, checks, prefix):
                command = worker.record(["dotnet", str(worker.scratch / "consumers/comparison-3.70.1/probe/bin/Release/net10.0/Probe.dll"),
                                         *arguments], worker.scratch, preparation.stamp(), 0, executor.canonical(value))
                checks.import_probe(value, prefix, command["id"])
                return admission

            worker.probe = probe
            row, evidence = worker.case(lane, identifier, lambda checks: worker.boundary_case(lane, identifier, checks), False)
            source = Path(executor.__file__)
            binding = {"repository": str(preparation.ROOT), "main": {"files": [
                {"path": source.relative_to(preparation.ROOT).as_posix(), "sha256": executor.digest(source.read_bytes())}]}}
            executor.witnesses.validate_case(row, evidence, binding, lane, list(worker.witness_sources.values()))
            policy = qualification.load_support_table({"candidate": {"version": "3.119.0"},
                                                        "comparisons": [{"version": version} for version in qualification.COMPARISON_VERSIONS]})
            rule = next(item for item in policy["lanes"][lane]["cases"] if item["id"] == identifier)
            self.assertTrue(qualification.supported_case(rule, row, evidence, lane))

    def testUnsafeOldWriterRefusesBeforeAnyWriterOrActorCall(self):
        worker = executor.Executor.__new__(executor.Executor)
        worker.probe = mock.Mock(return_value={"handling": "refused", "boundary": "before-protected-route",
                                              "protected_access": False, "effect": False, "write": False})
        worker.actor = mock.Mock(side_effect=AssertionError("actor must not be reached"))
        worker.seed = mock.Mock(side_effect=AssertionError("writer must not be reached"))
        checks = executor.Measurements()
        observed, outcome, disposition = worker.boundary_case("metadata-write", "3.119.0-to-3.70.1", checks)
        worker.probe.assert_called_once_with("3.70.1", ["admit", "metadata-write", "3.119.0-to-3.70.1"],
                                             checks, "admission:")
        worker.actor.assert_not_called()
        worker.seed.assert_not_called()
        self.assertEqual((outcome, disposition), ("refusal", "incompatible"))
        self.assertTrue(observed["unsupported"])
        self.assertEqual(checks.counter["failed"], 0)

    def testOpaqueOrLateBoundaryResponseDoesNotQualify(self):
        worker = executor.Executor.__new__(executor.Executor)
        worker.probe = mock.Mock(return_value={"handling": "refused", "boundary": "after-protected-route",
                                              "protected_access": True, "effect": False, "write": False})
        checks = executor.Measurements()
        worker.boundary_case("query-wire", "3.119.0-3.70.1-json-dual", checks)
        self.assertEqual(checks.counter["failed"], 1)


class PublishedPatchSourceTests(unittest.TestCase):
    """Real selection, wait and snapshot callers; Git observations are tooling fixtures."""

    def fixture(self):
        source_root = Path('/selected/tagged-source')
        clean = {'head': qualification.SELECTED_SOURCE_COMMIT, 'diff_sha256': preparation.digest(b''),
                 'files': [{'path': 'src/Production.cs', 'git_blob': 'a'*40,
                            'index_blob': 'a'*40, 'sha256': 'b'*64}],
                 'gitlinks': {'references/Hexalith.Builds': qualification.SELECTED_TAG_BUILDS}}
        builds = dict(copy.deepcopy(clean), head=qualification.SELECTED_TAG_BUILDS)
        source = {'repository': str(source_root), 'main': clean,
                  'dependencies': [{'path':'references/Hexalith.Builds','initialized':True,'binding':builds}]}
        binding = {'repository':str(preparation.ROOT), 'main':{'head':'f'*40}, 'candidate_source':source}
        candidate = {'version':'3.125.1','tag':'v3.125.1','tag_commit':qualification.SELECTED_SOURCE_COMMIT,
            'tag_builds_commit':qualification.SELECTED_TAG_BUILDS,
            'builds':{'version':'4.33.1','commit':'c'*40},'feed':'https://api.nuget.org/v3/index.json'}
        inputs = {'candidate':candidate,'rollback':None,'comparisons':[{'version':v} for v in qualification.COMPARISON_VERSIONS],
            'assertion_instrumentation':{'mechanism':executor.MECHANISM},'selected_additions':list(qualification.ADDITIONS),
            'operational_profile':{'runtime':'dapr','runtime_version':'1.18.2','backend':'state.postgresql','backend_image':executor.POSTGRES}}
        def git(root,*arguments):
            if Path(root) != source_root:
                return b'f'*40
            if arguments[0] == 'ls-tree':
                return ('160000 commit '+qualification.SELECTED_TAG_BUILDS+'\treferences/Hexalith.Builds\n').encode()
            return qualification.SELECTED_SOURCE_COMMIT.encode()
        return source_root,binding,inputs,git

    def testNewerVerifierCanSelectOnlyExactCleanTaggedProductionAndBuildClosure(self):
        source_root,binding,inputs,git = self.fixture()
        for mutation in (None,'head','builds','dirty','missing-builds','tag','feed'):
            changed = copy.deepcopy(binding)
            selected = copy.deepcopy(inputs)
            if mutation == 'head': changed['candidate_source']['main']['head'] = '0'*40
            if mutation == 'builds': changed['candidate_source']['dependencies'][0]['binding']['head'] = '0'*40
            if mutation == 'dirty': changed['candidate_source']['main']['diff_sha256'] = preparation.digest(b'dirty source')
            if mutation == 'missing-builds': changed['candidate_source']['dependencies'] = []
            if mutation == 'tag': selected['candidate']['tag_commit'] = '0'*40
            if mutation == 'feed': selected['candidate']['feed'] = 'https://substituted.invalid/v3/index.json'
            with self.subTest(mutation=mutation), mock.patch.object(qualification,'validate_inputs',return_value='owner-selected'), \
                    mock.patch.object(preparation,'source_binding',return_value=changed) as bind, \
                    mock.patch.object(preparation,'require_tagged_checkout'), \
                    mock.patch.object(preparation,'self_contained_checkout'), mock.patch.object(preparation,'git',side_effect=git):
                if mutation is None:
                    executor.validate_execution_inputs(selected,source_root)
                    bind.assert_called_once_with(executor.ROOT,source_root)
                else:
                    with self.assertRaises(preparation.InvalidPacket):
                        executor.validate_execution_inputs(selected,source_root)

    def testMissingCandidateClosureCannotBeRehashedIntoAnImport(self):
        _,binding,inputs,_ = self.fixture()
        del binding['candidate_source']
        for binder in (qualification.bind_lane,qualification.bind_recovery):
            context = {'source_binding':binding,'inputs':{'value':inputs,'scope':'owner-selected'}}
            with self.subTest(binder=binder.__name__), self.assertRaisesRegex(preparation.InvalidPacket,'source closure missing'):
                if binder == qualification.bind_lane:
                    binder({'lane':'checkout'},{'scope':'operational'},context)
                else:
                    binder({}, {'scope':'operational'}, 'candidate-restore', context)

    def testReadyWaitRechecksSourceAfterReturningAndRefusesDrift(self):
        worker = executor.Executor.__new__(executor.Executor)
        worker.http = mock.Mock(return_value={'ready':True})
        worker.source_binding = {'repository':'/selected/verifier','candidate_source':{'repository':'/selected/source'}}
        worker.inputs = None
        item = {'process':mock.Mock()}
        item['process'].poll.return_value = None
        with mock.patch.object(preparation,'check_current_source',side_effect=preparation.InvalidPacket('post-wait drift')) as check:
            with self.assertRaisesRegex(preparation.InvalidPacket,'post-wait drift'):
                worker.wait('http://127.0.0.1:12345/ready',item)
        worker.http.assert_called_once()
        check.assert_called_once_with(worker.source_binding)

    def testSelectedPatchRequiresEveryGenuineSnapshotWitness(self):
        helper = QualifiedSnapshotWitnessTests()
        helper.candidate_version = '3.125.1'
        helper.setUp()
        self.addCleanup(helper.doCleanups)
        for lane in ('post-upgrade-restore','snapshot-tail','retained-covered','retained-uncovered','metadata-write'):
            for writer in (('3.125.1',) if lane == 'post-upgrade-restore' else ('3.125.1','source')):
                with self.subTest(lane=lane,writer=writer):
                    fixture = helper.fixture(writer_version=writer,lane=lane)
                    helper.validate(fixture,lane)
                    del fixture[1]['snapshot_fixture_source']
                    with self.assertRaises(preparation.InvalidPacket):
                        helper.validate(fixture,lane)
        self.assertIn('3.125.1',qualification.ISOLATION_VERSIONS)
        self.assertFalse(executor.witnesses.requires_qualified_snapshot('3.127.0','snapshot-tail','source-to-3.110.0'))


if __name__ == "__main__":
    unittest.main()


class ReviewedExecutorFinalizationTests(unittest.TestCase):
    def testRealStoreRestoreFinalizationKeepsExternalResponsesAndInventories(self):
        from test_p1r_published_qualification import LaneReceiptTests, at
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            receipt = LaneReceiptTests.bound_restore()
            receipt['schema'] = runtime.RESTORE_SCHEMA_V2
            receipt['fixture'] = {'id':'p1r-'+'a'*32,'synthetic':False}
            remap = {identifier: identifier if identifier < 9 else identifier + 1 if identifier < 14 else 16 for identifier in range(1,15)}
            for command in receipt['commands']: command['id'] = remap[command['id']]
            for inventory in receipt['inventories'].values(): inventory['command'] = remap[inventory['command']]
            prototype = copy.deepcopy(receipt['commands'][8])
            receipt['commands'].extend([dict(prototype,id=9,step='replay'),dict(prototype,id=15,step='replay')])
            receipt['commands'].sort(key=lambda row:row['id'])
            for command in receipt['commands']:
                command['started_utc'] = command['finished_utc'] = at(command['id'])
            binding = {'repository':str(root/'checkout')}
            (root/'checkout').mkdir()
            enclosing = storage.receipt_context(receipt,binding,{'candidate':{'version':'3.125.1'}})
            store = storage.Store(root/'proof',enclosing)
            responses = {}
            for name,identity,tenant,sequence,events in (('primary',9,'tenant-a',12,0),('append',10,'tenant-a',13,1),
                    ('replay',14,'tenant-a',13,0),('secondary',15,'tenant-b',3,0)):
                command = receipt['commands'][identity-1]
                command['step'] = 'append' if name == 'append' else 'replay'
                command['argv'] = ['dotnet','/owned/Probe.dll','actor','http://127.0.0.1:1',tenant,'fixture',
                    '12' if name == 'append' else str(sequence),'IncrementCounter' if name == 'append' else 'AssertCounter']
                observation = {'accepted':True,'sequence':sequence,'event_count':events}
                value = {'instrumentation':executor.MECHANISM,'observation':observation,
                    'measurement':{'checks':[{'id':'executed','passed':True}], 'assertions':{'attempted':1,'passed':1,'failed':0}}}
                data = executor.canonical(value)
                command['output_sha256'] = executor.digest(data)
                recorded = {key:command[key] for key in storage.COMMAND_FIELDS}
                store.command(recorded,data)
                responses[name] = {'command':identity,'output_command':recorded,'observation':observation}
            receipt['observations'].update(responses=responses,restore_checks=[{'id':name,'passed':True} for name in runtime.ACTUAL_RESTORE_CHECKS])
            receipt = storage.retain_inventories(receipt,store)
            worker = executor.Executor.__new__(executor.Executor)
            worker.inputs,worker.candidate,worker.versions = {'selected_additions':[]},'3.125.1',()
            worker.output,worker.storage,worker.pending_restore,worker.receipts = root/'packet',store,receipt,[]
            worker.output.mkdir()
            worker.verify_selected_candidate_archives = worker.save = mock.Mock()
            worker.topology = mock.Mock(side_effect=RuntimeError('fixture execution boundary'))
            worker.cleanup = mock.Mock(return_value=receipt['cleanup'])
            with mock.patch.object(executor,'validate_execution_inputs'), mock.patch.object(executor,'provisional_successor_execution_policy',return_value={}):
                with self.assertRaisesRegex(RuntimeError,'fixture execution boundary'): worker.execute()
            persisted = json.loads(Path(worker.receipts[0]).read_bytes())
            self.assertIn('output_command',persisted['observations']['responses']['primary'])
            self.assertEqual(persisted['inventories']['source']['rows']['schema'],storage.JSON_SCHEMA)
            self.assertEqual(runtime.restore_outcome(persisted,enclosing)['execution'],'passed')

    def testObservationCaptureIdentitySurvivesGeneratedInputsAndCleanup(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            repository = root/'checkout';repository.mkdir()
            candidate = {'version':'3.121.0','tag':'v3.121.0','tag_commit':'a'*40,'tag_builds_commit':'b'*40,
                'builds':{'version':'4.31.0','commit':'c'*40},'feed':'https://api.nuget.org/v3/index.json','packages':[]}
            content_hash = base64.b64encode(b'x'*64).decode()
            for identifier in executor.PACKAGE_IDS:
                candidate['packages'].append({'id':identifier,'archive_sha256':executor.digest((identifier+':3.121.0').encode()),
                    'content_hash':content_hash,'repository_commit':'a'*40})
            planning, binding = {'candidate_proposed':candidate}, {'repository':str(repository)}
            def shared(worker):
                worker.record([sys.executable,'-c','pass'],worker.scratch,executor.stamp(),0,b'early observation')
                return {}
            def git(owner,*args):
                if args[0]=='ls-tree':return ('160000 commit '+'b'*40+'\treferences/Hexalith.Builds\n').encode()
                if args[0]=='describe':return b'v4.31.0'
                return ('a'*40).encode()
            with mock.patch.object(executor,'ROOT',repository), mock.patch.object(preparation,'source_binding',return_value=binding), \
                    mock.patch.object(executor.Executor,'shared',shared), mock.patch.object(preparation,'git',side_effect=git):
                worker = executor.Executor(root/'packet',observation=planning,observation_sha256=executor.digest(executor.canonical(planning)))
                self.addCleanup(lambda:shutil.rmtree(worker.scratch,ignore_errors=True))
                capture = dict(worker.storage.context)
                def build(version):
                    consumer = worker.output/'consumers'/version
                    for identifier in executor.PACKAGE_IDS:
                        package = consumer/'packages'/identifier.lower()/version
                        package.mkdir(parents=True)
                        (package/(identifier.lower()+'.'+version+'.nupkg')).write_bytes((identifier+':'+version).encode())
                        preparation.write_json(package/'.nupkg.metadata',{'contentHash':content_hash})
                    return consumer
                def inspect(path,data):
                    return ({'valid':True,'id':next(name for name in executor.PACKAGE_IDS if name.lower()==path.parent.parent.name),
                        'version':path.parent.name,'repository_commit':'a'*40}, {'signature':True,'synthetic_marker':False},{})
                def run(argv,**kwargs):
                    data = qualification.REPOSITORY_SIGNATURE.encode() if argv[:3]==['dotnet','nuget','verify'] else b''
                    worker.record(argv,repository,executor.stamp(),0,data)
                    return data
                worker.run = run
                worker.check_sources = mock.Mock()
                worker.build_consumers = build
                with mock.patch.object(qualification,'inspect_archive',side_effect=inspect):
                    worker.observe_inputs(planning,'bounded observation control')
                worker.cleanup()
                self.assertEqual(worker.storage.context,capture)
                self.assertEqual(storage.command_text(worker.commands[0],capture),'early observation')
                self.assertNotEqual(worker.result['generated_owner_inputs_sha256'],capture['inputs'])
                self.assertEqual(worker.result['capture_context'],capture)


class ReviewedExecutorSummaryTests(unittest.TestCase):
    def cli(self):
        spec = importlib.util.spec_from_file_location('review_executor_cli',preparation.ROOT/'tools/p1r-published-executor.py')
        cli = importlib.util.module_from_spec(spec);spec.loader.exec_module(cli)
        return cli

    def testRequestedSummarySurvivesAdmissionFailureBeforeChildCreation(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            cli = self.cli()
            with mock.patch.object(storage,'default_root',return_value=root/'diagnostics'), \
                    contextlib.redirect_stderr(io.StringIO()),contextlib.redirect_stdout(io.StringIO()):
                code = cli.main(['observe','--out',str(root/'absent-packet'),'--planning-observations',str(root/'missing.json'),
                    '--selection-reference','control','--summary-out',str(root/'summary')])
            self.assertEqual(code,2)
            summary = json.loads((root/'summary/summary.json').read_bytes())
            self.assertEqual(summary['outcome']['status'],'failed')
            self.assertEqual(summary['outcome']['exit_code'],2)
            self.assertEqual(summary['files'],0)

    def testActualCliAdmissionFailureNeverExportsForeignPacketOrSibling(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            packet, outer = root/'packet', root/'packet-isolation'
            packet.mkdir();outer.mkdir()
            originals = {packet/'private-control.txt':('packet-private-'+uuid.uuid4().hex).encode(),
                outer/'private-sibling.txt':('sibling-private-'+uuid.uuid4().hex).encode()}
            for path,data in originals.items():path.write_bytes(data)
            inputs = root/'inputs.json';inputs.write_text('{}')
            blocked = root/'bin';blocked.mkdir()
            marker = root/'docker-called'
            docker = blocked/'docker';docker.write_text("#!/bin/sh\n: > '"+str(marker)+"'\nexit 99\n");docker.chmod(0o700)
            env = dict(os.environ,PATH=str(blocked),HEXALITH_P1R_EVIDENCE_ROOT=str(root/'diagnostics'))
            env.pop('HEXALITH_P1R_ISOLATION',None)
            completed = subprocess.run([sys.executable,str(preparation.ROOT/'tools/p1r-published-executor.py'),
                'run','--out',str(packet),'--inputs',str(inputs),'--summary-out',str(root/'summary')],
                env=env,capture_output=True,timeout=30)
            self.assertEqual(completed.returncode,2,completed.stderr.decode())
            self.assertFalse(marker.exists())
            summary = json.loads((root/'summary/summary.json').read_bytes())
            self.assertEqual(summary['outcome']['status'],'failed')
            self.assertEqual(summary['outcome']['exit_code'],2)
            index = json.loads(storage.resolve(summary['complete_index'],summary['context'],{'kind':'complete-index'}))
            self.assertEqual(index['files'],[])
            for path,data in originals.items():self.assertEqual(path.read_bytes(),data)
            for path in root.rglob('*'):
                if path.is_file() and path not in originals:
                    retained = path.read_bytes()
                    for foreign,data in originals.items():
                        self.assertNotIn(foreign.name.encode(),retained,str(path))
                        self.assertNotIn(data,retained,str(path))
            for foreign,data in originals.items():
                self.assertNotIn(foreign.name.encode(),completed.stdout+completed.stderr)
                self.assertNotIn(data,completed.stdout+completed.stderr)

    def testRequestedSummaryKeepsOwnedPartialExecutorWithoutAdoptingForeignSibling(self):
        import p1r_isolation as isolation
        for existing in (False,True):
            with self.subTest(existing=existing),tempfile.TemporaryDirectory() as temporary:
                root = Path(temporary)
                packet,outer = root/'packet',root/'packet-isolation'
                outer.mkdir();foreign=outer/'foreign-sibling.txt';data=uuid.uuid4().hex.encode();foreign.write_bytes(data)
                if existing:
                    packet.mkdir();(packet/'foreign-packet.txt').write_bytes(data)
                planning=root/'planning.json';planning.write_text('{"candidate_proposed":{}}')
                cli=self.cli()
                with mock.patch.object(cli,'validate_observation_inputs'), \
                        mock.patch.object(storage,'default_root',return_value=root/'diagnostics'), \
                        mock.patch.object(isolation,'private_proof',return_value={}), \
                        mock.patch.object(preparation,'source_binding',side_effect=preparation.InvalidPacket('owned initialization control')), \
                        mock.patch.object(executor.Executor,'run',side_effect=AssertionError('Docker blocked')), \
                        mock.patch.object(executor.Executor,'check_sources'), \
                        mock.patch.dict(os.environ,{'HEXALITH_P1R_ISOLATION':'a'*32},clear=True), \
                        contextlib.redirect_stdout(io.StringIO()),contextlib.redirect_stderr(io.StringIO()):
                    self.assertEqual(cli.main(['observe','--out',str(packet),'--planning-observations',str(planning),
                        '--selection-reference','control','--summary-out',str(root/'summary')]),2)
                summary=json.loads((root/'summary/summary.json').read_bytes())
                index=json.loads(storage.resolve(summary['complete_index'],summary['context'],{'kind':'complete-index'}))
                paths={row['path'] for row in index['files']}
                self.assertEqual(summary['outcome']['status'],'failed')
                if existing:self.assertEqual(paths,set())
                else:
                    self.assertIn('execution.json',paths)
                    self.assertGreaterEqual(summary['outcome']['cleanup']['attempts'],4)
                self.assertEqual(foreign.read_bytes(),data)
                self.assertFalse(summary['isolation_proof'])
                for path in root.rglob('*'):
                    if path.is_file() and path not in (foreign,packet/'foreign-packet.txt'):
                        self.assertNotIn(data,path.read_bytes(),str(path))
                        self.assertNotIn(b'foreign-sibling.txt',path.read_bytes(),str(path))
                        self.assertNotIn(b'foreign-packet.txt',path.read_bytes(),str(path))

    def testRequestedSummaryKeepsPartialControllerCleanupAfterConstructorFailure(self):
        for existing in (False,True):
            with self.subTest(existing=existing),tempfile.TemporaryDirectory() as temporary:
                root=Path(temporary)
                packet,outer=root/'attempt/packet',root/'attempt/packet-isolation'
                data=uuid.uuid4().hex.encode()
                if existing:
                    packet.mkdir(parents=True);outer.mkdir()
                    (packet/'foreign-packet.txt').write_bytes(data)
                    (outer/'foreign-sibling.txt').write_bytes(data)
                planning=root/'planning.json';planning.write_text('{}')
                cli=self.cli()
                with mock.patch.object(cli,'validate_observation_inputs'), \
                        mock.patch.object(storage,'default_root',return_value=root/'diagnostics'), \
                        mock.patch.object(subprocess,'check_output',return_value=b'tcp://rejected'), \
                        mock.patch.object(subprocess,'run',side_effect=AssertionError('Docker blocked')), \
                        mock.patch.dict(os.environ,{},clear=True), \
                        contextlib.redirect_stdout(io.StringIO()),contextlib.redirect_stderr(io.StringIO()):
                    self.assertEqual(cli.main(['observe','--out',str(packet),
                        '--planning-observations',str(planning),'--selection-reference','control',
                        '--summary-out',str(root/'summary')]),2)
                summary=json.loads((root/'summary/summary.json').read_bytes())
                self.assertEqual(summary['outcome']['status'],'failed')
                if not existing:
                    self.assertGreater(summary['outcome']['cleanup']['attempts'],0)
                    self.assertIn('isolation.json',summary['isolation_proof'])
                else:
                    self.assertEqual(summary['files'],0)
                    self.assertFalse(summary['isolation_proof'])
                    for path in root.rglob('*'):
                        if path.is_file() and path not in (packet/'foreign-packet.txt',outer/'foreign-sibling.txt'):
                            retained=path.read_bytes()
                            self.assertNotIn(data,retained,str(path))
                            self.assertNotIn(b'foreign-packet.txt',retained,str(path))
                            self.assertNotIn(b'foreign-sibling.txt',retained,str(path))

    def testRequestedSummaryIncludesOuterCleanupFailureWithoutChildPacket(self):
        import p1r_isolation as isolation
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            planning = root/'planning.json';planning.write_text('{}')
            outer = root/'packet-isolation';outer.mkdir()
            result = {'passed':False,'errors':[],'command_exit_code':0,
                'cleanup':[{'errors':['owned cleanup failed'],'remaining':['owned']}], 'command_stdout':''}
            preparation.write_json(outer/'isolation.json',result)
            store = storage.Store(root/'proof',storage.context('a'*32,preparation.ROOT))
            runner = mock.Mock(output=outer,storage=store,result=result,owns_attempt=True)
            runner.run.return_value = 2
            class Runner:
                def __new__(cls):return runner
                def __init__(self,*args,**kwargs):pass
            with mock.patch.object(isolation,'IsolatedRunner',Runner), \
                    mock.patch.object(cli := self.cli(),'validate_observation_inputs'), \
                    mock.patch.dict(os.environ,{},clear=True),contextlib.redirect_stdout(io.StringIO()):
                self.assertEqual(cli.main(['observe','--out',str(root/'packet'),'--planning-observations',str(planning),
                    '--selection-reference','control','--summary-out',str(root/'summary')]),2)
            summary = json.loads((root/'summary/summary.json').read_bytes())
            self.assertEqual(summary['outcome']['status'],'failed')
            self.assertEqual(summary['outcome']['cleanup']['remaining'],1)
            self.assertIn('isolation.json',summary['isolation_proof'])

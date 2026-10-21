"""Producer-shaped success and rehashed semantic refusals through real receipt import."""
import copy
import ast
import inspect
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest
import uuid
from unittest import mock

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

import p1r_evidence_storage as storage
import p1r_isolation as isolation
import p1r_isolation_evidence as isolation_evidence
import p1r_published_qualification as qualification
import p1r_qualification as preparation
from p1r_isolation_fixture import run_controller, separate_inputs, separate_source_fixture
import test_p1r_published_executor as published_tests


class TaggedSourceLifecycleTests(unittest.TestCase):
    def testControllerRechecksSelectedSourceBeforeProvisioningAndAfterOuterCleanup(self):
        witness = published_tests.Published31251SnapshotWitnessTests()
        witness.setUp()
        self.addCleanup(witness.doCleanups)
        events = []
        check = preparation.check_current_source
        docker = isolation.IsolatedRunner.docker
        finalize = qualification.finalize_isolation_receipts
        def checked(binding, **kwargs):
            events.append(('source', kwargs))
            return check(binding, **kwargs)
        def called(worker, *args, **kwargs):
            events.append(('docker', None))
            return docker(worker, *args, **kwargs)
        def finalized(*args, **kwargs):
            events.append(('finalize', None))
            return finalize(*args, **kwargs)
        with mock.patch.object(preparation, 'check_current_source', side_effect=checked), \
                mock.patch.object(isolation.IsolatedRunner, 'docker', new=called), \
                mock.patch.object(qualification, 'finalize_isolation_receipts', side_effect=finalized):
            witness.import_fixture(scope='operational')
        self.assertEqual(events[0][0], 'source')
        final = next(index for index, value in enumerate(events) if value[0] == 'finalize')
        self.assertEqual(events[final - 1][0], 'source')
        self.assertTrue(any(value == ('source', {'check_python': False}) for value in events[final + 1:]))

    def testIndependentImportRefusesRehashedRoleAndRootSubstitution(self):
        for record,mutation in (
            ('private',lambda value:value['source_binding']['candidate_source'].__setitem__('repository',str(preparation.ROOT))),
            ('wrapper',lambda value:value.__setitem__('source_root',str(preparation.ROOT))),
            ('wrapper',lambda value:value['controller_source_binding']['candidate_source']['main'].__setitem__('head','0'*40)),
            ('wrapper',lambda value:value['inner_command'].__setitem__(-1,str(preparation.ROOT))),
        ):
            witness = published_tests.Published31251SnapshotWitnessTests()
            witness.setUp()
            self.addCleanup(witness.doCleanups)
            receipt,context = witness.import_fixture(scope='operational')
            self.assertEqual(qualification.receipt_outcome(receipt,context)[2]['execution'],'passed')
            witness.change_isolation(receipt,record,mutation)
            with self.subTest(record=record),self.assertRaises(preparation.InvalidPacket):
                qualification.receipt_outcome(receipt,context)
            witness.doCleanups()


class SeparateRootIsolationTests(unittest.TestCase):
    def testExactSourceSuffixAndPrivatePreflightKeepBothBindings(self):
        with tempfile.TemporaryDirectory() as temporary, separate_source_fixture(temporary) as f:
            import p1r_published_executor as executor
            selected = f.root/'inputs.json'
            preparation.write_json(selected,separate_inputs(f))
            command = ['python3',str(f.tooling/'tools/p1r-published-executor.py'),'run','--out',str(f.root/'output'),
                       '--inputs',str(selected),'--source-root',str(f.source),'--source-builds-root',str(f.source_builds)]
            with mock.patch.object(isolation,'ROOT',f.tooling), mock.patch.object(executor,'ROOT',f.tooling):
                binding = isolation.command_source_binding(command)
                self.assertEqual(binding['runtime_source']['repository'],str(f.source))
                self.assertEqual(isolation.executor_input(command,f.tooling)['sha256'],preparation.digest(selected.read_bytes()))
                for changed in (command[:-2],command+command[-4:],command[:-1]+[str(f.builds)]):
                    with self.assertRaises(preparation.InvalidPacket): isolation.command_source_binding(changed)

    def testSourceDriftAfterChildStartStillRunsRepeatedOwnedCleanup(self):
        with tempfile.TemporaryDirectory() as temporary, separate_source_fixture(temporary) as f:
            binding = preparation.source_binding(f.tooling,f.pins,f.source,f.source_builds)
            target = f.source/'src/Runtime.cs'
            command = [sys.executable,'-c','from pathlib import Path; import sys; Path(sys.argv[1]).write_text("// drift\\n")',str(target)]
            output = f.root/'inner'
            with mock.patch.object(isolation,'private_proof',return_value={}), \
                    mock.patch.object(isolation,'command_source_binding',return_value=binding), \
                    mock.patch.object(isolation,'private_environment',return_value=dict(os.environ)):
                self.assertEqual(isolation._inner(output,'outer',command),2)
            result = preparation.read_json(output/'private-execution.json')
            self.assertTrue(result['errors'])
            self.assertFalse(result['passed'])
            self.assertEqual(len(result['cleanup']),2)
            self.assertTrue(all(item['remaining'] == [] and item['errors'] == [] for item in result['cleanup']))


class LifecycleTests(unittest.TestCase):
    def testSeparateRuntimeRootsSurviveProducerFinalizerAndIndependentImport(self):
        witness = published_tests.Published31251SnapshotWitnessTests()
        witness.setUp()
        self.addCleanup(witness.doCleanups)
        with mock.patch.object(witness,'attach_isolation',side_effect=lambda receipt,context:receipt):
            receipt,context = witness.import_fixture(scope='operational')
        root = Path(witness.temporary.name)
        source_tree = tempfile.TemporaryDirectory(dir=preparation.ROOT)
        self.addCleanup(source_tree.cleanup)
        source, builds = Path(source_tree.name)/'release-source',Path(source_tree.name)/'release-builds'
        (source/'deploy/dapr').mkdir(parents=True)
        builds.mkdir()
        shutil.copyfile(preparation.ROOT/'deploy/dapr/statestore-postgresql.yaml',source/'deploy/dapr/statestore-postgresql.yaml')
        pins = {'commit':'a'*40,'builds_commit':'b'*40}
        binding = context['source_binding']
        binding.update(tooling=pins,runtime_source={'repository':str(source),'main':{'head':preparation.RUNTIME_COMMIT},
                       'builds':{'path':str(builds),'binding':{'head':preparation.RUNTIME_BUILDS_COMMIT}}})
        binding.setdefault('main',{})['head'] = pins['commit']
        selection = context['inputs']['value']
        selection['tooling'] = pins
        selection['candidate'].update(tag='v3.125.1',tag_commit=preparation.RUNTIME_COMMIT,tag_builds_commit=preparation.RUNTIME_BUILDS_COMMIT)
        receipt['execution_evidence']['executor_source_sha256'] = preparation.digest(json.dumps(binding,indent=2,sort_keys=True).encode()+b'\n')
        preparation.write_json(root/'executor-source.json',binding)
        execution = preparation.read_json(root/'execution.json')
        execution['executor_source_sha256'] = preparation.digest((root/'executor-source.json').read_bytes())
        preparation.write_json(root/'execution.json',execution)
        with mock.patch.object(isolation,'command_source_binding',return_value=binding), \
                mock.patch.object(preparation,'check_current_source'):
            code,worker,retained = run_controller(root,receipt,context,source_paths=(source,builds))
            self.assertEqual(code,0,worker.result['errors'])
            self.assert_imports(retained,context)
            reference = retained['execution_evidence']['isolation']
            wrapper = json.loads(storage.preserved_bytes(reference['wrapper'], storage.receipt_context(retained, context["source_binding"], context["inputs"]["value"])))
            wrapper = storage.materialize(wrapper, wrapper['evidence_context'])
            private = json.loads(storage.preserved_bytes(reference['private'], storage.receipt_context(retained, context["source_binding"], context["inputs"]["value"])))
            execution = json.loads(storage.preserved_bytes(reference['execution'], storage.receipt_context(retained, context["source_binding"], context["inputs"]["value"])))
            with tempfile.TemporaryDirectory() as outside:
                for role,index in (('source',-3),('builds',-1)):
                    submitted,changed = copy.deepcopy(context),copy.deepcopy(wrapper)
                    runtime = submitted['source_binding']['runtime_source']
                    if role == 'source': runtime['repository'] = outside
                    else: runtime['builds']['path'] = outside
                    changed['inner_command'][index] = outside
                    with self.subTest(context_mismatch=role), self.assertRaisesRegex(preparation.InvalidPacket,'isolation artifact context differs'):
                        isolation_evidence.validate(changed,private,dict(submitted,execution=execution),root,
                                                    reference['wrapper']['path'],qualification.validate_times)
                    changed['evidence_context']['source'] = storage.source_identity(submitted['source_binding'])
                    with self.subTest(role=role), self.assertRaisesRegex(preparation.InvalidPacket,'outside the isolated workspace'):
                        isolation_evidence.validate(changed,private,dict(submitted,execution=execution),root,
                                                    reference['wrapper']['path'],qualification.validate_times)
            # Fresh wrapper hashes cannot make a different runtime path selected.
            witness.change_isolation(retained,'wrapper',lambda value:value['inner_command'].__setitem__(-3,str(preparation.ROOT)))
            with self.assertRaises(preparation.InvalidPacket): qualification.receipt_outcome(retained,context)

    def fixture(self, **options):
        witness = published_tests.ProposedQualifiedSnapshotWitnessTests()
        witness.setUp()
        self.addCleanup(witness.doCleanups)
        # Supply the real caller with a receipt before finalization.
        with mock.patch.object(witness,'attach_isolation',side_effect=lambda receipt,context:receipt):
            receipt,context = witness.import_fixture(scope='operational')
        root = Path(witness.temporary.name)
        code,worker,retained = run_controller(root,receipt,context,**options)
        return witness,code,worker,retained,context

    def assert_imports(self, receipt, context):
        self.assertIn('isolation',receipt['execution_evidence'], 'caller finalization was not executed')
        self.assertEqual(qualification.receipt_outcome(receipt,context)[2]['execution'],'passed')

    def testRealCallerFinalizesPersistedReceiptOnlyAfterPrivateAndOuterTeardown(self):
        _,code,worker,receipt,context = self.fixture()
        self.assertEqual(code,0,worker.result['errors'])
        self.assertFalse(worker.scratch.exists())
        self.assertEqual(worker.result['pending_containers'],{})
        self.assert_imports(receipt,context)

    def testPositiveControlFailsWhenCallerFinalizationIsRemoved(self):
        _,code,worker,receipt,context = self.fixture(finalize=lambda *args:None)
        self.assertEqual(code,0,worker.result['errors'])
        with self.assertRaisesRegex(AssertionError,'caller finalization'):
            self.assert_imports(receipt,context)
        with self.assertRaisesRegex(preparation.InvalidPacket,'final isolation evidence'):
            qualification.receipt_outcome(receipt,context)

    def testPrivateOuterAndFinalizerFailuresNeverAttachPassingEvidence(self):
        for options in ({'private_failure':True},{'outer_failure':True},
                        {'finalize':preparation.InvalidPacket('injected finalizer failure')}):
            with self.subTest(options=options):
                _,code,worker,receipt,context = self.fixture(**options)
                self.addCleanup(lambda path=worker.scratch:shutil.rmtree(path,ignore_errors=True))
                self.assertEqual(code,2)
                self.assertFalse(worker.result['passed'])
                self.assertNotIn('isolation',receipt['execution_evidence'])
                with self.assertRaises(preparation.InvalidPacket):
                    qualification.receipt_outcome(receipt,context)

    def testEveryRehashedSemanticGapIsRefusedThroughActualReceiptCaller(self):
        def command(value, kind):
            return next(row for row in value['commands'] if row['argv'][5] == kind)
        def modify_output(row,change):
            value = json.loads(row['output'])
            change(value)
            row['output'] = json.dumps(value)
            row['output_sha256'] = preparation.digest(row['output'].encode())
        def append_mutation(value):
            from datetime import datetime,timedelta
            row = copy.deepcopy(value['commands'][-1])
            last_end = datetime.fromisoformat(row['finished_utc'])
            row.update(id=len(value['commands'])+1,argv=row['argv'][:5]+['rm','-f','unrelated-container'],
                       output='removed unrelated container',output_sha256=preparation.digest(b'removed unrelated container'),
                       exit_code=0,started_utc=(last_end+timedelta(microseconds=1)).isoformat(),
                       finished_utc=(last_end+timedelta(microseconds=2)).isoformat())
            value['commands'].append(row)
        def duplicate_absence(value):
            matches = [index for index,row in enumerate(value['commands']) if row['argv'][5:] ==
                       ['inspect','--format',isolation.INSPECT,value['sdk']] and row['exit_code'] != 0]
            value['commands'][matches[1]] = copy.deepcopy(value['commands'][matches[0]])
        mutations = (
            ('missing-image','wrapper',lambda v:v.pop('image_measurements')),
            ('image-os','wrapper',lambda v:modify_output(command(v,'image'),lambda x:x.update(Os='windows'))),
            ('image-architecture','wrapper',lambda v:modify_output(command(v,'image'),lambda x:x.update(Architecture='arm64'))),
            ('image-digest','wrapper',lambda v:modify_output(command(v,'image'),lambda x:x.update(RepoDigests=['wrong@sha256:'+'a'*64]))),
            ('image-id','wrapper',lambda v:modify_output(command(v,'inspect'),lambda x:x.update(Image='sha256:'+'f'*64))),
            ('missing-python-tools','wrapper',lambda v:v['owned_containers'].pop('e'*64)),
            ('create-options','wrapper',lambda v:command(v,'create')['argv'].insert(6,'--network=host')),
            ('copy-chain','wrapper',lambda v:command(v,'cp')['argv'].__setitem__(-1,'/substituted/tool')),
            ('missing-launch','wrapper',lambda v:v['commands'].__setitem__(slice(None),[x for x in v['commands'] if '_inner' not in x['argv']])),
            ('launch-options','wrapper',lambda v:next(x for x in v['commands'] if '_inner' in x['argv'])['argv'].append('--substituted')),
            ('altered-arguments','wrapper',lambda v:v['inner_command'].append('--substituted')),
            ('selected-hash','wrapper',lambda v:v['selected_input'].update(sha256='f'*64)),
            ('selected-path','wrapper',lambda v:v['selected_input'].update(path='/missing/inputs.json')),
            ('unrelated-mutation','wrapper',append_mutation),
            ('duplicate-command-id','wrapper',lambda v:v['commands'][1].update(id=v['commands'][0]['id'])),
            ('boolean-command-id','wrapper',lambda v:v['commands'][0].update(id=True)),
            ('boolean-cleanup-id','private',lambda v:v['cleanup'][0].update(id=True)),
            ('duplicate-command','wrapper',lambda v:v['commands'].__setitem__(1,copy.deepcopy(v['commands'][0]))),
            ('duplicate-cleanup-absence','wrapper',duplicate_absence),
            ('reordered-command','wrapper',lambda v:v['commands'].__setitem__(slice(0,2),list(reversed(v['commands'][:2])))),
            ('command-time-overlap','wrapper',lambda v:v['commands'][1].update(started_utc=v['commands'][0]['started_utc'])),
            ('missing-supervisor','private',lambda v:v.pop('supervisor')),
            ('missing-root','private',lambda v:v.pop('root')),
            ('missing-owned','private',lambda v:v.pop('owned_processes')),
            ('incomplete-root','private',lambda v:v['root'].pop('session')),
            ('missing-process-cleanup','private',lambda v:v['cleanup'].clear()),
            ('reordered-process-cleanup','private',lambda v:v['cleanup'].reverse()),
            ('duplicate-process-cleanup','private',lambda v:v['cleanup'].__setitem__(1,copy.deepcopy(v['cleanup'][0]))),
            ('unowned-process','private',lambda v:v['cleanup'][0]['before'].append({'pid':999,'pgid':999,'session':999,'start_ticks':1})),
            ('docker-hash','private',lambda v:v['proof']['executables']['docker'].update(sha256='f'*64)),
            ('docker-path','private',lambda v:v['proof']['executables']['docker'].update(path='/substituted/docker')),
            ('docker-client-version','private',lambda v:v['proof']['docker_version']['Client'].update(Version='0.0')),
            ('docker-server-version','private',lambda v:v['proof']['docker_version']['Server'].update(Version='0.0')),
            ('dotnet-hash','private',lambda v:v['proof']['executables']['dotnet'].update(sha256='f'*64)),
            ('dotnet-path','private',lambda v:v['proof']['executables']['dotnet'].update(path='/substituted/dotnet')),
            ('dotnet-version','private',lambda v:v['proof'].update(dotnet_version='0.0')),
            ('duplicate-outer-cleanup','wrapper',lambda v:v['cleanup'].__setitem__(1,copy.deepcopy(v['cleanup'][0]))),
            ('remote-controller','wrapper',lambda v:v.update(outer_host='tcp://remote:2376')),
        )
        # Rehash the physical reference and enclosing receipt: failures must be
        # semantic, not stale-hash detection. A fresh positive precedes each mutation.
        for name,record,mutate in mutations:
            with self.subTest(name=name):
                witness,code,worker,receipt,context = self.fixture()
                self.assertEqual(code,0,worker.result['errors'])
                self.assert_imports(receipt,context)
                witness.change_isolation(receipt,record,mutate)
                with self.assertRaises(preparation.InvalidPacket):
                    qualification.receipt_outcome(receipt,context)

    def testTiedTimestampsPreserveOrderedIdsAndDistinctAbsenceObservations(self):
        witness,code,_,receipt,context = self.fixture()
        self.assertEqual(code,0)
        def private_ties(value):
            for attempt in value['cleanup']:
                attempt['started_utc'] = attempt['finished_utc'] = '2026-10-14T00:00:31+00:00'
        witness.change_isolation(receipt,'private',private_ties)
        def wrapper_ties(value):
            launch = next(index for index,row in enumerate(value['commands']) if '_inner' in row['argv'])
            for row in value['commands'][:launch]:
                row['started_utc'] = row['finished_utc'] = value['started_utc']
            boundary = value['commands'][launch]['finished_utc']
            for row in [*value['commands'][launch+1:],*value['cleanup']]:
                row['started_utc'] = row['finished_utc'] = boundary
        witness.change_isolation(receipt,'wrapper',wrapper_ties)
        self.assert_imports(receipt,context)
        # Ties do not permit reuse of one literal absence observation.
        def duplicate(value):
            rows = [index for index,row in enumerate(value['commands']) if row['argv'][5:] ==
                    ['inspect','--format',isolation.INSPECT,value['sdk']] and row['exit_code'] != 0]
            value['commands'][rows[1]] = copy.deepcopy(value['commands'][rows[0]])
        witness.change_isolation(receipt,'wrapper',duplicate)
        with self.assertRaises(preparation.InvalidPacket):
            qualification.receipt_outcome(receipt,context)

    def testLiteralStartupBudgetIncludesStartAndUsesExactBounds(self):
        from datetime import datetime,timedelta
        for label,start_seconds,probe_seconds,passes in (
                ('exact-bounds',28,2,True),('overlong-start',31,1,False),
                ('overlong-total',29,1.1,False),('overlong-probe',0,2.1,False)):
            with self.subTest(label=label):
                witness,code,_,receipt,context = self.fixture()
                self.assertEqual(code,0)
                def retime(value):
                    commands = value['commands']
                    start_index = next(index for index,row in enumerate(commands) if row['argv'][5:] == ['start',value['daemon']])
                    launch_index = next(index for index,row in enumerate(commands) if '_inner' in row['argv'])
                    base = datetime.fromisoformat(value['started_utc'])-timedelta(minutes=2)
                    value['started_utc'] = (base-timedelta(seconds=1)).isoformat()
                    for row in commands[:start_index]:
                        row['started_utc'] = row['finished_utc'] = value['started_utc']
                    commands[start_index]['started_utc'] = base.isoformat()
                    probe_start = base+timedelta(seconds=start_seconds)
                    commands[start_index]['finished_utc'] = probe_start.isoformat()
                    commands[start_index+1]['started_utc'] = probe_start.isoformat()
                    end = (probe_start+timedelta(seconds=probe_seconds)).isoformat()
                    commands[start_index+1]['finished_utc'] = end
                    for row in commands[start_index+2:launch_index]:
                        row['started_utc'] = row['finished_utc'] = end
                witness.change_isolation(receipt,'wrapper',retime)
                if passes:
                    self.assert_imports(receipt,context)
                else:
                    with self.assertRaisesRegex(preparation.InvalidPacket,'startup|probes'):
                        qualification.receipt_outcome(receipt,context)

    def testEveryObservedRoleMustAgreeWithSafeCreationAndStaticSdkSettings(self):
        mutations = (
            ('daemon','NetworkMode','host'),('daemon','PidMode','host'),('daemon','Privileged',False),
            ('daemon','Ports',{'2375/tcp':[]}),('daemon','Mounts',[]),
            ('python-tools','NetworkMode','bridge'),('python-tools','PidMode','host'),
            ('python-tools','Privileged',True),('python-tools','Ports',{'80/tcp':[]}),
            ('python-tools','Mounts',[{'Destination':'/var/run/docker.sock','Type':'bind','Source':'/var/run/docker.sock','RW':True}]),
            ('sdk','NetworkMode','host'),('sdk','PidMode','host'),('sdk','Privileged',True),
            ('sdk','Ports',{'80/tcp':[]}),('sdk','Mounts',[]),
        )
        for role,key,replacement in mutations:
            with self.subTest(role=role,key=key):
                witness,_,_,receipt,context = self.fixture()
                def alter(value):
                    identity = next(identity for identity,row in value['owned_containers'].items() if row['role']==role)
                    row = next(row for row in value['commands'] if row['argv'][5:] ==
                               ['inspect','--format',isolation.INSPECT,identity] and row['exit_code']==0)
                    observed = json.loads(row['output'])
                    observed[key] = replacement
                    row['output'] = json.dumps(observed)
                    row['output_sha256'] = preparation.digest(row['output'].encode())
                witness.change_isolation(receipt,'wrapper',alter)
                with self.assertRaises(preparation.InvalidPacket):
                    qualification.receipt_outcome(receipt,context)
        witness,_,_,receipt,context = self.fixture()
        def altered_static_mount(value):
            row = next(row for row in value['commands'] if row['argv'][5:] ==
                       ['inspect','--format',isolation.INSPECT,value['sdk']] and row['exit_code']==0)
            observed = json.loads(row['output'])
            observed['Mounts'][0]['Source'] = '/contradictory/volume/source'
            row['output'] = json.dumps(observed)
            row['output_sha256'] = preparation.digest(row['output'].encode())
        witness.change_isolation(receipt,'wrapper',altered_static_mount)
        with self.assertRaisesRegex(preparation.InvalidPacket,'static role'):
            qualification.receipt_outcome(receipt,context)

    def testCoordinatedToolAndNamespaceProofSubstitutionsAreRejected(self):
        mutations = (
            ('docker-path',lambda p:p['executables']['docker'].update(path='/substituted/docker')),
            ('docker-hash',lambda p:p['executables']['docker'].update(sha256='f'*64)),
            ('dotnet-path',lambda p:p['executables']['dotnet'].update(path='/substituted/dotnet')),
            ('dotnet-hash',lambda p:p['executables']['dotnet'].update(sha256='f'*64)),
            ('missing-mount-namespace',lambda p:p['namespaces'].pop('mnt')),
            ('missing-daemon-mount-namespace',lambda p:p['daemon_namespaces'].pop('mnt')),
            ('invalid-namespace',lambda p:p['namespaces'].update(mnt='mnt:unmeasured')),
            ('invalid-daemon-namespace',lambda p:p['daemon_namespaces'].update(mnt=None)),
            ('missing-glibc',lambda p:p.update(glibc=None)),
        )
        for label,mutate in mutations:
            with self.subTest(label=label):
                witness,_,_,receipt,context = self.fixture()
                witness.change_isolation(receipt,'private',lambda value:mutate(value['proof']))
                def coordinated(value):
                    mutate(value['toolchain_proof'])
                    row = next(row for row in value['commands'] if '_proof' in row['argv'])
                    row['output'] = json.dumps(value['toolchain_proof'])
                    row['output_sha256'] = preparation.digest(row['output'].encode())
                witness.change_isolation(receipt,'wrapper',coordinated)
                with self.assertRaises(preparation.InvalidPacket):
                    qualification.receipt_outcome(receipt,context)

    def private_identity_mutations(self):
        def root_session(value):
            original = copy.deepcopy(value['root'])
            replacement = dict(original,pgid=original['pid']+1,session=original['pid']+1)
            value['root'] = replacement
            inventories = [value['owned_processes']]
            inventories += [attempt[name] for attempt in value['cleanup']
                            for name in ('owned_processes','before','remaining')]
            for inventory in inventories:
                for index,row in enumerate(inventory):
                    if row == original:
                        inventory[index] = copy.deepcopy(replacement)
        def alias(value,protected,location):
            original = value['supervisor'] if protected=='supervisor' else value['proof']['process']
            row = {key:original[key] for key in ('pid','start_ticks','pgid','session')}
            row['start_ticks'] += 1
            value['owned_processes'].append(copy.deepcopy(row))
            for attempt in value['cleanup']:
                attempt['owned_processes'].append(copy.deepcopy(row))
            if location != 'owned':
                value['cleanup'][0][location].append(copy.deepcopy(row))
        mutations = [('root-session',root_session),('resurrection',lambda value:value['cleanup'][1]['before'].append(copy.deepcopy(value['root'])))]
        mutations += [(protected+'-'+location,lambda value,p=protected,l=location:alias(value,p,l))
                      for protected in ('supervisor','dockerd') for location in ('owned','before','remaining')]
        return mutations

    def testNewSessionProtectedPidAliasesAndResurrectedProcessAreRefused(self):
        for label,mutate in self.private_identity_mutations():
            with self.subTest(label=label):
                witness,_,_,receipt,context = self.fixture()
                witness.change_isolation(receipt,'private',mutate)
                with self.assertRaises(preparation.InvalidPacket):
                    qualification.receipt_outcome(receipt,context)

    def testCoherentIdentityControlsDetectSessionOmissionAndDictionaryPidRegression(self):
        original = isolation_evidence.validate
        class RegressGuard(ast.NodeTransformer):
            def __init__(self,kind):
                self.kind,self.changed = kind,0
            def visit_Expr(self,node):
                call = node.value
                if (self.kind=='session' and isinstance(call,ast.Call) and isinstance(call.func,ast.Name)
                        and call.func.id=='require' and len(call.args)>1 and isinstance(call.args[1],ast.Constant)
                        and call.args[1].value=='private root does not own its new session'):
                    self.changed += 1
                    return None
                return self.generic_visit(node)
            def visit_Compare(self,node):
                if (self.kind=='protected' and isinstance(node.left,ast.Subscript)
                        and isinstance(node.left.value,ast.Name) and node.left.value.id=='row'
                        and isinstance(node.left.slice,ast.Constant) and node.left.slice.value=='pid'
                        and len(node.ops)==1 and isinstance(node.ops[0],ast.NotIn)
                        and isinstance(node.comparators[0],ast.Name) and node.comparators[0].id=='protected'):
                    self.changed += 1
                    return ast.parse("row not in (private['supervisor'],daemon_process)",mode='eval').body
                return self.generic_visit(node)
        mutations = dict(self.private_identity_mutations())
        for kind,labels,expected_changes in (
                ('session',('root-session',),1),
                ('protected',('supervisor-owned','supervisor-before','dockerd-owned','dockerd-before'),2)):
            regressor = RegressGuard(kind)
            tree = ast.fix_missing_locations(regressor.visit(ast.parse(inspect.getsource(original))))
            self.assertEqual(regressor.changed,expected_changes)
            namespace = {}
            exec(compile(tree,original.__code__.co_filename,'exec'),original.__globals__,namespace)
            for label in labels:
                with self.subTest(kind=kind,label=label):
                    witness,_,_,receipt,context = self.fixture()
                    witness.change_isolation(receipt,'private',mutations[label])
                    with self.assertRaises(preparation.InvalidPacket):
                        qualification.receipt_outcome(receipt,context)
                    # With the sole relevant guard regressed, the coherent
                    # evidence imports: the refusal control would fail.
                    with mock.patch.object(isolation_evidence,'validate',namespace['validate']):
                        self.assert_imports(receipt,context)

    def testCanonicalEngineBindSourcePassesButRequestedSourceSubstitutionRefuses(self):
        witness,code,worker,receipt,context = self.fixture(mapped_bind=True)
        self.assertEqual(code,0,worker.result['errors'])
        self.assert_imports(receipt,context)
        def substitute(value):
            row = next(row for row in value['commands'] if row['argv'][5]=='create' and isolation.SDK_IMAGE in row['argv'])
            index = next(index for index,arg in enumerate(row['argv']) if arg.startswith('type=bind,src='))
            workspace = next(mount['Destination'] for mount in value['runner_proof']['Mounts'] if mount['Type']=='bind')
            row['argv'][index] = 'type=bind,src=/substituted/workspace,dst='+workspace
        witness.change_isolation(receipt,'wrapper',substitute)
        with self.assertRaises(preparation.InvalidPacket):
            qualification.receipt_outcome(receipt,context)

    def testCoordinatedMalformedCanonicalEngineSourcesAreRefused(self):
        for source in ('/engine/source/../workspace','//engine/workspace','/engine/workspace/.',
                       '/engine//workspace','/engine/workspace/','/','/engine/workspace.sock',
                       '/engine/workspace.socket','relative/workspace'):
            with self.subTest(source=source):
                witness,code,_,receipt,context = self.fixture(mapped_bind=True)
                self.assertEqual(code,0)
                def substitute(value):
                    for mount in value['runner_proof']['Mounts']:
                        if mount['Type']=='bind':mount['Source'] = source
                    for row in value['commands']:
                        if row['argv'][5:] == ['inspect','--format',isolation.INSPECT,value['sdk']] and row['exit_code']==0:
                            observed = json.loads(row['output'])
                            for mount in observed['Mounts']:
                                if mount['Type']=='bind':mount['Source'] = source
                            row['output'] = json.dumps(observed)
                            row['output_sha256'] = preparation.digest(row['output'].encode())
                witness.change_isolation(receipt,'wrapper',substitute)
                with self.assertRaises(preparation.InvalidPacket):
                    qualification.receipt_outcome(receipt,context)

    def testRealLateDiscoveryExtendsOwnershipAndCleansFreshRegisteredProcess(self):
        witness,code,worker,receipt,context = self.fixture(real_inner=True,late_process=True)
        self.assertEqual(code,0,worker.result['errors'])
        private = json.loads(storage.preserved_bytes(receipt['execution_evidence']['isolation']['private'], storage.receipt_context(receipt, context["source_binding"], context["inputs"]["value"])))
        first,second = private['cleanup']
        self.assertEqual(first['remaining'],[])
        self.assertTrue(second['before'])
        key = lambda row:(row['pid'],row['start_ticks'])
        first_owned = {key(row) for row in first['owned_processes']}
        second_owned = {key(row) for row in second['owned_processes']}
        self.assertLess(first_owned,second_owned)
        self.assertTrue(all(key(row) not in first_owned for row in second['before']))
        self.assertEqual(second['remaining'],[])
        self.assertTrue(all(preparation.process_state(row['pid']) is None for row in second['before']))
        self.assert_imports(receipt,context)
        def resurrect(value):
            value['cleanup'][1]['before'].append(copy.deepcopy(value['root']))
        witness.change_isolation(receipt,'private',resurrect)
        with self.assertRaisesRegex(preparation.InvalidPacket,'resurrected'):
            qualification.receipt_outcome(receipt,context)

    def testCleanupOwnershipSnapshotsMustBeCompleteAndMonotonic(self):
        mutations = (
            lambda value:value['cleanup'][0].pop('owned_processes'),
            lambda value:value['cleanup'][0]['owned_processes'].clear(),
            lambda value:value['cleanup'][1]['owned_processes'].clear(),
            lambda value:value['cleanup'][1]['before'].append({'pid':999,'start_ticks':1,'pgid':999,'session':999}),
        )
        for mutate in mutations:
            witness,_,_,receipt,context = self.fixture()
            witness.change_isolation(receipt,'private',mutate)
            with self.assertRaises(preparation.InvalidPacket):
                qualification.receipt_outcome(receipt,context)

    def testActualInnerSupervisorProcessesSupplyFinalizedImportEvidence(self):
        _,code,worker,receipt,context = self.fixture(real_inner=True)
        self.assertEqual(code,0,worker.result['errors'])
        private = json.loads(storage.preserved_bytes(receipt['execution_evidence']['isolation']['private'], storage.receipt_context(receipt, context["source_binding"], context["inputs"]["value"])))
        self.assertEqual(private['supervisor']['pid'],os.getpid())
        self.assertGreaterEqual(len(private['owned_processes']),2)
        self.assertTrue(private['cleanup'][0]['before'])
        self.assertEqual(private['cleanup'][1]['before'],[])
        self.assert_imports(receipt,context)

    def testActualProducerRootAndTimedCleanupOmissionsBreakThePositiveControl(self):
        original = isolation._inner
        class OmitEvidence(ast.NodeTransformer):
            def __init__(self,kind):
                self.kind = kind
            def visit_Assign(self,node):
                if self.kind=='root' and any(isinstance(target,ast.Subscript) and isinstance(target.slice,ast.Constant)
                                           and target.slice.value=='root' for target in node.targets):
                    return None
                return self.generic_visit(node)
            def visit_Call(self,node):
                if self.kind=='timed-cleanup' and isinstance(node.func,ast.Name) and node.func.id=='dict':
                    node.keywords = [item for item in node.keywords if item.arg not in ('id','started_utc','finished_utc')]
                return self.generic_visit(node)
        for kind in ('root','timed-cleanup'):
            with self.subTest(kind=kind):
                tree = ast.fix_missing_locations(OmitEvidence(kind).visit(ast.parse(inspect.getsource(original))))
                namespace = {}
                exec(compile(tree,original.__code__.co_filename,'exec'),original.__globals__,namespace)
                with mock.patch.object(isolation,'_inner',namespace['_inner']):
                    _,code,worker,receipt,context = self.fixture(real_inner=True)
                self.assertEqual(code,2)
                self.assertFalse(worker.result['passed'])
                with self.assertRaisesRegex(AssertionError,'caller finalization'):
                    self.assert_imports(receipt,context)

    def testPhysicalSelectedInputMutationIsRefusedEvenWhenSelectionValueIsUnchanged(self):
        _,_,_,receipt,context = self.fixture()
        wrapper = json.loads(storage.preserved_bytes(receipt['execution_evidence']['isolation']['wrapper'], storage.receipt_context(receipt, context["source_binding"], context["inputs"]["value"])))
        selected = Path(wrapper['selected_input']['path'])
        selected.write_bytes(selected.read_bytes()+b'\n')
        with self.assertRaisesRegex(preparation.InvalidPacket,'physical input'):
            qualification.receipt_outcome(receipt,context)


class LocalStartupTests(unittest.TestCase):
    def testUnsafeWorkspaceMountsRefuseBeforeAnyDockerProvisioning(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory) / 'repo'
            root.mkdir()
            for index, workspace in enumerate(('/', directory, str(root) + '/.', str(root) + '/',
                                               str(root) + '/a/..', '/proc', '/dev', 'relative', '/tmp/repo,socket')):
                output = root.parent / ('refusal-' + str(index))
                with self.subTest(workspace=workspace), mock.patch.object(isolation, 'ROOT', root), \
                        mock.patch.object(preparation, 'git', return_value=b''), \
                        mock.patch.object(isolation.IsolatedRunner, 'docker') as docker:
                    with self.assertRaises(preparation.InvalidPacket):
                        isolation.IsolatedRunner(output, workspace=workspace, outer_host='unix:///outside/docker.sock')
                    docker.assert_not_called()
                    self.assertFalse(json.loads((output / 'isolation.json').read_text())['passed'])

    def testWorkspaceCannotExposeTheSelectedSocketOrAnAlias(self):
        import socket
        with tempfile.TemporaryDirectory() as directory:
            parent = Path(directory)
            root = parent / 'repo'
            root.mkdir()
            with socket.socket(socket.AF_UNIX) as listener:
                selected = parent / 'outer.sock'
                listener.bind(str(selected))
                alias = root / 'alias.sock'
                os.link(selected, alias)
                for endpoint in (selected, alias):
                    with self.subTest(endpoint=endpoint), mock.patch.object(isolation, 'ROOT', root), \
                            mock.patch.object(isolation.IsolatedRunner, 'docker') as docker:
                        with self.assertRaisesRegex(preparation.InvalidPacket, 'outer Docker socket'):
                            isolation.IsolatedRunner(parent / ('refusal-' + endpoint.name), workspace=root,
                                                     outer_host='unix://' + str(endpoint))
                        docker.assert_not_called()
                alias.unlink()
                link = parent / 'linked.sock'
                selected.rename(alias)
                link.symlink_to(alias)
                with self.assertRaisesRegex(preparation.InvalidPacket, 'outer Docker socket'):
                    isolation.validate_workspace(root, root, 'unix://' + str(link))

    def testSupportedWorkspaceAndReadOnlySocketOutsideItRemainUsable(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            self.assertEqual(isolation.validate_workspace(root, root, 'unix:///outside/docker.sock'), root)
            with mock.patch.object(preparation, 'git', return_value=str(root).encode()):
                self.assertEqual(isolation.validate_workspace(root, root / 'references/EventStore', 'unix:///outside/docker.sock'), root)

    def testUnreadableWorkspaceSubtreeRefusesRatherThanHidingSocketAliases(self):
        import socket
        with tempfile.TemporaryDirectory() as directory:
            parent = Path(directory)
            root = parent / 'repo'
            root.mkdir()
            with socket.socket(socket.AF_UNIX) as listener:
                selected = parent / 'outer.sock'
                listener.bind(str(selected))
                def unavailable(path, *, onerror):
                    onerror(PermissionError('unreadable subtree'))
                    return iter(())
                with mock.patch.object(isolation.os, 'walk', side_effect=unavailable), \
                        mock.patch.object(isolation, 'ROOT', root), \
                        mock.patch.object(isolation.IsolatedRunner, 'docker') as docker:
                    with self.assertRaisesRegex(preparation.InvalidPacket, 'workspace contents unavailable'):
                        isolation.IsolatedRunner(parent / 'refusal', workspace=root, outer_host='unix://' + str(selected))
                    docker.assert_not_called()

    def testEarlyConfigurationAndInitialSaveFailuresCleanOwnedScratch(self):
        for failure in ('config', 'save'):
            with self.subTest(failure=failure), tempfile.TemporaryDirectory() as directory:
                root = Path(directory)
                scratch = root / 'owned-scratch'
                scratch.mkdir()
                original_mkdir = Path.mkdir
                original_save = isolation.IsolatedRunner.save
                calls = []
                def mkdir(path, *args, **kwargs):
                    if failure == 'config' and path.name == 'docker-config':
                        raise OSError('injected config failure')
                    return original_mkdir(path, *args, **kwargs)
                def save(worker):
                    calls.append(True)
                    if failure == 'save' and len(calls) == 1:
                        raise OSError('injected save failure')
                    return original_save(worker)
                with mock.patch.object(isolation.tempfile, 'mkdtemp', return_value=str(scratch)), \
                        mock.patch.object(Path, 'mkdir', mkdir), \
                        mock.patch.object(isolation.IsolatedRunner, 'save', save), \
                        mock.patch.object(isolation.IsolatedRunner, 'docker') as docker:
                    with self.assertRaisesRegex(OSError, 'injected'):
                        isolation.IsolatedRunner(root / 'evidence', workspace=root, outer_host='unix:///outside/docker.sock')
                    docker.assert_not_called()
                self.assertFalse(scratch.exists())
                retained = json.loads((root / 'evidence/isolation.json').read_text())
                self.assertFalse(retained['passed'])
                self.assertEqual(len(retained['cleanup']), 2)

    def testRemoteEndpointsRefuseBeforeProvisioningAndDoNotRetainCredentials(self):
        credential = uuid.uuid4().hex
        for host in (f'tcp://user:{credential}@remote:2376',f'ssh://user:{credential}@remote','unix://remote/socket',
                     f'https://user:{credential}@remote',f'unix:///socket?token={credential}',f'unix:///socket#{credential}'):
            with self.subTest(host=host), tempfile.TemporaryDirectory() as directory:
                output = Path(directory)/'evidence'
                with mock.patch.object(isolation.IsolatedRunner,'docker') as docker:
                    with self.assertRaisesRegex(preparation.InvalidPacket,'local Unix'):
                        isolation.IsolatedRunner(output,workspace=Path(directory),outer_host=host)
                docker.assert_not_called()
                raw = (output/'isolation.json').read_text()
                self.assertNotIn(credential,raw)
                retained = json.loads(raw)
                self.assertFalse(retained['passed'])
                self.assertEqual(len(retained['cleanup']),2)

    def testHungFailedAndLateSuccessfulProbesShareOneBoundedMonotonicDeadline(self):
        for mode in ('hung','failed','late-success'):
            with self.subTest(mode=mode),tempfile.TemporaryDirectory() as directory:
                root = Path(directory)
                repository = root/'repo';repository.mkdir()
                with mock.patch.object(isolation,'ROOT',repository):
                    worker = isolation.IsolatedRunner(root/'evidence',workspace=repository,outer_host='unix:///outer')
                self.addCleanup(lambda path=worker.scratch:shutil.rmtree(path,ignore_errors=True))
                clock = [0.0]
                calls = []
                def docker(args,**kwargs):
                    calls.append((args,kwargs))
                    if args[0] == 'info':return 0,b'outer'
                    if args[0] == 'version':return 0,b'{}'
                    if args[:2] == ['image','inspect']:
                        return 0,json.dumps({'Id':'sha256:'+'a'*64,'RepoDigests':[args[-1]],'Os':'linux','Architecture':'amd64'}).encode()
                    if args[0] == 'exec':
                        self.assertLessEqual(kwargs['timeout'],isolation.PROBE_SECONDS)
                        clock[0] += isolation.STARTUP_SECONDS+1 if mode=='late-success' else kwargs['timeout']
                        return (0,b'private') if mode=='late-success' else (124 if mode=='hung' else 1,b'failure')
                    return 0,b''
                with mock.patch.object(worker,'docker',side_effect=docker), \
                        mock.patch.object(worker,'create',return_value='owned-daemon'), \
                        mock.patch.object(isolation.time,'monotonic',side_effect=lambda:clock[0]), \
                        mock.patch.object(isolation.time,'sleep',side_effect=lambda value:clock.__setitem__(0,clock[0]+value)):
                    self.assertEqual(worker._run(['python3','verification.py']),2)
                self.assertIn('private daemon startup timeout',worker.result['errors'])
                self.assertEqual(len(worker.result['cleanup']),2)
                self.assertFalse(worker.result['passed'])
                probes = [row for row in calls if row[0][0]=='exec']
                self.assertTrue(probes)
                if mode != 'late-success':self.assertLessEqual(clock[0],isolation.STARTUP_SECONDS)


if __name__ == '__main__':
    unittest.main()

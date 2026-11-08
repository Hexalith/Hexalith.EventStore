"""Synthetic identity/custody fault tests; no Docker daemon or live proof is used."""
import copy
import importlib.util
import json
import os
from pathlib import Path
import shutil
import socket
import sys
import signal
import subprocess
import hashlib
import time
import tempfile
import types
import unittest
from unittest import mock

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from dapr_broker_qualification import digest, validate_profile, write_json
from dapr_runtime_qualification import CANDIDATE_PROFILE, ExclusiveDocker, CONTROL_BINARIES, CustodyError, require_candidate, run_process_group, child_docker, cleanup_child_resources, retain_compatibility_witnesses

ROOT = Path(__file__).resolve().parents[2]


class RuntimeIdentityTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.profile = json.loads((ROOT / CANDIDATE_PROFILE).read_text())
        files = set(self.profile['inputs']) | {row['manifest']['path'] for row in self.profile['images'].values()}
        for name in files:
            destination = self.root / name
            destination.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(ROOT / name, destination)

    def testCandidatePositiveControl(self):
        validate_profile(self.profile, self.root)

    def testOriginalLockBytesRemainUnchanged(self):
        self.assertEqual('326a24228313b4f76428f3091bfa449ba00c6a37fc4bdca55fb78409ab433238', digest(ROOT / 'deploy/dapr/qualification/runtime-lock.json'))

    def testResealedWrongExecutableIsRejected(self):
        self.profile['runtime']['binarySha256'] = '0' * 64
        with self.assertRaisesRegex(ValueError, 'official identity'): validate_profile(self.profile, self.root)

    def testUnapprovedTupleIsRejected(self):
        self.profile['runtime']['cliVersion'] = '1.19.0'
        with self.assertRaisesRegex(ValueError, 'requalification'): validate_profile(self.profile, self.root)

    def testSourceCommitCorruptionIsRejected(self):
        self.profile['runtime']['sources'][1]['commit'] = '0' * 40
        with self.assertRaisesRegex(ValueError, 'source identity'): validate_profile(self.profile, self.root)

    def testChartArchiveCorruptionIsRejected(self):
        (self.root / self.profile['chart']['path']).write_bytes(b'corrupt chart')
        with self.assertRaisesRegex(ValueError, 'chart: archive'): validate_profile(self.profile, self.root)

    def testChartAppVersionCorruptionIsRejected(self):
        self.profile['chart']['appVersion'] = '1.18.7'
        with self.assertRaisesRegex(ValueError, 'official identity'): validate_profile(self.profile, self.root)

    def testArm64ChildCorruptionIsRejected(self):
        child = self.profile['images']['scheduler']['children']['arm64']
        (self.root / child['path']).write_text('{}')
        with self.assertRaisesRegex(ValueError, 'child digest'): validate_profile(self.profile, self.root)

    def testAmd64ChildBindingCorruptionIsRejected(self):
        self.profile['images']['daprd']['children']['amd64']['digest'] = 'sha256:' + '0' * 64
        with self.assertRaisesRegex(ValueError, 'child digest'): validate_profile(self.profile, self.root)

    def testEmbeddedComponentSourceCorruptionIsRejected(self):
        path = next(name for name in self.profile['inputs'] if name.endswith('/1.19.1/dapr-dapr-go.mod'))
        (self.root / path).write_text('github.com/dapr/components-contrib v1.18.6')
        with self.assertRaisesRegex(ValueError, 'embedded components'): validate_profile(self.profile, self.root)

    def testReleaseChecksumCorruptionIsRejected(self):
        path = self.profile['runtime']['archives']['daprd']['checksumPath']
        (self.root / path).write_text('0' * 64)
        with self.assertRaisesRegex(ValueError, 'release checksum'): validate_profile(self.profile, self.root)


class CandidatePacketTests(unittest.TestCase):
    def setUp(self):
        from test_dapr_broker_qualification import BrokerQualificationTests
        self.fixture = BrokerQualificationTests(); self.fixture.setUp(); self.addCleanup(self.fixture.tearDown)
        f = self.fixture
        profile = json.loads((ROOT / CANDIDATE_PROFILE).read_text())
        files = set(profile['inputs']) | {CANDIDATE_PROFILE} | {row['manifest']['path'] for row in profile['images'].values()}
        for name in files:
            destination = f.directory / 'inputs' / name
            destination.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(ROOT / name, destination)
        for name in (path.removeprefix('tools/') for path in profile['inputs'] if path.startswith('tools/') and path.endswith('.py')):
            destination = f.directory / 'execution-tools' / name; destination.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(ROOT / 'tools' / name, destination)
        for path in f.directory.glob('*/metadata/*.json'):
            data = json.loads(path.read_text()); data['runtimeVersion'] = '1.19.1'; write_json(path, data)
        for command in f.commands:
            if command.get('argv', [''])[0] == 'HTTP' and command['argv'][-1].endswith('/v1.0/metadata'):
                data = json.loads(command['output']); data['runtimeVersion'] = '1.19.1'; command['output'] = json.dumps(data)
        custody = {'exclusive': True, 'endpoint': 'unix:///synthetic.sock', 'daemonId': 'synthetic-exclusive-daemon', 'workerId': 'synthetic-worker', 'socketIdentity': [1, 123]}
        template = {'startedUtc': '2026-11-08T00:00:00+00:00', 'endedUtc': '2026-11-08T00:00:00+00:00', 'exitCode': 0}
        def daemon():
            return {**template, 'argv': ['docker', '--host', custody['endpoint'], 'info', '--format', '{{.ID}}'], 'output': custody['daemonId'], 'socketIdentity': custody['socketIdentity']}
        original = f.commands
        f.commands = [daemon(), {**template, 'argv': ['WORKER', 'exclusive-allocation'], 'output': json.dumps(custody, sort_keys=True), 'daemonObservation': 0, 'leaseLocked': True}]
        offsets = {}
        for index, command in enumerate(original):
            if command['argv'][0] == 'docker':
                f.commands.append(daemon())
                command.update(dockerCustody=custody, executedArgv=['docker', '--host', custody['endpoint'], *command['argv'][1:]], daemonObservation=len(f.commands) - 1, workerAdmission=1)
            offsets[index] = len(f.commands)
            f.commands.append(command)
        def reindex(data):
            if isinstance(data, dict):
                for key, value in data.items():
                    if key in ('commandReceipt', 'restartReceipt', 'quotaReceipt'): data[key] = offsets[value]
                    elif key == 'offsetReceipts': data[key] = [offsets[item] for item in value]
                    else: reindex(value)
            elif isinstance(data, list):
                for value in data: reindex(value)
        for path in f.directory.rglob('*.json'):
            if path.is_relative_to(f.directory / 'inputs'): continue
            data = json.loads(path.read_text()); reindex(data); write_json(path, data)
        def receipt(argv, output='', **fields):
            if argv[0] == 'docker': f.commands.append(daemon())
            row = {**template, 'argv': argv, 'output': output, **fields}
            if argv[0] == 'docker':
                row.update(dockerCustody=custody, executedArgv=['docker', '--host', custody['endpoint'], *argv[1:]], daemonObservation=len(f.commands) - 1, workerAdmission=1)
            f.commands.append(row)
            return len(f.commands) - 1
        inventories = {}
        baseline = {kind + ':' + kind + '-synthetic': hashlib.sha256(kind.encode()).hexdigest() for kind in ('container', 'network', 'volume')}
        for phase in ('before', 'after'):
            observations = {}
            for kind, listing in (('container', ['ps', '-aq', '--no-trunc']), ('network', ['network', 'ls', '-q', '--no-trunc']), ('volume', ['volume', 'ls', '-q'])):
                name = kind + '-synthetic'
                observations[kind] = {'listingReceipt': receipt(['docker', *listing], name),
                    'inspectionReceipt': receipt(['docker', 'inspect', '--type', kind, name], json.dumps({kind + ':' + name: baseline[kind + ':' + name]}))}
            inventories[phase] = receipt(['WORKSPACE', 'inventory', phase], json.dumps(baseline, sort_keys=True), resourceObservations=observations)
            write_json(f.directory / ('workspace-' + phase + '.json'), baseline)
        controls = {}
        for name in ('placement', 'scheduler'):
            binary = '/synthetic/' + name
            identity = ('a' if name == 'placement' else 'b') * 64
            controls[name] = {'image': profile['images'][name]['reference'], 'binarySha256': CONTROL_BINARIES[name],
                'creationReceipt': receipt(['docker', 'create', profile['images'][name]['reference'], './' + name], identity),
                'extractionReceipt': receipt(['docker', 'cp', identity + ':/' + name, binary]),
                'hashReceipt': receipt(['SHA256', binary], CONTROL_BINARIES[name]),
                'launchReceipt': receipt([binary, '--port', '12345'], event='native-launch', processId=1234)}
        write_json(f.directory / 'commands.json', f.commands)
        write_json(f.directory / 'execution-identities.json', {'stage': 'local-docker-first-stage', 'targetProof': False, 'preflightPassed': True,
            'daprdSha256': profile['runtime']['binarySha256'], 'cliSha256': profile['runtime']['cliSha256'],
            'dockerCustody': custody, 'controlPlane': controls, 'workspaceInventories': inventories})
        write_json(f.directory / 'cleanup.json', {'ownedResourcesRemaining': [], 'unrelatedContainersUnchanged': True, 'complete': True})
        f.packet.update(schemaVersion=2, profile=CANDIDATE_PROFILE); f.seal()

    def testSelectedCandidatePacketPositiveControl(self):
        from dapr_broker_qualification import validate_packet
        self.assertEqual('PASS', validate_packet(self.fixture.directory)['technicalQualification'])

    def testQualificationUploadsPreserveHiddenInputsForIndependentValidation(self):
        import re
        from dapr_broker_qualification import validate_packet
        for workflow in ('dapr-broker-qualification.yml', 'dapr-runtime-qualification.yml'):
            with self.subTest(workflow=workflow), tempfile.TemporaryDirectory() as directory:
                source = (ROOT / '.github/workflows' / workflow).read_text()
                upload = source.split('- uses: actions/upload-artifact@', 1)[1]
                paths = re.findall(r'^\s+path:\s*(.+)$', upload, re.M)
                self.assertEqual(['${{ env.QUALIFICATION_OUTPUT }}'], paths)
                include_hidden = bool(re.search(r'^\s+include-hidden-files:\s*true\s*$', upload, re.M))
                destination = Path(directory) / 'downloaded-artifact'
                # Model the action's hidden-file selection, then use the actual
                # independent validator on the transported bytes, without resealing.
                ignore = None if include_hidden else shutil.ignore_patterns('.*')
                shutil.copytree(self.fixture.directory, destination, ignore=ignore)
                self.assertEqual('PASS', validate_packet(destination)['integrity'])
                hidden = destination / 'inputs/.github/workflows/dapr-broker-qualification.yml'
                self.assertTrue(hidden.is_file())
                hidden.unlink()
                with self.assertRaisesRegex(ValueError, 'missing retained file'):
                    validate_packet(destination)

    def testMissingWorkerCustodyCannotPass(self):
        from dapr_broker_qualification import validate_packet
        f = self.fixture
        f.change('execution-identities.json', lambda data: data.pop('dockerCustody'))
        with self.assertRaisesRegex(ValueError, 'exclusive daemon custody'): validate_packet(f.directory)

    def testResealedCaptureToolCorruptionIsRejected(self):
        from dapr_broker_qualification import validate_packet
        f = self.fixture
        (f.directory / 'execution-tools/qualify-dapr-brokers.py').write_text('substituted source')
        f.seal()
        with self.assertRaisesRegex(ValueError, 'capture source binding'): validate_packet(f.directory)

    def testIncompleteCleanupCannotPass(self):
        from dapr_broker_qualification import validate_packet
        f = self.fixture; f.change('cleanup.json', lambda data: data.update(complete=False))
        with self.assertRaisesRegex(ValueError, 'incomplete baseline'): validate_packet(f.directory)

    def testResealedSchemaDowngradeCannotBypassCustody(self):
        from dapr_broker_qualification import validate_packet
        f = self.fixture
        f.packet['schemaVersion'] = 1
        f.change('execution-identities.json', lambda data: data.pop('dockerCustody'))
        f.change('cleanup.json', lambda data: data.update(complete=False))
        with self.assertRaisesRegex(ValueError, 'require schema 2'): validate_packet(f.directory)

    def testOmittedControlPlaneIdentitiesAreRejected(self):
        from dapr_broker_qualification import validate_packet
        f = self.fixture
        f.change('execution-identities.json', lambda data: data.pop('controlPlane'))
        with self.assertRaisesRegex(ValueError, 'placement and scheduler'): validate_packet(f.directory)

    def testUnrelatedCustodyIsRejected(self):
        from dapr_broker_qualification import validate_packet
        for field, wrong in [('endpoint', 'unix:///unrelated.sock'), ('daemonId', 'other-daemon'), ('workerId', 'other-worker'), ('socketIdentity', [2, 456])]:
            with self.subTest(field=field):
                path = self.fixture.directory / 'execution-identities.json'
                original = path.read_bytes()
                self.fixture.change('execution-identities.json', lambda data: data['dockerCustody'].update({field: wrong}))
                with self.assertRaisesRegex(ValueError, 'custody'): validate_packet(self.fixture.directory)
                path.write_bytes(original); self.fixture.seal()

    def testEveryDockerOperationRequiresObservedCustody(self):
        from dapr_broker_qualification import validate_packet
        f = self.fixture
        command = next(row for row in f.commands if row.get('workerAdmission') == 1)
        command.pop('daemonObservation')
        write_json(f.directory / 'commands.json', f.commands); f.seal()
        with self.assertRaisesRegex(ValueError, 'observed daemon binding'): validate_packet(f.directory)

    def testBothRetainedSourcesAndLockSubstitutionAreRejected(self):
        from dapr_broker_qualification import validate_packet
        f = self.fixture
        name = 'dapr_runtime_qualification.py'
        for target in ('execution-tools/' + name, 'inputs/tools/' + name):
            (f.directory / target).write_text('substituted code')
        lock = f.directory / 'inputs' / CANDIDATE_PROFILE
        data = json.loads(lock.read_text()); data['inputs']['tools/' + name] = digest(f.directory / 'execution-tools' / name)
        write_json(lock, data); f.seal()
        with self.assertRaisesRegex(ValueError, 'trusted owning checkout'): validate_packet(f.directory)

    def testEntireRetainedInputInventoryCannotBeDroppedOrResealed(self):
        from dapr_broker_qualification import validate_packet
        f = self.fixture
        lock = f.directory / 'inputs' / CANDIDATE_PROFILE
        original = lock.read_bytes()
        for name in ('tools/dapr_broker_probe.py', 'tools/dapr_qualification_tcp_proxy.py', 'tools/qualification-docker.py'):
            for mode in ('drop', 'substitute'):
                with self.subTest(name=name, mode=mode):
                    data = json.loads(original)
                    if mode == 'drop': data['inputs'].pop(name)
                    else:
                        (f.directory / 'inputs' / name).write_text('synthetic substituted helper')
                        data['inputs'][name] = digest(f.directory / 'inputs' / name)
                    write_json(lock, data); f.seal()
                    with self.assertRaisesRegex(ValueError, 'trusted owning checkout'): validate_packet(f.directory)
                    shutil.copyfile(ROOT / name, f.directory / 'inputs' / name)
        lock.write_bytes(original); f.seal()

    def testSnapshotDeletionCannotExploitSubsetOrEmptyInventories(self):
        from dapr_broker_qualification import validate_packet
        f = self.fixture
        for phase in ('before', 'after'):
            path = f.directory / ('workspace-' + phase + '.json'); original = path.read_bytes()
            for kind in ('container', 'network', 'volume', 'all'):
                data = json.loads(original)
                data = {} if kind == 'all' else {key: value for key, value in data.items() if not key.startswith(kind + ':')}
                write_json(path, data); f.seal()
                with self.assertRaisesRegex(ValueError, 'inventory|specifications changed'): validate_packet(f.directory)
            path.write_bytes(original); f.seal()

    def testControlPlanePlaceholdersCannotReplaceObservedCommandChains(self):
        from dapr_broker_qualification import validate_packet
        f = self.fixture; path = f.directory / 'execution-identities.json'; original = path.read_bytes()
        for name in ('placement', 'scheduler'):
            for field in ('creationReceipt', 'extractionReceipt', 'hashReceipt', 'launchReceipt'):
                data = json.loads(original); data['controlPlane'][name].pop(field)
                write_json(path, data); f.seal()
                with self.assertRaisesRegex(ValueError, 'command binding'): validate_packet(f.directory)
        path.write_bytes(original); f.seal()

    def testEmptyInventoriesRequireExplicitIndependentEmptyListings(self):
        from dapr_broker_qualification import validate_packet
        f = self.fixture; path = f.directory / 'execution-identities.json'; identities = json.loads(path.read_text())
        for phase in ('before', 'after'):
            record = f.commands[identities['workspaceInventories'][phase]]
            for observation in record['resourceObservations'].values():
                f.commands[observation['listingReceipt']]['output'] = ''
                observation['inspectionReceipt'] = None
            record['output'] = '{}'
            write_json(f.directory / ('workspace-' + phase + '.json'), {})
        write_json(f.directory / 'commands.json', f.commands); f.seal()
        self.assertEqual('PASS', validate_packet(f.directory)['technicalQualification'])
        f.commands[identities['workspaceInventories']['after']]['resourceObservations'].pop('volume')
        write_json(f.directory / 'commands.json', f.commands); f.seal()
        with self.assertRaisesRegex(ValueError, 'denominator'): validate_packet(f.directory)


class WorkerCustodyTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(); self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.channel = socket.socket(socket.AF_UNIX); self.addCleanup(self.channel.close)
        self.path = self.root / 'docker.sock'; self.channel.bind(str(self.path))
        self.endpoint = 'unix://' + str(self.path)
        self.daemon = 'synthetic-daemon'
        self.calls = []
        self.lease = self.root / 'worker.json'
        write_json(self.lease, {'exclusive': True, 'endpoint': self.endpoint, 'daemonId': self.daemon, 'workerId': 'synthetic-exclusive-worker'})

    def command(self, argv, **kwargs):
        self.calls.append((argv, kwargs))
        if argv[1:3] == ['context', 'inspect']:
            return 0, json.dumps([{'Endpoints': {'docker': {'Host': self.endpoint}}}])
        if argv[-3:] == ['info', '--format', '{{.ID}}']: return 0, self.daemon
        return 0, ''

    def owner(self, lease=True, env=None):
        owner = ExclusiveDocker(self.command, self.lease if lease else None, environment=env or {})
        self.addCleanup(owner.close)
        return owner

    def testPositiveControlPinsEndpointDaemonAndWorker(self):
        owner = self.owner(); owner.capture(); owner.run('ps', '-aq')
        self.assertEqual(self.endpoint, owner.endpoint)
        self.assertEqual('synthetic-daemon', owner.daemon_id)
        self.assertEqual(['docker', '--host', self.endpoint, 'ps', '-aq'], self.calls[-1][0])

    def testMissingExclusivityCannotMutate(self):
        with self.assertRaisesRegex(ValueError, 'exclusive disposable-worker'): self.owner(False).capture()
        self.assertFalse(any('run' in row[0] for row in self.calls))

    def testMismatchedAllocationCannotMutate(self):
        write_json(self.lease, {'exclusive': False})
        with self.assertRaisesRegex(ValueError, 'allocation does not match'): self.owner().capture()

    def testCompetingAllocationLockIsRejected(self):
        first = self.owner(); first.capture()
        with self.assertRaises(BlockingIOError): self.owner().capture()

    def testConflictingContextAndHostAreRejectedBeforeInfo(self):
        with self.assertRaisesRegex(ValueError, 'conflicting'):
            self.owner(env={'DOCKER_CONTEXT': 'selected', 'DOCKER_HOST': 'unix:///other.sock'}).capture()
        self.assertEqual(1, len(self.calls))

    def testRemoteEndpointIsRejectedBeforeInfo(self):
        with self.assertRaisesRegex(ValueError, 'local Unix'):
            self.owner(env={'DOCKER_HOST': 'ssh://remote'}).capture()
        self.assertEqual(1, len(self.calls))

    def testChangedDaemonCannotPerformCleanupMutation(self):
        owner = self.owner(); owner.capture(); self.daemon = 'different-daemon'
        with self.assertRaisesRegex(ValueError, 'daemon changed'): owner.run('rm', '-f', 'owned-id')
        self.assertFalse(any('rm' in row[0] for row in self.calls))

    def testChangedSocketCannotPerformMutation(self):
        owner = self.owner(); owner.capture(); self.path.unlink()
        other = socket.socket(socket.AF_UNIX); self.addCleanup(other.close); other.bind(str(self.path))
        with self.assertRaisesRegex(ValueError, 'socket changed'): owner.run('run', 'image')

    def testEnvironmentChangeCannotRedirectAcceptedDaemon(self):
        environment = {'DOCKER_HOST': self.endpoint}
        owner = self.owner(env=environment); owner.capture(); environment['DOCKER_HOST'] = 'ssh://remote'
        owner.run('ps')
        self.assertEqual(self.endpoint, self.calls[-1][0][2])
        self.assertNotIn('DOCKER_HOST', self.calls[-1][1]['env'])

    def testReplacedWorkerLeaseCannotPerformMutation(self):
        owner = self.owner(); owner.capture()
        replacement = self.root / 'replacement.json'
        replacement.write_bytes(self.lease.read_bytes()); replacement.replace(self.lease)
        with self.assertRaisesRegex(CustodyError, 'worker custody changed'): owner.run('rm', '-f', 'owned-id')
        self.assertFalse(any('rm' in row[0] for row in self.calls))

    def testChangedWorkerAllocationCannotPerformMutation(self):
        owner = self.owner(); owner.capture()
        allocation = json.loads(self.lease.read_text()); allocation['workerId'] = 'replacement-worker'; write_json(self.lease, allocation)
        with self.assertRaisesRegex(CustodyError, 'worker custody changed'): owner.run('rm', '-f', 'owned-id')
        self.assertFalse(any('rm' in row[0] for row in self.calls))


class StartupCleanupTests(unittest.TestCase):
    def setUp(self):
        spec = importlib.util.spec_from_file_location('broker_test_runner', ROOT / 'tools/qualify-dapr-brokers.py')
        self.module = importlib.util.module_from_spec(spec); spec.loader.exec_module(self.module)
        self.temporary = tempfile.TemporaryDirectory(); self.addCleanup(self.temporary.cleanup)
        self.out = Path(self.temporary.name) / 'packet'
        self.run = self.module.Run(types.SimpleNamespace(profile=ROOT / 'deploy/dapr/qualification/runtime-lock.json', output=self.out, exclusive_worker=None))
        self.addCleanup(lambda: shutil.rmtree(self.run.private_parent, ignore_errors=True))
        self.run.context = 'unix:///synthetic.sock'
        self.run.before = {'sentinel': 'unchanged'}
        write_json(self.out / 'workspace-before.json', self.run.before)
        self.run.docker = mock.Mock(return_value=(0, ''))

    def finish(self, after):
        self.run.inventory = mock.Mock(return_value=after)
        with mock.patch.object(self.module.signal, 'signal'):
            self.run.finish()
        return json.loads((self.out / 'cleanup.json').read_text())

    def testStartupFailureStillComparesCompleteBaseline(self):
        cleanup = self.finish({'sentinel': 'unchanged'})
        self.assertTrue(cleanup['complete'])
        self.run.inventory.assert_called_once()

    def testMissingSentinelFailsCleanup(self):
        cleanup = self.finish({})
        self.assertFalse(cleanup['complete'])
        self.assertFalse(cleanup['unrelatedContainersUnchanged'])

    def testChangedSentinelFailsCleanup(self):
        self.assertFalse(self.finish({'sentinel': 'changed'})['complete'])

    def testProcessCleanupFailureStillComparesBaseline(self):
        process = mock.Mock(); process.returncode = 0
        log = Path(self.temporary.name) / 'process.log'; log.write_text('synthetic')
        self.run.processes = [(process, ['owned-process'], self.module.utc(), log)]
        self.run.stop = mock.Mock(side_effect=RuntimeError('synthetic startup cancellation'))
        self.assertFalse(self.finish({'sentinel': 'unchanged'})['complete'])
        self.run.inventory.assert_called_once()

    def testCancellationRecordsAll24BlockedVectorsAndComparesBaseline(self):
        self.run.prepare = mock.Mock(side_effect=KeyboardInterrupt())
        self.run.inventory = mock.Mock(return_value={'sentinel': 'unchanged'})
        with mock.patch.object(self.module, 'Run', return_value=self.run), mock.patch.object(self.module.signal, 'signal'), \
                mock.patch.object(sys, 'argv', ['qualify-dapr-brokers.py', '--profile', 'unused', '--output', 'unused']), \
                mock.patch('builtins.print'):
            self.assertEqual(1, self.module.main())
        packet = json.loads((self.out / 'packet.json').read_text())
        self.assertEqual(24, sum(len(rows) for rows in packet['brokers'].values()))
        self.assertTrue(all(row['result'] == 'FAIL' for rows in packet['brokers'].values() for row in rows.values()))
        self.run.inventory.assert_called_once()

    def testStartupExceptionRecordsEveryUnexecutedVector(self):
        self.run.prepare = mock.Mock(side_effect=RuntimeError('synthetic startup failure'))
        self.run.inventory = mock.Mock(return_value={'sentinel': 'unchanged'})
        with mock.patch.object(self.module, 'Run', return_value=self.run), mock.patch.object(self.module.signal, 'signal'), \
                mock.patch.object(sys, 'argv', ['qualify-dapr-brokers.py', '--profile', 'unused', '--output', 'unused']), \
                mock.patch('builtins.print'):
            self.assertEqual(1, self.module.main())
        self.assertEqual(24, sum(len(rows) for rows in self.run.packet['brokers'].values()))
        self.assertTrue(json.loads((self.out / 'cleanup.json').read_text())['complete'])

    def testRejectedEndpointNeverRunsDockerCleanup(self):
        self.run.context = None; self.run.before = {}
        self.assertTrue(self.finish({})['complete'])
        self.run.docker.assert_not_called()

    def testDaemonChangeRetainsUnresolvedIdsAndSealsFailure(self):
        cidfile = self.run.private / 'owned.cid'; cidfile.write_text('1' * 64)
        self.run.containers = [cidfile]
        self.run.docker.side_effect = ValueError('Docker daemon changed')
        cleanup = self.finish({'sentinel': 'unchanged'})
        self.assertFalse(cleanup['complete'])
        self.assertEqual(['1' * 64], cleanup['ownedResourcesRemaining'])
        self.assertTrue((self.out / 'packet.json').exists())



class RealContainerLaunchTests(unittest.TestCase):
    setUp = WorkerCustodyTests.setUp
    def testRealRunContainerMergesEnvironmentOnceIncludingNone(self):
        spec = importlib.util.spec_from_file_location('container_launch_runner', ROOT / 'tools/qualify-dapr-brokers.py')
        module = importlib.util.module_from_spec(spec); spec.loader.exec_module(module)
        run = module.Run(types.SimpleNamespace(profile=ROOT / 'deploy/dapr/qualification/runtime-lock.json', output=self.root / 'packet', exclusive_worker=self.lease))
        self.addCleanup(lambda: shutil.rmtree(run.private_parent, ignore_errors=True))
        observed = []
        resources = {}
        def invoke(argv, **kwargs):
            observed.append((argv, kwargs))
            if argv[1:3] == ['context', 'inspect']:
                output = json.dumps([{'Endpoints': {'docker': {'Host': self.endpoint}}}])
            elif argv[-3:] == ['info', '--format', '{{.ID}}']: output = self.daemon
            elif argv[3:5] == ['image', 'inspect']: output = json.dumps([{'Config': {'Volumes': {}}}])
            elif argv[3] == 'ps': output = ''
            elif argv[3] == 'inspect': output = json.dumps([resources[argv[-1]]])
            else:
                self.assertEqual(['docker', '--host', self.endpoint, 'run'], argv[:4])
                Path(argv[argv.index('--cidfile') + 1]).write_text('a' * 64)
                labels = dict(argv[index + 1].split('=', 1) for index, argument in enumerate(argv) if argument == '--label')
                resources['a' * 64] = {'Id': 'a' * 64, 'Config': {'Labels': labels}}
                output = 'a' * 64
            return subprocess.CompletedProcess(argv, 0, output)
        with mock.patch.object(module.subprocess, 'run', side_effect=invoke):
            run.docker_owner = ExclusiveDocker(run.command, self.lease, environment={'BASE': 'retained', 'DOCKER_HOST': self.endpoint})
            run.docker_owner.capture(); self.addCleanup(run.docker_owner.close)
            for index, supplied in enumerate((None, {'BROKER_PASSWORD': 'synthetic-private', 'DOCKER_CONTEXT': 'wrong'})):
                self.assertEqual('a' * 64, run.container('launch-' + str(index), 'synthetic-image', env=supplied))
                actual = next(row[1]['env'] for row in reversed(observed) if row[0][3] == 'run')
                self.assertEqual('retained', actual['BASE'])
                self.assertFalse(set(actual) & {'DOCKER_HOST', 'DOCKER_CONTEXT', 'DOCKER_TLS_VERIFY', 'DOCKER_CERT_PATH'})
                if supplied: self.assertEqual('synthetic-private', actual['BROKER_PASSWORD'])

    def testRabbitMqAddUserArgumentsAreRedactedInBothReceiptForms(self):
        spec = importlib.util.spec_from_file_location('credential_runner', ROOT / 'tools/qualify-dapr-brokers.py')
        module = importlib.util.module_from_spec(spec); spec.loader.exec_module(module)
        run = module.Run(types.SimpleNamespace(profile=ROOT / 'deploy/dapr/qualification/runtime-lock.json', output=self.root / 'packet', exclusive_worker=self.lease))
        self.addCleanup(lambda: shutil.rmtree(run.private_parent, ignore_errors=True))
        secret = 'synthetic-rabbitmq-add-user-private-password'; run.redactions.append(secret)
        def invoke(argv, **kwargs):
            output = json.dumps([{'Endpoints': {'docker': {'Host': self.endpoint}}}]) if argv[1:3] == ['context', 'inspect'] else self.daemon if argv[-3:] == ['info', '--format', '{{.ID}}'] else ''
            return subprocess.CompletedProcess(argv, 0, output)
        with mock.patch.object(module.subprocess, 'run', side_effect=invoke):
            run.docker_owner = ExclusiveDocker(run.command, self.lease, environment={}); run.docker_owner.capture(); self.addCleanup(run.docker_owner.close)
            run.docker('exec', 'a' * 64, 'rabbitmqctl', 'add_user', 'qualification-v2', secret)
        self.assertNotIn(secret, json.dumps(run.receipts))
        self.assertEqual('<redacted-disposable-credential>', run.receipts[-1]['argv'][-1])
        self.assertEqual('<redacted-disposable-credential>', run.receipts[-1]['executedArgv'][-1])


class IndependentCleanupTests(unittest.TestCase):
    setUp = StartupCleanupTests.setUp
    finish = StartupCleanupTests.finish
    def testRemovalFailureContinuesToEveryOtherOwnedIdentity(self):
        ids = ['a' * 64, 'b' * 64, 'c' * 64]
        for index, identity in enumerate(ids):
            path = self.run.private / f'{index}.cid'; path.write_text(identity); self.run.containers.append(path)
        live = set(ids)
        def docker(*args, **kwargs):
            if args[0] == 'inspect': return 0, json.dumps([{'Id': args[1], 'Config': {'Labels': {'hexalith.qualification': self.run.run_id}}}])
            if args[0] == 'rm':
                if args[-1] == ids[1]: raise RuntimeError('synthetic removal failure')
                live.discard(args[-1])
            if args[0] == 'ps': return 0, '\n'.join(live)
            return 0, ''
        self.run.docker.side_effect = docker
        baseline = {kind + ':' + str(index): 'stable' for kind in ('container', 'network', 'volume') for index in range(4)}
        self.run.before = baseline
        cleanup = self.finish(dict(baseline))
        self.assertFalse(cleanup['complete'])
        self.assertEqual([ids[1]], cleanup['ownedResourcesRemaining'])
        self.assertEqual({tuple(call.args) for call in self.run.docker.call_args_list if call.args[0] == 'rm'}, {('rm', '-f', '-v', identity) for identity in ids})
        self.assertEqual(baseline, json.loads((self.out / 'workspace-after.json').read_text()))

    def testCustodyLossStopsMutationAndRetainsFullUnresolvedDenominator(self):
        ids = ['a' * 64, 'b' * 64]
        for index, identity in enumerate(ids):
            path = self.run.private / f'{index}.cid'; path.write_text(identity); self.run.containers.append(path)
        self.run.docker.side_effect = CustodyError('synthetic daemon changed')
        cleanup = self.finish(dict(self.run.before))
        self.assertEqual(ids, cleanup['ownedResourcesRemaining'])
        self.assertEqual(1, self.run.docker.call_count)
        self.assertFalse(cleanup['complete'])


class ProcessGroupTests(unittest.TestCase):
    def testGrandchildrenAreTerminatedAndReapedOnEveryExitPath(self):
        with tempfile.TemporaryDirectory() as directory:
            for mode in ('success', 'failure', 'timeout', 'interruption', 'escaped-session'):
                with self.subTest(mode=mode):
                    pidfile = Path(directory) / mode
                    program = (f"import subprocess,sys,time; p=subprocess.Popen([sys.executable,'-c','import time; time.sleep(60)'], start_new_session={mode == 'escaped-session'}); "
                               f"open({str(pidfile)!r},'w').write(str(p.pid)); " +
                               ('time.sleep(60)' if mode in ('timeout', 'interruption') else "time.sleep(.1); sys.exit(" + ('0' if mode in ('success', 'escaped-session') else '7') + ')'))
                    witness = {}
                    previous = None
                    try:
                        if mode == 'interruption':
                            previous = signal.signal(signal.SIGALRM, lambda *_: (_ for _ in ()).throw(KeyboardInterrupt()))
                            signal.setitimer(signal.ITIMER_REAL, .25)
                            with self.assertRaises(KeyboardInterrupt): run_process_group([sys.executable, '-c', program], timeout=3, witness=witness)
                        else:
                            code, _, _ = run_process_group([sys.executable, '-c', program], timeout=.25 if mode == 'timeout' else 3, witness=witness)
                            self.assertEqual({'success': 0, 'failure': 7, 'timeout': 124, 'escaped-session': 0}[mode], code)
                        child = int(pidfile.read_text())
                        with self.assertRaises(ProcessLookupError): os.kill(child, 0)
                        self.assertTrue(witness['complete'])
                    finally:
                        signal.setitimer(signal.ITIMER_REAL, 0)
                        if previous is not None: signal.signal(signal.SIGALRM, previous)

    def testBaselineNativeProcessIsPreserved(self):
        sentinel = subprocess.Popen([sys.executable, '-c', 'import time; time.sleep(60)'])
        try:
            witness = {}
            code, _, _ = run_process_group([sys.executable, '-c', 'pass'], witness=witness)
            self.assertEqual(0, code); self.assertTrue(witness['complete'])
            self.assertIsNone(sentinel.poll())
        finally:
            sentinel.terminate(); sentinel.wait(timeout=5)


class ChildDockerTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory(); self.addCleanup(self.directory.cleanup)
        self.root = Path(self.directory.name); self.journal = self.root / 'resources.json'
        self.resources = {}; self.calls = []; self.count = 0
        self.owner = types.SimpleNamespace(run_id='synthetic-owner', output=self.root)
        self.owner.docker = self.docker

    def invoke(self, args):
        self.calls.append(args)
        if args[:2] == ['image', 'inspect']:
            return subprocess.CompletedProcess(args, 0, json.dumps([{'Config': {'Volumes': getattr(self, 'image_volumes', {})}}]), '')
        if args[:1] in (['create'], ['run']) or args[:2] in (['volume', 'create'], ['network', 'create']):
            kind = 'container' if args[0] in ('create', 'run') else args[0]
            self.count += 1
            identity = f'{self.count:064x}'
            name = (args[args.index('--name') + 1] if '--name' in args else next(value.split('=', 1)[1] for value in args if value.startswith('--name='))) if kind == 'container' else args[-1]
            labels = dict(args[index + 1].split('=', 1) for index, argument in enumerate(args) if argument == '--label')
            value = {'kind': kind, 'Id': identity, 'Name': name, 'Config': {'Labels': labels}, 'Labels': labels, 'CreatedAt': '2026-11-08T00:00:00Z', 'Driver': 'local'}
            if kind == 'volume': value.pop('Id'); identity = name
            self.resources[identity] = value
            if '--cidfile' in args: Path(args[args.index('--cidfile') + 1]).write_text(identity)
            return subprocess.CompletedProcess(args, 0, identity, '')
        if args[0] == 'inspect':
            value = self.resources.get(args[-1]) or next((value for value in self.resources.values() if value['Name'] == args[-1]), None)
            return subprocess.CompletedProcess(args, 0 if value else 1, json.dumps([value]) if value else '', '')
        if args[0] == 'rm' or args[:2] in (['volume', 'rm'], ['network', 'rm']):
            self.resources.pop(args[-1], None)
            return subprocess.CompletedProcess(args, 0, args[-1], '')
        if args[0] == 'ps' and '--filter' in args:
            target = args[-1].removeprefix('name=^/').removesuffix('$')
            output = '\n'.join(key for key, value in self.resources.items() if value['Name'] == target)
        elif args[:2] in (['network', 'ls'], ['volume', 'ls']):
            output = '\n'.join(value['Name'] if '--format' in args and args[args.index('--format') + 1] == '{{.Name}}' else identity for identity, value in self.resources.items() if value.get('kind') == args[0])
        else: output = '\n'.join(self.resources)
        return subprocess.CompletedProcess(args, 0, output, '')

    def docker(self, *args, **kwargs):
        result = self.invoke(list(args))
        return result.returncode, result.stdout

    def testChildContainerNetworkAndVolumeAreTrackedAndCleaned(self):
        for arguments in (['create', '--name', 'child-container', 'synthetic-image'], ['network', 'create', 'child-network'], ['volume', 'create', 'child-volume']):
            self.assertEqual(0, child_docker(arguments, self.invoke, self.journal, self.owner.run_id).returncode)
        rows = json.loads(self.journal.read_text())['resources']
        self.assertEqual(3, len(rows)); self.assertTrue(all(row['id'] and not row['pending'] for row in rows))
        self.assertTrue(cleanup_child_resources(self.owner, self.journal)['complete'])
        self.assertEqual({}, self.resources)

    def testChangedOwnerAndVolumeCreationIdentityAreNeverRemoved(self):
        child_docker(['create', '--name', 'child-container', 'image'], self.invoke, self.journal, self.owner.run_id)
        child_docker(['volume', 'create', 'child-volume'], self.invoke, self.journal, self.owner.run_id)
        for value in self.resources.values():
            if value['Name'] == 'child-volume': value['CreatedAt'] = 'replacement'
            else: value['Config']['Labels']['hexalith.qualification'] = 'other-owner'
        result = cleanup_child_resources(self.owner, self.journal)
        self.assertFalse(result['complete']); self.assertEqual(2, len(self.resources))
        self.assertFalse(any(args[0] == 'rm' or args[:2] == ['volume', 'rm'] for args in self.calls))

    def testPendingCreationAndFailedRemovalRemainUnresolved(self):
        write_json(self.journal, {'resources': [{'kind': 'volume', 'name': 'unresolved', 'id': None, 'owner': self.owner.run_id}]})
        self.resources['unresolved'] = {'Name': 'unresolved', 'Labels': {'hexalith.qualification': self.owner.run_id}, 'CreatedAt': 'today'}
        self.assertFalse(cleanup_child_resources(self.owner, self.journal)['complete'])
        self.assertIn('unresolved', self.resources)

    def testOneChildRemovalFailureDoesNotSkipOtherOwnedIds(self):
        for name in ('first', 'second'):
            child_docker(['create', '--name', name, 'image'], self.invoke, self.journal, self.owner.run_id)
        failed = next(identity for identity, value in self.resources.items() if value['Name'] == 'second')
        original = self.owner.docker
        def docker(*args, **kwargs):
            if args[0] == 'rm' and args[-1] == failed: raise RuntimeError('synthetic removal failure')
            return original(*args, **kwargs)
        self.owner.docker = docker
        result = cleanup_child_resources(self.owner, self.journal)
        self.assertFalse(result['complete']); self.assertEqual([failed], list(self.resources))
        self.assertEqual(2, len(result['resources']))

    def testNamespacedCreateAndRunAreJournaledByImmutableIdentity(self):
        for operation in ('create', 'run'):
            child_docker(['container', operation, '--name=alias-' + operation, 'image'], self.invoke, self.journal, self.owner.run_id)
        rows = json.loads(self.journal.read_text())['resources']
        self.assertEqual(2, len(rows)); self.assertTrue(all(len(row['id']) == 64 for row in rows))
        self.assertTrue(cleanup_child_resources(self.owner, self.journal)['complete']); self.assertEqual({}, self.resources)

    def testImplicitVolumeSpellingsAreRejectedBeforeCreation(self):
        for options in (['-v', '/data'], ['--volume=/data'], ['-v', 'implicit:/data'], ['--volume=implicit:/data'],
                        ['--mount', 'type=volume,destination=/data'], ['--mount=type=volume,source=implicit,target=/data'], ['--volumes-from', 'other']):
            with self.subTest(options=options):
                with self.assertRaises(ValueError): child_docker(['container', 'create', *options, 'image'], self.invoke, self.journal, self.owner.run_id)
                self.assertEqual({}, self.resources)
        self.assertFalse(any(args[0] in ('create', 'run') for args in self.calls))

    def testImageDeclaredVolumesRequireExplicitOwnedVolumeBindOrTmpfs(self):
        self.image_volumes = {'/data': {}}
        with self.assertRaisesRegex(ValueError, 'image-declared'): child_docker(['run', 'image'], self.invoke, self.journal, self.owner.run_id)
        child_docker(['volume', 'create', 'owned-data'], self.invoke, self.journal, self.owner.run_id)
        for index, options in enumerate((['--mount=type=volume,src=owned-data,dst=/data'], ['--volume=/absolute/data:/data'], ['--tmpfs=/data'])):
            child_docker(['create', '--name', 'supported-' + str(index), *options, 'image'], self.invoke, self.journal, self.owner.run_id)
        self.assertTrue(cleanup_child_resources(self.owner, self.journal)['complete']); self.assertEqual({}, self.resources)

    def testCreationIntentIsDurableBeforeEngineAndPendingIdentityCanBeReconciled(self):
        original = self.invoke
        def interrupted(arguments):
            if arguments[0] == 'create':
                row = json.loads(self.journal.read_text())['resources'][-1]
                self.assertTrue(row['pending']); self.assertTrue(row['absenceVerified']); self.assertIsNone(row['id'])
                result = original(arguments)
                # Model loss of the create response and final inspection.
                raise RuntimeError('synthetic interrupted creation')
            if arguments[0] == 'inspect' and self.resources:
                raise RuntimeError('synthetic interrupted observation')
            return original(arguments)
        with self.assertRaises(RuntimeError): child_docker(['create', '--name', 'pending-container', 'image'], interrupted, self.journal, self.owner.run_id)
        self.assertTrue(json.loads(self.journal.read_text())['resources'][0]['pending'])
        self.assertTrue(cleanup_child_resources(self.owner, self.journal)['complete']); self.assertEqual({}, self.resources)

    def testPendingNetworkNameIsNotComparedWithNetworkIdsAfterInspectFailure(self):
        child_docker(['network', 'create', 'pending-network'], self.invoke, self.journal, self.owner.run_id)
        state = json.loads(self.journal.read_text()); state['resources'][0].update(id=None, pending=True); write_json(self.journal, state)
        original = self.owner.docker
        def docker(*args, **kwargs):
            if args[0] == 'inspect': return 1, 'synthetic inspection unavailable'
            return original(*args, **kwargs)
        self.owner.docker = docker
        result = cleanup_child_resources(self.owner, self.journal)
        self.assertFalse(result['complete']); self.assertFalse(result['resources'][0]['absent'])
        self.assertEqual(1, len(self.resources))

    def testReservedShortOwnershipLabelIsRejectedBeforeCreation(self):
        for option in (['-l', 'hexalith.qualification=other'], ['-l=hexalith.qualification.resource=other']):
            with self.assertRaisesRegex(ValueError, 'ownership labels'):
                child_docker(['create', *option, 'image'], self.invoke, self.journal, self.owner.run_id)
        self.assertEqual({}, self.resources)

    def testAlwaysPullMutableImageCannotInvalidateVolumeInspection(self):
        with self.assertRaisesRegex(ValueError, 'invalidate'):
            child_docker(['create', '--pull=always', 'mutable:tag'], self.invoke, self.journal, self.owner.run_id)
        self.assertFalse(any(args[:2] == ['image', 'inspect'] or args[0] == 'create' for args in self.calls))


class OrchestrationWitnessTests(unittest.TestCase):
    def testCandidateAdmissionUsesBytesBeforeCreatingOutput(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / 'runtime-lock-1.19.1.json'
            shutil.copyfile(ROOT / 'deploy/dapr/qualification/runtime-lock.json', path)
            with self.assertRaisesRegex(ValueError, 'approved candidate tuple'): require_candidate(path, ROOT)
            spec = importlib.util.spec_from_file_location('runtime_lane', ROOT / 'tools/qualify-dapr-runtime.py')
            module = importlib.util.module_from_spec(spec); spec.loader.exec_module(module)
            output = Path(directory) / 'must-not-exist'
            with mock.patch.object(sys, 'argv', ['qualify-dapr-runtime.py', '--profile', str(path), '--output', str(output), '--assembly', 'unused', '--class', '*']), mock.patch('sys.stderr'), self.assertRaises(SystemExit): module.main()
            self.assertFalse(output.exists())

    def testAdmissionLaneClaimsItsOwnWorkerWithoutNestedWrapper(self):
        spec = importlib.util.spec_from_file_location('lane_runner', ROOT / 'tools/run-dapr-runtime-lane.py')
        module = importlib.util.module_from_spec(spec); spec.loader.exec_module(module)
        command, environment = module.lane_command('actor-admission-process', Path('candidate.json'), Path('worker.json'), Path('/tmp/synthetic'))
        self.assertEqual('dotnet', command[0]); self.assertIn('*ActorCallbackAdmissionProcessTests', command)
        self.assertEqual('candidate.json', environment['EVENTSTORE_DAPR_QUALIFICATION_PROFILE'])
        self.assertEqual('worker.json', environment['QUALIFICATION_EXCLUSIVE_WORKER'])
        self.assertEqual('1.19.1', environment['EVENTSTORE_DAPR_RUNTIME_PROFILE'])
        source = (ROOT / 'tests/Hexalith.EventStore.PublishedCompatibility.Tests/ConsumerMaterializer.cs').read_text()
        commands = module.compatibility_git_commands()
        self.assertTrue(all('--recurse-submodules' not in command for command in commands))
        for identity in ('3.70.1', '3.110.0', '3.128.2', '935c21ac5ebc5e50738ae6ee79dc661acd2fd5a8', 'db897eac9f2819220b32d237109d7255218b46a9'):
            self.assertIn(identity, source); self.assertIn(identity, str(commands))

    def testCompatibilityWitnessProjectionExcludesSecretsAndMaterializedTrees(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory); scratch = root / 'private'; invocation = scratch / 'p1r-synthetic'; invocation.mkdir(parents=True)
            secret = 'synthetic-unrelated-private-credential'
            write_json(invocation / 'executor-inputs.json', {'executor': 'a' * 40, 'builds': 'b' * 40, 'private': secret})
            write_json(invocation / 'command-000001.json', {'command': {'FileName': 'dotnet', 'Environment': {'TOKEN': secret}, 'Arguments': [secret]}, 'Output': secret, 'ExitCode': 0})
            write_json(invocation / 'cleanup-pass-000001.json', {'passed': True, 'remaining': []})
            (invocation / 'scenarios.ndjson').write_text(json.dumps({'restore': True, 'Output': secret}) + '\n')
            (invocation / 'private.key').write_text(secret)
            (invocation / 'consumer').mkdir(); (invocation / 'consumer' / 'command-1.json').write_text(secret)
            (invocation / 'sample.nupkg').write_text(secret)
            destination = root / 'artifact'
            rows = retain_compatibility_witnesses(scratch, destination)
            self.assertEqual(4, len(rows)); self.assertTrue(all(row['retained'] for row in rows))
            retained = ''.join(path.read_text() for path in destination.rglob('*') if path.is_file())
            self.assertNotIn(secret, retained); self.assertIn('a' * 40, retained)
            self.assertFalse((destination / 'p1r-synthetic' / 'consumer').exists())
            self.assertTrue(json.loads((destination / 'p1r-synthetic' / 'cleanup-pass-000001.json').read_text())['passed'])

    def testWitnessLimitsAndManifestCommitActualRetainedBytes(self):
        from dapr_runtime_qualification import verify_compatibility_witnesses
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory); invocation = root / 'scratch' / 'invocation'; invocation.mkdir(parents=True)
            # Pretty serialization expands this witness beyond the per-file cap.
            (invocation / 'command-1.json').write_text(json.dumps({'values': [[0] * 2048 for _ in range(30)]}, separators=(',', ':')))
            write_json(invocation / 'cleanup.json', {'passed': True, 'diagnostic': 'synthetic-private'})
            destination = root / 'retained'
            rows = retain_compatibility_witnesses(invocation.parent, destination)
            self.assertFalse(next(row for row in rows if row['path'].endswith('command-1.json'))['retained'])
            manifest = verify_compatibility_witnesses(destination)
            self.assertEqual(sum(row['retainedBytes'] for row in rows if row['retained']), manifest['retainedBytes'])
            for row in rows:
                if row['retained']:
                    data = (destination / row['path']).read_bytes()
                    self.assertEqual(len(data), row['retainedBytes']); self.assertEqual(hashlib.sha256(data).hexdigest(), row['retainedSha256'])
            kept = next(row for row in rows if row['path'].endswith('cleanup.json'))
            self.assertNotEqual(kept['sha256'], kept['retainedSha256'])
            (destination / kept['path']).write_text('substituted')
            with self.assertRaisesRegex(ValueError, 'commitment'): verify_compatibility_witnesses(destination)

    def testOnlyExactGeneratedDiagnosticDigestsAreTrusted(self):
        from dapr_runtime_qualification import sanitize_xunit_report
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / 'report.xml'
            private = 'diagnostic-sha256:synthetic-private-value'
            exact = 'diagnostic-sha256:' + 'a' * 64
            path.write_text('<assemblies><failure>' + private + '</failure><output>' + exact + '</output></assemblies>')
            sanitize_xunit_report(path)
            first = path.read_bytes()
            self.assertNotIn(private.encode(), first); self.assertIn(exact.encode(), first)
            sanitize_xunit_report(path); self.assertEqual(first, path.read_bytes())

    def testAttributePolicyCommitsParameterizedNamesAndDropsCustomDiagnostics(self):
        from dapr_runtime_qualification import xunit_outcome
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / 'report.xml'
            private = 'synthetic-private-parameter'
            path.write_text('<assemblies><test result="Pass" time="0.12" name="Test(' + private + ')" method="' + private + '" diagnostic="' + private + '" /></assemblies>')
            self.assertTrue(xunit_outcome(path, 0)['passed'])
            self.assertNotIn(private, path.read_text())
            self.assertTrue(xunit_outcome(path, 0)['passed'])

    def testOriginalOfflinePacketValidationDoesNotRequirePosixFcntl(self):
        from test_dapr_broker_qualification import BrokerQualificationTests
        fixture = BrokerQualificationTests(); fixture.setUp(); self.addCleanup(fixture.tearDown)
        program = "import sys; sys.modules['fcntl']=None; sys.path.insert(0,sys.argv[1]); from dapr_broker_qualification import validate_packet; from pathlib import Path; assert validate_packet(Path(sys.argv[2]))['integrity']=='PASS'"
        result = subprocess.run([sys.executable, '-B', '-c', program, str(ROOT / 'tools'), str(fixture.directory)], capture_output=True, text=True)
        self.assertEqual(0, result.returncode, result.stderr)


FAKE_DOCKER = r'''import json,os,sys,re,subprocess
from pathlib import Path
args=sys.argv[1:]
if args[:1]==['--host']: args=args[2:]
state_path=Path(os.environ['FAKE_DOCKER_STATE'])
state=json.loads(state_path.read_text())
with Path(os.environ['FAKE_DOCKER_CALLS']).open('a') as stream: stream.write(json.dumps(args)+'\n')
if args[:2]==['context','inspect']:
 print(json.dumps([{'Endpoints':{'docker':{'Host':os.environ['FAKE_DOCKER_ENDPOINT']}}}]))
elif args[:1]==['info']: print(state['daemon'])
elif args[:1]==['pull']:
 state.setdefault('images',[]).append(args[1]);state_path.write_text(json.dumps(state));print(args[1])
elif args[:2]==['image','inspect']:
 if state.get('coldCache') and args[-1] not in state.get('images',[]): sys.exit(1)
 print(json.dumps([{'Config':{'User':state.get('imageUser',''),'Volumes':state.get('imageVolumes',{})}}]))
elif args[:1] in (['create'],['run']) or args[:2] in (['volume','create'],['network','create']):
 kind='container' if args[0] in ('create','run') else args[0]
 state['counter']=state.get('counter',0)+1
 identity=f"{state['counter']:064x}"
 name=(args[args.index('--name')+1] if '--name' in args else next(value.split('=',1)[1] for value in args if value.startswith('--name='))) if kind=='container' else args[-1]
 labels=dict(args[index+1].split('=',1) for index,value in enumerate(args) if value=='--label')
 if kind=='volume': identity=name
 value={'kind':kind,'Id':identity,'Name':name,'Config':{'Labels':labels},'Labels':labels,'CreatedAt':'2026-11-08T00:00:00Z','Driver':'local','HostConfig':{},'Mounts':[]}
 if kind=='container' and state.get('entrypoint') and name.endswith('-openbao'):
  environment=dict(os.environ);environment.pop('SKIP_CHOWN',None)
  for index,argument in enumerate(args):
   if argument in ('-e','--env'):
    key,separator,item=args[index+1].partition('=')
    if separator:environment[key]=item
  result=subprocess.run(['/bin/sh',state['entrypoint']],env=environment,text=True,stdout=subprocess.PIPE,stderr=subprocess.STDOUT)
  value.update(Running=result.returncode==0,EntrypointExitCode=result.returncode,EntrypointLog=result.stdout)
 if kind=='volume': value.pop('Id')
 state['resources'][identity]=value
 state_path.write_text(json.dumps(state))
 if '--cidfile' in args: Path(args[args.index('--cidfile')+1]).write_text(identity)
 print(identity)
elif args[:1]==['inspect']:
 if args[1:2]==['--format'] and state.get('entrypoint'):
  value=state['resources'][args[-1]]
  if not value['Running']:print(value['EntrypointLog']);sys.exit(1)
  print('38200');sys.exit(0)
 names=args[3:] if args[1:2]==['--type'] else args[1:]
 values=[]
 for name in names:
  value=state['resources'].get(name) or next((row for row in state['resources'].values() if row['Name']==name),None)
  if value is None: sys.exit(1)
  values.append({key:item for key,item in value.items() if key!='kind'})
 print(json.dumps(values))
elif args[:1]==['rm'] or args[:2] in (['volume','rm'],['network','rm']):
 state['resources'].pop(args[-1],None);state_path.write_text(json.dumps(state))
elif args[:1]==['ps'] or args[:2] in (['volume','ls'],['network','ls']):
 kind='container' if args[0]=='ps' else args[0]
 rows=[(identity,row) for identity,row in state['resources'].items() if row['kind']==kind]
 if '--filter' in args:
  value=args[args.index('--filter')+1]
  if value.startswith('name='): rows=[(identity,row) for identity,row in rows if re.fullmatch(value[5:],'/'+row['Name'])]
  elif value.startswith('label='):
   key,expected=value[6:].split('=',1);rows=[(identity,row) for identity,row in rows if row['Config']['Labels'].get(key)==expected]
 print('\n'.join(row['Name'] if '--format' in args and args[args.index('--format')+1]=='{{.Name}}' else identity for identity,row in rows))
'''


class InstalledDockerAdapterTests(unittest.TestCase):
    def setUp(self):
        WorkerCustodyTests.setUp(self)
        self.engine = self.root / 'docker'
        self.engine.write_text('#!' + sys.executable + '\n' + FAKE_DOCKER); self.engine.chmod(0o755)
        self.state = self.root / 'engine.json'; write_json(self.state, {'daemon': self.daemon, 'resources': {}})
        self.calls_path = self.root / 'calls.ndjson'
        observed = self.path.stat(); lease = self.lease.stat()
        self.environment = {**os.environ, 'PYTHONDONTWRITEBYTECODE': '1', 'PYTHONPATH': str(ROOT / 'tools'),
            'FAKE_DOCKER_STATE': str(self.state), 'FAKE_DOCKER_CALLS': str(self.calls_path), 'FAKE_DOCKER_ENDPOINT': self.endpoint,
            'QUALIFICATION_REAL_DOCKER': str(self.engine), 'QUALIFICATION_PINNED_ENDPOINT': self.endpoint,
            'QUALIFICATION_PINNED_DAEMON': self.daemon, 'QUALIFICATION_PINNED_SOCKET': f'{observed.st_dev}:{observed.st_ino}',
            'QUALIFICATION_PINNED_WORKER': str(self.lease), 'QUALIFICATION_PINNED_LEASE': f'{lease.st_dev}:{lease.st_ino}',
            'QUALIFICATION_PINNED_ALLOCATION': self.lease.read_text(), 'QUALIFICATION_CHILD_RESOURCES': str(self.root / 'journal.json'),
            'QUALIFICATION_RESOURCE_OWNER': 'installed-adapter-owner'}
        # Inherited worker helper tests retain their own fake command isolation.

    def adapter(self, *arguments):
        return subprocess.run([sys.executable, '-B', str(ROOT / 'tools/qualification-docker.py'), *arguments], env=self.environment, capture_output=True, text=True)

    def testInstalledAliasCreatesAndCleansOnlyExactOwnedIdentity(self):
        for operation in ('create', 'run'):
            result = self.adapter('container', operation, '--name', 'adapter-' + operation, 'synthetic-image')
            self.assertEqual(0, result.returncode, result.stderr)
        state = json.loads(self.state.read_text()); self.assertEqual(2, len(state['resources']))
        journal = self.root / 'journal.json'
        owner = types.SimpleNamespace(run_id='installed-adapter-owner', output=self.root)
        def docker(*arguments, **kwargs):
            result = self.adapter(*arguments)
            return result.returncode, result.stdout
        owner.docker = docker
        self.assertTrue(cleanup_child_resources(owner, journal)['complete'])
        self.assertEqual({}, json.loads(self.state.read_text())['resources'])

    def testInstalledAdapterRejectsChangedAllocationSocketAndDaemon(self):
        original_lease = self.lease.read_bytes()
        for mode in ('allocation-content', 'allocation-replacement', 'socket-replacement', 'daemon-change'):
            with self.subTest(mode=mode):
                # A fresh expected baseline for each mutation.
                self.lease.write_bytes(original_lease)
                allocation_identity = self.lease.stat()
                self.environment['QUALIFICATION_PINNED_LEASE'] = f'{allocation_identity.st_dev}:{allocation_identity.st_ino}'
                socket_identity = self.path.stat()
                self.environment['QUALIFICATION_PINNED_SOCKET'] = f'{socket_identity.st_dev}:{socket_identity.st_ino}'
                write_json(self.state, {'daemon': self.daemon, 'resources': {}})
                if mode == 'allocation-content': write_json(self.lease, {'exclusive': False})
                elif mode == 'allocation-replacement':
                    replacement = self.root / 'replacement'; replacement.write_bytes(original_lease); os.replace(replacement, self.lease)
                elif mode == 'socket-replacement':
                    self.path.unlink(); channel = socket.socket(socket.AF_UNIX); self.addCleanup(channel.close); channel.bind(str(self.path))
                else: write_json(self.state, {'daemon': 'replacement-daemon', 'resources': {}})
                before = len(self.calls_path.read_text().splitlines()) if self.calls_path.exists() else 0
                result = self.adapter('container', 'run', 'synthetic-image')
                self.assertEqual(1, result.returncode)
                calls = [json.loads(line) for line in self.calls_path.read_text().splitlines()[before:]] if self.calls_path.exists() else []
                self.assertFalse(any(row[0] in ('run', 'create') or row[:2] in (['container','run'],['container','create']) for row in calls))
                self.assertEqual({}, json.loads(self.state.read_text())['resources'])

    def testInstalledImplicitVolumeIsRejectedBeforeCreate(self):
        result = self.adapter('container', 'create', '--mount=type=volume,source=implicit,target=/data', 'image')
        self.assertEqual(1, result.returncode)
        self.assertFalse(any(json.loads(line)[0] in ('run','create') for line in self.calls_path.read_text().splitlines()))

    def testInstalledAdapterAllowsOwnedMutationAndRefusesUnrelatedTargets(self):
        result = self.adapter('create', '--name', 'owned', 'image'); self.assertEqual(0, result.returncode, result.stderr)
        owned = result.stdout.strip()
        state = json.loads(self.state.read_text())
        unrelated = 'f' * 64
        state['resources'][unrelated] = {'kind': 'container', 'Id': unrelated, 'Name': 'unrelated', 'Config': {'Labels': {}}, 'HostConfig': {}, 'Mounts': []}
        write_json(self.state, state)
        for command in (['rm', '-f', unrelated], ['container', 'stop', 'unrelated'], ['exec', unrelated, '/bin/sh', '-c', 'true'], ['cp', '/tmp/source', unrelated + ':/data']):
            self.assertEqual(1, self.adapter(*command).returncode)
        result = self.adapter('exec', owned, '/bin/sh', '-c', 'true')
        self.assertEqual(0, result.returncode, result.stderr)
        result = self.adapter('rm', '-f', owned); self.assertEqual(0, result.returncode, result.stderr)
        self.assertEqual([unrelated], list(json.loads(self.state.read_text())['resources']))

    def testInstalledCreatePayloadCanContainShellContextLikeArguments(self):
        result = self.adapter('create', 'image', '/bin/sh', '-c', 'true')
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertEqual(1, len(json.loads(self.state.read_text())['resources']))


class DirectLaneSupervisionTests(unittest.TestCase):
    setUp = InstalledDockerAdapterTests.setUp
    def testActualSigtermReconcilesChildWhoseCleanupOutlastsGrace(self):
        self.supervise_interrupted_child(False)

    def testActualSigtermBeforeFirstCreateReconcilesRegisteredPrivateDirectory(self):
        self.supervise_interrupted_child(True)

    def supervise_interrupted_child(self, before_create):
        child = self.root / 'slow-owner.py'
        child.write_text(
            "import importlib.util,os,signal,sys,time,types\nfrom pathlib import Path\n"
            f"sys.path.insert(0,{str(ROOT / 'tools')!r})\n"
            "from dapr_runtime_qualification import ExclusiveDocker\n"
            f"s=importlib.util.spec_from_file_location('broker',{str(ROOT / 'tools/qualify-dapr-brokers.py')!r});m=importlib.util.module_from_spec(s);s.loader.exec_module(m)\n"
            "r=m.Run(types.SimpleNamespace(profile=Path(os.environ['QUALIFICATION_PROFILE']),output=Path(os.environ['CHILD_OUTPUT']),exclusive_worker=Path(os.environ['QUALIFICATION_EXCLUSIVE_WORKER'])))\n"
            "if os.environ.get('PRECREATE_ONLY')=='1':\n signal.signal(signal.SIGTERM,signal.SIG_IGN)\n Path(os.environ['CHILD_READY']).write_text(str(r.private_parent))\n time.sleep(60)\n"
            "r.docker_owner=ExclusiveDocker(r.command,r.args.exclusive_worker);r.docker_owner.capture()\n"
            "identity=r.container('slow-child','synthetic-image')\n"
            "def stop(*_):\n time.sleep(15)\n r.docker('rm','-f',identity)\n sys.exit(0)\n"
            "signal.signal(signal.SIGTERM,stop)\n"
            "Path(os.environ['CHILD_READY']).write_text(identity)\n"
            "time.sleep(60)\n")
        parent = self.root / 'supervisor.py'
        parent.write_text(
            "import importlib.util,os,sys\nfrom pathlib import Path\n"
            f"sys.path.insert(0,{str(ROOT / 'tools')!r})\n"
            f"s=importlib.util.spec_from_file_location('lane',{str(ROOT / 'tools/run-dapr-runtime-lane.py')!r});m=importlib.util.module_from_spec(s);s.loader.exec_module(m)\n"
            "m.lane_command=lambda lane,profile,worker,output:([sys.executable,'-B',os.environ['CHILD_PROGRAM']],dict(os.environ))\n"
            "sys.argv=['lane','--lane','actor-admission-process']\nraise SystemExit(m.main())\n")
        ready = self.root / 'ready'; output = self.root / 'parent-output'
        baseline = {}
        for kind in ('container', 'network', 'volume'):
            identity = kind + '-sentinel'
            baseline[identity] = {'kind':kind,'Id':identity,'Name':identity,'Config':{'Labels':{}},'HostConfig':{},'Mounts':[], 'Labels':{}, 'CreatedAt':'baseline','Driver':'local'}
        write_json(self.state, {'daemon': self.daemon, 'resources': baseline})
        environment = {**self.environment, 'PATH': str(self.root) + os.pathsep + os.environ['PATH'],
            'QUALIFICATION_PROFILE': str(ROOT / CANDIDATE_PROFILE), 'QUALIFICATION_EXCLUSIVE_WORKER': str(self.lease),
            'QUALIFICATION_OUTPUT': str(output), 'CHILD_OUTPUT': str(self.root / 'child-output'), 'CHILD_READY': str(ready), 'CHILD_PROGRAM': str(child), 'PRECREATE_ONLY': '1' if before_create else '0'}
        process = subprocess.Popen([sys.executable, '-B', str(parent)], env=environment, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)
        try:
            deadline = time.monotonic() + 15
            while not ready.exists() and process.poll() is None and time.monotonic() < deadline: time.sleep(.02)
            self.assertTrue(ready.exists(), process.communicate(timeout=2) if process.poll() is not None else 'child startup timed out')
            child_identity = ready.read_text()
            if before_create: self.assertTrue(Path(child_identity).is_dir())
            else: self.assertIn(child_identity, json.loads(self.state.read_text())['resources'])
            process.terminate()
            stdout, stderr = process.communicate(timeout=15)
            self.assertEqual(1, process.returncode, stdout + stderr)
            result = json.loads((output / 'lane-result.json').read_text())
            self.assertEqual('FAIL', result['technicalQualification'])
            self.assertTrue(result['process']['complete'])
            self.assertTrue(result['dockerCleanup']['complete'], result)
            self.assertTrue(result['dockerCleanup']['preservationComplete'], result)
            self.assertTrue(all(row['privateDirectory']['absent'] for row in result['dockerCleanup']['owners']))
            self.assertEqual(baseline, json.loads(self.state.read_text())['resources'])
            self.assertEqual(json.loads((output / 'workspace-before.json').read_text()), json.loads((output / 'workspace-after.json').read_text()))
            # The killed owner never reached its delayed rm. Parent reconciliation did.
            if before_create: self.assertFalse(Path(child_identity).exists())
            else: self.assertTrue(any(row['id'] == child_identity and row['absent'] for owner in result['dockerCleanup']['owners'] for row in owner['resources']))
        finally:
            if process.poll() is None: process.kill(); process.wait(timeout=5)
            process.stdout.close(); process.stderr.close()


class ResultEntrypointTests(unittest.TestCase):
    setUp = InstalledDockerAdapterTests.setUp

    def testBothEntrypointsAdjudicateEmptySkippedFailedPassingAndPartialReports(self):
        # Use an actual isolated fake executable and on-disk reports. Only runtime
        # service bootstrapping is substituted; entry-point adjudication/cleanup run.
        report_program = self.root / 'synthetic-xunit'
        report_program.write_text('#!' + sys.executable + '\n' +
            "import json,os,sys\nfrom pathlib import Path\n"
            "mode=os.environ['SYNTHETIC_REPORT_MODE']\n"
            "path=Path(sys.argv[sys.argv.index('-xml')+1]);path.parent.mkdir(parents=True,exist_ok=True)\n"
            "text='<assemblies><test result=\"'+{'passing':'Pass','skipped':'Skip','failed':'Fail'}.get(mode,'Pass')+'\"><failure>diagnostic-sha256:synthetic-secret-diagnostic</failure></test></assemblies>'\n"
            "if mode=='empty':text='<assemblies />'\n"
            "if mode=='partial':text='<assemblies><failure>diagnostic-sha256:synthetic-secret-diagnostic'\n"
            "path.write_text(text)\n"
            "admission=path.parent/'admission';admission.mkdir(exist_ok=True);(admission/'result.json').write_text(json.dumps({'passed':True,'qualification_passed':True,'cleanup_passed':True}))\n"
            "raise SystemExit(1 if mode=='failed' else 0)\n")
        report_program.chmod(0o755)
        (self.root / 'dotnet').symlink_to(report_program)
        for entry in ('runtime', 'direct'):
            for mode in ('empty', 'skipped', 'failed', 'passing', 'partial'):
                with self.subTest(entry=entry, mode=mode):
                    write_json(self.state, {'daemon': self.daemon, 'resources': {}, 'coldCache': True})
                    output = self.root / (entry + '-' + mode)
                    environment = {**self.environment, 'PATH':str(self.root)+os.pathsep+os.environ['PATH'], 'SYNTHETIC_REPORT_MODE':mode,
                        'QUALIFICATION_PROFILE':str(ROOT / CANDIDATE_PROFILE), 'QUALIFICATION_OUTPUT':str(output),
                        'QUALIFICATION_EXCLUSIVE_WORKER':str(self.lease)}
                    harness = self.root / ('harness-' + entry + '.py')
                    if entry == 'direct':
                        program = ("import importlib.util,os,sys\nfrom pathlib import Path\n"
                            f"sys.path.insert(0,{str(ROOT / 'tools')!r})\n"
                            f"s=importlib.util.spec_from_file_location('lane',{str(ROOT / 'tools/run-dapr-runtime-lane.py')!r});m=importlib.util.module_from_spec(s);s.loader.exec_module(m)\n"
                            f"m.lane_command=lambda lane,profile,worker,output:([{str(report_program)!r},'-xml',str(output/'xunit.xml')],dict(os.environ))\n"
                            "sys.argv=['lane','--lane','actor-admission-process']\nraise SystemExit(m.main())\n")
                    else:
                        program = ("import importlib.util,os,sys\nfrom pathlib import Path\n"
                            f"sys.path.insert(0,{str(ROOT / 'tools')!r})\n"
                            "from dapr_runtime_qualification import ExclusiveDocker\nfrom dapr_broker_qualification import write_json\n"
                            f"s=importlib.util.spec_from_file_location('runtime',{str(ROOT / 'tools/qualify-dapr-runtime.py')!r});m=importlib.util.module_from_spec(s);s.loader.exec_module(m)\n"
                            "original=importlib.util.spec_from_file_location\n"
                            "def prepare(self):\n"
                            " self.docker_owner=ExclusiveDocker(self.command,self.args.exclusive_worker);self.docker_owner.capture();self.context=self.docker_owner.endpoint\n"
                            " self.before=self.inventory();write_json(self.output/'workspace-before.json',self.before)\n"
                            " self.docker('pull',self.profile['openbao']['reference'])\n"
                            " self.daprd=self.private/'synthetic-daprd';self.daprd.write_text('synthetic')\n"
                            " self.placement='127.0.0.1:12345';self.scheduler='127.0.0.1:12346'\n"
                            " for name in ('placement','scheduler'):(self.private/name).write_text('synthetic')\n"
                            "def spec(*args,**kwargs):\n"
                            " value=original(*args,**kwargs);execute=value.loader.exec_module\n"
                            " def load(module):\n"
                            "  execute(module);module.Run.prepare=prepare;module.Run.published_port=lambda *args:1234;module.Run.wait=lambda *args,**kwargs:None\n"
                            " value.loader.exec_module=load\n return value\n"
                            "importlib.util.spec_from_file_location=spec\n"
                            "sys.argv=['runtime','--profile',os.environ['QUALIFICATION_PROFILE'],'--exclusive-worker',os.environ['QUALIFICATION_EXCLUSIVE_WORKER'],'--output',os.environ['QUALIFICATION_OUTPUT'],'--assembly','synthetic.dll','--class','*SyntheticSelectedTest']\n"
                            "raise SystemExit(m.main())\n")
                    harness.write_text(program)
                    result = subprocess.run([sys.executable,'-B',str(harness)],env=environment,text=True,capture_output=True,timeout=20)
                    self.assertEqual(0 if mode=='passing' else 1,result.returncode,result.stdout+result.stderr)
                    receipt = json.loads((output/('regression-results.json' if entry=='runtime' else 'lane-result.json')).read_text())
                    self.assertEqual('PASS' if mode=='passing' else 'FAIL',receipt['technicalQualification'],receipt)
                    self.assertEqual('FAIL',receipt['productionEligibility']);self.assertIsNone(receipt['selectedBroker'])
                    retained=''.join(path.read_text() for path in output.rglob('xunit*') if path.is_file())
                    self.assertNotIn('synthetic-secret-diagnostic',retained)
                    if mode=='partial': self.assertTrue(list(output.glob('xunit*.invalid.json')))
                    self.assertEqual({},json.loads(self.state.read_text())['resources'])


class FollowupRegressionTests(unittest.TestCase):
    setUp = InstalledDockerAdapterTests.setUp

    def testOpenBaoNonrootEntrypointFailureAndSupportedSetup(self):
        spec = importlib.util.spec_from_file_location('openbao_startup', ROOT / 'tools/qualify-dapr-brokers.py')
        module = importlib.util.module_from_spec(spec); spec.loader.exec_module(module)
        commands = self.root / 'entrypoint-commands'; commands.mkdir()
        # Model the pinned image's nonroot ownership gate with a real shell:
        # tmpfs hides the image-owned directory, stat sees root, and chown fails.
        # Source: openbao/openbao dd9c19c37a878cf4a81b18efb8d6f0599c7da923,
        # scripts/docker/docker-entrypoint.sh. No daemon or privilege is used.
        for name, body in (('stat', 'echo 0'), ('id', 'echo 100'),
                           ('chown', 'echo "chown: /openbao/logs: Operation not permitted" >&2; exit 1')):
            executable = commands / name; executable.write_text('#!/bin/sh\n' + body + '\n'); executable.chmod(0o755)
        entrypoint = self.root / 'openbao-entrypoint.sh'
        entrypoint.write_text('set -e\nif [ -z "${SKIP_CHOWN:-}" ]; then\n'
            ' for directory in /openbao/logs /openbao/file; do\n'
            '  if [ "$(stat -c %u "$directory")" != "$(id -u openbao)" ]; then\n'
            '   chown -R openbao:openbao "$directory"\n  fi\n done\nfi\necho server-started\n')
        environment = {**self.environment, 'PATH': str(commands) + os.pathsep + str(self.root) + os.pathsep + os.environ['PATH']}
        for remove_switch in (True, False):
            with self.subTest(remove_switch=remove_switch), mock.patch.dict(os.environ, environment):
                write_json(self.state, {'daemon': self.daemon, 'resources': {}, 'imageUser': 'openbao',
                    'imageVolumes': {'/openbao/logs': {}, '/openbao/file': {}}, 'entrypoint': str(entrypoint)})
                run = module.Run(types.SimpleNamespace(profile=ROOT / CANDIDATE_PROFILE,
                    output=self.root / ('openbao-negative' if remove_switch else 'openbao-positive'), exclusive_worker=self.lease))
                self.addCleanup(lambda path=run.private_parent: shutil.rmtree(path, ignore_errors=True))
                run.ssl = object()
                run.docker_owner = ExclusiveDocker(run.command, self.lease)
                try:
                    run.docker_owner.capture()
                    # Exercise the actual container, volume admission and durable
                    # journal paths; only the negative removes the proposed switch.
                    original = run.docker
                    def docker(*args, **kwargs):
                        args = list(args)
                        if remove_switch and args[0] == 'run':
                            index = args.index('SKIP_CHOWN=1'); del args[index - 1:index + 1]
                        return original(*args, **kwargs)
                    run.docker = docker
                    initialized = {'root_token': 'synthetic-root', 'keys': ['synthetic-key'], 'keys_base64': ['synthetic-key64']}
                    run.request = mock.Mock(side_effect=lambda url, *args, **kwargs:
                        (501, {}) if url.endswith('/sys/health') else (200, initialized) if url.endswith('/sys/init') else
                        (200, {'auth': {'client_token': 'synthetic-read-token'}}))
                    if remove_switch:
                        with self.assertRaisesRegex(RuntimeError, 'Operation not permitted'): run.openbao()
                        run.request.assert_not_called()
                    else:
                        run.openbao()
                        self.assertEqual('synthetic-read-token', (run.private / 'bao.token').read_text())
                        self.assertEqual('https://127.0.0.1:38200', run.bao_url)
                    value = next(iter(json.loads(self.state.read_text())['resources'].values()))
                    self.assertEqual(not remove_switch, value['Running'])
                    self.assertIn('Operation not permitted' if remove_switch else 'server-started', value['EntrypointLog'])
                    creation = next(row['argv'] for row in run.receipts if row['argv'][:2] == ['docker', 'run'])
                    self.assertFalse(any(option in creation for option in ('--user', '--privileged', '--entrypoint')))
                    self.assertIn('/openbao/logs', creation); self.assertIn('/openbao/file', creation)
                    self.assertIn(str(run.private / 'bao-data') + ':/data', creation)
                    self.assertTrue(cleanup_child_resources(run, run.resource_journal)['complete'])
                    self.assertEqual({}, json.loads(self.state.read_text())['resources'])
                finally:
                    run.docker_owner.close()

    def testActualLoadedProfileBytesRetainedForAdmittedTopicsAndOpenBaoVariants(self):
        spec = importlib.util.spec_from_file_location('retained_profile_runner', ROOT / 'tools/qualify-dapr-brokers.py')
        module = importlib.util.module_from_spec(spec); spec.loader.exec_module(module)
        from dapr_runtime_qualification import profile_path
        for index, selected in enumerate(('deploy/dapr/qualification/runtime-lock.json', CANDIDATE_PROFILE)):
            for mutation in ('topics', 'openbao'):
                profile = json.loads((ROOT / selected).read_text())
                if mutation == 'topics': profile['topics'].append('synthetic.extra-topic')
                else: profile['openbao']['reference'] = 'quay.io/openbao/openbao@sha256:' + '0' * 64
                source = self.root / ('selected-' + str(index) + '-' + mutation + '.json'); write_json(source, profile)
                run = module.Run(types.SimpleNamespace(profile=source, output=self.root / ('packet-' + str(index) + '-' + mutation), exclusive_worker=None))
                self.addCleanup(lambda directory=run.private_parent: shutil.rmtree(directory, ignore_errors=True))
                retained = run.retained_inputs / profile_path(profile)
                self.assertEqual(source.read_bytes(), retained.read_bytes())
                self.assertNotEqual((ROOT / selected).read_bytes(), retained.read_bytes())
                validate_profile(json.loads(retained.read_text()), run.retained_inputs)

    def testSelectedAdmissionPreparesAllImmutableImagesOnColdCache(self):
        from dapr_runtime_qualification import REDIS_IMAGE
        spec = importlib.util.spec_from_file_location('cold_admission', ROOT / 'tools/qualify-internal-actor-admission.py')
        module = importlib.util.module_from_spec(spec); spec.loader.exec_module(module)
        write_json(self.state, {'daemon': self.daemon, 'resources': {}, 'coldCache': True})
        environment = {**self.environment, 'PATH': str(self.root) + os.pathsep + os.environ['PATH'], 'QUALIFICATION_DAPRD': str(self.root / 'synthetic-daprd')}
        run = module.Qualification(module.parse_args(['--profile', str(ROOT / CANDIDATE_PROFILE), '--exclusive-worker', str(self.lease), '--evidence', str(self.root / 'admission')]))
        self.addCleanup(lambda: shutil.rmtree(run.runtime, ignore_errors=True))
        expected = {REDIS_IMAGE, *(run.profile['images'][name]['reference'] for name in ('placement', 'scheduler'))}
        def first_container(kind, image, *args, **kwargs):
            self.assertEqual('redis', kind); self.assertEqual(REDIS_IMAGE, image)
            self.assertEqual(expected, set(json.loads(self.state.read_text())['images']))
            run.run('docker', 'image', 'inspect', image)
            raise RuntimeError('cold-cache preparation reached supported creation')
        run.launch_container = first_container
        try:
            with mock.patch.dict(os.environ, environment), mock.patch.object(module, 'sha', return_value=run.profile['runtime']['binarySha256']):
                with self.assertRaisesRegex(RuntimeError, 'cold-cache preparation'): run.setup()
        finally:
            if run.docker_owner is not None: run.docker_owner.close()

    def testRepeatedRealSigtermDuringRuntimeCleanupRetainsFinalResult(self):
        output = self.root / 'runtime-cleanup'; ready = self.root / 'cleanup-ready'; harness = self.root / 'runtime-cleanup.py'
        harness.write_text(
            "import importlib.util,os,sys,time\nfrom pathlib import Path\n"
            f"sys.path.insert(0,{str(ROOT / 'tools')!r})\n"
            "import dapr_runtime_qualification as helper\n"
            f"s=importlib.util.spec_from_file_location('runtime',{str(ROOT / 'tools/qualify-dapr-runtime.py')!r});m=importlib.util.module_from_spec(s);s.loader.exec_module(m)\n"
            "original=importlib.util.spec_from_file_location\n"
            "def prepare(self):raise RuntimeError('synthetic preparation failure')\n"
            "def spec(*args,**kwargs):\n"
            " value=original(*args,**kwargs);execute=value.loader.exec_module\n"
            " def load(module):\n  execute(module);module.Run.prepare=prepare\n"
            " value.loader.exec_module=load\n return value\n"
            "def retain(scratch,destination):\n Path(os.environ['CLEANUP_READY']).write_text(str(scratch.parent))\n time.sleep(1)\n"
            "helper.retain_compatibility_witnesses=retain\nimportlib.util.spec_from_file_location=spec\n"
            "sys.argv=['runtime','--profile',os.environ['QUALIFICATION_PROFILE'],'--output',os.environ['QUALIFICATION_OUTPUT'],'--assembly','unused','--class','*']\nraise SystemExit(m.main())\n")
        environment = {**self.environment, 'QUALIFICATION_PROFILE': str(ROOT / CANDIDATE_PROFILE), 'QUALIFICATION_OUTPUT': str(output), 'CLEANUP_READY': str(ready)}
        process = subprocess.Popen([sys.executable, '-B', str(harness)], env=environment, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)
        try:
            deadline = time.monotonic() + 10
            while not ready.exists() and process.poll() is None and time.monotonic() < deadline: time.sleep(.02)
            self.assertTrue(ready.exists())
            process.terminate(); time.sleep(.05); process.terminate()
            stdout, stderr = process.communicate(timeout=10)
            self.assertEqual(1, process.returncode, stdout + stderr)
            result = json.loads((output / 'regression-results.json').read_text())
            self.assertEqual('FAIL', result['technicalQualification']); self.assertTrue(result['cleanupComplete'])
            self.assertFalse(Path(ready.read_text()).exists())
        finally:
            if process.poll() is None: process.kill(); process.wait(timeout=5)
            process.stdout.close(); process.stderr.close()


if __name__ == "__main__": unittest.main()

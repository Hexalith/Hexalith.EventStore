import hashlib
import json
import os
import pathlib
import socket
import subprocess
import sys
import tempfile
import time

lane_name, test_pattern = sys.argv[1:3]
previous = json.loads(pathlib.Path('/tmp/story64-qualification-controls.json').read_text())
work = pathlib.Path(tempfile.mkdtemp(prefix='story64-qualification-controls-'))
commands = previous['commands']
commands[1][commands[1].index('--etcd-data-dir') + 1] = str(work / 'etcd')
ports = [previous['placementPort'], previous['schedulerPort']]
for port in ports:
    with socket.socket() as candidate:
        candidate.settimeout(0.25)
        if candidate.connect_ex(('127.0.0.1', port)) == 0:
            raise RuntimeError('Explicit owned control-plane port is already occupied')
environment = os.environ.copy()
environment.update(EVENTSTORE_TEST_DAPRD_PATH='/tmp/story-6-4-dapr-1.18.2-wpll4ztg/daprd',
                   EVENTSTORE_TEST_PLACEMENT_PORT=str(ports[0]), EVENTSTORE_TEST_SCHEDULER_PORT=str(ports[1]))
configuration = sys.argv[3] if len(sys.argv) > 3 else 'Debug'
if configuration not in ('Debug', 'Release'):
    raise ValueError('Unsupported asset configuration')
command = ['dotnet', 'tests/Hexalith.EventStore.Server.LiveSidecar.Tests/bin/' + configuration + '/net10.0/Hexalith.EventStore.Server.LiveSidecar.Tests.dll']
if test_pattern == '__FOCUSED__':
    for selected in ('*NamedProjectionDispatchLiveSidecarTests', '*ReadModelBatchLiveSidecarTests', '*ProjectionDeliveryCutoverLiveSidecarTests', '*ProjectionEraseLiveSidecarTests'):
        command.extend(('-class', selected))
elif test_pattern != '__FULL__':
    command.extend(('-method', test_pattern))
command.append('-showLiveOutput')
evidence = pathlib.Path('/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/evidence/story-6-4')
log = evidence / ('qualification-20261017-' + lane_name + '.native.log')
command.extend(['-xml', str(evidence / ('qualification-20261017-' + lane_name + '.xml')), '-noColor'])
packet = {'work': str(work), 'commands': commands, 'testCommand': command,
          'placementPort': ports[0], 'schedulerPort': ports[1], 'testExitCode': None,
          'environment': {name: environment[name] for name in ('EVENTSTORE_TEST_DAPRD_PATH', 'EVENTSTORE_TEST_PLACEMENT_PORT', 'EVENTSTORE_TEST_SCHEDULER_PORT', 'EVENTSTORE_TEST_DELIVERY_RECEIPT_LIMIT', 'EVENTSTORE_TEST_DELIVERY_CAS_ATTEMPTS', 'EVENTSTORE_TEST_REDIS_PORT', 'HEXALITH_OQ8_EVIDENCE_DIRECTORY', 'HEXALITH_OQ8_CLEANUP_PATH', 'HEXALITH_OQ8_CONFIGURATION') if name in environment}}
assembly_directory = pathlib.Path(command[1]).parent
packet['assets'] = [{'assembly': item.name, 'bytes': item.stat().st_size,
                     'sha256': hashlib.sha256(item.read_bytes()).hexdigest()}
                    for item in sorted(assembly_directory.glob('Hexalith.EventStore*.dll'))]
processes = []
files = []
try:
    for name, arguments in zip(('placement', 'scheduler'), commands):
        stream = (work / (name + '.log')).open('w')
        files.append(stream)
        processes.append(subprocess.Popen(arguments, stdout=stream, stderr=subprocess.STDOUT))
    ready_until = time.monotonic() + 35
    for port in ports:
        while True:
            if any(process.poll() is not None for process in processes):
                raise RuntimeError('Owned control plane exited before readiness')
            with socket.socket() as candidate:
                candidate.settimeout(0.25)
                if candidate.connect_ex(('127.0.0.1', port)) == 0:
                    break
            if time.monotonic() > ready_until:
                raise TimeoutError('Owned control plane readiness timeout')
            time.sleep(0.15)
    with log.open('w') as output:
        packet['testExitCode'] = subprocess.run(command, env=environment, stdout=output, stderr=subprocess.STDOUT).returncode
except Exception as failure:
    packet['wrapperFailureClass'] = type(failure).__name__
finally:
    for process in processes:
        if process.poll() is None:
            process.terminate()
    for process in processes:
        try:
            process.wait(timeout=20)
        except subprocess.TimeoutExpired:
            process.kill()
            process.wait(timeout=5)
    packet['infrastructureExitCodes'] = [process.returncode for process in processes]
    packet['ownedControlPids'] = [process.pid for process in processes]
    packet['nativeLog'] = str(log)
    packet['nativeLogSha256'] = hashlib.sha256(log.read_bytes()).hexdigest() if log.exists() else None
    packet['binarySha256'] = {str(pathlib.Path(argv[0])): hashlib.sha256(pathlib.Path(argv[0]).read_bytes()).hexdigest() for argv in commands}
    packet['binarySha256'][environment['EVENTSTORE_TEST_DAPRD_PATH']] = hashlib.sha256(pathlib.Path(environment['EVENTSTORE_TEST_DAPRD_PATH']).read_bytes()).hexdigest()
    for stream in files:
        stream.close()
    packet['runnerSha256'] = hashlib.sha256(pathlib.Path(__file__).read_bytes()).hexdigest()
    packet['controlConfigurationSha256'] = hashlib.sha256(pathlib.Path('/tmp/story64-qualification-controls.json').read_bytes()).hexdigest()
    xml_path = evidence / ('qualification-20261017-' + lane_name + '.xml')
    if xml_path.exists():
        packet['nativeXmlSha256'] = hashlib.sha256(xml_path.read_bytes()).hexdigest()
    if log.exists():
        import re
        allowed = {'ProjectionGuardDepthAllocation','ProjectionCost','ProjectionRetryCost','ProjectionSchedulerCost','ProjectionSchedulerMutationCost','ProjectionReplayContext','ProjectionTransitionCapCost','SourceMetadataWriterProfile','ProjectionGuardActivationClosure','ProjectionGuardCatalogColdWarm','ProjectionGuardCurrentMatrix','ProjectionGuardEmptyCost','ProjectionGuardLiveCost','ProjectionGuardNativeAttempts','ProjectionGuardTypedFailure','ProjectionReplayRecoveryAttempt','ProjectionSourceCost'}
        observations = []
        for line in log.read_text(errors='replace').splitlines():
            match = re.search(r'\[OUTPUT\] (\w+) (\{.*\})$', line)
            if match and match[1] in allowed:
                observations.append({'kind':match[1], 'value':json.loads(match[2])})
        vector_path = evidence / ('qualification-20261017-' + lane_name + '-native-vectors.json')
        assert not vector_path.exists()
        vector_path.write_text(json.dumps({'sourceSnapshot':'qualification-20261017-'+lane_name,'exitCode':packet['testExitCode'],'command':command,'observations':observations}, indent=2)+'\n')
        packet['nativeVectors'] = {'path':str(vector_path),'sha256':hashlib.sha256(vector_path.read_bytes()).hexdigest()}
    packet_path = evidence / ('qualification-20261017-' + lane_name + '-controls.json')
    assert not packet_path.exists()
    packet_path.write_text(json.dumps(packet, indent=2) + '\n')
if log.exists():
    for line in log.read_text(errors='replace').splitlines():
        if ('Total:' in line or '[SKIP]' in line or '[FAIL]' in line):
            print(line)
print(json.dumps({'lane': lane_name, 'testExitCode': packet['testExitCode'], 'infrastructureExitCodes': packet['infrastructureExitCodes'], 'log': str(log)}))
sys.exit(packet['testExitCode'] if packet['testExitCode'] is not None else 1)

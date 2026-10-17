"""Adapted finite native capture; all controls and containers are private to one lane."""
import datetime, hashlib, json, os, pathlib, signal, socket, subprocess, sys, tempfile, threading, time

ROOT = pathlib.Path('/home/administrator/projects/hexalith/eventstore')
EVIDENCE = ROOT / '_bmad-output/implementation-artifacts/evidence/story-6-4/runtime-corrections-20261017'
lane, pattern, configuration = sys.argv[1:4]
assert configuration in ('Debug', 'Release')
target = EVIDENCE / (lane + '-controls.json')
assert not target.exists()
assert not (EVIDENCE/(lane+'.native.log')).exists()
assert not (EVIDENCE/(lane+'.xml')).exists()
work = pathlib.Path(tempfile.mkdtemp(prefix='story64-runtime-' + lane + '-'))
regular = pathlib.Path('/tmp/story-6-4-dapr-1.18.2-wpll4ztg')
oq8_daprd = pathlib.Path('/home/administrator/.dapr/bin/daprd')
token = 'g6-oq8-rc-' + os.urandom(5).hex()
packet = {'lane': lane, 'configuration': configuration, 'work': str(work), 'commands': [], 'containers': [],
          'processes': [], 'testExitCode': None, 'profile': 'test-local HotReload=false; OQ8 private discovery; no production-profile qualification'}
containers, controls, streams, observed = [], [], [], {}
test = None
monitor_done = threading.Event()

def sha(path): return hashlib.sha256(pathlib.Path(path).read_bytes()).hexdigest()
def command(args):
    packet['commands'].append(args)
    return subprocess.check_output(args, text=True, stderr=subprocess.STDOUT).strip()
def ports(count):
    handles = [socket.socket() for _ in range(count)]
    try:
        for handle in handles: handle.bind(('127.0.0.1', 0))
        return [handle.getsockname()[1] for handle in handles]
    finally:
        for handle in handles: handle.close()
def wait_port(port):
    until = time.monotonic() + 40
    while time.monotonic() < until:
        if any(process.poll() is not None for process in controls): raise RuntimeError('private control exited')
        with socket.socket() as handle:
            handle.settimeout(.2)
            if handle.connect_ex(('127.0.0.1', port)) == 0: return
        time.sleep(.1)
    raise TimeoutError('private control readiness')
def start_container(name, image, arguments, published):
    argv = ['docker', 'run', '--rm', '-d', '--name', name]
    for port in published: argv += ['-p', '127.0.0.1::' + str(port)]
    argv += ['--entrypoint', arguments[0], image] + arguments[1:]
    identity = command(argv)
    containers.append(identity)
    details = json.loads(command(['docker', 'inspect', identity]))[0]
    packet['containers'].append({'id': identity, 'name': name, 'image': details['Image'],
        'imageReference': image, 'mounts': details['Mounts'], 'ports': details['NetworkSettings']['Ports']})
    return {port: int(details['NetworkSettings']['Ports'][str(port) + '/tcp'][0]['HostPort']) for port in published}
def process_table():
    table = {}
    for entry in pathlib.Path('/proc').iterdir():
        if not entry.name.isdigit(): continue
        try:
            raw = (entry/'stat').read_text(); tail = raw[raw.rindex(')')+2:].split()
            table[int(entry.name)] = (int(tail[1]), tail[19], entry)
        except (OSError, ValueError, IndexError): pass
    return table
def monitor():
    # Save only allowlisted startup arguments and configuration hashes. Never read environments
    # or copy generated PostgreSQL components (which contain the private disposable password).
    allowed = {'--app-id', '--config', '--resources-path', '--app-port', '--dapr-http-port', '--dapr-grpc-port',
               '--dapr-internal-grpc-port', '--metrics-port', '--profile-port', '--placement-host-address', '--scheduler-host-address'}
    while not monitor_done.wait(.1):
        if test is None: continue
        table = process_table(); owned = {test.pid}
        for _ in range(8): owned.update(pid for pid,(ppid,_,_) in table.items() if ppid in owned)
        for pid in owned:
            if pid not in table: continue
            ppid, started, entry = table[pid]; key = str(pid) + ':' + started
            if key in observed: continue
            try:
                args = (entry/'cmdline').read_bytes().split(b'\0'); args = [a.decode(errors='replace') for a in args if a]
                executable = str((entry/'exe').resolve())
                item = {'pid': pid, 'ppid': ppid, 'startTicks': started, 'executable': executable,
                        'executableSha256': sha(executable), 'safeArguments': [], 'configurations': []}
                for index, arg in enumerate(args):
                    if arg in allowed and index+1 < len(args):
                        value = args[index+1]; item['safeArguments'] += [arg, value]
                        if arg == '--config' and pathlib.Path(value).is_file():
                            body = pathlib.Path(value).read_bytes()
                            item['configurations'].append({'path': value, 'sha256': hashlib.sha256(body).hexdigest(),
                                'hotReloadDisabled': b'name: HotReload' in body and b'enabled: false' in body})
                            if pathlib.Path(value).name == 'test-runtime.yaml':
                                copy = EVIDENCE / (lane + '-regular-runtime.yaml')
                                if not copy.exists(): copy.write_bytes(body)
                    elif arg.endswith('.dll') and pathlib.Path(arg).is_file():
                        item.setdefault('entryAssemblies', []).append({'path': arg, 'sha256': sha(arg)})
                observed[key] = item
            except (OSError, ValueError): pass

thread = threading.Thread(target=monitor, daemon=True)
try:
    placement_port, scheduler_port, ph, pm, peer, sh, sm, etcd_client, etcd_peer = ports(9)
    commands = [
        [str(regular/'placement'), '--id', 'story64-rc-placement', '--port', str(placement_port), '--listen-address', '127.0.0.1',
         '--healthz-port', str(ph), '--metrics-port', str(pm), '--initial-cluster', 'story64-rc-placement=127.0.0.1:' + str(peer)],
        [str(regular/'scheduler'), '--id', 'story64-rc-scheduler', '--port', str(scheduler_port), '--listen-address', '127.0.0.1',
         '--healthz-port', str(sh), '--metrics-port', str(sm), '--etcd-client-port', str(etcd_client),
         '--etcd-initial-cluster', 'story64-rc-scheduler=http://127.0.0.1:' + str(etcd_peer), '--etcd-data-dir', str(work/'etcd')]]
    for role, args in zip(('placement', 'scheduler'), commands):
        stream = (work/(role+'.log')).open('w'); streams.append(stream)
        process = subprocess.Popen(args, stdout=stream, stderr=subprocess.STDOUT)
        controls.append(process); packet['commands'].append(args)
        packet['processes'].append({'role': role, 'pid': process.pid, 'binarySha256': sha(args[0])})
    wait_port(placement_port); wait_port(scheduler_port)
    redis = start_container(token+'-redis', 'redis:6', ['redis-server', '--appendonly', 'no'], [6379])
    wait_port(redis[6379])
    redis_identity = containers[-1]
    assert packet['containers'][-1]['image'] == 'sha256:c35b83ce044bb6d148c484d36e059ad28e02d5714ba6731fb55b6421e2ed0ccf'
    assert command(['docker', 'exec', redis_identity, 'redis-cli', 'CONFIG', 'SET', 'save', '3600 1 300 100 60 10000']) == 'OK'
    packet['redisConfiguration'] = {
        'version': command(['docker', 'exec', redis_identity, 'redis-server', '--version']),
        'save': command(['docker', 'exec', redis_identity, 'redis-cli', '--raw', 'CONFIG', 'GET', 'save']),
        'appendonly': command(['docker', 'exec', redis_identity, 'redis-cli', '--raw', 'CONFIG', 'GET', 'appendonly'])}
    assert packet['redisConfiguration']['save'] == 'save\n3600 1 300 100 60 10000'
    assert packet['redisConfiguration']['appendonly'] == 'appendonly\nno'

    oq8_placement = start_container(token+'-placement', 'daprio/dapr:1.18.7', ['./placement'], [50005])
    oq8_scheduler = start_container(token+'-scheduler', 'daprio/dapr:1.18.7', ['./scheduler', '--etcd-data-dir=/var/lock/dapr/scheduler',
        '--etcd-client-listen-address=0.0.0.0'], [50006])
    wait_port(oq8_placement[50005]); wait_port(oq8_scheduler[50006])
    env = os.environ.copy()
    env.update(EVENTSTORE_TEST_DAPRD_PATH=str(regular/'daprd'), EVENTSTORE_TEST_PLACEMENT_PORT=str(placement_port),
        EVENTSTORE_TEST_SCHEDULER_PORT=str(scheduler_port), EVENTSTORE_TEST_REDIS_PORT=str(redis[6379]), EVENTSTORE_TEST_DAPR_HOT_RELOAD='false',
        HEXALITH_OQ8_DAPRD_PATH=str(oq8_daprd), HEXALITH_OQ8_CONFIGURATION='Release',
        HEXALITH_OQ8_PLACEMENT_CONTAINER=token+'-placement', HEXALITH_OQ8_SCHEDULER_CONTAINER=token+'-scheduler',
        HEXALITH_OQ8_REDIS_ENDPOINT='127.0.0.1:'+str(redis[6379]), HEXALITH_OQ8_NAMESPACE=token)
    oq8_evidence = EVIDENCE/(lane+'-oq8'); assert not oq8_evidence.exists()
    env.update(HEXALITH_OQ8_EVIDENCE_DIRECTORY=str(oq8_evidence), HEXALITH_OQ8_CLEANUP_PATH=str(EVIDENCE/(lane+'-oq8-cleanup.json')))
    env['HEXALITH_OQ8_DIAGNOSTICS_PATH'] = str(EVIDENCE/(lane+'-oq8-diagnostics.json'))
    packet['producerBindings'] = {}
    for role, packet_name, prefixes in [
        ('entryHosts', 'live-release-build.json', ['src/Hexalith.EventStore/bin/Release/net10.0/', 'samples/Hexalith.EventStore.Sample/bin/Release/net10.0/']),
        ('testAssembly', 'live-debug-build-2.json' if configuration == 'Debug' else 'live-release-build.json', ['tests/Hexalith.EventStore.Server.LiveSidecar.Tests/bin/'+configuration+'/net10.0/'])]:
        producer_file = EVIDENCE/packet_name
        producer = json.loads(producer_file.read_text())
        assert producer['exitCode'] == 0
        expected = {name:digest for name,digest in producer['after']['runtime'].items() if any(name.startswith(prefix) for prefix in prefixes)}
        assert expected
        assert all((ROOT/name).exists() and sha(ROOT/name)==digest for name,digest in expected.items())
        packet['producerBindings'][role] = {'path': str(producer_file), 'sha256': sha(producer_file), 'consumedOutputs': expected}
    packet['environment'] = {k:v for k,v in env.items() if k.startswith(('EVENTSTORE_TEST_', 'HEXALITH_OQ8_'))}
    packet['runtimeVersions'] = {'regular': command([str(regular/'daprd'), '--version']), 'oq8': command([str(oq8_daprd), '--version'])}
    packet['sidecarBinaries'] = {str(p): sha(p) for p in (regular/'daprd', oq8_daprd)}
    assembly = ROOT/'tests/Hexalith.EventStore.Server.LiveSidecar.Tests/bin'/configuration/'net10.0/Hexalith.EventStore.Server.LiveSidecar.Tests.dll'
    native = ['dotnet', str(assembly)]
    if pattern == '__FOCUSED__':
        for method in ('*SourceGuard_TwoRealReplicas*', '*MultiHostAdmission_PrimaryHostRemoved*', '*TwoSidecarsAndRestartPreserve*',
                       '*ProductionMatrix_IndependentProcesses*'):
            native += ['-method', method]
    elif pattern == '__OQ8__': native += ['-method', '*ProductionMatrix_IndependentProcesses*']
    elif pattern != '__FULL__': native += ['-class', pattern]
    native += ['-showLiveOutput', '-noColor', '-xml', str(EVIDENCE/(lane+'.xml'))]
    packet['testCommand'] = native
    # Persist startup ownership immediately, for safe recovery even if the wrapper is interrupted.
    (EVIDENCE/(lane+'-active-controls.json')).write_text(json.dumps(packet, indent=2)+'\n')
    thread.start()
    with (EVIDENCE/(lane+'.native.log')).open('w') as log:
        test = subprocess.Popen(native, cwd=ROOT, env=env, stdout=log, stderr=subprocess.STDOUT, start_new_session=True)
        packet['testPid'] = test.pid
        packet['testExitCode'] = test.wait()
except Exception as failure:
    packet['wrapperFailureClass'] = type(failure).__name__
finally:
    monitor_done.set()
    if thread.is_alive(): thread.join(timeout=3)
    packet['observedChildren'] = list(observed.values())
    current = process_table()
    alive = [item for item in observed.values() if item['pid'] in current and current[item['pid']][1] == item['startTicks']]
    packet['ownedChildrenAliveAfterNative'] = alive
    for item in alive:
        try: os.kill(item['pid'], signal.SIGKILL)
        except ProcessLookupError: pass
    for process in controls:
        if process.poll() is None: process.terminate()
    for process in controls:
        try: process.wait(timeout=15)
        except subprocess.TimeoutExpired: process.kill(); process.wait(timeout=5)
    packet['controlExitCodes'] = [p.returncode for p in controls]
    for stream in streams: stream.close()
    for role in ('placement', 'scheduler'):
        source = work/(role+'.log')
        if source.exists(): (EVIDENCE/(lane+'-'+role+'.log')).write_bytes(source.read_bytes())
    for identity in reversed(containers):
        result = subprocess.run(['docker', 'rm', '-f', '-v', identity], stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True)
        packet.setdefault('containerCleanup', []).append({'id': identity, 'exitCode': result.returncode})
    packet['containerPhysicalAbsence'] = {identity: subprocess.run(['docker','inspect',identity], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL).returncode != 0 for identity in containers}
    native_log = EVIDENCE/(lane+'.native.log')
    if native_log.exists():
        import re
        observations = []
        for line in native_log.read_text(errors='replace').splitlines():
            match = re.search(r'\[OUTPUT\] (\w+) (\{.*\})$', line)
            if match and match[1].startswith(('Projection', 'SourceMetadata', 'SourceCost')):
                observations.append({'kind': match[1], 'value': json.loads(match[2])})
            if 'Total:' in line: print(line)
        vectors = EVIDENCE/(lane+'-vectors.json')
        assert not vectors.exists()
        vectors.write_text(json.dumps({'command': packet.get('testCommand'), 'exitCode': packet['testExitCode'], 'observations': observations}, indent=2)+'\n')
        packet['vectors'] = {'path': str(vectors), 'sha256': sha(vectors)}
        packet['nativeLogSha256'] = sha(native_log)
    packet['volumePhysicalAbsence'] = {mount['Name']: subprocess.run(['docker','volume','inspect',mount['Name']], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL).returncode != 0
        for container in packet['containers'] for mount in container['mounts'] if mount['Type']=='volume'}
    packet['resourceDirectoriesRemoved'] = {item['safeArguments'][i+1]: not pathlib.Path(item['safeArguments'][i+1]).exists()
        for item in observed.values() for i,arg in enumerate(item['safeArguments']) if arg=='--resources-path'}
    checked_ports = {int(item['HostPort']) for container in packet['containers'] for group in container['ports'].values() if group for item in group}
    checked_ports.update([placement_port, scheduler_port, ph, pm, peer, sh, sm, etcd_client, etcd_peer])
    packet['physicalPortsClosed'] = {}
    for port in checked_ports:
        with socket.socket() as check:
            check.settimeout(.2)
            packet['physicalPortsClosed'][str(port)] = check.connect_ex(('127.0.0.1',port)) != 0
    import shutil
    shutil.rmtree(work)
    packet['scratchRemoved'] = not work.exists()
    packet['runnerSha256'] = sha(__file__)
    packet['completedAt'] = datetime.datetime.now(datetime.timezone.utc).isoformat()
    target.write_text(json.dumps(packet, indent=2)+'\n')
    active = EVIDENCE/(lane+'-active-controls.json')
    if active.exists(): active.unlink()
print(json.dumps({'lane': lane, 'testExitCode': packet['testExitCode'], 'wrapperFailureClass': packet.get('wrapperFailureClass'),
                  'liveChildrenAfterNative': len(packet.get('ownedChildrenAliveAfterNative', [])), 'controls': str(target)}), flush=True)
sys.exit(packet['testExitCode'] if packet['testExitCode'] is not None else 1)

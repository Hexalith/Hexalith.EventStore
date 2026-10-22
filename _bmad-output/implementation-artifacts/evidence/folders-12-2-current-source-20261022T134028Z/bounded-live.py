#!/usr/bin/env python3
"""Run unchanged bounded process tests on privately owned Dapr infrastructure."""
import hashlib
import importlib.util
import json
import os
import signal
from pathlib import Path
import subprocess
import sys
import time
from types import SimpleNamespace

packet = Path(__file__).resolve().parent
root = packet.parents[3]
spec = importlib.util.spec_from_file_location('admission_infrastructure', root / 'tools/qualify-internal-actor-admission.py')
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)
owned = module.Qualification(SimpleNamespace(evidence=str(packet / 'bounded-infrastructure')))
observations = {}
processes = {}
resources = {}
observation_errors = []
process = None


def cancel(signum, frame):
    raise KeyboardInterrupt('Bounded qualification canceled by signal ' + str(signum))


signal.signal(signal.SIGINT, cancel)
signal.signal(signal.SIGTERM, cancel)


def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def observe(parent):
    table = {}
    for directory in Path('/proc').glob('[0-9]*'):
        try:
            fields = (directory / 'stat').read_text().rsplit(')', 1)[1].split()
            table[int(directory.name)] = (int(fields[1]), fields[19])
        except (OSError, ValueError, IndexError):
            continue
    selected = {parent}
    while True:
        extra = {pid for pid, (ppid, _) in table.items() if ppid in selected}
        if extra.issubset(selected):
            break
        selected.update(extra)
    for pid in selected:
        proc = Path('/proc') / str(pid)
        try:
            argv = (proc / 'cmdline').read_bytes().decode().strip('\0').split('\0')
            identity = str(pid) + ':' + table[pid][1]
            if identity not in processes:
                processes[identity] = {'pid': pid, 'start_ticks': table[pid][1], 'argv': argv, 'exe': str((proc / 'exe').resolve()), 'exe_sha256': sha(proc / 'exe')}
            if '--resources-path' in argv:
                directory = Path(argv[argv.index('--resources-path') + 1])
                if str(directory) not in resources and directory.is_dir():
                    resources[str(directory)] = {str(p): {'sha256': sha(p), 'content': p.read_text()} for p in directory.glob('*.yaml')}
            for line in (proc / 'maps').read_text().splitlines():
                path = line.split()[-1]
                if path.startswith('/') and path.endswith('.dll') and ('Hexalith.' in path or Path(path).name.startswith(('Dapr.', 'Ulid.'))):
                    key = identity + ':' + path
                    if key not in observations:
                        pdb = Path(path).with_suffix('.pdb')
                        observations[key] = {'pid': pid, 'start_ticks': table[pid][1], 'path': path, 'dll_sha256': sha(path), 'pdb_sha256': sha(pdb) if pdb.exists() else None}
        except (OSError, ValueError, KeyError, UnicodeError) as error:
            observation_errors.append({'pid': pid, 'error': repr(error), 'observed_at': time.time()})
            continue


result = None
cleanup = False
try:
    _, redis_port = owned.launch_container('redis', 'redis:7-alpine', 6379)
    _, placement_port = owned.launch_container('placement', 'daprio/dapr:1.18.7', 50005, '--port', '50005')
    scheduler_port = module.port()
    owned.launch_container('scheduler', 'daprio/dapr:1.18.7', 50006,
                           '--etcd-data-dir=/tmp/bounded-etcd',
                           '--override-broadcast-host-port=127.0.0.1:' + str(scheduler_port),
                           '--etcd-client-listen-address=0.0.0.0', host_port=scheduler_port)
    env = dict(os.environ, EVENTSTORE_TEST_REDIS_PORT=str(redis_port), EVENTSTORE_TEST_PLACEMENT_PORT=str(placement_port),
               EVENTSTORE_TEST_SCHEDULER_PORT=str(scheduler_port), EVENTSTORE_TEST_DAPR_HOT_RELOAD='false',
               DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE='false', DOTNET_USE_POLLING_FILE_WATCHER='1',
               BOUNDED_IDENTITY_EVIDENCE_DIR=str(packet / 'bounded-scenarios'))
    owned.save('selection.json', {'environment': {k: v for k, v in env.items() if k.startswith(('EVENTSTORE_TEST_', 'BOUNDED_IDENTITY_', 'DOTNET_HOSTBUILDER__RELOAD', 'DOTNET_USE_POLLING_'))},
               'daprd': str(Path.home() / '.dapr/bin/daprd'), 'daprd_sha256': sha(Path.home() / '.dapr/bin/daprd')})
    (packet / 'bounded-scenarios').mkdir()
    argv = [sys.executable, str(packet / 'run.py'), 'bounded-live', 'dotnet', str(root / 'tests/Hexalith.EventStore.Server.LiveSidecar.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.LiveSidecar.Tests.dll'),
            '-class', 'Hexalith.EventStore.Server.LiveSidecar.Tests.Integration.ProjectionBoundedIdentityLiveTests', '-showLiveOutput', '-result-xml', str(packet / 'bounded-live.xml')]
    process = subprocess.Popen(argv, cwd=root, env=env)
    while process.poll() is None:
        observe(process.pid)
        time.sleep(0.2)
    result = process.returncode
finally:
    signal.signal(signal.SIGINT, signal.SIG_IGN)
    signal.signal(signal.SIGTERM, signal.SIG_IGN)
    if process is not None and process.poll() is None:
        process.send_signal(signal.SIGINT)
        try:
            process.wait(timeout=30)
        except subprocess.TimeoutExpired:
            process.kill()
            process.wait()
    # Any observer-identified descendants surviving runner cancellation are owned by
    # their exact PID/start identity. Recheck identity before each signal.
    if result is None:
        for record in processes.values():
            try:
                stat = Path('/proc') / str(record['pid']) / 'stat'
                if stat.read_text().rsplit(')', 1)[1].split()[19] == record['start_ticks']:
                    os.kill(record['pid'], signal.SIGTERM)
            except (OSError, ProcessLookupError):
                pass
    owned.save('observed-processes.json', processes)
    owned.save('mapped-images.json', list(observations.values()))
    owned.save('components.json', resources)
    owned.save('observation-errors.json', observation_errors)
    cleanup = owned.cleanup()
    remaining = []
    for record in processes.values():
        stat = Path('/proc') / str(record['pid']) / 'stat'
        try:
            current = stat.read_text().rsplit(')', 1)[1].split()[19]
            if current == record['start_ticks']:
                remaining.append(record)
        except OSError:
            pass
    directories = {path: not Path(path).exists() for path in resources}
    coverage_present = bool(processes) and bool(observations) and bool(resources)
    observed_cleanup = coverage_present and not remaining and all(directories.values())
    owned.save('observed-cleanup.json', {'remaining_processes': remaining, 'component_directories_absent': directories, 'coverage_present': coverage_present, 'passed': observed_cleanup})
    owned.save('result.json', {'test_exit_code': result, 'owned_infrastructure_cleanup': cleanup, 'observed_cleanup': observed_cleanup})
sys.exit(0 if result == 0 and cleanup and observed_cleanup else 1)

#!/usr/bin/env python3
"""Run source-only P1R lanes on invocation-owned infrastructure and preserve receipts."""
import datetime
import hashlib
import json
import os
import re
from pathlib import Path
import signal
import socket
import subprocess
import sys
import time
import uuid

ROOT = Path(__file__).resolve().parents[4]
INVOCATION = uuid.uuid4().hex
OUTPUT = Path(__file__).resolve().parent / 'receipts' / ('live-attempt-' + INVOCATION[:8])
LABEL = 'hexalith.p1r.remediation=' + INVOCATION
COMMANDS = []
OWNED = []
GROUPS = []


def stamp():
    return datetime.datetime.now(datetime.timezone.utc).isoformat()


def command(argv, name=None, env=None, timeout=120):
    started = stamp()
    try:
        process = subprocess.Popen(argv, cwd=ROOT, env=env, stdout=subprocess.PIPE,
                                   stderr=subprocess.STDOUT, text=True, start_new_session=True)
    except Exception as failure:
        output = type(failure).__name__ + ': ' + str(failure)
        receipt = {'argv': argv, 'cwd': str(ROOT), 'started_utc': started,
                   'finished_utc': stamp(), 'exit_code': None, 'launch_error': output,
                   'output_sha256': hashlib.sha256(output.encode()).hexdigest()}
        COMMANDS.append(receipt)
        if name:
            (OUTPUT / (name + '.log')).write_text(output)
            (OUTPUT / (name + '.json')).write_text(json.dumps(receipt, indent=2) + '\n')
        raise
    if argv[:1] == ['dotnet']:
        GROUPS.append(process.pid)
    timed_out = False
    try:
        output, _ = process.communicate(timeout=timeout)
    except subprocess.TimeoutExpired:
        os.killpg(process.pid, signal.SIGTERM)
        try:
            output, _ = process.communicate(timeout=10)
        except subprocess.TimeoutExpired:
            os.killpg(process.pid, signal.SIGKILL)
            output, _ = process.communicate()
        timed_out = True
    receipt = {'argv': argv, 'cwd': str(ROOT), 'started_utc': started,
               'finished_utc': stamp(), 'exit_code': process.returncode, 'timed_out': timed_out,
               'output_sha256': hashlib.sha256(output.encode()).hexdigest()}
    COMMANDS.append(receipt)
    if name:
        (OUTPUT / (name + '.log')).write_text(output)
        (OUTPUT / (name + '.json')).write_text(json.dumps(receipt, indent=2) + '\n')
        print(name + ': ' + str(process.returncode), flush=True)
        print('\n'.join(output.splitlines()[-4:]), flush=True)
    if timed_out:
        raise RuntimeError('Owned command exceeded its deadline: ' + argv[0])
    if process.returncode:
        raise RuntimeError('Command failed with exit ' + str(process.returncode) + ': ' + repr(argv))
    return output.strip()


def resources(name_prefix=None):
    ids = command(['docker', 'ps', '-aq', '--no-trunc'],
                  name_prefix + '-discover' if name_prefix else None).split()
    if not ids:
        return {}
    # Retain only safe resource identity/state fields, never full Docker environments.
    projection = '{"Id":{{json .Id}},"Name":{{json .Name}},"Image":{{json .Image}},"State":{"Running":{{json .State.Running}},"StartedAt":{{json .State.StartedAt}}}}'
    output = command(['docker', 'inspect', '--format', projection, *ids],
                     name_prefix + '-inspect' if name_prefix else None)
    raw = [json.loads(line) for line in output.splitlines()]
    return {r['Id']: {'name': r['Name'], 'image': r['Image'],
                     'running': r['State']['Running'], 'started_at': r['State']['StartedAt']}
            for r in raw}


def free_port():
    with socket.socket() as sock:
        sock.bind(('127.0.0.1', 0))
        return sock.getsockname()[1]


def container(role, image, port, args, host_port=None):
    name = 'p1r-remediation-' + INVOCATION + '-' + role
    argv = ['docker', 'run', '--rm', '-d', '--name', name, '--label', LABEL,
            '-p', '127.0.0.1:' + (str(host_port) if host_port else '') + ':' + str(port),
            '--entrypoint', args[0], image, *args[1:]]
    # Record the intended exact owned name before launch to permit cleanup if launch acknowledgement fails.
    OWNED.append(name)
    identity = command(argv)
    binding = json.loads(command(['docker', 'inspect', identity]))[0]['NetworkSettings']['Ports'][str(port) + '/tcp']
    actual_port = int(binding[0]['HostPort'])
    deadline = time.monotonic() + 45
    while time.monotonic() < deadline:
        try:
            with socket.create_connection(('127.0.0.1', actual_port), timeout=1):
                return actual_port
        except OSError:
            time.sleep(.2)
    raise RuntimeError('Owned infrastructure did not listen: ' + role)


def live_processes():
    found = []
    for entry in Path('/proc').iterdir():
        if not entry.name.isdigit():
            continue
        try:
            if os.getpgid(int(entry.name)) in GROUPS:
                found.append(int(entry.name))
        except ProcessLookupError:
            pass
    return found


def cleanup(before, error, runtime_version=None, identities=None):
    cleanup_errors = []
    owned_labels = {}

    def failed(stage, failure, resource=None):
        cleanup_errors.append({'stage': stage, 'resource': resource,
                               'error': type(failure).__name__ + ': ' + str(failure)})

    # Every process/resource is attempted independently; a failed discovery never grants cleanup success.
    remaining_groups = set()
    try:
        for pid in live_processes():
            try:
                group = os.getpgid(pid)
                if group in GROUPS:
                    remaining_groups.add(group)
            except ProcessLookupError:
                pass
            except Exception as failure:
                failed('process-group-discovery', failure, pid)
    except Exception as failure:
        failed('process-discovery', failure)
    for group in remaining_groups:
        try:
            os.killpg(group, signal.SIGKILL)
        except ProcessLookupError:
            pass
        except Exception as failure:
            failed('process-stop', failure, group)

    for index, name in enumerate(reversed(OWNED)):
        stage = 'container-discovery'
        try:
            existing = command(['docker', 'ps', '-aq', '--no-trunc', '--filter', 'name=^/' + name + '$'],
                               'cleanup-' + str(index) + '-discover')
            if not existing:
                continue
            stage = 'container-ownership'
            labels = json.loads(command(['docker', 'inspect', '--format', '{{json .Config.Labels}}', existing],
                                        'cleanup-' + str(index) + '-inspect'))
            owned_labels[name] = labels
            if not isinstance(labels, dict) or labels.get('hexalith.p1r.remediation') != INVOCATION:
                raise RuntimeError('Owned name no longer carries this invocation label')
            stage = 'container-remove'
            command(['docker', 'rm', '-f', existing], 'cleanup-' + str(index) + '-remove')
        except Exception as failure:
            failed(stage, failure, name)

    after = None
    remaining_containers = None
    remaining_processes = None
    try:
        after = resources('final-inventory')
    except Exception as failure:
        failed('final-resource-inventory', failure)
    try:
        remaining_containers = command(['docker', 'ps', '-aq', '--no-trunc', '--filter', 'label=' + LABEL],
                                       'final-owned-container-inventory').split()
    except Exception as failure:
        failed('final-owned-container-inventory', failure)
    try:
        remaining_processes = live_processes()
    except Exception as failure:
        failed('final-owned-process-inventory', failure)
    shared_preserved = before is not None and after is not None and all(
        after.get(key) == value for key, value in before.items())
    summary = {'invocation': INVOCATION, 'ownership_label': LABEL,
               'owned_resource_names': list(OWNED), 'owned_resource_labels': owned_labels,
               'started_inputs': before, 'finished_inputs': after,
               'owned_resource_identities': identities or {}, 'runtime': runtime_version,
               'source_configuration': 'Debug/project-references', 'shared_preserved': shared_preserved,
               'owned_containers_remaining': remaining_containers, 'owned_processes_remaining': remaining_processes,
               'commands': COMMANDS, 'error': error, 'cleanup_errors': cleanup_errors,
               'qualification': 'source-only; published candidate/rollback pending'}
    summary['exit_code'] = int(bool(error or cleanup_errors or remaining_containers is None
                                   or remaining_processes is None or remaining_containers
                                   or remaining_processes or not shared_preserved))
    (OUTPUT / 'live-runtime-and-cleanup.json').write_text(json.dumps(summary, indent=2) + '\n')
    return summary


def main():
    OUTPUT.mkdir(parents=True, exist_ok=True)
    before = None
    error = None
    runtime_version = None
    identities = {}
    try:
        before = resources('initial-inventory')
        runtime_version = command([str(Path.home() / '.dapr/bin/daprd'), '--version']).splitlines()[0]
        runtime_image = 'daprio/dapr:1.18.4'
        redis = container('redis', 'redis:6', 6379, ['redis-server', '--appendonly', 'yes'])
        placement = container('placement', runtime_image, 50005, ['./placement', '--port', '50005'])
        scheduler_port = free_port()
        scheduler = container('scheduler', runtime_image, 50006,
                              ['./scheduler', '--port', '50006', '--etcd-data-dir', '/tmp/p1r-scheduler',
                               '--etcd-client-listen-address', '0.0.0.0',
                               '--override-broadcast-host-port', '127.0.0.1:' + str(scheduler_port)], scheduler_port)
        identities = resources()
        env = dict(os.environ, EVENTSTORE_TEST_REDIS_PORT=str(redis),
                   EVENTSTORE_TEST_PLACEMENT_PORT=str(placement),
                   EVENTSTORE_TEST_SCHEDULER_PORT=str(scheduler), EVENTSTORE_TEST_DAPR_HOT_RELOAD='false')
        assembly = 'tests/Hexalith.EventStore.Server.LiveSidecar.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.LiveSidecar.Tests.dll'
        lanes = [('live-persistence', ['*P1RRemediationPersistenceTests', '*TrustedEffectPersistenceTests']),
                 ('live-reminder', ['*ReminderRecoveryLiveSidecarTests']),
                 ('live-evolution', ['*DaprEventEvolutionLogicalReadbackLiveSidecarTests'])]
        for name, classes in lanes:
            args = ['dotnet', assembly, '-parallelMode', 'none', '-showLiveOutput']
            for cls in classes:
                args += ['-class', cls]
            output = command(args, name, env, timeout=600)
            summary = re.search(r'Total: (\d+), Errors: (\d+), Failed: (\d+), Skipped: (\d+), Not Run: (\d+)', output)
            expected = 3 if name == 'live-persistence' else 1
            if not summary or tuple(map(int, summary.groups())) != (expected, 0, 0, 0, 0):
                raise RuntimeError('Live lane did not execute its full required test inventory: ' + name)
    except (Exception, KeyboardInterrupt) as failure:
        error = type(failure).__name__ + ': ' + str(failure)
    finally:
        summary = cleanup(before, error, runtime_version, identities)
    if summary['exit_code']:
        print(json.dumps({'error': error, 'cleanup_errors': summary['cleanup_errors'],
                          'shared_preserved': summary['shared_preserved'],
                          'owned_containers_remaining': summary['owned_containers_remaining'],
                          'owned_processes_remaining': summary['owned_processes_remaining']}))
    else:
        print('Owned infrastructure cleaned; shared resources unchanged.', flush=True)
    return summary['exit_code']


if __name__ == '__main__':
    sys.exit(main())

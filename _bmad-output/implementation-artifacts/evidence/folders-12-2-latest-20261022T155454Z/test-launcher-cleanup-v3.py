#!/usr/bin/env python3
"""Fresh owned-infrastructure controls; every driver exit tears down its launcher."""
from pathlib import Path
import datetime
import json
import os
import signal
import subprocess
import sys
import time

packet = Path(__file__).resolve().parent
root = Path(os.environ['EVENTSTORE_QUALIFICATION_SOURCE_ROOT'])
target = packet / 'launcher-cleanup-v3-result.json'
if target.exists():
    raise SystemExit('Refusing to overwrite evidence')
results = []
for label, control in [('malformed-capture', 'malformed-capture'), ('write-failure', 'write-failure'), ('cancel', 'cancel'), ('runner-cancel', 'runner-cancel'), ('runner-crash', 'runner-crash'), ('driver-error', 'cancel')]:
    directory = packet / 'cleanup-controls-v3' / label
    if directory.exists():
        raise SystemExit('Refusing existing control directory')
    env = dict(os.environ, BOUNDED_LAUNCHER_PACKET=str(directory), BOUNDED_LAUNCHER_CONTROL=control)
    argv = [sys.executable, str(packet / 'run.py'), 'cleanup-v3-' + label, sys.executable, str(packet / 'bounded-live-v2.py')]
    started = datetime.datetime.now(datetime.timezone.utc).isoformat()
    process = subprocess.Popen(argv, cwd=root, env=env, start_new_session=True)
    driver_error = None
    signals = []
    try:
        if control in ('cancel', 'runner-cancel', 'runner-crash'):
            deadline = time.monotonic() + 30
            while not (directory / 'control-ready').exists() and process.poll() is None and time.monotonic() < deadline:
                time.sleep(0.1)
            if not (directory / 'control-ready').exists():
                raise RuntimeError('Control did not become ready')
            selected = json.loads((directory / 'control-selection.json').read_text())
            ticks = Path('/proc', str(selected['pid']), 'stat').read_text().rsplit(')', 1)[1].split()[19]
            if ticks != selected['start_ticks']:
                raise RuntimeError('Control launcher identity changed')
            if label == 'driver-error':
                raise RuntimeError('Intentional driver failure after readiness')
            if control == 'runner-crash':
                (directory / 'crash-trigger').touch()
            else:
                os.kill(selected['pid'], signal.SIGTERM)
                signals.append({'pid': selected['pid'], 'start_ticks': ticks, 'signal': 'SIGTERM'})
        process.wait(timeout=60)
    except Exception as error:
        driver_error = repr(error)
    finally:
        # The launcher receives cancellation and runs its own unconditional cleanup.
        # Its private runner session is independently closed by the launcher.
        if process.poll() is None:
            try:
                os.killpg(process.pid, signal.SIGINT)
                signals.append({'owned_wrapper_group': process.pid, 'signal': 'SIGINT'})
                process.wait(timeout=60)
            except subprocess.TimeoutExpired:
                os.killpg(process.pid, signal.SIGKILL)
                process.wait(timeout=10)
        code = process.returncode
    errors = []
    if driver_error and not (label == 'driver-error' and 'Intentional driver failure' in driver_error):
        errors.append(driver_error)
    cleanup_path = directory / 'bounded-infrastructure/cleanup.json'
    cleanup = json.loads(cleanup_path.read_text()) if cleanup_path.exists() else {}
    checks = []
    for item in cleanup.get('containers', []):
        commands = []
        for command in (['docker', 'container', 'inspect', item['id']], ['docker', 'container', 'ls', '--all', '--no-trunc', '--filter', 'id=' + item['id'], '--format', '{{.ID}}']):
            result = subprocess.run(command, capture_output=True, text=True, timeout=10)
            commands.append({'argv': command, 'exit_code': result.returncode, 'stdout': result.stdout, 'stderr': result.stderr})
        absent = commands[0]['exit_code'] != 0 and 'no such' in commands[0]['stderr'].lower() and commands[1]['exit_code'] == 0 and not commands[1]['stdout'].strip()
        checks.append({'id': item['id'], 'absent': absent, 'commands': commands})
    identities = {}
    observed = directory / 'bounded-infrastructure/observed-processes.json'
    if observed.exists():
        for row in json.loads(observed.read_text()).values():
            identities[(row['pid'], row['start_ticks'])] = row
    for row in cleanup.get('processes', []):
        identities.setdefault((row['pid'], row.get('start_ticks')), row)
    active = directory / 'active-runner-identities.json'
    if control.startswith('runner-'):
        if not active.exists():
            errors.append('Active-runner descendants were not observed')
        else:
            receipt = json.loads(active.read_text())
            if len(receipt['processes']) < 3:
                errors.append('Require runner plus worker and observer surrogate descendants')
            for row in receipt['processes'].values():
                identities[(row['pid'], row['start_ticks'])] = row
    process_checks = []
    for (pid, ticks), row in identities.items():
        try:
            current = Path('/proc', str(pid), 'stat').read_text().rsplit(')', 1)[1].split()[19]
        except FileNotFoundError:
            current = None
        absent = current is None or ticks is not None and current != ticks
        process_checks.append({'pid': pid, 'expected_start_ticks': ticks, 'current_start_ticks': current, 'absent': absent})
    runtime = cleanup.get('runtime')
    good = (not errors and code != 0 and cleanup.get('passed') is True and len(checks) == 3 and all(row['absent'] for row in checks)
            and all(row['absent'] for row in process_checks) and bool(runtime) and not Path(runtime).exists())
    if control in ('cancel', 'runner-cancel', 'runner-crash'):
        good = good and bool(process_checks)
    results.append({'label': label, 'control': control, 'utc_start': started, 'utc_end': datetime.datetime.now(datetime.timezone.utc).isoformat(), 'argv': argv,
                    'expected_rejection_exit_code': code, 'driver_error': driver_error, 'signals': signals, 'containers': checks, 'processes': process_checks,
                    'runtime': runtime, 'runtime_absent': bool(runtime) and not Path(runtime).exists(), 'errors': errors, 'passed': bool(good)})
result = {'passed': len(results) == 6 and all(row['passed'] for row in results), 'controls': results,
          'scope': 'Real disposable owned Dapr infrastructure. Active runner/worker/observer are process-tree surrogates at the exact real subprocess boundary; these are lifecycle controls, not real-Dapr scenario acceptance.'}
with target.open('x') as stream:
    json.dump(result, stream, indent=2)
    stream.write('\n')
print(json.dumps({'passed': result['passed'], 'controls': [(row['label'], row['passed']) for row in results]}))
raise SystemExit(0 if result['passed'] else 1)

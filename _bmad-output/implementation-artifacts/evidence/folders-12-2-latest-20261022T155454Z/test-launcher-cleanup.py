#!/usr/bin/env python3
"""Exercise the successor's actual error/cancellation cleanup using disposable infrastructure."""
from pathlib import Path
import datetime
import json
import os
import signal
import subprocess
import sys
import time

packet = Path(__file__).resolve().parent
root = packet.parents[3]
results = []
for control in ('malformed-capture', 'write-failure', 'cancel'):
    directory = packet / 'cleanup-controls-v2' / control
    env = dict(os.environ, BOUNDED_LAUNCHER_PACKET=str(directory), BOUNDED_LAUNCHER_CONTROL=control)
    label = 'cleanup-v2-' + control
    argv = [sys.executable, str(packet / 'run.py'), label, sys.executable, str(packet / 'bounded-live.py')]
    started = datetime.datetime.now(datetime.timezone.utc).isoformat()
    process = subprocess.Popen(argv, cwd=root, env=env)
    sent_signal = None
    if control == 'cancel':
        deadline = time.monotonic() + 30
        while not (directory / 'control-ready').exists() and process.poll() is None and time.monotonic() < deadline:
            time.sleep(0.1)
        if not (directory / 'control-ready').exists():
            raise RuntimeError('Cancellation control did not become ready.')
        selected = json.loads((directory / 'control-selection.json').read_text())
        pid = selected['pid']
        ticks = Path('/proc', str(pid), 'stat').read_text().rsplit(')', 1)[1].split()[19]
        if ticks != selected['start_ticks']:
            raise RuntimeError('Cancellation target identity changed.')
        os.kill(pid, signal.SIGTERM)
        sent_signal = {'pid': pid, 'start_ticks': ticks, 'signal': 'SIGTERM'}
    code = process.wait(timeout=60)
    cleanup = json.loads((directory / 'bounded-infrastructure/cleanup.json').read_text())
    checks = []
    for item in cleanup['containers']:
        identity = item['id']
        commands = []
        for command in (['docker', 'container', 'inspect', identity], ['docker', 'container', 'ls', '--all', '--no-trunc', '--filter', 'id=' + identity, '--format', '{{.ID}}']):
            command_result = subprocess.run(command, capture_output=True, text=True, timeout=10)
            commands.append({'argv': command, 'exit_code': command_result.returncode, 'stdout': command_result.stdout, 'stderr': command_result.stderr})
        absent = commands[0]['exit_code'] != 0 and 'no such' in commands[0]['stderr'].lower() and commands[1]['exit_code'] == 0 and not commands[1]['stdout'].strip()
        checks.append({'id': identity, 'absent': absent, 'commands': commands})
    good = code != 0 and cleanup['passed'] and len(checks) == 3 and all(row['absent'] for row in checks)
    if control == 'cancel':
        good = good and bool(cleanup['processes']) and all(row['absent'] for row in cleanup['processes'])
    results.append({'control': control, 'utc_start': started, 'utc_end': datetime.datetime.now(datetime.timezone.utc).isoformat(), 'argv': argv,
                    'environment': {k: env[k] for k in ('BOUNDED_LAUNCHER_PACKET', 'BOUNDED_LAUNCHER_CONTROL')},
                    'expected_rejection_exit_code': code, 'signal': sent_signal, 'cleanup_passed': cleanup['passed'], 'containers': checks, 'passed': good})
    (packet / 'launcher-cleanup-v2-result.json').write_text(json.dumps({'passed': all(row['passed'] for row in results), 'controls': results}, indent=2) + '\n')
print(json.dumps({'passed': all(row['passed'] for row in results), 'control_count': len(results)}))
raise SystemExit(0 if len(results) == 3 and all(row['passed'] for row in results) else 1)

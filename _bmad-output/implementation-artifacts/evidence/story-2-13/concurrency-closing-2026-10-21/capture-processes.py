#!/usr/bin/env python3
"""Observe fixture cleanup without stopping or modifying any process."""
import datetime
import json
from pathlib import Path
import re

here = Path(__file__).resolve().parent
roots = [here.parent / n for n in ['runs-2026-10-21-concurrency-safety', 'runs-2026-10-21-concurrency-retirement']]
known = {}
ports = set()
for root in roots:
    for path in (root / 'scenarios').glob('*/manifest.json'):
        data = json.loads(path.read_text())
        for key, value in data.items():
            if key.endswith('ProcessId') and isinstance(value, int):
                known.setdefault(value, []).append(str(path.relative_to(here.parent)) + ':' + key)
            if key.endswith('Port') and isinstance(value, int):
                ports.add(value)

owned_runtime_names = ['/tmp/eventstore-2-13-concurrency-waq49g1u/daprd-concurrency',
                       '/tmp/eventstore-2-13-retirement-oduc267u/daprd-retirement']
owned = []
existing_recorded_pids = []
for proc in Path('/proc').glob('[0-9]*'):
    try:
        args = (proc / 'cmdline').read_bytes().decode().rstrip('\x00').split('\x00')
    except (OSError, UnicodeError):
        continue
    if not args or not args[0]:
        continue
    pid = int(proc.name)
    selected_runtime = args[0] in owned_runtime_names
    recorded_host = pid in known and Path(args[0]).name in ['dotnet', 'Hexalith.EventStore'] and any('Hexalith.EventStore.dll' in a for a in args[1:])
    if selected_runtime or recorded_host:
        owned.append({'pid': pid, 'executable': args[0], 'selectedRuntime': selected_runtime, 'recordedHost': recorded_host})
    if pid in known:
        existing_recorded_pids.append({'pid': pid, 'executable': args[0], 'roles': known[pid], 'matchesOwnedRuntimeOrHost': selected_runtime or recorded_host})

listening = set()
for table in ['tcp', 'tcp6']:
    for row in (Path('/proc/net') / table).read_text().splitlines()[1:]:
        cols = row.split()
        if len(cols) > 3 and cols[3] == '0A':
            listening.add(int(cols[1].split(':')[-1], 16))
result = {'observedUtc': datetime.datetime.now(datetime.timezone.utc).isoformat(),
          'matchingCandidateOrRecordedHostProcesses': owned,
          'recordedProcessIdsStillPresent': existing_recorded_pids,
          'recordedPortsCurrentlyListening': sorted(ports & listening),
          'matchingFixtureComponentDirectories': sorted(str(p) for p in Path('/tmp').glob('eventstore-2-13-*') if p.is_dir() and re.fullmatch(r'eventstore-2-13-[a-f0-9]{32}', p.name)),
          'pidReuseAndPortReuseCaveat': 'Presence alone does not establish fixture ownership. No unrelated process, directory or listener is changed.',
          'runtimeSourceAndExecutableDirectoriesRetained': True}
out = here / 'processes.json'
with out.open('x') as stream:
    stream.write(json.dumps(result, indent=2) + '\n')
print(json.dumps(result, indent=2))

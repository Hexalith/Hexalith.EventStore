#!/usr/bin/env python3
"""Verify exact owned cleanup and retire only this task's identified verifier directory."""
import datetime
import hashlib
import json
from pathlib import Path
import re
import shutil
import subprocess

packet = Path(__file__).resolve().parent
receipts = []


def sha(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def run(argv):
    result = subprocess.run(argv, text=True, capture_output=True)
    clean = lambda s: re.sub(r'(/login\?t=)[a-fA-F0-9]+', r'\1[REDACTED]', s)
    receipts.append({'argv': argv, 'exit_code': result.returncode, 'observed_utc': datetime.datetime.now(datetime.timezone.utc).isoformat(), 'stdout': clean(result.stdout), 'stderr': clean(result.stderr), 'redaction': 'Aspire dashboard login tokens only'})
    return result


errors = []
containers = []
directories = []
for name in ('actor-live/cleanup.json', 'bounded-infrastructure/cleanup.json'):
    cleanup = json.loads((packet / name).read_text())
    for item in cleanup['containers']:
        result = run(['docker', 'inspect', '--format', '{{.Id}}', item['id']])
        listed = run(['docker', 'ps', '-aq', '--filter', 'id=' + item['id']])
        absent = result.returncode == 1 and 'no such object' in result.stderr.lower() and listed.returncode == 0 and not listed.stdout.strip()
        containers.append({'id': item['id'], 'name': item['name'], 'absent': absent})
        if not absent:
            errors.append('Owned container not confirmed absent: ' + item['id'])
    runtime = Path(cleanup['runtime'])
    directories.append({'path': str(runtime), 'absent': not runtime.exists()})
    if runtime.exists():
        errors.append('Owned runtime directory remains: ' + str(runtime))
for name in json.loads((packet / 'bounded-infrastructure/components.json').read_text()):
    directories.append({'path': name, 'absent': not Path(name).exists()})
    if Path(name).exists():
        errors.append('Owned component directory remains: ' + name)

aspire = run(['aspire', 'ps', '--format', 'Json'])
if aspire.returncode:
    errors.append('Cannot inspect final Aspire lifecycle state.')
else:
    hosts = json.loads(aspire.stdout)
    if any('eventstore/src/Hexalith.EventStore.AppHost/' in h['appHostPath'] for h in hosts):
        errors.append('Task baseline AppHost is still active.')
    tenants = [h for h in hosts if h['appHostPid'] == 67730]
    if not tenants:
        errors.append('Unrelated initial Tenants AppHost was not observed.')

location = Path(json.loads((packet / 'verifier-location.json').read_text())['directory'])
if location.parent != Path('/tmp') or not location.name.startswith('eventstore-current-pdb-') or location.is_symlink():
    raise RuntimeError('Verifier location ownership check failed.')
if location.exists():
    if sha(location / 'Program.cs') != sha(packet / 'pdb-verifier-source.cs'):
        raise RuntimeError('Verifier source identity check failed.')
    shutil.copytree(location / 'bin/Debug/net10.0', packet / 'pdb-verifier-runtime')
    shutil.copyfile(location / 'Verifier.csproj', packet / 'pdb-verifier-project.csproj.source')
    temporary = {'path': str(location), 'source_sha256': sha(location / 'Program.cs'), 'project_sha256': sha(location / 'Verifier.csproj'),
                 'archived_runtime_sha256': {str(p.relative_to(packet)): sha(p) for p in (packet / 'pdb-verifier-runtime').rglob('*') if p.is_file()}}
    shutil.rmtree(location)
    temporary['absent'] = not location.exists()
    temporary['passed'] = temporary['absent']
    (packet / 'temporary-cleanup.json').write_text(json.dumps(temporary, indent=2) + '\n')
else:
    temporary = json.loads((packet / 'temporary-cleanup.json').read_text())
    if temporary['path'] != str(location) or temporary['source_sha256'] != sha(packet / 'pdb-verifier-source.cs') or not temporary['passed']:
        raise RuntimeError('Already-retired verifier identity mismatch.')
    if any(not (packet / name).is_file() or sha(packet / name) != expected for name, expected in temporary['archived_runtime_sha256'].items()):
        raise RuntimeError('Retired verifier archive mismatch.')
(packet / 'cleanup-final.json').write_text(json.dumps({'passed': not errors and temporary['passed'], 'errors': errors, 'containers': containers, 'directories': directories,
    'unrelated_tenants_pid': 67730, 'commands': receipts,
    'retained_generic_apphost_materialization': {'paths': ['/tmp/hexalith-eventstore-empty-dapr-resources', '/tmp/hexalith-eventstore-dapr-components/statestore'], 'reason': 'Preexisting ownership unknown; no deletion performed.'}}, indent=2) + '\n')
print(json.dumps({'passed': not errors and temporary['passed'], 'owned_containers_absent': sum(c['absent'] for c in containers), 'errors': errors, 'verifier_absent': temporary['absent']}))
raise SystemExit(1 if errors or not temporary['passed'] else 0)

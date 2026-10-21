#!/usr/bin/env python3
"""Read-only closing observations for the local concurrency evaluations."""
import datetime
import hashlib
import json
import os
from pathlib import Path
import stat
import subprocess

HERE = Path(__file__).resolve().parent
EVIDENCE = HERE.parent
ROOT = next(p for p in HERE.parents if (p / 'Hexalith.EventStore.slnx').exists())

def digest(path):
    h = hashlib.sha256()
    with path.open('rb') as stream:
        for chunk in iter(lambda: stream.read(1048576), b''):
            h.update(chunk)
    return h.hexdigest()

def inventory(root):
    result = {}
    for p in sorted(root.rglob('*')):
        s = p.lstat()
        entry = {'mode': stat.S_IMODE(s.st_mode)}
        if p.is_symlink():
            entry.update(type='symlink', target=os.readlink(p))
        elif p.is_dir():
            entry.update(type='directory')
        elif p.is_file():
            entry.update(type='file', bytes=s.st_size, sha256=digest(p))
        else:
            entry.update(type='other')
        result[str(p.relative_to(root))] = entry
    return result

def differences(expected, observed):
    return {key: {'before': expected.get(key), 'after': observed.get(key)}
            for key in sorted(set(expected) | set(observed)) if expected.get(key) != observed.get(key)}

def current_hashes(expected):
    return {p: digest(ROOT / p) if (ROOT / p).is_file() else None for p in expected}

start = json.loads((EVIDENCE / 'runs-2026-10-21-concurrency-safety/start-preservation.json').read_text())
original = EVIDENCE / 'runs-2026-10-21-concurrency-safety'
retirement = EVIDENCE / 'runs-2026-10-21-concurrency-retirement'
source_checks = {}
for name, packet, expected_name in [
    ('retained', original, 'retained-source-inventory.json'),
    ('reset', original, 'before-candidate-build-source-inventory.json'),
    ('retirement', retirement, 'before-candidate-build-source-inventory.json'),
]:
    acquisition = json.loads((packet / 'runtime-acquisition.json').read_text())
    directory = Path(acquisition['retainedSourceDirectory'] if name == 'retained' else acquisition['sourceDirectory'])
    expected = json.loads((packet / expected_name).read_text())
    observed = inventory(directory)
    source_checks[name] = {'path': str(directory), 'expectedPaths': len(expected), 'observedPaths': len(observed),
                          'differences': differences(expected, observed), 'inventorySha256': digest(packet / expected_name)}

first_binary = json.loads((original / 'parent-lifecycle/initial-inotify.json').read_text())['binaryBefore']
runtime_checks = {}
for name, entry in [('installed', start['installedDaprd']), ('retained', start['retainedRuntime'])]:
    runtime_checks[name] = {**entry, 'currentSha256': digest(Path(entry['path']))}
for name, packet, binary_name in [('reset', original, 'daprd-concurrency'), ('retirement', retirement, 'daprd-retirement')]:
    acquisition = json.loads((packet / 'runtime-acquisition.json').read_text())
    path = Path(acquisition['workDirectory']) / binary_name
    runtime_checks[name] = {'path': str(path), 'currentSha256': digest(path)}

spec = ROOT / '_bmad-output/implementation-artifacts/spec-2-13-dapr-notification-distribution-qualification.md'
def frozen(data):
    return data.split(b'<frozen-after-approval', 1)[1].split(b'</frozen-after-approval>', 1)[0]

out = HERE / 'preservation.json'
if out.exists():
    raise FileExistsError(out)
result = {
    'observedUtc': datetime.datetime.now(datetime.timezone.utc).isoformat(),
    'headAtStart': start['head'],
    'headAtClose': subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip(),
    'gitStatusAtClose': subprocess.check_output(['git', 'status', '--short'], cwd=ROOT, text=True),
    'historicalFilesChecked': len(start['historicalFiles']),
    'historicalDifferences': differences(start['historicalFiles'], current_hashes(start['historicalFiles'])),
    'protectedDifferences': differences(start['protectedFiles'], current_hashes(start['protectedFiles'])),
    'applicationBinaryPathsChecked': len(first_binary),
    'applicationBinaryDifferences': differences(first_binary, current_hashes(first_binary)),
    'sourceTrees': source_checks,
    'runtimes': runtime_checks,
    'frozenIntentUnchanged': frozen((original / 'spec-before.source').read_bytes()) == frozen(spec.read_bytes()),
    'currentSpecSha256': digest(spec),
    'legacyHandoffSpecSha256': json.loads((EVIDENCE / 'review-2026-10-21-runtime-recovery/handoff-binding.json').read_text())['currentSpecSha256'],
    'runStartSpecSha256': digest(original / 'spec-before.source'),
    'legacyLiveSpecBindingSupersededByAuthorizedContinuation': True,
    'gitMutationsByThisTask': [],
    'externalRevisionChange': start['head'] != subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip(),
    'authority': 'Local evidence only; no production approval, runtime installation or exception acceptance.'
}
out.write_text(json.dumps(result, indent=2) + '\n')
print(json.dumps({'historicalFilesChecked': result['historicalFilesChecked'],
                  'historicalDifferences': len(result['historicalDifferences']),
                  'protectedDifferences': len(result['protectedDifferences']),
                  'applicationBinaryDifferences': len(result['applicationBinaryDifferences']),
                  'sourceDifferences': {k: len(v['differences']) for k, v in source_checks.items()},
                  'frozenIntentUnchanged': result['frozenIntentUnchanged']}))

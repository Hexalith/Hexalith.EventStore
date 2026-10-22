#!/usr/bin/env python3
"""Archive and remove only the two identified tools owned by this qualification."""
import hashlib
import json
from pathlib import Path
import shutil
import tarfile

packet = Path(__file__).resolve().parent
initial = packet.parent / 'folders-12-2-gates-20261022T151209Z'
scanner = Path('/tmp/eventstore-gates-scanner-1l6o0i8f')
observer = Path('/tmp/folders-observer-review')
for name in ('private-tools.tar.gz', 'private-tools-cleanup.json'):
    if (packet / name).exists():
        raise SystemExit('Refusing to overwrite evidence: ' + name)

def sha(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()

checks = [(scanner / 'Program.cs', sha(initial / 'scanner-source.cs.source')),
          (scanner / 'Scanner.csproj', sha(initial / 'scanner-project.csproj.source'))]
checks += [(Path(row['prepared_path']), row['prepared_sha256'])
           for row in json.loads((initial / 'observer-review-prepared-identities.json').read_text())]
errors = []
for path, expected in checks:
    if not path.is_file() or sha(path) != expected:
        errors.append('Owned tool source differs: ' + str(path))
active = []
for path in Path('/proc').glob('[0-9]*/cmdline'):
    try:
        argv = path.read_bytes().split(b'\0')
        if any(str(root).encode() in argument for root in (scanner, observer) for argument in argv):
            active.append(int(path.parent.name))
    except OSError:
        continue
if active:
    errors.append('Owned tool directory still appears in process argv: ' + str(active))
files = {}
if not errors:
    with tarfile.open(packet / 'private-tools.tar.gz', 'w:gz') as archive:
        for root in (scanner, observer):
            if root.is_symlink() or root.resolve() != root:
                raise RuntimeError('Refusing unexpected owned-tool path: ' + str(root))
            for path in sorted(root.rglob('*')):
                if path.is_symlink():
                    raise RuntimeError('Refusing unexpected owned-tool link: ' + str(path))
                if path.is_file():
                    files[str(path)] = sha(path)
                    archive.add(path, arcname=str(Path(root.name) / path.relative_to(root)), recursive=False)
    for root in (scanner, observer):
        shutil.rmtree(root)
result = {'passed': not errors and all(not root.exists() for root in (scanner, observer)), 'source_checks': [{'path': str(path), 'sha256': expected} for path, expected in checks],
          'files_sha256': files, 'archive_sha256': sha(packet / 'private-tools.tar.gz') if (packet / 'private-tools.tar.gz').exists() else None,
          'directories_absent': {str(root): not root.exists() for root in (scanner, observer)}, 'errors': errors,
          'retained_source_workspaces': [
              '/tmp/eventstore-folders-12-2-latest-20261022T155454Z',
              '/tmp/eventstore-folders-12-2-guardfix-20261022T171346Z',
              '/tmp/eventstore-folders-12-2-guardtight-20261022T173000Z',
              '/tmp/eventstore-folders-12-2-guardfixtures-20261022T173900Z',
              '/tmp/eventstore-folders-12-2-guardfinal-20261022T175200Z'],
          'retained_source_disposition': 'Deliberately retained replay/build inputs; no running fixture is retained.'}
with (packet / 'private-tools-cleanup.json').open('x') as stream:
    json.dump(result, stream, indent=2)
    stream.write('\n')
print(json.dumps({key: value for key, value in result.items() if key != 'files_sha256'}))
raise SystemExit(0 if result['passed'] else 1)

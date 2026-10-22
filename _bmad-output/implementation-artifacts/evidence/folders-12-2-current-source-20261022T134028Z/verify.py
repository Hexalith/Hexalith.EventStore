#!/usr/bin/env python3
"""Verify this packet's seal, optionally also checking the live producer inputs."""
import argparse
import hashlib
import json
from pathlib import Path

packet = Path(__file__).resolve().parent
parser = argparse.ArgumentParser()
parser.add_argument('--current-inputs', action='store_true')
args = parser.parse_args()


def sha(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


errors = []
entries = {}
for line in (packet / 'SHA256SUMS').read_text().splitlines():
    expected, name = line.split('  ', 1)
    path = packet / name
    if path.resolve().is_relative_to(packet) and path.is_file():
        entries[name] = expected
        if sha(path) != expected:
            errors.append({'kind': 'seal-mismatch', 'path': name})
    else:
        errors.append({'kind': 'missing-or-unsafe-path', 'path': name})
actual = {str(p.relative_to(packet)) for p in packet.rglob('*') if p.is_file() and p != packet / 'SHA256SUMS'}
if actual != set(entries):
    errors.append({'kind': 'file-set', 'unsealed': sorted(actual - set(entries)), 'missing': sorted(set(entries) - actual)})
for row in json.loads((packet / 'bound-source-index-final.json').read_text()):
    path = packet / row['archive']
    if not path.is_file() or sha(path) != row['sha256']:
        errors.append({'kind': 'archived-pdb-source', 'path': row['archive']})
if args.current_inputs:
    for name, key in [('source-final.json', None), ('binary-final.json', 'files_sha256')]:
        doc = json.loads((packet / name).read_text())
        for path, expected in (doc[key] if key else doc).items():
            if not Path(path).is_file() or sha(Path(path)) != expected:
                errors.append({'kind': 'current-input', 'path': path})
    for row in json.loads((packet / 'bound-source-index-final.json').read_text()):
        if row['generated']:
            continue
        path = Path(row['path'])
        if not path.is_file() or sha(path) != row['sha256']:
            errors.append({'kind': 'current-pdb-source', 'path': str(path)})
print(json.dumps({'passed': not errors, 'sealed_files': len(entries), 'seal_sha256': sha(packet / 'SHA256SUMS'), 'checked_current_inputs': args.current_inputs, 'errors': errors}, indent=2))
raise SystemExit(1 if errors else 0)

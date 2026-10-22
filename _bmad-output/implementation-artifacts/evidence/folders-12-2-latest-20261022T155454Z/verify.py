#!/usr/bin/env python3
"""Check the complete append-only packet seal; optionally verify explicitly selected live inputs."""
import argparse
import hashlib
import json
from pathlib import Path, PurePosixPath

packet = Path(__file__).resolve().parent
parser = argparse.ArgumentParser()
parser.add_argument('--current-inputs', action='store_true')
parser.add_argument('--final-producer-inputs', action='store_true')
args = parser.parse_args()

def sha(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()

errors = []
entries = {}
for line in (packet / 'SHA256SUMS').read_text().splitlines():
    expected, name = line.split('  ', 1)
    relative = PurePosixPath(name)
    path = packet / name
    if name in entries or relative.is_absolute() or '..' in relative.parts or any((packet / Path(*relative.parts[:n])).is_symlink() for n in range(1, len(relative.parts) + 1)):
        errors.append({'kind': 'duplicate-or-unsafe-name', 'path': name})
        continue
    entries[name] = expected
    if not path.is_file() or sha(path) != expected:
        errors.append({'kind': 'seal-mismatch-or-missing', 'path': name})
actual = {str(path.relative_to(packet)) for path in packet.rglob('*') if path.is_file() and path != packet / 'SHA256SUMS'}
if actual != set(entries):
    errors.append({'kind': 'file-set', 'unsealed': sorted(actual - set(entries)), 'missing': sorted(set(entries) - actual)})
seal_errors = list(errors)
inventories = []
if args.current_inputs:
    inventories.append(packet / 'current-inputs.json')
if args.final_producer_inputs:
    inventories.append(packet / 'guard-final/producer-current-inputs.json')
for selected in inventories:
    if not selected.is_file():
        errors.append({'kind': 'live-input-inventory-not-enrolled'})
    else:
        for name, expected in json.loads(selected.read_text()).items():
            path = Path(name)
            if not path.is_file() or sha(path) != expected:
                errors.append({'kind': 'current-input-mismatch', 'path': name})
status_file = packet / 'qualification.json'
if not status_file.exists():
    status_file = packet / 'disposition.json'
status = json.loads(status_file.read_text()).get('disposition') if status_file.exists() else 'not-declared'
print(json.dumps({'seal_passed': not seal_errors, 'all_requested_checks_passed': not errors, 'sealed_files': len(entries), 'seal_sha256': sha(packet / 'SHA256SUMS'), 'disposition': status, 'current_inputs_checked': args.current_inputs, 'final_producer_inputs_checked': args.final_producer_inputs, 'errors': errors}, indent=2))
raise SystemExit(0 if not errors else 1)

#!/usr/bin/env python3
import hashlib
import json
from pathlib import Path
import tarfile

packet = Path(__file__).resolve().parent
selection = json.loads((packet / 'snapshot-selection.json').read_text())
root = Path(selection['snapshot'])
manifest = json.loads((packet / 'snapshot-inputs.json').read_text())
expected = {str(Path(name).relative_to(root)): row['sha256'] for name, row in manifest.items()}
errors = []
seen = set()
with tarfile.open(packet / 'ordinary-source.tar.gz', 'r:gz') as archive:
    for member in archive:
        if not member.isfile() or member.name in seen or member.name not in expected:
            errors.append('Unexpected, duplicate or nonregular member: ' + member.name)
            continue
        seen.add(member.name)
        with archive.extractfile(member) as stream:
            if hashlib.file_digest(stream, 'sha256').hexdigest() != expected[member.name]:
                errors.append('Archive source mismatch: ' + member.name)
if seen != set(expected):
    errors.append('Archive file set differs from selected inputs.')
result = {'passed': not errors, 'source_files': len(seen), 'errors': errors, 'earlier_command_failure_preserved': 'archive-inputs-command.json'}
(packet / 'source-archive-verification.json').write_text(json.dumps(result, indent=2) + '\n')
print(json.dumps(result))
raise SystemExit(0 if result['passed'] else 1)

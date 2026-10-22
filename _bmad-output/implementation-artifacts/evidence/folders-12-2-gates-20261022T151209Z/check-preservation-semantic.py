#!/usr/bin/env python3
"""Compare protected bytes and repository/index identities without mutating either repository."""
from pathlib import Path
import hashlib
import json
import os
import subprocess
import sys

packet = Path(__file__).resolve().parent

def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()

def git(root, *args):
    return subprocess.check_output(['git', *args], cwd=root, text=True, env=os.environ | {'GIT_OPTIONAL_LOCKS': '0'})

protected = json.loads((packet / 'protected-start.json').read_text())
changes = [{'path': name, 'before': expected, 'after': sha(name) if Path(name).is_file() else None}
           for name, expected in protected.items() if not Path(name).is_file() or sha(name) != expected]
repositories = {}
for root, before in json.loads((packet / 'repositories-start.json').read_text()).items():
    head = git(root, 'rev-parse', 'HEAD').strip()
    submodules = git(root, 'submodule', 'status')
    status = git(root, 'status', '--short')
    repositories[root] = {'head': head, 'head_unchanged': head == before['head'],
        'raw_index_bytes_unchanged': sha(before['index_path']) == before['index_sha256'],
        'semantic_index_unchanged': not git(root, 'diff', '--cached', '--raw', 'HEAD') and not any(len(line) > 1 and line[0] not in (' ', '?') for line in before['status'].splitlines()),
        'submodule_revisions_unchanged': submodules == before['submodules'], 'status': status}
    if root.endswith('/folders') and status != before['status']:
        changes.append({'kind': 'folders-status-drift', 'status': status})
passed = not changes and all(row['head_unchanged'] and row['semantic_index_unchanged'] and row['submodule_revisions_unchanged'] for row in repositories.values())
result = {'passed': passed, 'protected_files': len(protected), 'changes': changes, 'repositories': repositories}
output = packet / sys.argv[1]
with output.open('x') as stream:
    json.dump(result, stream, indent=2)
    stream.write('\n')
print(json.dumps({'passed': passed, 'protected_files': len(protected), 'changes': changes}))
raise SystemExit(0 if passed else 1)

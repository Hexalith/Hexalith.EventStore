#!/usr/bin/env python3
"""Keep snapshot integrity distinct from changes made by other workspace writers."""
import datetime
import hashlib
import json
import os
from pathlib import Path
import subprocess
from review_audit_checks import ordinary_paths

packet = Path(__file__).resolve().parent
prior = packet.parent / 'folders-12-2-gates-20261022T151209Z'
origin = packet.parents[3]
for name in ('late-current-inputs-v2.json', 'late-input-integrity-v2.json'):
    if (packet / name).exists():
        raise SystemExit('Refusing to overwrite evidence: ' + name)

def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()

def git(root, *args):
    return subprocess.check_output(['git', *args], cwd=root, text=True, env=os.environ | {'GIT_OPTIONAL_LOCKS': '0'})

def changes(inputs):
    return [{'path': name, 'expected': expected, 'actual': sha(name) if Path(name).is_file() else None}
            for name, expected in inputs.items() if not Path(name).is_file() or sha(name) != expected]

selected = {}
conflicts = []

def enroll(inputs):
    for name, expected in inputs.items():
        if name in selected and selected[name] != expected:
            conflicts.append({'path': name, 'earlier': selected[name], 'later': expected})
        else:
            selected[name] = expected

enroll({name: row['sha256'] for name, row in json.loads((packet / 'snapshot-inputs.json').read_text()).items()})
enroll(json.loads((packet / 'tracked-guard-inputs.json').read_text()))
enroll(json.loads((packet / 'debug-binary-freeze.json').read_text())['files_sha256'])
qualified = json.loads((packet / 'qualified-worker-runtime-inputs.json').read_text())
enroll(qualified['files'])
enroll(json.loads((packet / 'guard-final/producer-current-inputs.json').read_text()))
# Enumerate full ordinary input scopes, including ignored source/config additions.
# Exclusions are the original freeze exclusions plus interpreter bytecode caches.
set_deltas = []
for prefix, expected in [('/tmp/eventstore-folders-12-2-latest-20261022T155454Z', set(json.loads((packet / 'snapshot-inputs.json').read_text()))), ('/tmp/eventstore-folders-12-2-guardfinal-20261022T175200Z', set(json.loads((packet / 'guard-final/source-inputs.json').read_text())))]:
    actual = ordinary_paths(Path(prefix))
    set_deltas.append({'root': prefix, 'expected_count': len(expected), 'actual_count': len(actual), 'unexpected': sorted(actual - expected), 'missing': sorted(expected - actual)})
input_changes = changes(selected)
protected = json.loads((prior / 'protected-start.json').read_text())
protected_changes = changes(protected)
folders = {name: expected for name, expected in protected.items() if '/folders/' in name}
folders_changes = [row for row in protected_changes if row['path'] in folders]
repositories = {}
for name, before in json.loads((prior / 'repositories-start.json').read_text()).items():
    repositories[name] = {'head': git(name, 'rev-parse', 'HEAD').strip(), 'branch': git(name, 'branch', '--show-current').strip(),
                          'status': git(name, 'status', '--short'), 'staged_diff': git(name, 'diff', '--cached', '--raw', 'HEAD'),
                          'submodules': git(name, 'submodule', 'status'), 'index_sha256': sha(before['index_path']),
                          'initial_head': before['head'], 'initial_index_sha256': before['index_sha256']}
latest_origin_changes = {}
for repo, before in json.loads((packet / 'origin-before.json').read_text()).items():
    delta = changes({str(Path(repo) / name): expected for name, expected in before['inputs'].items()})
    latest_origin_changes[repo] = {'head': git(repo, 'rev-parse', 'HEAD').strip(), 'selected_head': before['repository']['head'], 'input_changes': delta}
folder_repo = next(row for name, row in repositories.items() if name.endswith('/folders'))
folder_before = next(row for name, row in json.loads((prior / 'repositories-start.json').read_text()).items() if name.endswith('/folders'))
folders_passed = (not folders_changes and folder_repo['head'] == folder_before['head'] and folder_repo['branch'] == folder_before['branch']
                  and folder_repo['index_sha256'] == folder_before['index_sha256'] and folder_repo['status'] == folder_before['status']
                  and folder_repo['submodules'] == folder_before['submodules'])
with (packet / 'late-current-inputs-v2.json').open('x') as stream:
    stream.write(json.dumps(selected | folders, indent=2) + '\n')
result = {'utc': datetime.datetime.now(datetime.timezone.utc).isoformat(), 'frozen_inputs_passed': not input_changes and not conflicts and all(not row['unexpected'] and not row['missing'] for row in set_deltas), 'ordinary_file_set_comparisons': set_deltas, 'frozen_input_paths': len(selected), 'frozen_input_changes': input_changes, 'conflicting_input_hashes': conflicts,
          'folders_passed': folders_passed, 'folders_protected_paths': len(folders), 'folders_changes': folders_changes,
          'all_initial_protected_paths': len(protected), 'all_initial_protected_changes': protected_changes,
          'repositories': repositories, 'latest_origin_changes': latest_origin_changes,
          'interpretation': 'Snapshot inputs and Folders are strict predicates. Original workspace changes are reported separately; no external source, dependency, Git index or historical evidence is restored or overwritten.'}
with (packet / 'late-input-integrity-v2.json').open('x') as stream:
    stream.write(json.dumps(result, indent=2) + '\n')
print(json.dumps({k:v for k,v in result.items() if k not in ('repositories', 'latest_origin_changes', 'all_initial_protected_changes')}))
raise SystemExit(0 if result['frozen_inputs_passed'] and folders_passed else 1)

#!/usr/bin/env python3
"""Freeze current root and root-declared source dependencies without mutating their Git state."""
from pathlib import Path
from concurrent.futures import ThreadPoolExecutor
import hashlib
import json
import os
import shutil
import subprocess
import datetime

packet = Path(__file__).resolve().parent
origin = packet.parents[3]
selection = json.loads((packet / 'snapshot-selection.json').read_text())
snapshot = Path(selection['snapshot'])
env = os.environ | {'GIT_OPTIONAL_LOCKS': '0'}
commands = []

def git(repo, *args):
    return subprocess.check_output(['git', *args], cwd=repo, text=True, env=env)

def sha(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()

def state(repo):
    return {'head': git(repo, 'rev-parse', 'HEAD').strip(), 'branch': git(repo, 'branch', '--show-current').strip(),
            'status': git(repo, 'status', '--short'), 'remotes': git(repo, 'remote', '-v'),
            'history': git(repo, 'log', '-3', '--format=%H %s'),
            'index_path': git(repo, 'rev-parse', '--path-format=absolute', '--git-path', 'index').strip()}

def inputs(repo):
    names = git(repo, 'ls-files', '--cached', '--others', '--exclude-standard', '-z').split('\0')
    result = {}
    for name in names:
        path = repo / name
        if not name or any(part in {'references', '_bmad-output', '_bmad', '.git', 'bin', 'obj', '.artifacts', 'node_modules', '.agents', '.claude', '.codex'} for part in Path(name).parts) or path.name == 'SKILL.md':
            continue
        if path.is_file():
            result[name] = sha(path)
    return result

paths = git(origin, 'config', '--file', '.gitmodules', '--get-regexp', r'^submodule\..*\.path$').splitlines()
roots = [(origin, snapshot)]
for line in paths:
    relative = line.split(None, 1)[1]
    source = origin / relative
    if source.is_dir() and (source / '.git').exists():
        roots.append((source, snapshot / relative))
before = {str(repo): {'repository': state(repo), 'inputs': inputs(repo)} for repo, _ in roots}
for row in before.values():
    row['repository']['index_sha256'] = sha(Path(row['repository']['index_path']))
(packet / 'origin-before.json').write_text(json.dumps(before, indent=2) + '\n')

def clone(repo, target):
    records = []
    for argv, cwd in [(['git', 'clone', '--shared', '--no-checkout', '--no-hardlinks', str(repo), str(target)], repo),
                      (['git', 'checkout', '--detach', before[str(repo)]['repository']['head']], target)]:
        result = subprocess.run(argv, cwd=cwd, env=env, text=True, capture_output=True)
        records.append({'argv': argv, 'cwd': str(cwd), 'exit_code': result.returncode, 'stdout': result.stdout, 'stderr': result.stderr})
        if result.returncode:
            raise RuntimeError(json.dumps(records))
    # Copy exact current ordinary bytes, including unstaged/untracked source; retain all history in the clone.
    for name in before[str(repo)]['inputs']:
        path = target / name
        path.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(repo / name, path)
    changed = git(repo, 'diff', 'HEAD', '--name-only', '-z').split('\0')
    for name in changed:
        source = repo / name
        dest = target / name
        if name and source.is_file() and not source.is_symlink():
            dest.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(source, dest)
    remote = git(repo, 'remote', 'get-url', 'origin').strip()
    result = subprocess.run(['git', 'remote', 'set-url', 'origin', remote], cwd=target, env=env, text=True, capture_output=True)
    records.append({'argv': ['git', 'remote', 'set-url', 'origin', remote], 'cwd': str(target), 'exit_code': result.returncode, 'stdout': result.stdout, 'stderr': result.stderr})
    if result.returncode:
        raise RuntimeError('Snapshot-only remote setup failed.')
    return records

commands.extend(clone(*roots[0]))
with ThreadPoolExecutor(max_workers=4) as executor:
    for records in executor.map(lambda pair: clone(*pair), roots[1:]):
        commands.extend(records)
errors = []
manifest = {}
after = {}
for repo, target in roots:
    current = {'repository': state(repo), 'inputs': inputs(repo)}
    after[str(repo)] = current
    if current['repository']['head'] != before[str(repo)]['repository']['head'] or current['inputs'] != before[str(repo)]['inputs']:
        errors.append('Origin changed during freeze: ' + str(repo))
    for name, expected in before[str(repo)]['inputs'].items():
        path = target / name
        actual = sha(path) if path.is_file() else None
        manifest[str(path)] = {'origin': str(repo / name), 'sha256': expected}
        if actual != expected:
            errors.append('Snapshot differs: ' + str(path))
result = {'passed': not errors, 'snapshot': str(snapshot), 'repositories': len(roots), 'ordinary_inputs': len(manifest),
          'utc_completed': datetime.datetime.now(datetime.timezone.utc).isoformat(), 'errors': errors,
          'no_origin_git_mutation': True, 'no_nested_submodule_initialization': True}
for name, doc in [('snapshot-commands.json', commands), ('origin-after.json', after), ('snapshot-inputs.json', manifest), ('snapshot-result.json', result)]:
    (packet / name).write_text(json.dumps(doc, indent=2) + '\n')
print(json.dumps(result))
raise SystemExit(0 if result['passed'] else 1)

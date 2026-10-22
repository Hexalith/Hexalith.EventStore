#!/usr/bin/env python3
"""Hash qualification inputs and historical evidence; never mutate observed roots."""
import hashlib
import json
from pathlib import Path
import subprocess
import sys

PACKET = Path(__file__).resolve().parent
ROOT = PACKET.parents[3]
FOLDERS = ROOT.parent / 'folders'


def digest(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def git(root, *args):
    return subprocess.check_output(['git', '-C', str(root), *args], text=True)


def write(name, value):
    path = PACKET / (name + '.json')
    if path.exists():
        raise RuntimeError('Refusing overwrite: ' + str(path))
    path.write_text(json.dumps(value, indent=2) + '\n')


def source(root):
    values = {}
    for name in git(root, 'ls-files', '--cached', '--others', '--exclude-standard', '-z').split('\0'):
        p = root / name
        if not name or not p.is_file() or any(s in p.relative_to(root).parts for s in ('references', '_bmad-output', '_bmad', '.git', 'bin', 'obj', '.artifacts', 'node_modules')):
            continue
        values[str(p)] = digest(p)
    return values


def initial():
    repos = {}
    for root in (ROOT, FOLDERS):
        index = Path(git(root, 'rev-parse', '--path-format=absolute', '--git-path', 'index').strip())
        repos[str(root)] = {k: git(root, *args) for k, args in {'head': ('rev-parse', 'HEAD'), 'branch': ('branch', '--show-current'), 'status': ('status', '--short'), 'remotes': ('remote', '-v'), 'history': ('log', '-6', '--format=%H %s'), 'submodules': ('submodule', 'status')}.items()}
        repos[str(root)]['index'] = {'path': str(index), 'sha256': digest(index)}
        write(root.name + '-source-before', source(root))
    write('repositories-before', repos)
    historical = {}
    for root in (ROOT, FOLDERS):
        for base in (root / '_bmad-output' / 'implementation-artifacts' / 'evidence', root / 'docs' / 'evidence'):
            for p in base.rglob('*'):
                if p.is_file() and not p.is_relative_to(PACKET):
                    historical[str(p)] = digest(p)
    write('historical-before', historical)
    print(json.dumps({'historical_files': len(historical), 'repositories': {k: {'head': v['head'].strip(), 'status': v['status']} for k, v in repos.items()}}))


def verify():
    results = {}
    violations = []
    authorized = {str(ROOT / 'src/Hexalith.EventStore.Testing/Fakes/TestServiceOverrides.cs'), str(ROOT / 'tests/Hexalith.EventStore.Testing.Tests/Fakes/TestServiceOverridesTests.cs'), str(ROOT / 'tests/Hexalith.EventStore.Contracts.Tests/Packaging/ContractsPackageDependencyTests.FrozenEvidence.cs')}
    for name in ('historical-before', 'folders-source-before', 'eventstore-source-before'):
        values = json.loads((PACKET / (name + '.json')).read_text())
        changes = [{'path': p, 'before': h, 'after': digest(Path(p)) if Path(p).is_file() else None} for p, h in values.items() if not Path(p).is_file() or digest(Path(p)) != h]
        results[name] = {'checked': len(values), 'changes': changes}
        for change in changes:
            if name != 'eventstore-source-before' or change['path'] not in authorized:
                violations.append({'kind': name, **change})
    repos = json.loads((PACKET / 'repositories-before.json').read_text())
    results['repositories'] = {r: {'head': git(Path(r), 'rev-parse', 'HEAD').strip(), 'head_unchanged': git(Path(r), 'rev-parse', 'HEAD').strip() == v['head'].strip(), 'index_unchanged': digest(Path(v['index']['path'])) == v['index']['sha256'], 'status': git(Path(r), 'status', '--short')} for r, v in repos.items()}
    for repo, value in results['repositories'].items():
        if not value['head_unchanged'] or not value['index_unchanged']:
            violations.append({'kind': 'repository-drift', 'repository': repo, **value})
    allowed_new = {str(ROOT / 'tests/Hexalith.EventStore.Contracts.Tests/Packaging/ContractsPackageDependencyTests.InheritedOwnerEvidence.cs')}
    for root in (ROOT, FOLDERS):
        before = json.loads((PACKET / (root.name + '-source-before.json')).read_text())
        added = sorted(set(source(root)) - set(before))
        results[root.name + '-source-additions'] = added
        for path in added:
            if root != ROOT or path not in allowed_new:
                violations.append({'kind': 'unexpected-source-addition', 'path': path})
        observed = git(root, 'submodule', 'status')
        canonical = lambda value: {line[42:].split()[0]: line[1:41] for line in value.splitlines() if len(line) > 42}
        same = canonical(observed) == canonical(repos[str(root)]['submodules'])
        results[str(root) + '-dependency-revisions'] = {'observed': observed, 'unchanged': same}
        if not same:
            violations.append({'kind': 'dependency-revision-drift', 'repository': str(root)})
    for path, expected in json.loads((PACKET / 'folders-preexisting-spec-observed.json').read_text()).items():
        if not Path(path).is_file() or digest(Path(path)) != expected:
            violations.append({'kind': 'folders-existing-spec', 'path': path})
    results['violations'] = violations
    results['passed'] = not violations
    write('preservation-final', results)
    print(json.dumps({k: {'checked': v['checked'], 'changed': len(v['changes'])} for k, v in results.items() if isinstance(v, dict) and 'checked' in v}))
    if violations:
        raise SystemExit(1)


if __name__ == '__main__':
    {'initial': initial, 'verify': verify}[sys.argv[1]]()

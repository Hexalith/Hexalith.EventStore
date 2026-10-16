"""Recover only the spec-authorized, content-bound P1R closure; refuse drift."""
import sys

if sys.flags.optimize:
    raise SystemExit("P1R recovery refuses optimized Python; run without -O or PYTHONOPTIMIZE.")

from pathlib import Path
import collections
import datetime
import hashlib
import json
import os
import stat
import subprocess

ROOT = Path('/home/administrator/projects/hexalith/projects/references/Hexalith.EventStore')
WORKSPACE = ROOT.parents[1]
COMMIT = 'b7a1917cec2a59ffe80dc3ee977cf3aa31376b14'
PARENT = '210b90da172cc7ca946fbb06cfd44390bffa707b'
BASE = '5b0df296abc86adf8d37a92fae02e26574d19ad5'
PREFIX = '_bmad-output/implementation-artifacts/evidence/'
BUNDLES = ['6-1-p1r-31210-blocker-repair-20261014', '6-1-p1r-successor-enrollment-20261014']
COMPARE = BUNDLES + ['6-1-p1r-31210-published-run']
OUT = ROOT / PREFIX / '6-1-p1r-recovery-20261015'

def git(*argv, root=ROOT):
    return subprocess.check_output(['git', *argv], cwd=root)

def digest(data):
    return hashlib.sha256(data).hexdigest()

def snapshot(path):
    if not path.exists() and not path.is_symlink():
        return None
    if path.is_symlink():
        data = os.fsencode(os.readlink(path))
        return {'type': 'symlink', 'target': os.readlink(path), 'bytes': len(data), 'sha256': digest(data), 'mode': stat.S_IMODE(path.lstat().st_mode)}
    assert path.is_file(), f'not an ordinary file: {path}'
    data = path.read_bytes()
    return {'bytes': len(data), 'sha256': digest(data), 'mode': stat.S_IMODE(path.stat().st_mode)}

def save(name, value):
    with (OUT / name).open('x') as stream:
        json.dump(value, stream, indent=2)
        stream.write('\n')

assert git('rev-parse','HEAD').decode().strip() == BASE, 'owner HEAD changed'
assert git('rev-parse',COMMIT+'^').decode().strip() == PARENT
assert git('rev-parse','fix/p1r-successor-enrollment').decode().strip() == COMMIT
assert not OUT.exists(), 'recovery evidence destination already exists'
manifest_path = PREFIX + BUNDLES[1] + '/reviewed-source.json'
manifest = json.loads(git('show', COMMIT+':'+manifest_path))
assert len(manifest['files']) == 23
reviewed = {entry['path']: entry for entry in manifest['files']}
assert len(reviewed) == 23
assert all(not path.startswith('src/') for path in reviewed)
tree = {}
for entry in git('ls-tree','-rlz',COMMIT).split(b'\0'):
    if not entry:
        continue
    metadata, path = entry.split(b'\t',1)
    mode, kind, oid, size = metadata.split()
    if kind == b'blob':
        tree[path.decode()] = {'mode': mode.decode(), 'git_blob': oid.decode(), 'bytes': int(size)}
evidence_paths = sorted(path for path in tree if any(path.startswith(PREFIX+b+'/') for b in COMPARE))
paths = sorted(set(reviewed) | set(evidence_paths))
blobs = {}
inventory = []
for path in paths:
    record = tree[path]
    data = git('cat-file','blob',record['git_blob'])
    assert len(data) == record['bytes']
    assert hashlib.sha1(b'blob '+str(len(data)).encode()+b'\0'+data).hexdigest() == record['git_blob']
    expected = {'bytes': len(data), 'sha256': digest(data)}
    if path in reviewed:
        assert all(reviewed[path][key] == expected[key] for key in expected), f'manifest mismatch: {path}'
    start = snapshot(ROOT/path)
    is_evidence = path in evidence_paths
    identical = start is not None and all(start[key] == expected[key] for key in expected)
    if is_evidence and start is not None:
        assert identical, f'existing evidence conflicts: {path}'
    action = 'unchanged' if identical else 'recover-missing' if start is None else 'recover-reviewed-source'
    inventory.append({'path': path, **record, **expected, 'starting': start, 'action': action,
                      'reviewed_source': path in reviewed, 'compared_evidence': is_evidence})
    if action != 'unchanged':
        assert not is_evidence or any(path.startswith(PREFIX+b+'/') for b in BUNDLES), f'unapproved missing evidence: {path}'
        blobs[path] = data
# Verify every delivered item against its own manifest as well as its Git blob.
delivery = json.loads(git('show', COMMIT+':'+PREFIX+BUNDLES[1]+'/delivery-manifest.json'))
assert len(delivery['files']) == 117
for item in delivery['files']:
    path = PREFIX+BUNDLES[1]+'/'+item['path']
    record = next(row for row in inventory if row['path'] == path)
    assert all(record[key] == item[key] for key in ['bytes','sha256']), f'delivery mismatch: {path}'
helpers = json.loads(git('show', COMMIT+':'+PREFIX+BUNDLES[1]+'/required-ci-test-dependencies.json'))
assert len(helpers['files']) == 4
for item in helpers['files']:
    assert reviewed[item['path']] == item, f'helper mismatch: {item["path"]}'
# Protect every tracked file outside authorized changes, including the user-edited Story 6.1.
protected = []
for repo in [WORKSPACE, ROOT]:
    tracked = [os.fsdecode(p) for p in git('ls-files','-z',root=repo).split(b'\0') if p]
    existing_evidence = [str(p.relative_to(repo)) for p in (repo/PREFIX).rglob('*') if p.is_file() and not p.is_symlink()]
    if repo == WORKSPACE:
        tracked += ['_bmad-output/implementation-artifacts/spec-6-1-p1r-5.md']
    for path in sorted(set(tracked + existing_evidence)):
        absolute = repo/path
        if absolute.is_dir():
            continue
        if repo == ROOT and path in blobs:
            continue
        protected.append({'repository': str(repo), 'path': path, 'starting': snapshot(absolute)})
source_state = {'captured_at_utc': datetime.datetime.now(datetime.timezone.utc).isoformat(),
                'recovery_commit': COMMIT, 'recovery_parent': PARENT,
                'authoritative_manifest': manifest_path, 'manifest_sha256': digest(git('show',COMMIT+':'+manifest_path)),
                'repositories': []}
for repo in [WORKSPACE, ROOT]:
    source_state['repositories'].append({'path': str(repo), 'head': git('rev-parse','HEAD',root=repo).decode().strip(),
      'branch': git('branch','--show-current',root=repo).decode().strip(),
      'status': git('status','--short',root=repo).decode(), 'remotes': git('remote','-v',root=repo).decode(),
      'recent_history': git('log','-5','--oneline',root=repo).decode(),
      'index_sha256': digest((repo / git('rev-parse','--git-path','index',root=repo).decode().strip()).read_bytes()),
      'gitlinks': git('ls-tree','-r','HEAD',root=repo).decode().splitlines()})
    source_state['repositories'][-1]['gitlinks'] = [line for line in source_state['repositories'][-1]['gitlinks'] if line.startswith('160000 ')]
summary = {'reviewed_source_actions': dict(collections.Counter(row['action'] for row in inventory if row['reviewed_source'])),
           'evidence_actions': dict(collections.Counter(row['action'] for row in inventory if row['compared_evidence'])),
           'missing_evidence_bytes': sum(row['bytes'] for row in inventory if row['compared_evidence'] and row['action']=='recover-missing'),
           'delivery_entries_verified': len(delivery['files']), 'required_ci_helpers_verified': len(helpers['files']),
           'protected_files': len(protected)}
# Recheck every planned destination and all protected bytes before creating the receipt directory.
for row in inventory:
    assert snapshot(ROOT/row['path']) == row['starting'], f'concurrent destination change: {row["path"]}'
for row in protected:
    assert snapshot(Path(row['repository'])/row['path']) == row['starting'], f'concurrent protected change: {row["path"]}'
OUT.mkdir()
save('initial-source-state.json', source_state)
save('protected-files-before.json', protected)
save('recovery-inventory.json', {'summary': summary, 'files': inventory})
(OUT/'recover-reviewed-closure.py').write_bytes(Path(__file__).read_bytes())
# No previously existing evidence file is opened for write. Exclusive creation refuses races.
for row in inventory:
    path = ROOT/row['path']
    assert snapshot(path) == row['starting'], f'concurrent destination change: {row["path"]}'
    if row['action'] == 'unchanged':
        continue
    path.parent.mkdir(parents=True, exist_ok=True)
    if row['starting'] is None:
        with path.open('xb') as stream:
            stream.write(blobs[row['path']])
        path.chmod(int(row['mode'], 8) & 0o777)
    else:
        # Open without truncation, compare again through that descriptor, then write exact bytes.
        with path.open('r+b') as stream:
            current = stream.read()
            assert len(current) == row['starting']['bytes'] and digest(current) == row['starting']['sha256'], f'concurrent open change: {path}'
            stream.seek(0)
            stream.write(blobs[row['path']])
            stream.truncate()
for row in inventory:
    current = snapshot(ROOT/row['path'])
    assert all(current[key] == row[key] for key in ['bytes','sha256']), f'recovery verification failed: {row["path"]}'
for row in protected:
    assert snapshot(Path(row['repository'])/row['path']) == row['starting'], f'preservation failed: {row["path"]}'
save('recovery-result.json', {'passed': True, 'summary': summary, 'all_recovery_git_blobs_verified': True,
     'reviewed_source_hashes_match': True, 'delivery_hashes_match': True, 'protected_bytes_preserved': True,
     'historical_evidence_overwritten': False, 'package_qualified': False, 'p1r_usable': False, 'story_6_1': 'blocked'})
print(json.dumps(summary, indent=2))

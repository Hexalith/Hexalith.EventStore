"""Recheck all starting bytes, recovered manifests and Git boundaries after fresh checks."""
from pathlib import Path
import datetime
import gzip
import hashlib
import json
import os
import re
import stat
import subprocess

OUT = Path(__file__).resolve().parent
ROOT = OUT.parents[3]


def sha(data):
    return hashlib.sha256(data).hexdigest()


def snapshot(path):
    if not path.exists() and not path.is_symlink():
        return None
    if path.is_symlink():
        data = os.fsencode(os.readlink(path))
        return {'type': 'symlink', 'target': os.readlink(path), 'bytes': len(data), 'sha256': sha(data),
                'mode': stat.S_IMODE(path.lstat().st_mode)}
    assert path.is_file(), str(path)
    data = path.read_bytes()
    return {'bytes': len(data), 'sha256': sha(data), 'mode': stat.S_IMODE(path.stat().st_mode)}


def save(name, value):
    with (OUT/name).open('x') as stream:
        json.dump(value, stream, indent=2)
        stream.write('\n')


def authorized_workflow_change(row, actual, authorizations):
    """Permit only exact reviewed metadata snapshots with their original bytes independently bound."""
    approval = next((item for item in authorizations if item['repository'] == row['repository']
                     and item['path'] == row['path']), None)
    if approval is None or approval['starting'] != row['starting'] or approval['approved'] != actual:
        return False
    data = (Path(row['repository'])/row['path']).read_bytes()
    if approval['kind'] == 'nine-review-deferrals':
        length = approval['prefix_bytes']
        return (length == row['starting']['bytes']
                and sha(data[:length]) == row['starting']['sha256'] == approval['prefix_sha256']
                and data[length:].count(b'- source_spec:') == approval['appended_review_entries'] == 9)
    if approval['kind'] == 'workflow-metadata':
        original = data.replace(b"status: 'in-review'\r\n", b"status: 'in-progress'\r\n").replace(b'- [x]', b'- [ ]')
        original = re.sub(rb'(## Implementation Notes\r\n\r\n).*?(?=## Spec Change Log)',
                          rb'\1', original, flags=re.S)
        original = re.sub(rb'(## Review Triage Log\r\n\r\n).*?(?=## Verification)',
                          rb'\1', original, flags=re.S)
        return len(original) == row['starting']['bytes'] and sha(original) == row['starting']['sha256']
    return False


if __name__ == '__main__':
    encoded = json.loads((OUT/'protected-files-encoding.json').read_text())
    zipped = (OUT/encoded['path']).read_bytes()
    raw = gzip.decompress(zipped)
    assert sha(zipped) == encoded['compressed_sha256']
    assert sha(raw) == encoded['uncompressed_sha256'] and len(raw) == encoded['uncompressed_bytes']
    protected = json.loads(raw)
    authorizations = json.loads((OUT/'authorized-workflow-changes-v2.json').read_text())['files']
    authorized_changes = []
    mismatches = []
    for row in protected:
        actual = snapshot(Path(row['repository'])/row['path'])
        if actual != row['starting']:
            if authorized_workflow_change(row, actual, authorizations):
                authorized_changes.append({'repository': row['repository'], 'path': row['path'], 'actual': actual})
            else:
                mismatches.append({'repository': row['repository'], 'path': row['path']})
    inventory = json.loads((OUT/'recovery-inventory.json').read_text())
    bad_recovery = []
    for row in inventory['files']:
        actual = snapshot(ROOT/row['path'])
        if actual is None or any(actual[key] != row[key] for key in ['bytes','sha256']):
            bad_recovery.append(row['path'])
    binding = json.loads((OUT/'fresh-source-binding.json').read_text())
    changed_check_inputs = []
    for row in binding['files']:
        actual = snapshot(ROOT/row['path'])
        if actual is None or any(actual[key] != row[key] for key in ['bytes','sha256']):
            changed_check_inputs.append(row['path'])
    before = json.loads((OUT/'initial-source-state.json').read_text())
    repositories = []
    for baseline in before['repositories']:
        repo = Path(baseline['path'])
        commands = []
        for argv in [['git','diff','--check'], ['git','diff','--cached','--quiet'],
                     ['git','diff','--name-only','--','src/']]:
            result = subprocess.run(argv,cwd=repo,text=True,stdout=subprocess.PIPE,stderr=subprocess.PIPE)
            commands.append({'argv':argv,'cwd':str(repo),'exit_code':result.returncode,
                             'stdout':result.stdout,'stderr':result.stderr})
        head = subprocess.check_output(['git','rev-parse','HEAD'],cwd=repo,text=True).strip()
        branch = subprocess.check_output(['git','branch','--show-current'],cwd=repo,text=True).strip()
        gitlinks = [line for line in subprocess.check_output(['git','ls-tree','-r','HEAD'],cwd=repo,text=True).splitlines()
                    if line.startswith('160000 ')]
        index = repo/subprocess.check_output(['git','rev-parse','--git-path','index'],cwd=repo,text=True).strip()
        repositories.append({'path':str(repo),'head':head,'branch':branch,
            'head_unchanged':head == baseline['head'],'branch_unchanged':branch == baseline['branch'],
            'gitlinks_unchanged':gitlinks == baseline['gitlinks'],
            'index_sha256':sha(index.read_bytes()),'index_bytes_unchanged':sha(index.read_bytes()) == baseline['index_sha256'],
            'commands':commands})
    passed = not mismatches and not bad_recovery and not changed_check_inputs and all(
        repo['head_unchanged'] and repo['branch_unchanged'] and repo['gitlinks_unchanged'] and repo['index_bytes_unchanged']
        and all(command['exit_code']==0 and not command['stdout'] for command in repo['commands']) for repo in repositories)
    result = {'captured_at_utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),
        'protected_files_checked':len(protected),'protected_mismatches':mismatches,
        'authorized_workflow_changes':authorized_changes,
        'recovery_files_checked':len(inventory['files']),'recovery_mismatches':bad_recovery,
        'fresh_check_source_inputs':len(binding['files']),'changed_check_inputs':changed_check_inputs,
        'repositories':repositories,'passed':passed,'package_qualified':False,'p1r_usable':False,'story_6_1':'blocked'}
    save('final-preservation-v2.json',result)
    print(json.dumps(result,indent=2))
    raise SystemExit(0 if passed else 1)

#!/usr/bin/env python3
"""Parent checks for immutable inputs, task scope and independent wire bytes.

This checks specification artifacts only. It confers no provider or AD-13 authority.
Run from the repository root; the retained historical evidence is audited separately.
"""
import hashlib
import gzip
import json
import re
import subprocess
import tempfile
from pathlib import Path

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
ART = HERE.parent
RUN_HEAD = 'ab11c86a991a6aa8349041e73312a9655a0aa1bf'
PROTECTED = {
    'spec-6-5a-event-contract-writer-and-migration-evidence.md': '31f63fc0ea54043e7b821f311b28d3e856667789eac9eca8f79807f6d2e74444',
    'spec-6-5b-verified-read-replay-and-projection.md': 'b889951bc248a7a4d19067a84d197bbe8dd1bc14457d56ed5665cc9c72347152',
    'spec-6-5c-publication-subscription-and-rollout.md': 'f24116aaf4dc3cd041034f40a1d858e114f9f8a4219b1ca0cc40bdea80385ca9',
    'spec-event-versioning-upcasting.md': 'c474df76a687b2798757051efbdb382bb6b9650f7bd7a7637d0c67bfc5e7b715',
}
ARCHIVES = {
    'previous-candidate.md': 'fb0c7bec739df1752bc4bc47bd8aa223f74f709fbb6e4926d4be1992c8bc6954',
    'previous-execution.md': 'a2556eea423ac546aca15e7405f2cf64012a544966fec80464de27e905f87386',
}


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def git(*args):
    return subprocess.check_output(['git', *args], cwd=ROOT)


def disposition_ids(text):
    section = text.split('<!-- pass2-dispositions-start -->')[1].split('<!-- pass2-dispositions-end -->')[0]
    return re.findall(r'^\| ((?:VG2|BH2|E2)-[^ |]+) \|', section, re.M)


def exact_integers(value):
    if type(value) is int:
        return {'_integer': str(value)}
    if type(value) is list:
        return [exact_integers(x) for x in value]
    if type(value) is dict:
        return {key: exact_integers(x) for key, x in value.items()}
    return value


def independent_input(answers):
    result = json.loads(json.dumps(answers))
    for row in result['records'].values():
        if 'fields' in row:
            for field in row['fields']:
                if field[0].removeprefix('O:') in ('N', 'Q', 'I', 'P') and field[1] is not None:
                    field[1] = str(field[1])
        if 'json' in row:
            row['json'] = exact_integers(row['json'])
    for row in result['keys'].values():
        for field in row['fields']:
            if field[0].removeprefix('O:') in ('N', 'Q', 'I', 'P') and field[1] is not None:
                field[1] = str(field[1])
    for row in result['controls'].values():
        row['json'] = exact_integers(row['json'])
    return result


def main():
    checkpoint_bytes = gzip.decompress((HERE / 'recovery-checkpoint.json.gz').read_bytes())
    assert sha(checkpoint_bytes) == 'bad90f07b40ca2555f962717e38ce07e6a0d7f5b38220be66989a3eeb9825b7e'
    checkpoint = json.loads(checkpoint_bytes)
    assert checkpoint['head'] == RUN_HEAD
    for name, row in checkpoint['protected'].items():
        if 'sha256' in row:
            assert sha((ROOT / name).read_bytes()) == row['sha256'], ('protected recovery input changed', name)
        else:
            actual = subprocess.check_output(['git', '-C', str(ROOT / name), 'rev-parse', 'HEAD'], text=True).strip()
            assert actual == row['gitlink'], ('protected recovery submodule changed', name)
    for name, expected in PROTECTED.items():
        assert sha((ART / name).read_bytes()) == expected, name
    for name, expected in ARCHIVES.items():
        assert sha((HERE / name).read_bytes()) == expected, name
    execution = (ART / 'spec-6-5d-hold-lifecycle-resume-and-legacy-admission-2.md').read_bytes()
    frozen = re.search(rb'<frozen-after-approval[^>]*>.*?</frozen-after-approval>', execution, re.S).group()
    assert len(frozen) == 3569
    assert sha(frozen) == '9234f13ebb6aef6634fff07a239be0b32c948d4fe9ddca2e49a6a77d8eec10d9'
    old_ids = disposition_ids((HERE / 'previous-candidate.md').read_text())
    new_ids = disposition_ids((HERE / 'obligations.md').read_text())
    assert len(new_ids) == len(set(new_ids)) == 54 and set(old_ids) == set(new_ids)
    ad13 = (ART / 'spec-event-versioning-upcasting.md').read_text()
    for label in ('ApprovalDigest', 'Approver', 'ApprovalDateUtc', 'Authorization', 'ApprovalEvidence'):
        assert f'{label}: UNAPPROVED' in ad13, label
    assert git('rev-parse', 'HEAD').decode().strip() == RUN_HEAD, 'Git history changed'
    prefix = '_bmad-output/implementation-artifacts/'
    allowed = {prefix + name for name in (
        'spec-6-5d-hold-lifecycle-resume-and-legacy-admission.md',
        'spec-6-5d-hold-lifecycle-resume-and-legacy-admission-2.md',
        'sprint-status.yaml', 'deferred-work.md')}
    changed = set(git('diff', RUN_HEAD, '--name-only').decode().splitlines())
    changed |= {row[3:] for row in git('status', '--porcelain', '--untracked-files=all').decode().splitlines() if row.startswith('?? ')}
    outside = {name for name in changed if name not in allowed and not name.startswith(prefix + '6-5d-simplification/')}
    assert not outside, ('changes outside recovery scope', sorted(outside))
    links = []
    for row in git('ls-tree', 'HEAD').decode().splitlines():
        # Root gitlinks are inside references; enumerate them without recursive updates.
        if row.startswith('040000 tree ') and row.endswith('\treferences'):
            for link in git('ls-tree', 'HEAD:references').decode().splitlines():
                metadata, name = link.split('\t')
                if metadata.startswith('160000 commit '):
                    expected = metadata.split()[2]
                    actual = subprocess.check_output(['git', '-C', str(ROOT / 'references' / name), 'rev-parse', 'HEAD'], text=True).strip()
                    assert actual == expected, ('submodule changed', name)
                    links.append({'path': 'references/' + name, 'revision': actual})
    historical = json.loads((HERE / 'prior-evidence/manifest.json').read_text())
    for name, row in historical.items():
        raw = (HERE / 'prior-evidence' / name).read_bytes()
        assert len(raw) == row['bytes'] and sha(raw) == row['sha256'], name
    answers = json.loads((HERE / 'known-answers.json').read_text())
    with tempfile.TemporaryDirectory(prefix='6-5d-independent-') as directory:
        input_path = Path(directory) / 'input.json'
        input_path.write_text(json.dumps(independent_input(answers), ensure_ascii=False))
        command = ['node', str(HERE / 'prior-evidence/verify-6-5d-simplified-independent.mjs'), str(input_path)]
        result = subprocess.run(command, capture_output=True, text=True)
        assert result.returncode == 0, result.stdout + result.stderr
    shared = subprocess.run(['node', str(HERE / 'independent-shared-keys.mjs'),
                             str(HERE / 'known-answers.json')], capture_output=True, text=True)
    assert shared.returncode == 0, shared.stdout + shared.stderr
    active = ['verify.py', 'known-answers.json', 'obligations.md']
    hashes = {name: sha((HERE / name).read_bytes()) for name in active}
    candidate = ART / 'spec-6-5d-hold-lifecycle-resume-and-legacy-admission.md'
    hashes[candidate.name] = sha(candidate.read_bytes())
    print(json.dumps({'result': 'passed', 'runHead': RUN_HEAD,
                      'recoveryCheckpointProtectedPaths': len(checkpoint['protected']),
                      'recoveryCheckpointSha256': sha((HERE / 'recovery-checkpoint.json.gz').read_bytes()),
                      'scopeChangedPaths': sorted(changed), 'submodules': links,
                      'frozenIntentSha256': sha(frozen), 'archives': ARCHIVES,
                      'protectedSourceHashes': PROTECTED, 'routedDispositions': len(new_ids),
                      'historicalEvidenceFiles': len(historical),
                      'independentWireCheck': result.stdout.strip(),
                      'independentSharedKeyCheck': shared.stdout.strip(),
                      'activeArtifactSha256': hashes}, indent=2, sort_keys=True))


if __name__ == '__main__':
    main()

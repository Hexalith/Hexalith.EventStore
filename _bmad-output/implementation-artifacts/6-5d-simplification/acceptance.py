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
import tarfile
import tempfile
from pathlib import Path

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
ART = HERE.parent
RUN_HEAD = '5e32d07a6ac7a1bf70cc0ca554ea9928145b65f6'
CHECKPOINT_SHA = '35855fdc2e0ce9a8f1662458bae2544db85f98bfd6c26b0397cc1ecc8a0bee01'
CORRECTION_FROZEN_SHA = '4fe2612ae350650cf6108f42afdc74f8e77f0450a0b1a1dbe8911535c1b81e88'
SNAPSHOT_MANIFEST_SHA = '8d96bc38e3134addfbdbb98398a2befc0c513720dccacb429a4d27a7af26ed97'
SNAPSHOT_SHA = 'e8b07ff82c7b9bec7ab1fc7ea1a65d6f9649e4f9a87c8e2230c93bb69a9431bc'
PREFIX = '_bmad-output/implementation-artifacts/'
ALLOWED = {PREFIX + name for name in (
    'spec-6-5d-hold-lifecycle-resume-and-legacy-admission.md',
    'spec-6-5d-hold-lifecycle-resume-and-legacy-admission-2.md',
    'spec-6-5d-hold-lifecycle-resume-and-legacy-admission-3.md',
    'sprint-status.yaml', 'deferred-work.md')}
ALLOWED_PREFIX = PREFIX + '6-5d-simplification/'
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


def tracker_remainder(name, text):
    if name == 'sprint-status.yaml':
        return '\n'.join(line for line in text.splitlines()
                         if not line.startswith('# last_updated:') and
                         not line.startswith('  6-5d-hold-lifecycle-resume-and-legacy-admission-spec:'))
    marker = '  summary: Create and run Story 6.5d.'
    assert text.count(marker) == 1, 'D-SPLIT entry missing or duplicated'
    start = text.rfind('\n- source_spec:', 0, text.index(marker))
    end = text.index('\n\n', text.index(marker))
    return text[:start] + text[end:]


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
    for row in result.get('public', {}).values():
        row['json'] = exact_integers(row['json'])
    return result


def main():
    checkpoint_bytes = gzip.decompress((HERE / 'correction-checkpoint.json.gz').read_bytes())
    assert sha(checkpoint_bytes) == CHECKPOINT_SHA, 'correction checkpoint changed'
    checkpoint = json.loads(checkpoint_bytes)
    assert checkpoint['head'] == RUN_HEAD
    assert set(checkpoint['allowedPaths']) == ALLOWED and checkpoint['allowedPrefix'] == ALLOWED_PREFIX
    ancestor = subprocess.run(['git', 'merge-base', '--is-ancestor', RUN_HEAD, 'HEAD'], cwd=ROOT,
                              capture_output=True, text=True)
    assert ancestor.returncode == 0, ('correction baseline is not an ancestor', ancestor.stderr)
    # Audit every committed path as well as the final working tree. A forbidden
    # change later reverted in another commit is still outside this run's scope.
    committed = set(git('log', '--format=', '--name-only', RUN_HEAD + '..HEAD').decode().splitlines()) - {''}
    changed = set(git('diff', RUN_HEAD, '--name-only', '-z').decode().split('\0')) - {''}
    untracked = set(git('ls-files', '--others', '--exclude-standard', '-z').decode().split('\0')) - {''}
    changed |= untracked | committed
    outside = {name for name in changed if name not in ALLOWED and not name.startswith(ALLOWED_PREFIX)}
    assert not outside, ('changes outside correction scope', sorted(outside))
    for name in ('sprint-status.yaml', 'deferred-work.md'):
        baseline_text = git('show', RUN_HEAD + ':' + PREFIX + name).decode()
        current_text = (ART / name).read_text()
        assert tracker_remainder(name, current_text) == tracker_remainder(name, baseline_text), (
            'unrelated tracker entries changed', name)
    for name, row in checkpoint['protected'].items():
        if 'sha256' in row:
            assert sha((ROOT / name).read_bytes()) == row['sha256'], ('protected correction input changed', name)
        else:
            actual = subprocess.check_output(['git', '-C', str(ROOT / name), 'rev-parse', 'HEAD'], text=True).strip()
            assert actual == row['gitlink'], ('protected correction submodule changed', name)
    # Earlier baseline hashes remain authenticated historical evidence. They do
    # not claim that later, pre-existing owner changes never happened.
    old_checkpoint = gzip.decompress((HERE / 'recovery-checkpoint.json.gz').read_bytes())
    assert sha(old_checkpoint) == 'bad90f07b40ca2555f962717e38ce07e6a0d7f5b38220be66989a3eeb9825b7e'
    manifest_raw = (HERE / 'pre-correction-manifest.json').read_bytes()
    assert sha(manifest_raw) == SNAPSHOT_MANIFEST_SHA, 'pre-correction manifest changed'
    snapshot = json.loads(manifest_raw)
    assert snapshot['head'] == RUN_HEAD and snapshot['checkpointSha256'] == CHECKPOINT_SHA
    assert snapshot['archiveSha256'] == SNAPSHOT_SHA
    assert sha((HERE / 'pre-correction-evidence.tar.gz').read_bytes()) == SNAPSHOT_SHA
    with tarfile.open(HERE / 'pre-correction-evidence.tar.gz', 'r:gz') as archive:
        members = archive.getmembers()
        assert len(members) == len(snapshot['files']) and len({m.name for m in members}) == len(members)
        assert {m.name for m in members} == set(snapshot['files'])
        for member in members:
            assert member.isfile(), ('unexpected snapshot entry', member.name)
            raw = archive.extractfile(member).read()
            row = snapshot['files'][member.name]
            assert len(raw) == row['bytes'] and sha(raw) == row['sha256'], member.name
            if member.name == ALLOWED_PREFIX + 'known-answers.json':
                original_answers = json.loads(raw)
    # These historical inputs are intentionally still byte-identical outside
    # the snapshot too: old probes remain attributed to their original limits.
    preserved_history = ['recovery-checkpoint.json.gz', 'reviews/owner-and-cleanup-probes.py',
                         'reviews/restore-probe.py', 'reviews/pre-fix-probe-output.txt']
    for name in preserved_history:
        assert sha((HERE / name).read_bytes()) == snapshot['files'][ALLOWED_PREFIX + name]['sha256'], name
    for name, expected in PROTECTED.items():
        assert sha((ART / name).read_bytes()) == expected, name
    for name, expected in ARCHIVES.items():
        assert sha((HERE / name).read_bytes()) == expected, name
    execution = (ART / 'spec-6-5d-hold-lifecycle-resume-and-legacy-admission-2.md').read_bytes()
    frozen = re.search(rb'<frozen-after-approval[^>]*>.*?</frozen-after-approval>', execution, re.S).group()
    assert len(frozen) == 3569
    assert sha(frozen) == '9234f13ebb6aef6634fff07a239be0b32c948d4fe9ddca2e49a6a77d8eec10d9'
    correction = (ART / 'spec-6-5d-hold-lifecycle-resume-and-legacy-admission-3.md').read_bytes()
    correction_frozen = re.search(rb'<frozen-after-approval[^>]*>.*?</frozen-after-approval>', correction, re.S).group()
    assert sha(correction_frozen) == CORRECTION_FROZEN_SHA, 'approved correction intent changed'
    old_ids = disposition_ids((HERE / 'previous-candidate.md').read_text())
    new_ids = disposition_ids((HERE / 'obligations.md').read_text())
    assert len(new_ids) == len(set(new_ids)) == 54 and set(old_ids) == set(new_ids)
    ad13 = (ART / 'spec-event-versioning-upcasting.md').read_text()
    for label in ('ApprovalDigest', 'Approver', 'ApprovalDateUtc', 'Authorization', 'ApprovalEvidence'):
        assert f'{label}: UNAPPROVED' in ad13, label
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
    retained_answers = {}
    for family in ('records', 'keys', 'sharedKeys'):
        for name, row in original_answers[family].items():
            assert answers[family].get(name) == row, ('retained public bytes or descriptor changed', family, name)
        retained_answers[family] = len(original_answers[family])
    with tempfile.TemporaryDirectory(prefix='6-5d-independent-') as directory:
        input_path = Path(directory) / 'input.json'
        input_path.write_text(json.dumps(independent_input(answers), ensure_ascii=False))
        command = ['node', str(HERE / 'independent-answers.mjs'), str(input_path), str(HERE / 'obligations.md')]
        result = subprocess.run(command, capture_output=True, text=True)
        assert result.returncode == 0, result.stdout + result.stderr
        legacy_constructor = subprocess.run(['node', str(HERE / 'prior-evidence/verify-6-5d-simplified-independent.mjs'),
                                             str(input_path)], capture_output=True, text=True)
        assert legacy_constructor.returncode == 0, legacy_constructor.stdout + legacy_constructor.stderr
    shared = subprocess.run(['node', str(HERE / 'independent-shared-keys.mjs'),
                             str(HERE / 'known-answers.json')], capture_output=True, text=True)
    assert shared.returncode == 0, shared.stdout + shared.stderr
    active = sorted({'known-answers.json', 'obligations.md', 'independent-shared-keys.mjs', 'independent-answers.mjs',
                     'reviews/focused-regressions.py'} |
                    {path.name for path in HERE.glob('*.py')})
    hashes = {name: sha((HERE / name).read_bytes()) for name in active}
    candidate = ART / 'spec-6-5d-hold-lifecycle-resume-and-legacy-admission.md'
    hashes[candidate.name] = sha(candidate.read_bytes())
    print(json.dumps({'result': 'passed', 'authority': 'specification evidence only; no provider or AD-13 approval',
                      'runHead': RUN_HEAD, 'currentHead': git('rev-parse', 'HEAD').decode().strip(),
                      'baselineIsAncestor': True,
                      'correctionCheckpointProtectedPaths': len(checkpoint['protected']),
                      'correctionCheckpointSha256': CHECKPOINT_SHA,
                      'historicalRecoveryHead': json.loads(old_checkpoint)['head'],
                      'historicalRecoveryCheckpointSha256': sha(old_checkpoint),
                      'preCorrectionSnapshotFiles': len(snapshot['files']),
                      'preCorrectionSnapshotSha256': SNAPSHOT_SHA,
                      'scopeChangedPaths': sorted(changed), 'submodules': links,
                      'frozenIntentSha256': sha(frozen), 'approvedCorrectionFrozenSha256': sha(correction_frozen),
                      'archives': ARCHIVES,
                      'protectedSourceHashes': PROTECTED, 'routedDispositions': len(new_ids),
                      'historicalEvidenceFiles': len(historical),
                      'retainedAnswers': retained_answers,
                      'independentWireCheck': result.stdout.strip(),
                      'independentPublicAnswers': len(answers.get('public', {})),
                      'historicalConstructorCompatibility': legacy_constructor.stdout.strip(),
                      'independentSharedKeyCheck': shared.stdout.strip(),
                      'activeArtifactSha256': hashes}, indent=2, sort_keys=True))


if __name__ == '__main__':
    main()

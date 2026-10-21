#!/usr/bin/env python3
"""Verify retained evidence and its incomplete disposition; never grant qualification."""
import copy
import datetime
import hashlib
import json
from pathlib import Path
import sys

HERE = Path(__file__).resolve().parent
BASE = HERE.parent
PACKETS = ['runs-2026-10-21-concurrency-safety', 'runs-2026-10-21-concurrency-retirement', HERE.name]
EXCLUDED = {HERE.name + '/index.json', HERE.name + '/verification.json'}

def need(value, message):
    if not value:
        raise RuntimeError(message)

def sha(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()

def read(path):
    return json.loads(path.read_text())

def check_index(index):
    need(set(index['excluded']) == EXCLUDED, 'unexpected exclusions')
    for name in index['files']:
        need(not Path(name).is_absolute() and '..' not in Path(name).parts, 'unsafe path')
        need((BASE / name).resolve().is_relative_to(BASE.resolve()), 'path escapes evidence root')
    actual = set()
    for directory in PACKETS:
        for path in (BASE / directory).rglob('*'):
            need(not path.is_symlink(), 'symlink in retained packet')
            if path.is_file():
                actual.add(path.relative_to(BASE).as_posix())
    actual.add('qualification-2026-10-21-concurrency-retirement.md')
    need(actual - EXCLUDED == set(index['files']), 'packet membership mismatch')
    for name, item in index['files'].items():
        path = BASE / name
        need(path.stat().st_size == item['bytes'] and sha(path) == item['sha256'], 'bytes mismatch: ' + name)

def check_persistence(item, packet):
    observed = item['restoredActorPostPersistenceUtc']
    need(bool(observed), 'correlated persistence observation missing')
    started = datetime.datetime.fromisoformat(item['invocationStartedUtc'])
    completed = datetime.datetime.fromisoformat(item['invocationCompletedUtc'])
    broadcast = datetime.datetime.fromisoformat(item['broadcastStartedUtc'])
    need(started <= completed <= broadcast, 'invocation/broadcast ordering changed')
    found = []
    path = packet / item['manifest']
    for line in (path.parent / 'second.log').read_text().splitlines():
        if ' {' not in line:
            continue
        try:
            data = json.loads('{' + line.split(' {', 1)[1])
        except ValueError:
            continue
        utc = line.split()[0]
        if (data.get('Message', '').startswith('ETag regenerated for actor ' + item['actor'] + '.')
                and started <= datetime.datetime.fromisoformat(utc) <= completed):
            found.append(utc)
    need(sorted(found) == sorted(observed), 'persistence observations do not match correlated raw logs')

def check_measurements():
    p = BASE / PACKETS[1]
    old = BASE / PACKETS[0]
    identity = read(p / 'candidate-runtime-identity.json')
    need(identity['candidateSha256'] == 'a2721c252fffee9ba529826cca4ab7050c4367d994f8c4916730046a7a276df6', 'candidate identity differs')
    need(identity['compiledDependenciesEqualRetainedCandidate'] is True, 'dependency evidence missing')
    need(sha(p / 'candidate-from-upstream.patch') == identity['patchSha256'], 'patch identity mismatch')
    need(read(p / 'before-candidate-build-source-inventory.json') == read(p / 'after-candidate-build-source-inventory.json'), 'build source changed')
    for packet in [old, p]:
        for receipt in packet.glob('*.json'):
            data = read(receipt)
            if 'command' not in data or 'outputSha256' not in data:
                continue
            need(sha(receipt.with_suffix('.log')) == data['outputSha256'], 'command log mismatch: ' + receipt.name)
            for a, b in [('sourcesBefore', 'sourcesAfter'), ('externalInputsBefore', 'externalInputsAfter'), ('binariesBefore', 'binariesAfter')]:
                if a in data:
                    if data[a] != data[b]:
                        need(receipt.name == 'retirement-retained-redis-regression.json' and a == 'binariesBefore', 'undeclared input drift: ' + receipt.name)
                        declared = read(HERE / 'measurement-input-drift.json')
                        actual = {k: {'before': data[a].get(k), 'after': data[b].get(k)} for k in set(data[a]) | set(data[b]) if data[a].get(k) != data[b].get(k)}
                        need(actual == declared['differences'] and sha(receipt) == declared['receiptSha256'], 'measurement drift not fully declared')
            for source, expected in data.get('sourcesBefore', {}).items():
                retained = packet / data['snapshot'] / data['retainedSourcePaths'][source]
                need(sha(retained) == expected, 'source snapshot mismatch: ' + str(retained))
    good = ['retirement-final-focused-controls', 'retirement-runtime-build', 'retirement-final-focused-reused-endpoint',
            'retirement-final-complete-distribution-class', 'retirement-final-go-regressions',
            'retirement-focused-application-regressions', 'retirement-retained-redis-regression']
    for name in good:
        need(read(p / (name + '.json'))['exitCode'] == 0, 'required local pass missing: ' + name)
    for name in ['retirement-real-projection-regression', 'retirement-ordinary-direct-regression',
                 'retirement-complete-distribution-class', 'retirement-cancel-guard-mutation-red']:
        need(read(p / (name + '.json'))['exitCode'] != 0, 'retained failure missing: ' + name)
    for name in ['retained-candidate-concurrency-red', 'pinned-reset-resolver-churn', 'cancel-guard-mutation-red']:
        need(read(old / (name + '.json'))['exitCode'] != 0, 'earlier failure missing: ' + name)
    focused = read(p / 'retirement-final-focused-reused-endpoint.json')
    complete = read(p / 'retirement-final-complete-distribution-class.json')
    regress = read(p / 'retirement-final-go-regressions.json')
    need(focused['completedUtc'] <= complete['startedUtc'] <= complete['completedUtc'] <= regress['startedUtc'], 'required lane order changed')
    need(not any('stoponfail' in arg.lower() for arg in complete['command']), 'class stops early')
    need('Total: 6, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0' in (p / 'retirement-final-complete-distribution-class.log').read_text(), 'six-case result missing')
    for data in [focused, complete]:
        need(data['fixtureRuntimePath'] == identity['candidatePath'], 'fixture runtime path mismatch')
        need(data['fixtureRuntimeSha256Before'] == data['fixtureRuntimeSha256After'] == identity['candidateSha256'], 'fixture runtime changed')
    timelines = read(p / 'recovery-timelines.json')['timelines']
    need(len(timelines) == 4, 'four measured timelines missing')
    for item in timelines:
        need(item['candidateSha256'] == identity['candidateSha256'] and item['sameInternalPort'], 'timeline identity/endpoint mismatch')
        need(item['initialOwner'] == 'second-proof-host' and item['signalWaitBoundMilliseconds'] == 15000, 'ownership/deadline evidence missing')
        need(item['observedRetirementAndRecoveryWithinMeasuredInvocation'], 'causal retirement observation absent')
        check_persistence(item, p)
        path = p / item['manifest']
        need(sha(path) == item['manifestSha256'], 'timeline manifest changed')
        for role in ['first', 'second']:
            need(sha(path.parent / (role + '.log')) == item[role + 'LogSha256'], 'timeline log changed')
    empty = copy.deepcopy(timelines[0])
    empty['restoredActorPostPersistenceUtc'] = []
    try:
        check_persistence(empty, p)
    except RuntimeError:
        pass
    else:
        raise RuntimeError('empty-persistence tamper control accepted')
    review = HERE / 'review-lifetime-controls'
    for label, code in [('lifetime-controls', 0), ('caller-lease-candidate', 0),
                        ('caller-lease-extra-release-mutant', 1), ('full-router-manager-review', 0)]:
        record = read(review / (label + '.json'))
        need(record['exitCode'] == code and record['preservedInputsUnchanged'], 'review control outcome changed: ' + label)
        need(sha(review / record['outputFile']) == record['outputSha256'], 'review output changed')
        before, after = record['inputsBefore'], record['inputsAfter']
        for key in ['overlay', 'toolchainExecutable', 'dependencyManifests', 'sourcePackages', 'executables', 'inventoryDigests']:
            need(before[key] == after[key], 'review effective inputs changed: ' + key)
        for inputs in [before, after]:
            need(sha(review / inputs['inventoryFile']) == inputs['inventorySha256'], 'review inventory changed')
            overlay = inputs['overlay']
            need(sha(Path(overlay['path'])) == overlay['sha256'], 'effective overlay changed')
            mapping = read(Path(overlay['path']))['Replace']
            need(mapping == {r['originalPath']: r['replacementPath'] for r in overlay['replace']}, 'overlay mapping mismatch')
            for replacement in overlay['replace']:
                need(sha(Path(replacement['replacementPath'])) == replacement['replacementSha256'], 'effective overlay replacement changed')
    need(read(HERE / 'recorder-immutability-control.json')['existingFilesUnchanged'], 'recorder reuse overwrote evidence')
    gate = read(HERE / 'qualification-gate-final.json')
    need(gate['exitCode'] == gate['expectedExitCode'] == 1, 'qualification gate incorrectly passed')
    need(sha(HERE / 'qualification-gate-final.log') == gate['outputSha256'], 'qualification gate output changed')
    handoff = read(HERE / 'handoff.json')
    need(sha(HERE / 'final-spec.md.source') == handoff['finalSpecSha256'] == handoff['finalSpecSnapshotSha256'], 'final spec snapshot mismatch')
    need(sha(HERE / 'pre-drift-spec.md.source') == handoff['preservationObservationSpecSha256'], 'preservation-to-final handoff mismatch')
    need(sha(BASE / 'qualification-2026-10-21-concurrency-retirement.md') == handoff['reportSha256'], 'report handoff mismatch')
    def frozen(path):
        return path.read_bytes().split(b'<frozen-after-approval', 1)[1].split(b'</frozen-after-approval>', 1)[0]
    need(frozen(HERE / 'final-spec.md.source') == frozen(old / 'spec-before.source'), 'final frozen intent differs')
    need("status: 'in-progress'" in (HERE / 'final-spec.md.source').read_text().split('---', 2)[1], 'final spec status advanced')
    disposition = read(HERE / 'disposition.json')
    need(disposition['qualification'] == 'incomplete' and disposition['acceptedExceptions'] == [], 'disposition weakened')
    need(disposition['activeSpecStatus'] == 'in-progress' and disposition['originalStoryStatus'] == 'backlog', 'status advanced')
    need(disposition['productionGates'] == 'closed' and disposition['launchIdentityHardening'] == 'explicitly deferred', 'authority changed')
    preservation = read(HERE / 'preservation.json')
    need(not preservation['historicalDifferences'], 'historical evidence preservation failed')
    drift = read(HERE / 'post-run-binary-drift.json')
    need(drift['allObservedMtimesAfterLastApplicationCommand'], 'live output timestamps changed; inspect separately')
    need({k: {'before': v['before'], 'after': v['after']} for k, v in drift['changedOutputs'].items()} == preservation['applicationBinaryDifferences'], 'post-run binary drift is not fully declared')
    need(set(preservation['protectedDifferences']) <= {'_bmad-output/implementation-artifacts/sprint-status.yaml'}, 'protected production file changed')
    need(all(not item['differences'] for item in preservation['sourceTrees'].values()), 'runtime source preservation failed')
    need(preservation['frozenIntentUnchanged'], 'frozen intent changed')
    need(preservation['legacyHandoffSpecSha256'] == preservation['runStartSpecSha256'], 'historical handoff not bound to run start')
    need(not read(HERE / 'unrecorded-host-scan.json')['matchingProcesses'], 'workspace product host remains')
    processes = read(HERE / 'processes.json')
    need(not processes['matchingCandidateOrRecordedHostProcesses'] and not processes['matchingFixtureComponentDirectories'] and not processes['recordedPortsCurrentlyListening'], 'fixture cleanup incomplete')
    return len(timelines)

def verify():
    index = read(HERE / 'index.json')
    check_index(index)
    count = check_measurements()
    tests = []
    for kind in ['altered-hash', 'missing-index-entry', 'unsafe-path']:
        mutant = copy.deepcopy(index)
        key = next(iter(mutant['files']))
        if kind == 'altered-hash':
            mutant['files'][key]['sha256'] = '0' * 64
        elif kind == 'missing-index-entry':
            del mutant['files'][key]
        else:
            mutant['files']['../outside'] = mutant['files'].pop(key)
        try:
            check_index(mutant)
        except (RuntimeError, OSError):
            tests.append(kind + ':rejected')
        else:
            raise RuntimeError('tamper control accepted: ' + kind)
    return {'result': 'integrity-and-local-measurements-verified', 'qualification': 'incomplete',
            'indexSha256': sha(HERE / 'index.json'), 'measuredRecoveryTimelines': count,
            'tamperControls': tests, 'authority': 'No approval, completion, exception or production authority.'}

if __name__ == '__main__':
    try:
        print(json.dumps(verify(), indent=2))
    except (RuntimeError, OSError, ValueError, KeyError, IndexError) as error:
        print('VALIDATION FAILED: ' + str(error), file=sys.stderr)
        sys.exit(1)

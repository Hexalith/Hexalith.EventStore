#!/usr/bin/env python3
"""Fail-closed review checks around the unchanged original packet verifier."""
import datetime
import hashlib
import json
from pathlib import Path
import runpy
import subprocess
import sys


class ValidationFailure(Exception):
    pass


def require(condition, message):
    if not condition:
        raise ValidationFailure(message)


def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def load(path):
    require(path.is_file(), 'mandatory artifact missing: ' + str(path))
    return json.loads(path.read_text())


def check_index(directory, index, excluded):
    require(set(index.get('excludedFromIndex', [])) == set(excluded), 'unexpected index exclusions')
    files = index['files']
    actual = {p.relative_to(directory).as_posix() for p in directory.rglob('*') if p.is_file()}
    require(actual - set(excluded) == set(files), 'index membership mismatch: ' + str(directory))
    for name, expected in files.items():
        require(not Path(name).is_absolute() and '..' not in Path(name).parts, 'unsafe index path')
        require(sha(directory / name) == expected, 'indexed bytes mismatch: ' + name)


def frozen(path):
    data = path.read_bytes()
    start = data.index(b'<frozen-after-approval')
    end = data.index(b'</frozen-after-approval>') + len(b'</frozen-after-approval>')
    return hashlib.sha256(data[start:end]).hexdigest()


def check_handoff(supplement, packet, repository):
    binding = load(supplement / 'handoff-binding.json')
    original = load(packet / 'handoff-identities.json')
    for name, key in [('index.json', 'originalIndexSha256'), ('verification.json', 'originalVerificationSha256'), ('handoff-identities.json', 'originalHandoffSha256')]:
        require(sha(packet / name) == binding[key], 'original packet handoff mismatch: ' + name)
    require(packet.name == binding['originalPacket'], 'original packet path mismatch')
    require(sha(supplement.parent / binding['originalReport']) == binding['originalReportSha256'] == original['reportSha256'], 'original report binding mismatch')
    require(sha(supplement / 'handoffs/original-spec.md.source') == binding['originalSpecSha256'] == original['specSha256'], 'original spec binding mismatch')
    require(sha(supplement / 'handoffs/current-spec.md.source') == sha(repository / binding['currentSpecPath']) == binding['currentSpecSha256'], 'current spec binding mismatch')
    for path in [supplement / 'handoffs/original-spec.md.source', supplement / 'handoffs/current-spec.md.source']:
        require(frozen(path) == binding['frozenIntentSha256'] == original['frozenIntentSha256'], 'frozen intent binding mismatch')
    require(binding['originalSourceRevision'] == original['revision'], 'handoff repository revision mismatch')
    require(binding['candidateSha256'] == original['finalCandidateSha256'], 'handoff candidate mismatch')
    return binding


def check_provenance(packet, binding):
    identity = load(packet / 'final-runtime-identity.json')
    patch = load(packet / 'final-runtime-patch-identity.json')
    build = load(packet / 'final-candidate-build.json')
    acquisition = load(packet / 'runtime-acquisition.json')
    require(identity['candidateSha256'] == binding['candidateSha256'], 'runtime/handoff candidate mismatch')
    require(sha(Path(identity['candidatePath'])) == identity['candidateSha256'], 'current disposable runtime bytes mismatch')
    require(identity['sourceRevision'] == patch['revision'], 'runtime/patch source revision mismatch')
    require(sha(packet / 'final-runtime-recovery.patch') == identity['patchSha256'] == patch['patchSha256'], 'runtime/patch digest mismatch')
    require(sha(packet / 'router-final-candidate.go.source') == identity['routerSha256'] == patch['afterSha256'], 'runtime/router digest mismatch')
    for filename, key in [('go.mod', 'goModSha256'), ('go.sum', 'goSumSha256')]:
        require(sha(packet / (filename + '.source')) == identity[key], 'runtime/module identity mismatch: ' + filename)
    download = next(x for x in acquisition['downloads'] if x['label'] == 'dapr-source')
    require(download['sha256'] == identity['sourceArchiveSha256'], 'runtime/acquisition archive mismatch')
    source = str(Path(acquisition['workDirectory']) / ('dapr-' + identity['sourceRevision']))
    cmd = build['command']
    require(build['exitCode'] == 0 and build['runStartRevision'] == build['revisionAfter'] == binding['originalSourceRevision'], 'build/repository handoff mismatch')
    require(cmd[cmd.index('-C') + 1] == source and cmd[cmd.index('-o') + 1] == identity['candidatePath'], 'build source/output mismatch')
    require(all(flag in cmd for flag in ['CGO_ENABLED=0', '-mod=readonly', '-tags=allcomponents', './cmd/daprd']), 'build profile mismatch')
    require(any(identity['sourceRevision'] in flag and flag.startswith('-ldflags=') for flag in cmd), 'build embedded revision mismatch')
    require(build['externalInputsBefore'] == build['externalInputsAfter'], 'build inputs changed')
    for suffix, expected in [('go.mod', identity['goModSha256']), ('go.sum', identity['goSumSha256']), ('pkg/actors/router/router.go', identity['routerSha256'])]:
        require(build['externalInputsBefore'].get(source + '/' + suffix) == expected, 'build/runtime source identity mismatch: ' + suffix)
    require(build['externalInputsBefore'].get(str(Path(acquisition['workDirectory']) / 'go/bin/go')) == identity['toolchainSha256'], 'build/runtime toolchain mismatch')
    return identity


def check_scenarios(packet, identity):
    completed_build = datetime.datetime.fromisoformat(load(packet / 'final-candidate-build.json')['completedUtc'])
    # Bind every retained post-build scenario, including the complete class and real projection.
    for path in packet.glob('scenarios/*/manifest.json'):
        manifest = load(path)
        if datetime.datetime.fromisoformat(manifest['StartedUtc']) >= completed_build:
            for host in ['First', 'Second']:
                require(manifest[host + 'SidecarExecutableSha256'] == identity['candidateSha256'] and manifest[host + 'SidecarExecutablePath'] == identity['candidatePath'], 'scenario runtime identity mismatch')
    timelines = load(packet / 'recovery-timelines.json')['timelines']
    require(len(timelines) == 3 and len({x['manifest'] for x in timelines}) == 3, 'three distinct final measured timelines required')
    for item in timelines:
        path = packet / item['manifest']
        manifest = load(path)
        require(sha(path) == item['manifestSha256'], 'timeline manifest digest mismatch')
        require(item['candidateSha256'] == identity['candidateSha256'], 'timeline candidate mismatch')
        require(manifest['Result'] == item['assertionResult'] == 'passed-scenario-assertions-only', 'timeline scenario failed')
        for host in ['First', 'Second']:
            require(manifest[host + 'SidecarExecutableSha256'] == identity['candidateSha256'] and manifest[host + 'SidecarExecutablePath'] == identity['candidatePath'], 'scenario runtime identity mismatch')
            require(sha(path.parent / (host.lower() + '.log')) == item[host.lower() + 'LogSha256'], 'timeline log digest mismatch')
        observation = manifest['Observations']
        require(observation['OriginalSecondInternalGrpcPort'] == observation['RestoredSecondInternalGrpcPort'] == item['internalPort'] and item['sameInternalPort'] is True, 'timeline endpoint mismatch')
        require(observation['InitialETagActorOwner'] == item['initialOwner'] == 'second-proof-host', 'timeline selected owner mismatch')
        require(item['actor'] == observation['ReadModelKey'].split(':', 2)[2] + ':' + observation['Tenant'], 'timeline selected actor mismatch')
        require(item['observedEvictionAndRecoveryWithinMeasuredInvocation'] is True and item['restoredActorPostPersistenceUtc'] and 0 < item['invocationElapsedMillisecondsFromCapturedUtc'] < item['signalWaitBoundMilliseconds'] == 15000, 'timeline recovery evidence missing')
    for name in ['final-focused-with-restored-capacity.json', 'final-class-with-restored-capacity.json']:
        command = load(packet / name)
        require(command['exitCode'] == 0 and command['fixtureRuntimePath'] == identity['candidatePath'], 'final evaluation failed/path mismatch')
        require(command['fixtureRuntimeSha256Before'] == command['fixtureRuntimeSha256After'] == identity['candidateSha256'], 'final evaluation candidate mismatch')
        require('-stopOnFail' not in command['command'], 'final evaluation stops early')
    return len(timelines)


def check_controls(supplement, packet, identity):
    directory = supplement / 'cancellation-control'
    index = load(directory / 'index.json')
    check_index(directory, dict(index, excludedFromIndex=['index.json']), ['index.json'])
    require(sha(directory / 'router-original.go.source') == identity['routerSha256'], 'cancellation candidate router mismatch')
    require(sha(directory / 'local-cancellation-original.go.source') == sha(packet / 'router_cancellation_test.go.source'), 'cancellation original test mismatch')
    original = (directory / 'router-original.go.source').read_bytes()
    require(original.replace(b' == codes.Unavailable && ctx.Err() == nil', b' == codes.Unavailable') == (directory / 'router-mutated.go').read_bytes(), 'mutant changes more than cancellation guard')
    for label, result in [('original-candidate-race-green', 0), ('mutated-race-red', 1), ('mutated-existing-controls-green', 0)]:
        receipt = load(directory / (label + '.json'))
        require(receipt['exitCode'] == receipt['expectedExitCode'] == result, 'cancellation control result mismatch')
        require(sha(directory / (label + '.log')) == receipt['outputSha256'], 'cancellation log mismatch')
        require(sha(directory / (label + '.overlay.json')) == receipt['overlaySha256'], 'cancellation overlay mismatch')
        require(receipt['originalSourceHashesBefore'] == receipt['originalSourceHashesAfter'], 'original sources changed during overlay control')
        if label != 'mutated-existing-controls-green':
            require('TestReviewCanceledUnavailableRetainsChannel' in receipt['command'], 'discriminating test missing')
        for name, expected in receipt['originalSourceHashesBefore'].items():
            retained = 'router-original.go.source' if Path(name).name == 'router.go' else 'local-cancellation-original.go.source'
            require(sha(directory / retained) == expected, 'overlay original source binding mismatch')


def original_command(packet):
    # -E ignores inherited PYTHONOPTIMIZE; -B prevents any writes to the closed packet.
    return [sys.executable, '-E', '-B', str(packet / 'verify-packet.py')]


def verify(supplement):
    require(sys.flags.optimize == 0, 'optimized Python execution is forbidden')
    repository = next(p for p in supplement.parents if (p / 'Hexalith.EventStore.slnx').exists())
    packet = supplement.parent / 'runs-2026-10-21-runtime-recovery'
    # Mandatory before invoking the older verifier, whose two checks were optional.
    index = load(packet / 'index.json')
    load(packet / 'recovery-timelines.json')
    check_index(packet, index, ['index.json', 'verification.json'])
    check_index(supplement, load(supplement / 'index.json'), ['index.json', 'verification.json'])
    binding = check_handoff(supplement, packet, repository)
    identity = check_provenance(packet, binding)
    count = check_scenarios(packet, identity)
    check_controls(supplement, packet, identity)
    comparison = load(supplement / 'source-tree-comparison.json')
    require(comparison['sourceArchiveSha256'] == identity['sourceArchiveSha256'] and comparison['sourceRevision'] == identity['sourceRevision'] and comparison['patchSha256'] == identity['patchSha256'], 'source comparison/runtime identity mismatch')
    compare = runpy.run_path(str(supplement / 'compare-source-tree.py'))['compare']
    allowed = {item['path']: item['current'] for item in comparison['differences']}
    expected = {'pkg/actors/router/router.go': 'router-final-candidate.go.source', 'pkg/actors/router/local_recovery_test.go': 'router_recovery_test.go.source', 'pkg/actors/router/local_cancellation_test.go': 'router_cancellation_test.go.source'}
    require(set(allowed) == set(expected), 'source comparison allowlist mismatch')
    for name, retained in expected.items():
        require(allowed[name]['sha256'] == sha(packet / retained), 'source comparison retained bytes mismatch')
    require(compare(comparison['archiveEntries'], comparison['currentEntries'], allowed) == comparison['differences'], 'source comparison differences mismatch')
    command = original_command(packet)
    child = subprocess.run(command, capture_output=True, text=True)
    require(child.returncode == 0, 'original verifier failed: ' + child.stderr)
    return {'result': 'review-supplement-integrity-and-identity-checks-passed', 'authority': binding['authority'], 'originalVerifierCommand': command, 'originalVerifierResult': json.loads(child.stdout), 'measuredTimelines': count, 'originalPacketIndexSha256': sha(packet / 'index.json'), 'originalSpecSha256': binding['originalSpecSha256'], 'currentSpecSha256': binding['currentSpecSha256']}


if __name__ == '__main__':
    try:
        print(json.dumps(verify(Path(__file__).resolve().parent), indent=2))
    except (ValidationFailure, OSError, ValueError, KeyError, StopIteration, IndexError) as error:
        print('VALIDATION FAILED: ' + str(error), file=sys.stderr)
        sys.exit(1)

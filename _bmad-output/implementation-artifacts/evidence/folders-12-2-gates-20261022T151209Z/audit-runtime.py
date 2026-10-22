#!/usr/bin/env python3
"""Require complete worker/module observations and persisted proof for the live matrix."""
import json
from pathlib import Path

packet = Path(__file__).resolve().parent
infra = packet / 'bounded-infrastructure'
errors = []
processes = json.loads((infra / 'observed-processes.json').read_text())
images = json.loads((infra / 'mapped-images.json').read_text())
components = json.loads((infra / 'components.json').read_text())
binding = json.loads((packet / 'source-binding-final/all-pdb-documents.json').read_text())
by_hash = {row['peSha256'].lower(): row for row in binding}
required = {'Hexalith.EventStore.Client', 'Hexalith.EventStore.DomainService', 'Hexalith.EventStore.Server', 'Hexalith.EventStore.Server.LiveSidecar.Tests'}
scenarios = []
workers = set()
maximums = {}
expected_matrix = {(129, False, False), (129, True, False), (129, False, True), (129, True, True), (10001, True, True), (10001, False, False), (32769, False, False), (10001, True, False), (32769, True, False)}
actual_matrix = []
for directory in sorted((packet / 'bounded-scenarios').iterdir()):
    proofs = [json.loads(path.read_text()) for path in sorted(directory.glob('*.json'))]
    if not proofs:
        errors.append('No persisted worker proofs: ' + str(directory))
        continue
    first = proofs[0]
    actual_matrix.append((first['count'], first['bufferedOwner'], first['inheritOwner']))
    if any(any(row[key] != first[key] for key in ('count', 'bufferedOwner', 'inheritOwner', 'tenant')) for row in proofs):
        errors.append('Inconsistent scenario identity: ' + str(directory))
    expected = {'stage', 'promote', 'resume', 'inherited'} if proofs[0]['inheritOwner'] else {'stage', 'promote', 'resume'}
    if {row['phase'] for row in proofs} != expected or len(proofs) != len(expected):
        errors.append('Missing/duplicate phases: ' + str(directory))
    scenarios.append({'directory': str(directory.relative_to(packet)), 'count': proofs[0]['count'], 'buffered_owner': proofs[0]['bufferedOwner'], 'inherited_owner': proofs[0]['inheritOwner'],
                      'workers': [{'pid': row['pid'], 'phase': row['phase'], 'observations': row['observations'], 'measurements': row['measurements']} for row in proofs]})
    for proof in proofs:
        observation = proof['observations']
        phase = proof['phase']
        if phase == 'stage':
            if not (observation.get('phase') == 'AfterInstallOperation' and observation.get('ordinal') == 0 and observation.get('preparedMarkerObserved') is True and observation.get('participantInstalled') is True and observation.get('stageReturned') is False and observation.get('stageSealed') is False and observation.get('completedStagePages') == 0):
                errors.append('Incomplete stage interruption proof: ' + str(directory))
        elif phase != 'inherited':
            keys = ['firstOwnerSeeded', 'secondOwnerCheckpointAbsent', 'handoffFenceHeld', 'secondOwnerEpochInstalled' if phase == 'promote' else 'partialHandoffRecovered']
            if not all(observation.get(key) is True for key in keys):
                errors.append('Incomplete promoted/recovered handoff proof: ' + str(directory))
        if proof['inheritOwner'] and phase in ('resume', 'inherited'):
            resume = next((row for row in proofs if row['phase'] == 'resume'), None)
            if (observation.get('originalOwnerOmitted') is not True or observation.get('retainedProofEpoch', 0) >= observation.get('selectedGeneration', 0)
                or not resume or not observation.get('inheritedCheckpointSha256') or observation.get('inheritedCheckpointSha256') != resume['observations'].get('inheritedCheckpointSha256')
                or phase == 'inherited' and observation.get('unchangedReplayVerified') is not True):
                errors.append('Incomplete inherited-owner proof: ' + str(directory))
        pid = proof['pid']
        workers.add(pid)
        observed = [row for row in images if row['pid'] == pid]
        if not any(row['pid'] == pid for row in processes.values()):
            errors.append('Worker absent from process observer: ' + str(pid))
        if not required.issubset({Path(row['path']).stem for row in observed}):
            errors.append('Worker module observation gap: ' + str(pid))
        if {row['name'] for row in proof['runtime']} != required:
            errors.append('Worker runtime inventory gap: ' + str(pid))
        for module in proof['runtime']:
            bound = by_hash.get(module['peSha256'].lower())
            if not module['loadedMvidMatchesPe'] or not bound or module['loadedMvid'] != bound['mvid'] or module['pdbSha256'].lower() != bound['pdbSha256'].lower():
                errors.append('Worker PE/PDB/MVID mismatch: ' + str(pid) + ':' + module['name'])
            candidates = [row for row in observed if Path(row['path']).stem == module['name']]
            if not any(row['dll_sha256'].lower() == module['peSha256'].lower()
                       and row['pdb_sha256'] and row['pdb_sha256'].lower() == module['pdbSha256'].lower()
                       and by_hash.get(row['dll_sha256'].lower(), {}).get('mvid') == module['loadedMvid']
                       for row in candidates):
                errors.append('Worker self-report disagrees with mapped module: ' + str(pid) + ':' + module['name'])
            if any(doc['sha256'].lower() != doc['pdbChecksum'].lower() for doc in module['documents']):
                errors.append('Worker source checksum mismatch: ' + str(pid) + ':' + module['name'])
        if proof['buildGraph'] != 'Debug;UseHexalithProjectReferences=true;net10.0':
            errors.append('Wrong worker build graph: ' + str(pid))
        for key, value in proof['measurements'].items():
            maximums[key] = max(maximums.get(key, 0), value)
        if proof['measurements']['MaximumBatchOperations'] > 100 or proof['measurements']['MaximumCanonicalBatchBytes'] > 1_048_576 or proof['measurements']['MaximumPhysicalValueBytes'] > 1_048_576 or proof['measurements']['MaximumIdentityValueBytes'] >= 65_536:
            errors.append('Storage bound exceeded: ' + str(pid))
if len(actual_matrix) != 9 or set(actual_matrix) != expected_matrix:
    errors.append('Expected the exact unchanged nine-case matrix.')
if len(scenarios) != 9 or len(workers) != 30:
    errors.append('Expected 9 complete scenarios and 30 distinct workers.')
sdk_loads = []
for row in images:
    if not Path(row['path']).name.startswith('Hexalith.'):
        continue
    bound = by_hash.get(row['dll_sha256'].lower())
    good = bool(bound and row['pdb_sha256'] and row['pdb_sha256'].lower() == bound['pdbSha256'].lower())
    if not good:
        errors.append('Observed SDK image lacks matching PE/PDB binding: ' + row['path'])
    sdk_loads.append({**row, 'mvid': bound['mvid'] if bound else None, 'passed': good})
sidecars = [row for row in processes.values() if Path(row['exe']).name == 'daprd']
if not sidecars or not components or not sdk_loads:
    errors.append('Missing sidecar/component/SDK observations.')
for row in sidecars:
    args = row['argv']
    if '--resources-path' not in args or args[args.index('--resources-path') + 1] not in components:
        errors.append('Observed sidecar resources not retained: ' + str(row['pid']))
for path, files in components.items():
    if not {'statestore.yaml', 'pubsub.yaml'}.issubset({Path(name).name for name in files}):
        errors.append('Missing materialized Dapr components: ' + path)
cleanup = json.loads((infra / 'observed-cleanup.json').read_text())
outer = json.loads((infra / 'result.json').read_text())
if cleanup['remaining_processes'] or not cleanup['component_directories_absent'] or not all(cleanup['component_directories_absent'].values()) or not cleanup['passed']:
    errors.append('Observed fixture cleanup failed or lacks directory evidence.')
if outer['test_exit_code'] != 0 or not outer['owned_infrastructure_cleanup'] or not outer['observed_cleanup']:
    errors.append('Outer fixture lifecycle failed.')
(packet / 'bounded-summary.json').write_text(json.dumps({'scenarios': scenarios, 'distinct_worker_pids': sorted(workers), 'observed_maximums': maximums}, indent=2) + '\n')
result = {'passed': not errors, 'scenario_count': len(scenarios), 'worker_count': len(workers), 'sidecar_count': len(sidecars), 'sdk_load_observations': sdk_loads, 'errors': errors,
          'observer_version': 'Fresh v3 execution: periodic descendant observations plus synchronous external worker captures; complete phase/process/module coverage unchanged.'}
(packet / 'bounded-runtime-audit.json').write_text(json.dumps(result, indent=2) + '\n')
print(json.dumps({k: v for k, v in result.items() if k != 'sdk_load_observations'}))
raise SystemExit(0 if result['passed'] else 1)

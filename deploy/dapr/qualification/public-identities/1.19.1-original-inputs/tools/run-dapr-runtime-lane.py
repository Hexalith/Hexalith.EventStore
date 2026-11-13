#!/usr/bin/env python3
"""Explicit non-promoting CI orchestration; each lane owns its disposable worker."""
import argparse
import importlib.util
import json
import os
from pathlib import Path
import secrets
import signal
import subprocess
import sys
import xml.etree.ElementTree as ET
from dapr_broker_qualification import write_json
from dapr_runtime_qualification import require_candidate, run_process_group, sanitize_xunit_report, xunit_outcome, durable_json, ExclusiveDocker, cleanup_child_resources, reconcile_private_directory

LANES = {
    'durable-broker-fixture': ('Hexalith.EventStore.IntegrationTests', ['*DurableBrokerQualificationTests']),
    'routing-actors-snapshots': ('Hexalith.EventStore.Server.LiveSidecar.Tests', ['*RouteCatalogHostedLiveTests', '*ActorConcurrencyConflictTests', '*ActorTenantIsolationTests', '*ReminderRecoveryLiveSidecarTests', '*FoldedSnapshotReloadLiveSidecarTests']),
    'actor-admission-process': ('Hexalith.EventStore.Server.LiveSidecar.Tests', ['*ActorCallbackAdmissionProcessTests']),
    'compatibility-security-restore': ('Hexalith.EventStore.PublishedCompatibility.Tests', ['*PublishedSecurityAndCrossBoundaryTests', '*PublishedRestoreBoundaryTests']),
}


def lane_command(lane, profile, worker, output, configuration='Release'):
    project, classes = LANES[lane]
    assembly = f'tests/{project}/bin/{configuration}/net10.0/{project}.dll'
    env = dict(os.environ)
    if lane in ('durable-broker-fixture', 'actor-admission-process'):
        env.update(EVENTSTORE_DAPR_QUALIFICATION_PROFILE=str(profile), QUALIFICATION_EXCLUSIVE_WORKER=str(worker),
                   EVENTSTORE_DAPR_RUNTIME_PROFILE='1.19.1', EVENTSTORE_DAPR_QUALIFICATION_OUTPUT=str(output / 'packet'),
                   HEXALITH_ACTOR_ADMISSION_EVIDENCE_DIRECTORY=str(output / 'admission'))
        argv = ['dotnet', assembly, '-class', classes[0], '-xml', str(output / 'xunit.xml')]
        if lane == 'durable-broker-fixture': argv += ['-explicit', 'only']
    else:
        argv = [sys.executable, '-B', 'tools/qualify-dapr-runtime.py', '--profile', str(profile), '--exclusive-worker', str(worker),
                '--output', str(output), '--assembly', assembly]
        for selected in classes: argv += ['--class', selected]
    return argv, env


def compatibility_git_commands():
    return [
        ['git', 'rev-parse', '--verify', 'v3.70.1^{}'],
        ['git', 'rev-parse', '--verify', 'v3.110.0^{}'],
        ['git', 'rev-parse', '--verify', 'v3.128.2^{}'],
        ['git', 'cat-file', '-e', '935c21ac5ebc5e50738ae6ee79dc661acd2fd5a8^{commit}'],
        ['git', '-C', 'references/Hexalith.Builds', 'fetch', '--no-recurse-submodules', 'origin', 'db897eac9f2819220b32d237109d7255218b46a9'],
        ['git', '-C', 'references/Hexalith.Builds', 'cat-file', '-e', 'db897eac9f2819220b32d237109d7255218b46a9^{commit}'],
    ]


def supervise_direct(owner, argv, environment, *, timeout=1200):
    """Release child lease, then independently reclaim and reconcile Docker custody."""
    witness, cleanup, outcome = {}, {'complete': False, 'owners': []}, {'passed': False, 'tests': 0, 'exitCode': 1}
    error, admitted = None, False
    previous = {kind: signal.signal(kind, lambda *_: (_ for _ in ()).throw(KeyboardInterrupt())) for kind in (signal.SIGTERM, signal.SIGINT)}
    root = owner.output / 'child-owners'
    try:
        owner.docker_owner = ExclusiveDocker(owner.command, owner.args.exclusive_worker)
        owner.docker_owner.capture()
        owner.context = owner.docker_owner.endpoint
        owner.identities['dockerCustody'] = owner.docker_owner.custody()
        owner.before = owner.inventory()
        write_json(owner.output / 'workspace-before.json', owner.before)
        write_json(owner.output / 'commands.json', owner.receipts)
        token = secrets.token_hex(32)
        durable_json(root / 'supervisor.json', {'custody': owner.docker_owner.custody(), 'token': token, 'before': owner.before})
        environment = {**environment, 'QUALIFICATION_SUPERVISOR_ROOT': str(root), 'QUALIFICATION_SUPERVISOR_TOKEN': token}
        admitted = True
        owner.docker_owner.close()
        code, _, _ = run_process_group(argv, env=environment, timeout=timeout, witness=witness)
        outcome = xunit_outcome(owner.output / 'xunit.xml', code)
    except (Exception, KeyboardInterrupt) as failure:
        error = str(failure) or type(failure).__name__
    finally:
        # Repeated termination cannot bypass independent parent reconciliation.
        for kind in previous: signal.signal(kind, signal.SIG_IGN)
        try:
            if admitted:
                owner.docker_owner.reacquire()
                cleanup['complete'] = True
                parent_id = owner.run_id
                journals = [path for path in sorted(root.glob('*.json')) if path.name != 'supervisor.json']
                if not journals and witness.get('exitCode') not in (None, 0):
                    cleanup.update(complete=False, error='interrupted child did not establish private/resource custody')
                for journal in journals:
                    try:
                        state = json.loads(journal.read_text())
                        if (state.get('custody') != owner.docker_owner.custody() or state.get('supervisorToken') != token
                                or journal.stem != state.get('owner')):
                            raise ValueError('child resource journal has unrelated supervisor custody')
                        owner.run_id = state['owner']
                        result = cleanup_child_resources(owner, journal)
                        if result['complete']:
                            result['privateDirectory'] = reconcile_private_directory(state)
                            result['complete'] &= result['privateDirectory']['absent']
                        cleanup['owners'].append({'owner': state['owner'], **result})
                        cleanup['complete'] &= result['complete']
                    except Exception as failure:
                        cleanup['complete'] = False
                        cleanup['owners'].append({'journal': journal.name, 'complete': False, 'error': str(failure)})
                    finally: owner.run_id = parent_id
            else:
                cleanup['complete'] = owner.context is None
        except Exception as failure:
            cleanup.update(complete=False, error=str(failure))
            owner.cleanup_errors.append('independent Docker custody unavailable: ' + str(failure))
        if not cleanup['complete']: owner.cleanup_errors.append('independent child Docker reconciliation incomplete')
        if witness and not witness.get('complete'): owner.cleanup_errors.append('native descendant cleanup incomplete')
        owner.child_remaining = [] if cleanup['complete'] else ['unresolved-supervised-child-resources']
        try:
            owner.finish()
        except Exception as failure:
            owner.cleanup_complete = False
            error = str(failure)
        try: sanitize_xunit_report(owner.output / 'xunit.xml')
        except Exception as failure: error = str(failure)
        cleanup['preservationComplete'] = owner.cleanup_complete
        write_json(owner.output / 'parent-docker-cleanup.json', cleanup)
        for kind, handler in previous.items(): signal.signal(kind, handler)
    passed = outcome['passed'] and error is None and witness.get('complete') and cleanup['complete'] and owner.cleanup_complete
    return {'process': witness, **outcome, 'blocker': error, 'dockerCleanup': cleanup,
        'technicalQualification': 'PASS' if passed else 'FAIL', 'productionEligibility': 'FAIL', 'selectedBroker': None}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--lane', choices=LANES, required=True)
    parser.add_argument('--prepare-compatibility', action='store_true')
    args = parser.parse_args()
    require_candidate(Path(os.environ['QUALIFICATION_PROFILE']), Path(__file__).resolve().parents[1])
    if args.prepare_compatibility:
        for command in compatibility_git_commands(): subprocess.run(command, check=True)
        return 0
    output = Path(os.environ['QUALIFICATION_OUTPUT'])
    argv, environment = lane_command(args.lane, Path(os.environ['QUALIFICATION_PROFILE']), Path(os.environ['QUALIFICATION_EXCLUSIVE_WORKER']), output)
    if args.lane not in ('durable-broker-fixture', 'actor-admission-process'):
        return subprocess.run(argv, env=environment).returncode
    spec = importlib.util.spec_from_file_location('direct_lane_owner', Path(__file__).with_name('qualify-dapr-brokers.py'))
    module = importlib.util.module_from_spec(spec); spec.loader.exec_module(module)
    import types
    owner = module.Run(types.SimpleNamespace(profile=Path(os.environ['QUALIFICATION_PROFILE']), output=output,
        exclusive_worker=Path(os.environ['QUALIFICATION_EXCLUSIVE_WORKER'])))
    owner.regression = True
    result = supervise_direct(owner, argv, environment)
    try:
        if result['technicalQualification'] == 'PASS' and args.lane == 'durable-broker-fixture':
            validator = [sys.executable, '-B', 'tools/validate-dapr-broker-qualification.py', str(output / 'packet')]
            subprocess.run([*validator, '--allow-failures'], check=True)
            if subprocess.run([*validator, '--require-production-authority']).returncode != 1:
                raise ValueError('production eligibility must remain FAIL')
            subprocess.run(validator, check=True)
        elif result['technicalQualification'] == 'PASS':
            child = json.loads((output / 'admission/result.json').read_text())
            if not all(child.get(field) is True for field in ('passed', 'qualification_passed', 'cleanup_passed')):
                raise ValueError('selected admission child outcome is incomplete or failed')
    except (Exception, KeyboardInterrupt) as failure:
        result.update(blocker=str(failure) or type(failure).__name__, technicalQualification='FAIL')
    write_json(output / 'lane-result.json', result)
    return 0 if result['technicalQualification'] == 'PASS' else 1


if __name__ == '__main__': raise SystemExit(main())

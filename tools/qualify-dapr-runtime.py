#!/usr/bin/env python3
"""Execute selected built xUnit classes on exclusive disposable Dapr infrastructure."""
import argparse
import importlib.util
import json
import os
from pathlib import Path
import shutil
import signal
import subprocess
import sys
from dapr_broker_qualification import digest, write_json
from dapr_runtime_qualification import require_candidate, run_process_group, cleanup_child_resources, sanitize_xunit_report, xunit_outcome, REDIS_IMAGE


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--profile', type=Path, required=True)
    parser.add_argument('--exclusive-worker', type=Path, default=os.environ.get('QUALIFICATION_EXCLUSIVE_WORKER'))
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--assembly', type=Path, required=True)
    parser.add_argument('--class', dest='classes', action='append', required=True)
    args = parser.parse_args()
    spec = importlib.util.spec_from_file_location('broker_runner', Path(__file__).with_name('qualify-dapr-brokers.py'))
    module = importlib.util.module_from_spec(spec); spec.loader.exec_module(module)
    try:
        require_candidate(args.profile, Path(__file__).resolve().parents[1])
    except (ValueError, KeyError, OSError) as failure:
        parser.error(str(failure))
    owner = module.Run(args)
    owner.regression = True
    results = []
    error = None
    process_witnesses = []
    child_journal = owner.private / "child-resources.json"
    compatibility_scratch = owner.private_parent / "compatibility"
    signal.signal(signal.SIGTERM, lambda *_: (_ for _ in ()).throw(KeyboardInterrupt()))
    try:
        owner.prepare()
        for name in ('sentry',):
            owner.docker('pull', owner.profile['images'][name]['reference'], timeout=180)
        owner.docker('pull', REDIS_IMAGE, timeout=180)
        redis = owner.container('runtime-redis', REDIS_IMAGE, options=['-p', '127.0.0.1::6379'])
        redis_port = owner.published_port(redis, 6379)
        owner.wait(lambda: owner.docker('exec', redis, 'redis-cli', 'PING', check=False)[1].strip() == 'PONG')
        shutil.copyfile(owner.daprd, owner.private / 'daprd')
        write_json(owner.private / 'selected-runtime.json', {'version': owner.profile['runtime']['version'], 'binaries': {name: digest(owner.private / name) for name in ('daprd', 'placement', 'scheduler')}, 'images': {name: owner.profile['images'][name]['reference'] for name in ('placement', 'scheduler')}})
        adapter = owner.private / 'docker-adapter'
        adapter.mkdir()
        executable = adapter / 'docker'
        source = Path(__file__).with_name('qualification-docker.py').read_text()
        executable.write_text('#!' + sys.executable + '\n' + '\n'.join(source.splitlines()[1:]) + '\n')
        executable.chmod(0o755)
        env = {**os.environ,
            'QUALIFICATION_REAL_DOCKER': shutil.which('docker') or '/missing-docker',
            'QUALIFICATION_PINNED_ENDPOINT': owner.docker_owner.endpoint,
            'QUALIFICATION_PINNED_DAEMON': owner.docker_owner.daemon_id,
            'QUALIFICATION_PINNED_SOCKET': ':'.join(map(str, owner.docker_owner.socket_identity)),
            'QUALIFICATION_PINNED_WORKER': str(owner.docker_owner.lease_path.resolve()),
            'QUALIFICATION_PINNED_WORKER_ID': owner.docker_owner.worker_id,
            'QUALIFICATION_PINNED_ALLOCATION': json.dumps(owner.docker_owner.allocation, sort_keys=True),
            'QUALIFICATION_PINNED_LEASE': ':'.join(map(str, owner.docker_owner.lease_identity)),
            'QUALIFICATION_CHILD_RESOURCES': str(child_journal), 'QUALIFICATION_RESOURCE_OWNER': owner.run_id,
            'PYTHONPATH': str(Path(__file__).resolve().parent),
            'P1R_QUALIFICATION_SCRATCH_ROOT': str(compatibility_scratch),
            'PATH': str(adapter) + os.pathsep + os.environ['PATH'], 'PYTHONDONTWRITEBYTECODE': '1', 'DOCKER_HOST': owner.docker_owner.endpoint,
            'EVENTSTORE_DAPR_RUNTIME_PROFILE': '1.19.1', 'EVENTSTORE_DAPR_RUNTIME_BINARIES': str(owner.private),
            'EVENTSTORE_ROUTE_CATALOG_DAPRD': str(owner.daprd), 'EVENTSTORE_TEST_DAPRD_PATH': str(owner.daprd),
            'EVENTSTORE_ROUTE_CATALOG_DAPR_CLI': shutil.which('dapr') or '',
            'EVENTSTORE_TEST_REDIS_PORT': str(redis_port), 'EVENTSTORE_TEST_PLACEMENT_PORT': owner.placement.rsplit(':', 1)[1],
            'EVENTSTORE_TEST_SCHEDULER_PORT': owner.scheduler.rsplit(':', 1)[1]}
        env.pop('DOCKER_CONTEXT', None)
        for selected in args.classes:
            owner.docker_owner.verify()
            xml = owner.output / ('xunit-' + str(len(results)) + '.xml')
            witness = {'class': selected}
            process_witnesses.append(witness)
            code, output, _ = run_process_group(['dotnet', args.assembly.resolve(), '-class', selected, '-xml', xml], env=env, timeout=1200, witness=witness)
            if not witness['complete']:
                raise RuntimeError('selected test process descendants remain; cleanup is incomplete')
            # xUnit output is bounded and sanitized before retention.
            import hashlib
            write_json(owner.output / ('xunit-' + str(len(results)) + '-output.json'), {'sha256': hashlib.sha256(output.encode()).hexdigest(), 'bytes': len(output.encode())})
            # A successful process with skipped or absent live tests is not qualification.
            results.append({'class': selected, **xunit_outcome(xml, code)})
    except (Exception, KeyboardInterrupt) as failure:
        error = str(failure) or type(failure).__name__
    finally:
        # The entire cleanup phase must survive repeated cancellation.
        signal.signal(signal.SIGTERM, signal.SIG_IGN)
        signal.signal(signal.SIGINT, signal.SIG_IGN)
        # This is a regression receipt, not a broker packet or a new evidence registry.
        from dapr_runtime_qualification import retain_compatibility_witnesses
        try:
            retain_compatibility_witnesses(compatibility_scratch, owner.output / 'compatibility-witnesses')
        except Exception as failure:
            owner.cleanup_errors.append('compatibility witnesses: ' + str(failure))
        try:
            child_cleanup = cleanup_child_resources(owner, child_journal)
        except Exception as failure:
            owner.cleanup_errors.append('unresolved child Docker journal: ' + str(failure))
            child_cleanup = {'complete': False, 'resources': [{'id': None, 'name': 'unresolved-child-resource-journal', 'absent': False}]}
        for xml in owner.output.glob('xunit-*.xml'):
            try: sanitize_xunit_report(xml)
            except Exception as failure: owner.cleanup_errors.append('xUnit diagnostics: ' + str(failure))
        if not child_cleanup['complete']:
            owner.cleanup_errors.append('child Docker resource cleanup incomplete')
        if any(not row.get('complete') for row in process_witnesses):
            owner.cleanup_errors.append('test process descendant cleanup incomplete')
        write_json(owner.output / 'test-process-cleanup.json', process_witnesses)
        # Unresolved child resources prevent deletion of private custody material.
        owner.child_remaining = [row.get('id') or row['name'] for row in child_cleanup['resources'] if not row['absent']]
        owner.child_remaining += ['process:' + str(pid) for row in process_witnesses for pid in row.get('remaining', [])]
        owner.finish()
        write_json(owner.output / 'regression-results.json', {'classes': results, 'blocker': error,
            'technicalQualification': 'PASS' if results and all(row['passed'] for row in results) and error is None and owner.cleanup_complete else 'FAIL',
            'cleanupComplete': owner.cleanup_complete, 'productionEligibility': 'FAIL', 'selectedBroker': None})
    return 0 if results and all(row['passed'] for row in results) and error is None and owner.cleanup_complete else 1


if __name__ == '__main__':
    raise SystemExit(main())

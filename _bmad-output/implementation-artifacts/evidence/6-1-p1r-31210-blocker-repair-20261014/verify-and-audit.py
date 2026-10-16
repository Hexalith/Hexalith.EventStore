#!/usr/bin/env python3
"""Freeze one new repair attempt; measurement validity awaits the final outer audit."""
import argparse
import json
import os
from pathlib import Path
import signal
import subprocess
import sys
import time

NAMES = ('verify-and-audit.py', 'verify-repair.py', 'independent-repair-audit.py')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--attempt', type=Path, required=True)
    parser.add_argument('--repo', type=Path, required=True)
    parser.add_argument('--frozen', action='store_true', help=argparse.SUPPRESS)
    args = parser.parse_args()
    repo, attempt = args.repo.resolve(), args.attempt.absolute()
    sys.path.insert(0, str(repo / 'tools'))
    import p1r_qualification as p
    import p1r_isolation as isolation
    p.require(attempt == attempt.resolve() and attempt.is_relative_to(repo), 'attempt must be inside the mounted owner repository')
    isolation.private_proof(os.environ.get(isolation.OUTER_MARKER, ''))
    if not args.frozen:
        attempt.mkdir(parents=True, exist_ok=False)
        scripts = attempt / 'scripts'
        scripts.mkdir()
        rows = {}
        for name in NAMES:
            source = Path(__file__).resolve().parent / name
            data = p.regular(source)
            target = scripts / name
            target.write_bytes(data)
            rows[name] = {'source_path':str(source), 'executed_path':str(target), 'sha256':p.digest(data)}
        p.write_json(attempt / 'scripts-binding.json', {'schema':'hexalith.p1r.repair-scripts.v1',
            'attempt':str(attempt), 'scripts':rows, 'created_utc':p.stamp()})
        os.execv(sys.executable, [sys.executable, str(scripts / NAMES[0]), '--attempt', str(attempt),
                                '--repo', str(repo), '--frozen'])

    binding_path = attempt / 'scripts-binding.json'
    binding_data = p.regular(binding_path)
    binding = json.loads(binding_data)
    def frozen():
        p.require(binding['attempt'] == str(attempt) and set(binding['scripts']) == set(NAMES), 'script binding inventory differs')
        for name, row in binding['scripts'].items():
            p.require(row['executed_path'] == str(attempt / 'scripts' / name), 'executed script path differs')
            for key in ('source_path', 'executed_path'):
                p.require(p.digest(p.regular(Path(row[key]))) == row['sha256'], 'frozen script changed: ' + name)
        p.require(p.regular(binding_path) == binding_data, 'script binding changed')
    frozen()
    with (attempt / 'orchestration-started.json').open('x') as marker:
        json.dump({'scripts_binding_sha256':p.digest(binding_data),'started_utc':p.stamp()}, marker)
    report = {'scope':'repair-measurement-and-audit', 'attempt':str(attempt), 'stages':[], 'errors':[],
              'scripts_binding_sha256':p.digest(binding_data), 'valid_evidence':None,
              'final_outer_isolation_audit':'pending', 'p1r_usable':False, 'story_6_1':'blocked'}
    interrupted = []
    active = [None]
    def interrupt(signum, frame):
        interrupted.append(signum)
        if active[0] is not None and active[0].root is not None:
            p.signal_owned(active[0].root, signal.SIGINT)
    previous = {sig:signal.signal(sig, interrupt) for sig in (signal.SIGINT,signal.SIGTERM)}
    try:
        for name in NAMES[1:]:
            owner = p.OwnedProcesses()
            active[0] = owner
            argv = [sys.executable, binding['scripts'][name]['executed_path'], '--attempt', str(attempt), '--repo', str(repo)]
            row = {'script':name, 'argv':argv, 'script_sha256':binding['scripts'][name]['sha256'], 'started_utc':p.stamp()}
            process = None
            try:
                process = subprocess.Popen(argv, env=dict(os.environ, HEXALITH_P1R_CONTROL_NONCE=owner.nonce), start_new_session=True)
                owner.attach(process)
                deadline = None
                while process.poll() is None:
                    owner.discover()
                    if interrupted and deadline is None:
                        p.signal_owned(owner.root, signal.SIGINT)
                        deadline = time.monotonic() + 35
                    if deadline is not None and time.monotonic() > deadline:
                        raise p.InvalidPacket('interrupted stage did not finish orderly')
                    time.sleep(.1)
                row['exit_code'] = process.returncode
            except (Exception,KeyboardInterrupt) as error:
                row['exit_code'] = 2
                report['errors'].append(type(error).__name__ + ': ' + str(error))
            finally:
                row['owned_processes'] = list(owner.owned.values())
                row['cleanup'] = [owner.cleanup(),owner.cleanup()]
                row['finished_utc'] = p.stamp()
                report['stages'].append(row)
                active[0] = None
                p.write_json(attempt / 'orchestration.json', report)
            if interrupted:
                break
        frozen()
        report['scripts_verified_after_audit'] = True
    except (Exception,KeyboardInterrupt) as error:
        report['errors'].append(type(error).__name__ + ': ' + str(error))
    finally:
        for sig,handler in previous.items():
            signal.signal(sig,handler)
        report['interrupted_signals'] = interrupted
        report['finished_utc'] = p.stamp()
        report['measurement_and_audit_passed'] = not interrupted and not report['errors'] and len(report['stages']) == 2 and all(
            row['exit_code'] == 0 and all(not c['remaining'] and not c['errors'] for c in row['cleanup']) for row in report['stages'])
        p.write_json(attempt / 'orchestration.json', report)
        print(json.dumps(report),flush=True)
    return 0 if report['measurement_and_audit_passed'] else 2


if __name__ == '__main__':
    raise SystemExit(main())

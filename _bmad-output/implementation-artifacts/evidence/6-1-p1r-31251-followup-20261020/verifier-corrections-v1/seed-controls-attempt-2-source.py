"""Bounded discovery controls only, using exact retained consumer bytes.

Run through p1r-isolated-runner.py. No matrix, publication, source rebuild,
shared-service intervention, discovery configuration change or qualification.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import sqlite3
import sys
import threading
import time

ROOT = Path(__file__).resolve().parents[5]
sys.path.insert(0, str(ROOT / 'tools'))
import p1r_published_executor as executor
from p1r_qualification import require, stamp

BASE = Path(__file__).resolve().parent
OLD = ROOT / '_bmad-output/implementation-artifacts/evidence/6-1-p1r-31251-published-run'
INPUTS = OLD / 'final-attempt/observation-754c6a84815246a6b5af12b3d47841e2/owner-inputs.json'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def sample(worker, label):
    row = {'utc': stamp(), 'monotonic': time.monotonic(), 'label': label, 'files': [], 'processes': []}
    db = worker.scratch / 'discovery.sqlite'
    for suffix in ('', '-wal', '-shm', '-journal'):
        path = Path(str(db) + suffix)
        try:
            if path.exists():
                stat = path.stat()
                row['files'].append({'name': path.name, 'bytes': stat.st_size, 'mtime_ns': stat.st_mtime_ns,
                                     'device': stat.st_dev, 'inode': stat.st_ino, 'sha256': sha(path)})
        except OSError as error:
            row.setdefault('observer_errors', []).append(str(error))
    if db.exists():
        try:
            with sqlite3.connect(db.as_uri() + '?mode=ro', uri=True, timeout=0) as connection:
                connection.execute('PRAGMA query_only=ON')
                row['journal_mode'] = connection.execute('PRAGMA journal_mode').fetchone()[0]
                row['schema'] = connection.execute('SELECT name,sql FROM sqlite_master ORDER BY name').fetchall()
                row['hosts'] = connection.execute("SELECT registration_id,address,app_id,namespace,last_update,unixepoch('now')-last_update FROM hosts ORDER BY app_id,address").fetchall()
                row['metadata'] = connection.execute('SELECT * FROM metadata').fetchall()
        except sqlite3.Error as error:
            row['sqlite_observer_error'] = str(error)
    try:
        stat = os.statvfs(worker.scratch)
        row['filesystem'] = {'free_bytes': stat.f_bavail * stat.f_frsize, 'block_size': stat.f_frsize,
                             'mountinfo': [line for line in Path('/proc/self/mountinfo').read_text().splitlines()
                                           if ' / ' in line or str(ROOT) in line]}
    except OSError as error:
        row['filesystem_error'] = str(error)
    row['host_pressure_observation'] = {}
    for name in ('pressure/cpu', 'pressure/io', 'pressure/memory', 'loadavg'):
        try:
            row['host_pressure_observation'][name] = Path('/proc/' + name).read_text()
        except OSError as error:
            row['host_pressure_observation'][name] = str(error)
    pids = set()
    for item in list(worker.active):
        pid = item['ownership'].root['pid']; pids.add(str(pid))
        process = {'identity': item['ownership'].root, 'argv': list(map(str, item['argv']))}
        for name in ('io', 'status'):
            try:
                process[name] = Path(f'/proc/{pid}/{name}').read_text()
            except OSError as error:
                process[name + '_error'] = str(error)
        process['fds'] = []
        try:
            for path in Path(f'/proc/{pid}/fd').iterdir():
                try:
                    target = str(path.resolve())
                    if 'discovery.sqlite' in target:
                        process['fds'].append({'fd': path.name, 'target': target,
                            'fdinfo': Path(f'/proc/{pid}/fdinfo/{path.name}').read_text()})
                except OSError:
                    pass
        except OSError as error:
            process['fd_error'] = str(error)
        row['processes'].append(process)
    row['owned_locks'] = [line for line in Path('/proc/locks').read_text().splitlines()
                          if len(line.split()) > 4 and line.split()[4] in pids]
    return row


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--out', type=Path, required=True)
    parser.add_argument('--source-root', type=Path, required=True)
    args = parser.parse_args()
    require(bool(os.environ.get('HEXALITH_P1R_ISOLATION')), 'private runner required')
    manifest = json.loads((BASE / 'control-inputs-manifest.json').read_text())
    require(all(sha(ROOT / row['copy']) == row['sha256'] == row['copy_sha256'] for row in manifest['files']),
            'retained consumer copy changed')
    worker = executor.Executor(args.out, json.loads(INPUTS.read_text()), args.source_root)
    report = {'scope': 'bounded two-tenant discovery controls; not a qualification', 'qualified': False,
              'p1r_usable': False, 'script_sha256': sha(Path(__file__)), 'attempts': [], 'errors': [],
              'inputs_manifest_sha256': sha(BASE / 'control-inputs-manifest.json')}
    worker.evidence = {role: BASE / 'control-inputs' / role for role in ('comparison-3.110.0', 'candidate')}
    def save():
        executor.write_json(args.out / 'seed-controls.json', report)
    try:
        worker.topology()
        for version, role in (('3.110.0', 'comparison-3.110.0'), ('3.125.1', 'candidate')):
            checks = executor.Measurements()
            identity = worker.probe(version, ['identity'], checks, 'identity:')
            executor.write_json(args.out / (role + '-physical-identity.json'), identity)
            require(all(row['passed'] for row in checks.checks), 'physical identity probe failed')
            retained = json.loads((worker.evidence[role] / 'loaded/Probe.json').read_text())
            signature = lambda record: sorted((r['name'], r['version'], r['sha256']) for r in record['assemblies'])
            require(signature(identity) == signature(retained)
                    and all(Path(r['path']).is_relative_to(worker.evidence[role] / 'probe/bin/Release/net10.0')
                            and sha(Path(r['path'])) == r['sha256'] for r in identity['assemblies']),
                    'loaded package identities differ from retained binaries')
        for name, injection in (('cold-3.110.0', False), ('reused-discovery-3.110.0', False),
                                ('induced-eight-second-write-lock-3.110.0', True)):
            attempt = {'name': name, 'version': '3.110.0', 'started_utc': stamp(), 'injected': injection,
                       'tenants': {}, 'samples': [], 'errors': [], 'command_start': len(worker.commands) + 1}
            report['attempts'].append(attempt); save()
            measured = executor.Measurements()
            finished = threading.Event()
            def sampler():
                while not finished.wait(.5):
                    attempt['samples'].append(sample(worker, 'periodic'))
            observer = None; lock_thread = None
            try:
                worker.stop_nodes()
                attempt['samples'].append(sample(worker, 'before-start'))
                worker.postgres_query(worker.redis, "DO $$ BEGIN IF to_regclass('public.state') IS NOT NULL THEN TRUNCATE TABLE state; END IF; END $$;")
                worker.start_nodes('3.110.0', interval=1000)
                attempt['samples'].append(sample(worker, 'both-ready'))
                observer = threading.Thread(target=sampler); observer.start()
                if injection:
                    acquired = threading.Event()
                    def hold():
                        try:
                            with sqlite3.connect(worker.scratch / 'discovery.sqlite', timeout=1) as connection:
                                connection.execute('BEGIN IMMEDIATE')
                                attempt['lock_acquired_utc'] = stamp(); acquired.set()
                                time.sleep(8)
                                connection.rollback(); attempt['lock_released_utc'] = stamp()
                        except Exception as error:
                            attempt['lock_error'] = str(error); acquired.set()
                    lock_thread = threading.Thread(target=hold); lock_thread.start()
                    require(acquired.wait(2) and 'lock_error' not in attempt, 'task-private control lock unavailable')
                    time.sleep(6)
                for tenant, count in (('tenant-a', 12), ('tenant-b', 3)):
                    attempt['samples'].append(sample(worker, 'before-' + tenant))
                    try:
                        observed = worker.probe('3.110.0', ['seed', f'http://127.0.0.1:{worker.sidecar_port}', tenant, 'fixture', str(count)], measured, tenant + ':')
                        attempt['tenants'][tenant] = {'requested': count, 'observation': observed,
                                                     'command_id': worker.commands[-1]['id']}
                        measured.check(tenant + ':committed-count', observed.get('committed') == count and observed.get('sequence') == count)
                    except Exception as error:
                        attempt['tenants'][tenant] = {'requested': count, 'error': type(error).__name__ + ': ' + str(error),
                                                     'command_id': worker.commands[-1]['id']}
                        attempt['errors'].append(tenant + ': ' + str(error))
                    attempt['samples'].append(sample(worker, 'after-' + tenant))
                attempt['inventory'] = worker.inventory()
            except Exception as error:
                attempt['errors'].append(type(error).__name__ + ': ' + str(error))
            finally:
                if lock_thread:
                    lock_thread.join(12)
                    require(not lock_thread.is_alive(), 'lock control thread failed to finish')
                finished.set()
                if observer:
                    observer.join(5)
                attempt['samples'].append(sample(worker, 'before-stop'))
                worker.stop_nodes()
                attempt['samples'].append(sample(worker, 'after-stop'))
                attempt.update(checks=measured.checks, assertions=measured.counter, finished_utc=stamp(),
                               command_end=len(worker.commands),
                               unattempted_tenants=[name for name in ('tenant-a', 'tenant-b') if name not in attempt['tenants']])
                attempt['passed'] = not attempt['errors'] and measured.counter['failed'] == 0 and len(attempt['tenants']) == 2
                save()
                print(json.dumps({'attempt': name, 'passed': attempt['passed'], 'assertions': measured.counter}), flush=True)
        # Two finite verifier fixture controls, separate from discovery and any matrix.
        for name in ('coherent-retained-restore-3.125.1', 'mismatched-proof-refusal-3.125.1'):
            attempt = {'name': name, 'version': '3.125.1', 'started_utc': stamp(), 'errors': [],
                       'command_start': len(worker.commands) + 1}
            report['attempts'].append(attempt); save()
            measured = executor.Measurements()
            try:
                worker.stop_nodes()
                # A separately named fixture gets fresh discovery state. Previous attempts stay retained.
                attempt['discovery_epoch'] = {'reason': 'independent retained-fixture control after completed discovery controls', 'preserved_files': []}
                for suffix in ('', '-wal', '-shm', '-journal'):
                    old = Path(str(worker.scratch / 'discovery.sqlite') + suffix)
                    if old.exists():
                        target = worker.scratch / (name + '-prior-discovery' + suffix)
                        attempt['discovery_epoch']['preserved_files'].append({'from': str(old), 'to': str(target), 'sha256': sha(old)})
                        old.rename(target)
                if name.startswith('coherent'):
                    observed, outcome, disposition = worker.restore_case('bounded-positive', measured)
                    attempt.update(observations=observed, outcome=outcome, disposition=disposition,
                                   recovery=worker.pending_restore)
                else:
                    worker.seed('3.125.1', 10, measured)
                    worker.mutate('retained', 'mismatched-proof')
                    before = worker.inventory()
                    fixture = worker.last_retained_fixture
                    worker.start_nodes('3.125.1', interval=1000)
                    append = worker.actor('3.125.1', 'tenant-a', 12, 'IncrementCounter', measured, 'negative-append:')
                    measured.check('mismatched-proof-append-refused', append.get('accepted') is False and append.get('event_count') == 0)
                    worker.stop_nodes()
                    after = worker.inventory()
                    measured.check('mismatched-proof-domain-unchanged',
                        [r for r in before if r['kind'] != 'bookkeeping'] == [r for r in after if r['kind'] != 'bookkeeping'])
                    attempt.update(observations={'before': before, 'after': after, 'append': append, 'fixture': fixture},
                                   outcome='refusal' if append.get('accepted') is False else 'effect',
                                   disposition='compatible' if measured.counter['failed'] == 0 else 'incompatible')
            except Exception as error:
                attempt['errors'].append(type(error).__name__ + ': ' + str(error))
            finally:
                worker.stop_nodes()
                attempt.update(checks=measured.checks, assertions=measured.counter, finished_utc=stamp(),
                               command_end=len(worker.commands))
                attempt['passed'] = not attempt['errors'] and measured.counter['failed'] == 0
                save()
                print(json.dumps({'attempt': name, 'passed': attempt['passed'], 'assertions': measured.counter}), flush=True)
    except Exception as error:
        report['errors'].append(type(error).__name__ + ': ' + str(error))
    finally:
        try:
            report['cleanup'] = worker.cleanup()
            if worker.pending_restore is not None:
                receipt = worker.pending_restore
                receipt['cleanup'] = report['cleanup']
                commands = executor.runtime.validate_commands(receipt['commands'])
                rows = {name: value['rows'] for name, value in receipt['inventories'].items()}
                receipt['checks'] = executor.runtime.restore_observations(receipt, commands, rows)
                receipt['assertions'] = executor.checks_counter(receipt['checks'])
                receipt['execution'], receipt['compatibility'] = executor.runtime.disposition('operational', receipt['assertions']['failed'] == 0)
                executor.runtime.restore_outcome(receipt)
                executor.write_json(args.out / 'bounded-restore-recovery.json', receipt)
                report['bounded_restore_recovery'] = {'assertions': receipt['assertions'], 'execution': receipt['execution'],
                                                     'compatibility': receipt['compatibility']}
        except Exception as error:
            report['cleanup_error'] = str(error)
        report['finished_utc'] = stamp(); save()
    report['uninduced_controls_passed'] = all(a['passed'] for a in report['attempts'] if not a.get('injected'))
    save()
    # Exit status covers diagnostic collection/cleanup; individual failed attempts stay nonpassing.
    return 0 if not report['errors'] and report.get('cleanup', {}).get('execution') == 'passed' else 2


if __name__ == '__main__':
    raise SystemExit(main())

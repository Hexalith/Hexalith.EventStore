"""Retain one bounded private invocation and complete unfiltered host inventories."""
import hashlib
import json
from pathlib import Path
import subprocess
import sys
import uuid

ROOT = Path(__file__).resolve().parents[5]
sys.path.insert(0, str(ROOT / 'tools'))
from p1r_isolation import IsolatedRunner
from p1r_published_executor import safe_inspect_argv, docker_observations
from p1r_qualification import stamp, write_json

BASE = Path(__file__).resolve().parent
SOURCE = Path('/tmp/p1r-31251-tagged-source-import-da30571ce90c4296ba51b1d98b3c1775')


def inventory():
    argv = ['docker', 'ps', '-aq', '--no-trunc']
    data = subprocess.check_output(argv)
    ids = data.decode().split()
    rows = docker_observations(subprocess.check_output(safe_inspect_argv(*ids))) if ids else []
    return {'utc': stamp(), 'list_argv': argv, 'inspect_argv': safe_inspect_argv(*ids) if ids else None,
            'containers': sorted(rows, key=lambda row: row['Id']), 'scope': 'all containers; no exclusions'}


def main():
    invocation = BASE / ('seed-control-invocation-' + uuid.uuid4().hex)
    invocation.mkdir()
    report = {'scope': 'bounded controls only; no qualification', 'qualified': False, 'p1r_usable': False,
              'started_utc': stamp(), 'source_root': str(SOURCE), 'errors': []}
    before = inventory(); write_json(invocation / 'whole-host-before.json', before)
    command = ['python3', str(BASE / 'seed_controls.py'), '--out', str(invocation / 'runtime'), '--source-root', str(SOURCE), *sys.argv[1:]]
    report['command'] = command
    runner = None
    try:
        runner = IsolatedRunner(invocation / 'isolation', workspace=ROOT, source_root=SOURCE)
        report['exit_code'] = runner.run(command)
        report['isolation_passed'] = runner.result['passed']
    except BaseException as error:
        report['errors'].append(type(error).__name__ + ': ' + str(error))
        if runner:
            runner.cleanup()
    finally:
        after = inventory(); write_json(invocation / 'whole-host-after.json', after)
        b = {r['Id']: r for r in before['containers']}; a = {r['Id']: r for r in after['containers']}
        report['whole_host'] = {'before_count': len(b), 'after_count': len(a),
            'added': sorted(a.keys() - b.keys()), 'removed': sorted(b.keys() - a.keys()),
            'changed': [key for key in sorted(a.keys() & b.keys()) if a[key] != b[key]],
            'entire_set_unchanged': a == b,
            'before_sha256': hashlib.sha256((invocation / 'whole-host-before.json').read_bytes()).hexdigest(),
            'after_sha256': hashlib.sha256((invocation / 'whole-host-after.json').read_bytes()).hexdigest()}
        report['finished_utc'] = stamp(); write_json(invocation / 'invocation.json', report)
    print(json.dumps({'invocation': str(invocation), **report}), flush=True)
    return 0 if report.get('isolation_passed') and report['whole_host']['entire_set_unchanged'] else 2


if __name__ == '__main__':
    raise SystemExit(main())

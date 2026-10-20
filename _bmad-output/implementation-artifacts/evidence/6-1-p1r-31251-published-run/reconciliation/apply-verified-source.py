"""Apply verified P1R bytes only after matching the shared checkout preservation snapshot."""
import datetime
import hashlib
import json
import os
from pathlib import Path
import stat
import subprocess
import sys
import tempfile
import uuid

base = Path(__file__).resolve().parent
source = Path(__file__).resolve().parents[5]
target = Path('/home/administrator/projects/hexalith/eventstore')
suite = Path(sys.argv[1]).resolve()
prior = json.loads((base / 'shared-pre-reconcile-92d408e662554024b5f6eaee403331e6.json').read_bytes())
expected = {row['path']: row for row in prior['files']}
manifest = json.loads((suite / 'source-manifest-before.json').read_bytes())
audit = json.loads((suite / 'parent-independent-audit.json').read_bytes())
names = ['.gitattributes', 'tools/p1r-published-consumers/README.md',
         'tools/p1r-published-consumers/host/FixtureReminderIntentSource.cs',
         'tools/p1r-published-consumers/host/QualificationCapabilities.cs', 'tools/p1r-published-executor.py',
         'tools/p1r_check_witnesses.py', 'tools/p1r_isolation.py', 'tools/p1r_isolation_evidence.py',
         'tools/p1r_published_executor.py', 'tools/p1r_published_qualification.py', 'tools/p1r_qualification.py',
         'tools/tests/p1r_isolation_fixture.py', 'tools/tests/test_p1r_isolation.py',
         'tools/tests/test_p1r_isolation_hardening.py', 'tools/tests/test_p1r_published_executor.py',
         'tools/tests/test_p1r_published_qualification.py', 'tools/tests/test_p1r_qualification.py']
sha = lambda data: hashlib.sha256(data).hexdigest()
environment = dict(os.environ, GIT_OPTIONAL_LOCKS='0')
record = {'scope': 'compare-before-write reconciliation of verified tooling; preserve concurrent shared work',
          'started_utc': datetime.datetime.now(datetime.timezone.utc).isoformat(), 'source': str(source),
          'target': str(target), 'suite': str(suite), 'script_sha256': sha(Path(__file__).read_bytes()),
          'commands': [], 'files': [], 'errors': [], 'finished_utc': None}
output = base / ('source-application-' + uuid.uuid4().hex + '.json')
def save():
    output.write_text(json.dumps(record, indent=2) + '\n')
def git(root, *arguments):
    argv = ['git', *arguments]
    process = subprocess.run(argv, cwd=root, env=environment, capture_output=True)
    record['commands'].append({'argv': argv, 'cwd': str(root), 'exit_code': process.returncode,
                               'stdout': process.stdout.decode(), 'stderr': process.stderr.decode()})
    if process.returncode:
        raise RuntimeError('Git observation failed')
    return process.stdout
save()
try:
    if audit['total_test_executions'] != 406 or not audit['suite_inventory_and_literal_logs_match']:
        raise RuntimeError('required complete merged-source verification absent')
    if git(target, 'rev-parse', 'HEAD').decode().strip() != prior['head']:
        raise RuntimeError('shared HEAD advanced; reconcile before writing')
    for arguments in (('status', '--short', '--branch'), ('remote', '-v'), ('log', '-5', '--format=%H')):
        git(target, *arguments)
    index = target / '.git/index'
    if sha(index.read_bytes()) != prior['index_sha256']:
        raise RuntimeError('shared index changed; reconcile before writing')
    changed = git(source, 'diff', '--name-only', '--', '.gitattributes', 'tools').decode().splitlines()
    if set(changed) != set(names):
        raise RuntimeError('source application scope differs')
    copies = []
    for name in names:
        src, dst = source / name, target / name
        if src.is_symlink() or dst.is_symlink() or not dst.is_file():
            raise RuntimeError('nonregular source application path: ' + name)
        data, old = src.read_bytes(), dst.read_bytes()
        if sha(data) != manifest[name] or sha(old) != expected[name]['sha256']:
            raise RuntimeError('source or concurrent target bytes changed: ' + name)
        copies.append((name, data, sha(old), stat.S_IMODE(dst.stat().st_mode)))
    for filename in ('run-required-suites.py', 'audit-complete-suites-v2.py'):
        src = base / filename
        dst = target / base.relative_to(source) / filename
        if dst.exists() and dst.read_bytes() != src.read_bytes():
            raise RuntimeError('existing shared evidence helper differs: ' + filename)
    for name, data, before, mode in copies:
        dst = target / name
        if sha(dst.read_bytes()) != before:
            raise RuntimeError('concurrent write before application: ' + name)
        descriptor, temporary = tempfile.mkstemp(prefix='.p1r-apply-', dir=dst.parent)
        with os.fdopen(descriptor, 'wb') as stream:
            stream.write(data)
            os.fchmod(stream.fileno(), mode)
        os.replace(temporary, dst)
        record['files'].append({'path': name, 'before_sha256': before, 'after_sha256': sha(dst.read_bytes()),
                                'mode': mode, 'matches_verified_source': dst.read_bytes() == data})
        save()
    for filename in ('run-required-suites.py', 'audit-complete-suites-v2.py'):
        dst = target / base.relative_to(source) / filename
        dst.parent.mkdir(parents=True, exist_ok=True)
        if not dst.exists():
            dst.write_bytes((base / filename).read_bytes())
    if sha(index.read_bytes()) != prior['index_sha256']:
        raise RuntimeError('shared index changed during source application')
    record['index_unchanged'] = True
    record['head_unchanged'] = git(target, 'rev-parse', 'HEAD').decode().strip() == prior['head']
except Exception as error:
    record['errors'].append({'type': type(error).__name__, 'message': str(error)})
finally:
    record['finished_utc'] = datetime.datetime.now(datetime.timezone.utc).isoformat()
    save()
print(json.dumps({'record': str(output), 'files': len(record['files']), 'errors': record['errors']}))
raise SystemExit(2 if record['errors'] else 0)

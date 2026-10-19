"""Run all required integration suites sequentially against unchanged source bytes."""
import datetime
import hashlib
import json
from pathlib import Path
import re
import subprocess
import sys
import uuid

root = Path(__file__).resolve().parents[5]
base = Path(__file__).resolve().parent / ('verification-' + uuid.uuid4().hex)
base.mkdir()


def sources():
    paths = [p for p in (root / 'tools').rglob('*') if p.is_file()
             and not set(p.relative_to(root).parts) & {'bin', 'obj', '__pycache__', 'node_modules'}
             and p.suffix in {'.py', '.cs', '.csproj', '.props', '.targets', '.md', '.json', '.yaml', '.yml'}]
    paths += [root / name for name in ('.gitattributes', '.editorconfig', 'global.json',
              'Directory.Build.props', 'Directory.Build.targets', 'Directory.Packages.props') if (root / name).is_file()]
    return {str(p.relative_to(root)): hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(paths)}


frozen = sources()
(base / 'source-manifest-before.json').write_text(json.dumps(frozen, indent=2) + '\n')
rows = []
for name in ('qualification', 'published_qualification', 'published_executor', 'snapshot_registration',
             'isolation', 'isolation_hardening', 'isolation_hardening_optimized'):
    argv = [sys.executable, *(['-O'] if name.endswith('_optimized') else []), '-m', 'unittest', 'discover',
            '-s', 'tools/tests', '-p', 'test_p1r_' + name.replace('_optimized', '') + '.py']
    started = datetime.datetime.now(datetime.timezone.utc).isoformat()
    print(json.dumps({'started': name, 'argv': argv}), flush=True)
    process = subprocess.run(argv, cwd=root, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
    log = base / (name + '.log')
    log.write_bytes(process.stdout)
    text = process.stdout.decode(errors='replace')
    count = re.findall(r'^Ran (\d+) tests? in ', text, re.M)
    row = {'name': name, 'argv': argv, 'cwd': str(root), 'started_utc': started,
           'finished_utc': datetime.datetime.now(datetime.timezone.utc).isoformat(), 'exit_code': process.returncode,
           'log': str(log), 'log_sha256': hashlib.sha256(process.stdout).hexdigest(),
           'tests': int(count[-1]) if count else None, 'ok': text.rstrip().endswith('OK'),
           'skips': 'skipped=' in text, 'source_unchanged': sources() == frozen}
    (base / (name + '.json')).write_text(json.dumps(row, indent=2) + '\n')
    rows.append(row)
    (base / 'index.json').write_text(json.dumps({'scope': 'complete integration verification after compatibility repairs',
        'root': str(root), 'driver_sha256': hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
        'source_manifest_sha256': hashlib.sha256((base / 'source-manifest-before.json').read_bytes()).hexdigest(),
        'suites': rows}, indent=2) + '\n')
    print(json.dumps(row), flush=True)
    if process.returncode:
        print(text[-15000:], flush=True)
passed = all(row['exit_code'] == 0 and row['ok'] and not row['skips'] and row['source_unchanged'] for row in rows)
print(json.dumps({'out': str(base), 'all_passed': passed}), flush=True)
raise SystemExit(0 if passed else 2)

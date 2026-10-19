"""Retain a literal, separate-reader compatibility diagnostic without qualification."""
import argparse
import datetime
import hashlib
import json
from pathlib import Path
import subprocess
import sys
import uuid

parser = argparse.ArgumentParser(description=__doc__)
for name in ('execution', 'inputs', 'reader-source'):
    parser.add_argument('--' + name, type=Path, required=True)
args = parser.parse_args()
root = Path(__file__).resolve().parents[5]
base = Path(__file__).resolve().parent
script = base / 'diagnose-retained-producer.py'
out = base / ('retained-producer-diagnostic-' + uuid.uuid4().hex)
argv = [sys.executable, str(script), '--reader-root', str(root), '--reader-source', str(args.reader_source),
        '--execution', str(args.execution), '--inputs', str(args.inputs), '--out', str(out)]
sha = lambda data: hashlib.sha256(data).hexdigest()
record = {'scope': 'nonqualifying compatibility diagnostic with separate producer and current merged reader closures',
          'argv': argv, 'cwd': str(root), 'driver_argv': sys.argv,
          'started_utc': datetime.datetime.now(datetime.timezone.utc).isoformat(),
          'script_sha256': sha(script.read_bytes()), 'driver_sha256': sha(Path(__file__).read_bytes())}
record_path = out.with_suffix('.command.json')
record_path.write_text(json.dumps(record, indent=2) + '\n')
log = out.with_suffix('.log')
with log.open('wb') as stream:
    process = subprocess.run(argv, cwd=root, stdout=stream, stderr=subprocess.STDOUT)
record.update(exit_code=process.returncode, finished_utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
              log=str(log), log_sha256=sha(log.read_bytes()))
record_path.write_text(json.dumps(record, indent=2) + '\n')
print(json.dumps(record, indent=2), flush=True)
print(log.read_text()[-5000:], flush=True)
raise SystemExit(process.returncode)

#!/usr/bin/env python3
import datetime
import hashlib
import json
from pathlib import Path
import subprocess
import sys
p = Path(__file__).resolve().parent
gate = p / 'qualification-gate.py'
log = p / 'qualification-gate.log'
receipt = p / 'qualification-gate.json'
if log.exists() or receipt.exists():
    raise FileExistsError('Qualification gate receipt is immutable')
command = [sys.executable, '-B', str(gate)]
before = hashlib.sha256(gate.read_bytes()).hexdigest()
started = datetime.datetime.now(datetime.timezone.utc).isoformat()
with log.open('xb') as stream:
    run = subprocess.run(command, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
    stream.write(run.stdout)
data = {'command': command, 'startedUtc': started, 'completedUtc': datetime.datetime.now(datetime.timezone.utc).isoformat(),
        'exitCode': run.returncode, 'expectedExitCode': 1, 'scriptSha256Before': before,
        'scriptSha256After': hashlib.sha256(gate.read_bytes()).hexdigest(),
        'outputSha256': hashlib.sha256(log.read_bytes()).hexdigest(),
        'meaning': 'Qualification remains blocked by retained failed regressions; no waiver or approval.'}
with receipt.open('x') as stream:
    stream.write(json.dumps(data, indent=2) + '\n')
print(json.dumps(data, indent=2))
sys.exit(run.returncode)

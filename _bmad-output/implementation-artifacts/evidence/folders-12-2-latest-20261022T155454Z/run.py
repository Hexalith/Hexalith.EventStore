#!/usr/bin/env python3
"""Retain literal command receipts without shell interpolation or implicit retries."""
import datetime
import json
import os
from pathlib import Path
import subprocess
import sys
import time

PACKET = Path(__file__).resolve().parent
ROOT = Path(os.environ.get('EVENTSTORE_QUALIFICATION_SOURCE_ROOT', str(PACKET.parents[3])))


def main():
    label, *argv = sys.argv[1:]
    receipt = PACKET / (label + '.json')
    if receipt.exists():
        raise SystemExit('Refusing to overwrite receipt: ' + str(receipt))
    selected = {k: v for k, v in os.environ.items() if k.startswith(('HEXALITH_ACTOR_ADMISSION_EVIDENCE_', 'EVENTSTORE_TEST_', 'EVENTSTORE_QUALIFICATION_', 'BOUNDED_IDENTITY_', 'DOTNET_USE_POLLING_', 'DOTNET_HOSTBUILDER__RELOAD')) or k in ('UseHexalithProjectReferences', 'UseNuGetDeps')}
    record = {'argv': argv, 'cwd': str(ROOT), 'utc_start': datetime.datetime.now(datetime.timezone.utc).isoformat(), 'environment_overrides': selected, 'runner_pid': os.getpid()}
    receipt.write_text(json.dumps(record, indent=2) + '\n')
    started = time.monotonic()
    with (PACKET / (label + '.log')).open('wb') as output:
        process = subprocess.Popen(argv, cwd=ROOT, stdout=output, stderr=subprocess.STDOUT)
        record['pid'] = process.pid
        receipt.write_text(json.dumps(record, indent=2) + '\n')
        try:
            result = process.wait()
        except KeyboardInterrupt:
            process.send_signal(2)
            result = process.wait()
            record['interrupted'] = True
    record.update(exit_code=result, elapsed_seconds=time.monotonic() - started, utc_end=datetime.datetime.now(datetime.timezone.utc).isoformat())
    receipt.write_text(json.dumps(record, indent=2) + '\n')
    print(json.dumps(record))
    return result


if __name__ == '__main__':
    sys.exit(main())

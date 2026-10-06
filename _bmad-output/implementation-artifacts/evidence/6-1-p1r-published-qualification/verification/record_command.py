#!/usr/bin/env python3
"""Run one command and retain its exact argv, cwd, UTC times, exit status and combined output hash.

Usage: record_command.py RECEIPT.json EXPECTED_EXIT -- ARGV...
The output is written next to the receipt as RECEIPT.txt; an existing receipt is never overwritten.
"""
import datetime as dt
import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys


def main():
    receipt = Path(sys.argv[1]).absolute()
    expected = int(sys.argv[2])
    if sys.argv[3] != "--":
        raise SystemExit("usage: record_command.py RECEIPT.json EXPECTED_EXIT -- ARGV...")
    argv = sys.argv[4:]
    output = receipt.with_suffix(".txt")
    if receipt.exists() or output.exists():
        raise SystemExit("refusing to overwrite an existing receipt")
    started = dt.datetime.now(dt.timezone.utc).isoformat()
    result = subprocess.run(argv, cwd=os.getcwd(), stdout=subprocess.PIPE, stderr=subprocess.STDOUT, check=False)
    finished = dt.datetime.now(dt.timezone.utc).isoformat()
    with output.open("xb") as handle:
        handle.write(result.stdout)
    value = {"argv": argv, "cwd": os.getcwd(), "started_utc": started, "finished_utc": finished,
             "exit_code": result.returncode, "expected_exit_code": expected,
             "output": {"path": output.name, "sha256": hashlib.sha256(result.stdout).hexdigest()}}
    with receipt.open("x") as handle:
        handle.write(json.dumps(value, indent=2, sort_keys=True) + "\n")
    print(json.dumps({"receipt": receipt.name, "exit_code": result.returncode, "expected_exit_code": expected}))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

"""Retain exact local qualification commands, complete output and dispositions."""
import datetime
import json
import pathlib
import subprocess
import sys
import time

directory = pathlib.Path(__file__).resolve().parent
label, *command = sys.argv[1:]
started = datetime.datetime.now(datetime.timezone.utc).isoformat()
before = time.monotonic()
with (directory / (label + ".log")).open("wb") as output:
    result = subprocess.run(command, stdout=output, stderr=subprocess.STDOUT)
receipt = dict(command=command, cwd=str(pathlib.Path.cwd()), started_at=started,
               elapsed_seconds=time.monotonic() - before, exit_code=result.returncode)
(directory / (label + ".json")).write_text(json.dumps(receipt, indent=2) + "\n")
print(json.dumps(receipt))
print((directory / (label + ".log")).read_text(errors="replace")[-5000:])
sys.exit(result.returncode)

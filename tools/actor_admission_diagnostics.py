"""Export actor admission outcomes and runner line numbers from private CI evidence."""

import json
from pathlib import Path
import re
import sys
import xml.etree.ElementTree as ET

RUNNER_FRAME = re.compile(r'^  File "[^"\n]*/qualify-internal-actor-admission\.py", line ([0-9]+), in (\w+)$', re.MULTILINE)


def read_json(path):
    try:
        return json.loads(path.read_text())
    except (FileNotFoundError, json.JSONDecodeError):
        return {}


def runner_frames(trace):
    return [{"line": int(line), "function": function} for line, function in RUNNER_FRAME.findall(trace)]


def startup_frames(trx):
    if trx is None or not trx.is_file():
        return []
    try:
        results = ET.parse(trx)
    except ET.ParseError:
        return []
    for test in results.iterfind(".//{*}UnitTestResult"):
        if ".ActorCallbackAdmissionProcessTests." in test.get("testName", ""):
            return runner_frames(test.findtext(".//{*}Message") or "")
    return []


def summarize(evidence, trx=None):
    result = read_json(evidence / "result.json")
    cleanup = read_json(evidence / "cleanup.json")
    report = {key: result[key] if isinstance(result.get(key), bool) else None
              for key in ("qualification_passed", "cleanup_passed")}
    report["remaining"] = {
        key: sum(item.get(flag) is False for item in cleanup[key]) if key in cleanup else None
        for key, flag in (("processes", "absent"), ("containers", "absent"), ("logs", "closed"))
    }
    report["runtime_absent"] = cleanup.get("runtime_absent") if isinstance(cleanup.get("runtime_absent"), bool) else None
    report["frames"] = {}
    for phase, filename in (("qualification", "failure.txt"), ("cleanup", "cleanup-failure.txt")):
        path = evidence / filename
        if path.is_file():
            report["frames"][phase] = runner_frames(path.read_text(errors="replace"))
    if not result and "qualification" not in report["frames"] and (frames := startup_frames(trx)):
        report["frames"]["startup"] = frames
    return report


if __name__ == "__main__":
    output = Path(sys.argv[2])
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(summarize(Path(sys.argv[1]), output.parent / "live-sidecar-results.trx"), indent=2) + "\n")

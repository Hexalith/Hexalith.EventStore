"""Retain independent fresh source-only controls; never reuse an output path."""
import argparse
import datetime
import hashlib
import json
from pathlib import Path
import shlex
import subprocess
import sys
import time

BUNDLE = Path(__file__).resolve().parent
ROOT = BUNDLE.parents[3]
WORKSPACE = ROOT.parents[1]
sys.path.insert(0, str(ROOT / "tools"))
import p1r_qualification as preparation


def write(path, value):
    with path.open("x") as stream:
        json.dump(value, stream, indent=2, sort_keys=True)
        stream.write("\n")


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--out", type=Path, required=True)
    parser.add_argument("--live-smoke", action="store_true")
    args = parser.parse_args()
    out = args.out.absolute()
    if out.exists() or not out.is_relative_to(BUNDLE):
        raise SystemExit("verification output must be new and inside the bundle")
    out.mkdir()
    binding = preparation.source_binding()
    write(out / "source-before.json", binding)
    records = []

    def run(name, argv, cwd=ROOT, timeout=600):
        log = out / (name + ".log")
        started = datetime.datetime.now(datetime.timezone.utc).isoformat()
        began = time.monotonic()
        timed_out = False
        with log.open("xb") as stream:
            try:
                code = subprocess.run(argv, cwd=cwd, stdout=stream, stderr=subprocess.STDOUT,
                                      timeout=timeout).returncode
            except subprocess.TimeoutExpired:
                code, timed_out = 124, True
        record = {"argv": argv, "command": shlex.join(argv), "cwd": str(cwd), "started_utc": started,
                  "duration_seconds": time.monotonic() - began, "exit_code": code,
                  "timed_out": timed_out, "log": log.name, "log_sha256": sha(log), "passed": code == 0}
        records.append(record)
        write(out / (name + ".json"), record)
        print(json.dumps({"command": name, "exit_code": code}), flush=True)
        return code

    suites = ["qualification", "published_qualification", "published_executor", "isolation"]
    if (ROOT / "tools/tests/test_p1r_isolation_hardening.py").exists():
        suites.append("isolation_hardening")
    for suite in suites:
        run("python-" + suite, ["python3", "-B", "-m", "unittest", "discover", "-s", "tools/tests",
                                "-p", "test_p1r_" + suite + ".py", "-v"])
    run("python-optimized-importer", ["python3", "-O", "-B", "-m", "unittest", "discover", "-s",
                                      "tools/tests", "-p", "test_p1r_isolation_hardening.py", "-v"])
    run("eventstore-whitespace", ["git", "diff", "--check"])
    run("projects-whitespace", ["git", "diff", "--check"], cwd=WORKSPACE)
    run("production-authority", ["python3", "tools/planning/validate_production_authority.py",
                                  "--story-id", "6.1"], cwd=WORKSPACE)
    if args.live_smoke and all(record["passed"] for record in records):
        smoke = out / "live-isolation"
        run("live-isolation-smoke", ["python3", "-B", "tools/p1r-isolated-runner.py", "--out", str(smoke),
                                      "--", "python3", "-c", "print('p1r-source-only-hardening-smoke', flush=True)"], timeout=900)
        wrapper = json.loads((smoke / "isolation.json").read_bytes())
        private = json.loads((smoke / "private-execution.json").read_bytes()) if (smoke / "private-execution.json").is_file() else {}
        smoke_passed = (wrapper.get("passed") is True and private.get("passed") is True
                        and len(wrapper.get("cleanup", [])) == len(private.get("cleanup", [])) == 2
                        and not wrapper.get("pending_containers")
                        and not Path(wrapper["owned_scratch"]).exists())
        write(out / "live-smoke-assessment.json", {"passed": smoke_passed,
              "wrapper_sha256": sha(smoke / "isolation.json"), "scope": "source-only Python smoke; no package qualification"})
        if not smoke_passed:
            records[-1]["passed"] = False
    final_binding = preparation.source_binding()
    write(out / "source-after.json", final_binding)
    source_unchanged = binding == final_binding
    result = {"passed": source_unchanged and all(record["passed"] for record in records), "commands": records,
              "source_unchanged": source_unchanged, "source_before_sha256": sha(out / "source-before.json"),
              "source_after_sha256": sha(out / "source-after.json"), "driver_sha256": sha(Path(__file__)),
              "p1r_usable": False, "story_6_1": "blocked", "package_qualified": False}
    write(out / "verification.json", result)
    print(json.dumps({"passed": result["passed"], "source_unchanged": source_unchanged}), flush=True)
    return 0 if result["passed"] else 2


if __name__ == "__main__":
    raise SystemExit(main())

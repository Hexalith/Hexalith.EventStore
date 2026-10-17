"""Bind the final handoff and append-only wrapper without rerunning native cohorts."""
import datetime
import hashlib
import json
import pathlib
import subprocess

HERE = pathlib.Path(__file__).resolve().parent
ROOT = HERE.parents[4]
WRAPPER = "_bmad-output/implementation-artifacts/spec-6-4-projection-cost-and-sequence-guard-implementation.md"


def sha(path):
    return hashlib.sha256(pathlib.Path(path).read_bytes()).hexdigest()


entry = json.loads((HERE / "entry.json").read_text())
protected = {key: value for key, value in entry["preservedFiles"].items() if key != WRAPPER}
assert all((ROOT / key).is_file() and sha(ROOT / key) == value for key, value in protected.items())
original = (HERE / "wrapper-entry.md").read_bytes()
assert hashlib.sha256(original).hexdigest() == entry["preservedFiles"][WRAPPER]
assert (ROOT / WRAPPER).read_bytes().startswith(original)
sources = json.loads((HERE / "live-release-full.json").read_text())["after"]["sourceInputs"]
assert all((ROOT / key).is_file() and sha(ROOT / key) == value for key, value in sources.items())
head = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=ROOT, text=True).strip()
index = hashlib.sha256(subprocess.check_output(["git", "ls-files", "-s", "-z"], cwd=ROOT)).hexdigest()
assert head == entry["head"] and index == entry["indexSha256"]
check = subprocess.run(["git", "diff", "--check"], cwd=ROOT, capture_output=True, text=True)
assert check.returncode == 0, check.stdout + check.stderr
paths = [pathlib.Path(__file__), HERE / "parent-final-handoff.md", HERE / "correction-handoff.md",
         HERE / "implementation-closing-audit.json", HERE / "parent-command-ledger.json",
         HERE / "parent-native-audit.json", HERE / "parent-completed-release-audit.json",
         HERE / "parent-final-preservation-audit.json", HERE / "parent-final-resource-audit.json",
         HERE / "parent-all-runtime-frame-resource-audit.json", ROOT / WRAPPER]
report = {"checkedUtc": datetime.datetime.now(datetime.timezone.utc).isoformat(), "head": head,
          "boundFiles": [{"path": str(path.relative_to(ROOT)), "sha256": sha(path)} for path in paths],
          "protectedFilesUnchanged": len(protected), "wrapperOriginalPrefixBytes": len(original),
          "wrapperAppendOnly": True, "sourceInputsStillMatchCompletedCohorts": len(sources),
          "indexUnchanged": True, "gitDiffCheck": {"command": ["git", "diff", "--check"], "exitCode": 0},
          "acceptance": "in-progress; Release failed/unproven; separate formal gates open",
          "scope": "Local final preservation/handoff audit only. No native execution, Git mutation, approval or authenticated CI seal."}
target = HERE / "parent-final-handoff-audit.json"
assert not target.exists()
target.write_text(json.dumps(report, indent=2) + "\n")
print(json.dumps({"report": str(target.relative_to(ROOT)), "protectedFilesUnchanged": len(protected),
                  "sourceInputsUnchanged": len(sources), "wrapperAppendOnly": True, "indexUnchanged": True,
                  "gitDiffCheck": 0, "acceptance": report["acceptance"]}))

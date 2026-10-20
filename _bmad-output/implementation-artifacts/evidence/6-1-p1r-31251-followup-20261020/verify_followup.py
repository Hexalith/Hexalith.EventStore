"""Recheck this follow-up's preservation scope; report unrelated drift separately.

Reads files and runs only the local forensic reader and read-only Git commands.
Prints JSON and does not write any evidence or invoke the historical audit tools.
"""
import hashlib
import json
import re
import subprocess
import sys
from pathlib import Path


OUT = Path(__file__).resolve().parent
ROOT = OUT.parents[3]
ENTRY = json.loads((OUT / "entry.json").read_text())


def sha(path):
    result = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            result.update(block)
    return result.hexdigest()


def changes(files):
    return [{"path": path, "before_sha256": before,
             "after_sha256": sha(ROOT / path) if (ROOT / path).is_file() else None}
            for path, before in files.items()
            if not (ROOT / path).is_file() or sha(ROOT / path) != before]


def git(*args):
    return subprocess.check_output(["git", *args], cwd=ROOT)


wrapper = "_bmad-output/implementation-artifacts/spec-6-1-p1r-31251-published-qualification.md"
old_wrapper = git("show", ENTRY["head"] + ":" + wrapper)
new_wrapper = (ROOT / wrapper).read_bytes()
frozen = lambda value: re.search(b"<frozen-after-approval.*?</frozen-after-approval>", value, re.S).group()
old_attributes = git("show", ENTRY["head"] + ":.gitattributes")
new_attributes = (ROOT / ".gitattributes").read_bytes()
addition = (b"\n# Read-only P1R follow-up binds the original evidence and exact diagnostic bytes.\n"
            b"_bmad-output/implementation-artifacts/evidence/6-1-p1r-31251-followup-20261020/** -text -whitespace\n")
historical = changes(ENTRY["historical_files"])
historical_paths = {str(path.relative_to(ROOT)) for path in (ROOT / ENTRY["historical_root"]).rglob("*") if path.is_file()}
historical_added = sorted(historical_paths - set(ENTRY["historical_files"]))
protected = changes({path: value for path, value in ENTRY["protected_files"].items()
                     if path not in (wrapper, ".gitattributes")})
preexisting = changes(ENTRY["preexisting_worktree_files"])
replays = []
for flags in ([], ["-O"]):
    argv = [sys.executable, *flags, str((OUT / "inspect_v4.py").relative_to(ROOT))]
    result = subprocess.run(argv, cwd=ROOT, capture_output=True)
    replays.append({"argv": argv, "exit_code": result.returncode,
                    "stdout_sha256": hashlib.sha256(result.stdout).hexdigest(),
                    "identical_to_diagnosis": result.stdout == (OUT / "diagnosis.json").read_bytes(),
                    "stderr": result.stderr.decode()})
diff_argv = ["git", "diff", "--check", "--", ".gitattributes", wrapper]
diff = subprocess.run(diff_argv, cwd=ROOT, capture_output=True, text=True)
text_errors = []
for path in [ROOT / wrapper, ROOT / ".gitattributes", *OUT.iterdir()]:
    if not path.is_file() or path.name == "SHA256SUMS":
        continue
    data = path.read_bytes()
    try:
        value = data.decode("utf-8")
        if b"\r" in data or value and not value.endswith("\n") or any(line.rstrip() != line for line in value.splitlines()):
            text_errors.append(str(path.relative_to(ROOT)))
    except UnicodeError:
        text_errors.append(str(path.relative_to(ROOT)))
checks = {
    "both_offline_replays_identical": all(row["exit_code"] == 0 and row["identical_to_diagnosis"] for row in replays),
    "historical_files_unchanged": not historical and not historical_added,
    "source_tools_tests_other_protected_inputs_unchanged": not protected,
    "wrapper_original_bytes_match_entry": hashlib.sha256(old_wrapper).hexdigest() == ENTRY["protected_files"][wrapper],
    "wrapper_append_only": new_wrapper.startswith(old_wrapper),
    "frozen_intent_unchanged": frozen(old_wrapper) == frozen(new_wrapper),
    "original_attributes_preserved": new_attributes.replace(addition, b"", 1) == old_attributes
        and hashlib.sha256(old_attributes).hexdigest() == ENTRY["protected_files"][".gitattributes"],
    "head_unchanged": git("rev-parse", "HEAD").decode().strip() == ENTRY["head"],
    "index_unchanged": sha(ROOT / ".git/index") == ENTRY["index_sha256"],
    "diff_check": diff.returncode == 0,
    "text_checks": not text_errors,
}
print(json.dumps({
    "scope": "offline forensic reproducibility and task-scope preservation; no runtime qualification",
    "qualified": False, "p1r_usable": False, "scope_checks": checks,
    "commands": [*replays, {"argv": diff_argv, "exit_code": diff.returncode, "stdout": diff.stdout, "stderr": diff.stderr}],
    "historical_files": ENTRY["historical_file_count"], "historical_bytes": ENTRY["historical_bytes"],
    "historical_changes": historical, "historical_additions": historical_added, "protected_changes": protected,
    "preexisting_worktree_observation": {
        "all_unchanged": not preexisting, "count": len(ENTRY["preexisting_worktree_files"]), "changes": preexisting,
        "limit": "Unrelated files may change during this capture. No restoration or repair of them is performed; whole-worktree equality is reported literally.",
    },
    "text_errors": text_errors,
}, indent=2, sort_keys=True))
sys.exit(0 if all(checks.values()) else 1)

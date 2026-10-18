"""Read-only preservation audit; emit JSON to stdout without changing either repo."""
import argparse
import ast
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import subprocess


MUTABLE_OWNER_PATHS = (
    "tools/p1r-published-consumers/Directory.Build.props",
    "tools/p1r-published-consumers/host/Host.csproj",
    "tools/p1r-published-consumers/domain/Domain.csproj",
    "tools/p1r_published_executor.py",
    "tools/p1r_check_witnesses.py",
    "tools/p1r_published_qualification.py",
    "tools/tests/test_p1r_published_executor.py",
    "tools/tests/test_p1r_snapshot_registration.py",
    "tools/tests/test_p1r_published_qualification.py",
    "tools/p1r-published-consumers/README.md",
)
ADMISSION_PATHS = MUTABLE_OWNER_PATHS[:6]


def git(repository, *arguments):
    return subprocess.check_output(["git", "-C", str(repository), *arguments])


def sha256(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def audit_repository(repository, prior, excluded):
    tracked = git(repository, "ls-files", "-z").decode().split("\0")
    selected = {name for name in tracked if name and name not in excluded
                and (repository / name).is_file()}
    expected = prior["protected_sha256"]
    changed = [name for name, digest in expected.items()
               if (repository / name).is_file() and sha256(repository / name) != digest]
    missing = [name for name in expected if not (repository / name).is_file()]
    head = git(repository, "rev-parse", "HEAD").decode().strip()
    branch = git(repository, "branch", "--show-current").decode().strip()
    history = git(repository, "log", "-5", "--format=%H").decode().splitlines()
    links = [line for line in git(repository, "ls-files", "--stage").decode().splitlines()
             if line.startswith("160000 ")]
    return {
        "protected_paths": len(expected), "changed": changed, "missing": missing,
        "selection_added": sorted(selected - set(expected)),
        "selection_removed": sorted(set(expected) - selected),
        "head": head, "head_unchanged": head == prior["head"],
        "branch_unchanged": branch == prior["branch"],
        "history_unchanged": history == prior["history"],
        "gitlinks_unchanged": links == prior["gitlinks"],
    }


def guard_bodies(source):
    return {node.name: ast.get_source_segment(source, node)
            for node in ast.walk(ast.parse(source))
            if isinstance(node, ast.FunctionDef)
            and node.name in ("validate_execution_inputs", "observe_inputs")}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repository", required=True, type=Path)
    parser.add_argument("--workspace", required=True, type=Path)
    parser.add_argument("--baseline", required=True, type=Path)
    parser.add_argument("--verification-receipts", required=True, action="append", type=Path)
    arguments = parser.parse_args()
    started = datetime.now(timezone.utc)
    before = json.loads(arguments.baseline.read_bytes())
    repository = arguments.repository.resolve()
    result = {
        "scope": "Post-verification read-only preservation; no package qualification.",
        "procedure_sha256": sha256(Path(__file__)),
        "baseline_sha256": sha256(arguments.baseline),
        "selection": {
            "rule": "git ls-files -z regular files, excluding only the exact listed owner paths; workspace excludes none.",
            "owner_exclusions": MUTABLE_OWNER_PATHS,
            "workspace_exclusions": [],
            "comparison": "Each original selected path is rehashed by SHA-256; current selected membership is also compared.",
        },
        "normalization": {
            "paths": ADMISSION_PATHS,
            "rule": "Remove only exact , \"3.125.1\", ,'3.125.1', or MSBuild or/and EventStoreVersion 3.125.1 comparison tokens; compare full remaining text with the original owner commit.",
            "baseline_commit": before["EventStore"]["head"],
        },
        "EventStore": audit_repository(repository, before["EventStore"], MUTABLE_OWNER_PATHS),
        "Projects": audit_repository(arguments.workspace.resolve(), before["Projects"], ()),
    }
    preservation = {}
    for name in ADMISSION_PATHS:
        prior = git(repository, "show", before["EventStore"]["head"] + ":" + name).decode()
        normalized = (repository / name).read_text().replace(', "3.125.1"', "").replace(",'3.125.1'", "")
        for operator, conjunction in (("==", "or"), ("!=", "and")):
            normalized = normalized.replace(
                f" {conjunction} '$(EventStoreVersion)' {operator} '3.125.1'", "")
        preservation[name] = {"only_exact_31251_membership_added": normalized == prior}
    result["admission_preservation"] = preservation
    readme = "tools/p1r-published-consumers/README.md"
    result["historical_readme_prefix_unchanged"] = (repository / readme).read_bytes().startswith(
        git(repository, "show", before["EventStore"]["head"] + ":" + readme))
    executor = "tools/p1r_published_executor.py"
    prior_guards = guard_bodies(git(repository, "show", before["EventStore"]["head"] + ":" + executor).decode())
    current_guards = guard_bodies((repository / executor).read_text())
    result["immutable_guards"] = {
        name: {"unchanged": body == current_guards[name],
               "sha256": hashlib.sha256(body.encode()).hexdigest()}
        for name, body in prior_guards.items()
    }
    verification = []
    finishes = []
    for path in arguments.verification_receipts:
        rows = json.loads(path.read_bytes())
        verification.append({"path": str(path), "sha256": sha256(path),
                             "all_passed": all(row["exit_code"] == 0 for row in rows)})
        finishes.extend(datetime.fromisoformat(row["finished_utc"]) for row in rows)
    result["verification_receipts"] = verification
    result["latest_verification_finished_utc"] = max(finishes).isoformat()
    result["started_utc"] = started.isoformat()
    result["observed_at"] = datetime.now(timezone.utc).isoformat()
    result["after_verification"] = started >= max(finishes)
    result["passed"] = (
        all(not result[key][field] for key in ("EventStore", "Projects")
            for field in ("changed", "missing", "selection_added", "selection_removed"))
        and all(result[key][field] for key in ("EventStore", "Projects")
                for field in ("head_unchanged", "branch_unchanged", "history_unchanged", "gitlinks_unchanged"))
        and all(item["only_exact_31251_membership_added"] for item in preservation.values())
        and all(item["unchanged"] for item in result["immutable_guards"].values())
        and result["historical_readme_prefix_unchanged"] and result["after_verification"]
        and all(item["all_passed"] for item in verification)
    )
    print(json.dumps(result, indent=2) + "\n", end="")
    return 0 if result["passed"] else 1


if __name__ == "__main__":
    raise SystemExit(main())

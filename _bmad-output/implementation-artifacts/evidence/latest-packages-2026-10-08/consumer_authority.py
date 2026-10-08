"""Capture the current Contracts test's sealed preflight and exact exclusions."""

import hashlib
import json
import os
import re
import subprocess
from datetime import datetime, timezone
from pathlib import Path


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def inspect_configuration(root):
    source = root / "tests/Hexalith.EventStore.Contracts.Tests/Packaging/ContractsPackageDependencyTests.cs"
    text = source.read_text()
    constants = dict(re.findall(r'private const string (\w+)\s*=\s*"([^"\n]*)";', text))
    block = re.search(r"_standaloneEvidenceProbeProjects\s*=\s*\[(.*?)\];", text, re.S)
    seals = re.search(r"_sealedPackageObservations\s*=\s*\[(.*?)\];", text, re.S)
    if not block or not seals or "PublishedPackageObservation" not in constants:
        raise ValueError("Current Contracts exclusion/seal declarations could not be derived")
    exclusions = []
    for term in block.group(1).split(","):
        term = term.strip()
        if not term:
            continue
        match = re.fullmatch(r'(?:(\w+)\s*\+\s*)?"([^"\n]+)"', term)
        if not match or (match[1] and match[1] not in constants):
            raise ValueError("Unsupported exclusion declaration; do not infer an exemption")
        exclusions.append(constants.get(match[1], "") + match[2])
    prefix = constants["PublishedPackageObservation"]
    declared = re.findall(r'\("([^"\n]+)",\s*"([0-9a-f]{64})"\)', seals.group(1))
    if not declared or len(declared) != seals.group(1).count("("):
        raise ValueError("Current sealed-file declarations could not be derived exactly")
    checks = []
    sums_path = root / prefix / ".." / "SHA256SUMS"
    sums = sums_path.read_text().splitlines()
    for filename, expected in declared:
        path = root / prefix / filename
        canonical = path.read_bytes().decode("utf-8").replace("\r\n", "\n").encode("utf-8")
        actual = hashlib.sha256(canonical).hexdigest()
        if actual != expected:
            raise ValueError(f"Sealed archive digest mismatch: {prefix}{filename}")
        if f"{expected}  package-observation/{filename}" not in sums:
            raise ValueError(f"Sealed archive checksum entry is missing: {filename}")
        checks.append({"path": prefix + filename, "expectedCanonicalSha256": expected,
                       "actualCanonicalSha256": actual, "rawSha256": digest(path)})
    return {"sourcePath": str(source.relative_to(root)), "sourceSha256": digest(source),
            "excludedPaths": exclusions, "sealedFiles": checks,
            "checksumPath": str(sums_path.resolve().relative_to(root)),
            "checksumSha256": digest(sums_path),
            "canonicalization": "UTF-8 decode/encode with CRLF replaced by LF, matching VerifySealedPackageObservation"}


def main():
    evidence = Path(__file__).resolve().parent
    root = evidence.parents[3]
    preflight = inspect_configuration(root)
    preflight["completedAtUtc"] = datetime.now(timezone.utc).isoformat()
    preflight["command"] = ["python3", str(Path(__file__).relative_to(root))]
    preflight["exitCode"] = 0
    preflight["driverSha256"] = digest(Path(__file__))
    (evidence / "consumer-authority-sealed-preflight.json").write_text(json.dumps(preflight, indent=2) + "\n")
    log = "\n".join(f'PASS sealed {row["path"]}: {row["actualCanonicalSha256"]}'
                    for row in preflight["sealedFiles"])
    (evidence / "consumer-authority-sealed-preflight.log").write_text(log + "\n")
    catalog = root / "references/Hexalith.Builds/Props/Directory.Packages.props"
    validator = root / "references/Hexalith.Builds/Tools/validate-consumer-package-authority.ps1"
    before = {str(path.relative_to(root)): digest(path) for path in (catalog, validator)}
    command = ["pwsh", "-NoProfile", "-File", str(validator), "-RepositoryRoot", str(root),
               "-CatalogPath", str(catalog), "-ExcludedPath", ";".join(preflight["excludedPaths"])]
    env = {key: value for key, value in os.environ.items()
           if key not in ("GIT_DIR", "GIT_WORK_TREE", "GIT_COMMON_DIR")}
    run = subprocess.run(command, cwd=root, env=env, text=True, stdout=subprocess.PIPE,
                         stderr=subprocess.STDOUT, timeout=480)
    output = evidence / "consumer-authority-after-sealed-preflight.log"
    output.write_text(run.stdout)
    if inspect_configuration(root) != {key: value for key, value in preflight.items()
                                       if key not in ("completedAtUtc", "command", "exitCode", "driverSha256")}:
        raise ValueError("Contracts preflight inputs changed during validation")
    if any(digest(root / path) != value for path, value in before.items()):
        raise ValueError("Catalog/validator changed during validation")
    result = {"completedAtUtc": datetime.now(timezone.utc).isoformat(), "command": command,
              "exitCode": run.returncode, "preflight": "consumer-authority-sealed-preflight.json",
              "preflightSha256": digest(evidence / "consumer-authority-sealed-preflight.json"),
              "log": output.name, "logSha256": digest(output), "inputs": before,
              "excludedPathCount": len(preflight["excludedPaths"]),
              "note": "Current exact exclusions were derived from unchanged Contracts test source; its sealed-content and checksum-file checks passed before the unchanged PowerShell validator. Earlier invocation records remain time-bound history."}
    (evidence / "consumer-authority-after-sealed-preflight.json").write_text(json.dumps(result, indent=2) + "\n")
    print(f'Sealed preflight passed for {len(preflight["sealedFiles"])} files; validator exit {run.returncode}, {len(preflight["excludedPaths"])} exact exclusions.')
    raise SystemExit(run.returncode)


if __name__ == "__main__":
    main()

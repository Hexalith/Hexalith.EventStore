#!/usr/bin/env python3
"""Resolve only v2's JSON-path interpretation, reusing its explicit byte checks."""
import base64
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import sys

OUT = Path(__file__).resolve().parent
PACKET = OUT.parent
S2 = Path("/tmp/eventstore-folders-12-2-guardfix-20261022T171346Z")
S3 = Path("/tmp/eventstore-folders-12-2-guardtight-20261022T173000Z")
UNIT = Path("tests/Hexalith.EventStore.Server.Tests/bin/Debug/net10.0")
ERROR = "Build-context metadata differs beyond literal S2-to-S3 paths: MvcTestingAppManifest.json"


def sha(path):
    with Path(path).open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def main():
    target = OUT / "input-identity-audit-v3.json"
    if target.exists():
        raise SystemExit("Refusing to overwrite evidence: " + str(target))
    errors, fresh = [], []
    prior_path = OUT / "input-identity-audit.json"
    prior = json.loads(prior_path.read_text())
    result = {"passed": False, "utc_start": datetime.now(timezone.utc).isoformat(),
              "argv": [sys.executable, str(Path(__file__).resolve())], "auditor_sha256": sha(Path(__file__).resolve()),
              "reused_exhaustive_audit": {"path": str(prior_path), "sha256": sha(prior_path),
                                          "utc_start": prior["utc_start"], "utc_end": prior["utc_end"],
                                          "retained_passed": prior["passed"], "retained_errors": prior["errors"]},
              "fresh_metadata_checks": [], "fresh_reference_hashes": fresh, "errors": errors}
    if prior["errors"] != [ERROR] or prior["s1_fresh_changes"] or prior["s2_fresh_changes"] or prior["snapshot_inputs_changed_during_audit"]:
        errors.append("Prior exhaustive audit has an additional unresolved byte/source failure.")
    if not all(row["passed"] for row in prior["worker_module_identity"]) or prior["s3_qualified_worker_output_changes"] or prior["pin_conflicts"]:
        errors.append("Prior producer/module identity checks did not authenticate.")
    references = {str(PACKET / name): digest for name, digest in prior["source_manifest_sha256"].items()}
    references.update({str(OUT / name): sha(OUT / name) for name in ("source-inputs.json", "binary-freeze.json", "producer-current-inputs.json")})
    producer = json.loads((OUT / "producer-current-inputs.json").read_text())
    for archive in prior["archived_final_unit_runtime_and_source"]:
        references[str(PACKET / archive["archive"])] = archive["sha256"]
        references[archive["source"]] = archive["sha256"]
    for row in prior["worker_module_identity"]:
        for snapshot in (Path(prior["original_producer"]), S2, S3):
            base = snapshot / "tests/Hexalith.EventStore.Server.LiveSidecar.Tests/bin/qualified-Debug/net10.0"
            references[str(base / (row["module"] + ".dll"))] = row["pe_sha256"]
            references[str(base / (row["module"] + ".pdb"))] = row["pdb_sha256"]
    for path, expected in sorted(references.items()):
        actual = sha(path)
        fresh.append({"path": path, "expected_sha256": expected, "actual_sha256": actual, "passed": actual == expected})
        if actual != expected:
            errors.append("Referenced evidence/runtime changed: " + path)
    for row in prior["unit_output_comparison"]["path_context_changes"]:
        name = row["relative"]
        before, after = (S2 / UNIT / name).read_bytes(), (S3 / UNIT / name).read_bytes()
        if before != base64.b64decode(row["before_bytes_base64"]) or after != base64.b64decode(row["after_bytes_base64"]):
            errors.append("Retained metadata bytes changed: " + name)
        encoding = "utf-16-le" if before.startswith(b"\xff\xfe") else "utf-16-be" if before.startswith(b"\xfe\xff") else "utf-8"
        old_path, new_path = str(S2), str(S3)
        if name == "MvcTestingAppManifest.json":
            old_path, new_path = old_path.replace("/", "\\/"), new_path.replace("/", "\\/")
        same = before.replace(old_path.encode(encoding), new_path.encode(encoding)) == after
        if not same:
            errors.append("Metadata change exceeds its exact snapshot-path bytes: " + name)
        result["fresh_metadata_checks"].append({"relative": name, "before_sha256": sha(S2 / UNIT / name),
                                                 "after_sha256": sha(S3 / UNIT / name), "encoding": encoding,
                                                 "json_escaped_slashes": name == "MvcTestingAppManifest.json",
                                                 "exact_snapshot_path_translation": same})
    result["reused_exhaustive_scope"] = {"s1_paths": prior["s1_expected_paths"], "s2_paths": prior["s2_previous_pinned_paths"],
                                           "s3_guard_paths": prior["s3_tracked_guard_paths"], "s3_ordinary_paths": prior["s3_ordinary_source_paths"],
                                           "unit_output_paths": prior["unit_output_comparison"]["s3_count"],
                                           "worker_module_observations": sum(row["phase_observations"] for row in prior["worker_module_identity"]),
                                           "qualified_worker_paths": 788, "pdb_pairs": prior["pdb_manifest"]["s3_pairs"]}
    result["producer_current_inputs"] = {"path": str(OUT / "producer-current-inputs.json"), "sha256": sha(OUT / "producer-current-inputs.json"), "paths": len(producer)}
    result["strict_current_inputs_retained"] = prior["strict_current_inputs_retained"]
    result["separate_preservation_conflicts"] = prior["separate_preservation_conflicts"]
    result["reuse_scope"] = "The exhaustive S1/S2/S3 byte/source comparisons are reused from v2 with their exact receipt hash and retained failure. Fresh checks authenticate the listed evidence/runtime references and the four metadata byte transformations. The sole v2 JSON-escaping interpretation failure is resolved; every earlier failure remains retained."
    result["producer_reuse_justified"] = not errors
    result["passed"] = not errors
    result["scope_limit"] = "Fresh Unit execution and PDB/source verification remain separate gates; mutable origin, Folders adoption and whole-story completion are not qualified."
    result["utc_end"] = datetime.now(timezone.utc).isoformat()
    with target.open("x") as stream:
        json.dump(result, stream, indent=2)
        stream.write("\n")
    print(json.dumps({"passed": result["passed"], "fresh_references": len(fresh), "metadata_checks": len(result["fresh_metadata_checks"]), "errors": errors}))
    return 0 if result["passed"] else 1


if __name__ == "__main__":
    raise SystemExit(main())

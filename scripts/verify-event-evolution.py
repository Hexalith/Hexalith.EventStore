#!/usr/bin/env python3
"""Check the current Dapr-only preparation boundary, without granting activation.

The approved 6.5 verifier remains a separate historical-design check. This gate
checks application dependency and mutation ownership at the current source, and
tests each negative policy input in a fresh process with an explicit timeout.
Functional, compatibility and live-component evidence remain separate gates.
The obligation audit binds the historical approved inputs separately from both
current amendments. It checks traceability and open gates, not qualification or
completion of the twenty implementation obligations.
The source denylist recognizes selected database symbols, including factory
creation; it is not complete semantic/static analysis or a runtime inventory.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import pathlib
import re
import subprocess
import sys
import xml.etree.ElementTree as ET


ROOT = pathlib.Path(__file__).resolve().parents[1]
SERVER = pathlib.Path("src/Hexalith.EventStore.Server")
SERVER_PROJECT = SERVER / "Hexalith.EventStore.Server.csproj"
BUILD_CATALOG = pathlib.Path("references/Hexalith.Builds/Props/Directory.Packages.props")
PERSISTER = SERVER / "Events/EventPersister.cs"
LOGICAL_READER = SERVER / "Events/DaprLogicalEventReader.cs"
AMENDMENT = pathlib.Path("_bmad-output/implementation-artifacts/story-6-6-dapr-only-amendment.md")
TRUST_AMENDMENT = pathlib.Path("_bmad-output/implementation-artifacts/story-6-6-trusted-code-amendment.md")
HISTORICAL_SPEC = pathlib.Path("_bmad-output/implementation-artifacts/spec-event-versioning-upcasting.md")
OBLIGATION_AUDIT = pathlib.Path("_bmad-output/implementation-artifacts/6-6-obligation-audit.json")
HISTORICAL_APPROVAL_DIGEST = "bc1625e3b8147fb0bc2cd9491fad8379a95c1ce0b597eba70f8d29f2fee3b050"
APPROVAL_MARKER = "<!-- APPROVAL RECEIPT: mutable fields below -->\n"
MUTATIONS = {
    "application-sql": "application-storage-boundary",
    "application-sql-factory": "application-storage-boundary",
    "server-npgsql": "server-dependencies",
    "catalog-npgsql": "story-dependency-footprint",
    "persister-save": "actor-save-ownership",
    "v2-admission": "dormant-v2-writer",
    "v2-comment-spoof": "dormant-v2-writer",
    "v2-block-comment-spoof": "dormant-v2-writer",
    "v2-string-spoof": "dormant-v2-writer",
    "v2-disabled-branch": "dormant-v2-writer",
    "v2-preprocessor-spoof": "dormant-v2-writer",
    "historical-approval-pin": "historical-approval-binding",
    "historical-reviewed-input": "historical-approval-binding",
    "missing-dapr-amendment": "current-amendment-binding",
    "changed-trusted-code-amendment": "current-amendment-binding",
    "missing-obligation": "current-obligation-accounting",
    "duplicate-obligation": "current-obligation-accounting",
    "changed-obligation-source": "current-obligation-binding",
    "superseded-provider-assurance": "current-obligation-disposition",
    "unavailable-proof-enabled": "current-obligation-disposition",
    "premature-obligation-closure": "unqualified-activation-fence",
    "historical-approval-as-current": "unqualified-activation-fence",
}


class BoundaryFailure(Exception):
    """One current-boundary policy violation, carrying its owning check."""

    def __init__(self, check: str, detail: str):
        super().__init__(detail)
        self.check = check


def read(path: pathlib.Path, mutation: str | None) -> str:
    """Load real source; mutation probes alter only a process-private string."""
    value = (ROOT / path).read_bytes().decode("utf-8")
    if path == HISTORICAL_SPEC and mutation == "historical-reviewed-input":
        value = value.replace("### 11.7", "### changed-11.7", 1)
    elif path == AMENDMENT and mutation == "missing-dapr-amendment":
        raise BoundaryFailure("current-amendment-binding", f"{path}: required reviewed amendment is missing")
    elif path == TRUST_AMENDMENT and mutation == "changed-trusted-code-amendment":
        value += "\nUnreviewed loader assurance.\n"
    elif path == OBLIGATION_AUDIT and mutation in MUTATIONS:
        audit = json.loads(value)
        if mutation == "historical-approval-pin":
            audit["historicalApproval"]["normativeSha256"] = "0" * 64
        elif mutation == "missing-obligation":
            audit["obligations"].pop()
        elif mutation == "duplicate-obligation":
            audit["obligations"][-1] = audit["obligations"][0]
        elif mutation == "changed-obligation-source":
            audit["obligations"][0]["historicalRowSha256"] = "0" * 64
        elif mutation == "superseded-provider-assurance":
            audit["obligations"][17]["evidenceBoundary"] = "provider-attestation"
        elif mutation == "unavailable-proof-enabled":
            audit["unavailableProofOperations"] = "enabled"
        elif mutation == "premature-obligation-closure":
            audit["obligations"][0]["status"] = "complete"
        elif mutation == "historical-approval-as-current":
            audit["activationAuthority"] = True
        value = json.dumps(audit)
    elif mutation == "application-sql" and path == LOGICAL_READER:
        value += "\nusing Npgsql;\n"
    elif mutation == "application-sql-factory" and path == LOGICAL_READER:
        factory_probe = (
            '\n    private static void UnmanifestedFactoryProbe()\n    {\n'
            '        var factory = System.Data.Common.DbProviderFactories.GetFactory("owned-probe-provider");\n'
            '        using var connection = factory.CreateConnection();\n'
            '        connection!.Open();\n    }\n'
        )
        closing = value.rindex("}")
        value = value[:closing] + factory_probe + value[closing:]
    elif mutation == "server-npgsql" and path == SERVER_PROJECT:
        value = value.replace("</Project>", '<ItemGroup><PackageReference Include="Npgsql" /></ItemGroup></Project>')
    elif mutation == "catalog-npgsql" and path == BUILD_CATALOG:
        value = value.replace("</Project>", '<ItemGroup><PackageVersion Include="Npgsql" Version="10.0.3" /></ItemGroup></Project>')
    elif mutation == "persister-save" and path == PERSISTER:
        value += "\nawait stateManager.SaveStateAsync(cancellationToken);\n"
    elif mutation == "v2-admission" and path == PERSISTER:
        value = value.replace("if (metadataVersion == 2)", "if (metadataVersion == 3)")
    elif mutation in {"v2-comment-spoof", "v2-block-comment-spoof", "v2-string-spoof"} and path == PERSISTER:
        value = value.replace("if (metadataVersion == 2)", "if (metadataVersion == 3)")
        fake = 'if (metadataVersion == 2) { throw new InvalidOperationException('
        if mutation == "v2-comment-spoof":
            spoof = "// " + fake + "\n"
        elif mutation == "v2-block-comment-spoof":
            spoof = "/* " + fake + " */\n"
        else:
            spoof = 'string fakeFence = "' + fake + '";\n'
        value = spoof + value
    elif mutation == "v2-disabled-branch" and path == PERSISTER:
        value = value.replace("if (metadataVersion == 2)", "if (false && metadataVersion == 2)")
    elif mutation == "v2-preprocessor-spoof" and path == PERSISTER:
        value = value.replace("if (metadataVersion == 2)", "#if false\nif (metadataVersion == 2)")
        value = value.replace("validatedPayloads.Add(", "#endif\nvalidatedPayloads.Add(", 1)
    return value


def executable_csharp(value: str) -> str:
    """Blank comments and string/character literals so documentation cannot satisfy a code fence.

    This is a conservative source-policy scan, not a C# semantic verifier. The
    fence check also requires its adjacent admission calls and refuses conditional
    compilation; executed V2 refusal tests remain the runtime evidence.
    """
    noncode = re.compile(
        r'//[^\r\n]*|/\*[\s\S]*?\*/|(?:\$?@|@\$)"(?:[^"]|"")*"'
        r'|\$?"(?:\\.|[^"\\])*"|\'(?:\\.|[^\'\\])*\''
    )
    return noncode.sub(lambda match: re.sub(r"[^\r\n]", " ", match.group()), value)


def require_no_dependency(path: pathlib.Path, element_name: str, check: str, mutation: str | None) -> None:
    """Reject the withdrawn direct database package from its two owning inventories."""
    tree = ET.fromstring(read(path, mutation))
    for element in tree.iter(element_name):
        if element.get("Include", "").casefold() == "npgsql":
            raise BoundaryFailure(check, f"{path}: withdrawn Npgsql dependency remains")


def verify_obligation_audit(mutation: str | None) -> dict:
    """Bind exactly the reviewed inputs and keep every unqualified obligation open.

    No Git ancestry, gitlink, whole-tree inventory, production receipt or physical
    database observation is used. This schema deliberately cannot close an O-row:
    qualified implementation evidence needs its own reviewed closure gate.
    """
    audit = json.loads(read(OBLIGATION_AUDIT, mutation))
    if (not isinstance(audit, dict) or type(audit.get("schemaVersion")) is not int
            or audit.get("schemaVersion") != 1
            or audit.get("storyKey") != "6-6-event-versioning-and-upcasting-implementation"):
        raise BoundaryFailure("current-obligation-accounting", "obligation audit has an unsupported identity/schema")

    historical = read(HISTORICAL_SPEC, mutation)
    approval = audit.get("historicalApproval", {})
    if (not isinstance(approval, dict) or historical.count(APPROVAL_MARKER) != 1
            or approval.get("path") != HISTORICAL_SPEC.as_posix()
            or approval.get("normativeSha256") != HISTORICAL_APPROVAL_DIGEST
            or approval.get("authority") != "historical-design-only"):
        raise BoundaryFailure("historical-approval-binding", "historical approval identity differs from the reviewed input")
    normative, receipt = historical.split(APPROVAL_MARKER)
    if (hashlib.sha256(normative.encode("utf-8")).hexdigest() != HISTORICAL_APPROVAL_DIGEST
            or re.findall(r"^ApprovalDigest: ([0-9a-f]{64})$", receipt, re.MULTILINE) != [HISTORICAL_APPROVAL_DIGEST]):
        raise BoundaryFailure("historical-approval-binding", "historical reviewed bytes or approval digest changed")

    amendments = audit.get("currentAmendments", [])
    expected_paths = [AMENDMENT.as_posix(), TRUST_AMENDMENT.as_posix()]
    if (not isinstance(amendments, list) or not all(isinstance(item, dict) for item in amendments)
            or [item.get("path") for item in amendments] != expected_paths):
        raise BoundaryFailure("current-amendment-binding", "both current amendments must be bound exactly once")
    for item, path in zip(amendments, [AMENDMENT, TRUST_AMENDMENT], strict=True):
        try:
            actual = hashlib.sha256(read(path, mutation).encode("utf-8")).hexdigest()
        except OSError as error:
            raise BoundaryFailure("current-amendment-binding", f"{path}: required reviewed amendment is missing") from error
        if item.get("sha256") != actual:
            raise BoundaryFailure("current-amendment-binding", f"{path}: reviewed amendment bytes changed")

    section = normative.split("### 11.7 ", 1)[1].split("## 12.", 1)[0]
    historical_rows = re.findall(r"^\| (O-\d{2}) \| (.*)$", section, re.MULTILINE)
    expected_ids = [f"O-{index:02}" for index in range(1, 21)]
    rows = audit.get("obligations", [])
    if (not isinstance(rows, list) or not all(isinstance(row, dict) for row in rows)
            or [row[0] for row in historical_rows] != expected_ids
            or [row.get("id") for row in rows] != expected_ids):
        raise BoundaryFailure("current-obligation-accounting", "historical and current audits must contain ordered O-01 through O-20 exactly once")

    if (audit.get("activationAuthority") is not False
            or audit.get("productionQualification") != "not-established"
            or any(row.get("status") != "open" for row in rows)):
        raise BoundaryFailure("unqualified-activation-fence", "traceability and local checks cannot close obligations or grant activation")
    if audit.get("unavailableProofOperations") != "fenced":
        raise BoundaryFailure("current-obligation-disposition", "operations requiring unavailable proof must remain fenced")

    replacements = {"O-10", "O-13", "O-14", "O-17", "O-18", "O-20"}
    for row, (identifier, original) in zip(rows, historical_rows, strict=True):
        source_hash = hashlib.sha256(("| " + identifier + " | " + original + "\n").encode("utf-8")).hexdigest()
        if row.get("historicalRowSha256") != source_hash:
            raise BoundaryFailure("current-obligation-binding", f"{identifier}: historical obligation row changed")
        disposition = "replaced-provider-requirements" if identifier in replacements else "retained-compatible-requirements"
        boundary = "dapr-logical-and-qualified-capabilities" if identifier in replacements else "compatible-contract-and-separate-qualification"
        if (row.get("disposition") != disposition or row.get("evidenceBoundary") != boundary
                or row.get("owner") != "Story 6.6 implementation and verification owner"
                or row.get("blockingGate") != "preactivation-and-applicable-ad26-qualification"
                or not isinstance(row.get("currentRequirement"), str) or not row["currentRequirement"].strip()
                or not isinstance(row.get("requiredEvidence"), list) or not row["requiredEvidence"]
                or not all(isinstance(value, str) and value.strip() for value in row["requiredEvidence"])):
            raise BoundaryFailure("current-obligation-disposition", f"{identifier}: missing current requirement, owner, gate or compatible evidence boundary")

    return {
        "scope": "traceability and open-gate accounting; no qualification or closure authority",
        "historical_approval_sha256": HISTORICAL_APPROVAL_DIGEST,
        "audit_sha256": hashlib.sha256(read(OBLIGATION_AUDIT, mutation).encode("utf-8")).hexdigest(),
        "current_amendment_sha256": {item["path"]: item["sha256"] for item in amendments},
        "open_obligations": expected_ids,
        "replaced_provider_requirements": sorted(replacements),
        "activation_authority": False,
    }


def verify(mutation: str | None = None) -> dict:
    """Validate only current local policy, leaving unproven obligations open."""
    require_no_dependency(SERVER_PROJECT, "PackageReference", "server-dependencies", mutation)
    require_no_dependency(BUILD_CATALOG, "PackageVersion", "story-dependency-footprint", mutation)
    # A lexical symbol denylist, including inferred factory-created connections.
    # It deliberately claims neither a C# call graph nor complete database inventory;
    # reflection, generated/dynamic code and unlisted APIs require separate analysis.
    forbidden = re.compile(
        r"\b(?:using\s+Npgsql|NpgsqlConnection|NpgsqlDataSource|SqlConnection|DbConnection|DbProviderFactories)\b"
    )
    source_count = 0
    for path in sorted((ROOT / SERVER).rglob("*.cs")):
        if "obj" in path.parts or "bin" in path.parts:
            continue
        relative = path.relative_to(ROOT)
        source_count += 1
        if forbidden.search(read(relative, mutation)):
            raise BoundaryFailure("application-storage-boundary", f"{relative}: application database access is forbidden")

    persister = executable_csharp(read(PERSISTER, mutation))
    if re.search(r"\bSaveStateAsync\s*\(", persister):
        raise BoundaryFailure("actor-save-ownership", "EventPersister must stage state; AggregateActor owns saving")
    fence = re.search(
        r"ValidateEventVersionMetadata\(eventTypeName,\s*metadataVersion,\s*eventContractType,\s*payloadVersion\);"
        r"\s*if\s*\(metadataVersion\s*==\s*2\)\s*\{\s*throw\s+new\s+InvalidOperationException\([^;{}]*\);"
        r"\s*\}\s*validatedPayloads\.Add\(", persister
    )
    first_read = persister.index(".TryGetStateAsync<AggregateMetadata>")
    if fence is None or fence.start() > first_read or re.search(r"^\s*#", persister, re.MULTILINE):
        raise BoundaryFailure("dormant-v2-writer", "V2 admission must refuse before actor metadata read and mutation")

    obligation_audit = verify_obligation_audit(mutation)
    amendment = (ROOT / AMENDMENT).read_bytes()
    return {
        "result": "passed",
        "scope": "current Dapr-only source boundary; no activation or completion authority",
        "amendment_sha256": hashlib.sha256(amendment).hexdigest(),
        "server_source_files": source_count,
        "checks": sorted(set(MUTATIONS.values())),
        "v2_writes": "fenced",
        "production_qualification": "not established by this preflight",
        "source_analysis_limits": "selected lexical symbols in Server C# source; no complete semantic/static or runtime inventory",
        "obligation_audit": obligation_audit,
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--mutations", action="store_true", help="run each policy mutation with a 10 second timeout")
    parser.add_argument("--mutation", choices=sorted(MUTATIONS), help=argparse.SUPPRESS)
    args = parser.parse_args()
    try:
        result = verify(args.mutation)
        if args.mutations:
            outcomes = []
            for mutation, expected_check in MUTATIONS.items():
                try:
                    child = subprocess.run(
                        [sys.executable, str(pathlib.Path(__file__).resolve()), "--mutation", mutation],
                        capture_output=True, text=True, timeout=10, check=False,
                    )
                except subprocess.TimeoutExpired as error:
                    raise BoundaryFailure("mutation-timeout", f"{mutation}: exceeded 10 seconds") from error
                observed = json.loads(child.stdout)
                if child.returncode != 2 or observed.get("check") != expected_check:
                    raise BoundaryFailure("mutation-rejection", f"{mutation}: missing rejection from {expected_check}")
                outcomes.append({"mutation": mutation, "check": expected_check, "result": "rejected", "timeout_seconds": 10})
            result["mutations"] = outcomes
        print(json.dumps(result, sort_keys=True))
        return 0
    except BoundaryFailure as error:
        print(json.dumps({"result": "failed", "check": error.check, "detail": str(error)}, sort_keys=True))
        return 2
    except (OSError, ET.ParseError, ValueError, KeyError) as error:
        print(json.dumps({"result": "failed", "check": "preflight-input", "detail": str(error)}, sort_keys=True))
        return 2


if __name__ == "__main__":
    raise SystemExit(main())

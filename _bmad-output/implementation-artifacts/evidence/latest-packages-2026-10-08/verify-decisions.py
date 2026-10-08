"""Verify the spec matrix against the current catalog and retained run evidence."""

import hashlib
import json
import subprocess
import tempfile
from pathlib import Path


def main():
    evidence = Path(__file__).resolve().parent
    root = evidence.parents[3]
    catalog = root / "references/Hexalith.Builds/Props/Directory.Packages.props"
    decisions = json.loads((evidence / "decisions.json").read_text())
    discovery_path = evidence / "discovery-audit.json"
    discovery = json.loads(discovery_path.read_text())
    assert hashlib.sha256(discovery_path.read_bytes()).hexdigest() == decisions["discoveryAuditSha256"]
    assert hashlib.sha256(catalog.read_bytes()).hexdigest() == decisions["catalogRawSha256"]
    assert catalog.read_bytes().startswith(b"\xef\xbb\xbf")
    assert catalog.read_bytes().count(b"\n") == catalog.read_bytes().count(b"\r\n")
    evaluated = json.loads(subprocess.check_output(
        ["dotnet", "msbuild", "Directory.Packages.props", "-getItem:PackageVersion"],
        cwd=root, text=True))
    versions = {row["Identity"]: row["Version"] for row in evaluated["Items"]["PackageVersion"]}
    rows = {row["id"]: row for row in decisions["packages"]}
    old_rows = {row["id"]: row for row in discovery["packages"]}
    assert len(rows) == len(versions) == 304
    assert len({row["family"] for row in rows.values()}) == 146
    assert all(row["selectedVersion"] == versions[name] for name, row in rows.items())
    assert all(row["family"] and row["evidence"] and row["removalTrigger"] for row in rows.values())
    assert all("latestStable" in row and "latestPrerelease" in row for row in rows.values())

    accepted = [row for row in rows.values() if row["disposition"] == "accepted-uncommitted"]
    assert len(accepted) == 21
    consumer = json.loads((evidence / "accepted-consumer-final-inputs.json").read_text())
    for row in accepted:
        assert row["selectedVersion"] in (row["latestStable"], row["latestPrerelease"])
        assert consumer["acceptedPackageVersions"][row["id"]] == row["selectedVersion"]
    for package in ("Verify", "Verify.XunitV3"):
        assert versions[package] == "33.3.1"
    assert "SC021" in (evidence / "isolated-consumer-build.log").read_text()
    assert "SC021" in (evidence / "retained-verify-consumer-build.log").read_text()
    assert "Total: 20, Errors: 0, Failed: 0, Skipped: 0" in (evidence / "accepted-consumer-tests.log").read_text()

    aspire = [row for row in rows.values() if row["family"] == "aspire"]
    assert len(aspire) == 13
    for row in aspire:
        expected = "13.6.1-preview.1.26506.6" if row["id"] in (
            "Aspire.Hosting.Keycloak", "Aspire.Hosting.Kubernetes") else "13.6.1"
        assert row["selectedVersion"] == expected
    apphost = root / "src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj"
    assert 'Sdk="Aspire.AppHost.Sdk/13.6.1"' in apphost.read_text()
    runtime = json.loads((evidence / "final-runtime-results.json").read_text())
    assert runtime["requiredServicesRunningHealthy"] and runtime["inputsStableDuringRun"]
    assert runtime["allModeledResourcesRunningHealthy"] and runtime["resourceCount"] == 30
    assert not any(runtime["binaryFailureCounts"].values())
    assert "stopped successfully" in (evidence / "final-runtime-stop.log").read_text()

    assert versions["CommunityToolkit.Aspire.Hosting.Dapr"] == "13.6.0-preview.1.261001-0243"
    with tempfile.TemporaryDirectory(prefix="eventstore-folders-catalog-") as folder:
        folders_project = Path(folder) / "Hexalith.Folders.Aspire.proj"
        folders_project.write_text(f'<Project><Import Project="{catalog}" /></Project>\n')
        folders = json.loads(subprocess.check_output(
            ["dotnet", "msbuild", str(folders_project), "-getItem:PackageVersion"],
            cwd=root, text=True))
    folders_versions = {row["Identity"]: row["Version"] for row in folders["Items"]["PackageVersion"]}
    assert folders_versions["CommunityToolkit.Aspire.Hosting.Dapr"] == "13.0.0"

    parties = [row for row in rows.values() if row["family"] == "hexalith-parties"]
    assert len(parties) == 9
    assert all(row["selectedVersion"] == "1.1.1" and row["disposition"] == "retained" for row in parties)
    assert all(old_rows[name]["listingState"] == "unresolved" for name in (
        "Hexalith.Parties.Server", "Hexalith.Parties.ServiceDefaults", "Hexalith.Parties.UI"))

    assert versions["Microsoft.OpenApi"] == "2.12.2"
    floors = json.loads((evidence / "dependency-floors.json").read_text())
    openapi = next(row for row in floors if row["package"] == "microsoft.aspnetcore.openapi")
    assert any(row["id"] == "Microsoft.OpenApi" and row["version"] == "[2.12.0, 3.0.0)" for row in openapi["dependencies"])

    pending = json.loads((evidence / "audit-regeneration-pending.json").read_text())
    audit_path = root / "references/Hexalith.Builds/Tools/package-version-audit.json"
    audit = json.loads(audit_path.read_text())
    finalization = json.loads((evidence / "authoritative-audit-finalization.json").read_text())
    prior_path = evidence / "authoritative-audit-before-finalization.json.txt"
    prior = json.loads(prior_path.read_text())
    assert pending["exitCode"] == 1 and pending["unchanged"]
    assert hashlib.sha256(prior_path.read_bytes()).hexdigest() == pending["authoritativeAuditSha256"]
    assert "is dirty relative to generated-from revision" in (evidence / "audit-regeneration-pending.log").read_text()
    assert finalization["status"] == "finalized-locally" and finalization["exitCode"] == 0
    assert hashlib.sha256(audit_path.read_bytes()).hexdigest() == finalization["authoritativeAuditSha256"]
    assert audit["generatedFromRevision"] == finalization["generatedFromRevision"]
    assert audit["catalogSha256"] == hashlib.sha256(catalog.read_bytes()).hexdigest()
    committed_catalog = subprocess.check_output(
        ["git", "show", f'{audit["generatedFromRevision"]}:Props/Directory.Packages.props'],
        cwd=root / "references/Hexalith.Builds")
    assert hashlib.sha256(committed_catalog).hexdigest() == audit["catalogRawSha256"]
    audit_rows = {row["id"]: row for row in audit["packages"]}
    audit_families = {row["family"]: row for row in audit["familyDecisions"]}
    assert len(audit_rows) == 304 and len(audit_families) == 146
    assert all(row["selectedVersion"] == versions[name] for name, row in audit_rows.items())
    for group, key in (("packages", "id"), ("familyDecisions", "family")):
        current = {row[key]: row for row in audit[group]}
        for row in prior[group]:
            assert all(history in current[row[key]]["historicalContext"]
                       for history in row.get("historicalContext", []))
    assert finalization["priorHistoryPreserved"]
    for family in finalization["authoritativeAcceptedFamilies"]:
        assert audit_families[family]["disposition"] == "accepted"
        assert audit_families[family]["representativeConsumers"]
    for family in finalization["authoritativeRetainedClassificationLimitations"]:
        assert audit_families[family]["disposition"] == "retained"
        assert not audit_families[family]["representativeConsumers"]
        assert "owning-Builds" in audit_families[family]["rationale"]
    print("PASS: Candidate, Aspire, Preview, Incomplete family, Compatibility, and Provenance matrix checks; 304 decisions match the current catalog.")


if __name__ == "__main__":
    main()

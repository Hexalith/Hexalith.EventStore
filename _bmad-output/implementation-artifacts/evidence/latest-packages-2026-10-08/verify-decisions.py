"""Verify the spec matrix against the current catalog and retained run evidence."""
import hashlib
import json
import argparse
import subprocess
import sys
import tempfile
import xml.etree.ElementTree as ET
import zipfile
from pathlib import Path
from consumer_authority import inspect_configuration


class EvidenceError(ValueError):
    pass


def require(condition, message):
    if not condition:
        raise EvidenceError(message)


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def bound_file(evidence, name, expected):
    path = evidence / name
    require(path.is_file(), f"Missing retained evidence: {name}")
    require(digest(path) == expected, f"Retained evidence digest mismatch: {name}")
    return path


def verify_runtime(root, evidence):
    runtime = json.loads((evidence / "review-runtime-results.json").read_text())
    expected_services = ["eventstore", "eventstore-admin", "eventstore-admin-ui", "sample",
                         "sample-api", "sample-blazor-ui", "tenants-api", "security"]
    require(runtime["requiredServices"] == expected_services, "Required service names changed")
    resources = json.loads(bound_file(evidence, runtime["resourcesPath"], runtime["resourcesSha256"]).read_text())
    require(isinstance(resources, list) and len(resources) == 30, "Actual resource list must contain 30 resources")
    require(len({row["displayName"] for row in resources}) == len(resources), "Duplicate actual resource names")
    for name in expected_services:
        matches = [row for row in resources if row["displayName"] == name]
        require(len(matches) == 1, f"Required service absent or duplicated: {name}")
        require(matches[0]["state"] == "Running" and matches[0]["healthStatus"] == "Healthy",
                f"Actual required service is unhealthy: {name}")
    require(all(row["state"] == "Running" and row["healthStatus"] == "Healthy" for row in resources),
            "Actual modeled resource is unhealthy")
    before = runtime["beforeInputs"]["sha256"]
    after = runtime["afterInputs"]["sha256"]
    required_inputs = set(json.loads((evidence / "final-runtime-results.json").read_text())["afterInputs"]["sha256"])
    require(set(before) == set(after) == required_inputs, "Runtime source input inventory changed")
    for name, expected in before.items():
        require(after[name] == expected == digest(root / name), f"Runtime source drift: {name}")
    console = bound_file(evidence, runtime["consolePath"], runtime["consoleSha256"]).read_text()
    failures = ["TypeLoadException", "MissingMethodException", "MissingFieldException", "FileLoadException"]
    actual_counts = {name: console.count(name) for name in failures}
    require(actual_counts == runtime["binaryFailureCounts"], "Binary failure summary does not match retained console")
    require(not any(actual_counts.values()), "Retained runtime console contains a binary failure")
    commands = runtime["commands"]
    require(len(commands) == 12 and all(row["exitCode"] == 0 for row in commands), "Runtime command sequence did not pass")
    require(commands[0]["command"][1:3] == ["start", "--isolated"], "Runtime startup was not explicitly isolated")
    require([row["command"][2] for row in commands[1:9]] == expected_services,
            "Runtime waits do not cover the exact required services")
    require([row["command"][1] for row in commands[9:]] == ["describe", "logs", "stop"], "Runtime describe/logs/stop sequence changed")
    for row in commands:
        require(row["command"][-3:] == ["--apphost", "src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj", "--non-interactive"],
                "Runtime command targets a different AppHost")
        output = bound_file(evidence, row["log"], row["sha256"]).read_text()
        if row["command"][1] == "stop":
            require("stopped successfully" in output, "Runtime stop is not confirmed")
    description = bound_file(evidence, commands[9]["log"], commands[9]["sha256"]).read_text()
    decoder = json.JSONDecoder()
    described = None
    for offset, character in enumerate(description):
        if character != "{":
            continue
        try:
            candidate, _ = decoder.raw_decode(description[offset:])
            if isinstance(candidate, dict) and isinstance(candidate.get("resources"), list):
                described = candidate["resources"]
                break
        except json.JSONDecodeError:
            continue
    require(described is not None, "Retained Aspire describe output has no actual resource list")
    fields = ("name", "displayName", "resourceType", "state", "healthStatus")
    require([{key: row.get(key) for key in fields} for row in described] == resources,
            "Actual describe output differs from retained resource states")


def verify_consumer(root, evidence, versions):
    capture = json.loads((evidence / "consumer-resolved-capture.json").read_text())
    require(capture["exitCode"] == 0 and capture["testCount"] == 20, "Resolved consumer rerun did not pass 20 cases")
    output = bound_file(evidence, capture["testLog"], capture["testLogSha256"]).read_text()
    require("Total: 20, Errors: 0, Failed: 0, Skipped: 0" in output, "Resolved consumer test log does not confirm 20 passing cases")
    require((evidence / capture["probeDllDigestPath"]).read_text() == capture["probeDllSha256"] + "  Probe.dll\n",
            "Passing Probe.dll digest binding changed")
    require(capture["catalogRawSha256"] == digest(root / "references/Hexalith.Builds/Props/Directory.Packages.props"), "Resolved consumer catalog drift")
    require(capture["globalJsonSha256"] == digest(root / "global.json"), "Resolved consumer SDK selection drift")
    for name, expected in capture["sourceFiles"].items():
        bound_file(evidence, name, expected)
    inputs = json.loads((evidence / "accepted-consumer-final-inputs.json").read_text())
    require(capture["acceptedPackageVersions"] == inputs["acceptedPackageVersions"], "Resolved consumer candidate inventory changed")
    for name, text in inputs["files"].items():
        require((evidence / "isolated-consumer-source" / f"{name}.txt").read_text() == text, f"Retained consumer source changed: {name}")
    project = ET.parse(evidence / "isolated-consumer-source/Probe.csproj.txt").getroot()
    wrapper = ET.parse(evidence / "isolated-consumer-source/Directory.Packages.props.txt").getroot()
    require(not list(wrapper.iter("PackageVersion")), "Consumer wrapper declares local versions")
    imports = [row.attrib["Project"] for row in wrapper.iter("Import")]
    owned_catalog = "references/Hexalith.Builds/Props/Directory.Packages.props"
    require(imports == [capture["catalogCapturePath"]], "Consumer import differs from its recorded capture origin")
    require(capture["catalogRepositoryRelativePath"] == owned_catalog and imports[0].replace("\\", "/").endswith("/" + owned_catalog),
            "Captured consumer import does not identify the owned Builds catalog")
    require(json.loads((evidence / "isolated-consumer-source/global.json.txt").read_text()) == json.loads((root / "global.json").read_text()), "Consumer source SDK selection changed")
    archive = capture["resolvedArchive"]
    expected_members = {"project.assets.json", "Probe.deps.json"}
    require(set(archive["entries"]) == expected_members, "Resolved manifest entry inventory changed")
    archive_path = bound_file(evidence, archive["path"], archive["sha256"])
    retained = {}
    with zipfile.ZipFile(archive_path) as stored:
        members = stored.namelist()
        require(len(members) == 2 and set(members) == expected_members,
                "Resolved archive must contain exactly the two actual manifests")
        for name in members:
            raw = stored.read(name)
            require(hashlib.sha256(raw).hexdigest() == archive["entries"][name],
                    f"Uncompressed actual manifest digest mismatch: {name}")
            retained[name] = json.loads(raw)
    assets = retained["project.assets.json"]
    deps = retained["Probe.deps.json"]
    resolved = {name.split("/")[0].lower(): name.split("/")[1] for name, row in assets["libraries"].items() if row["type"] == "package"}
    references = list(project.iter("PackageReference"))
    direct = {row.attrib["Include"]: resolved[row.attrib["Include"].lower()] for row in references}
    require(direct == capture["directResolvedPackageVersions"], "Actual resolved direct packages differ from the capture")
    for row in references:
        name = row.attrib["Include"]
        require(not any(key in row.attrib or any(child.tag == key for child in row) for key in ("Version", "VersionOverride")), f"Local consumer version metadata: {name}")
        require(direct[name] == versions[name], f"Actual resolved consumer pin differs from current catalog: {name}")
        dependency = assets["project"]["frameworks"]["net10.0"]["dependencies"][name]
        require(dependency.get("versionCentrallyManaged") and dependency["version"] == f"[{versions[name]}, )", f"Actual restore input does not use the central pin: {name}")
    for name, expected in capture["acceptedPackageVersions"].items():
        require(direct[name] == expected, f"Resolved candidate changed: {name}")
    for name, row in deps["libraries"].items():
        if row["type"] == "package":
            require(name in assets["libraries"] and assets["libraries"][name]["type"] == "package", f"Runtime dependency absent from restored assets: {name}")
    runtime_target = deps["targets"][deps["runtimeTarget"]["name"]]
    require(runtime_target["Probe/1.0.0"]["runtime"] == {"Probe.dll": {}}, "Runtime manifest does not bind Probe.dll")
    for name, version in runtime_target["Probe/1.0.0"]["dependencies"].items():
        require(resolved[name.lower()] == version, f"Runtime dependency version differs from assets: {name}")


def verify_authority(root, evidence):
    record = json.loads((evidence / "consumer-authority-after-sealed-preflight.json").read_text())
    require(record["exitCode"] == 0, "Configured consumer authority did not pass")
    preflight = json.loads(bound_file(evidence, record["preflight"], record["preflightSha256"]).read_text())
    current = inspect_configuration(root)
    require(all(preflight[key] == value for key, value in current.items()), "Current Contracts exclusions/seals differ from preflight")
    require(preflight["exitCode"] == 0 and preflight["completedAtUtc"] <= record["completedAtUtc"], "Sealed preflight did not precede validation")
    require(record["excludedPathCount"] == len(current["excludedPaths"]), "Authority exclusion count differs from source")
    require(record["command"][-2:] == ["-ExcludedPath", ";".join(current["excludedPaths"])], "Validator did not use exact current source exclusions")
    for name, expected in record["inputs"].items():
        require(digest(root / name) == expected, f"Authority input drift: {name}")
    bound_file(evidence, record["log"], record["logSha256"])

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--evidence-dir", type=Path, help="Read an alternate packet for isolated negative checks")
    args = parser.parse_args()
    source_evidence = Path(__file__).resolve().parent
    root = source_evidence.parents[3]
    evidence = args.evidence_dir or source_evidence
    catalog = root / 'references/Hexalith.Builds/Props/Directory.Packages.props'
    decisions = json.loads((evidence / 'decisions.json').read_text())
    discovery_path = evidence / 'discovery-audit.json'
    discovery = json.loads(discovery_path.read_text())
    require(hashlib.sha256(discovery_path.read_bytes()).hexdigest() == decisions['discoveryAuditSha256'], "hashlib.sha256(discovery_path.read_bytes()).hexdigest() == decisions['discoveryAuditSha256']")
    require(hashlib.sha256(catalog.read_bytes()).hexdigest() == decisions['catalogRawSha256'], "hashlib.sha256(catalog.read_bytes()).hexdigest() == decisions['catalogRawSha256']")
    require(catalog.read_bytes().startswith(b'\xef\xbb\xbf'), "catalog.read_bytes().startswith(b'\\xef\\xbb\\xbf')")
    require(catalog.read_bytes().count(b'\n') == catalog.read_bytes().count(b'\r\n'), "catalog.read_bytes().count(b'\\n') == catalog.read_bytes().count(b'\\r\\n')")
    evaluated = json.loads(subprocess.check_output(['dotnet', 'msbuild', 'Directory.Packages.props', '-getItem:PackageVersion'], cwd=root, text=True))
    versions = {row['Identity']: row['Version'] for row in evaluated['Items']['PackageVersion']}
    rows = {row['id']: row for row in decisions['packages']}
    old_rows = {row['id']: row for row in discovery['packages']}
    require(len(rows) == len(versions) == 304, 'len(rows) == len(versions) == 304')
    require(len({row['family'] for row in rows.values()}) == 146, "len({row['family'] for row in rows.values()}) == 146")
    require(all((row['selectedVersion'] == versions[name] for name, row in rows.items())), "all((row['selectedVersion'] == versions[name] for name, row in rows.items()))")
    require(all((row['family'] and row['evidence'] and row['removalTrigger'] for row in rows.values())), "all((row['family'] and row['evidence'] and row['removalTrigger'] for row in rows.values()))")
    require(all(('latestStable' in row and 'latestPrerelease' in row for row in rows.values())), "all(('latestStable' in row and 'latestPrerelease' in row for row in rows.values()))")
    accepted = [row for row in rows.values() if row['disposition'] == 'accepted-uncommitted']
    require(len(accepted) == 21, 'len(accepted) == 21')
    consumer = json.loads((evidence / 'accepted-consumer-final-inputs.json').read_text())
    for row in accepted:
        require(row['selectedVersion'] in (row['latestStable'], row['latestPrerelease']), "row['selectedVersion'] in (row['latestStable'], row['latestPrerelease'])")
        require(consumer['acceptedPackageVersions'][row['id']] == row['selectedVersion'], "consumer['acceptedPackageVersions'][row['id']] == row['selectedVersion']")
    for package in ('Verify', 'Verify.XunitV3'):
        require(versions[package] == '33.3.1', "versions[package] == '33.3.1'")
    require('SC021' in (evidence / 'isolated-consumer-build.log').read_text(), "'SC021' in (evidence / 'isolated-consumer-build.log').read_text()")
    require('SC021' in (evidence / 'retained-verify-consumer-build.log').read_text(), "'SC021' in (evidence / 'retained-verify-consumer-build.log').read_text()")
    require('Total: 20, Errors: 0, Failed: 0, Skipped: 0' in (evidence / 'accepted-consumer-tests.log').read_text(), "'Total: 20, Errors: 0, Failed: 0, Skipped: 0' in (evidence / 'accepted-consumer-tests.log').read_text()")
    aspire = [row for row in rows.values() if row['family'] == 'aspire']
    require(len(aspire) == 13, 'len(aspire) == 13')
    for row in aspire:
        expected = '13.6.1-preview.1.26506.6' if row['id'] in ('Aspire.Hosting.Keycloak', 'Aspire.Hosting.Kubernetes') else '13.6.1'
        require(row['selectedVersion'] == expected, "row['selectedVersion'] == expected")
    apphost = root / 'src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj'
    require('Sdk="Aspire.AppHost.Sdk/13.6.1"' in apphost.read_text(), '\'Sdk="Aspire.AppHost.Sdk/13.6.1"\' in apphost.read_text()')
    verify_runtime(root, evidence)
    verify_consumer(root, evidence, versions)
    verify_authority(root, evidence)
    require(versions['CommunityToolkit.Aspire.Hosting.Dapr'] == '13.6.0-preview.1.261001-0243', "versions['CommunityToolkit.Aspire.Hosting.Dapr'] == '13.6.0-preview.1.261001-0243'")
    with tempfile.TemporaryDirectory(prefix='eventstore-folders-catalog-') as folder:
        folders_project = Path(folder) / 'Hexalith.Folders.Aspire.proj'
        folders_project.write_text(f'<Project><Import Project="{catalog}" /></Project>\n')
        folders = json.loads(subprocess.check_output(['dotnet', 'msbuild', str(folders_project), '-getItem:PackageVersion'], cwd=root, text=True))
    folders_versions = {row['Identity']: row['Version'] for row in folders['Items']['PackageVersion']}
    require(folders_versions['CommunityToolkit.Aspire.Hosting.Dapr'] == '13.0.0', "folders_versions['CommunityToolkit.Aspire.Hosting.Dapr'] == '13.0.0'")
    parties = [row for row in rows.values() if row['family'] == 'hexalith-parties']
    require(len(parties) == 9, 'len(parties) == 9')
    require(all((row['selectedVersion'] == '1.1.1' and row['disposition'] == 'retained' for row in parties)), "all((row['selectedVersion'] == '1.1.1' and row['disposition'] == 'retained' for row in parties))")
    require(all((old_rows[name]['listingState'] == 'unresolved' for name in ('Hexalith.Parties.Server', 'Hexalith.Parties.ServiceDefaults', 'Hexalith.Parties.UI'))), "all((old_rows[name]['listingState'] == 'unresolved' for name in ('Hexalith.Parties.Server', 'Hexalith.Parties.ServiceDefaults', 'Hexalith.Parties.UI')))")
    require(versions['Microsoft.OpenApi'] == '2.12.2', "versions['Microsoft.OpenApi'] == '2.12.2'")
    floors = json.loads((evidence / 'dependency-floors.json').read_text())
    openapi = next((row for row in floors if row['package'] == 'microsoft.aspnetcore.openapi'))
    require(any((row['id'] == 'Microsoft.OpenApi' and row['version'] == '[2.12.0, 3.0.0)' for row in openapi['dependencies'])), "any((row['id'] == 'Microsoft.OpenApi' and row['version'] == '[2.12.0, 3.0.0)' for row in openapi['dependencies']))")
    pending = json.loads((evidence / 'audit-regeneration-pending.json').read_text())
    audit_path = root / 'references/Hexalith.Builds/Tools/package-version-audit.json'
    audit = json.loads(audit_path.read_text())
    finalization = json.loads((evidence / 'authoritative-audit-finalization.json').read_text())
    prior_path = evidence / 'authoritative-audit-before-finalization.json.txt'
    prior = json.loads(prior_path.read_text())
    require(pending['exitCode'] == 1 and pending['unchanged'], "pending['exitCode'] == 1 and pending['unchanged']")
    require(hashlib.sha256(prior_path.read_bytes()).hexdigest() == pending['authoritativeAuditSha256'], "hashlib.sha256(prior_path.read_bytes()).hexdigest() == pending['authoritativeAuditSha256']")
    require('is dirty relative to generated-from revision' in (evidence / 'audit-regeneration-pending.log').read_text(), "'is dirty relative to generated-from revision' in (evidence / 'audit-regeneration-pending.log').read_text()")
    require(finalization['status'] == 'finalized-locally' and finalization['exitCode'] == 0, "finalization['status'] == 'finalized-locally' and finalization['exitCode'] == 0")
    require(hashlib.sha256(audit_path.read_bytes()).hexdigest() == finalization['authoritativeAuditSha256'], "hashlib.sha256(audit_path.read_bytes()).hexdigest() == finalization['authoritativeAuditSha256']")
    require(audit['generatedFromRevision'] == finalization['generatedFromRevision'], "audit['generatedFromRevision'] == finalization['generatedFromRevision']")
    require(audit['catalogSha256'] == hashlib.sha256(catalog.read_bytes()).hexdigest(), "audit['catalogSha256'] == hashlib.sha256(catalog.read_bytes()).hexdigest()")
    committed_catalog = subprocess.check_output(['git', 'show', f"{audit['generatedFromRevision']}:Props/Directory.Packages.props"], cwd=root / 'references/Hexalith.Builds')
    require(hashlib.sha256(committed_catalog).hexdigest() == audit['catalogRawSha256'], "hashlib.sha256(committed_catalog).hexdigest() == audit['catalogRawSha256']")
    audit_rows = {row['id']: row for row in audit['packages']}
    audit_families = {row['family']: row for row in audit['familyDecisions']}
    require(len(audit_rows) == 304 and len(audit_families) == 146, 'len(audit_rows) == 304 and len(audit_families) == 146')
    require(all((row['selectedVersion'] == versions[name] for name, row in audit_rows.items())), "all((row['selectedVersion'] == versions[name] for name, row in audit_rows.items()))")
    for group, key in (('packages', 'id'), ('familyDecisions', 'family')):
        current = {row[key]: row for row in audit[group]}
        for row in prior[group]:
            require(all((history in current[row[key]]['historicalContext'] for history in row.get('historicalContext', []))), "all((history in current[row[key]]['historicalContext'] for history in row.get('historicalContext', [])))")
    require(finalization['priorHistoryPreserved'], "finalization['priorHistoryPreserved']")
    for family in finalization['authoritativeAcceptedFamilies']:
        require(audit_families[family]['disposition'] == 'accepted', "audit_families[family]['disposition'] == 'accepted'")
        require(audit_families[family]['representativeConsumers'], "audit_families[family]['representativeConsumers']")
    for family in finalization['authoritativeRetainedClassificationLimitations']:
        require(audit_families[family]['disposition'] == 'retained', "audit_families[family]['disposition'] == 'retained'")
        require(not audit_families[family]['representativeConsumers'], "not audit_families[family]['representativeConsumers']")
        require('owning-Builds' in audit_families[family]['rationale'], "'owning-Builds' in audit_families[family]['rationale']")
    print('PASS: Candidate, Aspire, Preview, Incomplete family, Compatibility, and Provenance matrix checks; 304 decisions match the current catalog.')
if __name__ == '__main__':
    try:
        main()
    except (EvidenceError, ValueError, KeyError, TypeError, OSError, zipfile.BadZipFile, subprocess.SubprocessError) as error:
        print(f"FAIL: {error}", file=sys.stderr)
        raise SystemExit(1)

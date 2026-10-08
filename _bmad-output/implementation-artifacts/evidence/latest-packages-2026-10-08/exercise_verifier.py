"""Exercise evidence validation under optimization without editing captures."""

import hashlib
import json
import shutil
import subprocess
import tempfile
import zipfile
from datetime import datetime, timezone
from pathlib import Path


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    evidence = Path(__file__).resolve().parent
    root = evidence.parents[3]
    verifier = evidence / "verify-decisions.py"
    original = {str(path.relative_to(evidence)): digest(path) for path in evidence.rglob("*")
                if path.is_file() and "__pycache__" not in path.parts}
    cases = []

    def update_json(packet, name, mutate):
        path = packet / name
        value = json.loads(path.read_text())
        mutate(value)
        path.write_text(json.dumps(value, indent=2) + "\n")

    def resource(packet, mutate):
        update_json(packet, "review-runtime-resources.json", mutate)
        update_json(packet, "review-runtime-results.json",
                    lambda value: value.update(resourcesSha256=digest(packet / "review-runtime-resources.json")))

    def manifest(packet, name, mutate):
        capture_path = packet / "consumer-resolved-capture.json"
        capture = json.loads(capture_path.read_text())
        archive = packet / capture["resolvedArchive"]["path"]
        with zipfile.ZipFile(archive) as stored:
            entries = {member: stored.read(member) for member in stored.namelist()}
        member = Path(name).name
        value = json.loads(entries[member])
        mutate(value)
        entries[member] = (json.dumps(value, indent=2) + "\n").encode()
        with zipfile.ZipFile(archive, "w", compression=zipfile.ZIP_DEFLATED) as stored:
            for member, raw in entries.items():
                stored.writestr(member, raw)
        capture["resolvedArchive"]["sha256"] = digest(archive)
        capture["resolvedArchive"]["entries"] = {member: hashlib.sha256(raw).hexdigest() for member, raw in entries.items()}
        capture_path.write_text(json.dumps(capture, indent=2) + "\n")

    def binary_failure(packet):
        path = packet / "review-runtime-console.log"
        path.write_text(path.read_text() + "\nTypeLoadException\n")

        def bind(value):
            value["consoleSha256"] = digest(path)
            for row in value["commands"]:
                if row["log"] == path.name:
                    row["sha256"] = digest(path)

        update_json(packet, "review-runtime-results.json", bind)

    def wrong_asset(value):
        value["libraries"]["AngleSharp/1.8.3"] = value["libraries"].pop("AngleSharp/1.8.4")

    def wrong_deps(value):
        value["libraries"]["Aspire.Hosting/13.6.0"] = value["libraries"].pop("Aspire.Hosting/13.6.1")

    def extra_member(packet):
        capture_path = packet / "consumer-resolved-capture.json"
        capture = json.loads(capture_path.read_text())
        archive = packet / capture["resolvedArchive"]["path"]
        with zipfile.ZipFile(archive, "a") as stored:
            stored.writestr("unexpected.txt", "extra")
        capture["resolvedArchive"]["sha256"] = digest(archive)
        capture_path.write_text(json.dumps(capture, indent=2) + "\n")

    def malformed_archive(packet):
        capture_path = packet / "consumer-resolved-capture.json"
        capture = json.loads(capture_path.read_text())
        archive = packet / capture["resolvedArchive"]["path"]
        archive.write_bytes(b"not a ZIP")
        capture["resolvedArchive"]["sha256"] = digest(archive)
        capture_path.write_text(json.dumps(capture, indent=2) + "\n")

    mutations = [
        ("unhealthy-actual-resource", lambda packet: resource(packet, lambda rows: rows[0].update(healthStatus="Unhealthy"))),
        ("missing-required-resource", lambda packet: resource(packet, lambda rows: rows.pop(next(index for index, row in enumerate(rows) if row["displayName"] == "eventstore")))),
        ("wrong-required-names", lambda packet: update_json(packet, "review-runtime-results.json", lambda value: value.update(requiredServices=[]))),
        ("runtime-input-drift", lambda packet: update_json(packet, "review-runtime-results.json", lambda value: value["beforeInputs"]["sha256"].update({"src/Hexalith.EventStore.AppHost/Program.cs": "0" * 64}))),
        ("binary-failure-with-valid-digest", binary_failure),
        ("resolved-asset-drift", lambda packet: manifest(packet, "consumer-resolved/project.assets.json", wrong_asset)),
        ("runtime-dependency-drift", lambda packet: manifest(packet, "consumer-resolved/Probe.deps.json", wrong_deps)),
        ("consumer-source-drift", lambda packet: update_json(packet, "consumer-resolved-capture.json", lambda value: value["sourceFiles"].update({"isolated-consumer-source/CatalogProjection.cs.txt": "0" * 64}))),
        ("malformed-resource-shape", lambda packet: resource(packet, lambda rows: rows.clear())),
        ("sealed-preflight-drift", lambda packet: update_json(packet, "consumer-authority-after-sealed-preflight.json", lambda value: value.update(preflightSha256="0" * 64))),
        ("extra-resolved-archive-member", extra_member),
        ("wrong-uncompressed-entry-digest", lambda packet: update_json(packet, "consumer-resolved-capture.json", lambda value: value["resolvedArchive"]["entries"].update({"project.assets.json": "0" * 64}))),
        ("malformed-resolved-archive", malformed_archive),
    ]
    command = ["python3", "-O", str(verifier)]
    positive = subprocess.run(command, cwd=root, text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
    if positive.returncode:
        raise ValueError("Current optimized verifier did not pass: " + positive.stdout)
    with tempfile.TemporaryDirectory(prefix="eventstore-relocated-evidence-") as folder:
        packet = Path(folder) / "packet"
        shutil.copytree(evidence, packet, ignore=shutil.ignore_patterns("__pycache__"))
        wrapper = packet / "isolated-consumer-source/Directory.Packages.props.txt"
        current = json.loads((packet / "consumer-resolved-capture.json").read_text())["catalogCapturePath"]
        relocated = "/recorded/previous-checkout/references/Hexalith.Builds/Props/Directory.Packages.props"
        wrapper.write_text(wrapper.read_text().replace(current, relocated))

        def relocate_capture(value):
            value["catalogCapturePath"] = relocated
            value["sourceFiles"]["isolated-consumer-source/Directory.Packages.props.txt"] = digest(wrapper)

        update_json(packet, "consumer-resolved-capture.json", relocate_capture)
        update_json(packet, "accepted-consumer-final-inputs.json",
                    lambda value: value["files"].update({"Directory.Packages.props": wrapper.read_text()}))
        relocated_command = [*command, "--evidence-dir", str(packet)]
        relocated_run = subprocess.run(relocated_command, cwd=root, text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
        if relocated_run.returncode:
            raise ValueError("Valid historical capture origin was rejected: " + relocated_run.stdout)
        portability = {"command": relocated_command, "exitCode": relocated_run.returncode,
                       "output": relocated_run.stdout.strip(), "historicalCatalogOrigin": relocated}
    for name, mutate in mutations:
        with tempfile.TemporaryDirectory(prefix="eventstore-malformed-evidence-") as folder:
            packet = Path(folder) / "packet"
            shutil.copytree(evidence, packet, ignore=shutil.ignore_patterns("__pycache__"))
            mutate(packet)
            candidate = [*command, "--evidence-dir", str(packet)]
            run = subprocess.run(candidate, cwd=root, text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
            if run.returncode != 1 or not run.stdout.startswith("FAIL:"):
                raise ValueError(f"Optimized malformed case did not fail explicitly: {name}: {run.stdout}")
            cases.append({"name": name, "command": candidate, "exitCode": run.returncode,
                          "output": run.stdout.strip()})
    if any(digest(evidence / name) != value for name, value in original.items()):
        raise ValueError("A retained capture changed during malformed-evidence checks")
    overview = (root / "docs/brownfield/project-overview.md").read_text()
    if "| `13.6.1` (Keycloak/K8s `13.6.1-preview.1.26506.6`) |" not in overview or "| `10.0.12` / `10.3.0` |" not in overview:
        raise ValueError("Project overview version cells do not match validated pins")
    attribute_checks = []
    for name in ("discovery-audit.json", "isolated-consumer-source/Probe.csproj.txt", "consumer-resolved/Probe.resolved-inputs.zip"):
        path = evidence / name
        relative = str(path.relative_to(root))
        attrs = subprocess.check_output(["git", "check-attr", "text", "--", relative], cwd=root, text=True).strip()
        if not attrs.endswith(": text: unset"):
            raise ValueError(f"Exact packet byte protection is missing: {relative}")
        data = path.read_bytes()
        blob_hash = hashlib.sha1(f"blob {len(data)}\0".encode() + data).hexdigest()
        actual = subprocess.check_output(["git", "-c", "core.autocrlf=true", "hash-object", "--path", relative, "--stdin"], input=data, cwd=root, text=False).decode().strip()
        if actual != blob_hash:
            raise ValueError(f"autocrlf would change retained evidence: {relative}")
        attribute_checks.append({"path": relative, "attribute": attrs, "unconvertedGitBlobSha1": actual})
    result = {"completedAtUtc": datetime.now(timezone.utc).isoformat(), "positiveCommand": command,
              "positiveExitCode": positive.returncode, "positiveOutput": positive.stdout.strip(),
              "historicalOriginPortability": portability,
              "negativeCases": cases, "originalCapturesUnchanged": True,
              "verifierSha256": digest(verifier), "attributeChecks": attribute_checks,
              "versionCellsMatch": True}
    (evidence / "review-verifier-checks.json").write_text(json.dumps(result, indent=2) + "\n")
    print(f"PASS: optimized verifier; {len(cases)} malformed packets rejected; original captures unchanged; version cells and autocrlf byte protection verified.")


if __name__ == "__main__":
    main()

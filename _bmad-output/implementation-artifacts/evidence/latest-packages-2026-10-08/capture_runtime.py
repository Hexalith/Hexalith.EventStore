"""Capture the explicit isolated AppHost check with sanitized console evidence."""

import hashlib
import json
import re
import subprocess
from datetime import datetime, timezone
from pathlib import Path


REQUIRED = ["eventstore", "eventstore-admin", "eventstore-admin-ui", "sample",
            "sample-api", "sample-blazor-ui", "tenants-api", "security"]
BINARY_FAILURES = ["TypeLoadException", "MissingMethodException", "MissingFieldException", "FileLoadException"]


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def sanitize(text):
    text = re.sub(r"(?i)([?&]t=)[^\s\"&]+", r"\1[REDACTED]", text)
    text = re.sub(r"(?i)(Bearer\s+)[A-Za-z0-9._~+/=-]+", r"\1[REDACTED]", text)
    text = re.sub(r"eyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+", "[REDACTED JWT]", text)
    text = re.sub(r'(?i)((?:password|secret|api[-_ ]?key|access[-_ ]?token|app[-_ ]?token|authorization)\s*[=:]\s*)[^\s,;"\\]+',
                  r"\1[REDACTED]", text)
    return text


def parse_json(text):
    decoder = json.JSONDecoder()
    for offset, char in enumerate(text):
        if char not in "[{":
            continue
        try:
            value, _ = decoder.raw_decode(text[offset:])
            if isinstance(value, (list, dict)):
                return value
        except json.JSONDecodeError:
            continue
    raise ValueError("Aspire describe did not provide a resource list")


def main():
    evidence = Path(__file__).resolve().parent
    root = evidence.parents[3]
    apphost = "src/Hexalith.EventStore.AppHost/Hexalith.EventStore.AppHost.csproj"
    files = list(json.loads((evidence / "final-runtime-results.json").read_text())["afterInputs"]["sha256"])

    def inputs():
        return {"sha256": {path: digest(root / path) for path in files},
                "revisions": {path: subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=root / path,
                                                             text=True).strip()
                              for path in (".", "references/Hexalith.Builds", "references/Hexalith.Tenants")}}

    commands = []
    outputs = []

    def run(command, filename):
        command = ["aspire", *command, "--apphost", apphost, "--non-interactive"]
        result = subprocess.run(command, cwd=root, text=True, stdout=subprocess.PIPE,
                                stderr=subprocess.STDOUT, timeout=300)
        path = evidence / filename
        path.write_text(sanitize(result.stdout))
        commands.append({"command": command, "exitCode": result.returncode,
                         "log": filename, "sha256": digest(path)})
        outputs.append(result.stdout)
        if result.returncode:
            raise ValueError(f"Aspire command failed; see {filename}")
        return result.stdout

    before = inputs()
    started_at = datetime.now(timezone.utc).isoformat()
    stopped = False
    try:
        run(["start", "--isolated"], "review-runtime-start.log")
        for name in REQUIRED:
            run(["wait", name], f"review-runtime-wait-{name}.log")
        description = parse_json(run(["describe", "--format", "Json"], "review-runtime-describe.log"))
        resources = description if isinstance(description, list) else description.get("resources")
        if not isinstance(resources, list):
            raise ValueError("Aspire describe resource shape is unsupported")
        resources = [{key: row.get(key) for key in ("name", "displayName", "resourceType", "state", "healthStatus")}
                     for row in resources]
        states_path = evidence / "review-runtime-resources.json"
        states_path.write_text(json.dumps(resources, indent=2) + "\n")
        logs = run(["logs", "--format", "Json"], "review-runtime-console.log")
        console_path = evidence / "review-runtime-console.log"
        counts = {name: console_path.read_text().count(name) for name in BINARY_FAILURES}
        if counts != {name: logs.count(name) for name in BINARY_FAILURES}:
            raise ValueError("Sanitization changed binary-failure evidence")
        after = inputs()
        run(["stop"], "review-runtime-stop.log")
        stopped = True
        required_rows = {name: [row for row in resources if row["displayName"] == name] for name in REQUIRED}
        if any(len(rows) != 1 or rows[0]["state"] != "Running" or rows[0]["healthStatus"] != "Healthy"
               for rows in required_rows.values()):
            raise ValueError("A required service is absent or unhealthy in the actual resource list")
        if any(counts.values()) or before["sha256"] != after["sha256"]:
            raise ValueError("Runtime binary failure or source drift")
        result = {"startedAtUtc": started_at, "completedAtUtc": datetime.now(timezone.utc).isoformat(),
                  "commands": commands, "requiredServices": REQUIRED,
                  "resourcesPath": states_path.name, "resourcesSha256": digest(states_path),
                  "resourceCount": len(resources), "requiredServicesRunningHealthy": True,
                  "allModeledResourcesRunningHealthy": all(row["state"] == "Running" and row["healthStatus"] == "Healthy" for row in resources),
                  "consolePath": console_path.name, "consoleSha256": digest(console_path),
                  "binaryFailureCounts": counts, "beforeInputs": before, "afterInputs": after,
                  "inputsStableDuringRun": True, "driverSha256": digest(Path(__file__)),
                  "note": "Current unchanged pins; explicit isolated start, eight waits, describe, logs, and stop. Failure counts are computed from the retained sanitized console bytes and checked against raw in-memory output. Prior observations remain history."}
        (evidence / "review-runtime-results.json").write_text(json.dumps(result, indent=2) + "\n")
        print(f"Runtime passed: {len(resources)} resources, eight healthy required services, zero retained-console binary failures, stopped.")
    finally:
        if not stopped:
            subprocess.run(["aspire", "stop", "--apphost", apphost, "--non-interactive"], cwd=root,
                           stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, timeout=60)


if __name__ == "__main__":
    main()

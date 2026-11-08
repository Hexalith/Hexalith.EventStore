"""Fail-closed, non-authorizing Story 3.21 evidence contract (stdlib only)."""
from __future__ import annotations

import hashlib
import json
import re
from datetime import datetime
from pathlib import Path

SCENARIOS = (
    "crash-redelivery", "broker-restart", "durable-quarantine", "failed-diversion",
    "retention-expiry", "stable-id-deduplication", "app-topic-scopes", "tls-negative",
    "missing-secret", "secret-rotation", "cancellation-backpressure", "missing-bindings",
)
BROKERS = ("rabbitmq", "kafka")
SHA = re.compile(r"^[a-f0-9]{64}$")
IMAGE = re.compile(r"^[a-z0-9./_-]+@sha256:[a-f0-9]{64}$")


def digest(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def write_json(path: Path, value: object) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2, sort_keys=True) + "\n", encoding="utf-8")


def safe_file(root: Path, name: str) -> Path:
    if not isinstance(name, str) or not name or Path(name).is_absolute():
        raise ValueError(f"invalid retained path: {name!r}")
    path = root / name
    if ".." in Path(name).parts or path.is_symlink() or not path.resolve().is_relative_to(root.resolve()):
        raise ValueError(f"escaping retained path: {name}")
    if not path.is_file():
        raise ValueError(f"missing retained file: {name}")
    return path


def validate_profile(profile: dict, root: Path) -> None:
    runtime = profile["runtime"]
    version = runtime["version"]
    if not re.fullmatch(r"[0-9]+\.[0-9]+\.[0-9]+", version) or tuple(map(int, version.split("."))) < (1, 18, 3):
        raise ValueError("runtime.version: production floor is 1.18.3")
    if version != "1.18.7" or runtime["cliVersion"] != "1.18.2" or runtime["componentsVersion"] != "1.18.6":
        raise ValueError("runtime: changed candidates require requalification")
    for key in ("binarySha256", "cliSha256"):
        if not SHA.fullmatch(runtime[key]):
            raise ValueError(f"runtime.{key}: missing immutable binary identity")
    if profile.get("productionEligible") is not False or profile.get("selectedBroker") is not None:
        raise ValueError("candidate inputs cannot select or authorize production")
    if profile.get("target") != "self-hosted-kubernetes" or profile.get("executionStage") != "local-docker-first-stage":
        raise ValueError("target/executionStage: local results cannot be Kubernetes proof")
    if profile.get("requiredScenarios") != list(SCENARIOS):
        raise ValueError("requiredScenarios: missing, duplicate or changed matrix")
    expected = {"daprd", "operator", "injector", "sentry", "placement", "scheduler", *BROKERS}
    if set(profile["images"]) != expected:
        raise ValueError("images: incomplete runtime/control-plane/broker identities")
    for name, identity in profile["images"].items():
        if not IMAGE.fullmatch(identity["reference"]):
            raise ValueError(f"images.{name}.reference: mutable image")
        expected_version = "4.3.6-management" if name == "rabbitmq" else "4.3.1" if name == "kafka" else version
        if identity.get("version") != expected_version:
            raise ValueError(f"images.{name}.version: candidate version mismatch")
        retained = safe_file(root, identity["manifest"]["path"])
        actual = digest(retained)
        if actual != identity["manifest"]["sha256"] or actual != identity["reference"].split(":")[-1]:
            raise ValueError(f"images.{name}: retained OCI index digest mismatch")
        manifests = json.loads(retained.read_text())["manifests"]
        if not any(x.get("platform", {}).get("os") == "linux" and x["platform"].get("architecture") == "amd64" for x in manifests):
            raise ValueError(f"images.{name}: missing linux/amd64 child")
    if not IMAGE.fullmatch(profile["openbao"]["reference"]):
        raise ValueError("openbao.reference: mutable image")
    if len(runtime["sources"]) != 2 or any(not re.fullmatch(r"[a-f0-9]{40}", x["commit"]) for x in runtime["sources"]):
        raise ValueError("runtime.sources: missing pinned runtime/components source")
    if {(x["repository"], x["tag"], x["commit"]) for x in runtime["sources"]} != {
        ("dapr/dapr", "v1.18.7", "b32a815fa2260983e181b95272ea555fe17d3895"),
        ("dapr/components-contrib", "v1.18.6", "eac9e31dd14cd798c9996aae30818b572aa12c59"),
    }:
        raise ValueError("runtime.sources: candidate source identity mismatch")
    for name, expected_hash in profile["inputs"].items():
        if digest(safe_file(root, name)) != expected_hash:
            raise ValueError(f"inputs.{name}: source/component bytes drifted")
    if not profile["inputs"]:
        raise ValueError("inputs: source/component inventory is empty")


def evaluate(scenario: str, observation: dict) -> bool:
    """Recompute conclusions from observations; producer PASS flags are never proof."""
    if observation.get("blockedBy"):
        return False
    o = observation
    if scenario == "crash-redelivery":
        return o["receiptCount"] >= 2 and o["effectCount"] == 1 and o["crashExitCode"] == 86 and o["sameId"] is True
    if scenario == "broker-restart":
        return o["beforeContainerId"] == o["afterContainerId"] and o["effectCount"] == 1 and o["publishedWhileOffline"] is True
    if scenario == "durable-quarantine":
        return o["poisonReceipts"] >= 1 and o["quarantineEffects"] == 1 and o["survivedRestart"] is True
    if scenario == "failed-diversion":
        return o["denialObserved"] is True and o["quarantineEffects"] == 0 and o["sourceRecoveredAfterRestart"] is True
    if scenario == "retention-expiry":
        return o["controlEffects"] == 1 and o["expiredEffects"] == 0 and o["outageSeconds"] >= 3 and o["brokerRetentionObserved"] is True
    if scenario == "stable-id-deduplication":
        return o["receiptCount"] >= 2 and o["effectCount"] == 1 and o["sameId"] is True
    if scenario == "app-topic-scopes":
        return o["allowedEffects"] == 1 and o["forbiddenAppStatus"] in (400, 401, 403, 404) and o["forbiddenTopicStatus"] in (400, 401, 403, 404) and o["forbiddenSubscriptions"] == 0
    if scenario in ("tls-negative", "missing-secret"):
        return o["allowedEffects"] == 1 and o["negativeReady"] is False and o["negativeExitedNaturally"] is True and o["negativeExitCode"] not in (None, 0) and o["diagnosticMatched"] is True
    if scenario == "secret-rotation":
        return o["oldGenerationDenied"] is True and o["newGenerationEffects"] == 1 and o["controlledRestart"] is True
    if scenario == "cancellation-backpressure":
        return o["cancelled"] is True and o["boundedSeconds"] <= 10 and o["negativeConfirmRejected"] is True and o["acknowledgedLoss"] == 0
    if scenario == "missing-bindings":
        return 400 <= o["unboundPublishStatus"] < 599 and o["allowedEffects"] == 1 and o["bindingAbsent"] is True
    raise ValueError(f"unknown scenario: {scenario}")


def validate_observations(scenario: str, observation: dict, readback: dict, diagnostic: dict, commands: list) -> None:
    """Bind producer counters to retained HTTP/Dapr state bytes and broker commands."""
    if observation.get("blockedBy"):
        return
    index = readback.get("commandReceipt")
    if not isinstance(index, int) or not 0 <= index < len(commands):
        raise ValueError(f"{scenario}: missing readback command binding")
    command = commands[index]
    body = {k: v for k, v in readback.items() if k != "commandReceipt"}
    if command.get("httpStatus") != 200 or not command["argv"][-1].endswith("/readback") or json.loads(command["output"]) != body:
        raise ValueError(f"{scenario}: application HTTP readback contradicts receipt")
    if readback.get("stateComponent") != "qualification-state" or readback.get("stateReadback", {}).get("status") not in (200, 204):
        raise ValueError(f"{scenario}: persisted Dapr state end-state is absent")
    ledger = readback["ledger"]
    effects = [row for row in ledger if row.get("kind") == "effect"]
    if effects != readback["stateReadback"]["value"]:
        raise ValueError(f"{scenario}: effect ledger contradicts Dapr state readback")
    event_id = observation.get("messageId")
    rows = [row for row in ledger if row.get("id") == event_id]
    count = sum(row["kind"] == "effect" for row in rows)
    counts = {"effectCount": count, "allowedEffects": count, "controlEffects": count, "newGenerationEffects": count,
              "quarantineEffects": sum(row["kind"] == "effect" and row["route"] == "/quarantine" for row in rows),
              "receiptCount": sum(row["kind"] == "receipt" for row in rows),
              "poisonReceipts": sum(row["kind"] == "receipt" and row["route"] == "/poison" for row in rows),
              "expiredEffects": sum(row["kind"] == "effect" and row["id"] == observation.get("expiredMessageId") for row in ledger)}
    for field, expected in counts.items():
        if field in observation and observation[field] != expected:
            raise ValueError(f"{scenario}.{field}: contradicts persisted readback")
    if "sameId" in observation and observation["sameId"] != bool(rows and all(row["id"] == row.get("messageId") for row in rows)):
        raise ValueError(f"{scenario}.sameId: CloudEvent identity differs from MessageId")
    index = diagnostic.get("commandReceipt")
    if not isinstance(index, int) or not 0 <= index < len(commands):
        raise ValueError(f"{scenario}: missing broker diagnostic command binding")
    command = commands[index]
    if diagnostic.get("output") != command.get("output") or diagnostic.get("exitCode") != command.get("exitCode") or command["argv"][:2] != ["docker", "exec"]:
        raise ValueError(f"{scenario}: broker diagnostic contradicts command receipt")
    if evaluate(scenario, observation) and diagnostic["exitCode"] != 0:
        raise ValueError(f"{scenario}: passing result lacks successful broker diagnostics")
    def publication(message_id):
        matches = [row for row in commands if row.get("argv", [""])[0] == "HTTP" and "/publish/" in row["argv"][-1] and row.get("input", {}).get("id") == message_id]
        if not matches:
            raise ValueError(f"{scenario}: missing publication command binding")
        return matches[-1]

    if event_id:
        publication(event_id)
    if scenario == "crash-redelivery" and not any(row.get("exitCode") == observation["crashExitCode"] and any(str(argument).endswith("dapr_broker_probe.py") for argument in row["argv"]) for row in commands):
        raise ValueError("crash-redelivery: missing crashed application process receipt")
    if scenario in ("broker-restart", "durable-quarantine", "failed-diversion"):
        index = observation.get("restartReceipt")
        if not isinstance(index, int) or not 0 <= index < len(commands) or commands[index]["argv"][:2] != ["docker", "restart"] or commands[index]["exitCode"] != 0:
            raise ValueError(f"{scenario}: missing broker restart receipt")
        restarted_at = datetime.fromisoformat(commands[index]["endedUtc"])
        if scenario == "broker-restart" and observation["publishedWhileOffline"] != (publication(event_id)["httpStatus"] < 300):
            raise ValueError("broker-restart: publish acknowledgement contradicts HTTP receipt")
        if scenario == "durable-quarantine":
            survived = any(row["kind"] == "effect" and row["route"] == "/quarantine" and datetime.fromisoformat(row["persistedUtc"]) >= restarted_at for row in rows)
            if observation["survivedRestart"] != survived:
                raise ValueError("durable-quarantine: capture does not follow broker restart")
        if scenario == "failed-diversion":
            recovered = any(row["kind"] == "receipt" and datetime.fromisoformat(row["receivedUtc"]) >= restarted_at for row in rows)
            denied = any(re.search(r"(not allowed|denied|forbidden).*qualification[.]quarantine-denied|qualification[.]quarantine-denied.*(not allowed|denied|forbidden)", row.get("output", ""), re.I) for row in commands if "--app-id" in row["argv"])
            if observation["sourceRecoveredAfterRestart"] != recovered or observation["denialObserved"] != denied:
                raise ValueError("failed-diversion: recovery/denial contradicts native receipts")
    if scenario == "app-topic-scopes":
        for field, id_field in (("forbiddenAppStatus", "forbiddenAppMessageId"), ("forbiddenTopicStatus", "forbiddenTopicMessageId")):
            if publication(observation[id_field])["httpStatus"] != observation[field]:
                raise ValueError(f"app-topic-scopes.{field}: denial contradicts HTTP receipt")
        metadata = [json.loads(row["output"]) for row in commands if row.get("httpStatus") == 200 and row["argv"][-1].endswith("/v1.0/metadata")]
        forbidden = [row for row in metadata if row.get("id") == "qualification-forbidden"]
        if not forbidden or observation["forbiddenSubscriptions"] != len(forbidden[-1].get("subscriptions", [])):
            raise ValueError("app-topic-scopes: subscription denial contradicts loaded metadata")
    if scenario in ("tls-negative", "missing-secret"):
        processes = [row for row in commands if "--resources-path" in row["argv"] and row["argv"][row["argv"].index("--resources-path") + 1] == observation["negativeResourceDirectory"]]
        if len(processes) != 1 or (observation["negativeExitedNaturally"] and processes[0]["exitCode"] != observation["negativeExitCode"]):
            raise ValueError(f"{scenario}: wrong negative sidecar receipt")
        matched = bool(re.search("certificate|failed to get secret|permission denied|not allowed", processes[0]["output"], re.I))
        if observation["diagnosticMatched"] != matched:
            raise ValueError(f"{scenario}: diagnostic contradicts sidecar receipt")
    if scenario == "secret-rotation":
        stale_id = observation["staleMessageId"]
        status = publication(stale_id)["httpStatus"]
        denied = 400 <= status < 599 and not any(row["id"] == stale_id and row["kind"] == "effect" for row in ledger)
        if observation["oldGenerationDenied"] != denied or observation["stalePublishStatus"] != status:
            raise ValueError("secret-rotation: old-generation denial contradicts receipts")
    if scenario == "cancellation-backpressure":
        results = observation["publishResults"]
        if any(publication(row["id"])["httpStatus"] != row["status"] for row in results):
            raise ValueError("cancellation-backpressure: status contradicts HTTP receipt")
        loss = sum(row["status"] < 300 and not any(effect["id"] == row["id"] for effect in effects) for row in results)
        cancelled = publication(observation["cancelledMessageId"])["httpStatus"] == 599
        rejected = any(400 <= row["status"] < 599 for row in results)
        if observation["acknowledgedLoss"] != loss or observation["cancelled"] != cancelled or observation["negativeConfirmRejected"] != rejected:
            raise ValueError("cancellation-backpressure: loss/cancellation contradicts receipts")
        if observation["broker"] == "kafka":
            quota = commands[observation["quotaReceipt"]]
            if quota["exitCode"] != 0 or quota["argv"][:2] != ["docker", "exec"] or "producer_byte_rate=1024" not in quota["argv"] or "qualification" not in quota["argv"]:
                raise ValueError("cancellation-backpressure: missing executed Kafka quota fault")
    if scenario == "missing-bindings" and publication(observation["unboundMessageId"])["httpStatus"] != observation["unboundPublishStatus"]:
        raise ValueError("missing-bindings: rejection contradicts HTTP receipt")
    if scenario == "retention-expiry" and observation["broker"] == "kafka":
        offsets = [commands[index] for index in observation["offsetReceipts"]]
        if not offsets or any(row["argv"][:2] != ["docker", "exec"] or not any("kafka-get-offsets.sh" in argument for argument in row["argv"]) for row in offsets):
            raise ValueError("retention-expiry: missing native Kafka offset receipt")
        native_expiry = any(row["exitCode"] == 0 and any(re.fullmatch(r"qualification[.]expiry:0:[1-9][0-9]*", line) for line in row.get("output", "").splitlines()) for row in offsets)
        replay = sum(row["kind"] == "receipt" for row in rows) > observation["beforeReplayReceiptCount"]
        if observation["brokerRetentionObserved"] != (native_expiry and replay):
            raise ValueError("retention-expiry: native Kafka evidence contradicts claim")


def validate_packet(directory: Path, *, allow_failures: bool = False, require_authority: bool = False) -> dict:
    packet = json.loads(safe_file(directory, "packet.json").read_text())
    if packet.get("schemaVersion") != 1 or packet.get("productionEligible") is not False or packet.get("selectedBroker") is not None:
        raise ValueError("packet: unsupported schema or fabricated production authority")
    artifacts = packet["artifacts"]
    if not artifacts:
        raise ValueError("artifacts: empty inventory")
    for name, expected in artifacts.items():
        if not SHA.fullmatch(expected) or digest(safe_file(directory, name)) != expected:
            raise ValueError(f"artifacts.{name}: retained-byte hash mismatch")
    actual_files = {str(x.relative_to(directory)) for x in directory.rglob("*") if x.is_file() and x != directory / "packet.json"}
    if actual_files != set(artifacts):
        raise ValueError("artifacts: unlisted or missing retained files")
    profile = json.loads(safe_file(directory, "inputs/deploy/dapr/qualification/runtime-lock.json").read_text())
    validate_profile(profile, directory / "inputs")
    for name, expected in profile["inputs"].items():
        if artifacts.get("inputs/" + name) != expected:
            raise ValueError(f"inputs.{name}: not bound by packet")
    identities = json.loads(safe_file(directory, "execution-identities.json").read_text())
    if identities.get("stage") != profile["executionStage"] or identities.get("targetProof") is not False:
        raise ValueError("execution-identities: local execution is not production-path proof")
    if identities.get("preflightPassed"):
        for key, expected in (("daprdSha256", profile["runtime"]["binarySha256"]), ("cliSha256", profile["runtime"]["cliSha256"])):
            if identities.get(key) != expected:
                raise ValueError(f"execution-identities.{key}: wrong loaded binary")
    commands = json.loads(safe_file(directory, "commands.json").read_text())
    if not commands:
        raise ValueError("commands: no execution receipts")
    for receipt in commands:
        if not receipt.get("argv") or not isinstance(receipt.get("exitCode"), int):
            raise ValueError("commands: missing argv or exit code")
        if datetime.fromisoformat(receipt["endedUtc"]) < datetime.fromisoformat(receipt["startedUtc"]):
            raise ValueError("commands: inverted execution window")
    for name, identity in identities.get("controlPlane", {}).items():
        if name not in ("placement", "scheduler") or identity.get("image") != profile["images"][name]["reference"] or not SHA.fullmatch(identity.get("binarySha256", "")):
            raise ValueError(f"controlPlane.{name}: wrong control-plane identity")
    # Metadata are captured from the actual live sidecar. Source templates alone
    # do not establish which component was loaded, even if all hashes are valid.
    for broker in BROKERS:
        metadata_files = list((directory / broker / "metadata").glob("*.json"))
        roles = set()
        for metadata_file in metadata_files:
            metadata = json.loads(metadata_file.read_text())
            role = metadata.get("id")
            roles.add(role)
            if metadata.get("runtimeVersion") != profile["runtime"]["version"]:
                raise ValueError(f"{broker}: wrong loaded runtimeVersion")
            if role != "qualification-forbidden":
                component = {"name": "pubsub", "type": "pubsub." + broker, "version": "v1"}
                if not any(all(row.get(k) == v for k, v in component.items()) for row in metadata.get("components", [])):
                    raise ValueError(f"{broker}: wrong loaded pubsub component")
            if not any(receipt.get("httpStatus") == 200 and receipt.get("argv", [""])[-1].endswith("/v1.0/metadata") and receipt.get("output") and json.loads(receipt["output"]) == metadata for receipt in commands if receipt.get("argv", [""])[0] == "HTTP"):
                raise ValueError(f"{broker}: loaded metadata lacks HTTP command binding")
        if any(outcome.get("result") == "PASS" for outcome in packet["brokers"].get(broker, {}).values()) and not {"qualification-consumer", "qualification-publisher"}.issubset(roles):
            raise ValueError(f"{broker}: PASS without loaded sidecar metadata")
    failures = []
    for broker in BROKERS:
        outcomes = packet["brokers"].get(broker, {})
        if set(outcomes) != set(SCENARIOS):
            raise ValueError(f"brokers.{broker}: incomplete scenario matrix")
        for scenario, outcome in outcomes.items():
            observation = json.loads(safe_file(directory, outcome["observation"]).read_text())
            readback = json.loads(safe_file(directory, outcome["applicationReadback"]).read_text())
            diagnostic = json.loads(safe_file(directory, outcome["brokerDiagnostics"]).read_text())
            if observation.get("scenario") != scenario or observation.get("broker") != broker:
                raise ValueError(f"{broker}/{scenario}: wrong observation identity")
            if not readback or not diagnostic or outcome["applicationReadback"] == outcome["brokerDiagnostics"]:
                raise ValueError(f"{broker}/{scenario}: independent readback/diagnostics missing")
            validate_observations(scenario, observation, readback, diagnostic, commands)
            passed = evaluate(scenario, observation)
            if outcome["result"] != ("PASS" if passed else "FAIL"):
                raise ValueError(f"{broker}/{scenario}: false result")
            if passed and (readback.get("blockedBy") or diagnostic.get("blockedBy") or not identities.get("preflightPassed")):
                raise ValueError(f"{broker}/{scenario}: PASS without executed evidence")
            if passed and readback.get("kind") != "persisted-application-http-readback":
                raise ValueError(f"{broker}/{scenario}: application readback is not persisted HTTP evidence")
            if passed and diagnostic.get("kind") != "broker-administration-diagnostic":
                raise ValueError(f"{broker}/{scenario}: missing separate broker diagnostics")
            if not passed:
                failures.append(f"{broker}/{scenario}")
    cleanup = json.loads(safe_file(directory, "cleanup.json").read_text())
    before = json.loads(safe_file(directory, "workspace-before.json").read_text())
    after = json.loads(safe_file(directory, "workspace-after.json").read_text())
    if any(after.get(key) != value for key, value in before.items()):
        raise ValueError("cleanup: unrelated container specifications changed")
    for snapshot in (before, after):
        if snapshot and not any(receipt.get("argv", [])[:2] == ["docker", "inspect"] and receipt.get("output") == json.dumps(snapshot, sort_keys=True) for receipt in commands):
            raise ValueError("cleanup: specification hashes lack command receipt binding")
    if cleanup.get("ownedResourcesRemaining") != [] or cleanup.get("unrelatedContainersUnchanged") is not True:
        raise ValueError("cleanup: remaining owned resources or changed unrelated containers")
    if require_authority:
        raise ValueError("production eligibility FAIL: authenticated AD-26 Architecture/Platform records, Story 3.19 publication/profile binding, Kubernetes rehearsal, restore and Story 4.16 envelope proof are absent; this lane never issues authority")
    if failures and not allow_failures:
        raise ValueError("technical qualification FAIL: " + ", ".join(failures))
    return {"integrity": "PASS", "technicalQualification": "FAIL" if failures else "PASS", "failedScenarios": failures, "productionEligibility": "FAIL", "selectedBroker": None}

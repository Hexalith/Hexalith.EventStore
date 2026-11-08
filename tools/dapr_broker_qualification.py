"""Fail-closed, non-authorizing Story 3.21 evidence contract (stdlib only)."""
from __future__ import annotations

import io
import tarfile
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
    from dapr_runtime_qualification import APPROVED, source_input
    selected = (version, runtime["cliVersion"], runtime["componentsVersion"])
    if selected not in APPROVED:
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
    if {(x["repository"], x["tag"], x["commit"]) for x in runtime["sources"]} != APPROVED[selected] or len(runtime["sources"]) != len(APPROVED[selected]):
        raise ValueError("runtime.sources: candidate source identity mismatch")
    if version == "1.19.1":
        from dapr_runtime_qualification import CANDIDATE_IDENTITIES, CANDIDATE_PROFILE
        trusted = json.loads((Path(__file__).resolve().parents[1] / CANDIDATE_PROFILE).read_text())
        if profile.get('inputs') != trusted['inputs']:
            raise ValueError('inputs: complete source inventory differs from trusted owning checkout')
        for field in ("binarySha256", "cliSha256", "archives"):
            if runtime.get(field) != CANDIDATE_IDENTITIES[field]:
                raise ValueError(f"runtime.{field}: official identity mismatch")
        if {name: identity["reference"] for name, identity in profile["images"].items()} != CANDIDATE_IDENTITIES["images"] or profile.get("chart") != CANDIDATE_IDENTITIES["chart"]:
            raise ValueError("images/chart: official identity mismatch")
        for name, identity in profile["images"].items():
            manifests = json.loads(safe_file(root, identity["manifest"]["path"]).read_text())["manifests"]
            for architecture in ("amd64", "arm64"):
                matches = [x for x in manifests if x.get("platform", {}).get("os") == "linux" and x["platform"].get("architecture") == architecture]
                if len(matches) != 1 or not re.fullmatch(r"sha256:[a-f0-9]{64}", matches[0].get("digest", "")):
                    raise ValueError(f"images.{name}: missing/ambiguous linux/{architecture} child")
                if name not in BROKERS:
                    child = identity["children"][architecture]
                    if child["digest"] != matches[0]["digest"] or "sha256:" + digest(safe_file(root, child["path"])) != child["digest"]:
                        raise ValueError(f"images.{name}: child digest mismatch")
        chart = profile["chart"]
        data = safe_file(root, chart["path"]).read_bytes()
        if hashlib.sha256(data).hexdigest() != chart["sha256"]:
            raise ValueError("chart: archive hash mismatch")
        with tarfile.open(fileobj=io.BytesIO(data), mode="r:gz") as archive:
            metadata = archive.extractfile("dapr/Chart.yaml").read().decode()
        for field in ("version", "appVersion"):
            match = re.search(r"^" + field + r":\s*[\"']?([^\s\"']+)", metadata, re.M)
            if not match or match[1] != version or chart[field] != version:
                raise ValueError(f"chart.{field}: runtime mismatch")
        gomod = next((name for name in profile["inputs"] if name.endswith("/1.19.1/dapr-dapr-go.mod")), None)
        if not gomod or "github.com/dapr/components-contrib v1.19.0" not in safe_file(root, gomod).read_text():
            raise ValueError("runtime: embedded components source mismatch")
        for binary, archive in runtime["archives"].items():
            checksum = safe_file(root, archive["checksumPath"]).read_text().split()[0]
            if checksum != archive["sha256"] or not SHA.fullmatch(checksum):
                raise ValueError(f"runtime.{binary}: release checksum mismatch")
    for name, expected_hash in profile["inputs"].items():
        if digest(source_input(profile, root, name, expected_hash)) != expected_hash:
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


def validate_candidate_custody(identities: dict, commands: list) -> None:
    """Bind accepted custody to observed allocation, socket and every Docker call."""
    custody = identities.get("dockerCustody", {})
    operations = [(index, row) for index, row in enumerate(commands)
                  if row.get("argv", [""])[0] == "docker" and row["argv"][1:3] != ["context", "inspect"]
                  and row["argv"][-3:] != ["info", "--format", "{{.ID}}"]]
    if not identities.get("preflightPassed") and not operations:
        return  # rejected infrastructure has no resource operations or accepted custody
    if (custody.get("exclusive") is not True or not custody.get("workerId") or not custody.get("daemonId")
            or not custody.get("endpoint", "").startswith("unix:///")
            or not isinstance(custody.get("socketIdentity"), list) or len(custody["socketIdentity"]) != 2):
        raise ValueError("execution-identities: exclusive daemon custody is absent")
    admissions = [(index, row) for index, row in enumerate(commands) if row.get("argv") == ["WORKER", "exclusive-allocation"]]
    if len(admissions) != 1:
        raise ValueError("custody: missing observed exclusive worker allocation")
    admission_index, admission = admissions[0]
    if admission.get("leaseLocked") is not True or json.loads(admission["output"]) != custody:
        raise ValueError("custody: unrelated worker allocation")
    def daemon(index):
        if not isinstance(index, int) or not 0 <= index < len(commands):
            raise ValueError("custody: missing observed daemon binding")
        row = commands[index]
        if (row.get("argv") != ["docker", "--host", custody["endpoint"], "info", "--format", "{{.ID}}"]
                or row.get("exitCode") != 0 or row.get("output", "").strip() != custody["daemonId"]
                or row.get("socketIdentity") != custody["socketIdentity"]):
            raise ValueError("custody: unrelated socket/endpoint/daemon observation")
        return index
    if daemon(admission.get("daemonObservation")) >= admission_index:
        raise ValueError("custody: allocation precedes daemon observation")
    for index, row in operations:
        if (row.get("dockerCustody") != custody or row.get("workerAdmission") != admission_index
                or row.get("executedArgv") != ["docker", "--host", custody["endpoint"], *row["argv"][1:]]
                or not admission_index < daemon(row.get("daemonObservation")) < index):
            raise ValueError("custody: Docker operation lacks accepted observed custody")


def validate_inventory(snapshot, phase, identities, commands):
    """Bind every kind, including empty lists, to its own complete observations."""
    index = identities.get('workspaceInventories', {}).get(phase)
    if not isinstance(index, int) or not 0 <= index < len(commands):
        raise ValueError('cleanup: complete inventory observation is absent')
    receipt = commands[index]
    if receipt.get('argv') != ['WORKSPACE', 'inventory', phase] or receipt.get('exitCode') != 0 or json.loads(receipt['output']) != snapshot:
        raise ValueError('cleanup: complete inventory contradicts snapshot')
    observations = receipt.get('resourceObservations', {})
    if set(observations) != {'container', 'network', 'volume'}:
        raise ValueError('cleanup: inventory resource denominator is incomplete')
    bound = {}
    lower = identities.get('workspaceInventories', {}).get('before', -1) if phase == 'after' else -1
    for kind, listing in (('container', ['ps', '-aq', '--no-trunc']), ('network', ['network', 'ls', '-q', '--no-trunc']), ('volume', ['volume', 'ls', '-q'])):
        row = observations[kind]
        ref = row.get('listingReceipt')
        if not isinstance(ref, int) or not max(lower + 1, 0) <= ref < index:
            raise ValueError('cleanup: inventory listing command is absent')
        command = commands[ref]
        if command.get('argv') != ['docker', *listing] or command.get('exitCode') != 0:
            raise ValueError('cleanup: unrelated inventory listing command')
        names = command['output'].split()
        if len(set(names)) != len(names): raise ValueError('cleanup: duplicate inventory resource')
        inspect = row.get('inspectionReceipt')
        if not names:
            if inspect is not None: raise ValueError('cleanup: empty inventory has contradictory inspection')
            continue
        if not isinstance(inspect, int) or not ref < inspect < index:
            raise ValueError('cleanup: inventory inspection command is absent')
        command = commands[inspect]
        if command.get('argv') != ['docker', 'inspect', '--type', kind, *names] or command.get('exitCode') != 0:
            raise ValueError('cleanup: unrelated inventory inspection command')
        values = json.loads(command['output'])
        if not isinstance(values, dict) or set(values) != {kind + ':' + name for name in names} or any(not SHA.fullmatch(value) for value in values.values()):
            raise ValueError('cleanup: incomplete inventory specification hashes')
        bound.update(values)
    if bound != snapshot:
        raise ValueError('cleanup: complete inventory specification hashes contradict snapshot')
    return index


def validate_control_observations(identities, profile, commands, executed):
    """Require an observed extraction, measured file and native launch chain."""
    for name, identity in identities.get('controlPlane', {}).items():
        def command(field):
            index = identity.get(field)
            if not isinstance(index, int) or not 0 <= index < len(commands):
                raise ValueError('controlPlane: missing ' + field + ' command binding')
            row = commands[index]
            if row.get('exitCode') != 0: raise ValueError('controlPlane: unsuccessful identity observation')
            return index, row
        created, creation = command('creationReceipt')
        copied, extraction = command('extractionReceipt')
        measured, measurement = command('hashReceipt')
        if (creation['argv'][:2] != ['docker', 'create'] or profile['images'][name]['reference'] not in creation['argv']
                or extraction['argv'][:2] != ['docker', 'cp'] or not extraction['argv'][2].endswith(':/' + name)
                or not SHA.fullmatch(creation.get('output', '').strip())
                or extraction['argv'][2] != creation['output'].strip() + ':/' + name
                or measurement['argv'] != ['SHA256', extraction['argv'][3]] or measurement.get('output') != identity['binarySha256']
                or not created < copied < measured):
            raise ValueError('controlPlane: extraction/hash command chain differs')
        if identity.get('launchReceipt') is not None or executed:
            launched, launch = command('launchReceipt')
            if (launch.get('event') != 'native-launch' or launch.get('processId', 0) <= 0
                    or launch['argv'][0] != measurement['argv'][1] or launched <= measured):
                raise ValueError('controlPlane: native launch differs from measured executable')


def validate_packet(directory: Path, *, allow_failures: bool = False, require_authority: bool = False) -> dict:
    packet = json.loads(safe_file(directory, "packet.json").read_text())
    if packet.get("schemaVersion") not in (1, 2) or packet.get("productionEligible") is not False or packet.get("selectedBroker") is not None:
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
    from dapr_runtime_qualification import LEGACY_PROFILE, CANDIDATE_PROFILE, profile_path
    selected_profile = packet.get("profile", LEGACY_PROFILE)
    if selected_profile not in (LEGACY_PROFILE, CANDIDATE_PROFILE):
        raise ValueError("packet: unsupported selected profile")
    profile = json.loads(safe_file(directory, "inputs/" + selected_profile).read_text())
    validate_profile(profile, directory / "inputs")
    if selected_profile != profile_path(profile):
        raise ValueError("packet: selected profile does not match loaded runtime tuple")
    if profile["runtime"]["version"] == "1.19.1" and packet.get("schemaVersion") != 2:
        raise ValueError("candidate packets require schema 2")
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
    if profile["runtime"]["version"] == "1.19.1":
        from dapr_runtime_qualification import CONTROL_BINARIES
        for name, identity in identities.get("controlPlane", {}).items():
            if identity.get("binarySha256") != CONTROL_BINARIES[name]:
                raise ValueError("controlPlane: official executable identity mismatch")
    if profile["runtime"]["version"] == "1.19.1":
        executed = any(not json.loads(safe_file(directory, row["observation"]).read_text()).get("blockedBy")
                       for outcomes in packet["brokers"].values() for row in outcomes.values())
        if executed and set(identities.get("controlPlane", {})) != {"placement", "scheduler"}:
            raise ValueError("controlPlane: executed candidate requires placement and scheduler observations")
        validate_candidate_custody(identities, commands)
        validate_control_observations(identities, profile, commands, executed)
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
    if packet.get('schemaVersion') == 2 and profile['runtime']['version'] == '1.19.1' and identities.get('dockerCustody'):
        before_index = validate_inventory(before, 'before', identities, commands)
        after_index = validate_inventory(after, 'after', identities, commands)
        if before_index >= after_index: raise ValueError('cleanup: before/after inventories are not independent observations')
    for snapshot in (before, after):
        if packet.get("schemaVersion") == 1:
            if snapshot and not any(receipt.get("argv", [])[:2] == ["docker", "inspect"] and receipt.get("output") == json.dumps(snapshot, sort_keys=True) for receipt in commands):
                raise ValueError("cleanup: specification hashes lack command receipt binding")
            continue
        bound = {}
        for receipt in commands:
            if receipt.get("argv", [])[:2] == ["docker", "inspect"]:
                try:
                    output = json.loads(receipt.get("output", ""))
                    if isinstance(output, dict): bound.update(output)
                except json.JSONDecodeError:
                    pass
        if any(bound.get(key) != value for key, value in snapshot.items()):
            raise ValueError("cleanup: specification hashes lack command receipt binding")
    if cleanup.get("ownedResourcesRemaining") != [] or cleanup.get("unrelatedContainersUnchanged") is not True:
        raise ValueError("cleanup: remaining owned resources or changed unrelated containers")
    if packet.get("schemaVersion") == 2:
        if profile["runtime"]["version"] == "1.19.1":
            trusted_profile = json.loads((Path(__file__).resolve().parents[1] / CANDIDATE_PROFILE).read_text())
            for path, expected in trusted_profile['inputs'].items():
                trusted = digest(Path(__file__).resolve().parents[1] / path)
                if profile['inputs'].get(path) != trusted or trusted != expected:
                    raise ValueError('inputs: complete source binding mismatch with trusted owning checkout')
                if path.startswith('tools/') and path.endswith('.py') and artifacts.get('execution-tools/' + path.removeprefix('tools/')) != trusted:
                    raise ValueError("execution-tools: current capture source binding mismatch with trusted owning checkout")
        custody = identities.get("dockerCustody", {})
        if identities.get("preflightPassed") and (custody.get("exclusive") is not True or not custody.get("daemonId") or not custody.get("endpoint", "").startswith("unix:///")):
            raise ValueError("execution-identities: exclusive daemon custody is absent")
        if cleanup.get("complete") is not True:
            raise ValueError("cleanup: incomplete baseline comparison or owned cleanup")
    if require_authority:
        raise ValueError("production eligibility FAIL: authenticated AD-26 Architecture/Platform records, Story 3.19 publication/profile binding, Kubernetes rehearsal, restore and Story 4.16 envelope proof are absent; this lane never issues authority")
    if failures and not allow_failures:
        raise ValueError("technical qualification FAIL: " + ", ".join(failures))
    return {"integrity": "PASS", "technicalQualification": "FAIL" if failures else "PASS", "failedScenarios": failures, "productionEligibility": "FAIL", "selectedBroker": None}

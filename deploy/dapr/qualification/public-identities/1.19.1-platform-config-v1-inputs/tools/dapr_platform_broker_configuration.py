"""Bounded Platform experiments and independent effective-input checks (stdlib only).

The caller supplies the same separate configuration to capture and verification.
Its declared revision/path are provenance, never authenticated owner acceptance.
"""
from __future__ import annotations

import hashlib
import json
from pathlib import Path, PurePosixPath
import re

PROFILE_ID = "platform-config-v1"
CONFIGURATION_PATH = "platform-configuration/configuration.json"
PROVENANCE_PATH = "platform-configuration/provenance.json"
RENDERED_PATH = "platform-configuration/rendered-inputs.json"
BOUNDS = {
    "rabbitmq": {"reconnectWaitSeconds": (1, 2, 3), "outboundTimeoutMilliseconds": (250, 500, 1000, 2000, 3000)},
    "kafka": {"consumeRetryIntervalMilliseconds": (100, 200, 500, 1000), "group.initial.rebalance.delay.ms": (0, 100, 500, 1000)},
}
DEFAULTS = {"rabbitmq": {"reconnectWaitSeconds": 1, "outboundTimeoutMilliseconds": 3000},
            "kafka": {"consumeRetryIntervalMilliseconds": 1000, "group.initial.rebalance.delay.ms": 0}}
KAFKA_STARTUP = "/opt/kafka/bin/kafka-storage.sh format --ignore-formatted --cluster-id MkU3OEVBNTcwNTJENDM2Qk --config /work/kafka.properties && exec /opt/kafka/bin/kafka-server-start.sh /work/kafka.properties"
ROLES = ("qualification-publisher", "qualification-consumer", "qualification-forbidden")
SIDECAR_FIELDS = ("--app-id", "--resources-path", "--config")
KAFKA_TOOL = re.compile(r"/opt/kafka/bin/kafka-[a-z]+(?:-[a-z]+)*[.]sh")
CONTAINER_ID = re.compile(r"[a-f0-9]{64}")


def fail(field, reason):
    """Report a schema field, never its supplied value or private content."""
    raise ValueError("platformConfig." + field + ": " + reason)


def parse_configuration(data: bytes) -> dict:
    """Admit only schema 1 JSON with exact integer bounds and safe provenance."""
    def pairs(items):
        result = {}
        for key, value in items:
            if key in result:
                fail("keys", "duplicate key")
            result[key] = value
        return result
    try:
        if len(data) > 65536:
            fail("document", "configuration exceeds size bound")
        config = json.loads(data.decode("utf-8"), object_pairs_hook=pairs,
                            parse_constant=lambda _: fail("document", "non-finite number"))
    except (UnicodeError, json.JSONDecodeError):
        fail("document", "expected UTF-8 JSON")
    if not isinstance(config, dict) or set(config) - {"schemaVersion", "platformRevision", "platformPath", *BOUNDS}:
        fail("keys", "unknown field or invalid document type")
    if type(config.get("schemaVersion")) is not int or config["schemaVersion"] != 1:
        fail("schemaVersion", "expected integer 1")
    if not isinstance(config.get("platformRevision"), str) or not re.fullmatch(r"[a-f0-9]{40}", config["platformRevision"]):
        fail("platformRevision", "expected full canonical Git revision")
    path = config.get("platformPath")
    if (not isinstance(path, str) or not re.fullmatch(r"[A-Za-z0-9_./-]+[.]json", path)
            or PurePosixPath(path).is_absolute() or any(part in ("", ".", "..") for part in path.split("/"))):
        fail("platformPath", "expected non-escaping relative JSON path")
    for broker, fields in BOUNDS.items():
        tuning = config.get(broker, {})
        if not isinstance(tuning, dict) or set(tuning) - set(fields):
            fail(broker, "unknown field or invalid object type")
        for field, value in tuning.items():
            if type(value) is not int or value not in fields[field]:
                fail(broker + "." + field, "invalid integer or outside experiment bounds")
    return config


def load_configuration(path: Path) -> tuple[bytes, dict]:
    """Reject escaping/symlink inputs before creating any output or resources."""
    path = Path(path)
    if (path.suffix != ".json" or ".." in path.parts
            or any(parent.is_symlink() for parent in (path, *path.parents)) or not path.is_file()):
        fail("file", "expected regular non-escaping JSON file")
    try:
        with path.open("rb") as stream:
            data = stream.read(65537)
    except OSError:
        fail("file", "configuration cannot be read")
    return data, parse_configuration(data)


def settings(config: dict) -> dict:
    """Keep historical defaults when an optional tuning field is absent."""
    return {broker: {**defaults, **config.get(broker, {})} for broker, defaults in DEFAULTS.items()}


def provenance(data: bytes, config: dict) -> dict:
    """Bind exact bytes and declared origin without claiming owner authentication."""
    return {"schemaVersion": 1, "sha256": hashlib.sha256(data).hexdigest(),
            "platformRevision": config["platformRevision"], "platformPath": config["platformPath"],
            "authentication": "unverified-declaration", "settings": settings(config)}


def json_bytes(value):
    return (json.dumps(value, indent=2, sort_keys=True) + "\n").encode()


def component_source(source: str, broker: str, config: dict) -> str:
    """Change only the two admitted component metadata values."""
    tuning = settings(config)[broker]
    old, new = (("name: reconnectWaitSeconds, value: \"1\"", 'name: reconnectWaitSeconds, value: "' + str(tuning["reconnectWaitSeconds"]) + '"')
                if broker == "rabbitmq" else ("name: consumeRetryInterval, value: 1s", "name: consumeRetryInterval, value: " +
                    ("1s" if tuning["consumeRetryIntervalMilliseconds"] == 1000 else str(tuning["consumeRetryIntervalMilliseconds"]) + "ms")))
    if source.count(old) != 1:
        fail("sources", "component tuning marker is missing or ambiguous")
    return source.replace(old, new)


def apply_resiliency(resiliency: dict, broker: str, config: dict) -> None:
    """Bound outbound RabbitMQ calls without changing inbound retries or bounds."""
    milliseconds = settings(config)["rabbitmq"]["outboundTimeoutMilliseconds"]
    if broker == "rabbitmq" and milliseconds != 3000:
        resiliency["spec"]["policies"]["timeouts"]["platformOutbound"] = str(milliseconds) + "ms"
        resiliency["spec"]["targets"]["components"]["pubsub"]["outbound"]["timeout"] = "platformOutbound"


def expected_components(root: Path, config: dict, row: dict) -> dict[str, bytes]:
    """Reconstruct all loaded files from trusted sources and established lab substitutions."""
    broker, role, mutation = row["broker"], row["role"], row["mutation"]
    private = Path(row["privateRoot"])
    if broker not in BOUNDS or role not in ROLES or mutation not in (None, "missing-secret", "tls-negative"):
        fail("renderedInputs", "unlisted broker, role or mutation")
    if not private.is_absolute() or ".." in private.parts or not re.fullmatch(r"https://127[.]0[.]0[.]1:[0-9]{1,5}", row["baoUrl"]):
        fail("renderedInputs", "invalid lab substitution")
    if not 1 <= int(row["baoUrl"].rsplit(":", 1)[1]) <= 65535 or row["kafkaGroup"] not in (None, "qualification-replay"):
        fail("renderedInputs", "invalid lab port or replay group")
    base = root / "deploy/dapr/qualification"
    source = component_source((base / (broker + ".yaml")).read_text(), broker, config)
    if mutation == "missing-secret":
        source = source.replace("name: qualification-" + broker + ",", "name: qualification-absent,")
    if broker == "kafka" and row["kafkaGroup"]:
        source = source.replace("value: qualification-consumer}", "value: qualification-replay}")
    openbao = {"apiVersion": "dapr.io/v1alpha1", "kind": "Component", "metadata": {"name": "openbao"}, "spec": {"type": "secretstores.hashicorp.vault", "version": "v1", "metadata": [{"name": "vaultAddr", "value": row["baoUrl"]}, {"name": "vaultTokenMountPath", "value": str(private / "bao.token")}, {"name": "caCert", "value": str(private / "ca.crt")}, {"name": "skipVerify", "value": "false"}, {"name": "vaultKVPrefix", "value": "dapr"}, {"name": "vaultValueType", "value": "map"}]}, "scopes": ["qualification-publisher", "qualification-consumer"]}
    if mutation == "tls-negative":
        openbao["spec"]["metadata"].append({"name": "tlsServerName", "value": "wrong-host.invalid"})
    result = {"pubsub.yaml": source.encode(), "openbao.yaml": json_bytes(openbao)}
    for index, subscription in enumerate((base / "subscriptions.yaml").read_text().split("\n---\n")):
        result[f"subscription-{index}.yaml"] = subscription.encode()
    result["config.yaml"] = json_bytes({"apiVersion": "dapr.io/v1alpha1", "kind": "Configuration", "metadata": {"name": "qualification"}, "spec": {"secrets": {"scopes": [{"storeName": "openbao", "defaultAccess": "deny", "allowedSecrets": ["qualification-" + broker]}]}}})
    if role == "qualification-consumer":
        result["state.yaml"] = json_bytes({"apiVersion": "dapr.io/v1alpha1", "kind": "Component", "metadata": {"name": "qualification-state"}, "spec": {"type": "state.sqlite", "version": "v1", "metadata": [{"name": "connectionString", "value": str(private / (broker + "-effects.sqlite"))}]}, "scopes": ["qualification-consumer"]})
    resiliency = {"apiVersion": "dapr.io/v1alpha1", "kind": "Resiliency", "metadata": {"name": "qualification"}, "spec": {"policies": {"retries": {"bounded": {"policy": "constant", "duration": "200ms", "maxRetries": 2}}, "timeouts": {"bounded": "3s"}}, "targets": {"components": {"pubsub": {"inbound": {"retry": "bounded", "timeout": "bounded"}, "outbound": {"retry": "bounded", "timeout": "bounded"}}}}}}
    apply_resiliency(resiliency, broker, config)
    result["resiliency.yaml"] = json_bytes(resiliency)
    return result


def kafka_settings(broker_port, controller_port, config, pem):
    """Preserve broker TLS, ACL, durability and pressure settings beside one admitted knob."""
    for value in (broker_port, controller_port):
        if type(value) is not int or not 1 <= value <= 65535:
            fail("renderedInputs", "invalid lab port")
    return {"process.roles": "broker,controller", "node.id": "1", "controller.quorum.voters": f"1@127.0.0.1:{controller_port}", "controller.listener.names": "CONTROLLER", "listeners": f"SSL://0.0.0.0:{broker_port},CONTROLLER://127.0.0.1:{controller_port}", "advertised.listeners": f"SSL://127.0.0.1:{broker_port}", "listener.security.protocol.map": "SSL:SSL,CONTROLLER:SSL", "inter.broker.listener.name": "SSL", "log.dirs": "/var/lib/kafka/data", "offsets.topic.replication.factor": "1", "transaction.state.log.replication.factor": "1", "transaction.state.log.min.isr": "1", "group.initial.rebalance.delay.ms": str(settings(config)["kafka"]["group.initial.rebalance.delay.ms"]), "auto.create.topics.enable": "false", "ssl.keystore.type": "PEM", "ssl.keystore.key": pem("server.key"), "ssl.keystore.certificate.chain": pem("server.crt"), "ssl.truststore.type": "PEM", "ssl.truststore.certificates": pem("ca.crt"), "ssl.client.auth": "required", "authorizer.class.name": "org.apache.kafka.metadata.authorizer.StandardAuthorizer", "super.users": "User:CN=localhost;User:CN=admin", "allow.everyone.if.no.acl.found": "false", "log.retention.check.interval.ms": "1000"}


def properties_bytes(values):
    return ("\n".join(f"{key}={value}" for key, value in values.items()) + "\n").encode()


def sanitize_kafka_properties(data: bytes) -> bytes:
    """Redact only PEM values from the actual measured properties bytes."""
    private_fields = {b"ssl.keystore.key", b"ssl.keystore.certificate.chain", b"ssl.truststore.certificates"}
    seen, result = set(), []
    for line in data.splitlines(keepends=True):
        key, separator, value = line.partition(b"=")
        if key in private_fields:
            if key in seen or not separator:
                fail("renderedInputs", "ambiguous private Kafka property")
            seen.add(key)
            result.append(key + b"=<disposable-pem>\n")
        else:
            result.append(line)
    if seen != private_fields:
        fail("renderedInputs", "missing private Kafka property")
    return b"".join(result)


def kafka_launch(argv: list[str], row: dict, image: str) -> None:
    """Accept only the producer's supported Docker launch grammar and exact startup."""
    if argv[:2] != ["docker", "run"]:
        fail("execution", "Kafka launch is not the supported Docker run")
    options, flags, index = [], [], 2
    valued = {"--cidfile", "--name", "--label", "-p", "-v", "--tmpfs"}
    while index < len(argv) and argv[index].startswith("-"):
        name = argv[index]
        if name == "-d":
            flags.append(name); index += 1
        elif name in valued and index + 1 < len(argv):
            options.append((name, argv[index + 1])); index += 2
        else:
            fail("execution", "Kafka launch has an unsupported option or override")
    if argv[index:] != [image, "/bin/bash", "-ec", KAFKA_STARTUP] or flags != ["-d"]:
        fail("execution", "Kafka image or startup command differs from the selected producer")
    for field in ("--cidfile", "--name", "-p"):
        if sum(name == field for name, value in options) != 1:
            fail("execution", "Kafka launch has ambiguous identity or port options")
    private = row["privateRoot"]
    if ([value for name, value in options if name == "--cidfile"] != [str(Path(private) / "kafka.cid")]
            or [value for name, value in options if name == "-p"] != ["127.0.0.1::" + str(row["brokerPort"]) ]
            or [value for name, value in options if name == "-v"] != [private + ":/work:ro", private + "/kafka:/var/lib/kafka/data"]):
        fail("execution", "Kafka launch has an uncommitted or ambiguous work mount")
    for name, value in options:
        if name == "--tmpfs":
            target = value.split(":", 1)[0].rstrip("/")
            if not target or any(target == base or target.startswith(base + "/") or base.startswith(target + "/")
                                 for base in ("/work", "/var/lib/kafka/data")):
                fail("execution", "Kafka temporary mount overrides committed inputs")


def kafka_administration(argv: list, container: str | None, broker_port) -> bool:
    """Match only the producer's own Kafka admin grammar, run inside the measured broker container.

    Kafka tools take their own `--config` option for topic settings such as `retention.ms`; it is not
    daprd configuration. Every other receipt, including `docker exec` into any other container or path,
    keeps the complete sidecar identification. `--app-id` and `--resources-path` still identify a
    sidecar even inside this grammar.
    """
    return (isinstance(container, str) and CONTAINER_ID.fullmatch(container) is not None and type(broker_port) is int
            and len(argv) > 8 and argv[:3] == ["docker", "exec", container]
            and KAFKA_TOOL.fullmatch(argv[3]) is not None
            and argv[4:8] == ["--bootstrap-server", f"127.0.0.1:{broker_port}", "--command-config", "/work/admin.properties"])


def validate_configuration_packet(directory: Path, config_path: Path | None, commands: list, executed: set[str] | bool) -> None:
    """Verify separate source bytes, every rendered byte and the actual execution path."""
    from dapr_broker_qualification import digest, safe_file, SHA
    executed_brokers = set(BOUNDS) if executed is True else set() if executed is False else executed
    if config_path is None:
        fail("file", "independent verification requires the separate original configuration")
    retained_configuration = safe_file(directory, CONFIGURATION_PATH)
    supplied = Path(config_path)
    supplied_identity = (supplied.stat().st_dev, supplied.stat().st_ino) if supplied.is_file() else None
    packet_identities = {(path.stat().st_dev, path.stat().st_ino) for path in directory.rglob("*") if path.is_file()}
    if supplied.resolve().is_relative_to(directory.resolve()) or supplied_identity in packet_identities:
        fail("file", "independent configuration cannot be a packet file or same-file alias")
    data, config = load_configuration(supplied)
    if retained_configuration.read_bytes() != data:
        fail("configuration", "retained bytes differ from the independent input")
    if json.loads(safe_file(directory, PROVENANCE_PATH).read_text()) != provenance(data, config):
        fail("provenance", "content or declared origin mismatch")
    rows = json.loads(safe_file(directory, RENDERED_PATH).read_text())
    if not isinstance(rows, list):
        fail("renderedInputs", "expected complete render inventory")
    covered, components, kafka = set(), {}, []
    for row in rows:
        index = row.get("renderReceipt")
        if type(index) is not int or not 0 <= index < len(commands):
            fail("renderedInputs", "missing render receipt")
        receipt = commands[index]
        if (receipt.get("argv") != ["CONFIGURATION", "render", row.get("kind")]
                or receipt.get("exitCode") != 0 or json.loads(receipt.get("output", "null")) != row):
            fail("renderedInputs", "render receipt mismatch")
        if row.get("kind") == "components":
            required = {"kind", "broker", "role", "mutation", "kafkaGroup", "privateRoot", "baoUrl", "resourcePath", "path", "files", "renderReceipt"}
            if set(row) != required or row["resourcePath"] in components:
                fail("renderedInputs", "invalid or duplicate component render")
            resource = Path(row["resourcePath"])
            if resource.parent != Path(row["privateRoot"]) or row["path"] != row["broker"] + "/loaded-inputs/" + resource.name:
                fail("renderedInputs", "component path substitution")
            expected = expected_components(directory / "inputs", config, row)
            if row["files"] != {name: hashlib.sha256(value).hexdigest() for name, value in expected.items()}:
                fail("renderedInputs", "requested settings or security invariants differ")
            actual = {str(path.relative_to(directory / row["path"])) for path in (directory / row["path"]).rglob("*") if path.is_file()}
            if actual != set(expected):
                fail("renderedInputs", "unlisted or missing loaded component")
            for name, value in expected.items():
                retained = row["path"] + "/" + name
                if safe_file(directory, retained).read_bytes() != value:
                    fail("renderedInputs", "effective component bytes differ")
                covered.add(retained)
            components[row["resourcePath"]] = row
        elif row.get("kind") == "kafka":
            required = {"kind", "path", "rawPath", "privateRoot", "brokerPort", "controllerPort", "rawSha256", "sha256", "renderReceipt", "hashReceipt", "sanitizationReceipt"}
            if (set(row) != required or kafka or row["path"] != "kafka/effective-inputs/kafka.properties"
                    or row["rawPath"] != str(Path(row["privateRoot"]) / "kafka.properties")
                    or not SHA.fullmatch(row["rawSha256"])):
                fail("renderedInputs", "invalid Kafka render")
            measured = row["hashReceipt"]
            if type(measured) is not int or not 0 <= measured < index:
                fail("execution", "Kafka measured-file receipt is absent")
            measurement = commands[measured]
            if (measurement.get("argv") != ["SHA256", row["rawPath"]] or measurement.get("exitCode") != 0
                    or measurement.get("output") != row["rawSha256"]):
                fail("execution", "Kafka measured file differs from raw commitment")
            sanitized = row["sanitizationReceipt"]
            if type(sanitized) is not int or not measured < sanitized < index:
                fail("execution", "Kafka sanitization receipt is absent or reordered")
            sanitization = commands[sanitized]
            if (sanitization.get("argv") != ["SANITIZE", "kafka-properties", row["rawPath"]]
                    or sanitization.get("exitCode") != 0 or json.loads(sanitization.get("output", "null")) != {
                        "rawSha256": row["rawSha256"], "sanitizedSha256": row["sha256"]}):
                fail("execution", "Kafka sanitization is not bound to the measured raw file")
            expected = properties_bytes(kafka_settings(row["brokerPort"], row["controllerPort"], config, lambda _: "<disposable-pem>"))
            retained = safe_file(directory, row["path"])
            if retained.read_bytes() != expected or digest(retained) != row["sha256"]:
                fail("renderedInputs", "effective Kafka settings differ")
            covered.add(row["path"])
            kafka.append(row)
        else:
            fail("renderedInputs", "unlisted render kind")
    loaded = {str(path.relative_to(directory)) for broker in BOUNDS for folder in ("loaded-inputs", "effective-inputs")
              for path in (directory / broker / folder).rglob("*") if path.is_file()}
    if loaded != covered:
        fail("renderedInputs", "effective input inventory differs")
    launched = set()
    identities_path = directory / "execution-identities.json"
    identities = json.loads(identities_path.read_text()) if identities_path.is_file() else {}
    executable = identities.get("daprdExecutable", {})
    executable_path = executable.get("path")
    profile_path = directory / "inputs/deploy/dapr/qualification/runtime-lock-1.19.1-platform-config-v1.json"
    profile = json.loads(profile_path.read_text()) if profile_path.is_file() else {}
    kafka_container = None
    if kafka and (directory / "kafka/image-identity.json").is_file():
        # The Kafka loop below binds this identity to the measured launch and inspection.
        kafka_identity = json.loads(safe_file(directory, "kafka/image-identity.json").read_text())
        kafka_container = kafka_identity.get("containerId") if isinstance(kafka_identity, dict) else None
    for index, receipt in enumerate(commands):
        argv = receipt["argv"]
        administration = kafka_administration(argv, kafka_container, kafka[0]["brokerPort"] if kafka else None)
        fields = SIDECAR_FIELDS[:2] if administration else SIDECAR_FIELDS
        identified = any(argument == field or argument.startswith(field + "=") for argument in argv[1:] for field in fields)
        if not identified and (argv[0] != executable_path or argv[1:] == ["--version"]):
            continue
        fields = SIDECAR_FIELDS
        measured = executable.get("hashReceipt")
        if (argv[0] != executable_path or type(measured) is not int or not 0 <= measured < index
                or identities.get("preflightPassed") is not True):
            fail("execution", "sidecar executable lacks the measured Dapr preflight binding")
        measurement = commands[measured]
        expected_binary = profile.get("runtime", {}).get("binarySha256")
        if (measurement.get("argv") != ["SHA256", executable_path] or measurement.get("exitCode") != 0
                or measurement.get("output") != expected_binary or identities.get("daprdSha256") != expected_binary):
            fail("execution", "sidecar executable differs from the measured Dapr binary")
        if (any(argument.startswith(field + "=") for argument in argv for field in fields)
                or any(argv.count(field) != 1 for field in fields)):
            fail("execution", "ambiguous or incomplete sidecar configuration")
        try:
            resource = argv[argv.index("--resources-path") + 1]
            row = components[resource]
            if (argv[argv.index("--app-id") + 1] != row["role"] or index <= row["renderReceipt"]
                    or argv[argv.index("--config") + 1] != str(Path(resource) / "config.yaml")):
                fail("execution", "sidecar differs from committed configuration")
        except (KeyError, IndexError):
            fail("execution", "sidecar uses an uncommitted resource path")
        launched.add((row["broker"], row["role"]))
    for row in kafka:
        launches = []
        image = profile.get("images", {}).get("kafka", {}).get("reference")
        for index, receipt in enumerate(commands):
            argv = receipt["argv"]
            if argv[:2] not in (["docker", "run"], ["docker", "create"]):
                continue
            # Other brokers share the same work mount. Identify Kafka from its
            # image, properties use or exact producer container-name suffix.
            named_kafka = any(argument == "--name" and position + 1 < len(argv) and argv[position + 1].endswith("-kafka")
                              for position, argument in enumerate(argv))
            if not named_kafka and not any("kafka.properties" in part or part == image for part in argv[2:]):
                continue
            kafka_launch(argv, row, image)
            if receipt.get("exitCode") != 0:
                if "kafka" in executed_brokers:
                    fail("execution", "Kafka creation failed beside an observed vector")
                continue
            container_id = receipt.get("output", "").strip()
            if not SHA.fullmatch(container_id):
                fail("execution", "Kafka creation lacks its resulting container identity")
            identity = json.loads(safe_file(directory, "kafka/image-identity.json").read_text())
            if identity.get("containerId") != container_id or identity.get("requestedImage") != image:
                fail("execution", "Kafka resulting container differs from its selected creation")
            inspections = []
            for observed_index, observed in enumerate(commands):
                if observed.get("argv") != ["docker", "inspect", container_id] or observed.get("exitCode") != 0:
                    continue
                try:
                    values = json.loads(observed.get("output", "null"))
                except (TypeError, ValueError):
                    continue
                if (isinstance(values, list) and len(values) == 1 and values[0].get("Id") == container_id
                        and values[0].get("Config", {}).get("Image") == image
                        and values[0].get("Image") == identity.get("imageId") and observed_index > index):
                    inspections.append(observed_index)
            if not inspections:
                fail("execution", "Kafka resulting container lacks its native identity observation")
            launches.append(index)
        if not launches and "kafka" in executed_brokers:
            fail("execution", "Kafka configuration lacks a broker execution receipt")
        if any(index <= row["renderReceipt"] for index in launches):
            fail("execution", "Kafka launch precedes configuration commitment")
    if (("kafka" in executed_brokers and not kafka)
            or not {(broker, role) for broker in executed_brokers for role in ROLES[:2]}.issubset(launched)):
        fail("execution", "observed vectors lack their broker's configured publishers and consumers")

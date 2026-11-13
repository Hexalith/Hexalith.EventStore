"""Bounded input, actual producer rendering and independently verified packet controls."""
import copy
import importlib.util
import json
import os
from pathlib import Path
import shutil
import sys
import tempfile
import types
import unittest
from unittest import mock

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from dapr_broker_qualification import digest, evaluate, validate_packet, validate_profile, write_json
from dapr_platform_broker_configuration import (BOUNDS, CONFIGURATION_PATH, PROVENANCE_PATH, RENDERED_PATH,
    load_configuration, parse_configuration, provenance, settings, validate_configuration_packet, KAFKA_STARTUP)
from dapr_runtime_qualification import CANDIDATE_PROFILE, PLATFORM_PROFILE, source_input
import test_dapr_runtime_qualification as historical

ROOT = Path(__file__).resolve().parents[2]
CONFIG = {"schemaVersion": 1, "platformRevision": "a" * 40, "platformPath": "qualification/broker-experiment.json",
          "rabbitmq": {"reconnectWaitSeconds": 2, "outboundTimeoutMilliseconds": 500},
          "kafka": {"consumeRetryIntervalMilliseconds": 200, "group.initial.rebalance.delay.ms": 100}}


def producer():
    spec = importlib.util.spec_from_file_location("platform_broker_runner", ROOT / "tools/qualify-dapr-brokers.py")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


class ConfigurationTests(unittest.TestCase):
    def testEveryPermittedValueAndHistoricalDefaults(self):
        empty = {key: CONFIG[key] for key in ("schemaVersion", "platformRevision", "platformPath")}
        self.assertEqual({"reconnectWaitSeconds": 1, "outboundTimeoutMilliseconds": 3000}, settings(parse_configuration(json.dumps(empty).encode()))["rabbitmq"])
        for broker, fields in BOUNDS.items():
            for field, values in fields.items():
                for value in values:
                    with self.subTest(broker=broker, field=field, value=value):
                        config = copy.deepcopy(empty); config[broker] = {field: value}
                        self.assertEqual(value, settings(parse_configuration(json.dumps(config).encode()))[broker][field])

    def testUnknownDuplicateForbiddenTypesRangesAndProvenanceAreRejectedWithoutValues(self):
        cases = [b'{"schemaVersion":1,"schemaVersion":1}', b'{}', b'[]', b'not-yaml: secret', b'\xff',
                 json.dumps({**CONFIG, "endpoint": "private-secret"}).encode()]
        for broker, fields in BOUNDS.items():
            for field in fields:
                for value in (True, False, None, "private-secret", 1.0, -1, 123456):
                    config = copy.deepcopy(CONFIG); config[broker][field] = value
                    cases.append(json.dumps(config).encode())
            for field in ("initialOffset", "credentials", "publisherConfirm", "skipVerify", "connections.max.reauth.ms", "commands"):
                config = copy.deepcopy(CONFIG); config[broker][field] = "private-secret"
                cases.append(json.dumps(config).encode())
        for field, values in (("platformRevision", ("main", "A" * 40, None)), ("platformPath", ("../escape.json", "/absolute.json", "scripts/run.py", "https://secret.json", "a//b.json")), ("schemaVersion", (True, 2, "1"))):
            for value in values:
                cases.append(json.dumps({**CONFIG, field: value}).encode())
        for data in cases:
            with self.subTest(data=data[:40]), self.assertRaises(ValueError) as error:
                parse_configuration(data)
            self.assertNotIn("private-secret", str(error.exception))

    def testAdmissionRejectsEscapingAndExecutableInputsBeforeOutput(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary); source = root / "config.json"; source.write_text(json.dumps(CONFIG))
            linked = root / "link.json"; linked.symlink_to(source)
            executable = root / "config.py"; executable.write_bytes(source.read_bytes())
            for path in (linked, executable, root / "absent.json", root / "child" / ".." / "config.json"):
                args = types.SimpleNamespace(profile=ROOT / PLATFORM_PROFILE, output=root / "output", platform_config=path)
                with self.subTest(path=path), self.assertRaises(ValueError): producer().Run(args)
                self.assertFalse(args.output.exists())
            source.write_text(json.dumps({**CONFIG, "rabbitmq": {"password": "private-secret"}}))
            with self.assertRaisesRegex(ValueError, "rabbitmq"):
                producer().Run(types.SimpleNamespace(profile=ROOT / PLATFORM_PROFILE, output=root / "output", platform_config=source))
            self.assertFalse((root / "output").exists())

    def testInputRequiresExplicitProfileWithoutOutputSideEffects(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary); source = root / "config.json"; source.write_text(json.dumps(CONFIG))
            for profile, config in ((CANDIDATE_PROFILE, source), ("deploy/dapr/qualification/runtime-lock.json", source), (PLATFORM_PROFILE, None)):
                with self.subTest(profile=profile), self.assertRaisesRegex(ValueError, "required together"):
                    producer().Run(types.SimpleNamespace(profile=ROOT / profile, output=root / "output", platform_config=config))
                self.assertFalse((root / "output").exists())

    def testNewProfileAndBothHistoricalLocksRetainImmutableIdentities(self):
        profile = json.loads((ROOT / PLATFORM_PROFILE).read_text()); validate_profile(profile, ROOT)
        historical_profile = json.loads((ROOT / CANDIDATE_PROFILE).read_text())
        self.assertEqual({key: value for key, value in historical_profile.items() if key != "inputs"},
                         {key: value for key, value in profile.items() if key not in ("inputs", "profileId")})
        self.assertEqual("326a24228313b4f76428f3091bfa449ba00c6a37fc4bdca55fb78409ab433238", digest(ROOT / "deploy/dapr/qualification/runtime-lock.json"))
        self.assertEqual("5bce69c02c7aee8e3ec37003186a7e602ef25adb2c2808137302b0f4a908ca58", digest(ROOT / CANDIDATE_PROFILE))

    def testActualProducerAppliesOnlyPermittedTuningsAndPreservesDefaults(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary); source = root / "config.json"; source.write_text(json.dumps(CONFIG))
            run = producer().Run(types.SimpleNamespace(profile=ROOT / PLATFORM_PROFILE, output=root / "packet", platform_config=source))
            self.addCleanup(shutil.rmtree, run.private_parent, True)
            self.assertEqual(3, run.packet["schemaVersion"])
            self.assertEqual(source.read_bytes(), (run.output / CONFIGURATION_PATH).read_bytes())
            run.bao_url = "https://127.0.0.1:1234"; run.broker_port = 12345
            for name in ("server.key", "server.crt", "ca.crt", "admin.key", "admin.crt"):
                (run.private / name).write_text("disposable-test-pem")
            for broker in ("rabbitmq", "kafka"):
                run.broker = broker
                for role, mutation in (("qualification-consumer", None), ("qualification-publisher", "missing-secret"), ("qualification-publisher", "tls-negative")):
                    path = run.components(role, mutation)
                    text = (path / "pubsub.yaml").read_text()
                    self.assertIn('name: publisherConfirm, value: "true"' if broker == "rabbitmq" else "name: initialOffset, value: oldest", text)
                    self.assertIn('name: reconnectWaitSeconds, value: "2"' if broker == "rabbitmq" else "name: consumeRetryInterval, value: 200ms", text)
                    resilience = json.loads((path / "resiliency.yaml").read_text())
                    self.assertEqual("3s", resilience["spec"]["policies"]["timeouts"]["bounded"])
                    self.assertEqual("platformOutbound" if broker == "rabbitmq" else "bounded", resilience["spec"]["targets"]["components"]["pubsub"]["outbound"]["timeout"])
            for milliseconds, duration in ((250, "250ms"), (500, "500ms"), (1000, "1000ms"), (2000, "2000ms"), (3000, "3s")):
                run.broker = "rabbitmq"
                run.platform_configuration = {**CONFIG, "rabbitmq": {"outboundTimeoutMilliseconds": milliseconds}}
                rendered = json.loads((run.components("qualification-publisher") / "resiliency.yaml").read_text())
                policies = rendered["spec"]["policies"]
                target = rendered["spec"]["targets"]["components"]["pubsub"]
                self.assertEqual(duration, policies["timeouts"][target["outbound"]["timeout"]])
                self.assertEqual({"retry": "bounded", "timeout": "bounded"}, target["inbound"])
                self.assertEqual("3s", policies["timeouts"][target["inbound"]["timeout"]])
                self.assertEqual({"bounded": {"policy": "constant", "duration": "200ms", "maxRetries": 2}}, policies["retries"])
            run.platform_configuration = CONFIG
            # Render rows above deliberately use different experiments. Start one
            # consistent experiment again before checking the complete inventory.
            for row in run.rendered_inputs:
                if row["kind"] == "components":
                    shutil.rmtree(run.output / row["path"])
            run.rendered_inputs.clear(); run.receipts.clear()
            for broker in ("rabbitmq", "kafka"):
                run.broker = broker; run.components("qualification-consumer")
            run.kafka_config()
            self.assertIn("group.initial.rebalance.delay.ms=100\n", (run.private / "kafka.properties").read_text())
            self.assertNotIn("disposable-test-pem", (run.output / "kafka/effective-inputs/kafka.properties").read_text())
            validate_configuration_packet(run.output, source, run.receipts, False)
            run.platform_configuration = {key: CONFIG[key] for key in ("schemaVersion", "platformRevision", "platformPath")}
            for broker in ("rabbitmq", "kafka"):
                run.broker = broker
                path = run.components("qualification-consumer")
                self.assertEqual((ROOT / "deploy/dapr/qualification" / (broker + ".yaml")).read_bytes(), (path / "pubsub.yaml").read_bytes())
                self.assertEqual({"bounded": "3s"}, json.loads((path / "resiliency.yaml").read_text())["spec"]["policies"]["timeouts"])


    def testActualWrittenKafkaPropertiesAreTheSanitizationSource(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary); source = root / "config.json"; source.write_text(json.dumps(CONFIG))
            module = producer()
            run = module.Run(types.SimpleNamespace(profile=ROOT / PLATFORM_PROFILE, output=root / "packet", platform_config=source))
            self.addCleanup(shutil.rmtree, run.private_parent, True)
            run.broker_port = 12345
            for name in ("server.key", "server.crt", "ca.crt", "admin.key", "admin.crt"):
                (run.private / name).write_text("private-key-value-that-must-not-be-retained")
            original = module.kafka_settings
            def substituted(*args):
                result = original(*args); result["ssl.client.auth"] = "none"
                return result
            with mock.patch.object(module, "kafka_settings", side_effect=substituted):
                run.kafka_config()
            retained = (run.output / "kafka/effective-inputs/kafka.properties").read_bytes()
            self.assertIn(b"ssl.client.auth=none\n", retained)
            self.assertNotIn(b"private-key-value", retained)
            self.assertNotIn("private-key-value", json.dumps(run.receipts))
            with self.assertRaisesRegex(ValueError, "effective Kafka settings"):
                validate_configuration_packet(run.output, source, run.receipts, False)

    def testActualProducerPreflightCommitsMeasuredDaprExecutable(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary); source = root / "config.json"; source.write_text(json.dumps(CONFIG))
            module = producer()
            run = module.Run(types.SimpleNamespace(profile=ROOT / PLATFORM_PROFILE, output=root / "packet", platform_config=source, exclusive_worker=None))
            self.addCleanup(shutil.rmtree, run.private_parent, True)
            owner = types.SimpleNamespace(capture=mock.Mock(), endpoint="unix:///synthetic.sock", daemon_id="synthetic", socket_identity=(1, 2), worker_id="synthetic")
            run.inventory = mock.Mock(return_value={}); run.command = mock.Mock(return_value=(0, "1.19.1"))
            run.docker = mock.Mock(); run.certificates = mock.Mock(); run.openbao = mock.Mock()
            run.process = mock.Mock(); run.extract_control = mock.Mock(return_value="/synthetic/control"); run.wait = mock.Mock()
            daprd = root / "daprd"; cli = root / "dapr"
            with mock.patch.object(module, "ExclusiveDocker", return_value=owner), mock.patch.object(module.shutil, "which", return_value=str(cli)), mock.patch.dict(os.environ, {"QUALIFICATION_DAPRD": str(daprd)}), mock.patch.object(module, "digest", side_effect=lambda path: run.profile["runtime"]["binarySha256" if Path(path) == daprd else "cliSha256"]):
                run.prepare()
            executable = run.identities["daprdExecutable"]
            self.assertEqual(str(daprd), executable["path"])
            receipt = run.receipts[executable["hashReceipt"]]
            self.assertEqual(["SHA256", str(daprd)], receipt["argv"])
            self.assertEqual(run.profile["runtime"]["binarySha256"], receipt["output"])
            self.assertTrue(run.identities["preflightPassed"])

    def testCompleteOriginalPacketDoesNotDependOnCurrentPlatformSourceInventory(self):
        control = historical.CandidatePacketTests(); control.setUp(); self.addCleanup(control.doCleanups)
        import dapr_broker_qualification as contract
        actual_digest = contract.digest
        changed = ROOT / "tools/dapr_platform_broker_configuration.py"
        with mock.patch.object(contract, "digest", side_effect=lambda path: "f" * 64 if Path(path) == changed else actual_digest(path)):
            self.assertEqual("PASS", contract.validate_packet(control.fixture.directory)["integrity"])

    def testEstablishedQualificationDiscoveryIncludesPlatformControls(self):
        def flattened(suite):
            for child in suite:
                if isinstance(child, unittest.TestCase):
                    yield child
                else:
                    yield from flattened(child)
        loader = unittest.TestLoader()
        explicit = {case.id() for case in flattened(loader.loadTestsFromModule(sys.modules[__name__]))}
        discovered = {case.id() for case in flattened(loader.discover(str(ROOT / "tools/tests"), pattern="test_dapr*qualification.py"))}
        self.assertTrue(explicit)
        self.assertTrue(explicit.issubset(discovered))


class PlatformPacketTests(unittest.TestCase):
    def setUp(self):
        control = historical.CandidatePacketTests(); control.setUp()
        self.addCleanup(control.doCleanups)
        self.f = control.fixture; f = self.f
        self.source = f.directory.parent / (f.directory.name + "-platform.json")
        self.addCleanup(self.source.unlink, missing_ok=True)
        self.source.write_text(json.dumps(CONFIG))
        profile = json.loads((ROOT / PLATFORM_PROFILE).read_text())
        for name in set(profile["inputs"]) | {PLATFORM_PROFILE}:
            destination = f.directory / "inputs" / name; destination.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(ROOT / name, destination)
        shutil.rmtree(f.directory / "execution-tools")
        for name in profile["inputs"]:
            if name.startswith("tools/") and name.endswith(".py"):
                destination = f.directory / "execution-tools" / name.removeprefix("tools/")
                destination.parent.mkdir(parents=True, exist_ok=True); shutil.copyfile(ROOT / name, destination)
        data = self.source.read_bytes()
        (f.directory / CONFIGURATION_PATH).parent.mkdir(parents=True, exist_ok=True)
        (f.directory / CONFIGURATION_PATH).write_bytes(data)
        write_json(f.directory / PROVENANCE_PATH, provenance(data, CONFIG))
        run = producer().Run.__new__(producer().Run)
        run.output = f.directory; run.retained_inputs = f.directory / "inputs"; run.receipts = f.commands
        run.private = Path(tempfile.mkdtemp(prefix="platform-render-control-")); self.addCleanup(shutil.rmtree, run.private, True)
        run.platform_configuration = CONFIG; run.rendered_inputs = []; run.bao_url = "https://127.0.0.1:1234"; run.broker_port = 12345
        template = {"startedUtc": "2026-11-08T00:00:00+00:00", "endedUtc": "2026-11-08T00:00:01+00:00", "exitCode": 0, "output": ""}
        daprd = "/synthetic/measured/daprd"
        identities_path = f.directory / "execution-identities.json"
        identities = json.loads(identities_path.read_text())
        identities["daprdExecutable"] = {"path": daprd, "hashReceipt": len(f.commands)}
        f.commands.append({**template, "argv": ["SHA256", daprd], "output": profile["runtime"]["binarySha256"]})
        write_json(identities_path, identities)
        for broker in ("rabbitmq", "kafka"):
            run.broker = broker
            for role in ("qualification-publisher", "qualification-consumer"):
                path = run.components(role)
                f.commands.append({**template, "argv": [daprd, "--resources-path", str(path), "--config", str(path / "config.yaml"), "--app-id", role, "--placement-host-address", "127.0.0.1:12345", "--scheduler-host-address", "127.0.0.1:12345"]})
        for command in list(f.commands):
            argv = command["argv"]
            if not any(flag in argv for flag in ("--app-id", "--resources-path")) or "--config" in argv:
                continue
            broker = "rabbitmq" if any("rabbitmq" in arg for arg in argv) else "kafka"
            run.broker = broker
            mutation = "missing-secret" if any("missing-secret" in arg for arg in argv) else "tls-negative" if "--resources-path" in argv else None
            path = run.components("qualification-consumer", mutation)
            if "--resources-path" in argv:
                old = argv[argv.index("--resources-path") + 1]
                for observation_path in f.directory.glob("*/*/observation.json"):
                    observation = json.loads(observation_path.read_text())
                    if observation.get("negativeResourceDirectory") == old:
                        observation["negativeResourceDirectory"] = str(path); write_json(observation_path, observation)
            replacement = {**command, "argv": [daprd, "--resources-path", str(path), "--config", str(path / "config.yaml"), "--app-id", "qualification-consumer", "--placement-host-address", "127.0.0.1:12345", "--scheduler-host-address", "127.0.0.1:12345"]}
            command["argv"] = ["SYNTHETIC", "superseded-sidecar-control"]
            f.commands.append(replacement)
        for name in ("server.key", "server.crt", "ca.crt", "admin.key", "admin.crt"):
            (run.private / name).write_text("disposable-pem")
        run.kafka_config()
        # Exercise the producer's actual container invocation shape without Docker.
        custody = identities["dockerCustody"]
        def docker(*arguments, **kwargs):
            f.commands.append({**template, "argv": ["docker", "--host", custody["endpoint"], "info", "--format", "{{.ID}}"], "output": custody["daemonId"], "socketIdentity": custody["socketIdentity"]})
            argv = ["docker", *map(str, arguments)]
            if argv[1] == "run":
                Path(argv[argv.index("--cidfile") + 1]).write_text("c" * 64)
                output = "c" * 64
            else:
                output = json.dumps([{"Id": "c" * 64, "Image": "sha256:" + "d" * 64, "Config": {"Image": profile["images"]["kafka"]["reference"]}}])
            f.commands.append({**template, "argv": argv, "output": output, "dockerCustody": custody,
                "executedArgv": ["docker", "--host", custody["endpoint"], *argv[1:]], "daemonObservation": len(f.commands) - 1, "workerAdmission": 1})
            return 0, output
        run.docker = docker; run.run_id = "story-3-21-synthetic"; run.containers = []
        container_id = run.container("kafka", profile["images"]["kafka"]["reference"], ["/bin/bash", "-ec", KAFKA_STARTUP],
            ["-p", "127.0.0.1::12345", "-v", str(run.private) + ":/work:ro", "-v", str(run.private / "kafka") + ":/var/lib/kafka/data"])
        run.docker("inspect", container_id)
        write_json(f.directory / "kafka/image-identity.json", {"containerId": container_id, "imageId": "sha256:" + "d" * 64,
            "requestedImage": profile["images"]["kafka"]["reference"], "kind": "disposable-broker-container"})
        f.packet.update(schemaVersion=3, profile=PLATFORM_PROFILE)
        write_json(f.directory / "commands.json", f.commands); f.seal()
        # Later controls drive the producer's own Kafka administration through the same Docker custody.
        run.broker_container = container_id; run.profile = profile; self.run = run; self.daprd = daprd

    def verify(self, **kwargs):
        return validate_packet(self.f.directory, platform_config=self.source, **kwargs)

    def testPositiveSchema3PacketKeepsAll24VectorsAndSeparateAuthority(self):
        result = self.verify()
        self.assertEqual("PASS", result["integrity"]); self.assertEqual("PASS", result["technicalQualification"])
        self.assertEqual("FAIL", result["productionEligibility"]); self.assertIsNone(result["selectedBroker"])
        self.assertEqual(24, sum(len(rows) for rows in self.f.packet["brokers"].values()))
        with self.assertRaisesRegex(ValueError, "authenticated AD-26"): self.verify(require_authority=True)

    def testMissingOrSubstitutedIndependentConfigurationCannotBeResealed(self):
        with self.assertRaisesRegex(ValueError, "separate original"): validate_packet(self.f.directory)
        changed = copy.deepcopy(CONFIG); changed["rabbitmq"]["reconnectWaitSeconds"] = 3
        data = json.dumps(changed).encode()
        (self.f.directory / CONFIGURATION_PATH).write_bytes(data)
        write_json(self.f.directory / PROVENANCE_PATH, provenance(data, changed)); self.f.seal()
        with self.assertRaisesRegex(ValueError, "independent input"): self.verify()

    def testSubstitutedProvenanceSourcesEffectiveInputsAndExecutionAreRejected(self):
        f = self.f
        def rejected(name, data, pattern):
            path = f.directory / name; original = path.read_bytes(); path.write_bytes(data); f.seal()
            with self.assertRaisesRegex(ValueError, pattern): self.verify()
            path.write_bytes(original); f.seal()
        rejected(PROVENANCE_PATH, json.dumps({**provenance(self.source.read_bytes(), CONFIG), "platformRevision": "b" * 40}).encode(), "provenance")
        rejected("inputs/tools/qualify-dapr-brokers.py", b"substituted producer", "source/component")
        row = json.loads((f.directory / RENDERED_PATH).read_text())[0]
        path = row["path"] + "/pubsub.yaml"
        rejected(path, (f.directory / path).read_bytes().replace(b'publisherConfirm, value: "true"', b'publisherConfirm, value: "false"'), "effective component")
        rejected("kafka/effective-inputs/kafka.properties", b"ssl.client.auth=none\n", "Kafka settings")
        commands = copy.deepcopy(f.commands)
        command = next(item for item in commands if "--resources-path" in item["argv"])
        command["argv"][command["argv"].index("--resources-path") + 1] = "/substituted/resources"
        rejected("commands.json", json.dumps(commands).encode(), "uncommitted resource")

    def testMissingRenderedInputsOrProvenanceCannotBeResealed(self):
        f = self.f
        for name in (PROVENANCE_PATH, RENDERED_PATH):
            path = f.directory / name; original = path.read_bytes(); path.unlink(); f.seal()
            with self.subTest(name=name), self.assertRaisesRegex(ValueError, "missing retained"): self.verify()
            path.write_bytes(original); f.seal()
        rows = json.loads((f.directory / RENDERED_PATH).read_text())
        write_json(f.directory / RENDERED_PATH, rows[1:]); f.seal()
        with self.assertRaisesRegex(ValueError, "inventory differs|uncommitted"): self.verify()

    def testInvalidKafkaMeasurementAndLaunchOrderingCannotBeResealed(self):
        f = self.f
        rows = json.loads((f.directory / RENDERED_PATH).read_text())
        kafka = next(row for row in rows if row["kind"] == "kafka")
        for index in (None, kafka["renderReceipt"], len(f.commands) + 1):
            row = copy.deepcopy(kafka); row["hashReceipt"] = index
            f.commands[row["renderReceipt"]]["output"] = json.dumps(row, sort_keys=True)
            write_json(f.directory / RENDERED_PATH, [row if item["kind"] == "kafka" else item for item in rows])
            write_json(f.directory / "commands.json", f.commands); f.seal()
            with self.subTest(index=index), self.assertRaisesRegex(ValueError, "measured-file"): self.verify()

    def testRecomputedRenderedHashesCannotAdmitUnlistedSettings(self):
        f = self.f; rows = json.loads((f.directory / RENDERED_PATH).read_text()); row = rows[0]
        path = f.directory / row["path"] / "pubsub.yaml"
        path.write_bytes(path.read_bytes().replace(b'publisherConfirm, value: "true"', b'publisherConfirm, value: "false"'))
        row["files"]["pubsub.yaml"] = digest(path)
        f.commands[row["renderReceipt"]]["output"] = json.dumps(row, sort_keys=True)
        write_json(f.directory / "commands.json", f.commands); write_json(f.directory / RENDERED_PATH, rows); f.seal()
        with self.assertRaisesRegex(ValueError, "security invariants"): self.verify()

    def testDowngradeMixedProfileAndMissingConfigurationCannotPass(self):
        f = self.f
        for schema, profile in ((2, PLATFORM_PROFILE), (1, PLATFORM_PROFILE), (3, CANDIDATE_PROFILE), (2, CANDIDATE_PROFILE)):
            f.packet.update(schemaVersion=schema, profile=profile); f.seal()
            with self.subTest(schema=schema, profile=profile), self.assertRaises(ValueError): self.verify()
        f.packet.update(schemaVersion=3, profile=PLATFORM_PROFILE); f.seal()
        (f.directory / CONFIGURATION_PATH).unlink(); f.seal()
        with self.assertRaisesRegex(ValueError, "missing retained"): self.verify()

    def testStrippingConfigurationAndRelabelingCaptureStillRejectsPlatformReceipts(self):
        f = self.f
        f.packet.update(schemaVersion=2, profile=CANDIDATE_PROFILE)
        shutil.rmtree(f.directory / "platform-configuration")
        (f.directory / "inputs" / PLATFORM_PROFILE).unlink()
        original_profile = json.loads((ROOT / CANDIDATE_PROFILE).read_text())
        for name, expected in original_profile["inputs"].items():
            shutil.copyfile(source_input(original_profile, ROOT, name, expected), f.directory / "inputs" / name)
        f.seal()
        with self.assertRaisesRegex(ValueError, "Platform capture identity or render receipts"):
            validate_packet(f.directory)

    def testSchema3PreservesCustodyCleanupAndRejectedGuaranteeAdjudication(self):
        f = self.f
        identities = json.loads((f.directory / "execution-identities.json").read_text())
        del identities["dockerCustody"]
        write_json(f.directory / "execution-identities.json", identities); f.seal()
        with self.assertRaisesRegex(ValueError, "custody"): self.verify()

    def testSchema3PreservesCompleteCleanupAndUnrelatedResourceDenominator(self):
        f = self.f
        original = (f.directory / "cleanup.json").read_bytes()
        for field in ("complete", "privateMaterialRemoved", "unrelatedContainersUnchanged"):
            data = json.loads(original); data[field] = False
            write_json(f.directory / "cleanup.json", data); f.seal()
            with self.subTest(field=field), self.assertRaisesRegex(ValueError, "cleanup"): self.verify()
        (f.directory / "cleanup.json").write_bytes(original)
        write_json(f.directory / "workspace-after.json", {}); f.seal()
        with self.assertRaisesRegex(ValueError, "unrelated container"): self.verify()

    def testResealedSourceInventoryAndKafkaMeasurementCannotBeSubstituted(self):
        f = self.f
        lock = f.directory / "inputs" / PLATFORM_PROFILE
        profile = json.loads(lock.read_text())
        source = f.directory / "inputs/tools/qualify-dapr-brokers.py"
        original = source.read_bytes(); source.write_bytes(b"substituted")
        profile["inputs"]["tools/qualify-dapr-brokers.py"] = digest(source)
        write_json(lock, profile); f.seal()
        with self.assertRaisesRegex(ValueError, "current source inventory"): self.verify()
        source.write_bytes(original); shutil.copyfile(ROOT / PLATFORM_PROFILE, lock)
        rows = json.loads((f.directory / RENDERED_PATH).read_text())
        row = next(item for item in rows if item["kind"] == "kafka")
        row["rawSha256"] = "f" * 64
        f.commands[row["renderReceipt"]]["output"] = json.dumps(row, sort_keys=True)
        write_json(f.directory / "commands.json", f.commands); write_json(f.directory / RENDERED_PATH, rows); f.seal()
        with self.assertRaisesRegex(ValueError, "measured file"): self.verify()

    def testPairedRawHashAndMeasurementSubstitutionCannotReplaceSanitization(self):
        f = self.f
        rows = json.loads((f.directory / RENDERED_PATH).read_text())
        row = next(item for item in rows if item["kind"] == "kafka")
        row["rawSha256"] = "e" * 64
        f.commands[row["hashReceipt"]]["output"] = row["rawSha256"]
        f.commands[row["renderReceipt"]]["output"] = json.dumps(row, sort_keys=True)
        write_json(f.directory / RENDERED_PATH, rows); write_json(f.directory / "commands.json", f.commands); f.seal()
        with self.assertRaisesRegex(ValueError, "sanitization is not bound"): self.verify()
        self.assertNotIn("disposable-pem", json.dumps(rows))

    def testIdentifiedSidecarsRequireMeasuredDaprAndCompleteUnambiguousArguments(self):
        f = self.f
        original = copy.deepcopy(f.commands)
        selected = next(index for index, row in enumerate(original) if "--resources-path" in row["argv"])
        base = original[selected]["argv"]
        mutations = [lambda argv: ["/bin/false", *argv[1:]],
                     lambda argv: [*argv, "--resources-path=/substituted"],
                     lambda argv: [*argv, "--config=/substituted"],
                     lambda argv: [*argv, "--app-id=qualification-forbidden"],
                     lambda argv: [*argv, "--resources-path", "/substituted"],
                     lambda argv: [argv[0], "--app-id", "qualification-consumer", "--placement-host-address", "127.0.0.1:12345", "--scheduler-host-address", "127.0.0.1:12345"]]
        for mutation in mutations:
            f.commands = copy.deepcopy(original); f.commands[selected]["argv"] = mutation(base[:])
            write_json(f.directory / "commands.json", f.commands); f.seal()
            with self.subTest(argv=f.commands[selected]["argv"]), self.assertRaisesRegex(ValueError, "sidecar"): self.verify()
        f.commands = original; write_json(f.directory / "commands.json", f.commands); f.seal()
        self.assertEqual("PASS", self.verify()["integrity"])
        # The positive packet also contains nonzero deliberate negative-sidecar exits.
        self.assertTrue(any(row["exitCode"] == 1 and "--resources-path" in row["argv"] for row in f.commands))

    def testKafkaLaunchRequiresSelectedImageStartupMountSuccessAndObservedIdentity(self):
        f = self.f; original = copy.deepcopy(f.commands)
        selected = next(index for index, row in enumerate(original) if row["argv"][:2] == ["docker", "run"])
        image = json.loads((ROOT / PLATFORM_PROFILE).read_text())["images"]["kafka"]["reference"]
        def image_override(row): row["argv"][row["argv"].index(image)] = "busybox:latest"
        def echo_override(row): row["argv"][-1] = "echo kafka-server-start.sh /work/kafka.properties"
        def failed_creation(row): row["exitCode"] = 1
        def no_identity(row): row["output"] = "not-a-container-id"
        def work_override(row):
            position = row["argv"].index(image); row["argv"][position:position] = ["-v", "/other:/work:ro"]
        def mount_override(row):
            position = row["argv"].index(image); row["argv"][position:position] = ["--mount", "type=bind,src=/other,dst=/work"]
        def no_inspect(row):
            for command in f.commands:
                if command["argv"] == ["docker", "inspect", "c" * 64]: command["output"] = "[]"
        for mutation in (image_override, echo_override, failed_creation, no_identity, work_override, mount_override, no_inspect):
            f.commands = copy.deepcopy(original); row = f.commands[selected]; mutation(row)
            row["executedArgv"] = ["docker", "--host", row["dockerCustody"]["endpoint"], *row["argv"][1:]]
            write_json(f.directory / "commands.json", f.commands); f.seal()
            with self.subTest(mutation=mutation.__name__), self.assertRaisesRegex(ValueError, "Kafka"): self.verify()

    def testIndependentConfigurationCannotComeFromPacketOrSameFileAlias(self):
        f = self.f; retained = f.directory / CONFIGURATION_PATH
        inside = f.directory / "independent.json"; inside.write_bytes(self.source.read_bytes()); f.seal()
        alias = self.source.with_name(self.source.stem + "-hardlink.json")
        self.addCleanup(alias.unlink, missing_ok=True); os.link(retained, alias)
        copy_alias = self.source.with_name(self.source.stem + "-copied-hardlink.json")
        self.addCleanup(copy_alias.unlink, missing_ok=True); os.link(inside, copy_alias)
        for source in (retained, inside, alias, copy_alias):
            with self.subTest(source=source), self.assertRaisesRegex(ValueError, "packet file or same-file alias"):
                validate_packet(f.directory, platform_config=source)
        self.assertEqual("PASS", self.verify()["integrity"])

    def testFullStrippingRelabelingCannotAdmitTunedSchema3AsHistorical(self):
        f = self.f
        f.packet.update(schemaVersion=2, profile=CANDIDATE_PROFILE)
        shutil.rmtree(f.directory / "platform-configuration")
        (f.directory / "inputs" / PLATFORM_PROFILE).unlink()
        original_profile = json.loads((ROOT / CANDIDATE_PROFILE).read_text())
        for name, expected in original_profile["inputs"].items():
            shutil.copyfile(source_input(original_profile, ROOT, name, expected), f.directory / "inputs" / name)
        for row in f.commands:
            if row["argv"][:2] == ["CONFIGURATION", "render"]:
                row["argv"][0] = "RELABELED"
        write_json(f.directory / "commands.json", f.commands); f.seal()
        with self.assertRaisesRegex(ValueError, "historical interpretation contains Platform effective"): validate_packet(f.directory)
        shutil.rmtree(f.directory / "kafka/effective-inputs"); f.seal()
        with self.assertRaisesRegex(ValueError, "historical interpretation contains tuned loaded"): validate_packet(f.directory)

    def testHistoricalLoadedTemplatesKeepEstablishedSecretAndReplayMutations(self):
        from dapr_broker_qualification import validate_historical_effective_inputs
        f = self.f
        for broker in ("rabbitmq", "kafka"):
            shutil.rmtree(f.directory / broker / "loaded-inputs")
        shutil.rmtree(f.directory / "kafka/effective-inputs")
        for broker in ("rabbitmq", "kafka"):
            source = (f.directory / "inputs/deploy/dapr/qualification" / (broker + ".yaml")).read_bytes()
            variants = [source, source.replace(("name: qualification-" + broker + ",").encode(), b"name: qualification-absent,")]
            if broker == "kafka": variants += [value.replace(b"value: qualification-consumer}", b"value: qualification-replay}") for value in list(variants)]
            for index, value in enumerate(variants):
                path = f.directory / broker / "loaded-inputs" / str(index) / "pubsub.yaml"
                path.parent.mkdir(parents=True); path.write_bytes(value)
        validate_historical_effective_inputs(f.directory)

    def testObservedRejectedGuaranteesStayFailAndCannotBeCountedAsPassingCapture(self):
        f = self.f
        # Reuse actual producer/evaluator regression semantics while retaining the schema-3 packet.
        for broker, scenario in (("rabbitmq", "secret-rotation"), ("rabbitmq", "cancellation-backpressure"), ("rabbitmq", "missing-bindings"), ("kafka", "secret-rotation"), ("kafka", "cancellation-backpressure")):
            path = f.directory / broker / scenario / "observation.json"
            observation = json.loads(path.read_text())
            if scenario == "missing-bindings":
                observation["unboundPublishStatus"] = 204
                message_id = observation["unboundMessageId"]
            elif scenario == "secret-rotation":
                observation["oldGenerationDenied"] = False; observation["stalePublishStatus"] = 599
                message_id = observation["staleMessageId"]
                index = observation["denialObservation"]["publicationReceipt"]
                f.commands[index].update(httpStatus=599, output="timeout")
                for row in observation["publicationReconciliation"]:
                    if row["id"] == message_id: row["httpStatus"] = 599
            else:
                observation["negativeConfirmRejected"] = False
                for index in observation["rejectedConfirmReceipts"]:
                    f.commands[index]["output"] = "{}"
                observation["rejectedConfirmReceipts"] = []
                message_id = None
            if scenario == "missing-bindings":
                for command in f.commands:
                    if command.get("input", {}).get("id") == message_id: command["httpStatus"] = 204
            self.assertFalse(evaluate(scenario, observation))
            write_json(path, observation); f.packet["brokers"][broker][scenario]["result"] = "FAIL"
        write_json(f.directory / "commands.json", f.commands); f.seal()
        result = self.verify(allow_failures=True)
        self.assertEqual("FAIL", result["technicalQualification"]); self.assertEqual(5, len(result["failedScenarios"]))
        with self.assertRaisesRegex(ValueError, "technical qualification FAIL"): self.verify()

    def administered_packet(self):
        """Append the producer's actual Kafka topic/ACL receipts; return them with the sealed commands."""
        start = len(self.f.commands)
        self.run.kafka_topics()
        write_json(self.f.directory / "commands.json", self.f.commands); self.f.seal()
        return [index for index in range(start, len(self.f.commands))
                if self.f.commands[index]["argv"][3:4] == ["/opt/kafka/bin/kafka-topics.sh"]]

    def rejected(self, index, argv, pattern="sidecar"):
        f = self.f; original = copy.deepcopy(f.commands)
        row = f.commands[index]; row["argv"] = argv
        if "dockerCustody" in row:
            row["executedArgv"] = ["docker", "--host", row["dockerCustody"]["endpoint"], *argv[1:]]
        write_json(f.directory / "commands.json", f.commands); f.seal()
        try:
            with self.assertRaisesRegex(ValueError, pattern): self.verify()
        finally:
            f.commands = original; write_json(f.directory / "commands.json", f.commands); f.seal()

    def testProducerKafkaTopicCreationConfigIsNotIdentifiedAsSidecarLaunch(self):
        created = self.administered_packet()
        self.assertEqual(7, len(created))
        for index in created:
            argv = self.f.commands[index]["argv"]
            # Attempt-2 receipt 1079 shape, which the pinned 02a4d3b2 rule read as a daprd launch.
            self.assertEqual(["docker", "exec", self.run.broker_container, "/opt/kafka/bin/kafka-topics.sh",
                              "--bootstrap-server", "127.0.0.1:12345", "--command-config", "/work/admin.properties", "--create"], argv[:9])
            self.assertEqual(2, argv.count("--config")); self.assertNotEqual(self.daprd, argv[0])
        self.assertEqual("PASS", self.verify()["integrity"])

    def testUnmeasuredExecutablesAndMisplacedSidecarFlagsStillFail(self):
        topic = self.administered_packet()[0]
        launch = next(index for index, row in enumerate(self.f.commands)
                      if row["argv"][0] == self.daprd and "--resources-path" in row["argv"])
        sidecar, admin = self.f.commands[launch]["argv"], self.f.commands[topic]["argv"]
        container = self.run.broker_container
        # Carry the measured control-plane endpoints so the independent control-plane check passes
        # and the configuration execution check itself must reject the launch.
        endpoints = [value for field in ("--placement-host-address", "--scheduler-host-address")
                     for value in (field, sidecar[sidecar.index(field) + 1])]
        cases = {
            "unmeasured executable with all three flags": (launch, ["/tmp/unmeasured/daprd", *sidecar[1:]]),
            "unmeasured executable with only --config": (launch, ["/usr/local/bin/unmeasured", "--config", sidecar[sidecar.index("--config") + 1]]),
            "measured sidecar run through the Kafka container": (topic, ["docker", "exec", container, *sidecar]),
            "--app-id inside Kafka administration": (topic, [*admin, "--app-id", "qualification-consumer", *endpoints]),
            "--resources-path inside Kafka administration": (topic, [*admin, "--resources-path", "/substituted", *endpoints]),
            "--config in another container": (topic, [*admin[:2], "e" * 64, *admin[3:]]),
            "--config from an executable outside the Kafka tools": (topic, [*admin[:3], "/work/kafka-topics.sh", *admin[4:]]),
            "--config against another listener": (topic, [*admin[:5], "127.0.0.1:1", *admin[6:]]),
            "--config without the admin credentials": (topic, [*admin[:7], "/work/other.properties", *admin[8:]]),
        }
        for name, (index, argv) in cases.items():
            with self.subTest(name):
                self.rejected(index, argv, "sidecar executable lacks the measured Dapr preflight binding")
        self.assertEqual("PASS", self.verify()["integrity"])

    def testDuplicatedOrAssignmentFormSidecarFlagsStillFail(self):
        topic = self.administered_packet()[0]
        launch = next(index for index, row in enumerate(self.f.commands)
                      if row["argv"][0] == self.daprd and "--resources-path" in row["argv"])
        sidecar, admin = self.f.commands[launch]["argv"], self.f.commands[topic]["argv"]

        def assigned(field):
            position = sidecar.index(field)
            return [*sidecar[:position], field + "=" + sidecar[position + 1], *sidecar[position + 2:]]
        cases = {"duplicated " + field: (launch, [*sidecar, field, sidecar[sidecar.index(field) + 1]])
                 for field in ("--app-id", "--resources-path", "--config")}
        cases.update({field + "= form": (launch, assigned(field)) for field in ("--app-id", "--resources-path", "--config")})
        cases.update({
            "--app-id= inside Kafka administration": (topic, [*admin, "--app-id=qualification-consumer"]),
            "--resources-path= inside Kafka administration": (topic, [*admin, "--resources-path=/substituted"]),
            "--config= in another container": (topic, [*admin[:2], "e" * 64, *admin[3:8], "--config=retention.ms=1"]),
        })
        for name, (index, argv) in cases.items():
            with self.subTest(name):
                self.rejected(index, argv, "ambiguous or incomplete sidecar configuration" if index == launch
                              else "sidecar executable lacks the measured Dapr preflight binding")
        self.assertEqual("PASS", self.verify()["integrity"])


if __name__ == "__main__":
    unittest.main()

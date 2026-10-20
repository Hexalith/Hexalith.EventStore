"""Synthetic verifier fixtures only; these are never retained as live proof."""
import copy
import json
from pathlib import Path
import shutil
import sys
import tempfile
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from dapr_broker_qualification import BROKERS, SCENARIOS, digest, validate_packet, write_json

ROOT = Path(__file__).resolve().parents[2]


class BrokerQualificationTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.directory = Path(self.temporary.name)
        profile = json.loads((ROOT / "deploy/dapr/qualification/runtime-lock.json").read_text())
        files = set(profile["inputs"]) | {"deploy/dapr/qualification/runtime-lock.json"}
        files.update(x["manifest"]["path"] for x in profile["images"].values())
        for name in files:
            destination = self.directory / "inputs" / name
            destination.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(ROOT / name, destination)
        self.profile = profile
        self.packet = {"schemaVersion": 1, "productionEligible": False, "selectedBroker": None, "brokers": {}}
        self.commands = [{"argv": ["synthetic-fixture"], "startedUtc": "2026-10-20T00:00:00+00:00", "endedUtc": "2026-10-20T00:00:01+00:00", "exitCode": 0, "output": "synthetic only"}]
        self.commands.append({"argv": ["docker", "inspect", "unrelated-container"], "startedUtc": "2026-10-20T00:00:00+00:00", "endedUtc": "2026-10-20T00:00:01+00:00", "exitCode": 0, "output": json.dumps({"unrelated-container": "same-spec-hash"}, sort_keys=True)})
        write_json(self.directory / "execution-identities.json", {"stage": "local-docker-first-stage", "targetProof": False, "preflightPassed": True, "daprdSha256": profile["runtime"]["binarySha256"], "cliSha256": profile["runtime"]["cliSha256"]})
        write_json(self.directory / "workspace-before.json", {"unrelated-container": "same-spec-hash"})
        write_json(self.directory / "workspace-after.json", {"unrelated-container": "same-spec-hash"})
        write_json(self.directory / "cleanup.json", {"ownedResourcesRemaining": [], "unrelatedContainersUnchanged": True})
        for broker in BROKERS:
            self.packet["brokers"][broker] = {}
            for scenario in SCENARIOS:
                self.scenario(broker, scenario)
        write_json(self.directory / "commands.json", self.commands)
        self.seal()

    def tearDown(self):
        self.temporary.cleanup()

    def scenario(self, broker, scenario):
        event_id = broker + "-" + scenario
        observation = {
            "broker": broker, "scenario": scenario, "messageId": event_id,
            "receiptCount": 3, "effectCount": 1, "crashExitCode": 86, "sameId": True,
            "beforeContainerId": "same", "afterContainerId": "same", "publishedWhileOffline": True,
            "poisonReceipts": 0, "quarantineEffects": 0, "survivedRestart": True,
            "denialObserved": True, "sourceRecoveredAfterRestart": True,
            "controlEffects": 1, "expiredMessageId": "expired", "expiredEffects": 0,
            "outageSeconds": 3, "brokerRetentionObserved": True, "allowedEffects": 1,
            "forbiddenAppStatus": 403, "forbiddenTopicStatus": 403, "forbiddenSubscriptions": 0,
            "negativeReady": False, "negativeExitCode": 1, "diagnosticMatched": True,
            "oldGenerationDenied": True, "newGenerationEffects": 1, "controlledRestart": True,
            "cancelled": True, "boundedSeconds": 1, "negativeConfirmRejected": True, "acknowledgedLoss": 0,
            "unboundPublishStatus": 500, "bindingAbsent": True,
        }
        route = "/quarantine" if scenario == "durable-quarantine" else "/events"
        receipts = [{"id": event_id, "messageId": event_id, "kind": "receipt", "route": "/poison" if scenario == "durable-quarantine" else route} for _ in range(3)]
        effects = [{"id": event_id, "messageId": event_id, "kind": "effect", "route": route}]
        if scenario == "durable-quarantine":
            observation.update({"poisonReceipts": 3, "quarantineEffects": 1})
        if scenario == "failed-diversion":
            effects = []
            for key in ("effectCount", "allowedEffects", "controlEffects", "newGenerationEffects"):
                observation[key] = 0
        body = {"kind": "persisted-application-http-readback", "stateComponent": "qualification-state", "stateReadback": {"status": 200, "value": effects, "etag": "synthetic"}, "ledger": receipts + effects}
        readback = {**body, "commandReceipt": len(self.commands)}
        self.commands.append({"argv": ["HTTP", "GET", "http://127.0.0.1:1234/readback"], "startedUtc": "2026-10-20T00:00:00+00:00", "endedUtc": "2026-10-20T00:00:01+00:00", "exitCode": 0, "httpStatus": 200, "output": json.dumps(body)})
        diagnostic = {"kind": "broker-administration-diagnostic", "exitCode": 0, "output": "synthetic diagnostic", "commandReceipt": len(self.commands)}
        self.commands.append({"argv": ["docker", "exec", "owned-synthetic", "broker-diagnostic"], "startedUtc": "2026-10-20T00:00:00+00:00", "endedUtc": "2026-10-20T00:00:01+00:00", "exitCode": 0, "output": diagnostic["output"]})
        path = f"{broker}/{scenario}/"
        write_json(self.directory / path / "observation.json", observation)
        write_json(self.directory / path / "application-readback.json", readback)
        write_json(self.directory / path / "broker-diagnostics.json", diagnostic)
        self.packet["brokers"][broker][scenario] = {"result": "PASS", "observation": path + "observation.json", "applicationReadback": path + "application-readback.json", "brokerDiagnostics": path + "broker-diagnostics.json"}

    def seal(self):
        self.packet["artifacts"] = {str(path.relative_to(self.directory)): digest(path) for path in self.directory.rglob("*") if path.is_file() and path != self.directory / "packet.json"}
        write_json(self.directory / "packet.json", self.packet)

    def change(self, name, mutation):
        path = self.directory / name
        value = json.loads(path.read_text())
        mutation(value)
        write_json(path, value)
        self.seal()

    def testPositiveSyntheticPacketVerifiesWithoutProductionAuthority(self):
        result = validate_packet(self.directory)
        self.assertEqual("PASS", result["technicalQualification"])
        self.assertEqual("FAIL", result["productionEligibility"])

    def testMissingOwnerBindingCannotAuthorizeEvenWithSelfDeclaredRecords(self):
        self.packet["authority"] = {"ad26": {"approved": True}, "publicationRecord": {"approved": True}}
        self.seal()
        with self.assertRaisesRegex(ValueError, "authenticated AD-26"):
            validate_packet(self.directory, require_authority=True)

    def testBelowFloorRuntimeFailsAfterResealing(self):
        self.change("inputs/deploy/dapr/qualification/runtime-lock.json", lambda p: p["runtime"].update(version="1.18.1"))
        with self.assertRaisesRegex(ValueError, "runtime.version"):
            validate_packet(self.directory)

    def testMutableImageFailsAfterResealing(self):
        self.change("inputs/deploy/dapr/qualification/runtime-lock.json", lambda p: p["images"]["scheduler"].update(reference="ghcr.io/dapr/scheduler:1.18.7"))
        with self.assertRaisesRegex(ValueError, "mutable image"):
            validate_packet(self.directory)

    def testWrongBinaryFailsAfterResealing(self):
        self.change("execution-identities.json", lambda p: p.update(daprdSha256="0" * 64))
        with self.assertRaisesRegex(ValueError, "wrong loaded binary"):
            validate_packet(self.directory)

    def testMissingScenarioFails(self):
        del self.packet["brokers"]["kafka"]["failed-diversion"]
        self.seal()
        with self.assertRaisesRegex(ValueError, "incomplete scenario"):
            validate_packet(self.directory)

    def testContradictoryReadbackFailsEvenWithUpdatedHttpReceiptAndHashes(self):
        name = "rabbitmq/crash-redelivery/application-readback.json"
        readback = json.loads((self.directory / name).read_text())
        readback["ledger"] = [r for r in readback["ledger"] if r["kind"] != "effect"]
        readback["stateReadback"]["value"] = []
        self.commands[readback["commandReceipt"]]["output"] = json.dumps({k: v for k, v in readback.items() if k != "commandReceipt"})
        write_json(self.directory / name, readback)
        write_json(self.directory / "commands.json", self.commands)
        self.seal()
        with self.assertRaisesRegex(ValueError, "contradicts persisted readback"):
            validate_packet(self.directory)

    def testFalseOutcomeFails(self):
        self.change("kafka/missing-bindings/observation.json", lambda p: p.update(unboundPublishStatus=204))
        with self.assertRaisesRegex(ValueError, "false result"):
            validate_packet(self.directory)

    def testMissingEvidenceFails(self):
        (self.directory / "rabbitmq/broker-restart/broker-diagnostics.json").unlink()
        with self.assertRaisesRegex(ValueError, "missing retained file"):
            validate_packet(self.directory)

    def testBrokerDiagnosticContradictionFailsAfterResealing(self):
        self.change("kafka/retention-expiry/broker-diagnostics.json", lambda p: p.update(exitCode=1))
        with self.assertRaisesRegex(ValueError, "broker diagnostic contradicts"):
            validate_packet(self.directory)

    def testCleanupTrueCannotHideChangedUnrelatedSpecification(self):
        self.change("workspace-after.json", lambda p: p.update({"unrelated-container": "changed-spec"}))
        with self.assertRaisesRegex(ValueError, "unrelated container specifications changed"):
            validate_packet(self.directory)

    def testTruthfulBlockedRowsVerifyOnlyAsNegativeEvidence(self):
        self.change("kafka/missing-bindings/observation.json", lambda p: p.update(blockedBy="synthetic infrastructure unavailable"))
        self.packet["brokers"]["kafka"]["missing-bindings"]["result"] = "FAIL"
        self.seal()
        self.assertEqual("FAIL", validate_packet(self.directory, allow_failures=True)["technicalQualification"])
        with self.assertRaisesRegex(ValueError, "technical qualification FAIL"):
            validate_packet(self.directory)

    def testUnlistedFileFails(self):
        (self.directory / "extra.log").write_text("unbound")
        with self.assertRaisesRegex(ValueError, "unlisted"):
            validate_packet(self.directory)

    def testEscapingPathFails(self):
        self.packet["artifacts"]["../outside"] = "0" * 64
        write_json(self.directory / "packet.json", self.packet)
        with self.assertRaisesRegex(ValueError, "escaping retained path"):
            validate_packet(self.directory)

    def testProductionSelectionFails(self):
        self.packet["selectedBroker"] = "rabbitmq"
        self.seal()
        with self.assertRaisesRegex(ValueError, "fabricated production authority"):
            validate_packet(self.directory)


if __name__ == "__main__":
    unittest.main()

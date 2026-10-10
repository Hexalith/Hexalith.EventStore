"""Executor tooling controls; no fixture in this suite grants operational qualification."""
import base64
import copy
import json
import os
import signal
import threading
import time
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest import mock

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import p1r_published_executor as executor
import p1r_published_qualification as qualification
import p1r_qualification as preparation
import p1r_qualification_runtime as runtime


class MeasurementTests(unittest.TestCase):
    def testRestoreReceiptAttributesBackupAndDatabaseCreationToTheirCommands(self):
        with tempfile.TemporaryDirectory() as scratch, mock.patch.object(executor, "reserve_port", return_value=15432):
            worker = CleanupTests.worker(self, scratch)
            worker.redis = "source-id"
            worker.postgres_password = "fixture-password"
            worker.inputs = {"operational_profile": {}, "candidate": {"packages": []}}

            def run(argv, **kwargs):
                output = b""
                if argv[:2] == ["docker", "inspect"]:
                    name = argv[-1]
                    output = (json.dumps({"Id": "restored-id", "Name": "/" + name,
                        "Config": {"Labels": {"hexalith.p1r.invocation": worker.invocation}}}) + "\n").encode()
                elif argv[:2] == ["docker", "cp"] and ":/tmp/" in str(argv[2]):
                    Path(argv[3]).write_bytes(b"synthetic-backup")
                worker.record(argv, worker.scratch, executor.stamp(), 0, output)
                return output

            def inventory(*args):
                run(["synthetic-inventory"])
                return []

            def actor(*args):
                run(["synthetic-actor"])
                return {"accepted": True, "event_count": 1}

            worker.run = run
            worker.inventory = inventory
            worker.actor = actor
            worker.seed = worker.mutate = worker.stop_nodes = mock.Mock()
            worker.start_nodes = mock.Mock(side_effect=lambda *args, **kwargs: [
                {"pid": 1, "start_ticks": 1, "pgid": 1, "session": 1}])

            worker.restore_case("synthetic-restore", executor.Measurements())
            commands = {row["step"]: row for row in worker.pending_restore["commands"]}
            self.assertEqual(commands["backup"]["argv"][:4], ["docker", "exec", "source-id", "pg_dump"])
            self.assertEqual(commands["create-database"]["argv"], ["docker", "start", "restored-id"])
            self.assertEqual(commands["backup"]["output_sha256"], executor.digest(b"synthetic-backup"))
            for step in ("backup", "create-database"):
                recorded = next(row for row in worker.commands if row["argv"] == commands[step]["argv"])
                for field in ("cwd", "started_utc", "finished_utc", "exit_code"):
                    self.assertEqual(commands[step][field], recorded[field])

    def testHttpCommandRecordClampsEarlierFinish(self):
        worker = executor.Executor.__new__(executor.Executor)
        worker.commands, worker.configurations = [], []
        worker.save = mock.Mock()
        started = "2026-10-10T12:00:00.001300+00:00"
        with mock.patch.object(executor, "stamp", return_value="2026-10-10T12:00:00+00:00"):
            row = worker.record(["HTTP", "GET", "http://127.0.0.1:1/ready"], Path("/tmp"), started, 0, b"ready")
        self.assertEqual(row["finished_utc"], started)
        preparation.validate_times(row)

    def testPostgresqlV1InventoryKeepsActorJsonAndDecodesBookkeeping(self):
        identity = "a" * 64
        observed = {"Id": identity, "Config": {"Labels": {"hexalith.p1r.invocation": "owned"}}}
        rows = [
            ["eventstore||AggregateActor||tenant-a:counter:fixture||tenant-a:counter:fixture:events:12",
             {"sequenceNumber": 12}],
            ["eventstore||AggregateActor||tenant-a:counter:fixture||tenant-a:counter:fixture:metadata",
             {"currentSequence": 12, "retainedFloor": 5}],
            ["eventstore||tenant-a:message:status", "eyJzdGF0dXMiOjV9"],
        ]
        with mock.patch.object(executor.subprocess, "check_output", side_effect=[
                (json.dumps(observed) + "\n").encode(), (json.dumps(rows) + "\n").encode()]):
            inventory = executor.postgres_inventory(identity, "owned")
        self.assertEqual([row["kind"] for row in inventory], ["event", "metadata", "bookkeeping"])
        self.assertEqual(inventory[1]["floor"], 5)
        self.assertEqual(inventory[0]["sha256"], executor.digest(executor.canonical({"sequenceNumber": 12})))
        self.assertEqual(executor.postgres_value("eyJzdGF0dXMiOjV9"), b'{"status":5}')

    def testPostgresqlMutationRetainsJsonObjectShape(self):
        worker = executor.Executor.__new__(executor.Executor)
        worker.containers = {"owned": {"image": executor.POSTGRES}}
        worker.run = mock.Mock(return_value=b"UPDATE 1\n")
        worker.postgres_write("owned", "tenant-a:counter:fixture:metadata", {"currentSequence": 12, "retainedFloor": 5})
        sql = worker.run.call_args.kwargs["input_bytes"].decode()
        self.assertIn(executor.canonical({"currentSequence": 12, "retainedFloor": 5}).hex(), sql)
        self.assertIn("::jsonb", sql)

    def testPostgresqlInventoryNormalizesDatabaseCollationOrder(self):
        identity = "a" * 64
        observed = {"Id": identity, "Config": {"Labels": {"hexalith.p1r.invocation": "owned"}}}
        rows = [["eventstore||eventstore:reminders:v1:control", {}],
                ["eventstore||GlobalPositionActor||global||current-global-position", {}]]
        with mock.patch.object(executor.subprocess, "check_output", side_effect=[
                (json.dumps(observed) + "\n").encode(), (json.dumps(rows) + "\n").encode()]):
            inventory = executor.postgres_inventory(identity, "owned")
        self.assertEqual([row["key"] for row in inventory], sorted(row[0] for row in rows))

    def testPostgresqlInventoryRejectsMalformedStreamState(self):
        identity = "a" * 64
        observed = {"Id": identity, "Config": {"Labels": {"hexalith.p1r.invocation": "owned"}}}
        for suffix in (":metadata", ":snapshot"):
            with self.subTest(suffix), mock.patch.object(executor.subprocess, "check_output", side_effect=[
                    (json.dumps(observed) + "\n").encode(),
                    (json.dumps([["eventstore||AggregateActor||tenant-a:counter:fixture||tenant-a:counter:fixture" + suffix,
                                  ["malformed"]]]) + "\n").encode()]):
                with self.assertRaisesRegex(preparation.InvalidPacket, "invalid persisted .* content"):
                    executor.postgres_inventory(identity, "owned")

    def testCandidateTrustedEffectsRequirePersistedAudit(self):
        for operation, observed, check_id in (
                ("trusted-effect", {"accepted": True, "sequence": 13, "replayed": True},
                 "candidate-trusted-effect-audit-persisted"),
                ("unauthorized-effect", {"accepted": False, "sequence": 12, "unexpected": False,
                                         "unauthorized_refused": True, "denial": "invalid-gateway-proof"},
                 "candidate-unauthorized-effect-audit-persisted")):
            with self.subTest(operation):
                worker = executor.Executor.__new__(executor.Executor)
                worker.redis, worker.host_port = "owned", 12345
                with mock.patch.object(worker, "probe", return_value={"actor_methods": ["ProcessTrustedEffectAsync"]}), \
                     mock.patch.object(worker, "seed"), mock.patch.object(worker, "inventory", side_effect=[[], []]), \
                     mock.patch.object(worker, "start_nodes"), mock.patch.object(worker, "stop_nodes"), \
                     mock.patch.object(worker, "http", return_value=observed), \
                     mock.patch.object(worker, "postgres_audits", return_value=[]):
                    checks = executor.Measurements()
                    worker.mixed_case("3.119.0-" + operation, checks)
                self.assertIn({"id": check_id, "passed": False}, checks.checks)

    def testCandidateAuditMatchRequiresExactlyOneMatchingRecord(self):
        trusted = {"action": "submission", "tenant": "tenant-a", "effectid": "effect-1", "workload": "p1r-fixture",
                   "purpose": "published-qualification", "disposition": "authorized"}
        denied = dict(trusted, action="gateway-proof", effectid=None, disposition="denied")
        observed = {"trusted-effect": {"accepted": True, "sequence": 13, "replayed": True},
                    "unauthorized-effect": {"accepted": False, "sequence": 12, "unexpected": False,
                                            "unauthorized_refused": True, "denial": "invalid-gateway-proof"}}
        for operation, records, passed in (
                ("trusted-effect", [trusted], True),
                ("trusted-effect", [trusted, dict(trusted)], False),
                ("trusted-effect", [dict(trusted, action="gateway-proof")], False),
                ("trusted-effect", [dict(trusted, disposition="denied")], False),
                ("trusted-effect", [dict(trusted, tenant="tenant-b")], False),
                ("trusted-effect", [dict(trusted, workload="other-workload")], False),
                ("trusted-effect", [dict(trusted, purpose="other-purpose")], False),
                ("trusted-effect", [dict(trusted, effectid=None)], False),
                ("unauthorized-effect", [denied], True),
                ("unauthorized-effect", [dict(denied, effectid="effect-1")], False),
                ("unauthorized-effect", [trusted], False)):
            with self.subTest(operation=operation, records=records):
                worker = executor.Executor.__new__(executor.Executor)
                worker.redis, worker.host_port = "owned", 12345
                with mock.patch.object(worker, "probe", return_value={"actor_methods": ["ProcessTrustedEffectAsync"]}), \
                     mock.patch.object(worker, "seed"), mock.patch.object(worker, "inventory", side_effect=[[], []]), \
                     mock.patch.object(worker, "start_nodes"), mock.patch.object(worker, "stop_nodes"), \
                     mock.patch.object(worker, "http", return_value=observed[operation]), \
                     mock.patch.object(worker, "postgres_audits",
                                       return_value=[{"key": "audit", "record": record} for record in records]):
                    checks = executor.Measurements()
                    worker.mixed_case("3.119.0-" + operation, checks)
                self.assertIn({"id": "candidate-" + operation + "-audit-persisted", "passed": passed}, checks.checks)

    def testPostgresqlAuditsDecodeDaprBinaryRowsAndFoldFieldNames(self):
        worker = executor.Executor.__new__(executor.Executor)
        worker.containers = {"owned": {"image": executor.POSTGRES}}
        rows = [["eventstore||p1r-qualification-audit-1",
                 base64.b64encode(json.dumps({"action": "submission", "effectId": "effect-1"}).encode()).decode()],
                ["eventstore||p1r-qualification-audit-2", base64.b64encode(b"not-json").decode()]]
        worker.run = mock.Mock(return_value=json.dumps(rows).encode())
        audits = worker.postgres_audits("owned")
        self.assertEqual([row["record"] for row in audits], [{"action": "submission", "effectid": "effect-1"}, {}])
        self.assertIn("p1r-qualification-audit-%", worker.run.call_args.kwargs["input_bytes"].decode())

    def testPostgresqlDiagnosticsRefuseUnownedContainers(self):
        identity = "a" * 64
        for observed in ({"Id": identity, "Config": {"Labels": {"hexalith.p1r.invocation": "other"}}},
                         {"Id": "b" * 64, "Config": {"Labels": {"hexalith.p1r.invocation": "owned"}}}):
            with self.subTest(observed=observed), mock.patch.object(
                    executor.subprocess, "check_output", return_value=(json.dumps(observed) + "\n").encode()) as check_output:
                with self.assertRaisesRegex(preparation.InvalidPacket, "unowned provider inventory"):
                    executor.postgres_inventory(identity, "owned")
                self.assertEqual(check_output.call_count, 1)
        worker = executor.Executor.__new__(executor.Executor)
        worker.run = mock.Mock()
        for containers in ({}, {"owned": {"image": executor.REDIS}}):
            worker.containers = containers
            with self.subTest(containers=containers), \
                 self.assertRaisesRegex(preparation.InvalidPacket, "unowned PostgreSQL diagnostic"):
                worker.postgres_query("owned", "TRUNCATE TABLE state;")
        worker.run.assert_not_called()

    def testRenderedPostgresqlCredentialIsAbsentFromAppEnvironments(self):
        with tempfile.TemporaryDirectory() as temporary, \
             mock.patch.dict(os.environ, {"POSTGRES_CONNECTION_STRING": "inherited-secret"}), \
             mock.patch.object(executor, "reserve_port", side_effect=range(12000, 12012)), \
             mock.patch.object(executor.time, "sleep"):
            root = Path(temporary)
            (root / "configurations").mkdir()
            worker = executor.Executor.__new__(executor.Executor)
            worker.output = worker.scratch = root
            worker.inputs = {"operational_profile": {"runtime_version": "1.18.2"}}
            worker.redis, worker.redis_port, worker.pubsub_port = "owned", 15432, 16379
            worker.postgres_password = "private-secret"
            worker.invocation = "a" * 32
            worker.digest_key = worker.delegation = worker.app_token = worker.workload_key = "fixture"
            worker.evidence = {"candidate": root}
            worker.daprd, worker.placement_port, worker.scheduler_port = root / "daprd", 50005, 50006
            worker.configurations, worker.active = [], []
            environments = []
            worker.stop_nodes = mock.Mock()
            worker.wait = mock.Mock()
            worker.launch = lambda argv, environment: environments.append(environment) or {"ownership": mock.Mock(root={})}
            worker.start_nodes(executor.CANDIDATE)
            self.assertEqual(len(environments), 4)
            self.assertTrue(all("POSTGRES_CONNECTION_STRING" not in value for value in environments))
            self.assertIn("password=private-secret", (root / "resources/state.yaml").read_text())
            self.assertNotIn("private-secret", json.dumps(worker.configurations))
            self.assertIsNotNone(worker.configurations[0]["source_workload_authority"])
            qualification.validate_postgresql_component(
                next(file for file in worker.configurations[0]["files"] if file["name"] == "state.yaml"))

    def testDockerDiscoverySelectsOnlyPreservationAndOwnershipFields(self):
        argv = executor.safe_inspect_argv("synthetic-id")
        self.assertEqual(argv[:3], ["docker", "inspect", "--format"])
        self.assertEqual(argv[-1], "synthetic-id")
        self.assertNotIn(".Config.Env", argv[3])
        self.assertNotIn("{{json .Config}}", argv[3])
        observed = {"Id": "synthetic-id", "Image": "sha256:synthetic", "Name": "/synthetic",
            "State": {"Running": True, "StartedAt": "synthetic-time"},
            "Config": {"Labels": {"hexalith.p1r.invocation": "synthetic-control"}}}
        self.assertEqual(executor.docker_observations((json.dumps(observed) + "\n").encode()), [observed])
    def testNaturalReminderRequiresCompletedReadOnlyObservationBeforeInjection(self):
        worker = executor.Executor.__new__(executor.Executor)
        worker.sidecar_port, worker.commands = 12345, []
        observed_arguments = []
        def probe(version, arguments, checks, prefix, timeout=180):
            observed_arguments.append(arguments)
            worker.commands.append({"id": len(worker.commands) + 1})
            checks.check(prefix + "actor-sequence-observed", True)
            return {"sequence": 12 if len(observed_arguments) == 1 else 13}
        with mock.patch.object(worker, "probe", side_effect=probe), mock.patch.object(executor.time, "sleep"):
            checks = executor.Measurements()
            observed = worker.natural_reminder(checks)
        self.assertTrue(observed["completed"])
        self.assertFalse(observed["manual_callback_sent_before_observation"])
        self.assertEqual([row["sequence"] for row in observed["observations"]], [12, 13])
        self.assertTrue(all(arguments[0] == "sequence" for arguments in observed_arguments))
        self.assertEqual(checks.counter, {"attempted": 3, "passed": 3, "failed": 0})

    def testExpiredNaturalReminderObservationIsNonpassing(self):
        worker = executor.Executor.__new__(executor.Executor)
        checks = executor.Measurements()
        with mock.patch.object(executor.time, "monotonic", side_effect=[0, 31]):
            observed = worker.natural_reminder(checks)
        self.assertFalse(observed["completed"])
        self.assertEqual(observed["observations"], [])
        self.assertEqual(checks.counter, {"attempted": 1, "passed": 0, "failed": 1})

    def testExecutionRefusesSubstitutedApprovedTupleBeforeOperations(self):
        inputs = {"candidate": {"version": "3.119.0", "tag": "v3.119.0",
            "tag_commit": "f463442cca19e4199982a23a08bae4a490767d4a",
            "builds": {"version": "4.30.1-20-g2cf0002", "commit": "2cf00028bbe563d80d4d12b5fb2054914f14fcb6"}},
            "rollback": None, "assertion_instrumentation": {"mechanism": executor.MECHANISM},
            "comparisons": [{"version": version} for version in qualification.COMPARISON_VERSIONS],
            "operational_profile": {"runtime": "dapr", "runtime_version": "1.18.2",
                "backend": "state.postgresql", "backend_image": executor.POSTGRES},
            "selected_additions": list(qualification.ADDITIONS)}
        # Isolate the executor's exact approved-selection guard from the input schema.
        # This tooling fixture cannot create a package or operational qualification.
        with mock.patch.object(qualification, "validate_inputs", return_value="owner-selected"):
            executor.validate_execution_inputs(inputs)
            for mutation in (lambda value: value["candidate"].update(tag_commit="0" * 40),
                             lambda value: value["candidate"]["builds"].update(version="4.30.0"),
                             lambda value: value["operational_profile"].update(runtime_version="1.18.4"),
                             lambda value: value["operational_profile"].update(backend_image="redis:latest"),
                             lambda value: value["operational_profile"].update(backend="state.redis"),
                             lambda value: value.update(rollback={"version": "3.110.0"}),
                             lambda value: value["selected_additions"].pop(),
                             lambda value: value["comparisons"].reverse()):
                changed = copy.deepcopy(inputs)
                mutation(changed)
                with self.assertRaisesRegex(preparation.InvalidPacket, "selections substituted"):
                    executor.validate_execution_inputs(changed)

    def testStatusRetryabilityKeepsUnknownFalseAndTrueDistinct(self):
        self.assertEqual(executor.STATUS_RETRYABILITY, (None, False, True))
        self.assertEqual([type(value) for value in executor.STATUS_RETRYABILITY], [type(None), bool, bool])

    def testMissingRequiredDirectionIsRefusedEvenWithConsistentCounts(self):
        from test_p1r_published_qualification import lane_case, lane_receipt, synthetic_inputs, synthetic_evidence
        with tempfile.TemporaryDirectory() as scratch:
            _, candidate = synthetic_evidence(Path(scratch) / "candidate", version="3.119.0")
            inputs = synthetic_inputs(candidate, fixture=None)
            inputs["assertion_instrumentation"]["mechanism"] = executor.MECHANISM
            cases = [lane_case(identity) for identity in qualification.executed_case_inventory(inputs)["full-replay"][:-1]]
            receipt = lane_receipt("full-replay", cases=cases, scope="published-package", compatibility="compatible")
            receipt["inputs_sha256"], receipt["instrumentation"] = "a" * 64, executor.MECHANISM
            command = {"argv": ["synthetic-fixture-command"], "cwd": "/synthetic", "exit_code": 0,
                       "started_utc": receipt["started_utc"], "finished_utc": receipt["finished_utc"],
                       "output": "synthetic fixture", "output_sha256": executor.digest(b"synthetic fixture")}
            receipt["execution_evidence"] = {"mechanism": executor.MECHANISM, "executor_source": {"fixture": qualification.SYNTHETIC},
                "cases": [{"id": case["id"], "commands": [command]} for case in cases]}
            receipt["execution_evidence"]["executor_source_sha256"] = executor.digest(
                json.dumps(receipt["execution_evidence"]["executor_source"], indent=2, sort_keys=True).encode() + b"\n")
            context = {"inputs": {"scope": "owner-selected", "value": inputs, "sha256": "a" * 64},
                       "source_binding": {"fixture": qualification.SYNTHETIC}, "verified": {}}
            # The direction guard precedes the archive trust boundary; no synthetic archive can qualify.
            inputs["comparisons"] = [{"version": version} for version in qualification.COMPARISON_VERSIONS]
            with self.assertRaisesRegex(preparation.InvalidPacket, "required direction"):
                qualification.bind_lane(receipt, {"scope": "published-package"}, context)

    def testExecutedPostgresqlConfigurationMustRetainRedactedTrackedTemplate(self):
        from test_p1r_published_qualification import lane_case, lane_receipt, synthetic_inputs, synthetic_evidence
        with tempfile.TemporaryDirectory() as scratch:
            _, candidate = synthetic_evidence(Path(scratch) / "candidate", version="3.119.0")
            inputs = synthetic_inputs(candidate, fixture=None)
            inputs["assertion_instrumentation"]["mechanism"] = executor.MECHANISM
            inputs["comparisons"] = [{"version": version} for version in qualification.COMPARISON_VERSIONS]
            inputs["operational_profile"]["backend"] = "state.postgresql"
            cases = [lane_case(identity) for identity in qualification.executed_case_inventory(inputs)["full-replay"]]
            receipt = lane_receipt("full-replay", cases=cases, scope="published-package", compatibility="compatible")
            receipt["inputs_sha256"], receipt["instrumentation"] = "a" * 64, executor.MECHANISM
            rendered = (executor.ROOT / "deploy/dapr/statestore-postgresql.yaml").read_text().replace(
                "{env:POSTGRES_CONNECTION_STRING}", "host=127.0.0.1 password=private-secret")
            configuration = {"backend_image": inputs["operational_profile"]["backend_image"],
                             "runtime_version": inputs["operational_profile"]["runtime_version"],
                             "files": [{"name": "state.yaml", "content": rendered, "sha256": executor.digest(rendered.encode()),
                                        "rendered_sha256": "a" * 64, "credential_redacted": True}]}
            receipt["execution_evidence"] = {"mechanism": executor.MECHANISM, "executor_source": {"fixture": qualification.SYNTHETIC},
                "cases": [{"id": case["id"], "configurations": [configuration]} for case in cases]}
            receipt["execution_evidence"]["executor_source_sha256"] = executor.digest(
                json.dumps(receipt["execution_evidence"]["executor_source"], indent=2, sort_keys=True).encode() + b"\n")
            context = {"inputs": {"scope": "owner-selected", "value": inputs, "sha256": "a" * 64},
                       "source_binding": {"fixture": qualification.SYNTHETIC}, "verified": {}}
            with self.assertRaisesRegex(preparation.InvalidPacket, "PostgreSQL component template"):
                qualification.bind_lane(receipt, {"scope": "published-package"}, context)

    def testComparisonArchivesHaveIndependentPacketLanes(self):
        from test_p1r_published_qualification import synthetic_inputs, synthetic_evidence, synthetic_verifier, SYNTHETIC_BINDING
        with tempfile.TemporaryDirectory() as scratch:
            root = Path(scratch)
            evidence, candidate = synthetic_evidence(root / "candidate")
            selections, directories = [], []
            for version in qualification.COMPARISON_VERSIONS:
                directory, selection = synthetic_evidence(root / version, "comparison-" + version, version)
                selections.append(selection)
                directories.append(directory)
            inputs = synthetic_inputs(candidate)
            inputs["comparisons"] = selections
            inputs_path = root / "inputs.json"
            executor.write_json(inputs_path, inputs)
            with mock.patch.object(preparation, "source_binding", return_value=copy.deepcopy(SYNTHETIC_BINDING)):
                packet = qualification.create_packet(root / "packet", inputs=inputs_path, candidate_evidence=evidence,
                    comparison_evidence=directories, verifier=synthetic_verifier)
                self.assertEqual(packet["errors"], [])
                result = qualification.validate_packet(root / "packet")
            self.assertTrue(result["valid"])
            self.assertFalse(result["technically_qualified"])
            self.assertFalse(result["capable_rollback_qualified"])
            self.assertEqual([row["id"] for row in packet["lanes"]["packages"]][-2:],
                ["comparison-3.70.1-packages", "comparison-3.110.0-packages"])

    def testFailuresAreIncludedInDerivedCounters(self):
        measured = executor.Measurements()
        measured.check("effect", True)
        measured.check("preservation", False)
        self.assertEqual(measured.counter, {"attempted": 2, "passed": 1, "failed": 1})
        self.assertEqual(measured.checks[-1], {"id": "preservation", "passed": False})

    def testDuplicateAndNonBooleanChecksRefused(self):
        measured = executor.Measurements()
        measured.check("effect", True)
        for identity, value in (("effect", False), ("numeric", 1), ("missing", None)):
            with self.assertRaises(preparation.InvalidPacket):
                measured.check(identity, value)

    def testInventedProbeCountersAndOmittedFailuresRefused(self):
        value = {"instrumentation": executor.MECHANISM, "measurement": {
            "checks": [{"id": "effect", "passed": False}], "assertions": {"attempted": 1, "passed": 1, "failed": 0}}}
        with self.assertRaises(preparation.InvalidPacket):
            executor.Measurements().import_probe(value, "probe:")
        value["measurement"]["checks"] = []
        with self.assertRaises(preparation.InvalidPacket):
            executor.Measurements().import_probe(value, "probe:")

    def testFiniteInventoryIncludesActualHistoricalAndCandidateDirections(self):
        selection = {"candidate": {"version": "3.119.0"}}
        inventory = qualification.executed_case_inventory(selection)
        for lane in ("metadata-write", "full-replay", "snapshot-tail", "retained-covered"):
            self.assertIn("3.110.0-to-3.70.1", inventory[lane])
            self.assertIn("3.70.1-to-3.110.0", inventory[lane])
            self.assertIn("3.119.0-to-3.70.1", inventory[lane])
            self.assertIn("3.70.1-to-3.119.0", inventory[lane])
        self.assertEqual(set(inventory), set(qualification.SCENARIOS + qualification.ADDITIONS))
        self.assertIn("3.119.0-fenced-effect", inventory["mixed-api"])
        self.assertIn("3.119.0-trusted-effect", inventory["mixed-api"])

    def testComparisonPolicyDoesNotGrantRollback(self):
        from test_p1r_published_qualification import synthetic_inputs, synthetic_evidence
        with tempfile.TemporaryDirectory() as scratch:
            root = Path(scratch)
            _, candidate = synthetic_evidence(root / "candidate")
            comparisons = []
            for version in qualification.COMPARISON_VERSIONS:
                _, selection = synthetic_evidence(root / version, role="comparison-" + version, version=version)
                comparisons.append(selection)
            inputs = synthetic_inputs(candidate, rollback=None)
            inputs["comparisons"] = comparisons
            qualification.validate_inputs(inputs)
            lanes = qualification.initial_lanes(inputs)
            self.assertEqual(len(lanes["packages"]), 4)
            result = qualification.evaluate(lanes, {"value": inputs, "scope": "tooling-synthetic"}, None)
            self.assertFalse(result["capable_rollback_qualified"])
            self.assertFalse(result["p1r_usable"])
            for mutation in (lambda value: value["comparisons"].pop(),
                             lambda value: value["comparisons"].reverse(),
                             lambda value: value["comparisons"][0].update(version="3.114.0")):
                changed = copy.deepcopy(inputs)
                mutation(changed)
                with self.assertRaises(preparation.InvalidPacket):
                    qualification.validate_inputs(changed)


class CleanupTests(unittest.TestCase):
    def worker(self, scratch):
        worker = executor.Executor.__new__(executor.Executor)
        worker.output = Path(scratch) / "receipts-output"
        worker.output.mkdir()
        worker.scratch = Path(scratch) / "owned-scratch"
        worker.scratch.mkdir()
        worker.invocation = "1" * 32
        worker.fixture = {"id": "synthetic-control", "synthetic": True}
        worker.inputs = None
        worker.inputs_sha256 = None
        worker.commands, worker.receipts, worker.errors = [], [], []
        worker.processes, worker.active, worker.containers = [], [], {}
        worker.pending_containers = {}
        worker.configurations = []
        preparation.subreaper()
        worker.shared_before = {}
        worker.result = {"errors": [], "receipts": []}
        return worker

    def testCleanupContinuesAfterOneFailureAndRepeatsEveryTarget(self):
        with tempfile.TemporaryDirectory() as scratch:
            worker = self.worker(scratch)
            worker.containers = {"bad": {}, "good": {}}
            visited = []
            def run(argv, **kwargs):
                visited.append(tuple(argv))
                if argv[:2] == ["docker", "inspect"] and len(argv) == 5:
                    identity = argv[-1]
                    if identity == "bad":
                        raise RuntimeError("synthetic injected cleanup failure")
                    worker.commands.append({"exit_code": 0})
                    return json.dumps({"Id": identity, "Config": {"Labels": {"hexalith.p1r.invocation": worker.invocation}}}).encode()
                return b""
            with mock.patch.object(worker, "run", side_effect=run):
                receipt = worker.cleanup()
            self.assertEqual(sum(row == ("docker", "rm", "-f", "good") for row in visited), 2)
            self.assertEqual(receipt["attempts"][-1]["remaining"], ["bad"])
            self.assertEqual(receipt["execution"], "failed")
            self.assertEqual(receipt["scope"], "tooling-synthetic")
            self.assertEqual(receipt["compatibility"], "unverified")
            self.assertFalse(worker.scratch.exists())

    def testInterruptedActualChildIsRemovedAndRepeatedCleanupIsIdempotent(self):
        with tempfile.TemporaryDirectory() as scratch:
            worker = self.worker(scratch)
            child = worker.launch([sys.executable, "-I", "-c", "import time; time.sleep(60)"], dict())
            identity = child["ownership"].root
            preparation.signal_owned(identity, __import__("signal").SIGINT)
            def discovery(argv, **kwargs):
                return b""
            with mock.patch.object(worker, "run", side_effect=discovery):
                receipt = worker.cleanup()
            self.assertFalse(preparation.same_process(identity, preparation.process_state(identity["pid"])))
            self.assertEqual(receipt["attempts"][-1]["remaining"], [])
            self.assertEqual(len(receipt["attempts"]), 2)
            self.assertEqual(receipt["scope"], "tooling-synthetic")

    def testInterruptionRetainsPartialCaseAndPropagatesAfterOwnedCleanup(self):
        with tempfile.TemporaryDirectory() as scratch:
            worker = self.worker(scratch)
            calls = []
            def interrupt(checks):
                checks.check("started", True)
                raise KeyboardInterrupt()
            with mock.patch.object(worker, "stop_nodes", side_effect=lambda: calls.append("cleanup")):
                with self.assertRaises(KeyboardInterrupt):
                    worker.case("full-replay", "synthetic-interruption", interrupt, True)
            self.assertEqual(calls, ["cleanup"])
            retained = list((worker.output / "interrupted").glob("*.json"))
            self.assertEqual(len(retained), 1)
            value = json.loads(retained[0].read_bytes())
            self.assertEqual(value["case"]["assertions"], {"attempted": 2, "passed": 1, "failed": 1})


class CorrectionTests(unittest.TestCase):
    worker = CleanupTests.worker
    def testCleanupDiagnosticsRetainErrnoAndCleanRetryCannotEraseRefusal(self):
        ownership=preparation.OwnedProcesses()
        with mock.patch.object(ownership,'discover',side_effect=OSError(5,'bounded synthetic read failure')):
            failed=ownership.cleanup()
        self.assertEqual(failed['errors'],['OSError'])
        self.assertEqual(ownership.cleanup_diagnostics,[{'operation':'discover-owned','type':'OSError','errno':5}])
        self.assertEqual(failed['remaining'],[])
        clean=ownership.cleanup()
        self.assertEqual(clean['errors'],[])
        with tempfile.TemporaryDirectory() as scratch:
            worker=self.worker(scratch)
            actual_cleanup=preparation.OwnedProcesses.cleanup
            attempts=[]
            def cleanup(owner):
                if not attempts:
                    with mock.patch.object(owner,'discover',side_effect=OSError(5,'bounded synthetic read failure')):
                        result=actual_cleanup(owner)
                else:
                    result=actual_cleanup(owner)
                attempts.append(result)
                return result
            with mock.patch.object(preparation.OwnedProcesses,'cleanup',cleanup):
                with self.assertRaisesRegex(preparation.InvalidPacket,'owned command cleanup failed'):
                    worker.run([sys.executable,'-I','-c','pass'])
            self.assertEqual(worker.commands[-1]['exit_code'],0)
            self.assertEqual(worker.commands[-1]['cleanup'],[failed,clean])
            self.assertEqual(worker.commands[-1]['cleanup_error_diagnostics'],ownership.cleanup_diagnostics)

    def metadata_fixture(self, scratch):
        worker = self.worker(scratch)
        def run(argv, **kwargs):
            observation = {"sequence":12,"floor":1,"etag":"fixture-etag","last_modified":"2026-01-01T00:00:00+00:00"}
            value = {"instrumentation":executor.MECHANISM, "measurement": {
                "checks":[{"id":identity,"passed":True} for identity in ("sequence-twelve","etag-preserved","last-modified-preserved")],
                "assertions":{"attempted":3,"passed":3,"failed":0}},"observation":observation}
            Path(argv[-2]).write_text('{}')
            data=executor.canonical(value)
            worker.record(argv,worker.scratch,preparation.stamp(),0,data)
            return data
        worker.evidence = {"comparison-3.70.1":worker.scratch}
        with mock.patch.object(worker,"run",side_effect=run):
            row,evidence = worker.case("metadata-read","3.70.1-web-floor-5",
                lambda checks:worker.metadata_case("metadata-read","3.70.1-web-floor-5",checks),False)
        binding={"repository":str(preparation.ROOT),"main":{"files":[
            {"path":"tools/p1r_published_executor.py","sha256":executor.digest((preparation.ROOT/'tools/p1r_published_executor.py').read_bytes())}]}}
        return worker,row,evidence,binding

    def validate(self, worker, row, evidence, binding):
        executor.witnesses.validate_case(row,evidence,binding,"metadata-read",list(worker.witness_sources.values()))

    def testFailedMetadataCheckCannotBeFlippedWithRecomputedCountersAndHashes(self):
        with tempfile.TemporaryDirectory() as scratch:
            worker,row,evidence,binding=self.metadata_fixture(scratch)
            self.validate(worker,row,evidence,binding)
            self.assertFalse(row["checks"][-1]["passed"])
            changed=copy.deepcopy(row)
            changed["checks"][-1]["passed"]=True
            changed["assertions"]=preparation.checks_counter(changed["checks"])
            with self.assertRaisesRegex(preparation.InvalidPacket,"predicate operands"):
                self.validate(worker,changed,evidence,binding)
            forged=copy.deepcopy(evidence)
            forged["check_witnesses"][-1]["predicate"]["operands"]=[5,5]
            with self.assertRaisesRegex(preparation.InvalidPacket,"retained floor"):
                self.validate(worker,changed,forged,binding)

    def testProbeFailureCannotBeOmittedEvenWhenOuterTotalsAgree(self):
        with tempfile.TemporaryDirectory() as scratch:
            worker,row,evidence,binding=self.metadata_fixture(scratch)
            row["checks"].pop(0)
            evidence["check_witnesses"].pop(0)
            row["assertions"]=preparation.checks_counter(row["checks"])
            with self.assertRaisesRegex(preparation.InvalidPacket,"silently drops"):
                self.validate(worker,row,evidence,binding)

    def testInventedInventoryDigestIsIndependentlyRefused(self):
        with tempfile.TemporaryDirectory() as scratch:
            worker,row,evidence,binding=self.metadata_fixture(scratch)
            row["inventory"]={"before_sha256":"a"*64,"after_sha256":"a"*64}
            with self.assertRaisesRegex(preparation.InvalidPacket,"inventory hashes"):
                self.validate(worker,row,evidence,binding)

    def testRuntimeOperationCannotDropRenderedConfigurations(self):
        with tempfile.TemporaryDirectory() as scratch:
            worker,row,evidence,binding=self.metadata_fixture(scratch)
            evidence["commands"][0]["argv"].append("actor")
            with self.assertRaisesRegex(preparation.InvalidPacket,"sidecar command configuration"):
                self.validate(worker,row,evidence,binding)

    def testExecutorSourceSubstitutionRefusedAfterOuterHashRecomputation(self):
        from test_p1r_published_qualification import lane_case,lane_receipt
        selected={"candidate":{"version":"3.119.0"},"comparisons":[{"version":v} for v in qualification.COMPARISON_VERSIONS],
            "assertion_instrumentation":{"mechanism":executor.MECHANISM},"selected_additions":[]}
        receipt=lane_receipt("provenance",scope="published-package",compatibility="compatible",
            cases=[lane_case(case) for case in qualification.executed_case_inventory(selected)["provenance"]])
        receipt["inputs_sha256"]= "a"*64
        receipt["instrumentation"]=executor.MECHANISM
        evidence={"mechanism":executor.MECHANISM,"executor_source":{"substituted":True},
            "cases":[{"id":case["id"],"commands":[]} for case in receipt["cases"]]}
        evidence["executor_source_sha256"]=executor.digest(json.dumps(evidence["executor_source"],indent=2,sort_keys=True).encode()+b"\n")
        receipt["execution_evidence"],receipt["output_sha256"]=evidence,executor.digest(executor.canonical(evidence))
        with self.assertRaisesRegex(preparation.InvalidPacket,"independently bound source closure"):
            qualification.bind_lane(receipt,{"scope":"published-package"},{"inputs":{"value":selected,"scope":"owner-selected","sha256":"a"*64},
                "source_binding":{"original":True},"verified":{}})

    def testTimeoutOwnsAndRemovesDetachedSynchronousDescendant(self):
        with tempfile.TemporaryDirectory() as scratch:
            worker=self.worker(scratch)
            sentinel=subprocess.Popen([sys.executable,'-I','-c','import time;time.sleep(20)'],start_new_session=True)
            shared=preparation.process_state(sentinel.pid)
            try:
                script="import subprocess,sys,time;p=subprocess.Popen([sys.executable,'-I','-c','import time;time.sleep(20)'],start_new_session=True);print(p.pid,flush=True);time.sleep(20)"
                worker.run([sys.executable,'-I','-c',script],timeout=.2,check=False)
                command=worker.commands[-1]
                self.assertEqual(command['exit_code'],124)
                self.assertGreaterEqual(len(command['owned_processes']),2)
                self.assertTrue(all(not attempt['remaining'] for attempt in command['cleanup']))
                self.assertTrue(preparation.same_process(preparation.identity(shared),preparation.process_state(sentinel.pid)))
            finally:
                sentinel.terminate();sentinel.wait()

    def testCancellationRetainsCommandTreeCleanup(self):
        with tempfile.TemporaryDirectory() as scratch:
            worker=self.worker(scratch)
            timer=threading.Timer(.2,lambda:os.kill(os.getpid(),signal.SIGINT))
            timer.start()
            try:
                with self.assertRaises(KeyboardInterrupt):
                    worker.run([sys.executable,'-I','-c','import time;time.sleep(20)'])
            finally:
                timer.cancel();timer.join()
            self.assertEqual(worker.commands[-1]['exit_code'],130)
            self.assertEqual(worker.commands[-1]['cleanup'][-1]['remaining'],[])

    def testInitializationFailureDestroysScratchAndRetainsRefusal(self):
        with tempfile.TemporaryDirectory() as scratch:
            owned=Path(scratch)/'owned';owned.mkdir()
            with mock.patch.object(executor.tempfile,'mkdtemp',return_value=str(owned)), \
                 mock.patch.object(preparation,'source_binding',side_effect=preparation.InvalidPacket('source refusal')), \
                 mock.patch.object(executor.Executor,'run',return_value=b''):
                with self.assertRaisesRegex(preparation.InvalidPacket,'source refusal'):
                    executor.Executor(Path(scratch)/'output')
            self.assertFalse(owned.exists())
            result=json.loads((Path(scratch)/'output/execution.json').read_bytes())
            self.assertIn('initialization: source refusal',result['errors'])
            self.assertTrue(result['consumer_cleanup'])

    def testInterruptedContainerCreationRecoversExactNameAndLabelWithoutCidfile(self):
        with tempfile.TemporaryDirectory() as scratch:
            worker=self.worker(scratch)
            observed={}
            def run(argv,**kwargs):
                if argv[:2]==['docker','create']:
                    observed['name']=argv[argv.index('--name')+1]
                    raise KeyboardInterrupt()
                worker.commands.append({'exit_code':0})
                return json.dumps({'Id':'owned-id','Name':'/'+observed['name'],
                    'Config':{'Labels':{'hexalith.p1r.invocation':worker.invocation}}}).encode()
            with mock.patch.object(worker,'run',side_effect=run):
                with self.assertRaises(KeyboardInterrupt):
                    worker.container('interrupted',executor.REDIS)
            self.assertIn('owned-id',worker.containers)
            self.assertEqual(worker.pending_containers,{})

    def testLateReminderThirteenRemainsNonpassingAndUsesRemainingTimeout(self):
        with tempfile.TemporaryDirectory() as scratch:
            worker=self.worker(scratch);worker.sidecar_port=12345
            timeouts=[]
            def probe(version,arguments,checks,prefix,timeout):
                timeouts.append(timeout);worker.commands.append({'id':1});return {'sequence':13}
            with mock.patch.object(worker,'probe',side_effect=probe),mock.patch.object(executor.time,'monotonic',side_effect=[0,1,2,31,32]):
                measured=executor.Measurements();observed=worker.natural_reminder(measured)
            self.assertEqual(timeouts,[28])
            self.assertFalse(observed['completed'])
            self.assertFalse(measured.checks[-1]['passed'])

    def testRetainedWitnessSourceSurvivesUnavailableOriginalPath(self):
        with tempfile.TemporaryDirectory() as scratch:
            worker,row,evidence,binding=self.metadata_fixture(scratch)
            with mock.patch.object(Path,'read_bytes',side_effect=OSError('original source unavailable')):
                self.validate(worker,row,evidence,binding)

    def testMissingStableMetadataFailureCannotBeDroppedTogetherWithWitness(self):
        with tempfile.TemporaryDirectory() as scratch:
            worker,row,evidence,binding=self.metadata_fixture(scratch)
            row['checks'].pop();evidence['check_witnesses'].pop()
            row['assertions']=preparation.checks_counter(row['checks'])
            with self.assertRaisesRegex(preparation.InvalidPacket,'retained floor'):
                self.validate(worker,row,evidence,binding)

    def testSharedSourceCaseMeasuresActualPackageDelta(self):
        with tempfile.TemporaryDirectory() as scratch:
            worker=self.worker(scratch);worker.inputs={'candidate':{'version':'3.119.0'}}
            def metadata(lane,identity,measurements):
                floor=5 if identity.startswith('source') else 1
                measurements.check('floor-one',floor == 1)
                return {'metadata':{'sequence':12,'floor':floor}},'effect','compatible'
            with mock.patch.object(worker,'metadata_case',side_effect=metadata):
                measured=executor.Measurements();observed,outcome,disposition=worker.checkout_case('legacy-metadata',measured)
            self.assertEqual(observed['comparison']['selected_case'],'3.119.0-pascal-floor-None')
            self.assertEqual(observed['comparison']['source_case'],'source-pascal-floor-None')
            self.assertFalse(measured.checks[-1]['passed'])
            self.assertEqual(disposition,'incompatible')
            self.assertTrue(observed['comparison']['delta'])

    def testDroppingNonNullQueryIdentityProofFailsWirePreservation(self):
        with tempfile.TemporaryDirectory() as scratch:
            worker=self.worker(scratch)
            def probe(version,arguments,measurements,prefix):
                original=json.loads(Path(arguments[3]).read_bytes())
                if prefix == 'reader:':
                    original.pop('identityAdmissionProof',None)
                Path(arguments[4]).write_bytes(executor.canonical(original))
                return {'handling':'executed','fields':original}
            with mock.patch.object(worker,'probe',side_effect=probe):
                measured=executor.Measurements();_,_,disposition=worker.wire_case('query-wire','3.119.0-3.110.0-json-dual',measured)
            self.assertIn({'id':'preserved:identityAdmissionProof','passed':False},measured.checks)
            self.assertEqual(disposition,'incompatible')

    def testUnexpectedProofFailureRetainsAfterInventoryAndRemainsError(self):
        with tempfile.TemporaryDirectory() as scratch:
            worker=self.worker(scratch);worker.host_port=12345;worker.redis='owned'
            observed={'accepted':False,'unexpected':True,'unauthorized_refused':False,'denial':None,'sequence':12,
                'diagnostic':[{'denial_message':'Trusted effect denial audit is unavailable.'}]}
            with mock.patch.object(worker,'seed'),mock.patch.object(worker,'inventory',return_value=[]), \
                 mock.patch.object(worker,'start_nodes'),mock.patch.object(worker,'stop_nodes'), \
                 mock.patch.object(worker,'probe',return_value={'actor_methods':['ProcessTrustedEffectAsync']}), \
                 mock.patch.object(worker,'http',return_value=observed), \
                 mock.patch.object(worker,'postgres_audits',return_value=[]):
                measured=executor.Measurements();result,outcome,_=worker.mixed_case('3.119.0-unauthorized-effect',measured)
            self.assertEqual(outcome,'error')
            self.assertEqual(result['after'],[])
            self.assertIn({'id':'execution-completed','passed':False},measured.checks)
            self.assertIn({'id':'refusal-domain-preserved','passed':True},measured.checks)

    def testSupportedMetadataAppendRestartsActualSelectedWriterAndReplaysThirteen(self):
        with tempfile.TemporaryDirectory() as scratch:
            worker=self.worker(scratch)
            def rows(head):
                values=[{'key':'tenant-a:metadata','tenant':'tenant-a','kind':'metadata','sha256':'a'*64,'sequence':head,'floor':5},
                        {'key':'tenant-a:snapshot','tenant':'tenant-a','kind':'snapshot','sha256':'b'*64,'sequence':9,'floor':None}]
                values += [{'key':'tenant-a:events:'+str(index),'tenant':'tenant-a','kind':'event','sha256':'c'*64,'sequence':index,'floor':None} for index in range(5,head+1)]
                return sorted(values,key=lambda value:value['key'])
            def actor(version,tenant,expected,kind,measurements,prefix):
                return {'accepted':True,'event_count':1 if kind=='IncrementCounter' else 0,'sequence':expected}
            starts=[]
            with mock.patch.object(worker,'seed'),mock.patch.object(worker,'mutate'), \
                 mock.patch.object(worker,'inventory',side_effect=[rows(12),rows(13),rows(13)]), \
                 mock.patch.object(worker,'start_nodes',side_effect=lambda version,**kwargs:starts.append(version)), \
                 mock.patch.object(worker,'stop_nodes'),mock.patch.object(worker,'actor',side_effect=actor):
                measured=executor.Measurements();observation,_,_=worker.live_case('metadata-write','3.110.0-to-3.119.0',measured)
            self.assertEqual(starts,['3.119.0','3.119.0'])
            self.assertEqual(observation['replay']['sequence'],13)
            self.assertIn({'id':'supported-append-restart-rehydrates-thirteen','passed':True},measured.checks)


    def wire_fixture(self,scratch,lane):
        worker=self.worker(scratch)
        worker.evidence={'candidate':worker.scratch,'comparison-3.110.0':worker.scratch}
        def run(argv,**kwargs):
            fields=json.loads(Path(argv[-2]).read_bytes())
            if lane=='projection-wire':
                fields['globalPosition']=None
            else:
                fields.pop('identityAdmissionProof',None)
            Path(argv[-1]).write_bytes(executor.canonical(fields))
            value={'instrumentation':executor.MECHANISM,'measurement':{
                'checks':[{'id':'contract-type-present','passed':True},{'id':'wire-output-nonempty','passed':True}],
                'assertions':{'attempted':2,'passed':2,'failed':0}},'observation':{'handling':'executed','fields':fields}}
            data=executor.canonical(value);worker.record(argv,worker.scratch,preparation.stamp(),0,data);return data
        identifier='3.119.0-3.110.0-json-'+('dual' if lane=='query-wire' else 'positive')
        with mock.patch.object(worker,'run',side_effect=run):
            row,evidence=worker.case(lane,identifier,lambda checks:worker.wire_case(lane,identifier,checks),False)
        binding={'repository':str(preparation.ROOT),'main':{'files':[{'path':'tools/p1r_published_executor.py',
                 'sha256':executor.digest((preparation.ROOT/'tools/p1r_published_executor.py').read_bytes())}]}}
        return worker,row,evidence,binding

    def testProjectionAndQueryFailuresCannotBeFlippedWithContradictoryProbeFields(self):
        for lane,field in (('projection-wire','globalPosition'),('query-wire','identityAdmissionProof')):
            with self.subTest(lane=lane),tempfile.TemporaryDirectory() as scratch:
                worker,row,evidence,binding=self.wire_fixture(scratch,lane)
                retained=list(worker.witness_sources.values())
                executor.witnesses.validate_case(row,evidence,binding,lane,retained)
                check=next(item for item in row['checks'] if item['id']=='preserved:'+field)
                witness=next(item for item in evidence['check_witnesses'] if item['id']==check['id'])
                self.assertFalse(check['passed'])
                check['passed']=True
                witness['predicate']['operands'][0]=witness['predicate']['operands'][1]
                row['assertions']=preparation.checks_counter(row['checks'])
                with self.assertRaisesRegex(preparation.InvalidPacket,'reader field'):
                    executor.witnesses.validate_case(row,evidence,binding,lane,retained)

    def testLogicalRegistrationCannotContradictRetainedDomainReadiness(self):
        with tempfile.TemporaryDirectory() as scratch:
            worker=self.worker(scratch)
            def action(checks):
                registration={'manifest_registered':False,'bounded_v1_serializer_registered':True}
                worker.record(['HTTP','GET','http://127.0.0.1:12345/ready'],worker.scratch,preparation.stamp(),124,b'URLError')
                worker.record(['HTTP','GET','http://127.0.0.1:12345/ready'],worker.scratch,preparation.stamp(),0,
                              executor.canonical({'evolution_registration':registration}))
                checks.check('registered-logical-alias-evolution-executed',registration.get('manifest_registered') is True)
                return {'domain_registration_observation':registration,'registered_logical_alias_evolution':{'executed':False}},'effect','incompatible'
            row,evidence=worker.case('logical-event-evolution','legacy-clr-readback',action,False)
            binding={'repository':str(preparation.ROOT),'main':{'files':[{'path':'tools/tests/test_p1r_published_executor.py',
                     'sha256':executor.digest(Path(__file__).read_bytes())}]}}
            retained=list(worker.witness_sources.values())
            with self.assertRaisesRegex(preparation.InvalidPacket,'independent test-only pin'):
                executor.witnesses.validate_case(row,evidence,binding,'logical-event-evolution',retained)
            self.assertEqual(evidence['commands'][0]['output'],'URLError')
            for output,status in (('URLError',0),('[]',0),(evidence['commands'][1]['output'],124)):
                with self.subTest(output=output,status=status):
                    failed=json.loads(json.dumps(evidence))
                    failed['commands'][1]['output']=output
                    failed['commands'][1]['exit_code']=status
                    with self.assertRaisesRegex(preparation.InvalidPacket,'Domain readiness'):
                        executor.witnesses.validate_case(row,failed,binding,'logical-event-evolution',retained)

    def testCompatibleLogicalWitnessBindsExactActorUrlAndInventory(self):
        with tempfile.TemporaryDirectory() as scratch:
            worker=self.worker(scratch)
            alias='P1R.Legacy.CounterIncremented'
            registration={'manifest_registered':True,'fixture_manifest_fingerprint':
                '5a5748913258b5832b333fe507bc689f1ac9e53a9201b4bb5cb983b535da933b',
                'fixture_manifest_test_only':True,'gateway_authority':False}
            actor={'accepted':True,'event_count':0,'sequence':12,'error':None}
            events=[]
            for sequence in range(1,13):
                value={'sequenceNumber':sequence,'eventTypeName':alias,'payload':'e30='}
                key=f'eventstore||AggregateActor||tenant-a:counter:fixture||tenant-a:counter:fixture:events:{sequence}'
                events.append({'key':key,'tenant':'tenant-a','kind':'event','sha256':executor.digest(executor.canonical(value)),
                               'sequence':sequence,'floor':None})
            for sequence in range(1,4):
                value={'sequenceNumber':sequence,'eventTypeName':alias,'payload':'e30='}
                key=f'eventstore||AggregateActor||tenant-b:counter:fixture||tenant-b:counter:fixture:events:{sequence}'
                events.append({'key':key,'tenant':'tenant-b','kind':'event','sha256':executor.digest(executor.canonical(value)),
                               'sequence':sequence,'floor':None})
            events.sort(key=lambda item:item['key'])
            def record(argv,value):
                worker.record(argv,worker.scratch,preparation.stamp(),0,executor.canonical(value))
            def action(checks):
                config={'id':'synthetic-runtime','files':[{'path':'/owned/config.yaml'},
                    {'path':'/owned/resources/state.yaml'},{'path':'/owned/resources/pubsub.yaml'}]}
                worker.configurations.append(config)
                record(['daprd','--config','/owned/config.yaml','--resources-path','/owned/resources'],{})
                record(['synthetic','inventory'],events)
                record(['HTTP','GET','http://127.0.0.1:12345/ready'],{'evolution_registration':registration})
                record(['dotnet','Probe.dll','actor','http://127.0.0.1:12345','tenant-a','fixture','12','AssertCounter'],
                    {'instrumentation':executor.MECHANISM,
                    'measurement':{'checks':[],'assertions':{'attempted':0,'passed':0,'failed':0}},'observation':actor})
                readback=[]
                for sequence in range(1,13):
                    value={'sequenceNumber':sequence,'eventTypeName':alias,'payload':'e30='}
                    url=(f'http://127.0.0.1:12345/v1.0/actors/AggregateActor/'
                         f'tenant-a%3Acounter%3Afixture/state/tenant-a%3Acounter%3Afixture%3Aevents%3A{sequence}')
                    record(['HTTP','GET',url],value)
                    readback.append({'sequence':sequence,'url':url,'sha256':executor.digest(executor.canonical(value)),
                                     'event_type_name':alias,'payload_sha256':executor.digest(b'{}')})
                record(['synthetic','inventory'],events)
                checks.check('tenant-a:committed-count',True)
                checks.check('tenant-b:committed-count',True)
                checks.check('seed-persisted-fifteen-events',len(events)==15)
                checks.check('unknown-version-refused-before-application',True)
                checks.check('registered-logical-alias-evolution-executed',True)
                return {'before':events,'after':events,'actor':actor,'legacy_alias':alias,
                    'domain_registration_observation':registration,'dapr_application_readback':readback,
                    'registered_logical_alias_evolution':{'executed':True,'gateway_authority':False}},'effect','compatible'
            row,evidence=worker.case('logical-event-evolution','legacy-alias-replay',action,False)
            binding={'repository':str(preparation.ROOT),'main':{'files':[{'path':'tools/tests/test_p1r_published_executor.py',
                     'sha256':executor.digest(Path(__file__).read_bytes())}]}}
            retained=list(worker.witness_sources.values())
            executor.witnesses.validate_case(row,evidence,binding,'logical-event-evolution',retained)
            for name,mutation in (
                ('url',lambda value:value['observations']['dapr_application_readback'][0].update(url='http://127.0.0.1:12345/state/wrong')),
                ('sidecar',lambda value:value['observations']['dapr_application_readback'][0].update(
                    url=value['observations']['dapr_application_readback'][0]['url'].replace(':12345/',':12346/'))),
                ('payload-sha',lambda value:value['observations']['dapr_application_readback'][0].update(payload_sha256='f'*64)),
                ('missing-probe-argument',lambda value:next(command for command in value['commands']
                    if 'actor' in command['argv'])['argv'].pop()),
                ('wrong-tenant',lambda value:next(command for command in value['commands']
                    if 'actor' in command['argv'])['argv'].__setitem__(4,'tenant-b')),
                ('wrong-aggregate',lambda value:next(command for command in value['commands']
                    if 'actor' in command['argv'])['argv'].__setitem__(5,'other')),
                ('wrong-count',lambda value:next(command for command in value['commands']
                    if 'actor' in command['argv'])['argv'].__setitem__(6,'11')),
                ('wrong-command',lambda value:next(command for command in value['commands']
                    if 'actor' in command['argv'])['argv'].__setitem__(7,'OtherCommand')),
                ('wrong-executable',lambda value:next(command for command in value['commands']
                    if 'actor' in command['argv'])['argv'].__setitem__(0,'python')),
                ('failed-probe',lambda value:next(command for command in value['commands']
                    if 'actor' in command['argv']).update(exit_code=1)),
                ('inventory',lambda value:value['observations']['after'][0].update(sha256='f'*64)),
                ('actor',lambda value:value['observations']['actor'].update(accepted=False))):
                with self.subTest(name=name):
                    altered=copy.deepcopy(evidence)
                    mutation(altered)
                    with self.assertRaises(preparation.InvalidPacket):
                        executor.witnesses.validate_case(row,altered,binding,'logical-event-evolution',retained)
            altered=copy.deepcopy(evidence)
            changed=copy.deepcopy(row)
            key=events[0]['key']
            for name in ('before','after'):
                next(item for item in altered['observations'][name] if item['key']==key)['sha256']='f'*64
            for command in altered['commands']:
                if 'inventory' in command['argv']:
                    inventory=json.loads(command['output'])
                    next(item for item in inventory if item['key']==key)['sha256']='f'*64
                    command['output']=executor.canonical(inventory).decode()
                    command['output_sha256']=executor.digest(command['output'].encode())
            changed['inventory']={name+'_sha256':executor.digest(executor.canonical(
                [item for item in altered['observations'][name] if item['kind']!='bookkeeping']))
                for name in ('before','after')}
            with self.assertRaisesRegex(preparation.InvalidPacket,'exact provider event inventory'):
                executor.witnesses.validate_case(changed,altered,binding,'logical-event-evolution',retained)

    def testUnknownVersionWitnessRejectsWrongActorProviderKey(self):
        artifact=Path(__file__).resolve().parents[2]/'_bmad-output/implementation-artifacts/evidence/6-1-p1r-source-repair-2026-10-10'
        original=json.loads((artifact/'source-cases/unknown-version-refusal.json').read_text())
        binding=json.loads((artifact/'executor-source.json').read_text())
        sources=json.loads((artifact/'predicate-sources.json').read_text())
        executor.witnesses.validate_case(original['case'],original['evidence'],binding,original['lane'],sources)
        altered=copy.deepcopy(original['evidence'])
        exact='eventstore||AggregateActor||tenant-a:counter:fixture||tenant-a:counter:fixture:events:7'
        changed=0
        for command in altered['commands']:
            try:
                pairs=json.loads(command.get('output') or '')
            except (ValueError,TypeError):
                continue
            if not isinstance(pairs,list):
                continue
            for pair in pairs:
                if isinstance(pair,list) and len(pair)==2 and pair[0]==exact:
                    pair[0]=exact.replace('||AggregateActor||','||OtherActor||')
                    changed+=1
            if any(isinstance(pair,list) and len(pair)==2 and pair[0].startswith('eventstore||OtherActor||')
                   for pair in pairs):
                command['output']=executor.canonical(pairs).decode()
                command['output_sha256']=executor.digest(command['output'].encode())
        self.assertGreater(changed,0)
        with self.assertRaisesRegex(preparation.InvalidPacket,'retained provider diagnostic'):
            executor.witnesses.validate_case(original['case'],altered,binding,original['lane'],sources)

    def testCompatibleStaleFenceWitnessRejectsAlteredDenialAndInventory(self):
        with tempfile.TemporaryDirectory() as scratch:
            worker=self.worker(scratch)
            rows=[{'key':'tenant-a:counter:fixture:metadata','tenant':'tenant-a','kind':'metadata',
                   'sha256':'a'*64,'sequence':12,'floor':1}]
            operation={'accepted':False,'unexpected':False,'sequence':12,'stale_refused':True,
                'stale_denial':'stale-fencing-token','stale_actor_method':'ProcessFencedCommandAsync',
                'stale_diagnostic':[{'actual_exception_type':'System.InvalidOperationException',
                                     'denial_message':'The idempotency execution authority is no longer current.'}],
                'forged_refused':True,'forged_denial':'stale-or-invalid-fence',
                'forged_actor_method':'ProcessFencedCommandAsync',
                'forged_diagnostic':[{'actual_exception_type':'System.InvalidOperationException',
                                      'denial_message':'The idempotency execution fence is missing, stale, or invalid.'}]}
            url='http://127.0.0.1:12345/qualification/stale-fence'
            def action(checks):
                worker.record(['synthetic','inventory'],worker.scratch,preparation.stamp(),0,executor.canonical(rows))
                worker.record(['HTTP','POST',url],worker.scratch,preparation.stamp(),0,executor.canonical(operation))
                worker.record(['synthetic','inventory'],worker.scratch,preparation.stamp(),0,executor.canonical(rows))
                for identity in ('tenant-a:committed-count','tenant-b:committed-count','seed-persisted-fifteen-events',
                                 'stale-context-refused','forged-proof-refused','refusal-domain-preserved'):
                    checks.check(identity,True)
                return {'before':rows,'after':rows,'operation':operation},'refusal','compatible'
            row,evidence=worker.case('mixed-api','source-stale-fence',action,False)
            binding={'repository':str(preparation.ROOT),'main':{'files':[{'path':'tools/tests/test_p1r_published_executor.py',
                     'sha256':executor.digest(Path(__file__).read_bytes())}]}}
            retained=list(worker.witness_sources.values())
            executor.witnesses.validate_case(row,evidence,binding,'mixed-api',retained)
            for name,mutation in (
                ('denial',lambda value:value['observations']['operation']['stale_diagnostic'][0].update(denial_message='opaque')),
                ('stale-type',lambda value:value['observations']['operation']['stale_diagnostic'][0].update(
                    actual_exception_type='System.Exception')),
                ('forged-type',lambda value:value['observations']['operation']['forged_diagnostic'][0].update(
                    actual_exception_type='System.Exception')),
                ('inventory',lambda value:value['observations']['after'][0].update(sha256='b'*64))):
                with self.subTest(name=name):
                    altered=copy.deepcopy(evidence)
                    mutation(altered)
                    with self.assertRaises(preparation.InvalidPacket):
                        executor.witnesses.validate_case(row,altered,binding,'mixed-api',retained)

    def testStaleFenceExecutorRejectsWrongDiagnosticExceptionTypes(self):
        with tempfile.TemporaryDirectory() as scratch:
            worker=self.worker(scratch)
            worker.host_port=12345
            baseline={'accepted':False,'unexpected':False,'sequence':12,'stale_refused':True,
                'stale_denial':'stale-fencing-token','stale_actor_method':'ProcessFencedCommandAsync',
                'stale_diagnostic':[{'actual_exception_type':'System.InvalidOperationException',
                                     'denial_message':'The idempotency execution authority is no longer current.'}],
                'forged_refused':True,'forged_denial':'stale-or-invalid-fence',
                'forged_actor_method':'ProcessFencedCommandAsync',
                'forged_diagnostic':[{'actual_exception_type':'System.InvalidOperationException',
                                      'denial_message':'The idempotency execution fence is missing, stale, or invalid.'}]}
            def evaluate(observation):
                checks=executor.Measurements()
                with mock.patch.object(worker,'probe',return_value={'actor_methods':['ProcessFencedCommandAsync']}), \
                     mock.patch.object(worker,'seed'), mock.patch.object(worker,'inventory',return_value=[]), \
                     mock.patch.object(worker,'start_nodes'), mock.patch.object(worker,'stop_nodes'), \
                     mock.patch.object(worker,'http',return_value=observation):
                    _,_,disposition=worker.mixed_case('source-stale-fence',checks)
                return disposition,{item['id']:item['passed'] for item in checks.checks}
            self.assertEqual(evaluate(baseline)[0],'compatible')
            for diagnostic,check in (('stale_diagnostic','stale-context-refused'),
                                     ('forged_diagnostic','forged-proof-refused')):
                with self.subTest(diagnostic=diagnostic):
                    altered=copy.deepcopy(baseline)
                    altered[diagnostic][0]['actual_exception_type']='System.Exception'
                    disposition,checks=evaluate(altered)
                    self.assertEqual(disposition,'incompatible')
                    self.assertFalse(checks[check])

    def testStatusBindingRetainsStartupFailuresAndRequiresSuccessfulJsonReadback(self):
        with tempfile.TemporaryDirectory() as scratch:
            worker=self.worker(scratch)
            def action(checks):
                observed=[]
                worker.record(['HTTP','GET','http://127.0.0.1:12345/ready'],worker.scratch,preparation.stamp(),124,b'URLError')
                for retryable in (None,False,True):
                    label='null' if retryable is None else str(retryable).lower()
                    value={'messageId':'fixture-'+label,'correlationId':'fixture-correlation','retryable':retryable,
                           'recoveryReasonCode':'fixture-code','drainAttemptCount':3,'domain':'counter','committedEventSequence':12}
                    worker.record(['HTTP','GET','http://127.0.0.1:12345/status/'+label],worker.scratch,preparation.stamp(),0,executor.canonical(value))
                    for name in ('messageId','correlationId','retryable','recoveryReasonCode','drainAttemptCount','domain','committedEventSequence'):
                        checks.check('status:'+label+':'+name,name in value and value[name] == value[name])
                    observed.append({'retryability_case':label,'input':value,'readback':value})
                return {'operation':observed},'effect','compatible'
            row,evidence=worker.case('mixed-api','3.119.0-status',action,False)
            binding={'repository':str(preparation.ROOT),'main':{'files':[{'path':'tools/tests/test_p1r_published_executor.py',
                     'sha256':executor.digest(Path(__file__).read_bytes())}]}}
            retained=list(worker.witness_sources.values())
            executor.witnesses.validate_case(row,evidence,binding,'mixed-api',retained)
            self.assertEqual(evidence['commands'][0]['output'],'URLError')
            for output,status in (('URLError',0),('[]',0),(evidence['commands'][1]['output'],124)):
                with self.subTest(output=output,status=status):
                    failed=json.loads(json.dumps(evidence))
                    failed['commands'][1]['output']=output
                    failed['commands'][1]['exit_code']=status
                    with self.assertRaisesRegex(preparation.InvalidPacket,'status readback'):
                        executor.witnesses.validate_case(row,failed,binding,'mixed-api',retained)



if __name__ == "__main__":
    unittest.main()

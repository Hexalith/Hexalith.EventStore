"""Executor tooling controls; no fixture in this suite grants operational qualification."""
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
        inputs = {"candidate": {"version": "3.115.0", "tag": "v3.115.0",
            "tag_commit": "283b07a52c9c70e1c940164a7011ee8c3ad98b2d",
            "builds": {"version": "4.29.1-22-gba4ca78", "commit": "ba4ca78c3868a4757cb92d912a54c8a237871b54"}},
            "rollback": None, "assertion_instrumentation": {"mechanism": executor.MECHANISM},
            "comparisons": [{"version": version} for version in qualification.COMPARISON_VERSIONS],
            "operational_profile": {"runtime": "dapr", "runtime_version": "1.18.4",
                "backend": "state.redis", "backend_image": executor.REDIS},
            "selected_additions": list(qualification.ADDITIONS)}
        # Isolate the executor's exact approved-selection guard from the input schema.
        # This tooling fixture cannot create a package or operational qualification.
        with mock.patch.object(qualification, "validate_inputs", return_value="owner-selected"):
            executor.validate_execution_inputs(inputs)
            for mutation in (lambda value: value["candidate"].update(tag_commit="0" * 40),
                             lambda value: value["candidate"]["builds"].update(version="4.30.0"),
                             lambda value: value["operational_profile"].update(runtime_version="1.18.2"),
                             lambda value: value["operational_profile"].update(backend_image="redis:latest"),
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
            _, candidate = synthetic_evidence(Path(scratch) / "candidate", version="3.115.0")
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
        selection = {"candidate": {"version": "3.115.0"}}
        inventory = qualification.executed_case_inventory(selection)
        for lane in ("metadata-write", "full-replay", "snapshot-tail", "retained-covered"):
            self.assertIn("3.110.0-to-3.70.1", inventory[lane])
            self.assertIn("3.70.1-to-3.110.0", inventory[lane])
            self.assertIn("3.115.0-to-3.70.1", inventory[lane])
            self.assertIn("3.70.1-to-3.115.0", inventory[lane])
        self.assertEqual(set(inventory), set(qualification.SCENARIOS + qualification.ADDITIONS))
        self.assertIn("3.115.0-fenced-effect", inventory["mixed-api"])
        self.assertIn("3.115.0-trusted-effect", inventory["mixed-api"])

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
        selected={"candidate":{"version":"3.115.0"},"comparisons":[{"version":v} for v in qualification.COMPARISON_VERSIONS],
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
            worker=self.worker(scratch);worker.inputs={'candidate':{'version':'3.115.0'}}
            def metadata(lane,identity,measurements):
                floor=5 if identity.startswith('source') else 1
                measurements.check('floor-one',floor == 1)
                return {'metadata':{'sequence':12,'floor':floor}},'effect','compatible'
            with mock.patch.object(worker,'metadata_case',side_effect=metadata):
                measured=executor.Measurements();observed,outcome,disposition=worker.checkout_case('legacy-metadata',measured)
            self.assertEqual(observed['comparison']['selected_case'],'3.115.0-pascal-floor-None')
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
                measured=executor.Measurements();_,_,disposition=worker.wire_case('query-wire','3.115.0-3.110.0-json-dual',measured)
            self.assertIn({'id':'preserved:identityAdmissionProof','passed':False},measured.checks)
            self.assertEqual(disposition,'incompatible')

    def testUnexpectedProofFailureRetainsAfterInventoryAndRemainsError(self):
        with tempfile.TemporaryDirectory() as scratch:
            worker=self.worker(scratch);worker.host_port=12345
            observed={'accepted':False,'unexpected':True,'unauthorized_refused':False,'denial':None,'sequence':12,
                'diagnostic':[{'denial_message':'Trusted effect denial audit is unavailable.'}]}
            with mock.patch.object(worker,'seed'),mock.patch.object(worker,'inventory',return_value=[]), \
                 mock.patch.object(worker,'start_nodes'),mock.patch.object(worker,'stop_nodes'), \
                 mock.patch.object(worker,'probe',return_value={'actor_methods':['ProcessTrustedEffectAsync']}), \
                 mock.patch.object(worker,'http',return_value=observed):
                measured=executor.Measurements();result,outcome,_=worker.mixed_case('3.115.0-unauthorized-effect',measured)
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
                measured=executor.Measurements();observation,_,_=worker.live_case('metadata-write','3.110.0-to-3.115.0',measured)
            self.assertEqual(starts,['3.115.0','3.115.0'])
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
        identifier='3.115.0-3.110.0-json-'+('dual' if lane=='query-wire' else 'positive')
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
            executor.witnesses.validate_case(row,evidence,binding,'logical-event-evolution',retained)
            self.assertEqual(evidence['commands'][0]['output'],'URLError')
            for output,status in (('URLError',0),('[]',0),(evidence['commands'][1]['output'],124)):
                with self.subTest(output=output,status=status):
                    failed=json.loads(json.dumps(evidence))
                    failed['commands'][1]['output']=output
                    failed['commands'][1]['exit_code']=status
                    with self.assertRaisesRegex(preparation.InvalidPacket,'Domain readiness'):
                        executor.witnesses.validate_case(row,failed,binding,'logical-event-evolution',retained)
            evidence['observations']['domain_registration_observation']['manifest_registered']=True
            evidence['observations']['registered_logical_alias_evolution']['executed']=True
            row['checks'][0]['passed']=True
            evidence['check_witnesses'][0]['predicate']['operands'][0]=True
            row['assertions']=preparation.checks_counter(row['checks'])
            row['disposition']='compatible'
            sha=executor.digest(executor.canonical(evidence['observations']))
            row['inventory']={'before_sha256':sha,'after_sha256':sha}
            with self.assertRaisesRegex(preparation.InvalidPacket,'Domain readiness'):
                executor.witnesses.validate_case(row,evidence,binding,'logical-event-evolution',retained)

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
            row,evidence=worker.case('mixed-api','3.115.0-status',action,False)
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

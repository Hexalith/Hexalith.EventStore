"""Positive and negative isolation controls; synthetic controls grant no qualification."""
import copy
import ast
import inspect
import contextlib
import io
import json
import os
from pathlib import Path
import signal
import runpy
import shutil
import subprocess
import sys
import tempfile
import time
from types import SimpleNamespace
import unittest
from unittest import mock

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import p1r_isolation as isolation
import p1r_qualification as preparation


class PrivateEndpointTests(unittest.TestCase):
    def environment(self):
        return {isolation.MARKER: "a" * 32, "DOCKER_HOST": isolation.SOCKET,
                "DOCKER_CONFIG": isolation.CLIENT_CONFIG, "PATH": os.defpath, "CUSTOM_FIXTURE": "preserved"}

    def testPrivateEndpointIsFixedAndClientOptionsCannotLeak(self):
        env = self.environment() | {"DOCKER_TLS_VERIFY": "1", "DOCKER_CERT_PATH": "/ambient/certs",
                                    "DOCKER_API_VERSION": "1.40", "CONTAINER_CONNECTION": "ambient"}
        cleaned = isolation.private_environment(env)
        self.assertEqual({key for key in cleaned if key.startswith("DOCKER_")}, {"DOCKER_HOST", "DOCKER_CONFIG"})
        self.assertEqual(cleaned["DOCKER_HOST"], isolation.SOCKET)
        self.assertEqual(cleaned["CUSTOM_FIXTURE"], "preserved")
        self.assertNotIn("CONTAINER_CONNECTION", cleaned)

    def testMissingIdentityAndAmbientEndpointsAreRefusedBeforeCommandLaunch(self):
        for replacement in ({isolation.MARKER: ""}, {"DOCKER_HOST": "unix:///var/run/docker.sock"},
                            {"DOCKER_CONFIG": "/ambient"}, {"DOCKER_CONTEXT": "remote"},
                            {"CONTAINER_HOST": "tcp://remote:2375"}):
            with self.subTest(replacement=replacement), self.assertRaises(preparation.InvalidPacket):
                isolation.private_environment(self.environment() | replacement)

    def testNewToolsAreInTheExistingSourceClosureInventory(self):
        for path in ("tools/p1r_isolation.py", "tools/p1r-isolated-runner.py", "tools/tests/test_p1r_isolation.py"):
            self.assertTrue(preparation.relevant(path), path)
        for image in (isolation.SDK_IMAGE, isolation.DAEMON_IMAGE, isolation.PYTHON_IMAGE):
            self.assertRegex(image, "^" + isolation.IMAGE_PATTERN + "$")


class SeparateSourceMountTests(unittest.TestCase):
    def testExactSourceArgumentAndMountAreIndependentOfVerifierRoot(self):
        with tempfile.TemporaryDirectory() as directory:
            base = Path(directory)
            verifier,source = base/'verifier',base/'source'
            verifier.mkdir();source.mkdir()
            inputs = verifier/'inputs.json';inputs.write_text('{}')
            command = ['python3',str(verifier/'tools/p1r-published-executor.py'),'run','--out',str(verifier/'run'),
                       '--inputs',str(inputs),'--source-root',str(source)]
            self.assertEqual(isolation.command_source_root(command),source)
            self.assertEqual(isolation.executor_input(command,verifier),{'path':str(inputs),'sha256':preparation.digest(b'{}')})
            with mock.patch.object(preparation,'declared_dependencies',return_value=['references/Hexalith.Builds']):
                args = isolation.sdk_arguments(verifier,verifier,'a'*32,'daemon','volume',source)
            self.assertIn('type=bind,src='+str(source)+',dst='+str(source),args)
            self.assertIn('type=bind,src='+str(verifier)+',dst='+str(verifier),args)
            for changed in (command+['--source-root',str(verifier)],command[:-1]+[str(source/'..')],
                            command[:7]+['--inputs',str(inputs)]):
                with self.subTest(command=changed), self.assertRaises(preparation.InvalidPacket):
                    isolation.executor_input(changed,verifier)

    def testOrdinaryCheckoutBoundaryRefusesLinkedAndBorrowedMetadata(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root/'.git/objects/info').mkdir(parents=True)
            with mock.patch.object(preparation,'declared_dependencies',return_value=[]), \
                    mock.patch.object(preparation,'git',return_value=str(root/'.git').encode()):
                preparation.self_contained_checkout(root)
                (root/'.git/objects/info/alternates').write_text('/shared/objects')
                with self.assertRaisesRegex(preparation.InvalidPacket,'alternates'):
                    preparation.self_contained_checkout(root)
            shutil.rmtree(root/'.git')
            (root/'.git').write_text('gitdir: /shared/worktrees/candidate')
            with self.assertRaisesRegex(preparation.InvalidPacket,'ordinary Git'):
                preparation.self_contained_checkout(root)


class ControllerTests(unittest.TestCase):
    def testObservePlanningIdentitySurvivesProvisioningAndRefusesChangedBytes(self):
        for changed in (False,True):
            with self.subTest(changed=changed):
                worker = isolation.IsolatedRunner(self.worker.workspace/str(changed),workspace=self.worker.workspace,outer_host='unix:///outer')
                self.addCleanup(lambda path=worker.scratch:shutil.rmtree(path,ignore_errors=True))
                planning = worker.workspace/'planning.json'
                planning.write_text('{"candidate_proposed":{}}')
                command = ['python3',str(worker.workspace/'tools/p1r-published-executor.py'),'observe',
                           '--out',str(worker.workspace/'observation'),'--planning-observations',str(planning),
                           '--selection-reference','fixture']
                def docker(argv, **kwargs):
                    if argv[:1] == ['info']: return 0,b'outer\n'
                    if argv[:2] == ['image','inspect']:
                        return 0,json.dumps({'Id':'sha256:'+'a'*64,'RepoDigests':[argv[-1]],'Os':'linux','Architecture':'amd64'}).encode()
                    if '_proof' in argv and changed:
                        planning.write_text('{"candidate_proposed":{"changed":true}}')
                    if '_inner' in argv:
                        isolation.write_json(worker.output/'private-execution.json',{'passed':True})
                    return 0,b'{}'
                def cleanup(): worker.result['passed'] = not worker.result['errors']
                proof = {'NetworkMode':'container:daemon','PidMode':'container:daemon','Ports':{},'Privileged':False,
                         'Mounts':[{'Destination':'/var/lib/docker'},{'Destination':str(worker.workspace)}]}
                with mock.patch.object(worker,'docker',side_effect=docker) as boundary, \
                        mock.patch.object(worker,'create',side_effect=['daemon','python','sdk']), \
                        mock.patch.object(worker,'private_finished',return_value=True), \
                        mock.patch.object(worker,'inspect',return_value=proof), mock.patch.object(worker,'cleanup',side_effect=cleanup) as cleaned:
                    self.assertEqual(worker._run(command),2 if changed else 0,worker.result['errors'])
                if changed: self.assertIn('selected input changed before executor start',worker.result['errors'])
                self.assertEqual(any('_inner' in call.args[0] for call in boundary.call_args_list),not changed)
                cleaned.assert_called_once()

    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        root = Path(self.temporary.name)
        patcher = mock.patch.object(isolation, "ROOT", root)
        patcher.start()
        self.addCleanup(patcher.stop)
        declarations = mock.patch.object(preparation, "declared_dependencies", return_value=[])
        declarations.start()
        self.addCleanup(declarations.stop)
        self.worker = isolation.IsolatedRunner(root / "evidence", workspace=root, outer_host="unix:///explicit-outer.sock")
        self.addCleanup(lambda: __import__("shutil").rmtree(self.worker.scratch, ignore_errors=True))

    def observed(self, identity="owned-id", label=None):
        return {"Id": identity, "Image": "sha256:image", "Name": "/owned", "Labels": {
            isolation.LABEL: self.worker.invocation if label is None else label}}

    def testNonUtf8DockerOutputRetainsRawBytesAndCannotPass(self):
        import base64
        completed = SimpleNamespace(returncode=0, stdout=b'fixture-\xff-output')
        with mock.patch.object(subprocess, 'run', return_value=completed):
            self.worker.docker(['info'])
        row = self.worker.result['commands'][-1]
        self.assertEqual(base64.b64decode(row['raw_output_base64']), completed.stdout)
        self.assertEqual(row['raw_output_sha256'], preparation.digest(completed.stdout))
        self.assertEqual(row['output_sha256'], preparation.digest(row['output'].encode()))
        self.worker.result['command_exit_code'] = 0
        self.worker.cleanup()
        self.assertFalse(self.worker.result['passed'])
        self.assertIn('outer Docker output is not UTF-8', self.worker.result['errors'])

    def testDockerPreservesOriginalSigintAndSigtermExceptionsAndRetainsCommand(self):
        for signum in (signal.SIGINT, signal.SIGTERM):
            original = isolation.IsolationInterrupted(signum)
            with self.subTest(signum=signum), mock.patch.object(subprocess, "run", side_effect=original):
                with self.assertRaises(isolation.IsolationInterrupted) as caught:
                    self.worker.docker(["info"])
            self.assertIs(caught.exception, original)
            self.assertEqual(caught.exception.signum, signum)
            retained = json.loads((self.worker.output / "isolation.json").read_bytes())
            self.assertEqual(retained["commands"][-1]["exit_code"], 130)
            self.assertEqual(retained["commands"][-1]["output"], "outer command interrupted")

    def testCleanupRetriesExactOwnedTargetsAndRetainsTransientFailure(self):
        worker = self.worker
        worker.containers = {"first-id": {"role": "daemon"}, "last-id": {"role": "sdk"}}
        worker.result["command_exit_code"] = 0
        visited, present = [], {"first-id", "last-id"}
        def docker(argv, **kwargs):
            visited.append(argv)
            if argv[:2] == ["rm", "-f"]:
                if argv[2] == "last-id" and sum(row == argv for row in visited) == 1:
                    raise preparation.InvalidPacket("injected removal failure")
                present.remove(argv[2])
            return 0, b""
        with mock.patch.object(worker, "docker", side_effect=docker), mock.patch.object(
                worker, "inspect", side_effect=lambda value: self.observed(value) if value in present else None):
            worker.cleanup()
        self.assertEqual([argv for argv in visited if argv[:1] == ["rm"]],
                         [["rm", "-f", "last-id"], ["rm", "-f", "first-id"], ["rm", "-f", "last-id"]])
        self.assertEqual(worker.result["cleanup"][-1]["remaining"], [])
        self.assertFalse(worker.result["passed"])

    def testCleanupNeverRemovesAnIdentityWithAChangedOwnershipLabel(self):
        worker = self.worker
        worker.containers = {"reused-id": {"role": "sdk"}}
        worker.result["command_exit_code"] = 0
        with mock.patch.object(worker, "inspect", return_value=self.observed("reused-id", "other-invocation")), \
                mock.patch.object(worker, "docker") as docker:
            worker.cleanup()
        docker.assert_not_called()
        self.assertTrue(all(row["remaining"] == ["reused-id"] for row in worker.result["cleanup"]))
        self.assertFalse(worker.result["passed"])

    def testInterruptedCreationIsRecoveredByExactNameAndLabel(self):
        worker = self.worker
        name = "p1r-isolation-" + worker.invocation + "-sdk"
        observed = self.observed("created-id") | {"Name": "/" + name}
        with mock.patch.object(worker, "docker", side_effect=KeyboardInterrupt), mock.patch.object(worker, "inspect", return_value=observed):
            with self.assertRaises(KeyboardInterrupt):
                worker.create("sdk", isolation.SDK_IMAGE, [])
        self.assertEqual(worker.pending, {})
        self.assertIn("created-id", worker.containers)

    def testFailedStartupPreservesErrorAndRunsBothCleanupAttempts(self):
        worker = self.worker
        with mock.patch.object(worker, "docker", side_effect=preparation.InvalidPacket("daemon unavailable")):
            code = worker.run(["python3", "verification.py"])
        self.assertEqual(code, 2)
        self.assertEqual(len(worker.result["cleanup"]), 2)
        self.assertIn("daemon unavailable", worker.result["errors"])
        self.assertFalse(worker.result["passed"])

    def testVolumeRemovalRequiresExactNameAndInvocationLabel(self):
        worker = self.worker
        worker.volume_created = True
        worker.result["command_exit_code"] = 0
        with mock.patch.object(worker, "docker", return_value=(0, json.dumps({"Name": worker.volume,
                "Labels": {isolation.LABEL: "unowned"}}).encode())) as docker:
            worker.cleanup()
        self.assertTrue(all(call.args[0][:2] == ["volume", "inspect"] for call in docker.call_args_list))
        self.assertFalse(worker.result["passed"])

    def testSuccessfulPlanSharesOnlyPrivateNamespacesAndNeverPublishesRunnerPorts(self):
        worker = self.worker
        plans, present = {}, set()
        volume_present = True
        def create(role, image, arguments, command=()):
            plans[role] = (image, arguments, command)
            worker.containers[role] = {"role": role, "image": image}
            present.add(role)
            return role
        def docker(argv, **kwargs):
            nonlocal volume_present
            if argv[:2] == ["info", "--format"]:
                return 0, b"outer-id\n"
            if argv[:2] == ["image", "inspect"]:
                return 0,json.dumps({"Id":"sha256:"+"a"*64,"RepoDigests":[argv[-1]],"Os":"linux","Architecture":"amd64"}).encode()
            if argv[:1] == ["version"] or "_proof" in argv:
                return 0,b"{}"
            if argv[:2] == ["exec", "sdk"]:
                isolation.write_json(worker.output / "private-execution.json", {"passed": True,
                    "invocation": worker.invocation, "finished_utc": preparation.stamp(),
                    "cleanup": [{"remaining": [], "errors": []}] * 2})
            if argv[:2] == ["rm", "-f"]:
                present.remove(argv[2])
            if argv[:2] == ["volume", "inspect"]:
                if not volume_present:
                    return 1,b"no such volume"
                return 0, json.dumps({"Name": worker.volume, "Labels": {isolation.LABEL: worker.invocation}}).encode()
            if argv[:2] == ["volume","rm"]:
                volume_present = False
            return 0, b""
        def inspect(identity):
            if identity not in present:
                return None
            observed = self.observed(identity)
            if identity == "sdk":
                observed.update(NetworkMode="container:daemon", PidMode="container:daemon", Ports={}, Privileged=False,
                                Mounts=[{"Destination": "/var/lib/docker"}, {"Destination": str(worker.workspace)}])
            return observed
        with mock.patch.object(worker, "create", side_effect=create), mock.patch.object(worker, "docker", side_effect=docker), \
                mock.patch.object(worker, "inspect", side_effect=inspect):
            self.assertEqual(worker.run(["python3", "verification.py"]), 0)
        sdk_options = plans["sdk"][1]
        self.assertEqual(sdk_options[sdk_options.index("--network") + 1], "container:daemon")
        self.assertEqual(sdk_options[sdk_options.index("--pid") + 1], "container:daemon")
        self.assertNotIn("--privileged", sdk_options)
        self.assertTrue(all("docker.sock" not in arg or arg == "DOCKER_HOST=" + isolation.SOCKET for arg in sdk_options))
        self.assertTrue(all(arg not in ("host", "-p", "--publish", "--publish-all") for arg in sdk_options))
        self.assertEqual(plans["daemon"][2][0], "--host=" + isolation.SOCKET)
        self.assertTrue(worker.result["passed"])

    def testUnmountedOutputRefusesBeforeAnyDockerProvisioningAndRetainsReason(self):
        output = Path(self.temporary.name).parent / ("p1r-unmounted-" + self.worker.invocation)
        self.addCleanup(lambda: shutil.rmtree(output, ignore_errors=True))
        with mock.patch.object(isolation.IsolatedRunner, "docker") as docker:
            with self.assertRaisesRegex(preparation.InvalidPacket, "outside the mounted workspace"):
                isolation.IsolatedRunner(output, workspace=Path(self.temporary.name), outer_host="unix:///outer")
        docker.assert_not_called()
        result = json.loads((output / "isolation.json").read_bytes())
        self.assertFalse(result["passed"])
        self.assertIn("outside the mounted workspace", result["errors"][0])
        self.assertEqual(len(result["cleanup"]), 2)

    def testUnexpectedOrUnobservableInnerContainersPreserveDaemonAndVolume(self):
        for status, data in ((0, b"unexpected-inner-id\n"), (1, b"private socket unavailable")):
            worker = self.worker
            worker.containers = {"daemon-id": {"role": "daemon"}, "sdk-id": {"role": "sdk"}}
            worker.volume_created = True
            worker.result.update(command_started=True, daemon="daemon-id", sdk="sdk-id", command_exit_code=0)
            worker.result["cleanup"] = []
            isolation.write_json(worker.output / "private-execution.json", {"invocation": worker.invocation,
                "finished_utc": preparation.stamp(), "cleanup": [{"remaining": [], "errors": []}] * 2})
            with mock.patch.object(worker, "docker", return_value=(status, data)) as docker, \
                    mock.patch.object(worker, "inspect", side_effect=lambda identity: self.observed(identity)):
                worker.cleanup()
            removed = [call.args[0] for call in docker.call_args_list if call.args[0][:1] == ["rm"]]
            self.assertTrue(all(argv == ["rm", "-f", "sdk-id"] for argv in removed))
            self.assertFalse(any(call.args[0][:2] == ["volume", "rm"] for call in docker.call_args_list))
            self.assertFalse(worker.result["passed"])
            self.assertTrue(all("daemon-id" in row["remaining"] and "volume:" + worker.volume in row["remaining"]
                                for row in worker.result["cleanup"]))

    def testControllerRequestsSupervisorInterruptionBeforeAnyOuterRemoval(self):
        worker = self.worker
        worker.result.update(command_started=True, sdk="sdk-id")
        order = []
        with mock.patch.object(worker, "private_finished", side_effect=[False, False, True, True]), \
                mock.patch.object(worker, "docker", side_effect=lambda argv, **kwargs: order.append(argv) or (0, b"")), \
                mock.patch.object(isolation.time, "sleep"):
            worker.orderly_interrupt()
        self.assertEqual(order[0][:4], ["exec", "sdk-id", "python3", isolation.ROOT / "tools/p1r-isolated-runner.py"])
        self.assertIn("_interrupt", order[0])
        self.assertIn(worker.invocation, order[0])

    def testLatePendingCreationIsFoundOnRetryAndRemovedOnlyByExactOwnership(self):
        worker = self.worker
        name = 'p1r-isolation-'+worker.invocation+'-late'
        worker.pending[name] = {'role':'sdk','image':isolation.SDK_IMAGE}
        worker.result['command_exit_code'] = 0
        inspections,present = 0,False
        def inspect(identity):
            nonlocal inspections,present
            if identity == name:
                inspections += 1
                if inspections == 1:
                    return None
                present = True
            return self.observed('late-id') | {'Name':'/'+name} if present else None
        def docker(argv,**kwargs):
            nonlocal present
            if argv[:2] == ['rm','-f']:
                self.assertEqual(argv[2],'late-id')
                present = False
            return 0,b''
        with mock.patch.object(worker,'inspect',side_effect=inspect),mock.patch.object(worker,'docker',side_effect=docker):
            worker.cleanup()
        self.assertEqual(inspections,2)
        self.assertEqual(worker.pending,{})
        self.assertIn('late-id',worker.result['cleanup'][-1]['absent_containers'])
        self.assertFalse(worker.result['passed'])

    def testUnresolvedPendingCreationRemainsRegisteredAndPreservesRecoveryScratch(self):
        worker = self.worker
        name = 'p1r-isolation-'+worker.invocation+'-late'
        worker.pending[name] = {'role':'sdk','image':isolation.SDK_IMAGE}
        worker.result['command_exit_code'] = 0
        with mock.patch.object(worker,'inspect',return_value=None),mock.patch.object(worker,'docker') as docker:
            worker.cleanup()
        self.assertIn(name,worker.pending)
        self.assertTrue(worker.scratch.exists())
        self.assertFalse(worker.result['passed'])
        docker.assert_not_called()

    def testSuccessfulRemovalWithoutObservedContainerOrVolumeAbsenceCannotPass(self):
        for kind in ('container','volume'):
            worker = self.worker
            worker.result.update(command_exit_code=0,cleanup=[])
            worker.containers = {'still-present':{'role':'sdk'}} if kind=='container' else {}
            worker.volume_created = kind=='volume'
            volume = {'Name':worker.volume,'Labels':{isolation.LABEL:worker.invocation}}
            with mock.patch.object(worker,'inspect',return_value=self.observed('still-present')), \
                    mock.patch.object(worker,'docker',return_value=(0,json.dumps(volume).encode())):
                worker.cleanup()
            self.assertFalse(worker.result['passed'])
            self.assertTrue(all(row['remaining'] for row in worker.result['cleanup']))


class AutomaticRoutingTests(unittest.TestCase):
    def testActualObserveAndRunRoutingPreserveCallerPathsAndStructuredResponse(self):
        entry = runpy.run_path(str(preparation.ROOT / "tools/p1r-published-executor.py"))
        for kind in ("observe", "run"):
            with tempfile.TemporaryDirectory() as directory:
                original = Path.cwd()
                root = Path(directory)
                (root / "planning.json").write_text("{}")
                observed, loads = [], []
                worker = mock.Mock(errors=[], result={})
                class Runner:
                    def __init__(self, output):
                        self.output, self.result = output, {"errors": []}
                    def run(self, argv):
                        observed.append(argv)
                        with mock.patch.dict(os.environ, {isolation.MARKER: "a" * 32,isolation.OUTER_MARKER:"outer"}), \
                                mock.patch.object(isolation,"private_proof",return_value={}), contextlib.redirect_stdout(io.StringIO()) as stdout:
                            os.chdir(preparation.ROOT)
                            try:
                                code = entry["main"](argv[2:])
                            finally:
                                os.chdir(root)
                        self.result["command_stdout"] = stdout.getvalue()
                        return code
                def load(path, validator):
                    loads.append(path)
                    return {"candidate": {}}, b"inputs", "owner-selected"
                try:
                    os.chdir(root)
                    with mock.patch.dict(os.environ, {}, clear=True), mock.patch.object(isolation, "IsolatedRunner", Runner), \
                            mock.patch.dict(entry["main"].__globals__, Executor=mock.Mock(return_value=worker),
                                            validate_execution_inputs=mock.Mock(), validate_observation_inputs=mock.Mock()), \
                            mock.patch.object(entry["qualification"], "load_document", side_effect=load), \
                            contextlib.redirect_stdout(io.StringIO()) as stdout:
                        arguments = [kind, "--out", "results"] + (["--inputs", "inputs.json"] if kind == "run" else
                            ["--planning-observations", "planning.json", "--selection-reference", "retained-reference"])
                        self.assertEqual(entry["main"](arguments), 0)
                    response = json.loads(stdout.getvalue().splitlines()[-1])
                    self.assertEqual(response["out"], str(root / "results"))
                    self.assertEqual(response["isolation_out"], str(root / "results-isolation"))
                    self.assertEqual(response["errors"], [])
                    self.assertIn(str(root / "results"), observed[0])
                    self.assertIn(str(root / ("inputs.json" if kind == "run" else "planning.json")), observed[0])
                    if kind == "run":
                        self.assertTrue(all(path == root / "inputs.json" for path in loads))
                finally:
                    os.chdir(original)

    def testImmutableAdmissionRefusesBeforeAutomaticProvisioning(self):
        entry = runpy.run_path(str(preparation.ROOT / "tools/p1r-published-executor.py"))
        with mock.patch.dict(os.environ, {}, clear=True), mock.patch.object(isolation, "IsolatedRunner") as runner, \
                mock.patch.object(entry["qualification"], "load_document", return_value=({}, b"", "owner-selected")), \
                mock.patch.dict(entry["main"].__globals__, validate_execution_inputs=mock.Mock(side_effect=preparation.InvalidPacket("exact-tag refusal"))), \
                contextlib.redirect_stderr(io.StringIO()):
            self.assertEqual(entry["main"](["run", "--out", "result", "--inputs", "inputs.json"]), 2)
        runner.assert_not_called()


class RealPrivateProofTests(unittest.TestCase):
    def assertDockerVersionProduction(self, producer):
        versions = {'Client':{'Version':'29.8.2'},'Server':{'Version':'29.8.2'}}
        daemon = {'ID':'private','ServerVersion':'29.8.2','DockerRootDir':'/var/lib/docker/data'}
        environment = {isolation.MARKER:'a'*32,'DOCKER_HOST':isolation.SOCKET,'DOCKER_CONFIG':isolation.CLIENT_CONFIG}
        own = {'net':'net:[1]','pid':'pid:[2]','mnt':'mnt:[3]'}
        with tempfile.TemporaryDirectory() as directory:
            executables = {name:Path(directory)/name for name in ('docker','dotnet')}
            for name,path in executables.items():
                path.write_bytes(('synthetic '+name+' executable').encode())
            def output(argv,**kwargs):
                self.assertEqual(kwargs['env']['DOCKER_HOST'],isolation.SOCKET)
                if argv == ['docker','info','--format','{{json .}}']:
                    return json.dumps(daemon).encode()
                if argv == ['docker','version','--format','{{json .}}']:
                    return json.dumps(versions).encode()
                self.assertEqual(argv,['dotnet','--version'])
                return b'10.0.401'
            original_exists,original_read = Path.exists,Path.read_text
            def read(path,*args,**kwargs):
                if str(path)=='/var/lib/docker/p1r-run/dockerd.pid':return '7'
                if str(path)=='/proc/7/comm':return 'dockerd'
                return original_read(path,*args,**kwargs)
            with mock.patch.dict(os.environ,environment,clear=True), \
                    mock.patch.object(Path,'is_socket',return_value=True), \
                    mock.patch.object(Path,'exists',lambda path:False if str(path)=='/var/run/docker.sock' else original_exists(path)), \
                    mock.patch.object(Path,'read_text',read), \
                    mock.patch.object(subprocess,'check_output',side_effect=output) as commands, \
                    mock.patch.object(shutil,'which',side_effect=lambda name:str(executables[name])), \
                    mock.patch.object(preparation,'process_state',return_value={'pid':7,'start_ticks':1,'pgid':1,'session':1}), \
                    mock.patch.object(isolation,'namespaces',return_value=own):
                proof = producer('outer')
            self.assertEqual(proof.get('docker_version'),versions,'private_proof omitted client/server version evidence')
            self.assertEqual(proof['daemon']['ServerVersion'],versions['Server']['Version'])
            self.assertEqual([call.args[0] for call in commands.call_args_list].count(
                ['docker','version','--format','{{json .}}']),1)

    def testActualPrivateProofRetainsMeasuredDockerClientAndServerVersions(self):
        self.assertDockerVersionProduction(isolation.private_proof)

    def testDockerVersionOmissionBreaksTheActualPrivateProofPositiveControl(self):
        original = isolation.private_proof
        class OmitVersion(ast.NodeTransformer):
            def visit_Dict(self,node):
                kept = [(key,value) for key,value in zip(node.keys,node.values)
                        if not isinstance(key,ast.Constant) or key.value!='docker_version']
                node.keys,node.values = [key for key,_ in kept],[value for _,value in kept]
                return self.generic_visit(node)
        tree = ast.fix_missing_locations(OmitVersion().visit(ast.parse(inspect.getsource(original))))
        namespace = {}
        exec(compile(tree,original.__code__.co_filename,'exec'),original.__globals__,namespace)
        with self.assertRaisesRegex(AssertionError,'private_proof omitted'):
            self.assertDockerVersionProduction(namespace['private_proof'])

    def testMarkedExecutorAdmissionMeasuresActualPrivateProofBeforeLaunching(self):
        entry = runpy.run_path(str(preparation.ROOT/'tools/p1r-published-executor.py'))
        for mismatch in (False,True):
            with tempfile.TemporaryDirectory() as directory:
                path = Path(directory)/'planning.json'
                path.write_text('{}')
                env = {isolation.MARKER:'a'*32,isolation.OUTER_MARKER:'outer','DOCKER_HOST':isolation.SOCKET,
                       'DOCKER_CONFIG':isolation.CLIENT_CONFIG,'PATH':os.defpath}
                own = {'net':'net:[1]','pid':'pid:[2]','mnt':'mnt:[3]'}
                daemon = {'ID':'outer' if mismatch else 'private','DockerRootDir':'/var/lib/docker/data'}
                def output(argv,**kwargs):
                    return b'10.0.401' if argv[:2]==['dotnet','--version'] else json.dumps(daemon).encode()
                exists = Path.exists
                executables = {name:str(Path(shutil.which(name)).resolve()) for name in ('docker','dotnet')}
                with mock.patch.dict(os.environ,env,clear=True), \
                        mock.patch.object(Path,'is_socket',return_value=True), \
                        mock.patch.object(Path,'exists',lambda path:False if str(path)=='/var/run/docker.sock' else exists(path)), \
                        mock.patch.object(Path,'read_text',lambda path,*a,**k:'7' if str(path).endswith('dockerd.pid') else 'dockerd'), \
                        mock.patch.object(subprocess,'check_output',side_effect=output), \
                        mock.patch.object(shutil,'which',side_effect=lambda name:executables[name]), \
                        mock.patch.object(preparation,'process_state',return_value={'pid':7,'start_ticks':1,'pgid':1,'session':1}), \
                        mock.patch.object(isolation,'namespaces',return_value=own), \
                        mock.patch.dict(entry['main'].__globals__,Executor=mock.Mock(return_value=mock.Mock(errors=[])),
                                        validate_observation_inputs=mock.Mock()) as globals_patch, \
                        contextlib.redirect_stdout(io.StringIO()),contextlib.redirect_stderr(io.StringIO()) as stderr:
                    factory = entry['main'].__globals__['Executor']
                    code = entry['main'](['observe','--out',str(Path(directory)/'out'),'--planning-observations',str(path),
                                          '--selection-reference','retained'])
                    self.assertEqual(code,2 if mismatch else 0,stderr.getvalue())
                    if mismatch:
                        factory.assert_not_called()
                    else:
                        factory.assert_called_once()
    def testRealProofRejectsSameDaemonAndEachNamespaceMismatchBeforeChildLaunch(self):
        for bad in ("daemon", "net", "pid"):
            with tempfile.TemporaryDirectory() as directory:
                environment = {isolation.MARKER: "a" * 32, "DOCKER_HOST": isolation.SOCKET, "DOCKER_CONFIG": isolation.CLIENT_CONFIG}
                own = {"net": "net:[1]", "pid": "pid:[2]", "mnt": "mnt:[3]"}
                daemon_ns = dict(own)
                if bad != "daemon":
                    daemon_ns[bad] = "substituted:[99]"
                daemon = {"ID": "outer" if bad == "daemon" else "private", "DockerRootDir": "/var/lib/docker/data"}
                read_text = lambda path, *args, **kwargs: "7" if str(path).endswith("dockerd.pid") else "dockerd"
                with mock.patch.dict(os.environ, environment, clear=True), \
                        mock.patch.object(Path, "is_socket", return_value=True), mock.patch.object(Path, "exists", return_value=False), \
                        mock.patch.object(Path, "read_text", read_text), \
                        mock.patch.object(subprocess, "check_output", return_value=json.dumps(daemon).encode()), \
                        mock.patch.object(preparation, "process_state", wraps=preparation.process_state) as process_state, \
                        mock.patch.object(isolation, "namespaces", side_effect=[own, daemon_ns]), \
                        mock.patch.object(subprocess, "Popen") as child:
                    # The proof predicates execute unchanged; only OS/Docker dependencies are supplied.
                    process_state.side_effect = lambda pid: {"pid": pid, "start_ticks": 1, "pgid": 1, "session": 1}
                    self.assertEqual(isolation.inner(Path(directory), "outer", ["must-not-launch"]), 2)
                child.assert_not_called()
                result = json.loads((Path(directory) / "private-execution.json").read_bytes())
                self.assertFalse(result["passed"])
                self.assertIn("daemon identity" if bad == "daemon" else "outside private daemon namespaces", result["errors"][0])


class PrivateCommandTests(unittest.TestCase):
    def testNonceOwnedDetachedChildMutatingSourceOnSigtermMakesFinalProofFail(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            source,ready = root/'source.py',root/'ready'
            source.write_text('frozen fixture')
            binding = {'path':str(source),'sha256':preparation.digest(source.read_bytes())}
            child = ("import signal,time\nfrom pathlib import Path\n"
                     "def cleanup(*args):\n Path("+repr(str(source))+").write_text('mutated by owned cleanup')\n raise SystemExit(0)\n"
                     "signal.signal(signal.SIGTERM,cleanup)\nPath("+repr(str(ready))+").write_text('ready')\ntime.sleep(30)\n")
            parent = ("import subprocess,sys,time\nfrom pathlib import Path\n"
                      "subprocess.Popen([sys.executable,'-c',"+repr(child)+"],start_new_session=True)\n"
                      "while not Path("+repr(str(ready))+").exists(): time.sleep(.01)\n")
            def current(value):
                preparation.require(preparation.digest(Path(value['path']).read_bytes())==value['sha256'],'source changed after cleanup')
            with mock.patch.object(isolation,'private_proof',return_value={}), \
                    mock.patch.object(isolation,'private_environment',return_value=dict(os.environ)), \
                    mock.patch.object(preparation,'source_binding',return_value=binding), \
                    mock.patch.object(preparation,'check_current_source',side_effect=current):
                self.assertEqual(isolation.inner(root/'out','outer',[sys.executable,'-c',parent]),2)
            result = json.loads((root/'out/private-execution.json').read_bytes())
            self.assertEqual(result['exit_code'],0)
            self.assertIn('source changed after cleanup',result['errors'])
            self.assertFalse(result['passed'])
            self.assertTrue(all(not row['remaining'] and not row['errors'] for row in result['cleanup']))
    def testActualSigintAndSigtermAllowChildCleanupBeforeFinalSupervisorReceipt(self):
        for sig in (signal.SIGINT, signal.SIGTERM):
            with tempfile.TemporaryDirectory() as directory:
                root = Path(directory)
                ready, cleaned = root / "ready", root / "child-cleaned"
                child = ("from pathlib import Path\nimport time\n"
                         "try:\n Path(" + repr(str(ready)) + ").write_text('ready')\n time.sleep(30)\n"
                         "except KeyboardInterrupt:\n Path(" + repr(str(cleaned)) + ").write_text('orderly')\n")
                script = ("import os,sys\nfrom pathlib import Path\nfrom unittest import mock\n"
                          "sys.path.insert(0," + repr(str(preparation.ROOT / "tools")) + ")\n"
                          "import p1r_isolation as i\nimport p1r_qualification as p\n"
                          "with mock.patch.object(i,'private_proof',return_value={}), "
                          "mock.patch.object(i,'private_environment',return_value=dict(os.environ)), "
                          "mock.patch.object(p,'source_binding',return_value={}), "
                          "mock.patch.object(p,'check_current_source'):\n"
                          " raise SystemExit(i.inner(Path(" + repr(directory) + "),'outer',[sys.executable,'-c'," + repr(child) + "]))\n")
                process = subprocess.Popen([sys.executable, "-c", script], stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
                try:
                    deadline = time.monotonic() + 10
                    while not ready.exists() and process.poll() is None and time.monotonic() < deadline:
                        time.sleep(.02)
                    self.assertTrue(ready.exists(), process.communicate(timeout=2)[0].decode() if process.poll() is not None else "child not ready")
                    process.send_signal(sig)
                    output, _ = process.communicate(timeout=15)
                    self.assertEqual(process.returncode, 2, output.decode())
                    self.assertEqual(cleaned.read_text(), "orderly")
                    result = json.loads((root / "private-execution.json").read_bytes())
                    self.assertEqual(result["interrupted_signal"], sig)
                    self.assertEqual(len(result["cleanup"]), 2)
                    self.assertTrue(all(not row["remaining"] and not row["errors"] for row in result["cleanup"]))
                    self.assertFalse(result["passed"])
                finally:
                    if process.poll() is None:
                        process.kill()
                        process.communicate()

    def run_inner(self, command, popen=None):
        with tempfile.TemporaryDirectory() as temporary, \
                mock.patch.object(isolation, "private_proof", return_value={"daemon": {"ID": "private"}}), \
                mock.patch.object(isolation, "private_environment", return_value=dict(os.environ)), \
                mock.patch.object(preparation, "source_binding", return_value={"frozen": True}), \
                mock.patch.object(preparation, "check_current_source"):
            if popen is None:
                code = isolation.inner(Path(temporary), "outer", command)
            else:
                with mock.patch.object(subprocess, "Popen", side_effect=popen):
                    code = isolation.inner(Path(temporary), "outer", command)
            return code, json.loads((Path(temporary) / "private-execution.json").read_bytes())

    def testSuccessfulAndFailedCommandsBothRetainRepeatedCleanup(self):
        for exit_code in (0, 7):
            code, result = self.run_inner([sys.executable, "-c", "raise SystemExit(" + str(exit_code) + ")"])
            self.assertEqual(result["exit_code"], exit_code)
            self.assertEqual(result["passed"], exit_code == 0)
            self.assertEqual(code, 0 if exit_code == 0 else 2)
            self.assertEqual(len(result["cleanup"]), 2)
            self.assertEqual(result['root']['pid'],result['root']['pgid'])
            self.assertEqual(result['root']['pid'],result['root']['session'])
            self.assertIn(result['root'],result['owned_processes'])
            self.assertEqual([row['id'] for row in result['cleanup']],[1,2])
            self.assertTrue(all(result['root'] in row['owned_processes'] for row in result['cleanup']))
            self.assertTrue(all(preparation.validate_times(row) for row in result['cleanup']))
            self.assertTrue(all(not row["remaining"] and not row["errors"] for row in result["cleanup"]))

    def testStartupErrorAndInterruptionAreNonpassingAndRetained(self):
        for error in (OSError("injected startup failure"), KeyboardInterrupt()):
            code, result = self.run_inner(["unused"], popen=error)
            self.assertEqual(code, 2)
            self.assertFalse(result["passed"])
            self.assertEqual(result["errors"], [type(error).__name__])

    def testPrivateProofFailurePreventsAnyCommand(self):
        with tempfile.TemporaryDirectory() as temporary, \
                mock.patch.object(isolation, "private_proof", side_effect=preparation.InvalidPacket("namespace mismatch")), \
                mock.patch.object(subprocess, "Popen") as popen:
            self.assertEqual(isolation.inner(Path(temporary), "outer", ["unused"]), 2)
            result = json.loads((Path(temporary) / "private-execution.json").read_bytes())
        popen.assert_not_called()
        self.assertEqual(result["errors"], ["namespace mismatch"])
        self.assertFalse(result["passed"])


class RepairAttemptTests(unittest.TestCase):
    root = preparation.ROOT / "_bmad-output/implementation-artifacts/evidence/6-1-p1r-31210-blocker-repair-20261014"

    def testDirectRepairDriverRefusesAmbientDockerBeforeCreatingAnything(self):
        with tempfile.TemporaryDirectory() as directory:
            attempt = Path(directory) / "must-not-exist"
            # An ambient call is rejected even if its caller names a mount-safe
            # owner repo; no Docker provisioning occurs before private proof.
            attempt = preparation.ROOT / ("ambient-refusal-" + Path(directory).name)
            process = subprocess.run([sys.executable, str(self.root / "verify-repair.py"), "--attempt", str(attempt),
                                      "--repo", str(preparation.ROOT)], env={"PATH": os.defpath},
                                     stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
            self.assertNotEqual(process.returncode, 0)
            self.assertIn(b"outer daemon identity missing", process.stdout)
            self.assertFalse(attempt.exists())

    def testWholeAttemptFreezesAllScriptsAndRefusesReuse(self):
        entry = runpy.run_path(str(self.root / "verify-and-audit.py"))
        with tempfile.TemporaryDirectory() as directory:
            repo = Path(directory)
            attempt = repo / "attempt"
            argv = [str(self.root / "verify-and-audit.py"), "--attempt", str(attempt), "--repo", str(repo)]
            with mock.patch.object(sys, "argv", argv), mock.patch.object(isolation, "private_proof", return_value={}), \
                    mock.patch.object(os, "execv", side_effect=SystemExit(99)):
                with self.assertRaises(SystemExit):
                    entry["main"]()
                binding = json.loads((attempt / "scripts-binding.json").read_bytes())
                self.assertEqual(set(binding["scripts"]), {"verify-and-audit.py", "verify-repair.py", "independent-repair-audit.py"})
                for row in binding["scripts"].values():
                    self.assertEqual(preparation.digest(Path(row["executed_path"]).read_bytes()), row["sha256"])
                before = (attempt / "scripts-binding.json").read_bytes()
                with self.assertRaises(FileExistsError):
                    entry["main"]()
                self.assertEqual((attempt / "scripts-binding.json").read_bytes(), before)
            # Frozen entry checks physical bytes before launching either stage.
            (attempt / "scripts" / "verify-repair.py").write_text("tampered frozen driver")
            with mock.patch.object(sys, "argv", argv + ["--frozen"]), mock.patch.object(isolation, "private_proof", return_value={}), \
                    mock.patch.object(subprocess, "Popen") as child:
                with self.assertRaisesRegex(preparation.InvalidPacket, "frozen script changed"):
                    entry["main"]()
            child.assert_not_called()

    def testIndependentAuditRequiresTenDistinctLanesAndEveryPersistedRecoveryReceipt(self):
        entry = runpy.run_path(str(self.root / "independent-repair-audit.py"))
        q = SimpleNamespace(require=preparation.require, LANE_RECEIPT_SCHEMA="lane")
        runtime = SimpleNamespace(CLEANUP_SCHEMA="cleanup", RESTORE_SCHEMA="restore")
        with tempfile.TemporaryDirectory() as directory:
            run = Path(directory)
            (run / "receipts").mkdir()
            receipts = [{"schema": "lane", "lane": lane, "cases": [{"id": "case", "checks": [{"id": "check", "passed": True}]}]}
                        for lane in entry["EXPECTED_LANES"]]
            cleanup = {"schema": "cleanup", "invocation": "measurement"}
            # Restore binds its invocation through its complete cleanup receipt,
            # matching the persisted contract rather than inventing a field.
            receipts += [cleanup, {"schema": "restore", "cleanup": copy.deepcopy(cleanup)}]
            paths = []
            for index, receipt in enumerate(receipts):
                path = run / "receipts" / (str(index) + ".json")
                path.write_text(json.dumps(receipt))
                paths.append(str(path))
            execution = {"receipts": paths, "invocation": "measurement"}
            lanes, _, _ = entry["require_inventory"](q, runtime, run, execution, receipts)
            self.assertEqual(entry["reconstructed_counter"](preparation, lanes), {"attempted": 10, "passed": 10, "failed": 0})
            changed = copy.deepcopy(receipts)
            changed[-1]["cleanup"]["invocation"] = "another-invocation"
            with self.assertRaisesRegex(preparation.InvalidPacket, "another measurement"):
                entry["require_inventory"](q, runtime, run, execution, changed)
            changed = copy.deepcopy(receipts)
            changed[-2]["invocation"] = changed[-1]["cleanup"]["invocation"] = "another-invocation"
            with self.assertRaisesRegex(preparation.InvalidPacket, "another measurement"):
                entry["require_inventory"](q, runtime, run, execution, changed)
            changed = copy.deepcopy(receipts)
            changed[0]["lane"] = changed[1]["lane"]
            with self.assertRaisesRegex(preparation.InvalidPacket, "ten distinct"):
                entry["require_inventory"](q, runtime, run, execution, changed)
            with self.assertRaisesRegex(preparation.InvalidPacket, "ten lanes"):
                entry["require_inventory"](q, runtime, run, execution, receipts[:-1])
            (run / "receipts" / "unregistered.json").write_text("{}")
            with self.assertRaisesRegex(preparation.InvalidPacket, "inventories differ"):
                entry["require_inventory"](q, runtime, run, execution, receipts)


if __name__ == "__main__":
    unittest.main()

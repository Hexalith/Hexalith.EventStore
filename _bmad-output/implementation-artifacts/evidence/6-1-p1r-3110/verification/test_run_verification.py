"""Evidence mutation and owned lifecycle controls; no shared resources are touched."""
import copy
import contextlib
import io
import json
import os
import pathlib
import shutil
import signal
import subprocess
import sys
import tempfile
import time
import unittest
from unittest import mock

import run_verification as verifier


class EvidenceTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.directory = pathlib.Path(self.temp.name)
        protected = mock.patch.object(verifier, "preserved", return_value={"acceptance": "unchanged"})
        protected.start()
        self.addCleanup(protected.stop)
        binaries = mock.patch.object(verifier, "shared_binary_paths", return_value={"/fixture/dapr", "/fixture/daprd"})
        binaries.start()
        self.addCleanup(binaries.stop)
        self.manifest = {"schema": "hexalith.p1r.verification.v1", "coordinates": verifier.SOURCES, "versions": list(verifier.VERSIONS), "runtime": {"dapr": "1.18.2", "postgresql": verifier.POSTGRES}, "usable_as_prerequisite": False, "owner_acceptance_granted": False, "preserved_before": {"acceptance": "unchanged"}, "preserved_after": {"acceptance": "unchanged"}, "fixture_hashes": {p: verifier.sha((verifier.HERE / p).read_bytes()) for p in verifier.FIXTURES}}
        self.results = {"qualified": False, "exit_code": 1, "scenarios": [{"id": name, "execution": "unavailable", "compatibility": "unverified", "command_ids": [1], "assertions": 0, "cases": [{"id": "blocked", "assertions": 0}]} for name in verifier.SCENARIOS]}
        self.commands = [{"id": 1, "argv": ["fixture-command"], "cwd": "/fixture", "started_utc": "2026-10-04T01:00:00Z", "finished_utc": "2026-10-04T01:00:01Z", "exit_code": 1, "output_sha256": "a" * 64, "process_id": 10001, "process_returncode": 1}]
        self.cleanup = {"invocation": "fixture-invocation", "owned_processes_stopped": True, "owned_containers_removed": True, "scratch_removed": True, "shared_before": {"shared": "unchanged"}, "shared_after": {"shared": "unchanged"}, "shared_discovery_complete": True}
        self.shared_evidence()
        self.save()

    def tearDown(self):
        self.temp.cleanup()

    def save(self):
        self.finish_nodes()
        if self.cleanup.get("owned_containers") and not any(c.get("fixture_cleanup") for c in self.commands):
            self.saving_cleanup = True
            try:
                for identity in self.cleanup["owned_containers"]:
                    self.add_command(["docker","inspect","--format",verifier.OWNERSHIP_INSPECT_FORMAT,identity], {"id":identity,"invocation":self.cleanup["invocation"]}, fixture_cleanup=True)
                    self.add_command(["docker","rm","-f",identity], fixture_cleanup=True)
                self.add_command(["docker","ps","-aq","--no-trunc","--filter","label=hexalith.p1r.invocation="+self.cleanup["invocation"]], fixture_cleanup=True)
            finally:
                self.saving_cleanup = False
        self.saving_cleanup = True
        try:
            self.shared_after_evidence()
        finally:
            self.saving_cleanup = False
        self.cleanup["owned_processes"] = sorted({c["process_id"] for c in self.commands if "process_id" in c})
        def bind(cases):
            for case in cases:
                case.setdefault("command_ids", [1])
                bind(case.get("cases", []))
        for row in self.results["scenarios"]:
            bind(row["cases"])
        for name, value in (("manifest.json", self.manifest), ("scenario-results.json", self.results), ("commands.json", self.commands), ("cleanup.json", self.cleanup)):
            verifier.write(self.directory / name, value)
        verifier.seal(self.directory)

    def reject(self):
        with self.assertRaises((ValueError, KeyError, OSError, json.JSONDecodeError)):
            verifier.validate(self.directory)

    def test_complete_nonpassing_packet_is_valid_without_qualification(self):
        result = verifier.validate(self.directory)
        self.assertFalse(result["qualified"])

    def test_missing_scenario_is_rejected_even_after_resealing(self):
        self.results["scenarios"].pop()
        self.save()
        self.reject()

    def test_duplicate_scenario_is_rejected(self):
        self.results["scenarios"].append(copy.deepcopy(self.results["scenarios"][0]))
        self.save()
        self.reject()

    def test_mismatched_artifact_bytes_are_rejected(self):
        self.directory.joinpath("commands.json").write_text("{}")
        self.reject()

    def test_unbound_artifact_is_rejected(self):
        self.directory.joinpath("unbound.json").write_text("{}")
        self.reject()

    def test_secret_bearing_receipt_is_rejected(self):
        self.commands[0]["diagnostic"] = 'password=fixture-secret'
        self.save()
        self.reject()

    def test_secret_bearing_json_key_is_rejected(self):
        self.commands[0]["diagnostic"] = {"password": "fixture-secret"}
        self.save()
        self.reject()

    def test_escaped_json_inside_diagnostic_is_rejected_after_resealing(self):
        self.commands[0]["diagnostic"] = '{"password":"fixture-secret"}'
        self.save()
        self.reject()

    def test_redacted_json_inside_diagnostic_is_allowed(self):
        self.commands[0]["diagnostic"] = '{"password":"[redacted]"}'
        self.save()
        verifier.validate(self.directory)

    def test_retained_bare_bearer_token_is_rejected(self):
        self.commands[0]["diagnostic"] = "Bearer fixture-secret"
        self.save()
        self.reject()

    def test_failed_process_cleanup_is_rejected(self):
        self.cleanup["owned_processes_stopped"] = False
        self.save()
        self.reject()

    def test_failed_container_cleanup_is_rejected(self):
        self.cleanup["owned_containers_removed"] = False
        self.save()
        self.reject()

    def test_shared_resource_drift_is_rejected(self):
        self.cleanup["shared_after"] = {}
        self.save()
        self.reject()

    def test_scratch_retention_is_rejected(self):
        self.cleanup["scratch_removed"] = False
        self.save()
        self.reject()

    def test_protected_acceptance_drift_is_rejected(self):
        self.manifest["preserved_after"] = {}
        self.save()
        self.reject()

    def test_usability_claim_is_rejected(self):
        self.manifest["usable_as_prerequisite"] = True
        self.save()
        self.reject()

    def test_qualification_claim_is_rejected(self):
        self.results["qualified"] = True
        self.save()
        self.reject()

    def test_zero_assertion_success_is_rejected(self):
        self.results["scenarios"][0]["execution"] = "passed"
        self.save()
        self.reject()

    def test_missing_case_from_passing_scenario_is_rejected(self):
        row = next(r for r in self.results["scenarios"] if r["id"] == "metadata-read")
        row.update(execution="passed", assertions=1, cases=[{"id": "web-floor-5", "assertions": 1}])
        self.save()
        self.reject()

    def test_coordinate_substitution_is_rejected(self):
        self.manifest["coordinates"] = {**verifier.SOURCES, "rollback_archive": verifier.SOURCES["rollback_tag"]}
        self.save()
        self.reject()

    def test_escaping_checksum_path_is_rejected(self):
        self.directory.joinpath("SHA256SUMS").write_text("a" * 64 + "  ../outside.json\n")
        self.reject()

    def test_database_dump_cannot_be_retained(self):
        self.directory.joinpath("fixture.dump").write_text("dump")
        verifier.seal(self.directory)
        self.reject()

    def test_fixture_hash_drift_is_rejected(self):
        self.manifest["fixture_hashes"]["run_verification.py"] = "a" * 64
        self.save()
        self.reject()

    def test_omitted_fixture_binding_is_rejected_after_resealing(self):
        del self.manifest["fixture_hashes"]["host/Program.cs"]
        self.save()
        self.reject()

    def test_escaping_fixture_binding_is_rejected(self):
        self.manifest["fixture_hashes"]["../outside.py"] = "a" * 64
        self.save()
        self.reject()

    def test_symlink_fixture_is_rejected(self):
        fixtures = self.directory / "fixtures"
        for relative in verifier.FIXTURES:
            destination = fixtures / relative
            destination.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(verifier.HERE / relative, destination)
        path = fixtures / "host/Program.cs"
        path.unlink()
        path.symlink_to(verifier.HERE / "host/Program.cs")
        with mock.patch.object(verifier, "HERE", fixtures):
            self.reject()

    def add_command(self, argv, data=None, output=None, code=0, **extra):
        if not getattr(self, "saving_cleanup", False):
            while self.commands and self.commands[-1].get("fixture_cleanup"):
                self.commands.pop()
        if "request_observation" in extra:
            extra["request_observation_sha256"] = verifier.sha(verifier.canonical(extra["request_observation"]))
            if not any(str(a).startswith("request-sha256=") for a in argv):
                argv = [*argv, "request-sha256="+verifier.sha(verifier.canonical(extra["request_observation"]))]
        if argv[:1] == ["HTTP"] and code == 0:
            code = 200
        rendered = output if output is not None else json.dumps(data, sort_keys=True) + "\n" if data is not None else ""
        instant = verifier.dt.datetime(2026,10,4,1,tzinfo=verifier.dt.timezone.utc) + verifier.dt.timedelta(seconds=2*len(self.commands))
        command = {"id": len(self.commands)+1, "argv": [str(a) for a in argv], "cwd": "/fixture", "started_utc": instant.isoformat(), "finished_utc": (instant+verifier.dt.timedelta(seconds=1)).isoformat(), "exit_code": code, "diagnostic": rendered, "output_sha256": verifier.sha(rendered.encode()), **extra}
        if argv[0] not in {"HTTP", "os.killpg", "read-fixture"}:
            command.update(process_id=10000+command["id"], process_returncode=code)
        self.commands.append(command)
        return command

    def shared_phase_evidence(self, phase, **extra):
        resource = {"id": "f"*64, "image": "sha256:"+"e"*64, "running": True, "started": "2026-10-04T00:00:00Z", "invocation": None}
        discovery = self.add_command(["docker", "ps", "-aq", "--no-trunc"], output=resource["id"]+"\n", **extra)
        inspection = self.add_command(["docker", "inspect", "--format", verifier.RESOURCE_INSPECT_FORMAT, resource["id"]], resource, **extra)
        binaries = {binary: self.add_command([sys.executable, "-c", verifier.FILE_HASH_SCRIPT, binary, "hash-shared-binary"], output="a"*64+"\n", **extra)["id"] for binary in sorted(verifier.shared_binary_paths())}
        self.cleanup.setdefault("shared_observations", {})[phase] = {"discovery_command_id": discovery["id"], "inspection_command_id": inspection["id"], "binary_command_ids": binaries}
        if phase == "before":
            self.cleanup["shared_"+phase] = {resource["id"]: {key:resource[key] for key in ("image", "running", "started")}, **{binary:"a"*64 for binary in binaries}}

    def shared_evidence(self):
        self.shared_phase_evidence("before")
        self.cleanup["shared_after"] = copy.deepcopy(self.cleanup["shared_before"])

    def shared_after_evidence(self):
        self.shared_phase_evidence("after")

    def reseal_cleanup(self):
        verifier.write(self.directory / "cleanup.json", self.cleanup)
        verifier.seal(self.directory)

    def test_shared_snapshot_omissions_and_values_are_rejected_after_resealing(self):
        verifier.validate(self.directory)
        original = copy.deepcopy(self.cleanup)
        for mutation in ("empty", "container", "binary", "value"):
            with self.subTest(mutation=mutation):
                self.cleanup = copy.deepcopy(original)
                for phase in ("before", "after"):
                    snapshot = self.cleanup["shared_"+phase]
                    if mutation == "empty": snapshot.clear()
                    elif mutation == "container": del snapshot["f"*64]
                    elif mutation == "binary": del snapshot["/fixture/dapr"]
                    else: snapshot["f"*64]["running"] = False
                self.reseal_cleanup()
                with self.assertRaisesRegex(ValueError, "Shared snapshot differs"):
                    verifier.validate(self.directory)

    def test_missing_shared_binary_receipt_binding_is_rejected_after_resealing(self):
        for phase in ("before", "after"):
            self.cleanup["shared_observations"][phase]["binary_command_ids"].pop("/fixture/dapr")
            self.cleanup["shared_"+phase].pop("/fixture/dapr")
        self.reseal_cleanup()
        with self.assertRaisesRegex(ValueError, "Missing shared binary observations"):
            verifier.validate(self.directory)

    def test_protected_inventory_omissions_and_hashes_are_rejected_after_resealing(self):
        verifier.validate(self.directory)
        for value in ({}, {"acceptance": "wrong-hash"}):
            with self.subTest(value=value):
                self.manifest["preserved_before"] = value
                self.manifest["preserved_after"] = value
                self.save()
                with self.assertRaisesRegex(ValueError, "Protected inventory differs"):
                    verifier.validate(self.directory)

    def test_omitted_and_substituted_processes_are_rejected_after_resealing(self):
        verifier.validate(self.directory)
        original = list(self.cleanup["owned_processes"])
        for value in ([], original[1:], [99999, *original[1:]]):
            with self.subTest(value=value):
                self.cleanup["owned_processes"] = value
                self.reseal_cleanup()
                with self.assertRaisesRegex(ValueError, "Owned process inventory differs"):
                    verifier.validate(self.directory)

    def test_missing_process_terminal_outcome_is_rejected_after_resealing(self):
        self.commands[0]["process_returncode"] = None
        self.save()
        with self.assertRaisesRegex(ValueError, "Tracked process lacks terminal outcome"):
            verifier.validate(self.directory)

    def test_null_and_wrong_shape_evidence_is_invalid_without_traceback(self):
        for name in ("manifest.json", "scenario-results.json", "commands.json", "cleanup.json"):
            for value in (None, 7, [], "wrong-shape"):
                with self.subTest(name=name, value=value):
                    self.save()
                    verifier.write(self.directory/name, value)
                    verifier.seal(self.directory)
                    with mock.patch("sys.stderr", new_callable=io.StringIO) as stderr:
                        self.assertEqual(verifier.main(["--validate", str(self.directory)]), 2)
                    self.assertIn("INVALID:", stderr.getvalue())
                    self.assertNotIn("Traceback", stderr.getvalue())

    def test_recovered_scheduler_packet_validates_with_only_runtime_containers(self):
        self.persisted_pass()
        failed = "e"*64
        runtime = json.loads((self.directory / "runtime-identity.json").read_text())
        scheduler = runtime["images"]["scheduler"]["id"]
        with tempfile.TemporaryDirectory() as scratch:
            runner = verifier.Runner(self.directory, pathlib.Path(scratch))
            runner.invocation = self.cleanup["invocation"]
            runner.commands = self.commands
            runner.containers = [identity for identity in self.cleanup["owned_containers"] if identity != scheduler]
            attempts = []
            def launch(role, image, arguments, **kwargs):
                attempts.append(role)
                identity = failed if len(attempts) == 1 else scheduler
                (runner.scratch / "scheduler.cid").write_text(identity)
                runner.containers.append(identity)
                self.add_command(["docker", "run", "--name", "p1r-"+runner.invocation+"-scheduler", "--label", "hexalith.p1r.invocation="+runner.invocation, image], output="port is already allocated\n" if identity == failed else identity+"\n", code=125 if identity == failed else 0)
                if identity == failed: raise ValueError("Command exited 125")
                return identity, kwargs["host_port"]
            def execute(argv, **kwargs):
                self.assertEqual(argv[-1], failed)
                data = {"id": failed, "invocation": runner.invocation} if argv[1] == "inspect" else None
                command = self.add_command(argv, data=data, output=None if data else failed+"\n")
                return command["diagnostic"]
            with mock.patch.object(runner, "container", side_effect=launch), mock.patch.object(runner, "run", side_effect=execute):
                self.assertEqual(runner.start_scheduler()[0], scheduler)
            self.cleanup["owned_containers"] = runner.containers
        self.assertEqual(set(self.cleanup["owned_containers"]), {row["id"] for row in runtime["images"].values()})
        self.assertTrue(any(c["argv"] == ["docker", "rm", "-f", failed] and c["exit_code"] == 0 for c in self.commands))
        self.save()
        verifier.validate(self.directory)

    def source_evidence(self):
        closures = {}
        for name, coordinate in verifier.SOURCE_REPOSITORIES.items():
            repository = str(verifier.PROJECTS / "references" / name)
            files = {p:{k:v for k,v in row.items() if k != "permitted_sha256"} for p,row in verifier.committed_source_inputs(name,coordinate).items()}
            for row in files.values(): row["sha256"] = row["committed_sha256"]
            value = {"coordinate":coordinate,"repository":repository,"files":files}
            command = self.add_command([sys.executable,"-c",verifier.SOURCE_CLOSURE_SCRIPT,repository,coordinate,"source-input-closure"],value)
            closures[name] = {**value,"command_id":command["id"],"output":command["diagnostic"]}
            self.add_command(["git","ls-tree",verifier.SOURCES["projects_baseline"],"references/"+name],output="160000 commit "+coordinate+"\treferences/"+name+"\n")
        verifier.write(self.directory / "source-input-closure.json",closures)
        comparison = subprocess.check_output(["git","diff","--name-status",verifier.SOURCES["selected"],verifier.SOURCES["current"],"--","src"],cwd=verifier.ROOT).decode()
        self.add_command(["git","diff","--name-status",verifier.SOURCES["selected"],verifier.SOURCES["current"],"--","src"],output=comparison)
        self.add_command(["git","diff","--exit-code",verifier.SOURCES["rollback_archive"],verifier.SOURCES["rollback_tag"],"--","src","tools/release-packages.json"])
        verifier.write(self.directory / "source-comparison.json",{"coordinates":verifier.SOURCES,"selected_to_current":comparison.splitlines(),"rollback_archive_to_tag_src_equal":True,"current_inventory":{p:r["sha256"] for p,r in closures["Hexalith.EventStore"]["files"].items() if p.startswith("src/")}})

    def graph_evidence(self,lane,project,hashes):
        names=verifier.graph_packages(lane,project)
        libraries={}
        locked={}
        for name in names:
            if lane=="current":
                path=str(verifier.ROOT / "src" / name / (name+".csproj"))
                libraries[name+"/3.110.0"]={"type":"project","msbuildProject":path,"path":path}
                locked[name]={"type":"Project"}
            else:
                content=verifier.PUBLISHED_CONTENT_HASHES[lane][name]
                libraries[name+"/"+lane]={"type":"package","sha512":content}
                locked[name]={"type":"Direct","resolved":lane,"contentHash":content}
        graph={"libraries":libraries,"project":{"restore":{"projectPath":"/fixture/"+lane+"/"+project.lower()+"/"+project+".csproj"}}}
        lock={"version":2,"dependencies":{"net10.0":locked}}
        verifier.write(self.directory / "artifacts" / lane / (project+"-assets.json"),graph)
        verifier.write(self.directory / "artifacts" / lane / (project+"-lock.json"),lock)
        assets="/fixture/"+lane+"/"+project.lower()+"/obj/project.assets.json"
        locked_path="/fixture/"+lane+"/"+project.lower()+"/packages.lock.json"
        self.add_command([sys.executable,"-c",verifier.ARTIFACT_INVENTORY_SCRIPT,assets,locked_path,"hash-restored-graphs"],{assets:verifier.sha(verifier.canonical(graph)),locked_path:verifier.sha(verifier.canonical(lock))})
        self.add_command([sys.executable,"-c",verifier.ASSEMBLY_INVENTORY_SCRIPT,"/fixture/"+lane+"/"+project.lower()+"/bin/"+("Debug" if lane=="current" else "Release")+"/net10.0","hash-built-assemblies"],{name:hashes[name] for name in names})

    def finish_nodes(self):
        instant = verifier.dt.datetime(2026,10,4,1,tzinfo=verifier.dt.timezone.utc) + verifier.dt.timedelta(seconds=2*len(self.commands))
        for command in getattr(self, "fixture_nodes", []):
            command["finished_utc"] = instant.isoformat()
        self.fixture_nodes = []

    def probe_binary(self, lane):
        return "/fixture/"+lane+"/probe/bin/"+("Debug" if lane=="current" else "Release")+"/net10.0/Probe.dll"

    def nodes(self,lane,hashes=None):
        self.finish_nodes()
        if hashes is None:
            hashes=self.current_hashes if lane=="current" else verifier.PUBLISHED_DLL_HASHES[lane]
        for kind,required in (("host",verifier.HOST_REQUIRED),("domain",verifier.DOMAIN_REQUIRED)):
            configuration="Debug" if lane=="current" else "Release"
            application = self.add_command(["dotnet","/fixture/"+lane+"/"+kind+"/bin/"+configuration+"/net10.0/"+kind.capitalize()+".dll"],node_environment={"ASPNETCORE_URLS":"http://127.0.0.1:12345","DAPR_HTTP_PORT":"12345"})
            sidecar = self.add_command(["/fixture/daprd","--app-id","eventstore" if kind=="host" else "counter","--dapr-http-port","12345","--app-port","12345"])
            for command in (application, sidecar):
                command["finished_utc"] = "2099-01-01T00:00:00+00:00"
                self.fixture_nodes.append(command)
            identities=[{"name":name,"version":(verifier.VERSIONS[0] if lane=="current" else lane)+".0","sha256":hashes[name]} for name in sorted(required)]
            self.add_command(["HTTP","GET","http://127.0.0.1:12345"+("/identity" if kind=="host" else "/ready")],identities if kind=="host" else {"ready":True,"assemblies":identities})
            verifier.write(self.directory / "artifacts" / (lane+"-"+kind+"-loaded.json"),identities)

    def runtime_evidence(self):
        invocation="fixture-invocation"
        images={role:{"id":letter*64,"image_id":verifier.POSTGRES.split("@")[1] if role=="postgresql" else "sha256:"+"e"*64} for role,letter in zip(("postgresql","placement","scheduler","pubsub"),"abcd")}
        self.cleanup.update(invocation=invocation,owned_containers=[r["id"] for r in images.values()])
        for role,value in images.items():
            image=verifier.POSTGRES if role=="postgresql" else verifier.RUNTIME if role in {"placement","scheduler"} else "redis:7.4"
            self.add_command(["docker","run","--name","p1r-"+invocation+"-"+role,"--label","hexalith.p1r.invocation="+invocation,image],output=value["id"]+"\n")
            self.add_command(["docker","inspect","--format","{{.Image}}",value["id"]],output=value["image_id"]+"\n")
        digest="e730688f06b9b7cea0d616a7dde8fc0124cb6cc8d6dae149192422c7ea67f4e6"
        self.add_command(["docker","cp",images["placement"]["id"]+":/daprd","/fixture/daprd"])
        self.add_command(["/fixture/daprd","--version"],output="1.18.2\n")
        self.add_command([sys.executable,"-c",verifier.FILE_HASH_SCRIPT,"/fixture/daprd","hash-private-runtime"],output=digest+"\n")
        verifier.write(self.directory / "runtime-identity.json",{"dapr_version":"1.18.2","private_discovery":True,"daprd_sha256":digest,"images":images})

    def state_rows(self):
        rows=[]
        for tenant,count in (("tenant-a",12),("tenant-b",3)):
            for index in range(1,count+1): rows.append({"key":"fixture:"+tenant+":counter:fixture:events:"+str(index),"sha256":verifier.sha((tenant+str(index)).encode()),"sequence":None,"floor":None})
            rows.append({"key":"fixture:"+tenant+":counter:fixture:metadata","sha256":verifier.sha(tenant.encode()),"sequence":str(count),"floor":"1"})
        return sorted(rows,key=lambda r:r["key"])

    def stream_rows(self, start=1, count=12, floor="1", snapshot=None, sequence=None, extra=None):
        rows=[]
        for index in range(start, count+1):
            rows.append({"key":"fixture:tenant-a:counter:fixture:events:"+str(index),"sha256":verifier.sha(("tenant-a"+str(index)).encode()),"sequence":None,"floor":None})
        if extra is not None:
            rows.append({"key":"fixture:tenant-a:counter:fixture:events:"+str(extra),"sha256":verifier.sha(("tenant-a"+str(extra)).encode()),"sequence":None,"floor":None})
        for index in range(1,4):
            rows.append({"key":"fixture:tenant-b:counter:fixture:events:"+str(index),"sha256":verifier.sha(("tenant-b"+str(index)).encode()),"sequence":None,"floor":None})
        meta_sequence=str(count if sequence is None else sequence)
        rows.append({"key":"fixture:tenant-a:counter:fixture:metadata","sha256":verifier.sha(("tenant-a"+meta_sequence+str(floor)).encode()),"sequence":meta_sequence,"floor":floor})
        rows.append({"key":"fixture:tenant-b:counter:fixture:metadata","sha256":verifier.sha(b"tenant-b"),"sequence":"3","floor":"1"})
        if snapshot is not None:
            rows.append({"key":"fixture:tenant-a:counter:fixture:snapshot","sha256":verifier.sha(("snapshot"+str(snapshot)).encode()),"sequence":None,"floor":None,"snapshotSequence":str(snapshot)})
        return sorted(rows,key=lambda r:r["key"])

    def retained_stream_rows(self):
        return self.stream_rows(start=5, floor="5")

    def next_database(self):
        self.database_index=getattr(self,"database_index",0)
        name="p1r_db"+str(self.database_index)
        self.database_index+=1
        return name

    def psql(self, database, statement):
        self.add_command(["docker","exec","a"*64,"psql","-U","postgres","-d",database,"-At","-v","ON_ERROR_STOP=1","-c",statement])

    def inventory_evidence(self,database,rows,name):
        self.finish_nodes()
        if not any("createdb" in c["argv"] and c["argv"][-1] == database for c in self.commands):
            self.add_command(["docker","exec","a"*64,"createdb","-U","postgres",database])
        output=json.dumps(rows)+"\n"
        command=self.add_command(["docker","exec","a"*64,"psql","-U","postgres","-d",database,"-At","-v","ON_ERROR_STOP=1","-c",verifier.INVENTORY_SQL],output=output)
        value={"rows":rows,"domain_rows":rows,"sha256":verifier.sha(verifier.canonical(rows)),"query_command_id":command["id"],"query_output":output,"database_identity_sha256":verifier.sha(database.encode())}
        verifier.write(self.directory / "inventories" / (name+".json"),value)
        return value

    def seed_evidence(self,lane):
        self.nodes(lane)
        for tenant,count in (("tenant-a",12),("tenant-b",3)):
            self.add_command(["dotnet",self.probe_binary(lane),"seed","http://127.0.0.1:12345",tenant,"fixture",str(count)],{"committedEvents":count,"hydratedCount":count,"assertions":2*(count+1)})

    def actor_evidence(self,lane,tenant,count=12,kind="AssertCounter",accepted=True):
        outcome={"accepted":accepted,"eventCount":1 if kind=="IncrementCounter" and accepted else 0,"assertions":1,"failureReason":None,"failureCategory":None,"errorSha256":None}
        command=self.add_command(["dotnet",self.probe_binary(lane),"actor","http://127.0.0.1:12345",tenant,"fixture","fixture-correlation",kind,str(count)],outcome)
        return command,outcome

    def persisted_pass(self):
        self.provenance_pass()
        self.runtime_evidence()
        cases=[]
        for index,(writer,reader) in enumerate((verifier.VERSIONS,tuple(reversed(verifier.VERSIONS)))):
            start=len(self.commands);database="p1r_fixture"+str(index);self.seed_evidence(writer)
            before=self.inventory_evidence(database,self.state_rows(),"before" if index==0 else "before-other")
            self.nodes(reader);_,outcome=self.actor_evidence(reader,"tenant-a");self.actor_evidence(reader,"tenant-b",3)
            self.add_command(["HTTP","GET","http://127.0.0.1:12345/sequence/tenant-a/fixture"],12)
            after=self.inventory_evidence(database,self.state_rows(),"after"+str(index))
            cases.append({"id":writer+"-to-"+reader,"writer":writer,"reader":reader,"variant":"default","assertions":1,"command_ids":[c["id"] for c in self.commands[start:]],"before_sha256":before["sha256"],"after_sha256":after["sha256"],"inventory_commands":{"before_sha256":before["query_command_id"],"after_sha256":after["query_command_id"]},"database_identity_sha256":before["database_identity_sha256"],"actor_outcome":outcome})
        row=next(r for r in self.results["scenarios"] if r["id"]=="full-replay")
        row.update(execution="passed",compatibility="compatible",assertions=2,cases=cases,command_ids=[i for c in cases for i in c["command_ids"]])
        self.save();verifier.validate(self.directory)
        return row

    def test_removed_persisted_inventory_is_rejected_after_resealing(self):
        self.persisted_pass()
        self.directory.joinpath("inventories/before.json").unlink()
        verifier.seal(self.directory)
        self.reject()

    def test_unresolved_persisted_inventory_hash_is_rejected(self):
        row = self.persisted_pass()
        row["cases"][0]["after_sha256"] = "a" * 64
        self.save()
        self.reject()

    def test_omitted_persisted_binding_is_rejected(self):
        row = self.persisted_pass()
        del row["cases"][0]["before_sha256"]
        self.save()
        self.reject()

    def restore_pass(self,scenario="post-upgrade-restore"):
        self.provenance_pass();self.runtime_evidence();start=len(self.commands)
        lane=verifier.VERSIONS[0] if scenario=="post-upgrade-restore" else verifier.VERSIONS[1]
        self.seed_evidence(lane)
        if scenario=="post-upgrade-restore":
            self.add_command(["docker","exec","a"*64,"createdb","-U","postgres","p1r_source"])
            self.add_command(["docker","exec","a"*64,"psql","-U","postgres","-d","p1r_source","-At","-v","ON_ERROR_STOP=1","-c",verifier.RETAINED_FLOOR_SQL])
            rows=self.retained_stream_rows()
        else:
            rows=self.state_rows()
        before=self.inventory_evidence("p1r_source",rows,"restore-before")
        dump=self.add_command(["docker","exec","a"*64,"pg_dump","-U","postgres","-Fc","p1r_source"],output_sha256="b"*64,output_bytes=10,binary_output_retained=False)
        if scenario=="pre-upgrade-restore":
            self.nodes(verifier.VERSIONS[0]);self.actor_evidence(verifier.VERSIONS[0],"tenant-a",0,"IncrementCounter")
            self.add_command(["dotnet","/fixture/3.110.0/host/bin/Release/net10.0/Host.dll"])
        self.finish_nodes()
        self.add_command(["docker","exec","a"*64,"createdb","-U","postgres","p1r_restored"])
        self.add_command(["docker","exec","-i","a"*64,"pg_restore","-U","postgres","-d","p1r_restored","--exit-on-error"],input_sha256="b"*64,input_bytes=10)
        copied=self.inventory_evidence("p1r_restored",rows,"restore-copied")
        self.nodes(verifier.VERSIONS[1])
        for tenant,count in (("tenant-a",12),("tenant-b",3)):
            self.actor_evidence(verifier.VERSIONS[1],tenant,count)
            self.add_command(["HTTP","GET","http://127.0.0.1:12345/sequence/"+tenant+"/fixture"],count)
        after=self.inventory_evidence("p1r_restored",rows,"restore-after")
        case={"id":"quiesced-full-backup","assertions":1,"command_ids":[c["id"] for c in self.commands[start:]],"backup_sha256":"b"*64,"backup_bytes":10,"source_inventory_sha256":before["sha256"],"restored_inventory_sha256":copied["sha256"],"restored_replay_sha256":after["sha256"],"inventory_commands":{"source_inventory_sha256":before["query_command_id"],"restored_inventory_sha256":copied["query_command_id"],"restored_replay_sha256":after["query_command_id"]},"containment_only":scenario=="pre-upgrade-restore"}
        row=next(r for r in self.results["scenarios"] if r["id"]==scenario)
        row.update(execution="passed",compatibility="compatible",assertions=1,cases=[case],command_ids=case["command_ids"])
        self.save();verifier.validate(self.directory)
        return row,dump

    def test_dump_command_hash_must_match_restore_case(self):
        _,dump=self.restore_pass()
        dump["output_sha256"]="a"*64
        self.save();self.reject()

    def provenance_pass(self):
        start=len(self.commands);cases=[]
        self.add_command([sys.executable,str(verifier.HERE.parent / "verify_public_packages.py")])
        for lane in verifier.VERSIONS:
            case_start=len(self.commands)
            loaded=json.loads((verifier.HERE / "attempt-10/artifacts" / (lane+"-identity.json")).read_text())["loaded"]
            identity={"lane":lane,"packages":[{"id":name,"version":lane,"archive_sha256":verifier.PUBLISHED_ARCHIVE_HASHES[lane][name],"content_hash":verifier.PUBLISHED_CONTENT_HASHES[lane][name],"repository":{"commit":verifier.SOURCES["selected" if lane==verifier.VERSIONS[0] else "rollback_archive"]},"assemblies":{"lib/net10.0/"+name+".dll":verifier.PUBLISHED_DLL_HASHES[lane][name]}} for name in sorted(verifier.PACKAGE_IDS)],"loaded":loaded}
            for project in ("Host","Domain","Probe"):
                target="/fixture/"+lane+"/"+project.lower()+"/"+project+".csproj"
                self.add_command(["dotnet","restore",target,"--configfile","/fixture/"+lane+"/NuGet.Config","--no-http-cache","--disable-parallel"])
                self.add_command(["dotnet","build",target,"--no-restore","-c","Release","-p:UseSharedCompilation=false"])
                self.graph_evidence(lane,project,verifier.PUBLISHED_DLL_HASHES[lane])
            for name in verifier.PACKAGE_IDS:
                self.add_command(["dotnet","nuget","verify","--all","/fixture/"+lane+"/packages/"+name.lower()+"/"+lane+"/"+name.lower()+"."+lane+".nupkg"],output="Signature type: Repository\n")
            self.add_command(["dotnet","/fixture/"+lane+"/probe/bin/Release/net10.0/Probe.dll","identity"],loaded)
            verifier.write(self.directory / "artifacts" / (lane+"-identity.json"),identity)
            self.nodes(lane)
            cases.append({"id":lane,"assertions":1,"packages":5,"actor_methods":loaded["actorMethods"],"command_ids":[c["id"] for c in self.commands[case_start:]]})
        self.source_evidence()
        row=next(r for r in self.results["scenarios"] if r["id"]=="provenance")
        row.update(execution="passed",compatibility="compatible",assertions=2,cases=cases,command_ids=[c["id"] for c in self.commands[start:]])
        self.save();verifier.validate(self.directory)

    def test_resealed_loaded_assembly_mismatch_is_rejected(self):
        self.provenance_pass()
        path = self.directory / "artifacts/3.110.0-identity.json"
        identity = json.loads(path.read_text())
        identity["loaded"]["assemblies"][0]["sha256"] = "c" * 64
        verifier.write(path, identity)
        verifier.seal(self.directory)
        self.reject()

    def test_resealed_rollback_archive_mismatch_is_rejected(self):
        self.provenance_pass()
        path = self.directory / "artifacts/3.70.1-identity.json"
        identity = json.loads(path.read_text())
        identity["packages"][0]["archive_sha256"] = "c" * 64
        verifier.write(path, identity)
        verifier.seal(self.directory)
        self.reject()

    def test_resealed_project_injection_into_package_graph_is_rejected(self):
        self.provenance_pass()
        path = self.directory / "artifacts/3.110.0/Host-assets.json"
        verifier.write(path, {"libraries": {"Hexalith.EventStore.Server/3.110.0": {"type": "project"}}})
        verifier.seal(self.directory)
        self.reject()

    def mutate_identity(self, mutation, lane="3.110.0"):
        self.provenance_pass()
        path = self.directory / "artifacts" / (lane + "-identity.json")
        data = json.loads(path.read_text())
        mutation(data)
        verifier.write(path, data)
        verifier.seal(self.directory)
        self.reject()

    def test_empty_probe_assembly_coverage_is_rejected(self):
        self.mutate_identity(lambda d: d["loaded"].update(assemblies=[]))

    def test_omitted_required_package_identity_is_rejected(self):
        self.mutate_identity(lambda d: d.update(packages=d["packages"][:-1]))

    def test_duplicate_package_identity_is_rejected(self):
        self.mutate_identity(lambda d: d["packages"].append(copy.deepcopy(d["packages"][0])))

    def test_swapped_known_probe_assembly_hash_is_rejected(self):
        self.mutate_identity(lambda d: d["loaded"]["assemblies"][0].update(sha256=d["loaded"]["assemblies"][1]["sha256"]))

    def test_coordinated_package_dll_and_loaded_hash_substitution_is_rejected(self):
        def coordinate(data):
            name = "Hexalith.EventStore.Server"
            package = next(p for p in data["packages"] if p["id"] == name)
            package["assemblies"] = {"lib/net10.0/" + name + ".dll": "a" * 64}
            next(a for a in data["loaded"]["assemblies"] if a["name"] == name)["sha256"] = "a" * 64
        self.mutate_identity(coordinate)

    def test_runtime_host_assembly_mismatch_is_rejected(self):
        self.provenance_pass()
        path = self.directory / "artifacts/3.110.0-host-loaded.json"
        data = json.loads(path.read_text())
        data[0]["sha256"] = "a" * 64
        verifier.write(path, data)
        verifier.seal(self.directory)
        self.reject()

    def test_missing_required_live_host_identity_is_rejected(self):
        self.persisted_pass()
        self.directory.joinpath("artifacts/3.70.1-host-loaded.json").unlink()
        verifier.seal(self.directory)
        self.reject()

    def current_identity(self):
        self.source_evidence()
        hashes={name:verifier.sha(name.encode()) for name in verifier.PACKAGE_IDS}
        self.current_hashes=hashes
        built={}
        for project in ("Host","Domain","Probe"):
            built[project]={name:hashes[name] for name in verifier.graph_packages("current",project)}
            self.graph_evidence("current",project,hashes)
        loaded={"assemblies":[{"name":name,"version":"3.110.0.0","sha256":hashes[name]} for name in sorted(verifier.PROBE_ASSEMBLIES)],"actorMethods":["ProcessCommandAsync"]}
        verifier.write(self.directory / "artifacts/current-identity.json",{"lane":"current","packages":[],"built_assemblies":built,"loaded":loaded})
        self.nodes("current",hashes)
        self.save();verifier.validate(self.directory)

    def test_current_host_must_match_executed_source_output_inventory(self):
        self.current_identity()
        path = self.directory / "artifacts/current-host-loaded.json"
        data = json.loads(path.read_text())
        data[0]["sha256"] = "a" * 64
        verifier.write(path, data)
        verifier.seal(self.directory)
        self.reject()

    def test_current_output_inventory_cannot_be_replaced_with_host_hash(self):
        self.current_identity()
        path = self.directory / "artifacts/current-identity.json"
        data = json.loads(path.read_text())
        data["built_assemblies"]["Host"]["Hexalith.EventStore.Client"] = "a" * 64
        verifier.write(path, data)
        host = self.directory / "artifacts/current-host-loaded.json"
        observed = json.loads(host.read_text())
        next(a for a in observed if a["name"] == "Hexalith.EventStore.Client")["sha256"] = "a" * 64
        verifier.write(host, observed)
        verifier.seal(self.directory)
        self.reject()

    def test_live_identity_http_receipt_is_checked_against_signed_dlls(self):
        self.provenance_pass()
        data = json.loads(self.directory.joinpath("artifacts/3.110.0-host-loaded.json").read_text())
        data[0]["sha256"] = "a" * 64
        diagnostic = json.dumps(data)
        self.add_command(["HTTP", "GET", "http://127.0.0.1:12345/identity"],output=diagnostic)
        self.save()
        with self.assertRaisesRegex(ValueError, "Loaded identity differs from verified DLL"):
            verifier.validate(self.directory)

    def test_no_command_receipt_is_rejected(self):
        self.results["scenarios"][0]["command_ids"] = []
        self.save()
        self.reject()

    def test_missing_per_case_commands_is_rejected(self):
        row = self.persisted_pass()
        row["cases"][0]["command_ids"] = []
        self.save()
        self.reject()

    def test_same_query_cannot_satisfy_before_and_after(self):
        row=self.persisted_pass();case=row["cases"][0]
        case["inventory_commands"]["after_sha256"]=case["inventory_commands"]["before_sha256"]
        case["after_sha256"]=case["before_sha256"]
        self.save()
        with self.assertRaisesRegex(ValueError,"observations must bracket"):
            verifier.validate(self.directory)

    def test_coordinated_inventory_mutation_must_match_literal_query(self):
        row=self.persisted_pass()
        for path in self.directory.joinpath("inventories").glob("*.json"):
            value=json.loads(path.read_text());value["rows"][0]["sha256"]="f"*64
            value["domain_rows"]=copy.deepcopy(value["rows"])
            value["sha256"]=verifier.sha(verifier.canonical(value["rows"]))
            value["query_output"]=json.dumps(value["rows"])+"\n"
            self.commands[value["query_command_id"]-1]["output_sha256"]=verifier.sha(value["query_output"].encode())
            for case in row["cases"]:
                for field,identity in case["inventory_commands"].items():
                    if identity==value["query_command_id"]: case[field]=value["sha256"]
            verifier.write(path,value)
        self.save()
        with self.assertRaisesRegex(ValueError,"actual state-query receipt"):
            verifier.validate(self.directory)

    def test_inventory_queries_require_owned_postgresql(self):
        self.persisted_pass()
        for command in self.commands:
            if "psql" in command["argv"]: command["argv"][2]="unowned-container"
        self.save()
        with self.assertRaisesRegex(ValueError,"unowned container"):
            verifier.validate(self.directory)

    def test_identity_endpoint_requires_actual_application_port(self):
        self.persisted_pass()
        for command in self.commands:
            if command["argv"][:2]==["HTTP","GET"] and command["argv"][2].endswith("/identity"):
                command["argv"][2]="http://127.0.0.1:54321/identity"
        self.save()
        with self.assertRaisesRegex(ValueError,"actual running lane identity"):
            verifier.validate(self.directory)

    def test_actor_endpoint_requires_its_application_sidecar(self):
        self.persisted_pass()
        for command in self.commands:
            if command["argv"][:1]==["dotnet"] and command["argv"][2:3]==["actor"]:
                command["argv"][3]="http://127.0.0.1:54321"
        self.save()
        with self.assertRaisesRegex(ValueError,"actual owned application"):
            verifier.validate(self.directory)

    def test_published_output_inventory_matches_signed_dlls(self):
        self.provenance_pass()
        command=next(c for c in self.commands if c["argv"][1:3]==["-c",verifier.ASSEMBLY_INVENTORY_SCRIPT])
        value=json.loads(command["diagnostic"]);value["Hexalith.EventStore.Client"]="f"*64
        command["diagnostic"]=json.dumps(value)+"\n";command["output_sha256"]=verifier.sha(command["diagnostic"].encode())
        self.save()
        with self.assertRaisesRegex(ValueError,"Physical output assembly hashes"):
            verifier.validate(self.directory)

    def test_empty_resealed_lock_graph_is_rejected(self):
        self.provenance_pass()
        verifier.write(self.directory/"artifacts/3.110.0/Host-lock.json",{})
        verifier.seal(self.directory)
        with self.assertRaisesRegex(ValueError,"Missing or substituted lock graph"):
            verifier.validate(self.directory)

    def test_unknown_lock_graph_version_is_rejected(self):
        self.provenance_pass();path=self.directory/"artifacts/3.110.0/Host-lock.json"
        value=json.loads(path.read_text());value["version"]=3;verifier.write(path,value);verifier.seal(self.directory)
        with self.assertRaisesRegex(ValueError,"Missing or substituted lock graph"):
            verifier.validate(self.directory)

    def test_source_closure_rejects_dirty_or_omitted_committed_inputs(self):
        self.current_identity();path=self.directory/"source-input-closure.json"
        original=json.loads(path.read_text())
        for mutation in ("dirty","omitted"):
            with self.subTest(mutation=mutation):
                value=copy.deepcopy(original);closure=value["Hexalith.EventStore"]
                relative=next(iter(closure["files"]))
                if mutation=="dirty": closure["files"][relative]["sha256"]="f"*64
                else: del closure["files"][relative]
                output=json.dumps({k:v for k,v in closure.items() if k not in {"command_id","output"}},sort_keys=True)+"\n"
                closure["output"]=output
                receipt=self.commands[closure["command_id"]-1];receipt["output_sha256"]=verifier.sha(output.encode());receipt["diagnostic"]=output
                verifier.write(path,value);self.save()
                with self.assertRaisesRegex(ValueError,"Source input differs|Source closure omitted"):
                    verifier.validate(self.directory)

    def test_compatible_replay_rejects_changed_after_event_bytes(self):
        row=self.persisted_pass();case=row["cases"][0]
        path=self.directory/"inventories/after0.json";value=json.loads(path.read_text())
        value["rows"][0]["sha256"]="f"*64;value["domain_rows"]=copy.deepcopy(value["rows"])
        value["sha256"]=verifier.sha(verifier.canonical(value["rows"]));value["query_output"]=json.dumps(value["rows"])+"\n"
        receipt=self.commands[value["query_command_id"]-1];receipt["diagnostic"]=value["query_output"];receipt["output_sha256"]=verifier.sha(value["query_output"].encode())
        case["after_sha256"]=value["sha256"];verifier.write(path,value);self.save()
        with self.assertRaisesRegex(ValueError,"mutated committed domain state"):
            verifier.validate(self.directory)

    def wire_pass(self,kind):
        self.provenance_pass();name="query-wire" if kind=="query" else "projection-wire"
        cases=[];start=len(self.commands)
        for identity in sorted(verifier.EXPECTED_CASES[name]):
            first,middle,form,shape=identity.split("-");direction=[first,middle]
            value=verifier.wire_fixture(kind,shape);case_start=len(self.commands)
            fields={"UserId":"fixture-user","SequenceNumber":3} if kind=="projection" else {"UserId":"fixture-user","OriginalActorId":None,"AuthenticatedWorkloadId":None,"IsDelegated":False,"DelegationId":None,"Scopes":None,"Audience":None}
            fields.update(verifier.common_wire_fields(value))
            if kind=="projection": fields["GlobalPosition"]=987
            if shape=="dual": fields.update(OriginalActorId="fixture-human",AuthenticatedWorkloadId="fixture-workload",IsDelegated=True,DelegationId="fixture-delegation",Scopes=value["scopes"],Audience=value["audience"])
            observations=[];input_hash=verifier.written_fixture_hash(value)
            for index,(lane,mode) in enumerate(((first,"to-xml" if form=="xml" else "json"),(middle,form),(first,form))):
                observed=copy.deepcopy(fields)
                if kind=="projection": observed["GlobalPosition"]=987 if lane==verifier.VERSIONS[0] and index==0 else 0
                elif shape=="dual":
                    if lane!=verifier.VERSIONS[0] or index>0:
                        observed.update(OriginalActorId=None,AuthenticatedWorkloadId=None,IsDelegated=False,DelegationId=None,Scopes=None,Audience=None)
                output_hash=verifier.sha((identity+str(index)).encode())
                observation={"fields":observed,"inputSha256":input_hash,"outputSha256":output_hash,"assertions":1}
                self.add_command(["dotnet","/fixture/"+lane+"/probe/bin/Release/net10.0/Probe.dll","wire",kind,mode,"/fixture/in","/fixture/out"],observation)
                observations.append(observation);input_hash=output_hash
            preserved=shape=="legacy"
            cases.append({"id":identity,"direction":direction,"format":form,"shape":shape,"assertions":1,"command_ids":[c["id"] for c in self.commands[case_start:]],"fixture_input":value,"first_fields":observations[0]["fields"],"middle_fields":observations[1]["fields"],"final_fields":observations[2]["fields"],"wire_hashes":[o["outputSha256"] for o in observations],"preserved":preserved})
        row=next(r for r in self.results["scenarios"] if r["id"]==name)
        row.update(execution="passed",compatibility="incompatible",assertions=len(cases),cases=cases,command_ids=[c["id"] for c in self.commands[start:]])
        self.save();verifier.validate(self.directory)
        return row

    def test_wire_controls_reject_substituted_fields_hashes_and_labels(self):
        for kind in ("query","projection"):
            for mutation in ("fields","hashes","disposition","substitution"):
                with self.subTest(kind=kind,mutation=mutation):
                    # Start with an independently validating positive fixture for every mutation.
                    row=self.wire_pass(kind)
                    if mutation=="fields": row["cases"][0]["middle_fields"]["UserId"]="substituted"
                    elif mutation=="hashes": row["cases"][0]["wire_hashes"][0]="f"*64
                    elif mutation=="substitution":
                        command=next(c for c in self.commands if c["id"] in row["cases"][0]["command_ids"] and c["argv"][2:3]==["wire"])
                        observed=json.loads(command["diagnostic"]);observed["type"]=verifier.SERVER_QUERY_SUBSTITUTE
                        command["diagnostic"]=json.dumps(observed,sort_keys=True)+"\n";command["output_sha256"]=verifier.sha(command["diagnostic"].encode())
                    else: row["compatibility"]="compatible"
                    self.save();self.reject()
                    row.update(execution="unavailable",compatibility="unverified",assertions=0,cases=[{"id":"blocked","assertions":0,"command_ids":[1]}])
                    self.save()


    def test_cleanup_requires_successful_owned_removal_receipts(self):
        self.persisted_pass()
        for command in self.commands:
            if command["argv"][:3] == ["docker","rm","-f"]:
                command["exit_code"] = 1
        self.save()
        with self.assertRaisesRegex(ValueError, "successful owned-resource removal"):
            verifier.validate(self.directory)

    def test_database_creation_must_succeed_before_restore(self):
        self.restore_pass()
        for command in self.commands:
            if "createdb" in command["argv"]:
                command["exit_code"] = 1
        self.save()
        with self.assertRaisesRegex(ValueError, "successful fresh creation"):
            verifier.validate(self.directory)

    def test_failed_application_launch_cannot_prove_live_requests(self):
        self.persisted_pass()
        for command in self.commands:
            if command["argv"][:1] == ["dotnet"] and command["argv"][1].endswith(("/Host.dll","/Domain.dll")):
                command["exit_code"] = 127
        self.save()
        with self.assertRaisesRegex(ValueError, "actual running lane identity"):
            verifier.validate(self.directory)

    def test_application_lifetime_must_cover_requests(self):
        self.persisted_pass()
        for command in self.commands:
            if command["argv"][:1] == ["dotnet"] and command["argv"][1].endswith(("/Host.dll","/Domain.dll")):
                command["finished_utc"] = command["started_utc"]
        self.save()
        with self.assertRaisesRegex(ValueError, "actual running lane identity"):
            verifier.validate(self.directory)

    def test_writers_must_stop_before_backup(self):
        row,_ = self.restore_pass()
        writer = next(c for c in self.commands if c["id"] in row["command_ids"] and c.get("node_environment"))
        writer["finished_utc"] = "2099-01-01T00:00:00+00:00"
        self.save()
        with self.assertRaisesRegex(ValueError, "Writers were not quiesced"):
            verifier.validate(self.directory)

    def test_provenance_requires_successful_archive_preflight(self):
        self.provenance_pass()
        next(c for c in self.commands if c["argv"][-1].endswith("/verify_public_packages.py"))["exit_code"] = 1
        self.save()
        with self.assertRaisesRegex(ValueError, "successful public-package preflight"):
            verifier.validate(self.directory)

    def changed_restore_inventory(self, scenario, observation):
        row,_ = self.restore_pass(scenario)
        case = row["cases"][0]
        filename,field = ("restore-copied.json","restored_inventory_sha256") if observation == "restored" else ("restore-after.json","restored_replay_sha256")
        path = self.directory / "inventories" / filename
        value = json.loads(path.read_text())
        next(r for r in value["rows"] if ":events:" in r["key"])["sha256"] = "f"*64
        value["domain_rows"] = copy.deepcopy(value["rows"])
        value["sha256"] = verifier.sha(verifier.canonical(value["rows"]))
        value["query_output"] = json.dumps(value["rows"])+"\n"
        receipt = self.commands[value["query_command_id"]-1]
        receipt.update(diagnostic=value["query_output"],output_sha256=verifier.sha(value["query_output"].encode()))
        case[field] = value["sha256"]
        verifier.write(path,value)
        self.save()
        with self.assertRaisesRegex(ValueError, "Restoration inventory/invariants differ"):
            verifier.validate(self.directory)

    def test_post_upgrade_restore_rejects_changed_restored_event(self):
        self.changed_restore_inventory("post-upgrade-restore","restored")

    def test_post_upgrade_restore_rejects_changed_replayed_event(self):
        self.changed_restore_inventory("post-upgrade-restore","replayed")

    def test_pre_upgrade_restore_rejects_changed_restored_event(self):
        self.changed_restore_inventory("pre-upgrade-restore","restored")

    def test_pre_upgrade_restore_rejects_changed_replayed_event(self):
        self.changed_restore_inventory("pre-upgrade-restore","replayed")

    def test_initial_common_query_and_projection_fields_are_required(self):
        for kind in ("query","projection"):
            with self.subTest(kind=kind):
                row = self.wire_pass(kind)
                case = row["cases"][0]
                command = next(c for c in self.commands if c["id"] in case["command_ids"] and c["argv"][2:3] == ["wire"])
                observation = json.loads(command["diagnostic"])
                del observation["fields"]["Payload"]
                case["first_fields"] = observation["fields"]
                command["diagnostic"] = json.dumps(observation)+"\n"
                command["output_sha256"] = verifier.sha(command["diagnostic"].encode())
                self.save()
                with self.assertRaisesRegex(ValueError, "Initial typed writer lost common"):
                    verifier.validate(self.directory)
                row.update(execution="unavailable",compatibility="unverified",assertions=0,cases=[{"id":"blocked","assertions":0,"command_ids":[1]}])
                self.save()

    def test_common_wire_fields_cannot_change_during_round_trip(self):
        for kind in ("query","projection"):
            with self.subTest(kind=kind):
                row=self.wire_pass(kind);case=row["cases"][0]
                calls=[c for c in self.commands if c["id"] in case["command_ids"] and c["argv"][2:3]==["wire"]]
                observation=json.loads(calls[1]["diagnostic"])
                observation["fields"]["Payload"]="changed"
                case["middle_fields"]=observation["fields"]
                calls[1]["diagnostic"]=json.dumps(observation)+"\n"
                calls[1]["output_sha256"]=verifier.sha(calls[1]["diagnostic"].encode())
                self.save()
                with self.assertRaisesRegex(ValueError,"Common wire fields changed"):
                    verifier.validate(self.directory)
                row.update(execution="unavailable",compatibility="unverified",assertions=0,cases=[{"id":"blocked","assertions":0,"command_ids":[1]}])
                self.save()

    def private_mixed_case(self, kind):
        start=len(self.commands)
        if kind == "status":
            self.nodes(verifier.VERSIONS[0])
            value={"retryable":True,"recoveryReasonCode":"fixture-recovery","drainAttemptCount":2}
            self.add_command(["HTTP","POST","http://127.0.0.1:12345/status/tenant-a/fixture-status"],request_observation=value)
            self.add_command(["HTTP","GET","http://127.0.0.1:12345/status/tenant-a/fixture-status"],value)
            self.nodes(verifier.VERSIONS[1])
            self.add_command(["HTTP","GET","http://127.0.0.1:12345/status/tenant-a/fixture-status"],{})
            case={"id":"status-downgrade","selected_keys":sorted(value),"old_keys":[],"handling":"lost-recovery-tristate"}
            compatibility="incompatible"
        else:
            digest=verifier.sha(b"opaque-fixture")
            self.nodes(verifier.VERSIONS[0])
            self.add_command(["HTTP","GET","http://127.0.0.1:12345/cursor-mint"],confidential_observations={"cursor_sha256":digest})
            self.nodes(verifier.VERSIONS[1])
            value={"tenantId":"tenant-a","domain":"counter","aggregateId":"fixture","queryType":"fixture","payload":"e30=","correlationId":"fixture-correlation","userId":"fixture-user","paging":{"cursor":{"sha256":digest}}}
            decoded={"decoded":True,"position":"position-3"}
            for result in (decoded,{"decoded":False}):
                payload=verifier.base64.b64encode(json.dumps(result).encode()).decode()
                self.add_command(["HTTP","POST","http://127.0.0.1:12345/query"],{"payloadBytes":payload},request_observation=value)
            case={"id":"cursor-downgrade","mint":verifier.VERSIONS[0],"consume":verifier.VERSIONS[1],"cursor_sha256":digest,"scope":"tenant-a|watermark:987","decode_outcome":decoded,"tamper_rejected":True,"key_material_retained":False}
            compatibility="compatible"
        self.finish_nodes()
        case.update(assertions=1,command_ids=[c["id"] for c in self.commands[start:]])
        if kind == "status":
            case["prerequisite_command_ids"] = list(case["command_ids"])
            case["command_ids"] = [case["command_ids"][-1]]
        row={"id":"mixed-api","execution":"passed","compatibility":compatibility,"cases":[case],"command_ids":[c["id"] for c in self.commands[start:]]}
        verifier.validate_operations(self.directory,self.commands,[row],{},None,None)
        return row

    def test_status_and_cursor_requests_require_the_claimed_application(self):
        for kind,suffix in (("status","/status/tenant-a/fixture-status"),("cursor","/query")):
            with self.subTest(kind=kind):
                row=self.private_mixed_case(kind)
                for command in self.commands:
                    if command["id"] in row["command_ids"] and command["argv"][:1]==["HTTP"] and command["argv"][2].endswith(suffix):
                        command["argv"][2]="http://unrelated.invalid"+suffix
                with self.assertRaisesRegex(ValueError,"Request endpoint differs"):
                    verifier.validate_operations(self.directory,self.commands,[row],{},None,None)

    def test_status_case_requires_both_versions_setup_receipts(self):
        row=self.private_mixed_case("status")
        case=row["cases"][0]
        case["prerequisite_command_ids"]=[c["id"] for c in self.commands if c["id"] in case["prerequisite_command_ids"] and c["argv"][:1]==["HTTP"]]
        with self.assertRaisesRegex(ValueError,"Missing executed lane application"):
            verifier.validate_operations(self.directory,self.commands,[row],{},None,None)

    def test_cursor_request_scope_cannot_be_rebound(self):
        row=self.private_mixed_case("cursor")
        command=next(c for c in self.commands if c["argv"][:2]==["HTTP","POST"])
        command["request_observation"]["tenantId"]="unrelated-tenant"
        command["request_observation_sha256"]=verifier.sha(verifier.canonical(command["request_observation"]))
        with self.assertRaisesRegex(ValueError,"Cursor request fixture scope differs"):
            verifier.validate_operations(self.directory,self.commands,[row],{},None,None)

    def test_request_observation_requires_its_literal_hash(self):
        value={"value":"fixture"}
        command=self.add_command(["HTTP","POST","http://127.0.0.1:12345/fixture"],request_observation=value)
        self.save();verifier.validate(self.directory)
        command["request_observation"]={"value":"changed"}
        command["request_observation_sha256"]=verifier.sha(verifier.canonical(command["request_observation"]))
        self.save()
        with self.assertRaisesRegex(ValueError,"literal request hash"):
            verifier.validate(self.directory)


    def rewrite_inventory(self, filename, mutate):
        path=self.directory/"inventories"/filename
        value=json.loads(path.read_text())
        mutate(value["rows"])
        value["domain_rows"]=[r for r in value["rows"] if ":events:" in r["key"] or r["key"].endswith((":snapshot",":metadata"))]
        value["sha256"]=verifier.sha(verifier.canonical(value["rows"]))
        value["query_output"]=json.dumps(value["rows"])+"\n"
        receipt=self.commands[value["query_command_id"]-1]
        receipt.update(diagnostic=value["query_output"],output_sha256=verifier.sha(value["query_output"].encode()))
        verifier.write(path,value)
        return value

    def inventory_name(self, scenario, case, label):
        return scenario+"-"+case["writer"]+"-"+case["reader"]+"-"+case["variant"]+"-"+label+".json"

    def mark_missing_event(self, lane):
        suffix="/"+lane+"/host/bin/"+("Debug" if lane=="current" else "Release")+"/net10.0/Host.dll"
        host=next(c for c in reversed(self.commands) if str(c["argv"][1]).endswith(suffix))
        diagnostic="CorrelationId=fixture-correlation ExceptionType=MissingEventException\n"
        host.update(diagnostic=diagnostic,output_sha256=verifier.sha(diagnostic.encode()),runtime_failures=[{"correlation_id":"fixture-correlation","category":"missing-event"}])
        return host["id"]

    def open_stream(self, scenario, writer, reader, variant="default"):
        start=len(self.commands)
        database=self.next_database()
        self.seed_evidence(writer)
        retained=scenario in {"retained-covered","retained-uncovered","metadata-write"}
        snapshot=9 if scenario in {"snapshot-tail","retained-covered","metadata-write"} else 2 if scenario=="retained-uncovered" and variant=="non-covering" else None
        omitted=7 if variant=="interior" else 12 if variant=="tail" else None
        rows=self.stream_rows(start=5 if retained else 1, floor="5" if retained else "1", snapshot=snapshot)
        if omitted:
            rows=[r for r in rows if not r["key"].endswith(":events:"+str(omitted))]
        self.add_command(["docker","exec","a"*64,"createdb","-U","postgres",database])
        if retained:
            self.psql(database, verifier.RETAINED_FLOOR_SQL)
        if scenario=="retained-uncovered":
            self.psql(database, "delete from state where key like '%tenant-a:counter:fixture:snapshot';" if variant=="absent" else "update state set value=jsonb_set(jsonb_set(value,'{sequenceNumber}','2'),'{state,count}','2') where key like '%tenant-a:counter:fixture:snapshot';")
        if scenario=="missing-event":
            self.psql(database, "delete from state where key like '%tenant-a:counter:fixture:events:"+str(omitted)+"';")
        before=self.inventory_evidence(database, rows, scenario+"-"+writer+"-"+reader+"-"+variant+"-before")
        self.nodes(reader)
        rejected=scenario in {"retained-uncovered","missing-event"}
        _,outcome=self.actor_evidence(reader,"tenant-a",accepted=not rejected)
        self.actor_evidence(reader,"tenant-b",3)
        self.add_command(["HTTP","GET","http://127.0.0.1:12345/sequence/tenant-a/fixture"],12)
        failure_ids=[self.mark_missing_event(reader)] if rejected else []
        if scenario=="metadata-write":
            self.actor_evidence(reader,"tenant-a",0,"IncrementCounter")
            rows=self.stream_rows(start=5, floor="5", snapshot=9, sequence=13, extra=13)
        after=self.inventory_evidence(database, rows, scenario+"-"+writer+"-"+reader+"-"+variant+"-after")
        return {"id":writer+"-to-"+reader+("-"+variant if variant!="default" else ""),"writer":writer,"reader":reader,"variant":variant,"assertions":1,"command_ids":[c["id"] for c in self.commands[start:]],"before_sha256":before["sha256"],"after_sha256":after["sha256"],"inventory_commands":{"before_sha256":before["query_command_id"],"after_sha256":after["query_command_id"]},"actor_outcome":outcome,"failure_category":"missing-event" if rejected else None,"failure_command_ids":failure_ids}

    def stream_pass(self, scenario, directions=None):
        self.provenance_pass();self.runtime_evidence()
        directions=directions or ((verifier.VERSIONS,) if scenario=="metadata-write" else (verifier.VERSIONS, tuple(reversed(verifier.VERSIONS))))
        variants=("absent","non-covering") if scenario=="retained-uncovered" else ("interior","tail") if scenario=="missing-event" else ("default",)
        cases=[self.open_stream(scenario, writer, reader, variant) for writer, reader in directions for variant in variants]
        row=next(r for r in self.results["scenarios"] if r["id"]==scenario)
        row.update(execution="passed",compatibility="compatible",assertions=len(cases),cases=cases,command_ids=[i for case in cases for i in case["command_ids"]])
        self.save();verifier.validate(self.directory)
        return row

    def test_replay_disposition_rejects_an_incomplete_stream(self):
        row=self.stream_pass("full-replay");case=row["cases"][0]
        for label in ("before","after"):
            value=self.rewrite_inventory(self.inventory_name("full-replay", case, label), lambda rows: rows.remove(next(r for r in rows if r["key"].endswith(":events:1"))))
            case[label+"_sha256"]=value["sha256"]
        self.save()
        with self.assertRaisesRegex(ValueError, "incomplete"):
            verifier.validate(self.directory)

    def test_snapshot_disposition_rejects_a_noncovering_snapshot(self):
        row=self.stream_pass("snapshot-tail");case=row["cases"][0]
        def retarget(rows):
            next(r for r in rows if r["key"].endswith(":snapshot"))["snapshotSequence"]="8"
        for label in ("before","after"):
            value=self.rewrite_inventory(self.inventory_name("snapshot-tail", case, label), retarget)
            case[label+"_sha256"]=value["sha256"]
        self.save()
        with self.assertRaisesRegex(ValueError, "covering snapshot"):
            verifier.validate(self.directory)

    def test_retained_floor_disposition_rejects_a_different_floor(self):
        row=self.stream_pass("retained-covered");case=row["cases"][0]
        def retarget(rows):
            next(r for r in rows if r["key"].endswith("tenant-a:counter:fixture:metadata"))["floor"]="1"
        for label in ("before","after"):
            value=self.rewrite_inventory(self.inventory_name("retained-covered", case, label), retarget)
            case[label+"_sha256"]=value["sha256"]
        self.save()
        with self.assertRaisesRegex(ValueError, "metadata differs"):
            verifier.validate(self.directory)

    def test_metadata_disposition_requires_the_appended_floor(self):
        row=self.stream_pass("metadata-write");case=row["cases"][0]
        value=self.rewrite_inventory(self.inventory_name("metadata-write", case, "after"), lambda rows: next(r for r in rows if r["key"].endswith("tenant-a:counter:fixture:metadata")).update(floor="1"))
        case["after_sha256"]=value["sha256"]
        self.save()
        with self.assertRaisesRegex(ValueError, "contradicts"):
            verifier.validate(self.directory)

    def invalid_cases(self, lanes):
        cases=[]
        for lane in lanes:
            for mutation, statement in verifier.INVALID_EVIDENCE_SQL.items():
                start=len(self.commands);database=self.next_database();self.seed_evidence(lane)
                self.add_command(["docker","exec","a"*64,"createdb","-U","postgres",database]);self.psql(database, statement)
                rows=self.stream_rows()
                before=self.inventory_evidence(database, rows, "invalid-"+lane+"-"+mutation+"-before")
                self.nodes(lane)
                _,outcome=self.actor_evidence(lane,"tenant-a",accepted=False)
                after=self.inventory_evidence(database, rows, "invalid-"+lane+"-"+mutation+"-after")
                cases.append({"id":lane+"-"+mutation,"lane":lane,"mutation":mutation,"handling":"rejected","actor_outcome":outcome,"assertions":1,"command_ids":[c["id"] for c in self.commands[start:]],"before_sha256":before["sha256"],"after_sha256":after["sha256"],"inventory_commands":{"before_sha256":before["query_command_id"],"after_sha256":after["query_command_id"]}})
        return cases

    def test_invalid_evidence_acceptance_cannot_remain_compatible(self):
        self.provenance_pass();self.runtime_evidence();start=len(self.commands)
        cases=self.invalid_cases(verifier.VERSIONS)
        row=next(r for r in self.results["scenarios"] if r["id"]=="invalid-evidence")
        row.update(execution="passed",compatibility="compatible",assertions=len(cases),cases=cases,command_ids=[i for case in cases for i in case["command_ids"]])
        self.save();verifier.validate(self.directory)
        case=cases[0]
        command=next(c for c in self.commands if c["id"] in case["command_ids"] and len(c["argv"])>2 and c["argv"][2]=="actor" and c["argv"][4]=="tenant-a")
        outcome=json.loads(command["diagnostic"]);outcome["accepted"]=True
        command["diagnostic"]=json.dumps(outcome,sort_keys=True)+"\n";command["output_sha256"]=verifier.sha(command["diagnostic"].encode())
        case.update(actor_outcome=outcome,handling="accepted-unsafe")
        self.save()
        with self.assertRaisesRegex(ValueError, "contradicts"):
            verifier.validate(self.directory)

    def wire_group(self, kind, source=False):
        name="query-wire" if kind=="query" else "projection-wire"
        cases=[];start=len(self.commands)
        for identity in sorted(verifier.expected_cases(name, source)):
            first,middle,form,shape=identity.split("-");direction=[first,middle]
            value=verifier.wire_fixture(kind,shape);case_start=len(self.commands)
            fields={"UserId":"fixture-user","SequenceNumber":3} if kind=="projection" else {"UserId":"fixture-user","OriginalActorId":None,"AuthenticatedWorkloadId":None,"IsDelegated":False,"DelegationId":None,"Scopes":None,"Audience":None}
            fields.update(verifier.common_wire_fields(value))
            if kind=="projection": fields["GlobalPosition"]=987
            if shape=="dual": fields.update(OriginalActorId="fixture-human",AuthenticatedWorkloadId="fixture-workload",IsDelegated=True,DelegationId="fixture-delegation",Scopes=value["scopes"],Audience=value["audience"])
            observations=[];input_hash=verifier.written_fixture_hash(value)
            for index,(lane,mode) in enumerate(((first,"to-xml" if form=="xml" else "json"),(middle,form),(first,form))):
                observed=copy.deepcopy(fields)
                writer=index==0 and lane in {verifier.VERSIONS[0],"current"}
                if kind=="projection": observed["GlobalPosition"]=987 if writer else 0
                elif shape=="dual" and not writer:
                    observed.update(OriginalActorId=None,AuthenticatedWorkloadId=None,IsDelegated=False,DelegationId=None,Scopes=None,Audience=None)
                output_hash=verifier.sha((identity+str(index)).encode())
                observation={"fields":observed,"inputSha256":input_hash,"outputSha256":output_hash,"assertions":1}
                self.add_command(["dotnet",self.probe_binary(lane),"wire",kind,mode,"/fixture/in","/fixture/out"],observation)
                observations.append(observation);input_hash=output_hash
            preserved=shape=="legacy"
            cases.append({"id":identity,"direction":direction,"format":form,"shape":shape,"assertions":1,"command_ids":[c["id"] for c in self.commands[case_start:]],"fixture_input":value,"first_fields":observations[0]["fields"],"middle_fields":observations[1]["fields"],"final_fields":observations[2]["fields"],"wire_hashes":[o["outputSha256"] for o in observations],"preserved":preserved})
        compatibility="compatible" if all(case["preserved"] for case in cases) else "incompatible"
        return {"id":name,"assertions":len(cases),"command_ids":[c["id"] for c in self.commands[start:]],"cases":cases,"compatibility":compatibility}

    def legacy_group(self, lane):
        cases=[];start=len(self.commands)
        for convention in ("pascal","web"):
            value={"CurrentSequence":12,"LastModified":"2026-01-01T00:00:00Z","ETag":"fixture-etag"}
            if convention=="web": value={k[0].lower()+k[1:]:v for k,v in value.items()}
            observed={"assertions":3,"currentSequence":12,"floor":1,"inputSha256":verifier.written_fixture_hash(value),"outputSha256":verifier.sha((convention+lane).encode())}
            case_start=len(self.commands)
            self.add_command(["dotnet",self.probe_binary(lane),"metadata","/fixture/in","/fixture/out",convention],observed)
            cases.append({"id":convention+"-floor-None","assertions":1,"command_ids":[c["id"] for c in self.commands[case_start:]],"fixture_input":value,"currentSequence":observed["currentSequence"],"floor":observed["floor"],"inputSha256":observed["inputSha256"],"outputSha256":observed["outputSha256"]})
        return {"id":"legacy-metadata","assertions":len(cases),"command_ids":[c["id"] for c in self.commands[start:]],"cases":cases,"compatibility":"compatible"}

    def current_build_case(self):
        closures=json.loads((self.directory/"source-input-closure.json").read_text())
        identity=json.loads((self.directory/"artifacts/current-identity.json").read_text())
        while self.commands and self.commands[-1].get("fixture_cleanup"):
            self.commands.pop()
        start=len(self.commands)
        for project in ("Host","Domain","Probe"):
            target="/fixture/current/"+project.lower()+"/"+project+".csproj"
            source=["-p:UseCurrentSource=true","-p:EventStoreSourceRoot="+closures["Hexalith.EventStore"]["repository"],"-p:HexalithCommonsRoot="+closures["Hexalith.Commons"]["repository"]]
            self.add_command(["dotnet","restore",target,"--configfile","/fixture/current/NuGet.Config","--no-http-cache","--disable-parallel",*source])
            self.add_command(["dotnet","build",target,"--no-restore","-c","Debug","-p:UseSharedCompilation=false",*source])
            self.graph_evidence("current", project, self.current_hashes)
        self.add_command(["dotnet",self.probe_binary("current"),"identity"],identity["loaded"])
        return {"id":"current-build","assertions":1,"packages":0,"actor_methods":identity["loaded"]["actorMethods"],"command_ids":[c["id"] for c in self.commands[start:]]}

    def stream_group(self, scenario, directions):
        start=len(self.commands)
        variants=("absent","non-covering") if scenario=="retained-uncovered" else ("interior","tail") if scenario=="missing-event" else ("default",)
        cases=[self.open_stream(scenario, writer, reader, variant) for writer, reader in directions for variant in variants]
        return {"id":scenario,"assertions":len(cases),"command_ids":[c["id"] for c in self.commands[start:]],"cases":cases,"compatibility":"compatible"}

    def checkout_pass(self):
        self.runtime_evidence();self.current_identity()
        directions=((verifier.VERSIONS[0],"current"),("current",verifier.VERSIONS[0]))
        groups=[self.current_build_case(),self.legacy_group("current"),self.wire_group("query",True),self.wire_group("projection",True)]
        for name in ("full-replay","snapshot-tail","retained-covered","retained-uncovered","missing-event","metadata-write"):
            groups.append(self.stream_group(name, directions))
        start=len(self.commands)
        invalid=self.invalid_cases(("current",))
        groups.append({"id":"invalid-evidence","assertions":len(invalid),"command_ids":[c["id"] for c in self.commands[start:]],"cases":invalid,"compatibility":"compatible"})
        row=next(r for r in self.results["scenarios"] if r["id"]=="checkout")
        row.update(execution="passed",compatibility="incompatible" if any(group.get("compatibility")=="incompatible" for group in groups) else "compatible",assertions=sum(group["assertions"] for group in groups),cases=groups,command_ids=[i for group in groups for i in group["command_ids"]])
        self.save();verifier.validate(self.directory)
        return row

    def test_checkout_label_mismatch_is_rejected(self):
        row=self.checkout_pass()
        next(group for group in row["cases"] if group["id"]=="legacy-metadata")["compatibility"]="incompatible"
        self.save()
        with self.assertRaisesRegex(ValueError, "contradicts nested"):
            verifier.validate(self.directory)

    def mixed_pass(self):
        self.provenance_pass();self.runtime_evidence();cases=[];start=len(self.commands)
        for client, host in (verifier.VERSIONS, tuple(reversed(verifier.VERSIONS))):
            case_start=len(self.commands);database=self.next_database();self.seed_evidence(host)
            self.add_command(["docker","exec","a"*64,"createdb","-U","postgres",database]);rows=self.stream_rows()
            before=self.inventory_evidence(database, rows, "mixed-"+client+"-"+host+"-before")
            self.nodes(host);self.actor_evidence(client,"tenant-a");self.actor_evidence(client,"tenant-b",3)
            after=self.inventory_evidence(database, rows, "mixed-"+client+"-"+host+"-after")
            cases.append({"id":client+"-client-"+host+"-host","client":client,"host":host,"assertions":1,"command_ids":[c["id"] for c in self.commands[case_start:]],"before_sha256":before["sha256"],"after_sha256":after["sha256"],"inventory_commands":{"before_sha256":before["query_command_id"],"after_sha256":after["query_command_id"]}})
        case_start=len(self.commands);self.nodes(verifier.VERSIONS[0])
        value={"retryable":True,"recoveryReasonCode":"fixture-recovery","drainAttemptCount":2}
        self.add_command(["HTTP","POST","http://127.0.0.1:12345/status/tenant-a/fixture-status"],request_observation=value)
        self.add_command(["HTTP","GET","http://127.0.0.1:12345/status/tenant-a/fixture-status"],value)
        self.nodes(verifier.VERSIONS[1])
        self.add_command(["HTTP","GET","http://127.0.0.1:12345/status/tenant-a/fixture-status"],{})
        cases.append({"id":"status-downgrade","assertions":1,"command_ids":[c["id"] for c in self.commands[case_start:]],"selected_keys":sorted(value),"old_keys":[],"handling":"lost-recovery-tristate"})
        for mint, consume, identity in ((verifier.VERSIONS[0], verifier.VERSIONS[1], "cursor-downgrade"), (verifier.VERSIONS[1], verifier.VERSIONS[0], "cursor-upgrade")):
            case_start=len(self.commands);digest=verifier.sha(("opaque-"+identity).encode());self.nodes(mint)
            self.add_command(["HTTP","GET","http://127.0.0.1:12345/cursor-mint"],confidential_observations={"cursor_sha256":digest})
            self.nodes(consume)
            observed={"tenantId":"tenant-a","domain":"counter","aggregateId":"fixture","queryType":"fixture","payload":"e30=","correlationId":"fixture-correlation","userId":"fixture-user","paging":{"cursor":{"sha256":digest}}}
            decoded={"decoded":True,"position":"position-3"}
            for result in (decoded, {"decoded":False}):
                payload=verifier.base64.b64encode(json.dumps(result).encode()).decode()
                self.add_command(["HTTP","POST","http://127.0.0.1:12345/query"],{"payloadBytes":payload},request_observation=observed)
            cases.append({"id":identity,"assertions":1,"command_ids":[c["id"] for c in self.commands[case_start:]],"mint":mint,"consume":consume,"cursor_sha256":digest,"scope":"tenant-a|watermark:987","decode_outcome":decoded,"tamper_rejected":True,"key_material_retained":False})
        for label, lane, outcome in (("old", verifier.VERSIONS[1], {"method":"AddProjectionWatermark","handling":"unsupported-client-method"}), ("selected", verifier.VERSIONS[0], {"method":"AddProjectionWatermark","handling":"executed","scope":"tenant:tenant-a|watermark:987","invalid_watermark_rejected":True})):
            case_start=len(self.commands)
            self.add_command(["dotnet",self.probe_binary(lane),"cursor-scope"],outcome)
            cases.append({"id":label+"-cursor-scope","assertions":1,"command_ids":[c["id"] for c in self.commands[case_start:]],**outcome})
        for host, label in ((verifier.VERSIONS[1], "old"), (verifier.VERSIONS[0], "selected")):
            node_start=len(self.commands);self.nodes(host);node_ids=[c["id"] for c in self.commands[node_start:]]
            for method in ("ProcessFencedCommandAsync","ProcessTrustedEffectAsync","GetRetainedFloorAsync"):
                case_start=len(self.commands)
                detail="" if host==verifier.VERSIONS[0] and method=="GetRetainedFloorAsync" else method+"ReqBody deserializer has no knowledge" if host==verifier.VERSIONS[1] else "ArgumentNullException"
                outcome="returned" if host==verifier.VERSIONS[0] and method=="GetRetainedFloorAsync" else "rejected"
                self.add_command(["dotnet",self.probe_binary(verifier.VERSIONS[0]),"capability","http://127.0.0.1:12345",method],{"outcome":outcome,"method":method,"detail":detail})
                cases.append({"id":label+"-dispatcher-"+method,"method":method,"outcome":outcome,"detail_sha256":verifier.sha(detail.encode()),"handling":"unsupported-old-actor-contract" if host==verifier.VERSIONS[1] else "selected-dispatcher-executed","assertions":1,"command_ids":node_ids+[c["id"] for c in self.commands[case_start:]]})
        row=next(r for r in self.results["scenarios"] if r["id"]=="mixed-api")
        row.update(execution="passed",compatibility="incompatible",assertions=len(cases),cases=cases,command_ids=[c["id"] for c in self.commands[start:]])
        self.save();verifier.validate(self.directory)
        return row

    def test_old_dispatcher_success_is_rejected(self):
        row=self.mixed_pass()
        case=next(item for item in row["cases"] if item["id"]=="old-dispatcher-ProcessFencedCommandAsync")
        command=next(c for c in self.commands if c["id"] in case["command_ids"] and len(c["argv"])>2 and c["argv"][2]=="capability")
        observed=json.loads(command["diagnostic"]);observed["outcome"]="returned"
        command["diagnostic"]=json.dumps(observed,sort_keys=True)+"\n";command["output_sha256"]=verifier.sha(command["diagnostic"].encode())
        case["outcome"]="returned"
        self.save()
        with self.assertRaisesRegex(ValueError, "unsupported old dispatcher"):
            verifier.validate(self.directory)

    def failure_cleanup_pass(self):
        start=len(self.commands);cases=[]
        for name, script, code in (("startup-failure","raise SystemExit(7)",7),("timeout","import time; time.sleep(10)",124),("cancellation","import time; time.sleep(10)",130)):
            case_start=len(self.commands)
            self.add_command([sys.executable,"-c",script],code=code)
            cases.append({"id":name,"assertions":1,"command_ids":[c["id"] for c in self.commands[case_start:]]})
        row=next(r for r in self.results["scenarios"] if r["id"]=="failure-cleanup")
        row.update(execution="passed",compatibility="compatible",assertions=len(cases),cases=cases,command_ids=[c["id"] for c in self.commands[start:]])
        self.save();verifier.validate(self.directory)
        return row

    def test_failure_cleanup_exit_outside_expected_codes_is_rejected(self):
        self.failure_cleanup_pass()
        command=next(c for c in self.commands if c["argv"][1:]==["-c","import time; time.sleep(10)"] and c["exit_code"]==124)
        command["exit_code"]=9
        self.save()
        with self.assertRaisesRegex(ValueError, "lifecycle failure"):
            verifier.validate(self.directory)

    def test_post_upgrade_without_retained_floor_stream_is_rejected(self):
        self.restore_pass()
        command=next(c for c in self.commands if "psql" in c["argv"] and "retainedFloor" in c["argv"][-1])
        command["argv"][-1]="select 1"
        self.save()
        with self.assertRaisesRegex(ValueError, "retained-floor stream"):
            verifier.validate(self.directory)

    def test_post_upgrade_dropped_floor_cannot_be_labeled_compatible(self):
        row,_=self.restore_pass();case=row["cases"][0]
        value=self.rewrite_inventory("restore-after.json", lambda rows: next(r for r in rows if r["key"].endswith("tenant-a:counter:fixture:metadata")).update(floor=None))
        case["restored_replay_sha256"]=value["sha256"]
        self.save()
        with self.assertRaisesRegex(ValueError, "contradicts"):
            verifier.validate(self.directory)
        row["compatibility"]="incompatible"
        self.save()
        result=verifier.validate(self.directory)
        self.assertFalse(result["qualified"])
        self.assertEqual(row["compatibility"],"incompatible")

    def test_unsupported_contract_type_cannot_pass_as_a_round_trip(self):
        row=self.wire_pass("query");case=row["cases"][0]
        for command in self.commands:
            if command["id"] in case["command_ids"] and len(command["argv"])>2 and command["argv"][2]=="wire":
                observed={"assertions":1,"handling":"unsupported-contract-type","type":verifier.CONTRACT_WIRE_TYPES["query"]}
                command["diagnostic"]=json.dumps(observed,sort_keys=True)+"\n"
                command["output_sha256"]=verifier.sha(command["diagnostic"].encode())
        case["preserved"]=False
        self.save();verifier.validate(self.directory)
        case["preserved"]=True
        self.save()
        with self.assertRaisesRegex(ValueError, "round trip"):
            verifier.validate(self.directory)

    def test_empty_operation_receipt_is_invalid_evidence(self):
        command={"exit_code":0,"diagnostic":"\n","output_sha256":verifier.sha(b"\n")}
        with self.assertRaisesRegex(ValueError, "no evidence"):
            verifier.receipt_json(command)

    def test_short_gitlink_receipt_is_invalid_evidence(self):
        self.provenance_pass()
        command=next(c for c in self.commands if c["argv"][:2]==["git","ls-tree"])
        command["diagnostic"]="short\n"
        command["output_sha256"]=verifier.sha(command["diagnostic"].encode())
        self.save()
        with self.assertRaisesRegex(ValueError, "gitlink"):
            verifier.validate(self.directory)

    def test_validation_index_and_stop_errors_are_invalid_evidence(self):
        for error in (IndexError("split"), StopIteration()):
            with self.subTest(error=type(error).__name__):
                with mock.patch.object(verifier, "_validate_packet", side_effect=error):
                    with self.assertRaisesRegex(ValueError, "Invalid evidence: "+type(error).__name__):
                        verifier.validate(self.directory)

    def test_lifetime_clock_uses_subsecond_timestamps(self):
        self.assertRegex(verifier.utc(), r"\.\d{6}\+00:00$")

    def test_writer_quiescence_compares_subsecond_timestamps(self):
        row,dump=self.restore_pass()
        writer=next(c for c in self.commands if c["id"] in row["command_ids"] and c.get("node_environment"))
        writer["finished_utc"]=dump["started_utc"].replace("+00:00",".100000+00:00")
        self.save()
        with self.assertRaisesRegex(ValueError, "Writers were not quiesced"):
            verifier.validate(self.directory)


class LifecycleTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        root = pathlib.Path(self.temp.name)
        self.output = root / "output"
        self.output.mkdir()
        scratch = root / "scratch"
        scratch.mkdir()
        with mock.patch.object(verifier.Runner, "shared", return_value={}):
            self.runner = verifier.Runner(self.output, scratch)
            self.runner.initialize()

    def tearDown(self):
        for process in self.runner.processes:
            self.runner.stop(process)
        self.runner.flush_logs()
        self.temp.cleanup()

    def test_isolated_lanes_clear_startup_and_profiler_injection(self):
        names = {"DOTNET_STARTUP_HOOKS", "DOTNET_ADDITIONAL_DEPS", "ASPNETCORE_HOSTINGSTARTUPASSEMBLIES"}
        names.update(prefix+"_"+suffix for prefix in ("DOTNET", "CORECLR", "COR") for suffix in ("ENABLE_PROFILING", "PROFILER", "PROFILER_PATH", "PROFILER_PATH_32", "PROFILER_PATH_64"))
        with mock.patch.dict(os.environ, {name: "private-sentinel" for name in names}):
            self.runner = verifier.Runner(self.output, self.runner.scratch)
        self.assertTrue(all(name not in self.runner.env for name in names))
        command = [sys.executable, "-c", "import json,os;print(json.dumps([name for name in " + repr(sorted(names)) + " if name in os.environ]))"]
        for lane in (*verifier.VERSIONS, "current"):
            self.assertEqual(json.loads(self.runner.run(command, env=self.runner.lane_env(lane))), [])

    def test_real_startup_exit_keeps_healthy_output_and_final_lifetime(self):
        ready = self.runner.scratch / "healthy-ready"
        finish = self.runner.scratch / "healthy-finish"
        script = "import pathlib,time\nprint('healthy-early',flush=True)\npathlib.Path("+repr(str(ready))+").write_text('ready')\nwhile not pathlib.Path("+repr(str(finish))+").exists(): time.sleep(.01)\nprint('healthy-late',flush=True)\n"
        healthy = self.runner.start([sys.executable, "-c", script])
        deadline = time.monotonic()+5
        while not ready.exists() and time.monotonic() < deadline: time.sleep(.01)
        self.assertTrue(ready.exists())
        failed = self.runner.start([sys.executable, "-c", "print('failed-startup',flush=True);raise SystemExit(7)"])
        failed.wait(timeout=5)
        with self.assertRaisesRegex(RuntimeError, "exited during startup"):
            self.runner.wait_http("http://127.0.0.1:12345/ready", failed)
        self.assertTrue(any(process is healthy for process, *_ in self.runner.logs))
        barrier = verifier.utc()
        finish.write_text("finish")
        healthy.wait(timeout=5)
        self.runner.flush_logs()
        receipt = next(c for c in self.runner.commands if c.get("process_id") == healthy.pid)
        self.assertIn("healthy-early", receipt["diagnostic"])
        self.assertIn("healthy-late", receipt["diagnostic"])
        self.assertEqual(receipt["exit_code"], 0)
        self.assertEqual(receipt["process_returncode"], 0)
        self.assertGreater(receipt["finished_utc"], barrier)

    def test_interrupted_binary_commands_retain_only_hash_and_length(self):
        marker="PGDMP-fixture-raw-database-contents"
        command=[sys.executable,"-c","import sys,time;sys.stdout.write("+repr(marker)+");sys.stdout.flush();time.sleep(30)"]
        for cancellation in (False,True):
            with self.subTest(cancellation=cancellation):
                old=signal.getsignal(signal.SIGALRM)
                try:
                    if cancellation:
                        def cancel(signum,frame): raise KeyboardInterrupt()
                        signal.signal(signal.SIGALRM,cancel);signal.setitimer(signal.ITIMER_REAL,.3)
                    with self.assertRaises(KeyboardInterrupt if cancellation else TimeoutError):
                        self.runner.run(command,timeout=.3 if not cancellation else 5,binary=True)
                finally:
                    signal.setitimer(signal.ITIMER_REAL,0);signal.signal(signal.SIGALRM,old)
                receipt=self.runner.commands[-1]
                self.assertNotIn(marker,receipt["diagnostic"])
                self.assertEqual(receipt["output_bytes"],len(marker))
                self.assertFalse(receipt["binary_output_retained"])

    def test_launch_cancellation_registers_and_stops_each_child(self):
        original=verifier.subprocess.Popen
        spawned=[]
        def create(*args,**kwargs):
            process=original(*args,**kwargs);spawned.append(process)
            signal.raise_signal(signal.SIGINT)
            return process
        command=[sys.executable,"-c","import time;time.sleep(30)"]
        try:
            for method in (self.runner.run,self.runner.start):
                with self.subTest(method=method.__name__),mock.patch.object(verifier.subprocess,"Popen",side_effect=create):
                    with self.assertRaises(KeyboardInterrupt): method(command)
                    self.assertIn(spawned[-1],self.runner.processes)
                    self.assertIsNotNone(spawned[-1].poll())
                    self.assertEqual(self.runner.commands[-1]["exit_code"],130)
        finally:
            for process in spawned:
                self.runner.stop(process)

    def test_termination_failure_keeps_timeout_and_cancellation_receipts(self):
        command=[sys.executable,"-c","import time;time.sleep(30)"]
        for cancellation in (False,True):
            with self.subTest(cancellation=cancellation):
                communicate=mock.patch.object(verifier.subprocess.Popen,"communicate",side_effect=KeyboardInterrupt()) if cancellation else contextlib.nullcontext()
                with communicate,mock.patch.object(self.runner,"stop",side_effect=RuntimeError("termination failed")):
                    with self.assertRaises(KeyboardInterrupt if cancellation else TimeoutError):
                        self.runner.run(command,timeout=.05)
                receipt=self.runner.commands[-1]
                self.assertEqual(receipt["exit_code"],130 if cancellation else 124)
                self.assertEqual(receipt["termination_failure"],"RuntimeError")
                self.runner.stop(self.runner.processes[-1])

    def test_cancellation_during_timeout_cleanup_remains_cancellation(self):
        command=[sys.executable,"-c","import time;time.sleep(30)"]
        with mock.patch.object(self.runner,"stop",side_effect=KeyboardInterrupt()):
            with self.assertRaises(KeyboardInterrupt): self.runner.run(command,timeout=.05)
        self.assertEqual(self.runner.commands[-1]["exit_code"],130)
        self.assertEqual(self.runner.commands[-1]["termination_failure"],"KeyboardInterrupt")
        self.runner.stop(self.runner.processes[-1])

    def test_child_temporary_directories_remain_inside_owned_scratch(self):
        command=[sys.executable,"-c","import tempfile,time;print(tempfile.mkdtemp(),flush=True);time.sleep(30)"]
        with self.assertRaises(TimeoutError): self.runner.run(command,timeout=.3)
        path=pathlib.Path(self.runner.commands[-1]["diagnostic"].strip())
        self.assertTrue(path.is_relative_to(self.runner.scratch))
        self.assertTrue(path.exists())
        with mock.patch.object(self.runner,"shared",return_value={}):
            self.assertTrue(self.runner.cleanup()["scratch_removed"])
        self.assertFalse(path.exists())

    def test_six_ports_are_reserved_concurrently_and_released(self):
        occupied=set()
        def make_socket():
            connection=mock.MagicMock();chosen=[]
            connection.__enter__.return_value=connection
            def bind(address):
                port=next(p for p in range(10000,10010) if p not in occupied)
                chosen.append(port);occupied.add(port)
            connection.bind.side_effect=bind
            connection.getsockname.side_effect=lambda:("127.0.0.1",chosen[0])
            connection.__exit__.side_effect=lambda *args:occupied.remove(chosen[0])
            return connection
        with mock.patch.object(verifier.socket,"socket",side_effect=make_socket):
            self.assertEqual(len(set(self.runner.ports(6))),6)
        self.assertFalse(occupied)

    def test_port_reservations_remain_owned_until_launch_handoff(self):
        with self.runner.reserved_ports(6) as connections:
            ports = [connection.getsockname()[1] for connection in connections]
            self.assertEqual(len(set(ports)), 6)
            for port in ports:
                with verifier.socket.socket() as competitor:
                    with self.assertRaises(OSError): competitor.bind(("127.0.0.1", port))
        for port in ports:
            with verifier.socket.socket() as competitor:
                competitor.bind(("127.0.0.1", port))

    def node_setup(self):
        self.runner.active = []
        self.runner.daprd = self.runner.scratch / "daprd"
        self.runner.placement_port = 12345
        self.runner.scheduler_port = 12346

    def test_node_handoff_keeps_sidecar_ports_reserved_until_application_ready(self):
        self.node_setup()
        reservations = []
        original = self.runner.reserved_ports
        @contextlib.contextmanager
        def reserve(count):
            with original(count) as connections:
                reservations.extend(connections)
                yield connections
        application, sidecar = mock.Mock(), mock.Mock()
        def launch(argv, env):
            if argv[0] == "dotnet":
                self.assertEqual(reservations[0].fileno(), -1)
                self.assertTrue(all(connection.fileno() >= 0 for connection in reservations[1:]))
                return application
            self.assertTrue(all(connection.fileno() == -1 for connection in reservations))
            return sidecar
        def readiness(url, process):
            if process is application:
                self.assertTrue(all(connection.fileno() >= 0 for connection in reservations[1:]))
            return {"assemblies": []}
        with mock.patch.object(self.runner, "reserved_ports", side_effect=reserve), mock.patch.object(self.runner, "start", side_effect=launch), mock.patch.object(self.runner, "wait_http", side_effect=readiness):
            self.runner.start_node("3.110.0", "domain", "counter", self.runner.scratch, self.runner.scratch / "config", 10)
        self.assertTrue(all(connection.fileno() == -1 for connection in reservations))
        self.assertEqual(self.runner.active, [application, sidecar])

    def test_node_collision_retries_fresh_ports_and_keeps_other_owned_processes(self):
        self.node_setup()
        original = self.runner.start
        healthy = original([sys.executable, "-c", "import time;time.sleep(30)"])
        self.runner.active.append(healthy)
        launches = []
        def launch(argv, env):
            launches.append(env["ASPNETCORE_URLS"])
            script = "print('address already in use',flush=True);raise SystemExit(7)" if len(launches) == 1 else "import time;time.sleep(30)"
            process = original([sys.executable, "-c", script], env)
            if len(launches) == 1: process.wait(timeout=5)
            return process
        def readiness(url, process):
            if process.poll() is not None:
                raise RuntimeError("Owned application exited during startup")
            return {"assemblies": []}
        with mock.patch.object(self.runner, "start", side_effect=launch), mock.patch.object(self.runner, "wait_http", side_effect=readiness):
            self.runner.start_node("3.110.0", "domain", "counter", self.runner.scratch, self.runner.scratch / "config", 10)
        self.assertEqual(len(launches), 3)
        self.assertNotEqual(launches[0], launches[1])
        self.assertIsNone(healthy.poll())
        self.assertEqual(len(self.runner.active), 3)
        self.assertTrue(any(c["exit_code"] == 7 and "address already in use" in c["diagnostic"] for c in self.runner.commands))
        self.assertTrue(any(process is healthy for process, *_ in self.runner.logs))

    def test_node_collision_retries_are_bounded_and_other_failures_stop(self):
        for diagnostic, attempts in (("address already in use", 3), ("invalid configuration", 1)):
            with self.subTest(diagnostic=diagnostic):
                self.node_setup()
                original = self.runner.start
                def launch(argv, env):
                    process = original([sys.executable, "-c", "print(" + repr(diagnostic) + ",flush=True);raise SystemExit(7)"], env)
                    process.wait(timeout=5)
                    return process
                with mock.patch.object(self.runner, "start", side_effect=launch) as started, mock.patch.object(self.runner, "wait_http", side_effect=RuntimeError("Owned application exited during startup")):
                    with self.assertRaises(RuntimeError):
                        self.runner.start_node("3.110.0", "domain", "counter", self.runner.scratch, self.runner.scratch / "config", 10)
                self.assertEqual(started.call_count, attempts)
                self.assertFalse(self.runner.active)

    def test_sidecar_collision_stops_its_application_before_retry(self):
        self.node_setup()
        original = self.runner.start
        launched = []
        competitor = verifier.socket.socket()
        def launch(argv, env):
            if argv[0] != "dotnet" and len(launched) == 1:
                port = int(argv[argv.index("--metrics-port") + 1])
                competitor.bind(("127.0.0.1", port))
                script = "import socket;socket.socket().bind(('127.0.0.1'," + str(port) + "))"
            else:
                if len(launched) == 2: self.assertIsNotNone(launched[0].poll())
                script = "import time;time.sleep(30)"
            process = original([sys.executable, "-c", script], env)
            launched.append(process)
            if len(launched) == 2: process.wait(timeout=5)
            return process
        def readiness(url, process):
            if process.poll() is not None:
                raise RuntimeError("Owned sidecar exited during startup")
            return {"assemblies": []}
        try:
            with mock.patch.object(self.runner, "start", side_effect=launch), mock.patch.object(self.runner, "wait_http", side_effect=readiness):
                self.runner.start_node("3.110.0", "domain", "counter", self.runner.scratch, self.runner.scratch / "config", 10)
            self.assertEqual(len(launched), 4)
            self.assertEqual(self.runner.active, launched[2:])
            self.assertTrue(any("Address already in use" in c["diagnostic"] and c["exit_code"] != 0 for c in self.runner.commands))
            self.assertGreaterEqual(competitor.fileno(), 0)
        finally:
            competitor.close()

    def test_retry_cleanup_failure_keeps_process_and_attempt_receipt(self):
        self.node_setup()
        original = self.runner.start
        def launch(argv, env):
            return original([sys.executable, "-c", "import time;time.sleep(30)"], env)
        with mock.patch.object(self.runner, "start", side_effect=launch) as started, mock.patch.object(self.runner, "wait_http", side_effect=RuntimeError("startup failed")), mock.patch.object(self.runner, "stop", side_effect=RuntimeError("termination failed")):
            with self.assertRaisesRegex(RuntimeError, "termination failed"):
                self.runner.start_node("3.110.0", "domain", "counter", self.runner.scratch, self.runner.scratch / "config", 10)
        self.assertEqual(started.call_count, 1)
        self.assertEqual(self.runner.commands[-1]["argv"][0], "os.killpg")
        self.assertEqual(self.runner.commands[-1]["exit_code"], 1)
        self.assertEqual(len(self.runner.active), 1)
        self.assertTrue(any(process is self.runner.active[0] for process, *_ in self.runner.logs))

    def test_retry_cleanup_cancellation_stops_startup_retries(self):
        self.node_setup()
        original = self.runner.start
        def launch(argv, env):
            return original([sys.executable, "-c", "import time;time.sleep(30)"], env)
        with mock.patch.object(self.runner, "start", side_effect=launch) as started, mock.patch.object(self.runner, "wait_http", side_effect=RuntimeError("startup failed")), mock.patch.object(self.runner, "stop", side_effect=KeyboardInterrupt()):
            with self.assertRaises(KeyboardInterrupt):
                self.runner.start_node("3.110.0", "domain", "counter", self.runner.scratch, self.runner.scratch / "config", 10)
        self.assertEqual(started.call_count, 1)
        self.assertEqual(self.runner.node_stop_errors, ["KeyboardInterrupt"])
        self.assertEqual(self.runner.commands[-1]["exit_code"], 1)

    def test_scheduler_collision_retries_only_owned_container_and_retains_failure(self):
        identity = "a" * 64
        cidfile = self.runner.scratch / "scheduler.cid"
        def launch(role, image, arguments, **kwargs):
            if not self.runner.commands:
                cidfile.write_text(identity)
                self.runner.containers.append(identity)
                self.runner.record(["docker", "run", "fixture"], verifier.utc(), 125, b"port is already allocated", self.runner.scratch)
                raise ValueError("Command exited 125")
            self.assertFalse(cidfile.exists())
            return "b" * 64, kwargs["host_port"]
        def command(argv, **kwargs):
            self.assertIn(identity, argv)
            if argv[1] == "inspect":
                return json.dumps({"id": identity, "invocation": self.runner.invocation})
            return identity
        with mock.patch.object(self.runner, "container", side_effect=launch) as started, mock.patch.object(self.runner, "run", side_effect=command) as executed:
            self.assertEqual(self.runner.start_scheduler()[0], "b" * 64)
        self.assertEqual(started.call_count, 2)
        self.assertEqual(executed.call_args_list[-1].args[0], ["docker", "rm", "-f", identity])
        self.assertEqual(self.runner.commands[0]["exit_code"], 125)
        self.assertNotIn(identity, self.runner.containers)

    def test_scheduler_retry_retires_a_verified_absent_failed_cid(self):
        identity = "a"*64
        attempts = []
        def launch(*args, **kwargs):
            attempts.append(args)
            if len(attempts) == 1:
                (self.runner.scratch / "scheduler.cid").write_text(identity)
                self.runner.containers.append(identity)
                self.runner.record(["docker", "run", "fixture"], verifier.utc(), 125, b"port is already allocated", self.runner.scratch)
                raise ValueError("Command exited 125")
            return "b"*64, kwargs["host_port"]
        def inspect(argv, **kwargs):
            self.runner.record(argv, verifier.utc(), 1, b"Error: No such object", self.runner.scratch)
            return self.runner.commands[-1]["diagnostic"]
        with mock.patch.object(self.runner, "container", side_effect=launch), mock.patch.object(self.runner, "run", side_effect=inspect):
            self.runner.start_scheduler()
        self.assertNotIn(identity, self.runner.containers)
        self.assertTrue(any(c["argv"][-1] == identity and "No such object" in c["diagnostic"] for c in self.runner.commands))

    def test_scheduler_collision_cannot_remove_an_unrelated_container(self):
        identity = "a" * 64
        def launch(*args, **kwargs):
            (self.runner.scratch / "scheduler.cid").write_text(identity)
            self.runner.record(["docker", "run", "fixture"], verifier.utc(), 125, b"port is already allocated", self.runner.scratch)
            raise ValueError("Command exited 125")
        with mock.patch.object(self.runner, "container", side_effect=launch), mock.patch.object(self.runner, "run", return_value=json.dumps({"id": identity, "invocation": "unrelated"})) as executed:
            with self.assertRaisesRegex(ValueError, "Scheduler retry ownership mismatch"):
                self.runner.start_scheduler()
        self.assertEqual(executed.call_count, 1)

    def test_http_error_body_failure_keeps_attempted_request(self):
        for failure,code in ((TimeoutError("body timed out"),124),(KeyboardInterrupt(),130)):
            body=io.BytesIO()
            error=verifier.urllib.error.HTTPError("http://127.0.0.1:12345/fixture",500,"failed",{},body)
            with self.subTest(code=code),mock.patch.object(verifier.urllib.request,"urlopen",side_effect=error),mock.patch.object(error,"read",side_effect=failure):
                with self.assertRaises(type(failure)):
                    self.runner.http("POST",error.url,{"value":"fixture"})
                self.assertEqual(self.runner.commands[-1]["exit_code"],code)
                self.assertEqual(self.runner.commands[-1]["argv"][-1],"request-sha256="+verifier.sha(verifier.canonical({"value":"fixture"})))

    def test_timeout_kills_owned_group_and_retains_receipt(self):
        with self.assertRaises(TimeoutError):
            self.runner.run([verifier.sys.executable, "-c", "import time; time.sleep(30)"], timeout=.05)
        self.assertEqual(self.runner.commands[-1]["exit_code"], 124)
        self.assertIsNotNone(self.runner.processes[-1].poll())

    def test_authorization_redaction_removes_entire_bearer_token(self):
        redacted = self.runner.redact("Authorization: Bearer fixture-secret")
        self.assertNotIn("fixture-secret", redacted)
        verifier.safe(redacted)

    def test_bare_bearer_redaction_removes_token(self):
        redacted = self.runner.redact("Bearer fixture-secret")
        self.assertNotIn("fixture-secret", redacted)
        verifier.safe(redacted)

    def test_cancellation_kills_owned_group_and_retains_receipt(self):
        with mock.patch.object(verifier.subprocess.Popen, "communicate", side_effect=[KeyboardInterrupt(), (b"", b"")]):
            with self.assertRaises(KeyboardInterrupt):
                self.runner.run([verifier.sys.executable, "-c", "import time; time.sleep(30)"])
        self.assertEqual(self.runner.commands[-1]["exit_code"], 130)
        self.assertIsNotNone(self.runner.processes[-1].poll())

    def test_repeated_cleanup_is_idempotent(self):
        with mock.patch.object(self.runner, "run", return_value=""), mock.patch.object(self.runner, "shared", return_value={}):
            first = self.runner.cleanup()
            second = self.runner.cleanup()
        self.assertEqual(first, second)
        self.assertTrue(first["scratch_removed"])

    def test_repeated_stop_of_startup_failure_is_safe(self):
        self.runner.run([verifier.sys.executable, "-c", "raise SystemExit(7)"], check=False)
        process = self.runner.processes[-1]
        self.runner.stop(process)
        self.runner.stop(process)
        self.assertEqual(process.returncode, 7)

    def test_command_launch_failure_retains_literal_exit_127(self):
        command = str(pathlib.Path(self.temp.name) / "missing-command")
        with self.assertRaises(FileNotFoundError):
            self.runner.run([command, "fixture-argument"])
        self.assertEqual(self.runner.commands[-1]["argv"], [command, "fixture-argument"])
        self.assertEqual(self.runner.commands[-1]["exit_code"], 127)

    def test_node_launch_failure_retains_literal_exit_127(self):
        command = str(pathlib.Path(self.temp.name) / "missing-node")
        with self.assertRaises(FileNotFoundError):
            self.runner.start([command])
        self.assertEqual(self.runner.commands[-1]["argv"], [command])
        self.assertEqual(self.runner.commands[-1]["exit_code"], 127)

    def test_cleanup_discovery_failure_still_removes_scratch_and_writes_receipt(self):
        with mock.patch.object(self.runner, "shared", side_effect=FileNotFoundError("missing Docker")):
            cleanup = self.runner.cleanup()
        self.assertTrue(cleanup["scratch_removed"] and cleanup["owned_processes_stopped"] and cleanup["owned_containers_removed"])
        self.assertFalse(cleanup["shared_discovery_complete"])
        self.assertIsNone(cleanup["shared_after"])
        self.assertTrue(self.output.joinpath("cleanup.json").exists())

    def test_shared_inspection_never_retains_unrelated_environment(self):
        root = pathlib.Path(self.temp.name)
        binary = root / "bin/docker"
        binary.parent.mkdir()
        projection = {"id": "a" * 64, "image": "sha256:" + "b" * 64, "running": True, "started": "fixture-time", "invocation": ""}
        script = "#!" + sys.executable + "\nimport json,sys\nif sys.argv[1]=='ps': print('" + "a" * 64 + "')\nelif '--format' in sys.argv and sys.argv[sys.argv.index('--format')+1]==" + repr(verifier.RESOURCE_INSPECT_FORMAT) + ": print(" + repr(json.dumps(projection)) + ")\nelse: print(json.dumps({'Config':{'Env':['UNRELATED_SECRET=fixture-private-value']},'Mounts':['fixture-mount']}))\n"
        binary.write_text(script)
        binary.chmod(0o700)
        self.runner.env["PATH"] = str(binary.parent)
        snapshot = self.runner.shared()
        self.assertEqual(snapshot["a" * 64], {key: projection[key] for key in ("image", "running", "started")})
        retained = self.output.joinpath("commands.json").read_text()
        self.assertNotIn("fixture-private-value", retained)
        self.assertNotIn("fixture-mount", retained)
        self.assertTrue(any(c["argv"][:3] == ["docker", "inspect", "--format"] for c in self.runner.commands))

    def test_process_exit_race_at_initial_killpg_is_tolerated(self):
        process = mock.Mock(pid=1234)
        process.poll.return_value = None
        with mock.patch.object(verifier.os, "killpg", side_effect=ProcessLookupError):
            self.runner.stop(process)
        process.wait.assert_called_once_with(timeout=8)

    def descendant_command(self):
        marker=self.runner.scratch / "descendant.pid"
        child="import os,pathlib,signal,time; signal.signal(signal.SIGTERM,signal.SIG_IGN); pathlib.Path("+repr(str(marker))+").write_text(str(os.getpid())); time.sleep(30)"
        parent="import subprocess,sys,time; subprocess.Popen([sys.executable,'-c',"+repr(child)+"],stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL); time.sleep(30)"
        return marker,[sys.executable,"-c",parent]

    def assert_descendant_stopped(self,marker):
        self.assertTrue(marker.is_file(),"Owned descendant never started")
        pid=int(marker.read_text());deadline=time.monotonic()+2
        try:
            while time.monotonic()<deadline:
                proc=pathlib.Path("/proc")/str(pid)/"stat"
                try:
                    if proc.read_text().split()[2]=="Z": return
                except (FileNotFoundError,ProcessLookupError):
                    return
                time.sleep(.02)
            self.fail("SIGTERM-ignoring owned descendant survived cleanup")
        finally:
            try: os.kill(pid,signal.SIGKILL)
            except ProcessLookupError: pass

    def test_timeout_terminates_sigterm_ignoring_descendant(self):
        marker,command=self.descendant_command()
        with self.assertRaises(TimeoutError): self.runner.run(command,timeout=.8)
        self.assert_descendant_stopped(marker)
        self.assertEqual(self.runner.commands[-1]["exit_code"],124)

    def test_cancellation_terminates_sigterm_ignoring_descendant(self):
        marker,command=self.descendant_command();previous=signal.getsignal(signal.SIGALRM)
        def cancel(signum,frame): raise KeyboardInterrupt()
        signal.signal(signal.SIGALRM,cancel);signal.setitimer(signal.ITIMER_REAL,.8)
        try:
            with self.assertRaises(KeyboardInterrupt): self.runner.run(command)
        finally:
            signal.setitimer(signal.ITIMER_REAL,0);signal.signal(signal.SIGALRM,previous)
        self.assert_descendant_stopped(marker)
        self.assertEqual(self.runner.commands[-1]["exit_code"],130)

    def test_repeated_cleanup_terminates_descendant_after_parent_exit(self):
        marker,command=self.descendant_command();process=self.runner.start(command)
        deadline=time.monotonic()+2
        while not marker.exists() and time.monotonic()<deadline: time.sleep(.02)
        preserved_marker=pathlib.Path(self.temp.name)/"captured-descendant.pid"
        preserved_marker.write_text(marker.read_text())
        os.kill(process.pid,signal.SIGTERM);process.wait(timeout=2)
        with mock.patch.object(self.runner,"shared",return_value={}):
            first=self.runner.cleanup();second=self.runner.cleanup()
        self.assertEqual(first,second)
        self.assertFalse(self.runner.scratch.exists())
        self.assert_descendant_stopped(preserved_marker)

    def test_cleanup_keyboard_interrupt_continues_independent_resources(self):
        self.runner.processes=[mock.Mock(pid=111),mock.Mock(pid=222)]
        for process in self.runner.processes: process.poll.return_value=0
        with mock.patch.object(self.runner,"stop",side_effect=[KeyboardInterrupt(),None]) as stop, mock.patch.object(self.runner,"shared",return_value={}):
            receipt=self.runner.cleanup()
        self.assertEqual(stop.call_count,2)
        self.assertTrue(receipt["scratch_removed"])
        self.assertIn("KeyboardInterrupt",receipt["errors"])
        self.runner.processes=[]

    def test_container_cleanup_uses_full_ids_and_retries_independent_failures(self):
        first,second="a"*64,"b"*64
        self.runner.containers=[first,second];self.runner.container_launch_attempted=True
        remaining={first,second};removed=[];fail_once=True
        def docker(argv,*args,**kwargs):
            nonlocal fail_once
            if argv[:3]==["docker","ps","-aq"]:
                self.assertIn("--no-trunc",argv)
                return "\n".join(sorted(remaining))
            identity=argv[-1]
            if argv[1]=="inspect":
                if identity not in remaining:
                    self.runner.record(argv,verifier.utc(),1,b"error: no such object",self.runner.scratch)
                    return "error: no such object"
                return json.dumps({"id":identity,"invocation":self.runner.invocation})
            if argv[1]=="rm":
                if identity==first and fail_once:
                    fail_once=False;raise RuntimeError("Owned removal failed once")
                removed.append(identity);remaining.remove(identity);return identity
            self.fail("Unexpected Docker operation")
        with mock.patch.object(self.runner,"run",side_effect=docker),mock.patch.object(self.runner,"shared",return_value={}):
            failed=self.runner.cleanup()
            self.assertFalse(failed["owned_containers_removed"])
            self.assertEqual(removed,[second])
            retried=self.runner.cleanup()
        self.assertEqual(removed,[second,first])
        self.assertTrue(retried["owned_containers_removed"])
        self.assertTrue(self.output.joinpath("cleanup-attempts.json").is_file())
        self.assertTrue(retried["errors"],"Original failure evidence must remain")

    def test_http_transport_failure_and_cancellation_retain_request(self):
        for failure,code in ((OSError("offline"),0),(KeyboardInterrupt(),130)):
            with self.subTest(code=code),mock.patch.object(verifier.urllib.request,"urlopen",side_effect=failure):
                with self.assertRaises(type(failure)):
                    self.runner.http("POST","http://127.0.0.1:12345/fixture",{"value":"fixture"})
                receipt=self.runner.commands[-1]
                self.assertEqual(receipt["exit_code"],code)
                self.assertEqual(receipt["argv"][-1],"request-sha256="+verifier.sha(verifier.canonical({"value":"fixture"})))

    def test_actual_cli_missing_and_failing_docker_retains_nonpassing_packet(self):
        root = pathlib.Path(self.temp.name)
        script = """import sys
import run_verification as v
original=v.tempfile.mkdtemp
v.tempfile.mkdtemp=lambda **kwargs: original(prefix='owned-',dir=sys.argv[2])
raise SystemExit(v.main(['--out',sys.argv[1]]))
"""
        for variant in ("missing", "failing"):
            with self.subTest(variant=variant):
                case = root / variant
                scratch = case / "scratch"
                scratch.mkdir(parents=True)
                binary = case / "bin"
                binary.mkdir()
                if variant == "failing":
                    docker = binary / "docker"
                    docker.write_text("#!/bin/sh\nexit 17\n")
                    docker.chmod(0o700)
                output = case / "output"
                result = subprocess.run([sys.executable, "-c", script, str(output), str(scratch)], cwd=verifier.HERE, env=dict(os.environ, PATH=str(binary)), capture_output=True, timeout=15)
                self.assertEqual(result.returncode, 2, result.stderr.decode())
                self.assertEqual(list(scratch.iterdir()), [])
                cleanup = json.loads((output / "cleanup.json").read_text())
                self.assertTrue(cleanup["owned_processes_stopped"] and cleanup["owned_containers_removed"] and cleanup["scratch_removed"])
                self.assertFalse(cleanup["shared_discovery_complete"])
                self.assertEqual({e["phase"] for e in cleanup["discovery_errors"]}, {"before", "after"})
                commands = json.loads((output / "commands.json").read_text())
                discovery = [c for c in commands if c["argv"][:3] == ["docker", "ps", "-aq"]]
                self.assertEqual([c["exit_code"] for c in discovery], [127, 127] if variant == "missing" else [17, 17])
                self.assertTrue((output / "SHA256SUMS").exists())
                rows = json.loads((output / "scenario-results.json").read_text())["scenarios"]
                self.assertEqual(len(rows), len(verifier.SCENARIOS))
                self.assertTrue(all(r["execution"] == "unavailable" for r in rows if r["id"] != "failure-cleanup"))

    def test_actual_cli_node_stop_failure_is_retained_with_cleanup(self):
        root = pathlib.Path(self.temp.name)
        output = root / "stop-failure"
        script = """import sys
import run_verification as v
class StopFailure(v.Runner):
    def shared(self): return {}
    def prepare(self):
        self.active=[self.start([sys.executable,'-c','import time; time.sleep(30)'])]
        self.assertion(True,'started')
        return [{'id':'fixture','assertions':1}], 'compatible'
    def stop(self, process):
        if not getattr(self,'failed_once',False):
            self.failed_once=True
            raise RuntimeError('injected owned stop failure')
        return super().stop(process)
v.Runner=StopFailure
raise SystemExit(v.main(['--out',sys.argv[1]]))
"""
        result = subprocess.run([sys.executable, "-c", script, str(output)], cwd=verifier.HERE, capture_output=True, timeout=15)
        self.assertEqual(result.returncode, 2, result.stderr.decode())
        cleanup = json.loads((output / "cleanup.json").read_text())
        self.assertTrue(cleanup["scratch_removed"])
        self.assertIn("RuntimeError", cleanup["errors"])
        self.assertFalse(cleanup["owned_processes_stopped"])
        commands = json.loads((output / "commands.json").read_text())
        self.assertTrue(any(c["argv"][0] == "os.killpg" and c["exit_code"] == 1 for c in commands))
        self.assertTrue((output / "manifest.json").exists() and (output / "SHA256SUMS").exists())

    def test_failed_scenario_quiesces_owned_nodes_before_next_lane(self):
        def fail_after_start():
            process = self.runner.start([verifier.sys.executable, "-c", "import time; time.sleep(30)"])
            self.runner.active = [process]
            raise ValueError("fixture failure")
        with mock.patch("builtins.print"):
            row = self.runner.scenario("mixed-api", fail_after_start)
        self.assertEqual(row["execution"], "failed")
        self.assertEqual(self.runner.active, [])
        self.assertIsNotNone(self.runner.processes[-1].poll())
        self.assertTrue(row["command_ids"])

    def test_actual_runner_sigint_stops_matrix_and_cleans_owned_processes(self):
        root = pathlib.Path(self.temp.name)
        ready = root / "cancel-ready"
        topology = root / "unexpected-topology"
        output = root / "cancelled-invocation"
        command = "import pathlib,time; pathlib.Path(" + repr(str(ready)) + ").write_text('ready'); time.sleep(30)"
        binaries = root / "cancel-bin"
        binaries.mkdir()
        docker = binaries / "docker"
        docker.write_text("#!"+sys.executable+"\nimport sys\nassert sys.argv[1:]==['ps','-aq','--no-trunc']\n")
        docker.chmod(0o700)
        # Only external-container discovery is stubbed: this isolated control owns no containers.
        # The real main loop, subprocess, SIGINT, receipts and filesystem/process cleanup all execute.
        script = """import pathlib,sys
import run_verification as v
class IsolatedRunner(v.Runner):
    def prepare(self):
        return self.run([sys.executable, '-c', sys.argv[3]])
    def topology(self):
        pathlib.Path(sys.argv[2]).write_text('unexpected')
v.Runner = IsolatedRunner
raise SystemExit(v.main(['--out', sys.argv[1]]))
"""
        process = subprocess.Popen([sys.executable, "-c", script, str(output), str(topology), command], cwd=verifier.HERE, env=dict(os.environ, PATH=str(binaries)+os.pathsep+os.environ.get("PATH", "")), stdout=subprocess.PIPE, stderr=subprocess.PIPE, start_new_session=True)
        try:
            deadline = time.monotonic() + 8
            while not ready.exists() and process.poll() is None and time.monotonic() < deadline:
                time.sleep(.01)
            self.assertTrue(ready.exists(), "Cancellation fixture did not reach its owned process")
            os.kill(process.pid, signal.SIGINT)
            stdout, stderr = process.communicate(timeout=12)
            self.assertEqual(process.returncode, 1, (stdout + stderr).decode())
            self.assertFalse(topology.exists(), "Cancelled invocation continued its matrix")
            commands = json.loads((output / "commands.json").read_text())
            self.assertTrue(any(c["exit_code"] == 130 and c["argv"][-1] == command for c in commands))
            cleanup = json.loads((output / "cleanup.json").read_text())
            self.assertTrue(cleanup["owned_processes_stopped"] and cleanup["scratch_removed"] and cleanup["owned_containers_removed"])
            manifest = json.loads((output / "manifest.json").read_text())
            self.assertIn("KeyboardInterrupt", manifest["interruption"])
            results = verifier.validate(output)
            self.assertTrue(all(r["execution"] == "unavailable" for r in results["scenarios"] if r["id"] != "failure-cleanup"))
        finally:
            if process.poll() is None:
                os.killpg(process.pid, signal.SIGKILL)
                process.wait()
            for stream in (process.stdout, process.stderr):
                stream.close()


if __name__ == "__main__":
    unittest.main()

"""Evidence mutation and owned lifecycle controls; no shared resources are touched."""
import copy
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
        self.manifest = {"schema": "hexalith.p1r.verification.v1", "coordinates": verifier.SOURCES, "versions": list(verifier.VERSIONS), "runtime": {"dapr": "1.18.2", "postgresql": verifier.POSTGRES}, "usable_as_prerequisite": False, "owner_acceptance_granted": False, "preserved_before": {"acceptance": "unchanged"}, "preserved_after": {"acceptance": "unchanged"}, "fixture_hashes": {p: verifier.sha((verifier.HERE / p).read_bytes()) for p in verifier.FIXTURES}}
        self.results = {"qualified": False, "exit_code": 1, "scenarios": [{"id": name, "execution": "unavailable", "compatibility": "unverified", "command_ids": [1], "assertions": 0, "cases": [{"id": "blocked", "assertions": 0}]} for name in verifier.SCENARIOS]}
        self.commands = [{"id": 1, "argv": ["fixture-command"], "cwd": "/fixture", "started_utc": "2026-10-04T01:00:00Z", "finished_utc": "2026-10-04T01:00:01Z", "exit_code": 1, "output_sha256": "a" * 64}]
        self.cleanup = {"owned_processes_stopped": True, "owned_containers_removed": True, "scratch_removed": True, "shared_before": {"shared": "unchanged"}, "shared_after": {"shared": "unchanged"}, "shared_discovery_complete": True}
        self.save()

    def tearDown(self):
        self.temp.cleanup()

    def save(self):
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
        if argv[:1] == ["HTTP"] and code == 0:
            code = 200
        rendered = output if output is not None else json.dumps(data, sort_keys=True) + "\n" if data is not None else ""
        instant = verifier.dt.datetime(2026,10,4,1,tzinfo=verifier.dt.timezone.utc) + verifier.dt.timedelta(seconds=2*len(self.commands))
        command = {"id": len(self.commands)+1, "argv": [str(a) for a in argv], "cwd": "/fixture", "started_utc": instant.isoformat(), "finished_utc": (instant+verifier.dt.timedelta(seconds=1)).isoformat(), "exit_code": code, "diagnostic": rendered, "output_sha256": verifier.sha(rendered.encode()), **extra}
        self.commands.append(command)
        return command

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
        lock={"version":1,"dependencies":{"net10.0":locked}}
        verifier.write(self.directory / "artifacts" / lane / (project+"-assets.json"),graph)
        verifier.write(self.directory / "artifacts" / lane / (project+"-lock.json"),lock)
        assets="/fixture/"+lane+"/"+project.lower()+"/obj/project.assets.json"
        locked_path="/fixture/"+lane+"/"+project.lower()+"/packages.lock.json"
        self.add_command([sys.executable,"-c",verifier.ARTIFACT_INVENTORY_SCRIPT,assets,locked_path,"hash-restored-graphs"],{assets:verifier.sha(verifier.canonical(graph)),locked_path:verifier.sha(verifier.canonical(lock))})
        self.add_command([sys.executable,"-c",verifier.ASSEMBLY_INVENTORY_SCRIPT,"/fixture/"+lane+"/"+project.lower()+"/bin/"+("Debug" if lane=="current" else "Release")+"/net10.0","hash-built-assemblies"],{name:hashes[name] for name in names})

    def nodes(self,lane,hashes=None):
        hashes=hashes or verifier.PUBLISHED_DLL_HASHES[lane]
        for kind,required in (("host",verifier.HOST_REQUIRED),("domain",verifier.DOMAIN_REQUIRED)):
            configuration="Debug" if lane=="current" else "Release"
            self.add_command(["dotnet","/fixture/"+lane+"/"+kind+"/bin/"+configuration+"/net10.0/"+kind.capitalize()+".dll"])
            self.add_command(["/fixture/daprd","--app-id","eventstore" if kind=="host" else "counter","--dapr-http-port","12345"])
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

    def inventory_evidence(self,database,rows,name):
        output=json.dumps(rows)+"\n"
        command=self.add_command(["docker","exec","a"*64,"psql","-U","postgres","-d",database,"-At","-v","ON_ERROR_STOP=1","-c",verifier.INVENTORY_SQL],output=output)
        value={"rows":rows,"domain_rows":rows,"sha256":verifier.sha(verifier.canonical(rows)),"query_command_id":command["id"],"query_output":output,"database_identity_sha256":verifier.sha(database.encode())}
        verifier.write(self.directory / "inventories" / (name+".json"),value)
        return value

    def seed_evidence(self,lane):
        self.nodes(lane)
        for tenant,count in (("tenant-a",12),("tenant-b",3)):
            self.add_command(["dotnet","/fixture/"+lane+"/probe/bin/Release/net10.0/Probe.dll","seed","http://127.0.0.1:12345",tenant,"fixture",str(count)],{"committedEvents":count,"hydratedCount":count,"assertions":2*(count+1)})

    def actor_evidence(self,lane,tenant,count=12,kind="AssertCounter",accepted=True):
        outcome={"accepted":accepted,"eventCount":1 if kind=="IncrementCounter" and accepted else 0,"assertions":1,"failureReason":None,"failureCategory":None,"errorSha256":None}
        command=self.add_command(["dotnet","/fixture/"+lane+"/probe/bin/Release/net10.0/Probe.dll","actor","http://127.0.0.1:12345",tenant,"fixture","fixture-correlation",kind,str(count)],outcome)
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
        before=self.inventory_evidence("p1r_source",self.state_rows(),"restore-before")
        dump=self.add_command(["docker","exec","a"*64,"pg_dump","-U","postgres","-Fc","p1r_source"],output_sha256="b"*64,output_bytes=10,binary_output_retained=False)
        if scenario=="pre-upgrade-restore":
            self.nodes(verifier.VERSIONS[0]);self.actor_evidence(verifier.VERSIONS[0],"tenant-a",0,"IncrementCounter")
            self.add_command(["dotnet","/fixture/3.110.0/host/bin/Release/net10.0/Host.dll"])
        self.add_command(["docker","exec","-i","a"*64,"pg_restore","-U","postgres","-d","p1r_restored","--exit-on-error"],input_sha256="b"*64,input_bytes=10)
        copied=self.inventory_evidence("p1r_restored",self.state_rows(),"restore-copied")
        self.nodes(verifier.VERSIONS[1])
        for tenant,count in (("tenant-a",12),("tenant-b",3)):
            self.actor_evidence(verifier.VERSIONS[1],tenant,count)
            self.add_command(["HTTP","GET","http://127.0.0.1:12345/sequence/"+tenant+"/fixture"],count)
        after=self.inventory_evidence("p1r_restored",self.state_rows(),"restore-after")
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
        built={}
        for project in ("Host","Domain","Probe"):
            built[project]={name:hashes[name] for name in verifier.graph_packages("current",project)}
            self.graph_evidence("current",project,hashes)
        loaded={"assemblies":[{"name":name,"version":"3.110.0.0","sha256":hashes[name]} for name in sorted(verifier.PROBE_ASSEMBLIES)]}
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
        self.commands.append({**self.commands[0], "id": 2, "argv": ["HTTP", "GET", "http://127.0.0.1:12345/identity"], "exit_code": 200, "diagnostic": diagnostic, "output_sha256": verifier.sha(diagnostic.encode())})
        self.save()
        self.reject()

    def test_no_command_receipt_is_rejected(self):
        self.results["scenarios"][0]["command_ids"] = []
        self.save()
        self.reject()

    def test_missing_per_case_commands_is_rejected(self):
        row = self.persisted_pass()
        row["cases"][0]["command_ids"] = []
        self.save()
        self.reject()


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
        # Only external-container discovery is stubbed: this isolated control owns no containers.
        # The real main loop, subprocess, SIGINT, receipts and filesystem/process cleanup all execute.
        script = """import pathlib,sys
import run_verification as v
class IsolatedRunner(v.Runner):
    def shared(self):
        return {}
    def prepare(self):
        return self.run([sys.executable, '-c', sys.argv[3]])
    def topology(self):
        pathlib.Path(sys.argv[2]).write_text('unexpected')
    def run(self, argv, *args, **kwargs):
        if str(argv[0]) == 'docker':
            self.record(argv, v.utc(), 0, b'', self.scratch)
            return ''
        return super().run(argv, *args, **kwargs)
v.Runner = IsolatedRunner
raise SystemExit(v.main(['--out', sys.argv[1]]))
"""
        process = subprocess.Popen([sys.executable, "-c", script, str(output), str(topology), command], cwd=verifier.HERE, stdout=subprocess.PIPE, stderr=subprocess.PIPE, start_new_session=True)
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

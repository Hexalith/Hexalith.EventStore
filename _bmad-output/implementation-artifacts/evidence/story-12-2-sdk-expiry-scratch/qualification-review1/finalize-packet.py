from pathlib import Path
import json,hashlib,subprocess,datetime,xml.etree.ElementTree as ET,shutil
R=Path('/home/administrator/projects/hexalith/folders/references/Hexalith.EventStore')
O=R/'_bmad-output/implementation-artifacts/evidence/story-12-2-sdk-expiry-scratch/qualification-review1'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def dump(name,obj):(O/name).write_text(json.dumps(obj,indent=2)+'\n')
commands=json.loads((O/'commands.json').read_text())
assert len(commands)==12
for c in commands:
 assert c['exit_code']==0
 if 'xml' in c:
  root=ET.parse(O/c['xml']).getroot()
  assert c['result']['total']>0 and c['result']['failed']==0 and c['result']['errors']==0
  c['skips']=[{'name':t.get('name'),'reason':t.findtext('reason/message') or t.findtext('reason')} for t in root.findall('.//test') if t.get('result')=='Skip']
  assert c['result']['skipped']==(2 if c['id']=='client-full' else 0)
dump('commands.json',commands)
# Current inventory and a bounded, explicit test-only difference from the completed SDK runs.
freeze=json.loads((O/'source-freeze-before-final.json').read_text())
drift=[v['path'] for v in freeze if sha(R/v['path'])!=v['sha256']]
assert drift==['tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Integration/ProjectionExpiryAndScratchRecoveryLiveTests.NamedDispatcher.cs'],drift
files={R/v['path'] for v in freeze}
for folder in ['src/Hexalith.EventStore.Client','src/Hexalith.EventStore.DomainService','src/Hexalith.EventStore.Testing','tests/Hexalith.EventStore.Client.Tests','tests/Hexalith.EventStore.DomainService.Tests','tests/Hexalith.EventStore.Server.LiveSidecar.Tests']:
 files|={p for p in (R/folder).rglob('*') if p.is_file() and not set(p.relative_to(R).parts)&{'bin','obj','.git'}}
# Include every source read from the qualified production Server PDB as well.
for c in json.loads((O/'pdb-live-process.json').read_text()):
 files|={Path(v['source']) for v in c['documents'] if Path(v['source']).is_relative_to(R)}
spec=R/'_bmad-output/implementation-artifacts/spec-folders-12-2-expiry-and-scratch-recovery.md';files.add(spec)
inventory=[{'path':str(p.relative_to(R)),'bytes':p.stat().st_size,'sha256':sha(p)} for p in sorted(files)]
dump('source-files.json',inventory)
dump('source-drift.json',{'production_source_drift':[],'test_only_process_extension_changed_existing_sources':drift,'new_test_sources':['tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Integration/ProjectionExpiryAndScratchRecoveryLiveTests.ProcessRestart.cs','tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Integration/ProjectionScratchProcessWorkerTests.cs'],'earlier_test_source_snapshot':'pre-process-extension-test-source/ProjectionExpiryAndScratchRecoveryLiveTests.NamedDispatcher.cs','note':'All 902 other pre-extension scoped source/config files retain exact hashes. The spec task record is administrative and was excluded from the pre-run freeze.'})
binary_drift=[]
for c in commands:
 if 'binary_group' not in c:continue
 for v in json.loads((O/c['binary_group']).read_text()):
  if sha(R/v['path'])!=v['sha256']:
   binary_drift.append({'run':c['id'],'path':v['path'],'qualified_sha256':v['sha256'],'current_sha256':sha(R/v['path'])})
assert all(v['run'] in ['live-expiry-scratch','live-read-model-batch','live-shared-epoch'] and Path(v['path']).stem in ['Hexalith.EventStore.Server.LiveSidecar.Tests','Hexalith.EventStore.Server','Hexalith.EventStore.Gateway'] for v in binary_drift),binary_drift
dump('binary-group-differences.json',binary_drift)
# Explicit post-extension group includes dependency manifests in addition to DLL/PDB/XML.
folder=R/'tests/Hexalith.EventStore.Server.LiveSidecar.Tests/bin/Debug/net10.0'
dump('live-process-runtime-group.json',[{'path':str(p.relative_to(R)),'bytes':p.stat().st_size,'sha256':sha(p)} for p in sorted(folder.iterdir()) if p.name.startswith('Hexalith.EventStore') and (p.suffix in ['.dll','.pdb','.xml'] or p.name.endswith(('.deps.json','.runtimeconfig.json')))])
observations=[]
for c in commands:
 if 'xml' not in c or not c['id'].startswith('live-'):continue
 root=ET.parse(O/c['xml']).getroot()
 for t in root.findall('.//test'):
  output=t.findtext('output')
  if output:observations.append({'run':c['id'],'test':t.get('name'),'output':output})
dump('raw-persisted-observations.json',observations)
workers=json.loads((O/'process-workers.json').read_text())
assert len(workers)==4 and len({w['pid'] for w in workers})==4
for tenant in {w['tenant'] for w in workers}:
 assert {w['phase'] for w in workers if w['tenant']==tenant}=={'persist','resume'}
# Historical packet byte-for-byte integrity and final read-only working tree.
history=R/'_bmad-output/implementation-artifacts/evidence/story-12-2-sdk-expiry-scratch/qualification-20261020'
p=subprocess.run(['sha256sum','--check','SHA256SUMS'],cwd=history,capture_output=True,text=True);assert p.returncode==0
(O/'historical-packet-unchanged.log').write_text(p.stdout+p.stderr)
p=subprocess.run(['git','diff','--check'],cwd=R,capture_output=True,text=True);assert p.returncode==0
(O/'diff-check.log').write_text('command: git diff --check\nexit-code: 0\n'+p.stdout+p.stderr)
p=subprocess.run(['git','status','--short'],cwd=R,capture_output=True,text=True);(O/'final-working-tree.log').write_text(p.stdout+p.stderr)
qual={
 'schema':'hexalith-sdk-expiry-scratch-review1-qualification/v1',
 'created_utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),
 'scope':'EventStore SDK local source qualification against the owner spec and its approved proposal context. Parent owns independent review and Folders adoption. No publication, deployment, dependency/pin change or Git mutation.',
 'status':'in-progress; implemented and locally qualified, pending parent review',
 'history':{'baseline':'d4d05f871991ec2af305267f59dcefeae3f127ed','eventstore_head':'509dbe3733956a86c2ee2fddfd9c0d0006bbfea3','exact_read_only_commands':'environment-and-history.json','historical_packet':'../qualification-20261020','historical_packet_integrity':'historical-packet-unchanged.log'},
 'public_api':{'deadline_write':'ReadModelBatchOperation.WriteExpiring(key, value, concurrency, expiresAtUtc)','compatibility':'Original Write four-argument signature and default/null/TimeSpan source calls, mutation constructor and Deconstruct preserved. Historical pre-review notes use the superseded overload spelling.'},
 'runtime':{'dotnet_sdk':'10.0.402','dotnet_runtime':'10.0.13','dapr_client':'1.18.4','daprd':'1.18.7','redis':'6.2.21','environment':'environment-and-history.json'},
 'source':{'inventory':'source-files.json','files':len(inventory),'inventory_sha256':sha(O/'source-files.json'),'aggregate_algorithm':'SHA256 of UTF-8 path + NUL + lowercase file SHA256 + LF, sorted by path','aggregate_sha256':hashlib.sha256(''.join(v['path']+'\0'+v['sha256']+'\n' for v in inventory).encode()).hexdigest(),'scope':'Complete affected Client, DomainService and Testing source/config/resources; all three affected test projects; production Server PDB sources; repository build/config/context guides and final owner spec. No bin/obj or historical evidence.','drift':'source-drift.json','production_source_drift':[]},
 'commands':'commands.json',
 'results':{'client_focused':{'passed':195,'failed':0,'skipped':0},'client_full':{'passed':1736,'failed':0,'skipped':2,'skips':next(c['skips'] for c in commands if c['id']=='client-full')},'domain_focused_actual_dispatcher':{'passed':10,'failed':0,'skipped':0},'domain_full':{'passed':1005,'failed':0,'skipped':0},'live_outer':{'passed':36,'failed':0,'skipped':0,'groups':{'expiry_scratch_owner_proof':20,'existing_batch':6,'existing_epoch':8,'pending_owner_scratch_os_process':2}},'live_child_workers':{'invocations':4,'passed':4,'failed':0,'skipped':0,'records':'process-workers.json'}},
 'builds':'Client, DomainService, live, and the test-only live process extension all built with zero warnings/errors in Debug/source mode. Exact commands and raw logs are retained.',
 'binary_provenance':{'groups':'Each test command references its distinct exact DLL/PDB/XML hash inventory. The added process lane also has live-process-runtime-group.json.','inspection':'pdb-source-checks.json','checks':'Every scoped project C# source is represented in the inspected PDB, every document exists, and every current source checksum matches. Client273/Domain179/Testing40 in each graph; ClientTests175, DomainTests102, Server606. LiveTests122 before the test-only extension and124 after it.','test_only_extension':'binary-group-differences.json retains the precise old/new live test DLL/PDB differences. The affected SDK Client/DomainService/Testing assemblies and all Client/Domain result groups remain unchanged. Server/Gateway rebuild outputs also differ and are retained as distinct groups; no cause for those byte differences is asserted. Original pre-extension helper source is retained; no identical-PE claim across different build graphs.'},
 'diagnostics':{'failures':'diagnostic-failures.json','retained_intermediate_artifacts':'diagnostic-* folders are historical diagnostics and are not substituted for final results.','dispositions':[{'test':'SharedProjectionEpochCoordinatorTests.SingleKeyReadClassifiesDaprPostReadStateOutageAsUnavailable','assertion':'Expected SharedProjectionReadUnavailableException, but no exception was thrown.','cause_and_fix':'Background promoted cleanup consumed the fixture next-read outage injection. The fixtures now await their own maintenance before arming the foreground fault; assertions remain intact.'},{'test':'SharedProjectionStreamedStageTests.TenThousandHighEntropyRowsBeyond64MiBPromoteOnlyAfterSealedStream','assertion':'Private scratch marker cleanup is incomplete; diagnostic repeated with status=Committed and etag-present=True.','cause_and_fix':'Overlapping same-instance background/foreground cleanup could erase and recreate its cleanup receipt. Own active work is drained; fresh workers retain durable terminal/CAS guard and retryable backpressure. Deterministic same/fresh overlap regressions and final full Client run pass. Not classified as a pre-existing flake.'}]},
 'live_observations':{'raw_assertion_outputs':'raw-persisted-observations.json','remaining_redis_ttl_milliseconds':3990,'physical_retention':'Original remaining TTL, physical model and dependent receipt disappearance, durable checkpoint, original previous-value TTL restoration and physical disappearance are asserted.','production_named_dispatcher':'12 instance-replacement phase cases plus2 owning-OS-process cases use production dispatcher cleanup, ordinary two-owner delivery and a fresh named rebuild; no test-authored recovery/private-delete repair.','os_process':'Two exited persist workers and two distinct recovery workers share the fixture sidecar/persisted state. Actual PIDs/commands/XML/logs are retained under process-workers. This is an owning worker OS-process change, not a sidecar restart.','owner_proof':'Both x and escape-heavy ~ logical16384 rows have physical model21896 and total physical proof22402 bytes. Real SDK proof is carried by a fixed HTTP test response through the production selected-owner gateway; unchanged physical32768 guard admits both. Component cases enforce logical16384 and refuse16385 in both profiles.','transaction':'Production Redis stays Resumable. Forced TransactionQualified is partial-visible-write/refusal evidence for legacy v1 and deadline v2 only; no atomicity, rollback or hidden-row claim.','cleanup':[c['cleanup'] for c in commands if 'cleanup' in c],'extra_diagnostic_cleanup':'diagnostic-owner-gateway-cleanup.json'},
 'remaining_limits':[
 'Parent independent review and Folders adoption remain open; owner status stays in-progress.',
 'Two existing optional Client isolated-endpoint skips are not acceptance evidence. All selected live outer and worker runs have zero skips.',
 'Generic abandoned streamed-metadata cleanup retains the baseline strict terminal guard: a Committed receipt with already physically missing participants is not accepted or reinterpreted through a new marker-only recovery protocol. Fresh-worker overlap is safely backpressured; retry succeeds after the original worker finishes. Approved owner-scratch recovery is independently proven across exited OS processes using original retained evidence.',
 'Initially insufficient legacy/no-manifest or committed legacy evidence remains fail-closed; unavailable original fingerprint material is not synthesized.',
 'No TransactionQualified Redis provider qualification, sidecar restart during pending owner-scratch, package/release qualification, publication, dependency pin change, public v2 exposure, >10000 identity-vector completion or whole-story acceptance is claimed.'
 ],
 'implementation_spec':{'path':str(spec.relative_to(R)),'sha256':sha(spec),'status':'in-progress','checked_tasks':spec.read_text().count('- [x]')},
 'evidence_retention':{'attributes':'.gitattributes','rule':'* -text','reason':'Preserves exact raw evidence bytes across Git checkout; historical packets are untouched.'},
 'result_authority':'Exact command exit codes, XML counts, raw persisted-state assertions and source/PDB/binary hashes are authoritative. Diagnostic and pre-extension groups are labeled separately.'
}
dump('qualification.json',qual)
(O/'README.md').write_text('''Current EventStore SDK source qualification, pending parent review. The owner spec stays in-progress.

Read qualification.json and commands.json for exact scope, commands, counts, source/binary binding and limits.
Full Client: 1,736 passed, two existing optional skips. Full DomainService: 1,005 passed, zero skips.
Live: 36 outer cases and four child worker invocations passed with zero skips.
The two pending owner-scratch process cases use exited owning workers and distinct recovery PIDs.

The process extension changes test helpers only; binary-group-differences.json and pre-process-extension-test-source
preserve the exact earlier live group. Production sources and affected Client/DomainService/Testing assemblies did not drift; Server/Gateway rebuild outputs are recorded as distinct binary groups.
The PDB checks cover every scoped project C# source with zero mismatches or missing documents.
Diagnostic folders retain the actual initial failures and repeats; diagnostic-failures.json records their assertions.
Generic abandoned streamed-metadata terminal guards, forced Redis transaction limitations, existing optional skips,
and all unclaimed release/consumer/story gates remain explicit in qualification.json.

SHA256SUMS hashes every packet artifact except itself. Packet-local * -text preserves raw bytes across checkout.
The historical qualification-20261020 packet passes its original checksum manifest unchanged.
''')
# Exact scripts become reviewable artifacts, not executable workflow authority.
shutil.copyfile('/tmp/finalize_review1_packet.py',O/'finalize-packet.py')
paths=sorted(p for p in O.rglob('*') if p.is_file() and p.name!='SHA256SUMS')
(O/'SHA256SUMS').write_text(''.join(sha(p)+'  '+str(p.relative_to(O))+'\n' for p in paths))
check=subprocess.run(['sha256sum','--check','SHA256SUMS'],cwd=O,capture_output=True,text=True);assert check.returncode==0,check.stdout+check.stderr
print(json.dumps({'packet':str(O),'source_files':len(inventory),'source_inventory_sha256':sha(O/'source-files.json'),'artifacts':len(paths),'spec_sha256':sha(spec),'packet_manifest_sha256':sha(O/'SHA256SUMS'),'live_outer_passed':36,'child_workers_passed':4},indent=2))

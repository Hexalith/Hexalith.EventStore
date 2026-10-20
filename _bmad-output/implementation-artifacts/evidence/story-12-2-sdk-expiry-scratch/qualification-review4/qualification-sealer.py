from pathlib import Path
import hashlib,json,subprocess,xml.etree.ElementTree as ET,re,shutil,difflib
from datetime import datetime,timezone
root=Path.cwd(); packet=root/'_bmad-output/implementation-artifacts/evidence/story-12-2-sdk-expiry-scratch/qualification-review4'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def save(name,value): (packet/name).write_text(json.dumps(value,indent=2)+'\n')
commands=json.load(open(packet/'commands.json')); results={r['id']:r.get('result',{'build_exit_code':r['exit_code']}) for r in commands}
assert len(commands)==9 and all(r['exit_code']==0 for r in commands)
assert json.load(open(packet/'source-drift.json'))['matches']
assert [results[n]['passed'] for n in ['client-focused','client-full','domain-full','live-expiry-scratch','live-read-model-batch','live-shared-epoch']]==[516,1828,1061,52,6,8]
assert results['client-full']['skipped']==2 and all(results[n]['skipped']==0 for n in ['client-focused','domain-full','live-expiry-scratch','live-read-model-batch','live-shared-epoch'])
projects=['Hexalith.EventStore.Client.Tests','Hexalith.EventStore.DomainService.Tests','Hexalith.EventStore.Server.LiveSidecar.Tests']
pdb_commands=[];pe_commands=[];api=[]
for project in projects:
 folder=root/'tests'/project/'bin/Debug/net10.0';pdbs=[str(p) for p in sorted(folder.glob('Hexalith.*.pdb'))]
 for tool,output,records in [('/tmp/story12-2-review3-pdb-verifier/bin/Debug/net10.0/Verifier.dll','pdb-'+project+'.json',pdb_commands),('/tmp/sdk-r4-pe-verifier/bin/Debug/net10.0/Verifier.dll','pe-'+project+'.json',pe_commands)]:
  cmd=['dotnet',tool]+pdbs;p=subprocess.run(cmd,capture_output=True,text=True);(packet/output).write_text(p.stdout);assert p.returncode==0,p.stderr
  records.append(dict(project=project,command=cmd,exit_code=p.returncode,result=output,sha256=sha(packet/output)))
 xml=folder/'Hexalith.EventStore.Client.xml';members={m.attrib['name'] for m in ET.parse(xml).iter('member')}
 expected=['M:Hexalith.EventStore.Client.Projections.ReadModelBatchOperation.Write``1(System.String,``0,Hexalith.EventStore.Client.Projections.ReadModelBatchConcurrency,System.Nullable{System.TimeSpan})','M:Hexalith.EventStore.Client.Projections.ReadModelBatchOperation.WriteExpiring``1(System.String,``0,Hexalith.EventStore.Client.Projections.ReadModelBatchConcurrency,System.DateTimeOffset)','P:Hexalith.EventStore.Client.Projections.ReadModelBatchOperation.ExpiresAtUtc','P:Hexalith.EventStore.Client.Projections.SharedProjectionMutation.ExpiresAtUtc']
 assert all(m in members for m in expected);api.append(dict(project=project,path=str(xml.relative_to(root)),sha256=sha(xml),verified_members=expected))
save('pdb-commands.json',pdb_commands);save('pe-commands.json',pe_commands);save('public-api-xml-audit.json',api)
for prefix,path in [('pdb','/tmp/story12-2-review3-pdb-verifier'),('pe','/tmp/sdk-r4-pe-verifier')]:
 for file in ['Program.cs','Verifier.csproj']:
  f=Path(path)/file
  if f.exists():(packet/(prefix+'-verifier-'+file+'.txt')).write_bytes(f.read_bytes())
workers=[]
for xml in sorted((packet/'process-workers').glob('*.xml')):
 assembly=ET.parse(xml).getroot().find('assembly');assert assembly.attrib['passed']=='1' and assembly.attrib['failed']=='0' and assembly.attrib['skipped']=='0'
 log=xml.with_suffix('.log');text=log.read_text();pids=set(map(int,re.findall(r'"pid"\s*:\s*(\d+)',text))) or set(map(int,re.findall(r'pid=(\d+)',text)));assert len(pids)==1,(xml,pids)
 workers.append(dict(xml=str(xml.relative_to(packet)),log=str(log.relative_to(packet)),pid=next(iter(pids)),passed=1,skipped=0))
assert len(workers)==20 and len({w['pid'] for w in workers})==20
save('process-workers.json',workers)
all_live=[];raw=[]
for name in ['live-expiry-scratch','live-read-model-batch','live-shared-epoch']:
 for case in ET.parse(packet/(name+'.xml')).iter('test'):
  all_live.append(dict(group=name,name=case.attrib['name'],result=case.attrib['result']))
 for line in (packet/(name+'.log')).read_text().splitlines():
  if any(token in line for token in ['[OUTPUT]','remaining-ttl','review4-','original-process-exited']):raw.append(dict(group=name,observation=line))
assert len(all_live)==66 and all(c['result']=='Pass' for c in all_live);save('executed-live-cases.json',all_live);save('raw-persistence-observations.json',raw)
all_cases=[]
for record in commands:
 if 'xml' not in record:continue
 for case in ET.parse(packet/record['xml']).iter('test'):
  all_cases.append(dict(command=record['id'],name=case.attrib['name'],result=case.attrib['result']))
rows={
 'Source deadline':['DelayedInstallationKeepsRemainingPhysicalTtlAndInclusiveDeadline','ReplacedStoreRecoversOriginalManifestAfterDeadline'],
 'Compatibility':['BuildCanonicalManifest_GoldenVector_IsFrozen','MixedV2ManifestAndFingerprintMatchFixedCanonicalGolden'],
 'Shared generations':['CowSelectedInheritedAndAbandonedPromotedPointerRemainTruthfulAfterPhysicalRedisExpiry'],
 'Scratch retry/abort':['ProductionNamedDispatcherRetriesOrAbortsInterruptedScratchAndDeliversAfterRecovery','PendingNamedOwnerScratchRecoversAfterOwningOsProcessExits'],
 'Fences/races':['RetiredLegacyWorkerPreservesOpenEpochAfterDelayedPhysicalInstallOrCommitCas','RetiredParticipantRecoversAfterLatePhysicalInstallerProcessExits'],
 'Real persistence':['NamedAbortReclaimsLateAcceptedOwnerAfterCleanupFlagsAndAllowsOrdinaryDeliveryAndFreshNamedRebuild','NearLimitLegacyOriginalRecoversWithLargerBoundedPhysicalProof']}
matrix=[]
for row,tokens in rows.items():
 cases=[c for c in all_cases if c['result']=='Pass' and any(token in c['name'] for token in tokens)];assert cases,row
 matrix.append(dict(row=row,executed_passes=cases,status='covered-by-executed-passing-cases'))
save('matrix-audit.json',matrix)

# Remove only inactive fixture namespaces created by this workflow; unrelated live apps are left alone.
owned=set()
for log in packet.rglob('*.log'):
 owned.update(re.findall(r'fixture-app-id=(eventstore-live-[0-9a-f]+)',log.read_text(errors='replace')))
ownership=[]
for log in packet.rglob('*.log'):
 if not log.name.startswith(('live-','sdk-r4-live')):continue
 for actor in sorted(set(re.findall(r'ETag actor warmup:([0-9a-f]{32})',log.read_text(errors='replace')))):
  cmd=['docker','exec','dapr_redis','redis-cli','--raw','--scan','--pattern','*||warmup:'+actor+'||*']
  found=subprocess.run(cmd,capture_output=True,text=True);assert found.returncode==0
  for key in found.stdout.splitlines():
   match=re.match(r'^(eventstore-live-[0-9a-f]{32})[|][|]ETagActor[|][|]warmup:'+actor+r'[|][|]etag$',key)
   assert match,key
   owned.add(match.group(1));ownership.append(dict(app_id=match.group(1),source_log=str(log.relative_to(packet)),warmup_actor=actor,observed_key=key,command=cmd))
save('fixture-namespace-ownership.json',ownership)
active=set()
for p in Path('/proc').iterdir():
 if not p.name.isdigit():continue
 try:args=(p/'cmdline').read_bytes().split(b'\0')
 except OSError:continue
 for i,arg in enumerate(args):
  if arg in [b'--app-id',b'-app-id'] and i+1<len(args):active.add(args[i+1].decode(errors='replace'))
assert not owned.intersection(active),owned.intersection(active)
cleanup=[]
for app in sorted(owned):
 cmd=['docker','exec','dapr_redis','redis-cli','--raw','--scan','--pattern',app+'||*'];r=subprocess.run(cmd,capture_output=True,text=True);assert r.returncode==0,r.stderr
 keys=r.stdout.splitlines()
 for first in range(0,len(keys),128):
  removed=subprocess.run(['docker','exec','dapr_redis','redis-cli','DEL']+keys[first:first+128],capture_output=True,text=True);assert removed.returncode==0,removed.stderr
 remaining=subprocess.run(cmd,capture_output=True,text=True);assert remaining.returncode==0 and not remaining.stdout.strip()
 cleanup.append(dict(app_id=app,sidecar_active=False,removed_keys=len(keys),remaining_keys=0,scan_command=cmd))
save('cleanup-summary.json',dict(namespaces=cleanup,unrelated_apps_stopped=0))
historical=[]
for name in ['qualification-20261020','qualification-review1','qualification-review2','qualification-review3']:
 folder=packet.parent/name;n=0
 for line in (folder/'SHA256SUMS').read_text().splitlines():
  expected,file=line.split(maxsplit=1);assert sha(folder/file.lstrip('*'))==expected;n+=1
 historical.append(dict(packet=name,artifacts=n,mismatches=0))
save('historical-packets-unchanged.json',historical)
# Diff against the positively preserved implementation; include the unchanged-at-preservation cost helper from the owning baseline.
rows=json.load(open('/tmp/story12-2-sdk-review4-keep-111x6kq_/files.json'));before={r['path']:Path(r['snapshot']).read_text() for r in rows if Path(r['repository'])==root}
changed=[];diff=[]
extra_names = [
 'src/Hexalith.EventStore.Client/Projections/ProjectionDeliveryCostDiagnostics.cs',
 'src/Hexalith.EventStore.Client/Projections/ReadModelBatchParticipantProof.cs',
 'src/Hexalith.EventStore.DomainService/DomainSharedProjectionScratchRetirement.cs',
 'tests/Hexalith.EventStore.Client.Tests/Projections/ReadModelReviewFourRegressionTests.cs',
 'tests/Hexalith.EventStore.DomainService.Tests/DomainPartitionedSharedProjectionRebuildTests.ReviewFour.cs',
 'tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Integration/CapturingScratchLiveBatchStore.cs',
 'tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Integration/ProjectionExpiryAndScratchRecoveryLiveTests.ReviewFour.cs',
 'tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Integration/ProjectionRetiredInstallerProcessWorkerTests.cs',
 'tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Integration/RefusedExpiryDeleteLiveAccessor.cs',
 'tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Integration/RetainedDeadlineLiveFaultInjector.cs',
 'tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Integration/ReviewFourLateInstallerAccessor.cs']
for p in [root/name for name in sorted(set(before).union(extra_names))]:
 if {'bin','obj'}.intersection(p.parts):continue
 name=str(p.relative_to(root));current=p.read_text()
 if name in before:old=before[name]
 else:
  git=subprocess.run(['git','show','d4d05f871991ec2af305267f59dcefeae3f127ed:'+name],capture_output=True,text=True)
  if git.returncode==0:old=git.stdout
  else:old=''
 if current!=old:
  changed.append(name);diff.extend(difflib.unified_diff(old.splitlines(True),current.splitlines(True),fromfile='preserved-or-baseline/'+name,tofile='current/'+name))
# The scoped diff compares review-four corrections with the positively preserved implementation.
save('changed-owning-sources.json',[dict(path=n,sha256=sha(root/n)) for n in sorted(changed)])
(packet/'final-source.diff').write_text(''.join(diff))
format_rows=[dict(path=n,has_crlf=b'\r\n' in (root/n).read_bytes()) for n in changed];assert not any(r['has_crlf'] for r in format_rows);save('format-audit.json',format_rows)
check=subprocess.run(['git','diff','--check'],capture_output=True,text=True);assert check.returncode==0
save('git-readonly-state.json',dict(sdk_head=subprocess.check_output(['git','rev-parse','HEAD'],text=True).strip(),workspace_head=subprocess.check_output(['git','-C',str(root.parent.parent),'rev-parse','HEAD'],text=True).strip(),diff_check_exit=check.returncode,status=subprocess.check_output(['git','status','--short'],text=True)))
# Mark only the EventStore owner's qualified tasks; parent adoption and independent review remain open.
spec=root/'_bmad-output/implementation-artifacts/spec-folders-12-2-expiry-and-scratch-recovery.md';text=spec.read_text()
if '\n\n## Review-four qualified SDK handoff\n' in text:text=text.split('\n\n## Review-four qualified SDK handoff\n')[0]
a=text.index('## Tasks & Acceptance');b=text.index('## Review-one implementation corrections',a);text=text[:a]+text[a:b].replace('- [ ]','- [x]')+text[b:]
a=text.index('## Review-four implementation corrections');b=text.index('## Verification',a);text=text[:a]+text[a:b].replace('- [ ]','- [x]')+text[b:]
text+='''\n\n## Review-four qualified SDK handoff\n\nAll EventStore-owned review-four corrections are implemented and source-qualified. Status remains\n**in-progress** for parent independent review and Folders adoption; frozen intent and original baseline\nare unchanged. The additive [qualification packet](evidence/story-12-2-sdk-expiry-scratch/qualification-review4/qualification.json) retains every earlier\npacket and failed/interrupted attempt.\n\n- Final Debug/source builds have zero warnings/errors. Client focused **516/516** and complete\n  **1828 passed, zero failed**, plus the two unchanged optional isolated-endpoint skips (non-acceptance).\n  Complete serialized DomainService is **1061/1061**, zero skips, including both new production named\n  late-owner cases after cleanup flags advance.\n- Real Dapr/Redis expiry/scratch **52/52**, existing batch **6/6** and shared epoch **8/8** pass with\n  zero skipped required cases: **66 distinct live cases**. These include actual conditional-delete\n  refusal, lost delete response with proven physical absence, near-limit legacy recovery, and production\n  named late accepted-owner Abort, ordinary delivery and a fresh named rebuild without test repair calls.\n- All **20 child workers** pass 1/1 with zero skips and 20 distinct PIDs: six production named pending\n  owner/recovery pairs and four primitive late-installer/recovery pairs over their shared fixture state.\n  The primitive Begin/Abort observation is a fresh epoch cycle, separately labelled from named rebuild.\n- Source freezes, every executed command/XML and runtime binary group, portable PDB document hashes,\n  PE/PDB CodeView identities and generated API XML are audited. Original v1/v2 golden identities and API\n  compile/default calls remain covered. Changed owning C#/Markdown use LF. No source drift is accepted.\n- Earlier proof/anchor, malformed metadata, scope/CAS, COW/journal expiry, legacy provider ABA, retry/Abort,\n  ordinary delivery, named rebuild and unsupported forced-provider transaction controls remain passing.\n  Production Redis remains Resumable; forced TransactionQualified partial writes/refusal are not atomicity\n  or rollback qualification. Fixture-owned namespaces are removed only after their sidecars exit, with\n  zero keys remaining and no unrelated application stopped.\n- Terminal fences, participant proofs, exact-scope indexes, original legacy Aborted markers and bounded\n  accepted-owner inventories intentionally remain operational metadata until an owner-authorized purge\n  meets the documented drained-writer, retention/legal-hold and restore prerequisites. Repeated named\n  Abort is the production trigger for late scratch reconciliation after cleanup flags are terminal.\n\nNo Folders consumer file, Git history/index, dependency/package pin, event, publication or deployment\nwas changed. Parent independent review, consumer clock/adoption qualification, package/PD10 and the\nseparate >10000 identity and performance work remain outside this SDK handoff.\n'''
spec.write_text(text);(packet/'owner-spec-at-handoff.md').write_text(text)
summary=dict(schema_version=1,status='ready-for-parent-independent-review',sealed_utc=datetime.now(timezone.utc).isoformat(),scope='Approved source expiry and interrupted private scratch recovery, all review-four SDK corrections',original_sdk_baseline='d4d05f871991ec2af305267f59dcefeae3f127ed',effective_sdk_head=subprocess.check_output(['git','rev-parse','HEAD'],text=True).strip(),results=results,source_inputs=len(json.load(open(packet/'source-freeze.json'))),unique_live_cases=66,worker_invocations=20,distinct_worker_pids=20,exited_worker_pairs=10,production_named_pairs=6,primitive_late_installer_pairs=4,commands='commands.json',source_freeze='source-freeze.json',source_final='source-final.json',source_drift='source-drift.json',pdb='pdb-commands.json',pe='pe-commands.json',api='public-api-xml-audit.json',workers='process-workers.json',raw_persistence='raw-persistence-observations.json',cleanup='cleanup-summary.json',historical='historical-packets-unchanged.json',spec=dict(path=str(spec.relative_to(root)),sha256=sha(spec),snapshot='owner-spec-at-handoff.md',status='in-progress'),notes=['Two existing optional Client skips are not acceptance. Every required live/child case executed without skipping.','Production Redis remains Resumable; forced transactions are unsupported diagnostic evidence.','The first passing pre-cleanup source group and all failed/interrupted diagnostic groups are retained separately.','Persistent payload-free tenant metadata and repeated named Abort recovery trigger are documented; OffboardTenantAsync does not enumerate these records.','Parent Folders adoption/clock qualification, independent review, package/PD10, >10000 identities and performance remain open.'])
save('qualification.json',summary)
shutil.copy2('/tmp/sdk-r4-qualify.py',packet/'qualification-runner.py');shutil.copy2('/tmp/sdk-r4-seal.py',packet/'qualification-sealer.py')
# Bind every artifact after the final report is written.
files=sorted(f for f in packet.rglob('*') if f.is_file() and f.name!='SHA256SUMS');(packet/'SHA256SUMS').write_text(''.join(sha(f)+'  '+str(f.relative_to(packet))+'\n' for f in files))
print(json.dumps(dict(status=summary['status'],artifacts=len(files),results=results,worker_invocations=20,unique_live_cases=66),indent=2))

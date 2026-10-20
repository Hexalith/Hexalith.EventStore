from pathlib import Path
import hashlib,json,subprocess,xml.etree.ElementTree as ET,re,shutil,difflib,os
from datetime import datetime,timezone
root=Path.cwd();packet=root/'_bmad-output/implementation-artifacts/evidence/story-12-2-sdk-expiry-scratch/qualification-review5'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def save(name,value): (packet/name).write_text(json.dumps(value,indent=2)+'\n')
commands=json.loads((packet/'commands.json').read_text());assert len(commands)==9 and all(r['exit_code']==0 for r in commands)
results={r['id']:r.get('result',{'build_exit_code':r['exit_code']}) for r in commands}
assert results['client-full']['skipped']==2
for name in ['client-focused','domain-full','live-expiry-scratch','live-read-model-batch','live-shared-epoch']:
 assert results[name]['passed']>0 and results[name]['skipped']==results[name]['failed']==results[name]['errors']==0,(name,results[name])
assert json.loads((packet/'source-drift.json').read_text())['matches']
# Preserve every exploratory/failed/zero-selection command as diagnostic history, never acceptance.
for f in Path('/tmp').glob('story12-2-review5-*'):
 if f.is_file() and f.suffix in ['.log','.xml']:shutil.copy2(f,packet/'diagnostics'/f.name)
save('diagnostic-disposition.json',{'acceptance':'Only the commands.json frozen final-source group qualifies acceptance.','first-frozen-attempt':'diagnostics/first-frozen-attempt retains543 passing and3 failing focused cases and its exact source/binary group.','earlier-probes':'Earlier fixture construction/assertion failures and the6-of7 live gate remain failures. The corrected live terminal stop explicitly asserts its injector exception.','zero-selection':'The initial mixed class/method-filter command selected zero tests; it is not acceptance. Final focused/full runs include every required regression.','unfrozen-exploration':'Earlier exploratory runs without source freezes are diagnostics only; exact source provenance is not claimed for them.'})
versions=[]
for cmd in [['dotnet','--info'],['dapr','--version'],['docker','version','--format','{{.Client.Version}} / {{.Server.Version}}']]:
 r=subprocess.run(cmd,capture_output=True,text=True);versions.append({'command':cmd,'exit_code':r.returncode,'output':r.stdout+r.stderr})
save('configured-tool-versions.json',versions)
projects=['Hexalith.EventStore.Client.Tests','Hexalith.EventStore.DomainService.Tests','Hexalith.EventStore.Server.LiveSidecar.Tests']
pdb_commands=[];pe_commands=[];api=[]
for project in projects:
 folder=root/'tests'/project/'bin/Debug/net10.0';pdbs=[str(p) for p in sorted(folder.glob('Hexalith.*.pdb'))]
 for tool,output,records in [('/tmp/story12-2-review3-pdb-verifier/bin/Debug/net10.0/Verifier.dll','pdb-'+project+'.json',pdb_commands),('/tmp/sdk-r4-pe-verifier/bin/Debug/net10.0/Verifier.dll','pe-'+project+'.json',pe_commands)]:
  cmd=['dotnet',tool]+pdbs;r=subprocess.run(cmd,capture_output=True,text=True);(packet/output).write_text(r.stdout);assert r.returncode==0,r.stderr
  records.append({'project':project,'command':cmd,'exit_code':r.returncode,'result':output,'sha256':sha(packet/output),'verifier_sha256':sha(Path(tool))})
 xml=folder/'Hexalith.EventStore.Client.xml';members={m.attrib['name'] for m in ET.parse(xml).iter('member')}
 expected=['M:Hexalith.EventStore.Client.Projections.ReadModelBatchOperation.Write``1(System.String,``0,Hexalith.EventStore.Client.Projections.ReadModelBatchConcurrency,System.Nullable{System.TimeSpan})','M:Hexalith.EventStore.Client.Projections.ReadModelBatchOperation.WriteExpiring``1(System.String,``0,Hexalith.EventStore.Client.Projections.ReadModelBatchConcurrency,System.DateTimeOffset)','P:Hexalith.EventStore.Client.Projections.ReadModelBatchOperation.ExpiresAtUtc','P:Hexalith.EventStore.Client.Projections.SharedProjectionMutation.ExpiresAtUtc']
 assert all(m in members for m in expected);api.append({'project':project,'path':str(xml.relative_to(root)),'sha256':sha(xml),'verified_members':expected})
save('pdb-commands.json',pdb_commands);save('pe-commands.json',pe_commands);save('public-api-xml-audit.json',api)
for prefix,path in [('pdb','/tmp/story12-2-review3-pdb-verifier'),('pe','/tmp/sdk-r4-pe-verifier')]:
 for file in ['Program.cs','Verifier.csproj']:
  f=Path(path)/file
  if f.exists():(packet/(prefix+'-verifier-'+file+'.txt')).write_bytes(f.read_bytes())
# Verify each recorded runtime graph has retained exactly its accepted binary bytes.
binary_checks=[]
for record in commands:
 if 'binary_group' not in record:continue
 rows=json.loads((packet/record['binary_group']).read_text());mismatches=[r['path'] for r in rows if sha(root/r['path'])!=r['sha256']]
 assert not mismatches,(record['id'],mismatches)
 binary_checks.append({'command':record['id'],'files':len(rows),'mismatches':mismatches})
save('final-binary-drift.json',binary_checks)
workers=[]
for xml in sorted((packet/'process-workers').glob('*.xml')):
 assembly=ET.parse(xml).getroot().find('assembly');assert assembly.attrib['passed']=='1' and assembly.attrib['failed']=='0' and assembly.attrib['skipped']=='0'
 log=xml.with_suffix('.log');body=log.read_text();pids=set(map(int,re.findall(r'"pid"\s*:\s*(\d+)',body))) or set(map(int,re.findall(r'pid=(\d+)',body)));assert len(pids)==1,(xml,pids)
 workers.append({'xml':str(xml.relative_to(packet)),'log':str(log.relative_to(packet)),'pid':next(iter(pids)),'passed':1,'skipped':0})
assert len(workers)==20 and len({w['pid'] for w in workers})==20
save('process-workers.json',workers)
pair_groups={}
for worker in workers:
 name=Path(worker['xml']).name
 match=re.fullmatch(r'(scratch-process-[0-9a-f]+)-(persist|resume)\.xml',name)
 if match:prefix,phase=match.groups();kind='production-named'
 else:
  match=re.fullmatch(r'(review4-exited-[0-9a-f]+)\.(install|recover)\.xml',name);assert match,name
  prefix,phase=match.groups();kind='primitive-late-installer'
 pair_groups.setdefault((prefix,kind),{})[phase]=worker
pairs=[]
for (prefix,kind),phases in sorted(pair_groups.items()):
 first,second=('persist','resume') if kind=='production-named' else ('install','recover')
 assert set(phases)=={first,second}
 original=phases[first];recovery=phases[second]
 original_xml=ET.parse(packet/original['xml']).getroot().find('assembly');recovery_xml=ET.parse(packet/recovery['xml']).getroot().find('assembly')
 assert original['pid']!=recovery['pid']
 assert original_xml.attrib['finish-rtf']<recovery_xml.attrib['start-rtf']
 pairs.append({'id':prefix,'kind':kind,'original_pid':original['pid'],'recovery_pid':recovery['pid'],'original_xml':original['xml'],'recovery_xml':recovery['xml'],'original_finished_utc':original_xml.attrib['finish-rtf'],'recovery_started_utc':recovery_xml.attrib['start-rtf'],'distinct_exited_worker_order':True})
assert sum(p['kind']=='production-named' for p in pairs)==6 and sum(p['kind']=='primitive-late-installer' for p in pairs)==4
save('process-pairs.json',pairs)
all_live=[];raw=[];all_cases=[]
for record in commands:
 if 'xml' not in record:continue
 for case in ET.parse(packet/record['xml']).iter('test'):
  row={'command':record['id'],'name':case.attrib['name'],'result':case.attrib['result']};all_cases.append(row)
  if record['id'].startswith('live-'):all_live.append(row)
 if record['id'].startswith('live-'):
  for line in (packet/record['log']).read_text().splitlines():
   if any(token in line for token in ['[OUTPUT]','remaining-ttl','review5-','review4-','original-process-exited']):raw.append({'group':record['id'],'observation':line})
assert len({r['name'] for r in all_live})==len(all_live) and all(r['result']=='Pass' for r in all_live)
save('executed-live-cases.json',all_live);save('raw-persistence-observations.json',raw)
rows={
 'Source deadline':['DelayedInstallationKeepsRemainingPhysicalTtlAndInclusiveDeadline','ReplacedStoreRecoversOriginalManifestAfterDeadline'],
 'Compatibility':['BuildCanonicalManifest_GoldenVector_IsFrozen','MixedV2ManifestAndFingerprintMatchFixedCanonicalGolden'],
 'Shared generations':['CowSelectedInheritedAndAbandonedPromotedPointerRemainTruthfulAfterPhysicalRedisExpiry'],
 'Scratch retry/abort':['ProductionNamedDispatcherRetriesOrAbortsInterruptedScratchAndDeliversAfterRecovery','PendingNamedOwnerScratchRecoversAfterOwningOsProcessExits'],
 'Fences/races':['RetiredLegacyWorkerPreservesOpenEpochAfterDelayedPhysicalInstallOrCommitCas','RetiredParticipantRecoversAfterLatePhysicalInstallerProcessExits'],
 'Real persistence':['TerminalAcceptedStreamedTransportAndHashPageLateInstallers','MaximumEscapedLegacyScopeRecoversOverRedis'],
 'R5-E1 retained competing-owner retry':['CompetingOwnerFenceChangesRetireEachOriginalAttempt','NamedMultiOwnerAccumulateRetriesRetainedFenceConflict','CompetingRetainedNamedOwnerRetriesOverRedis'],
 'R5-B1 bounded legal escaping':['MaximumEscapedLegacyScopeAndKeyRecover','MaximumEscapedTerminalAnchorRetainsOriginalControlBudget','MaximumEscapedTerminalAnchorRecoversCompletedAndInterruptedReceiptOverRedis'],
 'R5-B2 pre-write scope validation':['TerminalLogicalDiscoveryValidatesCompleteOriginalScopeBeforeAnyAuxiliaryWrite'],
 'R5-B3 typed participant proof':['TerminalAnchorRejectsMalformedParticipantConcurrencyBeforeAnyMutation'],
 'R5-B4 canonical compensation key':['TerminalLegacyCompensationRejectsNoncanonicalPointer'],
 'R5-B7 unavailable inventory refusal':['TerminalNamedAbortRefusesUnavailableOrCorruptOriginalOwnerInventory','MissingHistoricalOrCorruptRetiredOwnerInventoryRefusesNamedSuccessOverRedis'],
 'R5-V1 accepted late transport':['TerminalAcceptedStreamedOwnerReconcilesLateFragmentPartAndHashPage','TerminalAcceptedStreamedTransportAndHashPageLateInstallers'],
 'R5-B10 bounded diagnostics and gate':['StalledFirstMaintenanceReadUsesSdkBudget','MaintenanceBudgetSetupFailureReleasesGate']}
matrix=[]
for row,tokens in rows.items():
 cases=[c for c in all_cases if c['result']=='Pass' and any(t in c['name'] for t in tokens)];assert cases,row
 matrix.append({'row':row,'executed_passes':cases,'status':'covered-by-executed-passing-cases'})
save('matrix-audit.json',matrix)
# Only exited workflow-owned fixture namespaces may be purged; no unrelated app is stopped.
owned=set();ownership=[]
for log in packet.rglob('*.log'):owned.update(re.findall(r'fixture-app-id=(eventstore-live-[0-9a-f]+)',log.read_text(errors='replace')))
for log in packet.rglob('*.log'):
 if not log.name.startswith(('live-','story12-2-review5-live')):continue
 for actor in sorted(set(re.findall(r'ETag actor warmup:([0-9a-f]{32})',log.read_text(errors='replace')))):
  cmd=['docker','exec','dapr_redis','redis-cli','--raw','--scan','--pattern','*||warmup:'+actor+'||*'];found=subprocess.run(cmd,capture_output=True,text=True);assert found.returncode==0
  for key in found.stdout.splitlines():
   match=re.match(r'^(eventstore-live-[0-9a-f]{32})[|][|]ETagActor[|][|]warmup:'+actor+r'[|][|]etag$',key);assert match,key
   owned.add(match.group(1));ownership.append({'app_id':match.group(1),'source_log':str(log.relative_to(packet)),'warmup_actor':actor,'observed_key':key,'command':cmd})
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
 cmd=['docker','exec','dapr_redis','redis-cli','--raw','--scan','--pattern',app+'||*'];r=subprocess.run(cmd,capture_output=True,text=True);assert r.returncode==0,r.stderr;keys=r.stdout.splitlines()
 for first in range(0,len(keys),128):
  removed=subprocess.run(['docker','exec','dapr_redis','redis-cli','DEL']+keys[first:first+128],capture_output=True,text=True);assert removed.returncode==0,removed.stderr
 remaining=subprocess.run(cmd,capture_output=True,text=True);assert remaining.returncode==0 and not remaining.stdout.strip()
 cleanup.append({'app_id':app,'sidecar_active':False,'removed_keys':len(keys),'remaining_keys':0,'scan_command':cmd})
save('cleanup-summary.json',{'namespaces':cleanup,'unrelated_apps_stopped':0})
historical=[]
for name in ['qualification-20261020','qualification-review1','qualification-review2','qualification-review3','qualification-review4']:
 folder=packet.parent/name;n=0
 for line in (folder/'SHA256SUMS').read_text().splitlines():
  expected,file=line.split(maxsplit=1);assert sha(folder/file.lstrip('*'))==expected;n+=1
 historical.append({'packet':name,'artifacts':n,'mismatches':0})
save('historical-packets-unchanged.json',historical)
preserved=json.loads(Path('/tmp/story12-2-sdk-review5-keep-79zyj2r0/files.json').read_text());before={r['path']:Path(r['snapshot']).read_text() for r in preserved if Path(r['repository'])==root}
names=set(before)
for name in subprocess.check_output(['git','diff','--name-only'],text=True).splitlines()+subprocess.check_output(['git','ls-files','--others','--exclude-standard'],text=True).splitlines():
 if name.startswith(('src/','tests/','docs/')) and Path(name).suffix in ('.cs','.md'):names.add(name)
changed=[];diff=[]
for name in sorted(names):
 p=root/name
 if not p.exists():continue
 current=p.read_text()
 if name in before:old=before[name]
 else:
  r=subprocess.run(['git','show','HEAD:'+name],capture_output=True,text=True);old=r.stdout if r.returncode==0 else ''
 if current!=old:
  changed.append(name);diff.extend(difflib.unified_diff(old.splitlines(True),current.splitlines(True),fromfile='preserved-or-current-head/'+name,tofile='review5/'+name))
save('changed-owning-sources.json',[{'path':n,'sha256':sha(root/n)} for n in changed]);(packet/'final-source.diff').write_text(''.join(diff))
format_rows=[{'path':n,'has_crlf':b'\r\n' in (root/n).read_bytes()} for n in changed];assert not any(r['has_crlf'] for r in format_rows);save('format-audit.json',format_rows)
check=subprocess.run(['git','diff','--check'],capture_output=True,text=True);assert check.returncode==0
save('git-readonly-state.json',{'sdk_head':subprocess.check_output(['git','rev-parse','HEAD'],text=True).strip(),'workspace_head':subprocess.check_output(['git','-C',str(root.parent.parent),'rev-parse','HEAD'],text=True).strip(),'diff_check_exit':check.returncode,'status':subprocess.check_output(['git','status','--short'],text=True)})
# Preserve frozen intent and historical records; mark only the qualified owner tasks.
spec=root/'_bmad-output/implementation-artifacts/spec-folders-12-2-expiry-and-scratch-recovery.md';body=spec.read_text()
a=body.index('## Tasks & Acceptance');b=body.index('## Review-one implementation corrections',a);body=body[:a]+body[a:b].replace('- [ ]','- [x]')+body[b:]
a=body.index('## Review-five implementation corrections');b=body.index('## Verification',a);body=body[:a]+body[a:b].replace('- [ ]','- [x]')+body[b:]
body+='''\n\n## Review-five qualified SDK handoff\n\nAll review-five SDK corrections, including the parent terminal-receipt capacity and maintenance-setup\naudit follow-through, are implemented and qualified against current merged source. Status remains\n**in-progress** for parent independent review and Folders consumption. Original baseline and frozen\nintent are unchanged. The additive [qualification packet](evidence/story-12-2-sdk-expiry-scratch/qualification-review5/qualification.json)\npreserves every historical packet and every failed or non-qualifying attempt.\n\n'''
body+=f'''- Current SDK 10.0.402 Debug/source builds pass with zero warnings/errors. Focused Client\n  **{results['client-focused']['passed']}/{results['client-focused']['total']}**, complete Client **{results['client-full']['passed']} passed**, zero failed and the two unchanged optional\n  skips (not acceptance); serialized complete DomainService **{results['domain-full']['passed']}/{results['domain-full']['total']}**, zero skips.\n- Real Dapr/Redis expiry/scratch **{results['live-expiry-scratch']['passed']}/{results['live-expiry-scratch']['total']}**, batch **{results['live-read-model-batch']['passed']}/{results['live-read-model-batch']['total']}**, and shared epoch\n  **{results['live-shared-epoch']['passed']}/{results['live-shared-epoch']['total']}** pass: **{len(all_live)} distinct live cases**, no required skips. New live cases cover competing\n  named owners and retired original attempts, late accepted streamed fragments/parts/hash pages after\n  terminal flags, unavailable/corrupt inventory refusal, maximal legacy escaping and complete/interrupted\n  terminal anchors above 16 KiB. Physical private absence, original TTL/deadline and exact Open/session\n  preservation are asserted; named recovery is followed by ordinary growth and a fresh named rebuild.\n- All **{len(workers)} exited-worker invocations** pass 1/1 without skips, with **{len({w['pid'] for w in workers})} distinct PIDs**: six\n  production named pending/recovery pairs and four separately labelled primitive late-installer controls.\n  Source freezes, runtime binary groups, PE/PDB identities, PDB source hashes, public API XML and matrix\n  coverage are audited. All fixture namespaces are inactive before ownership-scoped final cleanup and\n  report zero remaining keys; no unrelated application was stopped.\n- The first frozen focused attempt (543 pass/3 fail), earlier fixture/assertion failures and zero-case\n  filter are preserved as diagnostics. Corrected final-source complete suites are the acceptance group;\n  no failed/skipped/unfrozen attempt is promoted to qualification.\n\n'''
body+='''Retained terminal/per-attempt metadata follows the documented tenant retention, legal-hold, ordered\nrestore and drained-writer purge prerequisites. Production Redis remains Resumable; forced provider\ntransaction controls do not qualify atomicity. No Folders file, Git/index/history, dependency/global/pin,\npublication, deployment or retained event was changed. Parent consumption/review, package/PD10, the\nseparate >10000 identity work and performance/whole-story gates remain open.\n'''
spec.write_text(body);(packet/'owner-spec-at-handoff.md').write_text(body)
summary={'schema_version':1,'status':'ready-for-parent-independent-review','sealed_utc':datetime.now(timezone.utc).isoformat(),'scope':'Approved source expiry and interrupted scratch recovery; all review-five SDK corrections and in-scope parent acceptance audits','original_sdk_baseline':'d4d05f871991ec2af305267f59dcefeae3f127ed','effective_sdk_head':subprocess.check_output(['git','rev-parse','HEAD'],text=True).strip(),'results':results,'source_inputs':len(json.loads((packet/'source-freeze.json').read_text())),'unique_live_cases':len(all_live),'worker_invocations':len(workers),'distinct_worker_pids':len({w['pid'] for w in workers}),'exited_worker_pairs':10,'production_named_pairs':6,'primitive_late_installer_pairs':4,'commands':'commands.json','source_freeze':'source-freeze.json','source_final':'source-final.json','source_drift':'source-drift.json','pdb':'pdb-commands.json','pe':'pe-commands.json','api':'public-api-xml-audit.json','workers':'process-workers.json','raw_persistence':'raw-persistence-observations.json','matrix':'matrix-audit.json','cleanup':'cleanup-summary.json','historical':'historical-packets-unchanged.json','diagnostics':'diagnostic-disposition.json','spec':{'path':str(spec.relative_to(root)),'sha256':sha(spec),'snapshot':'owner-spec-at-handoff.md','status':'in-progress'},'notes':['Two unchanged optional Client skips are not acceptance; all required live and worker cases execute with zero skips.','Production Redis remains Resumable; forced transactions remain unsupported diagnostic controls.','All old sealed packets and failed/non-qualifying attempts remain preserved.','Folders adoption, parent independent review, package/PD10, >10000 identities and performance/whole-story gates remain open.']}
save('qualification.json',summary)
files=sorted(f for f in packet.rglob('*') if f.is_file() and f.name!='SHA256SUMS');(packet/'SHA256SUMS').write_text(''.join(sha(f)+'  '+str(f.relative_to(packet))+'\n' for f in files))
print(json.dumps({'status':summary['status'],'artifacts':len(files),'results':results,'worker_invocations':len(workers),'unique_live_cases':len(all_live)},indent=2))

from pathlib import Path
import json,hashlib,datetime,subprocess,xml.etree.ElementTree as E,re,shutil
ROOT=Path('/home/administrator/projects/hexalith/folders/references/Hexalith.EventStore')
OUT=ROOT/'_bmad-output/implementation-artifacts/evidence/story-12-2-sdk-expiry-scratch/qualification-review2'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def dump(n,x):(OUT/n).write_text(json.dumps(x,indent=2)+'\n')
commands=json.loads((OUT/'commands.json').read_text());by={x['id']:x for x in commands}
assert by['client-full']['result']=={'total':1758,'passed':1756,'failed':0,'skipped':2,'errors':0,'not-run':0}
assert by['domain-full']['result']['failed']==1 and by['domain-full-serial']['result']['passed']==1057
assert by['live-expiry-scratch']['result']['passed']==34 and by['live-expiry-scratch']['result']['failed']==3
for n,total in [('client-focused',215),('domain-focused',62),('domain-full-serial',1057),('live-cow-process-final',9),('live-read-model-batch',6),('live-shared-epoch',8)]:
 assert by[n]['result']['passed']==total and by[n]['result']['failed']==0 and by[n]['result']['skipped']==0
assert json.loads((OUT/'source-drift.json').read_text())['unchanged']
for p in OUT.glob('pdb-*.json'):
 if p.name in ('pdb-commands.json',):continue
 data=json.loads(p.read_text())
 if isinstance(data,list):
  for row in data:
   if isinstance(row,dict) and 'checked_sources' in row:assert row['mismatches']==0 and not row['missing_sources']
observations=[];executed=[]
for record in commands:
 if 'xml' not in record:continue
 for t in E.parse(OUT/record['xml']).getroot().findall('.//test'):
  executed.append(dict(run=record['id'],name=t.get('name'),result=t.get('result'),time=t.get('time')))
  output=t.findtext('output')
  if output and record['id'].startswith('live-'):
   observations.append(dict(run=record['id'],name=t.get('name'),result=t.get('result'),output=output))
dump('executed-cases.json',executed);dump('raw-persisted-observations.json',observations)
cleanups=[]
for p in sorted(OUT.glob('live-*-cleanup.json')):
 data=json.loads(p.read_text());assert data['remaining_keys']==0 and data['sidecars_stopped'];cleanups.append(dict(artifact=p.name,**data))
dump('cleanup-summary.json',cleanups)
files={}
for p in sorted(OUT.glob('*-binary-group.json')):
 for x in json.loads(p.read_text()):files.setdefault(Path(x['path']).name,{}).setdefault(x['sha256'],[]).append(p.name)
dump('binary-group-crosswalk.json',{name:[dict(sha256=h,groups=g) for h,g in versions.items()] for name,versions in sorted(files.items())})
repo_states={}
for repo in (ROOT,ROOT.parent.parent):
 repo_states[str(repo)]={cmd:subprocess.run(['git',*cmd.split()],cwd=repo,text=True,capture_output=True).stdout for cmd in ('rev-parse HEAD','branch --show-current','status --short','remote -v','log -3 --oneline')}
repo_states['root_recorded_sdk_gitlink']=subprocess.run(['git','ls-tree','HEAD','references/Hexalith.EventStore'],cwd=ROOT.parent.parent,text=True,capture_output=True).stdout
dump('final-repository-state.json',repo_states)
check=subprocess.run(['git','diff','--check'],cwd=ROOT,capture_output=True);(OUT/'diff-check.log').write_bytes(check.stdout+check.stderr);assert check.returncode==0
spec=ROOT/'_bmad-output/implementation-artifacts/spec-folders-12-2-expiry-and-scratch-recovery.md';shutil.copyfile(spec,OUT/'owner-spec-at-handoff.md')
for p in (Path('/tmp/eventstore-review2-pdb-verifier-build.log'),):
 if p.exists():shutil.copyfile(p,OUT/p.name)
qualified={'client_focused':215,'client_full':{'passed':1756,'failed':0,'optional_skipped':2},'domain_focused':62,'domain_full_default_parallel':{'passed':1056,'failed':1,'skipped':0,'artifact':'domain-full.xml'},'domain_full_serialized_same_binary':{'passed':1057,'failed':0,'skipped':0,'artifact':'domain-full-serial.xml'},'expiry_scratch_live_union':{'unique_passed':37,'first_attempt':{'passed':34,'failed':3,'skipped':0},'affected_final_rerun':{'passed':9,'failed':0,'skipped':0},'single_all_37_case_pass_claim':False},'existing_batch_live':6,'existing_epoch_live':8,'unique_live_cases_qualified':51,'final_child_workers':{'passed':12,'failed':0,'skipped':0,'owning_recovery_pairs':6}}
qualification={'schema_version':1,'status':'ready-for-parent-independent-review','sealed_utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'scope':'Approved EventStore source-deadline and interrupted scratch recovery prerequisites; review-two corrections','original_sdk_baseline':'d4d05f871991ec2af305267f59dcefeae3f127ed','effective_sdk_head':repo_states[str(ROOT)]['rev-parse HEAD'].strip(),'effective_workspace_head':repo_states[str(ROOT.parent.parent)]['rev-parse HEAD'].strip(),'spec':{'path':str(spec.relative_to(ROOT)),'sha256':sha(spec),'snapshot':'owner-spec-at-handoff.md','status':'in-progress'},'results':qualified,'commands':'commands.json','source_groups':{'client_domain_and_initial_live':'source-freeze.json','final_live_test_only':'source-freeze-final-live.json','final':'source-final.json','bounded_delta':'test-only-live-source-delta.json','file_count':len(json.loads((OUT/'source-final.json').read_text()))},'positive_reuse':'positive-snapshot-reuse.json','binary_groups':'binary-group-crosswalk.json','pdb_checks':'pdb-commands.json','test_matrix':'executed-cases.json','live_case_union':'live-expiry-case-union.json','raw_persistence':'raw-persisted-observations.json','worker_raw_state_and_commands':'final-process-workers.json','distinct_pids':'final-process-pairs.json','cleanup':'cleanup-summary.json','historical_packets':'historical-packets-unchanged.json','parent_historical_audit':'parent-historical-packet-audit.json','replacement_scope':'New clients/stores/coordinators and six distinct exited-owner/recovery OS-process pairs over the same fixture sidecar/state; no sidecar restart claim.','notes':['The failed parallel full Domain run is retained; isolated pass is diagnostic only, and complete serialized same-binary run passes all 1057. Fixture timeout and renewal coincide at ten seconds; scheduling cause is an inference from inspected code.','First full expiry/scratch live attempt is retained at34/37. Exact unavailable-exception assertion and raw process evidence are test-only changes; only affected COW/process cases were rerun. Case union qualifies37 unique cases, with no all37 single-pass claim.','Two optional Client skips are not acceptance evidence. Required selected live and final child runs have zero skips.','Distinct dependency build-graph binaries and both Live test binary groups are retained rather than equated. Client and Domain runtime source/binaries remain stable across final test-only evidence changes.','Production Redis remains Resumable. Forced TransactionQualified diagnostics prove partial provider writes and SDK completion refusal, not atomicity/rollback.','No Git mutation, Folders edit, dependency/package pin change, publication or deployment was performed by this phase. Parent review and Folders adoption remain required.']}
dump('qualification.json',qualification)
print(json.dumps(qualified,indent=2))

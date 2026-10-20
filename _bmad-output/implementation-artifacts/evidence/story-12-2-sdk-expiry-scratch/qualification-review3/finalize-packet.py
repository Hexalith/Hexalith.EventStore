from pathlib import Path
import base64,datetime,hashlib,json,re,shutil,subprocess,xml.etree.ElementTree as E
ROOT=Path('/home/administrator/projects/hexalith/folders/references/Hexalith.EventStore')
OUT=ROOT/'_bmad-output/implementation-artifacts/evidence/story-12-2-sdk-expiry-scratch/qualification-review3'
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def dump(name,value): (OUT/name).write_text(json.dumps(value,indent=2)+'\n')
commands=json.loads((OUT/'commands.json').read_text());by={row['id']:row for row in commands}
for name,count in [('client-focused',294),('domain-focused',64),('domain-full',1059),('live-review3',5),('live-expiry-scratch',42),('live-read-model-batch',6),('live-shared-epoch',8)]:
 assert by[name]['exit_code']==0 and by[name]['result']['passed']==count and by[name]['result']['failed']==0 and by[name]['result']['skipped']==0,(name,by[name])
assert by['client-full']['result']==dict(total=1816,passed=1814,failed=0,skipped=2,errors=0,**{'not-run':0})
assert all(row['exit_code']==0 for row in commands)
builds=[]
for row in commands:
 assert sha(OUT/row['log'])==row['log_sha256']
 if 'xml' in row:assert sha(OUT/row['xml'])==row['xml_sha256']
 if 'binary_group' in row:
  assert all(sha(ROOT/item['path'])==item['sha256'] for item in json.loads((OUT/row['binary_group']).read_text())),row['id']
 if row['id'].endswith('-build'):
  log=(OUT/row['log']).read_text()
  warnings=re.findall(r'(\d+) Warning\(s\)',log);errors=re.findall(r'(\d+) Error\(s\)',log)
  assert 'Build succeeded.' in log and warnings and errors and int(warnings[-1])==int(errors[-1])==0,row['id']
  builds.append(dict(command=row['id'],exit_code=0,warnings=0,errors=0,log=row['log']))
dump('build-summary.json',builds)
assert json.loads((OUT/'source-drift.json').read_text())['unchanged']
final_source=json.loads((OUT/'source-final.json').read_text())
assert final_source==json.loads((OUT/'source-freeze.json').read_text())
assert all(sha(ROOT/row['path'])==row['sha256'] for row in final_source)
reuse=json.loads((OUT/'source-group-reuse.json').read_text())
prior={row['path']:row for row in json.loads((OUT/reuse['client_domain_source']).read_text())}
current={row['path']:row for row in final_source}
changed=[path for path in sorted(set(prior)|set(current)) if prior.get(path)!=current.get(path)]
assert changed==reuse['changed_paths']==['tests/Hexalith.EventStore.Server.LiveSidecar.Tests/Integration/ProjectionExpiryAndScratchRecoveryLiveTests.cs']
assert all(prior[path]==row for path,row in current.items() if path not in changed)
dump('accepted-command-source-groups.json',[dict(command=row['id'],binary_group=row.get('binary_group'),source_freeze=reuse['client_domain_source'] if row['id'].startswith(('client-','domain-')) else 'source-freeze.json',all_current_owning_project_inputs_unchanged=True) for row in commands])
records=[]
for name in ['qualification-20261020','qualification-review1','qualification-review2']:
 packet=OUT.parent/name;checked=[]
 for line in (packet/'SHA256SUMS').read_text().splitlines():
  h,path=line.split('  ',1);p=packet/path
  assert sha(p)==h,(name,path)
  checked.append(dict(path=path,sha256=h))
 records.append(dict(packet=name,manifest_sha256=sha(packet/'SHA256SUMS'),verified_files=len(checked),files=checked))
dump('historical-packets-unchanged.json',records)
pdb=[]
for project in ['Hexalith.EventStore.Client.Tests','Hexalith.EventStore.DomainService.Tests','Hexalith.EventStore.Server.LiveSidecar.Tests']:
 paths=sorted((ROOT/'tests'/project/'bin/Debug/net10.0').glob('Hexalith.EventStore*.pdb'))
 command=['dotnet','/tmp/story12-2-review3-pdb-verifier/bin/Debug/net10.0/Verifier.dll',*[str(path) for path in paths]]
 result=subprocess.run(command,capture_output=True)
 name='pdb-'+project+'.json';(OUT/name).write_bytes(result.stdout);(OUT/(name+'.stderr.log')).write_bytes(result.stderr)
 assert result.returncode==0,project
 data=json.loads(result.stdout)
 assert all(row['mismatches']==0 and not row['missing_sources'] for row in data)
 pdb.append(dict(project=project,command=command,exit_code=result.returncode,result=name,sha256=sha(OUT/name)))
dump('pdb-commands.json',pdb)
api=[]
required=[
 'M:Hexalith.EventStore.Client.Projections.ReadModelBatchOperation.Write``1(System.String,``0,Hexalith.EventStore.Client.Projections.ReadModelBatchConcurrency,System.Nullable{System.TimeSpan})',
 'M:Hexalith.EventStore.Client.Projections.ReadModelBatchOperation.WriteExpiring``1(System.String,``0,Hexalith.EventStore.Client.Projections.ReadModelBatchConcurrency,System.DateTimeOffset)',
 'P:Hexalith.EventStore.Client.Projections.ReadModelBatchOperation.ExpiresAtUtc',
 'P:Hexalith.EventStore.Client.Projections.SharedProjectionMutation.ExpiresAtUtc']
for project in ['Hexalith.EventStore.Client.Tests','Hexalith.EventStore.DomainService.Tests','Hexalith.EventStore.Server.LiveSidecar.Tests']:
 path=ROOT/'tests'/project/'bin/Debug/net10.0/Hexalith.EventStore.Client.xml'
 members={row.get('name'):row for row in E.parse(path).getroot().findall('./members/member')}
 assert all(name in members for name in required),(project,required)
 api.append(dict(project=project,path=str(path.relative_to(ROOT)),sha256=sha(path),verified_members=required))
dump('public-api-xml-audit.json',api)
workers=[];pairs=[]
for path in sorted((OUT/'process-workers').glob('*.json')):
 data=json.loads(path.read_text());xml=path.with_suffix('.xml')
 assert data['exit_code']==0 and xml.exists()
 result=E.parse(xml).getroot().find('assembly').attrib
 assert result['passed']=='1' and result['failed']=='0' and result['skipped']=='0'
 observations=[]
 for line in data['stdout'].splitlines():
  at=line.find('{"observation":')
  if at<0:continue
  row=json.loads(line[at:])
  for key,value in list(row.items()):
   if key.endswith('_base64') and value:
    row[key[:-7]+'_decoded']=json.loads(base64.b64decode(value))
  observations.append(row)
 workers.append(dict(**data,artifact=str(path.relative_to(OUT)),result=result,raw_observations=observations))
for persisted in workers:
 env=persisted['environment']
 if env['EXPIRY_SCRATCH_PROCESS_PHASE']!='persist':continue
 resumed=next(row for row in workers if row['environment']['EXPIRY_SCRATCH_PROCESS_PHASE']=='resume' and row['environment']['EXPIRY_SCRATCH_PROCESS_TENANT']==env['EXPIRY_SCRATCH_PROCESS_TENANT'])
 assert persisted['pid']!=resumed['pid']
 cleaned=next(row for row in resumed['raw_observations'] if row['observation']=='worker-cleaned-before-ordinary-delivery')
 assert cleaned['epoch_decoded']['phase']==0 and cleaned['session_decoded']['phase']==4
 assert not cleaned['scratch_exists'] and not cleaned['manifest_exists'] and not cleaned['original_marker_exists']
 pairs.append(dict(tenant=env['EXPIRY_SCRATCH_PROCESS_TENANT'],stop=env['EXPIRY_SCRATCH_PROCESS_STOP'],ordinal=env['EXPIRY_SCRATCH_PROCESS_ORDINAL'],populated=env['EXPIRY_SCRATCH_PROCESS_POPULATED'],retry=env['EXPIRY_SCRATCH_PROCESS_RETRY'],original_pid=persisted['pid'],recovery_pid=resumed['pid'],original_exited_before_recovery=True,persist=persisted['artifact'],resume=resumed['artifact']))
assert len(workers)==12 and len(pairs)==6
dump('final-process-workers.json',workers);dump('final-process-pairs.json',pairs)
executed=[];observations=[]
for record in commands:
 if 'xml' not in record:continue
 for test in E.parse(OUT/record['xml']).getroot().findall('.//test'):
  executed.append(dict(run=record['id'],name=test.get('name'),result=test.get('result'),time=test.get('time')))
  if record['id'].startswith('live-') and test.findtext('output'):
   observations.append(dict(run=record['id'],name=test.get('name'),result=test.get('result'),output=test.findtext('output')))
dump('executed-cases.json',executed);dump('raw-persisted-observations.json',observations)
cleanup=[]
for path in sorted(OUT.glob('live-*-cleanup.json')):
 row=json.loads(path.read_text());assert row['remaining_keys']==0 and row['sidecars_stopped'];cleanup.append(dict(artifact=path.name,**row))
dump('cleanup-summary.json',cleanup)
crosswalk={}
for path in sorted(OUT.glob('*-binary-group.json')):
 for row in json.loads(path.read_text()):crosswalk.setdefault(Path(row['path']).name,{}).setdefault(row['sha256'],[]).append(path.name)
dump('binary-group-crosswalk.json',{name:[dict(sha256=h,groups=g) for h,g in versions.items()] for name,versions in sorted(crosswalk.items())})
check=subprocess.run(['git','diff','--check'],cwd=ROOT,capture_output=True);(OUT/'diff-check.log').write_bytes(check.stdout+check.stderr);assert check.returncode==0
state={}
for repo in [ROOT,ROOT.parent.parent]:
 state[str(repo)]={command:subprocess.check_output(['git',*command.split()],cwd=repo,text=True) for command in ['rev-parse HEAD','branch --show-current','status --short','remote -v','log -3 --oneline']}
state['root_recorded_sdk_gitlink']=subprocess.check_output(['git','ls-tree','HEAD','references/Hexalith.EventStore'],cwd=ROOT.parent.parent,text=True)
dump('final-repository-state.json',state)
spec=ROOT/'_bmad-output/implementation-artifacts/spec-folders-12-2-expiry-and-scratch-recovery.md';shutil.copyfile(spec,OUT/'owner-spec-at-handoff.md')
shutil.copyfile('/tmp/story12-2-sdk-review3-pdb-verifier-build.log',OUT/'pdb-verifier-build.log')
qualification=dict(schema_version=1,status='ready-for-parent-independent-review',sealed_utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),scope='Approved EventStore source expiry and interrupted scratch recovery prerequisites; all review-three corrections',original_sdk_baseline='d4d05f871991ec2af305267f59dcefeae3f127ed',effective_sdk_head=state[str(ROOT)]['rev-parse HEAD'].strip(),effective_workspace_head=state[str(ROOT.parent.parent)]['rev-parse HEAD'].strip(),spec=dict(path=str(spec.relative_to(ROOT)),sha256=sha(spec),snapshot='owner-spec-at-handoff.md',status='in-progress'),results={row['id']:row.get('result',{'build_exit_code':row['exit_code']}) for row in commands},unique_live_cases_qualified=56,child_workers=dict(passed=12,failed=0,skipped=0,distinct_exited_owner_recovery_pairs=6),commands='commands.json',source_groups=dict(frozen='source-freeze.json',final='source-final.json',accepted_by_command='accepted-command-source-groups.json',unchanged_client_domain_reuse='source-group-reuse.json',file_count=len(json.loads((OUT/'source-final.json').read_text())),drift='source-drift.json',format='format-audit.json'),positive_reuse='positive-snapshot-reuse.json',binary_groups='binary-group-crosswalk.json',pdb_checks='pdb-commands.json',generated_api_docs='public-api-xml-audit.json',test_matrix='executed-cases.json',raw_persistence='raw-persisted-observations.json',worker_raw_state_and_commands='final-process-workers.json',distinct_pids='final-process-pairs.json',cleanup='cleanup-summary.json',historical_packets='historical-packets-unchanged.json',replacement_scope='New clients/stores/coordinators and six distinct exited-owner/recovery OS-process pairs over one fixture sidecar/state; no sidecar restart claim.',notes=['All failed or interrupted intermediate attempts remain in diagnostics, including first-final-attempt stopped for the still-Aborted-marker boundary correction and pre-lf-final-attempt stopped for authoritative repository LF formatting. Neither interrupted full Domain run is acceptance.','Two optional Client isolated-endpoint skips are not acceptance evidence. All required selected live and child worker cases execute with zero skips.','Final affected projects are Debug/source; complete Domain uses -parallel none to avoid the previously documented unrelated default-parallel timer coincidence. Client/Domain accepted groups retain their earlier source freeze because only the Live forced-provider diagnostic changed; all 3,760 other source/config/guide files remain identical. Live is rebuilt and fully requalified under its separate final freeze.','Fully validated legacy Aborted markers remain at their original keys as bounded payload-free provider-ABA fences, alongside immutable version-1 fingerprint/digest proof. Replacement retained scratch uses a distinct bounded deterministic scope-hash identity; recovery/reclamation inspect both exact identities. Original legacy-marker absence is intentionally not claimed.','Production Redis remains Resumable. The forced TransactionQualified diagnostic sends an actual stale guarded participant to Redis: native rejection leaves a partial model and Completed physical marker, while SDK read-back refuses success as Indeterminate on both initial call and retry. Newer guard bytes and marker bytes remain exact; legacy and deadline retention are asserted. This is partial-write/refusal evidence, not atomicity or rollback qualification.','Redis accepted historical absent-key stale-CAS recreation; the post-await retirement guard settled it safely before compaction. A separate replacement-version ABA cut preserved exact newer marker/row bytes. The earlier failing provider gate remains diagnostic evidence.', 'Prior SDK and parent evidence, Folders source, Git state, pins, packages, deployments and retained events were not changed. Parent review, consumer restoration/adoption, package/PD10 and >10,000 identities remain separate.'])
dump('qualification.json',qualification)
print(json.dumps(qualification['results'],indent=2))

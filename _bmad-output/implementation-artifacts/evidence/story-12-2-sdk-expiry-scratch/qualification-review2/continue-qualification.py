from pathlib import Path
import subprocess,os,json,hashlib,datetime,xml.etree.ElementTree as ET,shutil
ROOT=Path('/home/administrator/projects/hexalith/folders/references/Hexalith.EventStore')
OUT=ROOT/'_bmad-output/implementation-artifacts/evidence/story-12-2-sdk-expiry-scratch/qualification-review2'
def digest(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def dump(n,x):(OUT/n).write_text(json.dumps(x,indent=2)+'\n')
def now():return datetime.datetime.now(datetime.timezone.utc).isoformat()
def source():
 paths=[p for folder in ('src','tests') for p in (ROOT/folder).rglob('*') if p.is_file() and not any(part in ('bin','obj','node_modules') for part in p.relative_to(ROOT).parts)]
 paths += [ROOT/p for p in ('docs/guides/named-projection-authoring.md','docs/concepts/projection-delivery.md','Directory.Build.props','Directory.Build.targets','Directory.Packages.props','global.json','.editorconfig','.gitattributes') if (ROOT/p).exists()]
 return [{'path':str(p.relative_to(ROOT)),'bytes':p.stat().st_size,'sha256':digest(p)} for p in sorted(set(paths))]
def scan():
 p=subprocess.run(['docker','exec','dapr_redis','redis-cli','--scan','--pattern','eventstore-live-*'],capture_output=True,check=True)
 return {k for k in p.stdout.splitlines() if b'||' in k and k.startswith(b'eventstore-live-')}
def cleanup(before,label):
 after=scan(); namespaces={k.split(b'||',1)[0] for k in after}-{k.split(b'||',1)[0] for k in before}
 processes=subprocess.run(['pgrep','-af','daprd'],capture_output=True,text=True).stdout
 assert not any(n.decode() in processes for n in namespaces),'fixture sidecar remains active'
 keys=[k for k in after if k.split(b'||',1)[0] in namespaces]
 if keys:
  resp=b''.join(b'*2\r\n$3\r\nDEL\r\n$'+str(len(k)).encode()+b'\r\n'+k+b'\r\n' for k in keys)
  p=subprocess.run(['docker','exec','-i','dapr_redis','redis-cli','--pipe'],input=resp,capture_output=True,check=True)
  (OUT/(label+'-cleanup-native.log')).write_bytes(p.stdout+p.stderr)
 remaining=[k for k in scan() if k.split(b'||',1)[0] in namespaces]
 record={'app_ids':sorted(n.decode() for n in namespaces),'removed_keys':len(keys),'remaining_keys':len(remaining),'sidecars_stopped':True,'command':['docker','exec','-i','dapr_redis','redis-cli','--pipe'],'preserved_preexisting_app_ids':len({k.split(b'||',1)[0] for k in before})}
 dump(label+'-cleanup.json',record);assert not remaining;return record
def group(project,label):
 folder=ROOT/'tests'/project/'bin/Debug/net10.0'
 files=[p for p in folder.iterdir() if p.suffix in ('.dll','.pdb','.xml') and p.name.startswith('Hexalith.EventStore')]
 data=[{'path':str(p.relative_to(ROOT)),'bytes':p.stat().st_size,'sha256':digest(p)} for p in sorted(files)]
 dump(label+'-binary-group.json',data)
 return label+'-binary-group.json'
before_source=json.loads((OUT/'source-freeze.json').read_text())
assert before_source == source()
C='Hexalith.EventStore.Client.Tests';D='Hexalith.EventStore.DomainService.Tests';L='Hexalith.EventStore.Server.LiveSidecar.Tests'
def build(p):return ['dotnet','build',f'tests/{p}/{p}.csproj','-c','Debug','-p:UseHexalithProjectReferences=true','-p:UseNuGetDeps=false','-m:1','--no-restore']
def test(p,*args):return ['dotnet',f'tests/{p}/bin/Debug/net10.0/{p}.dll',*args]
commands=[('domain-full-serial',test(D,'-parallel','none'),{'HEXALITH_TENANTS_DOMAIN_ROOT':str(ROOT.parent/'Hexalith.Tenants/src/Hexalith.Tenants')},D),
('live-build',build(L),{},L),
('live-expiry-scratch',test(L,'-class','*ProjectionExpiryAndScratchRecoveryLiveTests','-showliveoutput'),{'EVENTSTORE_TEST_DAPR_HOT_RELOAD':'false','EVENTSTORE_SCRATCH_PROCESS_EVIDENCE_DIR':str(OUT/'process-workers')},L),
('live-read-model-batch',test(L,'-class','*ReadModelBatchLiveSidecarTests','-showliveoutput'),{'EVENTSTORE_TEST_DAPR_HOT_RELOAD':'false','RUN_TX_QUALIFICATION_PROBE':'1'},L),
('live-shared-epoch',test(L,'-class','*SharedProjectionEpochLiveSidecarTests','-showliveoutput'),{'EVENTSTORE_TEST_DAPR_HOT_RELOAD':'false'},L)]
records=json.loads((OUT/'commands.json').read_text())
for label,command,environment,project in commands:
 is_test='build' not in label
 if is_test:command += ['-result-xml',str(OUT/(label+'.xml'))]
 record={'id':label,'command':command,'environment':environment,'cwd':str(ROOT),'started_utc':now()}
 dump('running.json',record);before=scan() if label.startswith('live-') and is_test else None
 with (OUT/(label+'.log')).open('wb') as log:
  p=subprocess.run(command,cwd=ROOT,env={**os.environ,**environment},stdout=log,stderr=subprocess.STDOUT)
 record.update(exit_code=p.returncode,finished_utc=now(),log=label+'.log',log_sha256=digest(OUT/(label+'.log')))
 if is_test and (OUT/(label+'.xml')).exists():
  doc=ET.parse(OUT/(label+'.xml')).getroot();assemblies=doc.findall('assembly')
  record['result']={a:sum(int(x.get(a,'0')) for x in assemblies) for a in ('total','passed','failed','skipped','errors','not-run')}
  record['xml']=label+'.xml';record['xml_sha256']=digest(OUT/(label+'.xml'))
  record['skips']=[{'name':t.get('name'),'reason':t.findtext('reason')} for t in doc.findall('.//test') if t.get('result')=='Skip']
 if before is not None:record['cleanup']=cleanup(before,label)
 if is_test:record['binary_group']=group(project,label)
 records.append(record);dump('commands.json',records);print(label,p.returncode,record.get('result',{}),flush=True)
 if p.returncode:raise SystemExit(p.returncode)
after_source=source();dump('source-final.json',after_source);dump('source-drift.json',{'unchanged':before_source==after_source});assert before_source==after_source
print('FINAL_COMMANDS_COMPLETE',flush=True)

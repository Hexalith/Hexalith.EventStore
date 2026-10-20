from pathlib import Path
import subprocess, os, json, hashlib, datetime, xml.etree.ElementTree as ET, re, shutil
ROOT=Path('/home/administrator/projects/hexalith/folders/references/Hexalith.EventStore')
OUT=ROOT/'_bmad-output/implementation-artifacts/evidence/story-12-2-sdk-expiry-scratch/qualification-review1'
OUT.mkdir(parents=True, exist_ok=True)
def digest(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def dump(name,value): (OUT/name).write_text(json.dumps(value,indent=2)+'\n')
def now():return datetime.datetime.now(datetime.timezone.utc).isoformat()
def scan():
 p=subprocess.run(['docker','exec','dapr_redis','redis-cli','--scan','--pattern','eventstore-live-*'],capture_output=True,check=True)
 return {key for key in p.stdout.splitlines() if b'||' in key and key.startswith(b'eventstore-live-')}
def cleanup(before, after, run):
 namespaces={key.split(b'||',1)[0] for key in after}-{key.split(b'||',1)[0] for key in before}
 keys=[key for key in after if key.split(b'||',1)[0] in namespaces]
 processes=subprocess.run(['pgrep','-af','daprd'],capture_output=True,text=True).stdout
 assert not any(ns.decode() in processes for ns in namespaces), 'fixture sidecar still active at cleanup'
 if keys:
  resp=b''.join(b'*2\r\n$3\r\nDEL\r\n$'+str(len(key)).encode()+b'\r\n'+key+b'\r\n' for key in keys)
  p=subprocess.run(['docker','exec','-i','dapr_redis','redis-cli','--pipe'],input=resp,capture_output=True,check=True)
  (OUT/(run+'-cleanup-native.log')).write_bytes(p.stdout+p.stderr)
 remaining=[key for key in scan() if key.split(b'||',1)[0] in namespaces]
 record={'run':run,'at':now(),'exact_app_ids':sorted(ns.decode() for ns in namespaces),'delete_command':['docker','exec','-i','dapr_redis','redis-cli','--pipe'],'removed_key_count':len(keys),'remaining_key_count':len(remaining),'fixture_sidecars_stopped':True,'preserved_preexisting_app_ids':len({key.split(b'||',1)[0] for key in before})}
 dump(run+'-cleanup.json',record)
 assert not remaining
 return record
build=lambda project:['dotnet','build',f'tests/{project}/{project}.csproj','-c','Debug','-p:UseHexalithProjectReferences=true','-p:UseNuGetDeps=false','--no-restore']
def runner(project,*filters):return ['dotnet',f'tests/{project}/bin/Debug/net10.0/{project}.dll',*filters]
client='Hexalith.EventStore.Client.Tests';domain='Hexalith.EventStore.DomainService.Tests';live='Hexalith.EventStore.Server.LiveSidecar.Tests'
shutil.copyfile('/tmp/review1-process-build.log',OUT/'live-process-build.log')
records=json.loads((OUT/'commands.json').read_text())
records.append({'id':'live-process-build','command':build(live),'environment':{},'working_directory':str(ROOT),'exit_code':0,'log':'live-process-build.log','log_sha256':digest(OUT/'live-process-build.log'),'note':'Test-only process harness extension; unchanged production sources and completed Client/Domain qualification are retained.'})
dump('commands.json',records)
commands=[('live-process-restart',runner(live,'-class','*ProjectionExpiryAndScratchRecoveryLiveTests','-method','*PendingNamedOwnerScratchRecoversAfterOwningOsProcessExits','-showliveoutput'),{'EVENTSTORE_TEST_DAPR_HOT_RELOAD':'false','EVENTSTORE_SCRATCH_PROCESS_EVIDENCE_DIR':str(OUT/'process-workers')},live)]
records=json.loads((OUT/'commands.json').read_text())
for label,cmd,environment,project in commands:
 is_test='build' not in label
 if is_test:cmd += ['-result-xml',str(OUT/(label+'.xml'))]
 record={'id':label,'command':cmd,'environment':environment,'working_directory':str(ROOT),'started_utc':now()}
 dump('running.json',record)
 before=scan() if label.startswith('live-') and is_test else None
 with (OUT/(label+'.log')).open('wb') as log:
  completed=subprocess.run(cmd,cwd=ROOT,env={**os.environ,**environment},stdout=log,stderr=subprocess.STDOUT)
 record.update(exit_code=completed.returncode,finished_utc=now(),log=label+'.log',log_sha256=digest(OUT/(label+'.log')))
 if is_test and (OUT/(label+'.xml')).exists():
  doc=ET.parse(OUT/(label+'.xml')).getroot();assemblies=doc.findall('assembly')
  record['xml']=label+'.xml';record['xml_sha256']=digest(OUT/(label+'.xml'))
  record['result']={attr:sum(int(a.get(attr,'0')) for a in assemblies) for attr in ('total','passed','failed','skipped','errors','not-run')}
  record['skips']=[{'name':t.get('name'),'reason':t.findtext('reason')} for t in doc.findall('.//test') if t.get('result')=='Skip']
  record['failures']=[{'name':t.get('name'),'failure':t.findtext('.//message')} for t in doc.findall('.//test') if t.get('result')=='Fail']
 if before is not None:record['cleanup']=cleanup(before,scan(),label)
 if is_test:
  folder=ROOT/f'tests/{project}/bin/Debug/net10.0';group=[]
  for file in sorted(folder.iterdir()):
   if file.suffix in ('.dll','.pdb','.xml') and file.name.startswith('Hexalith.EventStore'):
    group.append({'path':str(file.relative_to(ROOT)),'bytes':file.stat().st_size,'sha256':digest(file)})
  dump(label+'-binary-group.json',group);record['binary_group']=label+'-binary-group.json'
 records.append(record);dump('commands.json',records)
 print(label,completed.returncode,record.get('result',''),flush=True)
 if completed.returncode: raise SystemExit(completed.returncode)
print('QUALIFICATION_COMMANDS_COMPLETE',flush=True)

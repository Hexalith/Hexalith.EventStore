import datetime,hashlib,json,pathlib,time
B=pathlib.Path('/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/evidence/story-6-4');P='qualification-20261017-';Q=87702
observed={};started=datetime.datetime.now(datetime.timezone.utc).isoformat();output=B/(P+'native-process-observations.json')
def proc(pid):
 try:
  p=pathlib.Path('/proc')/str(pid);raw=(p/'stat').read_text();parts=raw[raw.rfind(')')+2:].split();argv=[x.decode(errors='replace') for x in (p/'cmdline').read_bytes().split(b'\0') if x];return {'pid':pid,'ppid':int(parts[1]),'startTicks':parts[19],'argv':argv}
 except (FileNotFoundError,PermissionError,ProcessLookupError):return None
while True:
 try:state=json.loads((B/(P+'gates.json')).read_text())
 except (FileNotFoundError,json.JSONDecodeError):time.sleep(1);continue
 if state.get('finishedAt'):break
 active=B/(P+'active.json')
 try:lane=json.loads(active.read_text())['name']
 except (FileNotFoundError,json.JSONDecodeError):lane='between-frames'
 table={}
 for path in pathlib.Path('/proc').iterdir():
  if path.name.isdecimal():
   p=proc(int(path.name))
   if p:table[p['pid']]=p
 pending={Q};desc={}
 while pending:
  children={pid:p for pid,p in table.items() if p['ppid'] in pending and pid not in desc};desc.update(children);pending=set(children)
 changed=False
 for pid,p in desc.items():
  if not p['argv'] or pathlib.Path(p['argv'][0]).name not in {'dotnet','daprd','placement','scheduler'}:continue
  # These fixture commands pass authentication through environment, which is never read.
  key=str(pid)+':'+p['startTicks']
  if key not in observed:observed[key]={**p,'lane':lane,'firstObservedAt':datetime.datetime.now(datetime.timezone.utc).isoformat()};changed=True
 if changed:
  output.write_text(json.dumps({'observerStartedAt':started,'coordinatorPid':Q,'observerSha256':hashlib.sha256(pathlib.Path(__file__).read_bytes()).hexdigest(),'scope':'Read-only actual native descendant argv/startTicks. Started during Release Live; earlier fast Debug fixture child commands are not recovered or invented. Environment is never captured.','processes':list(observed.values())},indent=2)+'\n')
 time.sleep(1)
result=json.loads(output.read_text()) if output.exists() else {'observerStartedAt':started,'processes':[]};result['endedAt']=datetime.datetime.now(datetime.timezone.utc).isoformat();result['finalIdentityChecks']=[{'pid':p['pid'],'startTicks':p['startTicks'],'sameProcessStillPresent':(proc(p['pid']) or {}).get('startTicks')==p['startTicks']} for p in observed.values()];output.write_text(json.dumps(result,indent=2)+'\n');print('NATIVE_PROCESS_OBSERVER_ENDED',len(observed))

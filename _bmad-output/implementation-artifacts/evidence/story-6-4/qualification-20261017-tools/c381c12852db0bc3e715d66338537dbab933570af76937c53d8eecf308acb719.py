import datetime,hashlib,json,os,pathlib,re,shutil,signal,subprocess,time
BASE=pathlib.Path('/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/evidence/story-6-4');PREFIX='qualification-20261017-';QUEUE=87702
limits={'server-full':1800,'live':2700,'contracts-full':1800,'domain-full':900,'client-full':600,'build':1200,'restore':600,'pack':1800,'other':1800};observed={};owned_scratch={};timeouts=[]
policy={'queuePid':QUEUE,'limitsSeconds':limits,'graceSeconds':30,'policy':'No retry on timeout. Freeze queue, signal only native descendants whose PID/start-time still match, bounded drain then hard stop; retain failed/unproven timeout receipt; clean only scratch/cidfile ownership traced to those descendants. Healthy commands continue untouched.'}
(BASE/(PREFIX+'timeout-policy-fallback.json')).write_text(json.dumps(policy,indent=2)+'\n')
def process(pid):
 try:
  raw=pathlib.Path(f'/proc/{pid}/stat').read_text();tail=raw[raw.rfind(')')+2:].split();args=pathlib.Path(f'/proc/{pid}/cmdline').read_bytes().split(b'\0');return {'pid':pid,'ppid':int(tail[1]),'startTicks':tail[19],'args':[a.decode(errors='replace') for a in args if a]}
 except (FileNotFoundError,PermissionError,ProcessLookupError):return None

def descendants():
 table={}
 for p in pathlib.Path('/proc').iterdir():
  if p.name.isdecimal():
   d=process(int(p.name))
   if d:table[d['pid']]=d
 out={};pending={QUEUE}
 while pending:
  new={pid:d for pid,d in table.items() if d['ppid'] in pending and pid not in out};out.update(new);pending=set(new)
 return out

def same(d):
 now=process(d['pid']);return now is not None and now['startTicks']==d['startTicks']
def send(d,sig):
 if same(d):
  try:os.kill(d['pid'],sig)
  except ProcessLookupError:pass

def limit(name):
 if 'live-' in name:return limits['live']
 if 'server-' in name and '-full' in name:return limits['server-full']
 if 'contracts-' in name and '-full' in name:return limits['contracts-full']
 if ('domain-' in name or 'domainservice-' in name) and '-full' in name:return limits['domain-full']
 if 'client-' in name and '-full' in name:return limits['client-full']
 if 'restore' in name:return limits['restore']
 if 'build' in name:return limits['build']
 if name=='packages':return limits['pack']
 return limits['other']

while process(QUEUE):
 try:
  active=json.loads((BASE/(PREFIX+'active.json')).read_text());name=active['name'];tracked=observed.setdefault(name,{})
  for pid,d in descendants().items():
   tracked[(pid,d['startTicks'])]=d
   for arg in d['args']:
    for match in re.finditer(r'/tmp/eventstore-oq8-runtime-[0-9a-f]{32}',arg):owned_scratch.setdefault(name,set()).add(match[0])
  elapsed=(datetime.datetime.now(datetime.timezone.utc)-datetime.datetime.fromisoformat(active['startedAt'])).total_seconds()
  if elapsed>limit(name) and name not in timeouts:
   current=json.loads((BASE/(PREFIX+'active.json')).read_text())
   if current['name']!=name:continue
   os.kill(QUEUE,signal.SIGSTOP);receipt={'name':name,'command':active['command'],'deadlineSeconds':limit(name),'elapsedSeconds':elapsed,'nativeGate':'failed/unproven','automaticRetry':False,'startedAt':active['startedAt'],'timeoutAt':datetime.datetime.now(datetime.timezone.utc).isoformat(),'ownedProcesses':[{'pid':d['pid'],'startTicks':d['startTicks'],'executable':pathlib.Path(d['args'][0]).name if d['args'] else ''} for d in tracked.values()]}
   try:
    for d in tracked.values():send(d,signal.SIGINT if d['ppid']==QUEUE else signal.SIGTERM)
    deadline=time.monotonic()+30
    while time.monotonic()<deadline and any(same(d) for d in tracked.values()):time.sleep(.2)
    for d in tracked.values():send(d,signal.SIGKILL)
    cleanup=[]
    for scratch in owned_scratch.get(name,set()):
     directory=pathlib.Path(scratch);cidfile=directory/'postgresql.cid';item={'scratchIdentitySha256':hashlib.sha256(scratch.encode()).hexdigest()}
     if cidfile.exists():
      cid=cidfile.read_text().strip()
      if re.fullmatch('[0-9a-f]{64}',cid):
       check=subprocess.run(['docker','inspect','--format','{{.Name}}',cid],capture_output=True,text=True,timeout=10)
       if check.returncode==0 and check.stdout.strip().startswith('/eventstore-oq8-'):
        removed=subprocess.run(['docker','rm','-f','-v',cid],stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL,timeout=20);item['ownedPostgresRemoved']=removed.returncode==0
       else:item['ownedPostgresAlreadyAbsent']=check.returncode!=0
     if directory.exists():shutil.rmtree(directory)
     item['scratchRemoved']=not directory.exists();cleanup.append(item)
    receipt['cleanup']=cleanup;receipt['matchingProcessesRemaining']=[d['pid'] for d in tracked.values() if same(d)]
   finally:
    (BASE/(PREFIX+name+'-timeout.json')).write_text(json.dumps(receipt,indent=2)+'\n');timeouts.append(name);os.kill(QUEUE,signal.SIGCONT)
 except (FileNotFoundError,json.JSONDecodeError):pass
 time.sleep(2)
print('FINITE_QUEUE_WATCHDOG_ENDED',json.dumps({'timeouts':timeouts}),flush=True)

import hashlib,json,os,pathlib,socket,subprocess,tempfile,time,urllib.request
root=pathlib.Path('/home/administrator/projects/hexalith/eventstore')
binaries=pathlib.Path('/tmp/story-6-6-native-dapr')
run=pathlib.Path(tempfile.mkdtemp(prefix='story-6-6-native-run-'))
listeners=[]
ports=[]
for _ in range(9):
 s=socket.socket(); s.bind(('127.0.0.1',0)); ports.append(s.getsockname()[1]); listeners.append(s)
redis,placement,scheduler,raft,peer,etcd,placement_health,scheduler_health,metrics=ports
for s in listeners: s.close()
processes=[]
logs=[]
profile={'scope':'Development native loopback only; no production qualification','ports':dict(redis=redis,placement=placement,scheduler=scheduler), 'run_directory':str(run),'binaries':{name:hashlib.sha256((binaries/name).read_bytes()).hexdigest() for name in ('redis-server','placement','scheduler')}}
(run/'profile.json').write_text(json.dumps(profile,indent=2))
print(json.dumps(profile),flush=True)
try:
 (run/'redis').mkdir(); (run/'scheduler').mkdir()
 commands=[
  ['redis-server','--bind','127.0.0.1','--port',str(redis),'--dir',str(run/'redis'),'--appendonly','yes','--save','','--daemonize','no'],
  ['placement','--listen-address','127.0.0.1','--port',str(placement),'--initial-cluster',f'dapr-placement-0=127.0.0.1:{raft}','--healthz-listen-address','127.0.0.1','--healthz-port',str(placement_health),'--enable-metrics=false'],
  ['scheduler','--listen-address','127.0.0.1','--port',str(scheduler),'--etcd-data-dir',str(run/'scheduler'),'--etcd-client-listen-address','127.0.0.1','--etcd-client-port',str(etcd),'--etcd-initial-cluster',f'dapr-scheduler-server-0=http://127.0.0.1:{peer}','--override-broadcast-host-port',f'127.0.0.1:{scheduler}','--healthz-listen-address','127.0.0.1','--healthz-port',str(scheduler_health),'--enable-metrics=false']]
 for args in commands:
  log=(run/(args[0]+'.log')).open('wb');logs.append(log)
  processes.append(subprocess.Popen([str(binaries/args[0]),*args[1:]],stdout=log,stderr=subprocess.STDOUT))
 deadline=time.monotonic()+30
 ready=False
 while time.monotonic()<deadline:
  if any(p.poll() is not None for p in processes):
   raise RuntimeError('A native prerequisite exited; inspect captured logs in '+str(run))
  try:
   with socket.create_connection(('127.0.0.1',redis),timeout=1) as s:
    s.sendall(b'*1\r\n$4\r\nPING\r\n'); assert s.recv(128)==b'+PONG\r\n'
   for port in (placement_health,scheduler_health):
    with urllib.request.urlopen(f'http://127.0.0.1:{port}/healthz',timeout=1) as response: assert response.status==200
   ready=True;break
  except (OSError,AssertionError): time.sleep(.25)
 if not ready: raise RuntimeError('Native prerequisite readiness exceeded 30 seconds')
 env=os.environ.copy();env.update(EVENTSTORE_TEST_REDIS_PORT=str(redis),EVENTSTORE_TEST_PLACEMENT_PORT=str(placement),EVENTSTORE_TEST_SCHEDULER_PORT=str(scheduler))
 cmd=['dotnet',str(root/'tests/Hexalith.EventStore.Server.LiveSidecar.Tests/bin/Debug/net10.0/Hexalith.EventStore.Server.LiveSidecar.Tests.dll'),'-class','Hexalith.EventStore.Server.LiveSidecar.Tests.Events.DaprEventEvolutionLogicalReadbackLiveSidecarTests','-class','Hexalith.EventStore.Server.LiveSidecar.Tests.Fixtures.DaprTestInfrastructurePortsTests']
 print('Native prerequisites ready; starting the unchanged live assertion gate.',flush=True)
 with (run/'test.log').open('wb') as log:
  result=subprocess.run(cmd,cwd=root,env=env,stdout=log,stderr=subprocess.STDOUT,timeout=240)
 profile['test_exit_code']=result.returncode
 (run/'profile.json').write_text(json.dumps(profile,indent=2))
 print('Live test exit code:',result.returncode,flush=True)
 print((run/'test.log').read_text()[-4500:],flush=True)
 raise SystemExit(result.returncode)
finally:
 for p in reversed(processes):
  if p.poll() is None: p.terminate()
 for p in reversed(processes):
  try: p.wait(timeout=10)
  except subprocess.TimeoutExpired: p.kill();p.wait(timeout=5)
 for log in logs:log.close()
 print('All owned native prerequisite processes stopped.',flush=True)

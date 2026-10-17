import collections,hashlib,importlib.util,json,os,pathlib,re,socket,subprocess,sys,tempfile,time,uuid,xml.etree.ElementTree as ET,zipfile
spec=importlib.util.spec_from_file_location('capture','/tmp/story64-qualification-capture.py');c=importlib.util.module_from_spec(spec);spec.loader.exec_module(c)
R=c.ROOT;E=c.EVIDENCE;P=c.PREFIX
state={'startedAt':c.now(),'authorization':'one finite full-candidate section 10 pass; no automatic requeue for unrelated drift','gates':[],'packageOutput':tempfile.mkdtemp(prefix='story64-qualification-packages-'),'ownedResources':{},'plannedGates':['server-debug-build','server-debug-focused','server-debug-full','domain-debug-build','domain-debug-focused','domain-debug-full','client-debug-build','client-debug-full','contracts-debug-build','live-debug-build','live-debug-focused','release-restore-1','release-build-1','packages','contracts-debug-full','release-restore-2','release-build-2','server-release-full','domain-release-full','client-release-full','contracts-release-full','release-restore-3','release-build-3','live-debug-full','live-release-full','live-release-r4096']}
def save():
 (E/(P+'gates.json')).write_text(json.dumps(state,indent=2)+'\n')
def record(entry):
 state['gates'].append({'name':entry['name'],'packet':P+entry['name']+'.json','exitCode':entry['exitCode'],'summary':entry.get('summary'),'sourceInputChanges':len(entry['sourceInputsChanges']),'runtimeChanges':len(entry['runtimeChanges'])});save();return entry

def gate(name,command,kind='test',env=None,producer=None):return record(c.capture(name,command,kind,env,producer))
def output(project,config):return f'tests/Hexalith.EventStore.{project}.Tests/bin/{config}/net10.0/Hexalith.EventStore.{project}.Tests.dll'
def test(name,project,config,classes=(),env=None,producer=None):
 args=['dotnet',output(project,config)]
 for cls in classes:args+=['-class','*'+cls]
 args+=['-xml',str(E/(P+name+'.xml')),'-showLiveOutput','-noColor']
 return gate(name,args,env=env,producer=producer)
def build(project):return gate(project.lower()+'-debug-build',['dotnet','build',f'tests/Hexalith.EventStore.{project}.Tests/Hexalith.EventStore.{project}.Tests.csproj','-c','Debug','-p:UseHexalithProjectReferences=true','-p:GeneratePackageOnBuild=false','-m:1'],'build')
def release(n):
 restored=gate(f'release-restore-{n}',['dotnet','restore','Hexalith.EventStore.slnx','-p:Configuration=Release','-p:UseHexalithProjectReferences=false','-p:UseNuGetDeps=true'],'restore')
 if restored['exitCode']:state.setdefault('blocked',[]).append({'gate':f'release-build-{n}','reason':'owning restore failed'});save();return None
 return gate(f'release-build-{n}',['dotnet','build','Hexalith.EventStore.slnx','-c','Release','--no-restore','-p:UseHexalithProjectReferences=false','-p:UseNuGetDeps=true','-m:1'],'build')
def resource_setup():
 name='story64-qualification-20261017-'+uuid.uuid4().hex[:8]
 image=subprocess.check_output(['docker','inspect','dapr_redis','--format','{{.Image}}'],text=True).strip()
 run=subprocess.run(['docker','run','-d','--name',name,'--label','task=story64-qualification-20261017','-p','127.0.0.1::6379',image],capture_output=True,text=True)
 assert run.returncode==0,run.stderr
 cid=run.stdout.strip();info=json.loads(subprocess.check_output(['docker','inspect',cid],text=True))[0];port=info['NetworkSettings']['Ports']['6379/tcp'][0]['HostPort']
 state['ownedResources']={'redis':{'name':name,'id':cid,'image':image,'port':port,'command':info['Config']['Cmd'],'label':info['Config']['Labels'].get('task'),'createdAt':c.now()},'initialSharedContainers':subprocess.check_output(['docker','ps','--format','{{.ID}} {{.Names}}'],text=True).splitlines()};save()
 for _ in range(100):
  pong=subprocess.run(['docker','exec',cid,'redis-cli','ping'],capture_output=True,text=True)
  if pong.returncode==0 and pong.stdout.strip()=='PONG':break
  time.sleep(.1)
 else:raise RuntimeError('Owned Redis readiness failed')
 settings=['appendonly','save','maxmemory','maxmemory-policy']
 def readsettings(container):return {key:subprocess.check_output(['docker','exec',container,'redis-cli','--raw','CONFIG','GET',key],text=True).splitlines()[1:] for key in settings}
 own=readsettings(cid);shared=readsettings('dapr_redis');assert own==shared,(own,shared)
 state['ownedResources']['redis'].update(settings=own,matchedSharedSettings=True,version=subprocess.check_output(['docker','exec',cid,'redis-server','--version'],text=True).strip());save()
 previous=json.loads(pathlib.Path('/tmp/story-6-4-cont10-live-lane3.json').read_text());commands=previous['commands'];sockets=[];ports=[]
 for _ in range(9):
  sock=socket.socket();sock.bind(('127.0.0.1',0));sockets.append(sock);ports.append(sock.getsockname()[1])
 replacements={52991:ports[0],37417:ports[1],39691:ports[2],35201:ports[3],52353:ports[4],60445:ports[5],50553:ports[6],58479:ports[7],49725:ports[8]}
 for argv in commands:
  for i,value in enumerate(argv):
   for old,new in replacements.items():
    if str(old) in value:value=value.replace(str(old),str(new))
   argv[i]=value
 previous.update(commands=commands,placementPort=ports[0],schedulerPort=ports[4]);pathlib.Path('/tmp/story64-qualification-controls.json').write_text(json.dumps(previous,indent=2)+'\n')
 for sock in sockets:sock.close()
 return {'EVENTSTORE_TEST_REDIS_PORT':port}
def live(name,config,pattern,regular,Rlimit=256):
 env={**regular,'EVENTSTORE_TEST_DELIVERY_RECEIPT_LIMIT':str(Rlimit)}
 if pattern=='__FULL__':env.update(HEXALITH_OQ8_EVIDENCE_DIRECTORY='/tmp/story64-qualification-oq8-'+name,HEXALITH_OQ8_CLEANUP_PATH='/tmp/story64-qualification-oq8-'+name+'-cleanup.json')
 result=gate(name,['python3','/tmp/story64-qualification-live.py',name,pattern,config],env=env,producer='live-debug-build + actual coherent Release hosts' if config=='Debug' else 'release-build-3 + actual assets')
 controls=E/(P+name+'-controls.json')
 if controls.exists():
  data=json.loads(controls.read_text());state['gates'][-1]['controlsExitCodes']=data['infrastructureExitCodes'];save()
 native=E/(P+name+'.native.log')
 if native.exists():
  text=native.read_text(errors='replace');observations=[]
  for line in text.splitlines():
   match=re.search(r'\[OUTPUT\] (\w+) (\{.*\})$',line)
   if match and match[1] in c.ALLOWED:observations.append({'kind':match[1],'value':json.loads(match[2])})
  vector=E/(P+name+'-vectors.json');vector.write_text(json.dumps({'sourceSnapshot':P+name,'exitCode':result['exitCode'],'command':result['command'],'observations':observations},indent=2)+'\n')
  state['gates'][-1]['observations']=dict(collections.Counter(o['kind'] for o in observations));save()
 return result

def inventory():
 items=[];directory=pathlib.Path(state['packageOutput']);expected={p['id'] for p in json.loads((R/'tools/release-packages.json').read_text())['packages']}
 for path in sorted(directory.glob('*.nupkg')):
  with zipfile.ZipFile(path) as archive:
   xml=ET.fromstring(archive.read(next(n for n in archive.namelist() if n.endswith('.nuspec'))));metadata=next(e for e in xml if e.tag.split('}')[-1]=='metadata');fields={e.tag.split('}')[-1]:e.text for e in metadata};items.append({**c.identity(path),'id':fields['id'],'version':fields['version'],'dependencies':[e.attrib for e in metadata.iter() if e.tag.split('}')[-1]=='dependency'],'embeddedDlls':[{'path':n,'bytes':len(archive.read(n)),'sha256':hashlib.sha256(archive.read(n)).hexdigest()} for n in archive.namelist() if n.endswith('.dll')]})
 result={'packages':items,'manifestIdsExactlyMatched':len(items)==14 and {i['id'] for i in items}==expected,'allEffectiveVersionsCorrect':all(i['version']=='999.0.0-ci-test' for i in items),'capturedAt':c.now()};(E/(P+'package-inventory.json')).write_text(json.dumps(result,indent=2)+'\n');state['packageInventory']=result;save();return result

save()
try:
 while not (E/(P+'server-debug-build.json')).exists():time.sleep(.5)
 initial=json.loads((E/(P+'server-debug-build.json')).read_text());record(initial)
 if initial['exitCode']==0:
  test('server-debug-focused','Server','Debug',['ProjectionDeliveryIdempotencyCoordinatorTests','NamedProjectionDispatchCoordinatorTests','EventStoreProjectionDeliveryHistoryReaderTests','ProjectionRebuildProductionPathTests','ProjectionDeliveryCutoverTests','ProjectionDeliveryReconcilerTests','ProjectionDeliveryStateContractTests','ProjectionDeliveryDiagnosticsTests','ProjectionEraseCoordinatorTests','EventPersisterTests','AggregateMetadataTests','AggregateActorTests','AggregateActorInfrastructureFailureTests','AggregateActorGetEventsTests'],producer='server-debug-build')
  test('server-debug-full','Server','Debug',producer='server-debug-build')
 domain=build('DomainService')
 if domain['exitCode']==0:
  test('domain-debug-focused','DomainService','Debug',['DomainProjectionHandlerCompatibilityTests','DomainProjectionDispatcherV2Tests','MixedProjectionCatalogTests','DomainFencedSharedProjectionIntegrationTests','DomainPartitionedSharedProjectionRebuildTests'],producer='domainservice-debug-build')
  test('domain-debug-full','DomainService','Debug',producer='domainservice-debug-build')
 client=build('Client')
 if client['exitCode']==0:test('client-debug-full','Client','Debug',producer='client-debug-build')
 contracts=build('Contracts');livebuild=build('Server.LiveSidecar')
 regular=resource_setup()
 if livebuild['exitCode']==0:live('live-debug-focused','Debug','__FOCUSED__',regular)
 first=release(1)
 if first and first['exitCode']==0:
  gate('packages',['python3','scripts/pack-release-packages.py',state['packageOutput'],'0.0.0-ci-test'],'pack');inventory()
 if contracts['exitCode']==0:test('contracts-debug-full','Contracts','Debug',env={'EVENTSTORE_PACKAGE_CONTRACT_DIR':state['packageOutput']},producer='contracts-debug-build + fresh immutable package inventory')
 second=release(2)
 if second and second['exitCode']==0:
  for project in ['Server','DomainService','Client','Contracts']:
   test(project.lower()+'-release-full',project,'Release',env={'EVENTSTORE_PACKAGE_CONTRACT_DIR':state['packageOutput']} if project=='Contracts' else None,producer='release-build-2')
 third=release(3)
 if livebuild['exitCode']==0:live('live-debug-full','Debug','__FULL__',regular)
 if third and third['exitCode']==0:
  live('live-release-full','Release','__FULL__',regular)
  live('live-release-r4096','Release','*NamedProjectionDispatchLiveSidecarTests.SourceGuard_CurrentMatrixUsesExactlyElevenMetadataReadsAndPreservesEveryOwningSurface',regular,4096)
except BaseException as error:
 state['queueFailure']={'type':type(error).__name__,'message':str(error)};save();raise
finally:
 redis=state['ownedResources'].get('redis')
 if redis:
  info=json.loads(subprocess.check_output(['docker','inspect',redis['id']],text=True))[0]
  assert info['Name']=='/'+redis['name'] and info['Config']['Labels'].get('task')=='story64-qualification-20261017'
  result=subprocess.run(['docker','rm','-f','-v',redis['id']],capture_output=True,text=True)
  state['ownedResources']['redis']['cleanup']={'exitCode':result.returncode,'at':c.now(),'inspectAbsent':subprocess.run(['docker','inspect',redis['id']],stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL).returncode!=0}
 state['finishedAt']=c.now();save();print('FINITE_QUALIFICATION_QUEUE_ENDED',flush=True)

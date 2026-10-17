import collections,datetime,hashlib,importlib.util,json,os,pathlib,re,socket,subprocess,sys,tempfile,time,uuid,xml.etree.ElementTree as ET,zipfile
spec=importlib.util.spec_from_file_location('capture','/tmp/story64-qualification-capture.py');c=importlib.util.module_from_spec(spec);spec.loader.exec_module(c)
R=c.ROOT;E=c.EVIDENCE;P=c.PREFIX
old_source=pathlib.Path('/tmp/story64-qualification-queue.py').read_text();definitions=old_source[old_source.index('def save():'):old_source.index('\nsave()\ntry:')];exec(compile(definitions,'/tmp/story64-qualification-queue.py:definitions','exec'))
state=json.loads((E/(P+'gates.json')).read_text())
assert (E/(P+'preliminary-queue-boundary.json')).exists()
oldraw=pathlib.Path('/proc/35037/stat').read_text();assert oldraw[oldraw.rfind(')')+2:].split()[0]=='T'
oldcmd=pathlib.Path('/proc/35037/cmdline').read_bytes().split(b'\0');assert oldcmd[1]==b'/tmp/story64-qualification-queue.py'
assert not pathlib.Path('/proc/35037/task/35037/children').read_text().strip()
archive=E/(P+'gates-before-fallback.json');assert not archive.exists();archive.write_bytes((E/(P+'gates.json')).read_bytes())
preliminary=json.loads((E/(P+'contracts-debug-full.json')).read_text())
if not any(g['name']=='contracts-debug-full' for g in state['gates']):record(preliminary)
state['coordinatorTransitions']=[{'at':c.now(),'oldPid':35037,'newPid':os.getpid(),'oldCoordinatorSha256':c.sha('/tmp/story64-qualification-queue.py'),'newCoordinatorSha256':c.sha(__file__),'boundary':'Completed preliminary Contracts capture; no native child left before old coordinator stop','reason':'MSB4166 parallel restore failure; authorized bounded serialized producer/package fallback; continue unfinished gates only'}]
state['preliminaryPackageOutput']=state['packageOutput'];state['packageOutput']=tempfile.mkdtemp(prefix='story64-qualification-serialized-packages-')
state['additionalAuthorizedGates']=['release-restore-serialized-fallback','release-build-serialized-fallback','packages-serialized-fallback','contracts-debug-full-with-fresh-packages']
state['plannedExecutionOverrides']={'release-build-1':'release-build-serialized-fallback','packages':'packages-serialized-fallback','contracts-debug-full':'contracts-debug-full-with-fresh-packages'}
state['preliminaryFrames']=['contracts-debug-full'];save()
os.kill(35037,9)
print('FINITE_QUEUE_CONTINUATION_PID',os.getpid(),flush=True)
regular={'EVENTSTORE_TEST_REDIS_PORT':state['ownedResources']['redis']['port']}
def serial_release(suffix):
 debug=tempfile.mkdtemp(prefix='story64-qualification-msbuild-'+suffix+'-');env={'MSBUILDDEBUGPATH':debug}
 restore=gate('release-restore-'+suffix,['dotnet','restore','Hexalith.EventStore.slnx','-p:Configuration=Release','-p:UseHexalithProjectReferences=false','-p:UseNuGetDeps=true','-m:1'],'restore',env=env)
 if restore['exitCode']:
  state.setdefault('blocked',[]).append({'gate':'release-build-'+suffix,'reason':'Owning serialized restore failed; no repeated same-blocker restore'});save();return None
 return gate('release-build-'+suffix,['dotnet','build','Hexalith.EventStore.slnx','-c','Release','--no-restore','-p:UseHexalithProjectReferences=false','-p:UseNuGetDeps=true','-m:1'],'build',env=env)
def block(names,reason):
 state.setdefault('blocked',[]).extend({'gate':name,'reason':reason,'nativeTestStarted':False} for name in names);save()
try:
 initial=serial_release('serialized-fallback')
 if not initial or initial['exitCode']:
  block(['packages','contracts-debug-full-with-fresh-packages','server-release-full','domainservice-release-full','client-release-full','contracts-release-full','live-release-full','live-release-r4096'],'Bounded serialized Release producer did not succeed; dependent gate unproven')
  live('live-debug-full','Debug','__FULL__',regular)
 else:
  debug=tempfile.mkdtemp(prefix='story64-qualification-msbuild-pack-')
  packed=gate('packages-serialized-fallback',['python3','/tmp/story64-qualification-pack-serialized.py',state['packageOutput'],'0.0.0-ci-test'],'pack',env={'MSBUILDDEBUGPATH':debug})
  package_inventory=inventory();package_ok=packed['exitCode']==0 and package_inventory['manifestIdsExactlyMatched'] and package_inventory['allEffectiveVersionsCorrect']
  state['freshPackagePrerequisiteSatisfied']=package_ok;save()
  if package_ok:test('contracts-debug-full-with-fresh-packages','Contracts','Debug',env={'EVENTSTORE_PACKAGE_CONTRACT_DIR':state['packageOutput']},producer='contracts-debug-build + packages-serialized-fallback immutable inventory; exact actual execution assets remain captured')
  else:block(['contracts-debug-full-with-fresh-packages','contracts-release-full'],'Fresh complete immutable 14-package inventory unavailable after bounded pack attempt')
  second=serial_release('2')
  if second and second['exitCode']==0:
   for project in ['Server','DomainService','Client','Contracts']:
    if project=='Contracts' and not package_ok:continue
    test(project.lower()+'-release-full',project,'Release',env={'EVENTSTORE_PACKAGE_CONTRACT_DIR':state['packageOutput']} if project=='Contracts' else None,producer='release-build-2')
   third=serial_release('3') if package_ok else second
   if third and third['exitCode']==0:
    selected=E/(P+third['name']+'.json');regular['STORY64_RELEASE_PRODUCER_PACKET']=str(selected);state['selectedFinalReleaseProducer']=str(selected);save()
    live('live-debug-full','Debug','__FULL__',regular)
    live('live-release-full','Release','__FULL__',regular)
    live('live-release-r4096','Release','*NamedProjectionDispatchLiveSidecarTests.SourceGuard_CurrentMatrixUsesExactlyElevenMetadataReadsAndPreservesEveryOwningSurface',regular,4096)
   else:
    block(['live-release-full','live-release-r4096'],'Final coherent Release producer failed; no full native Live cohort on inherited hosts')
    live('live-debug-full','Debug','__FULL__',regular)
  else:
   block(['server-release-full','domainservice-release-full','client-release-full','contracts-release-full','live-release-full','live-release-r4096'],'Post-Contracts coherent serialized Release producer failed')
   live('live-debug-full','Debug','__FULL__',regular)
except BaseException as error:
 state['continuationFailure']={'type':type(error).__name__,'message':str(error)};save();raise
finally:
 redis=state['ownedResources']['redis'];query=subprocess.run(['docker','inspect',redis['id']],capture_output=True,text=True)
 if query.returncode==0:
  info=json.loads(query.stdout)[0];assert info['Name']=='/'+redis['name'] and info['Config']['Labels'].get('task')=='story64-qualification-20261017'
  removed=subprocess.run(['docker','rm','-f','-v',redis['id']],capture_output=True,text=True)
  redis['cleanup']={'exitCode':removed.returncode,'at':c.now(),'inspectAbsent':subprocess.run(['docker','inspect',redis['id']],stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL).returncode!=0}
 state['finishedAt']=c.now();save();print('FINITE_CONTINUED_QUALIFICATION_QUEUE_ENDED',flush=True)

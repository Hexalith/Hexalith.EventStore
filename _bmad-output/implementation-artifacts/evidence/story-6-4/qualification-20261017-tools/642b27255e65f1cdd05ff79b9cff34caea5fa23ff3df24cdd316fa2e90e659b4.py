import datetime,hashlib,json,pathlib,re,shutil,socket,subprocess
B=pathlib.Path('/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/evidence/story-6-4');P='qualification-20261017-'
def sha(p):return hashlib.sha256(pathlib.Path(p).read_bytes()).hexdigest()
def identity(p):return {'path':str(p),'sha256':sha(p)}
def absent_container(cid):return subprocess.run(['docker','inspect',cid],stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL).returncode!=0
def closed(port):
 with socket.socket() as candidate:
  candidate.settimeout(.3);return candidate.connect_ex(('127.0.0.1',int(port)))!=0
state=json.loads((B/(P+'gates.json')).read_text());assert state.get('finishedAt');resources={'capturedAt':datetime.datetime.now(datetime.timezone.utc).isoformat(),'redis':{},'regularControls':[],'oq8':[],'sharedResourcePolicy':'Only exact task-owned resources were removed. Shared Redis/Dapr containers, historical template work and external processes were preserved.'}
redis=state['ownedResources']['redis'];owned=json.loads((B/(P+'owned-resource-identities.json')).read_text())['redis'];resources['redis']={'id':redis['id'],'name':redis['name'],'cleanup':redis.get('cleanup'),'containerAbsent':absent_container(redis['id']),'anonymousVolumes':[]}
for mount in owned['mounts']:
 if mount['Type']=='volume':resources['redis']['anonymousVolumes'].append({'name':mount['Name'],'absent':subprocess.run(['docker','volume','inspect',mount['Name']],stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL).returncode!=0})
for cp in sorted(B.glob(P+'live-*-controls.json')):
 c=json.loads(cp.read_text());work=pathlib.Path(c['work']);assert work.parent==pathlib.Path('/tmp') and work.name.startswith('story64-qualification-controls-') and not work.is_symlink();row={'controlsPacket':identity(cp),'work':str(work),'controlExitCodes':c['infrastructureExitCodes'],'portsClosed':{str(c[k]):closed(c[k]) for k in ['placementPort','schedulerPort']},'pids':[],'retainedLogs':[]}
 for pid in c.get('ownedControlPids',[]):
  proc=pathlib.Path('/proc')/str(pid);cmd=(proc/'cmdline').read_bytes().split(b'\0') if (proc/'cmdline').exists() else [];row['pids'].append({'pid':pid,'exists':proc.exists(),'sameOwnedBinaryStillRunning':any(cmd and cmd[0].decode(errors='replace')==argv[0] for argv in c['commands'])})
 for role in ['placement','scheduler']:
  source=work/(role+'.log');target=B/(cp.stem.removesuffix('-controls')+'-'+role+'.log')
  if source.exists():
   if target.exists():assert target.read_bytes()==source.read_bytes()
   else:target.write_bytes(source.read_bytes())
   row['retainedLogs'].append(identity(target))
 assert all(row['portsClosed'].values()) and not any(x['sameOwnedBinaryStillRunning'] for x in row['pids']),row
 if work.exists():shutil.rmtree(work)
 row['workRemoved']=not work.exists();resources['regularControls'].append(row)
for lane in ['live-debug-full','live-release-full']:
 packet=B/(P+lane+'.json')
 if not packet.exists():continue
 p=json.loads(packet.read_text());env=p['environment'];raw=pathlib.Path(env['HEXALITH_OQ8_EVIDENCE_DIRECTORY']);cleanup=pathlib.Path(env['HEXALITH_OQ8_CLEANUP_PATH']);row={'gate':lane,'nativeEvidenceDirectory':str(raw),'cleanupSource':str(cleanup)}
 if raw.exists():
  for source in raw.glob('*.json'):
   # The fixture explicitly sanitizes emitted JSON; retain original exact bytes.
   target=B/(P+lane+'-oq8-'+source.name);assert not target.exists();target.write_bytes(source.read_bytes());row.setdefault('retainedNativeEvidence',[]).append(identity(target))
 if cleanup.exists():
  data=json.loads(cleanup.read_text());cid=data.pop('postgresContainerId',None) or data.pop('ownedPostgresContainerId',None)
  row.update(nativeCleanupSha256=sha(cleanup),cleanup=data)
  if cid:row['ownedPostgres']={'containerIdSha256':hashlib.sha256(cid.encode()).hexdigest(),'containerAbsent':absent_container(cid)}
  target=B/(P+lane+'-oq8-cleanup.json');assert not target.exists();target.write_text(json.dumps(row,indent=2)+'\n');row['retainedCleanup']=identity(target)
 else:row['cleanupReceiptMissing']=True
 resources['oq8'].append(row)
resources['allRecordedTaskResourcesReleased']=resources['redis']['containerAbsent'] and all(x['absent'] for x in resources['redis']['anonymousVolumes']) and all(x['workRemoved'] for x in resources['regularControls']) and all(not x.get('cleanupReceiptMissing') and x.get('cleanup',{}).get('runtimeScratchRemoved') and x.get('cleanup',{}).get('processesStopped') and x.get('ownedPostgres',{}).get('containerAbsent') for x in resources['oq8'])
out=B/(P+'owned-resource-cleanup.json');assert not out.exists();out.write_text(json.dumps(resources,indent=2)+'\n');print(json.dumps({'allRecordedTaskResourcesReleased':resources['allRecordedTaskResourcesReleased'],'regularLanes':len(resources['regularControls']),'oq8Lanes':len(resources['oq8']),'redisAbsent':resources['redis']['containerAbsent']}))

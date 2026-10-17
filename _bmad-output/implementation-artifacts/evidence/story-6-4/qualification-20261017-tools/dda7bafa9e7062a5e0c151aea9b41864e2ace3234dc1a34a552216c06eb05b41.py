import collections,datetime,hashlib,json,pathlib,re,xml.etree.ElementTree as ET
B=pathlib.Path('/home/administrator/projects/hexalith/eventstore/_bmad-output/implementation-artifacts/evidence/story-6-4');P='qualification-20261017-'
def sha(p):return hashlib.sha256(pathlib.Path(p).read_bytes()).hexdigest()
def identity(p):return {'path':str(p),'sha256':sha(p)}
state=json.loads((B/(P+'gates.json')).read_text());assert state.get('finishedAt'),'Finalize only after the finite queue ends'
frames=[];xmls={};all_observations=[]
for g in state['gates']:
 pp=B/g['packet'];d=json.loads(pp.read_text());log=B/(P+d['name']+'.log');assert sha(log)==d['log']['sha256'];assert d['exitCode']==g['exitCode'];observations=[]
 for n,line in enumerate(log.read_text(errors='replace').splitlines(),1):
  m=re.search(r'^\s*(.*?) \[OUTPUT\] (\w+) (\{.*\})$',line)
  if m:
   try:v=json.loads(m[3])
   except json.JSONDecodeError:continue
   observations.append({'method':m[1],'kind':m[2],'logLine':n,'value':v})
 if d['name'].startswith('live-'):
  native=B/(P+d['name']+'.native.log')
  if native.exists():
   for n,line in enumerate(native.read_text(errors='replace').splitlines(),1):
    m=re.search(r'^\s*(.*?) \[OUTPUT\] (\w+) (\{.*\})$',line)
    if m:
     try:v=json.loads(m[3])
     except json.JSONDecodeError:continue
     observations.append({'method':m[1],'kind':m[2],'nativeLogLine':n,'value':v})
 if observations:
  op=B/(P+d['name']+'-all-observations.json');assert not op.exists();op.write_text(json.dumps({'executionPacket':identity(pp),'outerLog':identity(log),'nativeLog':identity(native) if d['name'].startswith('live-') and native.exists() else None,'observationCounts':dict(collections.Counter(o['kind'] for o in observations)),'observations':observations},indent=2)+'\n');all_observations.append(identity(op))
 x=B/(P+d['name']+'.xml');summary=None;tests=[]
 if x.exists():
  assert sha(x)==d['nativeXml']['sha256'];root=ET.parse(x).getroot();a=root.find('assembly');tests=root.findall('.//test');xmls[d['name']]=tests
  summary={'selected':int(a.get('total')),'passed':int(a.get('passed')),'failed':int(a.get('failed')),'skipped':int(a.get('skipped')),'errors':int(a.get('errors')),'notRun':int(a.get('not-run')),'seconds':float(a.get('time'))};assert summary==d['summary'];assert len(tests)==summary['selected']
 frames.append({'gate':d['name'],'packet':identity(pp),'kind':d['kind'],'command':d['command'],'environment':d['environment'],'exitCode':d['exitCode'],'nativeXmlSummary':summary,'nativeFailedMethods':[t.get('name') for t in tests if t.get('result')=='Fail'],'nativeSkippedMethods':[{'name':t.get('name'),'reason':t.findtext('reason/message')} for t in tests if t.get('result')=='Skip'],'rootHeadBefore':d['before']['graph']['head'],'rootHeadAfter':d['after']['graph']['head'],'moduleGraphUnchanged':d['before']['graph']['modules']==d['after']['graph']['modules'],'sourceChanges':d['sourceInputsChanges'],'assetChanges':d['assetsChanges'],'runtimeChanges':d['runtimeChanges'],'warningLines':d['diagnostics']['warningLines'],'errorLines':d['diagnostics']['errorLines']})
# These are the retained executable selectors from the normative/historical workload map.
selectors={
'W0-current':['GuardedCurrent_ProductionActorsAndStoresMeetExactLogicalReadAndZeroMutationBudget','SourceGuard_CurrentMatrixUsesExactlyElevenMetadataReadsAndPreservesEveryOwningSurface'],
'W0-empty':['SourceBootstrap_EmptyNewOrTouchedSourceRetainsOneSeedWithoutInventingEnvelopes','SourceGuardQualification_ExplicitNewEmptyScopeInitializesZeroWithoutModelOrCompletionReceipt','SourceGuard_QualifiedTouchedEmptySourceStaysUnknownAndCreatesNoReceiptOrDeliveryEffects'],
'W1-long-small-tail':['GuardedTail_ProductionActorStoreAndHandlerCostsStayInsideEveryIndependentCeiling','GuardedThreeRouteTail_ProductionOwnersChargeAllSevenRealPassesWithoutPriorHistory','GuardedTail_ProductionDispatchAndStoreMatchCanonicalReplayWithinSourcePassCeilings'],
'W2-multiple-pages':['GuardedTail_ProductionActorStoreAndHandlerCostsStayInsideEveryIndependentCeiling','GuardedRetryWorker_UsesItsActual256EventRangeProfile'],
'W3-boundaries':['BoundedRange_ChargesArrayFramingAndAtMostOneLookahead','GuardedTail_ProductionActorStoreAndHandlerCostsStayInsideEveryIndependentCeiling'],
'W4-byte-limits':['GuardedMixedLargePayloadTail_ChargesEveryActualLookaheadDecodeAndSizingByte','GuardedProtectedMixedTail_ChargesActualProviderReadabilityAndKeepsCurrentReadOnly','GuardedSourcePass_Exact64MiBAndOneByteOverPreservePreEffectAdmission','GuardedProtectedWirePass_Exact64MiBAndOneByteOverChargeActualDecryptAndSizingBeforeEffects'],
'W4-working-memory':['ProjectionProofWorkingMemoryTests.','ProjectionEventWorkingMemoryTests.'],
'W4-scheduler-occupancy':['DaprProjectionDeliveryRetrySchedulerTests.','ActualGuardAndOwningSchedulerChargeNineFailedActivationsThenResumeTheOriginalPendingReservation'],
'W5-fallback':['ProjectionRebuildProductionPathTests.Legacy','ProjectionReplayRecoveryTests.','ReplayRecovery_GenericHandlerReusesOneOriginalManifestAcrossThreeLostDurableResponses'],
'W5-wider-streamed-compatibility':['SourceStreamedReplayUploadsMoreThan64MiBWithoutRetainingPartBodies','RebuildProjectionAsync_MappedOnlySourceStreamsBeyondLegacy64MiBLimit'],
'W6-transition-and-activation-limits':['GuardedOwningTransitionsExhaustExactlyTheConfiguredCapThenPreserveOriginalWorkAndUseFencedRecovery','ActualGuardAndOwningSchedulerChargeNineFailedActivationsThenResumeTheOriginalPendingReservation'],
'W6-retention-identities':['Compaction_RespectsConfiguredBoundaryAndKeepsCumulativeHead','OwningStorePreservesExactRetainedDuplicatesAndCompleteIdsAcrossRAndRPlusOne'],
'W6-replicas-source-owner-and-crashes':['GuardedOwningCancellationAfterClaimOrReservationPreservesOriginalWorkBeforeAnyHandler','GuardedOwningLostReleaseResponseAfterPendingCasSettlesOriginalCompletionWithoutRedispatch','GuardedTail_AppendAfterCompletionCasBeforeReadbackCompletesFrozenHeadWithoutClaimingCurrent'],
'domain-capability-ABI-shared-owner':['DomainProjectionHandlerCompatibilityTests.','DomainProjectionDispatcherV2Tests.','MixedProjectionCatalogTests.','DomainFencedSharedProjectionIntegrationTests.','DomainPartitionedSharedProjectionRebuildTests.'],
'section6-owning-durability-closure':['ProjectionRebuildProductionPathTests.','ProjectionDeliveryIdempotencyCoordinatorTests.','EventStoreProjectionDeliveryHistoryReaderTests.','ReadModelBatchLiveSidecarTests.']}
coverage={}
for row,patterns in selectors.items():
 coverage[row]={'selectors':patterns,'perFullGate':{}}
 for gate,tests in xmls.items():
  if not gate.endswith('-full'):continue
  matched=[{'name':t.get('name'),'result':t.get('result')} for t in tests if any(p in t.get('name','') for p in patterns)]
  if matched:coverage[row]['perFullGate'][gate]=matched
 coverage[row]['missingSelectorsAcrossFreshFullGates']=[p for p in patterns if not any(p in t.get('name','') for gate,tests in xmls.items() if gate.endswith('-full') for t in tests)]
# The historical exact 25 declarations remain authoritative; compare names to immutable fresh Debug XML.
server_skip_debug={t.get('name') for t in xmls.get('server-debug-full',[]) if t.get('result')=='Skip'};server_skip_release={t.get('name') for t in xmls.get('server-release-full',[]) if t.get('result')=='Skip'}
baseline_sources=json.loads((B/state['gates'][0]['packet']).read_text())['before']['sourceInputs'];source_cross_frame=[];asset_missing=[];direct_bindings=[]
producer_map={'server-debug-focused':'server-debug-build','server-debug-full':'server-debug-build','domain-debug-focused':'domainservice-debug-build','domain-debug-full':'domainservice-debug-build','client-debug-full':'client-debug-build','contracts-debug-full':'contracts-debug-build','contracts-debug-full-with-fresh-packages':'contracts-debug-build','server-release-full':'release-build-2','domainservice-release-full':'release-build-2','client-release-full':'release-build-2','contracts-release-full':'release-build-2','live-debug-focused':'server.livesidecar-debug-build','live-debug-full':'server.livesidecar-debug-build','live-release-full':'release-build-3','live-release-r4096':'release-build-3'}
for g in state['gates']:
 d=json.loads((B/g['packet']).read_text())
 for phase in ['before','after']:
  changes=[name for name in sorted(set(baseline_sources)|set(d[phase]['sourceInputs'])) if baseline_sources.get(name)!=d[phase]['sourceInputs'].get(name)]
  if changes:source_cross_frame.append({'gate':d['name'],'phase':phase,'changes':changes})
  for name,h in d[phase]['assets'].items():
   raw=B/(P+'assets')/(h+'.json')
   if raw.exists():assert sha(raw)==h
   else:asset_missing.append({'gate':d['name'],'phase':phase,'path':name,'sha256':h})
 if d['name'] not in producer_map:continue
 if d['name'].startswith('live-'):
  cp=B/(P+d['name']+'-controls.json')
  if not cp.exists():continue
  target=json.loads(cp.read_text())['testCommand'][1]
 else:target=d['command'][1]
 producer_path=B/(P+producer_map[d['name']]+'.json');produced=json.loads(producer_path.read_text());base=target.removesuffix('.dll');files=[]
 for file in [target,base+'.deps.json',base+'.runtimeconfig.json']:
  consumed=d['before']['runtime'].get(file);expected=produced['after']['runtime'].get(file)
  files.append({'path':file,'consumedBeforeSha256':consumed,'producerAfterSha256':expected,'exactContentMatch':consumed is not None and expected==consumed,'unchangedDuringNativeExecution':consumed is not None and consumed==d['after']['runtime'].get(file),'alreadyPresentWithSameBytesBeforeProducer':consumed is not None and consumed==produced['before']['runtime'].get(file)})
 direct_bindings.append({'gate':d['name'],'producer':identity(producer_path),'directNativeTestInputs':files,'interpretation':'Producer after-snapshot content identity and native stability; unchanged-before bytes may be reused. This does not assert a fresh compile, Release purity, or loading of every captured directory file.'})
live_skip_audit=[]
for lane in ['live-debug-full','live-release-full']:
 tests=xmls.get(lane,[]);skips=[t for t in tests if t.get('result')=='Skip'];parents=[t for t in tests if 'ProjectionWatermarkProcessRestartTests.PersistedEventHistory_RebuildInNewProcess_ConvergesReadModelAndWatermark' in t.get('name','')]
 live_skip_audit.append({'gate':lane,'exactSoleDeclaredWorkerSkip':len(skips)==1 and skips[0].get('name')=='Hexalith.EventStore.Server.LiveSidecar.Tests.Integration.ProjectionWatermarkProcessWorkerTests.ApplyPersistedHistoryAsync','owningRestartResults':[{'name':t.get('name'),'result':t.get('result')} for t in parents]})
result={'capturedAt':datetime.datetime.now(datetime.timezone.utc).isoformat(),'scope':'One finite local qualification pass; original failures/preliminary results are retained and never relabeled. Native passing selectors/numerical observations do not turn an enclosing failed suite green. Source and compiled-content identities are per frame, and captured output directories may contain inherited files.','entryHead':'b99a03dfc29021af16511ee664820e05126b73f8','observedCandidateHeads':sorted(set(f['rootHeadBefore'] for f in frames)|set(f['rootHeadAfter'] for f in frames)),'normativeSha256':'78e453a7af3daca5191731c2a6aff5e631db73d6d91993ca1686b6ebd70b8711','frames':frames,'allObservationInventories':all_observations,'workloadSelectorResults':coverage,'serverExactSkipSetMatchesBetweenConfigurations':server_skip_debug==server_skip_release and len(server_skip_debug)==25,'serverSkipCount':len(server_skip_debug),'coordinatorTransitions':state.get('coordinatorTransitions'),'plannedExecutionOverrides':state.get('plannedExecutionOverrides'),'blocked':state.get('blocked',[]),'preliminaryFrames':state.get('preliminaryFrames',[]),'allSourceSnapshotsStableWithinFrames':all(not f['sourceChanges'] for f in frames),'crossFrameSourceChanges':source_cross_frame,'allCapturedSourceSetsSameAcrossFrames':not source_cross_frame,'assetRawByteArchiveLimitations':asset_missing,'directNativeTestInputBindings':direct_bindings,'liveDeclaredSkipAndOwningRestartAudit':live_skip_audit,'ownedRedisFinal':state['ownedResources']['redis'],'formalAcceptance':'Open: affected failed/unproven gates, observed mixed Release dependencies, authenticated exact-head sealed CI and owner/AD-12/NFR8 assurance are separately required.'}
out=B/(P+'finite-qualification-audit.json');assert not out.exists();out.write_text(json.dumps(result,indent=2)+'\n');print(json.dumps({'frames':len(frames),'nativeTests':[(f['gate'],f['nativeXmlSummary']) for f in frames if f['nativeXmlSummary']],'sourceStable':result['allSourceSnapshotsStableWithinFrames'],'missingSelectors':{r:v['missingSelectorsAcrossFreshFullGates'] for r,v in coverage.items() if v['missingSelectorsAcrossFreshFullGates']}}))

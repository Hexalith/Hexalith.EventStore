#!/usr/bin/env python3
"""Derive selected measured-call timelines from hash-bound final candidate logs."""
import datetime
import hashlib
import json
from pathlib import Path

packet=Path(__file__).resolve().parent
candidate=json.loads((packet/'final-runtime-identity.json').read_text())['candidateSha256']
def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()
def time_of(line):
    return datetime.datetime.fromisoformat(line.split()[0])
def app_events(path):
    events=[]
    for line in path.read_text().splitlines():
        if ' {' not in line:
            continue
        try:
            data=json.loads('{'+line.split(' {',1)[1])
        except ValueError:
            continue
        events.append({'utc':line.split()[0],'message':data.get('Message',''),'state':data.get('State',{}),'eventId':data.get('EventId')})
    return events
results=[]
for path in sorted(packet.glob('scenarios/replica-restart-reused-internal-endpoint-*/manifest.json')):
    manifest=json.loads(path.read_text())
    if manifest['FirstSidecarExecutableSha256']!=candidate:
        continue
    observations=manifest['Observations']
    projection=observations['Topic'].split('.')[1]
    actor=projection+':'+observations['Tenant']
    events=app_events(path.parent/'first.log')
    received=next(event for event in events if event['state'].get('EntityId')=='after-replica-restart' and event['state'].get('ProjectionType')==projection)
    started=next(event for event in events if event['eventId']==1150 and event['state'].get('ActorId')==actor and event['utc']>=received['utc'])
    completed=next(event for event in events if event['eventId']==1151 and event['state'].get('RequestId')==started['state']['RequestId'])
    broadcast=next(event for event in events if event['message'].startswith('SignalR broadcast started.') and event['state'].get('ProjectionType')==projection and event['utc']>=completed['utc'])
    persistence=[event for event in app_events(path.parent/'second.log') if event['message'].startswith('ETag regenerated for actor '+actor+'.') and started['utc']<=event['utc']<=completed['utc']]
    sidecar=[line for line in (path.parent/'first.log').read_text().splitlines() if started['utc']<=line.split()[0]<=completed['utc'] and any(marker in line for marker in ['Evicting unavailable pooled','Recovered processing operation DaprBuiltInActorNotFoundRetries','Error for operation DaprBuiltInActorNotFoundRetries'])]
    elapsed=(datetime.datetime.fromisoformat(completed['utc'])-datetime.datetime.fromisoformat(started['utc'])).total_seconds()*1000
    observed=any('Evicting unavailable pooled' in line for line in sidecar) and any('Recovered processing' in line for line in sidecar)
    results.append({'manifest':path.relative_to(packet).as_posix(),'manifestSha256':sha(path),'firstLogSha256':sha(path.parent/'first.log'),'secondLogSha256':sha(path.parent/'second.log'),'candidateSha256':candidate,'sameInternalPort':observations['OriginalSecondInternalGrpcPort']==observations['RestoredSecondInternalGrpcPort'],'internalPort':observations['RestoredSecondInternalGrpcPort'],'initialOwner':observations['InitialETagActorOwner'],'candidateSelectionCount':observations['ActorOwnerSelectionCandidateCount'],'actor':actor,'requestId':started['state']['RequestId'],'receivedUtc':received['utc'],'invocationStartedUtc':started['utc'],'invocationCompletedUtc':completed['utc'],'broadcastStartedUtc':broadcast['utc'],'invocationElapsedMillisecondsFromCapturedUtc':elapsed,'signalWaitBoundMilliseconds':15000,'restoredActorPostPersistenceUtc':[event['utc'] for event in persistence],'runtimeEvents':sidecar,'observedEvictionAndRecoveryWithinMeasuredInvocation':observed,'independentTcpObservations':observations['IndependentTcpObservations'],'assertionResult':manifest['Result'],'readModelVersion':observations['FinalVersion'],'exactMetadataBoundary':observations['ExactBoundaryAcceptedOnBothHosts']})
assert results and any(result['observedEvictionAndRecoveryWithinMeasuredInvocation'] for result in results)
output=packet/'recovery-timelines.json'
assert not output.exists()
output.write_text(json.dumps({'source':'derived from retained final-candidate first/second logs; no historical channel-field measurement claimed','timelines':results},indent=2)+'\n')
print(json.dumps([{'manifest':result['manifest'],'evictionObserved':result['observedEvictionAndRecoveryWithinMeasuredInvocation'],'elapsedMilliseconds':result['invocationElapsedMillisecondsFromCapturedUtc']} for result in results],indent=2))

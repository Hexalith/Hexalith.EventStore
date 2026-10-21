#!/usr/bin/env python3
"""Derive restart routing observations from immutable, already-redacted fixture logs."""
import hashlib
import json
from pathlib import Path

packet = Path(__file__).resolve().parent
rows = []
for manifest_path in sorted(packet.glob('scenarios/replica-restart*/manifest.json')):
    manifest = json.loads(manifest_path.read_text())
    observed = manifest['Observations']
    tenant = observed.get('Tenant')
    topic = observed.get('Topic')
    if not tenant or not topic:
        continue
    projection = observed.get('Projection') or topic[len(tenant) + 1:].removesuffix('.projection-changed')
    actor = projection + ':' + tenant
    events = []
    for node in ('first', 'second'):
        log = manifest_path.parent / (node + '.log')
        for line in log.read_text().splitlines():
            try:
                content = json.loads(line[line.index('{'):])
            except (ValueError, json.JSONDecodeError):
                if 'Error for operation DaprBuiltInActorNotFoundRetries' in line or 'Recovered processing operation DaprBuiltInActorNotFoundRetries' in line:
                    events.append({'node': node, 'runtimeRetryLine': line})
                continue
            message = content.get('Message', '')
            state = content.get('State', {})
            if state.get('ActorId') == actor or (state.get('ProjectionType') == projection and state.get('TenantId') == tenant):
                if content.get('EventId') in (1050, 1051, 1055, 1090, 1091, 1150, 1151, 1152, 1153, 1154):
                    events.append({'node': node, 'capturePrefix': line[:line.index('{')].strip(),
                                   'eventId': content['EventId'], 'message': message})
    probes = observed.get('IndependentTcpObservations', [])
    rows.append({
        'scenario': manifest['Scenario'], 'result': manifest['Result'],
        'manifest': str(manifest_path.relative_to(packet)),
        'manifestSha256': hashlib.sha256(manifest_path.read_bytes()).hexdigest(),
        'sources': manifest['Sources'], 'binaries': manifest['Binaries'],
        'actor': actor, 'selectedCandidateCount': observed.get('ActorOwnerSelectionCandidateCount'),
        'renewInternalGrpcEndpoint': observed.get('RenewInternalGrpcEndpoint'),
        'originalInternalGrpcPort': observed.get('OriginalSecondInternalGrpcPort'),
        'restoredInternalGrpcPort': observed.get('RestoredSecondInternalGrpcPort'),
        'tcpObservations': probes, 'events': events,
    })
print(json.dumps({'scope': 'routing diagnostics; no production or disposition authority',
                  'historicalFailureLimit': 'Earlier captures lacking a retry error do not establish this exact internal mechanism.',
                  'scenarios': rows}, indent=2))

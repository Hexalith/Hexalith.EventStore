"""EventStore owner qualification orchestration. This module never installs authority.

Role programs are independently installed, hash-pinned transports to the real engine,
registrar and read-only persisted-state observer. They execute the owner plan; they
cannot supply a pass/fail verdict. Only this module evaluates the observations.
"""
from __future__ import annotations

import argparse
import base64
from datetime import date, datetime, timezone
import hashlib
import json
import os
from pathlib import Path
import re
import secrets
import subprocess
import uuid

ROOT = Path(__file__).resolve().parents[1]
PLAN_FILE = ROOT / 'tools/ext-protection-1/plan.json'
ROLE_NAMES = ('executor', 'observer', 'enrollment')
HEX = re.compile(r'[0-9a-f]{64}\Z')
CAPABILITY_FIELDS = set('Issuer Audience TenantId DeletionRequestId DestructionSealId BatchKind BatchOrdinal BatchId ManifestDigest GuardStreamId IntendedIssuedGuardRevision AttestationOrdinal SigningAttemptOrdinal CapabilityKeyVersion'.split())


class Blocked(Exception):
    """Missing or uncertain authority; no successor effect may be attempted."""


class Failed(Exception):
    """An authenticated observation contradicts the required contract."""


def canonical(value):
    return json.dumps(value, sort_keys=True, ensure_ascii=False, separators=(',', ':'), allow_nan=False).encode('utf-8')


def digest(value):
    return hashlib.sha256(value if isinstance(value, bytes) else canonical(value)).hexdigest()


def read_json(path):
    def pairs(items):
        result = {}
        for key, value in items:
            if key in result:
                raise Blocked('DUPLICATE_JSON_MEMBER')
            result[key] = value
        return result
    return json.loads(Path(path).read_bytes(), object_pairs_hook=pairs,
                      parse_constant=lambda _: (_ for _ in ()).throw(Blocked('NONFINITE_JSON')))


def require(condition, code):
    if not condition:
        raise Failed(code)


def durable(path, value):
    """Never overwrite evidence; fsync both original operation and its directory."""
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open('xb') as stream:
        stream.write(canonical(value) + b'\n')
        stream.flush()
        os.fsync(stream.fileno())
    fd = os.open(path.parent, os.O_RDONLY | os.O_DIRECTORY)
    try:
        os.fsync(fd)
    finally:
        os.close(fd)


def accepted_record(register):
    source = Path(register).read_text()
    section = re.search(r'^### EXT-PROTECTION-1 .*?(?=^### |^## |\Z)', source, re.M | re.S)
    if not section:
        raise Blocked('RECORD_MISSING')
    fields = {}
    for line in section.group().splitlines():
        match = re.match(r'^\| `([^`]+)` \| (.*) \|$', line)
        if match:
            fields[match[1]] = match[2].strip('`')
    if fields.get('AcceptedStatus') not in ('Committed', 'Available'):
        raise Blocked('DependencyNotAvailable:EXT-PROTECTION-1')
    if not re.fullmatch(r'[0-9a-f]{40}', fields.get('TargetVersionOrCommit', '')):
        raise Blocked('IMMUTABLE_TARGET_MISSING')
    try:
        date.fromisoformat(fields['TargetIntegrationDate'])
    except (KeyError, ValueError):
        raise Blocked('ACCEPTED_DATE_MISSING') from None
    # An accepted hash lives inside the existing compatibility field, not a tenth schema row.
    pin = re.search(r'ProtectionProfileSha256=([0-9a-f]{64})(?![0-9a-f])', fields.get('CompatibilityContractAndVerificationCommand', ''))
    if not pin:
        raise Blocked('ACCEPTED_PROFILE_PIN_MISSING')
    fields['profileSha256'] = pin[1]
    return fields


def exact_checkout(root, expected):
    def git(*args):
        return subprocess.check_output(['git', '-C', str(root), *args], stderr=subprocess.DEVNULL, text=True).strip()
    if git('rev-parse', 'HEAD') != expected or git('status', '--porcelain', '--untracked-files=all'):
        raise Blocked('EXACT_CLEAN_OWNER_CHECKOUT_REQUIRED')
    if subprocess.run(['git', '-C', str(root), 'merge-base', '--is-ancestor', expected, 'origin/main'],
                      stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL).returncode:
        raise Blocked('TARGET_NOT_REACHABLE_FROM_ORIGIN_MAIN')


def pinned_file(path, expected):
    if not isinstance(path, str) or not Path(path).is_absolute() or not HEX.fullmatch(expected or ''):
        raise Blocked('INVALID_FILE_REFERENCE')
    candidate = Path(path)
    if candidate.is_symlink() or not candidate.is_file() or digest(candidate.read_bytes()) != expected:
        raise Blocked('INSTALLED_FILE_MISSING_OR_CHANGED')
    return candidate


def anchor_identity(path):
    result = subprocess.run(['openssl', 'pkey', '-pubin', '-in', str(path), '-text', '-noout'],
                            capture_output=True, timeout=10)
    if result.returncode or b'prime256v1' not in result.stdout:
        raise Blocked('P256_PUBLIC_ANCHOR_REQUIRED')
    der = subprocess.check_output(['openssl', 'pkey', '-pubin', '-in', str(path), '-outform', 'DER'], stderr=subprocess.DEVNULL, timeout=10)
    return digest(der)


def validate_profile(path, record, plan_sha):
    raw = Path(path).read_bytes()
    if digest(raw) != record['profileSha256']:
        raise Blocked('PROFILE_HASH_MISMATCH')
    profile = read_json(path)
    if set(profile) != {'schema', 'targetCommit', 'planSha256', 'tenantReferences', 'roles', 'engineBinary'} \
            or profile['schema'] != 'ext-protection-1-profile/v1' \
            or profile['targetCommit'] != record['TargetVersionOrCommit'] or profile['planSha256'] != plan_sha:
        raise Blocked('PROFILE_BINDING_MISMATCH')
    tenants = profile['tenantReferences']
    if set(tenants) != {'primary', 'other'} or any(not isinstance(v, str) or not v.strip() for v in tenants.values()) or tenants['primary'] == tenants['other']:
        raise Blocked('DISTINCT_TENANTS_REQUIRED')
    if set(profile['roles']) != set(ROLE_NAMES):
        raise Blocked('INDEPENDENT_ROLES_REQUIRED')
    identities, credentials, anchors = [], [], []
    for role in ROLE_NAMES:
        config = profile['roles'][role]
        if set(config) != {'executable', 'executableSha256', 'publicAnchorFile', 'publicAnchorSha256', 'credentialReference', 'identity', 'installationManifest', 'installationManifestSha256'}:
            raise Blocked('ROLE_REFERENCE_SCHEMA_MISMATCH')
        pinned_file(config['executable'], config['executableSha256'])
        anchor = pinned_file(config['publicAnchorFile'], config['publicAnchorSha256'])
        manifest = read_json(pinned_file(config['installationManifest'], config['installationManifestSha256']))
        if set(manifest) != {'files'} or not manifest['files']:
            raise Blocked('INSTALLATION_INVENTORY_MISSING')
        inventory = {}
        for item in manifest['files']:
            if set(item) != {'path', 'sha256'} or item['path'] in inventory:
                raise Blocked('INSTALLATION_INVENTORY_CONFLICT')
            pinned_file(item['path'], item['sha256'])
            inventory[item['path']] = item['sha256']
        if inventory.get(config['executable']) != config['executableSha256']:
            raise Blocked('EXECUTABLE_NOT_IN_INSTALLATION_INVENTORY')
        credential = config['credentialReference']
        if not re.fullmatch(r'env:[A-Z][A-Z0-9_]*', credential) or not os.environ.get(credential[4:]):
            raise Blocked('CREDENTIAL_REFERENCE_NOT_INSTALLED')
        if not isinstance(config['identity'], str) or not config['identity'].strip():
            raise Blocked('ROLE_IDENTITY_MISSING')
        identities.append(config['identity'])
        credentials.append(credential)
        anchors.append(anchor_identity(anchor))
    if any(len(set(values)) != 3 for values in (identities, credentials, anchors)):
        raise Blocked('ROLE_SEPARATION_MISSING')
    binary = profile['engineBinary']
    if set(binary) != {'path', 'sha256'}:
        raise Blocked('ENGINE_BINARY_REFERENCE_MISSING')
    pinned_file(binary['path'], binary['sha256'])
    return profile


def p1363_der(signature):
    if len(signature) != 64:
        raise Blocked('ES256_SIGNATURE_INVALID')
    def integer(part):
        part = part.lstrip(b'\0') or b'\0'
        if part[0] & 0x80:
            part = b'\0' + part
        return bytes((2, len(part))) + part
    body = integer(signature[:32]) + integer(signature[32:])
    return bytes((48, len(body))) + body


def verify_statement(statement, signature, anchor, directory):
    """Detached ES256 over closed transport statement bytes; private material is never read."""
    try:
        raw = base64.b64decode(signature, validate=True)
    except (ValueError, TypeError):
        raise Blocked('ES256_SIGNATURE_INVALID') from None
    signature_path = Path(directory) / ('signature-' + uuid.uuid4().hex + '.der')
    durable_bytes = p1363_der(raw)
    with signature_path.open('xb') as stream:
        stream.write(durable_bytes)
    result = subprocess.run(['openssl', 'dgst', '-sha256', '-verify', str(anchor), '-signature', str(signature_path)],
                            input=canonical(statement), capture_output=True, timeout=10)
    if result.returncode:
        raise Blocked('SIGNED_ROLE_PROOF_INVALID')


def isolated_environment(credential_reference):
    name = credential_reference.removeprefix('env:')
    if not re.fullmatch(r'[A-Z][A-Z0-9_]*', name) or not os.environ.get(name):
        raise Blocked('CREDENTIAL_REFERENCE_NOT_INSTALLED')
    return {'PATH': os.defpath, 'LANG': 'C.UTF-8', 'LC_ALL': 'C.UTF-8', name: os.environ[name]}


def revalidate_installation(config, engine):
    manifest = read_json(pinned_file(config['installationManifest'], config['installationManifestSha256']))
    for item in manifest['files']:
        pinned_file(item['path'], item['sha256'])
    pinned_file(engine['path'], engine['sha256'])


class Role:
    """Pinned JSON-stdin transport. Roles provide facts, never verifier verdicts."""
    def __init__(self, name, config, binding, directory, engine):
        self.name, self.config, self.binding, self.directory = name, config, binding, Path(directory)
        self.engine = engine

    def request(self, request):
        pinned_file(self.config['executable'], self.config['executableSha256'])
        pinned_file(self.config['publicAnchorFile'], self.config['publicAnchorSha256'])
        revalidate_installation(self.config, self.engine)
        result = subprocess.run([self.config['executable']], input=canonical(request), capture_output=True, timeout=60,
                                env=isolated_environment(self.config['credentialReference']))
        if result.returncode or len(result.stdout) > 16 * 1024 * 1024:
            raise Blocked('ROLE_RESULT_UNCERTAIN')
        try:
            envelope = json.loads(result.stdout)
            if set(envelope) != {'statement', 'signature'}:
                raise ValueError()
            statement = envelope['statement']
            if set(statement) != {'role', 'identity', 'bindingDigest', 'operationId', 'requestSha256', 'response'} \
                    or statement['role'] != self.name or statement['identity'] != self.config['identity'] \
                    or statement['bindingDigest'] != self.binding or statement['operationId'] != request['operationId'] \
                    or statement['requestSha256'] != digest(request):
                raise ValueError()
        except (KeyError, TypeError, ValueError):
            raise Blocked('ROLE_RESULT_UNCERTAIN') from None
        verify_statement(statement, envelope['signature'], self.config['publicAnchorFile'], self.directory)
        return envelope


def response(envelope):
    return envelope['statement']['response']


def lookup_original(role, original, directory):
    request = {'operationId': uuid.uuid4().hex, 'action': 'LookupOriginalOperation',
               'bindingDigest': original['bindingDigest'], 'originalOperationId': original['operationId'],
               'originalRequestSha256': original.get('originalRequestSha256', digest(original)), 'originalRequest': original}
    proof = role.request(request)
    result = response(proof)
    if result.get('originalOperationId') != original['operationId'] or result.get('originalRequestSha256') != original.get('originalRequestSha256', digest(original)) \
            or result.get('status') in (None, 'Unknown', 'Unavailable', 'Pending'):
        raise Blocked('EXACT_ORIGINAL_OUTCOME_UNRESOLVED')
    durable(Path(directory) / (original['operationId'] + '.lookup.json'), proof)
    return proof


def admit_original(registrar, request, directory):
    admission = {'operationId': 'admit-' + request['operationId'], 'action': 'RegisterOriginalOperation',
                 'bindingDigest': request['bindingDigest'], 'originalOperationId': request['operationId'],
                 'originalRequestSha256': digest(request), 'originalRole': request['role']}
    try:
        proof = registrar.request(admission)
    except (Blocked, subprocess.TimeoutExpired, OSError):
        proof = lookup_original(registrar, admission, directory)
    value = response(proof)
    require(value.get('status') == 'Registered' and value.get('registeredOperationId') == request['operationId']
            and value.get('registeredRequestSha256') == digest(request), 'ORIGINAL_OPERATION_NOT_INDEPENDENTLY_RETAINED')
    durable(Path(directory) / (request['operationId'] + '.admission.json'), proof)


def mutate(role, request, directory, registrar=None):
    """Prepared original is durable before dispatch; loss only invokes exact lookup."""
    directory = Path(directory)
    durable(directory / (request['operationId'] + '.request.json'), request)
    if registrar is not None:
        admit_original(registrar, request, directory)
    try:
        proof = role.request(request)
    except (Blocked, subprocess.TimeoutExpired, OSError):
        proof = None
    if proof is None or response(proof).get('status') in (None, 'Unknown', 'Unavailable', 'Pending'):
        proof = lookup_original(role, request, directory)
    durable(directory / (request['operationId'] + '.result.json'), proof)
    return proof


def target_key(target):
    if set(target) != {'TenantId', 'AgentInteractionId', 'TargetProtectionKeyAlias'} or any(not isinstance(v, str) or not v.strip() for v in target.values()):
        raise Failed('TARGET_IDENTITY_INVALID')
    return tuple(target[k] for k in ('TenantId', 'AgentInteractionId', 'TargetProtectionKeyAlias'))


def evaluate(observation, case, targets, canary, commit, binary_sha):
    """Assert raw persisted state and ordered receipts; no aggregate pass booleans."""
    require(observation['engineSourceRevision'] == commit and observation['engineBinarySha256'] == binary_sha, 'ENGINE_IDENTITY_MISMATCH')
    require(observation['agentsRuntimeEnabled'] is False, 'AGENTS_RUNTIME_FORBIDDEN')
    roots = observation['roots']
    require(sorted(target_key(r['target']) for r in roots) == sorted(map(target_key, targets)), 'ROOT_INVENTORY_MISMATCH')
    checks = set(case['checks'])
    states = [r['state'] for r in roots]
    require(all(s in ('Active', 'Pinned', 'Destroyed') for s in states), 'ROOT_STATE_UNKNOWN')
    for record in observation['records']:
        require(record['kind'] in ('source-outbox', 'target-event', 'snapshot', 'cache', 'backup', 'restored'), 'COPY_KIND_UNKNOWN')
        require(target_key(record['target']) in set(map(target_key, targets)), 'COPY_TARGET_SUBSTITUTED')
        raw = base64.b64decode(record['bytesBase64'], validate=True)
        require(raw and canary not in raw, 'PLAINTEXT_PERSISTED')
        protected = json.loads(raw)
        if record['kind'] in ('source-outbox', 'target-event'):
            content = protected['ProtectedContent']
            require(content['Target'] == record['target'] and set(content['Value']) == {'$pdenc'}, 'PROTECTED_ENVELOPE_INVALID')
            encoded = content['Value']['$pdenc']
        else:
            require(protected['Format'] == 'json+pdenc-v2' and protected['SnapshotTypeId'], 'SNAPSHOT_ENVELOPE_INVALID')
            encoded = protected['Envelope']
        envelope = base64.urlsafe_b64decode(encoded + '=' * (-len(encoded) % 4))
        require(len(envelope) >= 83 and envelope[:8] == b'HXP2\x02\x01\x01\x01', 'PROTECTED_ENVELOPE_INVALID')
        require(record['protectedState'] == 'Sealed', 'DISTRIBUTION_NOT_SEALED')
    required_kinds = set()
    if 'SealDirectoryOutbox' in case['steps'] or 'SealAllTargets' in case['steps']:
        required_kinds.add('source-outbox')
    if 'SealTargetEvent' in case['steps'] or 'SealAllTargets' in case['steps']:
        required_kinds.add('target-event')
    if 'SealSnapshotAndCache' in case['steps']:
        required_kinds.update(('snapshot', 'cache'))
    if 'CreateBackup' in case['steps']:
        required_kinds.add('backup')
    if 'RestoreBackup' in case['steps']:
        required_kinds.add('restored')
    expected_pairs = {(target_key(t), kind) for t in targets for kind in required_kinds}
    require(expected_pairs <= {(target_key(r['target']), r['kind']) for r in observation['records']}, 'SOURCE_TARGET_PROOF_MISSING')
    if 'active' in checks:
        require(all(s in ('Active', 'Pinned') for s in states), 'UNEXPECTED_DESTRUCTION')
    if 'erased' in checks:
        require(all(s == 'Destroyed' for s in states), 'INCOMPLETE_ERASURE')
        replays = observation['replays']
        require(expected_pairs <= {(target_key(r['target']), r['kind']) for r in replays}, 'ERASED_REPLAY_MISSING')
        require(all(r['field'] == {'State': 'Erased'} and r['externalOutcome'] == 'Unreadable' for r in replays), 'ERASED_REPLAY_INVALID')
    if 'atomic' in checks:
        require(len(set(states)) == 1, 'PARTIAL_PROVIDER_EFFECT')
    if 'same-root' in checks:
        for target in targets:
            refs = [r['rootReference'] for r in observation['records'] if r['target'] == target]
            require(len(set(refs)) == 1 and bool(refs), 'INTERACTION_ROOT_DIVERGED')
    if 'receipts' in checks:
        receipts = observation['targetReceipts']
        require([r['target'] for r in receipts] == targets, 'ORDERED_RECEIPT_VECTOR_MISMATCH')
        require(len({r['receiptId'] for r in receipts}) == len(targets) and all(r['irreversible'] is True for r in receipts), 'IRREVERSIBLE_RECEIPTS_MISSING')
    if 'no-effect' in checks:
        require(observation['effectRevisionAfterRejectedOperation'] == observation['effectRevisionBeforeRejectedOperation'], 'REJECTED_OPERATION_HAD_EFFECT')
    if 'pin' in checks:
        require([r['status'] for r in observation['pinOutcomes']] == ['Pinned', 'HoldPinned', 'HoldPinned', 'Authorized', 'Unpinned'], 'HOLD_PIN_PROOF_INVALID')
    if 'exact-results' in checks:
        results = observation['exactResults']
        require(len(results) >= 2 and all(canonical(r) == canonical(results[0]) for r in results), 'EXACT_RESULT_DIVERGED')
    if 'copies' in checks or 'restore' in checks:
        inventory = observation['copyInventoryBefore']
        require(inventory and len(inventory) == len(set(inventory)) == len(observation['copyStatesAfter']) and set(inventory) == {r['copyId'] for r in observation['copyStatesAfter']}, 'COPY_INVENTORY_INCOMPLETE')
        if 'erased' in checks:
            require(all(r['readOutcome'] == 'Erased' for r in observation['copyStatesAfter']), 'RESTORE_REVIVED_CONTENT')
    if 'blocked' in checks:
        require(observation['ownerOutcome']['status'] == 'ConsumptionBlocked' and observation['ownerOutcome']['receiptId'], 'AUTHENTICATED_BLOCK_MISSING')
    if 'replacement-blocked' in checks:
        value = observation['activationOutcome']
        require(value['status'] == 'ActivationBlockedByReplacementKeyCompromise' and value['newBlockReceiptId'] \
                and value['newKeyVersion'] == observation['ownerOutcome']['blockedKeyVersion'] \
                and value['revocationRevision'] == observation['ownerOutcome']['revocationRevision'] \
                and observation['ownerOutcome']['status'] == 'ConsumptionBlocked', 'REPLACEMENT_COMPROMISE_NOT_PRESERVED')
    if 'race' in checks and 'hold-order' not in checks:
        events = observation['ownerTransitions']
        winners = [r for r in events if r['to'] in ('ConsumptionReserved', 'ConsumptionBlocked')]
        require(len(winners) == 1 and winners[0]['from'] == 'Unconsumed', 'BLOCK_RESERVE_NOT_SINGLE_WINNER')
        require(all(s == 'Destroyed' for s in states) if winners[0]['to'] == 'ConsumptionReserved' else all(s != 'Destroyed' for s in states), 'RACE_STATE_CONTRADICTION')
    if 'reserved-recovery' in checks:
        require(len({r['reservationReceiptId'] for r in observation['reservationRecoveries']}) == 1, 'RECOVERY_REAUTHORIZED_BATCH')
    if 'capability' in checks or 'receipts' in checks:
        for capability in observation['capabilities']:
            require(set(capability) == CAPABILITY_FIELDS, 'CAPABILITY_EXPIRY_RENEWAL_OR_FIELD_SUBSTITUTION')
            require(capability['TenantId'] == targets[0]['TenantId'], 'CAPABILITY_TENANT_SUBSTITUTION')
        require(bool(observation['capabilities']), 'CAPABILITY_PROOF_MISSING')
        guard = observation['guardDispatch']
        require(guard['soleActiveAttestation'] is True and guard['lookupConsistency'] == 'Linearizable' and guard['dispatchReceiptId'] \
                and guard['issuedGuardRevision'] <= guard['dispatchGuardRevision'] <= guard['observedGuardRevision'], 'ONLINE_DISPATCH_PROOF_MISSING')
    if 'hold-order' in checks:
        require(bool(observation['holdOrders']), 'HOLD_ORDER_PROOF_MISSING')
        for hold in observation['holdOrders']:
            require(hold['holdOperationId'] and hold['dispatchOperationId'], 'HOLD_ORDER_IDENTITY_MISSING')
            require(type(hold['dispatchWon']) is bool and type(hold['holdRevision']) is int
                    and type(hold['dispatchRevision']) is int and hold['holdRevision'] > 0
                    and hold['dispatchRevision'] > 0 and hold['holdRevision'] != hold['dispatchRevision'],
                    'HOLD_ORDER_REVISION_INVALID')
            require((hold['holdRevision'] > hold['dispatchRevision']) == hold['dispatchWon'], 'HOLD_DISPATCH_ORDER_MISMATCH')
        if 'race' in checks:
            # A hold that wins before dispatch never reaches the protection owner's
            # post-dispatch block/reserve race. Do not require a fabricated reservation.
            require(len(observation['holdOrders']) == 1, 'HOLD_RACE_OUTCOME_AMBIGUOUS')
            dispatch_won = observation['holdOrders'][0]['dispatchWon']
            require(all(s == 'Destroyed' for s in states) if dispatch_won else all(s != 'Destroyed' for s in states),
                    'HOLD_RACE_STATE_CONTRADICTION')
    if 'coverage' in checks or 'containment' in checks:
        groups = observation['targetCoverage']
        require(sorted(target_key(g['target']) for g in groups) == sorted(map(target_key, targets)), 'TARGET_COVERAGE_INCOMPLETE')
        require(all(len(set(g['batchIds'])) == 1 and len(set(g['ordinals'])) == 1 for g in groups), 'SAME_TARGET_ALLOCATED_SUCCESSOR_BATCH')
        if 'containment' in checks:
            require(len({g['ordinals'][0] for g in groups}) == len(groups) and all(g['singletonManifest'] and not g['reusesAcceptedSetBatch'] for g in groups), 'CONTAINMENT_ORDINAL_OR_MANIFEST_REUSED')
    if 'already-destroyed' in checks:
        require(observation['coverageOutcome']['status'] == 'AlreadyDestroyedByBatch' \
                and observation['coverageOutcome']['targetReceipts'] == observation['targetReceipts'], 'ORIGINAL_COVERAGE_RECEIPT_LOST')
    if 'reattested' in checks:
        value = observation['reattestation']
        require(value['sameBatch'] is True and value['sameManifest'] is True and value['oldAttestationRevoked'] is True \
                and value['activeCredentialCount'] == 1 and value['compromiseBlockReceiptId'] \
                and value['expectedBlockSetRevision'] == value['activatedBlockSetRevision'], 'RE_ATTESTATION_AUTHORITY_INVALID')
    if 'signing-attempts' in checks:
        attempts = observation['signingAttempts']
        require(len(attempts) >= 3 and len({(a['DestructionSealId'], a['BatchId'], a['ManifestDigest'], a['AttestationOrdinal']) for a in attempts}) == 1, 'SIGNING_SUCCESSOR_CHANGED_LOGICAL_IDENTITY')
        require([a['SigningAttemptOrdinal'] for a in attempts] == list(range(attempts[0]['SigningAttemptOrdinal'], attempts[0]['SigningAttemptOrdinal'] + len(attempts))) \
                and all(a['status'] == 'SignedAttestationObsoleteUnissued' and a['exactNoIssueReceiptId'] for a in attempts[:-1]), 'STALE_SIGNING_NO_ISSUE_PROOF_MISSING')


def rejection_statuses(step, checks):
    if step == 'RejectActivation':
        return {'ActivationBlockedByReplacementKeyCompromise'}
    if step == 'RejectReattestation' or 'blocked' in checks:
        return {'ConsumptionBlocked', 'Conflict'}
    if step in ('RejectManifestDestroy', 'RejectPerKeyDestroy') and 'pin' in checks:
        return {'HoldPinned'}
    if step == 'RejectManifestDestroy' and 'hold-order' in checks:
        return {'HoldBlocked'}
    if step == 'RejectManifestDestroy':
        return {'ProviderUnavailable', 'GuardUnavailable'}
    return {'Conflict', 'Denied'}


def execute(profile, record, plan, attempt, recover=None):
    binding = digest({'target': profile['targetCommit'], 'profile': record['profileSha256'], 'plan': profile['planSha256'],
                      'anchors': {name: profile['roles'][name]['publicAnchorSha256'] for name in ROLE_NAMES}})
    durable(attempt / 'binding.json', {'bindingDigest': binding, 'profileSha256': record['profileSha256'], 'targetCommit': profile['targetCommit'], 'recoveryOf': str(recover) if recover else None})
    roles = {name: Role(name, profile['roles'][name], binding, attempt, profile['engineBinary']) for name in ROLE_NAMES}
    journal = attempt / 'original-operations'
    journal.mkdir()
    if recover:
        old_binding = read_json(Path(recover).parent / 'binding.json')
        if old_binding['bindingDigest'] != binding:
            raise Blocked('RECOVERY_BINDING_CHANGED')
        originals = sorted([*Path(recover).glob('*.request.json'), *Path(recover).glob('*.seal-identity.json')])
        if not originals:
            raise Blocked('ORIGINAL_OPERATION_JOURNAL_EMPTY')
        inventory_request = {'operationId': uuid.uuid4().hex, 'action': 'LookupAttemptOperations', 'bindingDigest': binding,
                             'originalAttemptId': Path(recover).parent.name}
        inventory_proof = roles['enrollment'].request(inventory_request)
        inventory = response(inventory_proof)
        expected = [{'operationId': read_json(p)['operationId'], 'requestSha256': read_json(p).get('originalRequestSha256', digest(read_json(p)))} for p in originals]
        require(inventory.get('originalAttemptId') == Path(recover).parent.name and
                sorted(inventory.get('operationIdentities', []), key=lambda i: i['operationId']) == sorted(expected, key=lambda i: i['operationId']),
                'ORIGINAL_OPERATION_JOURNAL_INCOMPLETE')
        durable(attempt / 'original-attempt-inventory.json', inventory_proof)
        for original_file in originals:
            original = read_json(original_file)
            lookup_original(roles[original['role']], original, journal)
        return
    required = sorted({s for c in plan['cases'] for s in c['steps']})
    installations = {}
    for name in ROLE_NAMES:
        request = {'operationId': uuid.uuid4().hex, 'action': 'InspectInstallation', 'bindingDigest': binding, 'requiredOperations': required}
        proof = roles[name].request(request)
        value = response(proof)
        require(value['agentsRuntimeEnabled'] is False and value['storeIdentity'] and value['deploymentIdentity'], 'INSTALLATION_SCOPE_INVALID')
        installations[name] = {'storeIdentity': value['storeIdentity'], 'deploymentIdentity': value['deploymentIdentity']}
        if name == 'executor':
            require(set(required) <= set(value['supportedOperations']), 'INSTALLED_DRIVER_INCOMPLETE')
        if name == 'observer':
            require(value['credentialScope'] == 'ReadPersistedStateOnly', 'OBSERVER_NOT_INDEPENDENT_READ_ONLY')
        durable(attempt / (name + '-installation.json'), proof)
    require(len({canonical(i) for i in installations.values()}) == 1, 'ROLE_INSTALLATION_MISMATCH')
    enrollment = {'operationId': uuid.uuid4().hex, 'role': 'enrollment', 'action': 'EnrollSingleAttempt', 'bindingDigest': binding, 'attemptId': attempt.name, 'installation': installations['executor']}
    enrolled = response(mutate(roles['enrollment'], enrollment, journal))
    require(enrolled['status'] == 'Enrolled' and enrolled['attemptId'] == attempt.name and enrolled['bindingDigest'] == binding, 'ONE_ATTEMPT_ENROLLMENT_DENIED')
    for case in plan['cases']:
        fixture = attempt.name + '-' + case['id']
        targets = [{'TenantId': profile['tenantReferences']['primary'], 'AgentInteractionId': fixture + '-' + str(i), 'TargetProtectionKeyAlias': 'qualification'} for i in (1, 2)]
        canary = ('protected-canary-' + secrets.token_hex(32)).encode()
        base = {'fixtureId': fixture, 'caseId': case['id'], 'targets': targets, 'otherTenantReference': profile['tenantReferences']['other']}
        prepare = {'operationId': uuid.uuid4().hex, 'role': 'executor', 'action': 'PrepareFixture', 'bindingDigest': binding, **base}
        mutate(roles['executor'], prepare, journal, roles['enrollment'])
        roundtrips = 0
        expected_operations = []
        for index, step in enumerate(case['steps']):
            request = {'operationId': uuid.uuid4().hex, 'role': 'executor', 'action': step, 'bindingDigest': binding, **base}
            # Only synthetic canaries enter this fixture. Destructive requests never contain plaintext.
            if step.startswith('Seal'):
                request['canaryBase64'] = base64.b64encode(canary).decode()
                # Content-bearing seal response loss cannot mint another occurrence; exact lookup uses digest/id.
                safe = {k: v for k, v in request.items() if k != 'canaryBase64'}
                safe['originalRequestSha256'] = digest(request)
                durable(journal / (request['operationId'] + '.seal-identity.json'), safe)
                admit_original(roles['enrollment'], request, journal)
                try:
                    proof = roles['executor'].request(request)
                except (Blocked, subprocess.TimeoutExpired, OSError):
                    proof = lookup_original(roles['executor'], safe, journal)
            elif step.startswith('Unseal'):
                proof = roles['executor'].request(request)
                require(base64.b64decode(response(proof)['canaryBase64'], validate=True) == canary, 'UNSEAL_PAYLOAD_MISMATCH')
                roundtrips += 1
                durable(attempt / (case['id'] + '-' + str(index) + '-roundtrip.json'), {'operationId': request['operationId'], 'equal': True, 'responseSha256': digest(proof)})
            else:
                proof = mutate(roles['executor'], request, journal, roles['enrollment'])
            value = response(proof)
            expected_operations.append({'operationId': request['operationId'], 'requestSha256': digest(request), 'action': step})
            if step.startswith('Reject'):
                require(value['status'] in rejection_statuses(step, case['checks']), 'NEGATIVE_FIXTURE_ACCEPTED')
        if 'roundtrip' in case['checks']:
            require(roundtrips > 0, 'ROUNDTRIP_PROOF_MISSING')
        observed = roles['observer'].request({'operationId': uuid.uuid4().hex, 'action': 'ObservePersistedState', 'bindingDigest': binding, 'expectedOperations': expected_operations, **base})
        require(response(observed)['operationOutcomes'] == expected_operations, 'OBSERVED_OPERATION_IDENTITY_MISMATCH')
        evaluate(response(observed), case, targets, canary, profile['targetCommit'], profile['engineBinary']['sha256'])
        durable(attempt / (case['id'] + '-persisted-observation.json'), observed)
        durable(attempt / (case['id'] + '-assertions.json'), {'case': case['id'], 'checks': case['checks'], 'status': 'PASS'})


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--mode', choices=('Source', 'Live'), default='Source')
    parser.add_argument('--profile', type=Path)
    parser.add_argument('--evidence-root', type=Path)
    parser.add_argument('--recover-journal', type=Path)
    args = parser.parse_args(argv)
    plan = read_json(PLAN_FILE)
    if args.mode == 'Source':
        print(json.dumps({'mode': 'Source', 'cases': len(plan['cases']), 'planSha256': digest(PLAN_FILE.read_bytes()), 'liveExecuted': False}))
        return 0
    if args.profile is None or args.evidence_root is None or not args.profile.is_absolute() or not args.evidence_root.is_absolute():
        parser.error('Live requires absolute --profile and --evidence-root references')
    attempt = args.evidence_root / (datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%SZ') + '-' + uuid.uuid4().hex)
    attempt.mkdir(parents=True, exist_ok=False)
    try:
        record = accepted_record(Path.cwd() / '_bmad-output/planning-artifacts/external-dependency-register.md')
        exact_checkout(ROOT, record['TargetVersionOrCommit'])
        profile = validate_profile(args.profile, record, digest(PLAN_FILE.read_bytes()))
        execute(profile, record, plan, attempt, args.recover_journal)
        status, code = ('RECOVERED', 'EXACT_ORIGINAL_RECOVERED') if args.recover_journal else ('PASS', 'OWNER_COMPONENT_CONTRACT_PASSED')
    except (Blocked, Failed) as exc:
        status, code = ('BLOCKED' if isinstance(exc, Blocked) else 'FAIL'), str(exc)
    except Exception:
        # Provider exception text, stdout/stderr, plaintext and secret values never enter diagnostics.
        status, code = 'BLOCKED', 'INPUT_OR_PROVIDER_UNAVAILABLE'
    durable(attempt / 'result.json', {'status': status, 'code': code, 'recoveryOnly': bool(args.recover_journal), 'qualificationPassed': status == 'PASS'})
    print(json.dumps({'status': status, 'code': code, 'evidenceDirectory': str(attempt)}))
    return 0 if status in ('PASS', 'RECOVERED') else 1

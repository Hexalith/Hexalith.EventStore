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
import hmac
import atexit
import shutil
import subprocess
import tempfile
import time
import uuid

ROOT = Path(__file__).resolve().parents[1]
PLAN_FILE = ROOT / 'tools/ext-protection-1/plan.json'
ROLE_NAMES = ('executor', 'observer', 'enrollment')
HEX = re.compile(r'[0-9a-f]{64}\Z')
CAPABILITY_FIELDS = set('Issuer Audience TenantId DeletionRequestId DestructionSealId BatchKind BatchOrdinal BatchId ManifestDigest GuardStreamId IntendedIssuedGuardRevision AttestationOrdinal SigningAttemptOrdinal CapabilityKeyVersion'.split())
CAPABILITY_NUMBERS = {'BatchOrdinal', 'IntendedIssuedGuardRevision', 'AttestationOrdinal', 'SigningAttemptOrdinal'}
TRUST_FIELDS = set('issuer audience tenantId keyFamily keyVersion publicAnchorId publicAnchorVersion publicAnchorFile publicAnchorSha256 trustProfileRevision'.split())
MAX_EXACT_INTEGER = 9007199254740991
MAX_NATIVE_LONG = 9223372036854775807
MAX_OBSERVATION_BYTES = 16 * 1024 * 1024
OBSERVATION_VERIFICATION_SECONDS = 30


class Blocked(Exception):
    """Missing or uncertain authority; no successor effect may be attempted."""


class ValidatedRoleConfigurations(dict):
    """Runtime-only marker: all shipped dependency/configuration closures passed validate_profile."""


class Failed(Exception):
    """An authenticated observation contradicts the required contract."""


def canonical(value):
    return json.dumps(value, sort_keys=True, ensure_ascii=False, separators=(',', ':'), allow_nan=False).encode('utf-8')


def digest(value):
    return hashlib.sha256(value if isinstance(value, bytes) else canonical(value)).hexdigest()


def strict_json(raw):
    def pairs(items):
        result = {}
        for key, value in items:
            if key in result:
                raise Blocked('DUPLICATE_JSON_MEMBER')
            result[key] = value
        return result
    return json.loads(raw, object_pairs_hook=pairs,
                      parse_constant=lambda _: (_ for _ in ()).throw(Blocked('NONFINITE_JSON')))


def read_json(path):
    return strict_json(Path(path).read_bytes())


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
    platform_pin = re.search(r'ProtectionPlatformTargetCommit=([0-9a-f]{40})(?![0-9a-f])', fields.get('CompatibilityContractAndVerificationCommand', ''))
    if not platform_pin:
        raise Blocked('ACCEPTED_PLATFORM_TARGET_MISSING')
    fields['platformTargetCommit'] = platform_pin[1]
    return fields


def exact_checkout(root, expected):
    def git(*args):
        return subprocess.check_output(['git', '-C', str(root), *args], stderr=subprocess.DEVNULL, text=True).strip()
    if git('rev-parse', 'HEAD') != expected or git('status', '--porcelain', '--untracked-files=all'):
        raise Blocked('EXACT_CLEAN_OWNER_CHECKOUT_REQUIRED')
    if subprocess.run(['git', '-C', str(root), 'merge-base', '--is-ancestor', expected, 'origin/main'],
                      stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL).returncode:
        raise Blocked('TARGET_NOT_REACHABLE_FROM_ORIGIN_MAIN')


def paired_checkout_preflight(eventstore_root, record, profile):
    """Checks both accepted clean local owner revisions without fetching or invoking a role."""
    if Path(profile['platformRoot']).resolve() == Path(eventstore_root).resolve():
        raise Blocked('DISTINCT_PLATFORM_CHECKOUT_REQUIRED')
    exact_checkout(eventstore_root, record['TargetVersionOrCommit'])
    exact_checkout(profile['platformRoot'], profile['platformTargetCommit'])


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


def role_credential_name(reference):
    """A credential name cannot be a CLR, native-loader, shell or machine-runtime control."""
    if not isinstance(reference, str) or not re.fullmatch(r'env:EXT_PROTECTION_ROLE_[A-Z][A-Z0-9_]{0,63}', reference):
        raise Blocked('INERT_ROLE_CREDENTIAL_NAMESPACE_REQUIRED')
    return reference[4:]


def validate_profile(path, record, plan_sha):
    raw = Path(path).read_bytes()
    if digest(raw) != record['profileSha256']:
        raise Blocked('PROFILE_HASH_MISMATCH')
    profile = read_json(path)
    if set(profile) != {'schema', 'targetCommit', 'platformTargetCommit', 'platformRoot', 'roleProgramContract', 'planSha256', 'tenantReferences', 'roles', 'engineBinary', 'capabilityTrust'} \
            or profile['schema'] != 'ext-protection-1-profile/v3' \
            or profile['targetCommit'] != record['TargetVersionOrCommit'] or profile['planSha256'] != plan_sha \
            or not re.fullmatch(r'[0-9a-f]{40}', profile['platformTargetCommit'] or '') \
            or profile['platformTargetCommit'] != record.get('platformTargetCommit') \
            or not isinstance(profile['platformRoot'], str) or not Path(profile['platformRoot']).is_absolute() \
            or profile['roleProgramContract'] != 'ext-protection-1-role-program/v1':
        raise Blocked('PROFILE_BINDING_MISMATCH')
    tenants = profile['tenantReferences']
    if set(tenants) != {'primary', 'other'} or any(not isinstance(v, str) or not v.strip() for v in tenants.values()) or tenants['primary'] == tenants['other']:
        raise Blocked('DISTINCT_TENANTS_REQUIRED')
    if set(profile['roles']) != set(ROLE_NAMES):
        raise Blocked('INDEPENDENT_ROLES_REQUIRED')
    identities, credentials, anchors, credential_values = [], [], [], []
    for role in ROLE_NAMES:
        config = profile['roles'][role]
        if set(config) != {'executable', 'executableSha256', 'publicAnchorFile', 'publicAnchorSha256', 'credentialReference', 'identity', 'installationManifest', 'installationManifestSha256'}:
            raise Blocked('ROLE_REFERENCE_SCHEMA_MISMATCH')
        role_credential_name(config['credentialReference'])
        pinned_file(config['executable'], config['executableSha256'])
        anchor = pinned_file(config['publicAnchorFile'], config['publicAnchorSha256'])
        validate_role_installation(role, config)
        credential = config['credentialReference']
        if not re.fullmatch(r'env:[A-Z][A-Z0-9_]*', credential) or not os.environ.get(credential[4:]):
            raise Blocked('CREDENTIAL_REFERENCE_NOT_INSTALLED')
        if not isinstance(config['identity'], str) or not config['identity'].strip():
            raise Blocked('ROLE_IDENTITY_MISSING')
        identities.append(config['identity'])
        credentials.append(credential)
        validate_role_credential(os.environ[credential[4:]])
        credential_values.append(os.environ[credential[4:]].encode("utf-8"))
        anchors.append(anchor_identity(anchor))
    if any(hmac.compare_digest(credential_values[i], credential_values[j]) for i in range(3) for j in range(i)) \
            or any(len(set(values)) != 3 for values in (identities, credentials, anchors)):
        raise Blocked('ROLE_SEPARATION_MISSING')
    validate_capability_trust(profile['capabilityTrust'], profile['roles'])
    if any(entry['tenantId'] not in tenants.values() for entry in profile['capabilityTrust']):
        raise Blocked('CAPABILITY_TRUST_TENANT_MISMATCH')
    binary = profile['engineBinary']
    if set(binary) != {'path', 'sha256'}:
        raise Blocked('ENGINE_BINARY_REFERENCE_MISSING')
    pinned_file(binary['path'], binary['sha256'])
    profile['roles'] = ValidatedRoleConfigurations(profile['roles'])
    return profile


_RUNTIME_RESOLVER = None


def resolved_application_assets(entry, host, template):
    """Runs only the trusted owner-checkout resolver, never a role or its entry assembly."""
    global _RUNTIME_RESOLVER
    if _RUNTIME_RESOLVER is None:
        directory = Path(tempfile.mkdtemp(prefix='ext-protection-runtime-resolver-'))
        atexit.register(shutil.rmtree, directory, ignore_errors=True)
        source = ROOT / 'tools/ext-protection-1/runtime-closure'
        project = directory / 'RuntimeClosure.csproj'
        shutil.copyfile(source / 'RuntimeClosure.csproj', project)
        shutil.copyfile(source / 'Program.cs', directory / 'Program.cs')
        build = subprocess.run([str(Path(shutil.which('dotnet')).resolve()), 'build', str(project), '-c', 'Debug', '-nodeReuse:false', '--nologo', '-o', str(directory / 'out')],
                               capture_output=True, timeout=120, env={'PATH': os.defpath, 'LANG': 'C.UTF-8', 'DOTNET_CLI_TELEMETRY_OPTOUT': '1'})
        if build.returncode:
            raise Blocked('TRUSTED_RUNTIME_RESOLVER_UNAVAILABLE')
        _RUNTIME_RESOLVER = directory / 'out/RuntimeClosure.dll'
    result = subprocess.run([str(host), str(_RUNTIME_RESOLVER), str(entry), str(template)], capture_output=True, timeout=15,
                            env={'PATH': os.defpath, 'LANG': 'C.UTF-8', 'DOTNET_CLI_TELEMETRY_OPTOUT': '1'})
    if result.returncode or len(result.stdout) > MAX_OBSERVATION_BYTES:
        raise Blocked('APPLICATION_DEPENDENCY_RESOLUTION_FAILED')
    return strict_json(result.stdout)


def validate_role_installation(role, config):
    role_credential_name(config['credentialReference'])
    manifest = read_json(pinned_file(config['installationManifest'], config['installationManifestSha256']))
    if not isinstance(manifest, dict) or set(manifest) != {'schema', 'role', 'entryAssembly', 'files', 'sharedFrameworkBoundary', 'dotnetHost', 'appHostTemplate'} \
            or manifest['schema'] != 'ext-protection-1-role-installation/v2' or role not in ROLE_NAMES or manifest['role'] != role \
            or manifest['sharedFrameworkBoundary'] != 'trusted-machine-dotnet10-sdk-framework-and-os' or not isinstance(manifest['files'], list) \
            or not 1 <= len(manifest['files']) <= 4096:
        raise Blocked('COMPLETE_SHIPPED_ROLE_INSTALLATION_REQUIRED')
    inventory, physical = {}, set()
    for item in manifest['files']:
        if not isinstance(item, dict) or set(item) != {'path', 'sha256'} or item['path'] in inventory:
            raise Blocked('INSTALLATION_INVENTORY_CONFLICT')
        path = pinned_file(item['path'], item['sha256'])
        if str(path) != str(path.resolve()) or str(path.resolve()) in physical or any(part.is_symlink() for part in [path, *path.parents]):
            raise Blocked('INSTALLATION_PATH_OWNERSHIP_INVALID')
        physical.add(str(path.resolve()))
        inventory[str(path)] = item['sha256']
    executable = Path(config['executable'])
    expected_name = 'Hexalith.Platform.Protection.Qualification.' + role.capitalize()
    if executable.name != expected_name or not os.access(executable, os.X_OK) or executable.read_bytes()[:4] != b'\x7fELF' or inventory.get(str(executable)) != config['executableSha256']:
        raise Blocked('SHIPPED_FRAMEWORK_DEPENDENT_APPHOST_REQUIRED')
    entry = executable.with_suffix('.dll')
    deps = executable.with_suffix('.deps.json')
    runtime = executable.with_suffix('.runtimeconfig.json')
    # with_suffix would replace the last dotted name; apphosts have dotted assembly names.
    entry, deps, runtime = (Path(str(executable) + suffix) for suffix in ('.dll', '.deps.json', '.runtimeconfig.json'))
    client_path = executable.parent / ('protection-role.' + role + '.json')
    mandatory = {str(value) for value in (executable, entry, deps, runtime, client_path)}
    if manifest['entryAssembly'] != str(entry) or not mandatory <= set(inventory):
        raise Blocked('APPLICATION_DEPENDENCY_PIN_MISSING')
    options = read_json(runtime).get('runtimeOptions')
    if not isinstance(options, dict) or options.get('tfm') != 'net10.0' or set(options) - {'tfm', 'framework', 'frameworks', 'configProperties', 'rollForward'} \
            or options.get('rollForward', 'LatestPatch') not in ('LatestPatch', 'Minor') or Path(str(executable) + '.runtimeconfig.dev.json').exists():
        raise Blocked('EXTERNAL_RUNTIME_RESOLUTION_UNSUPPORTED')
    properties = options.get('configProperties', {})
    permitted_properties = {'MSTest.EnableParentProcessQuery', 'System.Runtime.Serialization.EnableUnsafeBinaryFormatterSerialization', 'System.Reflection.Metadata.MetadataUpdater.IsSupported', 'System.Globalization.Invariant', 'System.GC.Server'}
    if not isinstance(properties, dict) or set(properties) - permitted_properties or any(type(value) is not bool for value in properties.values()):
        raise Blocked('EXTERNAL_RUNTIME_RESOLUTION_UNSUPPORTED')
    frameworks = options.get('frameworks', [options['framework']] if 'framework' in options else [])
    if not frameworks or any(not isinstance(value, dict) or set(value) != {'name', 'version'} or value['name'] not in ('Microsoft.NETCore.App', 'Microsoft.AspNetCore.App')
                             or not re.fullmatch(r'10\.0\.[0-9]+', value['version']) for value in frameworks):
        raise Blocked('SHARED_FRAMEWORK_BOUNDARY_UNSUPPORTED')
    client = read_json(client_path)
    expected_fields = {'Schema', 'Role', 'Identity', 'Endpoint', 'CredentialReference', 'PublicAnchorFile', 'PublicAnchorSha256'}
    if not isinstance(client, dict) or set(client) != expected_fields or client['Schema'] != 'ext-protection-1-role-program/v1' \
            or client['Role'] != role or client['Identity'] != config['identity'] or client['CredentialReference'] != config['credentialReference'] \
            or client['PublicAnchorFile'] != config['publicAnchorFile'] or client['PublicAnchorSha256'] != config['publicAnchorSha256'] \
            or not isinstance(client['Endpoint'], str) or not re.fullmatch(r'https://[^/?#@]+(?:/[^?#]*)?', client['Endpoint']):
        raise Blocked('ROLE_CONFIGURATION_PIN_MISMATCH')
    mandatory.add(client['PublicAnchorFile'])
    host = Path(manifest['dotnetHost'])
    if str(host) != str(Path(shutil.which('dotnet')).resolve()) or str(host) not in inventory or host.read_bytes()[:4] != b'\x7fELF':
        raise Blocked('TRUSTED_DOTNET_HOST_PIN_REQUIRED')
    mandatory.add(str(host))
    template = Path(manifest['appHostTemplate'])
    if not template.is_relative_to(host.parent) or not re.fullmatch(r'packs/Microsoft\.NETCore\.App\.Host\.linux-x64/10\.0\.[0-9]+/runtimes/linux-x64/native/apphost', str(template.relative_to(host.parent))) or str(template) not in inventory:
        raise Blocked('TRUSTED_APPHOST_TEMPLATE_PIN_REQUIRED')
    mandatory.add(str(template))
    resolved = resolved_application_assets(entry, host, template)
    if not re.fullmatch(r'10\.0\.[0-9]+', resolved.get('runtime', '')) or set(resolved) != {'files', 'runtime'}:
        raise Blocked('SHARED_FRAMEWORK_BOUNDARY_UNSUPPORTED')
    for asset in resolved['files']:
        if not isinstance(asset, str) or not Path(asset).is_relative_to(executable.parent):
            raise Blocked('EXTERNAL_RUNTIME_RESOLUTION_UNSUPPORTED')
        mandatory.add(asset)
    if set(inventory) != mandatory:
        raise Blocked('APPLICATION_DEPENDENCY_PIN_INCOMPLETE_OR_FOREIGN')
    # Recheck accepted files after resolution; Role.request repeats this entire closure at invocation.
    for path, pin in inventory.items():
        pinned_file(path, pin)
    return inventory


def identity_text(value, code='IDENTITY_INVALID', byte_bound=False):
    require(isinstance(value, str) and bool(value.strip()), code)
    try:
        require(len(value.encode('utf-16-be')) <= 4096, code)
        raw = value.encode('utf-8')
        require(not byte_bound or len(raw) <= 2048, code)
    except UnicodeError:
        raise Failed(code) from None
    return value


def ordinal(value, minimum=1, maximum=MAX_EXACT_INTEGER):
    require(type(value) is int and minimum <= value <= maximum, 'ORDINAL_INVALID')
    return value


class VerificationBudget:
    """One monotonic offline verification deadline, including subprocess time."""
    def __init__(self):
        self.deadline = time.monotonic() + OBSERVATION_VERIFICATION_SECONDS
        self.snapshots = []

    def remaining(self):
        remaining = self.deadline - time.monotonic()
        if remaining <= 0:
            raise Blocked('OBSERVATION_VERIFICATION_DEADLINE')
        return min(10, remaining)


def public_pin_bytes(path, expected, budget):
    budget.remaining()
    if not isinstance(path, str) or not Path(path).is_absolute() or not HEX.fullmatch(expected or ''):
        raise Blocked('INVALID_FILE_REFERENCE')
    candidate = Path(path)
    if candidate.is_symlink() or not candidate.is_file() or candidate.stat().st_size > 16384:
        raise Blocked('INSTALLED_FILE_MISSING_OR_CHANGED')
    with candidate.open('rb') as stream:
        raw = stream.read(16385)
    if len(raw) > 16384 or digest(raw) != expected:
        raise Blocked('INSTALLED_FILE_MISSING_OR_CHANGED')
    budget.remaining()
    return raw


class CapabilityTrustSnapshot:
    """Private hash-checked public bytes/index; this installs no signing or online authority."""
    def __init__(self, entries, roles, budget):
        self.budget, self.index, self.pins, self.verified = budget, {}, {}, set()
        budget.snapshots.append(self)
        if not isinstance(entries, list) or not entries or len(entries) > 1000:
            raise Blocked('CAPABILITY_TRUST_MISSING')
        captured = {}

        def capture(path, expected):
            pin = (path, expected)
            if pin not in captured:
                raw = public_pin_bytes(path, expected, budget)
                # Normalize point encoding before comparing actual keys across PEM encodings.
                try:
                    result = subprocess.run(['openssl', 'ec', '-pubin', '-conv_form', 'uncompressed', '-outform', 'DER'],
                                            input=raw, capture_output=True, timeout=budget.remaining())
                except subprocess.TimeoutExpired:
                    raise Blocked('OBSERVATION_VERIFICATION_DEADLINE') from None
                prefix = bytes.fromhex('3059301306072A8648CE3D020106082A8648CE3D03010703420004')
                if result.returncode or len(result.stdout) != 91 or not result.stdout.startswith(prefix):
                    raise Blocked('P256_PUBLIC_ANCHOR_REQUIRED')
                captured[pin] = (raw, digest(result.stdout))
                self.pins[pin] = raw
            return captured[pin]

        transports = {capture(role['publicAnchorFile'], role['publicAnchorSha256'])[1] for role in roles.values()}
        if roles and len(transports) != len(roles):
            raise Blocked('ROLE_SEPARATION_MISSING')
        tenant_keys = {}
        for entry in entries:
            budget.remaining()
            if not isinstance(entry, dict) or set(entry) != TRUST_FIELDS:
                raise Blocked('CAPABILITY_TRUST_SCHEMA_MISMATCH')
            try:
                for name in TRUST_FIELDS - {'trustProfileRevision'}:
                    identity_text(entry[name], 'CAPABILITY_TRUST_IDENTITY_INVALID')
                ordinal(entry['trustProfileRevision'])
            except Failed as exc:
                raise Blocked(str(exc)) from None
            identity = tuple(entry[k] for k in ('issuer', 'audience', 'tenantId', 'keyVersion'))
            if entry['keyFamily'] != 'DeletionBatchCapabilitySigningKey' or identity in self.index:
                raise Blocked('CAPABILITY_TRUST_AMBIGUOUS_OR_WRONG_FAMILY')
            raw, fingerprint = capture(entry['publicAnchorFile'], entry['publicAnchorSha256'])
            if fingerprint in transports:
                raise Blocked('CAPABILITY_TRANSPORT_KEY_SUBSTITUTION')
            if fingerprint in tenant_keys and tenant_keys[fingerprint] != entry['tenantId']:
                raise Blocked('CAPABILITY_CROSS_TENANT_KEY_REUSE')
            tenant_keys[fingerprint] = entry['tenantId']
            self.index[identity] = (dict(entry), raw)

    def recheck_pins(self, budget=None):
        for path, expected in self.pins:
            public_pin_bytes(path, expected, budget or self.budget)


def validate_capability_trust(entries, roles, budget=None):
    snapshot = CapabilityTrustSnapshot(entries, roles, budget or VerificationBudget())
    snapshot.recheck_pins()
    return snapshot


def capability_bytes(capability):
    require(isinstance(capability, dict) and set(capability) == CAPABILITY_FIELDS,
            'CAPABILITY_EXPIRY_RENEWAL_OR_FIELD_SUBSTITUTION')
    for name, value in capability.items():
        if name in CAPABILITY_NUMBERS:
            ordinal(value, 0 if name == 'BatchOrdinal' else 1)
        else:
            identity_text(value, 'CAPABILITY_IDENTITY_INVALID', byte_bound=True)
    return canonical(capability)


def base64url(raw):
    return base64.urlsafe_b64encode(raw).rstrip(b'=').decode('ascii')


def decode_base64url(value):
    require(isinstance(value, str) and bool(re.fullmatch(r'[A-Za-z0-9_-]+', value)), 'JWS_BASE64URL_INVALID')
    try:
        raw = base64.b64decode(value + '=' * (-len(value) % 4), altchars=b'-_', validate=True)
    except ValueError:
        raise Failed('JWS_BASE64URL_INVALID') from None
    require(base64url(raw) == value, 'JWS_BASE64URL_NONCANONICAL')
    return raw


def native_json_string(value, identity=True):
    """Default System.Text.Json/JavaScriptEncoder string bytes, not RFC8785 bytes."""
    if identity:
        identity_text(value, 'TARGET_IDENTITY_INVALID', byte_bound=True)
    else:
        require(isinstance(value, str), 'NATIVE_JSON_STRING_INVALID')
        try:
            value.encode('utf-8')
        except UnicodeError:
            raise Failed('NATIVE_JSON_STRING_INVALID') from None
    result = ['"']
    short = {'\b': '\\b', '\t': '\\t', '\n': '\\n', '\f': '\\f', '\r': '\\r', '\\': '\\\\'}
    for char in value:
        number = ord(char)
        if char in short:
            result.append(short[char])
        elif number < 0x20 or number > 0x7e or char in '"&\'<>+`':
            encoded = char.encode('utf-16-be')
            result.extend('\\u' + format(int.from_bytes(encoded[i:i + 2], 'big'), '04X') for i in range(0, len(encoded), 2))
        else:
            result.append(char)
    return ''.join(result) + '"'


def target_manifest_bytes(targets):
    return ('[' + ','.join('[' + ','.join(native_json_string(value) for value in target_key(target)) + ']'
                           for target in targets) + ']').encode('ascii')


def aggregate_identity(tenant, batch):
    identity_text(tenant, byte_bound=True)
    identity_text(batch, byte_bound=True)
    require(len(tenant) <= 64 and re.fullmatch(r'[a-z0-9](?:[a-z0-9-]*[a-z0-9])?', tenant.lower())
            and len(batch) <= 256 and re.fullmatch(r'[a-zA-Z0-9](?:[a-zA-Z0-9._-]*[a-zA-Z0-9])?', batch), 'AGGREGATE_IDENTITY_INVALID')


def capture_consumption_request(request):
    """Native DeletionConsumptionIdentity.Capture shape/identity, without granting signature authority."""
    require(isinstance(request, dict) and set(request) == set('Capability DetachedJws CommittedIssuedGuardRevision DispatchReceiptId DispatchGuardRevision Targets'.split()),
            'CONSUMPTION_REQUEST_SCHEMA_INVALID')
    payload = request['Capability']
    payload_bytes = capability_bytes(payload)
    aggregate_identity(payload['TenantId'], payload['BatchId'])
    ordinal(request['CommittedIssuedGuardRevision'], maximum=MAX_NATIVE_LONG)
    ordinal(request['DispatchGuardRevision'], maximum=MAX_NATIVE_LONG)
    require(request['DispatchGuardRevision'] >= request['CommittedIssuedGuardRevision'], 'ISSUE_DISPATCH_REVISION_MISMATCH')
    identity_text(request['DispatchReceiptId'], byte_bound=True)
    require(isinstance(request['DetachedJws'], str) and 0 < len(request['DetachedJws'].encode('utf-16-be')) <= 32768, 'DETACHED_JWS_INVALID')
    targets = request['Targets']
    require(isinstance(targets, list) and 1 <= len(targets) <= 1000, 'TARGET_INVENTORY_INVALID')
    keys = [target_key(target) for target in targets]
    require(all(key[0] == payload['TenantId'] for key in keys), 'CAPABILITY_TENANT_SUBSTITUTION')
    order = [(key[1].encode('utf-16-be'), key[2].encode('utf-16-be')) for key in keys]
    require(order == sorted(set(order)), 'TARGET_ORDER_OR_DUPLICATE_INVALID')
    require(payload['ManifestDigest'] == digest(target_manifest_bytes(targets)).upper(), 'NATIVE_MANIFEST_DIGEST_MISMATCH')
    return payload_bytes


def validate_consumption_request(request, trust, roles, snapshot=None):
    capture_consumption_request(request)
    payload = request['Capability']
    require(request['DispatchGuardRevision'] > request['CommittedIssuedGuardRevision'] == payload['IntendedIssuedGuardRevision'] + 1,
            'ISSUE_DISPATCH_REVISION_MISMATCH')
    return validate_capability_signature(payload, request['DetachedJws'], trust, roles, snapshot)


def validate_capability_signature(payload, jws, trust, roles, snapshot=None):
    """Verify the original signed artifact directly; obsolete unissued artifacts have no dispatch."""
    payload_bytes = capability_bytes(payload)
    owned_snapshot = snapshot is None
    snapshot = snapshot or validate_capability_trust(trust, roles)
    snapshot.budget.remaining()
    identity = tuple(payload[k] for k in ('Issuer', 'Audience', 'TenantId', 'CapabilityKeyVersion'))
    require(identity in snapshot.index, 'CAPABILITY_TRUST_NOT_EXACT')
    entry, public_bytes = snapshot.index[identity]
    header = {'alg': 'ES256', 'anchorVersion': entry['publicAnchorVersion'], 'keyVersion': entry['keyVersion'],
              'kid': entry['publicAnchorId'], 'typ': 'DeletionBatchCapabilityV1'}
    require(isinstance(jws, str) and len(jws) <= 16384 and len(jws.split('.')) == 3, 'DETACHED_JWS_INVALID')
    encoded, middle, signature = jws.split('.')
    # Parsing is duplicate-safe, and byte equality rejects reordered, extra or noncanonical headers.
    require(not middle and strict_json(decode_base64url(encoded)) == header and encoded == base64url(canonical(header)), 'CAPABILITY_HEADER_SUBSTITUTION')
    raw = decode_base64url(signature)
    require(len(raw) == 64, 'ES256_SIGNATURE_INVALID')
    cache_key = (identity, payload_bytes, jws)
    if cache_key not in snapshot.verified:
        with tempfile.TemporaryDirectory(prefix='ext-protection-capability-') as temporary:
            signature_path, public_path = Path(temporary) / 'signature.der', Path(temporary) / 'public.pem'
            signature_path.write_bytes(p1363_der(raw))
            public_path.write_bytes(public_bytes)
            try:
                result = subprocess.run(['openssl', 'dgst', '-sha256', '-verify', str(public_path), '-signature', str(signature_path)],
                                        input=(encoded + '.' + base64url(payload_bytes)).encode('ascii'), capture_output=True, timeout=snapshot.budget.remaining())
            except subprocess.TimeoutExpired:
                raise Blocked('OBSERVATION_VERIFICATION_DEADLINE') from None
        require(result.returncode == 0, 'CAPABILITY_SIGNATURE_INVALID')
        snapshot.verified.add(cache_key)
    snapshot.budget.remaining()
    if owned_snapshot:
        snapshot.recheck_pins()
    return digest(payload_bytes).upper(), digest(jws.encode('utf-8')).upper()


def native_record(value, fields, code):
    require(isinstance(value, dict) and set(value) == set(fields.split()), code)
    return value


def native_owner_outcome(value, status):
    native_record(value, 'TenantId BatchId Status OwnerRevision KeyBlockSetRevision ReceiptId BlockReason BlockedKeyVersion RevocationRevision TargetReceipts', 'TERMINAL_RESULT_SCHEMA_INVALID')
    aggregate_identity(value['TenantId'], value['BatchId'])
    ordinal(value['OwnerRevision'], maximum=MAX_NATIVE_LONG)
    ordinal(value['KeyBlockSetRevision'], 0, value['OwnerRevision'])
    identity_text(value['ReceiptId'], byte_bound=True)
    require(type(value['Status']) is int and value['Status'] == status and value['BlockReason'] is None
            and value['BlockedKeyVersion'] is None and value['RevocationRevision'] is None, 'TERMINAL_OWNER_STATE_INVALID')
    return value


def retained_owner_batches(observation, requests, outcomes, snapshot):
    """Match actual private DeletionConsumptionBatch records, not a fictional reservation API."""
    expected = dict(requests)
    expected_outcomes = dict(outcomes)
    coverage = observation.get('coverageRequest')
    if coverage is not None:
        capture_consumption_request(coverage)
        coverage_id = coverage['Capability']['BatchId']
        require(coverage_id not in expected, 'COVERAGE_BATCH_NOT_DISTINCT')
        expected[coverage_id] = coverage
        expected_outcomes[coverage_id] = native_owner_outcome(observation.get('coverageOutcome'), 4)
    values = observation.get('ownerBatches')
    require(isinstance(values, list) and len(values) == len(expected), 'RETAINED_OWNER_BATCHES_MISSING')
    logical = 'TenantId Issuer Audience DeletionRequestId DestructionSealId BatchKind BatchOrdinal BatchId ManifestDigest GuardStreamId'.split()
    seen = set()
    for value in values:
        snapshot.budget.remaining()
        native_record(value, 'Original Current Outcome BlockedReplacement', 'RETAINED_OWNER_BATCH_SCHEMA_INVALID')
        current, original = value['Current'], value['Original']
        capture_consumption_request(current)
        batch_id = current['Capability']['BatchId']
        require(batch_id in expected and batch_id not in seen, 'RETAINED_OWNER_BATCH_AMBIGUOUS')
        seen.add(batch_id)
        require(canonical(current) == canonical(expected[batch_id]) and canonical(value['Outcome']) == canonical(expected_outcomes[batch_id])
                and value['BlockedReplacement'] is None, 'RETAINED_OWNER_REQUEST_OR_RESERVATION_MISMATCH')
        if coverage is not None and batch_id == coverage['Capability']['BatchId']:
            capture_consumption_request(original)
        else:
            validate_consumption_request(original, None, None, snapshot)
        require(canonical({key: original['Capability'][key] for key in logical}) == canonical({key: current['Capability'][key] for key in logical})
                and canonical(original['Targets']) == canonical(current['Targets'])
                and original['Capability']['AttestationOrdinal'] <= current['Capability']['AttestationOrdinal'], 'RETAINED_ORIGINAL_BATCH_IDENTITY_CHANGED')
        if original['Capability']['AttestationOrdinal'] == current['Capability']['AttestationOrdinal']:
            require(canonical(original) == canonical(current), 'RETAINED_ORIGINAL_ATTESTATION_CHANGED')


def original_evidence(observation, targets, trust, roles, consumed=False, budget=None, snapshot=None):
    """Correlate original contract records; transport signatures do not prove currentness."""
    requests = observation.get('consumptionRequests')
    require(isinstance(requests, list) and 1 <= len(requests) <= 1000, 'CONSUMPTION_REQUESTS_MISSING')
    snapshot = snapshot or validate_capability_trust(trust, roles, budget)
    batches = observation.get('guardBatches')
    receipts = observation.get('guardReceipts')
    require(isinstance(batches, list) and len(batches) == len(requests) and isinstance(receipts, list) and len(receipts) <= 2000, 'GUARD_ORIGINALS_MISSING')
    batch_map, receipt_map = {}, {}
    for batch in batches:
        native_record(batch, 'BatchId Kind Ordinal Targets ManifestDigest AttestationOrdinal CapabilityKeyVersion AttestationDigest IssueReceiptId DispatchReceiptId ProtectionOutcome ProtectionReceiptId TargetReceiptIds BlockSetRevision CoveredResourceIds Capability DetachedJws SigningRequestId IssuedGuardRevision DispatchGuardRevision', 'GUARD_BATCH_SCHEMA_INVALID')
        identity_text(batch['BatchId'], byte_bound=True)
        ordinal(batch['BlockSetRevision'], 0, MAX_NATIVE_LONG)
        require(batch['BatchId'] not in batch_map, 'GUARD_BATCH_AMBIGUOUS')
        batch_map[batch['BatchId']] = batch
    for receipt in receipts:
        native_record(receipt, 'OperationId IntentDigest Status GuardHighWater AcceptedAtAdmissionFenceOrdinal ReferenceId ReceiptId AdmissionAttributionsJson AcceptedWriteResourceId AcceptedWriteFacts AcceptedTargetMutationDigest Capability SigningRequestId DetachedJwsDigest', 'GUARD_RECEIPT_SCHEMA_INVALID')
        identity_text(receipt['ReceiptId'], byte_bound=True)
        identity_text(receipt['OperationId'], byte_bound=True)
        ordinal(receipt['GuardHighWater'], maximum=MAX_NATIVE_LONG)
        require(receipt['ReceiptId'] not in receipt_map, 'GUARD_RECEIPT_AMBIGUOUS')
        receipt_map[receipt['ReceiptId']] = receipt
    request_map, covered, operations = {}, [], {}
    for request in requests:
        snapshot.budget.remaining()
        signing_id, signature_digest = validate_consumption_request(request, None, None, snapshot)
        capability = request['Capability']
        batch_id = capability['BatchId']
        require(batch_id not in request_map and batch_id in batch_map, 'REQUEST_BATCH_AMBIGUOUS_OR_MISSING')
        request_map[batch_id] = request
        covered.extend(target_key(target) for target in request['Targets'])
        batch = batch_map[batch_id]
        require(capability_bytes(batch['Capability']) == capability_bytes(capability) and batch['DetachedJws'] == request['DetachedJws']
                and batch['SigningRequestId'] == signing_id and batch['AttestationDigest'] == signature_digest
                and batch['Targets'] == request['Targets'] and batch['ManifestDigest'] == capability['ManifestDigest']
                and batch['Kind'] == capability['BatchKind'] and type(batch['Ordinal']) is int and batch['Ordinal'] == capability['BatchOrdinal']
                and type(batch['AttestationOrdinal']) is int and batch['AttestationOrdinal'] == capability['AttestationOrdinal']
                and batch['CapabilityKeyVersion'] == capability['CapabilityKeyVersion'], 'GUARD_BATCH_IDENTITY_MISMATCH')
        ordinal(batch['IssuedGuardRevision'], maximum=MAX_NATIVE_LONG)
        ordinal(batch['DispatchGuardRevision'], maximum=MAX_NATIVE_LONG)
        identity_text(batch['IssueReceiptId'], byte_bound=True)
        identity_text(batch['DispatchReceiptId'], byte_bound=True)
        require(batch['IssuedGuardRevision'] == request['CommittedIssuedGuardRevision']
                and batch['DispatchGuardRevision'] == request['DispatchGuardRevision']
                and batch['DispatchReceiptId'] == request['DispatchReceiptId']
                and batch['IssueReceiptId'] != batch['DispatchReceiptId'], 'GUARD_REVISION_OR_RECEIPT_MISMATCH')
        require((batch['Kind'] == 'AcceptedSet' and batch['Ordinal'] == 0)
                or (batch['Kind'] == 'Containment' and batch['Ordinal'] > 0 and len(batch['Targets']) == 1), 'BATCH_KIND_OR_ORDINAL_INVALID')
        originals = []
        for key, revision in (('IssueReceiptId', batch['IssuedGuardRevision']), ('DispatchReceiptId', batch['DispatchGuardRevision'])):
            require(batch[key] in receipt_map, 'GUARD_ORIGINAL_RECEIPT_MISSING')
            receipt = receipt_map[batch[key]]
            operation = (capability['GuardStreamId'], receipt['OperationId'])
            raw_receipt = canonical(receipt)
            require(operation not in operations or operations[operation] == raw_receipt, 'GUARD_OPERATION_ORIGINAL_CONFLICT')
            operations[operation] = raw_receipt
            require(receipt['Status'] == 'Committed' and receipt['ReferenceId'] == batch_id
                    and receipt['GuardHighWater'] == revision and capability_bytes(receipt['Capability']) == capability_bytes(capability)
                    and receipt['SigningRequestId'] == signing_id and receipt['DetachedJwsDigest'] == signature_digest,
                    'GUARD_ORIGINAL_RECEIPT_MISMATCH')
            originals.append(receipt)
        require(originals[0]['OperationId'] != originals[1]['OperationId'], 'ISSUE_DISPATCH_OPERATION_REUSED')
    require(len(covered) == len(set(covered)) and set(covered) == set(map(target_key, targets)), 'CONSUMPTION_TARGET_INVENTORY_MISMATCH')
    if not consumed:
        snapshot.recheck_pins()
        return request_map, []
    owner_fields = 'TenantId BatchId Status OwnerRevision KeyBlockSetRevision ReceiptId BlockReason BlockedKeyVersion RevocationRevision TargetReceipts'
    provider_fields = 'TenantId BatchId ReservationReceiptId State TargetReceipts'
    owners, providers, all_receipts = {}, {}, []
    for field, mapping, fields, state_field, state in (
            ('ownerResults', owners, owner_fields, 'Status', 3),
            ('providerResults', providers, provider_fields, 'State', 1)):
        values = observation.get(field)
        require(isinstance(values, list) and len(values) == len(requests), 'TERMINAL_ORIGINALS_MISSING')
        for value in values:
            native_record(value, fields, 'TERMINAL_RESULT_SCHEMA_INVALID')
            batch_id = value['BatchId']
            require(batch_id in request_map and batch_id not in mapping, 'TERMINAL_BATCH_AMBIGUOUS_OR_SUBSTITUTED')
            request = request_map[batch_id]
            require(value['TenantId'] == request['Capability']['TenantId'] and type(value[state_field]) is int
                    and value[state_field] == state, 'TERMINAL_RESULT_NOT_CONSUMED')
            vector = value['TargetReceipts']
            require(isinstance(vector, list) and len(vector) == len(request['Targets']), 'ORDERED_RECEIPT_VECTOR_MISMATCH')
            ids = []
            for receipt, target in zip(vector, request['Targets']):
                native_record(receipt, 'Target OriginalBatchId ReceiptId', 'TARGET_RECEIPT_SCHEMA_INVALID')
                identity_text(receipt['ReceiptId'], byte_bound=True)
                identity_text(receipt['OriginalBatchId'], byte_bound=True)
                require(receipt['Target'] == target and receipt['OriginalBatchId'] == batch_id, 'ORIGINAL_TARGET_RECEIPT_SUBSTITUTED')
                ids.append(receipt['ReceiptId'])
            require(len(set(ids)) == len(ids), 'TARGET_RECEIPT_ID_REUSED')
            if field == 'ownerResults':
                native_owner_outcome(value, 3)
            else:
                identity_text(value['ReservationReceiptId'], byte_bound=True)
            mapping[batch_id] = value
    for batch_id, owner in owners.items():
        provider = providers[batch_id]
        require(provider['ReservationReceiptId'] == owner['ReceiptId'] and provider['TargetReceipts'] == owner['TargetReceipts'],
                'PROVIDER_OWNER_ORIGINAL_VECTOR_MISMATCH')
        batch = batch_map[batch_id]
        identity_text(batch['ProtectionReceiptId'], byte_bound=True)
        require(batch['ProtectionOutcome'] == 'Consumed' and batch['ProtectionReceiptId'] == owner['ReceiptId']
                and batch['TargetReceiptIds'] == [receipt['ReceiptId'] for receipt in owner['TargetReceipts']]
                and batch['BlockSetRevision'] <= owner['KeyBlockSetRevision'], 'GUARD_TERMINAL_VECTOR_MISMATCH')
        all_receipts.extend(owner['TargetReceipts'])
    retained_owner_batches(observation, request_map, owners, snapshot)
    # These are exact native originals returned by the indicated terminal APIs, not labels.
    for field, originals in (('ownerRetryResults', owners), ('ownerLookupResults', owners), ('providerLookupResults', providers)):
        values = observation.get(field)
        require(isinstance(values, list) and len(requests) <= len(values) <= 3000, 'TERMINAL_RETRY_LOOKUP_MISSING')
        seen = set()
        for value in values:
            require(isinstance(value, dict) and value.get('BatchId') in originals
                    and canonical(value) == canonical(originals[value['BatchId']]), 'TERMINAL_RETRY_LOOKUP_DIVERGED')
            seen.add(value['BatchId'])
        require(seen == set(request_map), 'TERMINAL_RETRY_LOOKUP_INCOMPLETE')
    snapshot.recheck_pins()
    return request_map, sorted(all_receipts, key=lambda receipt: tuple(value.encode('utf-16-be') for value in target_key(receipt['Target'])))



def native_json_bytes(value):
    """Default native record serialization in its declared property order, after closed-schema validation."""
    if value is None:
        return b'null'
    if type(value) is bool:
        return b'true' if value else b'false'
    if isinstance(value, str):
        return native_json_string(value, identity=False).encode('ascii')
    if type(value) is int:
        return str(value).encode('ascii')
    if isinstance(value, list):
        return b'[' + b','.join(native_json_bytes(item) for item in value) + b']'
    require(isinstance(value, dict), 'NATIVE_JSON_VALUE_INVALID')
    return b'{' + b','.join(native_json_bytes(key) + b':' + native_json_bytes(item) for key, item in value.items()) + b'}'


def native_issue_intent(value):
    """Read the retained native intent bytes themselves; receipt summaries cannot supply a missing command."""
    native_record(value, 'BytesBase64', 'NO_ISSUE_INTENT_ORIGINAL_SCHEMA_INVALID')
    require(isinstance(value['BytesBase64'], str), 'NO_ISSUE_INTENT_BYTES_INVALID')
    try:
        raw = base64.b64decode(value['BytesBase64'], validate=True)
    except ValueError:
        raise Failed('NO_ISSUE_INTENT_BYTES_INVALID') from None
    require(0 < len(raw) <= MAX_OBSERVATION_BYTES and base64.b64encode(raw).decode('ascii') == value['BytesBase64'], 'NO_ISSUE_INTENT_BYTES_INVALID')
    try:
        intent = strict_json(raw)
    except (ValueError, UnicodeError):
        raise Failed('NO_ISSUE_INTENT_BYTES_INVALID') from None
    native_record(intent, 'Transition TargetMutationDigest', 'NO_ISSUE_INTENT_ORIGINAL_SCHEMA_INVALID')
    command_fields = 'TenantId OperationId Operation ExpectedGuardRevision EpochId DeletionRequestId Scope Write Repair Ordinal Hold Batch AuthorizationId ReferenceId BridgeOriginal RevocationReceipt'.split()
    batch_fields = 'BatchId BatchKind BatchOrdinal Targets ManifestDigest AttestationOrdinal CapabilityKeyVersion AttestationDigest ProtectionOutcome ProtectionReceiptId TargetReceiptIds BlockSetRevision Capability DetachedJws SigningRequestId'.split()
    capability_fields = 'Issuer Audience TenantId DeletionRequestId DestructionSealId BatchKind BatchOrdinal BatchId ManifestDigest GuardStreamId IntendedIssuedGuardRevision AttestationOrdinal SigningAttemptOrdinal CapabilityKeyVersion'.split()
    command = native_record(intent['Transition'], ' '.join(command_fields), 'NO_ISSUE_COMMAND_SCHEMA_INVALID')
    batch = native_record(command['Batch'], ' '.join(batch_fields), 'NO_ISSUE_BATCH_COMMAND_SCHEMA_INVALID')
    capability_bytes(batch['Capability'])
    require(isinstance(batch['Targets'], list) and 1 <= len(batch['Targets']) <= 1000, 'NO_ISSUE_COMMAND_TARGETS_INVALID')
    for target in batch['Targets']:
        target_key(target)
    require(type(command['Operation']) is int and command['Operation'] in (18, 19, 23), 'NO_ISSUE_COMMAND_OPERATION_INVALID')
    ordinal(command['ExpectedGuardRevision'], maximum=MAX_NATIVE_LONG)
    ordinal(batch['BatchOrdinal'], 0)
    ordinal(batch['AttestationOrdinal'])
    ordinal(batch['BlockSetRevision'], 0, MAX_NATIVE_LONG)
    require(all(command[key] is None for key in ('Scope', 'Write', 'Repair', 'Ordinal', 'Hold', 'BridgeOriginal', 'RevocationReceipt'))
            and command['EpochId'] == '' and command['ReferenceId'] == ''
            and batch['ProtectionOutcome'] == '' and batch['ProtectionReceiptId'] == '' and batch['TargetReceiptIds'] == [], 'NO_ISSUE_COMMAND_UNRELATED_EFFECT')
    if command['Operation'] == 19:
        identity_text(command['AuthorizationId'], 'NO_ISSUE_COMMAND_AUTHORIZATION_INVALID')
    else:
        require(command['AuthorizationId'] == '', 'NO_ISSUE_COMMAND_AUTHORIZATION_INVALID')
    require(intent['TargetMutationDigest'] == digest(b'[]').upper(), 'NO_ISSUE_COMMAND_TARGET_MUTATION_INVALID')
    # Reorder the supplied native record only to verify its exact original serializer bytes.
    ordered = {key: command[key] for key in command_fields}
    ordered_batch = {key: batch[key] for key in batch_fields}
    ordered_batch['Capability'] = {key: batch['Capability'][key] for key in capability_fields}
    ordered_batch['Targets'] = [{key: target[key] for key in ('TenantId', 'AgentInteractionId', 'TargetProtectionKeyAlias')} for target in batch['Targets']]
    ordered['Batch'] = ordered_batch
    require(raw == native_json_bytes({'Transition': ordered, 'TargetMutationDigest': intent['TargetMutationDigest']}), 'NO_ISSUE_INTENT_NATIVE_BYTES_MISMATCH')
    return command, digest(raw).upper()


def native_timestamp(value):
    """Exact DateTimeOffset-shaped historical instants in 100 ns ticks, without asserting a live lease."""
    require(isinstance(value, str), 'SIGNING_TIMESTAMP_INVALID')
    match = re.fullmatch(r'(\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2})(?:\.(\d{1,7}))?(Z|[+-]\d{2}:\d{2})', value)
    require(match is not None, 'SIGNING_TIMESTAMP_INVALID')
    try:
        zone = match[3]
        if zone != 'Z':
            hours, minutes = map(int, zone[1:].split(':'))
            require(hours <= 14 and minutes < 60 and (hours < 14 or minutes == 0), 'SIGNING_TIMESTAMP_INVALID')
        instant = datetime.fromisoformat(match[1] + ('+00:00' if zone == 'Z' else zone))
        delta = instant - datetime(1, 1, 1, tzinfo=timezone.utc)
        ticks = (delta.days * 86400 + delta.seconds) * 10000000 + int((match[2] or '').ljust(7, '0'))
        require(0 < ticks <= 3155378975999999999, 'SIGNING_TIMESTAMP_INVALID')
        return ticks
    except (ValueError, OverflowError):
        raise Failed('SIGNING_TIMESTAMP_INVALID') from None


def signing_attempt_evidence(observation, requests, snapshot):
    """Native signed originals plus exact terminal no-issue and fresh successor bases; no invented dispatches."""
    summaries, originals = observation.get('signingAttempts'), observation.get('signingOriginals')
    no_issues, recoveries, intents = observation.get('noIssueOriginals'), observation.get('signingRecoveries'), observation.get('noIssueIntents')
    require(isinstance(summaries, list) and 3 <= len(summaries) <= 1000
            and isinstance(originals, list) and len(originals) == len(summaries)
            and isinstance(no_issues, list) and len(no_issues) == len(summaries) - 1
            and isinstance(recoveries, list) and len(recoveries) == len(no_issues)
            and isinstance(intents, list) and len(intents) == len(no_issues), 'SIGNING_NATIVE_ORIGINALS_MISSING')
    original_fields = set('SigningRequestId Payload State DetachedJws PublicAnchorId PublicAnchorVersion'.split())
    summary_fields = 'DestructionSealId BatchId ManifestDigest AttestationOrdinal SigningAttemptOrdinal status exactNoIssueReceiptId'
    receipt_fields = 'OperationId IntentDigest Status GuardHighWater AcceptedAtAdmissionFenceOrdinal ReferenceId ReceiptId AdmissionAttributionsJson AcceptedWriteResourceId AcceptedWriteFacts AcceptedTargetMutationDigest Capability SigningRequestId DetachedJwsDigest'
    proof_fields = 'Payload SigningRequestId DetachedJwsDigest ProofId CurrentGuardRevision CurrentHealthyKeyVersion AuthorityRevision ObservedAt ValidUntil'
    recovery_fields = proof_fields.replace('ProofId', 'NoIssueReceiptId')
    by_attempt, receipt_map, recovery_map, receipt_signing_ids, intent_map = {}, {}, {}, set(), {}
    for original in originals:
        snapshot.budget.remaining()
        require(isinstance(original, dict) and original_fields <= set(original) <= original_fields | {'NoIssueProof'}, 'SIGNING_OUTCOME_SCHEMA_INVALID')
        payload = original['Payload']
        signing_id, signature_digest = validate_capability_signature(payload, original['DetachedJws'], None, None, snapshot)
        require(original['SigningRequestId'] == signing_id, 'SIGNING_ORIGINAL_IDENTITY_MISMATCH')
        entry = snapshot.index[tuple(payload[key] for key in ('Issuer', 'Audience', 'TenantId', 'CapabilityKeyVersion'))][0]
        require(original['PublicAnchorId'] == entry['publicAnchorId'] and original['PublicAnchorVersion'] == entry['publicAnchorVersion'], 'SIGNING_ORIGINAL_ANCHOR_MISMATCH')
        require(type(original['State']) is int and original['State'] in (0, 5), 'SIGNING_ORIGINAL_STATE_INVALID')
        attempt = payload['SigningAttemptOrdinal']
        require(attempt not in by_attempt, 'SIGNING_ORIGINAL_AMBIGUOUS')
        by_attempt[attempt] = (original, signing_id, signature_digest)
    for receipt in no_issues:
        native_record(receipt, receipt_fields, 'NO_ISSUE_ORIGINAL_SCHEMA_INVALID')
        identity_text(receipt['ReceiptId'], byte_bound=True)
        identity_text(receipt['OperationId'], byte_bound=True)
        require(isinstance(receipt['IntentDigest'], str) and re.fullmatch(r'[A-F0-9]{64}', receipt['IntentDigest']), 'NO_ISSUE_INTENT_INVALID')
        ordinal(receipt['GuardHighWater'], maximum=MAX_NATIVE_LONG)
        ordinal(receipt['AcceptedAtAdmissionFenceOrdinal'], 0, MAX_NATIVE_LONG)
        identity_text(receipt['SigningRequestId'])
        require(receipt['ReceiptId'] not in receipt_map and receipt['SigningRequestId'] not in receipt_signing_ids, 'NO_ISSUE_ORIGINAL_AMBIGUOUS')
        receipt_map[receipt['ReceiptId']] = receipt
        receipt_signing_ids.add(receipt['SigningRequestId'])
    for recovery in recoveries:
        native_record(recovery, recovery_fields, 'SIGNING_RECOVERY_SCHEMA_INVALID')
        identity_text(recovery['NoIssueReceiptId'], byte_bound=True)
        require(recovery['NoIssueReceiptId'] not in recovery_map, 'SIGNING_RECOVERY_AMBIGUOUS')
        recovery_map[recovery['NoIssueReceiptId']] = recovery
    for value in intents:
        snapshot.budget.remaining()
        command, intent_digest = native_issue_intent(value)
        identity_text(command['OperationId'], byte_bound=True)
        require(command['OperationId'] not in intent_map, 'NO_ISSUE_INTENT_ORIGINAL_AMBIGUOUS')
        intent_map[command['OperationId']] = (command, intent_digest)
    # Independently observed guard and no-issue inventories must retain one immutable
    # result for every receipt/operation identity, even when an old attempt never issued.
    inventory_receipts, inventory_operations = {}, {}
    for receipt in [*observation['guardReceipts'], *no_issues]:
        snapshot.budget.remaining()
        native_record(receipt, receipt_fields, 'GUARD_RECEIPT_SCHEMA_INVALID')
        identity_text(receipt['ReceiptId'], byte_bound=True)
        identity_text(receipt['OperationId'], byte_bound=True)
        for field, inventory in (('ReceiptId', inventory_receipts), ('OperationId', inventory_operations)):
            identity = receipt[field]
            original = canonical(receipt)
            require(identity not in inventory or inventory[identity] == original, 'SIGNING_GUARD_ORIGINAL_IDENTITY_REUSED')
            inventory[identity] = original
    ordered, used_receipts, used_recoveries = [], set(), set()
    immutable = CAPABILITY_FIELDS - {'SigningAttemptOrdinal', 'IntendedIssuedGuardRevision', 'CapabilityKeyVersion'}
    original_observations = []
    for index, summary in enumerate(summaries):
        snapshot.budget.remaining()
        native_record(summary, summary_fields, 'SIGNING_SUMMARY_SCHEMA_INVALID')
        attempt = ordinal(summary['SigningAttemptOrdinal'])
        ordinal(summary['AttestationOrdinal'])
        require(attempt in by_attempt and (not ordered or attempt == ordered[-1][0]['Payload']['SigningAttemptOrdinal'] + 1), 'SIGNING_ATTEMPT_ORDER_INVALID')
        original, signing_id, signature_digest = by_attempt[attempt]
        payload = original['Payload']
        require(all(summary[key] == payload[key] for key in ('DestructionSealId', 'BatchId', 'ManifestDigest', 'AttestationOrdinal')), 'SIGNING_SUMMARY_ORIGINAL_MISMATCH')
        if ordered:
            previous = ordered[-1][0]['Payload']
            require(all(payload[key] == previous[key] for key in immutable), 'SIGNING_SUCCESSOR_CHANGED_LOGICAL_IDENTITY')
        ordered.append((original, signing_id, signature_digest))
        if index == len(summaries) - 1:
            require(original['State'] == 0 and original.get('NoIssueProof') is None and summary['status'] == 'Signed'
                    and summary['exactNoIssueReceiptId'] is None, 'FINAL_SIGNING_ORIGINAL_INVALID')
            require(payload['BatchId'] in requests and capability_bytes(payload) == capability_bytes(requests[payload['BatchId']]['Capability'])
                    and original['DetachedJws'] == requests[payload['BatchId']]['DetachedJws'], 'FINAL_SIGNING_CONSUMPTION_MISMATCH')
            continue
        require(original['State'] == 5 and summary['status'] == 'SignedAttestationObsoleteUnissued', 'STALE_SIGNING_NO_ISSUE_PROOF_MISSING')
        proof = native_record(original.get('NoIssueProof'), proof_fields, 'NO_ISSUE_PROOF_SCHEMA_INVALID')
        identity_text(proof['ProofId'], byte_bound=True)
        require(summary['exactNoIssueReceiptId'] == proof['ProofId'] and proof['ProofId'] in receipt_map
                and proof['ProofId'] in recovery_map and proof['ProofId'] not in used_receipts, 'NO_ISSUE_ORIGINAL_IDENTITY_MISMATCH')
        receipt, recovery = receipt_map[proof['ProofId']], recovery_map[proof['ProofId']]
        used_receipts.add(proof['ProofId'])
        used_recoveries.add(proof['ProofId'])
        for current in (proof, recovery):
            require(capability_bytes(current['Payload']) == capability_bytes(payload) and current['SigningRequestId'] == signing_id
                    and current['DetachedJwsDigest'] == signature_digest, 'SIGNING_PROOF_ORIGINAL_MISMATCH')
            ordinal(current['CurrentGuardRevision'], maximum=MAX_NATIVE_LONG)
            identity_text(current['CurrentHealthyKeyVersion'])
            identity_text(current['AuthorityRevision'])
            require(current['CurrentGuardRevision'] > payload['IntendedIssuedGuardRevision'], 'SIGNING_SUCCESSOR_REVISION_INVALID')
        observed, valid_until = native_timestamp(proof['ObservedAt']), native_timestamp(proof['ValidUntil'])
        recovered, recovery_until = native_timestamp(recovery['ObservedAt']), native_timestamp(recovery['ValidUntil'])
        require(observed < valid_until and recovered < recovery_until and recovered >= observed
                and recovery['CurrentGuardRevision'] >= proof['CurrentGuardRevision'], 'SIGNING_PROOF_OBSERVATION_INVALID')
        original_observations.append((observed, recovered))
        issue_operation = 'issue-' + digest(('[' + ','.join(native_json_string(value) for value in (signing_id, signature_digest)) + ']').encode('ascii')).upper()
        require(receipt['OperationId'] == issue_operation and receipt['ReceiptId'] == recovery['NoIssueReceiptId']
                and receipt['SigningRequestId'] == signing_id and receipt['DetachedJwsDigest'] == signature_digest
                and capability_bytes(receipt['Capability']) == capability_bytes(payload)
                and receipt['Status'] in ('Stale', 'IssuanceStale')
                and receipt['ReferenceId'] == ('' if receipt['Status'] == 'Stale' else payload['BatchId'])
                and receipt['GuardHighWater'] <= proof['CurrentGuardRevision']
                and (receipt['Status'] != 'Stale' or receipt['AcceptedAtAdmissionFenceOrdinal'] == 0)
                and receipt['AdmissionAttributionsJson'] == '[]' and receipt['AcceptedWriteResourceId'] == ''
                and receipt['AcceptedWriteFacts'] is None and receipt['AcceptedTargetMutationDigest'] == '', 'NO_ISSUE_GUARD_ORIGINAL_MISMATCH')
        require(receipt['Status'] != 'IssuanceStale' or receipt['AcceptedAtAdmissionFenceOrdinal'] > 0, 'NO_ISSUE_DELETION_ORDINAL_INVALID')
        require(issue_operation in intent_map, 'NO_ISSUE_INTENT_ORIGINAL_MISSING')
        command, intent_digest = intent_map[issue_operation]
        carrier = command['Batch']
        require(receipt['IntentDigest'] == intent_digest, 'NO_ISSUE_INTENT_DIGEST_MISMATCH')
        require(command['TenantId'] == payload['TenantId'] and command['DeletionRequestId'] == payload['DeletionRequestId']
                and command['ExpectedGuardRevision'] == payload['IntendedIssuedGuardRevision']
                and carrier['SigningRequestId'] == signing_id and carrier['DetachedJws'] == original['DetachedJws']
                and carrier['AttestationDigest'] == signature_digest and capability_bytes(carrier['Capability']) == capability_bytes(payload)
                and payload['BatchId'] in requests and canonical(carrier['Targets']) == canonical(requests[payload['BatchId']]['Targets'])
                and all(carrier[key] == payload[key] for key in ('BatchId', 'BatchKind', 'BatchOrdinal', 'ManifestDigest', 'AttestationOrdinal', 'CapabilityKeyVersion'))
                and (command['Operation'] != 18 or payload['AttestationOrdinal'] == 1 and carrier['BlockSetRevision'] == 0)
                and (command['Operation'] != 23 or payload['AttestationOrdinal'] > 1)
                and (receipt['Status'] != 'IssuanceStale' or command['Operation'] == 19 and command['ExpectedGuardRevision'] == receipt['GuardHighWater'])
                and (receipt['Status'] != 'Stale' or command['ExpectedGuardRevision'] != receipt['GuardHighWater']), 'NO_ISSUE_COMMAND_BINDING_MISMATCH')
        for retained in observation['guardReceipts']:
            if retained['Status'] == 'Committed':
                require(retained['SigningRequestId'] != signing_id and retained['DetachedJwsDigest'] != signature_digest
                        and retained['OperationId'] != issue_operation
                        and (retained['Capability'] is None or capability_bytes(retained['Capability']) != capability_bytes(payload)), 'OBSOLETE_SIGNING_ATTEMPT_ISSUED')
        next_summary = summaries[index + 1]
        require(isinstance(next_summary, dict) and type(next_summary.get('SigningAttemptOrdinal')) is int
                and next_summary['SigningAttemptOrdinal'] in by_attempt, 'SIGNING_ATTEMPT_ORDER_INVALID')
        successor = by_attempt[next_summary['SigningAttemptOrdinal']][0]['Payload']
        require(successor['IntendedIssuedGuardRevision'] == recovery['CurrentGuardRevision']
                and successor['CapabilityKeyVersion'] == recovery['CurrentHealthyKeyVersion'], 'SIGNING_RECOVERY_SUCCESSOR_MISMATCH')
    require(len(by_attempt) == len(ordered) and used_receipts == set(receipt_map) and used_recoveries == set(recovery_map)
            and set(intent_map) == {receipt['OperationId'] for receipt in no_issues}, 'SIGNING_ORIGINAL_INVENTORY_MISMATCH')
    require(all(following[0] >= previous[1] for previous, following in zip(original_observations, original_observations[1:])), 'SIGNING_PROOF_OBSERVATION_INVALID')
    snapshot.recheck_pins()

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


def validate_role_credential(value):
    if not isinstance(value, str) or not value or any(ord(char) < 0x21 or ord(char) > 0x7E for char in value) or len('Bearer ' + value) > 8192:
        raise Blocked('CREDENTIAL_REFERENCE_NOT_INSTALLED')
    return value


def qualification_platform_root(eventstore_root):
    root = Path(eventstore_root)
    project = 'Hexalith.Platform.Protection.Qualification.Executor'
    declared = root / 'references' / 'Hexalith.Platform'
    if (declared / 'src' / project / (project + '.csproj')).is_file():
        return declared
    sibling = root.parent / 'platform'
    if (sibling / 'src' / project / (project + '.csproj')).is_file():
        return sibling
    raise Blocked('QUALIFICATION_PLATFORM_CHECKOUT_MISSING')


def isolated_environment(credential_reference, dotnet_root=None, accepted_configuration_sha256=None):
    name = role_credential_name(credential_reference)
    if not re.fullmatch(r'[A-Z][A-Z0-9_]*', name) or not os.environ.get(name):
        raise Blocked('CREDENTIAL_REFERENCE_NOT_INSTALLED')
    environment = {'PATH': os.defpath, 'LANG': 'C.UTF-8', 'LC_ALL': 'C.UTF-8', name: os.environ[name]}
    if dotnet_root is not None:
        environment['DOTNET_ROOT'] = str(dotnet_root)
    if accepted_configuration_sha256 is not None:
        if not isinstance(accepted_configuration_sha256, str) or not re.fullmatch(r'[0-9a-fA-F]{64}', accepted_configuration_sha256):
            raise Blocked('ROLE_CONFIGURATION_PIN_MISMATCH')
        environment['EXT_PROTECTION_ACCEPTED_ROLE_CONFIGURATION_SHA256'] = accepted_configuration_sha256
    return environment


def accepted_role_configuration(config, role, inventory):
    client = Path(config['executable']).parent / ('protection-role.' + role + '.json')
    if isinstance(inventory, dict):
        pin = inventory.get(str(client))
        if not isinstance(pin, str) or not re.fullmatch(r'[0-9a-fA-F]{64}', pin):
            raise Blocked('ROLE_CONFIGURATION_PIN_MISMATCH')
        return pin
    if client.is_file() and not client.is_symlink():
        return digest(client.read_bytes())
    return None


def revalidate_installation(config, engine):
    role = read_json(config['installationManifest']).get('role')
    inventory = validate_role_installation(role, config)
    pinned_file(engine['path'], engine['sha256'])
    return inventory


class Role:
    """Pinned JSON-stdin transport. Roles provide facts, never verifier verdicts."""
    def __init__(self, name, config, binding, directory, engine, capability_trust, role_configs, trust_snapshot=None):
        self.name, self.config, self.binding, self.directory = name, config, binding, Path(directory)
        self.engine = engine
        self.capability_trust, self.role_configs = capability_trust, role_configs
        self.trust_snapshot = trust_snapshot or validate_capability_trust(capability_trust, role_configs)

    def request(self, request):
        role_credential_name(self.config['credentialReference'])
        pinned_file(self.config['executable'], self.config['executableSha256'])
        pinned_file(self.config['publicAnchorFile'], self.config['publicAnchorSha256'])
        inventory = revalidate_installation(self.config, self.engine)
        self.trust_snapshot.recheck_pins(VerificationBudget())
        result = subprocess.run([self.config['executable']], input=canonical(request), capture_output=True, timeout=60,
                                env=isolated_environment(self.config['credentialReference'], Path(read_json(self.config['installationManifest'])['dotnetHost']).parent,
                                                         accepted_role_configuration(self.config, self.name, inventory)))
        if result.returncode or len(result.stdout) > 16 * 1024 * 1024:
            raise Blocked('ROLE_RESULT_UNCERTAIN')
        try:
            envelope = strict_json(result.stdout)
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
    if not isinstance(target, dict) or set(target) != {'TenantId', 'AgentInteractionId', 'TargetProtectionKeyAlias'}:
        raise Failed('TARGET_IDENTITY_INVALID')
    for value in target.values():
        identity_text(value, 'TARGET_IDENTITY_INVALID', byte_bound=True)
    return tuple(target[k] for k in ('TenantId', 'AgentInteractionId', 'TargetProtectionKeyAlias'))


def bind_hold_originals(observation):
    """Reject a hold summary whose native guard receipts were substituted."""
    receipts = observation.get('guardReceipts')
    require(isinstance(receipts, list) and receipts, 'HOLD_ORIGINAL_MISSING')
    fields = 'OperationId IntentDigest Status GuardHighWater AcceptedAtAdmissionFenceOrdinal ReferenceId ReceiptId AdmissionAttributionsJson AcceptedWriteResourceId AcceptedWriteFacts AcceptedTargetMutationDigest Capability SigningRequestId DetachedJwsDigest'
    parsed, seen = [], set()
    for receipt in receipts:
        native_record(receipt, fields, 'HOLD_ORIGINAL_SCHEMA_INVALID')
        identity_text(receipt['ReceiptId'], byte_bound=True)
        identity_text(receipt['OperationId'], byte_bound=True)
        ordinal(receipt['GuardHighWater'], maximum=MAX_NATIVE_LONG)
        require(receipt['ReceiptId'] not in seen, 'HOLD_ORIGINAL_AMBIGUOUS')
        seen.add(receipt['ReceiptId'])
        parsed.append(receipt)
    for hold in observation['holdOrders']:
        holds = [receipt for receipt in parsed if receipt['OperationId'] == hold['holdOperationId']
                 and receipt['GuardHighWater'] == hold['holdRevision'] and receipt['Status'] == 'Committed']
        dispatches = [receipt for receipt in parsed if receipt['OperationId'] == hold['dispatchOperationId']
                      and receipt['GuardHighWater'] == hold['dispatchRevision'] and receipt['Status'] == 'Committed']
        require(len(holds) == 1, 'HOLD_ORIGINAL_SUBSTITUTED')
        if hold.get('dispatchStatus', 'Committed') == 'Committed':
            require(len(dispatches) == 1 and holds[0]['ReceiptId'] != dispatches[0]['ReceiptId'], 'HOLD_ORIGINAL_SUBSTITUTED')
            require((holds[0]['GuardHighWater'] > dispatches[0]['GuardHighWater']) == hold['dispatchWon'], 'HOLD_ORIGINAL_SUBSTITUTED')
            continue
        # A hold-winning guard compare has no committed dispatch receipt. Require the
        # independent receiving owner's exact persisted attempt, rather than invent one.
        require(hold['dispatchStatus'] in ('Blocked', 'Stale') and hold['dispatchWon'] is False and not dispatches, 'HOLD_REFUSAL_INVALID')
        attempts = observation.get('guardAttempts')
        require(isinstance(attempts, list) and attempts, 'HOLD_ATTEMPT_ORIGINAL_MISSING')
        candidates = []
        for attempt in attempts:
            native_record(attempt, 'Transition TargetMutationDigest BeforeRevision AfterRevision Receipt', 'HOLD_ATTEMPT_SCHEMA_INVALID')
            transition, receipt = attempt['Transition'], attempt['Receipt']
            native_record(transition, 'TenantId OperationId Operation ExpectedGuardRevision EpochId DeletionRequestId Scope Write Repair Ordinal Hold Batch AuthorizationId ReferenceId BridgeOriginal RevocationReceipt', 'HOLD_ATTEMPT_SCHEMA_INVALID')
            native_record(receipt, fields, 'HOLD_ATTEMPT_SCHEMA_INVALID')
            ordinal(attempt['BeforeRevision'], maximum=MAX_NATIVE_LONG)
            ordinal(attempt['AfterRevision'], attempt['BeforeRevision'], MAX_NATIVE_LONG)
            ordinal(transition.get('ExpectedGuardRevision'), maximum=MAX_NATIVE_LONG)
            require(isinstance(attempt['TargetMutationDigest'], str) and re.fullmatch('[A-F0-9]{64}', attempt['TargetMutationDigest']), 'HOLD_ATTEMPT_INTENT_INVALID')
            intent = hashlib.sha256(native_json_bytes({'Transition': transition, 'TargetMutationDigest': attempt['TargetMutationDigest']})).hexdigest().upper()
            require(receipt['IntentDigest'] == intent and receipt['OperationId'] == transition.get('OperationId')
                    and attempt['BeforeRevision'] <= receipt['GuardHighWater'] <= attempt['AfterRevision'], 'HOLD_ATTEMPT_INTENT_INVALID')
            if receipt['OperationId'] == hold['dispatchOperationId']:
                candidates.append(attempt)
        require(len(candidates) == 1, 'HOLD_ATTEMPT_ORIGINAL_AMBIGUOUS')
        attempt = candidates[0]
        transition, receipt = attempt['Transition'], attempt['Receipt']
        require(type(transition.get('Operation')) is int and transition['Operation'] == 21
                and receipt['Status'] == hold['dispatchStatus'] and receipt['GuardHighWater'] == hold['dispatchRevision']
                and hold['holdRevision'] <= receipt['GuardHighWater'] and transition['ExpectedGuardRevision'] <= hold['holdRevision']
                and (receipt['Status'] != 'Stale' or transition['ExpectedGuardRevision'] < receipt['GuardHighWater'])
                and (receipt['Status'] != 'Blocked' or transition['ExpectedGuardRevision'] == receipt['GuardHighWater']), 'HOLD_REFUSAL_ORDER_INVALID')


def bind_hold_scope(observation, targets, trust):
    """Correlate actual outer guard commands and current native hold/deletion scope with their independently signed complete request."""
    attempts, contexts = observation.get('guardAttempts'), observation.get('holdContexts')
    require(isinstance(attempts, list) and attempts and isinstance(contexts, list) and len(contexts) == len(observation['holdOrders']), 'HOLD_SCOPE_ORIGINAL_MISSING')
    tenant = targets[0]['TenantId']
    require(all(target['TenantId'] == tenant for target in targets), 'HOLD_CASE_TENANT_INVALID')
    for order in observation['holdOrders']:
        holds = [item for item in attempts if item['Transition']['OperationId'] == order['holdOperationId'] and item['Receipt']['Status'] == 'Committed']
        dispatches = [item for item in attempts if item['Transition']['OperationId'] == order['dispatchOperationId']]
        selected = [item for item in contexts if item.get('HoldOperationId') == order['holdOperationId']]
        require(len(holds) == len(dispatches) == len(selected) == 1, 'HOLD_SCOPE_ORIGINAL_AMBIGUOUS')
        hold, dispatch, context = holds[0], dispatches[0], selected[0]
        native_record(context, 'HoldOperationId DeletionRequestId Scope Hold SealId', 'HOLD_SCOPE_CONTEXT_INVALID')
        command, hold_command = dispatch['Transition'], hold['Transition']
        batch = command['Batch']
        require(isinstance(batch, dict), 'HOLD_DISPATCH_BATCH_INVALID')
        signing, signature = validate_capability_signature(batch['Capability'], batch['DetachedJws'], None, None, trust)
        cap = batch['Capability']
        scope, native_hold = context['Scope'], context['Hold']
        native_record(scope, 'TenantId Kind AgentInteractionId SourceConversationId', 'HOLD_SCOPE_CONTEXT_INVALID')
        native_record(native_hold, 'HoldId Scope DispositionVersion Active PostStartAttempt ReleaseReceiptId', 'HOLD_SCOPE_CONTEXT_INVALID')
        require(scope['TenantId'] == tenant and scope['Kind'] in ('ExactInteraction', 'SourceDeletionExactConversation')
                and ((scope['Kind'] == 'ExactInteraction' and scope['AgentInteractionId'] and not scope['SourceConversationId'])
                     or (scope['Kind'] == 'SourceDeletionExactConversation' and scope['SourceConversationId'] and not scope['AgentInteractionId'])), 'HOLD_SCOPE_CONTEXT_INVALID')
        predicate = digest(native_json_bytes(scope)).upper()
        seal = digest(native_json_bytes([tenant, context['DeletionRequestId'], predicate, 'DestructionSealed'])).upper()
        require(command['TenantId'] == hold_command['TenantId'] == cap['TenantId'] == tenant
                and command['DeletionRequestId'] == hold_command['DeletionRequestId'] == cap['DeletionRequestId'] == context['DeletionRequestId']
                and command['Scope'] is None and hold_command['Scope'] is None
                and context['SealId'] == cap['DestructionSealId'] == seal
                and canonical(batch['Targets']) == canonical(targets)
                and batch['ManifestDigest'] == cap['ManifestDigest'] == digest(target_manifest_bytes(targets)).upper()
                and all(batch[key] == cap[key] for key in ('BatchId', 'BatchKind', 'BatchOrdinal', 'AttestationOrdinal', 'CapabilityKeyVersion'))
                and batch['SigningRequestId'] == signing and batch['AttestationDigest'] == signature
                and command['ReferenceId'] in ('', cap['BatchId']) and command['Hold'] is None
                and hold_command['Operation'] == 12 and hold_command['Hold']['HoldId'] == native_hold['HoldId'] == hold['Receipt']['ReferenceId']
                and hold_command['Hold']['Scope'] == native_hold['Scope'] == scope
                and hold_command['Hold']['DispositionVersion'] == native_hold['DispositionVersion']
                and native_hold['Active'] is True and native_hold['ReleaseReceiptId'] == hold_command['Hold']['ReleaseReceiptId'] == '', 'HOLD_SIGNED_SCOPE_MISMATCH')
        if order.get('dispatchStatus', 'Committed') == 'Committed':
            receipt = dispatch['Receipt']
            require(receipt['Capability'] == cap and receipt['SigningRequestId'] == signing and receipt['DetachedJwsDigest'] == signature
                    and receipt['ReferenceId'] == cap['BatchId'], 'HOLD_COMMITTED_REQUEST_MISMATCH')


def bind_pin_originals(observation, targets):
    """Reject a pin summary whose native pin receipts were substituted."""
    outcomes = observation['pinOutcomes']
    originals = observation.get('pinOriginals')
    require(isinstance(originals, list) and len(originals) == len(outcomes), 'PIN_ORIGINAL_MISSING')
    allowed, receipts, operations = set(map(target_key, targets)), set(), set()
    for original, outcome in zip(originals, outcomes):
        native_record(original, 'OperationId Status ReceiptId Target', 'PIN_ORIGINAL_SCHEMA_INVALID')
        identity_text(original['OperationId'], byte_bound=True)
        identity_text(original['ReceiptId'], byte_bound=True)
        require(original['Status'] == outcome['status'] and original['ReceiptId'] not in receipts and original['OperationId'] not in operations, 'PIN_ORIGINAL_SUBSTITUTED')
        require(target_key(original['Target']) in allowed, 'PIN_ORIGINAL_SUBSTITUTED')
        if 'target' in outcome:
            require(target_key(original['Target']) == target_key(outcome['target']), 'PIN_ORIGINAL_SUBSTITUTED')
        receipts.add(original['ReceiptId'])
        operations.add(original['OperationId'])


def bind_coverage_originals(observation):
    """Reject coverage groups whose native target originals were substituted."""
    groups = observation['targetCoverage']
    originals = observation.get('coverageOriginals')
    require(isinstance(originals, list) and len(originals) == len(groups), 'COVERAGE_ORIGINAL_MISSING')
    seen = set()
    for original, group in zip(originals, groups):
        native_record(original, 'Target BatchId Ordinal ManifestDigest ReceiptId', 'COVERAGE_ORIGINAL_SCHEMA_INVALID')
        identity_text(original['BatchId'], byte_bound=True)
        identity_text(original['ReceiptId'], byte_bound=True)
        identity_text(original['ManifestDigest'], byte_bound=True)
        ordinal(original['Ordinal'], 0, MAX_NATIVE_LONG)
        covered = next((item for item in observation.get('consumptionRequests', [])
                        if isinstance(item, dict) and item.get('Capability', {}).get('BatchId') == original['BatchId']), None)
        require(covered is not None and original['ManifestDigest'] == covered['Capability']['ManifestDigest'], 'COVERAGE_ORIGINAL_SUBSTITUTED')
        require(target_key(original['Target']) == target_key(group['target']), 'COVERAGE_ORIGINAL_SUBSTITUTED')
        require(len(group['batchIds']) == 1 and original['BatchId'] == group['batchIds'][0], 'COVERAGE_ORIGINAL_SUBSTITUTED')
        require(len(group['ordinals']) == 1 and type(group['ordinals'][0]) is int and original['Ordinal'] == group['ordinals'][0], 'COVERAGE_ORIGINAL_SUBSTITUTED')
        require(original['ReceiptId'] not in seen, 'COVERAGE_ORIGINAL_SUBSTITUTED')
        seen.add(original['ReceiptId'])


def bind_activation_original(observation):
    """Reject a re-attestation summary whose native activation original was substituted."""
    summary = observation['reattestation']
    original = observation.get('activationOriginal')
    native_record(original, 'OperationId CompromiseBlockReceiptId ExpectedKeyBlockSetRevision ActivatedBlockSetRevision GuardReplacementReceiptId BatchId ManifestDigest AttestationOrdinal OldAttestationRevoked ActiveCredentialCount', 'ACTIVATION_ORIGINAL_SCHEMA_INVALID')
    for field in ('OperationId', 'CompromiseBlockReceiptId', 'GuardReplacementReceiptId', 'BatchId', 'ManifestDigest'):
        identity_text(original[field], byte_bound=True)
    ordinal(original['ExpectedKeyBlockSetRevision'], 0, MAX_NATIVE_LONG)
    ordinal(original['ActivatedBlockSetRevision'], 0, MAX_NATIVE_LONG)
    ordinal(original['AttestationOrdinal'])
    ordinal(original['ActiveCredentialCount'])
    require(original['OldAttestationRevoked'] is True and original['ActiveCredentialCount'] == 1, 'ACTIVATION_ORIGINAL_SUBSTITUTED')
    require(type(summary['expectedBlockSetRevision']) is int and type(summary['activatedBlockSetRevision']) is int, 'ACTIVATION_ORIGINAL_SUBSTITUTED')
    require(original['CompromiseBlockReceiptId'] == summary['compromiseBlockReceiptId'], 'ACTIVATION_ORIGINAL_SUBSTITUTED')
    require(original['ExpectedKeyBlockSetRevision'] == summary['expectedBlockSetRevision'], 'ACTIVATION_ORIGINAL_SUBSTITUTED')
    require(original['ActivatedBlockSetRevision'] == summary['activatedBlockSetRevision'], 'ACTIVATION_ORIGINAL_SUBSTITUTED')
    requests = observation.get('consumptionRequests')
    require(isinstance(requests, list) and any(isinstance(item, dict) and item.get('Capability', {}).get('BatchId') == original['BatchId']
            and item['Capability'].get('ManifestDigest') == original['ManifestDigest']
            and item['Capability'].get('AttestationOrdinal') == original['AttestationOrdinal'] for item in requests),
            'ACTIVATION_ORIGINAL_SUBSTITUTED')
    receipts = observation.get('guardReceipts')
    require(isinstance(receipts, list) and any(isinstance(item, dict) and item.get('ReceiptId') == original['GuardReplacementReceiptId'] for item in receipts),
            'ACTIVATION_ORIGINAL_SUBSTITUTED')


def bind_replacement_original(observation):
    """Reject a blocked-activation summary whose native activation original was substituted."""
    if observation.get('rejectedUnissuedOriginal') is not None or observation.get('blockedReplacementOriginal') is not None:
        return bind_rejected_replacement_original(observation)
    if observation.get('postActivationBlockOriginal') is not None:
        return bind_post_activation_block_original(observation)
    value = observation['activationOutcome']
    original = observation.get('activationOriginal')
    native_record(original, 'OperationId NewBlockReceiptId NewKeyVersion RevocationRevision BatchId', 'ACTIVATION_ORIGINAL_SCHEMA_INVALID')
    for field in ('OperationId', 'NewBlockReceiptId', 'NewKeyVersion', 'BatchId'):
        identity_text(original[field], byte_bound=True)
    ordinal(original['RevocationRevision'])
    requests = observation.get('consumptionRequests')
    require(isinstance(requests, list) and requests and original['BatchId'] == requests[0]['Capability']['BatchId'], 'ACTIVATION_ORIGINAL_SUBSTITUTED')
    if observation.get('producerIdentity') is not None:
        owners = observation.get('ownerBatches')
        require(isinstance(owners, list) and len(owners) == 1, 'ACTIVATION_OWNER_ORIGINAL_MISSING')
        owner = native_record(owners[0], 'Original Current Outcome BlockedReplacement', 'ACTIVATION_OWNER_SCHEMA_INVALID')
        historical, current = owner['Original'], owner['Current']
        capture_consumption_request(historical)
        capture_consumption_request(current)
        logical = 'Issuer Audience TenantId DeletionRequestId DestructionSealId BatchKind BatchOrdinal BatchId ManifestDigest GuardStreamId'.split()
        require(current == requests[0] and owner['BlockedReplacement'] is None
                and all(historical['Capability'][key] == current['Capability'][key] for key in logical)
                and historical['Targets'] == current['Targets']
                and historical['Capability']['AttestationOrdinal'] + 1 == current['Capability']['AttestationOrdinal']
                and owner['Outcome']['ReceiptId'] == original['NewBlockReceiptId']
                and owner['Outcome']['BlockedKeyVersion'] == original['NewKeyVersion'], 'ACTIVATION_OWNER_ORIGINAL_SUBSTITUTED')
    require(original['NewBlockReceiptId'] == value['newBlockReceiptId']
            and original['NewKeyVersion'] == value['newKeyVersion'] == observation['ownerOutcome']['blockedKeyVersion']
            and original['RevocationRevision'] == value['revocationRevision'] == observation['ownerOutcome']['revocationRevision'],
            'ACTIVATION_ORIGINAL_SUBSTITUTED')


def ordered_blocked_replacement(value):
    """Reconstruct only declared native record property order; role transport canonicalizes wire object order."""
    fields = 'OperationId CompromiseBlockReceiptId ExpectedKeyBlockSetRevision GuardReplacementReceiptId Capability DetachedJws SigningRequestId CommittedIssuedGuardRevision Targets RevocationReceipt'.split()
    require(isinstance(value, dict) and set(value) in (set(fields), set(fields) | {'UnissuedProof'}), 'REJECTED_REPLACEMENT_SCHEMA_INVALID')
    result = {key: value[key] for key in fields}
    cap_fields = 'Issuer Audience TenantId DeletionRequestId DestructionSealId BatchKind BatchOrdinal BatchId ManifestDigest GuardStreamId IntendedIssuedGuardRevision AttestationOrdinal SigningAttemptOrdinal CapabilityKeyVersion'.split()
    result['Capability'] = {key: value['Capability'][key] for key in cap_fields}
    result['Targets'] = [{key: target[key] for key in ('TenantId', 'AgentInteractionId', 'TargetProtectionKeyAlias')} for target in value['Targets']]
    revocation = value['RevocationReceipt']
    native_record(revocation, 'Envelope OwnerRevision KeyBlockSetRevision ReceiptId AffectedBatchIds', 'REJECTED_REVOCATION_SCHEMA_INVALID')
    envelope_fields = 'Issuer Audience TenantId KeyFamily KeyVersion RevocationRevision TrustProfileRevision EventIdentity SignatureDigest'.split()
    native_record(revocation['Envelope'], ' '.join(envelope_fields), 'REJECTED_REVOCATION_SCHEMA_INVALID')
    result['RevocationReceipt'] = {key: ({field: revocation[key][field] for field in envelope_fields} if key == 'Envelope' else revocation[key])
                                   for key in ('Envelope', 'OwnerRevision', 'KeyBlockSetRevision', 'ReceiptId', 'AffectedBatchIds')}
    if value.get('UnissuedProof') is not None:
        proof = value['UnissuedProof']
        proof_fields = 'Transition TargetMutationDigest Refusal CurrentGuardRevision CurrentGuardStateDigest CurrentGuardStateBytes'.split()
        native_record(proof, ' '.join(proof_fields), 'REJECTED_NO_ISSUE_SCHEMA_INVALID')
        command_fields = 'TenantId OperationId Operation ExpectedGuardRevision EpochId DeletionRequestId Scope Write Repair Ordinal Hold Batch AuthorizationId ReferenceId BridgeOriginal RevocationReceipt'.split()
        batch_fields = 'BatchId BatchKind BatchOrdinal Targets ManifestDigest AttestationOrdinal CapabilityKeyVersion AttestationDigest ProtectionOutcome ProtectionReceiptId TargetReceiptIds BlockSetRevision Capability DetachedJws SigningRequestId'.split()
        receipt_fields = 'OperationId IntentDigest Status GuardHighWater AcceptedAtAdmissionFenceOrdinal ReferenceId ReceiptId AdmissionAttributionsJson AcceptedWriteResourceId AcceptedWriteFacts AcceptedTargetMutationDigest Capability SigningRequestId DetachedJwsDigest'.split()
        command = native_record(proof['Transition'], ' '.join(command_fields), 'REJECTED_NO_ISSUE_SCHEMA_INVALID')
        batch = native_record(command['Batch'], ' '.join(batch_fields), 'REJECTED_NO_ISSUE_SCHEMA_INVALID')
        receipt = native_record(proof['Refusal'], ' '.join(receipt_fields), 'REJECTED_NO_ISSUE_SCHEMA_INVALID')
        ordered_command = {key: command[key] for key in command_fields}
        ordered_batch = {key: batch[key] for key in batch_fields}
        ordered_batch['Capability'] = {key: batch['Capability'][key] for key in cap_fields}
        ordered_batch['Targets'] = [{key: target[key] for key in ('TenantId', 'AgentInteractionId', 'TargetProtectionKeyAlias')} for target in batch['Targets']]
        ordered_command['Batch'] = ordered_batch
        ordered_receipt = {key: receipt[key] for key in receipt_fields}
        ordered_receipt['Capability'] = {key: receipt['Capability'][key] for key in cap_fields}
        result['UnissuedProof'] = {'Transition': ordered_command, 'TargetMutationDigest': proof['TargetMutationDigest'], 'Refusal': ordered_receipt,
                                  'CurrentGuardRevision': proof['CurrentGuardRevision'], 'CurrentGuardStateDigest': proof['CurrentGuardStateDigest'], 'CurrentGuardStateBytes': proof['CurrentGuardStateBytes']}
    return result


def bind_rejected_replacement_original(observation):
    unissued = observation.get('rejectedUnissuedOriginal') is not None
    original = observation.get('rejectedUnissuedOriginal') if unissued else observation.get('blockedReplacementOriginal')
    native_record(original, 'Original Outcome IssuedRequest', 'REJECTED_REPLACEMENT_SCHEMA_INVALID')
    phase = ordered_blocked_replacement(original['Original'])
    outcome, issued = original['Outcome'], original['IssuedRequest']
    native_record(outcome, 'TenantId BatchId Status OwnerRevision KeyBlockSetRevision ReceiptId BlockReason BlockedKeyVersion RevocationRevision TargetReceipts', 'REJECTED_OWNER_SCHEMA_INVALID')
    capture_consumption_request(issued)
    cap, old = phase['Capability'], issued['Capability']
    immutable = CAPABILITY_FIELDS - {'IntendedIssuedGuardRevision', 'AttestationOrdinal', 'SigningAttemptOrdinal', 'CapabilityKeyVersion'}
    require(all(cap[key] == old[key] for key in immutable) and cap['AttestationOrdinal'] == old['AttestationOrdinal'] + 1
            and cap['CapabilityKeyVersion'] != old['CapabilityKeyVersion'] and phase['Targets'] == issued['Targets'], 'REJECTED_ISSUED_CREDENTIAL_CHANGED')
    require(type(outcome['Status']) is int and outcome['Status'] == 6 and outcome['BlockReason'] == 1 and outcome['TargetReceipts'] == []
            and outcome['TenantId'] == cap['TenantId'] and outcome['BatchId'] == cap['BatchId']
            and outcome['BlockedKeyVersion'] == cap['CapabilityKeyVersion'] and outcome['KeyBlockSetRevision'] == phase['ExpectedKeyBlockSetRevision'], 'REJECTED_OWNER_INVALID')
    revocation = phase['RevocationReceipt']
    require(revocation['Envelope']['TenantId'] == cap['TenantId'] and revocation['Envelope']['KeyVersion'] == cap['CapabilityKeyVersion']
            and revocation['Envelope']['KeyFamily'] == 'DeletionBatchCapabilitySigningKey' and revocation['Envelope']['RevocationRevision'] == outcome['RevocationRevision']
            and revocation['KeyBlockSetRevision'] <= phase['ExpectedKeyBlockSetRevision']
            and revocation['ReceiptId'] == digest(native_json_bytes(revocation['Envelope'])).upper()
            and sum(canonical(value) == canonical(revocation) for value in observation.get('ownerRevocations', [])) == 1
            and sum(canonical(value) == canonical(revocation) for value in observation.get('guardRevocations', [])) == 1, 'REJECTED_REVOCATION_SUBSTITUTED')
    batches = observation.get('ownerBatches', [])
    require(len(batches) == 1 and canonical(batches[0]['Current']) == canonical(issued)
            and canonical(batches[0]['BlockedReplacement']) == canonical(original['Original'])
            and batches[0]['Outcome']['Status'] == 2 and batches[0]['Outcome']['ReceiptId'] == outcome['ReceiptId'], 'REJECTED_OWNER_ORIGINAL_SUBSTITUTED')
    capture_consumption_request(batches[0]['Original'])
    historical = batches[0]['Original']
    require(all(historical['Capability'][key] == old[key] for key in immutable)
            and historical['Targets'] == issued['Targets']
            and historical['Capability']['AttestationOrdinal'] <= old['AttestationOrdinal'], 'REJECTED_HISTORICAL_OWNER_SUBSTITUTED')
    summary = observation['activationOutcome']
    require(summary['status'] == 'ActivationBlockedByReplacementKeyCompromise' and summary['newBlockReceiptId'] == outcome['ReceiptId']
            and summary['newKeyVersion'] == outcome['BlockedKeyVersion'] and summary['revocationRevision'] == outcome['RevocationRevision'], 'REJECTED_OUTCOME_SUBSTITUTED')
    label = 'blocked-unissued-replacement' if unissued else 'blocked-issued-replacement'
    require(outcome['ReceiptId'] == digest(native_json_bytes([phase['OperationId'], digest(native_json_bytes(phase)).upper(), label])).upper(), 'REJECTED_OWNER_RECEIPT_INVALID')
    guard_batches = observation.get('guardBatches', [])
    require(len(guard_batches) == 1, 'REJECTED_GUARD_ORIGINAL_MISSING')
    batch = guard_batches[0]
    if unissued:
        proof = phase.get('UnissuedProof')
        require(proof is not None and phase['CommittedIssuedGuardRevision'] == 0 and canonical(batch['Capability']) == canonical(old)
                and batch['AttestationOrdinal'] == old['AttestationOrdinal'] and batch.get('RejectedUnissuedReplacement') == {'Original': original['Original'], 'Outcome': outcome}, 'REJECTED_UNISSUED_ORDINAL_CHANGED')
        command, receipt = proof['Transition'], proof['Refusal']
        try:
            raw_state = base64.b64decode(proof['CurrentGuardStateBytes'], validate=True)
            state = strict_json(raw_state)
        except (ValueError, TypeError, UnicodeError):
            raise Failed('REJECTED_NO_ISSUE_STATE_INVALID') from None
        require(0 < len(raw_state) <= 8388608 and digest(raw_state).upper() == proof['CurrentGuardStateDigest']
                and state['TenantId'] == cap['TenantId'] and state['Revision'] == proof['CurrentGuardRevision']
                and cap['CapabilityKeyVersion'] in state['CompromisedKeyVersions'], 'REJECTED_NO_ISSUE_STATE_INVALID')
        historical = [item for deletion in state['Deletions'] for item in deletion['Batches'] if item['BatchId'] == cap['BatchId']]
        require(len(historical) == 1 and historical[0]['Capability'] == old and historical[0]['AttestationOrdinal'] == old['AttestationOrdinal']
                and historical[0]['ProtectionReceiptId'] == phase['CompromiseBlockReceiptId']
                and not any(item['Status'] == 'Committed' and item.get('SigningRequestId') == phase['SigningRequestId'] for item in state['Receipts'])
                and canonical(revocation) in [canonical(item) for item in state['Revocations']], 'REJECTED_NO_ISSUE_FALSE_CREDENTIAL')
        require(command['Operation'] == 23 and command['Batch']['Capability'] == cap and command['Batch']['DetachedJws'] == phase['DetachedJws']
                and receipt['Status'] in ('Blocked', 'Stale') and receipt['ReceiptId'] == phase['GuardReplacementReceiptId']
                and receipt['OperationId'] == command['OperationId'] and receipt['Capability'] == cap and receipt['SigningRequestId'] == phase['SigningRequestId']
                and receipt['DetachedJwsDigest'] == digest(phase['DetachedJws'].encode()).upper()
                and proof['TargetMutationDigest'] == digest(b'[]').upper() and proof['CurrentGuardRevision'] >= receipt['GuardHighWater']
                and receipt['IntentDigest'] == digest(native_json_bytes({'Transition': command, 'TargetMutationDigest': proof['TargetMutationDigest']})).upper(), 'REJECTED_NO_ISSUE_SUBSTITUTED')
        require(not any(value['Status'] == 'Committed' and value.get('SigningRequestId') == phase['SigningRequestId'] for value in observation['guardReceipts']), 'REJECTED_ATTEMPT_FALSE_ISSUE')
    else:
        require(phase.get('UnissuedProof') is None and phase['CommittedIssuedGuardRevision'] == batch['IssuedGuardRevision'] > 0
                and batch['Capability'] == cap and phase['GuardReplacementReceiptId'] == batch['IssueReceiptId']
                and batch['DispatchReceiptId'] == '' and batch['DispatchGuardRevision'] == 0, 'BLOCKED_UNDISPATCHED_ORIGINAL_INVALID')
        require(sum(value['Status'] == 'Committed' and value['ReceiptId'] == phase['GuardReplacementReceiptId'] and value['Capability'] == cap
                    and value['GuardHighWater'] == phase['CommittedIssuedGuardRevision'] for value in observation['guardReceipts']) == 1, 'BLOCKED_REPLACEMENT_ISSUE_MISSING')


def bind_post_activation_block_original(observation):
    value = native_record(observation.get('postActivationBlockOriginal'), 'Activation ActivatedOutcome RevocationReceipt BlockedOutcome', 'POST_ACTIVATION_BLOCK_SCHEMA_INVALID')
    activation, activated, revocation, blocked = (value[key] for key in ('Activation', 'ActivatedOutcome', 'RevocationReceipt', 'BlockedOutcome'))
    native_record(activation, 'OperationId CompromiseBlockReceiptId ExpectedKeyBlockSetRevision GuardReplacementReceiptId Replacement', 'POST_ACTIVATION_SCHEMA_INVALID')
    native_owner_outcome(activated, 0)
    native_record(blocked, 'TenantId BatchId Status OwnerRevision KeyBlockSetRevision ReceiptId BlockReason BlockedKeyVersion RevocationRevision TargetReceipts', 'POST_ACTIVATION_BLOCK_SCHEMA_INVALID')
    native_record(revocation, 'Envelope OwnerRevision KeyBlockSetRevision ReceiptId AffectedBatchIds', 'POST_ACTIVATION_REVOCATION_SCHEMA_INVALID')
    native_record(revocation['Envelope'], 'Issuer Audience TenantId KeyFamily KeyVersion RevocationRevision TrustProfileRevision EventIdentity SignatureDigest', 'POST_ACTIVATION_REVOCATION_SCHEMA_INVALID')
    for key in ('OperationId', 'CompromiseBlockReceiptId', 'GuardReplacementReceiptId'):
        identity_text(activation[key], byte_bound=True)
    for key in ('OwnerRevision', 'KeyBlockSetRevision'):
        ordinal(blocked[key], maximum=MAX_NATIVE_LONG)
        ordinal(revocation[key], maximum=MAX_NATIVE_LONG)
    capture_consumption_request(activation['Replacement'])
    cap = activation['Replacement']['Capability']
    request_fields = 'Capability DetachedJws CommittedIssuedGuardRevision DispatchReceiptId DispatchGuardRevision Targets'.split()
    cap_fields = 'Issuer Audience TenantId DeletionRequestId DestructionSealId BatchKind BatchOrdinal BatchId ManifestDigest GuardStreamId IntendedIssuedGuardRevision AttestationOrdinal SigningAttemptOrdinal CapabilityKeyVersion'.split()
    replacement = {key: activation['Replacement'][key] for key in request_fields}
    replacement['Capability'] = {key: cap[key] for key in cap_fields}
    replacement['Targets'] = [{key: target[key] for key in ('TenantId', 'AgentInteractionId', 'TargetProtectionKeyAlias')} for target in replacement['Targets']]
    ordered = {key: activation[key] for key in ('OperationId', 'CompromiseBlockReceiptId', 'ExpectedKeyBlockSetRevision', 'GuardReplacementReceiptId', 'Replacement')}
    ordered['Replacement'] = replacement
    require(activated['ReceiptId'] == digest(native_json_bytes([activation['OperationId'], digest(native_json_bytes(ordered)).upper(), 'activation'])).upper(), 'POST_ACTIVATION_ORIGINAL_RECEIPT_INVALID')
    owner = observation.get('ownerBatches')
    require(isinstance(owner, list) and len(owner) == 1 and owner[0]['Current'] == activation['Replacement']
            and owner[0]['Outcome'] == blocked and owner[0]['BlockedReplacement'] is None, 'POST_ACTIVATION_OWNER_ORIGINAL_INVALID')
    require(activated['TenantId'] == blocked['TenantId'] == revocation['Envelope']['TenantId'] == cap['TenantId']
            and revocation['Envelope']['KeyFamily'] == 'DeletionBatchCapabilitySigningKey'
            and activated['TargetReceipts'] == [] and type(blocked['Status']) is int and type(blocked['BlockReason']) is int,
            'POST_ACTIVATION_TENANT_OR_STATE_INVALID')
    guard_batch = [batch for batch in observation.get('guardBatches', []) if batch.get('BatchId') == cap['BatchId']]
    require(len(guard_batch) == 1 and guard_batch[0]['Capability'] == cap
            and guard_batch[0]['IssueReceiptId'] == activation['GuardReplacementReceiptId']
            and guard_batch[0]['DispatchReceiptId'] == replacement['DispatchReceiptId'], 'POST_ACTIVATION_GUARD_ORIGINAL_INVALID')
    original = owner[0]['Original']['Capability']
    logical = 'Issuer Audience TenantId DeletionRequestId DestructionSealId BatchKind BatchOrdinal BatchId ManifestDigest GuardStreamId'.split()
    prior = [item for item in observation.get('ownerRevocations', []) if item['Envelope']['KeyVersion'] == original['CapabilityKeyVersion']
             and cap['BatchId'] in item['AffectedBatchIds']]
    require(all(original[key] == cap[key] for key in logical) and original['AttestationOrdinal'] + 1 == cap['AttestationOrdinal']
            and len(prior) == 1 and prior[0]['KeyBlockSetRevision'] == activation['ExpectedKeyBlockSetRevision']
            and prior[0]['OwnerRevision'] < activated['OwnerRevision']
            and activation['CompromiseBlockReceiptId'] == digest(native_json_bytes([prior[0]['ReceiptId'], cap['BatchId']])).upper(),
            'POST_ACTIVATION_PRIOR_BLOCK_INVALID')
    require(activated['BatchId'] == blocked['BatchId'] == cap['BatchId'] and blocked['Status'] == 2 and blocked['BlockReason'] == 1
            and blocked['BlockedKeyVersion'] == revocation['Envelope']['KeyVersion'] == cap['CapabilityKeyVersion']
            and blocked['RevocationRevision'] == revocation['Envelope']['RevocationRevision'] and blocked['TargetReceipts'] == []
            and revocation['OwnerRevision'] > activated['OwnerRevision'] and blocked['OwnerRevision'] == revocation['OwnerRevision']
            and activation['ExpectedKeyBlockSetRevision'] == activated['KeyBlockSetRevision'] < revocation['KeyBlockSetRevision'] == blocked['KeyBlockSetRevision']
            and cap['BatchId'] in revocation['AffectedBatchIds'] and blocked['ReceiptId'] == digest(native_json_bytes([revocation['ReceiptId'], cap['BatchId']])).upper()
            and observation['activationOutcome']['status'] == 'ActivatedThenReplacementBlocked'
            and observation['activationOutcome']['newBlockReceiptId'] == blocked['ReceiptId'] == observation['ownerOutcome']['receiptId']
            and canonical(revocation) in [canonical(item) for item in observation.get('ownerRevocations', [])]
            and canonical(revocation) in [canonical(item) for item in observation.get('guardRevocations', [])], 'POST_ACTIVATION_BLOCK_ORIGINAL_INVALID')


def replacement_phase_evidence(observation, targets, snapshot):
    """Validate historical issued authority and the rejected candidate separately; no unissued or undispatched candidate is represented as current dispatched authority."""
    phase = observation.get('rejectedUnissuedOriginal') or observation.get('blockedReplacementOriginal')
    native_record(phase, 'Original Outcome IssuedRequest', 'REJECTED_REPLACEMENT_SCHEMA_INVALID')
    request, candidate = phase['IssuedRequest'], phase['Original']
    old_signing, old_digest = validate_consumption_request(request, None, None, snapshot)
    signing, signature = validate_capability_signature(candidate['Capability'], candidate['DetachedJws'], None, None, snapshot)
    require(candidate['SigningRequestId'] == signing and canonical(request['Targets']) == canonical(targets)
            and observation.get('consumptionRequests') == [request], 'REJECTED_REQUEST_INVENTORY_INVALID')
    batch_values = observation.get('guardBatches')
    require(isinstance(batch_values, list) and len(batch_values) == 1, 'REJECTED_GUARD_ORIGINAL_MISSING')
    batch = batch_values[0]
    fields = set('BatchId Kind Ordinal Targets ManifestDigest AttestationOrdinal CapabilityKeyVersion AttestationDigest IssueReceiptId DispatchReceiptId ProtectionOutcome ProtectionReceiptId TargetReceiptIds BlockSetRevision CoveredResourceIds Capability DetachedJws SigningRequestId IssuedGuardRevision DispatchGuardRevision'.split())
    require(isinstance(batch, dict) and set(batch) in (fields, fields | {'RejectedUnissuedReplacement'})
            and batch['BatchId'] == request['Capability']['BatchId'] and batch['ManifestDigest'] == request['Capability']['ManifestDigest']
            and batch['Targets'] == request['Targets'], 'REJECTED_GUARD_SCHEMA_INVALID')
    receipt_fields = 'OperationId IntentDigest Status GuardHighWater AcceptedAtAdmissionFenceOrdinal ReferenceId ReceiptId AdmissionAttributionsJson AcceptedWriteResourceId AcceptedWriteFacts AcceptedTargetMutationDigest Capability SigningRequestId DetachedJwsDigest'
    receipts = observation.get('guardReceipts')
    require(isinstance(receipts, list) and len(receipts) <= 2000, 'REJECTED_GUARD_RECEIPTS_MISSING')
    seen = set()
    for receipt in receipts:
        native_record(receipt, receipt_fields, 'REJECTED_GUARD_RECEIPT_SCHEMA_INVALID')
        require(receipt['ReceiptId'] not in seen, 'REJECTED_GUARD_RECEIPT_AMBIGUOUS')
        seen.add(receipt['ReceiptId'])
    for revision, receipt_id in ((request['CommittedIssuedGuardRevision'], None), (request['DispatchGuardRevision'], request['DispatchReceiptId'])):
        originals = [value for value in receipts if value['Status'] == 'Committed' and value['GuardHighWater'] == revision
                     and value['Capability'] == request['Capability'] and value['SigningRequestId'] == old_signing and value['DetachedJwsDigest'] == old_digest
                     and value['ReferenceId'] == request['Capability']['BatchId'] and (receipt_id is None or value['ReceiptId'] == receipt_id)]
        require(len(originals) == 1, 'REJECTED_PREDECESSOR_DISPATCH_ORIGINAL_MISSING')
    bind_rejected_replacement_original(observation)
    snapshot.recheck_pins()
    return {request['Capability']['BatchId']: request}, []


def bind_restore_originals(observation):
    """Reject a restore summary whose native copy originals were substituted."""
    copies = observation['copyStatesAfter']
    originals = observation.get('restoreOriginals')
    require(isinstance(originals, list) and originals and len(originals) == len(copies), 'RESTORE_ORIGINAL_MISSING')
    seen = set()
    for original, copy in zip(originals, copies):
        native_record(original, 'CopyId ReadOutcome Kind', 'RESTORE_ORIGINAL_SCHEMA_INVALID')
        identity_text(original['CopyId'], byte_bound=True)
        identity_text(original['ReadOutcome'])
        require(original['Kind'] in ('source-outbox', 'target-event', 'snapshot', 'cache', 'backup', 'restored'), 'RESTORE_ORIGINAL_SUBSTITUTED')
        if original['Kind'] not in ('backup', 'restored'):
            records = [value for value in observation.get('records', []) if value.get('copyId') == original['CopyId']]
            require(len(records) == 1 and records[0]['kind'] == original['Kind'], 'RESTORE_SOURCE_COPY_SUBSTITUTED')
        require(original['CopyId'] == copy['copyId'] and original['ReadOutcome'] == copy['readOutcome'], 'RESTORE_ORIGINAL_SUBSTITUTED')
        require(original['CopyId'] not in seen, 'RESTORE_ORIGINAL_SUBSTITUTED')
        seen.add(original['CopyId'])


def evaluate(observation, case, targets, canary, commit, binary_sha, capability_trust=None, role_configs=None):
    budget = VerificationBudget()
    require(len(canonical(observation)) <= MAX_OBSERVATION_BYTES, 'OBSERVATION_TOO_LARGE')
    _evaluate(observation, case, targets, canary, commit, binary_sha, capability_trust, role_configs, budget)
    for snapshot in budget.snapshots:
        snapshot.recheck_pins()
    budget.remaining()


def event_record_envelope(protected, target, raw):
    """Apply the production event's closed field shape and bounded V2 grammar to persisted bytes."""
    require(isinstance(protected, dict), 'PROTECTED_ENVELOPE_INVALID')
    content = protected.get('ProtectedContent')
    require(isinstance(content, dict) and set(content) == {'Target', 'Value'} and content['Target'] == target
            and isinstance(content['Value'], dict) and set(content['Value']) == {'$pdenc'}, 'PROTECTED_ENVELOPE_INVALID')
    pending, nodes = [(protected, 0)], 0
    while pending:
        node, depth = pending.pop()
        nodes += 1
        require(nodes <= 65536, 'EVENT_JSON_INVALID')
        if isinstance(node, (dict, list)):
            require(depth < 64, 'EVENT_JSON_INVALID')
            if isinstance(node, dict):
                for name in node:
                    try:
                        name.encode('utf-8')
                    except UnicodeError:
                        raise Failed('EVENT_JSON_INVALID') from None
                    require(name.lower() != 'protectedcontent' or (node is protected and name == 'ProtectedContent'), 'PROTECTED_ENVELOPE_INVALID')
                    require(name != '$enc' and (name != '$pdenc' or node is content['Value']), 'PROTECTED_ENVELOPE_INVALID')
                children = node.values()
            else:
                children = node
            pending.extend((child, depth + 1) for child in children)
    encoded = content['Value']['$pdenc']
    require(isinstance(encoded, str) and len(encoded) <= 1398211, 'PROTECTED_ENVELOPE_INVALID')
    # ReadProtectedWrappers rejects JSON escapes in the reserved marker and its carrier,
    # while permitting surrounding whitespace and ordinary escaped domain member names.
    require(re.search(rb'"\$pdenc"[ \t\r\n]*:[ \t\r\n]*"' + re.escape(encoded.encode('utf-8')) + rb'"', raw), 'PROTECTED_ENVELOPE_INVALID')
    envelope = decode_base64url(encoded)
    require(83 <= len(envelope) <= 1048658 and envelope[:8] == b'HXP2\x02\x01\x01\x01'
            and int.from_bytes(envelope[8:10], 'big') == 28 and int.from_bytes(envelope[10:12], 'big') == 26
            and int.from_bytes(envelope[12:16], 'big') > 0 and int.from_bytes(envelope[16:20], 'big') == 0
            and envelope[20:24] == bytes((12, 16, 0, 0)) and 0 < int.from_bytes(envelope[24:28], 'big') <= 1048576
            and len(envelope) == 82 + int.from_bytes(envelope[24:28], 'big')
            and re.fullmatch(rb'[0-7][0-9A-HJKMNP-TV-Z]{25}', envelope[28:54]) and envelope[54:66] == bytes(12), 'PROTECTED_ENVELOPE_INVALID')
    return encoded


def snapshot_record_envelopes(protected, target):
    """Keep whole-state V2 compatibility; validate complete mixed-root geometry and unchanged per-entry V2 envelopes."""
    if protected.get('Format') == 'json+pdenc-v2':
        require(protected.get('SnapshotTypeId'), 'SNAPSHOT_ENVELOPE_INVALID')
        return [protected['Envelope']]
    require(set(protected) == {'Format', 'SnapshotTypeId', 'SkeletonBytes', 'Entries'}
            and protected['Format'] == 'json+pdenc-v2-entries', 'SNAPSHOT_ENVELOPE_INVALID')
    parent = protected['SnapshotTypeId']
    require(isinstance(parent, str) and re.fullmatch(r'hx-snapshot-v1:[a-z0-9]+(?:-[a-z0-9]+)*', parent)
            and len(parent) <= 128, 'SNAPSHOT_ENVELOPE_INVALID')
    raw = base64.b64decode(protected['SkeletonBytes'], validate=True)
    require(raw and len(raw) <= 1048576 and base64.b64encode(raw).decode() == protected['SkeletonBytes'], 'SNAPSHOT_SKELETON_INVALID')
    skeleton = strict_json(raw)
    # Match BoundedJsonDocument before interpreting ownership, including objects the
    # layout walk treats as leaves. Depth counts open containers; nodes include scalars.
    pending, nodes = [(skeleton, 0)], 0
    while pending:
        node, depth = pending.pop()
        nodes += 1
        require(nodes <= 65536, 'SNAPSHOT_SKELETON_INVALID')
        if isinstance(node, (dict, list)):
            require(depth < 64, 'SNAPSHOT_SKELETON_INVALID')
            if isinstance(node, dict):
                require(not {'$pdenc', '$enc'} & set(node), 'SNAPSHOT_SKELETON_INVALID')
                children = node.values()
            else:
                children = node
            pending.extend((child, depth + 1) for child in children)
    geometry = []
    def visit(node, path=''):
        if isinstance(node, list):
            for index, child in enumerate(node):
                visit(child, path + '/' + str(index))
        elif isinstance(node, dict):
            target_fields = {'TenantId', 'AgentInteractionId', 'TargetProtectionKeyAlias'}
            if any(key.lower() == 'targetprotectionkeyalias' for key in node):
                require(set(node) == target_fields and node['TenantId'] == target['TenantId'], 'SNAPSHOT_SKELETON_INVALID')
                require(all(isinstance(value, str) and bool(value.strip()) for value in node.values()), 'SNAPSHOT_TARGET_INVALID')
                try:
                    for value in node.values():
                        value.encode('utf-8')
                except UnicodeError:
                    raise Failed('SNAPSHOT_TARGET_INVALID') from None
            addressed = node.get('Target')
            envelope = any(key.lower() == 'target' and isinstance(child, dict)
                           and any(member.lower() == 'targetprotectionkeyalias' for member in child)
                           for key, child in node.items()) and any(key.lower() == 'value' for key in node)
            if envelope:
                require(set(node) == {'Target', 'Value'} and node['Value'] is None
                        and isinstance(addressed, dict) and set(addressed) == target_fields
                        and addressed['TenantId'] == target['TenantId'], 'SNAPSHOT_SKELETON_INVALID')
                for value in addressed.values():
                    identity_text(value, 'SNAPSHOT_TARGET_INVALID', byte_bound=True)
                geometry.append((path + '/Value', addressed))
            else:
                for key, child in node.items():
                    require(key.lower() != 'protectedcontent' or (key == 'ProtectedContent' and isinstance(child, dict)
                            and set(child) == {'Target', 'Value'} and isinstance(child['Target'], dict)
                            and 'TargetProtectionKeyAlias' in child['Target'] and child['Value'] is None), 'SNAPSHOT_SKELETON_INVALID')
                    visit(child, path + '/' + key.replace('~', '~0').replace('/', '~1'))
    visit(skeleton)
    entries = protected['Entries']
    require(isinstance(entries, list) and 2 <= len(entries) <= 1000 and len(entries) == len(geometry), 'SNAPSHOT_ENTRY_MANIFEST_INVALID')
    found, paths = [], set()
    for entry, (path, addressed) in zip(entries, geometry):
        require(isinstance(entry, dict) and set(entry) == {'Path', 'Target', 'KeyReference', 'Protected'}
                and entry['Path'] == path and path not in paths and len(path.encode()) <= 2048 and entry['Target'] == addressed
                and isinstance(entry['KeyReference'], str) and re.fullmatch(r'[0-7][0-9A-HJKMNP-TV-Z]{25}', entry['KeyReference']), 'SNAPSHOT_ENTRY_MANIFEST_INVALID')
        paths.add(path)
        sealed = entry['Protected']
        child_type = 'hx-snapshot-v1:entry-' + hashlib.sha256(native_json_bytes({'PayloadTypeId': parent, 'path': path})).hexdigest()
        require(isinstance(sealed, dict) and set(sealed) == {'Format', 'SnapshotTypeId', 'Envelope'}
                and sealed['Format'] == 'json+pdenc-v2' and sealed['SnapshotTypeId'] == child_type, 'SNAPSHOT_ENTRY_ENVELOPE_INVALID')
        encoded = sealed['Envelope']
        require(isinstance(encoded, str), 'SNAPSHOT_ENTRY_ENVELOPE_INVALID')
        envelope = decode_base64url(encoded)
        require(83 <= len(envelope) <= 1048658 and envelope[:8] == b'HXP2\x02\x01\x01\x01'
                and int.from_bytes(envelope[8:10], 'big') == 28 and int.from_bytes(envelope[10:12], 'big') == 26
                and int.from_bytes(envelope[12:16], 'big') > 0 and int.from_bytes(envelope[16:20], 'big') == 0
                and envelope[20:24] == bytes((12, 16, 0, 0)) and 0 < int.from_bytes(envelope[24:28], 'big') <= 1048576
                and len(envelope) == 82 + int.from_bytes(envelope[24:28], 'big')
                and envelope[28:54] == entry['KeyReference'].encode('ascii'), 'PROTECTED_ENVELOPE_INVALID')
        if addressed == target:
            found.append(encoded)
    require(found, 'SNAPSHOT_TARGET_PROOF_MISSING')
    return found


def bind_snapshot_originals(observation):
    """Bind mixed carriers to complete native source, registry, writer and composite-completion originals."""
    mixed = {}
    for record in observation['records']:
        if record['kind'] in ('source-outbox', 'target-event'):
            continue
        carrier = strict_json(base64.b64decode(record['bytesBase64'], validate=True))
        if carrier.get('Format') != 'json+pdenc-v2-entries':
            continue
        mixed[hashlib.sha256(native_json_bytes(carrier)).hexdigest().upper()] = carrier
    if not mixed:
        return
    originals = observation.get('snapshotOriginals')
    require(isinstance(originals, list) and len(originals) == len(mixed), 'SNAPSHOT_NATIVE_ORIGINALS_MISSING')
    seen = set()
    for original in originals:
        native_record({key: value for key, value in original.items() if key != 'ProducerOriginal'}, 'CarrierSha256 Completion Entries', 'SNAPSHOT_NATIVE_ORIGINAL_SCHEMA_INVALID')
        key = original['CarrierSha256']
        require(key in mixed and key not in seen, 'SNAPSHOT_NATIVE_ORIGINAL_SUBSTITUTED')
        seen.add(key)
        carrier = mixed[key]
        completion = original['Completion']
        native_record(completion, 'Identity Occurrence OperationId KeyReference DekVersion LifecycleEpoch SnapshotEntries SnapshotContainerDigest SnapshotEntryManifest', 'SNAPSHOT_COMPLETION_SCHEMA_INVALID')
        parent = completion['Occurrence']
        native_record(parent, 'RecordSequence PayloadKind PayloadTypeId', 'SNAPSHOT_PARENT_SCHEMA_INVALID')
        ordinal(parent['RecordSequence'], maximum=MAX_NATIVE_LONG)
        require(parent['PayloadKind'] == 2 and parent['PayloadTypeId'] == carrier['SnapshotTypeId'], 'SNAPSHOT_PARENT_SUBSTITUTED')
        digest = hashlib.sha256(base64.b64decode(carrier['SkeletonBytes'], validate=True)).hexdigest().upper()
        entries = original['Entries']
        require(completion['SnapshotContainerDigest'] == digest and isinstance(entries, list) and len(entries) == len(carrier['Entries'])
                and isinstance(completion['SnapshotEntries'], list) and len(completion['SnapshotEntries']) == len(entries)
                and completion['SnapshotEntryManifest'] == [{'Path': entry['Path'], 'Target': entry['Target']} for entry in carrier['Entries']], 'SNAPSHOT_COMPLETION_MANIFEST_INVALID')
        for entry, sealed, child in zip(entries, carrier['Entries'], completion['SnapshotEntries']):
            native_record(entry, 'Path SourceEnrollment RegistryOriginal WriterProof', 'SNAPSHOT_ENTRY_ORIGINAL_SCHEMA_INVALID')
            native_record(child, 'Identity Occurrence OperationId KeyReference DekVersion LifecycleEpoch', 'SNAPSHOT_CHILD_COMPLETION_SCHEMA_INVALID')
            enrollment, registry, writer = entry['SourceEnrollment'], entry['RegistryOriginal'], entry['WriterProof']
            binding = enrollment['Binding']
            source = enrollment.get('SnapshotEntry')
            require(entry['Path'] == sealed['Path'] and source == {'Parent': parent, 'Path': sealed['Path'], 'ContainerDigest': digest, 'EntryCount': len(entries)}
                    and binding['Identity']['Target'] == sealed['Target'] and binding['Identity']['Owner'] == completion['Identity']
                    and binding['Identity']['Kind'] == 2 and binding['Identity']['Sequence'] == parent['RecordSequence']
                    and binding['Identity']['PayloadTypeId'] == sealed['Protected']['SnapshotTypeId']
                    and binding['KeyReference'] == sealed['KeyReference'], 'SNAPSHOT_SOURCE_ORIGINAL_SUBSTITUTED')
            require(child == {'Identity': completion['Identity'], 'Occurrence': {'RecordSequence': parent['RecordSequence'], 'PayloadKind': 2,
                    'PayloadTypeId': sealed['Protected']['SnapshotTypeId']}, 'OperationId': binding['OperationId'], 'KeyReference': binding['KeyReference'],
                    'DekVersion': 1, 'LifecycleEpoch': binding['LifecycleEpoch']}, 'SNAPSHOT_CHILD_COMPLETION_SUBSTITUTED')
            payload = native_json_bytes(sealed['Protected'])
            require(registry['Request']['Identity'] == binding['Identity'] and registry['KeyReference'] == binding['KeyReference']
                    and registry['WriterState'] == 2 and registry['WriterProofId'] == binding['OperationId']
                    and registry['Sealed'] == {'KeyReference': binding['KeyReference'], 'PayloadBytes': base64.b64encode(payload).decode(),
                                               'SerializationFormat': 'json+pdenc-v2', 'ProtectedPathCount': 1}, 'SNAPSHOT_REGISTRY_ORIGINAL_SUBSTITUTED')
            native_record(writer, 'Binding SealedPayloadSha256 Persisted WriterRetired ReceiptId', 'SNAPSHOT_WRITER_SCHEMA_INVALID')
            identity_text(writer['ReceiptId'], 'SNAPSHOT_WRITER_RECEIPT_INVALID', byte_bound=True)
            require(writer['Binding'] == binding and writer['Persisted'] is True and writer['WriterRetired'] is True
                    and writer['SealedPayloadSha256'] == hashlib.sha256(payload).hexdigest().upper(), 'SNAPSHOT_WRITER_ORIGINAL_SUBSTITUTED')
        first = completion['SnapshotEntries'][0]
        require(all(completion[field] == first[field] for field in ('Identity', 'OperationId', 'KeyReference', 'DekVersion', 'LifecycleEpoch')), 'SNAPSHOT_COMPOSITE_IDENTITY_SUBSTITUTED')


def typed_native_owner_outcome(value):
    native_record(value, 'TenantId BatchId Status OwnerRevision KeyBlockSetRevision ReceiptId BlockReason BlockedKeyVersion RevocationRevision TargetReceipts', 'EXACT_NATIVE_OUTCOME_SCHEMA_INVALID')
    aggregate_identity(value['TenantId'], value['BatchId'])
    ordinal(value['Status'], 0, 6); ordinal(value['OwnerRevision'], maximum=MAX_NATIVE_LONG); ordinal(value['KeyBlockSetRevision'], 0, value['OwnerRevision'])
    identity_text(value['ReceiptId'], 'EXACT_NATIVE_RECEIPT_MISSING', byte_bound=True)
    require(isinstance(value['TargetReceipts'], list), 'EXACT_NATIVE_OUTCOME_SCHEMA_INVALID')
    for receipt in value['TargetReceipts']:
        native_record(receipt, 'Target OriginalBatchId ReceiptId', 'EXACT_NATIVE_OUTCOME_SCHEMA_INVALID')
        target_key(receipt['Target']); identity_text(receipt['OriginalBatchId'], byte_bound=True); identity_text(receipt['ReceiptId'], byte_bound=True)
    if value['BlockReason'] is not None:
        ordinal(value['BlockReason'], 0, 1)
    if value['RevocationRevision'] is not None:
        ordinal(value['RevocationRevision'], maximum=MAX_NATIVE_LONG)
    if value['BlockedKeyVersion'] is not None:
        identity_text(value['BlockedKeyVersion'], byte_bound=True)
    require(value['Status'] in (2, 6) or all(value[name] is None for name in ('BlockReason', 'BlockedKeyVersion', 'RevocationRevision')), 'EXACT_NATIVE_OUTCOME_SCHEMA_INVALID')
    return value


def ordered_consumption_request(request):
    capture_consumption_request(request)
    result = {key: request[key] for key in 'Capability DetachedJws CommittedIssuedGuardRevision DispatchReceiptId DispatchGuardRevision Targets'.split()}
    result['Capability'] = {key: request['Capability'][key] for key in 'Issuer Audience TenantId DeletionRequestId DestructionSealId BatchKind BatchOrdinal BatchId ManifestDigest GuardStreamId IntendedIssuedGuardRevision AttestationOrdinal SigningAttemptOrdinal CapabilityKeyVersion'.split()}
    result['Targets'] = [{key: target[key] for key in ('TenantId', 'AgentInteractionId', 'TargetProtectionKeyAlias')} for target in request['Targets']]
    return result


def stripped_native_result(value):
    require(isinstance(value, dict), 'EXACT_RESULT_SCHEMA_INVALID')
    return {key: item for key, item in value.items() if key not in ('status', 'effectBefore', 'effectAfter', 'pinRefusals')}


def validate_native_call(observation, original, case, effect=False):
    require(isinstance(original, dict) and isinstance(original.get('Call'), dict), 'EXACT_CALL_ORIGINAL_MISSING')
    call = original['Call']
    required = {'Id', 'Kind', 'Payload', 'OperationId', 'RequestSha256'}
    require(required <= set(call) <= required | {'SourceDigest', 'HoldBlockedOriginal'} and type(call['Kind']) is int
            and isinstance(call['Payload'], dict) and isinstance(call['RequestSha256'], str)
            and re.fullmatch(r'[0-9a-f]{64}', call['RequestSha256']), 'EXACT_CALL_IDENTITY_INVALID')
    identity_text(call['Id'], byte_bound=True)
    identity_text(call['OperationId'], byte_bound=True)
    require(call['Id'].startswith(case['id'] + '/') and any('/' + action + '/' in call['Id'] for action in case['steps']), 'EXACT_CALL_SCENARIO_SUBSTITUTED')
    operations = observation.get('operationProducerOriginals')
    require(isinstance(operations, list), 'EXACT_OPERATION_ORIGINAL_MISSING')
    matches = [item for item in operations if item.get('OperationId') == call['OperationId']]
    require(len(matches) == 1, 'EXACT_OPERATION_ORIGINAL_SUBSTITUTED')
    operation = matches[0]
    request = operation.get('SafeRequest')
    require(operation.get('Role') == 'executor' and operation.get('RequestSha256') == call['RequestSha256']
            and operation.get('Producer') == observation.get('producerIdentity') and isinstance(request, dict)
            and request.get('caseId') == case['id'] and request.get('action') in case['steps']
            and '/' + request['action'] + '/' in call['Id'] and request.get('operationId') == call['OperationId']
            and operation.get('BindingDigest') == observation.get('producerIdentity', {}).get('BindingDigest'), 'EXACT_OPERATION_ORIGINAL_SUBSTITUTED')
    calls = observation.get('nativeCallOriginals')
    require(isinstance(calls, list), 'EXACT_CALL_ORIGINAL_MISSING')
    matches = [item for item in calls if item.get('Call') == call]
    require(len(matches) == 1 and isinstance(matches[0].get('Result'), dict), 'EXACT_CALL_ORIGINAL_SUBSTITUTED')
    if not effect:
        require(set(original) == {'Call', 'Result'} and original == matches[0], 'EXACT_CALL_ORIGINAL_SUBSTITUTED')
    return call, matches[0]['Result']


def bind_exact_recovery(observation, case):
    recipes = {'P06': ('DestroyWithLostAcknowledgement', 8, 'LookupOriginalDestruction', 14),
               'P35': ('LoseBlockDeliveryAndMirror', 9, 'LookupOriginalBlock', 15),
               'P36': ('LoseActivationResult', 11, 'LookupOriginalActivation', 16)}
    require(case.get('id') in recipes, 'EXACT_RESULT_SCENARIO_UNSUPPORTED')
    results = observation.get('exactResults')
    require(isinstance(results, list) and len(results) == 2, 'EXACT_RESULT_DIVERGED')
    action, kind, lookup_action, lookup_kind = recipes[case['id']]
    original_call, result = validate_native_call(observation, results[0], case)
    lookup_call, looked_up = validate_native_call(observation, results[1], case)
    require(original_call['Kind'] == kind and lookup_call['Kind'] == lookup_kind
            and '/' + action + '/' in original_call['Id'] and '/' + lookup_action + '/' in lookup_call['Id']
            and original_call['OperationId'] != lookup_call['OperationId'] and original_call['Id'] != lookup_call['Id'], 'EXACT_RESULT_OPERATION_SUBSTITUTED')
    result, looked_up = stripped_native_result(result), stripped_native_result(looked_up)
    typed_native_owner_outcome(result); typed_native_owner_outcome(looked_up)
    require(result == looked_up, 'EXACT_RESULT_DIVERGED')
    if kind == 8:
        matches = [item for item in observation.get('ownerBatches', []) if item.get('Current') == original_call['Payload'] and item.get('Outcome') == result]
        require(len(matches) == 1 and result.get('Status') == 3 and lookup_call['Payload'] == {'tenantId': result['TenantId'], 'batchId': result['BatchId']}, 'EXACT_NATIVE_TERMINAL_SUBSTITUTED')
    else:
        field = 'AdmissionBlockOriginal' if kind == 9 else 'ActivationOriginal'
        for operation in observation.get('ownerOperationOriginals', []):
            fields = {'OperationId', 'RequestDigest', 'Outcome', 'BlockedReplacementOriginal', 'ActivationOriginal'}
            require(isinstance(operation, dict) and fields <= set(operation) <= fields | {'AdmissionBlockOriginal'}, 'EXACT_NATIVE_OPERATION_SCHEMA_INVALID')
            typed_native_owner_outcome(operation['Outcome'])
        payload = original_call['Payload']
        if kind == 9:
            native_record(payload, 'TenantId BatchId OperationId AdmissionEvidenceId', 'EXACT_NATIVE_OPERATION_SCHEMA_INVALID')
            ordered_payload = {name: payload[name] for name in ('TenantId', 'BatchId', 'OperationId', 'AdmissionEvidenceId')}
        else:
            native_record(payload, 'OperationId CompromiseBlockReceiptId ExpectedKeyBlockSetRevision GuardReplacementReceiptId Replacement', 'EXACT_NATIVE_OPERATION_SCHEMA_INVALID')
            ordinal(payload['ExpectedKeyBlockSetRevision'], 0, MAX_NATIVE_LONG)
            ordered_payload = {name: payload[name] for name in ('OperationId', 'CompromiseBlockReceiptId', 'ExpectedKeyBlockSetRevision', 'GuardReplacementReceiptId', 'Replacement')}
            ordered_payload['Replacement'] = ordered_consumption_request(payload['Replacement'])
        matches = [item for item in observation.get('ownerOperationOriginals', []) if item.get(field) == payload and item.get('Outcome') == result]
        require(len(matches) == 1 and lookup_call['Payload'] == original_call['Payload']
                and matches[0].get('OperationId') == original_call['Payload'].get('OperationId')
                and matches[0].get('RequestDigest') == hashlib.sha256(native_json_bytes(ordered_payload)).hexdigest().upper(), 'EXACT_NATIVE_TERMINAL_SUBSTITUTED')
        require(result.get('Status') == (2 if kind == 9 else 0), 'EXACT_NATIVE_TERMINAL_SUBSTITUTED')
    require(any(item.get('Current', {}).get('Capability', {}).get('BatchId') == result.get('BatchId')
                and item.get('Current', {}).get('Capability', {}).get('TenantId') == result.get('TenantId') for item in observation.get('ownerBatches', [])), 'EXACT_NATIVE_BATCH_SUBSTITUTED')
    identity_text(result.get('ReceiptId'), 'EXACT_NATIVE_RECEIPT_MISSING', byte_bound=True)


def bind_reservation_recovery(observation, case):
    require(case.get('id') in ('P25', 'P28'), 'RECOVERY_SCENARIO_UNSUPPORTED')
    values = observation.get('reservationRecoveries')
    require(isinstance(values, list) and len(values) == 1, 'RESERVATION_RECOVERY_ORIGINALS_MISSING')
    value = native_record(values[0], 'Request ReservationOriginal Original Recovery OwnerOutcome ProviderOutcome', 'RESERVATION_RECOVERY_SCHEMA_INVALID')
    request = value['Request']
    capture_consumption_request(request)
    original, first = validate_native_call(observation, value['Original'], case)
    recovery, recovered = validate_native_call(observation, value['Recovery'], case)
    action = 'ReserveBeforeAdmissionBlock' if case['id'] == 'P25' else 'CompromiseAfterReserve'
    require(original['Kind'] == 8 and '/' + action + '/' in original['Id'] and original['Payload'] == request
            and recovery['Kind'] == 14 and '/LookupOriginalDestruction/' in recovery['Id']
            and original['OperationId'] != recovery['OperationId']
            and recovery['Payload'] == {'tenantId': request['Capability']['TenantId'], 'batchId': request['Capability']['BatchId']}, 'RESERVATION_RECOVERY_CALL_SUBSTITUTED')
    reserved = native_owner_outcome(value['ReservationOriginal'], 1)
    consumed = native_owner_outcome(value['OwnerOutcome'], 3)
    require(reserved['TenantId'] == consumed['TenantId'] == request['Capability']['TenantId']
            and reserved['BatchId'] == consumed['BatchId'] == request['Capability']['BatchId']
            and reserved['OwnerRevision'] < consumed['OwnerRevision'] and reserved['TargetReceipts'] == [], 'RESERVATION_ORIGINAL_SUBSTITUTED')
    require(stripped_native_result(first) in (reserved, consumed) and stripped_native_result(recovered) == consumed
            and consumed in observation.get('ownerResults', []) and value['ProviderOutcome'] in observation.get('providerResults', [])
            and any(item.get('Current') == request and item.get('Outcome') == consumed for item in observation.get('ownerBatches', [])), 'RESERVATION_RECOVERY_TERMINAL_SUBSTITUTED')
    provider = value['ProviderOutcome']
    require(provider.get('ReservationReceiptId') == reserved['ReceiptId'] and provider.get('TargetReceipts') == consumed['TargetReceipts']
            and provider.get('TenantId') == reserved['TenantId'] and provider.get('BatchId') == reserved['BatchId']
            and provider.get('State') == 1, 'RECOVERY_REAUTHORIZED_BATCH')


def bind_producer_originals(observation, commit, binary_sha, role_configs=None):
    """Authenticate the actual executor identity and every event/single snapshot against retained native originals."""
    producer = observation.get('producerIdentity')
    native_record(producer, 'EngineSourceRevision EngineBinarySha256 RoleIdentity BindingDigest StoreId EnrollmentId', 'PRODUCER_IDENTITY_MISSING')
    require(producer['EngineSourceRevision'] == commit and producer['EngineBinarySha256'] == binary_sha,
            'EXECUTOR_PRODUCER_IDENTITY_SUBSTITUTED')
    identity_text(producer['RoleIdentity'], 'EXECUTOR_PRODUCER_ROLE_MISSING', byte_bound=True)
    identity_text(producer['BindingDigest'], 'EXECUTOR_INSTALLATION_BINDING_MISSING', byte_bound=True)
    if role_configs:
        require(producer['RoleIdentity'] == role_configs['executor']['identity'], 'EXECUTOR_PRODUCER_ROLE_SUBSTITUTED')
    def bind_execution(retained):
        fields = set('SourceIdentity Bindings SealedPayloadSha256 CompletionDigest Completion Producer'.split())
        require(isinstance(retained, dict) and fields <= set(retained) <= fields | {'Call', 'CopyPurposes'}, 'COPY_PRODUCER_ORIGINAL_SCHEMA_INVALID')
        purposes = retained.get('CopyPurposes')
        if purposes is not None:
            require(isinstance(purposes, dict) and 1 <= len(purposes) <= 2
                    and all(isinstance(key, str) and re.fullmatch(r'[0-9A-F]{64}', key)
                            and value in ('source-outbox', 'target-event', 'snapshot', 'cache') for key, value in purposes.items()), 'COPY_PURPOSE_SCHEMA_INVALID')
        call = retained.get('Call')
        if call is None and not role_configs:
            require(purposes is None, 'COPY_PURPOSE_EXECUTION_ORIGINAL_MISSING')
            return
        native_record(call, 'Id Kind Payload OperationId RequestSha256', 'SOURCE_EXECUTION_ORIGINAL_MISSING')
        require(type(call['Kind']) is int and call['Kind'] in (0, 3) and isinstance(call['Payload'], dict), 'SOURCE_EXECUTION_CALL_INVALID')
        source = call['Payload'].get('Source', call['Payload'].get('source'))
        require(isinstance(source, dict), 'COPY_PURPOSE_SOURCE_MISSING')
        def location(key):
            return hashlib.sha256(native_json_bytes({'Component': source['Component'], 'Key': key,
                       'Metadata': dict(sorted(source['Metadata'].items()))})).hexdigest().upper()
        identity = retained['Bindings'][0]['Identity']
        expected_purposes = {location(source['Key']): 'snapshot', location(source['Key'] + ':cache'): 'cache'} if call['Kind'] == 3 else {
            location(source['Key']): 'target-event' if identity['Owner']['AggregateId'] == identity['Target']['AgentInteractionId'] else 'source-outbox'}
        require(purposes == expected_purposes and retained['SourceIdentity'] == location(source['Key']), 'COPY_PURPOSE_SUBSTITUTED')
        identity_text(call['Id'], byte_bound=True)
        identity_text(call['OperationId'], byte_bound=True)
        require(isinstance(call['RequestSha256'], str) and re.fullmatch(r'[a-f0-9]{64}', call['RequestSha256']), 'SOURCE_EXECUTION_REQUEST_INVALID')
        operations = observation.get('operationProducerOriginals')
        require(isinstance(operations, list), 'SOURCE_EXECUTION_OPERATION_MISSING')
        matching = [value for value in operations if value.get('OperationId') == call['OperationId']]
        require(len(matching) == 1 and matching[0].get('RequestSha256') == call['RequestSha256']
                and matching[0].get('Producer') == retained['Producer'] and matching[0].get('Role') == 'executor'
                and matching[0].get('BindingDigest') == producer['BindingDigest'], 'SOURCE_EXECUTION_OPERATION_SUBSTITUTED')
    for snapshot in observation.get('snapshotOriginals', []):
        retained = snapshot.get('ProducerOriginal')
        bind_execution(retained)
        require(isinstance(retained, dict) and retained.get('Producer') == producer and retained.get('SealedPayloadSha256') == snapshot['CarrierSha256']
                and retained.get('Completion') == snapshot['Completion']
                and retained.get('CompletionDigest') == hashlib.sha256(native_json_bytes(snapshot['Completion'])).hexdigest().upper()
                and retained.get('Bindings') == [entry['SourceEnrollment']['Binding'] for entry in snapshot['Entries']], 'SNAPSHOT_PRODUCER_ORIGINAL_SUBSTITUTED')
    originals = observation.get('copyOriginals', [])
    require(isinstance(originals, list), 'COPY_NATIVE_ORIGINALS_MISSING')
    required = {}
    physical_copies = {}
    locations = observation.get('copyLocations')
    require(isinstance(locations, list) and locations, 'COPY_LOCATION_ORIGINALS_MISSING')
    location_originals = {}
    for location in locations:
        fields = set('Cell PhysicalBacking PhysicalIdentity SourceIdentity CopyKind PersistedDocument PersistedDocumentBase64'.split())
        require(isinstance(location, dict) and fields <= set(location) <= fields | {'CopyCall', 'CopyOriginal'}, 'COPY_LOCATION_SCHEMA_INVALID')
        cell = location['Cell']
        require(isinstance(cell, dict) and set('Component Key Metadata'.split()) <= set(cell) <= set('Component Key Metadata AnchorScope CompletionKey CopyKind'.split()), 'COPY_CELL_SCHEMA_INVALID')
        require(cell.get('CopyKind') == location['CopyKind'] and isinstance(cell['Metadata'], dict)
                and isinstance(location['PhysicalBacking'], str) and location['PhysicalBacking'].strip(), 'COPY_LOCATION_SUBSTITUTED')
        def identity(component):
            return hashlib.sha256(native_json_bytes({'Component': component, 'Key': cell['Key'], 'Metadata': dict(sorted(cell['Metadata'].items()))})).hexdigest().upper()
        require(location['SourceIdentity'] == identity(cell['Component']) and location['PhysicalIdentity'] == identity(location['PhysicalBacking'])
                and location['SourceIdentity'] not in location_originals, 'COPY_LOCATION_SUBSTITUTED')
        if location['CopyKind'] in ('backup', 'restored'):
            require(set(location) == fields | {'CopyCall', 'CopyOriginal'}, 'COPY_ATOMIC_ORIGINAL_MISSING')
            call, original = location['CopyCall'], location['CopyOriginal']
            native_record(original, 'CallDigest SourceIdentity SourceDigest Result', 'COPY_ATOMIC_SCHEMA_INVALID')
            source = call['Payload']
            kind = 25 if location['CopyKind'] == 'backup' else 26
            # Names are encoded by the native enum; numeric values are checked below from the fixed public contract.
            require(call['Kind'] == kind and original['CallDigest'] == hashlib.sha256(native_json_bytes(call)).hexdigest().upper()
                    and original['SourceDigest'] == call['SourceDigest'] == hashlib.sha256(base64.b64decode(location['PersistedDocumentBase64'], validate=True)).hexdigest().upper(), 'COPY_ATOMIC_ORIGINAL_SUBSTITUTED')
            source_id = hashlib.sha256(native_json_bytes({'Component': source['Component'], 'Key': source['Key'], 'Metadata': dict(sorted(source['Metadata'].items()))})).hexdigest().upper()
            destination = source['Key'] + (':backup' if location['CopyKind'] == 'backup' else ':restored' if source.get('AnchorScope') is None else '')
            require(original['SourceIdentity'] == source_id and cell['Component'] == source['Component'] and cell['Metadata'] == source['Metadata']
                    and cell['Key'] == destination, 'COPY_ATOMIC_LOCATION_SUBSTITUTED')
        else:
            require(set(location) == fields, 'COPY_LOCATION_SCHEMA_INVALID')
        location_originals[location['SourceIdentity']] = location
    for record in observation['records']:
        location = record.get('sourceIdentity')
        require(isinstance(location, str) and len(location) == 64, 'COPY_PHYSICAL_IDENTITY_MISSING')
        require(location in location_originals and location_originals[location]['CopyKind'] == record['kind'], 'COPY_LOCATION_SUBSTITUTED')
        raw_document = base64.b64decode(location_originals[location]['PersistedDocumentBase64'], validate=True)
        require(0 < len(raw_document) <= MAX_OBSERVATION_BYTES, 'COPY_SOURCE_DOCUMENT_INVALID')
        persisted = strict_json(raw_document)
        require(isinstance(persisted, dict) and canonical(persisted) == canonical(location_originals[location]['PersistedDocument'])
                and record.get('sourceDigest') == hashlib.sha256(raw_document).hexdigest().upper(), 'COPY_SOURCE_DOCUMENT_SUBSTITUTED')
        actual_bytes = base64.b64decode(record['bytesBase64'], validate=True)
        if record['kind'] in ('source-outbox', 'target-event'):
            require(isinstance(persisted.get('Protected'), dict) and base64.b64decode(persisted['Protected']['PayloadBytes'], validate=True) == actual_bytes, 'COPY_SOURCE_DOCUMENT_SUBSTITUTED')
        else:
            require(native_json_bytes(persisted.get('State')) == actual_bytes, 'COPY_SOURCE_DOCUMENT_SUBSTITUTED')
        identity = (location_originals[location]['PhysicalIdentity'], target_key(record['target']))
        require(identity not in physical_copies, 'COPY_PHYSICAL_LOCATION_REUSED')
        physical_copies[identity] = record['kind']
        raw = base64.b64decode(record['bytesBase64'], validate=True)
        document = strict_json(raw)
        if document.get('Format') == 'json+pdenc-v2-entries':
            continue
        require(isinstance(record.get('copyId'), str) and record['copyId'] not in required, 'COPY_IDENTITY_MISSING')
        required[record['copyId']] = (record, raw)
    require(len(originals) == len(required), 'COPY_NATIVE_ORIGINALS_MISSING')
    seen = set()
    for original in originals:
        native_record(original, 'CopyId SourceEnrollment RegistryOriginal WriterProof ProducerOriginal', 'COPY_NATIVE_ORIGINAL_SCHEMA_INVALID')
        copy_id = original['CopyId']
        require(copy_id in required and copy_id not in seen, 'COPY_NATIVE_ORIGINAL_SUBSTITUTED')
        seen.add(copy_id)
        record, raw = required[copy_id]
        enrollment, registry, writer, retained = (original[name] for name in ('SourceEnrollment', 'RegistryOriginal', 'WriterProof', 'ProducerOriginal'))
        binding = enrollment['Binding']
        root = enrollment['RootIdentity']
        require(root['TenantId'] == record['target']['TenantId'] and root['ObjectId'] == record['target']['AgentInteractionId']
                and root['KeyAlias'] == record['target']['TargetProtectionKeyAlias'] and root['StoreTarget'] == producer['StoreId']
                and binding['Identity']['Owner']['TenantId'] == record['target']['TenantId']
                and record['rootReference'] == hashlib.sha256(native_json_bytes(root)).hexdigest().upper(), 'COPY_ROOT_ORIGINAL_SUBSTITUTED')
        require(binding['Identity']['Target'] == record['target'] and registry['Request']['Identity'] == binding['Identity']
                and registry['KeyReference'] == binding['KeyReference'] and registry['WriterState'] == 2 and registry['WriterProofId'] == binding['OperationId'],
                'COPY_SOURCE_REGISTRY_SUBSTITUTED')
        sealed = registry['Sealed']
        require(sealed['KeyReference'] == binding['KeyReference'] and sealed['SerializationFormat'] == 'json+pdenc-v2'
                and sealed['ProtectedPathCount'] == 1 and base64.b64decode(sealed['PayloadBytes'], validate=True) == raw,
                'COPY_CIPHERTEXT_SUBSTITUTED')
        digest_value = hashlib.sha256(raw).hexdigest().upper()
        require(writer['Binding'] == binding and writer['Persisted'] is True and writer['WriterRetired'] is True
                and writer['SealedPayloadSha256'] == digest_value and isinstance(writer['ReceiptId'], str) and writer['ReceiptId'].strip(),
                'COPY_WRITER_ORIGINAL_SUBSTITUTED')
        bind_execution(retained)
        purposes = retained.get('CopyPurposes')
        location = record['sourceIdentity']
        if record['kind'] in ('backup', 'restored'):
            require(location_originals[location]['CopyOriginal']['SourceIdentity'] in (purposes or {}), 'COPY_PURPOSE_SUBSTITUTED')
        else:
            legacy_kind = 'snapshot' if binding['Identity']['Kind'] == 2 else ('target-event' if binding['Identity']['Owner']['AggregateId'] == record['target']['AgentInteractionId'] else 'source-outbox')
            require((isinstance(purposes, dict) and purposes.get(location) == record['kind'])
                    or (purposes is None and retained.get('SourceIdentity') == location and record['kind'] == legacy_kind), 'COPY_PURPOSE_SUBSTITUTED')
        require(retained['Bindings'] == [binding] and retained['SealedPayloadSha256'] == digest_value and retained['Producer'] == producer,
                'COPY_PRODUCER_ORIGINAL_SUBSTITUTED')
        completion = retained['Completion']
        native_record(completion, 'Identity Occurrence OperationId KeyReference DekVersion LifecycleEpoch', 'COPY_SINGLE_COMPLETION_SCHEMA_INVALID')
        require(type(completion['DekVersion']) is int and completion['DekVersion'] == 1, 'COPY_COMPLETION_VERSION_SUBSTITUTED')
        require(retained['CompletionDigest'] == hashlib.sha256(native_json_bytes(completion)).hexdigest().upper()
                and completion['Identity'] == binding['Identity']['Owner'] and completion['OperationId'] == binding['OperationId']
                and completion['KeyReference'] == binding['KeyReference'] and completion['LifecycleEpoch'] == binding['LifecycleEpoch']
                and completion['Occurrence'] == {'RecordSequence': binding['Identity']['Sequence'], 'PayloadKind': binding['Identity']['Kind'],
                                                 'PayloadTypeId': binding['Identity']['PayloadTypeId']}, 'COPY_COMPLETION_SUBSTITUTED')
    return producer


def _evaluate(observation, case, targets, canary, commit, binary_sha, capability_trust, role_configs, budget):
    """Assert raw persisted state and ordered receipts; no aggregate pass booleans."""
    require(observation['engineSourceRevision'] == commit and observation['engineBinarySha256'] == binary_sha, 'ENGINE_IDENTITY_MISMATCH')
    require(observation['agentsRuntimeEnabled'] is False, 'AGENTS_RUNTIME_FORBIDDEN')
    shipped_case = isinstance(case.get('id'), str) and re.fullmatch(r'P(?:0[1-9]|[1-3][0-9]|4[01])', case['id']) is not None
    if shipped_case or isinstance(role_configs, ValidatedRoleConfigurations) or observation.get('producerIdentity') is not None or observation.get('copyOriginals') is not None:
        bind_producer_originals(observation, commit, binary_sha, role_configs)
    roots = observation['roots']
    require(sorted(target_key(r['target']) for r in roots) == sorted(map(target_key, targets)), 'ROOT_INVENTORY_MISMATCH')
    checks = set(case['checks'])
    native_receipts, requests, capability_snapshot = [], {}, None
    if 'capability' in checks or 'receipts' in checks or 'signing-attempts' in checks or observation.get('consumptionRequests') is not None:
        capability_snapshot = validate_capability_trust(capability_trust, role_configs or {}, budget)
        if 'replacement-blocked' in checks and (observation.get('rejectedUnissuedOriginal') is not None or observation.get('blockedReplacementOriginal') is not None):
            requests, native_receipts = replacement_phase_evidence(observation, targets, capability_snapshot)
        else:
            requests, native_receipts = original_evidence(observation, targets, capability_trust, role_configs or {}, 'receipts' in checks, budget, capability_snapshot)
    states = [r['state'] for r in roots]
    require(all(s in ('Active', 'Pinned', 'Destroyed') for s in states), 'ROOT_STATE_UNKNOWN')
    for record in observation['records']:
        require(record['kind'] in ('source-outbox', 'target-event', 'snapshot', 'cache', 'backup', 'restored'), 'COPY_KIND_UNKNOWN')
        require(target_key(record['target']) in set(map(target_key, targets)), 'COPY_TARGET_SUBSTITUTED')
        raw = base64.b64decode(record['bytesBase64'], validate=True)
        require(raw and canary not in raw, 'PLAINTEXT_PERSISTED')
        protected = strict_json(raw.decode('utf-8'))
        if record['kind'] in ('source-outbox', 'target-event'):
            encoded = event_record_envelope(protected, record['target'], raw)
        else:
            encoded_values = snapshot_record_envelopes(protected, record['target'])
        for encoded in [encoded] if record['kind'] in ('source-outbox', 'target-event') else encoded_values:
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
    bind_snapshot_originals(observation)
    if 'active' in checks:
        require(all(s in ('Active', 'Pinned') for s in states), 'UNEXPECTED_DESTRUCTION')
    if 'erased' in checks:
        require(all(s == 'Destroyed' for s in states), 'INCOMPLETE_ERASURE')
        replays = observation['replays']
        require(isinstance(replays, list), 'ERASED_REPLAY_MISSING')
        records = observation['records']
        record_index = {(r.get('copyId'), target_key(r['target'])): r for r in records}
        require(len(record_index) == len(records) and all(isinstance(key[0], str) and key[0] for key in record_index), 'ERASED_COPY_IDENTITY_INVALID')
        seen = set()
        for replay in replays:
            require(isinstance(replay, dict) and replay.get('field') == {'State': 'Erased'} and replay.get('externalOutcome') == 'Unreadable', 'ERASED_REPLAY_INVALID')
            key = (replay.get('copyId'), target_key(replay['target']))
            require(key in record_index and key not in seen, 'ERASED_REPLAY_COPY_SUBSTITUTED')
            seen.add(key)
            record = record_index[key]
            require(all(replay.get(field) == record.get(field) and replay.get(field) is not None for field in ('kind', 'sourceIdentity', 'sourceDigest')), 'ERASED_REPLAY_SOURCE_SUBSTITUTED')
        require(seen == set(record_index) and expected_pairs <= {(target_key(r['target']), r['kind']) for r in replays}, 'ERASED_REPLAY_MISSING')
    if 'atomic' in checks:
        require(len(set(states)) == 1, 'PARTIAL_PROVIDER_EFFECT')
    if 'same-root' in checks:
        for target in targets:
            refs = [r['rootReference'] for r in observation['records'] if r['target'] == target]
            require(len(set(refs)) == 1 and bool(refs), 'INTERACTION_ROOT_DIVERGED')
    if 'no-effect' in checks or any(step.startswith('Reject') for step in case['steps']) or 'rejectedEffectOriginals' in observation:
        if shipped_case or isinstance(role_configs, ValidatedRoleConfigurations) or observation.get('producerIdentity') is not None:
            effects = observation.get('rejectedEffectOriginals')
            require(isinstance(effects, list) and effects, 'INDEPENDENT_EFFECT_ORIGINALS_MISSING')
            before, after, effect_calls = [], [], set()
            required_rejections = {step for step in case['steps'] if step.startswith('Reject')}
            covered_rejections = set()
            for effect in effects:
                native_record(effect, 'Call Before After', 'EFFECT_ORIGINAL_SCHEMA_INVALID')
                validate_native_call(observation, effect, case, effect=True)
                call = effect['Call']
                require(call['Id'] not in effect_calls, 'EFFECT_ORIGINAL_DUPLICATED')
                effect_calls.add(call['Id'])
                covered_rejections.update(action for action in required_rejections if '/' + action + '/' in call['Id'])
                call_digest = hashlib.sha256(native_json_bytes(call)).hexdigest().upper()
                for phase, values in (('before', before), ('after', after)):
                    proof = effect['Before' if phase == 'before' else 'After']
                    native_record(proof, 'TenantId StoreId EnrollmentId CallDigest Boundary CustodyStateSha256 NativePolicy Fingerprint ReceiptId', 'EFFECT_ORIGINAL_SCHEMA_INVALID')
                    require(proof['CallDigest'] == call_digest and proof['Boundary'] == phase and proof['TenantId'] == targets[0]['TenantId']
                            and proof['StoreId'] == observation['producerIdentity']['StoreId']
                            and proof['EnrollmentId'] == observation['producerIdentity']['EnrollmentId'], 'EFFECT_ORIGINAL_SUBSTITUTED')
                    native_record(proof['NativePolicy'], 'LatestVersion MinEncryptionVersion MinDecryptionVersion MinAvailableVersion Derived Exportable AllowPlaintextBackup DeletionAllowed', 'NATIVE_EFFECT_POLICY_MISSING')
                    require(all(type(proof['NativePolicy'][name]) is int and proof['NativePolicy'][name] >= 0
                                for name in ('LatestVersion', 'MinEncryptionVersion', 'MinDecryptionVersion', 'MinAvailableVersion')), 'NATIVE_EFFECT_POLICY_INVALID')
                    require(isinstance(proof['CustodyStateSha256'], str) and re.fullmatch(r'[0-9A-F]{64}', proof['CustodyStateSha256'])
                            and all(type(proof['NativePolicy'][name]) is bool for name in ('Derived', 'Exportable', 'AllowPlaintextBackup', 'DeletionAllowed')), 'NATIVE_EFFECT_POLICY_INVALID')
                    digest = hashlib.sha256(native_json_bytes({'CustodyStateSha256': proof['CustodyStateSha256'], 'NativePolicy': proof['NativePolicy']})).hexdigest().upper()
                    receipt = 'erc-effect:' + hashlib.sha256(native_json_bytes({'tenant': proof['TenantId'], 'StoreId': proof['StoreId'], 'EnrollmentId': proof['EnrollmentId'],
                        'call': call_digest, 'boundary': phase, 'fingerprint': digest})).hexdigest().upper()
                    require(digest == proof['Fingerprint'] and proof['ReceiptId'] == receipt, 'EFFECT_FINGERPRINT_SUBSTITUTED')
                    values.append(digest)
                require(effect['Before']['Fingerprint'] == effect['After']['Fingerprint'], 'REJECTED_OPERATION_HAD_EFFECT')
            require(covered_rejections == required_rejections, 'INDEPENDENT_EFFECT_ORIGINALS_MISSING')
            if 'no-effect' in checks:
                require(hashlib.sha256(native_json_bytes(before)).hexdigest().upper() == observation['effectRevisionBeforeRejectedOperation']
                    and hashlib.sha256(native_json_bytes(after)).hexdigest().upper() == observation['effectRevisionAfterRejectedOperation'], 'INDEPENDENT_EFFECT_BOUNDARY_SUBSTITUTED')
        if 'no-effect' in checks:
            require(observation['effectRevisionAfterRejectedOperation'] == observation['effectRevisionBeforeRejectedOperation'], 'REJECTED_OPERATION_HAD_EFFECT')
    if 'pin' in checks:
        require([r['status'] for r in observation['pinOutcomes']] == ['Pinned', 'HoldPinned', 'HoldPinned', 'Authorized', 'Unpinned'], 'HOLD_PIN_PROOF_INVALID')
        bind_pin_originals(observation, targets)
    if 'exact-results' in checks:
        bind_exact_recovery(observation, case)
    if 'copies' in checks or 'restore' in checks:
        inventory = observation['copyInventoryBefore']
        require(inventory and len(inventory) == len(set(inventory)) == len(observation['copyStatesAfter']) and set(inventory) == {r['copyId'] for r in observation['copyStatesAfter']}, 'COPY_INVENTORY_INCOMPLETE')
        if 'erased' in checks:
            require(all(r['readOutcome'] == 'Erased' for r in observation['copyStatesAfter']), 'RESTORE_REVIVED_CONTENT')
        if 'restore' in checks:
            bind_restore_originals(observation)
    if 'blocked' in checks:
        require(observation['ownerOutcome']['status'] == 'ConsumptionBlocked' and observation['ownerOutcome']['receiptId'], 'AUTHENTICATED_BLOCK_MISSING')
    if case.get('id') == 'P34' or 'CompromiseReplacementAfterActivation' in case.get('steps', []) or observation.get('postActivationBlockOriginal') is not None:
        require(observation.get('postActivationBlockOriginal') is not None, 'POST_ACTIVATION_BLOCK_ORIGINAL_MISSING')
        bind_post_activation_block_original(observation)
    if 'replacement-blocked' in checks:
        value = observation['activationOutcome']
        expected_status = 'ActivatedThenReplacementBlocked' if observation.get('postActivationBlockOriginal') is not None else 'ActivationBlockedByReplacementKeyCompromise'
        require(value['status'] == expected_status and value['newBlockReceiptId'] \
                and value['newKeyVersion'] == observation['ownerOutcome']['blockedKeyVersion'] \
                and value['revocationRevision'] == observation['ownerOutcome']['revocationRevision'] \
                and observation['ownerOutcome']['status'] == 'ConsumptionBlocked', 'REPLACEMENT_COMPROMISE_NOT_PRESERVED')
        bind_replacement_original(observation)
    if 'race' in checks and 'hold-order' not in checks:
        events = observation['ownerTransitions']
        winners = [r for r in events if r['to'] in ('ConsumptionReserved', 'ConsumptionBlocked')]
        require(len(winners) == 1 and winners[0]['from'] == 'Unconsumed', 'BLOCK_RESERVE_NOT_SINGLE_WINNER')
        require(all(s == 'Destroyed' for s in states) if winners[0]['to'] == 'ConsumptionReserved' else all(s != 'Destroyed' for s in states), 'RACE_STATE_CONTRADICTION')
    if 'reserved-recovery' in checks:
        bind_reservation_recovery(observation, case)
    if 'hold-order' in checks:
        require(bool(observation['holdOrders']), 'HOLD_ORDER_PROOF_MISSING')
        for hold in observation['holdOrders']:
            require(hold['holdOperationId'] and hold['dispatchOperationId'], 'HOLD_ORDER_IDENTITY_MISSING')
            refused_dispatch = hold.get('dispatchStatus', 'Committed') in ('Blocked', 'Stale')
            require(type(hold['dispatchWon']) is bool and type(hold['holdRevision']) is int
                    and type(hold['dispatchRevision']) is int and hold['holdRevision'] > 0
                    and hold['dispatchRevision'] > 0 and (refused_dispatch or hold['holdRevision'] != hold['dispatchRevision']),
                    'HOLD_ORDER_REVISION_INVALID')
            require((hold['dispatchWon'] is False and hold['holdRevision'] <= hold['dispatchRevision']) if refused_dispatch
                    else (hold['holdRevision'] > hold['dispatchRevision']) == hold['dispatchWon'], 'HOLD_DISPATCH_ORDER_MISMATCH')
        if 'race' in checks:
            # A hold that wins before dispatch never reaches the protection owner's
            # post-dispatch block/reserve race. Do not require a fabricated reservation.
            require(len(observation['holdOrders']) == 1, 'HOLD_RACE_OUTCOME_AMBIGUOUS')
            dispatch_won = observation['holdOrders'][0]['dispatchWon']
            require(all(s == 'Destroyed' for s in states) if dispatch_won else all(s != 'Destroyed' for s in states),
                    'HOLD_RACE_STATE_CONTRADICTION')
        if any(hold.get('dispatchStatus', 'Committed') != 'Committed' for hold in observation['holdOrders']):
            hold_snapshot = capability_snapshot or validate_capability_trust(capability_trust, role_configs or {}, budget)
            for attempt in observation.get('guardAttempts', []):
                if attempt.get('Transition', {}).get('Operation') == 21:
                    batch = attempt['Transition']['Batch']
                    native_record(batch, 'BatchId BatchKind BatchOrdinal Targets ManifestDigest AttestationOrdinal CapabilityKeyVersion AttestationDigest ProtectionOutcome ProtectionReceiptId TargetReceiptIds BlockSetRevision Capability DetachedJws SigningRequestId', 'HOLD_DISPATCH_BATCH_INVALID')
                    signing_id, signature_digest = validate_capability_signature(batch['Capability'], batch['DetachedJws'], None, None, hold_snapshot)
                    require(batch['SigningRequestId'] == signing_id and batch['AttestationDigest'] == signature_digest
                            and batch['BatchId'] == batch['Capability']['BatchId'] and batch['ManifestDigest'] == batch['Capability']['ManifestDigest'], 'HOLD_DISPATCH_BATCH_INVALID')
        bind_hold_originals(observation)
        bind_hold_scope(observation, targets, capability_snapshot or validate_capability_trust(capability_trust, role_configs or {}, budget))
    if 'coverage' in checks or 'containment' in checks:
        groups = observation['targetCoverage']
        require(sorted(target_key(g['target']) for g in groups) == sorted(map(target_key, targets)), 'TARGET_COVERAGE_INCOMPLETE')
        require(all(len(set(g['batchIds'])) == 1 and len(set(g['ordinals'])) == 1 for g in groups), 'SAME_TARGET_ALLOCATED_SUCCESSOR_BATCH')
        if 'containment' in checks:
            require(len({g['ordinals'][0] for g in groups}) == len(groups) and all(g['singletonManifest'] and not g['reusesAcceptedSetBatch'] for g in groups), 'CONTAINMENT_ORDINAL_OR_MANIFEST_REUSED')
        if 'coverage' in checks:
            bind_coverage_originals(observation)
    if 'already-destroyed' in checks:
        request = observation.get('coverageRequest')
        capture_consumption_request(request)
        value = native_owner_outcome(observation.get('coverageOutcome'), 4)
        require(value['TenantId'] == request['Capability']['TenantId'] == targets[0]['TenantId']
                and value['BatchId'] == request['Capability']['BatchId']
                and canonical(request['Targets']) == canonical(targets)
                and canonical(value['TargetReceipts']) == canonical(native_receipts)
                and all(receipt['OriginalBatchId'] != value['BatchId'] for receipt in native_receipts)
                and native_receipts,
                'ORIGINAL_COVERAGE_RECEIPT_LOST')
    if 'reattested' in checks:
        value = observation['reattestation']
        require(value['sameBatch'] is True and value['sameManifest'] is True and value['oldAttestationRevoked'] is True \
                and value['activeCredentialCount'] == 1 and value['compromiseBlockReceiptId'] \
                and value['expectedBlockSetRevision'] == value['activatedBlockSetRevision'], 'RE_ATTESTATION_AUTHORITY_INVALID')
        bind_activation_original(observation)
    if 'signing-attempts' in checks:
        signing_attempt_evidence(observation, requests, capability_snapshot)


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


def require_executed_operation_producer(original, expected, binding, attempt_name, selected_producer):
    native_record(original, 'OperationId RequestSha256 Role BindingDigest AttemptId Producer SafeRequest', 'OPERATION_PRODUCER_ORIGINAL_SCHEMA_INVALID')
    require(original['OperationId'] == expected['operationId'] and original['RequestSha256'] == expected['requestSha256']
            and original['Role'] == 'executor' and original['BindingDigest'] == binding and original['AttemptId'] == attempt_name
            and original['Producer'] == selected_producer, 'OPERATION_PRODUCER_ORIGINAL_SUBSTITUTED')


def execute(profile, record, plan, attempt, recover=None):
    binding = digest({'target': profile['targetCommit'], 'platformTarget': profile['platformTargetCommit'],
                      'roleProgramContract': profile['roleProgramContract'], 'profile': record['profileSha256'], 'plan': profile['planSha256'],
                      'anchors': {name: profile['roles'][name]['publicAnchorSha256'] for name in ROLE_NAMES}})
    durable(attempt / 'binding.json', {'bindingDigest': binding, 'profileSha256': record['profileSha256'], 'targetCommit': profile['targetCommit'],
                                     'platformTargetCommit': profile['platformTargetCommit'], 'roleProgramContract': profile['roleProgramContract'],
                                     'recoveryOf': str(recover) if recover else None})
    trust_snapshot = validate_capability_trust(profile['capabilityTrust'], profile['roles'])
    roles = {name: Role(name, profile['roles'][name], binding, attempt, profile['engineBinary'], profile['capabilityTrust'], profile['roles'], trust_snapshot) for name in ROLE_NAMES}
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
            permission = value.get('observerPermissionEvidence')
            require(isinstance(permission, dict) and permission.get('CredentialScope') == 'ReadPersistedStateOnly'
                    and permission.get('RoleIdentity') == profile['roles']['observer']['identity']
                    and permission.get('BindingDigest') == binding and permission.get('ReceiptId')
                    and permission.get('NativeCredentialSha256') != permission.get('DaprCredentialSha256'), 'OBSERVER_PERMISSION_ORIGINAL_MISSING')
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
        selected_producer = bind_producer_originals(response(observed), profile['targetCommit'], profile['engineBinary']['sha256'], profile['roles'])
        require(selected_producer['BindingDigest'] == binding, 'EXECUTOR_INSTALLATION_BINDING_SUBSTITUTED')
        producer_originals = response(observed).get('operationProducerOriginals')
        require(isinstance(producer_originals, list) and len(producer_originals) == len(expected_operations), 'OPERATION_PRODUCER_ORIGINALS_MISSING')
        for original, expected in zip(producer_originals, expected_operations):
            require_executed_operation_producer(original, expected, binding, attempt.name, selected_producer)
        evaluate(response(observed), case, targets, canary, profile['targetCommit'], profile['engineBinary']['sha256'], profile['capabilityTrust'], profile['roles'])
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
        paired_checkout_preflight(ROOT, record, profile)
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

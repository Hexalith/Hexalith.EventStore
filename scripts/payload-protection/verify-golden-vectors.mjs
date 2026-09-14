#!/usr/bin/env node

// Story 8.2 independent Node.js verifier for Story 8.1 sections 7, 8.5, and 15.4.
// Normative digest: de9ba8866fd98a480629890ee2b89a492fbad96d4d5a927388e6aaa0fdd72b4e.

import crypto from 'node:crypto';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const scriptDirectory = path.dirname(fileURLToPath(import.meta.url));
const repositoryRoot = path.resolve(scriptDirectory, '..', '..');
const fixtureDirectory = path.join(
  repositoryRoot,
  'tests',
  'Hexalith.EventStore.Contracts.Tests',
  'Security',
  'Fixtures',
  'PayloadProtectionV2');
const approvedDigest = 'de9ba8866fd98a480629890ee2b89a492fbad96d4d5a927388e6aaa0fdd72b4e';
const approvedFixtureHashes = {
  'g-001.json': 'a821f36321bd5a020b35f89b1e3d18e7dc3070e3dbf694db5c0413847a012610',
  'nist-gcm-256-count0.json': '528a81472dc6bd4db653bf01dc52a16d5e092c7bcde2ef622f42878ba794a319',
  'vector-ownership.json': 'a3886eac22cf3b77c210ae2bb166f237980bc5e8cf1e9cbaf8c569aa6b4dc087',
};
const requiredManifestPaths = [
  '../../../../../scripts/payload-protection/requirements.txt',
  '../../../../../scripts/payload-protection/verify-golden-vectors.mjs',
  '../../../../../scripts/payload-protection/verify-golden-vectors.py',
  'g-001.json',
  'nist-gcm-256-count0.json',
  'vector-ownership.json',
].sort();

const readJson = name => JSON.parse(fs.readFileSync(path.join(fixtureDirectory, name), 'utf8'));
const sha256 = value => crypto.createHash('sha256').update(value).digest('hex');
const textBytes = value => Buffer.from(value, 'utf8');
const fromHex = value => Buffer.from(value, 'hex');
const u32 = value => {
  const bytes = Buffer.alloc(4);
  bytes.writeUInt32BE(value);
  return bytes;
};
const u64 = value => {
  const bytes = Buffer.alloc(8);
  bytes.writeBigUInt64BE(BigInt(value));
  return bytes;
};
const field = (identifier, type, value) => Buffer.concat([
  Buffer.from([identifier, type]),
  u32(value.length),
  value,
]);
const assertEqual = (name, actual, expected) => {
  if (actual !== expected) {
    throw new Error(`${name} mismatch`);
  }
};

const normativeBytes = fs.readFileSync(path.join(
  repositoryRoot,
  '_bmad-output',
  'implementation-artifacts',
  'spec-shared-payload-protection-engine.md'));
const beginMarker = textBytes('<!-- HX-PP-V2-NORMATIVE-BEGIN -->\n');
const endMarker = textBytes('<!-- HX-PP-V2-NORMATIVE-END -->\n');
const beginIndex = normativeBytes.indexOf(beginMarker);
const endIndex = normativeBytes.indexOf(endMarker, beginIndex + beginMarker.length);
assertEqual('normative-begin-marker', beginIndex >= 0 && beginIndex === normativeBytes.lastIndexOf(beginMarker), true);
assertEqual('normative-end-marker', endIndex >= 0 && endIndex === normativeBytes.lastIndexOf(endMarker), true);
assertEqual('normative-lf-only', normativeBytes.includes(13), false);
assertEqual('normative-no-bom', normativeBytes.subarray(0, 3).equals(Buffer.from([0xef, 0xbb, 0xbf])), false);
const recomputedDigest = sha256(normativeBytes.subarray(beginIndex + beginMarker.length, endIndex));
assertEqual('approved-normative-digest', recomputedDigest, approvedDigest);

const golden = readJson('g-001.json');
assertEqual('golden-file-hash', sha256(fs.readFileSync(path.join(fixtureDirectory, 'g-001.json'))), approvedFixtureHashes['g-001.json']);
assertEqual('golden-schema-version', golden.schemaVersion, 1);
assertEqual('golden-vector-id', golden.vectorId, 'V001');
assertEqual('golden-normative-digest', golden.normativeDigest, approvedDigest);
const input = golden.input;
const textInputs = [
  input.tenant,
  input.domain,
  input.aggregate,
  input.payloadType,
  input.propertyPath,
  input.keyReference,
  input.serializationFormat,
  input.plaintext,
  ...golden.pathManifest.entries,
];
for (const [index, value] of textInputs.entries()) {
  assertEqual(`utf8-${index}`, textBytes(value.text).toString('hex'), value.utf8Hex);
}

const paths = golden.pathManifest.entries
  .map(entry => textBytes(entry.text))
  .sort(Buffer.compare);
const manifest = Buffer.concat([
  textBytes('HXPM'),
  Buffer.from([1]),
  u32(paths.length),
  ...paths.flatMap(value => [u32(value.length), value]),
]);
assertEqual('manifest', manifest.toString('hex'), golden.pathManifest.encodedHex);
assertEqual('manifest-sha256', sha256(manifest), golden.pathManifest.sha256);

const manifestHash = crypto.createHash('sha256').update(manifest).digest();
const aadValues = [
  textBytes(input.tenant.text),
  textBytes(input.domain.text),
  textBytes(input.aggregate.text),
  textBytes(input.payloadType.text),
  textBytes(input.propertyPath.text),
  textBytes(input.keyReference.text),
  u32(input.dekVersion),
  textBytes(input.serializationFormat.text),
  u32(input.fieldOrdinal),
  u64(input.recordSequence),
  manifestHash,
];
const aadTypes = [1, 1, 1, 1, 1, 1, 2, 1, 2, 3, 4];
for (let index = 0; index < aadValues.length; index += 1) {
  const fixtureField = golden.aadFields[index];
  assertEqual(`aad-field-id-${index}`, fixtureField.id, index + 1);
  assertEqual(`aad-field-type-${index}`, fixtureField.type, aadTypes[index]);
  assertEqual(`aad-field-value-${index}`, aadValues[index].toString('hex'), fixtureField.valueHex);
}
const aad = Buffer.concat([
  textBytes('HXAD'),
  Buffer.from([1, 1, 11, 0]),
  ...aadValues.map((value, index) => field(index + 1, aadTypes[index], value)),
]);
assertEqual('aad', aad.toString('hex'), golden.expected.aadHex);
assertEqual('aad-sha256', sha256(aad), golden.expected.aadSha256);

const key = fromHex(input.dekHex);
const nonce = fromHex(input.nonceHex);
const plaintext = textBytes(input.plaintext.text);
const cipher = crypto.createCipheriv('aes-256-gcm', key, nonce, { authTagLength: 16 });
cipher.setAAD(aad, { plaintextLength: plaintext.length });
const ciphertext = Buffer.concat([cipher.update(plaintext), cipher.final()]);
const tag = cipher.getAuthTag();
assertEqual('ciphertext', ciphertext.toString('hex'), golden.expected.ciphertextHex);
assertEqual('tag', tag.toString('hex'), golden.expected.tagHex);

const header = Buffer.alloc(28);
header.write('HXP2', 0, 'ascii');
header.set([2, 1, 1, 1], 4);
header.writeUInt16BE(28, 8);
header.writeUInt16BE(26, 10);
header.writeUInt32BE(input.dekVersion, 12);
header.writeUInt32BE(input.fieldOrdinal, 16);
header.set([12, 16, 0, 0], 20);
header.writeUInt32BE(ciphertext.length, 24);
const envelope = Buffer.concat([
  header,
  textBytes(input.keyReference.text),
  nonce,
  ciphertext,
  tag,
]);
const envelopeBase64Url = envelope.toString('base64url');
const wrapper = textBytes(`{"$pdenc":"${envelopeBase64Url}"}`);
assertEqual('envelope', envelope.toString('hex'), golden.expected.envelopeHex);
assertEqual('envelope-base64url', envelopeBase64Url, golden.expected.envelopeBase64Url);
assertEqual('envelope-sha256', sha256(envelope), golden.expected.envelopeSha256);
assertEqual('wrapper', wrapper.toString('utf8'), golden.expected.wrapper);
assertEqual('wrapper-sha256', sha256(wrapper), golden.expected.wrapperSha256);

const decodedEnvelope = Buffer.from(golden.expected.envelopeBase64Url, 'base64url');
assertEqual('decoded-envelope-canonical', decodedEnvelope.toString('base64url'), golden.expected.envelopeBase64Url);
assertEqual('decoded-envelope-bytes', decodedEnvelope.toString('hex'), golden.expected.envelopeHex);
assertEqual('decoded-magic', decodedEnvelope.subarray(0, 4).toString('ascii'), 'HXP2');
assertEqual('decoded-header', decodedEnvelope.subarray(4, 8).toString('hex'), '02010101');
assertEqual('decoded-header-length', decodedEnvelope.readUInt16BE(8), 28);
assertEqual('decoded-key-reference-length', decodedEnvelope.readUInt16BE(10), 26);
assertEqual('decoded-dek-version', decodedEnvelope.readUInt32BE(12), input.dekVersion);
assertEqual('decoded-field-ordinal', decodedEnvelope.readUInt32BE(16), input.fieldOrdinal);
assertEqual('decoded-lengths', decodedEnvelope.subarray(20, 24).toString('hex'), '0c100000');
const decodedCiphertextLength = decodedEnvelope.readUInt32BE(24);
assertEqual('decoded-envelope-length', decodedEnvelope.length, 28 + 26 + 12 + decodedCiphertextLength + 16);
const decodedKeyReference = decodedEnvelope.subarray(28, 54);
const decodedNonce = decodedEnvelope.subarray(54, 66);
const decodedCiphertext = decodedEnvelope.subarray(66, 66 + decodedCiphertextLength);
const decodedTag = decodedEnvelope.subarray(66 + decodedCiphertextLength);
assertEqual('decoded-key-reference', decodedKeyReference.toString('utf8'), input.keyReference.text);
const decipher = crypto.createDecipheriv('aes-256-gcm', key, decodedNonce, { authTagLength: 16 });
decipher.setAAD(aad, { plaintextLength: decodedCiphertext.length });
decipher.setAuthTag(decodedTag);
const decrypted = Buffer.concat([decipher.update(decodedCiphertext), decipher.final()]);
assertEqual('decrypted-plaintext', decrypted.toString('hex'), input.plaintext.utf8Hex);

const nist = readJson('nist-gcm-256-count0.json');
assertEqual('nist-file-hash', sha256(fs.readFileSync(path.join(fixtureDirectory, 'nist-gcm-256-count0.json'))), approvedFixtureHashes['nist-gcm-256-count0.json']);
assertEqual('nist-schema-version', nist.schemaVersion, 1);
assertEqual('nist-vector-id', nist.vectorId, 'V003');
assertEqual('nist-source', nist.source, 'NIST CAVP gcmEncryptExtIV256.rsp Count 0');
assertEqual('nist-profile', JSON.stringify(nist.profile), JSON.stringify({ keyBits: 256, ivBits: 96, plaintextBits: 0, aadBits: 0, tagBits: 128 }));
assertEqual('nist-key', nist.keyHex, 'b52c505a37d78eda5dd34f20c22540ea1b58963cf8e5bf8ffa85f9f2492505b4');
assertEqual('nist-iv', nist.ivHex, '516c33929df5a3284ff463d7');
assertEqual('nist-tag-reference', nist.tagHex, 'bdc1ac884d332457a1d2664f168c76f0');
const nistCipher = crypto.createCipheriv(
  'aes-256-gcm',
  fromHex(nist.keyHex),
  fromHex(nist.ivHex),
  { authTagLength: 16 });
nistCipher.setAAD(fromHex(nist.aadHex), { plaintextLength: 0 });
const nistCiphertext = Buffer.concat([
  nistCipher.update(fromHex(nist.plaintextHex)),
  nistCipher.final(),
]);
assertEqual('nist-ciphertext', nistCiphertext.toString('hex'), nist.ciphertextHex);
assertEqual('nist-tag', nistCipher.getAuthTag().toString('hex'), nist.tagHex);

const ownership = readJson('vector-ownership.json');
assertEqual('ownership-file-hash', sha256(fs.readFileSync(path.join(fixtureDirectory, 'vector-ownership.json'))), approvedFixtureHashes['vector-ownership.json']);
assertEqual('ownership-schema-version', ownership.schemaVersion, 1);
assertEqual('ownership-normative-digest', ownership.normativeDigest, approvedDigest);
assertEqual('ownership-registry', ownership.normativeRegistry, 'spec-shared-payload-protection-engine.md#15.4');
for (const assignment of ownership.assignments) {
  assertEqual(`range-bounds-${assignment.first}`, Number.isSafeInteger(assignment.first)
    && Number.isSafeInteger(assignment.last)
    && assignment.first >= 1
    && assignment.last <= 138
    && assignment.first <= assignment.last, true);
  assertEqual(`assignment-state-${assignment.first}`, assignment.state, assignment.first <= 3 ? 'executed' : 'predecessor-gated');
}
const expanded = ownership.assignments.flatMap(assignment =>
  Array.from(
    { length: assignment.last - assignment.first + 1 },
    (_, offset) => assignment.first + offset));
assertEqual('vector-count', expanded.length, 138);
assertEqual('vector-order', expanded.join(','), Array.from({ length: 138 }, (_, index) => index + 1).join(','));
const executed = ownership.assignments
  .filter(assignment => assignment.state === 'executed')
  .flatMap(assignment => Array.from(
    { length: assignment.last - assignment.first + 1 },
    (_, offset) => `V${String(assignment.first + offset).padStart(3, '0')}`));
assertEqual('executed-vectors', executed.join(','), 'V001,V002,V003');

const fixtureManifest = readJson('manifest.json');
assertEqual('manifest-normative-digest', fixtureManifest.normativeDigest, approvedDigest);
const manifestPaths = fixtureManifest.files.map(entry => entry.path);
assertEqual('manifest-path-uniqueness', new Set(manifestPaths).size, manifestPaths.length);
assertEqual('manifest-path-allowlist', [...manifestPaths].sort().join(','), requiredManifestPaths.join(','));
for (const entry of fixtureManifest.files) {
  const resolvedPath = path.resolve(fixtureDirectory, entry.path);
  assertEqual(`manifest-path-contained-${entry.path}`, resolvedPath.startsWith(`${repositoryRoot}${path.sep}`), true);
  const fileBytes = fs.readFileSync(resolvedPath);
  assertEqual(`file-sha256-${entry.path}`, sha256(fileBytes), entry.sha256);
}

const derived = {
  manifestHex: manifest.toString('hex'),
  manifestSha256: sha256(manifest),
  aadHex: aad.toString('hex'),
  aadSha256: sha256(aad),
  ciphertextHex: ciphertext.toString('hex'),
  tagHex: tag.toString('hex'),
  envelopeHex: envelope.toString('hex'),
  envelopeBase64Url,
  envelopeSha256: sha256(envelope),
  wrapper: wrapper.toString('utf8'),
  wrapperSha256: sha256(wrapper),
};
assertEqual('manifest-schema-version', fixtureManifest.schemaVersion, 1);
assertEqual('manifest-story', fixtureManifest.story, '8.2');
assertEqual('manifest-executed-vectors', fixtureManifest.expected.executedVectors.join(','), executed.join(','));
assertEqual('manifest-future-vector-count', fixtureManifest.expected.predecessorGatedVectorCount, 135);
assertEqual('manifest-path-manifest-sha256', fixtureManifest.expected.manifestSha256, derived.manifestSha256);
assertEqual('manifest-aad-sha256', fixtureManifest.expected.aadSha256, derived.aadSha256);
assertEqual('manifest-envelope-sha256', fixtureManifest.expected.envelopeSha256, derived.envelopeSha256);
assertEqual('manifest-wrapper-sha256', fixtureManifest.expected.wrapperSha256, derived.wrapperSha256);
assertEqual('manifest-outcome', fixtureManifest.expected.outcome, 'PASS');

if (process.argv.includes('--emit-derived')) {
  process.stdout.write(`${JSON.stringify(derived, null, 2)}\n`);
} else {
  process.stdout.write(`${JSON.stringify({
    verifier: 'node',
    node: process.version,
    openssl: process.versions.openssl,
    normativeDigest: golden.normativeDigest,
    executedVectors: executed,
    futureVectors: 135,
    envelopeSha256: derived.envelopeSha256,
    wrapperSha256: derived.wrapperSha256,
    result: 'PASS',
  })}\n`);
}

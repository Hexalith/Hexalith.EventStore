#!/usr/bin/env python3
"""Independent Story 8.2 verifier for Story 8.1 sections 7, 8.5, and 15.4.

Normative digest: de9ba8866fd98a480629890ee2b89a492fbad96d4d5a927388e6aaa0fdd72b4e.
"""

from __future__ import annotations

import base64
import hashlib
import json
import ssl
import struct
import sys
from pathlib import Path

import cryptography
from cryptography.hazmat.primitives.ciphers.aead import AESGCM


REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
FIXTURE_DIRECTORY = (
    REPOSITORY_ROOT
    / "tests"
    / "Hexalith.EventStore.Contracts.Tests"
    / "Security"
    / "Fixtures"
    / "PayloadProtectionV2"
)
APPROVED_DIGEST = "de9ba8866fd98a480629890ee2b89a492fbad96d4d5a927388e6aaa0fdd72b4e"
APPROVED_FIXTURE_HASHES = {
    "g-001.json": "a821f36321bd5a020b35f89b1e3d18e7dc3070e3dbf694db5c0413847a012610",
    "nist-gcm-256-count0.json": "528a81472dc6bd4db653bf01dc52a16d5e092c7bcde2ef622f42878ba794a319",
    "vector-ownership.json": "a3886eac22cf3b77c210ae2bb166f237980bc5e8cf1e9cbaf8c569aa6b4dc087",
}
REQUIRED_MANIFEST_PATHS = sorted(
    [
        "../../../../../scripts/payload-protection/requirements.txt",
        "../../../../../scripts/payload-protection/verify-golden-vectors.mjs",
        "../../../../../scripts/payload-protection/verify-golden-vectors.py",
        "g-001.json",
        "nist-gcm-256-count0.json",
        "vector-ownership.json",
    ]
)


def read_json(name: str) -> dict:
    return json.loads((FIXTURE_DIRECTORY / name).read_text(encoding="utf-8"))


def sha256(value: bytes) -> str:
    return hashlib.sha256(value).hexdigest()


def text_bytes(value: str) -> bytes:
    return value.encode("utf-8", errors="strict")


def field(identifier: int, type_id: int, value: bytes) -> bytes:
    return bytes((identifier, type_id)) + struct.pack(">I", len(value)) + value


def check(name: str, actual, expected) -> None:
    if actual != expected:
        raise AssertionError(f"{name} mismatch")


normative_bytes = (
    REPOSITORY_ROOT
    / "_bmad-output"
    / "implementation-artifacts"
    / "spec-shared-payload-protection-engine.md"
).read_bytes()
begin_marker = b"<!-- HX-PP-V2-NORMATIVE-BEGIN -->\n"
end_marker = b"<!-- HX-PP-V2-NORMATIVE-END -->\n"
check("normative-begin-marker", normative_bytes.count(begin_marker), 1)
check("normative-end-marker", normative_bytes.count(end_marker), 1)
check("normative-lf-only", b"\r" in normative_bytes, False)
check("normative-no-bom", normative_bytes.startswith(b"\xef\xbb\xbf"), False)
begin_index = normative_bytes.index(begin_marker) + len(begin_marker)
end_index = normative_bytes.index(end_marker, begin_index)
recomputed_digest = sha256(normative_bytes[begin_index:end_index])
check("approved-normative-digest", recomputed_digest, APPROVED_DIGEST)

golden = read_json("g-001.json")
check("golden-file-hash", sha256((FIXTURE_DIRECTORY / "g-001.json").read_bytes()), APPROVED_FIXTURE_HASHES["g-001.json"])
check("golden-schema-version", golden["schemaVersion"], 1)
check("golden-vector-id", golden["vectorId"], "V001")
check("golden-normative-digest", golden["normativeDigest"], APPROVED_DIGEST)
inputs = golden["input"]
text_inputs = [
    inputs["tenant"],
    inputs["domain"],
    inputs["aggregate"],
    inputs["payloadType"],
    inputs["propertyPath"],
    inputs["keyReference"],
    inputs["serializationFormat"],
    inputs["plaintext"],
    *golden["pathManifest"]["entries"],
]
for index, value in enumerate(text_inputs):
    check(f"utf8-{index}", text_bytes(value["text"]).hex(), value["utf8Hex"])

paths = sorted(text_bytes(entry["text"]) for entry in golden["pathManifest"]["entries"])
manifest = b"HXPM" + b"\x01" + struct.pack(">I", len(paths))
manifest += b"".join(struct.pack(">I", len(value)) + value for value in paths)
check("manifest", manifest.hex(), golden["pathManifest"]["encodedHex"])
check("manifest-sha256", sha256(manifest), golden["pathManifest"]["sha256"])

manifest_hash = hashlib.sha256(manifest).digest()
aad_values = [
    text_bytes(inputs["tenant"]["text"]),
    text_bytes(inputs["domain"]["text"]),
    text_bytes(inputs["aggregate"]["text"]),
    text_bytes(inputs["payloadType"]["text"]),
    text_bytes(inputs["propertyPath"]["text"]),
    text_bytes(inputs["keyReference"]["text"]),
    struct.pack(">I", inputs["dekVersion"]),
    text_bytes(inputs["serializationFormat"]["text"]),
    struct.pack(">I", inputs["fieldOrdinal"]),
    struct.pack(">Q", int(inputs["recordSequence"])),
    manifest_hash,
]
aad_types = [1, 1, 1, 1, 1, 1, 2, 1, 2, 3, 4]
for index, (value, type_id) in enumerate(zip(aad_values, aad_types, strict=True)):
    fixture_field = golden["aadFields"][index]
    check(f"aad-field-id-{index}", fixture_field["id"], index + 1)
    check(f"aad-field-type-{index}", fixture_field["type"], type_id)
    check(f"aad-field-value-{index}", value.hex(), fixture_field["valueHex"])

aad = b"HXAD" + bytes((1, 1, 11, 0))
aad += b"".join(
    field(index + 1, type_id, value)
    for index, (value, type_id) in enumerate(zip(aad_values, aad_types, strict=True))
)
check("aad", aad.hex(), golden["expected"]["aadHex"])
check("aad-sha256", sha256(aad), golden["expected"]["aadSha256"])

key = bytes.fromhex(inputs["dekHex"])
nonce = bytes.fromhex(inputs["nonceHex"])
plaintext = text_bytes(inputs["plaintext"]["text"])
ciphertext_and_tag = AESGCM(key).encrypt(nonce, plaintext, aad)
ciphertext, tag = ciphertext_and_tag[:-16], ciphertext_and_tag[-16:]
check("ciphertext", ciphertext.hex(), golden["expected"]["ciphertextHex"])
check("tag", tag.hex(), golden["expected"]["tagHex"])

header = b"HXP2" + bytes((2, 1, 1, 1))
header += struct.pack(">HHII", 28, 26, inputs["dekVersion"], inputs["fieldOrdinal"])
header += bytes((12, 16, 0, 0)) + struct.pack(">I", len(ciphertext))
envelope = header + text_bytes(inputs["keyReference"]["text"]) + nonce + ciphertext + tag
envelope_base64url = base64.urlsafe_b64encode(envelope).rstrip(b"=").decode("ascii")
wrapper = text_bytes(json.dumps({"$pdenc": envelope_base64url}, separators=(",", ":")))
check("envelope", envelope.hex(), golden["expected"]["envelopeHex"])
check("envelope-base64url", envelope_base64url, golden["expected"]["envelopeBase64Url"])
check("envelope-sha256", sha256(envelope), golden["expected"]["envelopeSha256"])
check("wrapper", wrapper.decode("utf-8"), golden["expected"]["wrapper"])
check("wrapper-sha256", sha256(wrapper), golden["expected"]["wrapperSha256"])
decoded_envelope = base64.urlsafe_b64decode(golden["expected"]["envelopeBase64Url"] + "==")
check(
    "decoded-envelope-canonical",
    base64.urlsafe_b64encode(decoded_envelope).rstrip(b"=").decode("ascii"),
    golden["expected"]["envelopeBase64Url"],
)
check("decoded-envelope-bytes", decoded_envelope.hex(), golden["expected"]["envelopeHex"])
check("decoded-magic", decoded_envelope[:4], b"HXP2")
check("decoded-header", decoded_envelope[4:8], bytes((2, 1, 1, 1)))
check("decoded-header-length", struct.unpack(">H", decoded_envelope[8:10])[0], 28)
check("decoded-key-reference-length", struct.unpack(">H", decoded_envelope[10:12])[0], 26)
check("decoded-dek-version", struct.unpack(">I", decoded_envelope[12:16])[0], inputs["dekVersion"])
check("decoded-field-ordinal", struct.unpack(">I", decoded_envelope[16:20])[0], inputs["fieldOrdinal"])
check("decoded-lengths", decoded_envelope[20:24], bytes((12, 16, 0, 0)))
decoded_ciphertext_length = struct.unpack(">I", decoded_envelope[24:28])[0]
check("decoded-envelope-length", len(decoded_envelope), 28 + 26 + 12 + decoded_ciphertext_length + 16)
decoded_key_reference = decoded_envelope[28:54]
decoded_nonce = decoded_envelope[54:66]
decoded_ciphertext = decoded_envelope[66 : 66 + decoded_ciphertext_length]
decoded_tag = decoded_envelope[66 + decoded_ciphertext_length :]
check("decoded-key-reference", decoded_key_reference.decode("utf-8"), inputs["keyReference"]["text"])
check(
    "decrypted-plaintext",
    AESGCM(key).decrypt(decoded_nonce, decoded_ciphertext + decoded_tag, aad),
    plaintext,
)

nist = read_json("nist-gcm-256-count0.json")
check("nist-file-hash", sha256((FIXTURE_DIRECTORY / "nist-gcm-256-count0.json").read_bytes()), APPROVED_FIXTURE_HASHES["nist-gcm-256-count0.json"])
check("nist-schema-version", nist["schemaVersion"], 1)
check("nist-vector-id", nist["vectorId"], "V003")
check("nist-source", nist["source"], "NIST CAVP gcmEncryptExtIV256.rsp Count 0")
check("nist-profile", nist["profile"], {"keyBits": 256, "ivBits": 96, "plaintextBits": 0, "aadBits": 0, "tagBits": 128})
check("nist-key", nist["keyHex"], "b52c505a37d78eda5dd34f20c22540ea1b58963cf8e5bf8ffa85f9f2492505b4")
check("nist-iv", nist["ivHex"], "516c33929df5a3284ff463d7")
check("nist-tag-reference", nist["tagHex"], "bdc1ac884d332457a1d2664f168c76f0")
nist_result = AESGCM(bytes.fromhex(nist["keyHex"])).encrypt(
    bytes.fromhex(nist["ivHex"]),
    bytes.fromhex(nist["plaintextHex"]),
    bytes.fromhex(nist["aadHex"]),
)
check("nist-ciphertext", nist_result[:-16].hex(), nist["ciphertextHex"])
check("nist-tag", nist_result[-16:].hex(), nist["tagHex"])

ownership = read_json("vector-ownership.json")
check("ownership-file-hash", sha256((FIXTURE_DIRECTORY / "vector-ownership.json").read_bytes()), APPROVED_FIXTURE_HASHES["vector-ownership.json"])
check("ownership-schema-version", ownership["schemaVersion"], 1)
check("ownership-normative-digest", ownership["normativeDigest"], APPROVED_DIGEST)
check("ownership-registry", ownership["normativeRegistry"], "spec-shared-payload-protection-engine.md#15.4")
for assignment in ownership["assignments"]:
    check(
        f"range-bounds-{assignment['first']}",
        isinstance(assignment["first"], int)
        and not isinstance(assignment["first"], bool)
        and isinstance(assignment["last"], int)
        and not isinstance(assignment["last"], bool)
        and 1 <= assignment["first"] <= assignment["last"] <= 138,
        True,
    )
    check(
        f"assignment-state-{assignment['first']}",
        assignment["state"],
        "executed" if assignment["first"] <= 3 else "predecessor-gated",
    )
expanded = [
    vector_id
    for assignment in ownership["assignments"]
    for vector_id in range(assignment["first"], assignment["last"] + 1)
]
check("vector-count", len(expanded), 138)
check("vector-order", expanded, list(range(1, 139)))
executed = [
    f"V{vector_id:03d}"
    for assignment in ownership["assignments"]
    if assignment["state"] == "executed"
    for vector_id in range(assignment["first"], assignment["last"] + 1)
]
check("executed-vectors", executed, ["V001", "V002", "V003"])

fixture_manifest = read_json("manifest.json")
check("manifest-normative-digest", fixture_manifest["normativeDigest"], APPROVED_DIGEST)
manifest_paths = [entry["path"] for entry in fixture_manifest["files"]]
check("manifest-path-uniqueness", len(set(manifest_paths)), len(manifest_paths))
check("manifest-path-allowlist", sorted(manifest_paths), REQUIRED_MANIFEST_PATHS)
for entry in fixture_manifest["files"]:
    resolved_path = (FIXTURE_DIRECTORY / entry["path"]).resolve()
    check(
        f"manifest-path-contained-{entry['path']}",
        resolved_path.is_relative_to(REPOSITORY_ROOT),
        True,
    )
    file_bytes = resolved_path.read_bytes()
    check(f"file-sha256-{entry['path']}", sha256(file_bytes), entry["sha256"])

derived = {
    "manifestHex": manifest.hex(),
    "manifestSha256": sha256(manifest),
    "aadHex": aad.hex(),
    "aadSha256": sha256(aad),
    "ciphertextHex": ciphertext.hex(),
    "tagHex": tag.hex(),
    "envelopeHex": envelope.hex(),
    "envelopeBase64Url": envelope_base64url,
    "envelopeSha256": sha256(envelope),
    "wrapper": wrapper.decode("utf-8"),
    "wrapperSha256": sha256(wrapper),
}
check("manifest-schema-version", fixture_manifest["schemaVersion"], 1)
check("manifest-story", fixture_manifest["story"], "8.2")
check(
    "manifest-executed-vectors",
    fixture_manifest["expected"]["executedVectors"],
    executed,
)
check(
    "manifest-future-vector-count",
    fixture_manifest["expected"]["predecessorGatedVectorCount"],
    135,
)
check(
    "manifest-path-manifest-sha256",
    fixture_manifest["expected"]["manifestSha256"],
    derived["manifestSha256"],
)
check(
    "manifest-aad-sha256",
    fixture_manifest["expected"]["aadSha256"],
    derived["aadSha256"],
)
check(
    "manifest-envelope-sha256",
    fixture_manifest["expected"]["envelopeSha256"],
    derived["envelopeSha256"],
)
check(
    "manifest-wrapper-sha256",
    fixture_manifest["expected"]["wrapperSha256"],
    derived["wrapperSha256"],
)
check("manifest-outcome", fixture_manifest["expected"]["outcome"], "PASS")

if "--emit-derived" in sys.argv:
    print(json.dumps(derived, indent=2))
else:
    print(
        json.dumps(
            {
                "verifier": "python",
                "python": sys.version.split()[0],
                "openssl": ssl.OPENSSL_VERSION,
                "cryptography": cryptography.__version__,
                "normativeDigest": golden["normativeDigest"],
                "executedVectors": executed,
                "futureVectors": 135,
                "envelopeSha256": derived["envelopeSha256"],
                "wrapperSha256": derived["wrapperSha256"],
                "result": "PASS",
            },
            separators=(",", ":"),
        )
    )

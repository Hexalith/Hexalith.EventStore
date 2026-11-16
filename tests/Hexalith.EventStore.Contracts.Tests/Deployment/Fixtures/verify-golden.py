"""Independent stdlib-only calculation of the construction-only v1 and v2 golden vectors."""
import hashlib
import json
from pathlib import Path
import re


def canonical(value, field=""):
    if isinstance(value, dict):
        value = {key: canonical(value[key], key) for key in sorted(value)}
    elif isinstance(value, list):
        value = [canonical(item, field) for item in value]
        if field != "order" and value:
            value = sorted(value, key=(lambda item: item["id"])) if isinstance(value[0], dict) else sorted(value)
    return value


def encode(value):
    # Golden data deliberately stays within ASCII. Match default BCL escaping for
    # the plus sign in the retained artifact version independently of .NET.
    result = json.dumps(canonical(value), ensure_ascii=True, sort_keys=True, separators=(",", ":"))
    for char, replacement in {"+": "\\u002B", "<": "\\u003C", ">": "\\u003E", "&": "\\u0026", "'": "\\u0027"}.items():
        result = result.replace(char, replacement)
    return result.encode("utf-8")


def digest(value):
    return hashlib.sha256(encode(value)).hexdigest()


if __name__ == "__main__":
    directory = Path(__file__).resolve().parent
    for version in (1, 2):
        raw = (directory / f"SyntheticProfile.v{version}.json").read_bytes()
        profile = json.loads(raw)
        expected = json.loads((directory / f"SyntheticDigests.v{version}.json").read_bytes())
        preimage = {"purpose": "platform-infrastructure-profile", "schemaVersion": version,
                    "template": profile["template"], "infrastructure": profile["infrastructure"]}
        assert raw == encode(profile), "noncanonical golden bytes"
        assert (directory / f"SyntheticInfrastructurePreimage.v{version}.json").read_bytes() == encode(preimage)
        assert profile["infrastructureDigest"] == digest(preimage) == expected["infrastructure"]
        assert hashlib.sha256(raw).hexdigest() == expected["resolved"]
    print("Independent Python golden verification: 4 digests and 4 canonical byte vectors passed")

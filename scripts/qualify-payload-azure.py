#!/usr/bin/env python3
"""Run the separately authorized Azure lane without creating owner authority."""

from __future__ import annotations

from datetime import datetime, timedelta, timezone
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import tempfile


ROOT = Path(__file__).resolve().parents[1]
PROJECT = "tests/Hexalith.EventStore.PayloadProtection.AzureKeyVault.LiveTests/Hexalith.EventStore.PayloadProtection.AzureKeyVault.LiveTests.csproj"


def validate_request(environment: dict[str, str], source: str) -> tuple[bytes, bytes]:
    """Reject missing, mismatched, expired or source-unbound inputs before any service command."""
    authority = environment.get("PAYLOAD_AZURE_AUTHORITY_JSON", "").encode("utf-8")
    custody = environment.get("PAYLOAD_AZURE_CUSTODY_JSON", "").encode("utf-8")
    digest = environment.get("QUALIFICATION_AUTHORITY_SHA256", "")
    if (not authority or len(authority) > 16384 or not custody
            or re.fullmatch(r"[0-9a-f]{64}", digest) is None
            or hashlib.sha256(authority).hexdigest() != digest):
        raise ValueError("Exact separately supplied authority and custody receipt are required")
    def closed_pairs(pairs):
        result = {}
        for key, value in pairs:
            if key in result:
                raise ValueError("Duplicate owner-authority field")
            result[key] = value
        return result
    try:
        document = json.loads(authority, object_pairs_hook=closed_pairs)
        expiry = datetime.fromisoformat(document["ExpiresAtUtc"].replace("Z", "+00:00"))
        now = datetime.now(timezone.utc)
        if (re.fullmatch(r"[0-9a-f]{40}", source) is None
                or environment.get("QUALIFICATION_SOURCE_SHA") != source
                or document["SourceSha"] != source or not document["Owner"]
                or expiry.tzinfo is None or not now < expiry <= now + timedelta(days=1)
                or document["CustodyReceiptSha256"] != hashlib.sha256(custody).hexdigest()):
            raise ValueError("Source, expiry or custody binding differs from the exact owner request")
    except (KeyError, TypeError, json.JSONDecodeError) as error:
        raise ValueError("Malformed separately supplied owner authority") from error
    # The unchanged AzureLiveAuthority.Load performs the complete closed-schema,
    # resource, identity, private-network, receipt and fault-window validation
    # before its fixture creates anything or calls Azure.
    return authority, custody


def main() -> int:
    source = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=ROOT, text=True).strip()
    authority, custody = validate_request(os.environ, source)
    with tempfile.TemporaryDirectory(prefix="payload-azure-authority-") as temporary:
        root = Path(temporary)
        root.chmod(0o700)
        policy, receipt = root / "authority.json", root / "custody.json"
        for path, content in ((policy, authority), (receipt, custody)):
            path.write_bytes(content)
            path.chmod(0o600)
        environment = {key: value for key, value in os.environ.items()
                       if key not in ("PAYLOAD_AZURE_AUTHORITY_JSON", "PAYLOAD_AZURE_CUSTODY_JSON")}
        environment.update(HEXALITH_PAYLOAD_AZURE_LIVE="authorized", HEXALITH_PAYLOAD_AZURE_SOURCE_SHA=source,
                           HEXALITH_PAYLOAD_AZURE_AUTHORITY=str(policy), HEXALITH_PAYLOAD_AZURE_CUSTODY_RECEIPT=str(receipt),
                           HEXALITH_PAYLOAD_AZURE_AUTHORITY_SHA256=environment["QUALIFICATION_AUTHORITY_SHA256"])
        daprd = Path.home() / ".dapr/bin/daprd"
        if (hashlib.sha256(daprd.read_bytes()).hexdigest() != "e988a6f53abec017fc9ebcb4f89de9bed11bce005eb49bd8e85e8d3d6adbf488"
                or subprocess.check_output([str(daprd), "--version"], text=True).strip() != "1.18.7"):
            raise ValueError("The designated private runner must retain its pinned Dapr 1.18.7 executable")
        subprocess.run(["docker", "pull", "postgres@sha256:a02db8cac496f15b094798a38254f14d6e00741f709360e5e00bb6668ea31636"],
                       cwd=ROOT, env=environment, check=True)
        for phase, selected, count in (("healthy", "AzureKeyVaultConformanceTests", 11),
                                       ("prepared-faults", "AzurePreparedFaultTests", 1)):
            subprocess.run(["dotnet", "test", "--project", PROJECT, "--no-build", "--no-restore", "--configuration", "Release",
                            "--filter-class", "Hexalith.EventStore.PayloadProtection.AzureKeyVault.LiveTests." + selected,
                            "--minimum-expected-tests", str(count), "--fail-skips", "on", "--results-directory",
                            "TestResults/azure-live-" + phase, "--report-xunit-trx", "-p:UseHexalithProjectReferences=false"],
                           cwd=ROOT, env={**environment, "HEXALITH_PAYLOAD_AZURE_PHASE": phase}, check=True)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
